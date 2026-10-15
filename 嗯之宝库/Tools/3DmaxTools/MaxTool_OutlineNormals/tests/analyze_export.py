import sys,json,collections,math
from pathlib import Path
root=Path(__file__).resolve().parents[1];sys.path.insert(0,str(root))
from outline_math import dot,unit,oct_decode,smooth_normals
from tangent_space import corner_frames,from_tangent
out=root/'tests/artifacts'
def analyze(name):
 rows=json.loads((out/(name+'_corners.json')).read_text())
 positions=[tuple(r[:3]) for r in rows];faces=[(i,i+1,i+2) for i in range(0,len(rows),3)]
 normals=[[unit(tuple(r[3:6])) for r in rows[i:i+3]] for i in range(0,len(rows),3)]
 uv=[tuple(r[6:8]) for r in rows]
 frames=corner_frames(positions,faces,normals,faces,uv)
 decoded=[from_tangent(oct_decode(r[8:10]),frames[i]) for i,r in enumerate(rows)]
 groups=collections.defaultdict(list)
 for i,p in enumerate(positions):groups[p].append(i)
 bad=[(p,min(dot(decoded[g[0]],decoded[i]) for i in g)) for p,g in groups.items() if min(dot(decoded[g[0]],decoded[i]) for i in g)<.99999]
 print(name,'FBX own frames:',len(bad),'bad of',len(groups),'min dot',min(d for p,d in bad) if bad else 1)
 print('normal distinct',len(set(tuple(r[3:6]) for r in rows)),'UV3 distinct',len(set(tuple(r[8:10]) for r in rows)))
 return rows,frames,decoded
a,fa,da=analyze('3333');b,fb,db=analyze('diagnose_5087')
if (out/'diagnose_max_normals.json').exists():
 maxn=[n for face in json.loads((out/'diagnose_max_normals.json').read_text()) for n in face]
 for name,rows in [('3333',a),('diagnose',b)]:
  # exporter uses Y-up, X unchanged: Max (x,y,z) => FBX (x,z,-y)
  print(name,'Max normal comparison',min(dot(unit((n[0],-n[1],-n[2])),unit(tuple(r[3:6]))) for n,r in zip(maxn,rows)))
for name in ['diagnose_no_sg','diagnose_explicit']:
 if (out/(name+'_corners.json')).exists():analyze(name)
for begin,end,label in [(0,3,'positions'),(3,6,'normals'),(6,8,'UV1'),(8,10,'UV3')]:
 print(label,'different corners',sum(x[begin:end]!=y[begin:end] for x,y in zip(a,b)),'max delta',max(abs(x[i]-y[i]) for x,y in zip(a,b) for i in range(begin,end)))
for name,rows,frames in [('3333',a,fa),('__TLUVExportDiagnostic',b,fb)]:
 u=json.loads((out/(name+'_unity.json')).read_text())
 # Unity keeps FBX local axes with X reflection; normalize FBX positions by per-axis inferred scale.
 key=lambda p:tuple(round(x,4) for x in p)
 scales=[max(abs(p[i]) for p in u['vertices'])/max(abs(r[i]) for r in rows) for i in range(3)]
 fbx=collections.defaultdict(list)
 for i,r in enumerate(rows):fbx[(key((-r[0]*scales[0],r[1]*scales[1],r[2]*scales[2])),key(r[8:10]))].append(i)
 differences=[];miss=0;groups=collections.defaultdict(list)
 for i,p in enumerate(u['vertices']):
  n=unit(u['normals'][i]);t=unit(u['tangents'][i][:3]);uv3=u['uv3'][i]
  from outline_math import cross,mul
  frame=(t,mul(cross(n,t),u['tangents'][i][3]),n)
  d=from_tangent(oct_decode(uv3),frame);groups[tuple(p)].append(d)
  indices=fbx.get((key(p),key(uv3)))
  if not indices:miss+=1;continue
  j=indices[0];ft=frames[j][0];fn=frames[j][2]
  differences.append((dot(t,(-ft[0],ft[1],ft[2])),dot(n,(-fn[0],fn[1],fn[2]))))
 bad=[(p,min(dot(g[0],d) for d in g)) for p,g in groups.items() if min(dot(g[0],d) for d in g)<.99999]
 print(name,'Unity bad',len(bad),'min',min(d for p,d in bad) if bad else 1,'scale',scales,'unmatched',miss,'frame min',tuple(min(d[i] for d in differences) for i in range(2)) if differences else None)
 print('bad positions',bad[:6])
