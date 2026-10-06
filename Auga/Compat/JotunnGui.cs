using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Auga.Compat;

// Jotunn GUIManager (soft dependency, patched by reflection): every Jotunn-built UI takes the Auga look in one place.
// Mods build their windows through GUIManager's Create* builders (CreateWoodpanel, CreateButton, CreateText,
// CreateInputField, CreateToggle, CreateDropDown, CreateScrollView, CreateKeyBindField) or restyle their own bundle
// prefabs through its Apply*Style appliers (Jotunn 2.30.2 GUIManager.cs:1096-1416); the builders call the appliers
// (GUIManager.cs:618-1094). Each outermost call (a depth counter skips the nested ones, so a builder is styled once
// after it has finished sizing its children) ends with AugaStyle.Restyle on what it built or styled: woodpanel_*
// -> Auga panel, button -> ButtonFancy, text_field -> Auga input, checkbox -> diamond, Averia legacy fonts -> Source
// Sans / Norsebold, plus Auga scrollbars and dropdown lists here. Callers in the pack: StarLevelSystem (ConfigUI,
// QuickConfigureTool, QuickConfigBroker, startup popups), VNEI (BaseUI/Styling), Epic Loot (CreateButton,
// CreateScrollView), Jotunn's own ModCompatibility window (ModCompatibility.cs:219-233).
// Jotunn-built roots that no applier covers: the colour/gradient pickers (prefabs styled once at startup, before
// AugaSkin loads, GUIManager.cs:346-394, then instantiated by CreateColorPicker/CreateGradientPicker:547-616) are
// restyled per instance; the large-map overlay panel (MinimapManager.SetupGUI, MinimapManager.cs:891-900: only its
// button, base toggle and text go through appliers) is restyled from its root.
// VNEI bypasses GUIManager for part of its window: Styling.ApplyAllComponents (BaseUI.Awake) sets AveriaSerif on every
// text itself and only hands inputs/toggles/buttons/scroll rects to the appliers, its type filters are its own
// TypeToggle (checkbox art, no uGUI Toggle), and runtime content sets its own art (DisplayItem.Awake: item_background,
// AveriaSerif; RecipeScroll.SpawnRecipe: AveriaSerif row labels). Those three entry points get the same Restyle.
public static class JotunnGui
{
    private static Harmony _harmony;
    private static bool _jotunnPatched, _vneiPatched;
    private static int _depth;
    private static PropertyInfo _customGuiFront;
    private static FieldInfo _overlayPanel;

    public static void Init(Harmony harmony)
    {
        _harmony = harmony;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            OnAssembly(assembly);
        if (!_jotunnPatched || !_vneiPatched)
            AppDomain.CurrentDomain.AssemblyLoad += (_, args) => OnAssembly(args.LoadedAssembly);
    }

    private static void OnAssembly(Assembly assembly)
    {
        string name;
        try { name = assembly.GetName().Name; }
        catch { return; }
        try
        {
            if (name == "Jotunn" && !_jotunnPatched)
            {
                _jotunnPatched = true;
                PatchJotunn(assembly);
            }
            else if (name == "VNEI" && !_vneiPatched)
            {
                _vneiPatched = true;
                PatchVnei(assembly);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Auga] {name} GUI restyle hooks failed: {e}");
        }
    }

    private static void PatchJotunn(Assembly jotunn)
    {
        var gui = jotunn.GetType("Jotunn.Managers.GUIManager");
        if (gui == null)
        {
            Debug.LogWarning("[Auga] Jotunn loaded without Jotunn.Managers.GUIManager, its UI keeps the vanilla look");
            return;
        }
        _customGuiFront = gui.GetProperty("CustomGUIFront", BindingFlags.Public | BindingFlags.Static);
        var enter = new HarmonyMethod(typeof(JotunnGui), nameof(Enter));
        var count = 0;
        foreach (var method in gui.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            string exit = null;
            if (method.Name.StartsWith("Apply") && method.Name.EndsWith("Style") && method.GetParameters().Length > 0)
                exit = nameof(ApplyExit);
            else if (method.Name.StartsWith("Create") && method.ReturnType == typeof(GameObject))
                exit = nameof(CreateExit);
            else if (method.Name == "CreateColorPicker" || method.Name == "CreateGradientPicker")
                exit = nameof(PickerExit);
            if (exit == null)
                continue;
            _harmony.Patch(method, prefix: enter, finalizer: new HarmonyMethod(typeof(JotunnGui), exit) { priority = Priority.Last });
            count++;
        }

        var minimap = jotunn.GetType("Jotunn.Managers.MinimapManager");
        var setupGui = minimap?.GetMethod("SetupGUI", BindingFlags.NonPublic | BindingFlags.Instance);
        _overlayPanel = minimap?.GetField("OverlayPanel", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        if (setupGui != null && _overlayPanel != null)
        {
            _harmony.Patch(setupGui, prefix: enter, finalizer: new HarmonyMethod(typeof(JotunnGui), nameof(OverlayExit)) { priority = Priority.Last });
            count++;
        }
        Debug.Log($"[Auga] Jotunn GUIManager: {count} GUI builders/appliers restyled to Auga");
    }

    private static void PatchVnei(Assembly vnei)
    {
        var all = vnei.GetType("VNEI.UI.Styling")?.GetMethod("ApplyAllComponents", BindingFlags.Public | BindingFlags.Static);
        var item = vnei.GetType("VNEI.UI.DisplayItem")?.GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        var row = vnei.GetType("VNEI.UI.RecipeScroll")?.GetMethod("SpawnRecipe", BindingFlags.Public | BindingFlags.Instance);
        if (all != null)
            _harmony.Patch(all, postfix: new HarmonyMethod(typeof(JotunnGui), nameof(TransformPostfix)));
        if (item != null)
            _harmony.Patch(item, postfix: new HarmonyMethod(typeof(JotunnGui), nameof(ComponentPostfix)));
        if (row != null)
            _harmony.Patch(row, postfix: new HarmonyMethod(typeof(JotunnGui), nameof(RowPostfix)));
        if (all == null || item == null || row == null)
            Debug.LogWarning($"[Auga] VNEI restyle hooks: Styling.ApplyAllComponents={all != null} DisplayItem.Awake={item != null} RecipeScroll.SpawnRecipe={row != null}");
    }

    public static void Enter() => _depth++;

    // Apply*Style(target, ...): the first argument is the styled Component or Transform.
    public static void ApplyExit(object[] __args, Exception __exception)
    {
        if (--_depth == 0 && __exception == null && __args.Length > 0)
            Style((__args[0] as Component)?.transform);
    }

    public static void CreateExit(GameObject __result, Exception __exception)
    {
        if (--_depth == 0 && __exception == null && __result)
            Style(__result.transform);
    }

    public static void PickerExit(MethodBase __originalMethod, Exception __exception)
    {
        if (--_depth != 0 || __exception != null)
            return;
        var front = _customGuiFront?.GetValue(null) as GameObject;
        if (!front)
            return;
        Style(front.transform.Find("ColorPicker"));
        if (__originalMethod.Name == "CreateGradientPicker")
            Style(front.transform.Find("GradientPicker"));
    }

    public static void OverlayExit(object __instance, Exception __exception)
    {
        if (--_depth == 0 && __exception == null)
            Style((_overlayPanel.GetValue(__instance) as Component)?.transform);
    }

    public static void ComponentPostfix(Component __instance) => Style(__instance ? __instance.transform : null);

    public static void RowPostfix(RectTransform __result) => Style(__result);

    public static void TransformPostfix(Transform root) => Style(root);

    private static void Style(Transform root)
    {
        if (!root)
            return;
        try
        {
            AugaStyle.RestyleAll(root);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Auga] Jotunn GUI restyle of {root.name} failed: {e}");
        }
    }
}
