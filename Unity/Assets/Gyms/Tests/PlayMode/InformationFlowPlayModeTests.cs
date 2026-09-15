using System.Collections;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace LucidLoop.Gyms.PlayModeTests
{
    public sealed class InformationFlowPlayModeTests
    {
        GameObject root;
        LearnedInformationManager manager;
        [SetUp]public void Setup(){root=new GameObject("Information test");manager=root.AddComponent<LearnedInformationManager>();}
        [TearDown]public void Cleanup(){Object.DestroyImmediate(root);}
        [Test]public void DeferredFactsPublishOnceAndReadStateSurvivesNextLoop()
        {
            Assert.IsTrue(manager.AddLearnedInformation("one","First clue","",true));Assert.IsFalse(manager.HasUnread);
            manager.NotifyLoopStarted();Assert.IsTrue(manager.HasUnread);manager.MarkRead(new[]{"one"});
            Assert.IsFalse(manager.AddLearnedInformation("one","First clue"));manager.NotifyLoopStarted();Assert.IsFalse(manager.HasUnread);Assert.AreEqual(1,manager.Entries.Count);
        }
        [Test]public void AdapterPublishesRetainedFactsAtLoopBoundaryWithoutDuplicates()
        {
            var adapter=root.AddComponent<EncounterInformationBridge>();adapter.Manager=manager;
            var facts=new JArray(new JObject{["factId"]="affair",["text"]="Theo is having an affair.",["learnedInLoop"]="loop-one"});
            adapter.ObserveSnapshot(1,"loop-one",facts);Assert.IsFalse(manager.HasUnread);
            adapter.ObserveSnapshot(2,"loop-two",facts);Assert.IsTrue(manager.HasUnread);
            manager.MarkRead(new[]{"affair"});adapter.ObserveSnapshot(2,"loop-two",facts);adapter.ObserveSnapshot(3,"loop-three",facts);
            Assert.IsFalse(manager.HasUnread);Assert.AreEqual(1,manager.Entries.Count);
            adapter.ObserveSnapshot(1,"new-game",new JArray());Assert.AreEqual(0,manager.Entries.Count);
        }
        [UnityTest]public IEnumerator IconBannerPanelReadsEntriesAndReopensWithoutPausing()
        {
            var ui=root.AddComponent<InformationNotificationUI>();ui.Initialize(manager);
            manager.AddLearnedInformation("old","Old clue");manager.MarkRead(new[]{"old"});manager.AddLearnedInformation("new","Theo is having an affair.");
            float scale=Time.timeScale;yield return null;
            Assert.IsTrue(ui.IndicatorUnread);Assert.IsFalse(ui.BannerOpen);ui.IconButton.onClick.Invoke();
            Assert.IsTrue(ui.BannerOpen);Assert.AreEqual("Theo is having an affair.",ui.BannerText);Assert.IsTrue(manager.HasUnread);
            ui.BannerButton.onClick.Invoke();Assert.IsTrue(ui.Panel.IsOpen);Assert.IsFalse(ui.BannerOpen);Assert.IsFalse(manager.HasUnread);
            Assert.AreEqual(scale,Time.timeScale);Assert.IsTrue(ui.IndicatorVisible);Assert.IsFalse(ui.IndicatorUnread);
            Assert.IsTrue(root.GetComponentsInChildren<Transform>().Any(t=>t.name=="Unread tag"));
            ui.Panel.CloseButton.onClick.Invoke();Assert.IsFalse(ui.Panel.IsOpen);Assert.IsTrue(InformationNotificationUI.SuppressFloorTap);
            ui.ClickIcon();Assert.IsTrue(ui.Panel.IsOpen);yield return null;
        }
        [UnityTest]public IEnumerator LatestUnreadWinsAndOpenPanelReadsNewArrivals()
        {
            var ui=root.AddComponent<InformationNotificationUI>();ui.Initialize(manager);
            manager.AddLearnedInformation("a","Older");manager.AddLearnedInformation("b","Newer");ui.ClickIcon();Assert.AreEqual("Newer",ui.BannerText);
            ui.ClickBanner();manager.AddLearnedInformation("c","Newest");Assert.IsFalse(manager.HasUnread);
            Assert.AreEqual(3,manager.Entries.Count);yield return null;
        }
        [UnityTest]public IEnumerator VisibleButtonsParticipateInRealUiRaycasts()
        {
            var ui=root.AddComponent<InformationNotificationUI>();ui.Initialize(manager);manager.AddLearnedInformation("one","Clue");
            yield return new WaitForSecondsRealtime(.4f);Canvas.ForceUpdateCanvases();
            var r=(RectTransform)ui.IconButton.transform;var center=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center));
            var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=center},hits);
            Assert.IsTrue(hits.Any(h=>h.gameObject==ui.IconButton.gameObject),"The visible gem hit area must receive real pointer events.");
        }
        [UnityTest]public IEnumerator OpenPanelCannotReadNewlyRetainedCluesDuringReset()
        {
            var ui=root.AddComponent<InformationNotificationUI>();ui.Initialize(manager);
            var adapter=root.AddComponent<EncounterInformationBridge>();adapter.Manager=manager;adapter.UI=ui;
            var facts=new JArray(new JObject{["factId"]="old",["text"]="Old clue",["learnedInLoop"]="loop-one"});
            adapter.ObserveSnapshot(2,"loop-two",facts);ui.ClickIcon();ui.ClickBanner();Assert.IsTrue(ui.Panel.IsOpen);
            facts.Add(new JObject{["factId"]="new",["text"]="New clue",["learnedInLoop"]="loop-two"});
            adapter.ObserveSnapshot(2,"loop-two",facts);Assert.IsFalse(manager.HasUnread);
            adapter.ObserveSnapshot(3,"loop-three",facts);Assert.IsFalse(ui.Panel.IsOpen);Assert.IsTrue(manager.HasUnread);
            Assert.AreEqual("new",manager.MostRecentUnread.Id);yield return null;
        }
        [UnityTest]public IEnumerator EmptyStateAndDisabledUiDoNotBlockGameplay()
        {
            var ui=root.AddComponent<InformationNotificationUI>();ui.Initialize(manager);yield return null;
            Assert.IsFalse(ui.IconButton.interactable);Assert.IsFalse(ui.BannerButton.interactable);ui.ClickIcon();Assert.IsFalse(ui.BannerOpen);
            manager.AddLearnedInformation("one","Some clue");ui.ClickIcon();ui.enabled=false;Assert.IsFalse(InformationNotificationUI.SuppressFloorTap);
            Assert.IsFalse(root.transform.Find("Learned information canvas").gameObject.activeSelf);
            ui.enabled=true;Assert.IsTrue(root.transform.Find("Learned information canvas").gameObject.activeSelf);
        }
    }
}
