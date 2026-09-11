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
        // Pause menu (Esc), restyled in place (spec 2026-09-11-auga-native-rework, Phase 1). The vanilla Menu keeps
        // its GameObject, its own Canvas (override sorting, order 1700; scene _GameMain/LoadingGUI/PixelFix/IngameGui/Menu)
        // and every field, so Start/Show/Hide/Update/UpdateNavigation (Menu.cs:120-403) run unchanged.
        [HarmonyPatch(typeof(Menu), nameof(Menu.Start))]
        public static class Menu_Start_Patch
        {
            [UsedImplicitly]
            public static void Postfix(Menu __instance)
            {
                AddCompendium(__instance);
                AugaStyle.Restyle(__instance.transform);
            }

            // Compendium = the vanilla Texts dialog (InventoryGui.m_textsDialog, scene Inventory_screen/root/Texts).
            // Vanilla reaches it only from the inventory's Info panel: OnOpenTexts (InventoryGui.cs:2335-2343) runs
            // TextsDialog.Setup (TextsDialog.cs:63-74), and Esc closes it (InventoryGui.cs:539-542). The entry takes the
            // same path: close the menu (Menu.cs:269), open the inventory (InventoryGui.cs:1034), then OnOpenTexts.
            // The entry is a clone of the vanilla Settings entry.
            private static void AddCompendium(Menu menu)
            {
                var settings = menu.m_settingsButton;
                var entry = Object.Instantiate(settings.gameObject, settings.transform.parent, false);
                entry.name = "Compendium";
                entry.transform.SetSiblingIndex(settings.transform.GetSiblingIndex() + 1);
                var button = entry.GetComponent<Button>();
                button.onClick = new Button.ButtonClickedEvent(); // the clone carries Settings' persistent OnSettings call
                button.onClick.AddListener(() =>
                {
                    menu.Hide();
                    InventoryGui.instance.Show(null);
                    menu.StartCoroutine(OpenTexts());
                });
                entry.GetComponentInChildren<TMP_Text>(true).text = "$inventory_texts"; // vanilla Texts title
                Localization.instance.Localize(entry.transform);
                menu.UpdateNavigation(); // Start built the chain before the entry existed (Menu.cs:124)
            }

            // Show only sets the Animator's "visible" (InventoryGui.cs:1037); the animation activates the inventory
            // root a frame later. Vanilla calls OnOpenTexts from a visible inventory, where TextsDialog.ShowText can
            // start its FocusOnCurrentLevel coroutine (TextsDialog.cs:199), so wait for the root like a player would.
            private static System.Collections.IEnumerator OpenTexts()
            {
                var gui = InventoryGui.instance;
                while (gui && InventoryGui.IsVisible() && !gui.m_textsDialog.transform.parent.gameObject.activeInHierarchy)
                    yield return null;
                if (gui && InventoryGui.IsVisible())
                    gui.OnOpenTexts();
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

        // The vanilla Texts dialog, restyled once when it first wakes (it starts inactive; Setup activates it,
        // TextsDialog.cs:65). Its list element template is a child, so later entries inherit the look.
        [HarmonyPatch(typeof(TextsDialog), nameof(TextsDialog.Awake))]
        public static class TextsDialog_Awake_Patch
        {
            [UsedImplicitly]
            public static void Postfix(TextsDialog __instance) => AugaStyle.Restyle(__instance.transform);
        }

        // Vanilla highlights topics with <color=yellow> (TextsDialog.cs:248); Auga's topic colour instead.
        [HarmonyPatch(typeof(TextsDialog), nameof(TextsDialog.ShowText), typeof(TextsDialog.TextInfo))]
        public static class TextsDialog_ShowText_Patch
        {
            [UsedImplicitly]
            public static void Postfix(TextsDialog __instance)
            {
                __instance.m_textArea.text = __instance.m_textArea.text.Replace("color=yellow", $"color={Auga.Colors.Topic}");
            }
        }
    }
}
