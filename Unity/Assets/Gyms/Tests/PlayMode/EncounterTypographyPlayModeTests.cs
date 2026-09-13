using System;
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LucidLoop.Gyms.PlayModeTests
{
    public sealed class EncounterTypographyPlayModeTests
    {
        [UnityTest, Explicit("Actual scene Japanese text rendering; no relay, provider or microphone.")]
        public IEnumerator BundledJapaneseFontRendersPhoneConversation()
        {
            var loading = SceneManager.LoadSceneAsync("BeforeTheDrop", LoadSceneMode.Single);
            while (!loading.isDone) yield return null;
            yield return null;
            var hud = UnityEngine.Object.FindFirstObjectByType<EncounterHud>();
            var layout = UnityEngine.Object.FindFirstObjectByType<EncounterHudLayout>();
            var font = GymUI.Font;
            Assert.That(font, Is.SameAs(Resources.Load<Font>("Fonts/NotoSansJP-Regular")));
            Assert.That(GymUI.Font, Is.SameAs(font), "HUD labels share a cached font asset.");
            const string characters = "日本語漢字ひらがなカタカナ。、！？「」髙﨑ABCxyz0123456789";
            font.RequestCharactersInTexture(characters, 32, FontStyle.Normal);
            foreach (char character in characters)
                Assert.That(font.HasCharacter(character), Is.True, "Missing glyph U+" + ((int)character).ToString("X4"));
            layout.PreviewPhoneLayout = true;
            layout.ConversationExpanded = true;
            layout.Connection.gameObject.SetActive(false);
            hud.AppendTranscriptFragment(new JObject { ["role"] = "user", ["text"] = "ルカ、ここで待って。マヤと話してから戻ります。" });
            hud.AppendTranscriptFragment(new JObject { ["role"] = "assistant", ["text"] =
                "わかった。ここで待つよ。髙橋さんと﨑田さんにも伝えておくね。\n" +
                "「音楽を静かにして」とRenに頼んでから、Theoのところへ行こう。会話、再開、設定、音量。\n" +
                "ひらがな・カタカナ・漢字・ABC 123。\n" +
                "𠮷 — supplementary-name rendering sample." });
            yield return null;
            Canvas.ForceUpdateCanvases();
            // HUD canvas is a separate root owned by the HUD component.
            var text = layout.Conversation.GetComponentsInChildren<Text>().Single(t => t.name == "Transcript content");
            Assert.That(text.font, Is.SameAs(font));
            Assert.That(text.cachedTextGenerator.lineCount, Is.GreaterThan(4), "Japanese transcript wraps in the phone panel.");
            Assert.That(text.cachedTextGenerator.vertexCount, Is.GreaterThan(40));
            var reply = layout.Conversation.GetComponentsInChildren<InputField>().Single();
            reply.text = "録画を止めてください。";
            yield return new WaitForEndOfFrame();
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.local/validation/japanese-hud-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".png"));
            ScreenCapture.CaptureScreenshot(path);
            float deadline = Time.realtimeSinceStartup + 10;
            while (!File.Exists(path) || new FileInfo(path).Length < 24)
            { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
            layout.Conversation.GetComponentInChildren<ScrollRect>().verticalNormalizedPosition = 0;
            yield return new WaitForEndOfFrame();
            path = path.Replace("japanese-hud-", "japanese-hud-bottom-");
            ScreenCapture.CaptureScreenshot(path);
            deadline = Time.realtimeSinceStartup + 10;
            while (!File.Exists(path) || new FileInfo(path).Length < 24)
            { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
            LogAssert.NoUnexpectedReceived();
        }
    }
}
