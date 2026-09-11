using BepInEx.Configuration;
using UnityEngine;

namespace Auga
{
    public class MovableHudElement : MonoBehaviour
    {
        public ConfigEntry<TextAnchor> Anchor;
        public ConfigEntry<Vector2> Position;
        public ConfigEntry<float> Scale;
        public ConfigEntry<Vector2> Offset;

        private Vector2 _vanillaPosition;
        private Vector3 _vanillaScale;

        public void Init(TextAnchor defaultAnchor, float defaultPositionX, float defaultPositionY)
        {
            Init(gameObject.name, defaultAnchor, defaultPositionX, defaultPositionY);
        }

        public void Init(string nameOverride, TextAnchor defaultAnchor, float defaultPositionX, float defaultPositionY)
        {
            Anchor = Auga.instance.Config.Bind(nameOverride, $"{nameOverride}Anchor", defaultAnchor, $"Anchor for {nameOverride}");
            Position = Auga.instance.Config.Bind(nameOverride, $"{nameOverride}Position", new Vector2(defaultPositionX, defaultPositionY), $"Position for {nameOverride}");
            Scale = Auga.instance.Config.Bind(nameOverride, $"{nameOverride}Scale", 1.0f, $"Uniform scale for {nameOverride}");
        }

        // Vanilla HUD elements (spec D5): keep the game's anchors and layout; the config only shifts and scales the
        // element relative to where the game put it. Applied once and on config change, never per frame.
        public void InitOffset(string name)
        {
            var rt = (RectTransform)transform;
            _vanillaPosition = rt.anchoredPosition;
            _vanillaScale = rt.localScale;
            Offset = Auga.instance.Config.Bind("HudLayout", $"{name}Offset", Vector2.zero, $"Shift of {name} from its vanilla position, in UI pixels");
            Scale = Auga.instance.Config.Bind("HudLayout", $"{name}Scale", 1f, $"Uniform scale of {name}");
            Offset.SettingChanged += ApplyOffset;
            Scale.SettingChanged += ApplyOffset;
            ApplyOffset(null, null);
        }

        private void ApplyOffset(object sender, System.EventArgs e)
        {
            var rt = (RectTransform)transform;
            rt.anchoredPosition = _vanillaPosition + Offset.Value;
            rt.localScale = _vanillaScale * Scale.Value;
        }

        public void OnDestroy()
        {
            if (Offset == null)
                return;
            Offset.SettingChanged -= ApplyOffset;
            Scale.SettingChanged -= ApplyOffset;
        }

        public void Update()
        {
            if (Anchor == null || Position == null || Scale == null)
                return;

            var rectTransform = (RectTransform)transform;
            switch (Anchor.Value)
            {
                case TextAnchor.UpperLeft:
                    rectTransform.pivot = rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(0, 1);
                    break;
                case TextAnchor.UpperCenter:
                    rectTransform.pivot = rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(0.5f, 1);
                    break;
                case TextAnchor.UpperRight:
                    rectTransform.pivot = rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(1, 1);
                    break;
                case TextAnchor.MiddleLeft:
                    rectTransform.pivot = rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(0, 0.5f);
                    break;
                case TextAnchor.MiddleCenter:
                    rectTransform.pivot = rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    break;
                case TextAnchor.MiddleRight:
                    rectTransform.pivot = rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(1, 0.5f);
                    break;
                case TextAnchor.LowerLeft:
                    rectTransform.pivot = rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(0, 0);
                    break;
                case TextAnchor.LowerCenter:
                    rectTransform.pivot = rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(0.5f, 0);
                    break;
                case TextAnchor.LowerRight:
                    rectTransform.pivot = rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(1, 0);
                    break;
            }

            rectTransform.anchoredPosition = Position.Value;
            rectTransform.localScale = new Vector3(Scale.Value, Scale.Value);
        }
    }
}
