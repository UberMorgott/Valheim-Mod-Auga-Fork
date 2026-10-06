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
            AugaStyle.LinkGameFontFallbacks(); // no-op once linked from the main menu
            // hudroot holds the bars, food, status effects, hotkey bar, ship HUD, minimap and the build HUD.
            AugaStyle.Restyle(__instance.m_rootObject.transform);
            Hud_Awake_ModParts.Seen.Clear();
            foreach (Transform child in __instance.m_rootObject.transform)
                Hud_Awake_ModParts.Seen.Add(child.GetInstanceID());
            // Build-menu icons are instantiated from this template (Hud.cs:1210 UpdatePieceList).
            AugaStyle.Restyle(__instance.m_pieceIconPrefab.transform);
            // Hotbar slots are instantiated from this template (HotkeyBar.cs:29 m_elementPrefab), outside hudroot.
            var hotkeyBar = __instance.GetComponentInChildren<HotkeyBar>(true);
            if (hotkeyBar)
                AugaStyle.Restyle(hotkeyBar.m_elementPrefab.transform);

            // Auga's bar cluster (health, stamina, eitr and the food diamonds) built from the vanilla objects; it lives
            // in m_healthPanel, so the HealthPanel offset moves the whole cluster.
            AugaStatBars.Setup(__instance);
            // Bar fills and the ship HUD (sprite sets Restyle does not map).
            HudParts.Setup(__instance);
            // Frames of the local player's summons, under the hotkey bar (Phase 3).
            SummonFrames.Setup(__instance);
            // Loading / sleeping / teleporting screen: m_loadingScreen (LoadingBlack) sits beside hudroot, outside the
            // pass above (Hud.cs:219-231): Averia texts -> Auga fonts, the braid separator -> Auga divider colour.
            if (__instance.m_loadingScreen)
                AugaStyle.Restyle(__instance.m_loadingScreen.transform);

            Movable(__instance.GetComponentInChildren<HotkeyBar>(true)?.transform, "HotKeyBar");
            Movable(__instance.GetComponentInChildren<KeyHints>(true)?.transform, "KeyHints");
            Movable(__instance.m_statusEffectListRoot, "StatusEffects");
            Movable(__instance.m_healthPanel, "HealthPanel");
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

    // HUD parts other mods add in their own Hud.Awake postfixes, after this restyle ran: Epic Loot's AbilityBar and
    // DebugText (EL 0.14.13 EpicLoot.Abilities.Hud_Awake_Patch, EpicLoot.Hud_Awake_Patch: bundle prefabs under
    // m_rootObject), StarLevelSystem's no-map level text (NoMapLevelIndicator, Jotunn CreateText). Running last, every
    // hudroot child the first pass did not see gets the same restyle. BossAwakening's world boss bar (BA_*) keeps its
    // own design (user decision 2026-10-05).
    [HarmonyPatch(typeof(Hud), nameof(Hud.Awake))]
    public static class Hud_Awake_ModParts
    {
        public static readonly HashSet<int> Seen = new HashSet<int>();

        [UsedImplicitly]
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(Hud __instance)
        {
            foreach (Transform child in __instance.m_rootObject.transform)
            {
                if (!Seen.Add(child.GetInstanceID()) || child.name.StartsWith("BA_"))
                    continue;
                AugaStyle.RestyleAll(child);
            }
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

    // Auga's hover colour: vanilla turns the crosshair yellow over something interactable (Hud.cs:839), and every
    // Hoverable writes its key tags as "[<color=yellow><b>$KEY_Use</b></color>]" (e.g. Container, Door, ItemDrop
    // GetHoverText). The text UpdateCrosshair puts into m_hoverName goes through HoverText first: after the gamepad
    // rewrite (which matches the yellow tag), so only the keyboard tags it leaves take Auga's gold.
    [HarmonyPatch(typeof(Hud), nameof(Hud.UpdateCrosshair))]
    public static class Hud_UpdateCrosshair_Patch
    {
        private static Color _gold;
        private static string _goldTag;

        public static string HoverText(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf("<color=yellow>", System.StringComparison.Ordinal) < 0)
                return text;
            _goldTag ??= $"<color={Auga.Colors.BrightestGold}>";
            return text.Replace("<color=yellow>", _goldTag);
        }

        [UsedImplicitly]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var getHoverText = AccessTools.Method(typeof(Hoverable), nameof(Hoverable.GetHoverText));
            var setText = AccessTools.PropertySetter(typeof(TMPro.TMP_Text), nameof(TMPro.TMP_Text.text));
            var seenHover = false;
            var hits = 0;
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(getHoverText))
                    seenHover = true;
                else if (seenHover && hits == 0 && instruction.Calls(setText))
                {
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Hud_UpdateCrosshair_Patch), nameof(HoverText)));
                    hits++;
                }
                yield return instruction;
            }
            if (hits != 1)
                Debug.LogError($"[Auga] UpdateCrosshair transpiler: expected 1 hover text hit, got {hits}");
        }

        [UsedImplicitly]
        public static void Postfix(Hud __instance)
        {
            if (__instance.m_crosshair.color != Color.yellow)
                return;
            // A bad colour string leaves _gold default and the vanilla yellow crosshair untouched.
            if (_gold == default && !ColorUtility.TryParseHtmlString(Auga.Colors.BrightestGold, out _gold))
                return;
            __instance.m_crosshair.color = _gold;
        }
    }

    //UpdateBuild
    [HarmonyPatch(typeof(Hud), nameof(Hud.UpdateBuild))]
    public static class Hud_UpdateBuild_Patch
    {
        // No centre-message hiding in place mode (the old Auga build menu covered that spot; the restyled vanilla
        // menu does not): vanilla reports snap point cycling and placement errors there (Player.cs:4056, 3037-3074).

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
