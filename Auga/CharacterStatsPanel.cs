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
    // Character stats window, opened by a 5th button in the inventory's Info panel (InventoryGui.m_infoPanel, beside
    // Texts/Skills/Trophies/Achievements). Both are clones of vanilla objects that are already restyled
    // (PlayerInventory_Setup): the button of the Achievements button, the window of the Skills dialog (root/Skills:
    // blur, darken, click-outside Closebutton, SkillsFrame with topic and Close button), whose skill list is replaced by
    // two text columns cloned from the skill row's name text. It opens like InventoryGui.OnOpenSkills
    // (InventoryGui.cs:2325-2333), closes on its Close buttons, Escape/B (InventoryGui.Update :520-542) and with the
    // inventory (InventoryGui.Hide :1055-1066).
    // Every value is read from the live game objects the way vanilla computes it (Player/Character/Humanoid/SEMan,
    // cited per row), so status effects and mods that patch those methods (Epic Loot included) are counted without
    // referencing them. Epic Loot's percentage resistances and night reduction are the one exception: it applies them to
    // the incoming hit (EpicLoot.MagicItemEffects.ModifyResistance / Shards.DamageReductionAtNight .ModifyIncoming,
    // called from its Character.RPC_Damage patch), not through GetDamageModifiers, so those public static methods are
    // looked up by name and run on the probe hit when Epic Loot is loaded (soft dependency, no assembly reference).
    // Likewise MorgottTweaks [Armor] wraps the resistance call of Character.RPC_Damage (MorgottTweaks.ArmorResistance
    // .ApplyResistance, which pools resistant types on its armor curve); when loaded, the probe runs that same step.
    public class CharacterStatsPanel : MonoBehaviour
    {
        private static ConfigEntry<bool> _enabled;
        private static ConfigEntry<float> _referenceHit;
        private static ConfigEntry<float> _refreshInterval;

        public const string ButtonName = "CharacterStats", WindowName = "CharacterStatsDialog";
        private const string TitleToken = "auga_characterstats", DamageReductionToken = "auga_damagereduction";
        private const float Padding = 16f, FontSize = 20f;
        private const float FoodRegenPeriod = 10f; // Player.UpdateFood heals once per 10 s (Player.cs:2449-2454)

        private static readonly Color LabelColor = new Color(0.82f, 0.79f, 0.76f); // AugaStyle.Light
        private const string Good = "#80FF80", Bad = "#FFD24D";
        // The Auga icon is white. The lithud material renders a colour about as c^2.2 (measured: (0.87, 0.67, 0.33) drew
        // as (0.76, 0.44, 0.07)); this input lands on the bright tone of the vanilla Info icons, about (0.83, 0.63, 0.31).
        private static readonly Color IconColor = new Color(0.92f, 0.81f, 0.59f);
        // Visible glyph height of the vanilla Info icons in their 64 px box (trophies, texts_button: about 60 px).
        private const float IconGlyphHeight = 60f;

        public static CharacterStatsPanel Instance { get; private set; }
        private static GameObject _button;

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

        private static MethodInfo _epicLootResistance, _epicLootNight, _armorResistance;
        private static bool _epicLootLookedUp;

        private TMP_Text _labels, _values;
        private float _nextRefresh;
        private readonly StringBuilder _labelText = new StringBuilder();
        private readonly StringBuilder _valueText = new StringBuilder();

        // InventoryGui.Awake postfix (PlayerInventory_Setup), after the restyle, so both clones carry the Auga look.
        public static void Create(InventoryGui gui)
        {
            Bind();
            var achievements = gui.m_infoPanel ? gui.m_infoPanel.Find("Achievements") as RectTransform : null;
            var trophies = gui.m_infoPanel ? gui.m_infoPanel.Find("Trophies") as RectTransform : null;
            var skills = gui.m_skillsDialog;
            var nameTemplate = skills && skills.m_elementPrefab ? Utils.FindChild(skills.m_elementPrefab.transform, "name") : null;
            if (!achievements || !trophies || !skills || !nameTemplate)
            {
                Debug.LogError($"[Auga] character stats: vanilla objects missing (Achievements={(bool)achievements} " +
                               $"Trophies={(bool)trophies} Skills={(bool)skills} name={(bool)nameTemplate})");
                return;
            }
            var russian = Localization.instance.GetSelectedLanguage() == "Russian";
            Localization.instance.AddWord(TitleToken, russian ? "Характеристики" : "Character stats");
            Localization.instance.AddWord(DamageReductionToken, russian ? "Снижение урона" : "Damage reduction");

            Instance = CreateWindow(gui, skills, nameTemplate);
            _button = CreateButton(achievements, trophies);
            _button.SetActive(_enabled.Value);
        }

        // Next free slot of the Info row: the vanilla buttons sit 100 px apart (Texts -200 ... Achievements 100).
        private static GameObject CreateButton(RectTransform achievements, RectTransform trophies)
        {
            var go = Instantiate(achievements.gameObject, achievements.parent, false);
            go.name = ButtonName;
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = achievements.anchoredPosition + (achievements.anchoredPosition - trophies.anchoredPosition);
            rt.SetSiblingIndex(achievements.GetSiblingIndex() + 1);

            // The prefab wires OnOpenAchievements as a persistent call; the clone gets its own event.
            var button = go.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => Instance?.Open());

            if (go.TryGetComponent<UITooltip>(out var tooltip))
            {
                tooltip.m_topic = "";
                tooltip.m_text = "$" + TitleToken;
            }
            var icon = go.transform.Find("Image") ? go.transform.Find("Image").GetComponent<Image>() : null;
            // Auga's own player-panel tab icon (bundle Inventory_screen right panel), white, lit like its siblings.
            var augaIcon = AugaStyle.FromPrefab<Image>(Auga.Assets.InventoryScreen,
                "root/RightPanel/DefaultContent/TabButtonContainer/Tabs/TabButton_PlayerPanel/Icon");
            if (icon && augaIcon)
            {
                icon.sprite = RuneSprite(augaIcon.sprite);
                icon.color = IconColor;
                icon.preserveAspect = true;
                var rect = icon.sprite.rect;
                icon.rectTransform.sizeDelta = new Vector2(IconGlyphHeight * rect.width / rect.height, IconGlyphHeight);
            }
            return go;
        }

        // The bundle's PlayerPanel sprite is the whole 80x80 texture with the rune in its middle (alpha bounds x 29-51,
        // y 20-59 from the bottom, read from the bundle with UnityPy), so at the siblings' size the rune came out half
        // their height. A sprite over just the rune lets the box be the glyph, like the vanilla Info icons.
        private static Sprite RuneSprite(Sprite full)
        {
            if (_rune)
                return _rune;
            var tex = full.texture;
            if (full.packed || tex.width != 80 || tex.height != 80)
                return full;
            _rune = Sprite.Create(tex, new Rect(29f, 20f, 22f, 40f), new Vector2(0.5f, 0.5f), full.pixelsPerUnit);
            _rune.name = "PlayerPanelRune";
            return _rune;
        }

        private static Sprite _rune;

        private static CharacterStatsPanel CreateWindow(InventoryGui gui, SkillsDialog skills, Transform nameTemplate)
        {
            var go = Instantiate(skills.gameObject, skills.transform.parent, false);
            go.SetActive(false);
            go.name = WindowName;
            go.transform.SetSiblingIndex(skills.transform.GetSiblingIndex() + 1);
            DestroyImmediate(go.GetComponent<SkillsDialog>());

            var frame = go.transform.Find("SkillsFrame");
            foreach (var path in new[] { "totalskills_topic", "totalskills", "Skills/SkillListScroll", "Skills/SkillList" })
            {
                var child = frame ? frame.Find(path) : null;
                if (child)
                    DestroyImmediate(child.gameObject);
            }

            var topic = frame ? frame.Find("topic") : null;
            if (topic && topic.TryGetComponent<TMP_Text>(out var title))
            {
                if (topic.TryGetComponent<Localize>(out var localize))
                    DestroyImmediate(localize);
                title.text = Localization.instance.Localize("$" + TitleToken);
            }

            var panel = go.AddComponent<CharacterStatsPanel>();
            foreach (var path in new[] { "Closebutton", "SkillsFrame/Closebutton" })
            {
                var close = go.transform.Find(path);
                if (close && close.TryGetComponent<Button>(out var button))
                {
                    button.onClick = new Button.ButtonClickedEvent();
                    button.onClick.AddListener(panel.Close);
                }
            }

            // The skill list's backdrop (SkillsFrame/Skills, TextBackdrop) holds the two columns.
            var area = frame ? frame.Find("Skills") as RectTransform : null;
            if (!area)
                area = (RectTransform)go.transform;
            area.name = "Stats";
            panel._labels = Column(area, nameTemplate, TextAlignmentOptions.TopLeft, LabelColor);
            panel._values = Column(area, nameTemplate, TextAlignmentOptions.TopRight, Color.white);
            return panel;
        }

        private static void Bind()
        {
            if (_enabled != null)
                return;
            var config = Auga.instance.Config;
            _enabled = config.Bind("CharacterStats", "Enabled", true,
                "Show the character stats button in the inventory's Info panel; it opens a window with health, stamina and eitr with regeneration, armor, damage taken per type, speed, weight, block and rest.");
            _referenceHit = config.Bind("CharacterStats", "ReferenceHit", 100f,
                "Damage reduction rows: the share of a hit of this size, per damage type, that armor, resistances and Epic Loot remove (the game's own damage steps run on it).");
            _refreshInterval = config.Bind("CharacterStats", "RefreshSeconds", 0.25f, "Seconds between window updates while it is open.");
            _enabled.SettingChanged += (s, e) =>
            {
                if (_button)
                    _button.SetActive(_enabled.Value);
                if (!_enabled.Value && Instance)
                    Instance.Close();
            };
        }

        // A copy of the vanilla skill row's name text (already on Auga's body font), so the TMP component wakes up with
        // a font assigned, as every vanilla list text does (SkillsDialog.cs:119 instantiates its row prefab).
        private static TMP_Text Column(RectTransform parent, Transform template, TextAlignmentOptions alignment, Color color)
        {
            var go = Instantiate(template.gameObject, parent, false);
            go.name = alignment == TextAlignmentOptions.TopLeft ? "Labels" : "Values";
            go.SetActive(true);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(Padding, Padding);
            rt.offsetMax = new Vector2(-Padding, -Padding);
            var text = go.GetComponent<TMP_Text>();
            text.enableAutoSizing = false;
            text.fontSize = FontSize;
            text.alignment = alignment;
            text.color = color;
            text.richText = true;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.text = "";
            return text;
        }

        public bool IsOpen => gameObject.activeSelf;

        // InventoryGui.OnOpenSkills (InventoryGui.cs:2325-2333): focus the Info group, show the dialog.
        public void Open()
        {
            if (!Player.m_localPlayer || !InventoryGui.instance)
                return;
            InventoryGui.instance.SetActiveGroup(InventoryGui.instance.m_uiGroups[2]);
            gameObject.SetActive(true);
            _nextRefresh = 0f;
            Refresh();
        }

        public void Close() => gameObject.SetActive(false);

        [UsedImplicitly]
        private void Update()
        {
            if (Time.unscaledTime >= _nextRefresh)
                Refresh();
        }

        private void Refresh()
        {
            _nextRefresh = Time.unscaledTime + Mathf.Max(0.05f, _refreshInterval.Value);
            var player = Player.m_localPlayer;
            if (!player || player.IsDead())
                return;
            _labelText.Clear();
            _valueText.Clear();
            Fill(player);
            _labels.text = _labelText.ToString();
            _values.text = _valueText.ToString();
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

            // Damage reduction per type: the share of a reference hit of that type the damage pipeline removes right
            // now (armor, resistances, Epic Loot), see Reduction. Physical left, elemental right, poison and spirit last.
            Row($"${DamageReductionToken}", "");
            for (var i = 0; i < 4; i++)
            {
                var left = ReductionLayout[i * 2];
                var right = ReductionLayout[i * 2 + 1];
                _labelText.Append('\n').Append(TypeName(left)).Append("<pos=40%>").Append(ReductionValue(player, left))
                    .Append("<pos=55%>").Append(TypeName(right));
                _valueText.Append('\n').Append(ReductionValue(player, right));
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

        // A line holds two types: left name, its percent at a fixed column inside the label text (TMP <pos>), right
        // name, and the right percent in the value column like every other row.
        private static string TypeName(HitData.DamageType type) =>
            Localization.instance.Localize(Array.Find(DamageTypes, t => t.Type == type).Token);

        private static string ReductionValue(Player player, HitData.DamageType type)
        {
            var reduction = Reduction(player, type) * 100f;
            var value = $"{reduction:0}%";
            if (reduction >= 0.5f)
                return Colored(value, Good);
            return reduction <= -0.5f ? Colored(value, Bad) : $"<color=#FFFFFF>{value}</color>";
        }
        // Pairs per line: physical | elemental, then poison | spirit.
        private static readonly HitData.DamageType[] ReductionLayout =
        {
            HitData.DamageType.Slash, HitData.DamageType.Fire,
            HitData.DamageType.Blunt, HitData.DamageType.Frost,
            HitData.DamageType.Pierce, HitData.DamageType.Lightning,
            HitData.DamageType.Poison, HitData.DamageType.Spirit,
        };

        // Share of a ReferenceHit of one damage type the player would not take right now, 0..1 (negative when weak),
        // by the steps Character.RPC_Damage runs, in its order: Epic Loot's prefix (SharedCharacterRpcDamagePatch:
        // ModifyResistance.ModifyIncoming, then DamageReductionAtNight.ModifyIncoming, looked up by name when Epic Loot
        // is loaded; mods that patch those, e.g. MorgottTweaks [Armor], are included), then the resistances from
        // Character.GetDamageModifiers (body + armor + status effects, Character.cs:2378-2379, 2409-2415), then armor
        // (Player.GetBodyArmor into HitData.ApplyArmor, Character.cs:2380-2383, i.e. HitData.DamageTypes.ApplyArmor).
        public static float Reduction(Player player, HitData.DamageType type)
        {
            var probe = Mathf.Max(1f, _referenceHit?.Value ?? 100f);
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
            LookUpEpicLoot();
            var args = new object[] { player, hit };
            _epicLootResistance?.Invoke(null, args);
            _epicLootNight?.Invoke(null, args);
            var modifiers = player.GetDamageModifiers();
            if (_armorResistance != null)
                _armorResistance.Invoke(null, new object[] { hit, modifiers, HitData.DamageModifier.Normal, player });
            else
                hit.ApplyResistance(modifiers, out _);
            hit.ApplyArmor(player.GetBodyArmor());
            return 1f - hit.m_damage.GetTotalDamage() / probe;
        }

        private static void LookUpEpicLoot()
        {
            if (_epicLootLookedUp)
                return;
            _epicLootLookedUp = true;
            var args = new[] { typeof(Character), typeof(HitData) };
            var resistance = AccessTools.TypeByName("EpicLoot.MagicItemEffects.ModifyResistance");
            _epicLootResistance = resistance == null ? null : AccessTools.Method(resistance, "ModifyIncoming", args);
            var night = AccessTools.TypeByName("EpicLoot.MagicItemEffects.Shards.DamageReductionAtNight");
            _epicLootNight = night == null ? null : AccessTools.Method(night, "ModifyIncoming", args);
            // MorgottTweaks.ArmorResistance.ApplyResistance(HitData, DamageModifiers, out DamageModifier, Character).
            _armorResistance = AccessTools.Method("MorgottTweaks.ArmorResistance:ApplyResistance", new[]
            {
                typeof(HitData), typeof(HitData.DamageModifiers), typeof(HitData.DamageModifier).MakeByRefType(), typeof(Character),
            });
        }
    }
    [HarmonyPatch]
    public static class CharacterStatsPanel_InventoryGui_Patches
    {
        // Escape/B closes the window, not the inventory, like the vanilla dialogs in InventoryGui.Update
        // (InventoryGui.cs:513-542, same guards). The frame's Update is skipped so its "else hide the inventory"
        // branch (:551-560) does not see the same key press; the next frame runs as usual.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Update))]
        [UsedImplicitly]
        private static bool UpdatePrefix(InventoryGui __instance)
        {
            var panel = CharacterStatsPanel.Instance;
            if (!panel || !panel.IsOpen)
                return true;
            var player = Player.m_localPlayer;
            if (!player || player.IsDead() || player.InCutscene() || player.IsTeleporting())
                return true;
            if (__instance.m_craftTimer >= 0f || (Chat.instance && Chat.instance.HasFocus()) || Console.IsVisible() ||
                Menu.IsVisible() || !TextViewer.instance || TextViewer.instance.IsVisible() || GameCamera.InFreeFly() ||
                Minimap.IsOpen())
                return true;
            if (!ZInput.GetButtonDown("JoyButtonB") && !ZInput.GetKeyDown(KeyCode.Escape))
                return true;
            panel.Close();
            return false;
        }

        // InventoryGui.Hide closes every Info dialog with the inventory (InventoryGui.cs:1055-1066).
        [HarmonyPostfix]
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
        [UsedImplicitly]
        private static void HidePostfix()
        {
            if (CharacterStatsPanel.Instance)
                CharacterStatsPanel.Instance.Close();
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
