using UnityEngine;
using UnityEngine.UI;

namespace LucidLoop.Gyms
{
    // Shows real connection work; never inserts an artificial branding delay.
    [DefaultExecutionOrder(1000)]
    public sealed class EncounterLoadingBrand : MonoBehaviour
    {
        EncounterCoordinator coordinator;
        RectTransform panel, logo;
        Text message;
        Button cancel;
        float aspect = 2.5f;

        public void Initialize(EncounterCoordinator owner, RectTransform safeRoot)
        {
            coordinator = owner;
            panel = GymUI.Rect(safeRoot, "Loading the night", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            GymUI.Panel(panel, new Color(.015f, .012f, .025f, .98f));
            logo = GymUI.Rect(panel, "Before the Drop logo", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            var image = logo.gameObject.AddComponent<RawImage>();
            image.texture = Resources.Load<Texture2D>("Branding/BeforeTheDrop");
            image.raycastTarget = false;
            if (image.texture) aspect = (float)image.texture.width / image.texture.height;
            var caption = GymUI.Rect(panel, "Loading status", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            message = GymUI.Text(caption, "Connecting to the night…", 32, Color.white);
            message.alignment = TextAnchor.MiddleCenter;
            var button = GymUI.Rect(panel, "Cancel loading", new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            cancel = GymUI.Button(button, "Cancel", () => coordinator.Disconnect());
            panel.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (!panel) return;
            bool visible = coordinator && coordinator.IsConnecting;
            panel.gameObject.SetActive(visible);
            if (!visible) return;
            panel.SetAsLastSibling();
            float width = Mathf.Min(1000, panel.rect.width * .72f, panel.rect.height * .5f * aspect);
            float height = width / aspect;
            logo.sizeDelta = new Vector2(width, height);
            logo.anchoredPosition = new Vector2(0, 65);
            message.rectTransform.sizeDelta = new Vector2(Mathf.Min(900, panel.rect.width - 48), 60);
            message.rectTransform.anchoredPosition = new Vector2(0, 25 - height * .5f);
            var button = (RectTransform)cancel.transform;
            button.sizeDelta = new Vector2(260, Mathf.Max(64, EncounterHudLayout.TouchTargetUnits(panel.GetComponentInParent<Canvas>().scaleFactor)));
            button.anchoredPosition = new Vector2(0, -65 - height * .5f);
        }

        void OnDisable() { if (panel) panel.gameObject.SetActive(false); }
    }
}
