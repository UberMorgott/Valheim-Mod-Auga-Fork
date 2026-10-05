# Workspace

## Scope rule (user, 2026-10-04)
ALL UI — vanilla game and every mod in the pack — must be in Auga style. Any non-Auga-styled panel = AugaSkin defect: fix it without asking per panel (from game decompile / mod decompile, Auga side only, no third-party DLL patches). Close-out per panel: committed + pushed + published to VPS (`valheim-pack.ps1 -Only AugaSkin -Upload`). Backlog: `TASKS.md`.
Exception (user, 2026-10-05): BossAwakening world boss bar (`BA_WorldBossBar`) keeps its own design — do not restyle.

Valheim workspace rules: E:\DEV\Valheim\CLAUDE.md and shared memory C:\Users\Morgott\.claude\projects\E--DEV-Valheim\memory\MEMORY.md — read both first.

<!-- quality-gate -->
## Completion gate (mandatory)

After changing files, run `qgate` from the repository root. Use `qgate -All` when
dependencies, build configuration, generated files or several stacks changed.

Exit code 0 means done. Anything else means NOT done: the output names the exact
failures -- fix them and run it again. Do not report completion while the gate is
failing, and never edit or disable the gate to make it pass. Include the command
you ran and its pass/fail result in your final response.

If `qgate` is unavailable, report that as a blocker, do not skip it. It installs with
`irm https://raw.githubusercontent.com/UberMorgott/quality-gate/main/bootstrap.ps1 | iex`

If the gate itself is wrong -- it crashes, blames code that is provably correct,
misses a whole stack, or cannot be satisfied at all -- do not work around it and do
not disable it. Open an issue against the gate and say so in your final response:

```powershell
qgate where   # install path + commit, paste this into the issue
gh issue create --repo UberMorgott/quality-gate --title "<what broke>" --body "<qgate output, the command you ran, the file it blamed, `qgate where` output>"
```
<!-- /quality-gate -->
