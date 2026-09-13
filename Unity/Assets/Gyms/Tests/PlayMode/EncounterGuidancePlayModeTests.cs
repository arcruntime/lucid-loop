using System.Collections;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LucidLoop.Gyms.PlayModeTests
{
    public sealed class EncounterGuidancePlayModeTests
    {
        [UnityTest, Explicit("Actual HUD with authoritative snapshot fixtures; no provider or microphone.")]
        public IEnumerator GuidanceFollowsPhaseAndDoesNotDeclareEarlyVictory()
        {
            var loading = SceneManager.LoadSceneAsync("BeforeTheDrop", LoadSceneMode.Single);
            while (!loading.isDone) yield return null;
            yield return null;
            var coordinator = Object.FindFirstObjectByType<EncounterCoordinator>();
            var layout = Object.FindFirstObjectByType<EncounterHudLayout>();
            layout.PreviewPhoneLayout = true;
            var label = layout.GuidanceText.GetComponent<Text>();
            var apply = typeof(EncounterCoordinator).GetMethod("ApplySnapshot", BindingFlags.Instance | BindingFlags.NonPublic);
            long revision = 0;
            foreach (var phase in new[] { "exploring", "recording", "theo_approaching", "phone_dispute", "luca_intervening", "separating", "resolved", "victory", "catastrophe", "unresolved" })
            {
                var snapshot = new JObject { ["loopId"] = "guidance-loop", ["loopIndex"] = 1,
                    ["revision"] = ++revision, ["mood"] = "Intimate", ["actors"] = new JObject(),
                    ["playerDiscoveries"] = new JArray(), ["phase"] = phase, ["recording"] = phase == "recording" };
                Assert.That((bool)apply.Invoke(coordinator, new object[] { snapshot, false }), Is.True);
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(label.text, Does.Not.Contain("exposure").And.Not.Contain("private plan"), "Guidance must not reveal undiscovered evidence.");
                if (phase != "exploring") Assert.That(label.text, Does.Not.Contain("Walk toward Theo"));
                if (phase == "separating") Assert.That(label.text, Does.Contain("Walk away"));
                if (phase == "resolved") Assert.That(label.text, Does.Contain("Let the set finish").And.Not.Contain("reached the end"));
                if (phase == "victory") Assert.That(label.text, Does.Contain("reached the end"));
                if (phase == "catastrophe" || phase == "unresolved") Assert.That(label.text, Does.Contain("Rewind"));
                Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1), "Phone guidance must fit: " + phase);
            }
            LogAssert.NoUnexpectedReceived();
        }
    }
}
