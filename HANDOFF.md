# Auga fork — handoff (2026-09-11 night session)

Repo: https://github.com/UberMorgott/Valheim-Mod-AugaFork (fork of RandyKnapp/Auga), local `E:\DEV\Valheim\Auga`.
Target: Valheim 1.0.7 (Unity 6, network 39), BepInEx 5.4.23.5. Personal build.

- Spec: `docs/superpowers/specs/2026-09-11-auga-fork-design.md`
- Plan: `docs/superpowers/plans/2026-09-11-auga-port.md` (T9 = in-game checklist)

## State

- Build: `dotnet build Auga\Auga.csproj -c Release -p:ValheimDir=D:\Steam\steamapps\common\Valheim` → 0 errors.
- Deployed: `D:\Steam\steamapps\common\Valheim\BepInEx\plugins\Auga\Auga.dll` (hash-verified after each change).
- AugaLite (ZenDragon) deleted from plugins.
- AAACrafting disabled again: `plugins\AAACrafting\AzuAntiArthriticCrafting.dll.disabled`. 2.1.6 (latest on Thunderstore as of 2026-09-11) fails on 1.0.7 with Harmony `Undefined target method` - private `Inventory.AddItem` gained `bool skipValidPositionCheck`. Re-enable (rename back) once `update-mods.ps1` pulls a newer version.
- AdventureBackpacks fork: `2d5688a` skips its 54px durability-bar override under Auga; old DLL kept as `plugins\AdventureBackpacks\AdventureBackpacks.dll.bak`.
- Main menu decision (user, 2026-09-11): the main menu (FejdStartup: menu, character select/creation, start game, join) is pure vanilla. Auga's main-menu replacement (`MainMenu_Setup.cs`, ~1000 lines) is deleted. `MainMenu_Setup.cs` now only has a `FejdStartup.Awake` postfix that:
  - puts the font of Auga's old main-menu buttons (bundle TMP `Norsebold SDF`, 74 Cyrillic chars, loaded as `AugaAssets.NorseboldTMP`) on vanilla TMP texts inside a `Button`, with the vanilla fonts as glyph fallback. It is a display font, so body/list texts (world/server/character names) keep the vanilla font. The first try, Source Sans Pro on everything, was rejected as plain.
  - hides the button whose onClick calls `OnCinematics`, so the 1.0.7 cinematics list (Black Forest / Locked / Back) is unreachable. Vanilla `HideAll` keeps the list hidden.
  - Autotest smoke passes (menu reached, 40/40 assets). In-game recheck: the menu looks vanilla with Norse-style button labels, the Cinematics button is gone, and Cyrillic and icons render (no boxes).
  - `AugaAssets.MainMenuPrefab`/`AugaLogo`/`WorldListElement`/`ServerListElement` still load (public fields, kept for API compat) but are unused.
- APIManager `Failed patching ... InvalidCastException ... AddOverrides` for EpicLoot/EquipmentAndQuickSlots: `VisitMethod` re-owned `MethodDefinition`s nested in their `Auga.API` stub (`<Transpiler>d__2`). It is now guarded like `VisitField`.
- AAACrafting `Undefined target method ... InventoryAddItemPatchDataIntIntInt`: NOT Auga. Vanilla 1.0.7 changed private `Inventory.AddItem(ItemData,int,int,int)` to `(ItemData,int,int,int,bool skipValidPositionCheck=false)`. The exception aborts AAACrafting's `PatchAll`, so its later patch classes (ServerSync RPC, favoriting, paginator, ...) are not applied either. The mod is third-party (Azumatt, 2.1.6), so the options are: a newer AAACrafting build for 1.0.7, or disable it again (`.dll.disabled`).
- AdventureBackpacks `RegisterSlot` NRE: the statically read EAQS `AddSlot` path is null-safe once EAQS `Awake` has run, so a stack is needed. AB commit (see below) now logs the full exception. AB also ships its own APIManager patcher copy with the same `VisitMethod` bug. It is fixed the same way as Auga 76853a8 and deployed, SHA256 `D390B9727B6FFE86C453B76D9C5E8316F57234B9DEB6E9C1727EDD0DBBEDC9BF`. In-game: confirm that no `Failed patching` appears and that `Registered the 'Backpack' equipment slot` is logged. Otherwise read the logged stack.

## In-world crash (camera in the sky, placeholder HUD), fixed 2026-09-11

What the user's run log showed: Chat, Hud, and InventoryGui postfixes threw on world load, followed by ~3700x `Chat.HasFocus` NRE from `Player.TakeInput`/`Minimap.Update`/`HotkeyBar.Update`, which stalled player input, the camera and the minimap. Causes and fixes:
- Chat: the bundle's `AugaChat/Chat_box/ChatInput` uses `Fishlabs.GuiInputField` (ui_lib.dll, removed in 1.0.7), so `Chat.m_input` was null. `Chat_Setup.FixChatInput` now adds `GUIFramework.GuiInputField` to the prefab before instantiation, and falls back to vanilla chat if the layout is unexpected.
- Hud: the bundle (both mrcook1e's Unity 6000.0.61 build and upstream's 2020.3 build) has no `StatusEffectsExt/StatusEffectsInt`, which killed the postfix at the first `.gameObject`. Now guarded. Vanilla GuardianPower, LoadingBlack, EventBar and KeyHints are kept: the bundle has legacy `Text` where the 1.0.7 fields are `TMP_Text`, and it lacks `m_loadingIndicator`, `m_gpTouchButton`, `m_radialKeyHints` and `m_buildMenuHints*`.
- Build menu: `Auga.UseAugaBuildMenu => false`. The BuildHud replacement left about 15 1.0.7 fields (`m_buildSelection`, `m_pieceListRoot`, `m_requirementItems`, hovered-author...) on destroyed objects, so the vanilla build menu is used. Re-enabling means rewiring those fields.
- Inventory: 1.0.7 nests `root/Container` under `root/Player`, so the container is lifted to root before the Player replace.
- Minimap: `m_selectedIconPing`, `m_touchPingPanel` and `m_pingImageObject` now point to a hidden dummy.
- Tools: UnityPy dumps of the bundle vs the vanilla scene (`SoftRef/Bundles/17245031` = main.unity) map vanilla field→path. Autotest smoke is menu-only (quality-gate#25).
- Recheck in world: the log should have no `Chat.HasFocus`/`Hud_Awake_Postfix` errors. Check `[Auga] Hud:`/`InventoryGui:` dead-ref lines, that chat Enter sends exactly once, that the inventory and container open, that the minimap renders, and that the build menu is vanilla.

## Inventory placeholders / 7 rows / empty EAQS panel, fixed 2026-09-11 (084056b)

- Log: 3147x NRE in `InventoryGrid.UpdateGui`. Cause: 1.0.7 `InventoryGui.Awake` wires `CanDropDragOntoItem` (called unchecked, `InventoryGrid.cs:375`), `m_onReleased`, `m_onEnter`, `OnSetTouchSelection`, `OnMoveTo*` on the vanilla grids only; Auga's replaced grids had none. The NRE after the first item left empty slots with bundle placeholder art, skipped all UpdateGui postfixes and the rest of `InventoryGui.Update` (container, drag, stats, weight, recipe).
- 7 rows = EAQS 3.1.1: `m_inventory.m_height = BaseRows + 3` hidden rows for equipment/quick slots. Its `UpdateGui` postfix moves those cells into its Auga panel (`API.Panel_Create` on `m_player`), which never ran because of the NRE, so the cells stayed in the grid and the EAQS panel was empty.
- Fix: wire the delegates in the `InventoryGui.Awake` postfix. Auga's `UpdateGui` postfix now reparents only elements still under `m_gridRoot`, so it no longer fights EAQS for its cells.
- Tab "double click": no double listener found (tab buttons have no persistent onClick; `AugaTabController.Awake` is the only listener; `SelectTab` is idempotent). Suspected side effect of the per-frame NRE. Recheck.
- Recheck in world: no `UpdateGui` NRE; empty slots blank; 4 rows in the panel; EAQS equipment/quick slots in the middle panel; weight/armor labels in place; tabs switch on one click; drag/drop, split, and container take-all work.

## Done (main)

- T0 merge mrcook1e-ai/Auga (Unity 6 port) + cleanup.
- T1 build against 1.0.7 API; InventoryGrid uses vanilla `UpdateGui` with runtime `InventoryElement` wiring on Auga's slot prefab.
- T2 split dialog on 1.0.7 `SplitDialog`.
- T3 vanilla Settings window (Auga settings replacement removed; pause menu copies vanilla `m_settingsPrefab`).
- T4 `Hud.UpdateBuild` transpiler exact-match.
- T6 real APIManager patcher vendored (AAACrafting binding).
- T7 API compat: `Auga.API` unchanged (72 methods); consumers' calls all present.
- T8 SmoothRegen HP bar: `FastBar.m_changeDelay = 0` (`tools\guibar-delay-check.ps1`).
- T5 vanilla UI audit: pause menu wired to 1.0.7 fields (would have crashed), vanilla Info panel kept (Texts/Trophies/Achievements), mount panel + adrenaline bar passed through, main-menu vanilla elements passed through instead of dummies, `m_uiGroups` order fixed for 1.0.7.
- Vanilla main menu with Auga font; cinematics button hidden (see State).
- Review fixes: arrival log only on first spawn, chat scroll via `ZInput`, no double assembly loads, snapping icon like vanilla, HUD restyle independent of build-menu option.

## First in-game run

1. Start the game and play through the T9 checklist in the plan.
2. Collect `D:\Steam\steamapps\common\Valheim\BepInEx\LogOutput.log` and grep `[Auga]`:
   - `all 39 assets found` means the bundle loaded; otherwise it lists what is missing.
   - `MainMenu: Norsebold SDF missing` means the main menu fell back to the vanilla font.
   - `FixDeadFields` / dead-ref warnings mean a field points at a destroyed object.
   - A transpiler hit-count error means the `UpdateBuild` anchor is wrong.

## Known open risks

- The 2022.3 asset bundle has never been loaded in the Unity 6 runtime.
- Passed-through vanilla elements keep vanilla anchors and may overlap Auga panels.
- The adrenaline bar keeps its world position (no Auga layout yet).
- Backpacks wider than about 8 columns may overflow Auga's fixed container panel.
- Chat scroll speed with `ZInput.GetMouseScrollWheel()` is not checked.
