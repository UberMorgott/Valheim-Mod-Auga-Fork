using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using AugaUnity;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

namespace Auga
{
    [HarmonyPatch]
    public static class PauseMenu_Setup
    {
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

        // Pause menu (Esc), restyled in place (spec 2026-09-11-auga-native-rework, Phase 1). The vanilla Menu keeps
        // its GameObject, its own Canvas (override sorting, order 1700; scene _GameMain/LoadingGUI/PixelFix/IngameGui/Menu)
        // and every field, so Start/Show/Hide/Update/UpdateNavigation (Menu.cs:120-403) run unchanged. Sprites and fonts
        // come from the AugaMenu bundle prefab; Auga's Compendium is the one added element.
        [HarmonyPatch(typeof(Menu), nameof(Menu.Start))]
        public static class Menu_Start_Patch
        {
            private const string Entry = "MenuRoot/Menu/MenuEntries/Settings";
            private const string Dialog = "MenuRoot/ExitConfirm/ExitConfirmDialog";

            [UsedImplicitly]
            public static void Postfix(Menu __instance)
            {
                var prefab = Auga.Assets.MenuPrefab;

                // Backdrop: vanilla darken takes Auga's. The wooden ornament and border are decoration only
                // (no Menu field references them, Menu.cs:28-102) and Auga's menu has no counterpart.
                var dialog = __instance.m_menuDialog;
                AugaStyle.CopyImage(ImageAt(dialog, "darken"), AugaStyle.FromPrefab<Image>(prefab, "MenuRoot/Menu/Darken"));
                foreach (var decor in new[] { "ornament", "border (1)" })
                {
                    var image = ImageAt(dialog, decor);
                    if (image) image.enabled = false;
                }

                // Entries (m_continueButton .. m_quitButton): Auga label font and knot glyphs on the vanilla buttons.
                var entryText = AugaStyle.FromPrefab<TMP_Text>(prefab, Entry + "/Text");
                var leftKnot = AugaStyle.FromPrefab<Image>(prefab, Entry + "/Knots/LeftKnot/Glyph");
                var rightKnot = AugaStyle.FromPrefab<Image>(prefab, Entry + "/Knots/RightKnot/Glyph");
                foreach (var button in __instance.menuEntriesParent.GetComponentsInChildren<Button>(true))
                {
                    AugaStyle.CopyText(button.GetComponentInChildren<TMP_Text>(true), entryText);
                    Knot(ImageAt(button.transform, "LeftKnot"), leftKnot);
                    Knot(ImageAt(button.transform, "RightKnot"), rightKnot);
                }
                AugaStyle.CopyText(__instance.lastSaveText, AugaStyle.FromPrefab<TMP_Text>(prefab, "MenuRoot/Menu/MenuEntries/LastTimeSaved"));

                RestyleDialog(__instance.m_quitDialog, prefab);
                RestyleDialog(__instance.m_logoutDialog, prefab);
                RestyleDialog(__instance.m_cloudStorageWarning.transform, prefab);
                RestyleDialog(__instance.m_cloudStorageWarningNextSave.transform, prefab);

                AddCompendium(__instance, prefab);
            }

            // Auga's Compendium (AugaCompendiumController is the only Auga-only part). A child of the Menu, so it
            // draws in the Menu Canvas like vanilla's Settings instance (Menu.cs:414). Closed until ShowCompendium
            // (AugaCompendiumController.cs:135-149). Its entry is a clone of the vanilla Settings entry.
            private static void AddCompendium(Menu menu, GameObject prefab)
            {
                var compendium = Object.Instantiate(prefab.transform.Find("Compendium").gameObject, menu.transform, false);
                compendium.SetActive(false);
                var controller = compendium.GetComponent<AugaCompendiumController>();

                var settings = menu.m_settingsButton;
                var entry = Object.Instantiate(settings.gameObject, settings.transform.parent, false);
                entry.name = "Compendium";
                entry.transform.SetSiblingIndex(settings.transform.GetSiblingIndex() + 1);
                var button = entry.GetComponent<Button>();
                button.onClick = new Button.ButtonClickedEvent(); // the clone carries Settings' persistent OnSettings call
                button.onClick.AddListener(controller.ShowCompendium);
                entry.GetComponentInChildren<TMP_Text>(true).text = AugaStyle.FromPrefab<TMP_Text>(prefab, "MenuRoot/Menu/MenuEntries/Compendium/Text").text;
                Localization.instance.Localize(entry.transform);
                menu.UpdateNavigation(); // Start built the chain before the entry existed (Menu.cs:124)
            }

            // Confirm and cloud-storage dialogs: backdrop (the dialog's own Image or its "bkg"), buttons, labels.
            private static void RestyleDialog(Transform root, GameObject prefab)
            {
                var background = AugaStyle.FromPrefab<Image>(prefab, Dialog + "/Background");
                var buttonImage = AugaStyle.FromPrefab<Image>(prefab, Dialog + "/ButtonYes/Image");
                var label = AugaStyle.FromPrefab<TMP_Text>(prefab, Dialog + "/ButtonYes/Label");
                foreach (var image in root.GetComponentsInChildren<Image>(true))
                {
                    if (string.Equals(image.name, "dialog", StringComparison.OrdinalIgnoreCase) || image.name == "bkg")
                        AugaStyle.CopyImage(image, background);
                    else if (image.name == "border (1)")
                        image.enabled = false;
                }
                foreach (var button in root.GetComponentsInChildren<Button>(true))
                    AugaStyle.CopyImage(button.image, buttonImage);
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                    AugaStyle.CopyText(text, label);
            }

            private static Image ImageAt(Transform parent, string name)
            {
                var t = parent.Find(name);
                return t ? t.GetComponent<Image>() : null;
            }

            private static void Knot(Image knot, Image glyph)
            {
                AugaStyle.CopyImage(knot, glyph);
                if (knot) knot.preserveAspect = true;
            }
        }

        // Splices the Compendium entry after Settings into vanilla's explicit gamepad chain (Menu.cs:148-197).
        [HarmonyPatch(typeof(Menu), nameof(Menu.UpdateNavigation))]
        public static class Menu_UpdateNavigation_Patch
        {
            [UsedImplicitly]
            public static void Postfix(Menu __instance)
            {
                var entry = __instance.menuEntriesParent.Find("Compendium");
                if (!entry)
                    return; // Menu.Start's own call runs before Menu_Start_Patch adds the entry
                var compendium = entry.GetComponent<Button>();
                var settings = __instance.m_settingsButton;
                var nav = settings.navigation;
                var next = nav.selectOnDown;
                nav.selectOnDown = compendium;
                settings.navigation = nav;
                compendium.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = settings, selectOnDown = next };
                if (next)
                {
                    var n = next.navigation;
                    n.selectOnUp = compendium;
                    next.navigation = n;
                }
            }
        }

        [HarmonyPatch(typeof(Menu), nameof(Menu.OnClose))]
        public static class Menu_OnClose_Patch
        {
            [UsedImplicitly]
            public static void Postfix(Menu __instance)
            {
                var compendium = __instance.GetComponentInChildren<AugaCompendiumController>(true);
                if (compendium != null && compendium.gameObject.activeSelf)
                    compendium.HideCompendium();
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

