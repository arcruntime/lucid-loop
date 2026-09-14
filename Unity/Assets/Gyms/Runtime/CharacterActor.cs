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
        Material labelMaterial;
        TextMesh label;
        Camera view;
        void Start(){label=GetComponentInChildren<TextMesh>();view=Camera.main;}
        void LateUpdate()
        {
            if(!label||!view)return;
            label.transform.rotation=view.transform.rotation;
            label.characterSize=view.orthographicSize*.012f;
            if(HighContrastLabel)
            {
                if(!labelMaterial){labelMaterial=new Material(Shader.Find("BTD/OutlinedName"));label.GetComponent<Renderer>().sharedMaterial=labelMaterial;}
                label.color=Color.white;
                // Font atlases can rebuild; always use the font's current texture.
                labelMaterial.mainTexture=label.font.material.mainTexture;
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
