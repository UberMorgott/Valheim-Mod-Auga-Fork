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
// Start is patched from the first InventoryGui.Awake postfix, not when Almanac's assembly loads: Harmony compiles the
// patched Start, and Mono then runs FormPanel's static initializer (_Modal = AlmanacPlugin.AlmanacUIBundle.LoadAsset)
// - before AlmanacPlugin.Awake loaded the bundle that throws, and FormPanel stays broken for the session (Almanac's UI
// never builds, an NRE every frame). By InventoryGui.Awake Almanac's own postfix has used the bundle, and the panel's
// Start has not run yet (it runs before the panel's first Update).
[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Awake))]
public static class Almanac
{
    private static Harmony _harmony;
    private static bool _patched;
    private static MethodInfo _start;
    private static FieldInfo _formPanel;

    public static void Init(Harmony harmony)
    {
        _harmony = harmony;
    }

    private static void PatchStart()
    {
        if (_patched)
            return;
        _patched = true;
        try
        {
            var panel = AccessTools.TypeByName("Almanac.UI.AlmanacPanel");
            if (panel == null)
                return;
            _start = panel.GetMethod("Start", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            _formPanel = panel.GetField("formPanel", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (_start == null || _formPanel == null)
            {
                Debug.LogWarning("[Auga] Almanac loaded without AlmanacPanel.Start/formPanel, its form modal keeps Almanac's look");
                return;
            }
            _harmony.Patch(_start, postfix: new HarmonyMethod(typeof(Almanac), nameof(PanelStart_Postfix)) { priority = Priority.Last });
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
        var found = false;
        foreach (Transform child in hud)
        {
            if (!child.name.StartsWith("Almanac"))
                continue;
            found = true;
            Restyle(child);
        }
        if (found)
            PatchStart();
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
