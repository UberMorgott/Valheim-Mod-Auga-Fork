using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Auga
{
    // Compendium "Active effects" page (user 2026-10-19: the vanilla page was one text blob of names and tooltips,
    // TextsDialog.AddActiveEffects, TextsDialog.cs:230-253). Here it is a list: one row per active effect with its icon,
    // name, time left and its effect text with numbers. Sources, all live:
    // - foods (Player.GetFoods): the item icon and name, current health / stamina / eitr of the food (Food.m_health ...,
    //   which vanilla decays, Player.UpdateFood), regen per tick (SharedData.m_foodRegen, Player.cs:2449-2466), m_time left;
    // - status effects (SEMan.GetStatusEffects): an SE with a public `List<object[]> MtActiveEffectRowsV1()` (soft
    //   contract, MorgottTweaks ActiveEffectRows: {id, Sprite, name, text, seconds left (<0 none), active}) is drawn from
    //   its rows, hidden or not; any other SE as vanilla lists it (shown on the HUD: not m_hidden, has an icon,
    //   SEMan.GetHUDStatusEffects SEMan.cs:328-337): icon, Localize(m_name), Localize(GetTooltipString()) - vanilla
    //   SE_Stats prints its modifiers with numbers (SE_Stats.cs:483), mods override it - and the time left when m_ttl > 0;
    // - the selected guardian power (Player.GetGuardianPowerHUD) with its cooldown or "ready" (Hud.cs gp cooldown).
    // The page keeps vanilla's TextInfo (AddActiveEffects still fills its text): the TextInfo AddActiveEffects inserted is
    // remembered by reference (m_topic is already localized), and on ShowText of it the text area is swapped for the
    // list, after Epic Loot's own ShowText postfix (MagicPages.Reset turns the text area back on). Refreshed every
    // RefreshSeconds while shown; rows are reused by position, so the scroll position holds.
    public static class ActiveEffectsPage
    {
        private const string ProviderMethod = "MtActiveEffectRowsV1";
        private const float IconSize = 48f, RowGap = 10f, Pad = 12f, NameSize = 22f, BodySize = 18f;
        private const float RefreshSeconds = 1f;

        private static TextsDialog.TextInfo _info;
        private static ActiveEffectsList _list;

        internal static bool IsPage(TextsDialog.TextInfo text) => _info != null && ReferenceEquals(text, _info);

        [HarmonyPatch(typeof(TextsDialog), nameof(TextsDialog.AddActiveEffects))]
        public static class AddActiveEffects_Patch
        {
            [UsedImplicitly]
            public static void Postfix(TextsDialog __instance)
            {
                // AddActiveEffects returns early without a local player (TextsDialog.cs:232-235); it inserts at 0 (:252).
                _info = Player.m_localPlayer && __instance.m_texts.Count > 0 ? __instance.m_texts[0] : null;
            }
        }

        [HarmonyPatch(typeof(TextsDialog), nameof(TextsDialog.ShowText), typeof(TextsDialog.TextInfo))]
        public static class ShowText_Patch
        {
            [UsedImplicitly, HarmonyAfter("randyknapp.mods.epicloot")]
            public static void Postfix(TextsDialog __instance, TextsDialog.TextInfo text)
            {
                var list = List(__instance);
                if (list)
                    list.Show(IsPage(text));
            }
        }

        [HarmonyPatch(typeof(TextsDialog), nameof(TextsDialog.OnClose))]
        public static class OnClose_Patch
        {
            [UsedImplicitly]
            public static void Postfix(TextsDialog __instance)
            {
                if (_list)
                    _list.Show(false);
            }
        }

        private static ActiveEffectsList List(TextsDialog dialog)
        {
            if (_list && _list.Dialog == dialog)
                return _list;
            var area = dialog.m_textArea ? dialog.m_textArea.GetComponentInParent<ScrollRect>(true) : null;
            if (!area)
            {
                Debug.LogWarning("[Auga] active effects: no ScrollRect above TextsDialog.m_textArea");
                return null;
            }
            _list = ActiveEffectsList.Create(dialog, (RectTransform)area.transform);
            return _list;
        }

        // ---- rows

        internal struct Row
        {
            public Sprite Icon;
            public string Name, Text;
            public float Seconds;
            public bool Active;
        }

        private static readonly Dictionary<System.Type, MethodInfo> Providers = new Dictionary<System.Type, MethodInfo>();
        private static readonly List<StatusEffect> HudEffects = new List<StatusEffect>();

        internal static void Collect(Player player, List<Row> rows)
        {
            rows.Clear();
            var loc = Localization.instance;
            foreach (var food in player.GetFoods())
            {
                var item = food.m_item;
                if (item?.m_shared == null)
                    continue;
                var s = item.m_shared;
                var text = new List<string>(4);
                if (food.m_health > 0f)
                    text.Add($"$item_food_health: <color=#ff8080ff>{food.m_health:0}</color>");
                if (food.m_stamina > 0f)
                    text.Add($"$item_food_stamina: <color=#ffff80ff>{food.m_stamina:0}</color>");
                if (food.m_eitr > 0f)
                    text.Add($"$item_food_eitr: <color=#9090ffff>{food.m_eitr:0}</color>");
                if (s.m_foodRegen > 0f)
                    text.Add($"$item_food_regen: <color=orange>{s.m_foodRegen.ToString("0.#", CultureInfo.InvariantCulture)} hp/tick</color>");
                rows.Add(new Row
                {
                    Icon = item.GetIcon(),
                    Name = loc.Localize(s.m_name),
                    Text = loc.Localize(string.Join("   ", text)),
                    Seconds = food.m_time,
                    Active = true
                });
            }

            var seman = player.GetSEMan();
            HudEffects.Clear();
            seman.GetHUDStatusEffects(HudEffects);
            foreach (var se in seman.GetStatusEffects())
            {
                if (!se)
                    continue;
                var provider = Provider(se.GetType());
                if (provider != null)
                {
                    AddProvided(se, provider, rows);
                    continue;
                }
                if (!HudEffects.Contains(se))
                    continue;
                rows.Add(new Row
                {
                    Icon = se.m_icon,
                    Name = loc.Localize(se.m_name),
                    Text = Tidy(loc.Localize(se.GetTooltipString())),
                    Seconds = se.m_ttl > 0f ? se.GetRemaningTime() : -1f,
                    Active = true
                });
            }

            player.GetGuardianPowerHUD(out var gp, out var cooldown);
            if (gp)
            {
                var state = cooldown > 0f ? StatusEffect.GetTimeString(cooldown) : loc.Localize("$hud_ready");
                rows.Add(new Row
                {
                    Icon = gp.m_icon,
                    Name = loc.Localize("$inventory_selectedgp") + ": " + loc.Localize(gp.m_name),
                    Text = Tidy(loc.Localize(gp.GetTooltipString())) + $"\n<color={Auga.Colors.Topic}>{state}</color>",
                    Seconds = -1f,
                    Active = cooldown <= 0f
                });
            }
        }

        private static MethodInfo Provider(System.Type type)
        {
            if (Providers.TryGetValue(type, out var m))
                return m;
            m = type.GetMethod(ProviderMethod, BindingFlags.Public | BindingFlags.Instance, null, System.Type.EmptyTypes, null);
            if (m != null && !typeof(IEnumerable).IsAssignableFrom(m.ReturnType))
                m = null;
            Providers[type] = m;
            return m;
        }

        // Rows from a provider; on any failure the SE falls back to nothing new (its own default row is not used,
        // a provider decides what it shows), and the error is logged once per type.
        private static readonly HashSet<System.Type> Failed = new HashSet<System.Type>();

        private static void AddProvided(StatusEffect se, MethodInfo provider, List<Row> rows)
        {
            try
            {
                if (!(provider.Invoke(se, null) is IEnumerable list))
                    return;
                foreach (var o in list)
                {
                    if (!(o is object[] r) || r.Length < 6)
                        continue;
                    rows.Add(new Row
                    {
                        Icon = r[1] as Sprite,
                        Name = r[2] as string ?? "",
                        Text = Tidy(r[3] as string ?? ""),
                        Seconds = r[4] is float f ? f : -1f,
                        Active = !(r[5] is bool b) || b
                    });
                }
            }
            catch (System.Exception e)
            {
                if (Failed.Add(se.GetType()))
                    Debug.LogWarning($"[Auga] active effects: {se.GetType().Name}.{ProviderMethod} failed: {e.GetBaseException().Message}");
            }
        }

        // Tooltip texts carry blank lines (SE_Stats separates its parts with "\n\n", SE_Stats.cs:483); in a row they read as gaps.
        private static string Tidy(string text) =>
            string.IsNullOrEmpty(text) ? "" : System.Text.RegularExpressions.Regex.Replace(text.Trim(), @"\s*\n\s*\n\s*", "\n");

        internal static string Time(float seconds) => seconds >= 0f ? StatusEffect.GetTimeString(seconds) : "";

        internal static float IconPx => IconSize;
        internal static float Gap => RowGap;
        internal static float Padding => Pad;
        internal static float NameFont => NameSize;
        internal static float BodyFont => BodySize;
        internal static float Refresh => RefreshSeconds;
    }

    // The list view: a sibling of the text area's ScrollRect with the same rect, its own ScrollRect, viewport mask and a
    // copy of the dialog's right scrollbar (Auga-styled already). Texts are copies of m_textArea (font, material).
    public class ActiveEffectsList : MonoBehaviour
    {
        public TextsDialog Dialog;
        private GameObject _textScroll;
        private ScrollRect _scroll;
        private RectTransform _content;
        private TMP_Text _textTemplate;
        private readonly List<RowView> _views = new List<RowView>();
        private readonly List<ActiveEffectsPage.Row> _rows = new List<ActiveEffectsPage.Row>();
        private float _next;

        private sealed class RowView
        {
            public RectTransform Root;
            public Image Icon;
            public TMP_Text Name, Time, Text;
        }

        internal static ActiveEffectsList Create(TextsDialog dialog, RectTransform textScroll)
        {
            var go = new GameObject("AugaActiveEffects", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(textScroll.parent, false);
            rt.anchorMin = textScroll.anchorMin;
            rt.anchorMax = textScroll.anchorMax;
            rt.pivot = textScroll.pivot;
            rt.anchoredPosition = textScroll.anchoredPosition;
            rt.sizeDelta = textScroll.sizeDelta;
            rt.SetSiblingIndex(textScroll.GetSiblingIndex() + 1);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            viewport.SetParent(rt, false);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = new Vector2(-20f, 0f); // room for the scrollbar
            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;

            var list = go.AddComponent<ActiveEffectsList>();
            list.Dialog = dialog;
            list._textScroll = textScroll.gameObject;
            list._content = content;
            list._textTemplate = dialog.m_textArea;
            var scroll = go.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            scroll.viewport = viewport;
            scroll.content = content;
            // A raycast target over the whole view so the wheel scrolls anywhere on it.
            var hit = go.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            if (dialog.m_rightScrollbar)
            {
                var bar = Instantiate(dialog.m_rightScrollbar.gameObject, rt, false).GetComponent<Scrollbar>();
                bar.name = "Scrollbar";
                bar.onValueChanged = new Scrollbar.ScrollEvent();
                var barRt = (RectTransform)bar.transform;
                barRt.anchorMin = new Vector2(1f, 0f);
                barRt.anchorMax = Vector2.one;
                barRt.pivot = new Vector2(1f, 0.5f);
                barRt.anchoredPosition = Vector2.zero;
                barRt.sizeDelta = new Vector2(((RectTransform)dialog.m_rightScrollbar.transform).rect.width > 0f
                    ? ((RectTransform)dialog.m_rightScrollbar.transform).rect.width : 12f, 0f);
                bar.direction = Scrollbar.Direction.BottomToTop;
                scroll.verticalScrollbar = bar;
                scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            }
            list._scroll = scroll;
            go.SetActive(false);
            return list;
        }

        internal void Show(bool show)
        {
            if (show)
            {
                _textScroll.SetActive(false);
                gameObject.SetActive(true);
                _next = 0f;
                Refresh();
                _scroll.verticalNormalizedPosition = 1f;
            }
            else if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
                _textScroll.SetActive(true);
            }
        }

        [UsedImplicitly]
        private void Update()
        {
            if (ZInput.IsGamepadActive())
            {
                var y = ZInput.GetJoyRightStickY();
                if (Mathf.Abs(y) > 0.1f && _content.rect.height > 0f)
                    _scroll.verticalNormalizedPosition = Mathf.Clamp01(_scroll.verticalNormalizedPosition -
                        y * 1000f * Time.unscaledDeltaTime / Mathf.Max(1f, _content.rect.height));
            }
            if (Time.unscaledTime >= _next)
                Refresh();
        }

        private void Refresh()
        {
            _next = Time.unscaledTime + ActiveEffectsPage.Refresh;
            var player = Player.m_localPlayer;
            if (!player)
                return;
            ActiveEffectsPage.Collect(player, _rows);
            var width = ((RectTransform)_scroll.viewport).rect.width;
            var pad = ActiveEffectsPage.Padding;
            var icon = ActiveEffectsPage.IconPx;
            var textWidth = Mathf.Max(100f, width - pad * 3f - icon);
            var y = pad;
            for (var i = 0; i < _rows.Count; i++)
            {
                if (i >= _views.Count)
                    _views.Add(MakeRow(i));
                var v = _views[i];
                var r = _rows[i];
                v.Root.gameObject.SetActive(true);
                v.Icon.sprite = r.Icon;
                v.Icon.enabled = r.Icon;
                var alpha = r.Active ? 1f : 0.45f;
                v.Icon.color = new Color(1f, 1f, 1f, alpha);
                v.Name.text = r.Name;
                v.Name.alpha = alpha;
                v.Time.text = ActiveEffectsPage.Time(r.Seconds);
                v.Text.text = r.Text;
                v.Text.alpha = alpha;
                var nameHeight = ActiveEffectsPage.NameFont + 6f;
                var textHeight = string.IsNullOrEmpty(r.Text) ? 0f : v.Text.GetPreferredValues(r.Text, textWidth, 0f).y;
                ((RectTransform)v.Name.transform).sizeDelta = new Vector2(textWidth, nameHeight);
                ((RectTransform)v.Time.transform).sizeDelta = new Vector2(textWidth, nameHeight);
                var textRt = (RectTransform)v.Text.transform;
                textRt.anchoredPosition = new Vector2(pad * 2f + icon, -nameHeight);
                textRt.sizeDelta = new Vector2(textWidth, textHeight);
                var height = Mathf.Max(icon, nameHeight + textHeight);
                v.Root.anchoredPosition = new Vector2(0f, -y);
                v.Root.sizeDelta = new Vector2(0f, height);
                y += height + ActiveEffectsPage.Gap;
            }
            for (var i = _rows.Count; i < _views.Count; i++)
                _views[i].Root.gameObject.SetActive(false);
            _content.sizeDelta = new Vector2(0f, y + pad);
        }

        private RowView MakeRow(int index)
        {
            var root = new GameObject("Row" + index, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(_content, false);
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            var pad = ActiveEffectsPage.Padding;
            var iconPx = ActiveEffectsPage.IconPx;
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var iconRt = icon.rectTransform;
            iconRt.SetParent(root, false);
            iconRt.anchorMin = iconRt.anchorMax = iconRt.pivot = new Vector2(0f, 1f);
            iconRt.anchoredPosition = new Vector2(pad, 0f);
            iconRt.sizeDelta = new Vector2(iconPx, iconPx);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var name = Text(root, "Name", ActiveEffectsPage.NameFont, TextAlignmentOptions.TopLeft, pad * 2f + iconPx, 0f);
            name.color = ColorUtility.TryParseHtmlString(Auga.Colors.Topic, out var topic) ? topic : Color.white;
            var time = Text(root, "Time", ActiveEffectsPage.BodyFont, TextAlignmentOptions.TopRight, pad * 2f + iconPx, 0f);
            var text = Text(root, "Text", ActiveEffectsPage.BodyFont, TextAlignmentOptions.TopLeft, pad * 2f + iconPx, 0f);
            text.textWrappingMode = TextWrappingModes.Normal;
            return new RowView { Root = root, Icon = icon, Name = name, Time = time, Text = text };
        }

        private TMP_Text Text(RectTransform parent, string name, float size, TextAlignmentOptions align, float x, float y)
        {
            var go = Instantiate(_textTemplate.gameObject, parent, false);
            go.name = name;
            foreach (Transform child in go.transform)
                Destroy(child.gameObject);
            foreach (var c in go.GetComponents<Component>())
                if (c is ContentSizeFitter || c is LayoutElement || c is LayoutGroup)
                    Destroy(c);
            var t = go.GetComponent<TMP_Text>();
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            t.enableAutoSizing = false;
            t.fontSize = size;
            t.alignment = align;
            t.richText = true;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.text = "";
            return t;
        }
    }
}
