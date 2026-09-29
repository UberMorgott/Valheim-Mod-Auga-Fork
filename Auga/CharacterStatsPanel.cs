using System;
using System.Reflection;
using System.Text;
using BepInEx.Configuration;
using HarmonyLib;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Auga
{
    // Compact character stats under the small minimap (top right), in Auga's HUD look: the bundle minimap backdrop
    // (TextBackdrop, black 50%) and Auga's body font. Every value is read from the live game objects the way vanilla
    // computes it (Player/Character/Humanoid/SEMan, cited per row), so status effects and mods that patch those
    // methods (Epic Loot included) are counted without referencing them. Epic Loot's percentage resistances are the
    // one exception: it applies them to the incoming hit (EpicLoot.MagicItemEffects.ModifyResistance.ModifyIncoming,
    // called from its Character.RPC_Damage patch), not through GetDamageModifiers, so that public static method is
    // looked up by name and run on a probe hit when Epic Loot is loaded (soft dependency, no assembly reference).
    public class CharacterStatsPanel : MonoBehaviour
    {
        private static ConfigEntry<bool> _enabled;
        private static ConfigEntry<float> _referenceHit;
        private static ConfigEntry<float> _refreshInterval;

        private const float MinWidth = 260f, Padding = 8f, Gap = 8f, FontSize = 14f;
        private const float FoodRegenPeriod = 10f; // Player.UpdateFood heals once per 10 s (Player.cs:2449-2454)

        private static readonly Color LabelColor = new Color(0.82f, 0.79f, 0.76f); // AugaStyle.Light
        private const string Good = "#80FF80", Bad = "#FFD24D";

        private static readonly (HitData.DamageType Type, string Token)[] DamageTypes =
        {
            (HitData.DamageType.Blunt, "$inventory_blunt"),
            (HitData.DamageType.Slash, "$inventory_slash"),
            (HitData.DamageType.Pierce, "$inventory_pierce"),
            (HitData.DamageType.Fire, "$inventory_fire"),
            (HitData.DamageType.Frost, "$inventory_frost"),
            (HitData.DamageType.Lightning, "$inventory_lightning"),
            (HitData.DamageType.Poison, "$inventory_poison"),
            (HitData.DamageType.Spirit, "$inventory_spirit"),
        };

        private static MethodInfo _epicLootResistance;
        private static bool _epicLootLookedUp;

        private Image _background;
        private TMP_Text _labels, _values;
        private float _nextRefresh;
        private readonly StringBuilder _labelText = new StringBuilder();
        private readonly StringBuilder _valueText = new StringBuilder();

        // Minimap.Start postfix (Minimap_Setup): the small map's rect is final there and its Movable offset applied.
        public static void Create(Minimap minimap)
        {
            Bind();
            var small = (RectTransform)minimap.m_smallRoot.transform;
            var parent = (RectTransform)minimap.transform;

            var go = new GameObject("AugaCharacterStats", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            // First child of the minimap root: the small and large maps draw over it, so the open large map hides it.
            rt.SetAsFirstSibling();
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.one;
            // Below the small map's bottom-right corner, right edges aligned.
            var corners = new Vector3[4];
            small.GetWorldCorners(corners);
            Vector2 bottomRight = parent.InverseTransformPoint(corners[3]);
            Vector2 bottomLeft = parent.InverseTransformPoint(corners[0]);
            var area = parent.rect;
            rt.anchoredPosition = new Vector2(bottomRight.x - area.xMax, bottomRight.y - area.yMax - Gap);
            rt.sizeDelta = new Vector2(Mathf.Max(MinWidth, bottomRight.x - bottomLeft.x), 0f);

            var panel = go.AddComponent<CharacterStatsPanel>();
            panel._background = go.AddComponent<Image>();
            AugaStyle.Backdrop(panel._background);
            panel._background.raycastTarget = false;
            // The small map's biome label is already on Auga's body font with the vanilla font as glyph fallback.
            panel._labels = Column(rt, minimap.m_biomeNameSmall, TextAlignmentOptions.TopLeft, LabelColor);
            panel._values = Column(rt, minimap.m_biomeNameSmall, TextAlignmentOptions.TopRight, Color.white);
            panel.Show(false);

            Hud_Setup.Movable(rt, "CharacterStats");
            go.SetActive(_enabled.Value);
        }

        private static void Bind()
        {
            if (_enabled != null)
                return;
            var config = Auga.instance.Config;
            _enabled = config.Bind("CharacterStats", "Enabled", true,
                "Show the character stats panel under the minimap (health, stamina and eitr with regeneration, armor, damage taken per type, speed, weight, block, rest).");
            _referenceHit = config.Bind("CharacterStats", "ReferenceHit", 100f,
                "Armor row: damage of the example hit shown as \"hit -> damage taken\" after armor (HitData.DamageTypes.ApplyArmor).");
            _refreshInterval = config.Bind("CharacterStats", "RefreshSeconds", 0.25f, "Seconds between panel updates.");
            _enabled.SettingChanged += (s, e) =>
            {
                var panel = Minimap.instance ? Minimap.instance.GetComponentInChildren<CharacterStatsPanel>(true) : null;
                if (panel)
                    panel.gameObject.SetActive(_enabled.Value);
            };
        }

        private static TMP_Text Column(RectTransform parent, TMP_Text fontSource, TextAlignmentOptions alignment, Color color)
        {
            var go = new GameObject(alignment == TextAlignmentOptions.TopLeft ? "Labels" : "Values", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(Padding, Padding);
            rt.offsetMax = new Vector2(-Padding, -Padding);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = fontSource.font;
            text.fontSharedMaterial = fontSource.fontSharedMaterial;
            text.fontSize = FontSize;
            text.alignment = alignment;
            text.color = color;
            text.richText = true;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private void Show(bool show)
        {
            _background.enabled = show;
            _labels.enabled = show;
            _values.enabled = show;
        }

        [UsedImplicitly]
        private void Update()
        {
            // The large map does not cover the whole screen (hudroot/MiniMap/large is inset), so the panel would peek
            // out beside it; it goes with the small map, which Minimap.SetMapMode hides in Large mode (Minimap.cs:1233-1236).
            if (Minimap.instance && Minimap.instance.m_mode == Minimap.MapMode.Large)
            {
                Show(false);
                _nextRefresh = 0f;
                return;
            }
            if (Time.unscaledTime < _nextRefresh)
                return;
            _nextRefresh = Time.unscaledTime + Mathf.Max(0.05f, _refreshInterval.Value);

            var player = Player.m_localPlayer;
            if (!player || player.IsDead())
            {
                Show(false);
                return;
            }
            _labelText.Clear();
            _valueText.Clear();
            Fill(player);
            _labels.text = _labelText.ToString();
            _values.text = _valueText.ToString();
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, _labels.preferredHeight + 2f * Padding);
            Show(true);
        }

        private void Row(string token, string value)
        {
            if (_labelText.Length > 0)
            {
                _labelText.Append('\n');
                _valueText.Append('\n');
            }
            _labelText.Append(Localization.instance.Localize(token));
            _valueText.Append(value);
        }

        private static string Colored(string text, string color) => $"<color={color}>{text}</color>";

        private static string Rate(float perSecond) => perSecond > 0f ? Colored($"+{perSecond:0.#}/s", Good) : "0/s";

        private void Fill(Player player)
        {
            // Health: Character.GetHealth/GetMaxHealth (Character.cs:3003, 3070); regeneration = the last food tick.
            Row("$se_health", $"{Mathf.CeilToInt(player.GetHealth())} / {Mathf.CeilToInt(player.GetMaxHealth())}");
            Row("$se_healthregen", FoodTick.LastHeal < 0f ? "-" : Rate(FoodTick.LastHeal / FoodRegenPeriod));

            var maxStamina = player.GetMaxStamina();
            Row("$se_stamina", $"{Mathf.CeilToInt(player.GetStamina())} / {Mathf.CeilToInt(maxStamina)}");
            Row("$se_staminaregen", Rate(StaminaRegen(player, maxStamina)));

            var maxEitr = player.GetMaxEitr();
            if (maxEitr > 0f)
            {
                Row("$se_eitr", $"{Mathf.CeilToInt(player.GetEitr())} / {Mathf.CeilToInt(maxEitr)}");
                Row("$se_eitrregen", Rate(EitrRegen(player, maxEitr)));
            }

            // Armor: Player.GetBodyArmor (Player.cs:6832, gear GetArmor + SEMan.ApplyArmorMods) applied to a player's hit
            // by HitData.DamageTypes.ApplyArmor (Character.cs:2382-2383, HitData.cs:414-422).
            var armor = player.GetBodyArmor();
            var hit = Mathf.Max(1f, _referenceHit.Value);
            Row("$item_armor", $"{armor:0}   {hit:0} → {HitData.DamageTypes.ApplyArmor(hit, armor):0.#}");

            // Damage taken per type, in percent: resistances from Character.GetDamageModifiers (body + armor + status
            // effects, Character.cs:2409-2415) applied like Character.RPC_Damage does (Character.cs:2378-2379).
            var mods = player.GetDamageModifiers();
            foreach (var (type, token) in DamageTypes)
            {
                var taken = DamageTaken(player, mods, type);
                if (Mathf.Abs(taken - 100f) < 0.5f)
                    continue;
                Row(token, Colored($"{taken:0}%", taken < 100f ? Good : Bad));
            }

            // Movement: jog speed factor (Player.GetJogSpeedFactor = 1 + equipment modifier, Player.cs:7109-7112) and
            // status-effect speed mods, as Character.UpdateWalking combines them (Character.cs:1624, 1682).
            var speed = player.m_speed * player.GetJogSpeedFactor();
            player.GetSEMan().ApplyStatusEffectSpeedMods(ref speed, player.m_currentVel);
            var speedChange = player.m_speed > 0f ? (speed / player.m_speed - 1f) * 100f : 0f;
            if (Mathf.Abs(speedChange) >= 0.5f)
                Row("$item_movement_modifier", Colored($"{speedChange:+0;-0}%", speedChange > 0f ? Good : Bad));

            // Weight: Inventory.GetTotalWeight vs Player.GetMaxCarryWeight (Player.cs:5032-5037).
            var weight = player.GetInventory().GetTotalWeight();
            var maxWeight = player.GetMaxCarryWeight();
            Row("$item_weight", weight > maxWeight ? Colored($"{weight:0} / {maxWeight:0}", Bad) : $"{weight:0} / {maxWeight:0}");

            // Block: the item Humanoid.BlockAttack blocks with and its power at the current Blocking skill; a parry
            // multiplies it by m_timedBlockBonus plus status-effect bonuses (Humanoid.cs BlockAttack, :1760-1769).
            var blocker = player.GetCurrentBlocker();
            if (blocker != null)
            {
                var block = blocker.GetBlockPower(player.GetSkillFactor(Skills.SkillType.Blocking));
                Row("$item_blockarmor", $"{block:0}");
                if (blocker.m_shared.m_timedBlockBonus > 1f)
                {
                    var parry = block * blocker.m_shared.m_timedBlockBonus;
                    player.GetSEMan().ModifyTimedBlockBonus(ref parry);
                    Row("$item_parrybonus", $"{parry:0}");
                }
            }

            // Rested: comfort level found when resting started (Player.cs:1985) and the time left.
            var rested = player.GetSEMan().GetStatusEffect(SEMan.s_statusEffectRested);
            if (rested)
            {
                var left = TimeSpan.FromSeconds(Mathf.Max(0f, rested.GetRemaningTime()));
                Row("$se_rested_comfort", $"{player.GetComfortLevel()}   {(int)left.TotalMinutes}:{left.Seconds:00}");
            }
        }

        // Player.UpdateStats (Player.cs:2099-2117) at the current fill: blocking 0.8x, no regeneration while attacking,
        // dodging, wall-running, swimming off ground, encumbered or inside the post-use delay.
        private static float StaminaRegen(Player player, float max)
        {
            if (max <= 0f || player.m_staminaRegenTimer > 0f)
                return 0f;
            var factor = player.IsBlocking() ? 0.8f : 1f;
            if ((player.IsSwimming() && !player.IsOnGround()) || player.InAttack() || player.InDodge() || player.m_wallRunning || player.IsEncumbered())
                factor = 0f;
            var rate = (player.m_staminaRegen + (1f - player.GetStamina() / max) * player.m_staminaRegen * player.m_staminaRegenTimeMultiplier) * factor;
            var multiplier = 1f;
            player.GetSEMan().ModifyStaminaRegen(ref multiplier);
            return rate * multiplier * Game.m_staminaRegenRate;
        }

        // Player.UpdateStats (Player.cs:2133-2152).
        private static float EitrRegen(Player player, float max)
        {
            if (player.m_eitrRegenTimer > 0f)
                return 0f;
            var factor = player.IsBlocking() ? 0.8f : 1f;
            if (player.InAttack() || player.InDodge())
                factor = 0f;
            var rate = (player.m_eiterRegen + (1f - player.GetEitr() / max) * player.m_eiterRegen) * factor;
            var multiplier = 1f;
            player.GetSEMan().ModifyEitrRegen(ref multiplier);
            multiplier += player.GetEquipmentEitrRegenModifier();
            return rate * multiplier;
        }

        private static float DamageTaken(Player player, HitData.DamageModifiers mods, HitData.DamageType type)
        {
            const float probe = 100f;
            var hit = new HitData();
            switch (type)
            {
                case HitData.DamageType.Blunt: hit.m_damage.m_blunt = probe; break;
                case HitData.DamageType.Slash: hit.m_damage.m_slash = probe; break;
                case HitData.DamageType.Pierce: hit.m_damage.m_pierce = probe; break;
                case HitData.DamageType.Fire: hit.m_damage.m_fire = probe; break;
                case HitData.DamageType.Frost: hit.m_damage.m_frost = probe; break;
                case HitData.DamageType.Lightning: hit.m_damage.m_lightning = probe; break;
                case HitData.DamageType.Poison: hit.m_damage.m_poison = probe; break;
                case HitData.DamageType.Spirit: hit.m_damage.m_spirit = probe; break;
            }
            hit.ApplyResistance(mods, out _);
            EpicLootResistance()?.Invoke(null, new object[] { player, hit });
            return hit.m_damage.GetTotalDamage() / probe * 100f;
        }

        private static MethodInfo EpicLootResistance()
        {
            if (_epicLootLookedUp)
                return _epicLootResistance;
            _epicLootLookedUp = true;
            var type = AccessTools.TypeByName("EpicLoot.MagicItemEffects.ModifyResistance");
            _epicLootResistance = type == null ? null : AccessTools.Method(type, "ModifyIncoming", new[] { typeof(Character), typeof(HitData) });
            return _epicLootResistance;
        }
    }

    // Health regeneration as the game actually pays it: the amount Player.UpdateFood heals on its 10 s tick
    // (Player.cs:2449-2466: food m_foodRegen sum x SEMan.ModifyHealthRegen, plus whatever other mods inject there,
    // e.g. Epic Loot's flat AddHealthRegen). Read in a Heal prefix that runs first, before SmoothRegen splits it.
    [HarmonyPatch]
    public static class FoodTick
    {
        // Heal of the last food tick; -1 until the first tick after spawning.
        public static float LastHeal = -1f;
        private static bool _inTick;
        private static float _healed;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.UpdateFood))]
        [UsedImplicitly]
        private static void UpdateFoodPrefix(Player __instance, bool forceUpdate, out float __state)
        {
            __state = __instance.m_foodRegenTimer;
            _inTick = !forceUpdate && __instance == Player.m_localPlayer;
            _healed = 0f;
        }

        // The tick happened when the regen timer was reset (Player.cs:2454); a tick with no food regen heals nothing.
        [HarmonyFinalizer]
        [HarmonyPatch(typeof(Player), nameof(Player.UpdateFood))]
        [UsedImplicitly]
        private static void UpdateFoodFinalizer(Player __instance, float __state)
        {
            if (_inTick && __instance.m_foodRegenTimer < __state)
                LastHeal = _healed;
            _inTick = false;
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(Character), nameof(Character.Heal))]
        [UsedImplicitly]
        private static void HealPrefix(Character __instance, float hp)
        {
            if (_inTick && __instance == Player.m_localPlayer)
                _healed += hp;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        [UsedImplicitly]
        private static void OnSpawnedPostfix(Player __instance)
        {
            if (__instance == Player.m_localPlayer)
                LastHeal = -1f;
        }
    }
}
