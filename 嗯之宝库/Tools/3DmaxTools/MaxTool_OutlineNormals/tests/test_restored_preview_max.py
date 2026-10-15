import os,sys,runpy,traceback
from pathlib import Path
assert os.environ.get('TL_OUTLINE_BATCH_TEST')=='1'
root=Path(r'D:\other\Fork\Gate-of-Babylon\嗯之宝库\Tools\3DmaxTools\MaxTool_OutlineNormals')
sys.path.insert(0,str(root))
import pymxs
from pymxs import runtime as rt
import tl_outline_max as tool
from outline_preview import QtWidgets
out=root/'tests/artifacts/restored_preview_result.txt'
rows=['STARTED'];out.write_text('\n'.join(rows),encoding='utf-8')
def passed(message):
    rows.append(message+' PASS');out.write_text('\n'.join(rows),encoding='utf-8')
def idle():
    rt.subObjectLevel=0
    if rt.theHold.Holding():rt.theHold.Accept('Disposable fixture')
    rt.clearUndoBuffer()
def state(node):
    mesh=rt.snapshotAsMesh(node)
    return (tuple(tool.point(rt.getVert(mesh,i)) for i in range(1,int(rt.getNumVerts(mesh))+1)),
        tuple(tool.point(rt.getFace(mesh,i)) for i in range(1,int(rt.getNumFaces(mesh))+1)),
        tuple(int(rt.getHandleByAnim(m)) for m in node.modifiers),
        tuple(int(rt.getHandleByAnim(n)) for n in rt.selection),bool(rt.showEndResult),bool(rt.theHold.Holding()))
try:
    runpy.run_path(str(root/'tests/test_uv2_preview.py'),run_name='__main__')
    result=(root/'tests/artifacts/uv2_preview_result.txt').read_text(encoding='utf-8')
    assert result.rstrip().endswith('PASS'),result
    passed('Existing OpenGL preview: outline, Alpha black/gray/white, independent width Alpha, missing Alpha=1, mouse drag')
    dialog=tool.show()
    expected=['烘焙选中模型','清空日志','查看通道','预览描边 / 顶点色 A']
    assert sorted(b.text() for b in dialog.findChildren(QtWidgets.QPushButton))==sorted(expected)
    called=[];native=tool.open_channel_info;tool.open_channel_info=lambda:called.append(True)
    dialog.channels_button.click();assert called==[True];tool.open_channel_info=native
    passed('Four intended main actions restored; channel button invokes native channel-info entry')
    node=rt.box(mapCoords=True);rt.convertToMesh(node);rt.select(node)
    rt.meshop.setMapSupport(node,1,False)
    rt.meshop.setMapSupport(node,-2,True);rt.meshop.setNumMapVerts(node,-2,36)
    for fi in range(1,13):
        for ci,value in enumerate((0.,.5,1.)):
            rt.meshop.setMapVert(node,-2,(fi-1)*3+ci+1,rt.Point3(value,value,value))
        rt.meshop.setMapFace(node,-2,fi,rt.Point3(fi*3-2,fi*3-1,fi*3))
    rt.setCommandPanelTaskMode(rt.Name('modify'));rt.modPanel.setCurrentObject(node.baseObject,node=node)
    rt.showEndResult=False;idle();before=state(node)
    for mode in (0,1,2):
        payload,messages=tool.preview_rows([node],mode)
        assert all(row[9]==(0.,.5,1.)[i%3] for i,row in enumerate(payload))
        assert len(payload)==36 and state(node)==before
    dialog.preview_button.click();app=QtWidgets.QApplication.instance()
    for i in range(15):app.processEvents()
    preview=dialog.preview;assert preview is not None and preview.view.count==36,preview.status.text()
    preview.vertex_alpha.setChecked(True);preview.outline.setChecked(False)
    for i in range(5):app.processEvents()
    frame=preview.view.grabFramebuffer();assert not frame.isNull() and not preview.view.error,preview.view.error
    grayscale=[frame.pixelColor(x,y).red() for y in range(0,frame.height(),2) for x in range(0,frame.width(),2)
        if frame.pixelColor(x,y).red()==frame.pixelColor(x,y).green()==frame.pixelColor(x,y).blue()]
    assert len(grayscale)>1000 and min(grayscale)<25 and max(grayscale)>230 and any(100<=v<=155 for v in grayscale)
    assert state(node)==before
    passed('No UV1: all three preview modes and Alpha gray rendering work; full scene/panel/end-result/hold state preserved')
    node=rt.box(mapCoords=True);rt.convertToMesh(node)
    rt.meshop.setNumVerts(node,9);rt.setVert(node,9,rt.Point3(100,200,300))
    rt.setNumFaces(node,13,True);rt.setFace(node,13,rt.Point3(1,1,2))
    rt.meshop.setNumMapFaces(node,1,13,keep=True)
    rt.meshop.setMapFace(node,1,13,rt.Point3(1,1,2));rt.update(node);rt.select(node);idle()
    with tool.undo_transaction('Disposable bake'):tool.bake_node(node)
    rt.modPanel.setCurrentObject(node.baseObject,node=node);rt.showEndResult=False;idle();before=state(node)
    for mode in (0,1,2):
        payload,messages=tool.preview_rows([node],mode)
        assert len(payload)==39 and state(node)==before
        if mode==0:assert '已烘焙第 3 套 UV' in messages[0],messages
    preview.load_selection()
    for i in range(5):app.processEvents()
    assert preview.view.count==39 and '预览读取失败' not in preview.status.text(),preview.status.text()
    assert preview.grab().save(str(root/'tests/artifacts/restored_vertex_alpha_ui.png'))
    assert dialog.grab().save(str(root/'tests/artifacts/restored_preview_main_ui.png'))
    passed('Baked orphan/degenerate mesh previews all modes with end-result OFF; full-stack UV3 recognized and geometry retained')
    dialog.close();rows.append('ALL RESTORED PREVIEW TESTS PASSED')
except Exception:
    rows.append(traceback.format_exc())
finally:
    out.write_text('\n'.join(rows),encoding='utf-8')
