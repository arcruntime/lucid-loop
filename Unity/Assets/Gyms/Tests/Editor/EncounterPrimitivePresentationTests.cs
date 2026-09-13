using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace LucidLoop.Gyms.Tests
{
    public sealed class EncounterPrimitivePresentationTests
    {
        GameObject host;
        CharacterActor luca, maya;
        EncounterPrimitivePresentation presentation;

        [SetUp] public void SetUp()
        {
            host = new GameObject("Primitive presentation test");
            luca = Actor("luca"); maya = Actor("maya");
            presentation = host.AddComponent<EncounterPrimitivePresentation>();
            presentation.BindActors(new[] { luca, maya });
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(host);

        CharacterActor Actor(string id)
        {
            var root = new GameObject(id); root.transform.SetParent(host.transform, false);
            root.transform.position = new Vector3(4, .6f, -2); root.transform.rotation = Quaternion.Euler(0, 31, 0);
            var actor = root.AddComponent<CharacterActor>(); actor.Id = id;
            actor.Visual = new GameObject("Replaceable visual").transform; actor.Visual.SetParent(root.transform, false);
            actor.Visual.localPosition = new Vector3(.1f, .05f, .03f); actor.Visual.localRotation = Quaternion.Euler(0, 8, 0);
            Part(actor.Visual, "Torso", new Vector3(0, 1.08f, 0));
            Part(actor.Visual, "Arm", new Vector3(.35f, 1.07f, 0));
            Part(actor.Visual, "Hand", new Vector3(.39f, .73f, 0));
            return actor;
        }
        static void Part(Transform visual, string name, Vector3 position)
        { var part = new GameObject(name).transform; part.SetParent(visual, false); part.localPosition = position; }
        static EncounterClientState State(bool fall = false, bool recording = false, int loop = 1)
        {
            var state = new EncounterClientState();
            Assert.That(state.TryApply(new JObject
            {
                ["loopId"] = "loop-" + loop, ["loopIndex"] = loop, ["revision"] = 1, ["mood"] = "Aggressive",
                ["recording"] = recording, ["playerDiscoveries"] = new JArray(),
                ["actors"] = new JObject
                {
                    ["luca"] = new JObject { ["action"] = new JObject { ["type"] = fall ? "fall" : "idle" } },
                    ["maya"] = new JObject { ["action"] = new JObject { ["type"] = "idle" } }
                }
            }, out _), Is.True);
            return state;
        }

        [Test] public void FallMovesOnlyVisualAndRemainsHeldAcrossFurtherUpdates()
        {
            var rootPosition = luca.transform.position; var rootRotation = luca.transform.rotation;
            var standing = luca.Visual.localRotation;
            presentation.ApplyState(State()); presentation.ApplyState(State(fall: true));
            presentation.AdvancePresentation(.2f);
            Assert.That(Quaternion.Angle(standing, luca.Visual.localRotation), Is.GreaterThan(1).And.LessThan(85));
            presentation.AdvancePresentation(10);
            var fallen = luca.Visual.localRotation; var fallenPosition = luca.Visual.localPosition;
            presentation.ApplyState(State(fall: true)); presentation.AdvancePresentation(30);
            Assert.That(Quaternion.Angle(standing, fallen), Is.EqualTo(85).Within(.01f));
            Assert.That(Quaternion.Angle(fallen, luca.Visual.localRotation), Is.LessThan(.001f));
            Assert.That(luca.Visual.localPosition, Is.EqualTo(fallenPosition));
            Assert.That(luca.transform.position, Is.EqualTo(rootPosition));
            Assert.That(Quaternion.Angle(rootRotation, luca.transform.rotation), Is.LessThan(.001f));
        }

        [Test] public void NewLoopRestoresExactStandingPoseAndHidesPhone()
        {
            var position = luca.Visual.localPosition; var rotation = luca.Visual.localRotation;
            var handPosition = maya.Visual.Find("Hand").localPosition;
            presentation.ApplyState(State(fall: true, recording: true));
            presentation.AdvancePresentation(10);
            presentation.ApplyState(State(loop: 2)); presentation.AdvancePresentation(10);
            Assert.That(luca.Visual.localPosition, Is.EqualTo(position));
            Assert.That(Quaternion.Angle(rotation, luca.Visual.localRotation), Is.LessThan(.001f));
            Assert.That(maya.Visual.Find("Hand").localPosition, Is.EqualTo(handPosition));
            Assert.That(maya.Visual.Find("Encounter recording phone (placeholder)").gameObject.activeSelf, Is.False);
        }

        [Test] public void RecordingRequiresAuthoritativeFlagAndRestoresBothArmAndHand()
        {
            var arm = maya.Visual.Find("Arm"); var hand = maya.Visual.Find("Hand");
            var armPosition = arm.localPosition; var armRotation = arm.localRotation; var handPosition = hand.localPosition;
            var rootPosition = maya.transform.position;
            presentation.ApplyState(State());
            Assert.That(maya.Visual.Find("Encounter recording phone (placeholder)"), Is.Null);
            presentation.ApplyState(State(recording: true));
            Assert.That(maya.Visual.Find("Encounter recording phone (placeholder)").gameObject.activeSelf, Is.True);
            Assert.That(hand.localPosition, Is.Not.EqualTo(handPosition));
            Assert.That(arm.localPosition, Is.Not.EqualTo(armPosition));
            presentation.ApplyState(State());
            Assert.That(hand.localPosition, Is.EqualTo(handPosition));
            Assert.That(arm.localPosition, Is.EqualTo(armPosition));
            Assert.That(Quaternion.Angle(armRotation, arm.localRotation), Is.LessThan(.001f));
            Assert.That(maya.transform.position, Is.EqualTo(rootPosition));
            Assert.That(maya.Visual.Find("Encounter recording phone (placeholder)").gameObject.activeSelf, Is.False);
        }
    }
}
