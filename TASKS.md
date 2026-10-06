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
- [ ] (c) ConfigurationManager (IMGUI) -- M. No uGUI to restyle. Styles static in `ConfigurationManagerStyles.cs:59-95`,
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
| [ ] 4 | AdventureBackpacks | backpack panel inherits Auga, but >~8 columns overflow (its `HANDOFF.md:361`) | PARTIAL | high | S | `46b-backpack-hotkey` |
| [ ] 5 | Almanac (fork) | Trophies button icon | NO | high | S | new `69-almanac` |
| [ ] 6 | StarLevelSystem | stacked boss bars | PARTIAL | high | S | new |
| [ ] 7 | MonsterModifiers | modifier icons on EnemyHud (own sprites) | PARTIAL | high | S | `60-creature-levels` |
| [x] 8 | Epic Loot | item tooltips (all vanilla tooltip instances): Auga tooltip frame + fonts via `UITooltip.OnHoverStart` postfix (2.0.11); EL colour tags kept (rarity/effect information, no Auga counterpart) | done | high | S | `65h-el-hudparts` (`65h-el-tooltip`) |
| [-] 9 | Epic Loot | rarity backgrounds on slots -- SKIPPED, needs a design decision: EL's `GenericItemBg` rarity glow is rarity information, not vanilla art; the bundle has no rarity-slot art (only solid `Container_Square_*` shapes) | design? | high | S | `40-inventory` |
| [x] 10 | Epic Loot | AbilityBar (Hud.Awake order) -- `Hud_Awake_ModParts` restyles hudroot children other mods add (2.0.11) | done | high | S | `65h-el-hudparts` |
| [x] 11 | Vanilla | loading/sleep/teleport screen (`Hud.m_loadingScreen` restyled 2.0.10: Auga fonts, divider colour; teleport swirl art + Prstartk text kept, no bundle counterpart in uGUI) | done | high | M | `28-vanilla-hud` |
| [x] 12 | Vanilla | ShipHud art -> bundle ShipHud icons (WindCircle, Ship, WindIndicator, RudderIndicator, rudder, ForwardSlow/Forward/ForwardFast/Backward; `HudParts.Ship`, 2.0.10) | done | high | M | `28-vanilla-hud` |
| [x] 13 | Vanilla | hover text [E] yellow tags -> Auga gold (UpdateCrosshair transpiler, 2.0.10) | done | high | M | `28-vanilla-hud` |
| [x] 14 | VNEI | main window (`BaseUI.cs`, `Styling.cs`; Jotunn-built) | done | high | M (via b) | `47-vnei` |
| [ ] 15 | Almanac (fork) | main window (bundle AlmanacUI); partial by accident (`UI_Patches.cs:71-220` copies vanilla sprites, order-dependent) | PARTIAL | high | L | new `69-almanac` |
| [x] 16 | Epic Loot | enchanting table window, all 7 tabs incl. locked (AugaSkin 2.0.8 `Compat\EpicLootEnchanting.cs`: `EnchantingTableUI.Start` postfix -> `AugaStyle.RestyleAll` + list `ElementPrefab`s); small labels fixed 2.0.9 (button art by height: Small/Medium/Fancy, labels grow to the Auga label size) | done | high | L | `65d-el-enchant` |
| [ ] 17 | Almanac (fork) | quest tracker HUD | NO | med-high | S | new |
| [ ] 18 | Vanilla | AchievementUnlockPopup (`Achievements.cs:165`) | NO | med | S | new |
| [ ] 19 | Vanilla | ConnectPanel F2 (`ConnectPanel.cs:8,166`) | NO | med | S | new `28-vanilla-misc` |
| [x] 20 | Vanilla | MountHud bars (`bar_gradient_40` -> Auga bar body, 2.0.10) | done | med | S | `28-vanilla-hud`, `62-mount-hud` |
| [x] 21 | Vanilla | stagger / action_progress bar (`bar_stagger` -> Auga bar body, 2.0.10) | done | med | S | `28-vanilla-hud` |
| [x] 22 | Vanilla | KeyHints `key_base` -> Auga keybind backdrop (2.0.10) | done | med | S | `28-vanilla-hud` |
| [x] 23 | Vanilla | EventBar (`point3` -> `darken_blob`, 2.0.10) | done | med | S | `28-vanilla-hud` |
| [x] 24 | StarLevelSystem | level text on minimap/no-map (Jotunn `CreateText`, covered by 2.0.7 JotunnGui; no-map text also by `Hud_Awake_ModParts` 2.0.11) | done | med | S | `20-hud` |
| [ ] 25 | DisplayBepInExInfo | main menu text Arial (`DisplayInfoPlugin.cs:34-69`, created at FejdStartup.Start after Auga restyle) | NO | med | S | new |
| [-] 26 | Epic Loot | recipe rarity tint -- SKIPPED with #9 (same `ApplyMagicItemBackgroundToIcon` art, design decision) | design? | med | S | new |
| [x] 27 | Epic Loot | Welcome/ConfigMessage popups (`MessagePanelBase.Awake`/`WelcomeMessage.Awake` postfix -> RestyleAll, 2.0.11) | done | med | S | `65g-el-popups` |
| [x] 28 | Epic Loot | map pin filter / adventure pins -- no uGUI of its own (pins go through vanilla `Minimap.AddPin`, icons are EL content); nothing to restyle | n/a | med | S | - |
| [ ] 29 | Almanac (fork) | NPC dialogue panel | NO | med | M | new |
| [x] 30 | Jotunn | ModCompatibility window (`ModCompatibility.cs:219-233`) | done | med | M (via b) | `67-jotunn-gui` |
| [ ] 31 | ValheimBuildCamera | IMGUI labels (`DismantlePreview.cs:560-647`) | NO | med | M | `21c-buildcamera` |
| [x] 32 | Epic Loot | AugmentChoiceDialog + CraftSuccessDialog (variant-dialog clones; frame/buttons were already Auga, scroll description scrollbar fixed 2.0.9 via `CraftSuccessDialog.ConvertToScrollingDescription` postfix) | done | med | M | `65f-el-augment` |
| [x] 33 | Epic Loot | Compendium MagicPages rows + search field (`MagicPages.Awake`/`OnSelectText` postfixes, 2.0.11) | done | med | M | `12-compendium` (+ `-p2..p8`) |
| [x] 34 | Epic Loot | socket break / chisel prompts (ConfirmPrompt : MessagePanelBase, same hook as #27, 2.0.11) | done | low-med | S | `65h-el-hudparts` |
| [ ] 35 | Vanilla | Console F5 (Terminal) | NO | low | S | new |
| [ ] 36 | Vanilla | Feedback (`Menu.cs:582`) | NO | low | S | new |
| [ ] 37 | Vanilla | ResolutionSwitchDialog | NO | low | S | new |
| [ ] 38 | Vanilla | EndCredits | NO | low | S | new |
| [ ] 39 | Vanilla | JoinCode / SessionPlayerList / ClosedCaptions | NO | low | S | new |
| [ ] 40 | Vanilla | ManageSavesMenu rows (saveElement `ManageSavesMenu.cs:657`) | PARTIAL | low | S | new |
| [x] 41 | Vanilla | Save / BadConnection icons -> bundle art of the same name (2.0.10) | done | low | S | `28-vanilla-hud` |
| [ ] 42 | ValheimPlus | version label font/pos override | NO | low | S | new |
| [x] 43 | Epic Loot | DebugText (`Hud_Awake_ModParts`, 2.0.11) | done | low | S | `65h-el-hudparts` |
| [x] 44 | Epic Loot | extra skill levels -- clones the already-restyled vanilla skill bar, appends a rarity colour tag; nothing vanilla-styled left | done | low | S | `44-skills` |
| [ ] 45 | Vanilla | ValheimRadial | PARTIAL | low | M | new |
| [ ] 46 | Almanac (fork) | form modal | NO | low | M | new |
| [ ] 47 | Almanac (fork) | NPC customization | NO | low | M | new |
| [x] 48 | Jotunn | color/gradient pickers (2.0.9: Cancel/Done take Auga small-button art, narrow inputs keep a text area) | done | low | (via b) | `67b-jotunn-colorpicker`, `67c-jotunn-gradientpicker` |
| [ ] 49 | Epic Loot | config drawers (IMGUI) | NO | low | (via c) | `68-configmanager` |
| [x] 50 | StarLevelSystem | QuickConfigureTool + Mod Config button + startup popups (`ConfigUI.cs`, `QuickConfigBroker.cs:172-215`; Jotunn-built) | done (tool + button shot; popups use the same CreateWoodpanel path, not shot) | ? | (via b) | `67d-sls-quickconfig`, `67-jotunn-gui` |
| [x] 51 | StarLevelSystem | zone-level map overlay panel (`ZoneScaleSystem.cs:190`; Jotunn MinimapManager overlay panel; expanded state shot `67e-map-overlay-open`, Arial toggle labels -> Source Sans 2.0.9) | done | ? | (via b) | `67e-map-overlay` |
| [ ] 52 | ConfigurationManager | settings window (IMGUI) | NO | ? | M (via c) | `68-configmanager` |
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
