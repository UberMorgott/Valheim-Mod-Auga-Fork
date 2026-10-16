using AugaUnity;
using HarmonyLib;
using UnityEngine;

namespace Auga
{
    // Vanilla tooltips are instances of each UITooltip's m_tooltipPrefab, created under the canvas on hover
    // (assembly_guiutils UITooltip.OnHoverStart; destroyed again by HideTooltip), so no screen restyle reaches them:
    // item tooltips kept Averia fonts and their plain dark box (Epic Loot moves the box image onto its scroll view's
    // Content, sprite "Background", in its own OnHoverStart postfix). After every other postfix, each new instance
    // gets the Auga tooltip frame (MainMenu Tooltip art) on its box and Auga fonts.
    [HarmonyPatch(typeof(UITooltip), nameof(UITooltip.OnHoverStart))]
    public static class UITooltip_OnHoverStart_Patch
    {
        private static readonly System.Reflection.FieldInfo Tooltip = AccessTools.Field(typeof(UITooltip), "m_tooltip");
        private static readonly System.Collections.Generic.HashSet<string> Boxes =
            new System.Collections.Generic.HashSet<string> { "Background", "woodpanel_400_tileable", "UISprite" };
        private static int _styled;

        [HarmonyPriority(Priority.Last)]
        public static void Postfix()
        {
            var tip = Tooltip?.GetValue(null) as GameObject;
            if (!tip || tip.GetInstanceID() == _styled)
                return;
            _styled = tip.GetInstanceID();
            AugaStyle.RestyleAll(tip.transform);
            // The box: the image that frames the texts (parent of Topic/Text, or EL's Content after its scroll view).
            var text = Utils.FindChild(tip.transform, "Text");
            for (var t = text ? text.parent : null; t && t != tip.transform.parent; t = t.parent)
                if (t.TryGetComponent<UnityEngine.UI.Image>(out var box) && box.sprite && Boxes.Contains(box.sprite.name))
                {
                    AugaStyle.Tooltip(box);
                    return;
                }
            // The plain "Tooltip" prefab of the crafting requirement elements (InventoryGui.SetupRequirement sets their
            // UITooltip text, InventoryGui.cs:1733) has no image at all: a fixed 307x40 root, a one-line 307x24 Text and an
            // empty child. Vanilla shows bare text there (one item name); Auga frames every tooltip, and mods put several
            // lines into it (MorgottTweaks recipe chains), which overflow the fixed rects. So the frame is its own image
            // drawn just before the Text, fitted to the rendered text every frame (TooltipFrame).
            if (text && text.TryGetComponent<TMPro.TMP_Text>(out var tmp) && !tip.GetComponentInChildren<UnityEngine.UI.Image>(true))
                TooltipFrame.Add(tmp);
        }
    }

    // Auga tooltip frame behind a box-less tooltip text, sized to the text's rendered bounds plus padding.
    public sealed class TooltipFrame : MonoBehaviour
    {
        private const float Pad = 8f;
        private TMPro.TMP_Text _text;
        private RectTransform _rect;
        private UnityEngine.UI.Image _image;
        private string _shown;

        public static void Add(TMPro.TMP_Text text)
        {
            var go = new GameObject("AugaTooltipFrame", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(text.rectTransform.parent, false);
            // Siblings draw in order: just before the text, so the frame is behind it.
            rect.SetSiblingIndex(text.rectTransform.GetSiblingIndex());
            rect.anchorMin = rect.anchorMax = text.rectTransform.anchorMin;
            rect.pivot = new Vector2(0.5f, 0.5f);
            // The prefab root lays out its children (a layout group sized the frame 10 px wide and grew the root):
            // the frame sizes itself.
            go.AddComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
            var image = go.AddComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;
            AugaStyle.Tooltip(image);
            var frame = go.AddComponent<TooltipFrame>();
            frame._text = text;
            frame._rect = rect;
            frame._image = image;
            frame.Fit();
        }

        // After every LateUpdate (UITooltip.LateUpdate moves the tooltip to the pointer and clamps its first child,
        // UITooltip.cs:92-93 / 116 - now this frame, not the text), right before the canvases render.
        private void OnEnable()
        {
            _shown = null;
            Canvas.willRenderCanvases += Fit;
        }

        private void OnDisable() => Canvas.willRenderCanvases -= Fit;

        private void Fit()
        {
            if (!_text) return;
            // UITooltip.Set / UpdateTextElements change the text while shown: rebuild the mesh so the bounds are current.
            if (_shown != _text.text)
            {
                _shown = _text.text;
                _text.ForceMeshUpdate();
            }
            var empty = string.IsNullOrEmpty(_shown);
            if (_image.enabled == empty) _image.enabled = !empty;
            if (empty) return;
            var bounds = _text.textBounds; // local to the text's pivot
            var t = _text.rectTransform;
            // Same parent as the text: the frame's centre (pivot 0.5) goes where the rendered text's centre is, its size is
            // the text's in the parent's units.
            _rect.position = t.TransformPoint(bounds.center);
            _rect.sizeDelta = Vector2.Scale((Vector2)bounds.size, t.localScale) + new Vector2(Pad * 2f, Pad * 2f);
            ClampToScreen();
        }

        // Vanilla's Utils.ClampUIToScreen (assembly_utils Utils.cs) measured on the frame, but moving the parent the frame
        // and the text share, so both stay on screen together.
        private void ClampToScreen()
        {
            var parent = _rect.parent;
            if (!parent) return;
            var corners = new Vector3[4];
            _rect.GetWorldCorners(corners);
            var dx = 0f;
            var dy = 0f;
            if (corners[2].x > Screen.width) dx -= corners[2].x - Screen.width;
            if (corners[0].x < 0f) dx -= corners[0].x;
            if (corners[2].y > Screen.height) dy -= corners[2].y - Screen.height;
            if (corners[0].y < 0f) dy -= corners[0].y;
            if (dx == 0f && dy == 0f) return;
            parent.position += new Vector3(dx, dy, 0f);
        }
    }

    [HarmonyPatch(typeof(UITooltip), nameof(UITooltip.UpdateTextElements))]
    public static class UITooltip_UpdateTextElements_Patch
    {
        public static bool Prefix(UITooltip __instance)
        {
            if (UITooltip.m_tooltip != null)
            {
                var customTooltip = UITooltip.m_tooltip.GetComponent<ComplexTooltip>();
                if (customTooltip != null)
                {
                    var itemTooltip = __instance.GetComponent<ItemTooltip>();
                    if (itemTooltip != null && itemTooltip.Item != null)
                    {
                        customTooltip.SetItem(itemTooltip.Item);
                        return false;
                    }

                    var foodTooltip = __instance.GetComponent<FoodTooltip>();
                    if (foodTooltip != null && foodTooltip.Food != null)
                    {
                        customTooltip.SetFood(foodTooltip.Food);
                        return false;
                    }

                    var statusTooltip = __instance.GetComponent<StatusTooltip>();
                    if (statusTooltip != null && statusTooltip.StatusEffect != null)
                    {
                        customTooltip.SetStatusEffect(statusTooltip.StatusEffect);
                        return false;
                    }

                    var skillTooltip = __instance.GetComponent<SkillTooltip>();
                    if (skillTooltip != null && skillTooltip.Skill != null)
                    {
                        customTooltip.SetSkill(skillTooltip.Skill, __instance);
                        return false;
                    }

                    customTooltip.SetDefault(__instance);
                }
            }

            return true;
        }
    }
}
