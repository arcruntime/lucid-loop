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
