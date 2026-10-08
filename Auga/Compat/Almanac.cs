using System;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace Auga.Compat;

// Almanac (soft dependency): its InventoryGui.Awake postfix (Almanac 3.8 UI\UI_Patches.cs) instantiates its windows
// under IngameGui/HUD - "Almanac" (bundle AlmanacUI), "Almanac Quest" and "Almanac NPC Dialogue" (DialoguePanel),
// "Almanac NPC UI" (NPCCustomization) - after forcing AveriaSerifLibre on every text (FontManager.SetFont), a
// woodpanel_trophys background (SetVanillaPanelBackground) and sprites copied from the inventory's crafting/trophy
// panels (CopySpriteAndMaterial: Auga's or vanilla's depending on which InventoryGui.Awake postfix ran first). Running
// last, every HUD child named Almanac* gets AugaStyle.RestyleAll.
// The form modal is created later: AlmanacPanel.Start (Almanac 3.8.0.3) instantiates the bundle prefab FormPanel._Modal
// next to the panel and keeps the clone in the static AlmanacPanel.formPanel. That clone is restyled in a postfix on
// Start. The prefab itself is never restyled: it is a bundle asset, and AugaStyle.Panel instantiating its corner
// ornaments under an asset fails in Unity ("Cannot instantiate objects with a parent which is persistent") and leaves
// the corners parentless in the scene.
[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Awake))]
public static class Almanac
{
    private static Harmony _harmony;
    private static bool _patched;
    private static FieldInfo _formPanel;

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
        if (name != "Almanac")
            return;
        _patched = true;
        try
        {
            var panel = assembly.GetType("Almanac.UI.AlmanacPanel");
            var start = panel?.GetMethod("Start", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            _formPanel = panel?.GetField("formPanel", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (start == null || _formPanel == null)
            {
                Debug.LogWarning("[Auga] Almanac loaded without AlmanacPanel.Start/formPanel, its form modal keeps Almanac's look");
                return;
            }
            _harmony.Patch(start, postfix: new HarmonyMethod(typeof(Almanac), nameof(PanelStart_Postfix)) { priority = Priority.Last });
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Auga] Almanac compat failed: {e}");
        }
    }

    [UsedImplicitly]
    [HarmonyPriority(Priority.Last)]
    public static void Postfix(InventoryGui __instance)
    {
        var hud = __instance.transform.parent ? __instance.transform.parent.Find("HUD") : null;
        if (!hud)
            return;
        foreach (Transform child in hud)
        {
            if (child.name.StartsWith("Almanac"))
                Restyle(child);
        }
    }

    private static void PanelStart_Postfix()
    {
        if (_formPanel.GetValue(null) is Component form && form)
            Restyle(form.transform);
    }

    private static void Restyle(Transform root)
    {
        try
        {
            AugaStyle.RestyleAll(root);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Auga] Almanac {root.name} restyle failed: {e}");
        }
    }
}
