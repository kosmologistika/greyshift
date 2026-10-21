# A module is EntitySystems with generated wiring

Status: accepted

The first C# SDK was a `World` handed to static handlers that a `Setup(ModApp)`
registered, named by an `[assembly: Mod]` attribute and found by reflection.
Porting Blood Money onto it showed the cost: every read was paired with an
explicit `Insert` or `Mutate` that was easy to forget, each file carried its
own copy of the live-rule check and the wallet lookup, a JSON context had one
hand-kept line per type, and none of it looked like anything the SS14 authors
this plan counts on (ADR 0017) had written before.

## The rule

A module is a set of `EntitySystem` subclasses, shaped as Robust's:
`[RegisterComponent] partial class XComponent : Component` with `[DataField]`
members, `[Dependency]` fields, `Initialize` subscribing with
`SubscribeLocalEvent`, a `GameRuleSystem<T>` for a rule. A Roslyn generator
in the SDK writes the wiring from those declarations: the registrations, the
marker interface that says what the module may do with each type, the
dependency injection, and the entry point. An author writes no `Setup` and
names no type twice.

Handlers write by changing what a read handed out. The runtime keeps the
bytes each read returned and writes back what differs when the handler
returns and before every write to the host, so the order a handler wrote in
is the order the host sees. Engine records that grant `mutate` are tracked
the same way against a shallow copy.

A system holds no state: an instance field that is not a `[Dependency]` is a
compile error. Systems are rebuilt on every `setup`, so a field would be
module memory by another name (ADR 0014). What the generator cannot wire is a
compile error with an `RS130xx` id, never a trap at load.

The legacy `World` API is deleted, not kept beside the systems. Two ways to
write a module would be two things to document, test and keep in step with
the host, and the one first-party mode already runs on systems.

## Where it departs from Robust

The names are Robust's where the meaning is; where the host differs the
difference stays visible. `AddComp` replaces. `QueueDel` is the only
deletion. An enumeration sees the world as it was when the call began, and
only point reads see the call's own writes. `CurTime` counts from the UNIX
epoch. There are no module-local events, no `Handled` or `Cancel`, and no
prediction (ADR 0016).

## What it rules out

A serializer of our own: own values stay System.Text.Json through a
generated context. A cache of reads across calls. Reflection at load to find
a module's types. A second authoring style for small modules
