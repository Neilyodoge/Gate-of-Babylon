import json, sys, collections
from pathlib import Path
root=Path(__file__).resolve().parents[1];sys.path.insert(0,str(root))
from outline_math import dot, unit, oct_decode
from tangent_space import corner_frames,unity_corner_frames,from_tangent
out=root/'tests/artifacts'
a=json.loads((out/'prisoner_max_reference.json').read_text())
b=json.loads((out/'prisoner_fbx_corners.json').read_text())
u=json.loads((out/'prisoner_unity.json').read_text())
f=[(i*3,i*3+1,i*3+2) for i in range(len(b)//3)]
p=[tuple(x['position']) for x in b]
n=[[tuple(b[i*3+c]['normal']) for c in range(3)] for i in range(len(f))]
uv=[tuple(x['uv']) for x in b]
frames=unity_corner_frames(p,f,n,f,uv)
groups=collections.defaultdict(list)
for i,x in enumerate(b):groups[tuple(x['position'])].append(from_tangent(oct_decode(x['uv3']),frames[i]))
bad=[(k,min(dot(v[0],d) for d in v)) for k,v in groups.items() if min(dot(v[0],d) for d in v)<.99999]
print('FBX Unity-import basis direction groups',len(bad),'min',min((x[1] for x in bad),default=1))
positions=collections.defaultdict(list)
for i,v in enumerate(a['vertices']):positions[tuple(round(x,4) for x in v)].append(i)
for x in b[:5]:print('position sample',x['position'])
print('max sample',a['vertices'][:5])
print('same normal max delta',max(abs(x[j]-y[j]) for i,face in enumerate(a['normals']) for c,x in enumerate(face) for j in range(3) for y in [b[i*3+c]['normal']]))
print('uv3 delta',max(abs(x[j]-b[i]['uv3'][j]) for i,x in enumerate(a['uv3']) for j in range(2)))
print('bad groups',bad[:12])
verts=[tuple(v) for v in u['vertices']];faces=[tuple(u['triangles'][i:i+3]) for i in range(0,len(u['triangles']),3)]
ns=[[tuple(u['normals'][vi]) for vi in face] for face in faces]
us=[tuple(x) for x in u['uv']]
uframes=corner_frames(verts,faces,ns,faces,us)
predicted=collections.defaultdict(list);actual=collections.defaultdict(list);worst=[]
from outline_math import cross,mul,sub
for fi,face in enumerate(faces):
 for c,vi in enumerate(face):
  frame=uframes[fi*3+c];N=tuple(u['normals'][vi]);T0=tuple(u['tangents'][vi][:3]);T=unit(sub(T0,mul(N,dot(N,T0))));B=mul(cross(N,T),u['tangents'][vi][3])
  e=oct_decode(u['uv3'][vi]);expected=from_tangent(e,frame);observed=from_tangent(e,(T,B,N))
  predicted[verts[vi]].append(expected);actual[verts[vi]].append(observed)
  if dot(T,frame[0])<.99999:worst.append((vi,dot(T,frame[0]),u['uv'][vi],frame,(T,B,N)))
for label,g in [('recalcUnityMikk',predicted),('UnityStored',actual)]:
 bad=[(k,min(dot(v[0],d) for d in v)) for k,v in g.items() if min(dot(v[0],d) for d in v)<.99999]
 print(label,'bad',len(bad),'min',min((x[1] for x in bad),default=1),'samples',bad[:3])
print('ordinary T mismatch',len(worst),'samples',worst[:3])
# Compare Unity face-corner UV/normal values to the exported FBX values.
fbx_by_uv=collections.defaultdict(list)
for row in b:fbx_by_uv[(tuple(round(x,6) for x in row['uv']),tuple(row['uv3']))].append(row)
matches=[]
for i in u['triangles']:
 key=(tuple(round(x,6) for x in u['uv'][i]),tuple(u['uv3'][i]))
 candidates=fbx_by_uv.get(key,[])
 if candidates:matches.append((i,candidates))
print('UV corner matches',len(matches),'/',len(u['triangles']))
print('fbx normal/Unity sample',b[0]['normal'],u['normals'][u['triangles'][0]])
print('normal distinct FBX',len(set(tuple(x['normal']) for x in b)),'Unity',len(set(tuple(x) for x in u['normals'])))
