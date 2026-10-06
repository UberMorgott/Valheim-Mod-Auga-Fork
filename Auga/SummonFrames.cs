using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Auga
{
    // Summon frames (Phase 3, BalanceSim TASKS.md): one small frame per living summon of the local player, Diablo-2
    // style, in a row under the hotkey bar: creature icon, health bar, level stars. Vanilla has no such element.
    //
    // Whose summon: MorgottTweaks marks every summon ZDO with long mt_sum_owner = summoner Player.GetPlayerID(). Without
    // the key (MorgottTweaks absent or an older summon) a vanilla staff summon counts: a tamed creature whose Tameable
    // unsummons (m_unsummonDistance / m_unsummonOnOwnerLogoutSeconds, Tameable.cs:30-32, set on Skeleton_Friendly) and
    // that follows the local player by name (ZDO "follow", written by Tameable.RPC_Command on the vanilla summon path
    // SpawnAbility m_commandOnSpawn -> Tameable.Command, SpawnAbility.cs:255-260, Tameable.cs:483-486).
    //
    // Art: the frame is the hotkey slot of the Auga bundle (HotKeyElement: Container_Square_A background,
    // Container_Square_A_Outline border), the bar is the bundle's AugaProgressBarBody_Small in the Auga enemy HUD's
    // friendly green, the stars are the Auga enemy HUD star.
    public class SummonFrames : MonoBehaviour
    {
        public const string RootName = "AugaSummonFrames";
        public const int MaxFrames = 8;
        private const float Refresh = 0.25f;
        private const float FrameSize = 44f, Spacing = 50f, BarHeight = 6f;
        // Under the hotkey slots: HotkeyBar puts each 64 px slot with its top-left pivot at the bar's origin
        // (HotkeyBar.cs:110, bundle HotKeyElement 64x64 pivot (0,1)); the durability bar hangs below the slot.
        private static readonly Vector2 Origin = new Vector2(0f, -86f);
        private static readonly Color BackgroundColor = new Color(0.0941f, 0.0784f, 0.0627f, 0.749f); // HotKeyElement/bkg
        private static readonly Color OutlineColor = new Color(0.5f, 0.45f, 0.4f, 0.8f);
        private static readonly Color HealthColor = new Color(0.1059f, 0.6078f, 0.2157f, 1f); // Auga HudBasePlayer health_fast
        private static readonly Color TrackColor = new Color(0f, 0f, 0f, 0.7f);
        private static readonly int OwnerKey = "mt_sum_owner".GetStableHashCode();
        private const int MaxStars = 3;

        private sealed class Frame
        {
            public RectTransform Root, Fill;
            public Image Icon;
            public TMP_Text Letter, Level;
            public GameObject[] Stars;
            public Character Character;
        }

        private readonly List<Frame> _frames = new List<Frame>();
        private readonly List<Character> _summons = new List<Character>();
        private static readonly Dictionary<string, Sprite> Icons = new Dictionary<string, Sprite>();
        private float _timer;
        private Sprite _square, _outline, _bar, _star;

        public static void Setup(Hud hud)
        {
            var hotkeyBar = hud.GetComponentInChildren<HotkeyBar>(true);
            var parent = hotkeyBar ? hotkeyBar.transform : hud.m_rootObject.transform;
            var root = new GameObject(RootName, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0f, 1f);
            root.sizeDelta = new Vector2(Spacing * MaxFrames, FrameSize + BarHeight + 4f);
            root.anchoredPosition = Origin;
            root.gameObject.AddComponent<SummonFrames>();
            Hud_Setup.Movable(root, "SummonFrames");
        }

        private void Awake()
        {
            _square = AugaStyle.BundleSprite("Container_Square_A");
            _outline = AugaStyle.BundleSprite("Container_Square_A_Outline");
            _bar = AugaStyle.BundleSprite("AugaProgressBarBody_Small");
            var enemyHud = Auga.Assets.EnemyHud ? Auga.Assets.EnemyHud.GetComponent<EnemyHud>() : null;
            var level2 = enemyHud && enemyHud.m_baseHud ? enemyHud.m_baseHud.transform.Find("level_2") : null;
            if (level2)
                foreach (var image in level2.GetComponentsInChildren<Image>(true))
                    if (image.sprite)
                    {
                        _star = image.sprite;
                        break;
                    }
        }

        private void Update()
        {
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f)
                return;
            _timer = Refresh;
            var player = Player.m_localPlayer;
            _summons.Clear();
            if (player && !player.IsDead() && Auga.ShowSummonFrames.Value)
                FindSummons(player, _summons);
            for (var i = 0; i < _summons.Count; i++)
            {
                if (i == _frames.Count)
                    _frames.Add(MakeFrame(i));
                Fill(_frames[i], _summons[i]);
            }
            for (var i = _summons.Count; i < _frames.Count; i++)
            {
                _frames[i].Root.gameObject.SetActive(false);
                _frames[i].Character = null;
            }
        }

        // Summons in Character.GetAllCharacters order (registration order, so a frame keeps its slot while it lives).
        public static void FindSummons(Player player, List<Character> result)
        {
            var id = player.GetPlayerID();
            var name = player.GetPlayerName();
            foreach (var c in Character.GetAllCharacters())
            {
                if (result.Count >= MaxFrames)
                    break;
                if (!c || c.IsPlayer() || c.IsDead() || !c.m_nview || c.m_nview.GetZDO() is not ZDO zdo)
                    continue;
                var owner = zdo.GetLong(OwnerKey);
                if (owner != 0L ? owner == id : IsVanillaSummonOf(c, zdo, name))
                    result.Add(c);
            }
        }

        private static bool IsVanillaSummonOf(Character c, ZDO zdo, string playerName)
        {
            if (!c.IsTamed() || !c.TryGetComponent<Tameable>(out var tameable))
                return false;
            if (tameable.m_unsummonDistance <= 0f && tameable.m_unsummonOnOwnerLogoutSeconds <= 0f)
                return false;
            return zdo.GetString(ZDOVars.s_follow) == playerName;
        }

        private Frame MakeFrame(int index)
        {
            var frame = new Frame();
            var root = NewRect(transform, "Summon", new Vector2(0f, 1f), new Vector2(0f, 1f));
            root.pivot = new Vector2(0f, 1f);
            root.sizeDelta = new Vector2(FrameSize, FrameSize + BarHeight + 3f);
            root.anchoredPosition = new Vector2(index * Spacing, 0f);
            frame.Root = root;

            var slot = NewRect(root, "Slot", new Vector2(0f, 1f), new Vector2(1f, 1f));
            slot.pivot = new Vector2(0.5f, 1f);
            slot.sizeDelta = new Vector2(0f, FrameSize);
            NewImage(slot, _square, BackgroundColor, Image.Type.Simple);

            var icon = NewRect(slot, "Icon", Vector2.zero, Vector2.one);
            icon.sizeDelta = new Vector2(-8f, -8f);
            frame.Icon = NewImage(icon, null, Color.white, Image.Type.Simple);
            frame.Icon.preserveAspect = true;

            frame.Letter = NewText(slot, "Letter", 22f, TextAlignmentOptions.Center);

            var outline = NewRect(slot, "Outline", Vector2.zero, Vector2.one);
            NewImage(outline, _outline, OutlineColor, Image.Type.Simple);

            var stars = NewRect(slot, "Stars", new Vector2(0f, 1f), new Vector2(1f, 1f));
            stars.pivot = new Vector2(0.5f, 1f);
            stars.sizeDelta = new Vector2(0f, 12f);
            stars.anchoredPosition = new Vector2(0f, -2f);
            frame.Stars = new GameObject[MaxStars];
            for (var s = 0; s < MaxStars; s++)
            {
                var star = NewRect(stars, "star", new Vector2(0f, 1f), new Vector2(0f, 1f));
                star.pivot = new Vector2(0f, 1f);
                star.sizeDelta = new Vector2(11f, 11f);
                star.anchoredPosition = new Vector2(3f + s * 11f, 0f);
                NewImage(star, _star, new Color(1f, 0.75f, 0.11f, 1f), Image.Type.Simple).preserveAspect = true; // Auga BrightestGold
                frame.Stars[s] = star.gameObject;
            }
            frame.Level = NewText(stars, "Many", 12f, TextAlignmentOptions.TopRight);
            frame.Level.rectTransform.offsetMax = new Vector2(-3f, 0f);

            var bar = NewRect(root, "Health", new Vector2(0f, 0f), new Vector2(1f, 0f));
            bar.pivot = new Vector2(0.5f, 0f);
            bar.sizeDelta = new Vector2(0f, BarHeight);
            NewImage(bar, _bar, TrackColor, Image.Type.Sliced);
            frame.Fill = NewRect(bar, "fill", Vector2.zero, Vector2.one);
            NewImage(frame.Fill, _bar, HealthColor, Image.Type.Sliced);
            return frame;
        }

        private void Fill(Frame frame, Character c)
        {
            frame.Root.gameObject.SetActive(true);
            if (frame.Character != c)
            {
                frame.Character = c;
                var icon = IconFor(c);
                frame.Icon.sprite = icon;
                frame.Icon.enabled = icon;
                frame.Letter.gameObject.SetActive(!icon);
                if (!icon)
                {
                    var name = Localization.instance.Localize(c.m_name);
                    frame.Letter.text = string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
                }
            }
            var max = c.GetMaxHealth();
            var fraction = max > 0f ? Mathf.Clamp01(c.GetHealth() / max) : 0f;
            frame.Fill.anchorMax = new Vector2(fraction, 1f);
            // Stars = level - 1 as on the enemy HUD (EnemyHud.cs:191-199); past MaxStars one star with "x N" like
            // Auga's level_X (EnemyHud_Setup.UpdateExtraLevels).
            var stars = Mathf.Max(0, c.GetLevel() - 1);
            var many = stars > MaxStars;
            for (var s = 0; s < frame.Stars.Length; s++)
                frame.Stars[s].SetActive(many ? s == 0 : s < stars);
            frame.Level.gameObject.SetActive(many);
            if (many)
                frame.Level.text = $"x {stars}";
        }

        // Creature icon = its trophy (the only per-creature art in the game: Trophies panel, Compendium). Summon
        // variants drop nothing (Skeleton_Friendly, Troll_Summoned, ...), so the trophy of the creature they are a
        // variant of: the prefab whose Character.m_name is the longest prefix of this one ("$enemy_skeleton" of
        // "$enemy_skeleton_summoned"). No trophy: null, the frame shows the name's first letter.
        public static Sprite IconFor(Character c)
        {
            var key = Utils.GetPrefabName(c.gameObject);
            if (Icons.TryGetValue(key, out var icon))
                return icon;
            icon = Trophy(c.gameObject);
            if (!icon && ZNetScene.instance)
            {
                var bestLength = 0;
                foreach (var prefab in ZNetScene.instance.m_prefabs)
                {
                    if (!prefab || !prefab.TryGetComponent<Character>(out var other) || string.IsNullOrEmpty(other.m_name))
                        continue;
                    var length = other.m_name.Length;
                    if (length <= bestLength || length >= c.m_name.Length || !c.m_name.StartsWith(other.m_name, System.StringComparison.Ordinal))
                        continue;
                    var trophy = Trophy(prefab);
                    if (!trophy)
                        continue;
                    icon = trophy;
                    bestLength = length;
                }
            }
            Icons[key] = icon;
            return icon;
        }

        private static Sprite Trophy(GameObject creature)
        {
            if (!creature.TryGetComponent<CharacterDrop>(out var drops))
                return null;
            foreach (var drop in drops.m_drops)
            {
                var item = drop.m_prefab ? drop.m_prefab.GetComponent<ItemDrop>() : null;
                if (item && item.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy && item.m_itemData.m_shared.m_icons.Length > 0)
                    return item.m_itemData.m_shared.m_icons[0];
            }
            return null;
        }

        private static RectTransform NewRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.sizeDelta = Vector2.zero;
            return rt;
        }

        private static Image NewImage(RectTransform rt, Sprite sprite, Color color, Image.Type type)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite ? type : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text NewText(RectTransform parent, string name, float size, TextAlignmentOptions alignment)
        {
            var rt = NewRect(parent, name, Vector2.zero, Vector2.one);
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            AugaStyle.SetFont(text, Auga.Assets.NorseboldTMP);
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }
    }
}
