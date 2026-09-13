using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace LucidLoop.Gyms.Mvp
{
    public sealed class PaintedBackdrop : MonoBehaviour
    {
        Camera view;
        public void Install(FirstLoop loop)
        {
            view=loop.Rig.Camera;view.aspect=16/9f;
            var occlusion=new Material(Shader.Find("BTD/PaintedOcclusion"));
            foreach(var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if(r.GetComponentInParent<CharacterActor>() || r.GetComponentInParent<CastVisual>() || r.transform.IsChildOf(loop.Partner))continue;
                if(r.GetComponent<TextMesh>()){r.enabled=false;continue;}
                // Invisible architectural depth retains foreground occlusion for live figures.
                r.sharedMaterial=occlusion;
            }
            var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.name="Painted room backdrop";Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(view.transform,false);quad.transform.localPosition=Vector3.forward*100;
            quad.transform.localScale=new Vector3(loop.Rig.OverviewSize*2*16/9f,loop.Rig.OverviewSize*2,1);
            var paint=new Material(Shader.Find("BTD/PaintedBackdrop"));paint.mainTexture=Resources.Load<Texture2D>("MvpArt/ClubBackdrop");quad.GetComponent<Renderer>().sharedMaterial=paint;
            // Painting already contains its color grading; avoid applying it twice.
            view.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        }
        void LateUpdate()
        {
            if(!view)return;
            float aspect=(float)Screen.width/Screen.height,target=16/9f;
            view.rect=aspect>target?new Rect((1-target/aspect)/2,0,target/aspect,1):new Rect(0,(1-aspect/target)/2,1,aspect/target);
        }
    }
}
