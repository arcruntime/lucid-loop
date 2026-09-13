using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace LucidLoop.Gyms.Tests
{
    public sealed class EncounterHudLayoutTests
    {
        GameObject host;
        bool hadInputEvents;
        EncounterHudLayout layout;
        [SetUp] public void SetUp()
        {
            hadInputEvents = GameObject.Find("Input events");
            host = new GameObject("HUD test");
            var hud = host.AddComponent<EncounterHud>();
            hud.Coordinator = host.AddComponent<EncounterCoordinator>();
            typeof(EncounterHud).GetMethod("Build", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hud, null);
            layout = Object.FindFirstObjectByType<EncounterHudLayout>();
        }
        [TearDown] public void TearDown()
        {
            if (layout) Object.DestroyImmediate(layout.SafeRoot.parent.gameObject);
            Object.DestroyImmediate(host);
            if (!hadInputEvents) Object.DestroyImmediate(GameObject.Find("Input events"));
        }
        static Rect Bounds(RectTransform rect, RectTransform root)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var min = root.InverseTransformPoint(corners[0]);
            var max = root.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        [TestCase(2556,1179,177,63)]
        [TestCase(2796,1290,177,63)]
        [TestCase(2556,1179,0,0)]
        public void PhoneControlsMeet44PointTargetsAndKeyboardAvoidance(float width, float height, float side, float bottom)
        {
            float scale = Mathf.Sqrt(width / 1920f * height / 1080f);
            var size = new Vector2((width - 2 * side) / scale, (height - bottom) / scale);
            layout.SafeRoot.anchorMin = layout.SafeRoot.anchorMax = Vector2.zero;
            layout.SafeRoot.sizeDelta = size;
            float target = EncounterHudLayout.TouchTargetUnits(scale);
            layout.ConversationExpanded = true;
            foreach (float keyboardPixels in new[] { 0f, height * .55f })
            {
                float inset = EncounterHudLayout.KeyboardInset(new Rect(0,0,width,keyboardPixels), new Rect(side,bottom,width-2*side,height-bottom), scale, keyboardPixels > 0);
                layout.ApplyPhoneLayout(size,target,inset);
                foreach (string name in new[] { "Reply area", "Send", "Microphone", "Leave" })
                {
                    var rect = (RectTransform)layout.Conversation.Find(name);
                    Assert.That(rect.rect.height * scale / 3f, Is.GreaterThanOrEqualTo(43.99f), name);
                    Assert.That(rect.rect.width * scale / 3f, Is.GreaterThanOrEqualTo(43.99f), name);
                    var bounds = Bounds(rect,layout.SafeRoot);
                    Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(layout.SafeRoot.rect.yMin + inset), name + " keyboard overlap");
                    Assert.That(bounds.yMax, Is.LessThanOrEqualTo(layout.SafeRoot.rect.yMax), name + " top clipping");
                    Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(layout.SafeRoot.rect.xMin));
                    Assert.That(bounds.xMax, Is.LessThanOrEqualTo(layout.SafeRoot.rect.xMax));
                }
                var reply = Bounds((RectTransform)layout.Conversation.Find("Reply area"), layout.SafeRoot);
                var send = Bounds((RectTransform)layout.Conversation.Find("Send"), layout.SafeRoot);
                Assert.That(reply.Overlaps(send), Is.False);
            }
        }
        [Test] public void ExplorationDockKeepsCentreClearAndExpansionRestoresReplyControls()
        {
            var size = new Vector2(1800, 900);
            layout.SafeRoot.anchorMin = layout.SafeRoot.anchorMax = Vector2.zero;
            layout.SafeRoot.sizeDelta = size;
            layout.Connection.gameObject.SetActive(false);
            layout.ApplyPhoneLayout(size, 110, 0);
            Assert.That(layout.Conversation.rect.height, Is.LessThan(size.y * .25f));
            var centre = layout.SafeRoot.rect.center;
            foreach (var panel in new[] { layout.Header, layout.Conversation, layout.Guidance, layout.Clues })
                Assert.That(Bounds(panel,layout.SafeRoot).Contains(centre), Is.False, panel.name);
            foreach (string name in new[] { "Select maya", "Select ren", "Select luca", "Select theo", "Talk", "History" })
            {
                var rect = (RectTransform)layout.Conversation.Find(name);
                Assert.That(rect.gameObject.activeSelf, Is.True);
                Assert.That(rect.rect.width, Is.GreaterThanOrEqualTo(110));
                Assert.That(rect.rect.height, Is.GreaterThanOrEqualTo(110));
            }
            Assert.That(layout.Conversation.Find("Reply area").gameObject.activeSelf, Is.False);
            Click("Talk"); layout.ApplyPhoneLayout(size,110,0);
            Assert.That(layout.ConversationExpanded, Is.True);
            Assert.That(layout.Conversation.Find("Reply area").gameObject.activeSelf, Is.True);
            Assert.That(layout.Conversation.Find("Leave").gameObject.activeSelf, Is.True);
            Click("Leave"); layout.ApplyPhoneLayout(size,110,0);
            Assert.That(layout.ConversationExpanded, Is.False);
            Assert.That(layout.Conversation.Find("Reply area").gameObject.activeSelf, Is.False);
        }
        void Click(string name)
        {
            var button = layout.Conversation.Find(name).GetComponent("Button");
            var action = button.GetType().GetProperty("onClick").GetValue(button);
            action.GetType().GetMethod("Invoke").Invoke(action,null);
        }
        [TestCase("disconnected",true,"Choose someone nearby")]
        [TestCase("ready",true,"Choose someone nearby")]
        [TestCase("disconnected",false,"disconnected")]
        [TestCase("microphone_denied",true,"microphone_denied")]
        public void IdleVoiceStatusDoesNotReportConnectedGameAsDisconnected(string value, bool ready, string expected)
            => Assert.That(EncounterHud.IdleConversationStatus(value,ready), Is.EqualTo(expected));

        [Test] public void CluesExpandWithScrollableContentAndCanBeClosed()
        {
            layout.ApplyPhoneLayout(new Vector2(1800,900),110,0);
            Assert.That(layout.ClueViewport.gameObject.activeSelf, Is.False);
            layout.ToggleClues(); layout.ApplyPhoneLayout(new Vector2(1800,900),110,0);
            Assert.That(layout.ClueViewport.gameObject.activeSelf, Is.True);
            Assert.That(layout.Clues.rect.height, Is.GreaterThan(500));
            Assert.That(layout.ClueViewport.GetComponent("ScrollRect"), Is.Not.Null);
            Assert.That(layout.ClueViewport.Find("Clue content").GetComponent("ContentSizeFitter"), Is.Not.Null);
            layout.ToggleClues(); layout.ApplyPhoneLayout(new Vector2(1800,900),110,0);
            Assert.That(layout.ClueViewport.gameObject.activeSelf, Is.False);
        }
    }
}
