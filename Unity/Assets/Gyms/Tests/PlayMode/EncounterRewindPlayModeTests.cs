using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace LucidLoop.Gyms.PlayModeTests
{
    public sealed class EncounterRewindPlayModeTests
    {
        [UnityTest]
        public IEnumerator PreviewUsesUnscaledTimeAndCleansUpOnDisable()
        {
            yield return SceneManager.LoadSceneAsync("BeforeTheDrop");yield return null;
            var hud=Object.FindFirstObjectByType<EncounterHud>();
            var canvas=Object.FindFirstObjectByType<EncounterHudLayout>().SafeRoot.GetComponentInParent<Canvas>();
            var effect=hud.RewindTransition;
            float original=Time.timeScale;
            try
            {
                Time.timeScale=0;
                effect.Preview();effect.Preview();
                yield return new WaitForSecondsRealtime(.4f);
                Assert.That(effect.IsPlaying,Is.True);
                Assert.That(effect.Progress,Is.GreaterThan(0));
                Assert.That(canvas.GetComponent<CanvasGroup>().alpha,Is.LessThan(.01f));
                Assert.That(GameObject.Find("Time loop transition UI"),Is.Not.Null);
                effect.enabled=false;yield return null;
                Assert.That(effect.IsPlaying,Is.False);
                Assert.That(canvas.enabled,Is.True);
                Assert.That(GameObject.Find("Time loop transition UI"),Is.Null);
                Assert.That(hud.Coordinator.State.HasSnapshot,Is.False,"Preview must not create or reset a game.");
            }
            finally{effect.enabled=false;Time.timeScale=original;effect.enabled=true;}
        }
    }
}
