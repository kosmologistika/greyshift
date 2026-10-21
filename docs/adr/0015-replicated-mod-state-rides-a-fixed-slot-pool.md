# Replicated mod state rides a fixed slot pool

Status: accepted

A stock client has to join a modded server without installing anything, and
replicon hashes its protocol from the registered component types, so a
mismatch refuses the connection. A mod component registered as a replicated
type of its own would make every server's protocol different and every
client a per-server build, which is the problem mods exist to avoid.

## The rule

The engine registers a fixed pool, `ModSlot<0>` to `ModSlot<127>`, each an
ordinary replicated component holding opaque bytes, and one generic mod
event each way. The protocol is identical with or without mods. A module that
wants an own component on clients asks at setup, the host hands it a slot,
and every write to that component rides the slot to every client that sees
the entity. `ModSlotTable`, on one replicated entity, says which module and
type each slot carries, so a native client system can find a mod's bytes.

A slot is a component like any other, so PVS, late join, change detection
and replays apply unchanged. Slots go server to client only. A module keeps
its slots across a trap restart and a reload, and one that would take the
pool past 128 fails to load with every holder named.

The table is an entity, not a replicated resource, because a reconnecting
client still holds its resources and replicon refuses to replicate one that
already exists.

## What it rules out

A mod type in the replication registry. Secrets in a replicated mod
component: what rides a slot is readable by anyone who inspects a client.
Entity ids inside the bytes being mapped for free; a native reader maps them
itself
