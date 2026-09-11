# Auga native rework: audit and design (2026-09-11)

Status: Phases 0-3 done (2026-09-11). Phase 3: vanilla Hud, Minimap and build menu restyled in place, the Auga HUD/minimap/build-menu replacements deleted. Crutches removed so far: 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 20, 22, 23, 24, 26, 30, 31 (HUD part), 32, 33, 34, 35. Phases 4-6 are open. See HANDOFF.md. Target: Valheim 1.0.12 (Unity 6000.0.75, network 40), BepInEx 5.4.23.5.
Evidence: user run log `E:\Temp\claude\E--DEV-Valheim\0fc13589-0331-40cf-93b9-19a3a72bcd1a\scratchpad\user-run3-LogOutput.log`
(copy of `BepInEx\LogOutput.log`, deployed Auga.dll `082869AD...`), the 1.0.12 decompile in `E:\DEV\Valheim\ValheimDecompiled\assembly_valheim\`,
UnityPy 1.25 dumps of `AugaUnity\AssetBundles\augaassets` and the 1.0.12 main scene (`valheim_Data\StreamingAssets\SoftRef\Bundles\17245031`),
and EAQS 3.1.1 decompiled with ilspycmd.

## Goal (user)

- Auga fonts and style on the pause menu (Esc), Settings, and all pre-world menus (main menu, character select/create, start/join).
- Everything in-game works. No UI overlaps another UI. No errors or warnings.
- Native: as close to the game's own code as possible. No crutches (dummy objects for dead fields, skip-guards,
  hide-and-hope, post-hoc resizing). Auga may be reworked from scratch where needed.

## 1. Log triage (run 3)

No errors or exceptions in the run. 39 warnings:

| Group | Count | Source | Root cause | Owner |
|---|---|---|---|---|
| `[Auga] Esc: ...` | 7 | `PauseMenu_Setup.TraceEsc` (`Auga.cs:657`) | Diagnostic we added (config `TraceInput`). Remove after the Esc fix. | Auga |
| `Starting Auga InventoryGui.Postfix`, `API AAA/InputAmount is null` | 4 | `PlayerInventory_Setup.cs:24,83-85` | Leftover debug output, logged as warnings | Auga |
| `ACP is Awake`, `AAA/InputAmount/CraftButton is null` | 4 | `AugaUnityLib\AugaCraftingControls.cs:30-33` | Leftover debug output, logged as warnings | Auga |
| `[Auga] <X>: fields point to destroyed objects` | 3 | `SetupHelper.LogDeadRefs` | Diagnostic for real dead vanilla refs, see §2.3 | Auga |
| `Fishlabs.GuiInputField` missing / `referenced script ... missing` | 5 (2 at plugin load, 3 on world load incl. `ChatInput`) | bundle prefabs `AugaChat/Chat_box/ChatInput` + 1 unnamed | The bundle still serializes `Fishlabs.GuiInputField` (ui_lib.dll, gone since 1.0.7). `Chat_Setup.FixChatInput` adds a `GUIFramework.GuiInputField` beside the missing one, but the missing script stays in the bundle | Auga (bundle) |
| Jotunn `Ambiguous asset name` / `MockManager Dictionary` | 8 + 3 | Jotunn 2.30.0 | Third-party, unrelated to Auga | Jotunn |
| `newer version available of ValheimPlus` | 1 | Valheim Plus | Update notice | V+ |
| `Only custom filters ... Amb_MainMenu`, `Missing audio clip in music respawn`, `Placed 0/1-2 of 'Fortress Mountain'`, `Character ID ... was 0:0` | 4 | vanilla | Vanilla game warnings; also present without mods | vanilla |

Auga owns 23 of the 39 warnings. Every one of them is fixable at the source. The other 16 come from the game or other mods, so "no warnings" can only cover Auga's own log lines (decision D4).

### 1.1 Esc: pause menu opens but is invisible

The trace shows the menu logic works:
- Log line 378: `root=True hiddenFrames=1717`. Vanilla `Menu.Update` is in its hidden branch (`Menu.cs:384-392`). All `flag` terms are false and `m_hiddenFrames > 1`, so it calls `Show()` (`Menu.cs:391`), which activates `m_root` (`Menu.cs:224`) and pauses the game (`Menu.cs:229-232`). `TraceEsc` runs later in the same frame, before `m_hiddenFrames` is reset (`Menu.cs:326`).
- Log line 379: `root=False hiddenFrames=0`. The next Esc goes through the shown branch and calls `Hide()` (`Menu.cs:328-347`).
- Log lines 401-402: Esc while the inventory or map is open is blocked by `flag` (`Menu.cs:388`). That is vanilla behaviour.

So the menu is open and the game is paused, but nothing is drawn. The cause is in the scene. In 1.0.12 the vanilla `Menu` GameObject carries its own `Canvas` (override sorting, order 1700). Its parents `IngameGui`, `PixelFix` and `LoadingGUI` (under `_GameMain`) have no Canvas.
The AugaMenu prefab (root: `Menu` + `Localize`, `MenuRoot`: `CanvasGroup` a=1 + `UIGroupHandler`) has no Canvas either.
`Menu_Start_Patch` (`PauseMenu_Setup.cs:270-277`) instantiates AugaMenu as a sibling under `IngameGui` and destroys the vanilla `Menu` along with its Canvas. That leaves the Auga menu outside any Canvas: it never renders and never gets raycasts.
The Settings window is instantiated under `base.transform` (`Menu.cs:414`), which is the AugaMenu root, so it has the same problem unless its own prefab root carries a Canvas.

No input or condition is at fault. This is a structural bug from replacing the object, and restyling the vanilla `Menu` in place fixes it (§3).

### 1.2 EAQS slots offset from the Auga paper doll

EAQS 3.1.1 `AugaPanel.UpdatePanel` (decompiled `AugaPanel.cs:101-113`) calls `Auga.API.Panel_Create(m_player, (255,352))`. `Panel_Create` sets pivot and anchors to (0.5,0.5) (`Auga\API.cs:109`). EAQS then sets only the anchors to (0,1) and `anchoredPosition = (752,-166)`, and leaves the pivot at the centre.
Its slot positions assume a top-left pivot. `GetSlotPosition` = `panelBase + equipClusterCenter + equipPositions[i]` (`AugaPanel.cs:63-79,170-182`). The cells live in `EaqsSlotRoot`, which EAQS places at `m_gridRoot`'s reference point with `m_gridRoot`'s pivot (`EquipmentPanel.cs:611-631`).
Rects from the dump (m_player space):
- The panel centre is at (752,-166), so the panel art spans x 624.5..879.5 and y +10..-342.
- `EaqsSlotRoot` is at (4,-4), pivot (0,1).
- The cells sit where a top-left panel at (752,-166) would put them, e.g. the helmet at (866.5,-227). That is +4,-4 from the slot root.

Total offset between cells and art: (+127.5+4, -176-4) = **(+131.5, -180)**, i.e. half the panel size plus the slot-root inset.
The pivot contract bug is in EAQS. Auga's `Panel_Create` has centred the pivot since upstream 2021, and EpicLoot also calls it (1 call site).
- Native fix: EAQS sets `pivot = (0,1)` together with the anchors, and parents its cells under the panel instead of an `m_gridRoot`-relative root.
- Changing `Panel_Create`'s pivot globally would break other consumers.

The other half of "one interface layered over another" in the inventory is the misaligned art itself: EAQS cells sit on the Auga middle panel while the EAQS panel art sits 131px left and 180px up.

### 1.3 Where the overlap comes from (static audit)

- Vanilla objects passed through with vanilla anchors inside Auga-laid-out screens:
  - inventory `root/Info` (`PlayerInventory_Setup.cs:129-138`)
  - Hud `GuardianPower`, `EventBar`, `LoadingBlack` and `KeyHints` (`Hud_Setup.cs:56-59,198-200`)
  - mount panel and adrenaline bar (`Hud_Setup.cs:82-93`)
  - pause-menu invite, gamepad, cloud-storage and last-save objects adopted into Auga's MenuEntries (`PauseMenu_Setup.cs:305-357`)
- Layers stacked on purpose:
  - Settings: `AugaPanelBase` behind the vanilla tabs, while the vanilla `TabContent` interior background stays (`Settings_Setup.cs:24-32`).
  - Store: a second `StoreGui` is instantiated and the vanilla one is deactivated but kept (`Store_Setup.cs:14-50`).
- Misaligned third-party content: EAQS (§1.2).
- Runtime resize: `FitPlayerPanel` grows `m_player` after layout (`PlayerInventory_Setup.cs:209-219`), which moves everything anchored to it.
- Confirm with the runtime overlap check in Phase 0 before fixing. The list above is a static reading.

## 2. Architecture audit

### 2.1 Integration pattern today

Auga was written for Valheim 0.21x. It works in three ways:
- **Replace:** destroy the vanilla GameObject, instantiate a bundle prefab, then re-point vanilla fields by `Find(path)` (`Extensions.Replace`, `SetupHelper.DirectObjectReplace`, `IndirectTwoObjectReplace`).
- **Skip:** prefixes that `return false` so the vanilla update never touches the replaced objects.
- **Pass through:** vanilla objects that have no Auga equivalent stay where the game put them.

Since 1.0.x, the game's UI components have gained many fields that the 0.21x prefabs do not have. Each gap was patched with a dummy, a guard, or by passing the vanilla object through. That is where the dead refs, invisible menus, and overlaps come from.

| Screen / patch | File | Integration | Breaks in 1.0.12 |
|---|---|---|---|
| Pause menu | `PauseMenu_Setup.cs:259-399` | Replace whole `Menu` GO with `AugaMenu`; wire 1.0.7 fields by path; adopt vanilla invite/gamepad/cloud/lastSave/quit/logout | Loses vanilla Canvas + raycaster (§1.1); `UpdateNavigation` fully replaced by transpiler + try/catch (`:132-257`) |
| TextsDialog | `PauseMenu_Setup.cs:30-130,415-460` | `Update` and `ShowText` replaced by transpiler; `AddActiveEffects`/`AddLog` skipped | Vanilla texts features lost (active effects, log) |
| Settings | `Settings_Setup.cs:11-33` | Vanilla window, `AugaPanelBase` inserted behind, wood image disabled | Inner vanilla backgrounds stay (layered look) |
| Key binding display | `Settings_Setup.cs:43-119` | Full reimplementation of `AugaBindingDisplay.SetBinding` by reflection | Patches Auga's own lib instead of fixing it |
| Main menu (FejdStartup) | `MainMenu_Setup.cs:11-55` | Vanilla; Norsebold TMP on button labels; cinematics button hidden | Adds vanilla fonts to the bundle font's fallback table at runtime |
| Hud | `Hud_Setup.cs:19-227` | Replace HotKeyBar, StatusEffects, SaveIcon, BadConnection, Damaged, crosshair, action_progress, staggerpanel, ShipHud; destroy vanilla health/stamina/eitr; CopyOver Auga bars; null vanilla bar fields | 7 skip prefixes (`:229-276`); dead `m_foodIcon`, `m_statusEffectListRoot`, `m_statusEffectTemplate` (used at `Hud.cs:385,1651`); rudder fields point at one `Dummy` (`:205-206`) |
| Build menu | `Hud_Setup.cs:147-196`, `Auga.cs:122` | Disabled: `UseAugaBuildMenu => false` | Whole Auga build menu dead code |
| Inventory / crafting | `PlayerInventory_Setup.cs:18-191` | Replace `root/Player`, `root/Container`, `root/VariantDialog`, `root/Skills`, `root/SplitDialog`; destroy `root/Crafting`; instantiate Auga `RightPanel`; wire grid delegates by hand | Dead `m_upgradeItem*`, `m_qualityLevel*`, `m_touchSplitAnchor` (used at `InventoryGui.cs:786,1690-1710`); weight label never updated (prefix `:364-369` skips `InventoryGui.cs:648-675`) |
| InventoryGrid | `InventoryGrid_Patches.cs`, `PlayerInventory_Setup.cs:194-283` | Runtime `InventoryElement` wiring on the Auga slot prefab; reparent cells into Top/Main | Fights other mods for cells (EAQS) |
| Minimap | `Minimap_Setup.cs:82-187` | Replace `small`/`large`; re-point ~30 fields | Ping dummy (`:129-136`); dead `m_mapSmall`/`m_mapLarge` (declared only, `Minimap.cs:214,216`) |
| Chat | `Chat_Setup.cs:15-66` | Awake prefix: IndirectTwoObjectReplace with `AugaChat`; mutates the bundle prefab to add `GuiInputField` | Missing-script warnings stay |
| Store | `Store_Setup.cs:14-93` | Awake transpiler instantiates a 2nd StoreGui, deactivates the original | Two StoreGui instances |
| MessageHud | `MessageHud_Setup.cs:12-14` | IndirectTwoObjectReplace | Prefab predates 1.0.x fields (unverified) |
| TextInput, TextViewer, EnemyHud, DamageText, Barber | `TextInput_Setup.cs:13`, `TextViewer_Setup.cs:13`, `EnemeyHud_Setup.cs:17`, `DamageText_Setup.cs:15`, `Barber_Setup.cs:14` | Awake prefix: DirectObjectReplace (vanilla Awake skipped on the original) | Fields unverified against 1.0.12 |
| ZNet dialogs | `Connection_Setup.cs:11-21` | Destroy vanilla password/connecting dialogs, instantiate Auga's | Fields unverified |
| Skills | `SkillsDialog_Patch.cs:11-17` | `Update`/`OnClose`/`SkillClicked` skipped; `Setup` transpiled | Vanilla skill-dialog behaviour lost |
| Tooltips | `UITooltip_Patch.cs:10-49` | `UpdateTextElements` prefix reimplementation | Parallel logic |
| GuiBar | `GuiBar_Patch.cs:9-14` | `SetBar` prefix | Parallel logic |
| ButtonSfx | `ButtonSfx_Patch.cs:13` | Suppresses pointer select SFX | Compensates for bundle data (~130 buttons with select SFX) |
| Legacy Text | `Text_Patch.cs:13` | Global `Text.text` setter prefix | Global patch for bundle's legacy `Text` |

### 2.2 Crutch list (35)

Dummies and fake targets:
1. `PlayerInventory_Setup.cs:76-80`: `DummyDialogs` container parks replaced Variant/Skills dialogs inactive.
2. `PlayerInventory_Setup.cs:115-123`: 9 vanilla crafting fields pointed at `CraftingPanel.Dummy*` objects.
3. `PlayerInventory_Setup.cs:161-168`: `AugaInfoGroupDummy` UIGroupHandler fallback.
4. `PlayerInventory_Setup.cs:154-155`: split dialog touch/normal positions both = the panel.
5. `Minimap_Setup.cs:131-136`: `AugaPingDummy` for `m_selectedIconPing`, `m_pingImageObject`, `m_touchPingPanel`.
6. `Hud_Setup.cs:205-206`: `m_rudderLeft`/`m_rudderRight` → `ShipHud/Dummy`.
7. `Hud_Setup.cs:116-135`: 15 vanilla bar fields nulled or emptied.

Skip-guards and replaced vanilla methods:
8. `Hud_Setup.cs:229-276`: 7 prefixes return false (`UpdateStatusEffects`, `UpdateFood`, `SetHealthBarSize`, `SetStaminaBarSize`, `UpdateHealth`, `UpdateStamina`, `UpdateEitr`).
9. `Hud_Setup.cs:32-48`: guard for StatusEffectsExt/Int, which the bundle lacks.
10. `Hud_Setup.cs:344-460`: `SetupPieceInfo` replaced by a transpiler.
11. `Hud_Setup.cs:640-712`, `:714-775`: `UpdatePieceList` and `PieceTable.Prev/NextCategory` prefixes (dead while the build menu is off).
12. `PauseMenu_Setup.cs:132-257`: `Menu.UpdateNavigation` replaced, with try/catch.
13. `PauseMenu_Setup.cs:30-77`, `:79-130`: `TextsDialog.Update` and `ShowText` replaced, with null guards.
14. `PauseMenu_Setup.cs:418-430`: `AddActiveEffects`/`AddLog` skipped.
15. `PlayerInventory_Setup.cs:364-369`: `UpdateCharacterStats` skipped (weight label bug).
16. `SkillsDialog_Patch.cs:11-17`: `Update`/`OnClose`/`SkillClicked` skipped.
17. `UITooltip_Patch.cs:10-49`, `GuiBar_Patch.cs:9-14`, `Text_Patch.cs:13`: parallel reimplementations.
18. `Settings_Setup.cs:43-119`: reflection rewrite of Auga's own `SetBinding`.
19. `Chat_Setup.cs:20-21,30-57`: layout guard plus prefab mutation for the missing script.

Hide-and-hope and pass-through:
20. `Auga.cs:122`: `UseAugaBuildMenu => false`.
21. `PlayerInventory_Setup.cs:129-138`: vanilla Info panel left in place.
22. `Hud_Setup.cs:56-59,198-200`: vanilla GuardianPower, EventBar, LoadingBlack and KeyHints left in place.
23. `Hud_Setup.cs:82-93`: mount panel and adrenaline bar reparented to hudroot at world position.
24. `PauseMenu_Setup.cs:305-361`: vanilla invite, gamepad, cloud, feedback, lastSave, quit and logout objects adopted into Auga's menu, buttons rewired by reflection.
25. `Store_Setup.cs:26-30`: vanilla StoreGui deactivated, duplicate instantiated.
26. `Settings_Setup.cs:24-32`: vanilla wood image disabled, Auga panel stacked behind.
27. `MainMenu_Setup.cs:40-54`: cinematics button hidden. User requested this, so keep it but do it natively.
28. `ButtonSfx_Patch.cs:13`: compensates for bundle data.

Post-hoc resize and positions:
29. `PlayerInventory_Setup.cs:209-219`: `FitPlayerPanel` grows `m_player` after every `UpdateGui`.
30. `Hud_Setup.cs:194`: piece list root nudged +3/-3 (dead code).
31. `MovableHudElement` on ~20 elements with hardcoded offsets (`Hud_Setup.cs`, `Minimap_Setup.cs:99`, `Chat_Setup.cs:65`, `Store_Setup.cs:47`). This is a user feature, but it also re-anchors vanilla elements.

Diagnostics and hazards to remove:
32. `PauseMenu_Setup.cs:17-28`, `Auga.cs:657-658`: `TraceEsc`.
33. `PlayerInventory_Setup.cs:24,83-85`, `AugaCraftingControls.cs:30-33`: debug warnings.
34. `SetupHelper.cs:15-36`: `LogDeadRefs`. Keep it as a Phase 0 test tool, not in the release build.
35. `Auga.cs:344`, `API.cs:97`: `Thread.Sleep(15000/25000)` on failure paths. These freeze the game.

### 2.3 Dead vanilla refs (from the log)

- InventoryGui: `m_touchSplitAnchor`, `m_upgradeItemIcon/Durability/Name/Quality/QualityArrow/NextQuality/Index`, `m_qualityLevelDown/Up`, `m_qualityLevel`.
  - `SetupUpgradeItem` writes them (`InventoryGui.cs:1690-1710`). The 1.0.x upgrade/quality panel has no Auga equivalent, so this is a latent `MissingReferenceException`.
- Hud: `m_foodIcon` (declared only, `Hud.cs:135`); `m_statusEffectListRoot`/`m_statusEffectTemplate` (`Hud.cs:269-271`). These are used at `Hud.cs:1651`, which only the skip-prefix keeps from running.
- Minimap: `m_mapSmall`/`m_mapLarge` (declared only, `Minimap.cs:214,216`). Harmless.

### 2.4 Bundle and Unity project

- `AugaUnity\AssetBundles\augaassets` is **Unity 6000.0.61f1** (UnityPy `version_engine`), not 2022.3. mrcook1e rebuilt it (`34c68e9 Updated bundles`).
- `AugaUnity\ProjectSettings\ProjectVersion.txt` = 6000.0.61f1. The Unity 6 project is in the repo, and the game runs 6000.0.75.
- Rebuilding the bundle is possible. It needs a local 6000.0.x editor; whether one is installed is unverified.
- A rebuild alone does not help: the prefabs still reference `Fishlabs.GuiInputField` and pre-1.0 field layouts. Fix them in the editor by removing the missing scripts and stripping unused prefabs.

## 3. Options

- **A. Restyle in place.** Keep the vanilla GameObjects, components, Canvases, init order, input and focus. Swap sprites, TMP fonts and colours, adjust layout values through the vanilla layout groups, and add Auga-only panels as extra children.
  - Every vanilla field stays live, and `Menu.IsVisible`/`InventoryGui.IsVisible`/navigation keep working.
  - It removes dummies and skip-guards by construction.
  - Costs: Auga's exact look needs per-element sprite and font mapping, and some Auga layouts (paper doll, crafting side panel) are not a restyle.
- **B. Re-author Auga prefabs as vanilla prefabs.** Rebuild them in Unity 6 so they carry real vanilla components with vanilla hierarchy and field wiring.
  - It needs the game's prefab structure as an authoring source, and only decompiled code plus an extracted scene exist.
  - It has to be redone on every game UI update, which is exactly the failure mode we have now.
  - Rejected as the primary strategy.
- **C. Hybrid per screen.** A everywhere, except inventory/crafting, where Auga's layout and the public API require Auga structure. There, Auga panels are built around the vanilla `InventoryGui` objects: the grids, crafting fields and upgrade panel stay vanilla and are restyled and moved, and Auga's crafting controls wrap vanilla operations.

Codex (GPT peer, full text `E:\Temp\cx\b7b98c03bfc24486ae5b866eaa81ab86.out.md`) agrees: "Choose C, with A as the default".
- B is poor value because extracted scenes are not a maintainable authoring source.
- Keep vanilla `Menu` with its Canvas.
- EAQS must set the pivot itself; do not change `Panel_Create` defaults.
- Preserve both API binary compatibility and API behaviour (returned objects usable, parenting, timing).
- Audit consumers for direct hierarchy access.
- Gate on warnings, geometry, and screenshots.

### Per screen

| Screen | Choice | Evidence |
|---|---|---|
| Pause menu | A | Vanilla `Menu` owns Canvas order 1700 (scene dump). All logic is in `Menu.cs:221-403` and reads its own fields. Compendium button becomes an extra child in `MenuEntries`. |
| Settings | A | Already vanilla. Restyle sprites and fonts per tab instead of stacking a panel. |
| Main menu, char select/create, start/join | A | Vanilla since the earlier decision. Extend from font-only to sprites and fonts (see D1). |
| Hud bars, food, status effects | A | Restyle vanilla health/stamina/eitr/food; Auga text modes and ticks become added child components. Removes 7 skip prefixes and 15 nulled fields. |
| Minimap, Chat, MessageHud, TextInput, Barber, EnemyHud, DamageText, TextViewer, Store, ZNet dialogs | A | All Replace/DirectObjectReplace today. Restyle removes the dummies, the missing script and the duplicate StoreGui. |
| Build menu | A | Restyle the vanilla build hud. The Auga build menu (disabled) is deleted. |
| Inventory / crafting / container | C | API consumers need Auga structure (EAQS `Panel_Create` on `m_player`, EpicLoot workbench tabs/crafting controls). Vanilla grids, crafting and upgrade panel stay live. |
| Skills, Texts | A | Vanilla dialogs restyled. Delete the skip and replace patches. |

### API compatibility (Auga.API, 72 methods)

- Keep signatures and types unchanged, so compiled consumers load unmodified.
- Factories (`Panel_Create`, buttons, dividers, tooltips) keep instantiating bundle widgets. They are self-contained and unaffected.
- Accessors that return Auga screen objects (`GetCraftingControls`, `Workbench_*` tabs, and similar) must return live objects that drive vanilla operations.
- Test with the installed EpicLoot 0.14.2, EAQS 3.1.1, VNEI 0.17.6 and AdventureBackpacks (fork), not rebuilt copies.
- AdventureBackpacks source uses only the vendored APIManager and a GuiBar patch, with no direct `Auga.API` calls (grep).

## 4. Phased plan

Every phase ends with the steps below. The phase is not done until all of them pass.
- `pwsh -File E:\DEV\Valheim\tools\autotest.ps1`: build, patches, hash, and smoke must all PASS.
- A new world-smoke layer (Phase 0) must report zero Auga warnings or errors.
- An in-game checklist run by the user; the log is attached to the handoff.
- One commit per logical change.

**Phase 0: instrumentation (no UI change).**
- Add a dev-only console command `auga_audit` that dumps, for each open screen:
  - missing scripts
  - dead Unity refs on vanilla UI components
  - Graphics without a Canvas ancestor
  - screen-space rect overlaps between named top-level panels, with an allowlist
- Extend autotest with a world-load layer (quality-gate#25): load `DEVTEST`, open inventory/map/Esc via input simulation or console, run `auga_audit`, fail on findings.
- Remove the debug warnings (crutches 32-33). Wrap `LogDeadRefs` into the audit command.
- Verify: audit output reproduces §1.1 (AugaMenu without Canvas) and §1.2 (EAQS offset).

**Phase 1: pause menu and Settings (A).**
- Delete the Menu replace, `WireMenu`, the `UpdateNavigation` transpiler and `TraceEsc`.
- Restyle vanilla `Menu`: panel sprites and darken with Auga art, button sprites and TMP fonts from the bundle.
- Add Auga's Compendium as an extra `MenuEntries` button (its controller is the only Auga-only part).
- Settings: restyle the vanilla sprites; drop the stacked `AugaPanelBase` and the `wood.enabled = false` crutch.
- Verify: Esc opens, the menu is visible and clickable, and gamepad navigation works. Settings opens from the pause and main menus and saves. No overlap per `auga_audit`.

**Phase 2: pre-world menus (A).** Needs D1.
- Replace the font-only postfix with a restyle of `FejdStartup` panels (menu, character select/create, start game, join) using Auga sprites and fonts.
- Hide the cinematics button through the vanilla list setup, not `SetActive` on a found button, if a native hook exists. Otherwise keep it (explicit user request).
- Verify: autotest smoke, screenshots of each panel, Cyrillic renders.

**Phase 3: HUD (A).**
- Revert all Hud replaces. Restyle the vanilla bars, food, status effects, crosshair, ship HUD and KeyHints.
- Port the `AugaHealthBar` config (text mode and position, ticks, fixed size) as a component added to the vanilla bars.
- Delete the 7 skip prefixes and the nulled fields.
- Minimap: restyle the vanilla small/large frames and delete the ping dummy.
- Build menu: restyle vanilla; delete `UseAugaBuildMenu` and the dead code.
- Verify: health/stamina/eitr/food/status effects update, the ship HUD works, the map works with pings, building works, and no dead refs.

**Phase 4: small screens (A).**
- Chat, MessageHud, TextInput, TextViewer, Barber, EnemyHud, DamageText, Store, ZNet dialogs, Skills, Texts: restyle vanilla. Delete the Replace prefixes, the duplicate StoreGui and the TextsDialog/Skills skips.
- Verify: chat sends once, signs and portals accept text input, the trader buys and sells, the barber works, the password prompt appears, skills and texts show vanilla content.

**Phase 5: inventory and crafting (C).**
- Keep the vanilla `root/Player`, `root/Container` and `root/Crafting` objects and their `InventoryGrid`/crafting fields, including the upgrade/quality panel.
- Rearrange them into Auga's layout with anchors and layout groups at setup time. Restyle the element prefab.
- Re-home the Auga crafting controls and workbench tabs as wrappers over the vanilla buttons, keeping the API return types.
- Delete the Replace calls, the `Dummy*` fields, the grid delegate hand-wiring, `FitPlayerPanel`, the Info pass-through and the `UpdateCharacterStats` skip.
- EAQS: fix the pivot in EAQS (D2).
- Verify: drag/drop, split, take-all/stack-all, craft/upgrade/repair, the weight label updates, and EpicLoot, EAQS, VNEI and AdventureBackpacks panels are aligned. Also check `auga_audit` overlap, screenshots at 1080p/1440p and UI scale extremes.

**Phase 6: bundle cleanup.**
- In the Unity 6 project, strip replaced or unused prefabs (AugaMenu, MainMenu, HUD parts, BuildHud...) and remove the `Fishlabs.GuiInputField` references. Keep the `AugaAssets` public fields for API compat, or document the removal.
- Delete `Thread.Sleep` failure paths.
- Verify: zero missing-script warnings at plugin load and on world load.

## 5. Decisions (user, 2026-09-11)

- **D0. Full vanilla skin (supersedes option C).** Every screen is option A, inventory and crafting included: vanilla objects restyled in place. Auga's own inventory layout is deleted. The right stats panel and tabs survive only as additive panels, if at all.
- **D0a. No `Auga.API` for consumers.** The plugin gets a new GUID and a new assembly name, so EpicLoot, EAQS, VNEI and AdventureBackpacks see no Auga and take their normal vanilla code path. EAQS's stub detects assembly `Auga` and caches the result in `Awake` (Codex), so both names change. The APIManager legacy redirect is dropped. The config file resets (new GUID = new file); accepted.
- D1. Main menu and pre-world screens: vanilla layout and logic, restyled in place with Auga sprites, frames and fonts. The old Auga MainMenu prefab does not come back. (Phase 2.)
- D2. ~~EAQS offset: one sanctioned Auga-side compat patch on EAQS's own positioning code.~~ **Moot after D0a:** EAQS takes its vanilla path, so there is no Auga panel to align to.
- D3. Build menu: the Auga build menu code is deleted, the vanilla one is restyled. (Phase 3.)
- D4. Warnings scope: zero Auga-originated errors and warnings. Vanilla and third-party lines are only reported.
- D5. Movable HUD elements (`MovableHudElement`) stay as a feature, applied to vanilla elements. (Phase 3.)
- Hard rule (user): no crutches (dummy objects, skip-guards, hide-and-hope, post-hoc resizes, `Thread.Sleep`). Every change mirrors the vanilla code path; cite decompile `file:line` where non-obvious. If something cannot be native, stop and report instead of hacking.

### Phase list after D0/D0a

Phases 0-4 and 6 as in §4. Phase 5 changes, and a rename phase comes before it:
- **Phase 4b: GUID and assembly rename.** First decompile the installed EpicLoot 0.14.2, VNEI 0.17.6, EAQS 3.1.1 and AdventureBackpacks (fork) with ilspycmd and record exactly how each detects Auga (assembly name, type `Auga.API`, GUID `randyknapp.mods.auga`, `Chainloader.PluginInfos`). Then rename the GUID and assembly, delete `API.cs`, `API.Common.cs`, `API.External.cs` and `APIManagerPatcher.cs`, and check that every consumer logs its non-Auga path.
- **Phase 5: inventory and crafting (A, was C).** Restyle vanilla `root/Player`, `root/Container`, `root/Crafting`, `root/Info` and the upgrade/quality panel in place. Delete the Auga inventory layout, `RightPanel`, `CraftingPanel.Dummy*`, `FitPlayerPanel`, the grid hand-wiring and the `UpdateCharacterStats` skip. No EAQS patch.
