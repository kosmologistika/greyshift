# A pack, from this template to a pull request

A pack is its C# project: a directory under `packs/` with the csproj and
the sources at its root and the data they refer to beside them, so opening
the project in an IDE shows the whole pack. This directory is one you copy:

```
template/
    template.csproj      the project; builds to ./server.wasm
    Module.cs            the systems and their own types
    pack.ron              the manifest: id, version, the own types a prototype may name
    prototypes/          RON layered over the base game: a gamemode and its rule here
    locales/en-US/       Fluent lines the prototypes and the module refer to
```

The engine serves the data and never the project: `*.cs`, `*.csproj`,
`nuget.config`, what an IDE leaves beside them (`*.user`, `*.sln`,
`Properties/`) and the root `bin/` and `obj/` are not assets, do not ship and
do not sync.

Copy it to `packs/<id>`, then rename: the `id` in `pack.ron` must be the
directory name, the `AssemblyName` in the csproj is the same id and the prefix
of every own type path (`template::GreetingRule` becomes `<id>::GreetingRule`,
in the manifest and in the prototypes alike).

## Build

The module compiles with the .NET 10 SDK and componentize-dotnet, which pulls
the NativeAOT-LLVM compiler from the experimental feed `nuget.config`
names and the wasi-sdk on first use. The compiler pack exists for Linux
only.

Linux:

```
cd packs/<id>
dotnet build -c Release
cp bin/Release/net10.0/wasi-wasm/native/<id>.wasm server.wasm
```

macOS, through a linux/amd64 container (OrbStack or Docker Desktop):

```
docker run --rm --platform linux/amd64 -v "$PWD:/content" -w /content/packs/<id> \
  mcr.microsoft.com/dotnet/sdk:10.0 dotnet build -c Release
```

The project imports `sdk/Rs13.Packs.props` from the repository root, which
compiles the SDK in as sources and builds the generator in `sdk/Generators`
the first time. A wiring mistake is a compile error with an `RS130xx` id; a
type the engine does not grant does not compile either, since the records in
`sdk/Engine.g.cs` are the whole surface. `docs/surface.md` lists it with the
grants, `docs/packs.md` is the authoring guide.

The repository's own CI builds every pack's project and this template on
every push and pull request, so a pack that does not compile against the
published SDK fails there with the type named.

## Run

The server loads `packs/<id>/server.wasm` beside the manifest and lists the
gamemodes the pack's prototypes declare. With a build of the game, point
it at this repository as its asset tree (`assets/` in the engine checkout is
this repository) and start the server with the pack's gamemode selected.

## Pull request

Keep the module's build output out of the commit (`server.wasm`, `bin/`,
`obj/` are ignored), sign the commits off (`git commit -s`), and for art or
audio add the licence and source to the notice beside the files. CI green and
one maintainer's approval merge it; the engine moves its content pin after
