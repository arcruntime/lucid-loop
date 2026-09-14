using System;
using UnityEngine;

namespace LucidLoop.Gyms.PlayModeTests
{
    // Test observer only. Ren's facial compositor runs at order 100.
    [DefaultExecutionOrder(200)]
    public sealed class RenSpeechLateFrameRecorder : MonoBehaviour
    {
        [NonSerialized] public Action Observe;
        void LateUpdate() => Observe?.Invoke();
        void OnDestroy() => Observe = null;
    }
}
