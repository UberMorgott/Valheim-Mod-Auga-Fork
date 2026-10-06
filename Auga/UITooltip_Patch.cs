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
                    break;
                }
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
