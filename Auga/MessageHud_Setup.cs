using AugaUnity;
using HarmonyLib;
using UnityEngine;

namespace Auga
{
    [HarmonyPatch]
    public static class MessageHud_Setup
    {
        [HarmonyPatch(typeof(MessageHud), nameof(MessageHud.Awake))]
        [HarmonyPrefix]
        public static bool MessageHud_Awake_Prefix(MessageHud __instance)
        {
            return !SetupHelper.IndirectTwoObjectReplace(__instance.transform, Auga.Assets.MessageHud, "HudMessage", "TopLeftMessage", "AugaMessageHud");
        }

        [HarmonyPatch(typeof(MessageHud), nameof(MessageHud.Awake))]
        [HarmonyPostfix]
        public static void MessageHud_Awake_Postfix(MessageHud __instance)
        {
            if (__instance == null)
                return;

            var controller = __instance.GetComponent<AugaTopLeftMessageController>();
            if (controller)
            {
                var topLeft = controller.LogContainer.parent;
                // The bundle's TopLeftMessage root also carries the per-entry AugaTopLeftMessage script (the log
                // entries are LogPrefab instances). Its Update drags the root to an entry slot and, once the root is
                // its parent's first child, fades and destroys it with the whole log (AugaTopLeftMessage.cs:92-100).
                // In its own root canvas (SetupHelper.RootCanvas) it is that first child, so the stray script goes.
                var stray = topLeft.GetComponent<AugaTopLeftMessage>();
                if (stray)
                    Object.DestroyImmediate(stray);
                // Entry text: the row's HorizontalLayoutGroup sizes it to its preferred width, and with wrapping on and
                // Truncate overflow TMP drops the last glyph at that exact width ("Added Wood x" without the 5).
                var entryText = controller.LogPrefab ? controller.LogPrefab.MessageText : null;
                if (entryText)
                {
                    entryText.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                    entryText.overflowMode = TMPro.TextOverflowModes.Overflow;
                }
                topLeft.gameObject.AddComponent<MovableHudElement>().Init(TextAnchor.UpperLeft, 55, -115);
            }
            __instance.m_messageCenterText.gameObject.AddComponent<MovableHudElement>().Init(TextAnchor.MiddleCenter, 0, 150);
        }

        [HarmonyPatch(typeof(MessageHud), nameof(MessageHud.ShowMessage))]
        [HarmonyPostfix]
        public static void MessageHud_ShowMessage_Postfix(MessageHud __instance, MessageHud.MessageType type, string text, int amount, Sprite icon)
        {
            if (Hud.IsUserHidden())
            {
                return;
            }

            text = Localization.instance.Localize(text);
            if (type == MessageHud.MessageType.TopLeft)
            {
                var controller = __instance.GetComponent<AugaTopLeftMessageController>();
                controller.AddMessage(text, icon, amount);
            }
        }
    }
}
