using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Auga
{
    [HarmonyPatch]
    public static class EnemyHud_Setup
    {
        // Auga's extra level displays of one HUD (level_4..level_6, level_X), found once when the HUD is made, like
        // vanilla caches level_2/level_3 in ShowHud (EnemyHud.cs:149-150).
        private sealed class ExtraLevels
        {
            public GameObject[] Stars;
            public GameObject Many;
            public TMP_Text ManyText;
        }

        private const int FirstExtraLevel = 4;
        private const int LastExtraLevel = 6;

        private static readonly ConditionalWeakTable<EnemyHud.HudData, ExtraLevels> ExtraLevelDisplays = new();

        // The vanilla EnemyHud object stays: its root Canvas, GuiScaler, component and Awake (EnemyHud.cs:66-73) are
        // untouched. Only its templates change: before Awake hides them, the vanilla HudRoot (m_hudRoot) is swapped for
        // Auga's and the five template fields point into it. Every Awake patch of other mods (e.g. StarLevelSystem,
        // which clones "level_2/star" and looks up "HudRoot") then runs on this live instance and sees Auga's templates.
        [HarmonyPatch(typeof(EnemyHud), nameof(EnemyHud.Awake))]
        public static class EnemyHud_Awake_Patch
        {
            [HarmonyPriority(Priority.First), UsedImplicitly]
            public static void Prefix(EnemyHud __instance)
            {
                ReplaceTemplates(__instance, Auga.Assets.EnemyHud.GetComponent<EnemyHud>());
            }
        }

        private static void ReplaceTemplates(EnemyHud hud, EnemyHud auga)
        {
            var oldRoot = hud.m_hudRoot;
            var oldTemplates = new[] { hud.m_baseHud, hud.m_baseHudBoss, hud.m_baseHudPlayer, hud.m_baseHudMount };

            var newRoot = Object.Instantiate(auga.m_hudRoot, oldRoot.transform.parent, false);
            newRoot.name = oldRoot.name;
            newRoot.transform.SetSiblingIndex(oldRoot.transform.GetSiblingIndex());
            hud.m_hudRoot = newRoot;
            hud.m_baseHud = Counterpart(auga.m_baseHud, auga.m_hudRoot, newRoot);
            hud.m_baseHudBoss = Counterpart(auga.m_baseHudBoss, auga.m_hudRoot, newRoot);
            hud.m_baseHudPlayer = Counterpart(auga.m_baseHudPlayer, auga.m_hudRoot, newRoot);
            hud.m_baseHudMount = Counterpart(auga.m_baseHudMount, auga.m_hudRoot, newRoot);
            hud.m_maxShowDistance = auga.m_maxShowDistance;
            hud.m_maxShowDistanceBoss = auga.m_maxShowDistanceBoss;
            hud.m_hoverShowDuration = auga.m_hoverShowDuration;

            foreach (var template in oldTemplates)
            {
                if (template != null && !template.transform.IsChildOf(oldRoot.transform))
                {
                    Object.DestroyImmediate(template);
                }
            }

            Object.DestroyImmediate(oldRoot);

            MirrorVanillaStars(newRoot);
        }

        // The same object inside the instantiated copy of Auga's HudRoot.
        private static GameObject Counterpart(GameObject template, GameObject prefabRoot, GameObject copyRoot)
        {
            var path = template.name;
            for (var t = template.transform.parent; t != prefabRoot.transform; t = t.parent)
            {
                path = t.name + "/" + path;
            }

            return copyRoot.transform.Find(path).gameObject;
        }

        // Vanilla EnemyHud (SoftRef bundle d59cfac, HudBase/HudMount "level_2", "level_3"): every level star is an Image
        // "star" (black back, 15px) with a child Image "star (1)" (gold front, 12px). EnemyHud.cs:149-150 only resolves
        // "level_2"/"level_3", but level mods build on the star itself: StarLevelSystem clones "level_2/star" in its
        // EnemyHud.Awake postfix and reads "star(Clone)" and "star(Clone)/star (1)" as back/front Images; MonsterModifiers
        // recolours each "star*" Image and hides its first child. Auga's stars are an Image-less layout holder with an
        // Image child "star" (HudBase/HudMount) or a single Image (HudBaseBoss), so give them the vanilla shape:
        // holder gets the black back Image, the gold front becomes child "star (1)".
        // The back has the front's size, so the Auga look does not change.
        private static void MirrorVanillaStars(GameObject root)
        {
            foreach (var star in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (!star.name.StartsWith("star") || star.parent == null || !star.parent.name.StartsWith("level_"))
                {
                    continue;
                }

                var back = star.GetComponent<Image>();
                Image front;
                if (back == null)
                {
                    front = star.childCount > 0 ? star.GetChild(0).GetComponent<Image>() : null;
                    if (front == null)
                    {
                        continue;
                    }

                    back = star.gameObject.AddComponent<Image>();
                }
                else
                {
                    var frontObject = new GameObject("star (1)", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    var frontRect = (RectTransform)frontObject.transform;
                    frontRect.SetParent(star, false);
                    frontRect.anchorMin = Vector2.zero;
                    frontRect.anchorMax = Vector2.one;
                    frontRect.sizeDelta = Vector2.zero;
                    front = frontObject.GetComponent<Image>();
                    front.sprite = back.sprite;
                    front.color = back.color;
                }

                front.name = "star (1)";
                front.raycastTarget = false;
                back.sprite = front.sprite;
                back.color = Color.black;
                back.raycastTarget = false;
            }
        }

        // Caches the extra level displays of a new HUD. Vanilla ShowHud adds the HUD only when it is new
        // (EnemyHud.cs:128-157), so the prefix tells the postfix whether this call made it.
        [HarmonyPatch(typeof(EnemyHud), nameof(EnemyHud.ShowHud))]
        public static class EnemyHud_ShowHud_Patch
        {
            [UsedImplicitly]
            public static void Prefix(EnemyHud __instance, Character c, out bool __state)
            {
                __state = !__instance.m_huds.ContainsKey(c);
            }

            [UsedImplicitly]
            public static void Postfix(EnemyHud __instance, Character c, bool __state)
            {
                if (!__state)
                {
                    return;
                }

                var hud = __instance.m_huds[c];
                var gui = hud.m_gui.transform;
                var stars = new GameObject[LastExtraLevel - FirstExtraLevel + 1];
                for (var level = FirstExtraLevel; level <= LastExtraLevel; level++)
                {
                    stars[level - FirstExtraLevel] = gui.Find($"level_{level}")?.gameObject;
                }

                var many = gui.Find("level_X");
                ExtraLevelDisplays.Add(hud, new ExtraLevels
                {
                    Stars = stars,
                    Many = many?.gameObject,
                    ManyText = many != null ? many.GetComponentInChildren<TMP_Text>(true) : null,
                });
            }
        }

        // Vanilla shows a HUD's level with level_2/level_3 in UpdateHuds (EnemyHud.cs:191-199). Auga's extra displays
        // extend exactly that code: this call goes right after it, so they follow the same rules (shown HUDs only,
        // every frame). A level mod that replaces the vanilla level display (e.g. StarLevelSystem's UpdateHuds
        // transpiler removes it) also owns the levels above 3, so without the vanilla code there is nothing to extend
        // and Auga's extra displays stay hidden instead of doubling that mod's stars. Priority.Last: runs on the other
        // transpilers' result.
        [HarmonyPatch(typeof(EnemyHud), nameof(EnemyHud.UpdateHuds))]
        public static class EnemyHud_UpdateHuds_Transpiler
        {
            [HarmonyTranspiler, HarmonyPriority(Priority.Last), UsedImplicitly]
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var matcher = new CodeMatcher(instructions).MatchForward(true,
                    new CodeMatch(ci => ci.IsLdloc()),
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(EnemyHud.HudData), nameof(EnemyHud.HudData.m_level3))),
                    new CodeMatch(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Component), nameof(Component.gameObject))),
                    new CodeMatch(ci => ci.IsLdloc()),
                    new CodeMatch(OpCodes.Ldc_I4_3),
                    new CodeMatch(OpCodes.Ceq),
                    new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(GameObject), nameof(GameObject.SetActive))));
                if (matcher.IsInvalid)
                {
                    Auga.Log("EnemyHud.UpdateHuds: the vanilla level display is replaced by another mod, Auga's level_4..level_X stay hidden");
                    return instructions;
                }

                var loadHud = new CodeInstruction(matcher.InstructionAt(-6).opcode, matcher.InstructionAt(-6).operand);
                var loadLevel = new CodeInstruction(matcher.InstructionAt(-3).opcode, matcher.InstructionAt(-3).operand);
                matcher.Advance(1);
                // Both paths of the level_3 null check (EnemyHud.cs:196) join here; the call takes over their label.
                loadHud.MoveLabelsFrom(matcher.Instruction);
                matcher.Insert(loadHud, loadLevel, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(EnemyHud_Setup), nameof(UpdateExtraLevels))));
                return matcher.InstructionEnumeration();
            }
        }

        private static void UpdateExtraLevels(EnemyHud.HudData hud, int level)
        {
            if (!ExtraLevelDisplays.TryGetValue(hud, out var displays))
            {
                return;
            }

            for (var i = 0; i < displays.Stars.Length; i++)
            {
                if (displays.Stars[i] != null)
                {
                    displays.Stars[i].SetActive(level == FirstExtraLevel + i);
                }
            }

            if (displays.Many != null)
            {
                var many = level > LastExtraLevel;
                displays.Many.SetActive(many);
                if (many && displays.ManyText != null)
                {
                    var text = $"x {level - 1}";
                    if (displays.ManyText.text != text)
                    {
                        displays.ManyText.text = text;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(EnemyHud), nameof(EnemyHud.UpdateHuds))]
        [HarmonyAfter("org.bepinex.plugins.creaturelevelcontrol")]
        public static class EnemyHud_UpdateHuds_Patch
        {
            public static void Postfix(EnemyHud __instance)
            {
                foreach (var hud in __instance.m_huds)
                {
                    if (hud.Key != null && hud.Value != null && hud.Value.m_gui != null)
                    {
                        var name = hud.Value.m_gui.transform.Find("Name");
                        if (name != null)
                        {
                            var rt = (RectTransform)name;
                            rt.anchoredPosition = new Vector2(0, 38.5f);
                        }
                    }
                }
            }
        }
    }
}
