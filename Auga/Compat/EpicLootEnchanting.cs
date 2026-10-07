using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Auga.Compat;

// Epic Loot enchanting table window (soft dependency, patched by reflection). EnchantingTableUI.CreateUI instantiates
// Epic Loot's own bundle prefab EnchantingUI next to the StoreGui (EL 0.14.13 EnchantingTableUI.cs CreateUI): wood
// panel (woodpanel_large), tab column (TabBackground + TabHandler tabs on the plain "button" art), item_background
// lists, inputs, dropdowns, scrollbars and toggles, CheckMark ticks, AveriaSans/Serif legacy texts and EL's
// AveriaSansLibre-Bold SDF tab labels. Neither Jotunn's GUIManager nor the StoreGui.Show finalizer builds or reaches it,
// and Epic Loot's own Auga pass (EnchantingTableUI.Start -> EnchantingUIAugaFixup.AugaFixup) returns at once because
// EpicLoot.HasAuga is never assigned (TASKS.md lever a), so every tab kept the vanilla wood look.
// EnchantingTableUI.Start (after Epic Loot's localize, tab setup and its no-op Auga pass) gets AugaStyle.RestyleAll on
// the whole window - all tabs, locked or not, are children of one prefab - and on each MultiSelectItemList's
// ElementPrefab (a separate bundle asset the lists clone rows from, MultiSelectItemList.cs:387), so later rows are
// born Auga-styled.
public static class EpicLootEnchanting
{
    private static Harmony _harmony;
    private static bool _patched;
    private static FieldInfo _elementPrefab;
    private static readonly HashSet<int> StyledPrefabs = new HashSet<int>();

    public static void Init(Harmony harmony)
    {
        _harmony = harmony;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            OnAssembly(assembly);
        if (!_patched)
            AppDomain.CurrentDomain.AssemblyLoad += (_, args) => OnAssembly(args.LoadedAssembly);
    }

    private static void OnAssembly(Assembly assembly)
    {
        if (_patched)
            return;
        string name;
        try { name = assembly.GetName().Name; }
        catch { return; }
        if (name != "EpicLoot")
            return;
        _patched = true;
        try
        {
            var ui = assembly.GetType("EpicLoot_UnityLib.EnchantingTableUI");
            var start = ui?.GetMethod("Start", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            _elementPrefab = assembly.GetType("EpicLoot_UnityLib.MultiSelectItemList")?.GetField("ElementPrefab", BindingFlags.Public | BindingFlags.Instance);
            if (start == null)
            {
                Debug.LogWarning("[Auga] Epic Loot loaded without EnchantingTableUI.Start, its enchanting table keeps the vanilla look");
                return;
            }
            _harmony.Patch(start, postfix: new HarmonyMethod(typeof(EpicLootEnchanting), nameof(Start_Postfix)) { priority = Priority.Last });

            // Epic Loot's modal dialogs (AugmentChoiceDialog via AugmentHelper.CreateAugmentChoiceDialog, the crafting /
            // gamble result CraftSuccessDialog.Create) are clones of the vanilla variant dialog, which InventoryGui's
            // restyle already gave the Auga frame; their Jotunn buttons are Auga too. Only the item description they
            // turn into a scroll view (CraftSuccessDialog.ConvertToScrollingDescription, EL 0.14.13
            // CraftSuccessDialog.cs:87) clones InventoryGui.m_recipeListScroll, whose vanilla scrollbar art no restyle
            // pass maps: it gets Auga's scrollbar there.
            var convert = assembly.GetType("EpicLoot.Crafting.CraftSuccessDialog")?.GetMethod("ConvertToScrollingDescription", BindingFlags.Public | BindingFlags.Static);
            if (convert != null)
                _harmony.Patch(convert, postfix: new HarmonyMethod(typeof(EpicLootEnchanting), nameof(ScrollingDescription_Postfix)));
            else
                Debug.LogWarning("[Auga] Epic Loot without CraftSuccessDialog.ConvertToScrollingDescription, its dialog scrollbars keep the vanilla look");

            // Epic Loot's message panels, each its own bundle prefab with wood art, legacy Averia text and plain
            // buttons: the welcome and config-update popups at the main menu (FejdStartup.Start postfixes,
            // WelcomeMessage_FejdStartup_Start_Patch / ConfigUpdatePrompt_FejdStartup_Start_Patch) and the socket
            // break / shard chisel confirmations (ConfirmPrompt.Create). MessagePanelBase.Awake (ConfigMessage and every
            // ConfirmPrompt; EL 0.14.13 MessagePanelBase.cs:17) and WelcomeMessage.Awake (WelcomeMessage.cs:27) find
            // their parts and, with EpicLoot.HasAuga (never set), would run Epic Loot's empty Auga stubs; after them
            // the whole panel gets AugaStyle.RestyleAll.
            // Compendium pages (MagicPages on the TextsDialog, EL 0.14.13 EpicLoot.Compendium\MagicPages.cs:49): its
            // search field takes the close button's disabled sprite as background (now Auga's large fancy-button
            // art) and its page rows are legacy texts EL builds per page on AveriaSerifLibre (OnSelectText). The search
            // field gets Auga's input art once; each built page is restyled like the rest of the compendium.
            var pages = assembly.GetType("EpicLoot.Compendium.MagicPages");
            _magicSearch = pages?.GetProperty("Search", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) as MemberInfo
                           ?? pages?.GetField("Search", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var pagesAwake = pages?.GetMethod("Awake", BindingFlags.Public | BindingFlags.Instance);
            var selectText = pages?.GetMethod("OnSelectText", BindingFlags.Public | BindingFlags.Instance);
            if (pagesAwake != null && selectText != null && _magicSearch != null)
            {
                _harmony.Patch(pagesAwake, postfix: new HarmonyMethod(typeof(EpicLootEnchanting), nameof(MagicPagesAwake_Postfix)));
                _harmony.Patch(selectText, postfix: new HarmonyMethod(typeof(EpicLootEnchanting), nameof(MagicPagesSelect_Postfix)));
            }
            else
                Debug.LogWarning("[Auga] Epic Loot without MagicPages.Awake/OnSelectText/Search, its compendium pages keep its own look");

            // Rarity backgrounds Epic Loot re-assigns on every update (API.ApplyMagicItemBackground from its InventoryGrid
            // .UpdateGui / HotkeyBar.UpdateIcons transpilers, ApplyMagicItemBackgroundToIcon for the recipe list and
            // recipe icon, AugmentHelper / CraftSuccessDialog MagicBG) all take their art from EpicLoot.GetMagicItemBgSprite
            // (EL 0.14.13 EpicLoot.cs:941), Epic Loot's own switch between its generic and Auga art (on EpicLoot.HasAuga,
            // never assigned, and setting it would also run EL's broken Auga stubs on its popups). Its result becomes
            // AugaStyle.RarityFrame, the same art the Restyle role gives bundle rows; Epic Loot still sets the colour.
            var bgSprite = assembly.GetType("EpicLoot.EpicLoot")?.GetMethod("GetMagicItemBgSprite", BindingFlags.Public | BindingFlags.Static);
            if (bgSprite != null && bgSprite.ReturnType == typeof(Sprite))
                _harmony.Patch(bgSprite, postfix: new HarmonyMethod(typeof(EpicLootEnchanting), nameof(MagicItemBg_Postfix)));
            else
                Debug.LogWarning("[Auga] Epic Loot without EpicLoot.GetMagicItemBgSprite, its rarity slot backgrounds keep Epic Loot's art");

            foreach (var typeName in new[] { "EpicLoot.MessagePanelBase", "EpicLoot.WelcomeMessage" })
            {
                var awake = assembly.GetType(typeName)?.GetMethod("Awake", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (awake != null)
                    _harmony.Patch(awake, postfix: new HarmonyMethod(typeof(EpicLootEnchanting), nameof(Panel_Postfix)) { priority = Priority.Last });
                else
                    Debug.LogWarning($"[Auga] Epic Loot without {typeName}.Awake, that popup keeps the vanilla look");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Auga] Epic Loot enchanting table restyle hook failed: {e}");
        }
    }

    private static MemberInfo _magicSearch;

    public static void MagicPagesAwake_Postfix(MonoBehaviour __instance)
    {
        try
        {
            var search = _magicSearch is PropertyInfo p ? p.GetValue(__instance) : ((FieldInfo)_magicSearch).GetValue(__instance);
            var input = search?.GetType().GetProperty("Input")?.GetValue(search) as UnityEngine.UI.Selectable
                        ?? search?.GetType().GetField("Input")?.GetValue(search) as UnityEngine.UI.Selectable;
            var image = input ? (input.targetGraphic as UnityEngine.UI.Image ?? input.GetComponent<UnityEngine.UI.Image>()) : null;
            if (image)
                AugaStyle.InputArt(image);
            AugaStyle.Restyle(input ? input.transform : __instance.transform);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Auga] Epic Loot compendium search restyle failed: {e}");
        }
    }

    public static void MagicPagesSelect_Postfix(MonoBehaviour __instance)
    {
        var frame = __instance ? __instance.transform.Find("Texts_frame") : null;
        if (frame)
            AugaStyle.Restyle(frame);
    }

    public static void Panel_Postfix(MonoBehaviour __instance)
    {
        if (!__instance)
            return;
        try
        {
            AugaStyle.RestyleAll(__instance.transform);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Auga] Epic Loot {__instance.name} restyle failed: {e}");
        }
    }

    public static void MagicItemBg_Postfix(ref Sprite __result)
    {
        var frame = AugaStyle.RarityFrame;
        if (frame)
            __result = frame;
    }

    public static void ScrollingDescription_Postfix(UnityEngine.UI.ScrollRect __result)
    {
        if (__result && __result.verticalScrollbar)
            AugaStyle.Scrollbar(__result.verticalScrollbar);
    }

    public static void Start_Postfix(MonoBehaviour __instance)
    {
        if (!__instance)
            return;
        try
        {
            if (_elementPrefab != null)
                foreach (var list in __instance.GetComponentsInChildren(_elementPrefab.DeclaringType, true))
                    if (_elementPrefab.GetValue(list) is Component prefab && prefab && StyledPrefabs.Add(prefab.GetInstanceID()))
                        AugaStyle.RestyleAll(prefab.transform);
            AugaStyle.RestyleAll(__instance.transform);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Auga] Epic Loot enchanting table restyle failed: {e}");
        }
    }
}
