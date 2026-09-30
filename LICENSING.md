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

PERMITTED (written, e-mail 2026-09-30). The author's written permission overrides the Nexus terms above for this fork.

Previous status (before 2026-09-30): NOT FOUND; Nexus terms forbid modification; assets only credit-only reuse, not in sold mods.

## Written permission (2026-09-30)

- Channel: e-mail, Gmail thread "Permission request for AugaSkin and possible upstream contributions" (thread `1a0f182804c7d74c`).
- Request: msg `1a0f1dc309fd003f`, 2026-09-30 10:28 UTC, ubermorgott@gmail.com → randy.bravo2@gmail.com. Key text:
  > Would you give written permission for me to modify Auga, host AugaSkin's source on GitHub, and distribute free builds containing its UI assets, including in my Yggdrasil modpack?
  >
  > For clarity, the mod itself is free; Yggdrasil includes a paid launcher/update service. I'm asking whether that distribution arrangement is acceptable too.
  >
  > I'll clearly identify AugaSkin as an unofficial fork and credit you for the code, n4 for the design, and the community port. Please let me know whether anyone else's approval is needed for the included artwork.
- Reply: msg `1a0f20d7c848e503`, 2026-09-30 11:22 UTC, randy.bravo2@gmail.com (verbatim):
  > I don't have any problems with this. Right now I'm working through a few compatibility issues with Auga before relaunching it.
  >
  > I should have it done in the next few weeks, butt I'm out of town for a bit.
  >
  > Also if you find any bug fixes, please make some PRs on the original GitHub!
  >
  > Thanks!
  > +Randy
- Scope granted: modify Auga; host AugaSkin source on GitHub; distribute free builds including its UI assets, including in the Yggdrasil modpack with its paid launcher/update service.

## Open points

- n4 (design) artwork approval: the reply does not address it explicitly; Randy did not say anyone else must approve. Keep crediting n4 for the design.
- Randy asked for bug fixes as PRs to https://github.com/RandyKnapp/Auga.
- Randy plans an Auga relaunch "in the next few weeks" — re-evaluate AugaSkin vs the official build then.
- Keep the "unofficial fork" notice and credits (code RandyKnapp, design n4, community Unity 6 port by mrcook1e-ai), as promised in the request.

## Author activity and contact

- Active again: upstream commits by `rknappTC` (same commit e-mail as RandyKnapp) 2026-09-23..24, Auga + EAQS integration work.
- Contact: e-mail listed on the public GitHub profile https://github.com/RandyKnapp; Discord https://discord.gg/randyknappmods (Project Auga) / https://discord.gg/ZNhYeavv3C; comment on issue #242.

## Fork scope

- Fork: https://github.com/UberMorgott/Valheim-Mod-Auga-Fork (shipped as AugaSkin) — port to current Valheim / Unity 6 build, UI fixes, integrations (137 commits on top of upstream).
- `LICENSE` here covers only Morgott's own additions (CC BY-NC 4.0); upstream material is excluded.

## Next step

- Send AugaSkin bug fixes upstream as PRs (author's request).
