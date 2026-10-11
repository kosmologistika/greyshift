using Rs13.Mods;

namespace BloodMoney;

// On the victim's record: who to point their death cam at. Consumed by the
// first ghost the player controls
[RegisterComponent]
public sealed partial class KilledByComponent : Component
{
    [DataField] public PlayerId Player;
}

// The death cam was settled for this ghost; a player who then walks off it
// chose to, and is not re-attached
[RegisterComponent]
public sealed partial class CamSettledComponent : Component;

public sealed partial class DeathCamSystem : EntitySystem
{
    [Dependency] GameTicker _ticker = default!;
    [Dependency] WalletSystem _wallets = default!;
    [Dependency] EntityQuery<Dead> _dead = default!;
    [Dependency] EntityQuery<Ghost> _ghosts = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<Killed>(RememberKiller);
        UpdatesAfter.Add(typeof(LoadoutSystem));
        Every(TimeSpan.Zero, AttachDeathCam);
    }

    void RememberKiller(ref Killed args)
    {
        if (!_ticker.IsGameRuleActive<ShopRuleComponent>() || args.Killer is not { } killer || !_wallets.TryFind(args.Victim, out var record))
        {
            return;
        }
        AddComp(record, new KilledByComponent { Player = killer });
    }

    // A fresh ghost is pointed at its killer, or the nearest living body on its
    // map, and parked there; once settled it is never re-attached. The package
    // loads under every mode, so only where this rule runs
    void AttachDeathCam()
    {
        if (!Rules.RoundRunning(_ticker) || !_ticker.IsGameRuleActive<ShopRuleComponent>())
        {
            return;
        }
        var participants = new HashSet<PlayerId>();
        var sessions = EntityQueryEnumerator<SessionPlayer, Participant>();
        while (sessions.MoveNext(out _, out var session, out _))
        {
            participants.Add(session.Value);
        }
        var standing = new List<(EntityUid Body, PlayerId Player, TilePos At)>();
        var mobs = EntityQueryEnumerator<ControlledBy, TilePos, Mob>();
        while (mobs.MoveNext(out var body, out var controlled, out var at, out _))
        {
            if (!_dead.HasComp(body) && !_ghosts.HasComp(body))
            {
                standing.Add((body, controlled.Value, at));
            }
        }
        var fresh = new List<(EntityUid Ghost, PlayerId Player, TilePos Here)>();
        var ghosts = EntityQueryEnumerator<ControlledBy, TilePos, Ghost>();
        while (ghosts.MoveNext(out var ghost, out var controlled, out var here, out _))
        {
            if (participants.Contains(controlled.Value) && !HasComp<Following>(ghost) && !HasComp<CamSettledComponent>(ghost))
            {
                fresh.Add((ghost, controlled.Value, here));
            }
        }
        var wallets = _wallets.ByPlayer();
        foreach (var (ghost, player, here) in fresh)
        {
            var hasRecord = wallets.TryGetValue(player, out var record);
            var killer = hasRecord ? CompOrNull<KilledByComponent>(record)?.Player : null;
            if ((BodyOf(standing, killer) ?? Nearest(standing, here)) is not { } target)
            {
                continue;
            }
            AddComp(ghost, new Following(target));
            AddComp(ghost, new Facing(Dir.South));
            AddComp(ghost, new CamSettledComponent());
            if (hasRecord)
            {
                RemComp<KilledByComponent>(record);
            }
        }
    }

    static EntityUid? BodyOf(List<(EntityUid Body, PlayerId Player, TilePos At)> standing, PlayerId? player)
    {
        foreach (var (body, owner, _) in standing)
        {
            if (owner == player)
            {
                return body;
            }
        }
        return null;
    }

    // The first of the nearest in query order, as the stable OrderBy it replaces
    static EntityUid? Nearest(List<(EntityUid Body, PlayerId Player, TilePos At)> standing, TilePos here)
    {
        EntityUid? best = null;
        var bestDistance = int.MaxValue;
        foreach (var (body, _, at) in standing)
        {
            if (at.Map != here.Map)
            {
                continue;
            }
            var distance = CartSystem.Chebyshev(at.Pos, here.Pos);
            if (distance < bestDistance)
            {
                (best, bestDistance) = (body, distance);
            }
        }
        return best;
    }
}
