using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using AugaUnity;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Auga
{
    [HarmonyPatch]
    public static class PauseMenu_Setup
    {
        // Config Logging/TraceInput: every term of 1.0.7 Menu.Update's "open on Esc" condition.
        public static void TraceEsc()
        {
            var m = Menu.instance;
            if (m == null) { Debug.LogWarning("[Auga] Esc: Menu.instance is null"); return; }
            Debug.LogWarning($"[Auga] Esc: menu={m.name} activeInHierarchy={m.gameObject.activeInHierarchy} enabled={m.enabled} " +
                $"root={(m.m_root ? m.m_root.gameObject.activeInHierarchy.ToString() : "null")} hiddenFrames={m.m_hiddenFrames} demo={DemoMode.Disabled} | " +
                $"inv={InventoryGui.IsVisible()} map={Minimap.IsOpen()} console={Console.IsVisible()} textInput={TextInput.IsVisible()} " +
                $"pwd={ZNet.instance?.InPasswordDialog()} connecting={ZNet.instance?.InConnectingScreen()} store={StoreGui.IsVisible()} " +
                $"pieceSel={Hud.IsPieceSelectionVisible()} popup={UnifiedPopup.IsVisible()} barber={PlayerCustomizaton.IsBarberGuiVisible()} " +
                $"radial={Hud.InRadial()} chatWasFocused={Chat.instance?.m_wasFocused}");
        }

        [HarmonyPatch(typeof(TextsDialog), nameof(TextsDialog.Update))]
        public static class TextDialog_Update_Patch
        {
            public static void TextDialogUpdate(TextsDialog instance)
            {
                instance.UpdateGamepadInput();
                if (instance.m_texts.Count <= 0)
                    return;

                if (instance.m_leftScrollbar == null)
                    return;
                
                if (instance.m_leftScrollRect == null)
                    return;
                
                instance.m_leftScrollbar.size = ((RectTransform)instance.m_leftScrollRect.transform).rect.height / instance.m_listRoot.rect.height;    
            }
            
            [UsedImplicitly]
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var instrs = instructions.ToList();

                var counter = 0;

                CodeInstruction LogMessage(CodeInstruction instruction)
                {
                    //Debug.LogWarning($"IL_{counter}: Opcode: {instruction.opcode} Operand: {instruction.operand}");
                    return instruction;
                }

                for (int i = 0; i < instrs.Count; ++i)
                {
                    if (i == 0)
                    {
                        yield return LogMessage(new CodeInstruction(OpCodes.Ldarg_0));
                        counter++;

                        yield return LogMessage(new CodeInstruction(OpCodes.Call,AccessTools.DeclaredMethod(typeof(TextDialog_Update_Patch), nameof(TextDialogUpdate))));
                        counter++;

                        yield return LogMessage(new CodeInstruction(OpCodes.Ret));
                        counter++;

                    }
                }
            }
        }

        [HarmonyPatch(typeof(TextsDialog), nameof(TextsDialog.ShowText), new []{typeof(TextsDialog.TextInfo)})]
        public static class TextDialog_ShowText_Patch
        {
            public static void ShowText(TextsDialog instance, TextsDialog.TextInfo text)
            {
                if (text == null)
                    return;
                
                instance.m_textAreaTopic.text = Localization.instance.Localize(text.m_topic);
                instance.m_textArea.text = Localization.instance.Localize(text.m_text);
                foreach (TextsDialog.TextInfo text1 in instance.m_texts)
                    text1.m_selected.SetActive(false);
                text.m_selected.SetActive(true);
                if (instance.m_leftScrollRect != null)
                {
                    instance.StartCoroutine(instance.FocusOnCurrentLevel(instance.m_leftScrollRect, instance.m_listRoot, text.m_selected.transform as RectTransform));                    
                }
            }
            
            [UsedImplicitly]
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var instrs = instructions.ToList();

                var counter = 0;

                CodeInstruction LogMessage(CodeInstruction instruction)
                {
                    //Debug.LogWarning($"IL_{counter}: Opcode: {instruction.opcode} Operand: {instruction.operand}");
                    return instruction;
                }

                for (int i = 0; i < instrs.Count; ++i)
                {
                    if (i == 0)
                    {
                        yield return LogMessage(new CodeInstruction(OpCodes.Ldarg_0));
                        counter++;

                        yield return LogMessage(new CodeInstruction(OpCodes.Ldarg_1));
                        counter++;

                        yield return LogMessage(new CodeInstruction(OpCodes.Call,AccessTools.DeclaredMethod(typeof(TextDialog_ShowText_Patch), nameof(ShowText))));
                        counter++;

                        yield return LogMessage(new CodeInstruction(OpCodes.Ret));
                        counter++;

                    }
                }
            }
        }

        [HarmonyPatch(typeof(Menu), nameof(Menu.UpdateNavigation))]
        public static class Menu_UpdateNavigation_Patch
        {
            public static void UpdateNavigation(Menu instance)
            {
                try
                {
                    Button component1;
                    Button component2;
                    Button component3;
                    Button component4;
                    Button component5;
                    
                    List<Button> buttonList = new List<Button>();

                    if (instance.name.StartsWith("Auga"))
                    {
                        component1 = instance.m_menuDialog.Find("MenuEntries/Logout").GetComponent<Button>();
                        component2 = instance.m_menuDialog.Find("MenuEntries/Exit").GetComponent<Button>();
                        component3 = instance.m_menuDialog.Find("MenuEntries/DividerMedium/CloseButton").GetComponent<Button>();
                        component4 = instance.m_menuDialog.Find("MenuEntries/Settings").GetComponent<Button>();
                        component5 = instance.m_menuDialog.Find("MenuEntries/Compendium").GetComponent<Button>();

                        instance.m_firstMenuButton = component3;
                        
                        //Settings
                        buttonList.Add(component4);
                        
                        //Compendium
                        buttonList.Add(component5);

                        if (instance.m_skipButton != null && instance.m_skipButton.gameObject.activeSelf)
                            buttonList.Add(instance.m_skipButton);

                        //Save
                        if (instance.m_saveButton != null && instance.m_saveButton.interactable)
                            buttonList.Add(instance.m_saveButton);

                        if (instance.m_playerListButton != null && instance.m_playerListButton.gameObject.activeSelf)
                            buttonList.Add(instance.m_playerListButton);

                        if (instance.m_inviteButton != null && instance.m_inviteButton.gameObject.activeSelf)
                            buttonList.Add(instance.m_inviteButton);

                        //Logout
                        buttonList.Add(component1);

                        //Exit
                        if (component2.gameObject.activeSelf)
                            buttonList.Add(component2);

                        //Close Menu
                        buttonList.Add(component3);
                    }
                    else
                    {
                        component1 = instance.m_menuDialog.Find("MenuEntries/Logout").GetComponent<Button>();
                        component2 = instance.m_menuDialog.Find("MenuEntries/Exit").GetComponent<Button>();
                        component3 = instance.m_menuDialog.Find("MenuEntries/Continue").GetComponent<Button>();
                        component4 = instance.m_menuDialog.Find("MenuEntries/Settings").GetComponent<Button>();

                        instance.m_firstMenuButton = component3;
                        
                        buttonList.Add(component3);
                        
                        if (instance.m_saveButton.interactable)
                            buttonList.Add(instance.m_saveButton);

                        if (instance.m_playerListButton.gameObject.activeSelf)
                            buttonList.Add(instance.m_playerListButton);
                        
                        buttonList.Add(component4);

                        buttonList.Add(component1);
                        
                        if (component2.gameObject.activeSelf)
                            buttonList.Add(component2);
                    }
                    
                    for (int index = 0; index < buttonList.Count; ++index)
                    {
                        Navigation navigation = buttonList[index].navigation with
                        {
                            selectOnUp = index <= 0 ? buttonList[buttonList.Count - 1] : (Selectable) buttonList[index - 1],
                            selectOnDown = index >= buttonList.Count - 1 ? buttonList[0] : (Selectable) buttonList[index + 1]
                        };
                        buttonList[index].navigation = navigation;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Start Menu Navigation ({instance.name}) Error Caught {e.Message}");
                    Debug.LogWarning($"{e.StackTrace}");
                }
            }
            
            [UsedImplicitly]
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var instrs = instructions.ToList();

                var counter = 0;

                CodeInstruction LogMessage(CodeInstruction instruction)
                {
                    //Debug.LogWarning($"IL_{counter}: Opcode: {instruction.opcode} Operand: {instruction.operand}");
                    return instruction;
                }

                for (int i = 0; i < instrs.Count; ++i)
                {
                    if (i == 0)
                    {
                        yield return LogMessage(new CodeInstruction(OpCodes.Ldarg_0));
                        counter++;

                        yield return LogMessage(new CodeInstruction(OpCodes.Call,AccessTools.DeclaredMethod(typeof(Menu_UpdateNavigation_Patch), nameof(UpdateNavigation))));
                        counter++;

                        yield return LogMessage(new CodeInstruction(OpCodes.Ret));
                        counter++;

                    }
                }
            }
        }

        [HarmonyPatch(typeof(Menu), nameof(Menu.Start))]
        public static class Menu_Start_Patch
        {
            [UsedImplicitly]
            public static void Postfix(Menu __instance)
            {
                if (__instance.name != "Menu")
                {
                    return;
                }

                var parent = __instance.transform.parent;
                var playerPrefab = __instance.CurrentPlayersPrefab;
                var newMenu = Object.Instantiate(Auga.Assets.MenuPrefab, parent, false).GetComponent<Menu>();
                newMenu.CurrentPlayersPrefab = playerPrefab;
                // Vanilla 1.0.7 Settings window: Menu.OnSettings instantiates m_settingsPrefab
                newMenu.m_settingsPrefab = __instance.m_settingsPrefab;
                WireMenu(newMenu, __instance);
                Object.Destroy(__instance.gameObject);
                SetupHelper.LogDeadRefsNextFrame(newMenu);
            }

            // The AugaMenu prefab was serialized against the pre-1.0.7 Menu (saveButton, menuCurrentPlayersListButton),
            // so every 1.0.7 button field is null and Menu.Show()/SetButtonsEnabled() would NRE. Wire them by path
            // (paths verified in the augaassets bundle) and pass through vanilla parts Auga has no equivalent for.
            private static void WireMenu(Menu menu, Menu vanilla)
            {
                var entries = menu.m_menuDialog.Find("MenuEntries");
                Button Find(string path)
                {
                    var button = entries.Find(path)?.GetComponent<Button>();
                    if (button == null)
                        Debug.LogError($"[Auga] AugaMenu: MenuEntries/{path} missing");
                    return button;
                }

                menu.m_continueButton = Find("DividerMedium/CloseButton");
                menu.m_saveButton = Find("Save");
                menu.m_playerListButton = Find("CurrentPlayerList");
                menu.m_settingsButton = Find("Settings");
                menu.m_logoutButton = Find("Logout");
                menu.m_quitButton = Find("Exit");
                menu.m_skipButton = Find("SkipIntro");
                // Prefab wires SkipIntro to OnManualSave; vanilla skip is OnSkip.
                Rewire(menu.m_skipButton, menu.OnSkip);

                // Invite (host + platform invite support): no Auga button, keep the vanilla one.
                menu.m_inviteButton = vanilla.m_inviteButton;
                if (menu.m_inviteButton != null)
                {
                    menu.m_inviteButton.transform.SetParent(entries, false);
                    if (menu.m_playerListButton != null)
                        menu.m_inviteButton.transform.SetSiblingIndex(menu.m_playerListButton.transform.GetSiblingIndex() + 1);
                    Rewire(menu.m_inviteButton, menu.InviteFriends);
                }

                // Gamepad map (Show() -> HandleInputLayoutChanged derefs both).
                menu.m_gamepadRoot = Adopt(vanilla.m_gamepadRoot, menu.m_root);
                menu.m_gamepadMapController = vanilla.m_gamepadMapController;
                if (menu.m_gamepadMapController != null && menu.m_gamepadRoot != null
                    && !menu.m_gamepadMapController.transform.IsChildOf(menu.m_gamepadRoot.transform))
                    Adopt(menu.m_gamepadMapController.gameObject, menu.m_root);

                // Cloud storage warnings: vanilla dialogs, buttons rewired to the new Menu.
                // ponytail: every button in the dialog maps to its OK handler; split if a dialog gains a second action.
                menu.m_cloudStorageWarning = Adopt(vanilla.m_cloudStorageWarning, menu.m_root);
                foreach (var b in menu.m_cloudStorageWarning ? menu.m_cloudStorageWarning.GetComponentsInChildren<Button>(true) : new Button[0])
                    Rewire(b, menu.OnCloudStorageFullWarningOk);
                menu.m_cloudStorageWarningNextSave = Adopt(vanilla.m_cloudStorageWarningNextSave, menu.m_root);
                foreach (var b in menu.m_cloudStorageWarningNextSave ? menu.m_cloudStorageWarningNextSave.GetComponentsInChildren<Button>(true) : new Button[0])
                    Rewire(b, menu.OnCloudStorageLowNextSaveWarningOk);

                menu.m_feedbackPrefab = vanilla.m_feedbackPrefab;
                // SceneReference is not serialized in the Auga prefab; Logout loads it.
                menu.m_startScene = vanilla.m_startScene;
                SetupHelper.LogDeadRefsNextFrame(menu.m_gamepadMapController);

                // Save button: Update() and UpdateNavigation deref it every frame.
                if (menu.m_saveButton == null && vanilla.m_saveButton != null)
                {
                    Debug.LogWarning("[Auga] AugaMenu: no Save button, adopting vanilla");
                    menu.m_saveButton = vanilla.m_saveButton;
                    menu.m_saveButton.transform.SetParent(entries, false);
                    Rewire(menu.m_saveButton, menu.OnManualSave);
                }
                if (menu.m_saveButton == null)
                    Debug.LogError("[Auga] AugaMenu: m_saveButton unset; Menu.Update will NRE");

                // 1.0.7 fields (SetButtonsEnabled/Update deref both); Auga prefab predates them.
                menu.menuEntriesParent = (RectTransform)entries;
                if (menu.lastSaveText == null && vanilla.lastSaveText != null)
                {
                    menu.lastSaveText = vanilla.lastSaveText;
                    menu.lastSaveText.transform.SetParent(entries, false);
                    if (menu.m_saveButton != null)
                        menu.lastSaveText.transform.SetSiblingIndex(menu.m_saveButton.transform.GetSiblingIndex() + 1);
                }
                if (menu.lastSaveText == null)
                    Debug.LogError("[Auga] AugaMenu: lastSaveText unset; Menu.Show will NRE");

                // Show() hides both dialogs; fall back to vanilla's if the prefab lost them.
                menu.m_quitDialog = menu.m_quitDialog ? menu.m_quitDialog : AdoptDialog(vanilla.m_quitDialog, menu, "m_quitDialog");
                menu.m_logoutDialog = menu.m_logoutDialog ? menu.m_logoutDialog : AdoptDialog(vanilla.m_logoutDialog, menu, "m_logoutDialog");
            }

            private static Transform AdoptDialog(Transform dialog, Menu menu, string field)
            {
                if (dialog == null)
                {
                    Debug.LogError($"[Auga] AugaMenu: {field} missing in Auga and vanilla; Menu.Show will NRE");
                    return null;
                }
                Debug.LogWarning($"[Auga] AugaMenu: {field} not serialized, adopting vanilla");
                Adopt(dialog.gameObject, menu.m_root);
                dialog.gameObject.SetActive(false);
                // Vanilla buttons target the destroyed Menu; re-point each by its persistent method name.
                foreach (var b in dialog.GetComponentsInChildren<Button>(true))
                {
                    if (b.onClick.GetPersistentEventCount() == 0) continue;
                    var method = AccessTools.Method(typeof(Menu), b.onClick.GetPersistentMethodName(0));
                    if (method != null)
                        Rewire(b, () => method.Invoke(menu, null));
                }
                return dialog;
            }

            private static GameObject Adopt(GameObject go, Transform parent)
            {
                if (go == null) return null;
                go.transform.SetParent(parent, false);
                go.transform.SetAsLastSibling();
                return go;
            }

            private static void Rewire(Button button, UnityEngine.Events.UnityAction action)
            {
                if (button == null) return;
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(action);
            }
        }

        [HarmonyPatch(typeof(Menu), nameof(Menu.OnClose))]
        public static class Menu_OnClose_Patch
        {
            [UsedImplicitly]
            public static void Postfix(Menu __instance)
            {
                var compendium = __instance.GetComponent<AugaCompendiumController>();
                if (compendium != null)
                {
                    compendium.HideCompendium();
                }
            }
        }

        [HarmonyPatch(typeof(TextsDialog))]
        public static class TextsDialog_Patch
        {
            [HarmonyPrefix]
            [HarmonyPatch(nameof(TextsDialog.AddActiveEffects))]
            public static bool AddActiveEffects_Prefix()
            {
                return false;
            }

            [HarmonyPrefix]
            [HarmonyPatch(nameof(TextsDialog.AddLog))]
            public static bool AddLog_Prefix()
            {
                return false;
            }

            [HarmonyPrefix]
            [HarmonyPatch(nameof(TextsDialog.UpdateTextsList))]
            public static bool UpdateTextsList_Prefix(TextsDialog __instance)
            {
                __instance.m_texts.Clear();

                var filter = __instance.GetComponent<AugaTextsDialogFilter>();
                foreach (var knownText in Player.m_localPlayer.GetKnownTexts())
                {
                    if (filter == null || knownText.Key.Contains(filter.Filter))
                    {
                        var keyText = Localization.instance.Localize(knownText.Key);
                        var separatorIndex = keyText.IndexOf(": ", StringComparison.Ordinal);
                        keyText = separatorIndex >= 0 ? keyText.Substring(separatorIndex + 2) : keyText;
                        __instance.m_texts.Add(new TextsDialog.TextInfo(keyText, Localization.instance.Localize(knownText.Value)));
                    }
                }

                __instance.m_texts.Sort((a, b) => string.Compare(a.m_topic, b.m_topic, StringComparison.CurrentCulture));
                return false;
            }

            [HarmonyPostfix]
            [HarmonyPatch(nameof(TextsDialog.ShowText), typeof(TextsDialog.TextInfo))]
            public static void AddLog_Postfix(TextsDialog __instance)
            {
                __instance.m_textArea.text = __instance.m_textArea.text.Replace("color=yellow", $"color={Auga.Colors.Topic}");
            }
        }
    }
}

