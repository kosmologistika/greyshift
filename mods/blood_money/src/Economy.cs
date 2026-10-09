using Rs13.Mods;
using static TypedModuleWorld.wit.Imports.rs13.mods.v0_1_0.ITypedEcsImports;

// Money knocked loose this frame, pooled per victim before it becomes
// entities: a shotgun blast is six damage events in one tick, and without
// pooling it sprays six times as many piles as one rifle hit worth more
class PendingSpills
{
    public List<Spill> Spills { get; set; } = [];
}

class Spill
{
    public ulong Victim { get; set; }
    public ulong Player { get; set; }
    public ulong Map { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public float? HeadingX { get; set; }
    public float? HeadingY { get; set; }
    public uint Value { get; set; }
}

// A twenty-pile spill lands as one clink and is hoovered as one ding: the
// first pile each tick plays, the rest are the same moment
class CoinSoundGate
{
    public ulong ClinkMs { get; set; }
    public ulong DingMs { get; set; }
}

// When a pile stops waiting to be grabbed
class PileDecay
{
    public ulong EndsAtMs { get; set; }
}

// Whose wallet this pile flew out of. That player cannot pocket it until
// the lock passes; everyone else can immediately
class SpilledBy
{
    public ulong Player { get; set; }
    public ulong LockedUntilMs { get; set; }
}

// A counter that salts every spill's seed
class Salt
{
    public ulong Value { get; set; }
}

// Monotonic anchor of the drip: a rate counts elapsed seconds
class DripAnchor
{
    public ulong LastMs { get; set; }
}

// The coin economy: hits knock coins out of the victim onto the floor,
// anyone walks over them, kills mint a bounty, a coin bomb showers its worth
static class Economy
{
    const string CoinProto = "coin_handful";
    // How long a spill refuses its own victim. Walls can ricochet a handful
    // right back to the feet it flew from, and without this the leak refunds
    // itself while the victim stands still
    const ulong SpillerLockMs = 2000;
    // A big spill never becomes a hundred entities: past this many piles the
    // handfuls just get fatter
    const uint MaxPiles = 20;
    // One tick: sounds inside this window are the same moment
    const ulong SameMomentMs = 20;
    const float BurnSecs = 0.7f;

    public static void Setup(ModApp app)
    {
        app.RegisterResource(ModJson.Default.PendingSpills);
        app.RegisterResource(ModJson.Default.CoinSoundGate);
        app.RegisterResource(ModJson.Default.Salt);
        app.RegisterResource(ModJson.Default.DripAnchor);
        app.RegisterComponent(ModJson.Default.PileDecay);
        app.RegisterComponent(ModJson.Default.SpilledBy);
        app.Observe<DamageDealt>(LeakOnHit);
        app.Observe<Killed>(BountyOnKill);
        app.Observe<Triggered>(BurstOnTrigger);
        app.ObserveRemoved<InFlight>(ClinkOnLanding);
        app.Every(TimeSpan.Zero, FlushSpills);
        app.Every(TimeSpan.Zero, PickUpPiles);
        app.Every(TimeSpan.Zero, ReapStalePiles);
        app.Every(TimeSpan.FromSeconds(1), Drip);
    }

    // The live economy rule, none once its scope has ended
    static CoinEconomyRule? Rule(World world)
    {
        foreach (var (_, rule, childOf) in world.Query<CoinEconomyRule, ChildOf>())
        {
            if (world.Get<ScopeEnded>(childOf.Value) is null)
            {
                return rule;
            }
        }
        return null;
    }

    static bool Standing(World world, ulong body) =>
        world.Get<Mob>(body) is not null && world.Get<Dead>(body) is null && world.Get<Ghost>(body) is null;

    // The record entity and wallet of a player
    static (ulong entity, Wallet wallet)? WalletOf(World world, ulong player)
    {
        foreach (var (entity, record, wallet) in world.Query<PlayerRecord, Wallet>())
        {
            if (record.id == player)
            {
                return (entity, wallet);
            }
        }
        return null;
    }

    static void Credit(World world, ulong entity, Wallet wallet, uint amount) =>
        world.Mutate(entity, new Wallet(Saturate(wallet.balance, amount), Saturate(wallet.earned, amount)));

    static uint Saturate(uint a, uint b) => (uint)Math.Min((ulong)a + b, uint.MaxValue);

    // A hit knocks coins worth the damage out of the victim's wallet. A broke
    // victim still showers a minted crumb at the flush, so a hit always pays
    // something to whoever holds the ground afterwards. The weapon's CoinHooks
    // scale the knock-out and pocket a share of it for the attacker
    static void LeakOnHit(DamageDealt on, World world)
    {
        if (!world.RoundLive || Rule(world) is null)
        {
            return;
        }
        if (world.Get<ControlledBy>(on.entity) is not { } controlled || world.Get<TilePos>(on.entity) is not { } at || !Standing(world, on.entity))
        {
            return;
        }
        var player = controlled.Value;
        // A blow leaks coins; suffocating on the floor does not
        uint damage = 0;
        foreach (var (kind, amount) in on.spec)
        {
            if (kind != DamageType.ASPHYXIATION)
            {
                damage += amount;
            }
        }
        if (damage == 0)
        {
            return;
        }
        var hook = (on.weapon is { } weapon ? world.Get<CoinHooks>(weapon) : null) ?? new CoinHooks(1f, 0f);
        if (WalletOf(world, player) is not var (walletEntity, wallet))
        {
            return;
        }
        // Only what actually drained pools here; the broke-victim crumb is
        // decided at flush, once per victim per frame
        var knocked = (uint)Math.Round(damage * Math.Max(hook.knockout, 0f));
        var drained = Math.Min(wallet.balance, knocked);
        world.Mutate(walletEntity, new Wallet(wallet.balance - drained, wallet.earned));
        var refund = (uint)Math.Round(drained * Math.Clamp(hook.refund, 0f, 1f));
        var spilled = drained - refund;
        ulong? attacker = on.origin is { } origin && origin != on.entity ? world.Get<ControlledBy>(origin)?.Value : null;
        // A share nobody can pocket showers with the rest: coins never vanish
        if (refund > 0)
        {
            if (attacker is { } by && WalletOf(world, by) is var (attackerEntity, attackerWallet))
            {
                Credit(world, attackerEntity, attackerWallet, refund);
            }
            else
            {
                spilled = drained;
            }
        }
        var pending = world.GetResource<PendingSpills>() ?? new PendingSpills();
        var spill = pending.Spills.FirstOrDefault(s => s.Victim == on.entity);
        if (spill is null)
        {
            spill = new Spill { Victim = on.entity, Player = player };
            pending.Spills.Add(spill);
        }
        spill.Value += spilled;
        spill.Map = at.map;
        spill.X = at.pos.x;
        spill.Y = at.pos.y;
        if (spill.HeadingX is null && on.heading is { } heading)
        {
            spill.HeadingX = heading.x;
            spill.HeadingY = heading.y;
        }
        world.InsertResource(pending);
    }

    // Turn the tick's pooled spills into flying handfuls
    static void FlushSpills(World world)
    {
        if (world.GetResource<PendingSpills>() is not { } pending || pending.Spills.Count == 0)
        {
            return;
        }
        world.InsertResource(new PendingSpills());
        if (Rule(world) is not { } rule)
        {
            return;
        }
        foreach (var spill in pending.Spills)
        {
            // A spill entry with nothing drained means every hit this frame found
            // an empty wallet: mint one crumb for the lot, so a hit always
            // showers something and a pellet cloud never showers more
            var value = spill.Value == 0 ? rule.crumb : spill.Value;
            var at = new TilePos(spill.Map, new IVec2(spill.X, spill.Y));
            Vec2? heading = spill.HeadingX is { } x && spill.HeadingY is { } y ? new Vec2(x, y) : null;
            SpawnCoins(world, at, spill.Victim, heading, spill.Player, value, rule, Mix(NextSalt(world), spill.Player));
        }
    }

    // An attributed kill mints a bounty and sprays it wide at the corpse.
    // Unattributed deaths mint nothing
    static void BountyOnKill(Killed on, World world)
    {
        if (on.killer is null || !world.RoundLive || Rule(world) is not { } rule || world.Get<TilePos>(on.entity) is not { } at)
        {
            return;
        }
        SpawnCoins(world, at, on.entity, null, null, rule.bounty, rule, Mix(NextSalt(world), on.victim));
    }

    // A coin bomb going off: its worth showers out of wherever it is, held or
    // on the floor. Outside a running economy it just pops
    static void BurstOnTrigger(Triggered on, World world)
    {
        if (world.Get<CoinBurst>(on.entity) is not { } burst || !world.RoundLive || Rule(world) is not { } rule || world.GridPosition(on.entity) is not { } at)
        {
            return;
        }
        SpawnCoins(world, at, on.entity, null, null, burst.value, rule, Mix(on.entity, 0xc01));
    }

    static ulong NextSalt(World world)
    {
        var salt = (world.GetResource<Salt>()?.Value ?? 0) + 1;
        world.InsertResource(new Salt { Value = salt });
        return salt;
    }

    // Money thrown from the victim as handfuls of the rule's pile value each,
    // so the number of piles on the floor reads as the money on it. Each pile
    // flies a seeded ray out of the victim, fanned around the blow's heading
    // when it has one, all directions otherwise, and expires once down
    static void SpawnCoins(World world, TilePos at, ulong from, Vec2? heading, ulong? spiller, uint value, CoinEconomyRule rule, ulong seed)
    {
        if (value == 0)
        {
            return;
        }
        var nowMs = (ulong)world.Now.TotalMilliseconds;
        var piles = Math.Clamp(value / Math.Max(rule.pileValue, 1), 1, MaxPiles);
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
            if (world.SpawnPrototype(CoinProto) is not { } pile)
            {
                world.Log($"`{CoinProto}` prototype missing; {value} cr evaporated");
                return;
            }
            world.Insert(pile, new CoinPile(worth));
            world.InsertOwn(pile, new PileDecay { EndsAtMs = nowMs + rule.coinLifeSecs * 1000 });
            if (spiller is { } player)
            {
                world.InsertOwn(pile, new SpilledBy { Player = player, LockedUntilMs = nowMs + SpillerLockMs });
            }
            var roll = Mix(seed, n);
            // Fan onward past the victim, the way blood sprays: the blow's
            // heading plus up to a quarter turn of seeded jitter either way
            var angle = heading is { } dir
                ? MathF.Atan2(dir.y, dir.x) + UnitSigned(roll) * (MathF.PI / 2)
                : UnitSigned(roll) * MathF.PI;
            var range = 1.2f + Unit(Mix(roll, 7)) * 1.8f;
            // Coins stop at walls or ricochet off them, pass through bodies, and
            // strike nothing: a spill must not fire twenty impact sounds
            var rules = new FlightRules(true, false, false, true, 0, true);
            world.LaunchRay(pile, at, angle, range, 7.0f, from, rules);
        }
    }

    // A handful hitting the floor clinks and stops spinning: the looping flip
    // belongs to the flight, so the landing swaps to the face-up still
    static void ClinkOnLanding(ulong entity, World world)
    {
        if (world.Get<CoinPile>(entity) is null || world.Get<TilePos>(entity) is not { } at || world.Get<Appearance>(entity) is not { } appearance)
        {
            return;
        }
        appearance.sprite = new RsiRef(appearance.sprite.rsi, "coinshot_still");
        world.Insert(entity, appearance);
        var gate = world.GetResource<CoinSoundGate>() ?? new CoinSoundGate();
        var nowMs = (ulong)world.Now.TotalMilliseconds;
        if (nowMs - gate.ClinkMs >= SameMomentMs)
        {
            gate.ClinkMs = nowMs;
            world.InsertResource(gate);
            world.PlaySound("audio/effects/coin_drop.ogg", at);
        }
    }

    // A trickle that only fills wallets below the threshold: comeback fuel and a
    // guarantee that every hit has something to knock loose, never a reward
    // for hiding while rich
    static void Drip(World world)
    {
        var nowMs = (ulong)world.Now.TotalMilliseconds;
        var anchor = world.GetResource<DripAnchor>();
        if (anchor is null)
        {
            world.InsertResource(new DripAnchor { LastMs = nowMs });
            return;
        }
        var seconds = (uint)((nowMs - anchor.LastMs) / 1000);
        if (seconds == 0)
        {
            return;
        }
        world.InsertResource(new DripAnchor { LastMs = anchor.LastMs + seconds * 1000UL });
        if (Rule(world) is not { } rule)
        {
            return;
        }
        var standing = world.Query<ControlledBy, Mob>().Where(row => Standing(world, row.entity)).Select(row => row.a.Value).ToHashSet();
        foreach (var (_, session, _) in world.Query<SessionPlayer, Participant>())
        {
            if (!standing.Contains(session.Value) || WalletOf(world, session.Value) is not var (entity, wallet))
            {
                continue;
            }
            if (wallet.balance < rule.dripBelow)
            {
                Credit(world, entity, wallet, rule.dripPerSec * seconds);
            }
        }
    }

    // Walk-over pickup, open to everyone: the attacker harvesting, the victim
    // clawing their own money back, or a third party. First body on the tile
    // wins. A handful still in the air is not on the ground yet
    static void PickUpPiles(World world)
    {
        var nowMs = (ulong)world.Now.TotalMilliseconds;
        var standing = world.Query<ControlledBy, TilePos>().Where(row => Standing(world, row.entity)).Select(row => (player: row.a.Value, at: row.b)).ToList();
        if (standing.Count == 0)
        {
            return;
        }
        var gate = world.GetResource<CoinSoundGate>() ?? new CoinSoundGate();
        foreach (var (pile, coins, at) in world.Query<CoinPile, TilePos>())
        {
            if (world.Get<InFlight>(pile) is not null)
            {
                continue;
            }
            var spill = world.GetOwn<SpilledBy>(pile);
            // First body on the tile the pile does not refuse: a locked spiller
            // standing on their own coins must not shield them from everyone else
            var taker = standing
                .Where(s => s.at.map == at.map && s.at.pos.x == at.pos.x && s.at.pos.y == at.pos.y)
                .Select(s => (ulong?)s.player)
                .FirstOrDefault(who => !(spill is not null && spill.Player == who && nowMs < spill.LockedUntilMs));
            if (taker is not { } player || WalletOf(world, player) is not var (entity, wallet))
            {
                continue;
            }
            // Clawing your own spill back refills the balance but earns nothing:
            // earned is the round tiebreak, and laps around your own coins must
            // not farm it
            if (spill is not null && spill.Player == player)
            {
                world.Mutate(entity, new Wallet(Saturate(wallet.balance, coins.value), wallet.earned));
            }
            else
            {
                Credit(world, entity, wallet, coins.value);
            }
            if (nowMs - gate.DingMs >= SameMomentMs)
            {
                gate.DingMs = nowMs;
                world.InsertResource(gate);
                world.PlaySound("audio/effects/coin_ding.ogg", at);
            }
            world.Despawn(pile);
        }
    }

    // A stale pile burns away; a burning one can still be snatched from the
    // flames right up to the reaper
    static void ReapStalePiles(World world)
    {
        var now = world.Now;
        var nowMs = (ulong)now.TotalMilliseconds;
        foreach (var (pile, decay) in world.QueryOwn<PileDecay>())
        {
            if (nowMs < decay.EndsAtMs || world.Get<BurnOut>(pile) is not null)
            {
                continue;
            }
            world.Insert(pile, new BurnOut(new Deadline(Durations.From(now + TimeSpan.FromSeconds(BurnSecs))), BurnSecs));
            world.RemoveOwn<PileDecay>(pile);
        }
    }

    // Seeded rolls with no rand: the same inputs give the same shower
    static ulong Mix(ulong a, ulong b)
    {
        var x = a * 0x9E3779B97F4A7C15UL ^ (b + 0x632BE59BD9B4E019UL);
        x ^= x >> 30;
        x *= 0xBF58476D1CE4E5B9UL;
        x ^= x >> 27;
        x *= 0x94D049BB133111EBUL;
        return x ^ (x >> 31);
    }

    static float Unit(ulong seed) => (seed % 1024) / 1023f;

    static float UnitSigned(ulong seed) => Unit(seed) * 2f - 1f;
}
