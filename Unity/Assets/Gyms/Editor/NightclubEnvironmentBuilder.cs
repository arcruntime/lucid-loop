using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace LucidLoop.Gyms.Editor
{
    [InitializeOnLoad]
    public static class NightclubEnvironmentBuilder
    {
        const string Assets="Assets/EnvironmentArt/Generated";
        const string ScenePath="Assets/Gyms/Scenes/BeforeTheDrop.unity";
        static string Root=>Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
        static string Request=>Path.Combine(Root,".local/environment-art/build.request");
        [Serializable] class MaterialSpec { public string name,texture;public float[] color;public float roughness,metallic,emission; }
        [Serializable] class MaterialList { public MaterialSpec[] materials; }
        [Serializable] class DressingObstacle { public string id;public float x,z,width,depth,height; }
        [Serializable] class DressingPlan { public DressingObstacle[] obstacles; }
        [Serializable] class Audit
        {
            public string scene;public long environmentTriangles,sceneTriangles,projectedWideTriangles;
            public int environmentRenderers,materialSlots,lights,missingMeshes,missingMaterials,missingScripts;
            public string[] notes;
        }
        static NightclubEnvironmentBuilder(){EditorApplication.update+=CheckRequest;}
        static void CheckRequest()
        {
            if(SessionState.GetBool("Nightclub.SmokePending",false)&&string.IsNullOrEmpty(SessionState.GetString("LucidLoop.Validation.Path",""))&&!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Environment.SetEnvironmentVariable("LUCID_LOOP_SMOKE_GAME_URL",SessionState.GetString("Nightclub.PreviousSmokeUrl","") is string url&&url.Length>0?url:null);
                SessionState.EraseBool("Nightclub.SmokePending");
            }
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            string command=File.ReadAllText(Request).Trim();File.Delete(Request);
            try
            {
                if(command=="build")Build();
                else if(command=="capture")Capture();
                else if(command=="reflection")BakeReflection();
                else if(command=="tune")Tune();
                else if(command=="look")ReviseLook();
                else if(command=="tests")GymValidation.RunEditMode();
                else if(command=="smoke")
                {
                    SessionState.SetString("Nightclub.PreviousSmokeUrl",Environment.GetEnvironmentVariable("LUCID_LOOP_SMOKE_GAME_URL")??"");
                    Environment.SetEnvironmentVariable("LUCID_LOOP_SMOKE_GAME_URL","ws://127.0.0.1:8796/game");
                    SessionState.SetBool("Nightclub.SmokePending",true);GymValidation.RunEncounterPlayMode();
                }
                else throw new InvalidOperationException("Unknown environment request.");
                File.WriteAllText(Path.Combine(Root,".local/environment-art/last-result.txt"),command+" OK "+DateTime.UtcNow.ToString("O"));
            }
            catch(Exception e){Debug.LogException(e);File.WriteAllText(Path.Combine(Root,".local/environment-art/last-result.txt"),e.ToString());}
        }
        static Scene OpenEncounter()
        {
            var scene=SceneManager.GetSceneByPath(ScenePath);
            if(scene.IsValid()&&scene.isLoaded)
            {
                if(scene.isDirty)throw new InvalidOperationException("BeforeTheDrop has unsaved edits; cannot replace its environment.");
                return scene;
            }
            // Other open scenes, including unsaved character-art work, stay loaded.
            return EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        }
        static T Find<T>(Scene s) where T:Component=>s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).FirstOrDefault();
        static Material CreateMaterial(MaterialSpec spec)
        {
            string path=Assets+"/"+spec.name+".mat";
            var shader=Shader.Find(spec.name=="VortexScreen"?"LucidLoop/Environment/ClubVortex":spec.name=="CityWindow"?"LucidLoop/Environment/CityWindow":"Universal Render Pipeline/Lit");
            if(!shader)throw new InvalidOperationException("Environment shader missing: "+spec.name);
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(shader);AssetDatabase.CreateAsset(m,path);}else m.shader=shader;
            var color=new Color(spec.color[0],spec.color[1],spec.color[2]);
            if(spec.name=="CityWindow")
            {
                m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Assets+"/MetropolitanNightSky.png"));
                m.SetFloat("_Exposure",1.25f);EditorUtility.SetDirty(m);return m;
            }
            m.SetColor("_BaseColor",color);
            if(spec.name!="VortexScreen")
            {
                m.SetFloat("_Smoothness",1-spec.roughness);m.SetFloat("_Metallic",spec.metallic);
                GymMaterialEmission.Configure(m,color*spec.emission);
                if(!string.IsNullOrEmpty(spec.texture))
                {
                    string texturePath=Assets+"/"+spec.texture;
                    var ti=AssetImporter.GetAtPath(texturePath) as TextureImporter;
                    if(ti)
                    {
                        ti.maxTextureSize=1024;ti.mipmapEnabled=true;ti.wrapMode=TextureWrapMode.Repeat;
                        ti.textureCompression=TextureImporterCompression.Compressed;
                        var ios=ti.GetPlatformTextureSettings("iPhone");ios.overridden=true;ios.maxTextureSize=1024;ios.format=TextureImporterFormat.ASTC_6x6;ti.SetPlatformTextureSettings(ios);
                        ti.SaveAndReimport();
                    }
                    m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));m.SetColor("_BaseColor",Color.white);
                }
                else m.SetTexture("_BaseMap",null);
                if(spec.name.StartsWith("areca")||spec.name.StartsWith("PalmLeaf"))m.SetFloat("_Cull",0);
                if(spec.name=="PartitionGlass")
                {
                    m.SetColor("_BaseColor",new Color(color.r,color.g,color.b,.18f));m.SetFloat("_Surface",1);m.SetFloat("_ZWrite",0);m.SetFloat("_Cull",0);
                    m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.SetOverrideTag("RenderType","Transparent");m.renderQueue=3000;
                }
            }
            EditorUtility.SetDirty(m);return m;
        }
        [MenuItem("Lucid Loop/Environment/Build dressed nightclub")]
        public static void BuildAndReflect(){Build();BakeReflection();Capture();}
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit play mode before dressing the club.");
            string fbx=Assets+"/Nightclub.fbx";
            AssetDatabase.ImportAsset(fbx,ImportAssetOptions.ForceSynchronousImport);
            var importer=(ModelImporter)AssetImporter.GetAtPath(fbx);
            importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.importAnimation=false;
            importer.isReadable=false;importer.meshCompression=ModelImporterMeshCompression.Low;
            importer.generateSecondaryUV=true;importer.SaveAndReimport();
            var specs=JsonUtility.FromJson<MaterialList>(File.ReadAllText(Assets+"/materials.json"));
            var mats=specs.materials.ToDictionary(s=>s.name,CreateMaterial);
            // FBX material names supply a stable external remap even with material creation disabled.
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            foreach(var pair in mats)importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),pair.Key),pair.Value);
            importer.SaveAndReimport();
            var s=OpenEncounter();SceneManager.SetActiveScene(s);
            var gym=Find<GymRoot>(s);if(!gym)throw new InvalidOperationException("Club root missing.");
            var old=gym.transform.Find("Environment");if(!old)throw new InvalidOperationException("Original collision environment missing.");
            var club=Find<ClubLighting>(s);
            foreach(var r in old.GetComponentsInChildren<Renderer>(true))
            {
                bool dancer=club&&club.Dancers.Any(d=>d&&(r.transform==d||r.transform.IsChildOf(d)));
                if(!dancer)r.enabled=false;
            }
            var previous=gym.transform.Find("Dressed Nightclub");if(previous)UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(fbx);if(!model)throw new InvalidOperationException("Nightclub FBX not imported.");
            var set=(GameObject)PrefabUtility.InstantiatePrefab(model,s);set.name="Dressed Nightclub";set.transform.SetParent(gym.transform,false);
            // Blender FBX handedness conversion mirrors authored X in Unity.
            set.transform.localScale=new Vector3(-1,1,1);
            foreach(var r in set.GetComponentsInChildren<MeshRenderer>())
            {
                r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;
                // Camera views can omit whole spatial zones; no static-batch duplication of the kit.
                r.lightProbeUsage=LightProbeUsage.BlendProbes;r.reflectionProbeUsage=ReflectionProbeUsage.Simple;
            }
            var presentation=set.AddComponent<ClubEnvironmentPresentation>();presentation.Coordinator=Find<EncounterCoordinator>(s);
            presentation.NeonMaterial=mats["MoodNeon"];presentation.ScreenMaterial=mats["VortexScreen"];
            var ceiling=set.AddComponent<ClubCeilingVisibility>();ceiling.Ceiling=set.GetComponentsInChildren<Renderer>().Where(r=>r.name=="Club_ceiling").ToArray();
            foreach(var renderer in ceiling.Ceiling)renderer.shadowCastingMode=ShadowCastingMode.Off;
            CreateBeams(set.transform,club);
            var plan=JsonUtility.FromJson<DressingPlan>(File.ReadAllText(Path.Combine(Root,"server/src/nightclub-dressing.json")));
            foreach(var o in plan.obstacles)Collider(set.transform,o.id,new Vector3(o.x,o.height/2,o.z),new Vector3(o.width,o.height,o.depth));
            Collider(set.transform,"VIP platform",new Vector3(10.175f,.175f,-.9f),new Vector3(6.05f,.35f,8.9f));
            for(int i=0;i<3;i++)
            {
                float h=(i+1)*.35f/3;Collider(set.transform,"VIP step "+i,new Vector3(6.775f+i*.15f,h/2,-.9f),new Vector3(.15f,h,8.9f));
            }
            // Original stool blockers were offset by one metre from the server plan.
            var stools=old.Cast<Transform>().Where(t=>t.name=="Bar stool").OrderBy(t=>t.localPosition.z).ToArray();
            for(int i=0;i<stools.Length;i++){var p=stools[i].localPosition;p.z=-4+i*2;stools[i].localPosition=p;}
            foreach(Transform t in old)
            {
                var p=t.localPosition;if(p.x<7.15f||p.z< -5.35f||p.z>3.55f)continue;
                if(t.name=="Table top")p.y=1.20f;
                else if(t.name=="Table base"||t.name=="Lounge seat")p.y=.70f;
                else if(t.name=="VIP sofa")p.y=.80f;
                else continue;
                t.localPosition=p;
            }
            foreach(var actor in presentation.Coordinator.Characters)
            {
                var p=actor.transform.position;p.y=ClubEnvironmentPresentation.FloorHeight(p.x,p.z);actor.transform.position=p;
            }
            var camera=Find<GymCamera>(s);camera.Pitch=45;camera.Yaw=-18;camera.OverviewSize=13.7f;camera.Immediate=true;camera.Apply(1);camera.Immediate=false;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.34f,.31f,.43f);RenderSettings.ambientEquatorColor=new Color(.21f,.19f,.28f);RenderSettings.ambientGroundColor=new Color(.09f,.075f,.13f);
            var volume=Find<Volume>(s);
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Assets+"/NightclubAtmosphere.asset");
            if(!profile)
            {
                profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,Assets+"/NightclubAtmosphere.asset");
                var bloom=profile.Add<Bloom>(true);bloom.intensity.Override(.8f);bloom.threshold.Override(.9f);bloom.scatter.Override(.7f);AssetDatabase.AddObjectToAsset(bloom,profile);
                var tone=profile.Add<Tonemapping>(true);tone.mode.Override(TonemappingMode.ACES);AssetDatabase.AddObjectToAsset(tone,profile);
            }
            if(volume)volume.sharedProfile=profile;
            var key=s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>()).First(l=>l.type==LightType.Directional);
            key.intensity=.85f;key.color=new Color(.72f,.76f,1);
            ceiling.Key=key;ceiling.Haze=set.GetComponentsInChildren<Renderer>().Where(r=>r.name=="Fixture haze").ToArray();
            ConfigureLook(s,set.transform);
            // Save source-approved violet/cyan composition; authoritative mood takes over at runtime.
            if(!EditorSceneManager.SaveScene(s))throw new IOException("Could not save dressed encounter.");
            AssetDatabase.SaveAssets();WriteAudit(s,set);Capture();
            Debug.Log("NIGHTCLUB_ENVIRONMENT_BUILT: "+ScenePath);
        }
        static void Collider(Transform set,string name,Vector3 center,Vector3 size)
        {
            var go=new GameObject("Collision "+name);go.transform.SetParent(set,false);
            // Root X is mirrored to compensate FBX; colliders are already in Unity coordinates.
            go.transform.localPosition=new Vector3(-center.x,center.y,center.z);
            go.transform.localScale=new Vector3(-1,1,1);
            var box=go.AddComponent<BoxCollider>();box.size=size;
        }
        static void CreateBeams(Transform set,ClubLighting club)
        {
            const string meshPath=Assets+"/BeamMesh.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(!mesh)
            {
                mesh=new Mesh{name="Bounded eight-sided haze beam"};var v=new Vector3[18];var uv=new Vector2[18];var tris=new List<int>();
                for(int ring=0;ring<2;ring++)for(int i=0;i<=8;i++)
                {
                    float a=i*Mathf.PI/4;int k=ring*9+i;float radius=ring==0?.13f:2.6f;
                    v[k]=new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,ring*8);uv[k]=new Vector2(i/8f,ring);
                    if(ring==0&&i<8)tris.AddRange(new[]{i,i+1,i+10,i,i+10,i+9});
                }
                mesh.vertices=v;mesh.uv=uv;mesh.triangles=tris.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,meshPath);
            }
            var m=AssetDatabase.LoadAssetAtPath<Material>(Assets+"/BeamHaze.mat");
            if(!m){m=new Material(Shader.Find("LucidLoop/Environment/Beam"));AssetDatabase.CreateAsset(m,Assets+"/BeamHaze.mat");}
            m.SetFloat("_Opacity",.012f);EditorUtility.SetDirty(m);
            foreach(var fixture in club.Lights.Where(l=>l&&l.type==LightType.Spot))
            {
                var go=new GameObject("Fixture haze");go.transform.SetParent(set,false);go.transform.localScale=new Vector3(-1,1,1);go.transform.SetPositionAndRotation(fixture.transform.position,fixture.transform.rotation);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=m;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
                go.AddComponent<ClubBeamMotion>().Fixture=fixture;
            }
        }
        static void Tune()
        {
            var scene=OpenEncounter();SceneManager.SetActiveScene(scene);
            var set=Find<GymRoot>(scene).transform.Find("Dressed Nightclub");var ceiling=set.GetComponent<ClubCeilingVisibility>();
            ceiling.Key=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>()).First(l=>l.type==LightType.Directional);
            ceiling.Haze=set.GetComponentsInChildren<Renderer>().Where(r=>r.name=="Fixture haze").ToArray();
            foreach(var renderer in ceiling.Ceiling)renderer.shadowCastingMode=ShadowCastingMode.Off;
            foreach(var collider in set.GetComponentsInChildren<BoxCollider>())collider.transform.localScale=new Vector3(-1,1,1);
            var material=AssetDatabase.LoadAssetAtPath<Material>(Assets+"/BeamHaze.mat");material.SetFloat("_Opacity",.012f);EditorUtility.SetDirty(material);
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();WriteAudit(scene,set.gameObject);Capture();
        }
        [MenuItem("Lucid Loop/Environment/Revise reference lighting and skyline")]
        public static void ReviseLook()
        {
            var scene=OpenEncounter();SceneManager.SetActiveScene(scene);
            var set=Find<GymRoot>(scene).transform.Find("Dressed Nightclub");
            ConfigureLook(scene,set);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();WriteAudit(scene,set.gameObject);Capture();
        }
        static void ConfigureLook(Scene scene,Transform set)
        {
            var previous=set.Find("Practical lighting");if(previous)UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var group=new GameObject("Practical lighting");group.transform.SetParent(set,false);group.transform.localScale=new Vector3(-1,1,1);
            var focusProfile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Assets+"/ConversationFocus.asset");
            if(!focusProfile)
            {
                focusProfile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(focusProfile,Assets+"/ConversationFocus.asset");
                var dof=focusProfile.Add<DepthOfField>(true);dof.mode.Override(DepthOfFieldMode.Gaussian);
                dof.gaussianStart.Override(4.5f);dof.gaussianEnd.Override(11);dof.gaussianMaxRadius.Override(1);dof.highQualitySampling.Override(false);
                AssetDatabase.AddObjectToAsset(dof,focusProfile);
            }
            var focus=group.AddComponent<Volume>();focus.isGlobal=true;focus.priority=20;focus.weight=0;focus.sharedProfile=focusProfile;
            set.GetComponent<ClubCeilingVisibility>().ConversationFocus=focus;
            // Small unshadowed pools supplement the existing dance fixtures. Each light
            // reaches only its bar bay or seating group, rather than the entire room.
            foreach(float z in new[]{-3.8f,.6f,5f})
            {
                Practical(group.transform,"Blue bottle shelf",new Vector3(-12.50f,2.45f,z),new Color(.12f,.40f,1),5.5f,4.3f);
                Practical(group.transform,"Amber bar candle",new Vector3(-9.5f,1.94f,z+.65f),new Color(1,.48f,.17f),2.8f,3.3f);
            }
            foreach(var p in new[]{new Vector3(9,1.9f,1),new Vector3(9,1.9f,-3),new Vector3(-7,1.55f,-8),new Vector3(6,1.55f,-8)})
                Practical(group.transform,"Amber lounge lamp",p,new Color(1,.48f,.19f),4.2f,4.2f);
            foreach(var p in new[]{new Vector3(-12.7f,1.0f,8.6f),new Vector3(12.7f,1,8.6f),new Vector3(12.7f,1,-6.2f)})
                Practical(group.transform,"Plant uplight",p,new Color(1,.61f,.30f),2.2f,3.8f);
            var key=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>()).First(l=>l.type==LightType.Directional);
            key.intensity=.65f;key.color=new Color(.80f,.83f,1);
            var screen=AssetDatabase.LoadAssetAtPath<Material>(Assets+"/VortexScreen.mat");
            if(screen){screen.SetFloat("_Energy",.45f);EditorUtility.SetDirty(screen);}
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.43f,.40f,.48f);
            RenderSettings.ambientEquatorColor=new Color(.29f,.25f,.31f);
            RenderSettings.ambientGroundColor=new Color(.13f,.10f,.16f);
            var ti=AssetImporter.GetAtPath(Assets+"/MetropolitanNightSky.png") as TextureImporter;
            if(ti)
            {
                ti.maxTextureSize=2048;ti.mipmapEnabled=true;ti.wrapModeU=TextureWrapMode.Repeat;ti.wrapModeV=TextureWrapMode.Clamp;
                var ios=ti.GetPlatformTextureSettings("iPhone");ios.overridden=true;ios.maxTextureSize=2048;ios.format=TextureImporterFormat.ASTC_6x6;ti.SetPlatformTextureSettings(ios);ti.SaveAndReimport();
            }
            var sky=AssetDatabase.LoadAssetAtPath<Material>(Assets+"/MetropolitanNightSky.mat");
            if(!sky){sky=new Material(Shader.Find("Skybox/Panoramic"));AssetDatabase.CreateAsset(sky,Assets+"/MetropolitanNightSky.mat");}
            sky.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Assets+"/MetropolitanNightSky.png"));
            sky.SetFloat("_Exposure",.8f);sky.SetFloat("_Rotation",25);EditorUtility.SetDirty(sky);RenderSettings.skybox=sky;
            var cam=Find<GymCamera>(scene).Camera;cam.clearFlags=CameraClearFlags.Skybox;
            var rig=Find<GymCamera>(scene);rig.Pitch=42;rig.Yaw=-38;rig.OverviewSize=11.8f;rig.Immediate=true;rig.Apply(1);rig.Immediate=false;
            var amber=AssetDatabase.LoadAssetAtPath<Material>(Assets+"/AmberPractical.mat");
            if(amber){GymMaterialEmission.Configure(amber,new Color(1,.47f,.12f)*1.15f);EditorUtility.SetDirty(amber);}
        }
        static void Practical(Transform parent,string name,Vector3 position,Color color,float intensity,float range)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;
            var light=go.AddComponent<Light>();light.type=LightType.Point;light.color=color;light.intensity=intensity;light.range=range;light.shadows=LightShadows.None;
        }
        [MenuItem("Lucid Loop/Environment/Bake neutral room reflection")]
        public static void BakeReflection()
        {
            var scene=OpenEncounter();SceneManager.SetActiveScene(scene);
            var set=Find<GymRoot>(scene).transform.Find("Dressed Nightclub");
            var probe=set.GetComponentInChildren<ReflectionProbe>();
            if(!probe)
            {
                var go=new GameObject("Baked neutral room reflection");go.transform.SetParent(set,false);go.transform.position=new Vector3(0,2,0);probe=go.AddComponent<ReflectionProbe>();
            }
            probe.mode=ReflectionProbeMode.Baked;probe.resolution=128;probe.size=new Vector3(30,8,26);probe.center=Vector3.zero;probe.intensity=.7f;probe.cullingMask=1;probe.hdr=true;probe.clearFlags=ReflectionProbeClearFlags.SolidColor;probe.backgroundColor=new Color(.035f,.03f,.045f);
            var renderers=set.GetComponentsInChildren<Renderer>();
            foreach(var r in renderers)GameObjectUtility.SetStaticEditorFlags(r.gameObject,StaticEditorFlags.ReflectionProbeStatic);
            var materials=renderers.SelectMany(r=>r.sharedMaterials).Distinct().Where(m=>m).ToArray();
            var emissions=materials.Select(m=>m.HasProperty("_EmissionColor")?m.GetColor("_EmissionColor"):Color.black).ToArray();
            var display=materials.First(m=>m.name=="VortexScreen");var accent=display.GetColor("_AccentColor");var baseColor=display.GetColor("_BaseColor");float energy=display.GetFloat("_Energy");
            var lights=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>()).ToArray();var colors=lights.Select(l=>l.color).ToArray();
            var disabled=new List<GameObject>();
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i)!=scene)
                foreach(var g in SceneManager.GetSceneAt(i).GetRootGameObjects())if(g.activeSelf){g.SetActive(false);disabled.Add(g);}
            string path=Assets+"/NeutralRoomReflection.exr";
            try
            {
                for(int i=0;i<materials.Length;i++)if(materials[i].HasProperty("_EmissionColor"))materials[i].SetColor("_EmissionColor",Color.white*Mathf.Min(.35f,emissions[i].maxColorComponent));
                display.SetColor("_AccentColor",Color.gray*.4f);display.SetColor("_BaseColor",Color.gray*.4f);display.SetFloat("_Energy",.35f);
                foreach(var light in lights)light.color=new Color(.8f,.8f,.85f);
                if(!Lightmapping.BakeReflectionProbe(probe,path))throw new InvalidOperationException("Neutral reflection bake failed.");
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                probe.bakedTexture=AssetDatabase.LoadAssetAtPath<Cubemap>(path);
                if(!probe.bakedTexture)throw new InvalidOperationException("Reflection cubemap did not import.");
            }
            finally
            {
                for(int i=0;i<materials.Length;i++)if(materials[i].HasProperty("_EmissionColor"))materials[i].SetColor("_EmissionColor",emissions[i]);
                display.SetColor("_AccentColor",accent);display.SetColor("_BaseColor",baseColor);display.SetFloat("_Energy",energy);
                for(int i=0;i<lights.Length;i++)lights[i].color=colors[i];foreach(var g in disabled)if(g)g.SetActive(true);
            }
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Capture();
        }
        static long Tris(Renderer r)
        {
            var filter=r.GetComponent<MeshFilter>();
            var mesh=r is SkinnedMeshRenderer skin?skin.sharedMesh:filter?filter.sharedMesh:null;
            if(!mesh)return 0;
            long sum=0;for(int i=0;i<mesh.subMeshCount;i++)if(mesh.GetTopology(i)==MeshTopology.Triangles)sum+=mesh.GetIndexCount(i)/3;return sum;
        }
        static void WriteAudit(Scene scene,GameObject set)
        {
            var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)).ToArray();
            var env=set.GetComponentsInChildren<Renderer>();var club=Find<ClubLighting>(scene);
            var audit=new Audit{scene=ScenePath,environmentTriangles=env.Sum(Tris),environmentRenderers=env.Length,materialSlots=env.Sum(r=>r.sharedMaterials.Length),
                sceneTriangles=all.Where(r=>r.enabled&&r.gameObject.activeInHierarchy).Sum(Tris),lights=scene.GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<Light>().Length),
                missingMeshes=env.Count(r=>!r.GetComponent<MeshFilter>()||!r.GetComponent<MeshFilter>().sharedMesh),missingMaterials=env.Sum(r=>r.sharedMaterials.Count(m=>!m||!m.shader||!m.shader.isSupported)),
                missingScripts=scene.GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))),
                notes=new[]{"Triangle totals count all dressed environment meshes, including zones outside the camera.","Projected wide frame reserves five 12k main cast, twelve 6k dancers and a 6k affair partner.","Current encounter still uses its existing placeholder characters; this change supplies environment art.","Physical iPhone frame time, thermal and memory acceptance require device profiling."}};
            audit.projectedWideTriangles=audit.environmentTriangles+5*12000+12*6000+6000;
            string dir=Path.Combine(Root,"docs/validation/nightclub-v2");Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir,"geometry-audit.json"),JsonUtility.ToJson(audit,true));
            if(audit.missingMeshes+audit.missingMaterials+audit.missingScripts>0)throw new InvalidOperationException("Environment import audit failed.");
            if(audit.environmentTriangles>125000)throw new InvalidOperationException("Environment exceeds revised 125k allocation: "+audit.environmentTriangles);
        }
        [MenuItem("Lucid Loop/Environment/Capture dressed nightclub")]
        public static void Capture()
        {
            var scene=SceneManager.GetSceneByPath(ScenePath);if(!scene.IsValid()||!scene.isLoaded)scene=OpenEncounter();
            var rig=Find<GymCamera>(scene);var cam=rig.Camera;
            var otherRoots=new List<GameObject>();
            for(int i=0;i<SceneManager.sceneCount;i++)
            {
                var other=SceneManager.GetSceneAt(i);if(other==scene)continue;
                foreach(var root in other.GetRootGameObjects())if(root.activeSelf){otherRoots.Add(root);root.SetActive(false);}
            }
            var oldTarget=rig.Target;var position=cam.transform.position;var rotation=cam.transform.rotation;float oldSize=cam.orthographicSize;
            var oldRT=cam.targetTexture;float oldAspect=cam.aspect;
            string dir=Path.Combine(Root,"docs/validation/nightclub-v2");Directory.CreateDirectory(dir);
            var rt=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);rt.antiAliasing=2;rt.Create();
            var active=RenderTexture.active;
            try
            {
                rig.Target=null;rig.Immediate=true;rig.Apply(1);cam.aspect=16f/9;cam.targetTexture=rt;
                Shot(cam,rt,Path.Combine(dir,"overview.png"));
                CaptureMoods(scene,cam,rt,dir);
                // Eye-level entrance establishes how the architecture reads as a room.
                cam.orthographic=false;cam.fieldOfView=64;cam.transform.position=new Vector3(0,2.6f,-12.7f);cam.transform.rotation=Quaternion.LookRotation(new Vector3(0,2.1f,5)-cam.transform.position);
                Shot(cam,rt,Path.Combine(dir,"entrance.png"));
                cam.orthographic=true;
                foreach(var actor in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CharacterActor>()).Where(a=>!a.IsPlayer&&a.Id!="affair_partner"))
                {
                    rig.Present(actor);rig.Apply(1);Shot(cam,rt,Path.Combine(dir,"conversation-"+actor.Id+".png"));rig.Overview();
                }
            }
            finally
            {
                rig.Overview();rig.Target=oldTarget;rig.Immediate=false;cam.orthographic=true;cam.orthographicSize=oldSize;cam.transform.SetPositionAndRotation(position,rotation);cam.aspect=oldAspect;cam.targetTexture=oldRT;RenderTexture.active=active;
                rt.Release();UnityEngine.Object.DestroyImmediate(rt);
                foreach(var root in otherRoots)if(root)root.SetActive(true);
            }
        }
        static void Shot(Camera cam,RenderTexture rt,string path)
        {
            cam.Render();RenderTexture.active=rt;
            var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
        }
        static void CaptureMoods(Scene scene,Camera camera,RenderTexture rt,string directory)
        {
            var presentation=Find<ClubEnvironmentPresentation>(scene);var renderers=presentation.GetComponentsInChildren<Renderer>();
            var original=renderers.Select(r=>r.sharedMaterials).ToArray();
            var neon=new Material(presentation.NeonMaterial);var screen=new Material(presentation.ScreenMaterial);
            var lights=Find<ClubLighting>(scene).Lights;var colors=lights.Select(l=>l.color).ToArray();var intensities=lights.Select(l=>l.intensity).ToArray();
            try
            {
                for(int i=0;i<renderers.Length;i++)renderers[i].sharedMaterials=original[i].Select(m=>m==presentation.NeonMaterial?neon:m==presentation.ScreenMaterial?screen:m).ToArray();
                for(int mood=0;mood<2;mood++)
                {
                    ClubEnvironmentPresentation.ApplyPalette(neon,screen,mood);
                    for(int i=0;i<lights.Length;i++)
                    {
                        lights[i].color=mood==0?(i%2==0?new Color(1,.025f,.10f):new Color(.45f,.06f,1)):(i%2==0?new Color(1,.24f,.42f):new Color(1,.49f,.28f));
                        lights[i].intensity=intensities[i]*(mood==0?1:.65f);
                    }
                    Shot(camera,rt,Path.Combine(directory,mood==0?"overview-aggressive.png":"overview-intimate.png"));
                }
            }
            finally
            {
                for(int i=0;i<renderers.Length;i++)renderers[i].sharedMaterials=original[i];
                for(int i=0;i<lights.Length;i++){lights[i].color=colors[i];lights[i].intensity=intensities[i];}
                UnityEngine.Object.DestroyImmediate(neon);UnityEngine.Object.DestroyImmediate(screen);
            }
        }
    }
}
