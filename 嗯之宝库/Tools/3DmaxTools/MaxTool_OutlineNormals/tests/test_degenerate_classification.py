import sys,random,math
from pathlib import Path
root=Path(__file__).resolve().parents[1];sys.path.insert(0,str(root))
from outline_math import is_degenerate_triangle,smooth_normals,cross,unit,sub,dot
def old(v,f):
 a,b,c=(v[i] for i in f);r=cross(unit(sub(b,a)),unit(sub(c,a)));return dot(r,r)<1e-20
v=[(0.,0.,0.),(1.,0.,0.),(2.,0.,0.),(0.,1.,0.),(1.,.00001,0.)]
assert is_degenerate_triangle(v,(0,1,2)) and is_degenerate_triangle(v,(0,1,1))
assert not is_degenerate_triangle(v,(0,1,3)) and not is_degenerate_triangle(v,(0,1,4))
for scale in (1e-9,1,1e9):
 scaled=[tuple(scale*x for x in p) for p in v]
 for f in ((0,1,2),(0,1,1),(0,1,3),(0,1,4)):assert is_degenerate_triangle(scaled,f)==is_degenerate_triangle(v,f)
r=random.Random(36)
for _ in range(10000):
 points=[tuple(r.uniform(-20,20) for _ in range(3)) for _ in range(3)]
 assert is_degenerate_triangle(points,(0,1,2))==old(points,(0,1,2))
print('Geometric classifier matches the existing bake criterion; tiny valid triangles and uniform scale preserved PASS')
