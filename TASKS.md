# Auga-everywhere backlog (scope rule: every UI, vanilla + pack mods, Auga-styled)

Phase 1 audit 2026-10-04; full inventory merged 2026-10-04 (AugaSkin vs decompile comparison, code-verified, in-game
unverified unless stated). Order = visibility (high first), then size. Size S/M/L = approach estimate, not time.
Shot = autotest `-Shots` name (`tools\AutotestDriver`); "new" = still to write. Decompiles used:
`E:\Temp\claude\E--DEV-Valheim-Auga-Fork\c742cfb3-f750-4393-bdf4-89d49021f3f0\scratchpad\decomp\<mod>` (temporary).
Status "verify" = mod has UI code, AugaSkin has no code for it, look not yet checked in game.
Root cause of vanilla gaps: `AugaStyle.Restyle` only maps known sprites + fonts; runtime-instantiated prefabs miss restyle.
NO = no Auga styling; PARTIAL = partly styled.

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
| [ ] 3 | Vanilla | PieceHealth bar (`bar_monster_hp_5`) | PARTIAL | high | S | new `28-vanilla-misc` |
| [ ] 4 | AdventureBackpacks | backpack panel inherits Auga, but >~8 columns overflow (its `HANDOFF.md:361`) | PARTIAL | high | S | `46b-backpack-hotkey` |
| [ ] 5 | Almanac (fork) | Trophies button icon | NO | high | S | new `69-almanac` |
| [ ] 6 | StarLevelSystem | stacked boss bars | PARTIAL | high | S | new |
| [ ] 7 | MonsterModifiers | modifier icons on EnemyHud (own sprites) | PARTIAL | high | S | `60-creature-levels` |
| [ ] 8 | Epic Loot | tooltip text colours | NO | high | S | new `65e-el-tooltip` |
| [ ] 9 | Epic Loot | rarity backgrounds on slots | NO | high | S | `40-inventory` |
| [ ] 10 | Epic Loot | AbilityBar (Hud.Awake order) | NO | high | S | `20-hud` |
| [ ] 11 | Vanilla | loading/sleep/teleport screen (`Hud.m_loadingScreen`, `m_sleepingProgress`, `m_teleportingProgress`, `Hud.cs:219-231`, outside hudroot) | NO | high | M | new |
| [ ] 12 | Vanilla | ShipHud art (`ship_circle`, `rudder_arrow`, `windicon`; only made movable `Hud_Setup.cs:40`) | NO | high | M | new |
| [ ] 13 | Vanilla | hover text [E] yellow tags (only UpdateBuild recoloured `Hud_Setup.cs:89-99`) | PARTIAL | high | M | `20-hud` |
| [x] 14 | VNEI | main window (`BaseUI.cs`, `Styling.cs`; Jotunn-built) | done | high | M (via b) | `47-vnei` |
| [ ] 15 | Almanac (fork) | main window (bundle AlmanacUI); partial by accident (`UI_Patches.cs:71-220` copies vanilla sprites, order-dependent) | PARTIAL | high | L | new `69-almanac` |
| [x] 16 | Epic Loot | enchanting table window, all 7 tabs incl. locked (AugaSkin 2.0.8 `Compat\EpicLootEnchanting.cs`: `EnchantingTableUI.Start` postfix -> `AugaStyle.RestyleAll` + list `ElementPrefab`s); left: Norsebold tab/mode-toggle labels render small | done | high | L | `65d-el-enchant` |
| [ ] 17 | Almanac (fork) | quest tracker HUD | NO | med-high | S | new |
| [ ] 18 | Vanilla | AchievementUnlockPopup (`Achievements.cs:165`) | NO | med | S | new |
| [ ] 19 | Vanilla | ConnectPanel F2 (`ConnectPanel.cs:8,166`) | NO | med | S | new `28-vanilla-misc` |
| [ ] 20 | Vanilla | MountHud bars (`bar_gradient_40`) | PARTIAL | med | S | new |
| [ ] 21 | Vanilla | stagger / action_progress bar (`bar_stagger`) | PARTIAL | med | S | new |
| [ ] 22 | Vanilla | KeyHints `key_base` x47 | PARTIAL | med | S | `20-hud` |
| [ ] 23 | Vanilla | EventBar (`point3`) | PARTIAL | med | S | new |
| [ ] 24 | StarLevelSystem | level text on minimap/no-map (`NoMapLevelIndicator.cs:31`, `MinimapLevelIndicator.cs:34`) | PARTIAL | med | S | new |
| [ ] 25 | DisplayBepInExInfo | main menu text Arial (`DisplayInfoPlugin.cs:34-69`, created at FejdStartup.Start after Auga restyle) | NO | med | S | new |
| [ ] 26 | Epic Loot | recipe rarity tint | NO | med | S | new |
| [ ] 27 | Epic Loot | Welcome/ConfigMessage popups at FejdStartup.Start | NO | med | S | new |
| [ ] 28 | Epic Loot | map pin filter / adventure pins | NO | med | S | new |
| [ ] 29 | Almanac (fork) | NPC dialogue panel | NO | med | M | new |
| [x] 30 | Jotunn | ModCompatibility window (`ModCompatibility.cs:219-233`) | done | med | M (via b) | `67-jotunn-gui` |
| [ ] 31 | ValheimBuildCamera | IMGUI labels (`DismantlePreview.cs:560-647`) | NO | med | M | `21c-buildcamera` |
| [ ] 32 | Epic Loot | AugmentChoiceDialog | NO | med | M | new `65d-el-enchant` |
| [ ] 33 | Epic Loot | Compendium MagicPages rows (created after Auga restyle; `PauseMenu_Setup.cs:91-95`) | NO | med | M | `12-compendium` |
| [ ] 34 | Epic Loot | socket break / chisel prompts | NO | low-med | S | new |
| [ ] 35 | Vanilla | Console F5 (Terminal) | NO | low | S | new |
| [ ] 36 | Vanilla | Feedback (`Menu.cs:582`) | NO | low | S | new |
| [ ] 37 | Vanilla | ResolutionSwitchDialog | NO | low | S | new |
| [ ] 38 | Vanilla | EndCredits | NO | low | S | new |
| [ ] 39 | Vanilla | JoinCode / SessionPlayerList / ClosedCaptions | NO | low | S | new |
| [ ] 40 | Vanilla | ManageSavesMenu rows (saveElement `ManageSavesMenu.cs:657`) | PARTIAL | low | S | new |
| [ ] 41 | Vanilla | Save / BadConnection icons | PARTIAL | low | S | new |
| [ ] 42 | ValheimPlus | version label font/pos override | NO | low | S | new |
| [ ] 43 | Epic Loot | DebugText | NO | low | S | new |
| [ ] 44 | Epic Loot | extra skill levels | NO | low | S | new |
| [ ] 45 | Vanilla | ValheimRadial | PARTIAL | low | M | new |
| [ ] 46 | Almanac (fork) | form modal | NO | low | M | new |
| [ ] 47 | Almanac (fork) | NPC customization | NO | low | M | new |
| [x] 48 | Jotunn | color/gradient pickers | done | low | (via b) | `67b-jotunn-colorpicker`, `67c-jotunn-gradientpicker` |
| [ ] 49 | Epic Loot | config drawers (IMGUI) | NO | low | (via c) | `68-configmanager` |
| [x] 50 | StarLevelSystem | QuickConfigureTool + Mod Config button + startup popups (`ConfigUI.cs`, `QuickConfigBroker.cs:172-215`; Jotunn-built) | done (tool + button shot; popups use the same CreateWoodpanel path, not shot) | ? | (via b) | `67d-sls-quickconfig`, `67-jotunn-gui` |
| [x] 51 | StarLevelSystem | zone-level map overlay panel (`ZoneScaleSystem.cs:190`; Jotunn MinimapManager overlay panel) | done | ? | (via b) | `67e-map-overlay` |
| [ ] 52 | ConfigurationManager | settings window (IMGUI) | NO | ? | M (via c) | `68-configmanager` |
| [ ] 53 | Epic Loot | comparison tooltip | verify | ? | S | new `65e-el-tooltip` |
| [ ] 54 | Epic Loot | scrollbar handles faint (item_background -> backdrop on backdrop track; from item 1) | PARTIAL | ? | ? | `65c-haldor-merchant` |
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
