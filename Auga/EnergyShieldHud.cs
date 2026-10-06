using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UI;

namespace Auga
{
    // Other players' energy shield (MorgottTweaks, player ZDO floats mt_es / mt_es_max, see AugaStatBars.EnergyShield).
    // Vanilla shows another player's health only on the EnemyHud player HUD (m_baseHudPlayer, EnemyHud.cs:130, shown
    // for players in range and not crouching, :101-124; health set every frame, :213-224). Auga's HudBasePlayer has that
    // bar as Health (100x6 at y 40, bottom pivot) with the name above it; the ES strip is a thin violet bar right under
    // it, filled by current / max ES. It follows the HUD's own visibility (child of the HUD object) and is hidden when
    // the player has no energy shield (mt_es_max <= 0).
    [HarmonyPatch(typeof(EnemyHud), nameof(EnemyHud.UpdateHuds))]
    public static class EnergyShieldHud
    {
        public const string StripName = "AugaEnergyShield";
        private const float StripHeight = 3f, StripGap = 2f;
        private static readonly Color TrackColor = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color FillColor = new Color(0.66f, 0.42f, 1f, 1f); // AugaStatBars.EnergyShieldColor

        [UsedImplicitly]
        public static void Postfix(EnemyHud __instance)
        {
            var show = Auga.ShowEnergyShield.Value;
            foreach (var pair in __instance.m_huds)
            {
                var hud = pair.Value;
                if (!pair.Key || hud?.m_gui == null || !pair.Key.IsPlayer() || !hud.m_gui.activeSelf)
                    continue;
                var gui = hud.m_gui.transform;
                var strip = gui.Find(StripName) as RectTransform;
                var (es, max) = show ? AugaStatBars.EnergyShield(pair.Key) : (0f, 0f);
                if (max <= 0f)
                {
                    if (strip)
                        strip.gameObject.SetActive(false);
                    continue;
                }
                if (!strip)
                    strip = MakeStrip(gui);
                if (!strip)
                    continue;
                strip.gameObject.SetActive(true);
                var fill = (RectTransform)strip.GetChild(0);
                fill.anchorMax = new Vector2(Mathf.Clamp01(es / max), 1f);
            }
        }

        private static RectTransform MakeStrip(Transform gui)
        {
            if (!(gui.Find("Health") is RectTransform health))
                return null;
            var strip = new GameObject(StripName, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            strip.SetParent(gui, false);
            strip.anchorMin = health.anchorMin;
            strip.anchorMax = health.anchorMax;
            strip.pivot = new Vector2(health.pivot.x, 1f);
            strip.sizeDelta = new Vector2(health.sizeDelta.x, StripHeight);
            // Top edge StripGap px under the health bar's bottom edge.
            strip.anchoredPosition = health.anchoredPosition - new Vector2(0f, health.pivot.y * health.sizeDelta.y + StripGap);
            var track = strip.GetComponent<Image>();
            track.color = TrackColor;
            track.raycastTarget = false;
            var fill = new GameObject("fill", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            fill.SetParent(strip, false);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.sizeDelta = Vector2.zero;
            var fillImage = fill.GetComponent<Image>();
            fillImage.color = FillColor;
            fillImage.raycastTarget = false;
            // Same fill art as the health bar beside it, if it has one.
            if (health.Find("health_fast/bar")?.GetComponent<Image>() is Image hp && hp.sprite)
            {
                fillImage.sprite = hp.sprite;
                fillImage.type = hp.type;
            }
            return strip;
        }
    }
}
