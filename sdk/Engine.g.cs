// Generated from the marked types, see crates/server/src/mods/sdk.rs. Do not edit

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Rs13.Mods;

/// <summary>
/// The entity this one is a child of. Despawning the parent despawns it
/// </summary>
[JsonConverter(typeof(ChildOfJson))]
public sealed partial record ChildOf(EntityUid Value) : IEngineComponent, IReadable, IInsertable, ISpawnable;

/// <summary>A <see cref="ChildOf"/> is its bare value in JSON, as serde writes it, as a value and as a dictionary key</summary>
public sealed class ChildOfJson : JsonConverter<ChildOf>
{
    public override ChildOf Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(new EntityUid(reader.GetUInt64()));

    public override void Write(Utf8JsonWriter writer, ChildOf value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Value.Bits);

    public override ChildOf ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(new EntityUid(ulong.Parse(reader.GetString()!, CultureInfo.InvariantCulture)));

    public override void WriteAsPropertyName(Utf8JsonWriter writer, ChildOf value, JsonSerializerOptions options) =>
        writer.WritePropertyName(value.Value.Bits.ToString(CultureInfo.InvariantCulture));
}

/// <summary>
/// Marks an entity for replication to clients
/// </summary>
public sealed partial record Replicated : IEngineComponent, IInsertable, ISpawnable;

/// <summary>
/// This connection is playing the current round. The `lobby` writes it at the
/// round-start edge and for late joins; gamemode rules read it and nothing else,
/// so no rule re-derives participation from ready-up state that was already
/// resolved before any rule existed.
/// 
/// It lives on the connection entity, so a disconnect takes it away for free -
/// "playing" and "still here" are the same question.
/// </summary>
public sealed partial record Participant : IEngineComponent, IReadable;

/// <summary>
/// Stable opaque player identity, assigned by the server per username at
/// first login and kept for the server's lifetime. Survives reconnects;
/// the transport-level `NetworkId` does not.
/// </summary>
[JsonConverter(typeof(PlayerIdJson))]
public readonly partial record struct PlayerId(ulong Value);

/// <summary>A <see cref="PlayerId"/> is its bare value in JSON, as serde writes it, as a value and as a dictionary key</summary>
public sealed class PlayerIdJson : JsonConverter<PlayerId>
{
    public override PlayerId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetUInt64());

    public override void Write(Utf8JsonWriter writer, PlayerId value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Value);

    public override PlayerId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(ulong.Parse(reader.GetString()!, CultureInfo.InvariantCulture));

    public override void WriteAsPropertyName(Utf8JsonWriter writer, PlayerId value, JsonSerializerOptions options) =>
        writer.WritePropertyName(value.Value.ToString(CultureInfo.InvariantCulture));
}

/// <summary>
/// Server-private player record. Never replicated; usernames stay off the
/// wire until something client-side consumes them.
/// 
/// `body` is whatever the player currently controls. It may go stale
/// (despawned while the player was offline); liveness is re-checked at
/// login, and the round cycling back to the lobby clears it for everyone.
/// `connection` is the connection entity the player is online through.
/// </summary>
public sealed partial record PlayerRecord(string Username, PlayerId Id) : IEngineComponent, IReadable;

/// <summary>
/// Reference to a state inside an `.rsi/` folder. `rsi` is the
/// extension-less path to the folder relative to the asset root (e.g.
/// `"textures/structures/walls/solid"` → resolves to
/// `textures/structures/walls/solid.rsi/meta.ron` and the per-state
/// PNGs alongside). `state` names a state declared in that meta.
/// </summary>
public readonly partial record struct RsiRef(string Rsi, string State);

/// <summary>
/// Z-layer for the renderer. Higher draws on top.
/// </summary>
public enum DrawDepth
{
    Floor,
    FloorOverlay,
    /// <summary>
    /// Fixed to the deck under whatever stands on it: an open firelock's frame,
    /// a blast door's track. Over the decals, under walls and grilles, where
    /// tg's BELOW_OPEN_DOOR_LAYER and BLASTDOOR_LAYER sit
    /// </summary>
    FloorFixture,
    Walls,
    /// <summary>
    /// Painted onto a wall rather than standing in front of it: blood spatter,
    /// graffiti, scorch. Above `Walls` so it covers the wall's own art, below
    /// everything that stands on the floor, so a mob in front still occludes it.
    /// </summary>
    WallOverlay,
    Doors,
    /// <summary>
    /// Below the floor clutter, so a wrench on a tabletop outranks the table
    /// </summary>
    Furniture,
    BelowMobs,
    Mobs,
    /// <summary>
    /// Bolted to a wall above head height. Above `Mobs` because the fixture
    /// shares the tile a mob stands on, and under them it reads as lying on the
    /// floor at their feet
    /// </summary>
    WallMounted,
    Effects,
    Overlays,
}

/// <summary>
/// How the lighting passes treat a sprite, weakest claim first: a layer merges
/// with the body it covers by taking the stronger of the two. Anything past
/// `Lit` draws on an overlay above the lit world, so it ignores `depth` and
/// covers whatever stands in front of it
/// </summary>
public enum Shading
{
    /// <summary>
    /// Lit and cut off at the edge of what the eye can see, like everything else
    /// </summary>
    Lit,
    /// <summary>
    /// Glows in the dark but obeys line of sight: skips the lighting multiply,
    /// keeps the FOV mask. What status lights, indicators and screens want.
    /// SS14's name for the same thing; PBR would call it emission, except this
    /// emits nothing - lighting what is around it is still `PointLight`'s job
    /// </summary>
    Unshaded,
    /// <summary>
    /// Unshaded, and throws a soft glow around itself. For the lit part of a
    /// light fixture, so a lamp reads as on; a screen or an indicator stays
    /// `Unshaded` and keeps its edges
    /// </summary>
    Glow,
    /// <summary>
    /// Skips the FOV mask as well, so the sprite reads *through walls*. A
    /// gameplay claim rather than a brightness one: only for something that
    /// should be seen with no line of sight, like a ghost
    /// </summary>
    Unlit,
}

/// <summary>
/// Eight-way grid direction.
/// </summary>
public enum Dir
{
    North,
    South,
    East,
    West,
    NorthEast,
    NorthWest,
    SouthEast,
    SouthWest,
}

/// <summary>
/// Non-linear standard RGB with alpha.
/// <div>
/// </div>
/// </summary>
/// <param name="Red">The red channel. [0.0, 1.0]</param>
/// <param name="Green">The green channel. [0.0, 1.0]</param>
/// <param name="Blue">The blue channel. [0.0, 1.0]</param>
/// <param name="Alpha">The alpha channel. [0.0, 1.0]</param>
public readonly partial record struct Srgba(float Red, float Green, float Blue, float Alpha);

/// <summary>
/// One sprite over the base. Everything it does not name is inherited from the
/// entity's [`Appearance`] - the RSI, the draw depth, the shading - so a layer
/// is usually two fields
/// </summary>
/// <param name="State">State in the RSI, the one field with no sensible default</param>
/// <param name="Key">Names this layer for the system that owns it, and the only way to address one. `None` for a layer nothing ever rewrites, like a lamp's glow</param>
/// <param name="Rsi">Draw from another RSI than the body's - what a damage overlay wants, its art being one sheet shared by everything that can be hurt</param>
/// <param name="Visible">A declared layer waiting for its moment (`visible: false` on an airlock's light) costs no child entity until a visualizer shows it</param>
/// <param name="Shading">Raised over the body's own where it asks for more, never lowered. See [`Shading`]</param>
/// <param name="Facing">Pick this atlas row rather than following the entity's `Facing`. A smoothed tile packs its four quadrants as the four directions of one state, so each quadrant asks for a fixed one</param>
/// <param name="Tint">Multiplied over this layer alone, so a grayscale overlay can be red on a body and white on a wall. Falls back to the entity's [`SpriteTint`]</param>
/// <param name="Order">Draw order against the other layers, low first. Negative for anything standing in for the base itself</param>
/// <param name="Displacement">The displacement map that fits this layer to the body under it, a worn jumpsuit pinched to a female torso. Written by the worn visualizer</param>
public readonly partial record struct SpriteLayer(string State, string? Key, string? Rsi, bool Visible, Shading Shading, Dir? Facing, Srgba? Tint, sbyte Order, RsiRef? Displacement);

/// <summary>
/// What an entity is supposed to look like: a base sprite, a draw depth, and
/// the layers over it. Carried across the wire so the client and the editor
/// compose sprites the same way; the server itself never renders.
/// 
/// Immutable, so a change arrives as a fresh Insert and the renderer derives
/// from it. What is actually on screen is one `DrawnSprite` per drawn sprite,
/// which the render crate owns
/// </summary>
/// <param name="Shading">How the lighting passes treat this entity. See [`Shading`]</param>
/// <param name="SpriteOffsetRadians">Radians to add to a target world rotation so the sprite ends up pointing the way callers expect. Encodes the asset's "natural" orientation: 0 for an east-pointing sprite, -PI/2 for north-pointing, and so on. Read by any system that rotates an entity to face a direction (currently the smooth-render projectile path).</param>
/// <param name="Layers">Sprites drawn over `sprite`: a door's status light, a lamp's glow, a damage overlay. A prototype declares them and client-side visualizers rewrite the ones they own, through [`edit_appearance`][AppearanceCommands::edit_appearance] Write through [`set_layer`][Self::set_layer], which keys them; a bare push can seat two layers under one key</param>
/// <param name="HideBase">Draw nothing for `sprite` itself and let the layers be the whole picture. What a smoothed tile wants</param>
public sealed partial record Appearance(RsiRef Sprite, DrawDepth Depth, Shading Shading, float SpriteOffsetRadians, IReadOnlyList<SpriteLayer> Layers, bool HideBase) : IEngineComponent, IReadable, IInsertable, ISpawnable;

/// <summary>
/// An instant on the [`ServerClock`], as a `Duration` since the UNIX epoch.
/// Every read takes the clock, so no side can reach its own wall clock by accident
/// </summary>
public readonly partial record struct Deadline(TimeSpan EndsAt);

/// <summary>
/// [`FadeIn`]'s exit twin, with fire: the entity is burning away and will be
/// despawned when `until` passes. The server owns the clock and the despawn;
/// the client renders whatever burning means (a pixel dissolve). Consumers
/// insert this instead of despawning outright, so nothing pops
/// </summary>
public sealed partial record BurnOut(Deadline Until, float Secs) : IEngineComponent, IReadable, IInsertable, ISpawnable;

/// <summary>
/// Kind of harm a hit carries, the axis armor blocks along; new variants are
/// added as content needs them
/// </summary>
public enum DamageType
{
    Blunt,
    Slash,
    Piercing,
    Heat,
    /// <summary>
    /// Not breathing: what a downed body dies of, unarmored and unseen
    /// </summary>
    Asphyxiation,
    /// <summary>
    /// A poison breathed or swallowed, unarmored and unseen like asphyxiation
    /// </summary>
    Toxin,
}

/// <summary>
/// A hit landed: what `entity` takes after every modifier. Announced before
/// the ledgers change, so a consequence sees the world the blow arrived in -
/// the killing blow still bleeds, leaks and screams
/// </summary>
public readonly partial record struct DamageDealt(EntityUid Entity, IReadOnlyDictionary<DamageType, uint> Spec, EntityUid? Origin, System.Numerics.Vector2? Heading, EntityUid? Weapon) : IEngineEvent, IDirectedEvent
{
    EntityUid IDirectedEvent.Target => Entity;
}

/// <summary>
/// Marker for a deceased body. Sits on the corpse alongside its accumulated
/// `Damageable`. Future revive systems (defib, clone) remove it; future
/// medical systems (autopsy, organ harvest, undertaker NPCs) query for it.
/// </summary>
public sealed partial record Dead : IEngineComponent, IReadable;

/// <summary>
/// A controlled body just died, with the kill resolved against the freshness
/// of its `LastHitBy` stamp. `killer` is `None` for unattributed deaths -
/// environment, self, or a stale stamp - which score nobody. Server-triggered;
/// gamemodes observe it for scoring and bounties
/// </summary>
/// <param name="Entity">The corpse</param>
public readonly partial record struct Killed(EntityUid Entity, PlayerId Victim, PlayerId? Killer) : IEngineEvent, IDirectedEvent
{
    EntityUid IDirectedEvent.Target => Entity;
}

/// <summary>
/// Server-authoritative facing of any entity that can be oriented.
/// Default South matches SS14 convention so freshly spawned mobs face
/// the camera in a top-down layout. Replicated immutable so observers
/// fire on every change - render-side reacts by re-indexing the atlas
/// row without resetting animation playback.
/// </summary>
[JsonConverter(typeof(FacingJson))]
public sealed partial record Facing(Dir Value) : IEngineComponent, IInsertable, ISpawnable;

/// <summary>A <see cref="Facing"/> is its bare value in JSON, as serde writes it, as a value and as a dictionary key</summary>
public sealed class FacingJson : JsonConverter<Facing>
{
    public override Facing Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(Enum.Parse<Dir>(reader.GetString()!));

    public override void Write(Utf8JsonWriter writer, Facing value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value.ToString());

    public override Facing ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(Enum.Parse<Dir>(reader.GetString()!));

    public override void WriteAsPropertyName(Utf8JsonWriter writer, Facing value, JsonSerializerOptions options) =>
        writer.WritePropertyName(value.Value.ToString());
}

/// <summary>
/// What ends a flight and what it passes through. Seeded whole by [`launch`]
/// and [`launch_ray`], so a launcher authors the rule set in one place and a
/// landed entity carries none of it into whatever launches it next
/// </summary>
/// <param name="StopsAtSolid">Finish on a movement-blocking tile (`Solid` wall, closed door, grille). The throw flavor: a thrown wrench stops at a grille because it is bulky</param>
/// <param name="StopsAtBlocksAttack">Finish on a [`BlocksAttack`] tile (wall, window, closed door). The ranged / hook flavor: a grille is `Solid` only, so bullets cross its mesh</param>
/// <param name="StopsOnFirstDamageable">Finish on a tile carrying a [`Damageable`], once `pierces` is spent. Off, a flight passes damageables freely, like a melee thrust</param>
/// <param name="Bounces">Ricochet off a `stops_at_solid` face instead of landing on it: the heading mirrors and the remaining range shrinks by [`BOUNCE_RETENTION`], capped so a rebound never boomerangs home past its own leg. A damageable stop still ends the flight, so a bank shot hits the man behind cover</param>
/// <param name="Pierces">Skip-budget for `stops_on_first_damageable`: pass this many damageables before stopping on the next. Spent as the flight goes</param>
/// <param name="HitsNothing">Raise no [`Hit`], so nothing this flight crosses is struck by it - a blood droplet flies for the look of the thing. Stop rules are unaffected: it still comes to rest against a wall</param>
public sealed partial record FlightRules(bool StopsAtSolid, bool StopsAtBlocksAttack, bool StopsOnFirstDamageable, bool Bounces, uint Pierces, bool HitsNothing) : IEngineComponent, IReadable;

/// <summary>
/// An entity in flight, launched by `launcher`. Replicated descriptor the
/// client renders from directly: `origin_world + dir * speed * elapsed` is
/// the smooth line at the true angle, never tile-snapped. `launcher` is the
/// entity that started the flight (thrower, blast source, gun shooter); used
/// for excluding the launcher from self-hits and attributing damage.
/// </summary>
/// <param name="Speed">Tiles per second.</param>
/// <param name="OriginWorld">World-space (pixel) launch point.</param>
/// <param name="Dir">Unit heading.</param>
/// <param name="Range">Tiles along the ray before the flight runs out and lands. Lets every client normalize progress for flight visuals; the authoritative cap stays in the unreplicated [`Flight`].</param>
public sealed partial record InFlight(EntityUid Launcher, float Speed, System.Numerics.Vector2 OriginWorld, System.Numerics.Vector2 Dir, float Range) : IEngineComponent, IReadable, IAddObservable, IRemoveObservable;

/// <summary>
/// On a follower (a ghost): the entity it orbits. [`track_target`] keeps the
/// follower's `TilePos` equal to the target's; the client derives the orbit
/// visual from this. Removed when the follower moves, or when the target
/// despawns.
/// </summary>
[JsonConverter(typeof(FollowingJson))]
public sealed partial record Following(EntityUid Value) : IEngineComponent, IReadable, IInsertable, ISpawnable;

/// <summary>A <see cref="Following"/> is its bare value in JSON, as serde writes it, as a value and as a dictionary key</summary>
public sealed class FollowingJson : JsonConverter<Following>
{
    public override Following Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(new EntityUid(reader.GetUInt64()));

    public override void Write(Utf8JsonWriter writer, Following value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Value.Bits);

    public override Following ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(new EntityUid(ulong.Parse(reader.GetString()!, CultureInfo.InvariantCulture)));

    public override void WriteAsPropertyName(Utf8JsonWriter writer, Following value, JsonSerializerOptions options) =>
        writer.WritePropertyName(value.Value.Bits.ToString(CultureInfo.InvariantCulture));
}

/// <summary>
/// Where a HUD surface docks. The vocabulary is shared because it crosses the
/// wire on replicated surfaces, and it is deliberately not owned by any one
/// feature: gamemode banners, the control cluster and the vitals rail all
/// anchor against the same set, so nothing invents its own geometry.
/// 
/// Zones with no surface yet are still named, so the feature that fills one
/// lands where it was always going to. Surfaces sharing a zone stack inside
/// its root; how a surface lays out its own contents is its own business.
/// </summary>
public enum HudZone
{
    /// <summary>
    /// The action bar on the top edge, then the standings and the series
    /// board under it
    /// </summary>
    TopLeft,
    TopCenter,
    /// <summary>
    /// The status alert strip: one icon per cause on the local body
    /// </summary>
    TopRight,
    Center,
    /// <summary>
    /// The vitals rail: health, and stamina and the rest as they arrive.
    /// </summary>
    CenterRight,
    /// <summary>
    /// Hands and anything else centred on the bottom edge.
    /// </summary>
    BottomCenter,
    /// <summary>
    /// The control cluster.
    /// </summary>
    BottomRight,
    /// <summary>
    /// The equipment tray, tg's pop-up inventory
    /// </summary>
    BottomLeft,
}

/// <summary>
/// A headline with an optional subtitle and countdown. Rules spawn these as
/// replicated entities and one generic client plugin renders whatever exists,
/// so a new gamemode needs no client code
/// </summary>
/// <param name="Title">Locale key, never display text</param>
/// <param name="Subtitle">Locale key; empty means no subtitle</param>
public sealed partial record HudBanner(HudZone Zone, string Title, string Subtitle, Deadline? Countdown) : IEngineComponent, IInsertable, ISpawnable;

/// <param name="Name">Player name, shown verbatim</param>
/// <param name="Player">Whose row it is, so a client can pin its own to a trimmed board</param>
public readonly partial record struct ScoreRow(string Name, int Score, bool Highlight, PlayerId Player);

/// <summary>
/// A live table of participants. Same contract as [`HudBanner`]
/// </summary>
/// <param name="Title">Locale key, never display text</param>
public sealed partial record HudScoreboard(HudZone Zone, string Title, IReadOnlyList<ScoreRow> Rows) : IEngineComponent, IReadable, IInsertable, ISpawnable, IMutable
{
    public HudZone Zone { get; set; } = Zone;

    public string Title { get; set; } = Title;

    public IReadOnlyList<ScoreRow> Rows { get; set; } = Rows;

    object IMutable.Snapshot() => this with { };
}

/// <summary>
/// Fired on a rule entity once it is spawned and live. Rule plugins observe
/// this and filter by querying their own marker component, so no generics are
/// needed
/// </summary>
public readonly partial record struct RuleStarted(EntityUid Entity) : IEngineEvent, IDirectedEvent
{
    EntityUid IDirectedEvent.Target => Entity;
}

public readonly partial record struct OutcomePlace(PlayerId Player, string Name, int Score);

/// <summary>
/// Structured result of a scope, not a concatenated string, so the client can
/// render it, a replay can keep it, and stats can read it later
/// </summary>
/// <param name="Headline">Locale key, never display text</param>
/// <param name="Places">Ranked participants, best first</param>
public readonly partial record struct ScopeOutcome(string Headline, IReadOnlyList<OutcomePlace> Places);

/// <summary>
/// Request to end a scope. Rules never touch `NextState<RoundState>` - they
/// trigger this and the driver decides what it means
/// </summary>
public readonly partial record struct EndScope(EntityUid Entity, ScopeOutcome Outcome) : ITriggerEvent;

/// <summary>
/// Set once a teardown is accepted, so a second [`EndScope`] from a racing rule
/// is a no-op
/// </summary>
public sealed partial record ScopeEnded : IEngineComponent, IReadable;

/// <summary>
/// The rescue window, fired once per accepted teardown and before the cascade.
/// Anything that must outlive the scope has to leave its hierarchy here
/// </summary>
public readonly partial record struct ScopeEnding(EntityUid Entity, ScopeOutcome Outcome) : IEngineEvent, IDirectedEvent
{
    EntityUid IDirectedEvent.Target => Entity;
}

/// <summary>
/// Marker for the ghost behavior surface: orbit, ghost-only chat, the
/// "return to body" verb, future ghost-specific UI. Independent from the
/// PVS markers below so non-ghost incorporeal entities can reuse the
/// visibility layer without inheriting ghost behaviors.
/// </summary>
public sealed partial record Ghost : IEngineComponent, IReadable;

public enum Hand
{
    Left,
    Right,
}

/// <summary>
/// Mob's hand state. For MVP, only tracks which hand is active - held-item
/// slots land when the inventory system arrives.
/// </summary>
public sealed partial record Hands(Hand Active) : IEngineComponent, IReadable;

/// <summary>
/// Marker for a map entity. The map is the parent of every `Tile` entity
/// belonging to it (via `ChildOf`), so despawning the map cascades to its
/// tiles. Other domain components (atmos defaults, ambient music, parallax
/// background) attach here as separate components.
/// 
/// Requires `Transform` so children's `GlobalTransform` propagates. The render
/// crate adds the matching `Visibility` requirement (the `bevy_render` side of
/// the same B0004 fix), keeping the headless server free of `bevy_render`.
/// 
/// Requires `MapLight` because the renderer reads ambient off the map and falls
/// back to full brightness when no map carries it, so a map spawned without one
/// silently disables lighting for the whole world.
/// 
/// Requires `NextPlacementSeq` so every path that creates a map (game load,
/// editor new-file, a co-op host) can allocate placement order without
/// remembering to add the counter
/// </summary>
public sealed partial record Map : IEngineComponent, IReadable;

/// <summary>
/// Position of an entity on a map's tile grid. Carries the owning map's
/// entity ref so multiple maps (lavaland, centcom, the station) can coexist
/// without coordinate collisions.
/// </summary>
/// <param name="Map">Marked so replication rewrites it into the receiver's own id. Without this a peer keeps the sender's raw entity and every by-map lookup misses, which reads as an empty map rather than a mismatched one.</param>
public sealed partial record TilePos(EntityUid Map, Vector2i Pos) : IEngineComponent, IReadable, IInsertable, ISpawnable;

/// <summary>
/// Marks an entity as a mob - the in-world body that can move, be pushed,
/// occupy tiles, etc.
/// </summary>
public sealed partial record Mob : IEngineComponent, IReadable;

/// <summary>
/// Marker: this entity participates in tile-locked movement. Carries
/// `MoveSpeedModifier` + `MoveCooldown` via require so both are always present
/// wherever this marker is. `Facing` is also required - every movable
/// entity has an orientation, and the movement applier writes it on
/// each successful step.
/// </summary>
public sealed partial record Movable : IEngineComponent, IReadable, IInsertable, ISpawnable;

/// <summary>
/// Marks a controllable entity (a mob, ghost, etc.) as controlled by a
/// particular player identified by their stable [`PlayerId`].
/// 
/// Replicated and currently visible to all clients. Per-client visibility
/// filtering is deferred - for MVP it's acceptable for everyone to see who
/// controls what.
/// 
/// Body-swap and possession move this component (or change its inner id)
/// between entities. Clients reconstruct their own mob by querying the
/// entity carrying `ControlledBy(local_player_id)`. Reconnects never touch
/// it: the server just rebinds the connection to the same `PlayerId`.
/// </summary>
[JsonConverter(typeof(ControlledByJson))]
public sealed partial record ControlledBy(PlayerId Value) : IEngineComponent, IReadable;

/// <summary>A <see cref="ControlledBy"/> is its bare value in JSON, as serde writes it, as a value and as a dictionary key</summary>
public sealed class ControlledByJson : JsonConverter<ControlledBy>
{
    public override ControlledBy Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(new PlayerId(reader.GetUInt64()));

    public override void Write(Utf8JsonWriter writer, ControlledBy value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Value.Value);

    public override ControlledBy ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(new PlayerId(ulong.Parse(reader.GetString()!, CultureInfo.InvariantCulture)));

    public override void WriteAsPropertyName(Utf8JsonWriter writer, ControlledBy value, JsonSerializerOptions options) =>
        writer.WritePropertyName(value.Value.Value.ToString(CultureInfo.InvariantCulture));
}

/// <summary>
/// The [`PlayerId`] a live connection authenticated as. Server-side only,
/// lives on the connection entity next to `AuthorizedClient`; never
/// replicated.
/// </summary>
[JsonConverter(typeof(SessionPlayerJson))]
public sealed partial record SessionPlayer(PlayerId Value) : IEngineComponent, IReadable;

/// <summary>A <see cref="SessionPlayer"/> is its bare value in JSON, as serde writes it, as a value and as a dictionary key</summary>
public sealed class SessionPlayerJson : JsonConverter<SessionPlayer>
{
    public override SessionPlayer Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(new PlayerId(reader.GetUInt64()));

    public override void Write(Utf8JsonWriter writer, SessionPlayer value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Value.Value);

    public override SessionPlayer ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(new PlayerId(ulong.Parse(reader.GetString()!, CultureInfo.InvariantCulture)));

    public override void WriteAsPropertyName(Utf8JsonWriter writer, SessionPlayer value, JsonSerializerOptions options) =>
        writer.WritePropertyName(value.Value.Value.ToString(CultureInfo.InvariantCulture));
}

/// <summary>
/// A `RolePrototype` id. Lives on the connection entity as the assignment
/// record, which is what makes a team survive a match change, and is copied
/// onto each body so gameplay can read it without walking back to the session.
/// </summary>
[JsonConverter(typeof(RoleJson))]
public sealed partial record Role(string Value) : IEngineComponent, IInsertable, ISpawnable;

/// <summary>A <see cref="Role"/> is its bare value in JSON, as serde writes it, as a value and as a dictionary key</summary>
public sealed class RoleJson : JsonConverter<Role>
{
    public override Role Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, Role value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);

    public override Role ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetString()!);

    public override void WriteAsPropertyName(Utf8JsonWriter writer, Role value, JsonSerializerOptions options) =>
        writer.WritePropertyName(value.Value);
}

/// <summary>
/// Zeroes `earned` on every wallet. The wallet's owner stays its only writer:
/// a rule that wants a fresh tiebreak asks, it does not reach in
/// </summary>
public readonly partial record struct ClearEarned : ITriggerEvent;

/// <summary>
/// Money moved, the offer still standing; what handing over `product` means
/// is the observer's call (a body owed it, an item in hand, a skill granted)
/// </summary>
/// <param name="Price">What actually left the wallet, so an observer whose delivery fails can refund rather than eat the buyer's money</param>
public readonly partial record struct Purchased(PlayerId Player, EntityUid Connection, string Listing, string Product, uint Price) : IEngineEvent, IDirectedEvent
{
    EntityUid IDirectedEvent.Target => Connection;
}

/// <summary>
/// The shopper is finished with the offer, which comes down right after this
/// fires; a mode closing a shelf itself goes through the same `close_offer`
/// </summary>
public readonly partial record struct ShopDone(PlayerId Player, EntityUid Connection) : IEngineEvent, IDirectedEvent
{
    EntityUid IDirectedEvent.Target => Connection;
}

/// <param name="Product">What the buyer receives, carried on the row so the buy path never re-consults the listing registry after the roll</param>
/// <param name="Name">Product display name, resolved server-side so the client renders text</param>
/// <param name="Sprite">Product sprite, resolved server-side for the same reason; `None` for a product with nothing to draw</param>
/// <param name="Shelf">The staple shelf this row rides, straight off the listing</param>
/// <param name="Sold">A rolled row sells once; a staple never flips this</param>
public readonly partial record struct OfferRow(string Listing, string Product, string Name, RsiRef? Sprite, uint Price, bool Staple, string Shelf, bool Sold);

/// <summary>
/// What this connection may buy right now, on the connection entity. Absent
/// means no shop at all: whoever raised the offer decides when someone may
/// spend, and the shop never asks why. Server-side; [`ShopUpdate`] is its
/// wire mirror
/// </summary>
/// <param name="Unlocks">Before this, the shelf can be read but not bought from - the pause is where a mode puts its reveal</param>
/// <param name="ClosesAt">When the offer stops being the shopper's to act on. The shop itself does nothing at this moment - whoever raised the offer does - it is carried so the client can count down. `None` means the shelf stands until something withdraws it</param>
public sealed partial record ShopOffer(IReadOnlyList<OfferRow> Rows, Deadline Unlocks, Deadline? ClosesAt) : IEngineComponent, IReadable, IInsertable, ISpawnable, IMutable, IAddObservable, IRemoveObservable
{
    public IReadOnlyList<OfferRow> Rows { get; set; } = Rows;

    public Deadline Unlocks { get; set; } = Unlocks;

    public Deadline? ClosesAt { get; set; } = ClosesAt;

    object IMutable.Snapshot() => this with { };
}

/// <summary>
/// A player's money, on their `PlayerRecord` entity so it survives a
/// reconnect. `balance` is what they can spend; `earned` accumulates every
/// credit and never goes down, for round-level tiebreaks. One component: both
/// move on the same credit and reset through the same owner
/// </summary>
public sealed partial record Wallet(uint Balance, uint Earned) : IEngineComponent, IReadable, IMutable
{
    public uint Balance { get; set; } = Balance;

    public uint Earned { get; set; } = Earned;

    object IMutable.Snapshot() => this with { };
}

/// <summary>
/// On a hand slot: what the hand holds on to without storing it, see ADR 0010
/// </summary>
[JsonConverter(typeof(GrippingJson))]
public sealed partial record Gripping(EntityUid Value) : IEngineComponent, IReadable;

/// <summary>A <see cref="Gripping"/> is its bare value in JSON, as serde writes it, as a value and as a dictionary key</summary>
public sealed class GrippingJson : JsonConverter<Gripping>
{
    public override Gripping Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(new EntityUid(reader.GetUInt64()));

    public override void Write(Utf8JsonWriter writer, Gripping value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Value.Bits);

    public override Gripping ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(new EntityUid(ulong.Parse(reader.GetString()!, CultureInfo.InvariantCulture)));

    public override void WriteAsPropertyName(Utf8JsonWriter writer, Gripping value, JsonSerializerOptions options) =>
        writer.WritePropertyName(value.Value.Bits.ToString(CultureInfo.InvariantCulture));
}

/// <summary>
/// Marks a [`Container`] as one of a mob's hands - which side, and whose. The
/// mob's [`Hands::active`] selects which hand pickup/drop act on; `owner` is
/// the replicated slot-to-holder link, so both sides resolve "whose hand is
/// this" without relying on `ChildOf` (which is derived, not replicated). A
/// container without `HandSlot` (a toolbox) is openable storage, not a hand.
/// </summary>
public sealed partial record HandSlot(Hand Side, EntityUid Owner) : IEngineComponent, IReadable;

/// <summary>
/// In-container location source: this entity sits inside `container`, not on a
/// tile. The container is any entity with a [`Container`] - a hand or a
/// toolbox. Mutually exclusive with [`TilePos`] - the strip
/// observers guarantee an entity never carries both. The relationship derive
/// forces it immutable, so every re-store is a fresh insert that re-fires the
/// invariant
/// </summary>
public sealed partial record Stored(EntityUid Container) : IEngineComponent, IReadable, IInsertable, ISpawnable;

/// <summary>
/// Fires on the item as it goes off, in a hand, on the floor or mid-flight
/// </summary>
/// <param name="By">Who set it: the mob that lit it</param>
/// <param name="On">What set it off, when something did: the body that stepped on it</param>
public readonly partial record struct Triggered(EntityUid Entity, EntityUid? By, EntityUid? On) : IEngineEvent, IDirectedEvent
{
    EntityUid IDirectedEvent.Target => Entity;
}

