using System.Collections;
using System.IO;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace LucidLoop.Gyms.PlayModeTests
{
    public sealed class EncounterDialoguePlayModeTests
    {
        [UnityTest, Explicit("Actual scene dialogue layout and screenshot; no provider required.")]
        public IEnumerator PortraitDialogueExpandsAndReturnsToExploration()
        {
            yield return SceneManager.LoadSceneAsync("BeforeTheDrop");
            yield return null;
            var hud=Object.FindFirstObjectByType<EncounterHud>();
            var layout=Object.FindFirstObjectByType<EncounterHudLayout>();
            layout.Connection.gameObject.SetActive(false);
            layout.Conversation.Find("Select theo").GetComponent<Button>().onClick.Invoke();
            layout.ConversationExpanded=true;
            hud.AppendTranscriptFragment(new JObject { ["role"]="assistant",["text"]="You’re asking about the blackout? Funny.\nYou’re not the first." });
            yield return new WaitForEndOfFrame();
            Assert.That(layout.Conversation.Find("Dialogue portrait"),Is.Null);
            Assert.That(layout.Conversation.Find("Microphone/Microphone icon"),Is.Not.Null);
            Assert.That(layout.Conversation.Find("Send/Send icon"),Is.Not.Null);
            foreach(var text in layout.Conversation.GetComponentsInChildren<Text>())
                Assert.That(text.text,Does.Not.Contain("Enter to send").And.Not.Contain("Hold V"));
            foreach(var graphic in layout.Conversation.GetComponentsInChildren<DecoDialogueFrame>())Assert.That(graphic.raycastTarget,Is.False);
            var input=layout.Conversation.Find("Reply area").GetComponent<InputField>();
            input.text="Tell me what happened.";
            Assert.That(input.text,Is.EqualTo("Tell me what happened."));
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.local/validation"));Directory.CreateDirectory(folder);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"art-deco-dialogue-desktop.png"));
            yield return new WaitForSecondsRealtime(.5f);
            layout.PreviewPhoneLayout=true;
            yield return new WaitForEndOfFrame();
            var corners=new Vector3[4];layout.Conversation.GetWorldCorners(corners);
            foreach(var corner in corners)
            {
                var local=layout.SafeRoot.InverseTransformPoint(corner);
                Assert.That(local.x,Is.InRange(layout.SafeRoot.rect.xMin,layout.SafeRoot.rect.xMax));
                Assert.That(local.y,Is.InRange(layout.SafeRoot.rect.yMin,layout.SafeRoot.rect.yMax));
            }
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"art-deco-dialogue-phone.png"));
            yield return new WaitForSecondsRealtime(.5f);
            layout.ConversationExpanded=false;
            yield return new WaitForEndOfFrame();
            Assert.That(layout.Conversation.Find("Character nameplate").gameObject.activeSelf,Is.False);
            Assert.That(layout.Conversation.Find("Reply area").gameObject.activeSelf,Is.False);
            Assert.That(layout.Guidance.gameObject.activeSelf,Is.True);
        }
    }
}
