# Upstream licensing status

Checked 2026-09-30. Summary for the whole pack: `E:\DEV\Valheim\Yggdrasil\docs\mod-licenses.md`.

## Upstream

- Mod: Project Auga, author RandyKnapp (code), n4 (design); later co-maintained by Vapok (https://github.com/Vapok/Auga, fork, no license).
- Source: https://github.com/RandyKnapp/Auga
- Thunderstore: https://thunderstore.io/c/valheim/p/RandyKnapp/Auga/ (v1.3.11, updated 2023-11-18, `website_url` = GitHub repo)
- Nexus: https://www.nexusmods.com/valheim/mods/1413 ("Project Auga - Deprecated", updated 2023-11-19)

## Findings

- No LICENSE file in the upstream repo; GitHub license detection returns `null` (2026-09-30).
- Upstream git history: no commit ever touched `LICENSE`, `LICENSE.md` or `LICENSE.txt`. Nothing was removed.
- Upstream README: no license text. Only license files are third-party TextMesh Pro fonts (OFL/AFL) under `AugaUnity/Assets/TextMesh Pro/`.
- Source header: `Auga/Properties/AssemblyInfo.cs` upstream `AssemblyCopyright("Copyright © Randy Knapp 2021")`.
- Nexus permissions (read 2026-09-30):
  - Modification: "You are not allowed to modify my files, including creating bug fixes or improving on features under any circumstances"
  - Upload: "You are not allowed to upload this file to other sites under any circumstances"
  - Asset use: "You are allowed to use the assets in this file without permission as long as you credit me"
  - Assets in sold mods: "You are not allowed to use assets from this file in any mods/files that are being sold"
  - File credits: "Code: RandyKnapp, Design: n4"
- Author statement (same author, sister repo), https://github.com/RandyKnapp/ValheimMods/issues/324#issuecomment-922488325, 2021-09-19: "It's all rights reserved. You modified my source, compiled it, and uploaded it for others to download. that's redistribution." Earlier comment (2021-09-18): "you're not allowed to modify my source and redistribute it".
- Open issue https://github.com/RandyKnapp/Auga/issues/242 (2025-02-12, ZenDragonX) asks for a permissive license; no answer from the author as of 2026-09-30.
- Default: all rights reserved, and the author has explicitly refused modification + redistribution in the past.

## Status

NOT FOUND — and Nexus terms explicitly forbid modification. Assets: PARTLY (credit-only reuse, but not in sold mods). Highest risk of the pack.

## Author activity and contact

- Active again: upstream commits by `rknappTC` (same commit e-mail as RandyKnapp) 2026-09-23..24, Auga + EAQS integration work.
- Contact: e-mail listed on the public GitHub profile https://github.com/RandyKnapp; Discord https://discord.gg/randyknappmods (Project Auga) / https://discord.gg/ZNhYeavv3C; comment on issue #242.

## Fork scope

- Fork: https://github.com/UberMorgott/Valheim-Mod-Auga-Fork (shipped as AugaSkin) — port to current Valheim / Unity 6 build, UI fixes, integrations (137 commits on top of upstream).
- `LICENSE` here covers only Morgott's own additions (CC BY-NC 4.0); upstream material is excluded.

## Next step

- Send the permission request drafted in `Yggdrasil\docs\mod-licenses.md` (section "Draft: RandyKnapp") by e-mail or Discord DM.
- Until explicit written permission: owner decides whether to keep shipping AugaSkin; the Nexus terms say no modification under any circumstances.
