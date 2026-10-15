var toolRoot=System.Environment.GetEnvironmentVariable("TL_OUTLINE_TOOL_ROOT");
if(string.IsNullOrWhiteSpace(toolRoot))throw new System.Exception("Set TL_OUTLINE_TOOL_ROOT to the tool source directory before running this test.");
var artifacts=System.IO.Path.Combine(toolRoot,"tests/artifacts");
var results=new System.Collections.Generic.List<string>();
foreach(var name in new[]{"cube","poly","rotated","mirror","skin","existing"}){
 var path="Assets/__TLUV2Validation/"+name+".fbx";
 System.IO.File.Copy(System.IO.Path.Combine(artifacts,"uv2_"+name+".fbx"),path,true);
 AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
 var imp=(ModelImporter)AssetImporter.GetAtPath(path);imp.isReadable=true;imp.importNormals=ModelImporterNormals.Import;imp.importTangents=ModelImporterTangents.CalculateMikk;imp.bakeAxisConversion=false;imp.generateSecondaryUV=false;imp.SaveAndReimport();
 int count=0;
 foreach(var a in AssetDatabase.LoadAllAssetsAtPath(path)){
  var m=a as Mesh;if(m==null)continue;count++;
  var v=m.vertices;var tr=m.triangles;var sums=new System.Collections.Generic.Dictionary<Vector3,Vector3>();
  for(int f=0;f<tr.Length;f+=3)for(int c=0;c<3;c++){
   var p=v[tr[f+c]];var x=v[tr[f+(c+1)%3]]-p;var y=v[tr[f+(c+2)%3]]-p;
   var angle=Mathf.Acos(Mathf.Clamp(Vector3.Dot(x.normalized,y.normalized),-1,1));
   if(!sums.ContainsKey(p))sums[p]=Vector3.zero;sums[p]+=Vector3.Cross(x,y).normalized*angle;
  }
  var uv=m.uv2;if(uv.Length!=v.Length)throw new System.Exception(name+": UV2 missing");
  float minDot=1;
  for(int i=0;i<v.Length;i++){
   if(uv[i].x<1.9999f||uv[i].x>3.0001f||uv[i].y<-.0001f||uv[i].y>1.0001f)throw new System.Exception(name+": marker invalid");
   var f=(uv[i]-new Vector2(2,0))*2-Vector2.one;var n=new Vector3(f.x,f.y,1-Mathf.Abs(f.x)-Mathf.Abs(f.y));
   var fold=Mathf.Clamp01(-n.z);n.x+=n.x>=0?-fold:fold;n.y+=n.y>=0?-fold:fold;
   minDot=Mathf.Min(minDot,Vector3.Dot(n.normalized,sums[v[i]].normalized));
  }
  if(minDot<.99999f)throw new System.Exception(name+": direction="+minDot);
  var tangents=m.tangents;var normals=m.normals;float ortho=0;
  for(int i=0;i<v.Length;i++)ortho=Mathf.Max(ortho,Mathf.Abs(Vector3.Dot(normals[i],new Vector3(tangents[i].x,tangents[i].y,tangents[i].z))));
  if(ortho>.0001f)throw new System.Exception(name+": ordinary tangent corrupted");
  if(name=="skin"&&m.boneWeights.Length!=v.Length)throw new System.Exception("Skin weights lost");
  for(int c=2;c<8;c++){var data=new System.Collections.Generic.List<Vector2>();m.GetUVs(c,data);if(data.Count>0)throw new System.Exception("Unexpected UV"+c);}
  results.Add(name+": PASS decoded dot="+minDot+" ordinary tangent preserved, UV2 only; bones="+m.boneWeights.Length);
 }
 if(count!=1)throw new System.Exception(name+":mesh count="+count);
}
var output=string.Join("\n",results);System.IO.File.WriteAllText(System.IO.Path.Combine(artifacts,"uv2_unity_result.txt"),output);return output;
