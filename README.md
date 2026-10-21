# greyshift

The content of rust-station-13: everything the game loads at runtime, kept
apart from the engine so it can be public and open to contribution while the
engine stays private. The engine checks this repository out as its `assets/`
directory at a pinned commit.

## Layout

| Directory | What |
| --- | --- |
| `prototypes` | Entity, map, gamemode and effect definitions, RON |
| `maps` | Station and test maps, `.map.ron` |
| `textures` | Sprites as `.rsi` sheets (`meta.ron` plus one PNG per state) and loose UI images |
| `audio` | OGG, one `attributions.ron` per directory |
| `locales` | Fluent `.ftl`, one directory per language |
| `fonts`, `ui`, `shaders`, `editor` | Client fonts, the UI theme, WGSL shaders, editor presets |
| `mods/<id>` | A package: `mod.ron`, its prototypes and maps, and `src/` with the C# project the server module is built from |

`mods/blood_money` is the reference package, a whole gamemode written against
the mod SDK.

## Building a package

The engine builds every package's `src/` into `mods/<id>/server.wasm` with
`cargo run -p mod-build`. The C# SDK the projects import will be published into
`sdk/` here, so a package builds from this repository alone; until then a
package builds from an engine checkout.

## Contributing

Open a pull request. Sign your commits off (`git commit -s`), and for art or
audio add the licence and source to the notice beside the files. See
`LICENSES.md` for what applies where
