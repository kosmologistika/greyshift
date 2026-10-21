# The mod host is our own, on wasmtime

Status: accepted

The source is closed, so nobody forks the game the way SS13 and SS14
servers do, and client and server share compiled code, so a server with its
own mechanics would have to ship its own client. Mods are the one door, and
they are the layer our own gamemode rules sit on. The first question in the
2026-10-04 session was whether to build the host or adopt one.

wasvy was the candidate, checked at commit `13b36e0` (v0.0.9, Bevy 0.19,
wasmtime 46). Its guest interface is polled systems, queries, commands and
component get/set. Observers are listed under "may eventually". Its sandbox
docs say that loading a mod gives no security guarantee and that a mod may
reach WASI file io. Everything the rules need most, observers, grants, limits,
is missing, and what it has is what Blood Money needs least.

## The rule

The host is ours, directly on wasmtime's component model, in
`crates/server/src/mods`. wasvy's `wit/wasvy-ecs.wit` is reference material
for the shape of queries and entity handles, and nothing depends on it.

WASI is linked with nothing behind it. The session decided to link none; the
C# tracer (#337, 2026-10-08) amended that, since a .NET component imports
WASI to boot its runtime. So the linker carries `wasmtime-wasi` with no
directories, no network, no environment, stderr to the server's, a clock that
never moves, and the clock's subscribe functions shadowed to trap, because a
sleep would park the server thread for wall time that fuel never counts.

Every call runs under fuel, an epoch deadline and a memory cap, and a module
that trips any of them is a trap, not a hang.

## What it rules out

Adopting a third-party host and growing it. A guest reaching the filesystem,
the network or a WASI clock: time comes only from the host's `now`, the
server clock a `Deadline` is read against. Running mod code anywhere the
host's limits do not apply.

The tracer (#325, !307) measured the cost before the rest was built: a relay
floor of 0.53 us against 0.15 us native, about 0.5 us per row crossing the
boundary. That number is why bulk per-tick scans stay native (ADR 0013)
