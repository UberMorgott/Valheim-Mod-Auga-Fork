using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Auga
{
    // Summon frames (Phase 3, BalanceSim TASKS.md): one small frame per summon of the local player, Diablo-2 style, in a
    // row under the hotkey bar: creature icon, health bar with the summon energy shield over it, state text. Vanilla has
    // no such element. No level stars: summons stay level 1 (summons-ui-contract.md section 2).
    //
    // Two sources:
    // - MorgottTweaks permanent summons (soft dependency, summons-ui-contract.md section 1): the player ZDO mt_sum_ui
    //   holds the summoner's slot list in activation order, written by the summoner's client on every change. Frames are
    //   built from the slots, so a dead (respawn cooldown), off, suppressed or waiting slot without a body shows too.
    //   A loaded body (ZDOID + matching mt_sum_gen) gives live HP from its Character and the energy shield from its ZDO
    //   (mt_sum_es / mt_sum_esmax); otherwise the slot's last known hpFrac / esFrac.
    // - Without that key (no MorgottTweaks, or nothing written yet): the loaded creatures that are the player's summons
    //   (IsSummonOf, vanilla follow-name ownership; MorgottTweaks' mt_sum_owner stamp only narrows it).
    //
    // Art: the frame is the hotkey slot of the Auga bundle (HotKeyElement: Container_Square_A background,
    // Container_Square_A_Outline border), the bar is the bundle's AugaProgressBarBody_Small in the Auga enemy HUD's
    // friendly green, the shield overlay is the player HP bar's energy shield cyan (AugaStatBars.EnergyShieldColor).
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
        private static readonly Color InactiveIconColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
        private static readonly int OwnerKey = "mt_sum_owner".GetStableHashCode();

        // MorgottTweaks contract (summons-ui-contract.md): player ZDO slot list, body ZDO identity and energy shield.
        private static readonly int SlotsKey = "mt_sum_ui".GetStableHashCode();
        private static readonly int BodyGenerationKey = "mt_sum_gen".GetStableHashCode();
        private static readonly int BodyShieldKey = "mt_sum_es".GetStableHashCode();
        private static readonly int BodyShieldMaxKey = "mt_sum_esmax".GetStableHashCode();
        private const int SlotsVersion = 1;

        // mt_sum_ui slot state byte.
        private enum SlotState : byte
        {
            Spawning = 0,
            Alive = 1,
            Dead = 2,
            Off = 3,
            NoReserve = 4,
            Waiting = 5
        }

        // One frame's content, from a slot or from a loaded creature.
        private struct Entry
        {
            public int Key;           // slotId, or the creature's instance id without slots
            public GameObject Prefab; // icon / name source
            public Character Body;    // loaded current body (null = none)
            public SlotState State;
            public bool Stay;
            public float Health, Shield;
            public bool HasShield;
            public double CdUntil;
        }

        private sealed class Frame
        {
            public RectTransform Root, Fill, ShieldFill;
            public Image Icon;
            public TMP_Text Letter, Status;
            public GameObject Prefab;
            public int Key;
            public bool Used;
        }

        private readonly List<Frame> _frames = new List<Frame>();
        private readonly List<Entry> _entries = new List<Entry>();
        private readonly List<Character> _summons = new List<Character>();
        private static readonly Dictionary<string, Sprite> Icons = new Dictionary<string, Sprite>();
        private float _timer;
        private Sprite _square, _outline, _bar;

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
        }

        private void Update()
        {
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f)
                return;
            _timer = Refresh;
            var player = Player.m_localPlayer;
            _entries.Clear();
            var fromSlots = false;
            if (player && !player.IsDead() && Auga.ShowSummonFrames.Value)
            {
                fromSlots = ReadSlots(player, _entries);
                if (!fromSlots)
                    ReadLoaded(player, _entries);
            }
            if (Changed())
                Auga.Log($"[SummonFrames] {_entries.Count} {(fromSlots ? "slot(s)" : "summon(s)")}: {Describe(player)}");
            for (var i = 0; i < _entries.Count; i++)
            {
                if (i == _frames.Count)
                    _frames.Add(MakeFrame(i));
                Fill(_frames[i], _entries[i]);
            }
            for (var i = _entries.Count; i < _frames.Count; i++)
            {
                _frames[i].Root.gameObject.SetActive(false);
                _frames[i].Used = false;
            }
        }

        // MorgottTweaks slot list from the player ZDO (contract section 1). False when the key is absent (no
        // MorgottTweaks, or nothing written yet) or of an unknown version: the caller falls back to loaded creatures.
        private static bool ReadSlots(Player player, List<Entry> result)
        {
            var zdo = player.m_nview ? player.m_nview.GetZDO() : null;
            var bytes = zdo?.GetByteArray(SlotsKey);
            if (bytes == null || bytes.Length < 8)
                return false;
            var pkg = new ZPackage(bytes);
            if (pkg.ReadInt() != SlotsVersion)
                return false;
            var count = pkg.ReadInt();
            for (var i = 0; i < count; i++)
            {
                var slotId = pkg.ReadInt();
                var prefabHash = pkg.ReadInt();
                var state = (SlotState)pkg.ReadByte();
                pkg.ReadByte(); // weight
                var flags = pkg.ReadByte();
                var hpFrac = pkg.ReadSingle();
                var esFrac = pkg.ReadSingle();
                var cdUntil = pkg.ReadDouble();
                var generation = pkg.ReadInt();
                var bodyId = pkg.ReadZDOID();
                if (result.Count >= MaxFrames)
                    continue;
                var entry = new Entry
                {
                    Key = slotId,
                    Prefab = ZNetScene.instance ? ZNetScene.instance.GetPrefab(prefabHash) : null,
                    State = state,
                    Stay = (flags & 1) != 0,
                    Health = Mathf.Clamp01(hpFrac),
                    Shield = Mathf.Clamp01(esFrac),
                    HasShield = esFrac > 0f,
                    CdUntil = cdUntil
                };
                // A loaded current body wins: live HP from its Character, shield from its ZDO (max 0 = no shield).
                var body = Body(bodyId, generation);
                if (body)
                {
                    entry.Body = body;
                    var max = body.GetMaxHealth();
                    entry.Health = max > 0f ? Mathf.Clamp01(body.GetHealth() / max) : 0f;
                    var bodyZdo = body.m_nview.GetZDO();
                    var shieldMax = bodyZdo.GetFloat(BodyShieldMaxKey);
                    entry.HasShield = shieldMax > 0f;
                    entry.Shield = entry.HasShield ? Mathf.Clamp01(bodyZdo.GetFloat(BodyShieldKey) / shieldMax) : 0f;
                }
                result.Add(entry);
            }
            return true;
        }

        // The slot's body when it is loaded here and is the slot's current generation (contract: body ZDO mt_sum_gen).
        private static Character Body(ZDOID id, int generation)
        {
            if (id.IsNone() || !ZNetScene.instance)
                return null;
            var instance = ZNetScene.instance.FindInstance(id);
            if (!instance || !instance.TryGetComponent<Character>(out var c) || c.IsDead() || !c.m_nview ||
                c.m_nview.GetZDO() is not ZDO zdo)
                return null;
            return zdo.GetInt(BodyGenerationKey) == generation ? c : null;
        }

        // Without slots: the player's loaded summons (FindSummons), shield from the body ZDO when MorgottTweaks wrote it.
        private void ReadLoaded(Player player, List<Entry> result)
        {
            _summons.Clear();
            FindSummons(player, _summons);
            foreach (var c in _summons)
            {
                var max = c.GetMaxHealth();
                var zdo = c.m_nview.GetZDO();
                var shieldMax = zdo.GetFloat(BodyShieldMaxKey);
                result.Add(new Entry
                {
                    Key = c.GetInstanceID(),
                    Prefab = c.gameObject,
                    Body = c,
                    State = SlotState.Alive,
                    Health = max > 0f ? Mathf.Clamp01(c.GetHealth() / max) : 0f,
                    HasShield = shieldMax > 0f,
                    Shield = shieldMax > 0f ? Mathf.Clamp01(zdo.GetFloat(BodyShieldKey) / shieldMax) : 0f
                });
            }
        }

        // The entry list differs from what the frames show (by key and state).
        private bool Changed()
        {
            for (var i = 0; i < Mathf.Max(_entries.Count, _frames.Count); i++)
            {
                var shown = i < _frames.Count && _frames[i].Used;
                var found = i < _entries.Count;
                if (shown != found)
                    return true;
                if (found && (_frames[i].Key != _entries[i].Key || _frames[i].Status.text != StatusText(_entries[i], false)))
                    return true;
            }
            return false;
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
                if (IsSummonOf(c, zdo, player, id, name))
                    result.Add(c);
            }
        }

        // Vanilla's own test of "this player's summon" (Tameable.UnsummonMaxInstances, Tameable.cs:641-701): a tamed
        // creature whose ZDO follow name is the player's. Every summon follows its summoner from the spawn on
        // (SpawnAbility m_commandOnSpawn -> Tameable.Command, SpawnAbility.cs:255-260) and cannot be told to stay (no
        // vanilla summon is m_commandable, Tameable.cs:203); one that follows nobody has lost its owner and only waits for
        // UpdateSavedFollowTarget (Tameable.cs:501-528). The mt_sum_owner stamp alone is not ownership: it stays in the
        // ZDO for good, so it narrows the owner (another player's id = not mine) but never replaces the follow test.
        // The creature must be a summon (m_unsummonDistance / m_unsummonOnOwnerLogoutSeconds, Tameable.cs:30-32: every
        // prefab a player staff spawns has both, vanilla and Wizardry, all with m_commandOnSpawn), so a tame wolf or hen
        // that follows the player is not one. On the ZDO owner the live MonsterAI follow target (set only there,
        // RPC_Command, Tameable.cs:460-499) must be this player too (none yet = re-acquiring by name, kept), and a summon
        // past m_unsummonDistance is one vanilla removes (UpdateSummon, Tameable.cs:629-639).
        internal static bool IsSummonOf(Character c, ZDO zdo, Player player, long playerId, string playerName)
        {
            if (!c.IsTamed() || !c.TryGetComponent<Tameable>(out var tameable))
                return false;
            if (tameable.m_unsummonDistance <= 0f && tameable.m_unsummonOnOwnerLogoutSeconds <= 0f)
                return false;
            var stamp = zdo.GetLong(OwnerKey);
            if (stamp != 0L && stamp != playerId)
                return false;
            if (string.IsNullOrEmpty(playerName) || zdo.GetString(ZDOVars.s_follow) != playerName)
                return false;
            if (c.m_nview.IsOwner() && c.GetBaseAI() is MonsterAI ai && ai.GetFollowTarget() is GameObject target && target != player.gameObject)
                return false;
            return tameable.m_unsummonDistance <= 0f ||
                   Vector3.Distance(c.transform.position, player.transform.position) <= tameable.m_unsummonDistance;
        }

        // One line per change of the entry list (Logging/LoggingEnabled).
        private string Describe(Player player)
        {
            var lines = new List<string>(_entries.Count);
            foreach (var e in _entries)
            {
                var name = e.Prefab ? Utils.GetPrefabName(e.Prefab) : "?";
                var line = $"{name} key={e.Key} state={e.State} stay={e.Stay} hp={e.Health:0.##} es={(e.HasShield ? e.Shield.ToString("0.##") : "-")}";
                if (e.Body && player)
                {
                    var zdo = e.Body.m_nview.GetZDO();
                    var ai = e.Body.GetBaseAI() as MonsterAI;
                    var target = ai ? ai.GetFollowTarget() : null;
                    line += $" owner={zdo.GetLong(OwnerKey)} follow='{zdo.GetString(ZDOVars.s_follow)}' " +
                            $"target={(target ? target.name : "-")} dist={Vector3.Distance(e.Body.transform.position, player.transform.position):0.#}";
                }
                lines.Add(line);
            }
            return string.Join("; ", lines);
        }

        // State text (contract section 1): Spawning, Respawn Ns (ceil(cdUntil - now)), Off, No reserve, Respawning, Stay.
        private static string StatusText(Entry e, bool live)
        {
            switch (e.State)
            {
                case SlotState.Spawning:
                    return "Spawning";
                case SlotState.Dead:
                    if (!live)
                        return "Respawn";
                    var now = ZNet.instance ? ZNet.instance.GetTimeSeconds() : 0d;
                    return $"Respawn {Mathf.Max(0, Mathf.CeilToInt((float)(e.CdUntil - now)))}s";
                case SlotState.Off:
                    return "Off";
                case SlotState.NoReserve:
                    return "No reserve";
                case SlotState.Waiting:
                    return "Respawning";
                default:
                    return e.Stay ? "Stay" : "";
            }
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

            // State text across the slot's lower edge, over the icon.
            frame.Status = NewText(slot, "Status", 10f, TextAlignmentOptions.Bottom);
            frame.Status.rectTransform.offsetMin = new Vector2(1f, 2f);
            frame.Status.rectTransform.offsetMax = new Vector2(-1f, 0f);
            frame.Status.textWrappingMode = TextWrappingModes.Normal;
            frame.Status.enableAutoSizing = true;
            frame.Status.fontSizeMin = 7f;
            frame.Status.fontSizeMax = 10f;
            frame.Status.outlineWidth = 0.2f;
            frame.Status.outlineColor = Color.black;

            var bar = NewRect(root, "Health", new Vector2(0f, 0f), new Vector2(1f, 0f));
            bar.pivot = new Vector2(0.5f, 0f);
            bar.sizeDelta = new Vector2(0f, BarHeight);
            NewImage(bar, _bar, TrackColor, Image.Type.Sliced);
            frame.Fill = NewRect(bar, "fill", Vector2.zero, Vector2.one);
            NewImage(frame.Fill, _bar, HealthColor, Image.Type.Sliced);
            // Energy shield over the upper half of the health bar, from the left like the health fill.
            frame.ShieldFill = NewRect(bar, "shield", new Vector2(0f, 0.5f), Vector2.one);
            NewImage(frame.ShieldFill, _bar, AugaStatBars.EnergyShieldColor, Image.Type.Sliced);
            return frame;
        }

        private void Fill(Frame frame, Entry e)
        {
            frame.Root.gameObject.SetActive(true);
            frame.Used = true;
            frame.Key = e.Key;
            if (frame.Prefab != e.Prefab)
            {
                frame.Prefab = e.Prefab;
                var icon = e.Prefab ? IconFor(e.Prefab) : null;
                frame.Icon.sprite = icon;
                frame.Icon.enabled = icon;
                frame.Letter.gameObject.SetActive(!icon);
                if (!icon)
                {
                    var name = e.Prefab && e.Prefab.TryGetComponent<Character>(out var c) ? Localization.instance.Localize(c.m_name) : null;
                    frame.Letter.text = string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
                }
            }
            var active = e.State == SlotState.Alive || e.State == SlotState.Spawning;
            frame.Icon.color = active ? Color.white : InactiveIconColor;
            frame.Letter.color = active ? Color.white : InactiveIconColor;
            frame.Status.text = StatusText(e, true);
            frame.Fill.anchorMax = new Vector2(e.Health, 1f);
            frame.ShieldFill.gameObject.SetActive(e.HasShield && e.Shield > 0f);
            frame.ShieldFill.anchorMax = new Vector2(e.Shield, 1f);
        }

        // Creature icon = its trophy (the only per-creature art in the game: Trophies panel, Compendium). Summon
        // variants drop nothing (Skeleton_Friendly, Troll_Summoned, ...), so the trophy of the creature they are a
        // variant of: the prefab whose Character.m_name is the longest prefix of this one ("$enemy_skeleton" of
        // "$enemy_skeleton_summoned"). No trophy: null, the frame shows the name's first letter.
        public static Sprite IconFor(Character c) => c ? IconFor(c.gameObject) : null;

        public static Sprite IconFor(GameObject creature)
        {
            var key = Utils.GetPrefabName(creature);
            if (Icons.TryGetValue(key, out var icon))
                return icon;
            icon = Trophy(creature);
            var name = creature.TryGetComponent<Character>(out var c) ? c.m_name : null;
            if (!icon && ZNetScene.instance && !string.IsNullOrEmpty(name))
            {
                var bestLength = 0;
                foreach (var prefab in ZNetScene.instance.m_prefabs)
                {
                    if (!prefab || !prefab.TryGetComponent<Character>(out var other) || string.IsNullOrEmpty(other.m_name))
                        continue;
                    var length = other.m_name.Length;
                    if (length <= bestLength || length >= name.Length || !name.StartsWith(other.m_name, System.StringComparison.Ordinal))
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
