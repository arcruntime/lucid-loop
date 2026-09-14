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

        [UnityTest]
        public IEnumerator CatastropheSilencesBothTracksWhilePausedAndRewindRestoresPreference()
        {
            var root = new GameObject("catastrophe music test");
            var coordinator = root.AddComponent<EncounterCoordinator>();
            root.AddComponent<AudioListener>();
            var mood = root.AddComponent<EncounterMoodPresentation>();
            mood.Coordinator = coordinator;
            mood.MusicVolume = .27f;
            mood.MoodTransitionSeconds = .1f;
            mood.AggressiveLoop = AudioClip.Create("catastrophe aggressive", 24000, 1, 24000, false);
            mood.IntimateLoop = AudioClip.Create("catastrophe intimate", 24000, 1, 24000, false);
            try
            {
                yield return null;
                ApplyMood(coordinator, "Intimate", 1);
                yield return new WaitForSecondsRealtime(.2f);
                var sources = root.GetComponents<AudioSource>();
                Assert.That(sources.Sum(source => source.volume), Is.EqualTo(.27f).Within(.001f));
                ApplyMood(coordinator, "Intimate", 2, "catastrophe");
                root.SendMessage("OnPause", true);
                yield return new WaitForSecondsRealtime(.3f);
                foreach (var source in sources) Assert.That(source.volume, Is.Zero.Within(.001f), "Catastrophe must silence each club track even while paused.");
                Assert.That(mood.MusicVolume, Is.EqualTo(.27f), "Do not overwrite the player's volume preference.");
                mood.enabled = false;
                mood.enabled = true;
                yield return null;
                foreach (var source in sources) Assert.That(source.volume, Is.Zero.Within(.001f), "Rebinding the same catastrophe must not restore music.");
                ApplyMood(coordinator, "Aggressive", 3, "exploring", 2);
                root.SendMessage("OnPause", false);
                yield return new WaitForSecondsRealtime(1f);
                Assert.That(sources.Single(source => source.clip == mood.AggressiveLoop).volume, Is.EqualTo(.27f).Within(.001f));
                Assert.That(sources.Single(source => source.clip == mood.IntimateLoop).volume, Is.Zero.Within(.001f));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.DestroyImmediate(mood.AggressiveLoop);
                Object.DestroyImmediate(mood.IntimateLoop);
                Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator BackspinRequiresANewAuthoritativeLoopAndStopsOnPause()
        {
            var root = new GameObject("rewind cue test");
            var coordinator = root.AddComponent<EncounterCoordinator>();
            var mood = root.AddComponent<EncounterMoodPresentation>(); mood.Coordinator = coordinator;
            try
            {
                yield return null;
                var cue = root.GetComponents<AudioSource>().Single(source => !source.loop);
                Assert.That(cue.clip, Is.Not.Null, "Imported collaborator backspin must be bundled.");
                ApplyMood(coordinator, "Aggressive", 1);
                Assert.That(cue.isPlaying, Is.False, "Initial snapshot is not a rewind.");
                ApplyMood(coordinator, "Aggressive", 2, "exploring", 2);
                Assert.That(cue.isPlaying, Is.True);
                Assert.That(cue.volume, Is.EqualTo(mood.MusicVolume * .6f).Within(.001f));
                cue.Stop();
                ApplyMood(coordinator, "Aggressive", 3, "exploring", 2);
                Assert.That(cue.isPlaying, Is.False, "Ordinary snapshots cannot replay the cue.");
                ApplyMood(coordinator, "Aggressive", 4, "exploring", 3);
                root.SendMessage("OnPause", true);
                Assert.That(cue.isPlaying, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void ApplyMood(EncounterCoordinator coordinator, string value, long revision, string phase = "exploring", int loopIndex = 1)
        {
            var snapshot = new JObject { ["loopId"] = "mood-loop-" + loopIndex, ["loopIndex"] = loopIndex,
                ["revision"] = revision, ["mood"] = value, ["phase"] = phase, ["actors"] = new JObject(), ["playerDiscoveries"] = new JArray() };
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
