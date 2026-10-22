// Generated from the marked types, see crates/server/src/mods/sdk.rs. Do not edit

using System;
using System.Collections.Generic;
using System.Linq;
using Wit = TypedModuleWorld.wit.Imports.rs13.mods.v0_1_0.ITypedEcsImports;

namespace Rs13.Packs;

/// <summary>An engine component's kind and its case of the bindings' component variant, filled on first use</summary>
static class EngineComponent<T> where T : class
{
    static (Wit.ComponentKind kind, Func<Wit.Component, T> unwrap, Func<T, Wit.Component> wrap)? filled;

    internal static void Fill(Wit.ComponentKind kind, Func<Wit.Component, T> unwrap, Func<T, Wit.Component> wrap)
    {
        filled = (kind, unwrap, wrap);
        EngineWit.wraps[typeof(T)] = value => wrap((T)value);
    }

    public static Wit.ComponentKind Kind => Entry.kind;

    public static T Unwrap(Wit.Component component) => Entry.unwrap(component);

    public static Wit.Component Wrap(T value) => Entry.wrap(value);

    static (Wit.ComponentKind kind, Func<Wit.Component, T> unwrap, Func<T, Wit.Component> wrap) Entry
    {
        get
        {
            EngineWit.Ensure();
            return filled ?? throw new InvalidOperationException($"{typeof(T).Name} is not an engine component");
        }
    }
}

/// <summary>An observable event's kind and its case of the bindings' event variant</summary>
static class EngineEvent<T> where T : struct
{
    static (Wit.EventKind kind, Func<Wit.Event, T> unwrap)? filled;

    internal static void Fill(Wit.EventKind kind, Func<Wit.Event, T> unwrap) => filled = (kind, unwrap);

    public static Wit.EventKind Kind => Entry.kind;

    public static T Unwrap(Wit.Event @event) => Entry.unwrap(@event);

    static (Wit.EventKind kind, Func<Wit.Event, T> unwrap) Entry
    {
        get
        {
            EngineWit.Ensure();
            return filled ?? throw new InvalidOperationException($"{typeof(T).Name} is not an observable event");
        }
    }
}

/// <summary>How an event a module raises becomes the bindings' trigger variant</summary>
static class EngineTrigger<T> where T : struct
{
    static Func<T, Wit.TriggerEvent>? filled;

    internal static void Fill(Func<T, Wit.TriggerEvent> wrap) => filled = wrap;

    public static Wit.TriggerEvent Wrap(T value)
    {
        EngineWit.Ensure();
        return (filled ?? throw new InvalidOperationException($"{typeof(T).Name} is not an event a module raises"))(value);
    }
}

/// <summary>The edges of a component a module may observe, each carrying the entity it happened to</summary>
static class EngineEdge<T> where T : class
{
    static (Wit.EventKind kind, Func<Wit.Event, EntityUid> entity)? added;
    static (Wit.EventKind kind, Func<Wit.Event, EntityUid> entity)? removed;

    internal static void Added(Wit.EventKind kind, Func<Wit.Event, EntityUid> entity)
    {
        added = (kind, entity);
        EngineWit.edges[(typeof(T), true)] = (kind, entity);
    }

    internal static void Removed(Wit.EventKind kind, Func<Wit.Event, EntityUid> entity)
    {
        removed = (kind, entity);
        EngineWit.edges[(typeof(T), false)] = (kind, entity);
    }

    public static (Wit.EventKind kind, Func<Wit.Event, EntityUid> entity) OnAdd
    {
        get
        {
            EngineWit.Ensure();
            return added ?? throw new InvalidOperationException($"a module may not observe {typeof(T).Name} being added");
        }
    }

    public static (Wit.EventKind kind, Func<Wit.Event, EntityUid> entity) OnRemove
    {
        get
        {
            EngineWit.Ensure();
            return removed ?? throw new InvalidOperationException($"a module may not observe {typeof(T).Name} being removed");
        }
    }
}

static class EngineWit
{
    static bool registered;
    // The same tables by type, for a value whose static type is only a marker
    internal static readonly Dictionary<Type, Func<object, Wit.Component>> wraps = [];
    internal static readonly Dictionary<(Type, bool added), (Wit.EventKind kind, Func<Wit.Event, EntityUid> entity)> edges = [];

    internal static Wit.Component Wrap(ISpawnable component)
    {
        Ensure();
        return wraps.TryGetValue(component.GetType(), out var wrap)
            ? wrap(component)
            : throw new InvalidOperationException($"{component.GetType().Name} is not an engine component a module may insert");
    }

    internal static (Wit.EventKind kind, Func<Wit.Event, EntityUid> entity) Edge(Type component, bool added)
    {
        Ensure();
        return edges.TryGetValue((component, added), out var edge)
            ? edge
            : throw new InvalidOperationException($"a module may not observe {component.Name} being {(added ? "added" : "removed")}");
    }

    internal static void Ensure()
    {
        if (!registered)
        {
            registered = true;
            Register();
        }
    }

    static void Register()
    {
        EngineComponent<ChildOf>.Fill(Wit.ComponentKind.CHILD_OF, c => new ChildOf(new EntityUid(c.AsChildOf)), v => Wit.Component.ChildOf(v.Value.Bits));
        EngineComponent<Replicated>.Fill(Wit.ComponentKind.REPLICATED, _ => new Replicated(), _ => Wit.Component.Replicated());
        EngineComponent<Participant>.Fill(Wit.ComponentKind.PARTICIPANT, _ => new Participant(), _ => Wit.Component.Participant());
        EngineComponent<PlayerRecord>.Fill(Wit.ComponentKind.PLAYER_RECORD, c => From(c.AsPlayerRecord), v => Wit.Component.PlayerRecord(To(v)));
        EngineComponent<EventTarget>.Fill(Wit.ComponentKind.EVENT_TARGET, c => From(c.AsEventTarget), v => Wit.Component.EventTarget(To(v)));
        EngineComponent<Appearance>.Fill(Wit.ComponentKind.APPEARANCE, c => From(c.AsAppearance), v => Wit.Component.Appearance(To(v)));
        EngineComponent<BurnOut>.Fill(Wit.ComponentKind.BURN_OUT, c => From(c.AsBurnOut), v => Wit.Component.BurnOut(To(v)));
        EngineComponent<Critical>.Fill(Wit.ComponentKind.CRITICAL, _ => new Critical(), _ => Wit.Component.Critical());
        EngineComponent<Dead>.Fill(Wit.ComponentKind.DEAD, _ => new Dead(), _ => Wit.Component.Dead());
        EngineComponent<Stabilised>.Fill(Wit.ComponentKind.STABILISED, _ => new Stabilised(), _ => Wit.Component.Stabilised());
        EngineComponent<Facing>.Fill(Wit.ComponentKind.FACING, c => new Facing((Dir)c.AsFacing), v => Wit.Component.Facing((Wit.Dir)v.Value));
        EngineComponent<FlightRules>.Fill(Wit.ComponentKind.FLIGHT_RULES, c => From(c.AsFlightRules), v => Wit.Component.FlightRules(To(v)));
        EngineComponent<InFlight>.Fill(Wit.ComponentKind.IN_FLIGHT, c => From(c.AsInFlight), v => Wit.Component.InFlight(To(v)));
        EngineComponent<Following>.Fill(Wit.ComponentKind.FOLLOWING, c => new Following(new EntityUid(c.AsFollowing)), v => Wit.Component.Following(v.Value.Bits));
        EngineComponent<HudBanner>.Fill(Wit.ComponentKind.HUD_BANNER, c => From(c.AsHudBanner), v => Wit.Component.HudBanner(To(v)));
        EngineComponent<HudScoreboard>.Fill(Wit.ComponentKind.HUD_SCOREBOARD, c => From(c.AsHudScoreboard), v => Wit.Component.HudScoreboard(To(v)));
        EngineComponent<ScopeEnded>.Fill(Wit.ComponentKind.SCOPE_ENDED, _ => new ScopeEnded(), _ => Wit.Component.ScopeEnded());
        EngineComponent<Ghost>.Fill(Wit.ComponentKind.GHOST, _ => new Ghost(), _ => Wit.Component.Ghost());
        EngineComponent<Hands>.Fill(Wit.ComponentKind.HANDS, c => From(c.AsHands), v => Wit.Component.Hands(To(v)));
        EngineComponent<Map>.Fill(Wit.ComponentKind.MAP, _ => new Map(), _ => Wit.Component.Map());
        EngineComponent<TilePos>.Fill(Wit.ComponentKind.TILE_POS, c => From(c.AsTilePos), v => Wit.Component.TilePos(To(v)));
        EngineComponent<RamsThrough>.Fill(Wit.ComponentKind.RAMS_THROUGH, c => From(c.AsRamsThrough), v => Wit.Component.RamsThrough(To(v)));
        EngineComponent<Mob>.Fill(Wit.ComponentKind.MOB, _ => new Mob(), _ => Wit.Component.Mob());
        EngineComponent<Movable>.Fill(Wit.ComponentKind.MOVABLE, _ => new Movable(), _ => Wit.Component.Movable());
        EngineComponent<DisplayName>.Fill(Wit.ComponentKind.DISPLAY_NAME, c => From(c.AsDisplayName), v => Wit.Component.DisplayName(To(v)));
        EngineComponent<ControlledBy>.Fill(Wit.ComponentKind.CONTROLLED_BY, c => new ControlledBy(new PlayerId(c.AsControlledBy)), v => Wit.Component.ControlledBy(v.Value.Value));
        EngineComponent<SessionPlayer>.Fill(Wit.ComponentKind.SESSION_PLAYER, c => new SessionPlayer(new PlayerId(c.AsSessionPlayer)), v => Wit.Component.SessionPlayer(v.Value.Value));
        EngineComponent<Role>.Fill(Wit.ComponentKind.ROLE, c => new Role(c.AsRole), v => Wit.Component.Role(v.Value));
        EngineComponent<ShopOffer>.Fill(Wit.ComponentKind.SHOP_OFFER, c => From(c.AsShopOffer), v => Wit.Component.ShopOffer(To(v)));
        EngineComponent<Wallet>.Fill(Wit.ComponentKind.WALLET, c => From(c.AsWallet), v => Wit.Component.Wallet(To(v)));
        EngineComponent<Gripping>.Fill(Wit.ComponentKind.GRIPPING, c => new Gripping(new EntityUid(c.AsGripping)), v => Wit.Component.Gripping(v.Value.Bits));
        EngineComponent<HandSlot>.Fill(Wit.ComponentKind.HAND_SLOT, c => From(c.AsHandSlot), v => Wit.Component.HandSlot(To(v)));
        EngineComponent<Stored>.Fill(Wit.ComponentKind.STORED, c => From(c.AsStored), v => Wit.Component.Stored(To(v)));
        EngineEdge<InFlight>.Added(Wit.EventKind.IN_FLIGHT_ADDED, e => new EntityUid(e.AsInFlightAdded));
        EngineEdge<ShopOffer>.Added(Wit.EventKind.SHOP_OFFER_ADDED, e => new EntityUid(e.AsShopOfferAdded));
        EngineEdge<InFlight>.Removed(Wit.EventKind.IN_FLIGHT_REMOVED, e => new EntityUid(e.AsInFlightRemoved));
        EngineEdge<ShopOffer>.Removed(Wit.EventKind.SHOP_OFFER_REMOVED, e => new EntityUid(e.AsShopOfferRemoved));
        EngineEvent<DamageDealt>.Fill(Wit.EventKind.DAMAGE_DEALT, e => From(e.AsDamageDealt));
        EngineEvent<Killed>.Fill(Wit.EventKind.KILLED, e => From(e.AsKilled));
        EngineEvent<RuleStarted>.Fill(Wit.EventKind.RULE_STARTED, e => From(e.AsRuleStarted));
        EngineEvent<ScopeEnding>.Fill(Wit.EventKind.SCOPE_ENDING, e => From(e.AsScopeEnding));
        EngineEvent<Purchased>.Fill(Wit.EventKind.PURCHASED, e => From(e.AsPurchased));
        EngineEvent<ShopDone>.Fill(Wit.EventKind.SHOP_DONE, e => From(e.AsShopDone));
        EngineEvent<ChooseTarget>.Fill(Wit.EventKind.CHOOSE_TARGET, e => From(e.AsChooseTarget));
        EngineEvent<EventStrikes>.Fill(Wit.EventKind.EVENT_STRIKES, e => From(e.AsEventStrikes));
        EngineEvent<Triggered>.Fill(Wit.EventKind.TRIGGERED, e => From(e.AsTriggered));
        EngineTrigger<LiftCrit>.Fill(t => Wit.TriggerEvent.LiftCrit(To(t)));
        EngineTrigger<EndScope>.Fill(t => Wit.TriggerEvent.EndScope(To(t)));
        EngineTrigger<ClearEarned>.Fill(_ => Wit.TriggerEvent.ClearEarned());
    }

    internal static TimeSpan From(Wit.Duration d) => SerdeDuration.From(d.secs, d.nanos);

    internal static Wit.Duration To(TimeSpan t)
    {
        var (secs, nanos) = SerdeDuration.To(t);
        return new(secs, nanos);
    }

    internal static PlayerRecord From(Wit.PlayerRecord x) => new(x.username, new PlayerId(x.id));

    internal static Wit.PlayerRecord To(PlayerRecord v) => new(v.Username, v.Id.Value);

    internal static EventTarget From(Wit.EventTarget x) => new(new EntityUid(x.area), x.name);

    internal static Wit.EventTarget To(EventTarget v) => new(v.Area.Bits, v.Name);

    internal static RsiRef From(Wit.RsiRef x) => new(x.rsi, x.state);

    internal static Wit.RsiRef To(RsiRef v) => new(v.Rsi, v.State);

    internal static Srgba From(Wit.Srgba x) => new(x.red, x.green, x.blue, x.alpha);

    internal static Wit.Srgba To(Srgba v) => new(v.Red, v.Green, v.Blue, v.Alpha);

    internal static SpriteLayer From(Wit.SpriteLayer x) => new(x.state, x.key, x.rsi, x.visible, (Shading)x.shading, x.facing is { } x5_0 ? (Dir)x5_0 : default(Dir?), x.tint is { } x6_0 ? From(x6_0) : default(Srgba?), x.order, x.displacement is { } x8_0 ? From(x8_0) : default(RsiRef?));

    internal static Wit.SpriteLayer To(SpriteLayer v) => new(v.State, v.Key, v.Rsi, v.Visible, (Wit.Shading)v.Shading, v.Facing is { } x5_0 ? (Wit.Dir)x5_0 : default(Wit.Dir?), v.Tint is { } x6_0 ? To(x6_0) : default(Wit.Srgba?), v.Order, v.Displacement is { } x8_0 ? To(x8_0) : default(Wit.RsiRef?));

    internal static Appearance From(Wit.Appearance x) => new(From(x.sprite), (DrawDepth)x.depth, (Shading)x.shading, x.spriteOffsetRadians, x.layers.Select(x4_0 => From(x4_0)).ToList(), x.hideBase);

    internal static Wit.Appearance To(Appearance v) => new(To(v.Sprite), (Wit.DrawDepth)v.Depth, (Wit.Shading)v.Shading, v.SpriteOffsetRadians, v.Layers.Select(x4_0 => To(x4_0)).ToList(), v.HideBase);

    internal static Deadline From(Wit.Deadline x) => new(From(x.endsAt));

    internal static Wit.Deadline To(Deadline v) => new(To(v.EndsAt));

    internal static BurnOut From(Wit.BurnOut x) => new(From(x.until), x.secs);

    internal static Wit.BurnOut To(BurnOut v) => new(To(v.Until), v.Secs);

    internal static System.Numerics.Vector2 From(Wit.Vec2 x) => new(x.x, x.y);

    internal static Wit.Vec2 To(System.Numerics.Vector2 v) => new(v.X, v.Y);

    internal static DamageDealt From(Wit.DamageDealt x) => new(new EntityUid(x.entity), x.spec.ToDictionary(x1_p0 => (DamageType)x1_p0.Item1, x1_p0 => (uint)(x1_p0.Item2)), x.origin is { } x2_0 ? new EntityUid(x2_0) : default(EntityUid?), x.heading is { } x3_0 ? From(x3_0) : default(System.Numerics.Vector2?), x.weapon is { } x4_0 ? new EntityUid(x4_0) : default(EntityUid?));

    internal static Wit.DamageDealt To(DamageDealt v) => new(v.Entity.Bits, v.Spec.Select(x1_p0 => ((Wit.DamageType)x1_p0.Key, x1_p0.Value)).ToList(), v.Origin is { } x2_0 ? x2_0.Bits : default(ulong?), v.Heading is { } x3_0 ? To(x3_0) : default(Wit.Vec2?), v.Weapon is { } x4_0 ? x4_0.Bits : default(ulong?));

    internal static Killed From(Wit.Killed x) => new(new EntityUid(x.entity), new PlayerId(x.victim), x.killer is { } x2_0 ? new PlayerId(x2_0) : default(PlayerId?));

    internal static Wit.Killed To(Killed v) => new(v.Entity.Bits, v.Victim.Value, v.Killer is { } x2_0 ? x2_0.Value : default(ulong?));

    internal static LiftCrit From(Wit.LiftCrit x) => new(new EntityUid(x.entity));

    internal static Wit.LiftCrit To(LiftCrit v) => new(v.Entity.Bits);

    internal static FlightRules From(Wit.FlightRules x) => new(x.stopsAtSolid, x.stopsAtBlocksAttack, x.stopsOnFirstDamageable, x.bounces, x.pierces, x.hitsNothing);

    internal static Wit.FlightRules To(FlightRules v) => new(v.StopsAtSolid, v.StopsAtBlocksAttack, v.StopsOnFirstDamageable, v.Bounces, v.Pierces, v.HitsNothing);

    internal static InFlight From(Wit.InFlight x) => new(new EntityUid(x.launcher), x.speed, From(x.originWorld), From(x.dir), x.range);

    internal static Wit.InFlight To(InFlight v) => new(v.Launcher.Bits, v.Speed, To(v.OriginWorld), To(v.Dir), v.Range);

    internal static HudBanner From(Wit.HudBanner x) => new((HudZone)x.zone, x.title, x.subtitle, x.countdown is { } x3_0 ? From(x3_0) : default(Deadline?));

    internal static Wit.HudBanner To(HudBanner v) => new((Wit.HudZone)v.Zone, v.Title, v.Subtitle, v.Countdown is { } x3_0 ? To(x3_0) : default(Wit.Deadline?));

    internal static ScoreRow From(Wit.ScoreRow x) => new(x.name, x.score, x.highlight, new PlayerId(x.player));

    internal static Wit.ScoreRow To(ScoreRow v) => new(v.Name, v.Score, v.Highlight, v.Player.Value);

    internal static HudScoreboard From(Wit.HudScoreboard x) => new((HudZone)x.zone, x.title, x.rows.Select(x2_0 => From(x2_0)).ToList());

    internal static Wit.HudScoreboard To(HudScoreboard v) => new((Wit.HudZone)v.Zone, v.Title, v.Rows.Select(x2_0 => To(x2_0)).ToList());

    internal static RuleStarted From(Wit.RuleStarted x) => new(new EntityUid(x.entity));

    internal static Wit.RuleStarted To(RuleStarted v) => new(v.Entity.Bits);

    internal static OutcomePlace From(Wit.OutcomePlace x) => new(new PlayerId(x.player), x.name, x.score);

    internal static Wit.OutcomePlace To(OutcomePlace v) => new(v.Player.Value, v.Name, v.Score);

    internal static ScopeOutcome From(Wit.ScopeOutcome x) => new(x.headline, x.places.Select(x1_0 => From(x1_0)).ToList());

    internal static Wit.ScopeOutcome To(ScopeOutcome v) => new(v.Headline, v.Places.Select(x1_0 => To(x1_0)).ToList());

    internal static EndScope From(Wit.EndScope x) => new(new EntityUid(x.entity), From(x.outcome));

    internal static Wit.EndScope To(EndScope v) => new(v.Entity.Bits, To(v.Outcome));

    internal static ScopeEnding From(Wit.ScopeEnding x) => new(new EntityUid(x.entity), From(x.outcome));

    internal static Wit.ScopeEnding To(ScopeEnding v) => new(v.Entity.Bits, To(v.Outcome));

    internal static Hands From(Wit.Hands x) => new((Hand)x.active);

    internal static Wit.Hands To(Hands v) => new((Wit.Hand)v.Active);

    internal static Vector2i From(Wit.IVec2 x) => new(x.x, x.y);

    internal static Wit.IVec2 To(Vector2i v) => new(v.X, v.Y);

    internal static TilePos From(Wit.TilePos x) => new(new EntityUid(x.map), From(x.pos));

    internal static Wit.TilePos To(TilePos v) => new(v.Map.Bits, To(v.Pos));

    internal static RamsThrough From(Wit.RamsThrough x) => new(x.damage.ToDictionary(x0_p0 => (DamageType)x0_p0.Item1, x0_p0 => (uint)(x0_p0.Item2)), x.hits);

    internal static Wit.RamsThrough To(RamsThrough v) => new(v.Damage.Select(x0_p0 => ((Wit.DamageType)x0_p0.Key, x0_p0.Value)).ToList(), v.Hits);

    internal static DisplayName From(Wit.DisplayName x) => new(x.name, x.desc);

    internal static Wit.DisplayName To(DisplayName v) => new(v.Name, v.Desc);

    internal static Purchased From(Wit.Purchased x) => new(new PlayerId(x.player), new EntityUid(x.connection), x.listing, x.product, x.price);

    internal static Wit.Purchased To(Purchased v) => new(v.Player.Value, v.Connection.Bits, v.Listing, v.Product, v.Price);

    internal static ShopDone From(Wit.ShopDone x) => new(new PlayerId(x.player), new EntityUid(x.connection));

    internal static Wit.ShopDone To(ShopDone v) => new(v.Player.Value, v.Connection.Bits);

    internal static OfferRow From(Wit.OfferRow x) => new(x.listing, x.product, x.name, x.sprite is { } x3_0 ? From(x3_0) : default(RsiRef?), x.price, x.staple, x.shelf, x.sold);

    internal static Wit.OfferRow To(OfferRow v) => new(v.Listing, v.Product, v.Name, v.Sprite is { } x3_0 ? To(x3_0) : default(Wit.RsiRef?), v.Price, v.Staple, v.Shelf, v.Sold);

    internal static ShopOffer From(Wit.ShopOffer x) => new(x.rows.Select(x0_0 => From(x0_0)).ToList(), From(x.unlocks), x.closesAt is { } x2_0 ? From(x2_0) : default(Deadline?));

    internal static Wit.ShopOffer To(ShopOffer v) => new(v.Rows.Select(x0_0 => To(x0_0)).ToList(), To(v.Unlocks), v.ClosesAt is { } x2_0 ? To(x2_0) : default(Wit.Deadline?));

    internal static Wallet From(Wit.Wallet x) => new(x.balance, x.earned);

    internal static Wit.Wallet To(Wallet v) => new(v.Balance, v.Earned);

    internal static HandSlot From(Wit.HandSlot x) => new((Hand)x.side, new EntityUid(x.owner));

    internal static Wit.HandSlot To(HandSlot v) => new((Wit.Hand)v.Side, v.Owner.Bits);

    internal static Stored From(Wit.Stored x) => new(new EntityUid(x.container));

    internal static Wit.Stored To(Stored v) => new(v.Container.Bits);

    internal static ChooseTarget From(Wit.ChooseTarget x) => new(new EntityUid(x.entity));

    internal static Wit.ChooseTarget To(ChooseTarget v) => new(v.Entity.Bits);

    internal static EventStrikes From(Wit.EventStrikes x) => new(new EntityUid(x.entity));

    internal static Wit.EventStrikes To(EventStrikes v) => new(v.Entity.Bits);

    internal static Triggered From(Wit.Triggered x) => new(new EntityUid(x.entity), x.by is { } x1_0 ? new EntityUid(x1_0) : default(EntityUid?), x.on is { } x2_0 ? new EntityUid(x2_0) : default(EntityUid?));

    internal static Wit.Triggered To(Triggered v) => new(v.Entity.Bits, v.By is { } x1_0 ? x1_0.Bits : default(ulong?), v.On is { } x2_0 ? x2_0.Bits : default(ulong?));
}
