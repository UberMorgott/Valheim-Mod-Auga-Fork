# Auga Unofficial Fork — Design

Date: 2026-09-11. Owner: UberMorgott. Scope: personal modpack build (GitHub only, no Thunderstore).

## Goal

Revive RandyKnapp/Auga (abandoned 2024-05, targets Valheim 0.217.x) on Valheim 1.0.7 (Unity 6, network 39, BepInEx 5.4.23.5), fix upstream defects, and make the user's UI mods work with it through the Auga API.

## Sources and baseline

- Fork: https://github.com/UberMorgott/Auga, local `E:\DEV\Valheim\Auga`. Remotes: `origin` (fork), `upstream` (RandyKnapp), `mrcook1e` (https://github.com/mrcook1e-ai/Auga).
- Base = upstream `main` + merge of mrcook1e-ai `main` (19 commits ahead, 0 behind: Unity 6 build, API fixes, MainMenu rewrite). Each merged commit reviewed against the 1.0.7 decompile.
- ZenDragon AugaLite is NOT a source. It was removed from `plugins`.
- Ground truth for game code: `E:\DEV\Valheim\ValheimDecompiled\` (1.0.7: `assembly_valheim`, `assembly_utils`, `assembly_guiutils`). No guessing of members or signatures.

## Phase 0 — Build

- Convert `Auga/Auga.csproj` (and `AugaUnityLib`) to the SmoothRegen pattern (`E:\DEV\Valheim\SmoothRegen\SmoothRegen.csproj`): SDK-style, `net472`, `ValheimDir=D:\Steam\steamapps\common\Valheim`, HintPath to `Valheim_Data\Managed`, `Private=false`, `BepInEx.AssemblyPublicizer.MSBuild` with `Publicize="true"` on `assembly_valheim`, NuGet BepInEx.Core + HarmonyX.
- Remove hardcoded `M:\Code\VapokModBase\References` and the `G:\Steam` xcopy post-build. Resolve the APIManager dependency: vendor it or drop it.
- Keep embedding the asset bundle `AugaUnity/AssetBundles/augaassets` (LFS) + `Unity.Auga.dll` (ILRepack/embedded resources as today).
- Done when: `dotnet build` is green, the DLL is deployed to `BepInEx\plugins\Auga`, and the deployed hash matches the build output (HANDOFF.md deploy procedure).

## Phase 1 — Port to 1.0.7

1. Known breakages (from the static check):
   - `Settings_Setup.cs`: vanilla `Settings` was rewritten into tabs (`m_resButtonText`, `m_selectedRes`, `m_alternativeGlyphs`, `m_keys`, `m_resolutions`, `UpdateGamepadMap`, `UpdateBindings` are gone). Rewrite the file against 1.0.7.
   - `MainMenu_Setup.cs`: `m_joinIPPanel`, `m_manualIPButton`, `m_joinIPJoinButton`, `m_joinIPAddress`, `m_friendFilterSwitch`, `m_publicFilterSwitch` were removed from `FejdStartup`.
   - `Hud_Setup.cs:710` `m_pieceBarPosX`; `PlayerInventory_Setup.cs:120` `m_splitPanel`.
2. Transpilers: verify every IL pattern against 1.0.7 IL, not just member names.
3. Vanilla UI audit: vanilla UI changed a lot since 0.217. For `Hud`, `InventoryGui`, `Menu`, `Settings`, `FejdStartup`, `Minimap`, `StoreGui`, `SkillsDialog`, `TextsDialog`, list the 1.0.7 UI fields and elements Auga does not handle. Decide per element: restyle, hide, or pass through vanilla. Nothing may end up hidden under Auga panels or non-interactive.
4. Assets: load the existing bundle (built with Unity 2022.3.12f1) in the Unity 6 runtime first. Rebuild in a Unity 6 editor only if loading or rendering is broken.
- Done when: the game boots to the world, the BepInEx log shows no Auga exceptions, and the manual checklist passes: main menu, settings (all tabs), HUD, inventory, crafting, build menu, map, trader, skills, compendium, chat, tooltips.

## Phase 2 — Mod integration via Auga API

Rule: `Auga/API.cs` (72 static methods) is a public contract. Existing consumers ship stub `API` classes that are transpiled to the real `Auga.API` when the assembly `Auga` is loaded. Never change or remove existing signatures. Add new methods only when a consumer needs them.

Rejected: a universal runtime adapter that restyles or re-parents foreign UI. It does not restore handlers, references, or UI semantics, so it is fragile.

| Mod (installed) | Built-in Auga support | Plan |
|---|---|---|
| EquipmentAndQuickSlots 3.0.1 | yes (`IsLoaded`, `Panel_Create`, `PlayerPanel_*`, `ComplexTooltip_*`, `Workbench_*`, `Button_*`) | Should work once the API is compatible. Fix the slot display only if needed (local fork of the mod as the last resort). |
| EpicLoot 0.13.0 | yes (`EpicLootAuga`, `EnchantingTabAuga`, `AugaTooltipPreprocessor`) | Verify tooltips, rarity, enchanting tab. Add missing extension points to Auga. |
| Vnei 0.17.6 | yes, minimal (`PlayerPanel_AddTab/GetTabButton/HasTab/IsTabActive`) | Verify the tab. |
| AAACrafting 2.1.6 | yes (`AugaAPI`, `augaCraftingButton`, `AugaTextInput`) | Re-enable (currently `.dll.disabled`) and verify. UI-position fixes go in `Auga/Compat/`, recipe-logic fixes go in a local fork of the mod. |
| AdventureBackpacks 1.9.13.3 (our fork `E:\DEV\Valheim\AdventureBackpacks-Morgott`) | no | Write a consumer adapter in the fork using the stub API pattern. |
| SmoothRegen (our mod `E:\DEV\Valheim\SmoothRegen`) | n/a | Bug: during regen, Auga's health bar fills with a white "pending" layer while the red fill stays still. Fix: the red fill tracks current HP smoothly. The fix location (Auga `GuiBar`/HUD patch vs. a SmoothRegen hook) is decided after reading the code. |

Built-in Auga compat (`Compat/Chatter.cs`, `Jewelcrafting.cs`, `SearsCatalog.cs`; MultiCraft, BetterTrader, SimpleRecycling in `Auga.cs`) is kept as is. It is ported only if those mods are installed.

## Risks

1. Unity 6 / Valheim changes: prefab components, UI lifecycle, Harmony/IL targets.
2. Patch order and ownership of the crafting UI (Auga vs AAACrafting vs EpicLoot enchanting).
3. UI/inventory state drift: lost item access, wrong recipe or enchanting target.

## Testing

- No automated UI tests. Every step: build, deploy with hash check, BepInEx log clean of Auga exceptions, then the manual in-game checklist run by the user.
- Per-mod checklist item added in Phase 2.

## Process

- Commit every verified change to `main` (conventional commits). Push only on explicit request.
- Unclear or hard decisions (IL ports, asset issues, design forks): consult the Codex peer.
