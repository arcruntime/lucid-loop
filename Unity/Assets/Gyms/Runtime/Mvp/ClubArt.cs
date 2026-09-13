using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace LucidLoop.Gyms.Mvp
{
    public sealed class ClubArt : MonoBehaviour
    {
        Material brass,ink,leaf,velvet,pink,cyan;
        Transform decor;
        GameObject B(string name,Vector3 p,Vector3 size,Material m)=>CastVisual.Box(decor,name,p,size,m);
        void Barrier(GameObject g)
        {
            var box=g.AddComponent<BoxCollider>();
            var o=g.AddComponent<NavMeshObstacle>();o.shape=NavMeshObstacleShape.Box;o.size=Vector3.one;o.carving=true;
        }
        public void Dress(FirstLoop loop)
        {
            decor=CastVisual.Pivot(transform,"Club art pass",Vector3.zero);
            ink=CastVisual.Paint("Architectural ink",new Color(.075f,.06f,.11f));
            brass=CastVisual.Paint("Antique brass",new Color(.5f,.3f,.1f),.13f);
            velvet=CastVisual.Paint("Plum velvet",new Color(.31f,.07f,.24f));
            leaf=CastVisual.Paint("Palm jade",new Color(.06f,.24f,.15f));
            pink=CastVisual.Paint("Neon rose",new Color(.94f,.015f,.42f),2);
            cyan=CastVisual.Paint("Neon ice",new Color(.02f,.68f,.88f),2);
            RenderSettings.ambientLight=new Color(.36f,.31f,.43f);
            var camera=loop.Rig.Camera;camera.backgroundColor=new Color(.035f,.029f,.06f);
            loop.Rig.Player=CastVisual.Pivot(decor,"Fixed overview anchor",loop.Player.transform.position);
            loop.Rig.OverviewSize=12.8f;loop.Rig.Pitch=51;loop.Rig.Yaw=-23;
            foreach(var actor in FindObjectsByType<CharacterActor>(FindObjectsSortMode.None))
            {
                CastVisual.Build(actor.transform,actor.Id);
                var text=actor.GetComponentInChildren<TextMesh>();if(text){text.transform.localScale=Vector3.one*.55f;text.fontSize=80;text.color=Color.Lerp(actor.Accent,Color.white,.8f);}
            }
            // Replace the non-interactable partner and background crowd with articulated silhouettes.
            loop.Partner.localScale=Vector3.one;loop.Partner.position=new Vector3(7.65f,0,-.65f);
            var partnerRenderer=loop.Partner.GetComponent<Renderer>();if(partnerRenderer)partnerRenderer.enabled=false;
            CastVisual.Build(loop.Partner,"partner");
            var club=FindFirstObjectByType<ClubLighting>();
            if(club)foreach(var dancer in club.Dancers){CastVisual.Build(dancer,"crowd",true);dancer.localScale=Vector3.one*.85f;}
            // Recolor the existing walkable architecture without replacing collision/navigation.
            foreach(var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if(r.name=="VIP sofa"||r.name=="Lounge seat")r.sharedMaterial=velvet;
                if(r.name=="Club floor")r.sharedMaterial=FloorMaterial();
                if(r.name=="Graphic waveform")r.enabled=false;
                if(r.name=="LED wall")r.sharedMaterial=MuralMaterial();
            }
            // Paneled walls, lit pilasters, and brass ceiling frames.
            for(int x=-12;x<=12;x+=3)
            {
                B("Rear pilaster",new Vector3(x,2.1f,11.3f),new Vector3(.2f,4.2f,.2f),brass);
                B("Wall cornice",new Vector3(x,4.35f,11.15f),new Vector3(2.8f,.14f,.32f),ink);
            }
            for(int z=-9;z<10;z+=3)
            {
                B("Bar wall panel",new Vector3(-13.37f,2.2f,z),new Vector3(.12f,3.8f,2.8f),ink);
                B("Cyan pilaster",new Vector3(-13.18f,2.2f,z),new Vector3(.08f,3.1f,.07f),cyan);
            }
            // Two screen sections leave the existing entrance at z=-1 open.
            Screen(new Vector3(6.6f,0,2.5f),3.1f);
            Screen(new Vector3(6.6f,0,-4.1f),1.4f);
            for(int i=0;i<5;i++)
            {
                var p=new Vector3(12.45f,.72f,-4+i*1.5f);
                B("Upholstered VIP back",p+Vector3.up*.15f,new Vector3(.28f,1.0f,1.45f),velvet);
                B("Brass piping",p+new Vector3(-.17f,.66f,0),new Vector3(.035f,.035f,1.42f),brass);
            }
            Plant(new Vector3(6.6f,0,.7f));Plant(new Vector3(6.6f,0,-3.0f));Plant(new Vector3(11.7f,0,4.0f));
            Plant(new Vector3(-6.8f,0,5.8f));Plant(new Vector3(6.8f,0,5.8f));
            // Decorative arches flank the raised booth; don't span the playable approach.
            for(int side=-1;side<=1;side+=2)
            {
                for(int j=0;j<3;j++)B("Stage gold upright",new Vector3(side*(4.4f+j*.16f),2.9f,10.8f),new Vector3(.035f,3.3f,.08f),brass);
                B("Booth trim",new Vector3(side*2.05f,1.15f,7.6f),new Vector3(.06f,1.1f,.06f),brass);
            }
            // Warm pools around seating create a visual counterpoint to the dancefloor.
            foreach(var p in new[]{new Vector3(9,2,-1),new Vector3(6,2,-8),new Vector3(-7,2,-8)})
            {var light=CastVisual.Pivot(decor,"Amber pool",p).gameObject.AddComponent<Light>();light.color=new Color(1,.53f,.2f);light.intensity=4;light.range=5;}
            foreach(var vol in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                var profile=vol.profile;
                if(profile.TryGet<Bloom>(out var bloom)){bloom.intensity.Override(.65f);bloom.scatter.Override(.6f);}
            }
            if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-btd-art-reference")<0)gameObject.AddComponent<PaintedBackdrop>().Install(loop);
        }
        void Screen(Vector3 p,float length)
        {
            var basePanel=B("VIP partition",p+Vector3.up*.43f,new Vector3(.16f,.86f,length),velvet);Barrier(basePanel);
            B("VIP brass rail",p+Vector3.up*1.6f,new Vector3(.06f,.055f,length),brass);
            for(float z=-length*.5f;z<=length*.5f+.01f;z+=.38f)
                B("VIP screen fin",p+new Vector3(0,1.05f,z),new Vector3(.045f,1.1f,.04f),brass);
        }
        void Plant(Vector3 p)
        {
            var pot=CastVisual.Form(decor,"Planter",p,new[]{0,.1f,.6f},new[]{.22f,.3f,.38f},new[]{.22f,.3f,.38f},brass);
            var o=pot.gameObject.AddComponent<NavMeshObstacle>();o.shape=NavMeshObstacleShape.Capsule;o.radius=.4f;o.height=1.0f;o.center=Vector3.up*.5f;o.carving=true;
            for(int i=0;i<7;i++)
            {
                var frond=CastVisual.Form(decor,"Palm frond",p+Vector3.up*.55f,new[]{0,.55f,1.25f},new[]{.04f,.18f,0},new[]{.018f,.035f,0},leaf,4);
                frond.localRotation=Quaternion.Euler(15+i%3*14,i*137,12);
            }
        }
        Material FloorMaterial()
        {
            var m=CastVisual.Paint("Painted stone",new Color(.34f,.27f,.38f));
            var texture=new Texture2D(256,256,TextureFormat.RGB24,false);texture.wrapMode=TextureWrapMode.Repeat;
            for(int y=0;y<256;y++)for(int x=0;x<256;x++)
            {
                float noise=Mathf.PerlinNoise(x*.055f,y*.09f),vein=Mathf.Sin(x*.07f+y*.035f+noise*7);
                float value=.5f+noise*.35f+Mathf.Pow(Mathf.Abs(vein),18)*.1f;
                texture.SetPixel(x,y,new Color(value*.72f,value*.65f,value));
            }
            texture.Apply();m.mainTexture=texture;m.mainTextureScale=new Vector2(6,5);return m;
        }
        Material MuralMaterial()
        {
            var texture=new Texture2D(512,256,TextureFormat.RGB24,false);
            for(int y=0;y<256;y++)for(int x=0;x<512;x++)
            {
                float dx=(x-256)/256f,dy=(y-128)/128f,r=Mathf.Sqrt(dx*dx+dy*dy),a=Mathf.Atan2(dy,dx);
                float wave=Mathf.Pow(Mathf.Max(0,Mathf.Sin(a*5-r*24)),7),stars=Mathf.PerlinNoise(x*.7f,y*.7f)>.78f?1:0;
                texture.SetPixel(x,y,Color.Lerp(new Color(.1f,.015f,.23f),new Color(.95f,.18f,.7f),wave*.8f+stars*.2f));
            }
            texture.Apply();var m=CastVisual.Paint("Vortex mural",Color.white,.8f);m.mainTexture=texture;m.SetTexture("_EmissionMap",texture);return m;
        }
    }
}
