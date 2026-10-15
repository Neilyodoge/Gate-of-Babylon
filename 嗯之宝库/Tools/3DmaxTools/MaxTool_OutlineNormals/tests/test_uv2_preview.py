import os,sys,traceback
from pathlib import Path
root=Path(__file__).resolve().parents[1];sys.path.insert(0,str(root))
from pymxs import runtime as rt
from tl_outline_max import bake_node,preview_rows,show
from outline_preview import QtWidgets
from PySide2 import QtCore
out=root/'tests/artifacts';result=[]
try:
 n=rt.box(mapCoords=True,length=100,width=100,height=100)
 rt.convertToMesh(n)
 rt.meshop.setMapSupport(n,0,True);rt.meshop.setNumMapVerts(n,0,36)
 colors=[(1,0,0),(0,1,0),(0,0,1)]
 for f in range(1,13):
  for c in range(3):rt.meshop.setMapVert(n,0,(f-1)*3+c+1,rt.Point3(*colors[c]))
  rt.meshop.setMapFace(n,0,f,rt.Point3(f*3-2,f*3-1,f*3))
 rt.meshop.setMapSupport(n,-2,True);rt.meshop.setNumMapVerts(n,-2,36)
 for f in range(1,13):
  for c,alpha in enumerate((0.,.5,1.)):
   rt.meshop.setMapVert(n,-2,(f-1)*3+c+1,rt.Point3(alpha,alpha,alpha))
  rt.meshop.setMapFace(n,-2,f,rt.Point3(f*3-2,f*3-1,f*3))
 rows,_=preview_rows([n],units_per_meter=1)
 assert all(row[9]==(0.,.5,1.)[i%3] for i,row in enumerate(rows))
 rt.select(n);bake_node(n);count=len(list(rt.objects))
 launcher=show();launcher.open_preview();app=QtWidgets.QApplication.instance()
 for i in range(20):app.processEvents()
 p=launcher.preview
 assert p.adaptive.isChecked()
 assert launcher.channel.value()==3 and not launcher.overwrite.isChecked()
 assert not p.view.error
 a=p.view.grabFramebuffer();assert not a.isNull();a.save(str(out/'uv2_preview.png'))
 p.vertex_alpha.setChecked(True)
 for i in range(5):app.processEvents()
 b=p.view.grabFramebuffer();b.save(str(out/'uv2_vertex_alpha.png'))
 diff=sum(a.pixel(x,y)!=b.pixel(x,y) for y in range(a.height()) for x in range(a.width()))
 assert diff>1000
 p.outline.setChecked(False)
 for i in range(5):app.processEvents()
 c=p.view.grabFramebuffer()
 assert sum(c.pixel(x,y)!=b.pixel(x,y) for y in range(b.height()) for x in range(b.width()))>100
 grayscale=[]
 for y in range(0,c.height(),2):
  for x in range(0,c.width(),2):
   color=c.pixelColor(x,y)
   if color.red()==color.green()==color.blue():grayscale.append(color.red())
 assert len(grayscale)>1000 and min(grayscale)<25 and max(grayscale)>230
 assert any(100<=v<=155 for v in grayscale),'Missing interpolated Alpha gray'
 p.alpha.setChecked(False)
 for i in range(5):app.processEvents()
 unchanged=p.view.grabFramebuffer()
 assert all(unchanged.pixel(x,y)==c.pixel(x,y) for y in range(c.height()) for x in range(c.width())), 'Width Alpha toggle changed Alpha visualization'
 fallback=rt.box(mapCoords=True)
 assert all(row[9]==1 for row in preview_rows([fallback],units_per_meter=1)[0])
 rt.delete(fallback)
 class Mouse:
  def __init__(self,x,y):self.p=QtCore.QPoint(x,y)
  def pos(self):return self.p
  def buttons(self):return QtCore.Qt.LeftButton
 p.view.last_mouse=QtCore.QPoint(10,10);yaw=p.view.yaw
 p.view.mouseMoveEvent(Mouse(30,10));assert p.view.yaw<yaw
 launcher.close();assert len(list(rt.objects))==count
 result.append('UV2 preview, Alpha black/gray/white, independent width control, missing Alpha=1, adaptive default, outline, drag, scene isolation PASS')
except Exception:result.append(traceback.format_exc())
finally:(out/'uv2_preview_result.txt').write_text('\n'.join(result),encoding='utf-8')
