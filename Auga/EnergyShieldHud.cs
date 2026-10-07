using System.Runtime.CompilerServices;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UI;

namespace Auga
{
    // Other players' shields under their overhead health bar. Vanilla shows another player's health only on the
    // EnemyHud player HUD (m_baseHudPlayer, EnemyHud.cs:130, shown for players in range and not crouching, :101-124;
    // health set every frame, :213-224). Auga's HudBasePlayer has that bar as Health (100x6 at y 40, bottom pivot)
    // with the name above it. Under it go thin strips, each filled by current / max, in the local HUD's rim order
    // from the bar outwards (AugaStatBars.Shield):
    // - violet energy shield (MorgottTweaks [EnergyShield], player ZDO mt_es / mt_es_max, AugaStatBars.EnergyShield);
    // - blue absorb shield (vanilla SE_Shield, e.g. the Staff of Protection bubble). Its remaining absorb lives only
    //   in the owner's SEMan (SE_Shield.cs; SEMan syncs just the seAttrib bitmask, SEMan.cs:103-134), so the owner's
    //   MorgottTweaks [Multiplayer] ShareShieldAbsorb publishes it as player ZDO mt_shield / mt_shield_max.
    // A strip is hidden while its max is 0 and the strips close up; they follow the HUD's own visibility (children of
    // the HUD object). The two ZDO floats of a pair arrive independently, so the fill is clamped to 0..1.
    [HarmonyPatch(typeof(EnemyHud), nameof(EnemyHud.UpdateHuds))]
    public static class EnergyShieldHud
    {
        public const string StripName = "AugaEnergyShield";
        public const string ShieldStripName = "AugaShield";
        private const float StripHeight = 3f, StripGap = 2f;
        private static readonly Color TrackColor = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color EnergyFillColor = new Color(0.66f, 0.42f, 1f, 1f); // AugaStatBars.EnergyShieldColor
        private static readonly Color ShieldFillColor = new Color(0.35f, 0.62f, 1f, 1f); // AugaStatBars.ShieldColor
        public static readonly int ShieldKey = "mt_shield".GetStableHashCode();
        public static readonly int ShieldMaxKey = "mt_shield_max".GetStableHashCode();

        private sealed class Strip
        {
            public RectTransform Rect;
            public RectTransform Fill;
            public int Slot = -1;
            public float Ratio = -1f;
        }

        private sealed class Strips
        {
            public Strip Energy, Shield;
        }

        // Per HUD, made on first need; a HUD without a Health bar keeps an empty entry (nothing to attach to).
        private static readonly ConditionalWeakTable<EnemyHud.HudData, Strips> HudStrips = new();

        [UsedImplicitly]
        public static void Postfix(EnemyHud __instance)
        {
            var showEnergy = Auga.ShowEnergyShield.Value;
            foreach (var pair in __instance.m_huds)
            {
                var hud = pair.Value;
                var character = pair.Key;
                if (!character || hud?.m_gui == null || !hud.m_gui.activeSelf || !character.IsPlayer())
                    continue;
                var zdo = character.m_nview ? character.m_nview.GetZDO() : null;
                var (es, esMax) = showEnergy ? AugaStatBars.EnergyShield(character) : (0f, 0f);
                var shield = zdo != null ? zdo.GetFloat(ShieldKey) : 0f;
                var shieldMax = zdo != null ? zdo.GetFloat(ShieldMaxKey) : 0f;
                var strips = HudStrips.GetValue(hud, _ => new Strips());
                var slot = 0;
                if (Show(ref strips.Energy, hud.m_gui.transform, StripName, EnergyFillColor, es, esMax, slot))
                    slot++;
                Show(ref strips.Shield, hud.m_gui.transform, ShieldStripName, ShieldFillColor, shield, shieldMax, slot);
            }
        }

        // Shows `strip` at `slot` filled by value / max, or hides it when max <= 0; true when shown.
        private static bool Show(ref Strip strip, Transform gui, string name, Color color, float value, float max, int slot)
        {
            if (max <= 0f)
            {
                if (strip != null && strip.Rect && strip.Rect.gameObject.activeSelf)
                    strip.Rect.gameObject.SetActive(false);
                return false;
            }

            if (strip == null || !strip.Rect)
                strip = MakeStrip(gui, name, color);
            if (strip == null)
                return false;
            if (!strip.Rect.gameObject.activeSelf)
                strip.Rect.gameObject.SetActive(true);
            if (strip.Slot != slot)
            {
                strip.Slot = slot;
                Place(strip.Rect, gui, slot);
            }

            var ratio = Mathf.Clamp01(value / max);
            if (strip.Ratio != ratio)
            {
                strip.Ratio = ratio;
                strip.Fill.anchorMax = new Vector2(ratio, 1f);
            }

            return true;
        }

        // Top edge StripGap px under the health bar's bottom edge, each further slot one strip lower.
        private static void Place(RectTransform strip, Transform gui, int slot)
        {
            if (!(gui.Find("Health") is RectTransform health))
                return;
            var below = health.pivot.y * health.sizeDelta.y + StripGap + slot * (StripHeight + StripGap);
            strip.anchoredPosition = health.anchoredPosition - new Vector2(0f, below);
        }

        private static Strip MakeStrip(Transform gui, string name, Color color)
        {
            if (!(gui.Find("Health") is RectTransform health))
                return null;
            // A strip left by an earlier build of this HUD object is reused instead of doubled.
            var rect = gui.Find(name) as RectTransform;
            if (!rect)
            {
                rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                rect.SetParent(gui, false);
                var fillRect = new GameObject("fill", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                fillRect.SetParent(rect, false);
            }

            rect.anchorMin = health.anchorMin;
            rect.anchorMax = health.anchorMax;
            rect.pivot = new Vector2(health.pivot.x, 1f);
            rect.sizeDelta = new Vector2(health.sizeDelta.x, StripHeight);
            var track = rect.GetComponent<Image>();
            track.color = TrackColor;
            track.raycastTarget = false;
            var fill = (RectTransform)rect.GetChild(0);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.sizeDelta = Vector2.zero;
            var fillImage = fill.GetComponent<Image>();
            fillImage.color = color;
            fillImage.raycastTarget = false;
            // Same fill art as the health bar beside it, if it has one.
            if (health.Find("health_fast/bar")?.GetComponent<Image>() is Image hp && hp.sprite)
            {
                fillImage.sprite = hp.sprite;
                fillImage.type = hp.type;
            }

            return new Strip { Rect = rect, Fill = fill };
        }
    }
}
