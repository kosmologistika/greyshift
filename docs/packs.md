# packs and modules

A pack is a directory of content a server runs and a client fetches before joining: prototypes,
maps, locales, art, and a C# project whose module the server runs. The server runs those wasm
modules and lets them observe, read and write the world through a small host interface (#324). The decisions behind it are ADRs 0012 to 0018 under `docs/adr/`. Blood Money is the first mode to live there, whole: its rules, its
prototype-named components, its prototypes, maps and weapons are the `assets/packs/blood_money`
pack, built from the C# project at its root; the engine keeps only the slot machine the
client draws (#331, the UI API is #334). The station mode's events live in `assets/packs/station`
the same way, over the engine's storyteller (`storyteller.md`). The suite's fixture packs are links
to both.

## The boundary

`packs/sdk/wit/typed.wit` is the whole contract, generated from the marked types and committed, and
it lives beside the C# SDK because both are what a module author gets. The guest exports `setup`,
`dispatch` (an engine event), `dispatch-timer` (a scheduled handler by name) and `dispatch-client`
(a client's module event with the sender); the host gives it `observe`, `query`, `get`, `insert`,
`mutate`, `spawn`, `trigger`, the own-value calls, `schedule`, `now` and `log`. Engine values cross
as the WIT records the file declares, a module's own values as JSON bytes, so the host never knows
a guest type and the guest never links bevy. An entity is its `to_bits` number on both sides, the
same number it has inside a serialized component, which is why it is not a WIT resource.

WASI is linked with nothing behind it, since a .NET runtime needs it to boot (see below).

## Grants

A type exists for a module only when it is marked. `shared::mods::ModApi` is the marker, carried as a
reflect attribute on the type, `#[reflect(@ModApi::NONE.read())]`, or registered for a foreign type
with `register_mod_api::<ChildOf>(...)`. The grants are separate: `read`, `insert`, `mutate`,
`observe`, `trigger`. Events that are observable or triggerable also carry `#[reflect(ModEvent)]`,
which is what registers the native relay for the concrete type and fires a decoded value. A
component with `#[reflect(ModLifecycle)]` and `observe` relays its `Add` and `Remove` as
`Add<path>` and `Remove<path>` with the entity as the value.

The host reads the whole registry once every plugin has registered, in `ModHostPlugin::finish`.
A path that is not marked and a path that lacks the grant refuse with the same words, so a module
cannot probe for unmarked types. Engine state changes through events: the rule clears the round
tiebreak by triggering `shop::ClearEarned`, it does not write `Wallet`. `mutate` replaces the value of
a component the entity already holds; write access stays narrower than read access, a mob moves
through the intent event, not through `TilePos`.

Marked types are exactly what the shipped modules touch, added as a port needs them. The suite pins
the list, and the SDK generator writes it out as `packs/sdk/surface.md` beside the SDK.

## Observers run inside the trigger

Each observable event gets one native observer, `surface::relay`. It serializes the event through
reflection and calls the guest's `dispatch` on the spot, with a `DeferredWorld`. Reads see the world
as it is at that moment. Writes to engine components, spawns and triggers go through commands, so
they land when a native observer's would.

## Scheduled handlers

A rule that watches the world every tick (the coin race reading wallets) has no event to observe.
`Every(TimeSpan, handler)` in the SDK asks the host (`schedule`) to call the handler on a
period, under the name `timer:<System>.<Handler>`, through the same `dispatch` and the same trap policy as an
event. A period rather than a frame, since a module pays the relay on every call; the race takes
100 ms, so a wallet at the target ends the match within a tenth of a second rather than in
the frame it got there. Timers tick in every round phase; a handler that only plays during a round checks
`GameTicker.RunLevel` itself, which is what lets one that frees a stranded ghost run through the
wrap-up. A timer reaches the module through its `dispatch-timer` export, under the name the SDK
gave it.

`IGameTiming.CurTime` is the server clock, counted from the UNIX epoch, the one a `Deadline` is
read against, so a module computes its own deadlines without a clock of its own. `Duration` crosses
as the `duration { secs, nanos }` record, what serde writes for it.

## State lives on entities, and a call is all or nothing

Module memory is disposable. A component the module registers is stored host-side as bytes in
`ModComponents` on the entity it was inserted on, a singleton of its own in `ModResources`, and
that is the only state a module has. Both are reflected, so the inspector and BRP see them like
anything else; a replay sees what rides a slot.

Every write a call makes, engine commands and own values alike, waits in the call's own queue
until the call returns. A success lands them the way a native observer's commands would; a trap
takes every one of them back, so a handler that fails halfway leaves nothing behind. Reads inside
the call see its own pending writes, so a handler reads back what it wrote.

A call runs under three limits: a fuel budget (`FUEL_PER_CALL`, guest instructions), an epoch
deadline (`packs.call_deadline_ms` of wall time, ticked every 10 ms by a host thread, so a sleep or
a host-side loop ends too), and a memory cap on the instance (`MEMORY_CAP`). Running out of any is a trap like a panic.
The host's own call into the guest is wrapped so a host-side panic counts as a trap as well, and
the server never goes down because of a module.

A trap is logged with the module and the event, and the module comes back as a fresh instance
with its `setup` re-run. The third trap inside a minute unloads it instead, with a line in the
log and a system line in chat. A hot reload is the same path: `packs.modules` names the files, a
changed one is re-instantiated on the next frame, and `modules reload <name>` in the console asks
for that by hand. Nothing on entities is touched by either.

`modules` in the console lists each module with its traps in the window and, per event since load,
calls, average wall time and average fuel, so an operator sees what a module costs on a live server
with no extra tooling.

## One world, typed

C# is the module language, the only one (#324 as amended on 2026-10-09). `packs/sdk/wit/typed.wit` is
the one world a module binds, `typed-module`: every value is a WIT record, variant or enum the file
declares, and the generated C# bindings carry them as plain structs. A component that does not
import `typed-ecs` is refused at load. There was a bytes world for a Rust SDK during the tracer
slices; it went with #344, so one SDK gets the attention.

The host functions are dynamic (`wasmtime::component::Linker::func_new`), converting each
reflected value by the type the component declared for it, with fields matched by name, kebab-case
for snake_case, a newtype as its inner type, a unit struct as a payload-less case. `crates/server/src/mods/vals.rs` is
that converter and it names no engine type: #327 generates the WIT, the host needs no change per type.
A `component` variant with one case per engine type is how a query row or an insert names its type,
and `component-kind` and `event-kind` enums name a type the same way for `observe`, `query` and
`get`; the host picks the case by the type's short name. A C# module never sees a Rust type path.

A .NET module needs WASI to boot, so the linker carries `wasmtime-wasi` with nothing behind it: no
directories, no network, no environment, stderr to the server's, a clock that never moves, and
the clock's subscribe functions shadowed to trap, since a sleep would park the server thread for
real time that fuel never counts. Setup gets a budget of its own (`FUEL_PER_SETUP`): the .NET
runtime initializing its heap spends about 50M fuel before any `Initialize` runs. A call gets
100M, about 50 ms of guest work, sized for scans: a C# handler spends about 4k fuel per engine row
it enumerates and 13k per own row it writes back, so 100M holds a few thousand rows where 20M
trapped near a thousand (#370). Fuel is the guard that binds on guest work; the deadline is for
host-side stalls, which cost no fuel. A call that holds the server past one frame is logged with
its cost, once per module per trap window, since under the cap nothing else would say so.

C# modules build with componentize-dotnet, whose NativeAOT-LLVM compiler has no macOS build, so a Mac
goes through a linux/amd64 container (`pack-build` does this). CI's image carries the .NET SDK and the
wasi-sdk, so the pipeline builds the C# modules before the suite and their tests run there; locally
they skip while the module is absent.

## Replication through a fixed pool

A stock client knows nothing about a server's modules, so no module type can be a replicated component
of its own: the replicon protocol is hashed from the registered types, and a mismatch refuses the
connection. Instead the engine registers a fixed pool, `ModSlot<0>` to `ModSlot<127>`, each an
ordinary replicated component holding opaque bytes. A module that wants an own component on
clients marks it `[RegisterComponent, NetworkedComponent]`, the host hands it a
slot of the pool, and every write to that component
rides its slot to every client that sees the entity. `ModSlotTable`, a component on one replicated
entity the server spawns, says which (module, type) each slot carries, so a native client system can
find a module's bytes. It is an entity rather than a replicated resource because a reconnecting client
still holds its resources, and replicon refuses to replicate one that already exists.

A slot is a component like any other, so PVS, late join, change detection and replays apply
unchanged; the suite checks the first two. Slots go server to client only. A module keeps its slots
across a trap restart and a reload, and a module that would take the pool past 128 fails to load
with every holder named. What rides a slot is readable by anyone who inspects a client: secrets stay
in non-replicated components.

Nothing inside the bytes is mapped: an `Entity` a module puts in a replicated component or an
event reaches the client as the server's id, and a native reader maps it through
`ServerEntityMap`; the same holds the other way for a client event.

Events use one native pair the same way: `ModServerEvent { kind, bytes }` goes to every client
(`RaiseNetworkEvent`), and `ModClientEvent` comes back from a client, reaching the modules that
declared the kind at setup (`SubscribeNetworkEvent<T>`) through their `dispatch-client` export with the
sender's connection entity, inside a budget of twenty a second per connection.

## The generated SDK

`crates/server/src/mods/sdk.rs` walks the marked registry of a finished server and writes four
files, all committed:

- `packs/sdk/wit/typed.wit`: a record, enum or variant per marked type and everything they reach,
  the `component`, `event` and `trigger-event` variants with one case per type, and the
  `component-kind` and `event-kind` enums. Doc comments come through `reflect_documentation`, or
  the marker's `doc` override. A marker's `fields` keeps a struct to the named fields, which is how
  `PlayerRecord` exposes two of its five
- `packs/sdk-cs/Engine.g.cs`: what an author writes against. A record per marked type with its doc,
  typed ids (`EntityUid`, `PlayerId`) in place of bare integers, and a marker interface per grant,
  so `AddComp<Wallet>` on a type that grants no `insert` does not compile
- `packs/sdk-cs/Engine.Wit.g.cs`: the glue between those records and the bindings
  componentize-dotnet makes from the WIT: the conversions both ways and each type's kind and
  case, lifecycle edges included
- `packs/sdk/surface.md`: the same list as a table of path, grants and doc, for a reader rather
  than a compiler

`cargo test -p tests --test suite -- --ignored generate_mod_sdk` regenerates them, and
`the_generated_mod_sdk_is_current` fails CI while the committed copy is stale, so a change to a
marked type shows as a diff under the SDK in the same MR.

A module carries the types it was built against in its own `typed-ecs` import. The first time it
uses a case, the host compares what it declared against the live shape and traps naming the type
(`` `shared::shop::Wallet` changed since the module was built ``), then remembers the verdict. At
load every declared case is checked once more and drift is logged as a warning, so an operator
sees it before a round does. The check is per case because a generated module declares every
type on the surface: refusing at load would make one changed type break every module, where the
rule is that it breaks only the modules that touch it. That is decision 15's per-type check without
a hash: the shape itself is what gets compared.

A marker's `fields` holds on the host too: a read hands a module only the kept fields, in
declaration order, and a filtered type cannot carry `insert` or `mutate`, since a module could
never write a whole value. A marker with no grant at all is not on the surface: it only narrows
the fields a type shows where another type reaches it (`ScopeOutcome` inside `EndScope` keeps
its headline and places, not the engine's own story lines), and a value a module sends back
fills such a field with its default.

### Writing a C# module

`Rs13.Packs` compiles into the pack project as sources (`packs/sdk-cs/Rs13.Packs.props`), with the
SDK generator (`packs/sdk-cs/Generators`, built by `pack-build` into `target/pack-sdk`) writing the
module's registrations from the attributes on its types. The props find the WIT and the generator
wherever the SDK sits: `pack-build` hands both in, and a copy of the SDK directory with the WIT
under `wit/` builds the generator itself the first time, which is how the published SDK works. There is no entry point to write: a
module is its `EntitySystem` subclasses and the types they keep, shaped as Robust's (ADR 0018).

```csharp
using Rs13.Packs;

namespace BloodMoney;

// Registered as blood_money::Series: the assembly name, then the class name
// with a trailing Component dropped
[RegisterComponent]
public sealed partial class SeriesComponent : Component
{
    [DataField] public Dictionary<PlayerId, uint> Wins = [];
    [DataField] public uint Played;
    [DataField] public EntityUid Board;
}

public sealed partial class BestOfRuleSystem : GameRuleSystem<BestOfRuleComponent>
{
    [Dependency] WalletSystem _wallets = default!;

    protected override void Started(Entity<BestOfRuleComponent> rule, ref RuleStarted args)
    {
        var board = Spawn(Board([]), new ChildOf(rule), new Replicated());
        AddComp(rule, new SeriesComponent { Board = board });
    }

    protected override void NestedScopeEnding(Entity<BestOfRuleComponent> rule, EntityUid scope, ref ScopeEnding args)
    {
        if (!TryComp<SeriesComponent>(rule, out var series))
        {
            return;
        }
        series.Played += 1;
        if (series.Played >= rule.Comp.Matches)
        {
            RaiseLocalEvent(new EndScope(scope, new ScopeOutcome("gamemode-round-won", Ranking(series.Wins))));
        }
    }
}
```

**Own types.** `[RegisterComponent] partial class XComponent : Component` (plus
`[NetworkedComponent]` for a slot), `[RegisterResource]` for a singleton, `[NetSerializable]`
for a client event and `[MirrorComponent("bench_probe::Landed")]` for another pack's public
type under its path. Stored members are public, settable and marked `[DataField]`. They are
JSON on every side, through a `JsonSerializerContext` the props write before compile from the
same attributes: keys are the members' names in snake_case, enums are their names, a `TimeSpan`
is serde's `{"secs","nanos"}`, and a dictionary is a JSON object. That is what a RON prototype
authors (`(kind: Fire, n: 3)`) and what another module's mirror expects. A key the type lacks
traps naming it rather than reading as zero; a mirror reads leniently. The context is generated
because the module is trimmed native code, where the reflection serializer fails on the first
enum or generic property.

**Systems.** A system is partial, has no constructor arguments and holds no state: every field is
a `[Dependency]`, which is ADR 0014 checked at compile time, since systems are rebuilt on every
`setup`. A dependency is another of the module's systems, a built-in, or an `EntityQuery<T>`,
which fetches the holders of a component once per call so a check per row costs nothing.
`UpdatesAfter` orders one system's handlers and timers behind another's.

**Subscribing.** `Initialize` wires the handlers and touches nothing in the world:
`SubscribeLocalEvent<TEvent>`, `SubscribeLocalEvent<TComp, TEvent>` (runs when the event's
target holds the component; `ComponentAdd` and `ComponentRemove` are its edges),
`SubscribeNetworkEvent<T>` for a client's event with the sender's connection entity, and
`Every(period, handler)`, where `TimeSpan.Zero` is every tick. An `Update(frameTime)` override
is scheduled too. Timers run in every round phase, so a handler that only plays during a round
gates itself.

**Reading and writing.** Handlers read with `TryComp`, `Comp`, `CompOrNull`, `HasComp`,
`EnsureComp`, `Resource<T>` and `EntityQueryEnumerator<T1..T4>`, and write by changing what a
read handed out: the runtime writes a changed value back when the handler returns and before
every write to the host. `AddComp` replaces, `RemComp` takes away, `Spawn(components)` and
`Spawn(EntProtoId)` make an entity, `QueueDel` is the only deletion, `RaiseLocalEvent` triggers
an engine event the module was granted, and `RaiseNetworkEvent` sends an own event to every
client or to one. Point reads see the call's own adds, removes and edits; enumerations see the
world as it was when the call began.

Write-back covers engine records too: `Wallet`, `HudScoreboard` and `ShopOffer` grant `mutate`,
so their members are settable, the runtime keeps a shallow copy of what a read handed out and
sends a `mutate` when the value differs. `wallet.Balance -= 10; _shop.ExecutePurchase(...)`
therefore queues the wallet first, and the engine's purchase debits what the handler left. The
mutate replaces the whole record, so the other order, a change made after a host write that
touches the same component, replaces what that write did when the queue commits: change first,
then call.

**Rules.** A rule is a `GameRuleSystem<TRule>` over the component its prototype names. `Started`
runs on the rule's `RuleStarted`, `NestedScopeEnding` when a scope directly under the rule's
live scope ends, `Ending` when the rule's own scope ends (it already carries `ScopeEnded`, so
the rule is no longer active), `ActiveTick` every tick and `EveryActive` on a period, each once
per active rule. `EveryActive` is silent while no rule is live: a handler that must keep a clock
moving or drain a pool through an intermission is a plain `Every`.

**Built-ins.** The engine substrate a gamemode leans on stays engine code behind a call, taken
as a `[Dependency]` like a system: `GameTicker` (`RunLevel`, `IsRoundLive`,
`IsGameRuleActive<T>`), `IGameTiming` (`CurTime` is the server clock), `IPrototypeManager`
(`Name`, `Sprite`, `Declares`, `RoleBody`), `ShopSystem` (`Listings`, and `ExecutePurchase` and
`CloseOffer`, the same money and `Purchased` a client's own buy moves), `MindSystem`
(`TransferControl`), `GhostSystem` (`SpawnGhost`), `SeatSystem` (`SeatsFor`, `ObserverSeat`,
`SpreadOver` for where a body lands), `FlightSystem` (`LaunchRay` puts an entity into flight
the way a gun or a throw does, `ThrowFromSpace` spawns a prototype past the hull and flies it in),
`AreaSystem` (`AreaOf` a tile, `StandingFloor` of a room), `DoAfterSystem` (`TryStart` a windup on
an actor with its breaks and duplicate policy, handing back the windup entity the module keeps its
payload on as an own component, and `Cancel`; `DoAfterFinished` on that entity says how it ended),
`PopupSystem` (`Note` and `Caution` off an entity for the player controlling it, by locale key),
`BodySystem` (`Ledger`: a body's wounds by kind, its parts' included), `AudioSystem` (`Play` at a
tile) and `TransformSystem` (`GridPosition` finds the tile under an entity however deep it is
stored). `Seed.Mix` is the
seeded roll.

**Diagnostics.** A wiring mistake is a compile error with an `RS130xx` id: a system that is not
partial or holds state, a `[Dependency]` the generator cannot fill, an own type that is not
partial or that a store cannot round-trip, two types on one path, an edge subscription the host
does not relay (`RS13010`), and as a warning a world call from `Initialize` or an override that
skips a base system's `Initialize` (`RS13008`). `packs/sdk-cs/Generators.Tests` pins each one, and
`pack-build` runs those tests before it compiles a module.

`packs/bench_probe/Module.cs` is the shape whole, `packs/fault_probe/Module.cs` a mirror and
resources, `packs/template` the smallest pack that runs (one rule, one own component, a
prototype and a locale line, with the author kit in its README), and Blood Money's pack a whole
mode: four `GameRuleSystem`s, a cart, loadout, death cam and stranded-ghost system ordered by
`UpdatesAfter`, and a `WalletSystem` the others take as a `[Dependency]`.

## Packs

A pack is a directory under `assets/packs/` (`tests/packs/` for the suite), reachable
from asset paths as `packs://<id>/...`. `assets/` is the public content repository
(`github.com/kosmologistika/greyshift`), and a pack is its C# project: the csproj and the
sources at the root, the data they refer to beside them, so an IDE shows the whole pack as one
project:

```
assets/packs/blood_money/
    blood_money.csproj  the project the module is built from; a release ships without it
    *.cs                the systems and their own types
    pack.ron             the manifest
    server.wasm         the module, loaded by the server only, built by pack-build and never committed
    prototypes/         RON files the prototype loader layers over the base game
    maps/               the mode's maps
```

What the project is made of is never an asset: `shared::mods::pack::is_project_file` (`*.cs`,
`*.csproj`, `nuget.config`, the IDE's `*.user`, `*.sln`) with `is_project_dir` (`bin/`, `obj/`,
`Properties/` at the pack root only, so a data directory of that name deeper down is data) is
the one rule the overlay, the pack sync, the release archives and the server image share.

`pack.ron` names the pack and what it brings:

```ron
(
    id: "blood_money",
    version: "0.1.0",
    dependencies: { "probe": ">=0.1" },   // semver requirements on other packs
    after: ["probe"],                     // load order without a hard dependency
    components: ["blood_money::Series"],      // own types a prototype may name
    public: ["blood_money::Series"],          // own types other modules may read
    overrides: ["tool_crowbar"],          // base prototype ids this pack replaces
)
```

`PacksPlugin` reads every manifest at build, checks dependencies and orders the set: by
dependencies and `after`, by id where nothing else decides. The id must be the directory name,
since `packs://<id>/...` is the asset path. A missing or incompatible dependency, a cycle, or an
id that is not its directory refuses the whole set, the server exits with the reason and the
client and editor log it and run without packs. `after` only orders: one naming a pack
that is not installed is skipped. Modules load in pack order before anything in
`packs.modules`, so a pack's observers run in that order; `packs.enabled false` turns them
off for a test that brings its own module file.

Prototypes layer: the base game is layer 0, each pack the next one up. A prototype id that
appears on two layers is a clash unless the upper pack lists it in `overrides`, so a pack
cannot replace an item by accident; a declared override wins and the base one is gone. A
component named in `components` has no Rust type on either side: the loader turns its RON body
into the JSON serde would write for the same Rust value (`Fire` to `"Fire"`, `Thrust(len: 1)`
to `{"Thrust": {"len": 1}}`, `Some(1)` to `1`), spawning puts it into `ModComponents` under the
path, and the module reads it like any own value it wrote itself. That works because a module's
own values are JSON on every side, written by the module's own `JsonSerializerContext` with
snake_case names and string enums. A type in `public` is readable by every module, `components` of
the reader or not, once the pack's own module has registered it (a manifest cannot publish
what another module owns), and the reader mirrors its layout by hand. Both lists are promises
about the module: once its setup has run, a path in `components` or `public` it never registered
refuses the load naming every missing one (`ModHost::load_error`), so a misspelled `XComponent`
path is reported at load rather than sitting unread mid-round. The pack's prototypes still
load and author the path, since manifests are read at build; the refusal is the signal.

Everything else in a pack is an asset like the base game's. The default asset source reads
through the packs onto `assets/`, upper layer first, so a pack's prototype names its sprite
or sound by a bare path (`textures/objects/tools/my_tool`, `audio/items/my_tool.ogg`) and the
file is found inside the pack, and a folder the engine scans (`locales/`) lists what every layer
holds, so a pack's `locales/en-US/*.ftl` joins the catalog. A map is a `MapPrototype` under the
pack's `prototypes/maps/` whose file sits under its `maps/`. A file at a path the base game or a
lower pack also has replaces it, and must be declared in `overrides` like a prototype id,
exactly or by glob (`audio/items/*.ogg`, `textures/**/icon.png`); an undeclared shadow refuses
the set naming the file. `packs://<id>/...` still reaches a pack directly. A pack's
`prototypes/`, `pack.ron` and `.wasm` are not served through the overlay.

A client gets a pack's data with the rest of the assets and never its module: the release
archive drops the `.wasm` files and keeps the directories.

### Pack sync

A stock client joining a server that runs a pack it does not carry fetches it before it logs
in, the fastdl shape. At boot the server hashes each pack's client-side files (everything but
`server.wasm` and what its project is made of, in sorted order with their paths,
`shared::mods::sync::hash_pack`)
and, when `packs.archive_dir` is set, writes `<id>-<hash>.tar.gz` of those files there for a static
host to serve, beside its name and renamed in so a boot cut short leaves no half archive. The
moment a client connects, before any login, the server sends it a `PackManifest` (an
independent replicon event, so it reaches an unauthorized client): the server's protocol hash,
then per pack id, version, hash, size and, when `packs.download_url` is set, the tarball's URL,
in load order. A client of another build is refused on the protocol before anything is fetched,
with the mismatch reason it would have had from the login.

The client checks each entry's id and hash before a path is built from them, compares against the
packs it carried under `assets/packs` at boot (kept in `CarriedPacks`, since a synced set
replaces `Packs`) and against its cache (`<data dir>/packs/<id>-<hash>/`, or
`packs.cache_dir`, made absolute), hashing a cache hit again so a copy cut short is fetched over,
and fetches the rest on a thread of its own, one tarball at a time: a pack past
`packs.cap_mib` is refused before the request, the archive unpacks into a directory of
that attempt's own, bounded by the declared size, is hashed with the same function the server used
and moved under its name only when it matches, keeping a verified copy that got there first. A
disconnect sets a flag the thread checks between steps. Every fetch of a join shares
`packs.sync_timeout_secs`; while the sync runs the client is in `JoinState::FetchingPacks`, the
join watchdog is not armed, and the server adds its own `packs.sync_timeout_secs` to the login
deadline of a connection while it offers downloads. A pack the client lacks that the server
offers no URL for, a mismatch, a cap or a timeout refuses the join with the pack named, and the
connection screen says so (`connect-reason-pack-sync`).

With the set in place the client replaces its `Packs` with the server's, in the server's
order, swaps the asset layers (`shared::asset::PackLayers`, which the overlay reader and the
`packs://` source read through on every call, so a cached pack serves from the cache directory
like a shipped one), refreshes the prototype-named components, and runs the prototype folders
again as a diffed reload (`RelayerPrototypes`, which fires `PrototypesReloaded` and not
`PrototypesLoaded`, so the screen flow stays where it is). bevy hands a loaded folder back by
path, so a pack folder is asked for as `packs://<id>~<generation>/prototypes` with the layer
set's generation once a swap has happened (the first set keeps the bare path the file watcher
reports), and a swapped set is a new path to it. A pack's `locales/` are its own the same way,
not served through the overlay: the locale plugin loads the base game's folder plus one
`packs://<id>~<generation>/locales` per pack that brings any, pack files overriding the
base game's, and asks for them again on `PacksSynced`. The manifest is the first replicon
message the shared plugin registers, so its channel is the same in every build and the protocol
check in it reaches a client of another build. Only then
does `LoginRequest` go out: the login is an observer of `PacksSynced`, not of the transport
connecting. A client that already carries everything the server runs syncs in one tick and
fetches nothing; base assets never sync, a client still ships `assets/`.

A sync also prunes the cache: of a pack the server runs only the version in use stays, and
what other servers left is trimmed to `packs.cache_cap_mib` least recently used first, a `.used`
marker's mtime being the clock. A client holds a shared lock on a `.lease` file in every cache
directory it runs on, and a prune takes the exclusive lock before deleting, so two clients
sharing the default cache prune around each other; an attempt directory nobody finished goes
once it is older than a sync timeout. The archives themselves are immutable and named by hash, so
any static host or CDN in front of `packs.download_url` may cache them forever.

Textures and sounds a fetched pack brings load lazily through the swapped layers. An asset
the client already loaded under the same path keeps the loaded copy until it is reloaded: a
pack overriding a base file, or a new version of a pack whose sprites the client drew
before (the render crate retains every icon-resolved RSI). That is the one place this falls short
of a restart; a version change is served whole on the next launch.

## Measuring a module

```
cargo bench -p bench --bench mod_relay --bench mod_load --bench mod_tick --bench mod_boot
```

`mod_relay` times one event crossing into Blood Money against the native observers it replaced,
`mod_load` many modules on one event and one module over many rows, `mod_boot` a fresh instance.
`mod_tick` is the per-tick and per-row side: a timer dispatch with and without a resource write,
a whole server tick with Blood Money against the base game, a four-wide scan, own rows read and
written back, a per-row `HasComp` through an `EntityQuery` and through the host, and eight systems
of one module on one event. Every row loads `packs/bench_probe`, so a new shape is a knob value on
its `ProbeSystem` and an entity in `bench/src/lib.rs` that carries it.

Each bench prints the fuel its calls burnt beside criterion's time, from the same counters the
`modules` console shows, so a guest that got slower in instructions is told apart from a host that
got slower in wall time. A bench whose body dispatches more than one path, a whole tick, prints
each path's fuel under the total, so the timer that grew is named. A module that traps is restarted by the host, and the third trap unloads
it, so every bench makes one untimed call first and fails on a trap, or on a call no loaded module
answered, before criterion measures or saves anything; the same check runs again after the loop
for a trap that only shows up under repetition
