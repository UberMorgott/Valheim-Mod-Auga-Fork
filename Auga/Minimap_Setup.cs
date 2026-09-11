using HarmonyLib;

namespace Auga
{
    // Phase 3: the vanilla minimap (small and large) keeps all its objects and fields. It lives under hudroot, so
    // Hud_Setup's restyle covers it; this only makes the small map movable (Minimap.m_smallRoot, Minimap.cs:146).
    [HarmonyPatch(typeof(Minimap), nameof(Minimap.Start))]
    public static class Minimap_Setup
    {
        public static void Postfix(Minimap __instance) => Hud_Setup.Movable(__instance.m_smallRoot.transform, "Minimap");
    }
}
