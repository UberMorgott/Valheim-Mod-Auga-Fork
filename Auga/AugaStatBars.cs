using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx.Configuration;
using HarmonyLib;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Auga
{
    // Auga's stat-bar look on the vanilla bars (spec 2026-09-11-auga-native-rework, Phase 3 follow-up). The vanilla
    // objects, fields and update code stay the source of truth: Hud.UpdateHealth/UpdateStamina/UpdateEitr (Hud.cs:1081-1120,
    // 1163-1189) size the roots and drive the GuiBars, whose fill is horizontal by construction (GuiBar.SetBar sizes
    // m_bar on RectTransform.Axis.Horizontal, assembly_guiutils GuiBar.cs:118-121); Hud.UpdateFood (Hud.cs:1012-1058)
    // drives the food icons and timers. This class only re-parents, re-anchors and re-skins those objects once, in
    // the Hud.Awake postfix, into the bundle HUD prefab's arrangement (hudroot/HealthBar, StaminaBar, EitrBar,
    // FoodPanel0-2; positions in hudroot pixels below).
    public static class AugaStatBars
    {
        private const int Health = 0, Stamina = 1, Eitr = 2;
        private static readonly string[] Names = { "Health", "Stamina", "Eitr" };
        private static readonly string[] AugaBars = { "HealthBar", "StaminaBar", "EitrBar" };

        // Bundle hudroot layout (lower-left anchored, pivot (0,0)): bars 17 px high at (208,123.5), (208,99.5),
        // (185,74.5); food panels 50x50 at (138,66), (167,95), (138,124). The cluster origin is FoodPanel0.
        private static readonly Vector2 Origin = new Vector2(138f, 66f);
        private static readonly Vector2[] BarLeftCenter = { new Vector2(70f, 66f), new Vector2(70f, 42f), new Vector2(47f, 17f) };
        private static readonly Vector2[] FoodCenter = { new Vector2(25f, 25f), new Vector2(54f, 54f), new Vector2(25f, 83f) };
        private const float BarHeight = 16f, FoodSize = 50f, FoodIconSize = 32f;

        // Vanilla bar length is 32 px per 25 units (Hud.cs:1084, 1108, 1177); Auga's is 500/270 px per unit
        // (AugaHealthBar.PixelsPerUnit), 1.447x as long.
        private const float VanillaPixelsPerUnit = 32f / 25f, AugaLengthScale = (500f / 270f) / VanillaPixelsPerUnit;
        private const float UnitsPerTick = 25f;
        private const int TickCount = 40; // up to 1000 units; ticks past the bar end are clipped by RectMask2D

        private static ConfigEntry<float>[] _lengthScale;
        private static ConfigEntry<int>[] _fixedLength;
        private static ConfigEntry<bool>[] _showTicks;
        private static readonly RectTransform[] Ticks = new RectTransform[3];

        public static void Setup(Hud hud)
        {
            Bind();
            var prefab = Auga.Assets.Hud;
            var panel = hud.m_healthPanel;
            // One lower-left group for the cluster; its size only bounds the bars for the HUD overlap audit.
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.zero;
            panel.anchoredPosition = Origin;
            panel.sizeDelta = new Vector2(640f, 110f);

            // Vanilla decorations with no Auga counterpart and no vanilla code that shows them again: the walnut and
            // fork icons (Hud.m_foodIcon is declared only, Hud.cs:135). The food-bar strip `Food` is inactive in the scene.
            foreach (var decoration in new[] { "healthicon", "foodicon", "foodicon (1)" })
                panel.Find(decoration)?.gameObject.SetActive(false);
            // health_flash toggles this full-panel darken (m_IsActive key); over the wider cluster it would be a big
            // black box, so its Image is off (m_Enabled is not keyed).
            if (panel.Find("darken")?.GetComponent<Image>() is Image darken)
                darken.enabled = false;

            // Health: vanilla draws the bar rotated 90 degrees into a column (scene healthpanel/Health rotz 90); Auga's
            // is the same GuiBar pair lying flat.
            var health = hud.m_healthBarRoot;
            health.localRotation = Quaternion.identity;
            Place(health, BarLeftCenter[Health]);
            Skin(prefab, Health, health, hud.m_healthBarSlow, hud.m_healthBarFast, hud.m_healthText);

            // Stamina/eitr: vanilla writes the root's anchoredPosition every frame, (0,130) in the default layout
            // (Hud.cs:1116, 1185). So each root gets an anchor object in the cluster placed 130 px below the bar;
            // vanilla's own write then lands it in Auga's slot. The Stamina child is inset 8 px (the 16 px border
            // buffer, Hud.cs:993/1007), so the anchor sits 8 px left of the bar's visible start.
            Slot(panel, prefab, Stamina, hud.m_staminaBar2Root, hud.m_staminaBar2Slow, hud.m_staminaBar2Fast, hud.m_staminaText);
            Slot(panel, prefab, Eitr, hud.m_eitrBarRoot, hud.m_eitrBarSlow, hud.m_eitrBarFast, hud.m_eitrText);
            Adrenaline(hud, panel);

            // Food: Auga diamond frames in a diagonal column, the vanilla timer to the left of each icon.
            var frame = AugaStyle.FromPrefab<Image>(prefab, "hudroot/FoodPanel0/IconBG");
            for (var i = 0; i < hud.m_foodIcons.Length; i++)
            {
                var icon = hud.m_foodIcons[i];
                var slot = (RectTransform)icon.transform.parent;
                slot.anchorMin = slot.anchorMax = Vector2.zero;
                slot.pivot = new Vector2(0.5f, 0.5f);
                slot.anchoredPosition = FoodCenter[i];
                slot.sizeDelta = new Vector2(FoodSize, FoodSize);
                if (slot.TryGetComponent<Image>(out var slotImage))
                    AugaStyle.CopyImage(slotImage, frame);
                var iconRt = icon.rectTransform;
                iconRt.anchorMin = iconRt.anchorMax = iconRt.pivot = new Vector2(0.5f, 0.5f);
                iconRt.anchoredPosition = Vector2.zero;
                iconRt.sizeDelta = new Vector2(FoodIconSize, FoodIconSize);
                var time = hud.m_foodTime[i];
                var timeRt = time.rectTransform;
                timeRt.anchorMin = timeRt.anchorMax = new Vector2(0f, 0.5f);
                timeRt.pivot = new Vector2(1f, 0.5f);
                timeRt.anchoredPosition = new Vector2(4f, 0f); // the diamond's left corner is empty art
                time.alignment = TextAlignmentOptions.MidlineRight;
            }
        }

        private static void Bind()
        {
            if (_lengthScale != null)
                return;
            var config = Auga.instance.Config;
            _lengthScale = new ConfigEntry<float>[3];
            _fixedLength = new ConfigEntry<int>[3];
            _showTicks = new ConfigEntry<bool>[3];
            for (var i = 0; i < 3; i++)
            {
                var bar = i;
                _lengthScale[i] = config.Bind("StatBars", $"{Names[i]}BarLengthScale", AugaLengthScale,
                    $"Length of the {Names[i].ToLower()} bar relative to vanilla (32 px per 25 points); the default is Auga's length. The bar grows with the max value.");
                _fixedLength[i] = config.Bind("StatBars", $"{Names[i]}BarFixedLength", 0,
                    $"If greater than 0, the {Names[i].ToLower()} bar is this many pixels long regardless of the max value (ticks are then hidden).");
                _showTicks[i] = config.Bind("StatBars", $"{Names[i]}BarShowTicks", true, "Show a faint line on the bar every 25 points.");
                _lengthScale[i].SettingChanged += (s, e) => ApplyTicks(bar);
                _fixedLength[i].SettingChanged += (s, e) => ApplyTicks(bar);
                _showTicks[i].SettingChanged += (s, e) => ApplyTicks(bar);
            }
        }

        // The length options feed the size argument of vanilla's Set*BarSize (Hud.cs:982-1010); vanilla still
        // applies it to the root and both GuiBars.
        public static float Length(int bar, float size)
        {
            var fixedLength = _fixedLength?[bar].Value ?? 0;
            if (fixedLength > 0 && size > 0f)
                return fixedLength;
            return size * (_lengthScale?[bar].Value ?? 1f);
        }

        private static void Place(RectTransform bar, Vector2 leftCenter)
        {
            bar.anchorMin = bar.anchorMax = Vector2.zero;
            bar.pivot = new Vector2(0f, 0.5f);
            bar.anchoredPosition = leftCenter;
        }

        private static void Slot(RectTransform panel, GameObject prefab, int bar, RectTransform root, GuiBar slow, GuiBar fast, TMP_Text text)
        {
            var anchor = new GameObject($"Auga{Names[bar]}Anchor", typeof(RectTransform)).GetComponent<RectTransform>();
            anchor.SetParent(panel, false);
            anchor.anchorMin = anchor.anchorMax = anchor.pivot = Vector2.zero;
            anchor.sizeDelta = Vector2.zero;
            anchor.anchoredPosition = BarLeftCenter[bar] - new Vector2(8f, 130f);
            root.SetParent(anchor, false);
            Place(root, root.anchoredPosition);
            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, BarHeight + 16f);
            // eitrpanel/Stamina sits 23 px above its root in the scene (vanilla stacks eitr over stamina).
            var inner = (RectTransform)slow.transform.parent;
            inner.anchoredPosition = Vector2.zero;
            Skin(prefab, bar, inner, slow, fast, text);
        }

        // Auga bar art (bundle hudroot/<Bar>/Background): black slanted background, fill clipped by a slanted mask,
        // ticks, border, and the value in Norsebold centred on the bar.
        private static void Skin(GameObject prefab, int bar, RectTransform rect, GuiBar slow, GuiBar fast, TMP_Text text)
        {
            var src = $"hudroot/{AugaBars[bar]}/Background";
            var background = AugaStyle.FromPrefab<Image>(prefab, src);
            var fillMask = AugaStyle.FromPrefab<Image>(prefab, src + "/FillMask");
            var fillSlow = AugaStyle.FromPrefab<Image>(prefab, src + "/FillMask/FillSlow");
            var fillFast = AugaStyle.FromPrefab<Image>(prefab, src + "/FillMask/FillFast");
            var border = AugaStyle.FromPrefab<Image>(prefab, src + "/Border");
            var tick = AugaStyle.FromPrefab<Image>(prefab, src + "/TickContainer/Tick");
            var label = AugaStyle.FromPrefab<Text>(prefab, src + "/HealthTextCenter");
            // Auga mirrors the stamina and eitr art vertically, so their ends slant the other way.
            var flip = bar == Health ? Vector3.one : new Vector3(1f, -1f, 1f);

            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, BarHeight);
            foreach (Transform child in rect)
            {
                if (!child.TryGetComponent<Image>(out var image))
                    continue;
                if (child.name == "bkg")
                {
                    AugaStyle.CopyImage(image, background);
                    child.localScale = flip;
                }
                // The vanilla border (inactive on stamina/eitr) belongs to the health animator: clip health_flash
                // keys its m_Color and m_Sprite (scene bundle d59cfac, controller healthpanel), so a restyle would be
                // written back to the grey box. The clips never key m_Enabled, so the Image is switched off and Auga's
                // border is a child of its own. Likewise the flash-keyed darken images (m_Color / m_IsActive keys).
                else if (child.name == "border" || child.name == "darken")
                    image.enabled = false;
            }

            var mask = NewChild(rect, "AugaFillMask");
            mask.gameObject.AddComponent<Image>();
            AugaStyle.CopyImage(mask.GetComponent<Image>(), fillMask);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            mask.localScale = flip;
            foreach (var (guiBar, fill) in new[] { (slow, fillSlow), (fast, fillFast) })
            {
                guiBar.transform.SetParent(mask, false);
                if (guiBar.m_bar.TryGetComponent<Image>(out var image))
                    AugaStyle.CopyImage(image, fill);
                guiBar.m_bar.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, BarHeight);
            }
            // No lag layer: the slow GuiBar keeps running for vanilla, but only the fast fill is drawn on the dark track.
            if (slow.m_bar.TryGetComponent<Image>(out var slowImage))
                slowImage.enabled = false;

            var ticks = NewChild(rect, "AugaTicks");
            ticks.gameObject.AddComponent<RectMask2D>();
            for (var i = 0; i < TickCount; i++)
            {
                var t = NewChild(ticks, "Tick");
                t.anchorMin = t.anchorMax = new Vector2(0f, 0.5f);
                t.sizeDelta = tick ? tick.rectTransform.sizeDelta : new Vector2(12f, 17f);
                var image = t.gameObject.AddComponent<Image>();
                AugaStyle.CopyImage(image, tick);
                image.raycastTarget = false;
                var c = image.color;
                c.a = i == 0 ? 0.6666f : 0.4f; // AugaHealthBar.FirstTickAlpha / OtherTickAlpha
                image.color = c;
            }
            Ticks[bar] = ticks;
            ApplyTicks(bar);

            var augaBorder = NewChild(rect, "AugaBorder");
            augaBorder.localScale = flip;
            var borderImage = augaBorder.gameObject.AddComponent<Image>();
            AugaStyle.CopyImage(borderImage, border);
            borderImage.raycastTarget = false;

            // The value sits on the bar, not on the fill: vanilla parents HealthText to the fast bar (scene
            // Health/fast/bar/HealthText, rotated back by -90).
            var textRt = text.rectTransform;
            textRt.SetParent(rect, false);
            textRt.localRotation = Quaternion.identity;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.pivot = new Vector2(0.5f, 0.5f);
            textRt.anchoredPosition = new Vector2(0f, 1f);
            textRt.sizeDelta = new Vector2(0f, 14f);
            textRt.SetAsLastSibling();
            AugaStyle.SetFont(text, Auga.Assets.NorseboldTMP);
            text.enableAutoSizing = false;
            text.fontSize = 20f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            if (label)
                text.color = label.color;
            text.outlineWidth = 0.2f; // bundle label has an Outline component
            text.outlineColor = new Color32(0, 0, 0, 255);

            if (bar == Health)
                Shield(rect, background);
        }

        // Shield (1.0.x SE_Shield, staff of protection): vanilla shows it only as a status-effect icon
        // (Hud.cs:1657-1692); there is no vanilla shield element to restyle or to take the hide path from. Auga
        // draws it as an outline behind the HP bar: two copies of the bar's own background silhouette (HealthBarBG,
        // same slanted ends at any length), a blue one Pad px out and a dark one Gap px out, so a blue rim with a
        // dark gap shows around the bar. Its length is the shield in HP units at the HP bar's own pixels per HP, so
        // it outruns the bar when the shield exceeds max health and shrinks as the shield is hit. Nothing covers
        // the HP fill; with no shield the object is inactive.
        private static RectTransform _shieldRim;
        private static readonly Color ShieldColor = new Color(0.35f, 0.62f, 1f, 1f);
        private static readonly Color ShieldGapColor = new Color(0f, 0f, 0f, 0.5f);
        private const float ShieldGap = 3f, ShieldPad = 6f;

        private static void Shield(RectTransform health, Image background)
        {
            _shieldRim = NewChild(health, "AugaShield");
            _shieldRim.anchorMin = new Vector2(0f, 0f);
            _shieldRim.anchorMax = new Vector2(0f, 1f);
            _shieldRim.pivot = new Vector2(0f, 0.5f);
            _shieldRim.anchoredPosition = new Vector2(-ShieldPad, 0f);
            _shieldRim.SetAsFirstSibling(); // behind the bar
            var rim = _shieldRim.gameObject.AddComponent<Image>();
            AugaStyle.CopyImage(rim, background);
            rim.color = ShieldColor;
            rim.raycastTarget = false;
            var gap = NewChild(_shieldRim, "Gap");
            gap.sizeDelta = -2f * (ShieldPad - ShieldGap) * Vector2.one;
            var gapImage = gap.gameObject.AddComponent<Image>();
            AugaStyle.CopyImage(gapImage, background);
            gapImage.color = ShieldGapColor;
            gapImage.raycastTarget = false;
            _shieldRim.gameObject.SetActive(false);
        }

        // Postfix of Hud.UpdateHealth: remaining absorb = SE_Shield m_totalAbsorbDamage - m_damage (SE_Shield.cs:23-57).
        public static void UpdateShield(Hud hud, Player player)
        {
            if (!_shieldRim)
                return;
            var shield = 0f;
            foreach (var se in player.GetSEMan().GetStatusEffects())
                if (se is SE_Shield s)
                    shield += Mathf.Max(0f, s.m_totalAbsorbDamage - s.m_damage);
            _shieldRim.gameObject.SetActive(shield > 0f);
            if (shield <= 0f)
                return;
            // HP bar pixels per HP = its width / max health (covers the length scale and a fixed length alike).
            var pixelsPerHp = hud.m_healthBarRoot.rect.width / Mathf.Max(1f, player.GetMaxHealth());
            _shieldRim.sizeDelta = new Vector2(shield * pixelsPerHp + 2f * ShieldPad, 2f * ShieldPad);
        }

        // Adrenaline (1.0.x trinkets): the vanilla adrenaline bar (Hud.cs:1122-1161) becomes a thin strip along the
        // bottom of the stamina bar. Vanilla keeps sizing it (its length prefix below matches it to the stamina fill),
        // placing it at (0,130), filling it and hiding it at zero (Hud.cs:1130-1133). A full bar gets an amber rim.
        private const float AdrenalineHeight = 4f;
        private static GameObject _adrenalineRim;
        private static RectTransform _staminaRoot;

        private static void Adrenaline(Hud hud, RectTransform panel)
        {
            _staminaRoot = hud.m_staminaBar2Root;
            var root = hud.m_adrenalineBarRoot;
            // Inside the stamina bar (its text's parent), so the strip shows and fades with the stamina bar and draws
            // over its fill, under its value.
            var staminaBar = hud.m_staminaText.transform.parent;
            var anchor = new GameObject("AugaAdrenalineAnchor", typeof(RectTransform)).GetComponent<RectTransform>();
            anchor.SetParent(staminaBar, false);
            anchor.anchorMin = anchor.anchorMax = anchor.pivot = Vector2.zero;
            anchor.sizeDelta = Vector2.zero;
            anchor.anchoredPosition = new Vector2(-8f, AdrenalineHeight / 2f + 1f - 130f);
            hud.m_staminaText.transform.SetAsLastSibling();
            root.SetParent(anchor, false);
            Place(root, root.anchoredPosition);
            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, AdrenalineHeight + 16f);
            var inner = (RectTransform)hud.m_adrenalineBarSlow.transform.parent;
            inner.anchoredPosition = Vector2.zero;
            foreach (Transform child in inner)
                if (child.TryGetComponent<Image>(out var image) && !child.GetComponent<GuiBar>())
                    image.enabled = false; // darken/border/bkg: the strip lies on the stamina bar's own background
            hud.m_adrenalineBarSlow.m_bar.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, AdrenalineHeight);
            hud.m_adrenalineBarFast.m_bar.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, AdrenalineHeight);
            if (hud.m_adrenalineBarSlow.m_bar.TryGetComponent<Image>(out var slow))
                slow.color = new Color(0.55f, 0.27f, 0.16f, 1f); // vanilla fast colour (1,0.49,0.29) darkened, like Auga's slow fills
            // Vanilla only writes the text (Hud.cs:1144); the strip is too thin to carry it.
            hud.m_adrenalineText.gameObject.SetActive(false);

            var staminaBorder = staminaBar.Find("AugaBorder");
            if (staminaBorder)
            {
                _adrenalineRim = Object.Instantiate(staminaBorder.gameObject, staminaBorder.parent, false);
                _adrenalineRim.name = "AugaAdrenalineFull";
                var rim = (RectTransform)_adrenalineRim.transform;
                rim.sizeDelta = new Vector2(8f, 8f);
                _adrenalineRim.GetComponent<Image>().color = new Color(1f, 0.6f, 0.2f, 0.9f);
                rim.SetAsFirstSibling();
                _adrenalineRim.SetActive(false);
            }
        }

        public static float AdrenalineLength(float size) => _staminaRoot ? _staminaRoot.rect.width - 16f : size;

        // Postfix of Hud.UpdateAdrenaline: vanilla flashes the bar once when it fills (Player.cs:4604); the rim stays
        // while it is full.
        public static void UpdateAdrenaline(Player player)
        {
            if (!_adrenalineRim)
                return;
            var max = player.GetMaxAdrenaline();
            _adrenalineRim.SetActive(max > 0f && player.GetAdrenaline() >= max);
        }

        private static RectTransform NewChild(Transform parent, string name)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            return rt;
        }

        // Tick every 25 points: a fixed pixel step while the length scales with the max value (vanilla 32 px per
        // 25 points times the length scale). With a fixed length the step would change with the max value, so
        // ticks are hidden then.
        private static void ApplyTicks(int bar)
        {
            var ticks = Ticks[bar];
            if (!ticks)
                return;
            var show = _showTicks[bar].Value && _fixedLength[bar].Value <= 0;
            ticks.gameObject.SetActive(show);
            var step = UnitsPerTick * VanillaPixelsPerUnit * _lengthScale[bar].Value;
            for (var i = 0; i < ticks.childCount; i++)
                ((RectTransform)ticks.GetChild(i)).anchoredPosition = new Vector2(step * (i + 1), 0f);
        }
    }

    [HarmonyPatch(typeof(Hud))]
    public static class AugaStatBars_Size_Patch
    {
        [HarmonyPatch(nameof(Hud.SetHealthBarSize)), HarmonyPrefix, UsedImplicitly]
        private static void Health(ref float size) => size = AugaStatBars.Length(0, size);

        [HarmonyPatch(nameof(Hud.SetStaminaBarSize)), HarmonyPrefix, UsedImplicitly]
        private static void Stamina(ref float size) => size = AugaStatBars.Length(1, size);

        [HarmonyPatch(nameof(Hud.SetEitrBarSize)), HarmonyPrefix, UsedImplicitly]
        private static void Eitr(ref float size) => size = AugaStatBars.Length(2, size);

        // Vanilla lifts the centre-bottom stamina and eitr bars clear of the build and ship HUDs (Hud.cs:1110-1112
        // (0,320), 1179-1181 (0,285)). Auga's bars sit in the lower-left cluster with nothing to clear, so both
        // branches use the default-layout height 130 (Hud.cs:1116, 1185).
        [HarmonyPatch(nameof(Hud.UpdateStamina)), HarmonyTranspiler, UsedImplicitly]
        private static IEnumerable<CodeInstruction> StaminaLift(IEnumerable<CodeInstruction> code) => Lift(code, 320f, "UpdateStamina");

        [HarmonyPatch(nameof(Hud.UpdateEitr)), HarmonyTranspiler, UsedImplicitly]
        private static IEnumerable<CodeInstruction> EitrLift(IEnumerable<CodeInstruction> code) => Lift(code, 285f, "UpdateEitr");

        // Same lift for the adrenaline strip, which rides on the stamina bar (Hud.cs:1150-1153).
        [HarmonyPatch(nameof(Hud.UpdateAdrenaline)), HarmonyTranspiler, UsedImplicitly]
        private static IEnumerable<CodeInstruction> AdrenalineLift(IEnumerable<CodeInstruction> code) => Lift(code, 320f, "UpdateAdrenaline");

        // The strip spans the stamina fill; vanilla sizes it from max adrenaline (Hud.cs:1147) and runs after
        // UpdateStamina in the same frame (Hud.cs:533-534), so the stamina width is current.
        [HarmonyPatch(nameof(Hud.SetAdrenalineBarSize)), HarmonyPrefix, UsedImplicitly]
        private static void AdrenalineSize(ref float size) => size = AugaStatBars.AdrenalineLength(size);

        [HarmonyPatch(nameof(Hud.UpdateHealth)), HarmonyPostfix, UsedImplicitly]
        private static void ShieldUpdate(Hud __instance, Player player) => AugaStatBars.UpdateShield(__instance, player);

        [HarmonyPatch(nameof(Hud.UpdateAdrenaline)), HarmonyPostfix, UsedImplicitly]
        private static void AdrenalineUpdate(Player player) => AugaStatBars.UpdateAdrenaline(player);

        private static IEnumerable<CodeInstruction> Lift(IEnumerable<CodeInstruction> code, float lifted, string method)
        {
            var hits = 0;
            foreach (var instruction in code)
            {
                if (instruction.opcode == OpCodes.Ldc_R4 && instruction.operand is float f && f == lifted)
                {
                    instruction.operand = 130f;
                    hits++;
                }
                yield return instruction;
            }
            if (hits != 1)
                Debug.LogError($"[Auga] {method} transpiler: expected 1 ldc.r4 {lifted}, got {hits}");
        }
    }
}
