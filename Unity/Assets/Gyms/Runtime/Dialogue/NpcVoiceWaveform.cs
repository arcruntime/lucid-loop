using UnityEngine;
using UnityEngine.UI;
namespace LucidLoop.Gyms
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class NpcVoiceWaveform : MaskableGraphic
    {
        public float Sensitivity=8, Attack=.035f, Release=.18f, Glow=.35f;
        public NpcSpeechMeter Meter;
        readonly float[] heights=new float[35];
        public float VisibleLevel {get;private set;}
        public void ResetWave(){System.Array.Clear(heights,0,heights.Length);VisibleLevel=0;SetVerticesDirty();}
        void Update()
        {
            VisibleLevel=0;
            for(int i=0;i<heights.Length;i++)
            {
                float target=Meter==null?0:Mathf.Clamp01(Meter.Bars[i]*Sensitivity);
                float speed=target>heights[i]?Attack:Release;
                heights[i]=Mathf.Lerp(heights[i],target,1-Mathf.Exp(-Time.unscaledDeltaTime/Mathf.Max(.005f,speed)));
                VisibleLevel=Mathf.Max(VisibleLevel,heights[i]);
            }
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;
            Rect(vh,new Vector2(r.xMin,r.center.y-.5f),new Vector2(r.xMax,r.center.y+.5f),new Color(color.r,color.g,color.b,.2f));
            for(int i=0;i<heights.Length;i++)
            {
                float u=i/(float)(heights.Length-1),x=Mathf.Lerp(r.xMin+2,r.xMax-2,u);
                float taper=Mathf.Pow(Mathf.Max(0,Mathf.Sin(u*Mathf.PI)),.8f);
                float h=.5f+heights[i]*taper*r.height*.43f;
                var glow=color;glow.a=(.03f+heights[i]*Glow)*taper;
                Rect(vh,new Vector2(x-4,r.center.y-h-3),new Vector2(x+4,r.center.y+h+3),glow);
                var core=color;core.a=.18f+heights[i]*.82f;
                Rect(vh,new Vector2(x-.8f,r.center.y-h),new Vector2(x+.8f,r.center.y+h),core);
            }
        }
        static void Rect(VertexHelper vh,Vector2 a,Vector2 b,Color c)
        {int n=vh.currentVertCount;vh.AddVert(a,c,Vector2.zero);vh.AddVert(new Vector2(a.x,b.y),c,Vector2.zero);vh.AddVert(b,c,Vector2.zero);vh.AddVert(new Vector2(b.x,a.y),c,Vector2.zero);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);}
    }
}
