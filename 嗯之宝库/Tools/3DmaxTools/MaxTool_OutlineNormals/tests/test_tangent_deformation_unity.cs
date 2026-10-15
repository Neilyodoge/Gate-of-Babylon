// Actual imported Max data, Unity skinning/static batching, and Shader pixel comparison.
var toolRoot=System.Environment.GetEnvironmentVariable("TL_OUTLINE_TOOL_ROOT");
if(string.IsNullOrWhiteSpace(toolRoot))throw new System.Exception("Set TL_OUTLINE_TOOL_ROOT to the tool source directory before running this test.");
var artifacts=System.IO.Path.Combine(toolRoot,"tests/artifacts");
System.Func<Mesh,int,Vector3> decode=(mesh,i)=>{
 var uv=mesh.uv3[i];var f=(uv-new Vector2(2,0))*2-Vector2.one;
 var n=new Vector3(f.x,f.y,1-Mathf.Abs(f.x)-Mathf.Abs(f.y));var fold=Mathf.Clamp01(-n.z);
 n.x+=n.x>=0?-fold:fold;n.y+=n.y>=0?-fold:fold;n.Normalize();
 var N=mesh.normals[i].normalized;var tangent=mesh.tangents[i];
 var raw=new Vector3(tangent.x,tangent.y,tangent.z);var T=(raw-N*Vector3.Dot(N,raw)).normalized;
 return (n.x*T+n.y*Vector3.Cross(N,T)*tangent.w+n.z*N).normalized;
};
var shader=Shader.Find("TL/RenderFeature/Outline");
var material=new Material(shader);material.SetColor("_OutlineColor",Color.red);
material.SetFloat("_OutlineWidth",.04f);material.SetFloat("_OutlineDistAdaptive",0);ShaderUtil.CompilePass(material,0,true);
var objects=new System.Collections.Generic.List<UnityEngine.Object>();objects.Add(material);
System.Func<Mesh,Matrix4x4,Color32[]> render=(mesh,model)=>{
 var rt=new RenderTexture(256,256,24,RenderTextureFormat.ARGB32);rt.Create();
 var texture=new Texture2D(256,256,TextureFormat.RGBA32,false);
 var command=new UnityEngine.Rendering.CommandBuffer();
 var eye=new Vector3(.8f,.7f,1.2f).normalized*2.2f;
 var camera=Matrix4x4.TRS(eye,Quaternion.LookRotation(-eye,Vector3.up),Vector3.one).inverse;
 var view=Matrix4x4.Scale(new Vector3(1,1,-1))*camera;
 var projection=GL.GetGPUProjectionMatrix(Matrix4x4.Perspective(40,1,.01f,10),true);
 command.SetRenderTarget(rt);command.ClearRenderTarget(true,true,Color.black);
 command.SetViewProjectionMatrices(view,projection);command.DrawMesh(mesh,model,material,0,0);
 Graphics.ExecuteCommandBuffer(command);
 var old=RenderTexture.active;RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,256,256),0,0);texture.Apply();RenderTexture.active=old;
 var pixels=texture.GetPixels32();command.Release();UnityEngine.Object.DestroyImmediate(texture);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
 return pixels;
};
System.Func<Mesh,Vector3[],string> pixels=(mesh,expected)=>{
 var reference=UnityEngine.Object.Instantiate(mesh);reference.normals=expected;reference.SetUVs(1,new Vector3[reference.vertexCount]);reference.SetUVs(2,new Vector2[reference.vertexCount]);reference.tangents=new Vector4[reference.vertexCount];
 var bounds=mesh.bounds;var scale=.65f/Mathf.Max(bounds.extents.x,Mathf.Max(bounds.extents.y,bounds.extents.z));
 var model=Matrix4x4.Scale(Vector3.one*scale)*Matrix4x4.Translate(-bounds.center);
 var actual=render(mesh,model);var target=render(reference,model);UnityEngine.Object.DestroyImmediate(reference);
 int difference=0,active=0;for(int i=0;i<actual.Length;i++){if(actual[i].r>0)active++;if(actual[i].r!=target[i].r)difference++;}
 if(active<100)throw new System.Exception("No meaningful shader pixels: "+active);
 if(difference>8)throw new System.Exception("Shader/reference pixel mismatch="+difference);
 return "activePixels="+active+" pixelDiff="+difference;
};
var results=new System.Collections.Generic.List<string>();
try{
 var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/__TLUVChannelValidation/skin.fbx");
 var instance=UnityEngine.Object.Instantiate(asset);objects.Add(instance);
 var skin=instance.GetComponentInChildren<SkinnedMeshRenderer>();if(skin==null)throw new System.Exception("Imported Skin missing");
 var original=new Mesh();var baked=new Mesh();objects.Add(original);objects.Add(baked);
 skin.BakeMesh(original);var before=Enumerable.Range(0,original.vertexCount).Select(i=>decode(original,i)).ToArray();
 var bone=skin.bones[0];var initial=bone.localRotation;
 bone.localRotation=Quaternion.AngleAxis(90,Vector3.forward)*initial;skin.BakeMesh(baked);
 var first=Quaternion.FromToRotation(original.normals[0],baked.normals[0]);
 // Single bone rigid rotation: derive the common normal transform from the full tangent frame.
 var oldN=original.normals[0].normalized;var oldT=new Vector3(original.tangents[0].x,original.tangents[0].y,original.tangents[0].z).normalized;
 var newN=baked.normals[0].normalized;var newT=new Vector3(baked.tangents[0].x,baked.tangents[0].y,baked.tangents[0].z).normalized;
 var rotation=Quaternion.LookRotation(newN,newT)*Quaternion.Inverse(Quaternion.LookRotation(oldN,oldT));
 var expected=before.Select(x=>rotation*x).ToArray();float minDot=1;
 for(int i=0;i<baked.vertexCount;i++)minDot=Mathf.Min(minDot,Vector3.Dot(decode(baked,i),expected[i]));
 if(minDot<.99999f)throw new System.Exception("Animated direction dot="+minDot);
 if(Vector3.Dot(oldN,newN)>.99f)throw new System.Exception("Bone did not rotate test mesh");
 results.Add("Imported Skin Z90: minDot="+minDot+" "+pixels(baked,expected));
 var source=AssetDatabase.LoadAllAssetsAtPath("Assets/__TLUVChannelValidation/cube.fbx").OfType<Mesh>().Single();
 var parent=new GameObject("__TangentBatchCheck");objects.Add(parent);
 var expectedByPosition=new System.Collections.Generic.Dictionary<Vector3,System.Collections.Generic.List<Vector3>>();
 for(int k=0;k<2;k++){
  var go=new GameObject("Batch"+k);go.transform.SetParent(parent.transform);go.transform.position=new Vector3(k*.3f,0,0);
  go.transform.rotation=Quaternion.Euler(k*25,k*30,k*90);go.AddComponent<MeshFilter>().sharedMesh=source;
  go.AddComponent<MeshRenderer>().sharedMaterial=material;
  for(int i=0;i<source.vertexCount;i++){
   var position=go.transform.TransformPoint(source.vertices[i]);if(!expectedByPosition.ContainsKey(position))expectedByPosition[position]=new System.Collections.Generic.List<Vector3>();
   expectedByPosition[position].Add(go.transform.TransformDirection(decode(source,i)).normalized);
  }
 }
 StaticBatchingUtility.Combine(parent);
 var renderer=parent.GetComponentsInChildren<MeshRenderer>()[0];if(!renderer.isPartOfStaticBatch)throw new System.Exception("Static combine failed");
 var combined=renderer.GetComponent<MeshFilter>().sharedMesh;objects.Add(combined);
 var expectedBatch=new Vector3[combined.vertexCount];minDot=1;
 for(int i=0;i<combined.vertexCount;i++){
  var position=combined.vertices[i];var key=expectedByPosition.Keys.OrderBy(x=>(x-position).sqrMagnitude).First();
  if((key-position).sqrMagnitude>.00000001f)throw new System.Exception("Combined position lookup failed");
  var direction=decode(combined,i);var expectedDirection=expectedByPosition[key].OrderByDescending(x=>Vector3.Dot(x,direction)).First();
  expectedBatch[i]=expectedDirection;minDot=Mathf.Min(minDot,Vector3.Dot(direction,expectedDirection));
 }
 if(minDot<.99999f)throw new System.Exception("Static batch direction dot="+minDot);
 results.Add("Rotated static batch: minDot="+minDot+" "+pixels(combined,expectedBatch));
 var output=string.Join("\n",results);System.IO.File.WriteAllText(System.IO.Path.Combine(artifacts,"tangent_deformation_unity_result.txt"),output);return output;
}finally{
 foreach(var value in objects)if(value!=null)UnityEngine.Object.DestroyImmediate(value);
}
