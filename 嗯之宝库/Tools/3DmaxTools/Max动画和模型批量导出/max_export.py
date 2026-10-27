"""The Max-only export engine, called by max_worker.py in the tool-owned Max."""
from pathlib import Path
import tempfile
import batch_core as core


class MaxExporter:
    def __init__(self, runtime):
        self.rt = runtime
        self.rt.pluginManager.loadClass(self.rt.FBXEXP)

    def scene_path(self):
        return str(Path(str(self.rt.maxFilePath)) / str(self.rt.maxFileName))

    def rig_nodes(self, root_name):
        matches = [n for n in self.rt.objects if str(n.name).casefold() == str(root_name).casefold()]
        if len(matches) != 1:
            raise ValueError('需要唯一的 {} 节点（不区分大小写），实际找到 {} 个'.format(root_name, len(matches)))
        nodes, seen = [matches[0]], {int(matches[0].handle)}
        for node in nodes:
            for child in node.children:
                if int(child.handle) not in seen:
                    nodes.append(child)
                    seen.add(int(child.handle))
        return nodes

    def is_bone(self, node):
        cls = str(self.rt.classOf(node.baseObject)).lower()
        if cls in ('bonegeometry', 'biped_object', 'catbone', 'catparent'):
            return True
        try:
            return bool(node.boneEnable)
        except Exception:
            return False

    def is_mesh(self, node):
        return (self.rt.superClassOf(node) == self.rt.GeometryClass
                and not self.is_helper(node) and not self.is_bone(node))

    def is_helper(self, node):
        return (self.rt.superClassOf(node) == self.rt.helper
                or self.rt.superClassOf(node.baseObject) == self.rt.helper)

    def is_rig_node(self, node):
        return self.is_bone(node) and not self.is_helper(node)

    def select_export_nodes(self, kind, root_name):
        hierarchy = self.rig_nodes(root_name)
        root = hierarchy[0]
        if not (self.is_bone(root) or self.is_helper(root)):
            raise ValueError('Root 必须是骨骼或辅助节点，不能是模型网格。')
        # Root is the one explicit exception, even when it is a Point/Dummy.
        rig = [root] + [n for n in hierarchy[1:] if self.is_rig_node(n)]
        allowed = {int(n.handle) for n in rig}
        # Helper parents are bridged below; reject mesh/shape nodes in the rig.
        for node in rig:
            parent = node.parent
            if (parent is not None and parent in hierarchy and int(parent.handle) not in allowed
                    and not self.is_helper(parent)):
                raise ValueError('骨骼层级中间包含非骨骼节点：' + str(parent.name))
        if kind == 'animation':
            nodes = rig
        else:
            meshes = [n for n in self.rt.objects if self.is_mesh(n)]
            skinned = [n for n in meshes if any(str(self.rt.classOf(m)).lower() in ('skin', 'physique')
                                                for m in n.modifiers)]
            if not skinned:
                raise ValueError('角色本体没有找到带 Skin / Physique 的模型。')
            nodes = rig + meshes
            # FBX also exports skin influence nodes even if they are unselected.
            # Do not silently discard weights when a helper is used as a bone.
            for mesh in meshes:
                for modifier in mesh.modifiers:
                    if str(self.rt.classOf(modifier)).lower() not in ('skin', 'physique'):
                        continue
                    for ref in self.rt.refs.dependsOn(modifier):
                        if not self.rt.isValidNode(ref) or int(ref.handle) in allowed:
                            continue
                        parent = ref
                        while parent is not None and int(parent.handle) not in allowed:
                            if self.is_helper(parent):
                                raise ValueError('蒙皮依赖辅助对象 {}（模型 {}）；为避免丢失蒙皮，本文件未导出。请先调整蒙皮，使其仅依赖 Root 和骨骼。'.format(parent.name, mesh.name))
                            parent = parent.parent
        self.rt.select(self.rt.Array(*nodes))
        return nodes

    def bridge_excluded_parents(self, nodes, kind, config):
        """Keep evaluated poses while omitting intermediate helper ancestors.

        Work only in the loaded batch scene. Helpers remain alive as animation
        drivers, but no exported node can have an excluded helper as a parent.
        Sampling precedes every controller/hierarchy edit to preserve dependencies.
        """
        kept = {int(node.handle) for node in nodes}
        changes = []
        for node in nodes:
            parent = node.parent
            if parent is None or int(parent.handle) in kept:
                continue
            while parent is not None and int(parent.handle) not in kept:
                parent = parent.parent
            changes.append((node, parent))
        if not changes:
            return
        def depth(change):
            level, parent = 0, change[0].parent
            while parent is not None:
                level += 1
                parent = parent.parent
            return level
        changes.sort(key=depth)  # Assign parents before children at every sample.
        import pymxs
        if kind == 'model':
            frames = [int(config.get('model_frame', 0))]
        elif self.rt.FBXExporterGetParam('BakeAnimation'):
            frames = range(int(self.rt.FBXExporterGetParam('BakeFrameStart')),
                           int(self.rt.FBXExporterGetParam('BakeFrameEnd')) + 1)
        else:
            frames = range(int(self.rt.animationRange.start.frame), int(self.rt.animationRange.end.frame) + 1)
        if not frames:
            raise ValueError('动画范围无效，无法保留辅助节点驱动的动作。')
        samples = []
        for frame in frames:
            with pymxs.attime(frame):
                samples.append([self.rt.copy(node.transform) for node, _parent in changes])
        with pymxs.animate(False):
            for node, parent in changes:
                try:
                    node.controller = self.rt.PRS()
                    node.parent = parent
                except Exception as exc:
                    raise ValueError('无法跳过 {} 的辅助父节点并保留姿势：{}'.format(node.name, exc)) from exc
        with pymxs.animate(kind == 'animation'):
            for frame, transforms in zip(frames, samples):
                with pymxs.attime(frame):
                    for (node, _parent), transform in zip(changes, transforms):
                        node.transform = transform
        # Guard unusual/protected controllers rather than publishing broken motion.
        for frame, transforms in zip(frames, samples):
            with pymxs.attime(frame):
                for (node, _parent), transform in zip(changes, transforms):
                    actual = node.transform
                    if any(self.rt.distance(actual[row], transform[row]) > 0.001 for row in range(4)):
                        raise ValueError('跳过辅助节点后 {} 的姿势未能保持一致，本文件未导出。'.format(node.name))

    def load_preset(self, preset, kind, scene_range):
        core.validate_preset(preset)
        for command, args in [('ResetExport', ()), ('LoadExportPresetFile', (str(preset),))]:
            result = self.rt.FBXExporterSetParam(command, *args)
            if result is False or str(result).lower() in ('unsupplied', 'undefined'):
                raise RuntimeError('FBX 预设操作失败：' + command)
        expected_animation = kind == 'animation'
        if bool(self.rt.FBXExporterGetParam('Animation')) != expected_animation:
            raise ValueError(('动作预设必须开启动画' if expected_animation else '蒙皮预设必须关闭动画') + '，请在 Max 中修正并保存预设。')
        if kind == 'model':
            if not self.rt.FBXExporterGetParam('Skin') or self.rt.FBXExporterGetParam('ExportAnimationOnly'):
                raise ValueError('蒙皮预设必须包含 Skin，并关闭 Animation Only。')
        if kind == 'animation' and scene_range and self.rt.FBXExporterGetParam('BakeAnimation'):
            for key, value in [('BakeFrameStart', int(self.rt.animationRange.start.frame)),
                               ('BakeFrameEnd', int(self.rt.animationRange.end.frame))]:
                self.rt.FBXExporterSetParam(key, value)
                if self.rt.FBXExporterGetParam(key) != value:
                    raise RuntimeError('无法设置本文件的烘焙范围：' + key)
        self.rt.FBXExporterSetParam('ShowWarnings', False)

    def export(self, job, config):
        if job.get('issue'):
            raise ValueError(job['issue'])
        target = Path(job['target'])
        if target.exists():
            return 'skipped', 'FBX 已存在，未覆盖'
        if not self.rt.loadMaxFile(job['source'], quiet=True, useFileUnits=True):
            raise RuntimeError('MAX 文件打开失败')
        if core.normalized(self.scene_path()) != core.normalized(job['source']):
            raise RuntimeError('打开的场景与任务文件不一致')
        kind = job['kind']
        if kind not in ('model', 'animation'):
            raise ValueError('未知导出类型')
        self.rt.sliderTime = int(config.get('model_frame', 0)) if kind == 'model' else self.rt.animationRange.start
        nodes = self.select_export_nodes(kind, config.get('root_name', 'Root'))
        self.load_preset(config[kind + '_preset'], kind, config.get('scene_range', True))
        self.bridge_excluded_parents(nodes, kind, config)
        target.parent.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix='.mt-stage-', dir=str(target.parent)) as staging:
            result = self.rt.exportFile(str(Path(staging) / target.name), self.rt.name('noPrompt'),
                                        selectedOnly=True, using=self.rt.FBXEXP)
            if result is False:
                raise RuntimeError('FBX 导出器返回失败')
            core.publish_export(staging, target)
        detail = '{}：{} 个节点，Root 保留，其余辅助对象已排除'.format('带蒙皮角色（无动画）' if kind == 'model' else 'Root 骨骼动画', len(nodes))
        if kind == 'animation' and self.rt.FBXExporterGetParam('BakeAnimation'):
            detail += '，烘焙 {}–{} 帧'.format(self.rt.FBXExporterGetParam('BakeFrameStart'), self.rt.FBXExporterGetParam('BakeFrameEnd'))
        return 'success', detail
