"""Native Max FBX export used only by regression tests, outside the tool package."""
from pymxs import runtime as rt


def export_uv_fbx(nodes, destination):
    rt.pluginManager.loadClass(rt.FBXEXP)
    selected = list(rt.selection)
    rt.FBXExporterSetParam('PushSettings')
    try:
        rt.FBXExporterSetParam('Triangulate', True)
        rt.FBXExporterSetParam('SmoothingGroups', True)
        rt.FBXExporterSetParam('Skin', True)
        rt.showEndResult = True
        for node in nodes:
            rt.select(node)
            rt.setCommandPanelTaskMode(rt.Name('modify'))
            if len(list(node.modifiers)):
                rt.modPanel.setCurrentObject(node.modifiers[0])
            rt.notifyDependents(node)
            try:
                rt.update(node)
            except RuntimeError:
                pass
        rt.redrawViews()
        rt.select(rt.Array(*nodes))
        if not rt.exportFile(destination, rt.Name('noPrompt'), selectedOnly=True, using=rt.FBXEXP):
            raise RuntimeError('Native test FBX export failed')
        return 'Native Max FBX export complete'
    finally:
        rt.FBXExporterSetParam('PopSettings')
        if selected:
            rt.select(rt.Array(*selected))
        else:
            rt.clearSelection()
