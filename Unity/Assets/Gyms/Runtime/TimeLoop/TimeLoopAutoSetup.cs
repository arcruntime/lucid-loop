using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace LucidLoop.Gyms
{
    public interface ITimeLoopSceneProvider { void ConfigureTimeLoop(TimeLoopTransitionController controller); }
    [DisallowMultipleComponent]
    public sealed class TimeLoopAutoSetup : MonoBehaviour
    {
        IEnumerator Start()
        {
            yield return null;yield return null;
            var controller=GetComponent<TimeLoopTransitionController>();
            if(controller.ResetAdapter || controller.IsTransitioning)yield break;
            foreach(var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if(behaviour is ITimeLoopSceneProvider provider){provider.ConfigureTimeLoop(controller);yield break;}
            if(!controller.ResetAdapter)
            {
                ConfigureCommon(controller,Camera.main);
                var local=Ensure<LocalLoopCheckpoint>(gameObject);
                local.Actors=controller.Actors;local.Participants=FindObjectsByType<LoopCheckpointParticipant>(FindObjectsSortMode.None);local.CaptureCheckpoint();controller.ResetAdapter=local;
            }
        }
        public static T Ensure<T>(GameObject gameObject) where T:Component
        {var component=gameObject.GetComponent<T>();return component?component:gameObject.AddComponent<T>();}
        public static void ConfigureCommon(TimeLoopTransitionController controller,Camera camera)
        {
            if(camera)controller.GameplayCamera=camera;
            var registered=new List<LoopRewindActor>();var labels=new List<TextMesh>(controller.GameplayLabels);
            foreach(var actor in FindObjectsByType<CharacterActor>(FindObjectsSortMode.None))
            {
                foreach(var label in actor.GetComponentsInChildren<TextMesh>(true))if(!labels.Contains(label))labels.Add(label);
                var history=TimeLoopAutoSetup.Ensure<LoopRewindActor>(actor.gameObject);
                if(actor.Visual && history.VisualTransforms.Length==0){var transforms=new List<Transform>();foreach(var t in actor.Visual.GetComponentsInChildren<Transform>(true))transforms.Add(t);history.VisualTransforms=transforms.ToArray();}
                var writers=new List<Behaviour>(history.MovementWriters);
                foreach(var b in actor.GetComponentsInChildren<MonoBehaviour>(true))if(IsKnownWriter(b) && !writers.Contains(b))writers.Add(b);
                history.MovementWriters=writers.ToArray();history.Initialize();registered.Add(history);
            }
            foreach(var history in FindObjectsByType<LoopRewindActor>(FindObjectsSortMode.None))if(!registered.Contains(history))registered.Add(history);
            foreach(var actor in controller.Actors)if(actor && !registered.Contains(actor))registered.Add(actor);
            controller.Actors=registered.ToArray();controller.GameplayLabels=labels.ToArray();
            var activity=new List<Behaviour>(controller.WorldActivity);
            foreach(var b in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))if(IsKnownWriter(b) && !activity.Contains(b))activity.Add(b);
            controller.WorldActivity=activity.ToArray();
            var groups=new List<CanvasGroup>(controller.GameplayUI);
            foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if(canvas.renderMode==RenderMode.WorldSpace || canvas.isRootCanvas){var group=TimeLoopAutoSetup.Ensure<CanvasGroup>(canvas.gameObject);if(!groups.Contains(group))groups.Add(group);}
            }
            controller.GameplayUI=groups.ToArray();
            controller.GameplayAudio=FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            if(!controller.RewindClip)controller.RewindClip=Resources.Load<AudioClip>("MvpAudio/RecordScratch")??Resources.Load<AudioClip>("MvpAudio/Backspin");
        }
        static bool IsKnownWriter(MonoBehaviour b)
        {
            // Project adapters opt in by exact type; no arbitrary-script reversal.
            string name=b.GetType().Name;
            return name=="CastVisual" || name=="GymCamera" || name=="ClubLighting" || name=="NpcActionExecutor" || name=="EncounterPrimitivePresentation" || name=="CharacterActor" || name=="PlayerMotor";
        }
    }
}
