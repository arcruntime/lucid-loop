using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LucidLoop.Gyms.PlayModeTests
{
    public sealed class EncounterLoadingPlayModeTests
    {
        [UnityTest]
        public IEnumerator LogoFollowsConnectionAndCancelRestoresTheGame()
        {
            var load = SceneManager.LoadSceneAsync("BeforeTheDrop");
            while (!load.isDone) yield return null;
            yield return null;
            var coordinator = Object.FindFirstObjectByType<EncounterCoordinator>();
            var brand = Object.FindFirstObjectByType<EncounterLoadingBrand>();
            Assert.That(brand, Is.Not.Null);
            var panel = (RectTransform)brand.transform.Find("Loading the night");
            Assert.That(panel.gameObject.activeSelf, Is.False);
            // Deterministic connection-state fixture; no remote calls or secrets.
            var connecting = typeof(EncounterCoordinator).GetProperty("IsConnecting", BindingFlags.Instance | BindingFlags.Public);
            connecting.SetValue(coordinator, true);
            typeof(EncounterCoordinator).GetField("deadline", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(coordinator, Time.unscaledTime + 60);
            yield return null; yield return null;
            Assert.That(panel.gameObject.activeSelf, Is.True);
            var logo = panel.Find("Before the Drop logo").GetComponent<RawImage>();
            Assert.That(logo.texture, Is.Not.Null);
            Assert.That(logo.raycastTarget, Is.False);
            Assert.That(logo.rectTransform.rect.width / logo.rectTransform.rect.height,
                Is.EqualTo((float)logo.texture.width / logo.texture.height).Within(.001f));
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.local/validation/project-logo.png"));
            ScreenCapture.CaptureScreenshot(output);
            yield return new WaitForEndOfFrame(); yield return null;
            panel.Find("Cancel loading").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(coordinator.IsConnecting, Is.False);
            Assert.That(panel.gameObject.activeSelf, Is.False);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
