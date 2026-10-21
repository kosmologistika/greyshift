# A client module runs no prediction in v1

Status: accepted

Prediction is the engine's hardest contract: the client runs the same code
as the server from the same inputs and reconciles when they differ. SS14's
content layer gets predicted feel by being compiled into the client, which a
closed source cannot offer. The session's decision 3 said prediction stays
native for good. Since most of the game's content is meant to end up in
modules, that would leave most of the game without predicted feel, so the
decision is narrowed here to what v1 does and what the road past it is.

## The rule

v1 runs wasm on the server only. Clients receive data and run no mod code.
Client modules, when they exist, start as presentation: UI, sounds, effects,
what the client draws from the mod state it receives through the slots (ADR
0015). The Blood Money slot machine UI stays native until the client UI API
exists (#334); that API will be an engine-owned DOM styled by flair, deferred
and not designed.

Until a module predicts, a mod gets predicted feel only through engine
substrate it configures: verbs, DoAfter, pickup, storage, movement, status
effects. A rule that wants a new predicted action asks for the substrate.

Prediction for modules is deferred, not ruled out, and the host is shaped so
it stays reachable. A module's state lives in the ECS (ADR 0014), so the
engine can snapshot and rewind an own component like a native one, and it
writes through intents and events (ADR 0013), which is how native prediction
takes its inputs. The road is a shared module run on both sides, the mod
counterpart of the `shared` crate, whose own components ride the slots and
whose calls are deterministic: no `now`, no unseeded randomness, no host
answer that differs by side. A package would ship it beside `server.wasm`,
and only that part reaches clients. It is done when a mod needs it and the
station content shows which actions want it, and it gets its own ADR.

## What it rules out

A mod shipping its own client build. A client module touching predicted
state before the deterministic path exists. Designing the client UI API or
the shared module before a mod needs them
