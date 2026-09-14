using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace LucidLoop.Gyms.PlayModeTests
{
    public sealed class EncounterMarkersPlayModeTests
    {
        EncounterCoordinator coordinator;

        [UnityTest, Explicit("Requires the actual scene and localhost:8789 authoritative relay; no provider.")]
        public IEnumerator MarkersFollowAuthoritySelectionAndCameraVisibility()
        {
            var loading = SceneManager.LoadSceneAsync("BeforeTheDrop", LoadSceneMode.Single);
            while (!loading.isDone) yield return null;
            yield return null;
            coordinator = UnityEngine.Object.FindFirstObjectByType<EncounterCoordinator>();
            var hud = UnityEngine.Object.FindFirstObjectByType<EncounterHud>();
            var layout = UnityEngine.Object.FindFirstObjectByType<EncounterHudLayout>();
            layout.PreviewPhoneLayout = true;
            var markers = hud.InteractionMarkers;
            Assert.That(markers, Is.Not.Null);
            Assert.That(coordinator.ConnectNew(Environment.GetEnvironmentVariable("LUCID_LOOP_SMOKE_GAME_URL") ?? "ws://127.0.0.1:8789/game"), Is.True);
            float deadline = Time.realtimeSinceStartup + 30;
            while (!coordinator.IsReady || !coordinator.ConversationEligibility("maya", out _, out _))
            { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), coordinator.Status); yield return null; }
            Assert.That(coordinator.SendDestination(new Vector3(-2, 0, -4)), Is.True);
            while (!coordinator.ConversationEligibility("maya", out var eligible, out _) || !eligible)
            { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Maya speaking range"); yield return null; }
            yield return null;
            Assert.That(markers.MarkerCount, Is.EqualTo(5), "Player plus four speaking NPCs; no invented object clues.");
            Assert.That(markers.PlayerBeaconVisible, Is.True);
            Assert.That(markers.VisibleNpcCount, Is.EqualTo(4));
            Assert.That(markers.TryGetMarkerState("maya", out var state), Is.True);
            Assert.That(state, Is.EqualTo(EncounterMarkerState.Focused));
            Assert.That(markers.TryGetMarkerState("ren", out state), Is.True);
            Assert.That(state, Is.EqualTo(EncounterMarkerState.OutOfRange));
            Assert.That(markers.TryGetMarkerRectTransform("player", out var beacon), Is.True);
            var firstPosition = beacon.anchoredPosition;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(Vector2.Distance(firstPosition, beacon.anchoredPosition), Is.GreaterThan(.01f), "Beacon should bob or track the moving player.");
            Assert.That(markers.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget), Is.True, "Decorative markers must not block floor/actor taps.");

            var selectRen = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == "Select ren");
            selectRen.onClick.Invoke();
            yield return null;
            markers.TryGetMarkerState("maya", out state);
            Assert.That(state, Is.EqualTo(EncounterMarkerState.Available), "Eligible non-selected person is outlined.");
            markers.TryGetMarkerState("ren", out state);
            Assert.That(state, Is.EqualTo(EncounterMarkerState.OutOfRange), "Selection cannot bypass range eligibility.");
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".local", "validation"));
            Directory.CreateDirectory(directory);
            string capture = Path.Combine(directory, "interaction-markers-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".png");
            ScreenCapture.CaptureScreenshot(capture);
            deadline = Time.realtimeSinceStartup + 10;
            while (!File.Exists(capture) || new FileInfo(capture).Length < 24)
            { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Marker screenshot"); yield return null; }

            layout.ConversationExpanded = true;
            yield return null;
            Assert.That(markers.VisibleNpcCount, Is.Zero);
            Assert.That(markers.PlayerBeaconVisible, Is.False);
            layout.ConversationExpanded = false;

            hud.Rig.enabled = false;
            hud.Rig.Camera.transform.rotation = Quaternion.LookRotation(-hud.Rig.Camera.transform.forward, Vector3.up);
            yield return null;
            Assert.That(markers.VisibleNpcCount, Is.Zero, "Behind-camera actors must not leave mirrored markers.");
            Assert.That(markers.PlayerBeaconVisible, Is.False);
            hud.Rig.enabled = true;
            hud.Rig.Immediate = true;
            yield return null;
            yield return null;
            Assert.That(markers.PlayerBeaconVisible, Is.True);
            coordinator.Disconnect();
            yield return null;
            Assert.That(markers.VisibleNpcCount, Is.Zero);
            Assert.That(markers.PlayerBeaconVisible, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [TearDown] public void TearDown() { if (coordinator) coordinator.Disconnect(); }
    }
}
