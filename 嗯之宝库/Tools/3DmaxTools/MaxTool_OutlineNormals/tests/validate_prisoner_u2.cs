// Method body run through Unity MCP against disposable imports only.
var toolRoot=@"D:\other\Fork\Gate-of-Babylon\嗯之宝库\Tools\3DmaxTools\MaxTool_OutlineNormals";
var artifacts=System.IO.Path.Combine(toolRoot,"tests/artifacts");
var folder="Assets/__TLPrisonerU2Validation";
if(AssetDatabase.IsValidFolder(folder))throw new System.Exception("Validation folder already exists; inspect before retrying.");
AssetDatabase.CreateFolder("Assets","__TLPrisonerU2Validation");
var results=new System.Collections.Generic.List<object>();
System.Func<Vector2,Vector3> decode=uv=>{
 var p=(uv-new Vector2(2,0))*2-Vector2.one;
 var n=new Vector3(p.x,p.y,1-Mathf.Abs(p.x)-Mathf.Abs(p.y));
 var fold=Mathf.Clamp01(-n.z);n.x+=n.x>=0?-fold:fold;n.y+=n.y>=0?-fold:fold;return n.normalized;
};
try {
 foreach(var sourceName in new[]{"prisoner_baseline","prisoner_stack"}){
  var path=folder+"/"+sourceName+".fbx";
  System.IO.File.Copy(System.IO.Path.Combine(artifacts,sourceName+".fbx"),path,false);
  AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
  var importer=(ModelImporter)AssetImporter.GetAtPath(path);
  importer.isReadable=true;importer.importNormals=ModelImporterNormals.Import;
  importer.importTangents=ModelImporterTangents.CalculateMikk;
  importer.meshCompression=ModelImporterMeshCompression.Off;
  importer.generateSecondaryUV=false;importer.bakeAxisConversion=false;
  foreach(var weld in sourceName=="prisoner_stack"?new[]{true,false}:new[]{true}){
   importer.weldVertices=weld;importer.SaveAndReimport();
   var mesh=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().OrderByDescending(m=>m.vertexCount).First();
   var renderer=AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r=>r.sharedMesh==mesh);
   var v=mesh.vertices;var n=mesh.normals;var t=mesh.tangents;var uv3=mesh.uv3;
   var groups=new System.Collections.Generic.Dictionary<Vector3,System.Collections.Generic.List<Vector3>>();
   int marked=0,invalidTangents=0,bad=0;float min=1;
   if(sourceName=="prisoner_stack")for(int i=0;i<v.Length;i++){
    if(uv3[i].x<1.9999f||uv3[i].x>3.0001f||uv3[i].y<-.0001f||uv3[i].y>1.0001f)throw new System.Exception("Invalid U+2 marker");
    marked++;
    var e=decode(uv3[i]);var T=(new Vector3(t[i].x,t[i].y,t[i].z)-n[i]*Vector3.Dot(n[i],new Vector3(t[i].x,t[i].y,t[i].z))).normalized;
    if(T.sqrMagnitude<.5f||Mathf.Abs(t[i].w)<.5f)invalidTangents++;
    var B=Vector3.Cross(n[i],T)*t[i].w;var d=(e.x*T+e.y*B+e.z*n[i]).normalized;
    if(!groups.ContainsKey(v[i]))groups[v[i]]=new System.Collections.Generic.List<Vector3>();groups[v[i]].Add(d);
   }
   foreach(var g in groups.Values){float groupMin=1;foreach(var d in g)groupMin=Mathf.Min(groupMin,Vector3.Dot(g[0],d));min=Mathf.Min(min,groupMin);if(groupMin<.99999f)bad++;}
   var record=new { source=sourceName,weld,vertices=mesh.vertexCount,triangles=mesh.triangles.Length/3,uv2=mesh.uv2.Length,uv3=uv3.Length,marked,invalidTangents,bones=renderer==null?0:renderer.bones.Length,weights=mesh.boneWeights.Length,directionGroups=groups.Count,directionDiscontinuities=bad,minDot=min };
   results.Add(record);
   if(sourceName=="prisoner_stack")System.IO.File.WriteAllText(System.IO.Path.Combine(artifacts,weld?"prisoner_unity.json":"prisoner_unity_unwelded.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{vertices=v.Select(p=>new[]{p.x,p.y,p.z}),normals=n.Select(p=>new[]{p.x,p.y,p.z}),tangents=t.Select(p=>new[]{p.x,p.y,p.z,p.w}),uv=mesh.uv.Select(p=>new[]{p.x,p.y}),uv3=uv3.Select(p=>new[]{p.x,p.y}),triangles=mesh.triangles}));
  }
 }
 var mgr=Autodesk.Fbx.FbxManager.Create();
 try {
  var scene=Autodesk.Fbx.FbxScene.Create(mgr,"prisoner");var fbxImporter=Autodesk.Fbx.FbxImporter.Create(mgr,"");
  if(!fbxImporter.Initialize(System.IO.Path.Combine(artifacts,"prisoner_stack.fbx"),-1,mgr.GetIOSettings())||!fbxImporter.Import(scene))throw new System.Exception("FBX SDK import failed");
  var pending=new System.Collections.Generic.Queue<Autodesk.Fbx.FbxNode>();pending.Enqueue(scene.GetRootNode());
  Autodesk.Fbx.FbxMesh fbx=null;
  while(pending.Count>0){var node=pending.Dequeue();if(node.GetMesh()!=null&&(fbx==null||node.GetMesh().GetPolygonCount()>fbx.GetPolygonCount()))fbx=node.GetMesh();for(int i=0;i<node.GetChildCount();i++)pending.Enqueue(node.GetChild(i));}
  var rows=new System.Collections.Generic.List<object>();
  var normal=fbx.GetLayer(0).GetNormals();var uv=fbx.GetLayer(0).GetUVs();var outline=fbx.GetLayer(2).GetUVs();
  for(int fi=0;fi<fbx.GetPolygonCount();fi++)for(int ci=0;ci<3;ci++){
   int vi=fbx.GetPolygonVertex(fi,ci),corner=fi*3+ci;var p=fbx.GetControlPointAt(vi);
   int ni=normal.GetMappingMode()==Autodesk.Fbx.FbxLayerElement.EMappingMode.eByControlPoint?vi:corner;
   if(normal.GetReferenceMode()==Autodesk.Fbx.FbxLayerElement.EReferenceMode.eIndexToDirect)ni=normal.GetIndexArray().GetAt(ni);
   var N=normal.GetDirectArray().GetAt(ni);
   int ui=uv.GetMappingMode()==Autodesk.Fbx.FbxLayerElement.EMappingMode.eByControlPoint?vi:corner;
   if(uv.GetReferenceMode()==Autodesk.Fbx.FbxLayerElement.EReferenceMode.eIndexToDirect)ui=uv.GetIndexArray().GetAt(ui);var U=uv.GetDirectArray().GetAt(ui);
   int oi=outline.GetMappingMode()==Autodesk.Fbx.FbxLayerElement.EMappingMode.eByControlPoint?vi:corner;
   if(outline.GetReferenceMode()==Autodesk.Fbx.FbxLayerElement.EReferenceMode.eIndexToDirect)oi=outline.GetIndexArray().GetAt(oi);var O=outline.GetDirectArray().GetAt(oi);
   rows.Add(new{position=new[]{p.X,p.Y,p.Z},normal=new[]{N.X,N.Y,N.Z},uv=new[]{U.X,U.Y},uv3=new[]{O.X,O.Y}});
  }
  System.IO.File.WriteAllText(System.IO.Path.Combine(artifacts,"prisoner_fbx_corners.json"),Newtonsoft.Json.JsonConvert.SerializeObject(rows));
 } finally { mgr.Destroy(); }
 System.IO.File.WriteAllText(System.IO.Path.Combine(artifacts,"prisoner_stack_unity_result.json"),Newtonsoft.Json.JsonConvert.SerializeObject(results,Newtonsoft.Json.Formatting.Indented));
 return results;
} finally { AssetDatabase.DeleteAsset(folder); }
