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
  - puts Auga's font on every vanilla TMP text (a dynamic `TMP_FontAsset` built from bundle `SourceSansPro-SemiBold`, with the vanilla fonts as glyph fallback), including the `m_worldListElement` / `ServerListGui.m_serverListElement` prefabs;
  - hides the button whose onClick calls `OnCinematics`, so the 1.0.7 cinematics list (Black Forest / Locked / Back) is unreachable. Vanilla `HideAll` keeps the list hidden.
  - Autotest smoke passes (menu reached). In-game recheck: menu looks vanilla in Source Sans Pro, the Cinematics button is gone, and Cyrillic and icons render (no boxes).
  - `AugaAssets.MainMenuPrefab`/`AugaLogo`/`WorldListElement`/`ServerListElement` still load (public fields, kept for API compat) but are unused.
- APIManager `Failed patching ... InvalidCastException ... AddOverrides` for EpicLoot/EquipmentAndQuickSlots: `VisitMethod` re-owned `MethodDefinition`s nested in their `Auga.API` stub (`<Transpiler>d__2`). It is now guarded like `VisitField`.
- AAACrafting `Undefined target method ... InventoryAddItemPatchDataIntIntInt`: NOT Auga. Vanilla 1.0.7 changed private `Inventory.AddItem(ItemData,int,int,int)` to `(ItemData,int,int,int,bool skipValidPositionCheck=false)`. The exception aborts AAACrafting's `PatchAll`, so its later patch classes (ServerSync RPC, favoriting, paginator, ...) are not applied either. The mod is third-party (Azumatt, 2.1.6), so the options are: a newer AAACrafting build for 1.0.7, or disable it again (`.dll.disabled`).
- AdventureBackpacks `RegisterSlot` NRE: the statically read EAQS `AddSlot` path is null-safe once EAQS `Awake` has run, so a stack is needed. AB commit (see below) now logs the full exception. AB also ships its own APIManager patcher copy with the same `VisitMethod` bug. It is fixed the same way as Auga 76853a8 and deployed, SHA256 `D390B9727B6FFE86C453B76D9C5E8316F57234B9DEB6E9C1727EDD0DBBEDC9BF`. In-game: confirm that no `Failed patching` appears and that `Registered the 'Backpack' equipment slot` is logged. Otherwise read the logged stack.

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
   - `MainMenu: could not create TMP font` means the main menu fell back to the vanilla font.
   - `FixDeadFields` / dead-ref warnings mean a field points at a destroyed object.
   - A transpiler hit-count error means the `UpdateBuild` anchor is wrong.

## Known open risks

- The 2022.3 asset bundle has never been loaded in the Unity 6 runtime.
- Passed-through vanilla elements keep vanilla anchors and may overlap Auga panels.
- The adrenaline bar keeps its world position (no Auga layout yet).
- Backpacks wider than about 8 columns may overflow Auga's fixed container panel.
- Chat scroll speed with `ZInput.GetMouseScrollWheel()` is not checked.
