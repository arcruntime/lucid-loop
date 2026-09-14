using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace LucidLoop.Gyms.Tests
{
    public sealed class EncounterIntentFeedbackTests
    {
        [Test] public void VoiceTransportReportsCommitWithoutInventingTranscript()
        {
            var host = new GameObject("Intent feedback test");
            try
            {
                var voice = host.AddComponent<EncounterVoiceController>();
                string status = null;
                int transcripts = 0;
                voice.StatusChanged += value => status = value;
                voice.TranscriptFragment += _ => transcripts++;
                typeof(EncounterVoiceController).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(voice, new object[] { JObject.Parse("{type:'game.intent_result',ok:true,committed:true,outcome:{accepted:true}}") });
                StringAssert.StartsWith("Request accepted.", status);
                Assert.AreEqual(0, transcripts);
            }
            finally { Object.DestroyImmediate(host); }
        }
        [Test] public void ConfirmedActionSurvivesSpeechDeliveryFailure()
        {
            var result = JObject.Parse("{ok:true,committed:true,outcome:{accepted:true},delivered:false}");
            StringAssert.StartsWith("Request accepted.", EncounterIntentFeedback.Describe(result));
        }
        [TestCase("calmer_music_needed", "Ask Ren")]
        [TestCase("mediation_group_not_ready", "Leave so Luca")]
        [TestCase("safe_departure_in_progress", "move away from Theo")]
        public void ActionableRefusalTravelsThroughVoiceStatusWithoutInventingTranscript(string reason, string expected)
        {
            var host = new GameObject("Refusal feedback test");
            try
            {
                var voice = host.AddComponent<EncounterVoiceController>();
                string status = null; int transcripts = 0;
                voice.StatusChanged += value => status = value;
                voice.TranscriptFragment += _ => transcripts++;
                var result = new JObject { ["type"] = "game.intent_result", ["ok"] = true,
                    ["kind"] = "action_proposal", ["committed"] = false, ["delivered"] = false,
                    ["outcome"] = new JObject { ["accepted"] = false, ["reason"] = reason } };
                typeof(EncounterVoiceController).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(voice, new object[] { result });
                StringAssert.Contains(expected, status);
                StringAssert.DoesNotContain("Request accepted", status);
                Assert.AreEqual(0, transcripts);
            }
            finally { Object.DestroyImmediate(host); }
        }
        [TestCase("{ok:true,committed:false,outcome:{accepted:false}}")]
        [TestCase("{ok:true,committed:true,outcome:{accepted:false}}")]
        [TestCase("{ok:true,committed:'true',outcome:{accepted:true}}")]
        [TestCase("{ok:false,committed:true,outcome:{accepted:true}}")]
        public void UnconfirmedActionsNeverDisplaySuccess(string json)
        { StringAssert.DoesNotContain("Request accepted.", EncounterIntentFeedback.Describe(JObject.Parse(json))); }
        [Test] public void InternalPolicyReasonIsNotExposedAsAClue()
        {
            string text = EncounterIntentFeedback.Describe(JObject.Parse("{ok:true,kind:'action_proposal',committed:false,outcome:{reason:'disclosure_conditions_unmet'}}"));
            StringAssert.DoesNotContain("disclosure", text);
            StringAssert.Contains("not carried out", text);
        }
        [TestCase("no_action", "no action was taken")]
        [TestCase("clarification", "clarify")]
        public void ConversationAndClarificationAreDistinct(string kind, string expected)
        { StringAssert.Contains(expected, EncounterIntentFeedback.Describe(new JObject { ["ok"] = true, ["kind"] = kind })); }
    }
}
