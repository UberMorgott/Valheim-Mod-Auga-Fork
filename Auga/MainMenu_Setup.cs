using HarmonyLib;
using UnityEngine.UI;

namespace Auga
{
    // Pre-world menus (FejdStartup: main menu, character select/create, start game with world list, server options
    // and join tab, create world, dialogs). Vanilla layout and logic; every panel is a child of FejdStartup and
    // exists at Awake (HideAll only deactivates them, FejdStartup.cs:526-542), so one restyle covers all of them.
    [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.Awake))]
    public static class FejdStartup_Awake_Patch
    {
        public static void Postfix(FejdStartup __instance)
        {
            HideCinematicsButton(__instance);
            AugaStyle.LinkGameFontFallbacks();
            AugaStyle.Restyle(__instance.transform);
            // World rows are instantiated from this template (FejdStartup.cs:1319); restyle the template once.
            AugaStyle.Restyle(__instance.m_worldListElement.transform);
        }

        // Main-menu texts other mods add or change after Awake: DisplayBepInExInfo's "BepInEx Version" lines (Arial,
        // its FejdStartup.Start postfix, DisplayInfoPlugin.cs:34-69) and ValheimPlus' second version line
        // (FejdStartup.SetupGui postfix: m_versionLabel 900x30, size 14). Running last, the menu group is restyled
        // again (only vanilla/legacy fonts and art not yet mapped change). A corner button another mod puts over the
        // version text (StarLevelSystem's "Mod Config", Jotunn CreateButton at (-20,20) bottom-right of
        // CustomGUIFront, QuickConfigBroker.EnsureCornerButton) is built when Jotunn's GUI is ready, so a second
        // later the version text is right-aligned (V+'s second line kept, not truncated) and moves left clear of any
        // foreign button it overlaps (above it sits vanilla's merch-store button).
        [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.Start))]
        public static class FejdStartup_Start_Patch
        {
            [HarmonyPriority(Priority.Last)]
            public static void Postfix(FejdStartup __instance)
            {
                var label = __instance.m_versionLabel;
                if (!label)
                    return;
                AugaStyle.Restyle(label.transform.parent);
                __instance.StartCoroutine(ClearVersionLabel(__instance, label));
            }

            private static System.Collections.IEnumerator ClearVersionLabel(FejdStartup fejd, TMPro.TMP_Text label)
            {
                yield return new UnityEngine.WaitForSecondsRealtime(1f);
                if (!label)
                    yield break;
                label.overflowMode = TMPro.TextOverflowModes.Overflow;
                label.alignment = TMPro.TextAlignmentOptions.BottomRight;
                label.ForceMeshUpdate();
                var bounds = ScreenRect(label.rectTransform, label.textBounds);
                var shift = 0f;
                foreach (var button in UnityEngine.Object.FindObjectsByType<Button>(UnityEngine.FindObjectsSortMode.None))
                {
                    if (!button.isActiveAndEnabled || button.transform.IsChildOf(fejd.transform))
                        continue;
                    var corners = new UnityEngine.Vector3[4];
                    ((UnityEngine.RectTransform)button.transform).GetWorldCorners(corners);
                    var r = UnityEngine.Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
                    if (r.Overlaps(bounds))
                        shift = UnityEngine.Mathf.Max(shift, bounds.xMax - r.xMin);
                }
                if (shift <= 0f)
                    yield break;
                var scale = label.canvas ? label.canvas.scaleFactor : 1f;
                label.rectTransform.anchoredPosition -= new UnityEngine.Vector2(shift / scale + 8f, 0f);
            }

            // Screen rect of the text's glyph bounds (local) under a ScreenSpaceOverlay canvas.
            private static UnityEngine.Rect ScreenRect(UnityEngine.RectTransform rt, UnityEngine.Bounds local)
            {
                var min = rt.TransformPoint(local.min);
                var max = rt.TransformPoint(local.max);
                return UnityEngine.Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }
        }

        // Join tab: server rows are instantiated from m_serverListElement (ServerListGui.cs:608).
        [HarmonyPatch(typeof(ServerListGui), nameof(ServerListGui.Awake))]
        public static class ServerListGui_Awake_Patch
        {
            public static void Postfix(ServerListGui __instance) => AugaStyle.Restyle(__instance.m_serverListElement.transform);
        }

        // Vanilla notices ("$menu_cloudsavewarning"-style WarningPopup, UGC, modifiers disclaimer, FejdStartup.cs:1546)
        // all go through the one UnifiedPopup of the scene. Restyled once in its Awake (UnifiedPopup.cs:78-97), which
        // caches the button labels and hides the popup.
        [HarmonyPatch(typeof(UnifiedPopup), nameof(UnifiedPopup.Awake))]
        public static class UnifiedPopup_Awake_Patch
        {
            public static void Postfix(UnifiedPopup __instance) => AugaStyle.Restyle(__instance.transform);
        }

        // The 1.0.7 cinematics list (Black Forest / Locked / Back) is unwanted (user request). Vanilla HideAll
        // already hides m_cinematicsMenuList; the button that opens it has no field, only a prefab onClick -> OnCinematics.
        private static void HideCinematicsButton(FejdStartup instance)
        {
            foreach (var button in instance.GetComponentsInChildren<Button>(true))
            {
                var onClick = button.onClick;
                for (var i = 0; i < onClick.GetPersistentEventCount(); i++)
                {
                    if (onClick.GetPersistentMethodName(i) == nameof(FejdStartup.OnCinematics))
                    {
                        button.gameObject.SetActive(false);
                        break;
                    }
                }
            }
        }
    }
}
