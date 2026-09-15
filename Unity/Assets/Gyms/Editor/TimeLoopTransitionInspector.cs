using UnityEditor;
using UnityEngine;
namespace LucidLoop.Gyms.Editor
{
    [CustomEditor(typeof(TimeLoopTransitionController))]
    public sealed class TimeLoopTransitionInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();var controller=(TimeLoopTransitionController)target;
            EditorGUILayout.Space();EditorGUILayout.LabelField("State",controller.IsTransitioning?"Transition · stage "+(controller.StageIndex+1):"Ready");
            using(new EditorGUI.DisabledScope(!Application.isPlaying || controller.IsTransitioning))
                if(GUILayout.Button("Trigger loop"))controller.TriggerLoop();
            using(new EditorGUI.DisabledScope(!controller.IsTransitioning))
                if(GUILayout.Button("Cancel and restore control"))controller.CancelTransition();
            if(!string.IsNullOrEmpty(controller.LastError))EditorGUILayout.HelpBox(controller.LastError,MessageType.Warning);
        }
    }
}
