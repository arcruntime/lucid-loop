using UnityEngine;

namespace LucidLoop.Gyms
{
    public class CharacterActor : MonoBehaviour
    {
        public string Id;
        public string DisplayName;
        public string Role;
        [TextArea] public string Greeting;
        public Color Accent = Color.cyan;
        public Texture2D Reference;
        public Transform Visual;
        public Transform Mouth;
        public bool IsPlayer;
        public bool HighContrastLabel;
        Transform labelBacking;
        Material labelMaterial;
        TextMesh label;
        Camera view;
        void Start(){
            label=GetComponentInChildren<TextMesh>();view=Camera.main;
            if(HighContrastLabel && label){
                label.color=Color.white;
                var backing=GameObject.CreatePrimitive(PrimitiveType.Quad);backing.name="Name label backing";
                Destroy(backing.GetComponent<Collider>());labelBacking=backing.transform;labelBacking.SetParent(label.transform,false);
                labelMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));labelMaterial.SetColor("_BaseColor",new Color(.015f,.02f,.035f));
                backing.GetComponent<Renderer>().sharedMaterial=labelMaterial;
            }
        }
        void LateUpdate()
        {
            if(!label||!view)return;
            label.transform.rotation=view.transform.rotation;
            label.characterSize=view.orthographicSize*.012f;
            if(labelBacking){
                var bounds=label.GetComponent<Renderer>().localBounds;
                labelBacking.localPosition=new Vector3(bounds.center.x,bounds.center.y,.025f);
                labelBacking.localScale=new Vector3(bounds.size.x+.18f,bounds.size.y+.10f,1);
            }
        }
        void OnDestroy(){if(labelMaterial)Destroy(labelMaterial);}
        public Vector3 FacePosition => transform.position + Vector3.up * 1.72f;
        public void SetSpeech(float amount)
        {
            if (Mouth != null) Mouth.localScale = new Vector3(.11f, .018f + Mathf.Clamp01(amount) * .12f, .035f);
        }
    }
}
