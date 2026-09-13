using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LucidLoop.Gyms.Tests
{
    public sealed class EncounterClientStateTests
    {
        static JObject Snapshot(int index = 1, long revision = 1, string loopId = "loop-1") => new JObject
        {
            ["loopId"] = loopId, ["loopIndex"] = index, ["revision"] = revision, ["mood"] = "Aggressive",
            ["actors"] = new JObject { ["maya"] = new JObject { ["action"] = new JObject { ["type"] = "follow", ["targetId"] = "player" } } },
            ["playerDiscoveries"] = new JArray("recording-clue")
        };

        [Test] public void SnapshotDoesNotShareMutableInputOrOutput()
        {
            var state = new EncounterClientState();
            var input = Snapshot();
            Assert.That(state.TryApply(input, out _), Is.True);
            input["actors"]["maya"]["action"]["type"] = "wait";
            ((JArray)input["playerDiscoveries"]).Clear();
            state.Snapshot["mood"] = "Intimate";
            state.PlayerDiscoveries.Clear();
            Assert.That(state.Actors["maya"].Type, Is.EqualTo("follow"));
            Assert.That(state.Mood, Is.EqualTo("Aggressive"));
            Assert.That((string)state.PlayerDiscoveries[0], Is.EqualTo("recording-clue"));
        }

        [Test] public void DuplicateAndStaleUpdatesCannotUndoAcceptedWait()
        {
            var state = new EncounterClientState();
            var accepted = Snapshot(revision: 4);
            accepted["actors"]["maya"]["action"] = new JObject { ["type"] = "wait" };
            Assert.That(state.TryApply(accepted, out _), Is.True);
            Assert.That(state.TryApply(Snapshot(revision: 4), out var duplicate), Is.False);
            Assert.That(duplicate, Is.EqualTo("duplicate_revision"));
            Assert.That(state.TryApply(Snapshot(revision: 3), out var stale), Is.False);
            Assert.That(stale, Is.EqualTo("stale_revision"));
            Assert.That(state.Actors["maya"].Type, Is.EqualTo("wait"));
        }

        [Test] public void NewLoopCanRestartRevisionButOldLoopCannotReturn()
        {
            var state = new EncounterClientState();
            Assert.That(state.TryApply(Snapshot(revision: 900), out _), Is.True);
            Assert.That(state.TryApply(Snapshot(2, 0, "loop-2"), out _), Is.True);
            Assert.That(state.TryApply(Snapshot(1, 9999), out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("stale_loop"));
            Assert.That(state.LoopId, Is.EqualTo("loop-2"));
            Assert.That(state.Revision, Is.Zero);
        }

        [Test] public void InconsistentLoopIdentityCannotReplaceState()
        {
            var state = new EncounterClientState();
            state.TryApply(Snapshot(), out _);
            Assert.That(state.TryApply(Snapshot(1, 2, "different-loop"), out _), Is.False);
            Assert.That(state.TryApply(Snapshot(2, 2, "loop-1"), out _), Is.False);
            Assert.That(state.LoopIndex, Is.EqualTo(1));
        }

        [Test] public void MalformedActorRejectsWholeUpdateWithoutPartialCommit()
        {
            var state = new EncounterClientState();
            state.TryApply(Snapshot(), out _);
            var invalid = Snapshot(revision: 2);
            invalid["mood"] = "Intimate";
            invalid["actors"]["theo"] = new JObject { ["action"] = new JObject { ["type"] = "kill" } };
            Assert.That(state.TryApply(invalid, out _), Is.False);
            Assert.That(state.Mood, Is.EqualTo("Aggressive"));
            Assert.That(state.Revision, Is.EqualTo(1));
            Assert.That(state.Actors.ContainsKey("theo"), Is.False);
        }

        [Test] public void MissingFollowTargetAndNonIntegerRevisionAreRejected()
        {
            var state = new EncounterClientState();
            var noTarget = Snapshot();
            ((JObject)noTarget["actors"]["maya"]["action"]).Remove("targetId");
            Assert.That(state.TryApply(noTarget, out _), Is.False);
            var wrongRevision = Snapshot();
            wrongRevision["revision"] = "1";
            Assert.That(state.TryApply(wrongRevision, out _), Is.False);
            Assert.That(state.HasSnapshot, Is.False);
        }

        [Test] public void ProjectionDropsUnrelatedServerFields()
        {
            var input = Snapshot();
            input["privateTruth"] = "not part of client projection";
            input["actors"]["maya"]["secret"] = "hidden";
            var state = new EncounterClientState();
            Assert.That(state.TryApply(input, out _), Is.True);
            Assert.That(state.Snapshot["privateTruth"], Is.Null);
            Assert.That(state.Snapshot["actors"]["maya"]["secret"], Is.Null);
        }

        [TestCase("approach", "maya")]
        [TestCase("intervene", "maya")]
        [TestCase("keep_distance", "maya")]
        [TestCase("separate", "vip")]
        [TestCase("fall", null)]
        public void AuthoredDemoActionsSurviveProjection(string type, string target)
        {
            var input = Snapshot();
            var action = new JObject { ["type"] = type };
            if (target != null) action["targetId"] = target;
            input["actors"]["theo"] = new JObject { ["action"] = action };
            var state = new EncounterClientState();
            Assert.That(state.TryApply(input, out _), Is.True);
            Assert.That(state.Actors["theo"].Type, Is.EqualTo(type));
            Assert.That(state.Actors["theo"].TargetId, Is.EqualTo(target));
        }

        [Test] public void UnknownActionTargetCannotEnterProjection()
        {
            var input = Snapshot();
            input["actors"]["maya"]["action"]["targetId"] = "invented_character";
            Assert.That(new EncounterClientState().TryApply(input, out _), Is.False);
        }

        [Test] public void ServerClockIsPreservedAndNonFiniteTimeIsRejected()
        {
            var input = Snapshot(); input["elapsedSeconds"] = 12.5; input["durationSeconds"] = 180;
            input["phase"] = "exploring";
            var state = new EncounterClientState();
            Assert.That(state.TryApply(input, out _), Is.True);
            Assert.That(state.ElapsedSeconds, Is.EqualTo(12.5));
            Assert.That(state.DurationSeconds, Is.EqualTo(180));
            input["revision"] = 2; input["elapsedSeconds"] = double.NaN;
            Assert.That(state.TryApply(input, out _), Is.False);
            Assert.That(state.ElapsedSeconds, Is.EqualTo(12.5));
        }
    }
}
