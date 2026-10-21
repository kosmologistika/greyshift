# Module memory is disposable, and state lives in the ECS

Status: accepted

A module is a wasm instance with linear memory, and the obvious place for a
rule's counters is a static in that memory. Then a trap, a hot reload or a
crash in the .NET runtime loses the series score, and every reload has to
carry a migration of the old memory into the new module.

## The rule

A module holds no state the host cannot see. A component it registers is
stored host-side as bytes in `ModComponents` on the entity, a resource in
`ModResources`, and those are the only state a module has. Both are
reflected, so the inspector and BRP see them like anything else. An own
component the module declared replicated also rides a slot (ADR 0015), and
through the slot it reaches clients and a replay. Resources stay on the
server.

A trap means log, re-instantiate, continue: the module comes back as a fresh
instance with `setup` re-run, and nothing on entities is touched. The third
trap inside a minute unloads it instead. A hot reload is the same path, so a
changed module file on a live server is picked up on the next frame with the
round's state intact.

A call is all or nothing. Every write a call makes, engine commands and own
values alike, waits in the call's own queue until the call returns; a trap
takes every one of them back, so a handler that fails halfway leaves nothing
behind. A `Get` after an `Insert` or `Mutate` on the same entity returns the
written value, so a handler reads back what it wrote; a removal and a query
see the world as it was when the call began.

## What it rules out

A module caching anything across calls in its own memory and trusting it.
Migrating guest memory on reload. A partial write from a trapped handler
reaching the world
