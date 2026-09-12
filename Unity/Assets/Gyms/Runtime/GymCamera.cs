using UnityEngine;
using System.Collections.Generic;

namespace LucidLoop.Gyms
{
    public class GymCamera : MonoBehaviour
    {
        public Camera Camera;
        public Transform Player;
        public float Pitch = 45;
        public float Yaw = -18;
        public float OverviewSize = 12;
        public CharacterActor Target;
        public bool Studio;
        public bool Immediate;
        readonly List<Renderer> hidden=new List<Renderer>();
        public void Present(CharacterActor actor)
        {
            RestoreVisibility();Target=actor;
            if(!Studio)
            {
                // This is a solo conversation study. Keep the approach avatar and
                // nearby crowd from standing between the camera and the speaker.
                foreach(var other in FindObjectsByType<CharacterActor>(FindObjectsSortMode.None))
                    if(other!=actor)Hide(other.GetComponentsInChildren<Renderer>());
                var club=FindFirstObjectByType<ClubLighting>();
                if(club)foreach(var dancer in club.Dancers)Hide(dancer.GetComponentsInChildren<Renderer>());
            }
        }
        void Hide(Renderer[] renderers)
        {foreach(var renderer in renderers)if(!renderer.forceRenderingOff){renderer.forceRenderingOff=true;hidden.Add(renderer);}}
        void RestoreVisibility(){foreach(var renderer in hidden)if(renderer)renderer.forceRenderingOff=false;hidden.Clear();}
        public void Overview() { Target = null;RestoreVisibility(); }
        void OnDisable()=>RestoreVisibility();
        void LateUpdate() { Apply(Time.unscaledDeltaTime); }
        public void Apply(float delta)
        {
            if (!Camera) Camera = GetComponent<Camera>();
            var focus = new Vector3(0, 0, 1);
            if (Player && !Studio) focus += new Vector3(Player.position.x, 0, Player.position.z) * .08f;
            Quaternion rotation;
            float size;
            Vector3 position;
            if (Target)
            {
                focus = Target.FacePosition + Vector3.down * .14f;
                var direction = Target.transform.forward;
                position = focus + direction * 3f + Vector3.up * .15f;
                rotation = Quaternion.LookRotation(focus - position);
                // Offset subject left to reserve the right third for conversation controls.
                position += rotation * Vector3.right * .6f;
                size = 1.05f;
            }
            else
            {
                rotation = Quaternion.Euler(Pitch, Yaw, 0);
                position = focus - rotation * Vector3.forward * 35;
                size = OverviewSize;
            }
            float t = Immediate ? 1 : 1 - Mathf.Exp(-delta * 7);
            transform.position = Vector3.Lerp(transform.position, position, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, t);
            Camera.orthographic = true;
            Camera.orthographicSize = Mathf.Lerp(Camera.orthographicSize, size, t);
        }
    }
}
