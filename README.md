# greyshift

Greyshift is a multiplayer game about keeping a space station and its crew
alive through a shift, in the Space Station 13 tradition. This repository is
the game's content: everything the client and the server load at runtime,
public and open to contribution. The engine that runs it is not here, so the
tree alone is not a playable game.

## Playing

Download a client build from the releases of this repository and start it.
The client prefills the public server's address, and the content it needs
ships inside the build, so nothing here has to be cloned to play.

## What is here

| Directory | What |
| --- | --- |
| `prototypes` | Entity, map, gamemode and effect definitions, RON |
| `maps` | Station and test maps, `.map.ron` |
| `textures` | Sprites as `.rsi` sheets (`meta.ron` plus one PNG per state) and loose UI images |
| `audio` | OGG, one `attributions.ron` per directory |
| `locales` | Fluent `.ftl`, one directory per language |
| `fonts`, `ui`, `shaders`, `editor` | Client fonts, the UI theme, WGSL shaders, editor presets |
| `mods/<id>` | A package: `mod.ron`, its prototypes and maps, and `src/` with the C# project its server module is built from |

The engine pins a commit of this repository and checks it out as its asset
tree, so a change here reaches the game when the engine moves its pin.
`mods/blood_money` is the reference package, a whole gamemode written against
the mod SDK.

## Building a package

A package's `src/` compiles to `mods/<id>/server.wasm`, which the server loads
and which is never committed. The C# SDK the projects import will be published
into `sdk/` here, so a package builds from this repository alone; until then a
package builds from an engine checkout.

## Contributing

Open a pull request. Sign your commits off (`git commit -s`), and for art or
audio add the licence and source to the notice beside the files. See
`LICENSES.md` for what applies where
