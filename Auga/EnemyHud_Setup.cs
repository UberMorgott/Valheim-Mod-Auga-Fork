using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Auga
{
    [HarmonyPatch]
    public static class EnemyHud_Setup
    {
        [HarmonyPatch(typeof(EnemyHud), nameof(EnemyHud.Awake))]
        public static class EnemyHud_Awake_Patch
        {
            public static bool Prefix(EnemyHud __instance)
            {
                MirrorVanillaPrefab(Auga.Assets.EnemyHud);
                return !SetupHelper.DirectObjectReplace(__instance.transform, Auga.Assets.EnemyHud, "EnemyHud");
            }
        }

        private static bool _prefabMirrored;

        // Brings the Auga prefab to the vanilla EnemyHud shape once, before its first instance wakes up.
        private static void MirrorVanillaPrefab(GameObject prefab)
        {
            if (_prefabMirrored || prefab == null)
            {
                return;
            }

            _prefabMirrored = true;
            MirrorVanillaCanvas(prefab);
            MirrorVanillaStars(prefab);
        }

        // Vanilla EnemyHud root (SoftRef bundle d59cfac) is its own Canvas: ScreenSpaceOverlay, sorting order 200, with a
        // CanvasScaler (ConstantPixelSize, 50 px/unit) driven by GuiScaler (assembly_guiutils GuiScaler.cs:48-58).
        // EnemyHud.UpdateHuds places every HUD at mainCamera.WorldToScreenPointScaled (EnemyHud.cs:238-241, Utils.cs:1360),
        // i.e. in screen pixels, which is only right inside an overlay canvas. The Auga root has no Canvas, so its HUDs
        // inherited the parent GUI canvas and were placed off screen.
        private static void MirrorVanillaCanvas(GameObject prefab)
        {
            if (prefab.GetComponent<Canvas>() != null)
            {
                return;
            }

            var canvas = prefab.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = prefab.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.referencePixelsPerUnit = 50f;
            prefab.AddComponent<GuiScaler>();
        }

        // Vanilla EnemyHud (SoftRef bundle d59cfac, HudBase/HudMount "level_2", "level_3"): every level star is an Image
        // "star" (black back, 15px) with a child Image "star (1)" (gold front, 12px). EnemyHud.cs:149-150 only resolves
        // "level_2"/"level_3", but level mods build on the star itself: StarLevelSystem clones "level_2/star" in its
        // EnemyHud.Awake postfix and reads "star(Clone)" and "star(Clone)/star (1)" as back/front Images; MonsterModifiers
        // recolours each "star*" Image and hides its first child. Auga's stars are an Image-less layout holder with an
        // Image child "star" (HudBase/HudMount) or a single Image (HudBaseBoss), so give them the vanilla shape once,
        // before the prefab is instantiated: holder gets the black back Image, the gold front becomes child "star (1)".
        // The back has the front's size, so the Auga look does not change.
        private static void MirrorVanillaStars(GameObject prefab)
        {
            foreach (var star in prefab.GetComponentsInChildren<RectTransform>(true))
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

        [HarmonyPatch(typeof(EnemyHud), nameof(EnemyHud.ShowHud))]
        public static class EnemyHud_ShowHud_Patch
        {
            public static void Postfix(EnemyHud __instance, Character c)
            {
                if (c == null || __instance.m_huds.TryGetValue(c, out EnemyHud.HudData _))
                {
                    return;
                }

                var hud = __instance.m_huds.LastOrDefault();
                if (hud.Key != null && hud.Value != null)
                {
                    const int maxLevelForStarDisplays = 6;
                    const int firstStarDisplayLevel = 2;
                    const int lastStarDisplayLevel = 3;

                    var level = c.GetLevel();
                    if (level > lastStarDisplayLevel)
                    {
                        var hudGui = hud.Value.m_gui;
                        for (var i = firstStarDisplayLevel; i <= maxLevelForStarDisplays; i++)
                        {
                            var levelDisplay = hudGui.transform.Find($"level_{level}");
                            if (levelDisplay != null)
                            {
                                levelDisplay.gameObject.SetActive(i == level);
                            }
                        }

                        var levelDisplayX = hudGui.transform.Find("level_X");
                        if (levelDisplayX)
                        {
                            var useExtendedLevel = level > maxLevelForStarDisplays;
                            if (useExtendedLevel)
                            {
                                var levelXDisplayName = $"level_{level}";
                                var newLevelXDisplay = hudGui.transform.Find(levelXDisplayName);
                                if (newLevelXDisplay == null)
                                {
                                    newLevelXDisplay = Object.Instantiate(levelDisplayX, levelDisplayX.parent, false);
                                    newLevelXDisplay.name = levelXDisplayName;
                                }

                                var text = levelDisplayX.GetComponentInChildren<TMP_Text>();
                                if (text != null) text.text = $"x {level - 1}";
                            }
                        }
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
