using System;
using System.Collections;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LucidLoop.Gyms.PlayModeTests
{
    public sealed class EncounterPausePlayModeTests
    {
        EncounterCoordinator coordinator;
        const string MusicPreference = "lucid-loop.music-volume";
        bool restorePreference, hadPreference;
        float savedPreference;

        [UnityTest, Explicit("Requires the authoritative relay on 8789; no provider.")]
        public IEnumerator RestartNightFromPauseKeepsNewAttemptPaused()
        {
            var loading = SceneManager.LoadSceneAsync("BeforeTheDrop", LoadSceneMode.Single);
            while (!loading.isDone) yield return null;
            yield return null;
            coordinator = UnityEngine.Object.FindFirstObjectByType<EncounterCoordinator>();
            var hud = UnityEngine.Object.FindFirstObjectByType<EncounterHud>();
            var layout = UnityEngine.Object.FindFirstObjectByType<EncounterHudLayout>();
            layout.PreviewPhoneLayout = true;
            var menu = hud.PauseMenu;
            Assert.That(menu.RestartNight(), Is.False);
            string relay = Environment.GetEnvironmentVariable("LUCID_LOOP_SMOKE_GAME_URL") ?? "ws://127.0.0.1:8789/game";
            Assert.That(coordinator.ConnectNew(relay), Is.True);
            float deadline = Time.realtimeSinceStartup + 30;
            while (!coordinator.IsReady) { Before(deadline); yield return null; }
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(menu.RequestOpen(), Is.True);
            while (!menu.IsVisible) { Before(deadline); yield return null; }
            string loop = coordinator.State.LoopId;
            var clues = coordinator.State.PlayerDiscoveries.ToString();
            Click(menu, "Restart night");
            Assert.That(menu.RestartNight(), Is.False, "Do not send duplicate restart while awaiting acknowledgement.");
            while (coordinator.State.LoopId == loop) { Before(deadline); yield return null; }
            yield return null;
            Assert.That(menu.IsVisible, Is.True);
            Assert.That(coordinator.IsPaused, Is.True);
            Assert.That(coordinator.State.LoopIndex, Is.EqualTo(2));
            Assert.That(coordinator.State.PlayerDiscoveries.ToString(), Is.EqualTo(clues));
            Assert.That(coordinator.State.ElapsedSeconds, Is.Zero);
            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(coordinator.State.ElapsedSeconds, Is.Zero, "New night must not run behind pause menu.");
            Canvas.ForceUpdateCanvases();
            foreach (var button in menu.GetComponentsInChildren<Button>())
                Assert.That(((RectTransform)button.transform).rect.height * layout.SafeRoot.GetComponentInParent<Canvas>().scaleFactor,
                    Is.GreaterThanOrEqualTo(131.9f), button.name + " phone touch target");
            yield return Capture("restart", deadline);
            Assert.That(menu.Resume(), Is.True);
            while (coordinator.IsPaused || coordinator.State.ElapsedSeconds <= 0) { Before(deadline); yield return null; }
            Assert.That(menu.IsVisible, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest, Explicit("Requires the authoritative relay on 8789; no provider.")]
        public IEnumerator PauseMenuBlocksGameplayAndPreservesResumableNight()
        {
            hadPreference = PlayerPrefs.HasKey(MusicPreference);
            savedPreference = PlayerPrefs.GetFloat(MusicPreference);
            restorePreference = true;
            PlayerPrefs.SetFloat(MusicPreference, .27f);
            var loading = SceneManager.LoadSceneAsync("BeforeTheDrop", LoadSceneMode.Single);
            while (!loading.isDone) yield return null;
            yield return null;
            coordinator = UnityEngine.Object.FindFirstObjectByType<EncounterCoordinator>();
            var hud = UnityEngine.Object.FindFirstObjectByType<EncounterHud>();
            var layout = UnityEngine.Object.FindFirstObjectByType<EncounterHudLayout>();
            layout.PreviewPhoneLayout = true;
            var menu = hud.PauseMenu;
            Assert.That(menu.MusicVolume, Is.EqualTo(.27f).Within(.001f));
            string relay = Environment.GetEnvironmentVariable("LUCID_LOOP_SMOKE_GAME_URL") ?? "ws://127.0.0.1:8789/game";
            Assert.That(coordinator.ConnectNew(relay), Is.True);
            float deadline = Time.realtimeSinceStartup + 25;
            while (!coordinator.IsReady) { Before(deadline); yield return null; }
            string loop = coordinator.State.LoopId;
            menu.RequestOpen();
            while (!menu.IsVisible) { Before(deadline); yield return null; }
            Assert.That(coordinator.IsPaused, Is.True);
            layout.ToggleClues();
            layout.ConversationExpanded = true;
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(menu.CanQuit, Is.False, "Phone preview uses the iOS menu entries.");
            Assert.That(layout.Pause.GetComponent<Button>().IsInteractable(), Is.False, "Modal also blocks underlying selectable navigation.");
            foreach (var button in menu.GetComponentsInChildren<Button>())
                Assert.That(((RectTransform)button.transform).rect.height * layout.SafeRoot.GetComponentInParent<Canvas>().scaleFactor,
                    Is.GreaterThanOrEqualTo(131.9f), button.name + " must meet the phone touch target.");
            // Even when the layout raises another panel, the pause blocker wins UI raycasts.
            var pointer = new PointerEventData(EventSystem.current) {
                position = RectTransformUtility.WorldToScreenPoint(null, layout.Pause.TransformPoint(layout.Pause.rect.center)) };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            Assert.That(hits[0].gameObject.GetComponentInParent<EncounterPauseMenu>(), Is.SameAs(menu));
            Assert.That(coordinator.SendDestination(new Vector3(-2, 0, -4)), Is.False);
            Click(menu, "Settings");
            Assert.That(menu.CurrentPage, Is.EqualTo(EncounterPausePage.Settings));
            menu.SetMusicVolume(.41f);
            Assert.That(PlayerPrefs.GetFloat(MusicPreference), Is.EqualTo(.41f).Within(.001f));
            Assert.That(UnityEngine.Object.FindFirstObjectByType<EncounterMoodPresentation>().MusicVolume, Is.EqualTo(.41f).Within(.001f));
            yield return Capture("settings", deadline);
            menu.HandleEscape();
            Assert.That(menu.CurrentPage, Is.EqualTo(EncounterPausePage.Main));
            Assert.That(coordinator.IsPaused, Is.True);
            Click(menu, "Controls");
            Assert.That(menu.CurrentPage, Is.EqualTo(EncounterPausePage.Controls));
            yield return Capture("controls", deadline);
            menu.Back();
            yield return Capture("main", deadline);
            Click(menu, "Main Menu");
            Assert.That(coordinator.IsReady, Is.False);
            Assert.That(coordinator.CanResume, Is.True);
            Assert.That(menu.IsVisible, Is.False);
            Assert.That(layout.Connection.gameObject.activeSelf, Is.True);
            Assert.That(coordinator.Resume(relay), Is.True);
            while (!coordinator.IsReady || !menu.IsVisible) { Before(deadline); yield return null; }
            Assert.That(coordinator.State.LoopId, Is.EqualTo(loop));
            Click(menu, "Resume");
            while (menu.IsVisible) { Before(deadline); yield return null; }
            Assert.That(coordinator.IsPaused, Is.False);
            Assert.That(layout.Pause.GetComponent<Button>().IsInteractable(), Is.True);
            Assert.That(UnityEngine.Object.FindFirstObjectByType<EncounterVoiceController>().IsReady, Is.False);
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest, Explicit("Requires the authoritative relay on 8789 and audio fixture on 8790; no provider.")]
        public IEnumerator PauseClosesVoiceAndResynchronizesAcrossReconnect()
        {
            var loading = SceneManager.LoadSceneAsync("BeforeTheDrop", LoadSceneMode.Single);
            while (!loading.isDone) yield return null;
            yield return null;
            coordinator = UnityEngine.Object.FindFirstObjectByType<EncounterCoordinator>();
            var voice = UnityEngine.Object.FindFirstObjectByType<EncounterVoiceController>();
            string relay = Environment.GetEnvironmentVariable("LUCID_LOOP_SMOKE_GAME_URL") ?? "ws://127.0.0.1:8789/game";
            int starts = 0, pauses = 0;
            voice.StreamStarted += _ => starts++;
            coordinator.PauseChanged += _ => pauses++;
            Assert.That(coordinator.ConnectNew(relay), Is.True);
            float deadline = Time.realtimeSinceStartup + 25;
            while (!coordinator.IsReady) { Before(deadline); yield return null; }
            var request = (EncounterConversationRequest)Activator.CreateInstance(typeof(EncounterConversationRequest),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] {
                    "ws://127.0.0.1:8790/live", "maya", coordinator.State.LoopId,
                    new JObject { ["type"] = "gym.start", ["character"] = "maya" } }, null);
            voice.Begin(request);
            while (!voice.IsReady) { Before(deadline); yield return null; }
            Assert.That(coordinator.Pause(true), Is.True);
            Assert.That(voice.Output.isPlaying, Is.False, "Pause stops playback before the server acknowledgement.");
            while (!coordinator.IsPaused || !voice.FinalUsageConfirmed || voice.IsClosing)
            { Before(deadline); yield return null; }
            Assert.That(voice.MicrophoneEnabled, Is.False);
            Assert.That(coordinator.RequestConversation("maya"), Is.False);
            Assert.That(coordinator.SendDestination(new Vector3(-2, 0, -4)), Is.False);
            string loop = coordinator.State.LoopId;
            coordinator.Disconnect();
            Assert.That(coordinator.IsPaused, Is.False, "Disconnected local state must not label a new night paused.");
            Assert.That(coordinator.Resume(relay), Is.True);
            while (!coordinator.IsReady || !coordinator.IsPaused) { Before(deadline); yield return null; }
            Assert.That(coordinator.State.LoopId, Is.EqualTo(loop));
            Assert.That(coordinator.Pause(false), Is.True);
            while (coordinator.IsPaused) { Before(deadline); yield return null; }
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(starts, Is.EqualTo(1), "Resume must not reopen paid voice.");
            Assert.That(voice.IsReady, Is.False);
            Assert.That(coordinator.Pause(true), Is.True);
            while (!coordinator.IsPaused) { Before(deadline); yield return null; }
            Assert.That(coordinator.ConnectNew(relay), Is.True);
            int priorPauses = pauses;
            while (!coordinator.IsReady || pauses == priorPauses) { Before(deadline); yield return null; }
            Assert.That(coordinator.State.LoopId, Is.Not.EqualTo(loop));
            Assert.That(coordinator.IsPaused, Is.False, "A fresh game reports its own unpaused state.");
            LogAssert.NoUnexpectedReceived();
        }
        static void Before(float deadline) => Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Pause lifecycle timeout");
        static IEnumerator Capture(string page, float deadline)
        {
            yield return new WaitForEndOfFrame();
            string capture = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.local/validation/pause-menu-" + page + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".png"));
            ScreenCapture.CaptureScreenshot(capture);
            while (!File.Exists(capture) || new FileInfo(capture).Length < 24)
            { Before(deadline); yield return null; }
        }
        static void Click(EncounterPauseMenu menu, string name)
        {
            var button = menu.GetComponentsInChildren<Button>().Single(b => b.name == name);
            Assert.That(button.IsInteractable(), Is.True);
            button.onClick.Invoke();
        }
        [TearDown] public void TearDown()
        {
            if (coordinator) coordinator.Disconnect();
            if (restorePreference)
            {
                if (hadPreference) PlayerPrefs.SetFloat(MusicPreference, savedPreference);
                else PlayerPrefs.DeleteKey(MusicPreference);
                PlayerPrefs.Save(); restorePreference = false;
            }
        }
    }
}
