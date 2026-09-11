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
            AugaStyle.Restyle(__instance.transform);
            // World rows are instantiated from this template (FejdStartup.cs:1319); restyle the template once.
            AugaStyle.Restyle(__instance.m_worldListElement.transform);
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
