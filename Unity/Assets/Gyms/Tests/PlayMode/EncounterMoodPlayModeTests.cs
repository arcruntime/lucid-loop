using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LucidLoop.Gyms.Tests
{
    public sealed class EncounterMoodPlayModeTests
    {
        [UnityTest]
        public IEnumerator IntimateLightingPreservesPulseWithoutCompoundingAndRestoresOriginals()
        {
            var root = new GameObject("mood test");
            var lamp = root.AddComponent<Light>(); lamp.intensity = 8; lamp.color = Color.blue;
            var club = root.AddComponent<ClubLighting>();
            club.Lights = new[] { lamp }; club.Dancers = new Transform[0];
            root.AddComponent<AudioListener>();
            var observer = root.AddComponent<MoodFrameObserver>(); observer.Lamp = lamp;
            var mood = root.AddComponent<EncounterMoodPresentation>();
            mood.Club = club; mood.MoodTransitionSeconds = .1f;
            try
            {
                yield return null;
                root.SendMessage("OnMood", "intimate");
                yield return new WaitForSecondsRealtime(.15f);
                int previousFrame = -1;
                for (int frame = 0; frame < 20; frame++)
                {
                    yield return null;
                    // Observe the completed LateUpdate, not Update's temporary unscaled value.
                    Assert.That(observer.Frame, Is.GreaterThan(previousFrame)); previousFrame = observer.Frame;
                    Assert.That(observer.Intensity, Is.EqualTo(8 * .65f * (1 + Mathf.Sin(observer.SampleTime * .7f) * .2f)).Within(.01f));
                }
                Assert.That(observer.Color.r, Is.GreaterThan(.9f));
                root.SendMessage("OnPause", true);
                yield return null; yield return null;
                Assert.That(observer.Intensity, Is.EqualTo(8 * .65f * (1 + Mathf.Sin(observer.SampleTime * .7f) * .2f)).Within(.01f));
                root.SendMessage("OnPause", false);
                club.enabled = false;
                yield return null; yield return null;
                Assert.That(observer.Intensity, Is.EqualTo(5.2f).Within(.01f));
                mood.enabled = false;
                Assert.That(lamp.intensity, Is.EqualTo(8).Within(.01f));
                Assert.That(lamp.color, Is.EqualTo(Color.blue));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }

    [DefaultExecutionOrder(10000)]
    public sealed class MoodFrameObserver : MonoBehaviour
    {
        public Light Lamp;
        public float Intensity, SampleTime;
        public Color Color;
        public int Frame = -1;
        void LateUpdate()
        {
            Intensity = Lamp.intensity; Color = Lamp.color;
            SampleTime = Time.time; Frame = Time.frameCount;
        }
    }
}
