using System.Linq;
using HarmonyLib;
using JetBrains.Annotations;

namespace Auga
{
    // Chat (spec 2026-09-11-auga-native-rework, Phase 4, option A): the vanilla Chat window keeps its objects, Canvas,
    // GuiInputField and update path (Chat.cs:185-211 opens and focuses m_input). Auga only restyles it in place.
    // The old AugaChat bundle replacement is gone: it sat outside any Canvas (never drawn) and its runtime-added input
    // field had the text rect left of the viewport, so typed text ran past the field's left edge.
    [HarmonyPatch]
    public static class Chat_Setup
    {
        [HarmonyPatch(typeof(Chat), nameof(Chat.Awake))]
        public static class Chat_Awake_Patch
        {
            [UsedImplicitly]
            public static void Postfix(Chat __instance)
            {
                if (Auga.HasChatter) // Chatter builds its own chat UI
                    return;
                AugaStyle.Restyle(__instance.transform);
                Hud_Setup.Movable(__instance.m_chatWindow, "Chat");
            }
        }

        [HarmonyPatch(typeof(Chat), nameof(Chat.SetNpcText))]
        public static class Chat_SetNpcText_Patch
        {
            [UsedImplicitly]
            public static void Postfix(Chat __instance)
            {
                if (Auga.HasChatter)
                    return;

                var latestChatMessage = __instance.m_npcTexts.LastOrDefault();
                if (latestChatMessage != null)
                {
                    var text = latestChatMessage.m_textField.text;
                    text = text.Replace("<color=orange>", $"<color={Auga.Colors.Topic}>");
                    text = text.Replace("<color=yellow>", $"<color={Auga.Colors.Emphasis}>");
                    latestChatMessage.m_textField.text = text;
                }
            }
        }
    }
}
