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
        public Transform ConversationFaceAnchor;
        public float ConversationSize = 1.05f;
        public float ConversationHorizontalOffset = .6f;
        public float ConversationVerticalOffset = -.14f;
        public bool IsPlayer;
        TextMesh label;
        Camera view;
        void Start(){label=GetComponentInChildren<TextMesh>();view=Camera.main;}
        void LateUpdate()
        {
            if(!label||!view)return;
            label.transform.rotation=view.transform.rotation;
            label.characterSize=view.orthographicSize*.012f;
        }
        public Vector3 FacePosition => ConversationFaceAnchor ? ConversationFaceAnchor.position : transform.position + Vector3.up * 1.72f;
        public void SetSpeech(float amount)
        {
            if (Mouth != null) Mouth.localScale = new Vector3(.11f, .018f + Mathf.Clamp01(amount) * .12f, .035f);
        }
    }
}
