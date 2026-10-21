# Licences

`LICENSE` (MIT) covers everything in this repository that is not listed
below: prototypes, maps made here, locales, shaders, the UI theme, the editor
presets and the source of every package under `mods/`.

Art and audio keep the terms of where they came from. The notice sits beside
the files and is what counts; this table says where to look.

| Where | Notice | Terms |
| --- | --- | --- |
| `textures/**` | `meta.ron` in every sheet directory, `.rsi` or not, with `license` and `copyright` | CC-BY-SA-3.0 for everything taken from Space Station 14, CorvaxStation, tgstation and Paradise, the HUD art under `interface/` included. `objects/weapons/fireaxe.rsi` is CC-BY-NC-SA-3.0 and may not be used commercially. A few sheets are CC0-1.0 or CC-BY-SA-4.0, as their `meta.ron` says |
| `textures/interface/emotes` | `attributions.ron` | CC-BY-SA-3.0 |
| `textures/parallax` | `ATTRIBUTION.md` | `nebula.png` CC-BY-SA-3.0 from tgstation; `purple_nebula.png`, `kettle_nebula.png` and `kettle_bg.png` CC0-1.0 |
| `audio/**` | `attributions.ron` in every directory that holds files, one entry per file | As listed: CC-BY-SA-3.0 and CC-BY-3.0 for what came through tgstation and Space Station 14. The tracks under `audio/music` marked Proprietary were generated on a paid Suno plan and belong to Morbo. They ship with the game but are not open content |
| `fonts` | `ATTRIBUTION.txt` | Per font, as listed |
| `maps/stations` | `LICENSE` | Converted from CorvaxStation and Paradise maps and keep their AGPL-3.0 terms |

Two sheets name no licence in their `meta.ron` (`mobs/human.rsi`,
`mobs/ghost_human.rsi`): their origin was not recorded, so treat them as
CC-BY-SA-3.0 like the rest of the mob art until the notice is filled in.

A contribution carries a `Signed-off-by` line (the Developer Certificate of
Origin) and, for art or audio, the licence and source in the notice beside it
