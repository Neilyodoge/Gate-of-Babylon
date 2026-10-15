import os,sys,traceback
from pymxs import runtime as rt
root=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
results=[]
try:
    rt.pluginManager.loadClass(rt.FBXEXP)
    n=rt.box(name='TangentProbe',length=10,width=10,height=10,mapCoords=True)
    rt.select(n)
    for k,v in [('ASCII',False),('Animation',False),('SmoothingGroups',True),('TangentSpaceExport',True),('Triangulate',True)]:
        rt.FBXExporterSetParam(k,v)
    rt.exportFile(os.path.join(root,'tests/artifacts/raw.fbx'),rt.Name('noPrompt'),selectedOnly=True,using=rt.FBXEXP)
    results.append('export PASS')
except Exception:
    results.append(traceback.format_exc())
open(os.path.join(root,'tests/artifacts/probe.txt'),'w').write('\n'.join(results))
