# C# is the only mod language

Status: accepted

The 2026-10-04 session chose a generated Rust SDK crate as the one
first-class guest language, since the engine is Rust and a serde mirror of
every marked type is the shortest path. After the tracer passed, the audience
question came back: the modders this plan counts on come from SS14 and
scripting, and Rust is too niche for them. On 2026-10-08 C# became the main
language, and on 2026-10-09 the only one, so one SDK gets the attention
instead of two half-kept ones.

## The rule

A module is a C# project built with componentize-dotnet against the
`Rs13.Mods` SDK, written as `EntitySystem`s (ADR 0018). `mods/sdk/wit/typed.wit` is the one world, generated from
the marked types and committed; every value crosses as a WIT record, variant
or enum, and the bindings carry them as plain structs. A module's own values
cross as JSON through System.Text.Json contexts the SDK writes at build from
the attributes on the module's types, since reflection serialization has no
native code under NativeAOT. A component that does not
import the typed world is refused at load.

The engine's own packages are written in C# too: Blood Money is the reference
module, and a first-party mode gets no shortcut an outside author lacks.
Rust stays the language of the engine.

Modules are built from source in CI, never committed. The CI image carries
the .NET SDK and the wasi-sdk; a Mac builds through a linux container
because the NativeAOT-LLVM compiler has no macOS build.

## What it rules out

A Rust SDK or a bytes world beside the typed one. A second guest language
before the first has an author kit (#336). Committing a built module
