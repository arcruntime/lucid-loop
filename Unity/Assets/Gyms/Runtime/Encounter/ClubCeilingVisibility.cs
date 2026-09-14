using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace LucidLoop.Gyms
{
    // The reference's cutaway overview and enclosed conversation room share one set.
    [ExecuteAlways]
    public sealed class ClubCeilingVisibility : MonoBehaviour
    {
        public Renderer[] Ceiling;
        public Renderer[] Haze;
        public Light Key;
        public Volume ConversationFocus;
        bool adjusted;
        Quaternion keyRotation;
        Color keyColor;
        float keyIntensity;
        LightShadows keyShadows;
        float previousFocusWeight;
        bool focusAdjusted;
        Camera backdropCamera;
        CameraClearFlags previousClearFlags;
        readonly Dictionary<Material,float> energies=new Dictionary<Material,float>();
        void OnEnable(){RenderPipelineManager.beginCameraRendering+=BeforeCamera;RenderPipelineManager.endCameraRendering+=AfterCamera;}
        void BeforeCamera(ScriptableRenderContext context,Camera camera)
        {
            RestoreBackdrop();
            bool cutaway=camera.orthographic&&camera.orthographicSize>3;
            if(cutaway){backdropCamera=camera;previousClearFlags=camera.clearFlags;camera.clearFlags=CameraClearFlags.SolidColor;}
            if(Ceiling!=null)foreach(var r in Ceiling)if(r)r.forceRenderingOff=cutaway;
            var rig=camera.GetComponent<GymCamera>();
            bool conversation=rig&&rig.Target;
            if(Haze!=null)foreach(var r in Haze)if(r)r.forceRenderingOff=conversation;
            Restore();
            if(ConversationFocus){previousFocusWeight=ConversationFocus.weight;ConversationFocus.weight=conversation?1:0;focusAdjusted=true;}
            if(!conversation||!Key||!Key.gameObject.activeInHierarchy)return;
            adjusted=true;keyRotation=Key.transform.rotation;keyColor=Key.color;keyIntensity=Key.intensity;
            keyShadows=Key.shadows;Key.shadows=LightShadows.None;
            Key.transform.rotation=camera.transform.rotation*Quaternion.Euler(18,-20,0);Key.color=new Color(1,.88f,.8f);Key.intensity=.9f;
            var presentation=GetComponent<ClubEnvironmentPresentation>();var m=presentation?presentation.DisplayMaterial:null;
            if(m){energies[m]=m.GetFloat("_Energy");m.SetFloat("_Energy",energies[m]*.20f);}
        }
        void AfterCamera(ScriptableRenderContext context,Camera camera){Restore();RestoreBackdrop();}
        void RestoreBackdrop(){if(backdropCamera)backdropCamera.clearFlags=previousClearFlags;backdropCamera=null;}
        void Restore()
        {
            if(focusAdjusted&&ConversationFocus)ConversationFocus.weight=previousFocusWeight;
            focusAdjusted=false;
            if(!adjusted)return;
            if(Key){Key.transform.rotation=keyRotation;Key.color=keyColor;Key.intensity=keyIntensity;Key.shadows=keyShadows;}
            foreach(var pair in energies)if(pair.Key)pair.Key.SetFloat("_Energy",pair.Value);
            energies.Clear();adjusted=false;
        }
        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering-=BeforeCamera;
            RenderPipelineManager.endCameraRendering-=AfterCamera;Restore();RestoreBackdrop();
            if(Ceiling!=null)foreach(var r in Ceiling)if(r)r.forceRenderingOff=false;
            if(Haze!=null)foreach(var r in Haze)if(r)r.forceRenderingOff=false;
        }
    }
}
