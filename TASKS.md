# Auga-everywhere backlog (scope rule: every UI, vanilla + pack mods, Auga-styled)

Phase 1 audit 2026-10-04; full inventory merged 2026-10-04 (AugaSkin vs decompile comparison, code-verified, in-game
unverified unless stated). Order = visibility (high first), then size. Size S/M/L = approach estimate, not time.
Shot = autotest `-Shots` name (`tools\AutotestDriver`); "new" = still to write. Decompiles used:
`E:\Temp\claude\E--DEV-Valheim-Auga-Fork\c742cfb3-f750-4393-bdf4-89d49021f3f0\scratchpad\decomp\<mod>` (temporary).
Status "verify" = mod has UI code, AugaSkin has no code for it, look not yet checked in game.
Root cause of vanilla gaps: `AugaStyle.Restyle` only maps known sprites + fonts; runtime-instantiated prefabs miss restyle.
NO = no Auga styling; PARTIAL = partly styled.

## Progress log ("fix everything to the end", 2026-10-06)

- Batch 1 (AugaSkin 2.0.9): button art by height (ButtonSmall/Medium/Fancy, labels grow to the Auga label size;
  EL tab/mode labels, Jotunn picker buttons, map-overlay button), narrow inputs keep a text area, Arial/LegacyRuntime
  -> Source Sans, #32 EL dialogs scrollbar, #54 trader-panel scrollbars, EAQS quick-slot bar moved above the stat
  cluster (was over the health bar: HUD audit overlap; same rule as EAQS's own FixQuickSlotPositionForAuga), audit
  skips chat world texts (NPC bubbles). Shots `20-hud` (+ `quickslots` check), `65f-el-augment` (new),
  `67e-map-overlay-open` (new), regression 65b/65c/65d/67*/47-vnei/40-inventory/02/10/11/12/22/23/41/42 all PASS.

- Batch 2 (AugaSkin 2.0.10): vanilla HUD parts #3 #11 #12 #13 #20 #21 #22 #23 #41 (`HudParts.cs`, HUD roles,
  hover-text transpiler, loading screen restyle). Shot `28-vanilla-hud` (new, 6 screenshots + checks); harness: traders left
  by 65-hildir/65b/65c now removed with their ZDO.

- Batch 3 (AugaSkin 2.0.11): Epic Loot #8 #10 #27 #33 #34 #43 #53, #24 #28 #44 closed as covered / n.a.; #9 #26
  skipped (design decision). Shots `65g-el-popups`, `65h-el-hudparts` (new), `12-compendium` pages.

- Batch 4 (AugaSkin 2.0.12): vanilla panels #18 #19 #35 #36 #37 #38 #39 #40 #45 (`VanillaPanels.cs`); audit knows
  Menu.m_currentPlayersInstance is destroyed by vanilla. Shot `28b-vanilla-panels` (new).

- Batch 5 (AugaSkin 2.0.13): Almanac #5 #15 #17 #29 #46 #47, #25, #42, lever (c) + #49 #52, #31; #6 #7 closed (no own
  art); #4 handed to the AdventureBackpacks fork. Shots `69-almanac`, `68-configmanager` (new), `01-mainmenu`.

- Batch 6 (AugaSkin 2.1.0, Phase 3 of BalanceSim): energy shield (MorgottTweaks player ZDO `mt_es`/`mt_es_max`) as a
  violet rim behind the HP bar (`AugaStatBars`, SE_Shield rim moves out to pad 10 when both show), violet ES strip under
  other players' EnemyHud health (`EnergyShieldHud.cs`), summon frames under the hotkey bar (`SummonFrames.cs`: ZDO
  `mt_sum_owner` == local id, fallback vanilla staff summon following by name; trophy icon, HP bar, stars). Config
  `[Hud] ShowEnergyShield/ShowSummonFrames`, offset `HudLayout.SummonFramesOffset`. Shots `26b-hud-energy-shield`,
  `26c-summon-frames` (new). Unverified: real second player's strip (shot shows the local player on the player HUD).
- State 2026-10-07: no open row left. Open on purpose: #9/#26 (EL rarity slot/recipe art: needs a user design
  decision), #4 (AdventureBackpacks fork's own panel sizing). In-game unverified (code-only): #18 achievement popup,
  #31 build-camera badges, #37 resolution dialog, #38 credits, #39 join code/captions, #45 radial, #6 two-boss stack,
  #53 comparison tooltip, StarLevelSystem startup popups (same Jotunn path as the shot quick-config tool), Almanac
  quest/dialogue/NPC/form windows (checked while inactive). Vanilla WorldVersion dialog keeps UISprite buttons (plain
  Unity sprite, also used by verified build-menu tabs; not mapped globally).

## Cross-cutting levers (do first: one fix covers many surfaces)

- [x] (a) Epic Loot "HasAuga" -- evaluated, no rename. EL 0.14.13 `EpicLoot.HasAuga` is declared (`EpicLoot.cs:65`) and
  never assigned; embedded `Auga\API.cs:106` looks for assembly `Auga` but nothing sets HasAuga from it; every
  `EpicLootAuga` helper (`ReplaceBackground`, `FixItemBG`, `FixFonts`, `MakeSimpleTooltip`) is an empty stub and
  `ReplaceButton` would destroy the button. Renaming AugaSkin back to `Auga` would fix nothing (and LICENSING.md
  keeps the rename). Approach = restyle EL panels from AugaSkin (pattern: `Store_Setup.Show_Finalizer` + `AugaStyle.Restyle`).
- [x] (b) Jotunn `GUIManager` central hook -- DONE 2026-10-05 (AugaSkin 2.0.7, `Auga\Compat\JotunnGui.cs`: prefix+finalizer on every public `Apply*Style`/`Create*` of GUIManager, CreateColor/GradientPicker, MinimapManager.SetupGUI; VNEI bypasses hooked: Styling.ApplyAllComponents, DisplayItem.Awake, RecipeScroll.SpawnRecipe; shots `67-jotunn-gui`,`67b..67e`,`47-vnei`). Was: M. Postfix `GUIManager.ApplyWoodpanelStyle/ApplyButtonStyle/ApplyTextStyle/
  ApplyInputFieldStyle/ApplyToogleStyle/ApplyDropdownStyle/ApplyScrollRectStyle/ApplyScrollbarStyle/ApplySliderStyle`
  (Jotunn `GUIManager.cs:1096-1381`; the `Create*` builders call them) -> Auga art via `AugaStyle`. Callers in pack:
  StarLevelSystem (6 CreateWoodpanel, 5 CreateText, 3 CreateButton, 2 CreateInputField, toggle, slider, scroll view),
  VNEI (ApplyWoodpanel/Button/Text/InputField/Toggle/ScrollRect), EpicLoot (CreateButton, CreateScrollView),
  Jotunn's own windows (mod-compat/version mismatch dialog, keybind/settings). Shot: new `67-jotunn-gui`.
- [x] (c) ConfigurationManager (IMGUI) -- DONE as far as feasible 2026-10-07 (AugaSkin 2.0.13, `Compat\Imgui.cs`: every ConfigurationManagerStyles GUIStyle -> Source Sans Pro after CreateStyles; shot `68-configmanager`). Frame art not feasible (below); colour palette stays its config. Was: M. No uGUI to restyle. Styles static in `ConfigurationManagerStyles.cs:59-95`,
  rebuilt in `CreateStyles` -> postfix can set font/background. Cheap path = 14 `ConfigEntry<Color>` colour settings
  (`ConfigurationManager.cs` ~757; 1x1 `Texture2D` backgrounds `:892-902`, `:2141`) in the pack config (pack/VPS side,
  never local BepInEx\config). IMGUI needs standalone textures (Auga sprites are in atlases). Full Auga frame art not
  feasible. Also covers embedded ItemManager/PieceManager/CreatureManager config tables and EL config drawers.
  Shot: new `68-configmanager` (F1).

## Surfaces

| # | Mod | Surface | Status | Vis | Est | Shot |
|---|-----|---------|--------|-----|-----|------|
| [x] 1 | Epic Loot | Haldor MerchantPanel (stash/gamble/maps/bounties) | done | high | S | `65c-haldor-merchant` |
| [x] 2 | Epic Loot | Hildir TemperPanel | done | high | S | `65b-hildir-temper` |
| [x] 3 | Vanilla | PieceHealth bar (`bar_monster_hp_5` -> Auga `AugaProgressBarBody_Small`, `HudParts.Bars`, 2.0.10) | done | high | S | `28-vanilla-hud` |
| [-] 4 | AdventureBackpacks | backpack panel inherits Auga, but >~8 columns overflow -- SKIPPED here: sizing bug in AB's own panel clone (`Patches\BackpackPanel.cs:104` keeps the container size while `InventoryGrid.cs:9-26` widens the grid), fix belongs in AdventureBackpacks-Fork, not AugaSkin | AB fork | high | S | `46b-backpack-hotkey` |
| [x] 5 | Almanac (fork) | Trophies button icon (Auga cup shown, no Almanac swap visible) | done | high | S | `69-almanac` (`-inventory`) |
| [x] 6 | StarLevelSystem | stacked boss bars -- clones of the Auga-restyled vanilla boss HUD (`UIHudControl.StackBossHuds` only lays them out); no own art; unverified in-game (needs two bosses) | done | high | S | `61-boss-hud` |
| [x] 7 | MonsterModifiers | modifier icons on EnemyHud -- mod content icons on the Auga enemy HUD star slots, no frame art; nothing to restyle | n/a | high | S | `60-creature-levels` |
| [x] 8 | Epic Loot | item tooltips (all vanilla tooltip instances): Auga tooltip frame + fonts via `UITooltip.OnHoverStart` postfix (2.0.11); EL colour tags kept (rarity/effect information, no Auga counterpart) | done | high | S | `65h-el-hudparts` (`65h-el-tooltip`) |
| [-] 9 | Epic Loot | rarity backgrounds on slots -- SKIPPED, needs a design decision: EL's `GenericItemBg` rarity glow is rarity information, not vanilla art; the bundle has no rarity-slot art (only solid `Container_Square_*` shapes) | design? | high | S | `40-inventory` |
| [x] 10 | Epic Loot | AbilityBar (Hud.Awake order) -- `Hud_Awake_ModParts` restyles hudroot children other mods add (2.0.11) | done | high | S | `65h-el-hudparts` |
| [x] 11 | Vanilla | loading/sleep/teleport screen (`Hud.m_loadingScreen` restyled 2.0.10: Auga fonts, divider colour; teleport swirl art + Prstartk text kept, no bundle counterpart in uGUI) | done | high | M | `28-vanilla-hud` |
| [x] 12 | Vanilla | ShipHud art -> bundle ShipHud icons (WindCircle, Ship, WindIndicator, RudderIndicator, rudder, ForwardSlow/Forward/ForwardFast/Backward; `HudParts.Ship`, 2.0.10) | done | high | M | `28-vanilla-hud` |
| [x] 13 | Vanilla | hover text [E] yellow tags -> Auga gold (UpdateCrosshair transpiler, 2.0.10) | done | high | M | `28-vanilla-hud` |
| [x] 14 | VNEI | main window (`BaseUI.cs`, `Styling.cs`; Jotunn-built) | done | high | M (via b) | `47-vnei` |
| [x] 15 | Almanac (fork) | main window (`Compat\Almanac.cs`: InventoryGui.Awake Priority.Last -> RestyleAll on HUD/Almanac*, 2.0.13; search fields on button art -> Auga input) | done | high | L | `69-almanac` |
| [x] 16 | Epic Loot | enchanting table window, all 7 tabs incl. locked (AugaSkin 2.0.8 `Compat\EpicLootEnchanting.cs`: `EnchantingTableUI.Start` postfix -> `AugaStyle.RestyleAll` + list `ElementPrefab`s); small labels fixed 2.0.9 (button art by height: Small/Medium/Fancy, labels grow to the Auga label size) | done | high | L | `65d-el-enchant` |
| [x] 17 | Almanac (fork) | quest tracker HUD (same hook, 2.0.13; checked inactive, unverified on screen) | done | med-high | S | `69-almanac` |
| [x] 18 | Vanilla | AchievementUnlockPopup (`VanillaPanels`: Start postfix, 2.0.12; unverified in-game: no achievement trigger in the autotest) | done | med | S | - |
| [x] 19 | Vanilla | ConnectPanel F2 (+ player row template, `VanillaPanels`, 2.0.12) | done | med | S | `28b-vanilla-panels` |
| [x] 20 | Vanilla | MountHud bars (`bar_gradient_40` -> Auga bar body, 2.0.10) | done | med | S | `28-vanilla-hud`, `62-mount-hud` |
| [x] 21 | Vanilla | stagger / action_progress bar (`bar_stagger` -> Auga bar body, 2.0.10) | done | med | S | `28-vanilla-hud` |
| [x] 22 | Vanilla | KeyHints `key_base` -> Auga keybind backdrop (2.0.10) | done | med | S | `28-vanilla-hud` |
| [x] 23 | Vanilla | EventBar (`point3` -> `darken_blob`, 2.0.10) | done | med | S | `28-vanilla-hud` |
| [x] 24 | StarLevelSystem | level text on minimap/no-map (Jotunn `CreateText`, covered by 2.0.7 JotunnGui; no-map text also by `Hud_Awake_ModParts` 2.0.11) | done | med | S | `20-hud` |
| [x] 25 | DisplayBepInExInfo | main menu text Arial -> Source Sans (FejdStartup.Start Priority.Last restyle, 2.0.13) | done | med | S | `01-mainmenu` |
| [-] 26 | Epic Loot | recipe rarity tint -- SKIPPED with #9 (same `ApplyMagicItemBackgroundToIcon` art, design decision) | design? | med | S | new |
| [x] 27 | Epic Loot | Welcome/ConfigMessage popups (`MessagePanelBase.Awake`/`WelcomeMessage.Awake` postfix -> RestyleAll, 2.0.11) | done | med | S | `65g-el-popups` |
| [x] 28 | Epic Loot | map pin filter / adventure pins -- no uGUI of its own (pins go through vanilla `Minimap.AddPin`, icons are EL content); nothing to restyle | n/a | med | S | - |
| [x] 29 | Almanac (fork) | NPC dialogue panel (same hook, 2.0.13; checked inactive) | done | med | M | `69-almanac` |
| [x] 30 | Jotunn | ModCompatibility window (`ModCompatibility.cs:219-233`) | done | med | M (via b) | `67-jotunn-gui` |
| [x] 31 | ValheimBuildCamera | IMGUI badges -> Source Sans Pro Bold (`DismantlePreview.DrawBadge` postfix, 2.0.13; unverified in-game: dismantle preview not driven) | done | med | M | `21c-buildcamera` |
| [x] 32 | Epic Loot | AugmentChoiceDialog + CraftSuccessDialog (variant-dialog clones; frame/buttons were already Auga, scroll description scrollbar fixed 2.0.9 via `CraftSuccessDialog.ConvertToScrollingDescription` postfix) | done | med | M | `65f-el-augment` |
| [x] 33 | Epic Loot | Compendium MagicPages rows + search field (`MagicPages.Awake`/`OnSelectText` postfixes, 2.0.11) | done | med | M | `12-compendium` (+ `-p2..p8`) |
| [x] 34 | Epic Loot | socket break / chisel prompts (ConfirmPrompt : MessagePanelBase, same hook as #27, 2.0.11) | done | low-med | S | `65h-el-hudparts` |
| [x] 35 | Vanilla | Console F5 (`VanillaPanels`: Console.Awake, 2.0.12) | done | low | S | `33-input-console` |
| [x] 36 | Vanilla | Feedback (`VanillaPanels`: Feedback.Awake, 2.0.12) | done | low | S | `28b-vanilla-panels` |
| [x] 37 | Vanilla | ResolutionSwitchDialog -- child of the Settings prefab (`GraphicsSettings.m_resolutionSwitchDialog`), covered by `Settings_Setup` restyle; unverified in-game (needs a resolution change) | done | low | S | - |
| [x] 38 | Vanilla | EndCredits (`VanillaPanels`: EndCredits.Awake -> m_creditsPanel, 2.0.12; unverified in-game: world object) | done | low | S | - |
| [x] 39 | Vanilla | JoinCode / SessionPlayerList / ClosedCaptions (`VanillaPanels`, 2.0.12; SessionPlayerList shot, JoinCode/ClosedCaptions unverified in-game) | done | low | S | `28b-vanilla-panels` |
| [x] 40 | Vanilla | ManageSavesMenu rows (saveElement template, `VanillaPanels`, 2.0.12) | done | low | S | `28b-vanilla-panels` |
| [x] 41 | Vanilla | Save / BadConnection icons -> bundle art of the same name (2.0.10) | done | low | S | `28-vanilla-hud` |
| [x] 42 | ValheimPlus | version label: Prstartk -> Source Sans, both lines shown right-aligned, moved left of the StarLevelSystem "Mod Config" corner button it sat under (2.0.13) | done | low | S | `01-mainmenu` |
| [x] 43 | Epic Loot | DebugText (`Hud_Awake_ModParts`, 2.0.11) | done | low | S | `65h-el-hudparts` |
| [x] 44 | Epic Loot | extra skill levels -- clones the already-restyled vanilla skill bar, appends a rarity colour tag; nothing vanilla-styled left | done | low | S | `44-skills` |
| [x] 45 | Vanilla | radial menu (`Valheim.UI.RadialBase.Open` -> RestyleAll per open: fonts/known art; its radial art has no Auga counterpart; unverified in-game) | done | low | M | - |
| [x] 46 | Almanac (fork) | form modal (FormPanel._Modal prefab restyled, 2.0.13) | done | low | M | `69-almanac` |
| [x] 47 | Almanac (fork) | NPC customization (same hook, 2.0.13; checked inactive) | done | low | M | `69-almanac` |
| [x] 48 | Jotunn | color/gradient pickers (2.0.9: Cancel/Done take Auga small-button art, narrow inputs keep a text area) | done | low | (via b) | `67b-jotunn-colorpicker`, `67c-jotunn-gradientpicker` |
| [x] 49 | Epic Loot | config drawers (IMGUI; ConfigurationManager styles, via c) | done | low | (via c) | `68-configmanager` |
| [x] 50 | StarLevelSystem | QuickConfigureTool + Mod Config button + startup popups (`ConfigUI.cs`, `QuickConfigBroker.cs:172-215`; Jotunn-built) | done (tool + button shot; popups use the same CreateWoodpanel path, not shot) | ? | (via b) | `67d-sls-quickconfig`, `67-jotunn-gui` |
| [x] 51 | StarLevelSystem | zone-level map overlay panel (`ZoneScaleSystem.cs:190`; Jotunn MinimapManager overlay panel; expanded state shot `67e-map-overlay-open`, Arial toggle labels -> Source Sans 2.0.9) | done | ? | (via b) | `67e-map-overlay` |
| [x] 52 | ConfigurationManager | settings window (IMGUI fonts, via c) | done | ? | M (via c) | `68-configmanager` |
| [x] 53 | Epic Loot | comparison tooltip (child clone inside the tooltip instance, covered by the #8 postfix; unverified in-game: needs an equipped item of the hovered type) | done | ? | S | `65h-el-hudparts` |
| [x] 54 | Epic Loot | scrollbar handles faint (item_background -> backdrop on backdrop track; from item 1) -- 2.0.9 trader panels go through `RestyleAll` (Auga scrollbar) | done | ? | ? | `65c-haldor-merchant` |
| [-] 55 | BossAwakening | world boss bar `BA_WorldBossBar` (own art `BossBarAssets.cs:11`; font copied `BossBarHud.cs:334`) -- **WON'T DO: user 2026-10-05 keeps the mod's own design (exempt from Auga scope)** | - | - | - | `77-boss-bar` |

## Verify only (likely inherit Auga)

- BalrondSecondChance downed popup; Warfare build tab; V+ mute toggle; EquipmentAndQuickSlots quick-slot hotbar
  (`45-inventory-worn`, `20-hud`); Jotunn key hints; CurrencyPocket (emoji label may not render; `40-inventory`);
  StarLevelSystem stars; VNEI tab button.
- Not covered by new inventory, still verify: MultiUserChest in-use message / chest UI (`42-container`); Seasonality
  config drawer + season HUD image; ValheimPlus HUD features (required items, XP notes, bow ammo); Armory/Wizardry/
  OreMines config drawers / messages.
- Unconfirmed: chat world-text / NPC bubbles (`Chat.cs:383/692`); large-map pin labels (`Minimap.cs:876`).

No UI found: Advize_PlantEverything, BlacksmithTools, BoneAppetit, ChaosArmor, ConditionalConfigSync, HoneyPlus,
SolidHitboxes, JsonDotNET, YamlDotNet, SmoothRegen, FastStartup, Skidbladnir, SeedBed, MorgottTweaks.
Messages of all our mods go via MessageHud (styled).
