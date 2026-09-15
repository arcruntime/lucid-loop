using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace LucidLoop.Gyms.PlayModeTests
{
    public sealed class VictoryScreenPlayModeTests
    {
        GameObject root;
        [TearDown] public void Cleanup() { if(root)Object.DestroyImmediate(root);Time.timeScale=1; }
        VictoryScreenController Make()
        {root=new GameObject("Victory test");var screen=root.AddComponent<VictoryScreenController>();screen.RevealDuration=.1f;screen.ReadabilityDelay=.1f;return screen;}
        [Test] public void OnlySuccessfulCompletionQualifies()
        {Assert.IsTrue(VictoryScreenController.VictoryPhase("victory"));foreach(var phase in new[]{"exploring","catastrophe","unresolved","rewinding",null})Assert.IsFalse(VictoryScreenController.VictoryPhase(phase));}
        [UnityTest] public IEnumerator FreshInputAfterReadabilityDelayContinuesExactlyOnce()
        {
            var s=Make();int count=0;s.Continued.AddListener(()=>count++);s.ShowVictoryScreen();s.ShowVictoryScreen();
            s.SubmitInput(true,true);Assert.AreEqual(0,count);Time.timeScale=0;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.IsTrue(s.CanContinue);s.SubmitInput(true,true);Assert.AreEqual(0,count);
            s.SubmitInput(false,false);s.SubmitInput(true,true);s.SubmitInput(true,true);
            yield return null;yield return null;
            Assert.AreEqual(1,count);Assert.IsFalse(s.IsOpen);Assert.IsFalse(VictoryScreenController.InputBlocked);
        }
        [UnityTest] public IEnumerator DisableCleansUpAndAllowsAnotherShow()
        {
            var s=Make();s.ShowVictoryScreen();yield return null;
            Assert.IsTrue(VictoryScreenController.InputBlocked);s.enabled=false;
            Assert.IsFalse(VictoryScreenController.InputBlocked);s.enabled=true;s.ShowVictoryScreen();yield return null;
            Assert.IsTrue(s.IsOpen);s.HideVictoryScreen();Assert.IsFalse(s.IsOpen);
        }
        [UnityTest] public IEnumerator EncounterVictoryDeduplicatesAndRestoresHudVisibility()
        {
            var s=Make();var adapter=root.AddComponent<EncounterVictoryPresentation>();adapter.Screen=s;
            var canvasObject=new GameObject("Gameplay canvas",typeof(Canvas));var canvas=canvasObject.GetComponent<Canvas>();
            int shows=0;s.Shown.AddListener(()=>shows++);yield return null;
            try
            {
                adapter.ObserveCompletion("catastrophe","one");Assert.IsFalse(s.IsOpen);
                adapter.ObserveCompletion("victory","one");Assert.IsTrue(s.IsOpen);Assert.IsFalse(canvas.enabled);
                adapter.ObserveCompletion("victory","one");Assert.AreEqual(1,shows);
                s.HideVictoryScreen();Assert.IsTrue(canvas.enabled);
                adapter.ObserveCompletion("victory","one");Assert.IsFalse(s.IsOpen);
            }
            finally {Object.DestroyImmediate(canvasObject);}
        }
        [UnityTest] public IEnumerator ResponsiveOverlayKeepsExplicitTwoLines()
        {
            var s=Make();s.ShowVictoryScreen();yield return null;
            var title=root.transform.Find("Victory overlay/Victory composition/Victory heading");Assert.IsNotNull(title);
            Assert.AreEqual("THE NIGHT HAS BEEN\nSUCCESSFULLY CHANGED.",s.Heading);
            Assert.IsNotNull(root.transform.Find("Victory overlay/Victory composition/Broken loop traces").GetComponent<CanvasRenderer>());
            Assert.IsNotNull(root.transform.Find("Victory overlay/Continue prompt"));
        }
    }
}
