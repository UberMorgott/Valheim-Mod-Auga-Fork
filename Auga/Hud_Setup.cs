using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace Auga
{
    // Phase 3 (spec 2026-09-11-auga-native-rework, option A): the vanilla Hud keeps every object, field, Canvas and
    // update path (Hud.cs:467-543). Auga only restyles it in place and lets the player shift elements (D5).
    [HarmonyPatch(typeof(Hud), nameof(Hud.Awake))]
    public static class Hud_Setup
    {
        [UsedImplicitly]
        public static void Postfix(Hud __instance)
        {
            // hudroot holds the bars, food, status effects, hotkey bar, ship HUD, minimap and the build HUD.
            AugaStyle.Restyle(__instance.m_rootObject.transform);
            // Build-menu icons are instantiated from this template (Hud.cs:1210 UpdatePieceList).
            AugaStyle.Restyle(__instance.m_pieceIconPrefab.transform);
            // Hotbar slots are instantiated from this template (HotkeyBar.cs:29 m_elementPrefab), outside hudroot.
            var hotkeyBar = __instance.GetComponentInChildren<HotkeyBar>(true);
            if (hotkeyBar)
                AugaStyle.Restyle(hotkeyBar.m_elementPrefab.transform);

            var root = __instance.m_rootObject.transform;
            Movable(__instance.GetComponentInChildren<HotkeyBar>(true)?.transform, "HotKeyBar");
            Movable(__instance.GetComponentInChildren<KeyHints>(true)?.transform, "KeyHints");
            Movable(__instance.m_statusEffectListRoot, "StatusEffects");
            Movable(__instance.m_healthPanel, "HealthPanel");
            // Vanilla sets the stamina, adrenaline and eitr bars' anchoredPosition every frame (Hud.cs:1109-1117,
            // 1149-1157, 1178-1186), so their shared parent is what moves.
            var bars = __instance.m_staminaBar2Root.parent;
            if (bars != root && bars != __instance.m_healthPanel)
                Movable(bars, "StatBars");
            Movable(__instance.m_gpRoot, "GuardianPower");
            Movable(__instance.m_eventBar.transform, "EventBar");
            Movable(__instance.m_actionBarRoot.transform, "ActionProgress");
            Movable(__instance.m_staggerAnimator.transform, "StaggerPanel");
            Movable(__instance.m_mountPanel ? __instance.m_mountPanel.transform : null, "MountPanel");
            Movable(__instance.m_shipHudRoot.transform, "ShipHud");
        }

        public static void Movable(Transform t, string name)
        {
            if (t)
                t.gameObject.AddComponent<MovableHudElement>().InitOffset(name);
        }
    }

    // Build menu (BuildUIV2): tag rows and piece buttons are instantiated from these templates, the first one already
    // in BuildUi.Awake (BuildUi.cs:154, later :385-393, :577), so the templates are restyled before Awake runs.
    [HarmonyPatch(typeof(BuildUi), nameof(BuildUi.Awake))]
    public static class BuildUi_Awake_Patch
    {
        [UsedImplicitly]
        public static void Prefix(BuildUi __instance)
        {
            AugaStyle.Restyle(__instance.m_tagButtonPrefab.transform);
            AugaStyle.Restyle(__instance.m_pieceButtonPrefab.transform);
        }
    }

    // Auga's hover colour: vanilla turns the crosshair yellow over something interactable (Hud.cs:839).
    [HarmonyPatch(typeof(Hud), nameof(Hud.UpdateCrosshair))]
    public static class Hud_UpdateCrosshair_Patch
    {
        private static Color _gold;

        [UsedImplicitly]
        public static void Postfix(Hud __instance)
        {
            if (__instance.m_crosshair.color != Color.yellow)
                return;
            if (_gold == default)
                ColorUtility.TryParseHtmlString(Auga.Colors.BrightestGold, out _gold);
            __instance.m_crosshair.color = _gold;
        }
    }

    //UpdateBuild
    [HarmonyPatch(typeof(Hud), nameof(Hud.UpdateBuild))]
    public static class Hud_UpdateBuild_Patch
    {
        private static void Postfix(Hud __instance, Player player)
        {
            MessageHud.instance.m_messageCenterText.gameObject.SetActive(!player.InPlaceMode());
        }

        [UsedImplicitly]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // 1.0.7: Hud.UpdateBuild IL_00c1 ldstr "{0} [<color=yellow>{1}</color>]"
            const string anchor = "{0} [<color=yellow>{1}</color>]";
            var hits = 0;
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldstr && (instruction.operand as string) == anchor)
                {
                    // Mutate operand in place so labels/blocks on the instruction survive.
                    instruction.operand = anchor.Replace("<color=yellow>", $"<color={Auga.Colors.BrightestGold}>");
                    hits++;
                }
                yield return instruction;
            }
            if (hits != 1)
                Debug.LogError($"[Auga] UpdateBuild transpiler: expected 1 anchor hit, got {hits}");
        }
    }
}
