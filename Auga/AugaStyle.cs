using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Auga
{
    // Restyle in place (spec 2026-09-11-auga-native-rework, option A): give live vanilla UI the look of Auga's
    // bundle widgets. Visual properties only; RectTransforms, layout, components and vanilla fields stay untouched,
    // except that an Auga panel gains its gradient (a mesh effect on the same Image) and its corner ornaments.
    //
    // One entry point for every screen: Restyle(root) maps each vanilla Image by its sprite name (Roles), each
    // vanilla TMP text by role (button/header label or body), using art from the bundle. Vanilla sprite names come
    // from the runtime dump of FejdStartup, Settings, the pause menu and TextsDialog (tools\AutotestDriver
    // -autotestshots, dump-*.txt); Auga looks from the bundle prefabs AugaPanelBase, ButtonFancy, ButtonSettings,
    // MainMenu (toggles, list rows, inputs) and InventoryTooltip.
    public static class AugaStyle
    {
        private enum Role { Panel, Backdrop, Tooltip, Input, Button, Tab, TabSelected, Toggle, ToggleMark, Knob, Knot, Line }

        private static readonly Dictionary<string, Role> Roles = new Dictionary<string, Role>
        {
            // wood panels (woodpanel_* prefix, see RoleOf) -> Auga panel
            { "woodpanel_400_tileable", Role.Tooltip },     // tooltips and dropdown templates
            { "panel_interior_bkg_128", Role.Backdrop },    // Settings tab content
            { "panel_bkg_128", Role.Backdrop },             // AddServer, PleaseWait, GameVersion
            { "item_background", Role.Backdrop },           // world/server lists, help boxes
            { "text_field", Role.Input },
            { "InputFieldBackground", Role.Input },
            { "button", Role.Button },
            { "button_tab", Role.Tab },
            { "button_tab_selected", Role.TabSelected },    // TabHandler's "Selected" child (TabHandler.cs:247-251)
            { "checkbox", Role.Toggle },
            { "checkbox_marker", Role.ToggleMark },
            { "Knob", Role.Knob },
            { "BraidKnotMedium", Role.Knot },
            { "BraidLineHorisontalMedium", Role.Line },
            { "panel_separator", Role.Line },
        };

        // Bundle sprites are texture sub-assets (AssetBundle.LoadAsset<Sprite>(name) returns null), so they are taken
        // from the bundle prefabs that use them: Images and button sprite states of these roots.
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

        // Colours as the bundle prefabs set them (UnityPy dump of augaassets).
        private static readonly Color Light = new Color(0.82f, 0.79f, 0.76f);          // dividers, knots
        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.5f);      // MainMenu SourceInfo
        private static readonly Color TooltipColor = new Color(0.18f, 0.15f, 0.13f);    // MainMenu Tooltip
        private static readonly Color ToggleColor = new Color(0.09f, 0.08f, 0.06f);     // MainMenu toggle Background
        private static readonly Color ToggleMarkColor = new Color(0.73f, 0.54f, 0.07f); // MainMenu toggle Checkmark
        private static readonly Color SelectedRow = new Color(0.28f, 0.23f, 0.19f);     // RecipeElement/selected (Auga list rows)

        private static readonly HashSet<string> VanillaFonts =
            new HashSet<string> { "Valheim-AveriaSerifLibre", "Valheim-AveriaSansLibre", "Valheim-Norsebold" };
        private const float HeaderSize = 30f;
        private const float ButtonLabelInset = 28f, TabLabelInset = 14f;

        private static bool _loaded;
        private static Image _panel;
        private static Image[] _corners;
        private static Button _button, _tab;
        private static Image _buttonImage, _tabImage;
        private static TMP_FontAsset _labelFont, _bodyFont;

        private static void Load()
        {
            if (_loaded)
                return;
            _loaded = true;
            _panel = FromPrefab<Image>(Auga.Assets.PanelBase, "Background");
            _corners = _panel ? System.Array.FindAll(_panel.GetComponentsInChildren<Image>(true), i => i != _panel) : new Image[0];
            _button = FromPrefab<Button>(Auga.Assets.ButtonFancy, "");
            _buttonImage = FromPrefab<Image>(Auga.Assets.ButtonFancy, "Image");
            _tab = FromPrefab<Button>(Auga.Assets.ButtonSettings, "");
            _tabImage = FromPrefab<Image>(Auga.Assets.ButtonSettings, "Image");
            _labelFont = Auga.Assets.NorseboldTMP;
            _bodyFont = Auga.Assets.SourceSansProRegularTMP;

            void Add(Sprite s)
            {
                if (s && !Sprites.ContainsKey(s.name))
                    Sprites.Add(s.name, s);
            }
            foreach (var prefab in new[] { Auga.Assets.MainMenuPrefab, Auga.Assets.MenuPrefab, Auga.Assets.ButtonSettings, Auga.Assets.InventoryScreen, Auga.Assets.PasswordDialog })
            {
                if (!prefab)
                    continue;
                foreach (var image in prefab.GetComponentsInChildren<Image>(true))
                    Add(image.sprite);
                foreach (var selectable in prefab.GetComponentsInChildren<Selectable>(true))
                {
                    var state = selectable.spriteState;
                    Add(state.highlightedSprite);
                    Add(state.pressedSprite);
                    Add(state.selectedSprite);
                    Add(state.disabledSprite);
                }
            }
            foreach (var name in new[] { "TextBackdrop", "TextInputBG", "Container_Diamond", "Knob", "Divider_Chevron_b", "SettignsButtonOver" })
                if (!Sprites.ContainsKey(name))
                    Debug.LogError($"[Auga] AugaStyle: bundle sprite {name} not found in the Auga prefabs");
        }

        public static void Restyle(Transform root)
        {
            Load();
            var buttons = new Dictionary<Selectable, float>(); // button -> label side inset of the Auga art
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (!image.sprite)
                {
                    // List rows: vanilla marks the selection with a sprite-less "selected" image (FejdStartup.cs:1319 rows).
                    if (image.name == "selected")
                        image.color = SelectedRow;
                    continue;
                }
                var role = RoleOf(image.sprite.name);
                if (role == null)
                    continue;
                var selectable = image.GetComponentInParent<Selectable>(true);
                var owns = selectable && selectable.targetGraphic == image;
                switch (role.Value)
                {
                    case Role.Panel: Panel(image); break;
                    case Role.Backdrop: SetSprite(image, "TextBackdrop", BackdropColor, Image.Type.Sliced); break;
                    case Role.Tooltip: SetSprite(image, "TextBackdrop", TooltipColor, Image.Type.Sliced); break;
                    case Role.Input: SetSprite(image, "TextInputBG", Color.white, Image.Type.Sliced, 2f); break;
                    case Role.Button:
                    case Role.Tab:
                        var tab = role == Role.Tab;
                        CopyImage(image, tab ? _tabImage : _buttonImage);
                        if (owns)
                        {
                            Button(selectable, tab ? _tab : _button);
                            buttons[selectable] = tab ? TabLabelInset : ButtonLabelInset;
                        }
                        break;
                    case Role.TabSelected: SetSprite(image, "SettignsButtonOver", Color.white, Image.Type.Sliced); break;
                    case Role.Toggle: SetSprite(image, "Container_Diamond", ToggleColor, Image.Type.Simple); break;
                    case Role.ToggleMark: SetSprite(image, "Container_Diamond", ToggleMarkColor, Image.Type.Simple); break;
                    case Role.Knob: SetSprite(image, "Knob", Color.white, Image.Type.Simple); break;
                    case Role.Knot: SetSprite(image, "Divider_Chevron_b", Light, Image.Type.Simple); image.preserveAspect = true; break;
                    // Vanilla line sprites are a thin rule inside a tall transparent texture; Auga's Divider_Line would fill
                    // the whole rect, so the rule keeps its sprite and takes Auga's divider colour.
                    case Role.Line: image.color = Light; break;
                }
            }

            // Build-menu tag rows show the current tag through m_toggledOnObject (BuildUiTagButton.cs:89-92), a blue
            // sliced image; it takes the Auga list-row selection colour like the "selected" rows above.
            foreach (var tag in root.GetComponentsInChildren<BuildUiTagButton>(true))
                if (tag.m_toggledOnObject && tag.m_toggledOnObject.TryGetComponent<Image>(out var toggled))
                    toggled.color = SelectedRow;

            // Button and tab labels and headers: Norsebold (Auga's button/menu font). Everything else: Auga's body font.
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (!text.font || !VanillaFonts.Contains(text.font.name))
                    continue;
                var selectable = text.GetComponentInParent<Selectable>(true);
                var inButton = selectable && buttons.ContainsKey(selectable);
                var label = inButton || text.fontSize >= HeaderSize || text.font.name == "Valheim-Norsebold";
                SetFont(text, label ? _labelFont : _bodyFont);
                // Auga's button art has ornamented ends, so its label sits inset (bundle ButtonFancy/Label and
                // ButtonSettings/Label sizeDelta.x -56/-28). Labels vanilla already auto-sizes get the same inset as a
                // TMP margin and vanilla's auto-size shrinks them to fit; the RectTransform stays vanilla.
                if (inButton && text.enableAutoSizing)
                    text.margin = new Vector4(buttons[selectable], text.margin.y, buttons[selectable], text.margin.w);
            }
        }

        private static Role? RoleOf(string sprite)
        {
            if (Roles.TryGetValue(sprite, out var role))
                return role;
            return sprite.StartsWith("woodpanel_") ? Role.Panel : (Role?)null;
        }

        public static T FromPrefab<T>(GameObject prefab, string path) where T : Component
        {
            var t = prefab ? (path.Length == 0 ? prefab.transform : prefab.transform.Find(path)) : null;
            var c = t ? t.GetComponent<T>() : null;
            if (c == null)
                Debug.LogError($"[Auga] bundle prefab {(prefab ? prefab.name : "null")}: {path} ({typeof(T).Name}) missing");
            return c;
        }

        public static void CopyImage(Image dst, Image src)
        {
            if (!dst || !src)
                return;
            dst.sprite = src.sprite;
            dst.type = src.type;
            dst.color = src.color;
            dst.material = src.material;
            dst.pixelsPerUnitMultiplier = src.pixelsPerUnitMultiplier;
        }

        private static void SetSprite(Image image, string sprite, Color color, Image.Type type, float ppu = 1f)
        {
            if (!Sprites.TryGetValue(sprite, out var s))
                return;
            image.sprite = s;
            image.type = type;
            image.color = color;
            image.material = null;
            image.pixelsPerUnitMultiplier = ppu;
        }

        // Auga's panel art has no sprite: a plain quad coloured by a JoshH UIGradient mesh effect, with four
        // CornerDecoration images on top (bundle AugaPanelBase/Background). Copying only the Image gives a white quad.
        private static void Panel(Image dst)
        {
            CopyImage(dst, _panel);
            var gradient = _panel ? _panel.GetComponent<BaseMeshEffect>() : null;
            if (gradient)
            {
                var c = dst.GetComponent(gradient.GetType()) ?? dst.gameObject.AddComponent(gradient.GetType());
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(gradient), c);
            }
            if (dst.transform.Find("AugaCorner"))
                return;
            foreach (var corner in _corners)
            {
                var c = Object.Instantiate(corner.gameObject, dst.transform, false);
                c.name = "AugaCorner";
                c.transform.SetAsFirstSibling(); // the panel's own content draws above the ornaments
                c.GetComponent<Image>().raycastTarget = false;
            }
        }

        // Auga buttons swap sprites per state (Up/Over/Down/Disabled) instead of tinting one sprite.
        private static void Button(Selectable dst, Selectable src)
        {
            if (!src)
                return;
            dst.transition = src.transition;
            dst.spriteState = src.spriteState;
            dst.colors = src.colors;
        }

        public static void CopyText(TMP_Text dst, TMP_Text src)
        {
            if (!dst || !src)
                return;
            SetFont(dst, src.font);
            dst.fontSharedMaterial = src.fontSharedMaterial;
            dst.color = src.color;
            dst.fontStyle = src.fontStyle;
            dst.characterSpacing = src.characterSpacing;
        }

        // Glyphs the Auga font lacks (icons, symbols) still render from the vanilla font.
        public static void SetFont(TMP_Text text, TMP_FontAsset font)
        {
            var original = text.font;
            if (!font || original == font)
                return;
            font.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
            if (original && !font.fallbackFontAssetTable.Contains(original))
                font.fallbackFontAssetTable.Add(original);
            text.font = font;
            text.fontSharedMaterial = font.material;
        }
    }
}
