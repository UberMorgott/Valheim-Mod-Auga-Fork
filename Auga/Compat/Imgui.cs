using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Auga.Compat;

// IMGUI windows of pack mods have no uGUI to restyle; Auga's look reaches them through their GUIStyle fonts (TASKS
// lever c: Auga's frame art lives in sprite atlases, IMGUI backgrounds need standalone textures, so only fonts change).
// - ConfigurationManager: ConfigurationManagerStyles.CreateStyles (public static, rebuilt from GUI.skin when its
//   settings change, ConfigurationManagerStyles.cs:163) fills ~30 static GUIStyle fields; after it, each takes Auga's
//   Source Sans Pro. Covers its settings window, the embedded ItemManager/PieceManager config tables and Epic Loot's
//   config drawers, which draw with those styles.
// - ValheimBuildCamera: DismantlePreview.DrawBadge (private static, DismantlePreview.cs:560) creates its badge style
//   from GUI.skin.label into a ref field; after it, the badge takes Source Sans Pro Bold.
public static class Imgui
{
    private static Harmony _harmony;
    private static bool _cm, _camera;
    private static FieldInfo[] _styles;

    public static void Init(Harmony harmony)
    {
        _harmony = harmony;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            OnAssembly(assembly);
        if (!_cm || !_camera)
            AppDomain.CurrentDomain.AssemblyLoad += (_, args) => OnAssembly(args.LoadedAssembly);
    }

    private static void OnAssembly(Assembly assembly)
    {
        string name;
        try { name = assembly.GetName().Name; }
        catch { return; }
        try
        {
            if (name == "ConfigurationManager" && !_cm)
            {
                _cm = true;
                var styles = assembly.GetType("ConfigurationManager.ConfigurationManagerStyles");
                var create = styles?.GetMethod("CreateStyles", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (create == null)
                {
                    Debug.LogWarning("[Auga] ConfigurationManager without ConfigurationManagerStyles.CreateStyles, its window keeps its fonts");
                    return;
                }
                _styles = Array.FindAll(styles.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static), f => f.FieldType == typeof(GUIStyle));
                _harmony.Patch(create, postfix: new HarmonyMethod(typeof(Imgui), nameof(CreateStyles_Postfix)));
            }
            else if (name == "ValheimBuildCamera" && !_camera)
            {
                _camera = true;
                var draw = assembly.GetType("ValheimBuildCamera.DismantlePreview")?.GetMethod("DrawBadge", BindingFlags.NonPublic | BindingFlags.Static);
                if (draw != null)
                    _harmony.Patch(draw, postfix: new HarmonyMethod(typeof(Imgui), nameof(DrawBadge_Postfix)));
                else
                    Debug.LogWarning("[Auga] ValheimBuildCamera without DismantlePreview.DrawBadge, its badges keep their font");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Auga] {name} IMGUI font hook failed: {e}");
        }
    }

    public static void CreateStyles_Postfix()
    {
        var font = Auga.Assets.SourceSansProRegular;
        if (!font || _styles == null)
            return;
        foreach (var field in _styles)
            if (field.GetValue(null) is GUIStyle style)
                style.font = font;
    }

    public static void DrawBadge_Postfix(ref GUIStyle badge)
    {
        var font = Auga.Assets.SourceSansProBold;
        if (badge != null && font && badge.font != font)
            badge.font = font;
    }
}
