using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace LucidLoop.CharacterArt.Editor
{
    public static class RenDesignerEyeReviewBuilder
    {
        const string Root="Assets/CharacterArt/Generated/RenDesignerEyeReview";
        const string Scene="Assets/CharacterArt/Generated/Preview/Scenes/RenDesignerEyeReview.unity";
        [Serializable] sealed class Eye { public string name,cornersAsset,cornersSha256;public int vertices,triangles,cornerCount;public float[] colorMin,colorMax;public bool skin; }
        [Serializable] sealed class Config { public string sourceSha256,sourceContractSha256,sourceFbxAsset;public float[] nativeToReviewWorld;public Eye[] eyes; }
        [Serializable] sealed class ColorAudit { public string name,format,cornersSha256;public int vertices,triangles,referencedVertices,unusedVertices;public float[] min,max;public float rangeError,maximumReferencedColorError,maximumMatchedPositionError,maximumMatchedUvError; }
        [Serializable] sealed class ColorRow {public int index;public bool referenced;public float[] color;}
        [Serializable] sealed class ColorDump {public string name;public int vertices,referencedVertices;public ColorRow[] colors;}
        public static void DumpImportedColors()
        {
            var config=JsonUtility.FromJson<Config>(File.ReadAllText(Root+"/Manifest.json"));
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(config.sourceFbxAsset);var output=Argument("-renDesignerAuditOutput");Directory.CreateDirectory(output);
            foreach(var filter in asset.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh=filter.sharedMesh;var used=new HashSet<int>(Enumerable.Range(0,mesh.subMeshCount).SelectMany(i=>mesh.GetIndices(i)));var colors=mesh.colors;
                File.WriteAllText(Path.Combine(output,filter.name+".json"),JsonUtility.ToJson(new ColorDump{name=filter.name,vertices=mesh.vertexCount,referencedVertices=used.Count,colors=colors.Select((c,i)=>new ColorRow{index=i,referenced=used.Contains(i),color=new[]{c.r,c.g,c.b,c.a}}).ToArray()},true));
            }
            Debug.Log("REN_DESIGNER_COLOR_DUMP_OK: "+output);
        }
        [Serializable] sealed class Audit { public string status="ACTUAL_UNITY_NEUTRAL_IMPORT_CALIBRATED",sourceSha256,sourceContractSha256,headSha256,unityVersion;public float markerError;public ColorAudit[] eyes;public float[] nativeToUnity;public string[] oldEyesDisabled;public string limits="Neutral only. Historical H source morphs unchanged. No canonical endpoint restoration. Color min/max audit permits documented Unity UNorm8 quantization within one linear byte; source FBX itself is float color."; }
        public static void BuildWindowsViewer()
        {
            if(!Application.isBatchMode||Application.dataPath.IndexOf("LucidLoopScratch/ren-eye-import-verification",StringComparison.OrdinalIgnoreCase)<0)throw new InvalidOperationException("Scratch only.");
            var config=JsonUtility.FromJson<Config>(File.ReadAllText(Root+"/Manifest.json"));Verify(config.sourceFbxAsset,config.sourceSha256);
            AssetDatabase.ImportAsset(config.sourceFbxAsset,ImportAssetOptions.ForceSynchronousImport);
            var importer=(ModelImporter)AssetImporter.GetAtPath(config.sourceFbxAsset);importer.isReadable=true;importer.importAnimation=false;importer.importBlendShapes=false;importer.animationType=ModelImporterAnimationType.None;importer.materialImportMode=ModelImporterMaterialImportMode.None;
            importer.meshCompression=ModelImporterMeshCompression.Off;importer.optimizeMeshVertices=false;importer.optimizeMeshPolygons=false;importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.Import;importer.SaveAndReimport();
            var scene=EditorSceneManager.OpenScene("Assets/CharacterArt/Generated/Preview/Scenes/RenTokonReview.unity",OpenSceneMode.Single);
            var previous=Object.FindFirstObjectByType<RenTokonReviewController>();var old=previous.Controller;
            Verify("Assets/CharacterArt/Generated/RenHReferenceAnimation/Sources/Ren_H_CompleteHead_Review.fbx","073e47cc05a2daea1db7c25152cc28a0c51b820c0a40153727f767285c4293ad");
            var owner=old.gameObject;var model=old.ModelRoot;var head=old.HeadRenderer;var headFrame=old.AnimatedHeadFrame;var camera=old.ModelCamera;var artist=old.OriginalArtist;var pipeline=old.ReviewPipeline;
            var neutral=old.NeutralLights;var club=old.ClubLights;var target=old.CameraTarget;var distance=old.CameraDistance;var fov=old.CameraFieldOfView;
            var values=JsonUtility.FromJson<RenHReferenceTimeline>(old.TimelineAsset.text).channels.ToDictionary(c=>c.name,c=>c.rest);
            foreach(var binding in old.MorphBindings)
            {var skin=model.Find(binding.rendererPath).GetComponent<SkinnedMeshRenderer>();skin.SetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex(binding.shape),RenHReferenceAnimationController.EvaluateMorph(binding,values));}
            var hidden=model.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.StartsWith("Ren_H_Eye_",StringComparison.Ordinal)).ToArray();foreach(var r in hidden)r.enabled=false;
            var allMaterials=new List<Material>();Directory.CreateDirectory(Root+"/Materials");
            foreach(var slot in previous.Slots)
            {
                var m=new Material(slot.tokon);m.name="Selected-"+slot.tokon.name;
                if(slot.revisedControl)m.SetTexture("_ControlMap",slot.revisedControl);if(slot.revisedFaceMode>=0)m.SetFloat("_FaceMode",slot.revisedFaceMode);if(slot.capControl)m.SetTexture("_ControlMap",slot.capControl);
                m.SetFloat("_ClosedWeight",slot.closedEndpoint?1:0);m.SetFloat("_Unlit",0);
                var path=Root+"/Materials/"+m.name+".mat";var saved=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(saved){saved.CopyPropertiesFromMaterial(m);Object.DestroyImmediate(m);m=saved;}else AssetDatabase.CreateAsset(m,path);
                var mats=slot.renderer.sharedMaterials;mats[slot.slot]=m;slot.renderer.sharedMaterials=mats;allMaterials.Add(m);
            }
            var sourceRoot=model.Find("Source");if(!sourceRoot)throw new InvalidDataException("Expected original H Source subtree is absent.");
            var headNode=Find(sourceRoot,"Head");var sourceFrame=Matrix4x4.identity;
            sourceFrame.SetColumn(0,(Vector4)(Find(sourceRoot,"Ren_H_HeadAxis_X").position-headNode.position));sourceFrame.SetColumn(1,(Vector4)(Find(sourceRoot,"Ren_H_HeadAxis_Y").position-headNode.position));sourceFrame.SetColumn(2,(Vector4)(Find(sourceRoot,"Ren_H_HeadAxis_Z").position-headNode.position));sourceFrame.SetColumn(3,new Vector4(headNode.position.x,headNode.position.y,headNode.position.z,1));
            var sourceToUnity=sourceFrame*Matrix4x4.Translate(new Vector3(.05999999865889549f,0,.20000000298023224f));
            var nativeToUnity=sourceToUnity*Matrix(config.nativeToReviewWorld);
            var eye=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(config.sourceFbxAsset));eye.name="NeutralDesignerEyes";
            var importedOrigin=Find(eye.transform,"Ren_H_NativeAxis_Origin");var imported=Matrix4x4.identity;
            foreach(var item in new[]{("X",0),("Y",1),("Z",2)})imported.SetColumn(item.Item2,(Vector4)((Find(eye.transform,"Ren_H_NativeAxis_"+item.Item1).position-importedOrigin.position)/.01f));
            imported.SetColumn(3,new Vector4(importedOrigin.position.x,importedOrigin.position.y,importedOrigin.position.z,1));
            var wrapper=new GameObject("DesignerEyeRegistration").transform;wrapper.SetParent(model,false);eye.transform.SetParent(wrapper,false);
            SetMatrix(wrapper,model.worldToLocalMatrix*nativeToUnity*imported.inverse);
            var error=0f;foreach(var point in new[]{("Origin",Vector3.zero),("X",Vector3.right*.01f),("Y",Vector3.up*.01f),("Z",Vector3.forward*.01f)})error=Mathf.Max(error,Vector3.Distance(Find(eye.transform,"Ren_H_NativeAxis_"+point.Item1).position,nativeToUnity.MultiplyPoint3x4(point.Item2)));
            if(error>2e-5f)throw new InvalidDataException("Actual imported eye marker registration failed: "+error);
            var shader=Shader.Find("LucidLoop/Characters/RenTokonNPR");var presets=JsonUtility.FromJson<RenNprPresets>(File.ReadAllText("Assets/CharacterArt/Generated/RenTokonReview/Contracts/ren-tokon-presets.json"));
            var audits=new List<ColorAudit>();var graphic=new List<Material>();
            foreach(var spec in config.eyes)
            {
                var r=Find(eye.transform,spec.name).GetComponent<Renderer>();var mesh=r.GetComponent<MeshFilter>().sharedMesh;var colors=mesh.colors;
                if(colors.Length!=mesh.vertexCount||mesh.blendShapeCount!=0)throw new InvalidDataException("Neutral eye color/control contract mismatch: "+r.name);
                var used=new HashSet<int>(Enumerable.Range(0,mesh.subMeshCount).SelectMany(s=>mesh.GetIndices(s)));var rendered=used.Select(i=>colors[i]).ToArray();
                var min=Enumerable.Range(0,4).Select(c=>rendered.Min(x=>x[c])).ToArray();var max=Enumerable.Range(0,4).Select(c=>rendered.Max(x=>x[c])).ToArray();
                var rangeError=Enumerable.Range(0,4).SelectMany(c=>new[]{Mathf.Abs(min[c]-spec.colorMin[c]),Mathf.Abs(max[c]-spec.colorMax[c])}).Max();
                if(rangeError>1f/255+.00002f||rendered.Any(c=>c.a!=1))throw new InvalidDataException("Imported linear vertex color range mismatch: "+r.name+" "+rangeError);
                var triangles=Enumerable.Range(0,mesh.subMeshCount).Sum(s=>(int)mesh.GetIndexCount(s))/3;if(triangles!=spec.triangles)throw new InvalidDataException("Neutral eye triangle mismatch: "+r.name);
                var audit=new ColorAudit{name=r.name,vertices=mesh.vertexCount,triangles=triangles,min=min,max=max,rangeError=rangeError,format=mesh.GetVertexAttributeFormat(VertexAttribute.Color).ToString(),referencedVertices=used.Count,unusedVertices=mesh.vertexCount-used.Count,cornersSha256=spec.cornersSha256};
                VerifyCorners(spec,mesh,nativeToUnity.inverse*r.transform.localToWorldMatrix,used,audit);audits.Add(audit);
                var m=new Material(shader){name=r.name};var preset=presets.materials.Single(p=>p.sourceName==(spec.skin?"Skin":"Eye"));
                foreach(var p in preset.floats)m.SetFloat(p.name,p.value);foreach(var p in preset.colors)m.SetVector(p.name,new Vector4(p.linearRgba[0],p.linearRgba[1],p.linearRgba[2],p.linearRgba[3]));
                m.SetVector("_BaseColor",Vector4.one);m.SetFloat("_UseVertexColor",1);m.SetFloat("_VertexColorSrgb",0);m.SetFloat("_UseBaseMap",0);m.SetFloat("_UseControlMap",0);m.SetFloat("_UseShadowMap",0);m.SetFloat("_ClosedWeight",0);
                m.SetVector("_ControlFallback",spec.skin?new Vector4(1,0,0,1):Vector4.zero);m.SetFloat("_FaceMode",spec.skin?.90f:0);m.SetFloat("_HighlightStrength",0);m.SetFloat("_RimStrength",0);m.SetFloat("_Cull",0);m.SetFloat("_Unlit",spec.skin?0:1);
                var path=Root+"/Materials/"+r.name+".mat";var saved=AssetDatabase.LoadAssetAtPath<Material>(path);if(saved){saved.CopyPropertiesFromMaterial(m);Object.DestroyImmediate(m);m=saved;}else AssetDatabase.CreateAsset(m,path);
                r.sharedMaterials=new[]{m};r.shadowCastingMode=spec.skin?ShadowCastingMode.On:ShadowCastingMode.Off;allMaterials.Add(m);if(!spec.skin)graphic.Add(m);
            }
            if(audits.Count!=22||audits.Sum(x=>x.triangles)!=5145)throw new InvalidDataException("Incomplete neutral eye set.");
            foreach(var outline in Object.FindObjectsByType<RenTokonOutlineController>(FindObjectsInactive.Include,FindObjectsSortMode.None))Object.DestroyImmediate(outline);
            foreach(var r in model.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.StartsWith("RenInk_",StringComparison.Ordinal)))r.gameObject.SetActive(false);
            foreach(var video in Object.FindObjectsByType<VideoPlayer>(FindObjectsInactive.Include,FindObjectsSortMode.None))Object.DestroyImmediate(video);
            if(old.CapAttachment)Object.DestroyImmediate(old.CapAttachment);Object.DestroyImmediate(previous);Object.DestroyImmediate(old);
            foreach(var probe in Object.FindObjectsByType<RenHReferenceCaptureProbe>(FindObjectsInactive.Include,FindObjectsSortMode.None))Object.DestroyImmediate(probe);
            var review=owner.AddComponent<RenDesignerEyeReview>();review.ModelRoot=model;review.Head=head;review.FaceFrame=headFrame;review.ModelCamera=camera;review.Artist=review.Sketch=artist;review.Pipeline=pipeline;review.NeutralLights=neutral;review.ClubLights=club;review.Target=target;review.Distance=distance;review.FieldOfView=fov;
            review.OldEyes=hidden;review.NewEyes=eye.GetComponentsInChildren<Renderer>(true);review.Cap=Find(sourceRoot,"Ren_Cap").GetComponent<Renderer>();review.Hair=sourceRoot.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.StartsWith("Ren_Hair_H_",StringComparison.Ordinal)).ToArray();
            review.Materials=allMaterials.Distinct().ToArray();review.GraphicMaterials=graphic.ToArray();review.SourceSha256=config.sourceSha256;review.RuntimeSha256=Hash("Assets/CharacterArt/Runtime/RenDesignerEyeReview.cs");
            review.SourceCameraPosition=nativeToUnity.MultiplyPoint3x4(new Vector3(-1.153372328f,-2.907813828f,-.319281800f));review.SourceCameraTarget=nativeToUnity.MultiplyPoint3x4(new Vector3(.697887172f,-.603118250f,.191811689f));review.SourceCameraUp=nativeToUnity.MultiplyVector(new Vector3(-.416867578f,.135530245f,.898806417f)).normalized;review.SourceCameraOrthoSize=3.155528038f*nativeToUnity.MultiplyVector(Vector3.right).magnitude/3;
            var auditPath=Root+"/ImportAudit.json";File.WriteAllText(auditPath,JsonUtility.ToJson(new Audit{sourceSha256=config.sourceSha256,sourceContractSha256=config.sourceContractSha256,headSha256="073e47cc05a2daea1db7c25152cc28a0c51b820c0a40153727f767285c4293ad",unityVersion=Application.unityVersion,markerError=error,eyes=audits.ToArray(),nativeToUnity=Enumerable.Range(0,16).Select(i=>nativeToUnity[i/4,i%4]).ToArray(),oldEyesDisabled=hidden.Select(r=>r.name).ToArray()},true));review.ImportAuditSha256=Hash(auditPath);
            if(!EditorSceneManager.SaveScene(scene,Scene))throw new IOException("Scene save failed.");AssetDatabase.SaveAssets();
            var output=Argument("-renDesignerPlayerOutput");if(string.IsNullOrEmpty(output))throw new InvalidDataException("Explicit new player output required.");Directory.CreateDirectory(Path.GetDirectoryName(output));
            var before=GraphicsSettings.defaultRenderPipeline;var quality=QualitySettings.renderPipeline;
            try
            {
                GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{GraphicsDeviceType.Direct3D11});
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},target=BuildTarget.StandaloneWindows64,locationPathName=output,options=BuildOptions.Development});if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Designer player build failed.");
            }
            finally{GraphicsSettings.defaultRenderPipeline=before;QualitySettings.renderPipeline=quality;AssetDatabase.SaveAssets();}
            Debug.Log("REN_DESIGNER_BUILD_OK: "+output);
        }
        static Matrix4x4 Matrix(float[] values){var m=Matrix4x4.identity;for(var i=0;i<16;i++)m[i/4,i%4]=values[i];return m;}
        sealed class Corner {public Vector3 position;public Vector2 uv;public Color color;}
        static (int,int,int) Key(Vector3 p)=>(Mathf.FloorToInt(p.x*100000),Mathf.FloorToInt(p.y*100000),Mathf.FloorToInt(p.z*100000));
        static void VerifyCorners(Eye spec,Mesh mesh,Matrix4x4 toNative,HashSet<int> used,ColorAudit audit)
        {
            Verify(spec.cornersAsset,spec.cornersSha256);var buckets=new Dictionary<(int,int,int),List<Corner>>();
            using(var reader=new BinaryReader(File.OpenRead(spec.cornersAsset)))
                for(var i=0;i<spec.cornerCount;i++)
                {
                    var c=new Corner{position=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle()),uv=new Vector2(reader.ReadSingle(),reader.ReadSingle()),color=new Color(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle())};
                    var key=Key(c.position);if(!buckets.TryGetValue(key,out var list)){list=new List<Corner>();buckets.Add(key,list);}list.Add(c);
                }
            var vertices=mesh.vertices;var colors=mesh.colors;var uv=mesh.uv;
            foreach(var i in used)
            {
                var p=toNative.MultiplyPoint3x4(vertices[i]);var k=Key(p);var matches=new List<Corner>();
                for(var x=-1;x<=1;x++)for(var y=-1;y<=1;y++)for(var z=-1;z<=1;z++)
                    if(buckets.TryGetValue((k.Item1+x,k.Item2+y,k.Item3+z),out var rows))matches.AddRange(rows.Where(c=>Vector3.Distance(c.position,p)<=2e-6f&&Vector2.Distance(c.uv,uv[i])<=2e-5f));
                if(matches.Count==0)throw new InvalidDataException("No raw FBX corner position/UV match: "+spec.name+" vertex "+i+" native "+p.ToString("R"));
                var best=matches.OrderBy(c=>Enumerable.Range(0,4).Max(n=>Mathf.Abs(c.color[n]-colors[i][n]))).First();
                var error=Enumerable.Range(0,4).Max(n=>Mathf.Abs(best.color[n]-colors[i][n]));
                if(error>.5f/255+.00002f)throw new InvalidDataException("Rendered corner pigment mismatch: "+spec.name+" vertex "+i+" error "+error);
                audit.maximumReferencedColorError=Mathf.Max(audit.maximumReferencedColorError,error);audit.maximumMatchedPositionError=Mathf.Max(audit.maximumMatchedPositionError,Vector3.Distance(best.position,p));audit.maximumMatchedUvError=Mathf.Max(audit.maximumMatchedUvError,Vector2.Distance(best.uv,uv[i]));
            }
        }
        static void SetMatrix(Transform node,Matrix4x4 m)
        {
            var x=(Vector3)m.GetColumn(0);var y=(Vector3)m.GetColumn(1);var z=(Vector3)m.GetColumn(2);var sx=x.magnitude;if(Vector3.Dot(Vector3.Cross(x,y),z)<0)sx=-sx;
            node.localPosition=m.GetColumn(3);node.localRotation=Quaternion.LookRotation(z,y);node.localScale=new Vector3(sx,y.magnitude,z.magnitude);
            var actual=Matrix4x4.TRS(node.localPosition,node.localRotation,node.localScale);if(Enumerable.Range(0,16).Any(i=>Mathf.Abs(actual[i]-m[i])>1e-4f))throw new InvalidDataException("Eye registration matrix contains unsupported shear.");
        }
        static Transform Find(Transform root,string name)=>root.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
        static string Hash(string path){using(var s=File.OpenRead(path))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
        static void Verify(string path,string expected){if(Hash(path)!=expected)throw new InvalidDataException("Frozen input mismatch: "+path);}
        static string Argument(string name){var a=Environment.GetCommandLineArgs();var i=Array.IndexOf(a,name);return i>=0&&i+1<a.Length?a[i+1]:null;}
    }
}
