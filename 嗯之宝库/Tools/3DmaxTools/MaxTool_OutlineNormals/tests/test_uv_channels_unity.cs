// Execute method body through Unity MCP. Temporary assets are removed by the caller.
var toolRoot=System.Environment.GetEnvironmentVariable("TL_OUTLINE_TOOL_ROOT");
if(string.IsNullOrWhiteSpace(toolRoot))throw new System.Exception("Set TL_OUTLINE_TOOL_ROOT to the tool source directory before running this test.");
var artifacts=System.IO.Path.Combine(toolRoot,"tests/artifacts");
var folder="Assets/__TLUVChannelValidation";
if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets","__TLUVChannelValidation");
var results=new System.Collections.Generic.List<string>();
var cases=new[]{"cube","poly","rotated","mirror","skin","building","zero","curved","mirrored_uv","experimental_upgrade","channel2","channel4","channel8","override"};
foreach(var name in cases){
 int channel=name=="channel2"?2:name=="channel4"?4:name=="channel8"?8:3;
 var path=folder+"/"+name+".fbx";
 System.IO.File.Copy(System.IO.Path.Combine(artifacts,"channels_"+name+".fbx"),path,true);
 AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
 var importer=(ModelImporter)AssetImporter.GetAtPath(path);
 importer.isReadable=true;importer.importNormals=ModelImporterNormals.Import;
 importer.importTangents=ModelImporterTangents.CalculateMikk;
 importer.bakeAxisConversion=false;importer.generateSecondaryUV=false;
 importer.meshCompression=ModelImporterMeshCompression.Off;importer.SaveAndReimport();
 foreach(bool lightmap in name=="cube"||name=="building"?new[]{false,true}:new[]{false}){
  if(lightmap){importer.generateSecondaryUV=true;importer.SaveAndReimport();}
  var meshes=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().ToArray();
  if(meshes.Length!=1)throw new System.Exception(name+": mesh count="+meshes.Length);
  var mesh=meshes[0];var vertices=mesh.vertices;var tris=mesh.triangles;
  var uv=new System.Collections.Generic.List<Vector2>();mesh.GetUVs(channel-1,uv);
  if(uv.Count!=vertices.Length)throw new System.Exception(name+": selected UV missing");
  var sums=new System.Collections.Generic.Dictionary<Vector3,Vector3>();
  for(int f=0;f<tris.Length;f+=3)for(int c=0;c<3;c++){
   var p=vertices[tris[f+c]];var a=vertices[tris[f+(c+1)%3]]-p;var b=vertices[tris[f+(c+2)%3]]-p;
   var angle=Mathf.Acos(Mathf.Clamp(Vector3.Dot(a.normalized,b.normalized),-1,1));
   if(!sums.ContainsKey(p))sums[p]=Vector3.zero;sums[p]+=Vector3.Cross(a,b).normalized*angle;
  }
  float minDot=1;
  for(int i=0;i<vertices.Length;i++){
   if(uv[i].x<1.9999f||uv[i].x>3.0001f||uv[i].y<-.0001f||uv[i].y>1.0001f)throw new System.Exception(name+": invalid marker");
   var f=(uv[i]-new Vector2(2,0))*2-Vector2.one;var n=new Vector3(f.x,f.y,1-Mathf.Abs(f.x)-Mathf.Abs(f.y));
   var fold=Mathf.Clamp01(-n.z);n.x+=n.x>=0?-fold:fold;n.y+=n.y>=0?-fold:fold;
   n.Normalize();var N=mesh.normals[i];var tangent=mesh.tangents[i];
   var T=(new Vector3(tangent.x,tangent.y,tangent.z)-N*Vector3.Dot(N,new Vector3(tangent.x,tangent.y,tangent.z))).normalized;
   var B=Vector3.Cross(N,T)*tangent.w;
   var direction=(n.x*T+n.y*B+n.z*N).normalized;
   minDot=Mathf.Min(minDot,Vector3.Dot(direction,sums[vertices[i]].normalized));
  }
  if(minDot<.99999f)throw new System.Exception(name+": min direction dot="+minDot);
  float ortho=0;var normals=mesh.normals;var tangents=mesh.tangents;
  for(int i=0;i<vertices.Length;i++)ortho=Mathf.Max(ortho,Mathf.Abs(Vector3.Dot(normals[i],new Vector3(tangents[i].x,tangents[i].y,tangents[i].z))));
  if(ortho>.0001f)throw new System.Exception(name+": tangent changed");
  if(name=="skin"&&mesh.boneWeights.Length!=vertices.Length)throw new System.Exception("Skin weights lost");
  if(channel>2&&!lightmap){
   var second=mesh.uv2;
   if(second.Length!=vertices.Length)throw new System.Exception(name+": placeholder lost, UV channel shifted");
   if(name=="building"){
    var distinct=second.Distinct().ToArray();
    if(distinct.Length!=36)throw new System.Exception("Building lightmap UV lost seams");
    foreach(var value in distinct){int i=Mathf.RoundToInt(value.x*50);if(i<1||i>36||Mathf.Abs(value.x-i/50f)>.00001f||Mathf.Abs(value.y-(36-i)/50f)>.00001f)throw new System.Exception("Building UV2 changed");}
   }else if(name!="override"&&name!="experimental_upgrade"&&second.Any(x=>x!=Vector2.zero))throw new System.Exception(name+": placeholder should be empty zeros");
  }
  for(int c=channel;c<8;c++){var extra=new System.Collections.Generic.List<Vector2>();mesh.GetUVs(c,extra);if(extra.Count>0)throw new System.Exception(name+": unexpected UV"+(c+1));}
  results.Add(name+": PASS UV"+channel+" vertices="+vertices.Length+" minDot="+minDot+" lightmap="+lightmap+" ordinary tangents, Skin, UV2 preserved");
 }
}
var shaderPath="Assets/ArtAssets/_TopLegendArt/TARes/RenderFeature/TL_RF_Outline.shader";
AssetDatabase.ImportAsset(shaderPath,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
var shader=AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
var messages=ShaderUtil.GetShaderMessages(shader);
if(messages.Any(x=>x.severity.ToString()=="Error"))throw new System.Exception(string.Join("\n",messages.Select(x=>x.message)));
results.Add("Shader supported="+shader.isSupported+" compile messages="+messages.Length);
var output=string.Join("\n",results);
System.IO.File.WriteAllText(System.IO.Path.Combine(artifacts,"channels_unity_result.txt"),output);
return output;
