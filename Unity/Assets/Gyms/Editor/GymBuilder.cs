using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;

namespace LucidLoop.Gyms.Editor
{
    public static class GymBuilder
    {
        const string Base="Assets/Gyms/Generated";
        static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        static readonly Color Pink=new Color(1,.035f,.6f), Cyan=new Color(.03f,.85f,1), Gold=new Color(1,.52f,.15f);
        static Transform environment;
        static Material dark,metal,velvet,floor,skin;
        [MenuItem("Lucid Loop/Rebuild both gyms")]
        public static void Build()
        {
            Directory.CreateDirectory(Base);Directory.CreateDirectory("Assets/Gyms/Scenes");materials.Clear();
            ConfigurePipeline();ConfigurePlayer();CopyReferences();
            BuildClub();BuildStudio();AssetDatabase.SaveAssets();
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Gyms/Scenes/CharacterGym.unity",true),new EditorBuildSettingsScene("Assets/Gyms/Scenes/LiveGym.unity",true)};
            EditorSceneManager.OpenScene("Assets/Gyms/Scenes/CharacterGym.unity");
            Debug.Log("GYM_BUILD_OK: both scenes generated");
        }
        static void ConfigurePlayer()
        {
            PlayerSettings.companyName="Lucid Loop";PlayerSettings.productName="Lucid Loop · Gyms";
            PlayerSettings.colorSpace=ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToLandscapeLeft=PlayerSettings.allowedAutorotateToLandscapeRight=true;
            PlayerSettings.allowedAutorotateToPortrait=PlayerSettings.allowedAutorotateToPortraitUpsideDown=false;
            PlayerSettings.SetApiCompatibilityLevel(UnityEditor.Build.NamedBuildTarget.Standalone,ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetApiCompatibilityLevel(UnityEditor.Build.NamedBuildTarget.iOS,ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone,"com.lucidloop.gyms");
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS,"com.lucidloop.gyms");
            PlayerSettings.iOS.microphoneUsageDescription="Speak to a character in the live conversation gym.";
            PlayerSettings.iOS.targetOSVersionString="17.0";
            QualitySettings.vSyncCount=0;QualitySettings.antiAliasing=0;
            EditorSettings.serializationMode=SerializationMode.ForceText;
        }
        static void ConfigurePipeline()
        {
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Base+"/GymRenderer.asset");
            if(!renderer){renderer=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(renderer,Base+"/GymRenderer.asset");}
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Base+"/GymPipeline.asset");
            if(!pipeline){pipeline=UniversalRenderPipelineAsset.Create(renderer);AssetDatabase.CreateAsset(pipeline,Base+"/GymPipeline.asset");}
            pipeline.supportsHDR=true;pipeline.msaaSampleCount=2;pipeline.renderScale=1;
            pipeline.shadowDistance=40;pipeline.mainLightShadowmapResolution=1024;
            pipeline.maxAdditionalLightsCount=4;
            pipeline.supportsCameraDepthTexture=false;
            GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
            EditorUtility.SetDirty(pipeline);
            dark=Mat("Ink",new Color(.045f,.045f,.085f));metal=Mat("Metal",new Color(.12f,.13f,.2f));
            velvet=Mat("Velvet",new Color(.25f,.045f,.19f));floor=Mat("Floor",new Color(.085f,.075f,.13f));
            skin=Mat("Skin",new Color(.72f,.45f,.31f));
        }
        static Material Mat(string name,Color color,float emission=0)
        {
            if(materials.TryGetValue(name,out var m))return m;
            string path=Base+"/"+name+".mat";m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.color=color;m.SetFloat("_Smoothness",.32f);m.SetFloat("_Metallic",.1f);
            if(emission>0){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*emission);}
            EditorUtility.SetDirty(m);materials[name]=m;return m;
        }
        static GameObject Shape(string name,PrimitiveType type,Vector3 pos,Vector3 scale,Material material,bool collider=false,Transform parent=null)
        {
            var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(parent?parent:environment,false);g.transform.localPosition=pos;g.transform.localScale=scale;
            g.GetComponent<Renderer>().sharedMaterial=material;
            if(!collider)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }
        static GameObject Box(string name,Vector3 p,Vector3 s,Material m,bool c=false,Transform parent=null)=>Shape(name,PrimitiveType.Cube,p,s,m,c,parent);
        static GameObject Cylinder(string name,Vector3 p,float radius,float height,Material m,bool c=false,Transform parent=null)=>Shape(name,PrimitiveType.Cylinder,p,new Vector3(radius*2,height*.5f,radius*2),m,c,parent);
        static void Text(string value,Vector3 p,float size,Color color,Quaternion rotation,Transform parent=null)
        {
            var g=new GameObject(value);g.transform.SetParent(parent?parent:environment,false);g.transform.localPosition=p;g.transform.localRotation=rotation;
            var t=g.AddComponent<TextMesh>();t.text=value;t.fontSize=60;t.characterSize=size/60;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=color;
        }
        static void Ring(string name,Vector3 pos,float radius,Material material,float width=.065f)
        {
            const int n=96;for(int i=0;i<n;i++)
            {
                float a=i*2*Mathf.PI/n;var p=pos+new Vector3(Mathf.Sin(a)*radius,0,Mathf.Cos(a)*radius);
                var g=Box(name,p,new Vector3(width,.035f,2*Mathf.PI*radius/n+.02f),material);
                g.transform.localRotation=Quaternion.Euler(0,a*Mathf.Rad2Deg+90,0);
            }
        }
        static Light Lamp(string name,Vector3 p,Color color,float intensity,float range,LightType type=LightType.Point)
        {
            var g=new GameObject(name);g.transform.SetParent(environment,false);g.transform.localPosition=p;var light=g.AddComponent<Light>();
            light.type=type;light.color=color;light.intensity=intensity*(type==LightType.Directional?1:8);light.range=range;light.shadows=LightShadows.None;light.spotAngle=65;g.transform.rotation=Quaternion.Euler(75,0,0);return light;
        }
        static GymRoot NewScene(string name)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject(name).AddComponent<GymRoot>();environment=new GameObject("Environment").transform;environment.SetParent(root.transform,false);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.2f,.17f,.3f);RenderSettings.fog=false;
            var key=Lamp("Key light",new Vector3(0,10,0),new Color(.65f,.7f,1),.85f,40,LightType.Directional);key.transform.rotation=Quaternion.Euler(48,-35,0);key.shadows=LightShadows.Soft;
            var volume=new GameObject("Atmosphere").AddComponent<Volume>();volume.transform.SetParent(root.transform,false);volume.isGlobal=true;
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Base+"/Atmosphere.asset");
            if(!profile)
            {
                profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,Base+"/Atmosphere.asset");
                var bloom=profile.Add<Bloom>(true);bloom.intensity.Override(.38f);bloom.threshold.Override(.9f);bloom.scatter.Override(.65f);AssetDatabase.AddObjectToAsset(bloom,profile);
                var tone=profile.Add<Tonemapping>(true);tone.mode.Override(TonemappingMode.ACES);AssetDatabase.AddObjectToAsset(tone,profile);
            }
            volume.sharedProfile=profile;return root;
        }
        static GymCamera Camera(GymRoot root,bool studio=false)
        {
            var g=new GameObject("Main Camera");g.tag="MainCamera";g.transform.SetParent(root.transform,false);
            var c=g.AddComponent<Camera>();c.backgroundColor=new Color(.015f,.018f,.045f);c.clearFlags=CameraClearFlags.SolidColor;c.nearClipPlane=.1f;c.farClipPlane=150;
            c.allowHDR=true;c.orthographic=true;c.orthographicSize=12;g.AddComponent<AudioListener>();
            c.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            var rig=g.AddComponent<GymCamera>();rig.Camera=c;rig.Studio=studio;rig.Immediate=true;rig.Apply(1);rig.Immediate=false;return rig;
        }
        static void CopyReferences()
        {
            string dir="Assets/Gyms/References";Directory.CreateDirectory(dir);
            foreach(var name in new[]{"maya-model-sheet","ren-model-sheet","luca-model-sheet","theo-model-sheet","player-avatar"})
                File.Copy("../art/characters/"+name+".png",dir+"/"+name+".png",true);
            AssetDatabase.Refresh();
            foreach(var path in Directory.GetFiles(dir,"*.png"))
            {var importer=(TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));importer.maxTextureSize=1024;importer.mipmapEnabled=false;importer.SaveAndReimport();}
        }
        static CharacterActor Actor(string id,Vector3 p,Transform parent,bool obstacle=true)
        {
            int index=Array.IndexOf(new[]{"maya","ren","luca","theo","player"},id);
            string[] names={"Maya","Ren","Luca","Theo","You"};string[] roles={"Close friend","DJ","Bartender","Socialite","Player"};
            Color[] colors={new Color(.9f,.08f,.48f),new Color(.22f,.26f,.36f),new Color(.47f,.12f,.14f),new Color(.03f,.42f,.25f),new Color(.17f,.19f,.24f)};
            var g=new GameObject(names[index]);g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.rotation=Quaternion.Euler(0,180,0);g.layer=8;
            var actor=g.AddComponent<CharacterActor>();actor.Id=id;actor.DisplayName=names[index];actor.Role=roles[index];actor.Accent=colors[index];actor.IsPlayer=id=="player";
            actor.Greeting=new[]{"Hey. Good to see a familiar face. Come find me when you want to talk.","Give me a moment between tracks. How does the room feel to you?","What can I get you? You hear a lot from this side of the bar.","Quite a night, isn't it? Everyone seems to know someone.",""}[index];
            actor.Reference=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Gyms/References/"+(id=="player"?"player-avatar":id+"-model-sheet")+".png");
            var visual=new GameObject("Replaceable visual").transform;visual.SetParent(g.transform,false);actor.Visual=visual;
            var cloth=Mat(id+" cloth",colors[index]);var hair=Mat(id+" hair",index==0?new Color(.8f,.05f,.4f):index==1?new Color(.58f,.53f,.5f):index==3?new Color(.72f,.64f,.43f):new Color(.09f,.055f,.055f));
            var pale=Mat("Light fabric",new Color(.65f,.64f,.72f));
            Shape("Torso",PrimitiveType.Capsule,new Vector3(0,1.08f,0),new Vector3(.53f,.46f,.34f),cloth,false,visual);
            for(int side=-1;side<=1;side+=2)
            {
                var leg=Shape("Leg",PrimitiveType.Capsule,new Vector3(side*.14f,.43f,0),new Vector3(.2f,.4f,.23f),index==0?pale:cloth,false,visual);
                Box("Shoe",new Vector3(side*.14f,.075f,.07f),new Vector3(.22f,.14f,.4f),dark,false,visual);
                var arm=Shape("Arm",PrimitiveType.Capsule,new Vector3(side*.35f,1.07f,0),new Vector3(.16f,.37f,.18f),index==0||index==1?skin:cloth,false,visual);arm.transform.localRotation=Quaternion.Euler(0,0,side*8);
                Shape("Hand",PrimitiveType.Sphere,new Vector3(side*.39f,.73f,0),Vector3.one*.17f,skin,false,visual);
            }
            Cylinder("Neck",new Vector3(0,1.48f,0),.1f,.18f,skin,false,visual);
            Shape("Head",PrimitiveType.Sphere,new Vector3(0,1.72f,0),new Vector3(.4f,.5f,.38f),skin,false,visual);
            Shape("Hair",PrimitiveType.Sphere,new Vector3(0,1.89f,-.035f),new Vector3(.43f,.24f,.4f),hair,false,visual);
            if(index==0||index==4)Box("Long hair",new Vector3(0,1.49f,-.17f),new Vector3(.39f,.62f,.12f),hair,false,visual);
            if(index==1){Cylinder("Cap",new Vector3(0,1.94f,.01f),.23f,.1f,dark,false,visual);Box("Cap visor",new Vector3(0,1.93f,.2f),new Vector3(.36f,.035f,.27f),dark,false,visual);}
            if(index==2)Box("Apron",new Vector3(0,.93f,.185f),new Vector3(.44f,.67f,.035f),Mat("Apron",new Color(.32f,.07f,.1f)),false,visual);
            if(index==3){Box("Coat left",new Vector3(-.2f,.76f,-.03f),new Vector3(.18f,.7f,.35f),cloth,false,visual);Box("Coat right",new Vector3(.2f,.76f,-.03f),new Vector3(.18f,.7f,.35f),cloth,false,visual);}
            for(int s=-1;s<=1;s+=2){Box("Eye",new Vector3(s*.087f,1.77f,.176f),new Vector3(.07f,.033f,.02f),dark,false,visual);Box("Brow",new Vector3(s*.087f,1.82f,.17f),new Vector3(.09f,.023f,.025f),hair,false,visual);}
            actor.Mouth=Box("Mouth",new Vector3(0,1.62f,.18f),new Vector3(.11f,.018f,.035f),dark,false,visual).transform;
            var collider=g.AddComponent<CapsuleCollider>();collider.center=Vector3.up;collider.height=2;collider.radius=.42f;
            if(obstacle&&!actor.IsPlayer){var o=g.AddComponent<NavMeshObstacle>();o.shape=NavMeshObstacleShape.Capsule;o.center=Vector3.up;o.height=2;o.radius=.43f;o.carving=true;}
            if(!actor.IsPlayer)Text(names[index].ToUpperInvariant(),new Vector3(0,2.45f,0),.4f,colors[index]*.5f+Color.white*.5f,Quaternion.Euler(0,180,0),g.transform);
            return actor;
        }
        static void Table(Vector3 p,float radius= .65f)
        {
            Cylinder("Table base",p+Vector3.up*.3f,.15f,.6f,metal,true);
            Cylinder("Table top",p+Vector3.up*.8f,radius,.12f,metal,true);
            Cylinder("Amber table lamp",p+Vector3.up*1.05f,.12f,.28f,Mat("Amber glow",Gold,2));
            for(int s=-1;s<=1;s+=2)Box("Lounge seat",p+new Vector3(s*(radius+.65f),.3f,0),new Vector3(.9f,.6f,.9f),velvet,true);
        }
        static void BuildClub()
        {
            var root=NewScene("Character gym composition");var pink=Mat("Magenta glow",Pink,2);var cyan=Mat("Cyan glow",Cyan,2);
            Box("Foundation",new Vector3(0,-.33f,0),new Vector3(28,.6f,24),dark,true);
            Box("Club floor",new Vector3(0,-.025f,0),new Vector3(27,.05f,23),floor,true);
            for(int x=-13;x<=13;x+=2)Box("Floor seam",new Vector3(x,.006f,0),new Vector3(.025f,.01f,23),metal);
            for(int z=-11;z<=11;z+=2)Box("Floor seam",new Vector3(0,.007f,z),new Vector3(27,.01f,.025f),metal);
            Box("Rear wall",new Vector3(0,2.3f,11.65f),new Vector3(28,4.6f,.45f),dark,true);
            Box("Left wall",new Vector3(-13.65f,2.1f,0),new Vector3(.45f,4.2f,24),dark,true);
            Box("Right wall",new Vector3(13.65f,1.2f,0),new Vector3(.45f,2.4f,24),dark,true);
            for(int s=-1;s<=1;s+=2)Box("Low front wall",new Vector3(s*8.5f,.45f,-11.65f),new Vector3(10,.9f,.45f),dark,true);
            Cylinder("Dance floor",new Vector3(0,.012f,-.5f),4.4f,.02f,Mat("Dance surface",new Color(.14f,.04f,.2f)));
            Ring("Dance ring",new Vector3(0,.045f,-.5f),4.5f,pink,.09f);Ring("Inner ring",new Vector3(0,.04f,-.5f),3.2f,pink,.025f);
            for(int i=0;i<8;i++){var line=Box("Dance radial",new Vector3(0,.037f,-.5f),new Vector3(.024f,.014f,8.7f),metal);line.transform.localRotation=Quaternion.Euler(0,i*22.5f,0);}
            Box("DJ platform",new Vector3(0,.3f,8.5f),new Vector3(15,.6f,5),metal,true);
            Box("Stage fascia",new Vector3(0,.35f,5.95f),new Vector3(15,.5f,.08f),dark);
            Box("Stage rim",new Vector3(0,.63f,6),new Vector3(15,.07f,.1f),pink);
            for(int side=-1;side<=1;side+=2)for(int i=0;i<4;i++)
            {Box("Stage step",new Vector3(side*6,.075f*(i+1),4.8f+i*.3f),new Vector3(2,.15f*(i+1),.32f),metal,true);Box("Step light",new Vector3(side*6,.15f*(i+1)+.01f,4.65f+i*.3f),new Vector3(2,.025f,.03f),pink);}
            Box("DJ booth",new Vector3(0,1.12f,8.2f),new Vector3(4,1,1.2f),dark,true);
            Box("Booth light",new Vector3(0,.8f,7.58f),new Vector3(4,.06f,.025f),pink);
            for(int s=-1;s<=1;s+=2){Cylinder("Deck",new Vector3(s*.95f,1.65f,8.2f),.4f,.06f,metal);Box("Speaker stack",new Vector3(s*5,1.6f,9.4f),new Vector3(.9f,2,.8f),dark,true);for(int y=0;y<2;y++)Shape("Speaker cone",PrimitiveType.Sphere,new Vector3(s*5,1.1f+y,8.98f),new Vector3(.55f,.55f,.08f),metal);}
            Box("LED wall",new Vector3(0,2.75f,11.3f),new Vector3(11,3.1f,.12f),Mat("Screen purple",new Color(.27f,.035f,.45f),.6f));
            for(int i=0;i<15;i++)
            {float angle=i*.55f;var bar=Box("Graphic waveform",new Vector3(Mathf.Sin(angle)*i*.28f,2.75f+Mathf.Cos(angle)*i*.085f,11.2f),new Vector3(.09f,.6f+i*.065f,.03f),i%3==0?cyan:pink);bar.transform.rotation=Quaternion.Euler(0,0,-i*18);}
            Text("L U C I D   L O O P",new Vector3(0,4.15f,11.05f),.65f,Color.white,Quaternion.identity);
            Box("Bar",new Vector3(-10.3f,.65f,1),new Vector3(2.2f,1.3f,13),dark,true);
            Box("Bar counter",new Vector3(-10.3f,1.36f,1),new Vector3(2.5f,.15f,13.3f),metal,true);
            Box("Bar cyan edge",new Vector3(-9.02f,1.38f,1),new Vector3(.045f,.075f,13),cyan);
            for(int z=-5;z<=6;z+=2)
            {Cylinder("Bar stool",new Vector3(-8.2f,.42f,z),.32f,.84f,metal,true);}
            for(int shelf=0;shelf<3;shelf++)
            {
                Box("Bottle shelf",new Vector3(-13.2f,1.3f+shelf*.8f,1),new Vector3(.5f,.06f,12),cyan);
                for(int i=0;i<18;i++)Cylinder("Bottle",new Vector3(-13.1f,1.5f+shelf*.8f,-4.6f+i*.65f),.09f,.35f,Mat("Bottle"+(i%3),i%3==0?Gold:i%3==1?Cyan:Pink,.35f));
            }
            Text("B A R",new Vector3(-12,3.9f,3),.7f,Cyan,Quaternion.Euler(0,90,0));
            for(int i=0;i<5;i++)Box("VIP sofa",new Vector3(12.1f,.45f,-4+i*1.5f),new Vector3(1.4f,.9f,1.4f),velvet,true);
            Table(new Vector3(9,.05f,1));Table(new Vector3(9,.05f,-3));Table(new Vector3(-7,.05f,-8));Table(new Vector3(6,.05f,-8));
            Text("V I P",new Vector3(12.5f,2.6f,3.2f),.6f,Gold,Quaternion.Euler(0,-25,0));
            for(int s=-1;s<=1;s+=2)
            {
                Box("Entrance column",new Vector3(s*3,.9f,-10.8f),new Vector3(.35f,1.8f,.35f),pink,true);
                for(int z=0;z<4;z++)Box("Light column",new Vector3(s*13.3f,2,z*5-8),new Vector3(.1f,3.8f,.16f),s<0?cyan:pink);
            }
            Text("ENTRANCE",new Vector3(0,.035f,-10),.6f,new Color(.5f,.7f,.8f),Quaternion.Euler(90,0,0));
            Box("Service door",new Vector3(-10.5f,1.1f,11.36f),new Vector3(1.3f,2.2f,.1f),metal,true);
            Box("Restroom door",new Vector3(10.5f,1.1f,11.36f),new Vector3(1.3f,2.2f,.1f),metal,true);
            Text("SERVICE",new Vector3(-10.5f,2.55f,11.2f),.36f,Gold,Quaternion.identity);Text("WC",new Vector3(10.5f,2.55f,11.2f),.4f,Cyan,Quaternion.identity);
            var lights=new List<Light>{Lamp("Dance magenta",new Vector3(0,5,0),Pink,1.4f,13),Lamp("Bar cyan",new Vector3(-8,4,1),Cyan,1.5f,13),Lamp("VIP amber",new Vector3(9,4,-1),Gold,1.5f,11)};
            for(int i=0;i<4;i++){lights.Add(Lamp("Stage beam",new Vector3(-5+i*3.3f,5,7),i%2==0?Pink:Cyan,2,18,LightType.Spot));Shape("Light fixture",PrimitiveType.Cylinder,new Vector3(-5+i*3.3f,4.8f,7),new Vector3(.3f,.25f,.3f),metal);}
            var surface=environment.gameObject.AddComponent<NavMeshSurface>();surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.layerMask=1;surface.collectObjects=CollectObjects.Children;surface.BuildNavMesh();
            if(surface.navMeshData){var existing=AssetDatabase.LoadAssetAtPath<NavMeshData>(Base+"/ClubNavMesh.asset");if(existing)EditorUtility.CopySerialized(surface.navMeshData,existing);else AssetDatabase.CreateAsset(surface.navMeshData,Base+"/ClubNavMesh.asset");surface.RemoveData();surface.navMeshData=AssetDatabase.LoadAssetAtPath<NavMeshData>(Base+"/ClubNavMesh.asset");surface.AddData();}
            var actors=new GameObject("Characters").transform;actors.SetParent(root.transform,false);
            var player=Actor("player",new Vector3(0,0,-8),actors);var agent=player.gameObject.AddComponent<NavMeshAgent>();agent.radius=.35f;agent.height=2;agent.speed=3.7f;agent.angularSpeed=480;agent.acceleration=15;agent.stoppingDistance=.12f;
            var rig=Camera(root);rig.Player=player.transform;
            var director=new GameObject("Offline interaction");director.transform.SetParent(root.transform,false);var gym=director.AddComponent<OfflineGym>();gym.Player=agent;gym.Rig=rig;
            gym.Characters=new[]{Actor("maya",new Vector3(-2.8f,0,-2),actors),Actor("ren",new Vector3(2.5f,.6f,7.4f),actors),Actor("luca",new Vector3(-7,0,2),actors),Actor("theo",new Vector3(7,0,-1),actors)};
            gym.Marker=Cylinder("Destination",new Vector3(0,.04f,-8),.26f,.015f,cyan).transform;
            var dancers=new List<Transform>();for(int i=0;i<12;i++)
            {float a=i*2.4f;var p=new Vector3(Mathf.Cos(a)*(1+i%3),0,Mathf.Sin(a)*(1+i%3));var d=new GameObject("Partygoer");d.transform.SetParent(environment,false);d.transform.localPosition=p;Shape("Body",PrimitiveType.Capsule,new Vector3(0,.9f,0),new Vector3(.35f,.68f,.3f),i%2==0?velvet:metal,false,d.transform);Shape("Head",PrimitiveType.Sphere,new Vector3(0,1.68f,0),Vector3.one*.3f,skin,false,d.transform);dancers.Add(d.transform);}
            var show=root.gameObject.AddComponent<ClubLighting>();show.Lights=lights.ToArray();show.Dancers=dancers.ToArray();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),"Assets/Gyms/Scenes/CharacterGym.unity");
        }
        static void BuildStudio()
        {
            var root=NewScene("Live gym composition");Box("Studio floor",new Vector3(0,-.2f,0),new Vector3(16,.4f,12),floor,true);
            Box("Studio backdrop",new Vector3(0,2,3),new Vector3(16,5,.3f),dark);
            var pink=Mat("Magenta glow",Pink,2);var cyan=Mat("Cyan glow",Cyan,2);
            for(int i=-3;i<=3;i++)Box("Studio strip",new Vector3(i*1.5f,2,2.8f),new Vector3(.035f,4,.04f),i%2==0?pink:cyan);
            Ring("Studio halo",new Vector3(0,.04f,0),1.5f,pink);
            Lamp("Warm face key",new Vector3(-2,3,-3),new Color(1,.76f,.6f),.25f,10);Lamp("Cyan rim",new Vector3(2,2,1),Cyan,.25f,8);
            var characters=new[]{Actor("maya",Vector3.zero,root.transform,false),Actor("ren",Vector3.zero,root.transform,false),Actor("luca",Vector3.zero,root.transform,false),Actor("theo",Vector3.zero,root.transform,false)};
            foreach(var character in characters)character.GetComponentInChildren<TextMesh>().GetComponent<Renderer>().enabled=false;
            for(int i=1;i<characters.Length;i++)characters[i].gameObject.SetActive(false);
            var rig=Camera(root,true);rig.Target=characters[0];rig.Immediate=true;rig.Apply(1);rig.Immediate=false;
            var g=new GameObject("Live conversation");g.transform.SetParent(root.transform,false);var live=g.AddComponent<LiveGym>();live.Rig=rig;live.Characters=characters;
            live.Speaker=g.AddComponent<AudioSource>();live.Speaker.playOnAwake=false;live.Speaker.spatialBlend=0;
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),"Assets/Gyms/Scenes/LiveGym.unity");
        }
        public static void BuildWindows()
        {
            Build();Directory.CreateDirectory("Builds/Windows");
            var report=BuildPipeline.BuildPlayer(EditorBuildSettings.scenes,"Builds/Windows/LucidLoopGyms.exe",BuildTarget.StandaloneWindows64,BuildOptions.Development);
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Windows build failed: "+report.summary.result);
            Debug.Log("GYM_WINDOWS_BUILD_OK");
        }
    }
}
