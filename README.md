# greyshift

Greyshift is a multiplayer game about keeping a space station and its crew
alive through a shift, in the Space Station 13 tradition. This repository is
the game's content: everything the client and the server load at runtime,
public and open to contribution. The engine that runs it is not here, so the
tree alone is not a playable game.

## Playing

Download a client build from this repository's releases: each release
carries one archive per platform (`x86_64-windows`, `x86_64-linux`,
`aarch64-macos` for Apple Silicon) with the `client` and the `editor` inside,
and is tagged at the content commit the build ships. The client prefills the
public server's address, and the content it needs ships inside the build, so
nothing here has to be cloned to play.

### Running

Extract the archive and run a binary from inside the extracted folder: both
load everything from the `assets` directory beside them, so one moved out on
its own has nothing to load.

The builds are not code-signed, so each OS objects the first time.

- **Windows**: SmartScreen shows "Windows protected your PC". Click "More
  info", then "Run anyway".
- **macOS**: Gatekeeper refuses the first launch. Right-click the binary,
  choose "Open", then confirm in the dialog; a double-click does not offer
  that choice. If macOS still blocks it, clear the quarantine flag on the
  extracted folder: `xattr -dr com.apple.quarantine <folder>`.
- **Linux**: mark the binaries executable if your extractor dropped the bit:
  `chmod +x client editor`.

## What is here

| Directory | What |
| --- | --- |
| `prototypes` | Entity, map, gamemode and effect definitions, RON |
| `maps` | Station and test maps, `.map.ron` |
| `textures` | Sprites as `.rsi` sheets (`meta.ron` plus one PNG per state) and loose UI images |
| `audio` | OGG, one `attributions.ron` per directory |
| `locales` | Fluent `.ftl`, one directory per language |
| `fonts`, `ui`, `shaders`, `editor` | Client fonts, the UI theme, WGSL shaders, editor presets |
| `packs/<id>` | A pack: the C# project its server module is built from, with `pack.ron`, its prototypes, maps and the rest of its data beside the sources |
| `sdk` | The mod SDK: the `Rs13.Packs` sources, the WIT contract and the generator a pack project imports |
| `template` | A pack to copy, with the author kit in its README |
| `docs` | The mod authoring guide, the decisions behind the mod host, the grants list and the SDK changelog |

The engine pins a commit of this repository and checks it out as its asset
tree, so a change here reaches the game when the engine moves its pin.
`packs/blood_money` is the reference pack, a whole gamemode written against
the mod SDK.

## Building a pack

A pack's project compiles to `packs/<id>/server.wasm` beside it, which the
server loads and which is never committed. The project imports
`sdk/Rs13.Packs.props`, so a pack builds from this repository alone with the
.NET 10 SDK, and opening the csproj in an IDE shows the whole pack;
`template/README.md` is the whole path from a copy of the template to a pull request. `sdk/`,
`template/` and `docs/` are published here by the engine's CI on every change
to the mod surface and are not edited in this repository: a change to them
is overwritten by the next publish. `docs/CHANGELOG.md` says what each
publish changed.

## Contributing

Open a pull request. Sign your commits off (`git commit -s`), and for art or
audio add the licence and source to the notice beside the files. See
`LICENSES.md` for what applies where
