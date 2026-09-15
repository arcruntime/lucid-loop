using UnityEngine;
using UnityEngine.UI;
namespace LucidLoop.Gyms
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class InformationGemGraphic : MaskableGraphic
    {
        public bool Unread=true;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();float radius=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.22f;
            if(Unread){Diamond(vh,radius*1.7f,.04f);Diamond(vh,radius*1.3f,.13f);Diamond(vh,radius,.95f);Diamond(vh,radius*.5f,1);}
            else
            {
                var p=new[]{Vector2.up,Vector2.right,Vector2.down,Vector2.left};
                for(int i=0;i<4;i++)
                {int n=vh.currentVertCount;var c=color;c.a*=.65f;vh.AddVert(rectTransform.rect.center+p[i]*radius,c,Vector2.zero);vh.AddVert(rectTransform.rect.center+p[(i+1)%4]*radius,c,Vector2.zero);vh.AddVert(rectTransform.rect.center+p[(i+1)%4]*(radius-1.8f),c,Vector2.zero);vh.AddVert(rectTransform.rect.center+p[i]*(radius-1.8f),c,Vector2.zero);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);}
            }
        }
        void Diamond(VertexHelper vh,float radius,float alpha)
        {
            int n=vh.currentVertCount;var c=color;c.a*=alpha;
            vh.AddVert(rectTransform.rect.center+Vector2.up*radius,c,Vector2.zero);vh.AddVert(rectTransform.rect.center+Vector2.right*radius,c,Vector2.zero);
            vh.AddVert(rectTransform.rect.center+Vector2.down*radius,c,Vector2.zero);vh.AddVert(rectTransform.rect.center+Vector2.left*radius,c,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
    }
}
