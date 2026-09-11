using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Auga
{
    [HarmonyPatch]
    public static class Chat_Setup
    {
        [HarmonyPatch(typeof(Chat), nameof(Chat.Awake))]
        public static class Chat_Awake_Patch
        {
            public static bool Prefix(Chat __instance)
            {
                if (!Auga.AugaChatShow.Value || Auga.HasChatter)
                    return true;

                if (!FixChatInput(Auga.Assets.AugaChat))
                    return true;

                return !SetupHelper.IndirectTwoObjectReplace(__instance.transform, Auga.Assets.AugaChat, "Chat", "Chat_box", "AugaChat");
            }

            // The bundle's ChatInput carries Fishlabs.GuiInputField (ui_lib.dll, gone in 1.0.7), so
            // Chat.m_input loads null and Chat.Awake/HasFocus NRE every frame (player and camera stall).
            // Add the 1.0.7 GUIFramework.GuiInputField to the prefab before it is instantiated.
            // Returns false (keep vanilla chat) if the prefab does not have the expected layout.
            private static bool FixChatInput(GameObject prefab)
            {
                var chat = prefab ? prefab.GetComponentInChildren<Chat>(true) : null;
                if (chat == null) return false;
                if (chat.m_input != null) return true;

                var input = prefab.transform.Find("Chat_box/ChatInput");
                var text = input ? input.Find("Text - Input") : null;
                var textTmp = text ? text.GetComponent<TMP_Text>() : null;
                if (textTmp == null)
                {
                    Debug.LogWarning("[Auga] Chat: bundle ChatInput layout unexpected; using vanilla chat");
                    return false;
                }

                var field = input.GetComponent<GUIFramework.GuiInputField>();
                if (field == null)
                    field = input.gameObject.AddComponent<GUIFramework.GuiInputField>();
                field.textViewport = (RectTransform)input;
                field.textComponent = textTmp;
                var placeholder = text.Find("Placeholder");
                if (placeholder != null)
                    field.placeholder = placeholder.GetComponent<Graphic>();
                if (field.OnInputSubmit == null)
                    field.OnInputSubmit = new GUIFramework.OnInputSubmitEvent();
                chat.m_input = field;
                return true;
            }

            public static void Postfix(Chat __instance)
            {
                if (!Auga.AugaChatShow.Value || Auga.HasChatter)
                    return;

                if (__instance.m_input != null)
                    __instance.m_input.transform.parent.gameObject.AddComponent<MovableHudElement>().Init(TextAnchor.LowerRight, 0, 67);
            }
        }

        [HarmonyPatch(typeof(Chat), nameof(Chat.SetNpcText))]
        public static class Chat_SetNpcText_Patch
        {
            public static void Postfix(Chat __instance)
            {
                if (!Auga.AugaChatShow.Value || Auga.HasChatter)
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