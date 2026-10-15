"""独立 Qt/OpenGL 预览；描边顶点公式与 TL_RF_Outline.shader 一致。"""
import array
import math
import os
import re

try:
    from PySide6 import QtCore, QtGui, QtWidgets, QtOpenGL, QtOpenGLWidgets
    GLWidget = QtOpenGLWidgets.QOpenGLWidget
    GLProgram, GLShader, GLBuffer = (QtOpenGL.QOpenGLShaderProgram,
                                   QtOpenGL.QOpenGLShader, QtOpenGL.QOpenGLBuffer)
except ImportError:
    from PySide2 import QtCore, QtGui, QtWidgets
    GLWidget = QtWidgets.QOpenGLWidget
    GLProgram, GLShader, GLBuffer = (QtGui.QOpenGLShaderProgram,
                                   QtGui.QOpenGLShader, QtGui.QOpenGLBuffer)

DEFAULTS = dict(width=0.022, adaptive=True, min_dist=1.0, max_dist=10.0,
                near_scale=0.5, far_scale=2.0, color=(0.33725497, 0.11372497, 0.027450997, 1.0))
PROPERTIES = dict(width='_OutlineWidth', adaptive='_OutlineDistAdaptive',
                  min_dist='_OutlineMinDist', max_dist='_OutlineMaxDist',
                  near_scale='_OutlineNearScale', far_scale='_OutlineFarScale')


def read_unity_material(path):
    """直接读取 Unity 文本材质，不需要 Unity 或 YAML 依赖。"""
    with open(path, encoding='utf-8-sig') as file:
        text = file.read()
    result = dict(DEFAULTS)
    for key, prop in PROPERTIES.items():
        match = re.search(r'-\s+' + re.escape(prop) + r':\s*([-+\deE.]+)', text)
        if not match:
            raise ValueError('材质缺少参数：' + prop)
        value = float(match.group(1))
        if not math.isfinite(value):
            raise ValueError('材质参数无效：' + prop)
        result[key] = bool(value) if key == 'adaptive' else value
    match = re.search(r'-\s+_OutlineColor:\s*\{([^}]+)\}', text)
    if not match:
        raise ValueError('材质缺少 _OutlineColor。')
    values = dict(re.findall(r'([rgba]):\s*([-+\deE.]+)', match.group(1)))
    result['color'] = tuple(float(values[key]) for key in 'rgba')
    if not all(math.isfinite(v) for v in result['color']):
        raise ValueError('材质颜色无效。')
    return result


VERTEX_SHADER = '''#version 120
attribute vec3 position;
attribute vec3 surfaceNormal;
attribute vec3 outlineNormal;
attribute float vertexAlpha;
attribute vec3 vertexColor;
uniform mat4 view;
uniform mat4 projection;
uniform int outlinePass;
uniform float outlineWidth;
uniform float adaptive;
uniform float minDist;
uniform float maxDist;
uniform float nearScale;
uniform float farScale;
uniform float respectAlpha;
varying vec3 normalVS;
varying float colorAlpha;
void main() {
    vec4 posVS = view * vec4(position, 1.0);
    normalVS = mat3(view) * surfaceNormal;
    colorAlpha = vertexAlpha;
    if (outlinePass == 1) {
        vec3 n = mat3(view) * outlineNormal;
        float t = clamp((-posVS.z - minDist) / max(maxDist - minDist, 0.001), 0.0, 1.0);
        float width = outlineWidth * mix(1.0, mix(nearScale, farScale, t), adaptive);
        width *= mix(1.0, vertexAlpha, respectAlpha);
        float len = length(n.xy);
        if (len > 0.000001) posVS.xy += n.xy / len * width;
    }
    gl_Position = projection * posVS;
}
'''
FRAGMENT_SHADER = '''#version 120
uniform int outlinePass;
uniform vec4 outlineColor;
uniform int showVertexAlpha;
varying vec3 normalVS;
varying float colorAlpha;
void main() {
    if (outlinePass == 1) gl_FragColor = outlineColor;
    else if (showVertexAlpha == 1) gl_FragColor = vec4(vec3(clamp(colorAlpha, 0.0, 1.0)), 1.0);
    else {
        float light = 0.35 + 0.65 * max(dot(normalize(normalVS), normalize(vec3(-0.4, 0.6, 1.0))), 0.0);
        gl_FragColor = vec4(vec3(0.66, 0.70, 0.74) * light, 1.0);
    }
}
'''


class OutlineView(GLWidget):
    distanceChanged = QtCore.Signal(float)

    def __init__(self, parent=None):
        super().__init__(parent)
        fmt = QtGui.QSurfaceFormat()
        fmt.setVersion(2, 1)
        fmt.setDepthBufferSize(24)
        fmt.setSamples(4)
        self.setFormat(fmt)
        self.setMinimumSize(520, 400)
        self.settings = dict(DEFAULTS)
        self.respect_alpha = True
        self.show_vertex_alpha = False
        self.enabled = True
        self.distance = 3.0
        self.yaw, self.pitch = 35.0, 20.0
        self.fov = 45.0
        self.radius = 1.0
        self.center = QtGui.QVector3D()
        self.count = 0
        self.payload = b''
        self.dirty = False
        self.error = None
        self.program = self.buffer = None
        self.last_mouse = None

    def initializeGL(self):
        try:
            self.program = GLProgram(self)
            if not self.program.addShaderFromSourceCode(GLShader.Vertex, VERTEX_SHADER):
                raise RuntimeError(self.program.log())
            if not self.program.addShaderFromSourceCode(GLShader.Fragment, FRAGMENT_SHADER):
                raise RuntimeError(self.program.log())
            for i, name in enumerate(('position', 'surfaceNormal', 'outlineNormal', 'vertexAlpha', 'vertexColor')):
                self.program.bindAttributeLocation(name.encode('ascii'), i)
            if not self.program.link():
                raise RuntimeError(self.program.log())
            self.buffer = GLBuffer(GLBuffer.VertexBuffer)
            if not self.buffer.create():
                raise RuntimeError('无法创建 OpenGL 顶点缓冲。')
            self.dirty = True
        except Exception as error:
            self.error = str(error)

    def load(self, rows):
        positions = [row[:3] for row in rows]
        lo = [min(p[i] for p in positions) for i in range(3)]
        hi = [max(p[i] for p in positions) for i in range(3)]
        self.center = QtGui.QVector3D(*[(a + b) * 0.5 for a, b in zip(lo, hi)])
        self.radius = max(0.001, math.sqrt(sum((b - a) ** 2 for a, b in zip(lo, hi))) * 0.5)
        self.payload = array.array('f', (v for row in rows for v in row)).tobytes()
        self.count = len(rows)
        self.dirty = True
        self.fit()

    def fit(self):
        self.distance = self.radius / math.sin(math.radians(self.fov * 0.5)) * 1.2
        self.distanceChanged.emit(self.distance)
        self.update()

    def paintGL(self):
        gl = self.context().functions()
        gl.glClearColor(0.13, 0.15, 0.18, 1.0)
        gl.glClear(0x4000 | 0x0100)
        if self.error or not self.count:
            painter = QtGui.QPainter(self)
            painter.setPen(QtGui.QColor('#eeeeee'))
            painter.drawText(self.rect().adjusted(20, 20, -20, -20), QtCore.Qt.TextWordWrap,
                             self.error or '选择模型并点击“读取选中模型”。')
            painter.end()
            return
        if self.dirty:
            self.buffer.bind()
            self.buffer.allocate(self.payload, len(self.payload))
            self.dirty = False
        self.program.bind()
        self.buffer.bind()
        for i, (offset, size) in enumerate(((0, 3), (12, 3), (24, 3), (36, 1), (40, 3))):
            self.program.enableAttributeArray(i)
            self.program.setAttributeBuffer(i, 0x1406, offset, size, 52)
        yaw, pitch = math.radians(self.yaw), math.radians(self.pitch)
        eye = self.center + QtGui.QVector3D(math.sin(yaw) * math.cos(pitch),
                                          math.sin(pitch), math.cos(yaw) * math.cos(pitch)) * self.distance
        view = QtGui.QMatrix4x4()
        view.lookAt(eye, self.center, QtGui.QVector3D(0, 1, 0))
        projection = QtGui.QMatrix4x4()
        projection.perspective(self.fov, max(0.01, self.width() / max(1, self.height())),
                               max(0.0001, min(self.radius, self.distance) * 0.001),
                               self.distance + self.radius * 10.0 + 100.0)
        self.program.setUniformValue(self.program.uniformLocation(b'view'), view)
        self.program.setUniformValue(self.program.uniformLocation(b'projection'), projection)
        for uniform, key in (('outlineWidth', 'width'), ('adaptive', 'adaptive'),
                             ('minDist', 'min_dist'), ('maxDist', 'max_dist'),
                             ('nearScale', 'near_scale'), ('farScale', 'far_scale')):
            gl.glUniform1f(self.program.uniformLocation(uniform.encode('ascii')), float(self.settings[key]))
        gl.glUniform4f(self.program.uniformLocation(b'outlineColor'), *self.settings['color'])
        gl.glUniform1f(self.program.uniformLocation(b'respectAlpha'), float(self.respect_alpha))
        gl.glUniform1i(self.program.uniformLocation(b'showVertexAlpha'), int(self.show_vertex_alpha))
        gl.glEnable(0x0B71)  # GL_DEPTH_TEST
        gl.glDepthFunc(0x0203)  # GL_LEQUAL
        gl.glEnable(0x0B44)  # GL_CULL_FACE
        gl.glFrontFace(0x0901)  # GL_CCW
        gl.glCullFace(0x0405)  # GL_BACK: 中性灰模型
        gl.glUniform1i(self.program.uniformLocation(b'outlinePass'), 0)
        gl.glDrawArrays(0x0004, 0, self.count)
        if self.enabled:
            gl.glCullFace(0x0404)  # GL_FRONT: 与 Unity 相同的背面外扩
            gl.glUniform1i(self.program.uniformLocation(b'outlinePass'), 1)
            gl.glDrawArrays(0x0004, 0, self.count)
        gl.glDisable(0x0B44)
        for i in range(5):
            self.program.disableAttributeArray(i)
        self.buffer.release()
        self.program.release()

    def mousePressEvent(self, event):
        self.last_mouse = event.pos()

    def mouseMoveEvent(self, event):
        if self.last_mouse is not None and event.buttons() & QtCore.Qt.LeftButton:
            delta = event.pos() - self.last_mouse
            self.yaw -= delta.x() * 0.5
            self.pitch = max(-85, min(85, self.pitch + delta.y() * 0.5))
            self.last_mouse = event.pos()
            self.update()

    def mouseReleaseEvent(self, event):
        self.last_mouse = None

    def wheelEvent(self, event):
        self.distance = max(self.radius * 0.02,
                            self.distance * math.exp(-event.angleDelta().y() * 0.001))
        self.distanceChanged.emit(self.distance)
        self.update()


class OutlinePreview(QtWidgets.QDialog):
    def __init__(self, parent, reader, material_path, units_per_meter):
        super().__init__(parent)
        self.setWindowTitle('TL 描边预览 · Unity 参数')
        self.resize(940, 640)
        self.reader = reader
        self.source_nodes = []
        layout = QtWidgets.QVBoxLayout(self)
        row = QtWidgets.QHBoxLayout()
        load = QtWidgets.QPushButton('读取选中模型')
        load.clicked.connect(self.load_selection)
        row.addWidget(load)
        refresh = QtWidgets.QPushButton('刷新模型')
        refresh.clicked.connect(self.refresh)
        row.addWidget(refresh)
        self.mode = QtWidgets.QComboBox()
        self.mode.addItems(['已烘焙法线 UV（无标记时原始法线）', '实时平滑（不改模型）', '原始法线（比较硬边）'])
        self.mode.currentIndexChanged.connect(self.refresh)
        row.addWidget(self.mode, 1)
        layout.addLayout(row)
        body = QtWidgets.QHBoxLayout()
        self.view = OutlineView()
        body.addWidget(self.view, 1)
        form = QtWidgets.QFormLayout()
        self.controls = {}
        self.color = DEFAULTS['color']
        for key, label, low, high, decimals in (
                ('width', '描边宽度', 0, 0.1, 4),
                ('min_dist', '最近距离', 0, 10000, 3),
                ('max_dist', '最远距离', 0, 10000, 3),
                ('near_scale', '近处倍率', 0.1, 3, 3),
                ('far_scale', '远处倍率', 0.1, 5, 3)):
            control = QtWidgets.QDoubleSpinBox()
            control.setRange(low, high)
            control.setDecimals(decimals)
            control.setSingleStep(0.001 if key == 'width' else 0.1)
            control.valueChanged.connect(self.update_settings)
            self.controls[key] = control
            form.addRow(label, control)
        self.adaptive = QtWidgets.QCheckBox('启用距离自适应')
        self.adaptive.toggled.connect(self.update_settings)
        form.addRow(self.adaptive)
        self.color_button = QtWidgets.QPushButton('选择描边颜色')
        self.color_button.clicked.connect(self.choose_color)
        form.addRow(self.color_button)
        self.alpha = QtWidgets.QCheckBox('使用顶点 Alpha 控制宽度')
        self.alpha.setChecked(True)
        self.alpha.toggled.connect(self.update_settings)
        form.addRow(self.alpha)
        self.outline = QtWidgets.QCheckBox('显示描边')
        self.outline.setChecked(True)
        self.outline.toggled.connect(self.update_settings)
        form.addRow(self.outline)
        self.vertex_alpha = QtWidgets.QCheckBox('显示顶点色（A）')
        self.vertex_alpha.setToolTip('仅显示顶点 Alpha 灰度：0 为黑色，1 为白色；不叠加灯光，可与描边同时显示。没有 Alpha 时按 1 显示白色。')
        self.vertex_alpha.toggled.connect(self.update_settings)
        form.addRow(self.vertex_alpha)
        self.distance = QtWidgets.QDoubleSpinBox()
        self.distance.setRange(0.0001, 100000)
        self.distance.setDecimals(4)
        self.distance.valueChanged.connect(self.update_camera)
        self.view.distanceChanged.connect(self.sync_distance)
        form.addRow('观察距离（Unity 单位）', self.distance)
        self.fov = QtWidgets.QDoubleSpinBox()
        self.fov.setRange(5, 120)
        self.fov.setValue(45)
        self.fov.valueChanged.connect(self.update_camera)
        form.addRow('垂直视角 FOV', self.fov)
        self.units = QtWidgets.QDoubleSpinBox()
        self.units.setRange(0.000001, 1000000)
        self.units.setDecimals(6)
        self.units.setValue(units_per_meter)
        self.units.setToolTip('默认读取 Max 的系统单位：1 米对应多少 Max 单位。若 FBX 使用特殊缩放，在此调整。')
        self.units.editingFinished.connect(self.refresh)
        form.addRow('Max 单位 / Unity 单位', self.units)
        fit = QtWidgets.QPushButton('适配模型')
        fit.clicked.connect(self.view.fit)
        form.addRow(fit)
        material = QtWidgets.QPushButton('读取 Unity .mat 参数')
        material.clicked.connect(self.load_material)
        form.addRow(material)
        body.addLayout(form)
        layout.addLayout(body, 1)
        self.status = QtWidgets.QLabel('左键拖动旋转，滚轮拉近/拉远。模型使用中性灰显示，观察轮廓与硬边。')
        self.status.setWordWrap(True)
        layout.addWidget(self.status)
        note = QtWidgets.QLabel('参数只影响预览。显示当前网格快照；修改模型或动画姿势后点击刷新。')
        layout.addWidget(note)
        self.apply_settings(DEFAULTS)
        if os.path.isfile(material_path):
            try:
                self.apply_settings(read_unity_material(material_path))
            except Exception as error:
                self.status.setText('材质读取失败，使用内置参数：' + str(error))
        # 自动读取材质后仍遵循工具启动偏好；手动载入 .mat 时按材质值。
        self.adaptive.setChecked(True)

    def apply_settings(self, settings):
        for key, control in self.controls.items():
            control.setValue(settings[key])
        self.adaptive.setChecked(settings['adaptive'])
        self.color = settings['color']
        self.update_settings()

    def update_settings(self, *args):
        if not hasattr(self, 'outline'):
            return
        self.view.settings = {key: control.value() for key, control in self.controls.items()}
        self.view.settings.update(adaptive=self.adaptive.isChecked(), color=self.color)
        self.view.respect_alpha = self.alpha.isChecked()
        self.view.enabled = self.outline.isChecked()
        self.view.show_vertex_alpha = self.vertex_alpha.isChecked()
        rgb = [max(0, min(255, round(v * 255))) for v in self.color[:3]]
        self.color_button.setStyleSheet('background-color: rgb(%s,%s,%s); color: white;' % tuple(rgb))
        self.view.update()

    def choose_color(self):
        color = QtWidgets.QColorDialog.getColor(QtGui.QColor.fromRgbF(*self.color), self, '描边颜色')
        if color.isValid():
            self.color = (color.redF(), color.greenF(), color.blueF(), color.alphaF())
            self.update_settings()

    def load_material(self):
        path, _ = QtWidgets.QFileDialog.getOpenFileName(self, '选择 Unity 描边材质', '', 'Unity 材质 (*.mat)')
        if path:
            try:
                self.apply_settings(read_unity_material(path))
                self.status.setText('已读取材质：' + os.path.basename(path))
            except Exception as error:
                self.status.setText(str(error))

    def update_camera(self, *args):
        self.view.distance = self.distance.value()
        self.view.fov = self.fov.value()
        self.view.update()

    def sync_distance(self, distance):
        self.distance.blockSignals(True)
        self.distance.setValue(distance)
        self.distance.blockSignals(False)

    def load_selection(self):
        from pymxs import runtime as rt
        self.source_nodes = list(rt.selection)
        self.refresh()

    def refresh(self, *args):
        if not hasattr(self, 'units'):
            return
        try:
            rows, messages = self.reader(self.source_nodes, self.mode.currentIndex(), self.units.value())
            if not rows:
                raise ValueError('请选择要预览的模型。')
            self.view.load(rows)
            self.status.setText('；'.join(messages))
        except Exception as error:
            self.status.setText('预览读取失败：' + str(error))

    def closeEvent(self, event):
        # 只持有快照，没有创建或改动 Max 场景对象。
        self.source_nodes = []
        super().closeEvent(event)
