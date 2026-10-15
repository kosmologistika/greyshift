using System.Numerics;
using Rs13.Mods;

namespace BloodMoney;

// Money knocked loose this frame, pooled per victim before it becomes
// entities: a shotgun blast is six damage events in one tick, and without
// pooling it sprays six times as many piles as one rifle hit worth more
[RegisterResource]
public sealed partial class PendingSpills
{
    [DataField] public List<Spill> Spills = [];
}

public sealed class Spill
{
    public EntityUid Victim;
    public PlayerId Player;
    public TilePos At = new(EntityUid.Invalid, new Vector2i(0, 0));
    public Vector2? Heading;
    public uint Value;
}

// A twenty-pile spill lands as one clink and is hoovered as one ding: the
// first pile each tick plays, the rest are the same moment
[RegisterResource]
public sealed partial class CoinSoundGate
{
    [DataField] public ulong ClinkTick;
    [DataField] public ulong DingTick;
}

// When a pile stops waiting to be grabbed
[RegisterComponent]
public sealed partial class PileDecayComponent : Component
{
    [DataField] public TimeSpan EndsAt;
}

// Whose wallet this pile flew out of. That player cannot pocket it until
// the lock passes; everyone else can immediately
[RegisterComponent]
public sealed partial class SpilledByComponent : Component
{
    [DataField] public PlayerId Player;
    [DataField] public TimeSpan LockedUntil;
}

// A counter that salts every spill's seed
[RegisterResource]
public sealed partial class Salt
{
    [DataField] public ulong Value;
}

// Monotonic anchor of the drip: a rate counts elapsed seconds
[RegisterResource]
public sealed partial class DripAnchor
{
    [DataField] public TimeSpan Last;
}

// The coin economy: hits knock coins out of the victim onto the floor,
// anyone walks over them, kills mint a bounty, a coin bomb showers its worth
public sealed partial class EconomyRuleSystem : GameRuleSystem<CoinEconomyRuleComponent>
{
    const string CoinProto = "coin_handful";
    // How long a spill refuses its own victim. Walls can ricochet a handful
    // right back to the feet it flew from, and without this the leak refunds
    // itself while the victim stands still
    static readonly TimeSpan SpillerLock = TimeSpan.FromSeconds(2);
    // A big spill never becomes a hundred entities: past this many piles the
    // handfuls just get fatter
    const uint MaxPiles = 20;
    const float BurnSecs = 0.7f;

    [Dependency] WalletSystem _wallets = default!;
    [Dependency] IGameTiming _timing = default!;
    [Dependency] TransformSystem _transform = default!;
    [Dependency] FlightSystem _flight = default!;
    [Dependency] AudioSystem _audio = default!;
    [Dependency] EntityQuery<Mob> _mobs = default!;
    [Dependency] EntityQuery<Dead> _dead = default!;
    [Dependency] EntityQuery<Ghost> _ghosts = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DamageDealt>(LeakOnHit);
        SubscribeLocalEvent<Killed>(BountyOnKill);
        SubscribeLocalEvent<CoinBurstComponent, Triggered>(BurstOnTrigger);
        SubscribeLocalEvent<InFlight, ComponentRemove>(ClinkOnLanding);
        // The pool and the drip's anchor move whenever a round runs, rule or not:
        // a spill pooled as its match ended is dropped, and an intermission is not owed
        Every(TimeSpan.Zero, FlushSpills);
        EveryActive(TimeSpan.Zero, PickUpPiles);
        EveryActive(TimeSpan.Zero, ReapStalePiles);
        Every(TimeSpan.FromSeconds(1), Drip);
    }

    // The live economy rule, none once its scope has ended
    CoinEconomyRuleComponent? Rule()
    {
        var rules = EntityQueryEnumerator<CoinEconomyRuleComponent>();
        while (rules.MoveNext(out var uid, out var rule))
        {
            if (TryComp<ChildOf>(uid, out var childOf) && !HasComp<ScopeEnded>(childOf.Value))
            {
                return rule;
            }
        }
        return null;
    }

    bool Standing(EntityUid body) => _mobs.HasComp(body) && !_dead.HasComp(body) && !_ghosts.HasComp(body);

    // A hit knocks coins worth the damage out of the victim's wallet. A broke
    // victim still showers a minted crumb at the flush, so a hit always pays
    // something to whoever holds the ground afterwards. The weapon's CoinHooks
    // scale the knock-out and pocket a share of it for the attacker
    void LeakOnHit(ref DamageDealt args)
    {
        if (!GameTicker.IsRoundLive || Rule() is null)
        {
            return;
        }
        if (!TryComp<ControlledBy>(args.Entity, out var controlled) || !TryComp<TilePos>(args.Entity, out var at) || !Standing(args.Entity))
        {
            return;
        }
        var player = controlled.Value;
        // A blow from outside leaks coins; suffocating on the floor or a poison
        // wearing the body down from within does not
        uint damage = 0;
        foreach (var (kind, amount) in args.Spec)
        {
            if (kind is not (DamageType.Asphyxiation or DamageType.Toxin))
            {
                damage += amount;
            }
        }
        if (damage == 0)
        {
            return;
        }
        var hook = (args.Weapon is { } weapon ? CompOrNull<CoinHooksComponent>(weapon) : null) ?? new CoinHooksComponent();
        if (!_wallets.TryFind(player, out var record))
        {
            return;
        }
        var wallet = record.Comp2;
        // Only what actually drained pools here; the broke-victim crumb is
        // decided at flush, once per victim per frame
        // Half away from zero, as f32::round did natively
        var knocked = (uint)Math.Round(damage * Math.Max(hook.Knockout, 0f), MidpointRounding.AwayFromZero);
        var drained = Math.Min(wallet.Balance, knocked);
        wallet.Balance -= drained;
        var refund = (uint)Math.Round(drained * Math.Clamp(hook.Refund, 0f, 1f), MidpointRounding.AwayFromZero);
        var spilled = drained - refund;
        PlayerId? attacker = args.Origin is { } origin && origin != args.Entity ? CompOrNull<ControlledBy>(origin)?.Value : null;
        // A share nobody can pocket showers with the rest: coins never vanish
        if (refund > 0)
        {
            if (attacker is { } by && _wallets.TryFind(by, out var attackerRecord))
            {
                WalletSystem.Credit(attackerRecord.Comp2, refund);
            }
            else
            {
                spilled = drained;
            }
        }
        var pending = Resource<PendingSpills>();
        var victim = args.Entity;
        var spill = pending.Spills.FirstOrDefault(s => s.Victim == victim);
        if (spill is null)
        {
            spill = new Spill { Victim = victim, Player = player };
            pending.Spills.Add(spill);
        }
        spill.Value += spilled;
        spill.At = at;
        spill.Heading ??= args.Heading;
    }

    // Turn the tick's pooled spills into flying handfuls
    void FlushSpills()
    {
        if (!Rules.RoundRunning(GameTicker))
        {
            return;
        }
        var pending = Resource<PendingSpills>();
        if (pending.Spills.Count == 0)
        {
            return;
        }
        var spills = pending.Spills;
        pending.Spills = [];
        if (Rule() is not { } rule)
        {
            return;
        }
        foreach (var spill in spills)
        {
            // A spill entry with nothing drained means every hit this frame found
            // an empty wallet: mint one crumb for the lot, so a hit always
            // showers something and a pellet cloud never showers more
            var value = spill.Value == 0 ? rule.Crumb : spill.Value;
            SpawnCoins(spill.At, spill.Victim, spill.Heading, spill.Player, value, rule, Seed.Mix(NextSalt(), spill.Player.Value));
        }
    }

    // An attributed kill mints a bounty and sprays it wide at the corpse.
    // Unattributed deaths mint nothing
    void BountyOnKill(ref Killed args)
    {
        if (args.Killer is null || !GameTicker.IsRoundLive || Rule() is not { } rule || !TryComp<TilePos>(args.Entity, out var at))
        {
            return;
        }
        SpawnCoins(at, args.Entity, null, null, rule.Bounty, rule, Seed.Mix(NextSalt(), args.Victim.Value));
    }

    // A coin bomb going off: its worth showers out of wherever it is, held or
    // on the floor. Outside a running economy it just pops
    void BurstOnTrigger(Entity<CoinBurstComponent> bomb, ref Triggered args)
    {
        if (!GameTicker.IsRoundLive || Rule() is not { } rule || _transform.GridPosition(bomb) is not { } at)
        {
            return;
        }
        SpawnCoins(at, bomb, null, null, bomb.Comp.Value, rule, Seed.Mix(bomb.Owner.Bits, 0xc01));
    }

    ulong NextSalt() => ++Resource<Salt>().Value;

    // Money thrown from the victim as handfuls of the rule's pile value each,
    // so the number of piles on the floor reads as the money on it. Each pile
    // flies a seeded ray out of the victim, fanned around the blow's heading
    // when it has one, all directions otherwise, and expires once down
    void SpawnCoins(TilePos at, EntityUid from, Vector2? heading, PlayerId? spiller, uint value, CoinEconomyRuleComponent rule, ulong seed)
    {
        if (value == 0)
        {
            return;
        }
        var now = Now;
        var piles = Math.Clamp(value / Math.Max(rule.PileValue, 1), 1, MaxPiles);
        var share = value / piles;
        var remainder = value % piles;
        for (uint n = 0; n < piles; n++)
        {
            var worth = share + (remainder > 0 ? 1u : 0u);
            remainder = remainder > 0 ? remainder - 1 : 0;
            if (worth == 0)
            {
                continue;
            }
            if (!TrySpawn(CoinProto, out var pile))
            {
                Log($"`{CoinProto}` prototype missing; {value} cr evaporated");
                return;
            }
            AddComp(pile, new CoinPileComponent { Value = worth });
            AddComp(pile, new PileDecayComponent { EndsAt = now + TimeSpan.FromSeconds(rule.CoinLifeSecs) });
            if (spiller is { } player)
            {
                AddComp(pile, new SpilledByComponent { Player = player, LockedUntil = now + SpillerLock });
            }
            var roll = Seed.Mix(seed, n);
            // Fan onward past the victim, the way blood sprays: the blow's
            // heading plus up to a quarter turn of seeded jitter either way
            var angle = heading is { } dir
                ? MathF.Atan2(dir.Y, dir.X) + Seed.UnitSigned(roll) * (MathF.PI / 2)
                : Seed.UnitSigned(roll) * MathF.PI;
            var range = 1.2f + Seed.Unit(Seed.Mix(roll, 7)) * 1.8f;
            // Coins stop at walls or ricochet off them, pass through bodies, and
            // strike nothing: a spill must not fire twenty impact sounds
            var rules = new FlightRules(true, false, false, true, 0, true);
            _flight.LaunchRay(pile, at, angle, range, 7.0f, from, rules);
        }
    }

    // A handful hitting the floor clinks and stops spinning: the looping flip
    // belongs to the flight, so the landing swaps to the face-up still
    void ClinkOnLanding(Entity<InFlight> flier, ref ComponentRemove args)
    {
        if (!HasComp<CoinPileComponent>(flier) || !TryComp<TilePos>(flier, out var at) || !TryComp<Appearance>(flier, out var appearance))
        {
            return;
        }
        AddComp(flier, appearance with { Sprite = new RsiRef(appearance.Sprite.Rsi, "coinshot_still") });
        var gate = Resource<CoinSoundGate>();
        var tick = _timing.CurTick;
        if (gate.ClinkTick != tick)
        {
            gate.ClinkTick = tick;
            _audio.Play("audio/effects/coin_drop.ogg", at);
        }
    }

    // A trickle that only fills wallets below the threshold: comeback fuel and a
    // guarantee that every hit has something to knock loose, never a reward
    // for hiding while rich
    void Drip()
    {
        if (!Rules.RoundRunning(GameTicker))
        {
            return;
        }
        var now = Now;
        var anchor = Resource<DripAnchor>();
        if (anchor.Last == TimeSpan.Zero)
        {
            anchor.Last = now;
            return;
        }
        var seconds = (uint)(now - anchor.Last).TotalSeconds;
        if (seconds == 0)
        {
            return;
        }
        anchor.Last += TimeSpan.FromSeconds(seconds);
        if (Rule() is not { } rule)
        {
            return;
        }
        var standing = new HashSet<PlayerId>();
        var mobs = EntityQueryEnumerator<ControlledBy, Mob>();
        while (mobs.MoveNext(out var body, out var controlled, out _))
        {
            if (Standing(body))
            {
                standing.Add(controlled.Value);
            }
        }
        var wallets = _wallets.ByPlayer();
        var sessions = EntityQueryEnumerator<SessionPlayer, Participant>();
        while (sessions.MoveNext(out _, out var session, out _))
        {
            if (!standing.Contains(session.Value) || !wallets.TryGetValue(session.Value, out var record))
            {
                continue;
            }
            if (record.Comp2.Balance < rule.DripBelow)
            {
                WalletSystem.Credit(record.Comp2, rule.DripPerSec * seconds);
            }
        }
    }

    // Walk-over pickup, open to everyone: the attacker harvesting, the victim
    // clawing their own money back, or a third party. First body on the tile
    // wins. A handful still in the air is not on the ground yet
    void PickUpPiles(Entity<CoinEconomyRuleComponent> rule, EntityUid scope)
    {
        var now = Now;
        var standing = new List<(PlayerId player, TilePos at)>();
        var bodies = EntityQueryEnumerator<ControlledBy, TilePos>();
        while (bodies.MoveNext(out var body, out var controlled, out var at))
        {
            if (Standing(body))
            {
                standing.Add((controlled.Value, at));
            }
        }
        if (standing.Count == 0)
        {
            return;
        }
        var wallets = _wallets.ByPlayer();
        var gate = Resource<CoinSoundGate>();
        var tick = _timing.CurTick;
        var piles = EntityQueryEnumerator<CoinPileComponent>();
        while (piles.MoveNext(out var pile, out var coins))
        {
            if (!TryComp<TilePos>(pile, out var at) || HasComp<InFlight>(pile))
            {
                continue;
            }
            var spill = CompOrNull<SpilledByComponent>(pile);
            // First body on the tile the pile does not refuse: a locked spiller
            // standing on their own coins must not shield them from everyone else
            PlayerId? taker = null;
            foreach (var (player, here) in standing)
            {
                if (here.Map != at.Map || here.Pos != at.Pos)
                {
                    continue;
                }
                if (spill is not null && spill.Player == player && now < spill.LockedUntil)
                {
                    continue;
                }
                taker = player;
                break;
            }
            if (taker is not { } who || !wallets.TryGetValue(who, out var record))
            {
                continue;
            }
            // Clawing your own spill back refills the balance but earns nothing:
            // earned is the round tiebreak, and laps around your own coins must
            // not farm it
            if (spill is not null && spill.Player == who)
            {
                record.Comp2.Balance = WalletSystem.Saturate(record.Comp2.Balance, coins.Value);
            }
            else
            {
                WalletSystem.Credit(record.Comp2, coins.Value);
            }
            if (gate.DingTick != tick)
            {
                gate.DingTick = tick;
                _audio.Play("audio/effects/coin_ding.ogg", at);
            }
            QueueDel(pile);
        }
    }

    // A stale pile burns away; a burning one can still be snatched from the
    // flames right up to the reaper
    void ReapStalePiles(Entity<CoinEconomyRuleComponent> rule, EntityUid scope)
    {
        var now = Now;
        var decaying = EntityQueryEnumerator<PileDecayComponent>();
        while (decaying.MoveNext(out var pile, out var decay))
        {
            if (now < decay.EndsAt || HasComp<BurnOut>(pile))
            {
                continue;
            }
            AddComp(pile, new BurnOut(new Deadline(now + TimeSpan.FromSeconds(BurnSecs)), BurnSecs));
            RemComp<PileDecayComponent>(pile);
        }
    }
}
