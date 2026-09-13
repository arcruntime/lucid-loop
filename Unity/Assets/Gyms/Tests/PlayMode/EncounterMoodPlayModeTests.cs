using System.Collections;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
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
            var coordinator = root.AddComponent<EncounterCoordinator>();
            mood.Coordinator = coordinator;
            mood.AggressiveLoop = AudioClip.Create("aggressive test", 2400, 1, 24000, false);
            mood.IntimateLoop = AudioClip.Create("intimate test", 2400, 1, 24000, false);
            try
            {
                yield return null;
                ApplyMood(coordinator, "Aggressive", 1);
                ApplyMood(coordinator, "Intimate", 2);
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
                var sources = root.GetComponents<AudioSource>();
                Assert.That(sources.Single(source => source.clip == mood.IntimateLoop).volume, Is.EqualTo(mood.MusicVolume).Within(.001f));
                Assert.That(sources.Single(source => source.clip == mood.AggressiveLoop).volume, Is.Zero.Within(.001f));
                root.SendMessage("OnPause", true);
                yield return null; yield return null;
                Assert.That(observer.Intensity, Is.EqualTo(8 * .65f * (1 + Mathf.Sin(observer.SampleTime * .7f) * .2f)).Within(.01f));
                root.SendMessage("OnPause", false);
                club.enabled = false;
                yield return null; yield return null;
                Assert.That(observer.Intensity, Is.EqualTo(5.2f).Within(.01f));
                ApplyMood(coordinator, "Aggressive", 3);
                yield return new WaitForSecondsRealtime(.15f);
                Assert.That(observer.Intensity, Is.EqualTo(8).Within(.01f));
                Assert.That(sources.Single(source => source.clip == mood.AggressiveLoop).volume, Is.EqualTo(mood.MusicVolume).Within(.001f));
                Assert.That(sources.Single(source => source.clip == mood.IntimateLoop).volume, Is.Zero.Within(.001f));
                mood.enabled = false;
                Assert.That(lamp.intensity, Is.EqualTo(8).Within(.01f));
                Assert.That(lamp.color, Is.EqualTo(Color.blue));
            }
            finally
            {
                Object.DestroyImmediate(mood.AggressiveLoop);
                Object.DestroyImmediate(mood.IntimateLoop);
                Object.DestroyImmediate(root);
            }
        }

        static void ApplyMood(EncounterCoordinator coordinator, string value, long revision)
        {
            var snapshot = new JObject { ["loopId"] = "mood-loop", ["loopIndex"] = 1,
                ["revision"] = revision, ["mood"] = value, ["actors"] = new JObject(), ["playerDiscoveries"] = new JArray() };
            var apply = typeof(EncounterCoordinator).GetMethod("ApplySnapshot", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That((bool)apply.Invoke(coordinator, new object[] { snapshot, false }), Is.True);
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
