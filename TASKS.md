# Auga-everywhere backlog (scope rule: every UI, vanilla + pack mods, Auga-styled)

Phase 1 audit 2026-10-04. Order = player visibility/impact. Size S/M/L = approach estimate, not time.
Shot = autotest `-Shots` name (`tools\AutotestDriver`); "new" = still to write. Decompiles used:
`E:\Temp\claude\E--DEV-Valheim-Auga-Fork\c742cfb3-f750-4393-bdf4-89d49021f3f0\scratchpad\decomp\<mod>` (temporary).
Status "verify" = mod has UI code, AugaSkin has no code for it, look not yet checked in game.

## Cross-cutting levers (do first: one fix covers many surfaces)

- [x] (a) Epic Loot "HasAuga" -- evaluated, no rename. EL 0.14.13 `EpicLoot.HasAuga` is declared (`EpicLoot.cs:65`) and
  never assigned; embedded `Auga\API.cs:106` looks for assembly `Auga` but nothing sets HasAuga from it; every
  `EpicLootAuga` helper (`ReplaceBackground`, `FixItemBG`, `FixFonts`, `MakeSimpleTooltip`) is an empty stub and
  `ReplaceButton` would destroy the button. Renaming AugaSkin back to `Auga` would fix nothing (and LICENSING.md
  keeps the rename). Approach = restyle EL panels from AugaSkin (pattern: `Store_Setup.Show_Finalizer` + `AugaStyle.Restyle`).
- [ ] (b) Jotunn `GUIManager` central hook -- M. Postfix `GUIManager.ApplyWoodpanelStyle/ApplyButtonStyle/ApplyTextStyle/
  ApplyInputFieldStyle/ApplyToogleStyle/ApplyDropdownStyle/ApplyScrollRectStyle/ApplyScrollbarStyle/ApplySliderStyle`
  (Jotunn `GUIManager.cs:1096-1381`; the `Create*` builders call them) -> Auga art via `AugaStyle`. Callers in pack:
  StarLevelSystem (6 CreateWoodpanel, 5 CreateText, 3 CreateButton, 2 CreateInputField, toggle, slider, scroll view),
  VNEI (ApplyWoodpanel/Button/Text/InputField/Toggle/ScrollRect), EpicLoot (CreateButton, CreateScrollView),
  Jotunn's own windows (mod-compat/version mismatch dialog, keybind/settings). Shot: new `67-jotunn-gui`.
- [ ] (c) ConfigurationManager (IMGUI) -- M. No uGUI to restyle. Its look is 14 `ConfigEntry<Color>` (window/header/
  entry/tooltip/widget backgrounds, font colours; `ConfigurationManager.cs` ~757) + 1x1 `Texture2D` backgrounds
  (`:892-902`, `:2141`). Feasible: Auga palette via those colours in the pack config (pack/VPS side, never local
  BepInEx\config) + AugaSkin prefix on its `OnGUI` swapping `GUI.skin.font` to Auga's legacy SourceSansPro and window
  texture to an Auga backdrop. Full Auga frame art not feasible in IMGUI. Shot: new `68-configmanager` (F1).

## Surfaces

| # | Mod | Surface | Open | Est | Shot |
|---|-----|---------|------|-----|------|
| [x] 1 | Epic Loot | Haldor MerchantPanel (stash/gamble/maps/bounties) | Haldor -> trade | S | `65c-haldor-merchant` |
| [x] 2 | Epic Loot | Hildir TemperPanel | Hildir -> trade | S | `65b-hildir-temper` |
| [ ] 3 | Epic Loot | Enchanting table UI (sacrifice/enchant/augment/convert tabs) | use enchanting table | L | new `65d-el-enchant` |
| [ ] 4 | Epic Loot | magic item tooltip + crafting-tab MagicSearchField | hover magic item / workbench | M | new `65e-el-tooltip` |
| [ ] 5 | Epic Loot | bounty/treasure-map HUD, Compendium pages (ExplainPage, SetInfos ...) | accept bounty / Compendium | M | `12-compendium` + new |
| [ ] 6 | StarLevelSystem | in-game config/level UI (Jotunn woodpanels) + EnemyHud stars | its hotkey / any creature | M (via b) | `60-creature-levels` + new |
| [ ] 7 | VNEI | item browser panel + crafting tab | inventory | M (via b) | new `47-vnei` |
| [ ] 8 | ConfigurationManager | settings window (IMGUI) | F1 | M (via c) | new `68-configmanager` |
| [ ] 9 | EquipmentAndQuickSlots | equipment + quick-slot panels, hotbar | inventory / HUD | S verify (Auga has 26 refs) | `45-inventory-worn`, `20-hud` |
| [ ] 10 | AdventureBackpacks | backpack container panel, durability bar | open backpack | S verify | `46b-backpack-hotkey` + PNG |
| [ ] 11 | Almanac (fork) | Almanac panel (bundle UI) | its button/hotkey | L | new `69-almanac` |
| [ ] 12 | BossAwakening | boss HP bar -- **pending user decision (own design vs Auga)** | boss fight | S-M | `77-boss-bar` |
| [ ] 13 | MonsterModifiers | modifier icons on EnemyHud (Jotunn) | creature with modifiers | S verify | `60-creature-levels` |
| [ ] 14 | CurrencyPocket | coin pocket UI element (Jotunn + image) | inventory | S verify | `40-inventory` |
| [ ] 15 | MorgottTweaks | module UI (tames hover, meads HUD, hildir) | per module | S verify | `66-meads-hud` |
| [ ] 16 | Skidbladnir | network/graphics HUD or menu entries | settings / HUD | S verify | new |
| [ ] 17 | ValheimPlus | enabled HUD features (required items, XP notes, bow ammo) | per cfg | S verify | `20-hud` |
| [ ] 18 | MultiUserChest | in-use message / chest UI | shared chest | S verify | `42-container` |
| [ ] 19 | Seasonality | config drawer (GUILayout) + season HUD image | CM / HUD | S verify | new |
| [ ] 20 | ValheimBuildCamera | IMGUI overlay | F6 in build | S (IMGUI) | `21c-buildcamera` |
| [ ] 21 | Vanilla | Ship HUD, piece health bar, F2 connection panel, loading indicator, changelog/feedback | sail / hammer hover / F2 | S each, verify | new `28-vanilla-misc` |
| [ ] 22 | Armory/Wizardry/Warfare/OreMines/BalrondSecondChance/SeedBed/SmoothRegen | config drawers / messages only (likely) | CM | S verify | - |

No UI found: Advize_PlantEverything, BlacksmithTools, BoneAppetit, ChaosArmor, ConditionalConfigSync, HoneyPlus,
SolidHitboxes, JsonDotNET, YamlDotNet.

Open from item 1: EL scrollbar handles (item_background -> backdrop) faint on backdrop track.
