using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LucidLoop.Gyms.Tests
{
    public sealed class EncounterApproachIntentTests
    {
        static JObject Result(long sequence = 4, bool accepted = true) => new JObject
        { ["loopId"] = "loop-a", ["npcId"] = "ren", ["sequence"] = sequence, ["accepted"] = accepted, ["frame"] = 10 };
        static EncounterApproachIntent Pending()
        { var value = new EncounterApproachIntent(); value.Begin("loop-a","ren",4,0); return value; }
        [Test] public void EligibleSnapshotsNeverCreateConversationIntent()
        {
            var value = new EncounterApproachIntent();
            Assert.That(value.Observe("loop-a",4,11,true,null,"idle",1), Is.Null);
        }
        [Test] public void MatchingAckAndFreshEligibilityOpenOnlyOnce()
        {
            var value = Pending();
            Assert.That(value.Observe("loop-a",4,11,true,null,"idle",1), Is.Null);
            Assert.That(value.AcceptResult(Result()), Is.True);
            Assert.That(value.Observe("loop-a",4,11,true,null,"idle",2), Is.EqualTo("ren"));
            Assert.That(value.Observe("loop-a",4,12,true,null,"idle",3), Is.Null);
        }
        [Test] public void PreTickIdleDoesNotCancelButArrivingAfterTargetMovedDoes()
        {
            var value = Pending(); value.AcceptResult(Result());
            value.Observe("loop-a",4,10,false,"out_of_range","idle",1);
            Assert.That(value.Pending, Is.True);
            value.Observe("loop-a",4,11,false,"out_of_range","moving",2);
            Assert.That(value.Pending, Is.True);
            value.Observe("loop-a",4,12,false,"out_of_range","idle",3);
            Assert.That(value.Pending, Is.False);
            Assert.That(value.Outcome, Is.EqualTo("approach_target_moved"));
            Assert.That(value.Observe("loop-a",4,13,true,null,"idle",4), Is.Null);
        }
        [Test] public void CancelFencesLateAckAndEligibility()
        {
            var value = Pending(); value.Cancel();
            Assert.That(value.AcceptResult(Result()), Is.False);
            Assert.That(value.Observe("loop-a",4,11,true,null,"idle",1), Is.Null);
        }
        [Test] public void OldReplyCannotCancelReplacementIntent()
        {
            var value = Pending(); value.Begin("loop-a","ren",6,1);
            Assert.That(value.AcceptResult(Result(4,false)), Is.False);
            Assert.That(value.Pending, Is.True);
            Assert.That(value.Sequence, Is.EqualTo(6));
        }
        [Test] public void ResetAndReplacementMovementFenceEligibility()
        {
            var value = Pending(); value.AcceptResult(Result());
            Assert.That(value.Observe("loop-b",4,11,true,null,"idle",1), Is.Null);
            Assert.That(value.Pending, Is.False);
            value = Pending(); value.AcceptResult(Result());
            Assert.That(value.Observe("loop-a",5,11,true,null,"idle",1), Is.Null);
            Assert.That(value.Pending, Is.False);
        }
        [Test] public void TimeoutPreventsMuchLaterAutomaticConversation()
        {
            var value = Pending(); value.AcceptResult(Result());
            Assert.That(value.Tick(19.9), Is.False);
            Assert.That(value.Tick(20), Is.True);
            Assert.That(value.Outcome, Is.EqualTo("approach_timed_out"));
            Assert.That(value.Observe("loop-a",4,100,true,null,"idle",100), Is.Null);
        }
        [Test] public void DeniedAndMalformedResultsCannotAuthorizeOpening()
        {
            var value = Pending(); var malformed = Result(); malformed.Remove("frame");
            Assert.That(value.AcceptResult(malformed), Is.False);
            Assert.That(value.Accepted, Is.False);
            Assert.That(value.AcceptResult(Result(4,false)), Is.True);
            Assert.That(value.Pending, Is.False);
        }
        [Test] public void EarlierWorldCannotConfirmPostAckEligibility()
        {
            var value = Pending(); value.AcceptResult(Result());
            Assert.That(value.Observe("loop-a",3,12,true,null,"idle",1), Is.Null);
            Assert.That(value.Observe("loop-a",4,9,true,null,"idle",1), Is.Null);
            Assert.That(value.Pending, Is.True);
        }
        [Test] public void MissingAndMalformedEligibilityFailClosed()
        {
            Assert.That(EncounterConversationEligibility.TryRead(null,"ren",out var eligible,out _), Is.False);
            Assert.That(eligible, Is.False);
            Assert.That(EncounterConversationEligibility.TryRead(JObject.Parse("{ren:{eligible:'true'}}"),"ren",out eligible,out _), Is.False);
            Assert.That(eligible, Is.False);
            Assert.That(EncounterConversationEligibility.TryRead(JObject.Parse("{ren:{eligible:true}}"),"ren",out eligible,out _), Is.True);
            Assert.That(eligible, Is.True);
        }
    }
}
