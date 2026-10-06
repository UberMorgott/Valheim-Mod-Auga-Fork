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
            { "TabBackground", Role.Backdrop },             // Epic Loot enchanting table tab column
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

        // AveriaSansLibre-Bold SDF: Epic Loot's own TMP asset (enchanting table tab labels).
        private static readonly HashSet<string> VanillaFonts =
            new HashSet<string> { "Valheim-AveriaSerifLibre", "Valheim-AveriaSansLibre", "Valheim-Norsebold", "AveriaSansLibre-Bold SDF" };
        private const float HeaderSize = 30f;

        // Auga's button family (bundle prefabs, UnityPy dump of augaassets): art, native height, label side inset
        // (Label sizeDelta.x / -2) and label size per height (Label fontSize / height). Each art's ornamented ends are
        // a horizontal 9-slice border drawn at fixed size and stretched vertically, so a button takes the art made
        // for its height: ButtonFancy's 50 px ends squashed into a 22 px button left no room for its label.
        private sealed class ButtonArt
        {
            public Button Button;
            public Image Image;
            public float Height, Inset, LabelRatio;
        }

        private static ButtonArt _fancy, _medium, _small, _tabArt;

        private static bool _loaded;
        private static Image _panel;
        private static Image[] _corners;
        private static TMP_FontAsset _labelFont, _bodyFont;

        private static void Load()
        {
            if (_loaded)
                return;
            _loaded = true;
            _panel = FromPrefab<Image>(Auga.Assets.PanelBase, "Background");
            _corners = _panel ? System.Array.FindAll(_panel.GetComponentsInChildren<Image>(true), i => i != _panel) : new Image[0];
            // ButtonFancy 272x50 label 32 inset 28; ButtonMedium 122x35 label 16 inset 20; ButtonSmall 77x22 label 13
            // inset 14; ButtonSettings (tabs) 136x22 label 13 inset 14.
            ButtonArt Art(GameObject prefab, float height, float inset, float label) => new ButtonArt
            {
                Button = FromPrefab<Button>(prefab, ""),
                Image = FromPrefab<Image>(prefab, "Image"),
                Height = height,
                Inset = inset,
                LabelRatio = label / height
            };
            _fancy = Art(Auga.Assets.ButtonFancy, 50f, 28f, 32f);
            _medium = Art(Auga.Assets.ButtonMedium, 35f, 20f, 16f);
            _small = Art(Auga.Assets.ButtonSmall, 22f, 14f, 13f);
            _tabArt = Art(Auga.Assets.ButtonSettings, 22f, 14f, 13f);
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
            var buttons = new Dictionary<Selectable, ButtonArt>(); // button -> the Auga art it took
            var overrides = ControlRoles(root, out var tabs, out var bars);
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (!image.sprite)
                {
                    // List rows: vanilla marks the selection with a sprite-less "selected" image (FejdStartup.cs:1319 rows).
                    if (image.name == "selected")
                        image.color = SelectedRow;
                    continue;
                }
                // A GuiBar's fill keeps its art and colour: GuiBar.Awake caches the colour (GuiBar.cs:33-37) and mod
                // bars (Epic Loot progress bars, SetRarityColor) tint it per rarity.
                if (bars.Contains(image))
                    continue;
                var role = overrides.TryGetValue(image, out var forced) ? forced : RoleOf(image.sprite.name);
                if (role == null)
                    continue;
                var selectable = image.GetComponentInParent<Selectable>(true);
                var owns = selectable && selectable.targetGraphic == image;
                var isInput = owns && (selectable is TMP_InputField || selectable is InputField);
                // Mod bundles (Epic Loot) frame their input fields with item_background: an input field's own graphic
                // is an input whatever its vanilla sprite.
                if (role == Role.Backdrop && isInput)
                    role = Role.Input;
                // Tab buttons of a TabHandler (TabHandler.cs:228-251) take Auga's tab art even when the mod drew them
                // with the plain button sprite (Epic Loot enchanting table tabs).
                if (role == Role.Button && owns && tabs.Contains(selectable))
                    role = Role.Tab;
                // Vanilla also uses the input sprites as a plain frame: the minimap (hudroot/MiniMap/small and large),
                // bar "darken" overlays and the inventory lists (requirements, skills, trophies, texts). Auga's input
                // art has chevron ends, so on the minimap it peeked out behind the map like a second map. Only an
                // input field's own graphic is an input; any other use is a backdrop, as in Auga's bundle HUD
                // (MiniMap/small/MapBG = TextBackdrop).
                if (role == Role.Input && !isInput)
                    role = Role.Backdrop;
                // Mod sliders (Jotunn DefaultControls knob, StarLevelSystem ConfigUI.BuildSlider) draw their handle with
                // checkbox_marker; a slider handle is a knob, as vanilla's (Knob sprite).
                if (role == Role.ToggleMark && owns && selectable is Slider)
                    role = Role.Knob;
                switch (role.Value)
                {
                    case Role.Panel: Panel(image); break;
                    // Mod list rows (Epic Loot MerchantPanel ItemElement) mark the selection with a "Selected" child on
                    // the same item_background art as the row; it takes the Auga list-row selection colour.
                    case Role.Backdrop when string.Equals(image.name, "selected", System.StringComparison.OrdinalIgnoreCase):
                        SetSprite(image, "TextBackdrop", SelectedRow, Image.Type.Sliced); break;
                    case Role.Backdrop: SetSprite(image, "TextBackdrop", BackdropColor, Image.Type.Sliced); break;
                    case Role.Tooltip: SetSprite(image, "TextBackdrop", TooltipColor, Image.Type.Sliced); break;
                    case Role.Input:
                        var vanillaBorder = image.sprite.border / Ppu(image);
                        SetSprite(image, "TextInputBG", Color.white, Image.Type.Sliced, 2f);
                        FitInputEnds(image);
                        InsetInputText(image, vanillaBorder);
                        // Vanilla GuiInputField swaps to its own selected/highlighted sprites on focus; with empty
                        // states SpriteSwap keeps the Auga sprite.
                        if (owns && selectable.transition == Selectable.Transition.SpriteSwap)
                            selectable.spriteState = default;
                        break;
                    case Role.Button:
                    case Role.Tab:
                        var art = role == Role.Tab ? _tabArt : ArtFor(owns ? selectable.transform : image.transform);
                        CopyImage(image, art.Image);
                        if (owns)
                        {
                            Button(selectable, art.Button);
                            buttons[selectable] = art;
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
                // Auga's button art has ornamented ends, so its label sits inset (bundle Label sizeDelta.x -56/-40/-28).
                // Labels vanilla already auto-sizes get the same inset as a TMP margin, and may grow up to the Auga
                // label size for the button's height (mods size them for plain art: Epic Loot's tab labels max 10 in a
                // 20 px tab); auto-size still shrinks them to fit. The RectTransform stays vanilla.
                if (inButton && text.enableAutoSizing)
                {
                    var art = buttons[selectable];
                    text.margin = new Vector4(art.Inset, text.margin.y, art.Inset, text.margin.w);
                    text.fontSizeMax = Mathf.Max(text.fontSizeMax, Mathf.Round(art.LabelRatio * Height(selectable.transform, art)));
                }
            }

            // Legacy uGUI texts (mod bundle panels, e.g. Epic Loot MerchantPanel: Norsebold titles, AveriaSans/Serif
            // Libre-Bold body and button labels): body text takes Auga's Source Sans Pro, button labels and Norsebold
            // headers take the game's Norsebold, as the TMP rule above.
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                if (!text.font || !VanillaLegacyFonts.Contains(text.font.name))
                    continue;
                var selectable = text.GetComponentInParent<Selectable>(true);
                var inButton = selectable && buttons.ContainsKey(selectable);
                var label = inButton || IsAugaButton(selectable) || text.font.name == "Norsebold";
                var font = label ? LegacyNorsebold(text.font) : Auga.Assets.SourceSansProRegular;
                if (font)
                    text.font = font;
                if (inButton)
                    FitLegacyLabel(text, selectable.transform, buttons[selectable]);
            }
        }

        // A legacy button label (Jotunn CreateButton, Epic Loot bundle buttons) gets what a TMP label gets above: the
        // art's side inset (its rect, when it spans the button, pulled in from both ends) and best fit up to the Auga
        // label size for the button's height, never below its own size. Mods size these labels for plain art (Jotunn
        // 16 in a 40 px button, Epic Loot best fit 1-14 in 36 px), small inside Auga's ornamented frame.
        private static void FitLegacyLabel(Text text, Transform button, ButtonArt art)
        {
            var rt = text.rectTransform;
            if (rt.anchorMin.x == 0f && rt.anchorMax.x == 1f && rt.offsetMin.x < art.Inset && -rt.offsetMax.x < art.Inset)
            {
                rt.offsetMin = new Vector2(art.Inset, rt.offsetMin.y);
                rt.offsetMax = new Vector2(-art.Inset, rt.offsetMax.y);
            }
            var size = Mathf.RoundToInt(art.LabelRatio * Height(button, art));
            if (!text.resizeTextForBestFit)
            {
                text.resizeTextMinSize = Mathf.Min(text.fontSize, 10);
                text.resizeTextForBestFit = true;
            }
            text.resizeTextMaxSize = Mathf.Max(text.resizeTextMaxSize, text.fontSize, size);
        }

        // The Auga art for a button of this height: the one whose native height is nearest (geometric midpoints).
        private static ButtonArt ArtFor(Transform button)
        {
            var h = Height(button, null);
            if (h <= 0f)
                return _fancy;
            if (h < Mathf.Sqrt(_small.Height * _medium.Height))
                return _small;
            return h < Mathf.Sqrt(_medium.Height * _fancy.Height) ? _medium : _fancy;
        }

        // Laid-out height, else the LayoutElement's (layout groups size their children later), else the art's own.
        private static float Height(Transform t, ButtonArt art)
        {
            var h = t is RectTransform rt ? rt.rect.height : 0f;
            if (h <= 0f && t.TryGetComponent<LayoutElement>(out var layout))
                h = Mathf.Max(layout.preferredHeight, layout.minHeight);
            return h > 0f ? h : art?.Height ?? 0f;
        }

        // Auga's input art has chevron ends 136 texture px wide in all; at the Auga multiplier a narrow field (Jotunn
        // gradient picker: 75x20 and 50x20) is all ends and its text area goes negative. The ends shrink to at most
        // half the field's width.
        private static void FitInputEnds(Image image)
        {
            var width = ((RectTransform)image.transform).rect.width;
            var ends = image.sprite.border.x + image.sprite.border.z;
            if (width <= 0f || ends <= 0f || ends / Ppu(image) <= 0.5f * width)
                return;
            image.pixelsPerUnitMultiplier = ends / (image.pixelsPerUnit * 0.5f * width);
        }

        // Roles a sprite name alone cannot give, from the controls that own the images, plus the tab buttons and bar
        // fills of the tree. A toggle drawn as a box with a check sprite in it (Epic Loot bundle toggles: item_background
        // "Background" + CheckMark "Checkmark", the toggle's graphic) is Auga's diamond toggle, as vanilla checkbox art.
        private static Dictionary<Image, Role> ControlRoles(Transform root, out HashSet<Selectable> tabs, out HashSet<Image> bars)
        {
            var roles = new Dictionary<Image, Role>();
            foreach (var toggle in root.GetComponentsInChildren<Toggle>(true))
            {
                if (!(toggle.graphic is Image mark) || !mark.sprite || mark.sprite.name != "CheckMark")
                    continue;
                roles[mark] = Role.ToggleMark; // also the box-less tick of a dropdown list item
                var parent = mark.transform.parent;
                if (parent && parent.TryGetComponent<Image>(out var box) && box.sprite && RoleOf(box.sprite.name) == Role.Backdrop)
                    roles[box] = Role.Toggle;
            }
            tabs = new HashSet<Selectable>();
            foreach (var handler in root.GetComponentsInChildren<TabHandler>(true))
                foreach (var tab in handler.m_tabs)
                    if (tab != null && tab.m_button)
                        tabs.Add(tab.m_button);
            bars = new HashSet<Image>();
            foreach (var bar in root.GetComponentsInChildren<GuiBar>(true))
                if (bar.m_bar && bar.m_bar.TryGetComponent<Image>(out var fill))
                    bars.Add(fill);
            return roles;
        }

        // Restyle plus the controls whose Auga look is more than a sprite: scrollbars take Auga's scrollbar, legacy
        // dropdown lists Auga's tooltip/list frame (Jotunn's button_small frame, GUIManager.cs:1274-1279, and mod
        // bundle lists Restyle has already made a backdrop), as vanilla's woodpanel_400_tileable dropdown templates.
        public static void RestyleAll(Transform root)
        {
            Restyle(root);
            foreach (var scrollbar in root.GetComponentsInChildren<Scrollbar>(true))
                Scrollbar(scrollbar);
            foreach (var dropdown in root.GetComponentsInChildren<Dropdown>(true))
                if (dropdown.template && dropdown.template.TryGetComponent<Image>(out var list) && list.sprite &&
                    (list.sprite.name == "button_small" || list.sprite.name == "TextBackdrop"))
                    Tooltip(list);
        }

        // AveriaSerifLibre-Regular: Jotunn's GUIManager.AveriaSerif, VNEI's body text (Styling.ApplyText).
        // LegacyRuntime / Arial: Unity's built-in font, which mods get when they set none (Jotunn's map-overlay toggle
        // labels, MinimapManager.cs; DisplayBepInExInfo's main menu lines).
        private static readonly HashSet<string> VanillaLegacyFonts =
            new HashSet<string> { "AveriaSerifLibre-Bold", "AveriaSerifLibre-Regular", "AveriaSansLibre-Bold", "Norsebold", "LegacyRuntime", "Arial" };

        // A button restyled by an earlier pass (its graphic already carries Auga's button/tab art): its label is still a
        // label when a later pass reaches only the text (Jotunn's ApplyTextStyle on a button label).
        private static bool IsAugaButton(Selectable selectable) =>
            selectable && selectable.targetGraphic is Image image && image.sprite &&
            System.Array.Exists(new[] { _fancy, _medium, _small, _tabArt }, a => a?.Image && image.sprite == a.Image.sprite);
        private static Font _legacyNorsebold;

        private static Font LegacyNorsebold(Font current)
        {
            if (current.name == "Norsebold")
                return current;
            if (!_legacyNorsebold)
                _legacyNorsebold = System.Array.Find(Resources.FindObjectsOfTypeAll<Font>(), f => f && f.name == "Norsebold");
            return _legacyNorsebold;
        }

        private static Role? RoleOf(string sprite)
        {
            // Sprites fetched from a SpriteAtlas at runtime (Jotunn GUIManager.GetSprite, SpriteAtlas.GetSprite) are
            // copies named "<sprite>(Clone)".
            if (sprite.EndsWith("(Clone)"))
                sprite = sprite.Substring(0, sprite.Length - "(Clone)".Length);
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

        // Auga's HUD backdrop (bundle HUD MiniMap/small/MapBG: TextBackdrop, black 50%, sliced).
        public static void Backdrop(Image image)
        {
            Load();
            SetSprite(image, "TextBackdrop", BackdropColor, Image.Type.Sliced);
        }

        // Auga's tooltip / list frame (MainMenu Tooltip), as the Tooltip role gives vanilla dropdown templates.
        public static void Tooltip(Image image)
        {
            Load();
            SetSprite(image, "TextBackdrop", TooltipColor, Image.Type.Sliced);
        }

        private static bool _scrollbarLoaded;
        private static Scrollbar _scrollbar;
        private static Image _scrollbarTrack, _scrollbarHandle;

        // Auga's scrollbar (bundle Inventory_screen crafting recipe list, AugaCraftingPanel.RecipeListScrollbar): track
        // and handle art, transition and colours. The scrollbar's RectTransforms stay as the caller sized them.
        public static void Scrollbar(Scrollbar dst)
        {
            if (!_scrollbarLoaded)
            {
                _scrollbarLoaded = true;
                var crafting = Auga.Assets.InventoryScreen ? Auga.Assets.InventoryScreen.GetComponentInChildren<AugaUnity.AugaCraftingPanel>(true) : null;
                _scrollbar = crafting ? crafting.RecipeListScrollbar : null;
                if (_scrollbar)
                {
                    _scrollbar.TryGetComponent(out _scrollbarTrack);
                    _scrollbarHandle = _scrollbar.handleRect ? _scrollbar.handleRect.GetComponent<Image>() : null;
                }
                else
                    Debug.LogError("[Auga] AugaStyle: bundle Inventory_screen has no crafting recipe list scrollbar");
            }
            if (!dst || !_scrollbar || dst == _scrollbar)
                return;
            if (_scrollbarTrack && dst.TryGetComponent<Image>(out var track))
                CopyImage(track, _scrollbarTrack);
            if (_scrollbarHandle && dst.handleRect && dst.handleRect.TryGetComponent<Image>(out var handle))
                CopyImage(handle, _scrollbarHandle);
            dst.transition = _scrollbar.transition;
            dst.colors = _scrollbar.colors;
            dst.spriteState = _scrollbar.spriteState;
        }

        // Rendered size of a sliced border, in local units per texture pixel (Image.pixelsPerUnit already divides by
        // the canvas reference PPU).
        private static float Ppu(Image image) => image.pixelsPerUnit * image.pixelsPerUnitMultiplier;

        // Vanilla insets the text area of an input field (TMP_InputField.textViewport, or a legacy InputField's
        // text/placeholder) by a margin inside its own sprite's 9-slice border. Auga's TextInputBG has chevron ends
        // in a wider border, so the same margin is kept past the Auga border: the text area grows its inset by the
        // border difference on each side. The field's own RectTransform and the vanilla margin stay untouched.
        private static void InsetInputText(Image background, Vector4 vanillaBorder)
        {
            var extra = background.sprite.border / Ppu(background) - vanillaBorder;
            var left = Mathf.Max(0f, extra.x);
            var right = Mathf.Max(0f, extra.z);
            if (left == 0f && right == 0f)
                return;
            var areas = new List<RectTransform>();
            if (background.TryGetComponent<TMP_InputField>(out var tmp) && tmp.textViewport)
                areas.Add(tmp.textViewport);
            else if (background.TryGetComponent<InputField>(out var legacy))
            {
                if (legacy.textComponent) areas.Add(legacy.textComponent.rectTransform);
                if (legacy.placeholder) areas.Add(legacy.placeholder.rectTransform);
            }
            foreach (var area in areas)
            {
                area.offsetMin += new Vector2(left, 0f);
                area.offsetMax -= new Vector2(right, 0f);
            }
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

        private static bool _gameFontsLinked;

        // Source Sans Pro lacks some symbols other mods put into text (EpicLoot's set bonus bullet) and TMP draws a
        // box. SetFont only links the font a text had before, so bundle texts that start on Source Sans Pro never get
        // one. The game's body fonts (Noto fallback chain) go onto every Source Sans Pro asset. No-op until the game's
        // fonts are loaded, so it is called from the main menu and again from the in-game Hud.
        public static void LinkGameFontFallbacks()
        {
            if (_gameFontsLinked)
                return;
            TMP_FontAsset serif = null, sans = null;
            var augaFonts = new List<TMP_FontAsset>();
            foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                if (!font)
                    continue;
                if (font.name == "Valheim-AveriaSerifLibre")
                    serif = font;
                else if (font.name == "Valheim-AveriaSansLibre")
                    sans = font;
                else if (font.name.StartsWith("SourceSansPro"))
                    augaFonts.Add(font);
            }
            var gameFonts = new List<TMP_FontAsset>();
            if (serif) gameFonts.Add(serif);
            if (sans) gameFonts.Add(sans);
            if (gameFonts.Count == 0)
                return;
            _gameFontsLinked = true;
            foreach (var font in augaFonts)
            {
                font.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
                foreach (var gameFont in gameFonts)
                    if (!font.fallbackFontAssetTable.Contains(gameFont))
                        font.fallbackFontAssetTable.Add(gameFont);
            }
            Auga.Log($"Fonts: {string.Join(", ", gameFonts.ConvertAll(f => f.name))} added as fallbacks of {augaFonts.Count} Source Sans Pro assets.");
        }
    }
}
