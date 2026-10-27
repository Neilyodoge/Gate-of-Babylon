#target photoshop
/*
 * TL UI SDF Baker — Photoshop 2020+, Python 3 accelerates large images.
 * TL_UI_SDF_PACKED_V1: body A=128..255 stores antialiased contour coverage; outside A=0..127 stores distance.
 * Compatible with TL/UI/ImageOutlineSDF, default _SDFRange=16.
 */
var TLUISDFBaker = (function () {
    var MAX_PIXELS = 4194304;
    var INF = 1e20;
    var scriptFolder = typeof $ !== "undefined" ? new File($.fileName).parent : null;

    function requireValue(condition, message) {
        if (!condition) { throw new Error(message); }
    }

    function integer(value, fallback, minimum, maximum, label) {
        var number = value === undefined ? fallback : Number(value);
        requireValue(isFinite(number) && number === Math.floor(number) &&
            number >= minimum && number <= maximum,
            label + " 必须是 " + minimum + "–" + maximum + " 之间的整数。");
        return number;
    }

    function byteAt(text, offset) { return text.charCodeAt(offset) & 255; }
    function wordAt(text, offset) {
        return byteAt(text, offset) | (byteAt(text, offset + 1) << 8);
    }

    function decodeTga(binary) {
        requireValue(binary.length >= 18, "TGA 文件头不完整。");
        var kind = byteAt(binary, 2);
        requireValue(byteAt(binary, 1) === 0 && (kind === 2 || kind === 10),
            "只支持无调色板的 RGB / RLE TGA。");
        requireValue(byteAt(binary, 16) === 32, "TGA 缺少 32 位 Alpha 通道。");
        var width = wordAt(binary, 12);
        var height = wordAt(binary, 14);
        var count = width * height;
        requireValue(width > 0 && height > 0 && count <= MAX_PIXELS,
            "图片为空或超过 4,194,304 像素，请缩小后再烘焙。");
        var offset = 18 + byteAt(binary, 0);
        var pixels;
        if (kind === 2) {
            requireValue(binary.length >= offset + count * 4, "TGA 像素数据不完整。");
            pixels = binary.substr(offset, count * 4);
        } else {
            var pieces = [];
            var decoded = 0;
            while (decoded < count) {
                requireValue(offset < binary.length, "TGA RLE 数据不完整。");
                var packet = byteAt(binary, offset++);
                var length = (packet & 127) + 1;
                requireValue(decoded + length <= count, "TGA RLE 超出图片边界。");
                if (packet & 128) {
                    requireValue(offset + 4 <= binary.length, "TGA RLE 像素不完整。");
                    var pixel = binary.substr(offset, 4);
                    offset += 4;
                    var repeated = "";
                    for (var repeat = 0; repeat < length; repeat++) { repeated += pixel; }
                    pieces.push(repeated);
                } else {
                    requireValue(offset + length * 4 <= binary.length, "TGA RLE 像素不完整。");
                    pieces.push(binary.substr(offset, length * 4));
                    offset += length * 4;
                }
                decoded += length;
            }
            pixels = pieces.join("");
        }
        // Normalize to top-left origin, BGRA byte order.
        var descriptor = byteAt(binary, 17);
        if (!(descriptor & 32) || (descriptor & 16)) {
            var rows = [];
            for (var y = 0; y < height; y++) {
                var sourceY = descriptor & 32 ? y : height - 1 - y;
                var row = pixels.substr(sourceY * width * 4, width * 4);
                if (descriptor & 16) {
                    var reversed = [];
                    for (var x = width - 1; x >= 0; x--) {
                        reversed.push(row.substr(x * 4, 4));
                    }
                    row = reversed.join("");
                }
                rows.push(row);
            }
            pixels = rows.join("");
        }
        return { width: width, height: height, pixels: pixels };
    }

    function encodeTga(image) {
        requireValue(image.width > 0 && image.width <= 65535 && image.height > 0 &&
            image.height <= 65535 && image.pixels.length === image.width * image.height * 4,
            "TGA 尺寸或像素数据无效。");
        var header = [0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            image.width & 255, image.width >> 8,
            image.height & 255, image.height >> 8, 32, 40];
        var text = "";
        for (var i = 0; i < header.length; i++) { text += String.fromCharCode(header[i]); }
        return text + image.pixels;
    }

    // Exact squared Euclidean distance transform using a lower envelope of parabolas.
    function transform1D(input, length, output, nearest, vertices, boundaries) {
        var k = 0;
        vertices[0] = 0;
        boundaries[0] = -Infinity;
        boundaries[1] = Infinity;
        for (var q = 1; q < length; q++) {
            var intersection;
            do {
                var p = vertices[k];
                intersection = ((input[q] + q * q) - (input[p] + p * p)) / (2 * (q - p));
                if (intersection > boundaries[k]) { break; }
                k--;
            } while (k >= 0);
            k++;
            vertices[k] = q;
            boundaries[k] = intersection;
            boundaries[k + 1] = Infinity;
        }
        k = 0;
        for (q = 0; q < length; q++) {
            while (boundaries[k + 1] < q) { k++; }
            var delta = q - vertices[k];
            output[q] = delta * delta + input[vertices[k]];
            nearest[q] = vertices[k];
        }
    }

    function bakePixels(image, options) {
        options = options || {};
        var range = integer(options.range, 16, 1, 64, "距离范围");
        var threshold = integer(options.threshold, 128, 1, 255, "透明阈值");
        var alphaEdgeLow = Math.max(0, threshold - 32);
        var alphaEdgeHigh = Math.min(255, threshold + 32);
        var width = image.width;
        var height = image.height;
        var count = width * height;
        requireValue(width > 0 && height > 0 && count <= MAX_PIXELS &&
            image.pixels.length === count * 4, "图片尺寸或像素数据无效。");
        // ExtendScript uses a legacy string implementation: repeated random access
        // to a multi-megabyte binary string is very slow. Keep access within rows.
        var pixelRows = [];
        var y;
        for (y = 0; y < height; y++) {
            pixelRows[y] = image.pixels.substr(y * width * 4, width * 4);
        }
        var started = new Date().getTime();
        var maxComputeMs = integer(options.maxComputeMs, 0, 0, 600000, "计算时限");
        function checkpoint(fraction) {
            // Yield between blocks so Photoshop can process cancellation/UI events.
            if (typeof $ !== "undefined" && $.sleep) { $.sleep(1); }
            requireValue(!maxComputeMs || new Date().getTime() - started <= maxComputeMs,
                "计算超过 " + Math.round(maxComputeMs / 1000) + " 秒，已停止烘焙。请缩小图片后重试。");
            if (options.progress) { options.progress(fraction); }
        }
        var insideCount = 0;
        var fractionalCount = 0;
        var touchesEdge = false;
        var i;
        for (i = 0; i < count; i++) {
            var a = byteAt(pixelRows[Math.floor(i / width)], (i % width) * 4 + 3);
            if (a > 0 && a < 255) { fractionalCount++; }
            if (a >= threshold) {
                insideCount++;
                var row = Math.floor(i / width);
                var column = i % width;
                if (row === 0 || row === height - 1 || column === 0 || column === width - 1) {
                    touchesEdge = true;
                }
            }
            if (i % 4096 === 4095) { checkpoint(0.05 * i / count); }
        }
        requireValue(insideCount > 0, "透明阈值以上没有像素，无法生成轮廓。");
        var distances = new Array(count);
        var labels = new Array(count);
        var input = [], output = [], nearest = [], vertices = [], boundaries = [], rowLabels = [];
        var x;
        for (x = 0; x < width; x++) {
            for (y = 0; y < height; y++) {
                input[y] = byteAt(pixelRows[y], x * 4 + 3) >= threshold ? 0 : INF;
            }
            transform1D(input, height, output, nearest, vertices, boundaries);
            for (y = 0; y < height; y++) {
                distances[y * width + x] = output[y];
                labels[y * width + x] = nearest[y] * width + x;
            }
            if (x % 32 === 0) { checkpoint(0.05 + 0.3 * x / width); }
        }
        for (y = 0; y < height; y++) {
            for (x = 0; x < width; x++) {
                input[x] = distances[y * width + x];
                rowLabels[x] = labels[y * width + x];
            }
            transform1D(input, width, output, nearest, vertices, boundaries);
            for (x = 0; x < width; x++) {
                distances[y * width + x] = output[x];
                labels[y * width + x] = rowLabels[nearest[x]];
            }
            if (y % 32 === 0) { checkpoint(0.35 + 0.35 * y / height); }
        }
        var chunks = [];
        var chunk = "";
        var preservedAlphaCount = 0;
        for (i = 0; i < count; i++) {
            var rowIndex = Math.floor(i / width);
            var columnIndex = i % width;
            var originalAlpha = byteAt(pixelRows[rowIndex], columnIndex * 4 + 3);
            var distance = Math.sqrt(Math.max(0, distances[i]));
            // Reconstruct a narrow smooth contour from Alpha instead of admitting faint shadow pixels.
            // RGB stays untouched; only coverage within two texels of the contour is retained.
            // The shader decodes body coverage as saturate(2 * packedAlpha / 255 - 1).
            var edgeT = Math.min(1, Math.max(0, (originalAlpha - alphaEdgeLow) / (alphaEdgeHigh - alphaEdgeLow)));
            var coverageByte = Math.round(255 * edgeT * edgeT * (3 - 2 * edgeT));
            var keepCoverage = coverageByte > 0 && distance <= 2;
            if (keepCoverage && originalAlpha < 255) { preservedAlphaCount++; }
            var alpha = keepCoverage ? Math.round((255 + coverageByte) * 0.5) : Math.min(127, Math.max(0,
                Math.round(127.5 * Math.max(0, 1 - distance / range))));
            // Preserve the input RGB bytes; only Alpha receives the distance encoding.
            chunk += pixelRows[rowIndex].substr(columnIndex * 4, 3) + String.fromCharCode(alpha);
            if (i % 4096 === 4095) { chunks.push(chunk); chunk = ""; }
            if (i % 4096 === 4095) { checkpoint(0.7 + 0.3 * i / count); }
        }
        chunks.push(chunk);
        checkpoint(1);
        return {
            width: width, height: height, pixels: chunks.join(""),
            range: range, threshold: threshold, insideCount: insideCount,
            fractionalCount: fractionalCount, touchesEdge: touchesEdge,
            preservedAlphaCount: preservedAlphaCount,
            encoding: "TL_UI_SDF_PACKED_V1"
        };
    }

    function readBinary(file) {
        file.encoding = "BINARY";
        requireValue(file.open("r"), "无法读取临时文件：" + file.fsName);
        try { return file.read(); } finally { file.close(); }
    }

    function writeBinary(file, binary) {
        file.encoding = "BINARY";
        requireValue(file.open("w"), "无法写入临时文件：" + file.fsName);
        try { requireValue(file.write(binary), "临时文件写入失败。"); } finally { file.close(); }
    }

    function findPython(explicitPath) {
        if (explicitPath) {
            var explicit = new File(explicitPath);
            requireValue(explicit.exists, "指定的 Python 不存在。");
            return explicit;
        }
        var localRoot = new Folder($.getenv("LOCALAPPDATA") + "/Programs/Python");
        var versions = localRoot.exists ? localRoot.getFiles("Python*") : [];
        versions.sort(function (a, b) { return a.name < b.name ? 1 : -1; });
        for (var i = 0; i < versions.length; i++) {
            if (versions[i] instanceof Folder) {
                var candidate = new File(versions[i].fsName + "/python.exe");
                if (candidate.exists) { return candidate; }
            }
        }
        var paths = $.getenv("PATH").split(";");
        for (i = 0; i < paths.length; i++) {
            // Skip Microsoft Store execution aliases, which can open the Store.
            if (/WindowsApps/i.test(paths[i])) { continue; }
            candidate = new File(paths[i] + "/python.exe");
            if (candidate.exists) { return candidate; }
        }
        return null;
    }

    function vbsLiteral(value) {
        var result = '"';
        for (var i = 0; i < value.length; i++) {
            var code = value.charCodeAt(i);
            if (code < 32 || code > 126) { result += '" & ChrW(' + code + ') & "'; }
            else { result += value.charAt(i) === '"' ? '""' : value.charAt(i); }
        }
        return result + '"';
    }

    function readWorkerJson(file) {
        // These ASCII JSON files are authored solely by the local bundled worker.
        return eval("(" + readBinary(file) + ")");
    }

    function runPythonWorker(python, helper, input, output, temp, options) {
        var result = new File(temp.fsName + "/result.json");
        var progress = new File(temp.fsName + "/progress.json");
        var cancel = new File(temp.fsName + "/cancel.txt");
        var failed = new File(temp.fsName + "/runner_error.txt");
        var launcher = new File(temp.fsName + "/run.vbs");
        var command = '"' + python.fsName + '" -B "' + helper.fsName + '" "' +
            input.fsName + '" "' + output.fsName + '" "' + result.fsName + '" "' +
            progress.fsName + '" "' + cancel.fsName + '" ' + options.range + " " +
            options.threshold + " 0";
        // ASCII VBScript launches Python without a console window. ChrW handles
        // Chinese/user-specific paths without depending on the system code page.
        writeBinary(launcher, "On Error Resume Next\r\nDim sh, code, fso, log\r\n" +
            "Set sh = CreateObject(\"WScript.Shell\")\r\ncode = sh.Run(" + vbsLiteral(command) + ", 0, True)\r\n" +
            "If Err.Number <> 0 Or code <> 0 Then\r\n" +
            "Set fso = CreateObject(\"Scripting.FileSystemObject\")\r\n" +
            "Set log = fso.CreateTextFile(" + vbsLiteral(failed.fsName) + ", True, False)\r\n" +
            "log.Write \"Python worker could not finish. Check Python installation.\"\r\nlog.Close\r\nEnd If\r\n");
        requireValue(launcher.execute(), "无法启动 Python 加速程序。");
        var started = new Date().getTime();
        var limit = integer(options.maxComputeMs, 0, 0, 600000, "计算时限");
        var lastFraction = 0;
        try {
            while (!result.exists) {
                requireValue(!failed.exists, "Python 加速程序启动失败，请检查 Python 安装。");
                requireValue(!limit || new Date().getTime() - started <= limit,
                    "距离计算超过时限，已取消。请缩小图片后重试。");
                if (options.progress) {
                    try {
                        if (progress.exists) {
                            var fraction = readWorkerJson(progress).fraction;
                            if (typeof fraction === "number" && isFinite(fraction) &&
                                fraction >= lastFraction && fraction <= 1) { lastFraction = fraction; }
                        }
                    } catch (busyProgress) {
                        // A transient status-file read must not abort baking.
                    }
                    options.progress(0.15 + lastFraction * 0.65, "计算外侧欧氏距离并编码 Alpha");
                }
                $.sleep(100);
            }
            var data = readWorkerJson(result);
            requireValue(data.ok, "SDF 计算失败：" + data.error);
            requireValue(output.exists && data.metadata.encoding === "TL_UI_SDF_PACKED_V1",
                "Python 返回的 SDF 数据无效。");
            return data.metadata;
        } catch (error) {
            writeBinary(cancel, "cancel");
            // The worker acknowledges cancellation at the next distance block.
            for (var i = 0; i < 50 && !result.exists && !failed.exists; i++) { $.sleep(100); }
            throw error;
        }
    }

    function removeExtraChannels(document) {
        for (var i = document.channels.length - 1; i >= 0; i--) {
            if (document.channels[i].kind !== ChannelType.COMPONENT) { document.channels[i].remove(); }
        }
        document.activeChannels = document.componentChannels;
    }

    function selectLayerTransparency() {
        var descriptor = new ActionDescriptor();
        var target = new ActionReference();
        target.putProperty(charIDToTypeID("Chnl"), charIDToTypeID("fsel"));
        descriptor.putReference(charIDToTypeID("null"), target);
        var source = new ActionReference();
        source.putEnumerated(charIDToTypeID("Chnl"), charIDToTypeID("Chnl"), charIDToTypeID("Trsp"));
        descriptor.putReference(charIDToTypeID("T   "), source);
        executeAction(charIDToTypeID("setd"), descriptor, DialogModes.NO);
    }

    function exportSourceTga(document, file) {
        removeExtraChannels(document);
        // An empty helper makes Merge Visible available even for a single masked layer.
        document.artLayers.add().name = "__TL_SDF_MERGE_HELPER";
        document.mergeVisibleLayers();
        var bounds = document.activeLayer.bounds;
        requireValue(bounds[2].as("px") > bounds[0].as("px") &&
            bounds[3].as("px") > bounds[1].as("px"), "可见图层全部透明，无法生成轮廓。");
        var alpha = document.channels.add();
        alpha.name = "__TL_SDF_ALPHA";
        document.activeChannels = document.componentChannels;
        selectLayerTransparency();
        document.selection.store(alpha, SelectionType.REPLACE);
        document.selection.deselect();
        document.activeChannels = document.componentChannels;
        // Move pixel transparency to a mask, then discard that mask. This keeps
        // straight RGB intact instead of matting semitransparent pixels on white.
        // "transparency" is a distinct string ID from the Trsp channel enum in PS 2020.
        var makeMask = new ActionDescriptor();
        makeMask.putClass(stringIDToTypeID("new"), stringIDToTypeID("channel"));
        var maskAt = new ActionReference();
        maskAt.putEnumerated(stringIDToTypeID("channel"), stringIDToTypeID("channel"), stringIDToTypeID("mask"));
        makeMask.putReference(stringIDToTypeID("at"), maskAt);
        makeMask.putEnumerated(stringIDToTypeID("using"), stringIDToTypeID("userMaskEnabled"), stringIDToTypeID("transparency"));
        executeAction(stringIDToTypeID("make"), makeMask, DialogModes.NO);
        var discardMask = new ActionDescriptor();
        var mask = new ActionReference();
        mask.putEnumerated(charIDToTypeID("Chnl"), charIDToTypeID("Chnl"), charIDToTypeID("Msk "));
        discardMask.putReference(charIDToTypeID("null"), mask);
        discardMask.putBoolean(charIDToTypeID("Aply"), false);
        executeAction(charIDToTypeID("Dlt "), discardMask, DialogModes.NO);
        document.flatten();
        var options = new TargaSaveOptions();
        options.alphaChannels = true;
        options.resolution = TargaBitsPerPixels.THIRTYTWO;
        options.rleCompression = false;
        document.saveAs(file, options, true, Extension.LOWERCASE);
    }

    function alphaChannelToTransparency(document) {
        var alpha = null;
        for (var i = 0; i < document.channels.length; i++) {
            if (document.channels[i].kind !== ChannelType.COMPONENT) { alpha = document.channels[i]; break; }
        }
        requireValue(alpha !== null, "导入的 TGA 没有 Alpha 通道。");
        document.activeChannels = document.componentChannels;
        if (document.activeLayer.isBackgroundLayer) { document.activeLayer.isBackgroundLayer = false; }
        document.selection.load(alpha, SelectionType.REPLACE);
        var descriptor = new ActionDescriptor();
        descriptor.putClass(charIDToTypeID("Nw  "), charIDToTypeID("Chnl"));
        var at = new ActionReference();
        at.putEnumerated(charIDToTypeID("Chnl"), charIDToTypeID("Chnl"), charIDToTypeID("Msk "));
        descriptor.putReference(charIDToTypeID("At  "), at);
        descriptor.putEnumerated(charIDToTypeID("Usng"), charIDToTypeID("UsrM"), charIDToTypeID("RvlS"));
        executeAction(charIDToTypeID("Mk  "), descriptor, DialogModes.NO);
        document.selection.deselect();
        var apply = new ActionDescriptor();
        var mask = new ActionReference();
        mask.putEnumerated(charIDToTypeID("Chnl"), charIDToTypeID("Chnl"), charIDToTypeID("Msk "));
        apply.putReference(charIDToTypeID("null"), mask);
        apply.putBoolean(charIDToTypeID("Aply"), true);
        executeAction(charIDToTypeID("Dlt "), apply, DialogModes.NO);
        removeExtraChannels(document);
    }

    function bake(document, options) {
        options = options || {};
        requireValue(document && document.mode === DocumentMode.RGB &&
            document.bitsPerChannel === BitsPerChannelType.EIGHT,
            "请使用 8 位 RGB 文档；源文档不会被自动转换。");
        requireValue(!(document.layers.length === 1 && document.layers[0].name === "TL_UI_SDF_PACKED_V1"),
            "当前文档是已烘焙的 SDF 结果，A 通道已是距离数据。请切换回原始图片再生成。");
        var range = integer(options.range, 16, 1, 64, "距离范围");
        var padding = integer(options.padding, 16, 0, 256, "额外留边");
        var threshold = integer(options.threshold, 128, 1, 255, "透明阈值");
        var width = document.width.as("px") + padding * 2;
        var height = document.height.as("px") + padding * 2;
        requireValue(width * height <= MAX_PIXELS, "包含留边后的图片超过 4,194,304 像素。");
        var temp = new Folder(Folder.temp.fsName + "/TL_UI_SDF_" + new Date().getTime() + "_" + Math.floor(Math.random() * 1e8));
        requireValue(temp.create(), "无法创建临时目录。");
        var inputTga = new File(temp.fsName + "/source.tga");
        var bakedTga = new File(temp.fsName + "/baked.tga");
        var work = null;
        var resultDocument = null;
        var carrierDocument = null;
        var previousDialogs = app.displayDialogs;
        var previousUnits = app.preferences.rulerUnits;
        var succeeded = false;
        try {
            app.displayDialogs = DialogModes.NO;
            app.preferences.rulerUnits = Units.PIXELS;
            work = document.duplicate("__TL_SDF_WORK", false);
            app.activeDocument = work;
            try { work.backgroundLayer.isBackgroundLayer = false; } catch (noBackground) {}
            if (padding > 0) {
                work.resizeCanvas(UnitValue(width, "px"), UnitValue(height, "px"), AnchorPosition.MIDDLECENTER);
            }
            if (options.progress) { options.progress(0.05, "读取可见图层的透明度"); }
            exportSourceTga(work, inputTga);
            var python = options.backend === "extendscript" ? null : findPython(options.pythonExecutable);
            var helper = scriptFolder ? new File(scriptFolder.fsName + "/tl_ui_sdf_core.py") : null;
            var usePython = python && helper && helper.exists;
            requireValue(options.backend !== "python" || usePython, "Python 加速需要 Python 3 和同目录的 tl_ui_sdf_core.py。");
            var baked;
            var computeBackend = usePython ? "python" : "extendscript";
            if (usePython) {
                baked = runPythonWorker(python, helper, inputTga, bakedTga, temp, {
                    range: range, threshold: threshold,
                    maxComputeMs: options.maxComputeMs, progress: options.progress
                });
            } else {
                var image = decodeTga(readBinary(inputTga));
                baked = bakePixels(image, {
                    range: range, threshold: threshold,
                    maxComputeMs: options.maxComputeMs,
                    progress: options.progress ? function (fraction) {
                        options.progress(0.15 + fraction * 0.65, "计算外侧欧氏距离并编码 Alpha");
                    } : null
                });
                writeBinary(bakedTga, encodeTga(baked));
            }
            if (options.progress) { options.progress(0.85, "在 PS 中生成 SDF 结果"); }
            carrierDocument = app.open(bakedTga);
            alphaChannelToTransparency(carrierDocument);
            // Detach from the temporary TGA, so normal Photoshop saving starts
            // from a new unsaved document rather than a deleted temporary path.
            var resultName = document.name.replace(/\.[^.]+$/, "") + "_SDF";
            resultDocument = carrierDocument.duplicate(resultName, false);
            app.activeDocument = resultDocument;
            resultDocument.activeLayer.name = "TL_UI_SDF_PACKED_V1";
            carrierDocument.close(SaveOptions.DONOTSAVECHANGES);
            carrierDocument = null;
            var resultId = resultDocument.id;
            succeeded = true;
            if (options.progress) { options.progress(1, "烘焙完成"); }
            return {
                documentName: resultDocument.name, width: width, height: height,
                range: range, padding: padding, threshold: threshold,
                insideCount: baked.insideCount, binarizedPixels: baked.fractionalCount - baked.preservedAlphaCount,
                preservedAlphaPixels: baked.preservedAlphaCount,
                touchesEdge: baked.touchesEdge, encoding: baked.encoding,
                computeBackend: computeBackend,
                outputDocumentId: resultId
            };
        } finally {
            if (work) { try { work.close(SaveOptions.DONOTSAVECHANGES); } catch (ignoredWork) {} }
            if (!succeeded && resultDocument) {
                try { resultDocument.close(SaveOptions.DONOTSAVECHANGES); } catch (ignoredResult) {}
            }
            if (carrierDocument) {
                try { carrierDocument.close(SaveOptions.DONOTSAVECHANGES); } catch (ignoredCarrier) {}
            }
            try { if (inputTga.exists) { inputTga.remove(); } } catch (ignoredInput) {}
            try { if (bakedTga.exists) { bakedTga.remove(); } } catch (ignoredBaked) {}
            try {
                var leftovers = temp.getFiles();
                for (var i = 0; i < leftovers.length; i++) {
                    if (leftovers[i] instanceof File) { leftovers[i].remove(); }
                }
            } catch (ignoredLeftovers) {}
            try { temp.remove(); } catch (ignoredFolder) {}
            app.displayDialogs = previousDialogs;
            app.preferences.rulerUnits = previousUnits;
            try { app.activeDocument = succeeded && resultDocument ? resultDocument : document; } catch (ignoredActive) {}
        }
    }

    function showDialog() {
        if (!app.documents.length) { alert("请先打开要烘焙的 8 位 RGB 图片或 PSD。"); return; }
        var document = app.activeDocument;
        var window = new Window("dialog", "TL UI SDF 烘焙工具");
        window.orientation = "column";
        window.alignChildren = "fill";
        window.add("statictext", undefined, document.name + "   " + document.width.as("px") + " × " + document.height.as("px"));
        window.add("statictext", undefined, "生成新的 SDF 结果文档；原始文档保持原样。");
        function field(label, value) {
            var group = window.add("group");
            group.add("statictext", undefined, label).preferredSize.width = 185;
            var input = group.add("edittext", undefined, String(value));
            input.characters = 9;
            return input;
        }
        var range = field("距离范围 / 贴图像素", 16);
        var threshold = field("描边轮廓阈值 / 1–255", 128);
        var padding = field("额外透明留边 / 像素", 16);
        window.add("statictext", undefined, "默认四周各留 16 像素，只扩大结果画布；原图尺寸保持不变。");
        window.add("statictext", undefined, "现有 Unity SDF 材质使用范围 16；改范围时 Shader 也需匹配。");
        window.add("statictext", undefined, "保留输入图像 RGB，仅改写 A 通道；不扩展透明区颜色。");
        window.add("statictext", undefined, "仅对轮廓 Alpha 做窄范围平滑；淡阴影不混入描边。");
        window.add("statictext", undefined, "A 通道已是距离数据，请用 TL/UI/ImageOutlineSDF 显示。");
        var actions = window.add("group");
        actions.alignment = "right";
        actions.add("button", undefined, "取消", { name: "cancel" });
        var run = actions.add("button", undefined, "生成 SDF", { name: "ok" });
        var settings = null;
        run.onClick = function () {
            try {
                settings = {
                    range: integer(range.text, 16, 1, 64, "距离范围"),
                    threshold: integer(threshold.text, 128, 1, 255, "透明阈值"),
                    padding: integer(padding.text, 16, 0, 256, "额外留边")
                };
                window.close(1);
            } catch (error) { alert(error.message); }
        };
        if (window.show() !== 1 || !settings) { return; }
        var progress = new Window("palette", "正在烘焙 SDF");
        var status = progress.add("statictext", undefined, "准备中…");
        status.preferredSize.width = 370;
        var bar = progress.add("progressbar", undefined, 0, 100);
        bar.preferredSize.width = 370;
        var cancelled = false;
        var cancel = progress.add("button", undefined, "取消烘焙");
        cancel.onClick = function () { cancelled = true; };
        progress.show();
        settings.progress = function (fraction, label) {
            if (cancelled) { throw new Error("已取消烘焙，源文档保持原样。"); }
            bar.value = fraction * 100;
            status.text = label;
            progress.update();
            if (cancelled) { throw new Error("已取消烘焙，源文档保持原样。"); }
        };
        try {
            var result = bake(document, settings);
            progress.close();
            alert("SDF 生成完成：\n" + result.documentName + "\n" + result.width + " × " + result.height +
                "，距离范围 " + result.range + " 像素。\n使用 TL_UI_ImageOutlineSDF 材质。" +
                "\n可按你的 PS 流程继续处理或保存。" +
                (result.touchesEdge ? "\n主体触及画布边缘，描边可能被截断；可增加留边（只扩大结果）。" : ""));
        } catch (error) {
            progress.close();
            alert("SDF 烘焙失败：\n" + error.message + (error.line ? "\n脚本行号：" + error.line : ""));
        }
    }

    return { bake: bake, bakePixels: bakePixels, decodeTga: decodeTga, encodeTga: encodeTga,
        showDialog: showDialog, encoding: "TL_UI_SDF_PACKED_V1", maxPixels: MAX_PIXELS };
}());

if (typeof module !== "undefined" && module.exports) { module.exports = TLUISDFBaker; }
if (typeof $ !== "undefined" && $.global) {
    $.global.TLUISDFBaker = TLUISDFBaker;
    if (!$.global.TL_SDF_NO_AUTO_RUN) { TLUISDFBaker.showDialog(); }
}
