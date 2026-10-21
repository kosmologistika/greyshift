# The mod surface is opt-in, and every grant is named

Status: accepted

A module could have seen the whole reflected registry: every component, every
event, read and write alike. That is what wasvy offers and it is the cheapest
to build. It also makes every type in the engine a public contract the day a
module names it, and it lets a rule write `Damageable` or `Dead` directly,
past every gate the engine runs on its own paths.

## The rule

A type exists for a module only when it is marked, `#[reflect(@ModApi::...)]`
on the type or `register_mod_api::<T>()` for a foreign one, and the grants are
separate: `read`, `insert`, `mutate`, `observe`, `trigger`. A path that is not
marked and a path that lacks the grant refuse with the same words, so a module
cannot probe for what exists.

The session's decision 4 said guests write engine state only through intent
and trigger events. The port amended that in practice: an event is used where
a gated path exists (the rule clears the round tiebreak by triggering
`ClearEarned`, the series ends a scope with `EndScope`), and where none does,
a narrow write grant on a named type stands in. `Wallet`, `ShopOffer` and
`HudScoreboard` carry `mutate`; `TilePos`, `Facing`, `Role`, `Appearance`
and a few placement types carry `insert`, so a rule can seat a fresh body and
lay out its loadout. `mutate` replaces a component the entity already holds,
`insert` lays one, and a write grant never comes with a read grant for free.
The only state a module shapes freely is its own: the components and
resources it registered, stored host-side as bytes.

A write grant is a decision, taken when a port needs it and listed in the
port's MR, never a default. No move intent is on the surface yet; a module
that wants a mob to walk rather than appear is the next port's reason to add
one.

Marked types are exactly what the shipped modules touch, added as a port needs
them. The suite pins the list and the generator reads it, so a surface change
shows up in review (#324 decision 15). There is no stability promise on it
in 0.x.

Substrate is native, rules and event-driven props are modules. The test:
prediction, a bulk per-tick scan, or rendering code means native. A room-level
atmos or a station-wide power grid passes the test and may be a module,
decided after the port (#335).

## What it rules out

A module reading or writing a type nobody marked. A write grant granted for
symmetry rather than for a port that needed it. A module predicting anything
in v1 (ADR 0016). A whole-registry surface that makes every internal type an
API by accident
