# Auga fork — handoff (2026-09-11 night session)

Repo: https://github.com/UberMorgott/Valheim-Mod-AugaFork (fork of RandyKnapp/Auga), local `E:\DEV\Valheim\Auga`.
Target: Valheim 1.0.7 (Unity 6, network 39), BepInEx 5.4.23.5. Personal build.

- Spec: `docs/superpowers/specs/2026-09-11-auga-fork-design.md`
- Plan: `docs/superpowers/plans/2026-09-11-auga-port.md` (T9 = in-game checklist)

## State

- Build: `dotnet build Auga\Auga.csproj -c Release -p:ValheimDir=D:\Steam\steamapps\common\Valheim` → 0 errors.
- Deployed: `D:\Steam\steamapps\common\Valheim\BepInEx\plugins\Auga\Auga.dll` (hash-verified after each change).
- AugaLite (ZenDragon) deleted from plugins.
- AAACrafting re-enabled: `plugins\AAACrafting\AzuAntiArthriticCrafting.dll` (was `.dll.disabled`; rename back to undo).
- AdventureBackpacks fork: `2d5688a` skips its 54px durability-bar override under Auga; old DLL kept as `plugins\AdventureBackpacks\AdventureBackpacks.dll.bak`.
- NOTHING has been run in-game yet. All verification so far is static (build + decompile 1.0.7 + bundle inspection with UnityPy).

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
- Review fixes: arrival log only on first spawn, chat scroll via `ZInput`, no double assembly loads, snapping icon like vanilla, HUD restyle independent of build-menu option.

## First in-game run

1. Start the game and play through the T9 checklist in the plan.
2. Collect `D:\Steam\steamapps\common\Valheim\BepInEx\LogOutput.log` and grep `[Auga]`:
   - `all 39 assets found` means the bundle loaded; otherwise it lists what is missing.
   - `MainMenu: kept vanilla <field>` lines show which vanilla elements were passed through (they may overlap the Auga layout).
   - `FixDeadFields` / dead-ref warnings mean a field points at a destroyed object.
   - A transpiler hit-count error means the `UpdateBuild` anchor is wrong.

## Known open risks

- The 2022.3 asset bundle has never been loaded in the Unity 6 runtime.
- Passed-through vanilla elements keep vanilla anchors and may overlap Auga panels.
- The adrenaline bar keeps its world position (no Auga layout yet).
- Backpacks wider than about 8 columns may overflow Auga's fixed container panel.
- Chat scroll speed with `ZInput.GetMouseScrollWheel()` is not checked.
