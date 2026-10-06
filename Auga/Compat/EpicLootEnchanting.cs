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
        }
        catch (Exception e)
        {
            Debug.LogError($"[Auga] Epic Loot enchanting table restyle hook failed: {e}");
        }
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
