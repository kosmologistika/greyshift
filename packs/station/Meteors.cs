using Rs13.Packs;

namespace Station;

// The storyteller event behind a shower: on the strike, Count rocks drawn
// from the weighted Pool of meteor prototypes fly at random floor cells of
// the target room, spread over OverSecs
[RegisterComponent]
public sealed partial class MeteorShowerComponent : Component
{
    [DataField] public uint Count;
    [DataField] public float OverSecs;
    [DataField] public Dictionary<string, float> Pool = [];
}

// The rocks still owed after the strike, when the next one flies, and the
// ones already in the air
[RegisterComponent]
public sealed partial class ShoweringComponent : Component
{
    [DataField] public uint Thrown;
    [DataField] public TimeSpan Next;
    [DataField] public List<EntityUid> Rocks = [];
}

public sealed partial class MeteorShowerSystem : GameRuleSystem<MeteorShowerComponent>
{
    [Dependency] IGameTiming _timing = default!;
    [Dependency] AreaSystem _areas = default!;
    [Dependency] FlightSystem _flight = default!;
    [Dependency] EntityQuery<Dead> _dead = default!;
    [Dependency] EntityQuery<Ghost> _ghosts = default!;
    [Dependency] EntityQuery<RamsThrough> _rocks = default!;

    static readonly Vector2i[] Steps = [new(1, 0), new(-1, 0), new(0, 1), new(0, -1)];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MeteorShowerComponent, ChooseTarget>(OnChooseTarget);
        SubscribeLocalEvent<MeteorShowerComponent, EventStrikes>(OnStrike);
        EveryActive(TimeSpan.Zero, ThrowRocks);
    }

    // A room with a living participant in it, or one beside the cell a
    // participant stands on when that cell belongs to no room (a doorway),
    // and only a room with floor still standing to hit. None of that and
    // the event gives up
    void OnChooseTarget(Entity<MeteorShowerComponent> shower, ref ChooseTarget args)
    {
        var participants = new HashSet<PlayerId>();
        var sessions = EntityQueryEnumerator<SessionPlayer, Participant>();
        while (sessions.MoveNext(out _, out var session, out _))
        {
            participants.Add(session.Value);
        }
        var candidates = new List<EntityUid>();
        var fallbacks = new List<EntityUid>();
        var bodies = EntityQueryEnumerator<ControlledBy, TilePos, Mob>();
        while (bodies.MoveNext(out var body, out var controlled, out var at, out _))
        {
            if (!participants.Contains(controlled.Value) || _dead.HasComp(body) || _ghosts.HasComp(body))
            {
                continue;
            }
            if (_areas.AreaOf(at) is { } area)
            {
                candidates.Add(area);
                continue;
            }
            foreach (var step in Steps)
            {
                if (_areas.AreaOf(new TilePos(at.Map, at.Pos + step)) is { } beside)
                {
                    fallbacks.Add(beside);
                }
            }
        }
        var floored = new Dictionary<EntityUid, bool>();
        bool HasFloor(EntityUid area) => floored.TryGetValue(area, out var has) ? has : floored[area] = _areas.StandingFloor(area).Count > 0;
        candidates.RemoveAll(area => !HasFloor(area));
        fallbacks.RemoveAll(area => !HasFloor(area));
        var distinct = (candidates.Count > 0 ? candidates : fallbacks).Distinct().OrderBy(area => area.Bits).ToList();
        if (distinct.Count == 0)
        {
            Log("meteor shower finds nobody in a room with floor to hit; giving the slot back");
            QueueDel(shower);
            return;
        }
        var seed = Seed.Mix((ulong)_timing.CurTime.Ticks, shower.Owner.Bits);
        var target = distinct[(int)(seed % (ulong)distinct.Count)];
        AddComp(shower, new EventTarget(target, CompOrNull<DisplayName>(target)?.Name ?? ""));
    }

    void OnStrike(Entity<MeteorShowerComponent> shower, ref EventStrikes args)
    {
        if (HasComp<EventTarget>(shower))
        {
            AddComp(shower, new ShoweringComponent { Thrown = 0, Next = _timing.CurTime, Rocks = [] });
        }
    }

    // One rock per due deadline at a random standing cell of the room, from
    // a rolled bearing; the event is gone once its last rock has landed, so
    // the hull tag holds the pool while rocks are in the air
    void ThrowRocks(Entity<MeteorShowerComponent> shower, EntityUid scope)
    {
        if (!TryComp<ShoweringComponent>(shower, out var showering) || !TryComp<EventTarget>(shower, out var target))
        {
            return;
        }
        if (showering.Thrown >= shower.Comp.Count)
        {
            if (!showering.Rocks.Any(rock => _rocks.HasComp(rock)))
            {
                QueueDel(shower);
            }
            return;
        }
        if (_timing.CurTime < showering.Next)
        {
            return;
        }
        if (CompOrNull<TilePos>(target.Area) is not { } room)
        {
            QueueDel(shower);
            return;
        }
        var seed = Seed.Mix(Seed.Mix((ulong)_timing.CurTime.Ticks, shower.Owner.Bits), showering.Thrown);
        var standing = _areas.StandingFloor(target.Area);
        if (standing.Count == 0)
        {
            Log($"no floor left to aim at in {target.Name}");
        }
        else if (Draw(shower.Comp.Pool, Seed.Unit(Seed.Mix(seed, 1))) is not { } kind)
        {
            Log("the shower has an empty pool");
        }
        else
        {
            var cell = standing[(int)(seed % (ulong)standing.Count)];
            if (_flight.ThrowFromSpace(room.Map, cell, kind, Seed.Mix(seed, 2)) is { } rock)
            {
                showering.Rocks.Add(rock);
            }
            else
            {
                Log($"no clear bearing for a `{kind}` at {cell} in {target.Name}");
            }
        }
        showering.Thrown += 1;
        var gap = shower.Comp.OverSecs / Math.Max(shower.Comp.Count, 1u);
        showering.Next = _timing.CurTime + TimeSpan.FromSeconds(Math.Max(gap, 0f));
    }

    // The pool entry the unit in 0..1 lands on by weight, null for an empty pool
    static string? Draw(Dictionary<string, float> pool, float unit)
    {
        var kinds = pool.Where(kind => kind.Value > 0f).OrderBy(kind => kind.Key, StringComparer.Ordinal).ToList();
        var total = kinds.Sum(kind => kind.Value);
        if (total <= 0f)
        {
            return null;
        }
        var mark = Math.Clamp(unit, 0f, 0.999999f) * total;
        foreach (var (kind, weight) in kinds)
        {
            if (mark < weight)
            {
                return kind;
            }
            mark -= weight;
        }
        return null;
    }
}
