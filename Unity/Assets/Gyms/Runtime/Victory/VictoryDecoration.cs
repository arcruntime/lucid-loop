using UnityEngine;
using UnityEngine.UI;

namespace LucidLoop.Gyms
{
    // Resolution-independent geometry; no textures or fullscreen render pass required.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class VictoryDecoration : MaskableGraphic
    {
        public enum Shape { Ornament, Arcs, Vignette }
        public Shape Kind;
        [Range(0, 1)] public float Reveal = 1;
        [Range(0, 1)] public float Glow = .18f;
        public float Clock;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect;
            if (Kind == Shape.Ornament)
            {
                float end = r.width * .5f * Reveal;
                for (int side = -1; side <= 1; side += 2)
                    for (int i = 0; i < 48; i++)
                    {
                        float a = Mathf.Lerp(28, Mathf.Max(28, end), i / 48f);
                        float b = Mathf.Lerp(28, Mathf.Max(28, end), (i + 1) / 48f);
                        Stroke(vh, new Vector2(a * side, 0), new Vector2(b * side, 0), 1.15f,
                            color.a * Reveal * Mathf.Pow(1 - i / 48f, .65f));
                    }
                // Concave four-point glint with a subtly luminous larger silhouette.
                Glint(vh, 22, .10f * Reveal); Glint(vh, 17, .95f * Reveal);
            }
            else if (Kind == Shape.Arcs)
            {
                float radius = Mathf.Min(r.width, r.height) * .46f;
                for (int ring = 0; ring < 3; ring++)
                {
                    float start = -154 + ring * 41, sweep = 265 - ring * 36;
                    for (int i = 0; i < 190; i++)
                    {
                        float t = i / 190f, u = (i + 1) / 190f;
                        float angle = (start + sweep * t) * Mathf.Deg2Rad;
                        float next = (start + sweep * u) * Mathf.Deg2Rad;
                        float rad = radius * (1 + (ring - 1) * .052f);
                        var a = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * rad;
                        var b = new Vector2(Mathf.Cos(next), Mathf.Sin(next)) * rad;
                        float highlight = Mathf.Pow(Mathf.Max(0, Mathf.Cos(angle + .8f)), 12);
                        float alpha = Mathf.Sin(t * Mathf.PI) * (.07f + highlight * .52f) * color.a * Reveal;
                        alpha *= 1 + .06f * Mathf.Sin(Clock * .6f + t * 9 + ring);
                        Stroke(vh, a, b, 34, alpha * Glow * .08f);
                        Stroke(vh, a, b, 11, alpha * Glow * .25f);
                        Stroke(vh, a, b, 1.3f, alpha);
                    }
                }
            }
            else
            {
                // Elliptical soft vignette, transparent throughout the central composition.
                for (int band = 0; band < 20; band++)
                    for (int i = 0; i < 96; i++)
                    {
                        float a = i * Mathf.PI * 2 / 96, b = (i + 1) * Mathf.PI * 2 / 96;
                        float inner = .45f + band * .065f, outer = inner + .065f;
                        var p = new Vector2(Mathf.Cos(a) * r.width * .5f, Mathf.Sin(a) * r.height * .5f);
                        var q = new Vector2(Mathf.Cos(b) * r.width * .5f, Mathf.Sin(b) * r.height * .5f);
                        Quad(vh, p * inner, q * inner, q * outer, p * outer,
                            Mathf.SmoothStep(0, color.a, band / 19f), Mathf.SmoothStep(0, color.a, (band + 1) / 19f));
                    }
            }
        }
        void Glint(VertexHelper vh, float size, float alpha)
        {
            Vector2[] points = { new Vector2(0, size * 1.2f), new Vector2(size * .18f, size * .22f),
                new Vector2(size * .8f, 0), new Vector2(size * .18f, -size * .22f), new Vector2(0, -size * 1.2f),
                new Vector2(-size * .18f, -size * .22f), new Vector2(-size * .8f, 0), new Vector2(-size * .18f, size * .22f) };
            for (int i = 0; i < 8; i++) Quad(vh, Vector2.zero, points[i], points[(i + 1) % 8], Vector2.zero, alpha, alpha);
        }
        void Stroke(VertexHelper vh, Vector2 a, Vector2 b, float width, float alpha)
        {
            Vector2 normal = new Vector2(-(b-a).y, (b-a).x).normalized * width * .5f;
            Quad(vh, a-normal, b-normal, b+normal, a+normal, alpha, alpha);
        }
        void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, float alpha, float outer)
        {
            int n = vh.currentVertCount; var tint = color; tint.a = alpha;
            vh.AddVert(a, tint, Vector2.zero); vh.AddVert(b, tint, Vector2.zero); tint.a = outer;
            vh.AddVert(c, tint, Vector2.zero); vh.AddVert(d, tint, Vector2.zero);
            vh.AddTriangle(n,n+1,n+2); vh.AddTriangle(n,n+2,n+3);
        }
    }
}
