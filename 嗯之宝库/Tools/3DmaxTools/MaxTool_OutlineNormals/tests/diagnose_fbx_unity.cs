var toolRoot=System.Environment.GetEnvironmentVariable("TL_OUTLINE_TOOL_ROOT");
if(string.IsNullOrWhiteSpace(toolRoot))throw new System.Exception("Set TL_OUTLINE_TOOL_ROOT to the tool source directory before running this test.");
var artifacts=System.IO.Path.Combine(toolRoot,"tests/artifacts");
var mgr=Autodesk.Fbx.FbxManager.Create();
try {
 foreach(var path in new[]{"Assets/ArtAssets/_TopLegendArt/Scenes_better/Scene/meishuniubi/Models/chengjian/fbx/3333.fbx",System.IO.Path.Combine(artifacts,"diagnose_5087.fbx")}) {
  var scene=Autodesk.Fbx.FbxScene.Create(mgr,"inspect");
  var importer=Autodesk.Fbx.FbxImporter.Create(mgr,"");
  importer.Initialize(System.IO.Path.GetFullPath(path),-1,mgr.GetIOSettings());importer.Import(scene);
  var node=scene.GetRootNode().GetChild(0);var mesh=node.GetMesh();
  var rows=new System.Collections.Generic.List<double[]>();
  for(int f=0;f<mesh.GetPolygonCount();f++)for(int c=0;c<3;c++) {
   int vi=mesh.GetPolygonVertex(f,c), corner=f*3+c;var p=mesh.GetControlPointAt(vi);
   var normal=mesh.GetLayer(0).GetNormals();int ni=normal.GetMappingMode()==Autodesk.Fbx.FbxLayerElement.EMappingMode.eByControlPoint?vi:corner;
   if(normal.GetReferenceMode()==Autodesk.Fbx.FbxLayerElement.EReferenceMode.eIndexToDirect)ni=normal.GetIndexArray().GetAt(ni);
   var n=normal.GetDirectArray().GetAt(ni);
   var uv=mesh.GetLayer(0).GetUVs();int ui=uv.GetMappingMode()==Autodesk.Fbx.FbxLayerElement.EMappingMode.eByControlPoint?vi:corner;
   if(uv.GetReferenceMode()==Autodesk.Fbx.FbxLayerElement.EReferenceMode.eIndexToDirect)ui=uv.GetIndexArray().GetAt(ui);
   var u=uv.GetDirectArray().GetAt(ui);
   var uv3=mesh.GetLayer(2).GetUVs();int oi=uv3.GetIndexArray().GetAt(corner);var o=uv3.GetDirectArray().GetAt(oi);
   rows.Add(new[]{p.X,p.Y,p.Z,n.X,n.Y,n.Z,u.X,u.Y,o.X,o.Y});
  }
  System.IO.File.WriteAllText(System.IO.Path.Combine(artifacts,System.IO.Path.GetFileNameWithoutExtension(path)+"_corners.json"),Newtonsoft.Json.JsonConvert.SerializeObject(rows));
 }
 foreach(var path in new[]{"Assets/ArtAssets/_TopLegendArt/Scenes_better/Scene/meishuniubi/Models/chengjian/fbx/3333.fbx","Assets/__TLUVExportDiagnostic.fbx"}) {
  var m=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().First();
  System.IO.File.WriteAllText(System.IO.Path.Combine(artifacts,System.IO.Path.GetFileNameWithoutExtension(path)+"_unity.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{vertices=m.vertices.Select(p=>new[]{p.x,p.y,p.z}),normals=m.normals.Select(p=>new[]{p.x,p.y,p.z}),tangents=m.tangents.Select(p=>new[]{p.x,p.y,p.z,p.w}),uv=m.uv.Select(p=>new[]{p.x,p.y}),uv3=m.uv3.Select(p=>new[]{p.x,p.y}),triangles=m.triangles}));
 }
 return "FBX and Unity corner data saved";
}finally{mgr.Destroy();}
