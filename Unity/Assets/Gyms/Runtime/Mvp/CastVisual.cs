using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
namespace LucidLoop.Gyms.Mvp
{
    // Small articulated figures. All animation is local to the visual; navigation owns the root.
    public sealed class CastVisual : MonoBehaviour
    {
        Transform leftLeg,rightLeg,leftArm,rightArm,hair;
        NavMeshAgent agent;
        Vector3 rest;
        float stride;
        public bool Dancing;
        public bool Speaking;
        static readonly Dictionary<string,Material> palette=new Dictionary<string,Material>();
        public static Material Paint(string name,Color color,float glow=0)
        {
            if(palette.TryGetValue(name,out var m) && m)return m;
            m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.name=name;m.color=color;
            m.SetFloat("_Smoothness",.24f);m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*(.42f+glow));palette[name]=m;return m;
        }
        static Color C(float r,float g,float b)=>new Color(r,g,b);
        public static Transform Pivot(Transform parent,string name,Vector3 p)
        {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=p;return t;}
        public static GameObject Box(Transform parent,string name,Vector3 p,Vector3 scale,Material mat)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=scale;
            Destroy(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=mat;return g;
        }
        public static Transform Form(Transform parent,string name,Vector3 p,float[] heights,float[] widths,float[] depths,Material mat,int sides=8)
        {
            var t=Pivot(parent,name,p);var v=new List<Vector3>();var tris=new List<int>();
            // Independent quads give the painted, faceted silhouette a clear plane change.
            for(int ring=0;ring<heights.Length-1;ring++)for(int side=0;side<sides;side++)
            {
                float a=(side+.5f)*Mathf.PI*2/sides,b=(side+1.5f)*Mathf.PI*2/sides;int start=v.Count;
                v.Add(new Vector3(Mathf.Sin(a)*widths[ring],heights[ring],Mathf.Cos(a)*depths[ring]));
                v.Add(new Vector3(Mathf.Sin(b)*widths[ring],heights[ring],Mathf.Cos(b)*depths[ring]));
                v.Add(new Vector3(Mathf.Sin(b)*widths[ring+1],heights[ring+1],Mathf.Cos(b)*depths[ring+1]));
                v.Add(new Vector3(Mathf.Sin(a)*widths[ring+1],heights[ring+1],Mathf.Cos(a)*depths[ring+1]));
                tris.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
            }
            var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=mat;return t;
        }
        static Transform Limb(Transform parent,string name,Vector3 p,float length,float width,Material mat)
            =>Form(parent,name,p,new[]{-length,-length*.9f,-length*.48f,-.06f,0},new[]{0,width*.75f,width*.9f,width,width*.8f},new[]{0,width*.8f,width*.9f,width,width*.8f},mat);
        public static CastVisual Build(Transform root,string id,bool dancer=false)
        {
            var a=root.GetComponent<CharacterActor>();Transform visual;
            if(a){visual=a.Visual;foreach(Transform t in visual){t.gameObject.SetActive(false);Destroy(t.gameObject);}a.Mouth=null;}
            else{foreach(Transform t in root){t.gameObject.SetActive(false);Destroy(t.gameObject);}visual=Pivot(root,"Articulated silhouette",Vector3.zero);}
            var animation=visual.gameObject.AddComponent<CastVisual>();animation.agent=root.GetComponent<NavMeshAgent>();animation.Dancing=dancer;
            var black=Paint("Midnight fabric",C(.055f,.06f,.09f));var gold=Paint("Brass accents",C(.85f,.56f,.19f));
            var skin=Paint("Warm skin",C(.72f,.46f,.33f));var ivory=Paint("Ivory linen",C(.84f,.8f,.76f));
            var emerald=Paint("Emerald coat",C(.015f,.36f,.19f));var purple=Paint("Violet jacket",C(.21f,.085f,.31f));var burgundy=Paint("Burgundy apron",C(.39f,.055f,.095f));
            var pink=Paint("Magenta hair",C(.92f,.035f,.28f));var brown=Paint("Chestnut hair",C(.17f,.085f,.044f));var silver=Paint("Silver hair",C(.8f,.76f,.64f));
            bool maya=id=="maya",theo=id=="theo",ren=id=="ren",luca=id=="luca",pc=id=="player";
            var cloth=theo?emerald:black;var hairMat=maya?pink:theo||ren?silver:pc?black:brown;
            var legMat=maya?ivory:cloth;
            Form(visual,"Torso",Vector3.zero,new[]{.95f,1.06f,1.38f,1.55f,1.61f},new[]{.19f,.21f,.29f,.3f,.12f},new[]{.12f,.13f,.16f,.13f,.07f},cloth);
            if(maya)Form(visual,"Midriff",new Vector3(0,0,.01f),new[]{1.0f,1.13f,1.2f},new[]{.2f,.2f,.21f},new[]{.13f,.135f,.14f},skin);
            for(int side=-1;side<=1;side+=2)
            {
                var leg=Limb(visual,side<0?"Left stride":"Right stride",new Vector3(side*.13f,.98f,0),.9f,maya?.145f:.115f,legMat);
                Box(leg,"Shoe",new Vector3(0,-.91f,.08f),new Vector3(.21f,.13f,.36f),black);
                var arm=Limb(visual,side<0?"Left gesture":"Right gesture",new Vector3(side*.31f,1.5f,0),.61f,.092f,maya?purple:ren?skin:cloth);
                Form(arm,"Hand",new Vector3(0,-.66f,0),new[]{-.08f,0,.08f},new[]{0,.072f,.06f},new[]{0,.065f,.05f},skin);
                if(side<0){animation.leftLeg=leg;animation.leftArm=arm;}else{animation.rightLeg=leg;animation.rightArm=arm;}
            }
            Form(visual,"Neck",Vector3.zero,new[]{1.57f,1.76f},new[]{.08f,.085f},new[]{.08f,.08f},skin);
            Form(visual,"Face",Vector3.zero,new[]{1.67f,1.73f,1.92f,2.03f,2.08f},new[]{.07f,.145f,.175f,.13f,0},new[]{.07f,.115f,.145f,.12f,0},skin);
            Form(visual,"Hair crown",new Vector3(0,0,-.02f),new[]{1.91f,2.04f,2.15f},new[]{.19f,.19f,0},new[]{.16f,.17f,0},hairMat);
            for(int i=0;i<5;i++)
            {
                var lockHair=Form(visual,"Swept hair",new Vector3((i-2)*.067f,2.03f,.03f),new[]{0,.17f},new[]{.085f,0},new[]{.12f,0},hairMat,5);
                lockHair.localRotation=Quaternion.Euler(-20,-30,30);
            }
            if(maya||pc)
            {
                animation.hair=Pivot(visual,"Ponytail sway",new Vector3(0,2.03f,-.13f));
                Form(animation.hair,"Ponytail",Vector3.zero,new[]{-.67f,-.38f,-.03f,.1f},new[]{0,.105f,.12f,.06f},new[]{.02f,.13f,.13f,0},hairMat);
                Box(visual,"Hair clasp",new Vector3(0,2.025f,-.18f),new Vector3(.11f,.09f,.08f),gold);
            }
            if(theo)
            {
                for(int side=-1;side<=1;side+=2)
                {
                    var tail=Form(visual,"Coat tail",new Vector3(side*.17f,0,-.08f),new[]{.28f,.62f,1.08f,1.47f},new[]{.18f,.17f,.14f,.1f},new[]{.13f,.16f,.18f,.16f},emerald);
                    Box(visual,"Gold lapel",new Vector3(side*.13f,1.42f,.15f),new Vector3(.035f,.26f,.035f),gold).transform.localRotation=Quaternion.Euler(0,0,side*22);
                    Box(visual,"Amber lens",new Vector3(side*.082f,1.91f,.136f),new Vector3(.13f,.067f,.034f),gold);
                }
            }
            if(luca)
            {
                Form(visual,"Apron",new Vector3(0,0,.125f),new[]{.5f,.65f,1.13f},new[]{.26f,.25f,.21f},new[]{.022f,.04f,.03f},burgundy);
                Box(visual,"White shoulder towel",new Vector3(.19f,1.33f,.14f),new Vector3(.13f,.5f,.085f),ivory);
            }
            if(ren)
            {
                Form(visual,"Cap",Vector3.zero,new[]{2.0f,2.12f,2.17f},new[]{.2f,.17f,0},new[]{.18f,.16f,0},black);
                Box(visual,"Cap brim",new Vector3(0,2.025f,.17f),new Vector3(.34f,.04f,.25f),black);
                for(int side=-1;side<=1;side+=2)Box(visual,"Headphone cup",new Vector3(side*.14f,1.59f,.12f),new Vector3(.12f,.15f,.12f),Paint("Headphone blue",C(.08f,.16f,.36f)));
            }
            if(pc)Box(visual,"Face mask",new Vector3(0,1.81f,.125f),new Vector3(.26f,.11f,.065f),black);
            if(maya)Box(visual,"Belt",new Vector3(0,1.035f,.14f),new Vector3(.4f,.065f,.04f),black);
            Box(visual,"Buckle",new Vector3(0,1.025f,.173f),new Vector3(.06f,.055f,.035f),gold);
            animation.rest=visual.localPosition;
            return animation;
        }
        void Update()
        {
            if(!agent)agent=GetComponentInParent<NavMeshAgent>();
            float speed=agent&&agent.enabled&&agent.isOnNavMesh?agent.velocity.magnitude:0;
            bool running=speed>5f;
            stride+=Time.deltaTime*(speed> .05f?Mathf.Min(speed*3.4f,18f):2);
            float swing=speed>.05f?Mathf.Sin(stride)*(running?40:27):0;
            if(leftLeg)leftLeg.localRotation=Quaternion.Euler(swing,0,0);
            if(rightLeg)rightLeg.localRotation=Quaternion.Euler(-swing,0,0);
            if(leftArm)leftArm.localRotation=Quaternion.Euler(-swing*.7f+(Dancing?Mathf.Sin(Time.time*2)*30:0),0,Speaking?-22:8);
            if(rightArm)rightArm.localRotation=Quaternion.Euler(swing*.7f+(Speaking?-30+Mathf.Sin(Time.time*3)*8:0),0,-8);
            if(hair)hair.localRotation=Quaternion.Euler(speed*2+Mathf.Sin(stride)*speed*2,0,Mathf.Sin(stride*.5f)*3);
            transform.localPosition=rest+Vector3.up*(speed>.05f?Mathf.Abs(Mathf.Sin(stride))*(running?.065f:.035f):Mathf.Sin(Time.time*1.8f)*.009f);
        }
    }
}
