using UnityEngine;
using UnityEngine.UI;

namespace LucidLoop.Gyms
{
    // Resolution-independent ornament, drawn with the standard uGUI material.
    public sealed class DecoDialogueFrame : MaskableGraphic
    {
        public bool Nameplate;
        public bool Simple;
        public bool MicrophoneIcon, SendIcon;
        bool recording, available;
        public void SetIconState(bool active, bool enabled)
        {
            if(recording==active && available==enabled)return;
            recording=active;available=enabled;SetVerticesDirty();
        }
        static readonly Color Gold = new Color(.89f, .69f, .23f);
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            if (MicrophoneIcon || SendIcon)
            {
                var c=r.center;float unit=Mathf.Min(r.width,r.height);
                Color tint=available?Color.white:new Color(.55f,.55f,.55f,1);
                if(SendIcon)
                {
                    int n=vh.currentVertCount;
                    vh.AddVert(c+new Vector2(-.12f,-.18f)*unit,tint,Vector2.zero);
                    vh.AddVert(c+new Vector2(-.12f,.18f)*unit,tint,Vector2.zero);
                    vh.AddVert(c+new Vector2(.20f,0)*unit,tint,Vector2.zero);
                    vh.AddTriangle(n,n+1,n+2);return;
                }
                if(recording)
                {
                    var pink=new Color(1,.16f,.52f,1);
                    Arc(vh,c,unit*.41f,0,360,unit*.11f,new Color(1,.1f,.5f,.10f));
                    Arc(vh,c,unit*.41f,0,360,unit*.025f,pink);tint=pink;
                }
                // Rounded capsule, receiver cradle, stem and foot.
                Arc(vh,c+Vector2.up*unit*.08f,unit*.075f,0,180,unit*.045f,tint);
                Arc(vh,c-Vector2.up*unit*.06f,unit*.075f,180,360,unit*.045f,tint);
                Line(vh,c+new Vector2(-.075f,-.06f)*unit,c+new Vector2(-.075f,.08f)*unit,unit*.045f,tint);
                Line(vh,c+new Vector2(.075f,-.06f)*unit,c+new Vector2(.075f,.08f)*unit,unit*.045f,tint);
                Arc(vh,c-Vector2.up*unit*.035f,unit*.15f,180,360,unit*.035f,tint);
                Line(vh,c-Vector2.up*unit*.18f,c-Vector2.up*unit*.26f,unit*.035f,tint);
                Line(vh,c+new Vector2(-.10f,-.26f)*unit,c+new Vector2(.10f,-.26f)*unit,unit*.035f,tint);
                return;
            }
            if (r.width < 40 || r.height < 40) return;
            if (Simple) { Border(vh,r,2,8,Gold,1); return; }
            if (Nameplate)
            {
                var fill = new Color(.93f, .76f, .38f, .98f);
                Quad(vh, new Vector2(r.xMin+8,r.yMin+6), new Vector2(r.xMax-8,r.yMax-6), fill);
            }
            Border(vh, r, 3, 18, Gold, 2);
            Border(vh, r, 9, 12, new Color(1,.88f,.48f,.8f), 1);
            Border(vh, r, 15, 6, new Color(.58f,.40f,.12f,.85f), 1);
            if (!Nameplate)
            {
                foreach (float x in new[] { r.xMin + 9, r.xMax - 9 })
                {
                    Diamond(vh, new Vector2(x,r.center.y), 13, Gold);
                    Diamond(vh, new Vector2(x,r.center.y), 7, Gold);
                }
                foreach (float x in new[] { r.xMin+24, r.xMax-24 })
                foreach (float y in new[] { r.yMin+24, r.yMax-24 })
                {
                    float dx = x < r.center.x ? 1 : -1, dy = y < r.center.y ? 1 : -1;
                    Line(vh,new Vector2(x,y+dy*24),new Vector2(x,y),1,Gold);
                    Line(vh,new Vector2(x,y),new Vector2(x+dx*36,y),1,Gold);
                }
            }
        }
        void Arc(VertexHelper v, Vector2 c,float radius,float from,float to,float width,Color tint)
        {
            const int count=40;
            for(int i=0;i<count;i++)
            {
                float a=Mathf.Lerp(from,to,i/(float)count)*Mathf.Deg2Rad;
                float b=Mathf.Lerp(from,to,(i+1)/(float)count)*Mathf.Deg2Rad;
                Line(v,c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,c+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,width,tint);
            }
        }
        void Border(VertexHelper v, Rect r, float inset, float cut, Color tint, float width)
        {
            float l=r.xMin+inset, b=r.yMin+inset, t=r.yMax-inset, q=r.xMax-inset;
            var p=new[]{new Vector2(l+cut,b),new Vector2(q-cut,b),new Vector2(q,b+cut),new Vector2(q,t-cut),new Vector2(q-cut,t),new Vector2(l+cut,t),new Vector2(l,t-cut),new Vector2(l,b+cut)};
            for(int i=0;i<p.Length;i++) Line(v,p[i],p[(i+1)%p.Length],width,tint);
        }
        void Diamond(VertexHelper v, Vector2 c, float size, Color tint)
        {
            var p=new[]{c+Vector2.up*size,c+Vector2.right*size,c+Vector2.down*size,c+Vector2.left*size};
            for(int i=0;i<4;i++) Line(v,p[i],p[(i+1)%4],1.5f,tint);
        }
        void Quad(VertexHelper v,Vector2 a,Vector2 b,Color tint)
        {
            int n=v.currentVertCount;
            v.AddVert(new Vector3(a.x,a.y),tint,Vector2.zero);v.AddVert(new Vector3(a.x,b.y),tint,Vector2.zero);
            v.AddVert(new Vector3(b.x,b.y),tint,Vector2.zero);v.AddVert(new Vector3(b.x,a.y),tint,Vector2.zero);
            v.AddTriangle(n,n+1,n+2);v.AddTriangle(n,n+2,n+3);
        }
        void Line(VertexHelper v,Vector2 a,Vector2 b,float width,Color tint)
        {
            var d=(b-a).normalized;var s=new Vector2(-d.y,d.x)*width*.5f;int n=v.currentVertCount;
            v.AddVert(a-s,tint,Vector2.zero);v.AddVert(a+s,tint,Vector2.zero);v.AddVert(b+s,tint,Vector2.zero);v.AddVert(b-s,tint,Vector2.zero);
            v.AddTriangle(n,n+1,n+2);v.AddTriangle(n,n+2,n+3);
        }
    }
}
