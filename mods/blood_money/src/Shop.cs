using Rs13.Mods;

namespace BloodMoney;

// This record's wallet was staked for the current match. Stamped on every
// record at match open; a record without it joined late and gets the
// starting stake, not just the death floor
[RegisterComponent]
public sealed partial class StakedComponent : Component;

// What this shopper bought, billed at once and owed as one loadout at the
// close; Closed marks delivery, anything else that drops the shelf refunds
[RegisterComponent]
public sealed partial class CartComponent : Component
{
    [DataField] public PlayerId Player;
    [DataField] public List<string> Products = [];
    [DataField] public uint Spent;
    [DataField] public bool Closed;
}

// The products a fresh body is owed, delivered once its hand slots exist:
// they spawn a tick after the mob
[RegisterComponent]
public sealed partial class PendingLoadoutComponent : Component
{
    [DataField] public List<string> Products = [];
}

// Seats handed out by BodyTheBuyer this tick: a second close in the same
// tick cannot see the first one's body yet, and without this every
// backstop-bought player lands on one tile
[RegisterResource]
public sealed partial class ClaimedSeats
{
    [DataField] public ulong Tick;
    [DataField] public List<TilePos> Seats = [];
}

[RegisterResource]
public sealed partial class ShopSalt
{
    [DataField] public ulong Value;
}

// This mode's shelf dressing, sent beside the shop seam's own update: every
// listing in the catalog with its tier and art, so the client can spin decoys
// and tint landings. Covering the whole catalog leaks nothing about what rolled
[NetSerializable]
public sealed partial class CatalogStyle
{
    [DataField] public List<CatalogEntry> Entries = [];
}

public sealed class CatalogEntry
{
    public string Listing = "";
    public Rarity Rarity;
    public RsiRef? Sprite;
}

// How loudly the reveal celebrates a listing. Derived from the draw weight,
// never authored: a thing is gold exactly because it barely rolls
public enum Rarity
{
    Common,
    Uncommon,
    Rare,
}

// The shop: every bodiless participant of the match is put into it, with a
// ghost to watch through, a floor-topped wallet and a rolled shelf, and the
// body they pay for spawns at the close. Buying is the only way back
public sealed partial class ShopRuleSystem : GameRuleSystem<ShopRuleComponent>
{
    public const string Catalog = "blood_money";
    public const string DefaultRole = "role_default";

    [Dependency] WalletSystem _wallets = default!;
    [Dependency] ShopSystem _shop = default!;
    [Dependency] SeatSystem _seats = default!;
    [Dependency] IPrototypeManager _protos = default!;
    [Dependency] GhostSystem _ghostSystem = default!;
    [Dependency] EntityQuery<Ghost> _ghosts = default!;

    public override void Initialize()
    {
        base.Initialize();
        EveryActive(TimeSpan.Zero, OpenShops);
        EveryActive(TimeSpan.Zero, RunBackstops);
    }

    // The map a scope owns, once its map rule has loaded one
    public EntityUid? ScopeMap(EntityUid scope)
    {
        var maps = EntityQueryEnumerator<Map, ChildOf>();
        while (maps.MoveNext(out var map, out _, out var childOf))
        {
            if (childOf.Value == scope)
            {
                return map;
            }
        }
        return null;
    }

    // The live shop rule's scope, none once the scope has ended
    public EntityUid? LiveScope()
    {
        foreach (var (_, scope) in ModRules())
        {
            return scope;
        }
        return null;
    }

    IEnumerable<(Entity<ShopRuleComponent> rule, EntityUid scope)> ModRules()
    {
        var rules = EntityQueryEnumerator<ShopRuleComponent>();
        while (rules.MoveNext(out var uid, out var rule))
        {
            if (TryComp<ChildOf>(uid, out var childOf) && !HasComp<ScopeEnded>(childOf.Value))
            {
                yield return (new Entity<ShopRuleComponent>(uid, rule), childOf.Value);
            }
        }
    }

    public EntityUid? GhostOf(PlayerId player)
    {
        var ghosts = EntityQueryEnumerator<ControlledBy, Ghost>();
        while (ghosts.MoveNext(out var ghost, out var controlled, out _))
        {
            if (controlled.Value == player)
            {
                return ghost;
            }
        }
        return null;
    }

    public ulong NextSalt() => ++Resource<ShopSalt>().Value;

    // Every wallet opens the match at the same number, and no offer survives
    // from the previous match. Earned is left alone: it is the round's
    // tiebreak and outlives the match on purpose
    protected override void Started(Entity<ShopRuleComponent> rule, ref RuleStarted args)
    {
        // The stake below supersedes any cart; close them first or the drop
        // refunds on top of it
        var offers = EntityQueryEnumerator<ShopOffer>();
        var connections = new List<EntityUid>();
        while (offers.MoveNext(out var connection, out _))
        {
            connections.Add(connection);
            if (TryComp<CartComponent>(connection, out var cart))
            {
                cart.Closed = true;
            }
        }
        var records = EntityQueryEnumerator<PlayerRecord, Wallet>();
        while (records.MoveNext(out var record, out _, out var wallet))
        {
            wallet.Balance = rule.Comp.Start;
            // A fresh match also forgets who killed whom, or a stale stamp points
            // next match's first death cam at last match's killer
            AddComp(record, new StakedComponent());
            RemComp<KilledByComponent>(record);
        }
        foreach (var connection in connections)
        {
            RemComp<ShopOffer>(connection);
            RemComp<CartComponent>(connection);
        }
    }

    // A match that ends mid-shop takes its offers down with it: a stale shelf
    // would buy a body into the wrong match. The shelf is what parks a ghost,
    // so each shopper's ghost gets its wings back; the shelf drop refunds the cart
    protected override void Ending(Entity<ShopRuleComponent> rule, EntityUid scope, ref ScopeEnding args)
    {
        var offers = EntityQueryEnumerator<ShopOffer, SessionPlayer>();
        var shoppers = new List<(EntityUid connection, PlayerId player)>();
        while (offers.MoveNext(out var connection, out _, out var session))
        {
            shoppers.Add((connection, session.Value));
        }
        foreach (var (connection, player) in shoppers)
        {
            RemComp<ShopOffer>(connection);
            if (GhostOf(player) is { } ghost)
            {
                AddComp(ghost, new Movable());
            }
        }
    }

    // Puts every bodiless participant of the rule's match into the shop. Covers
    // the match opening (nobody has a body) and every death (the eject leaves
    // the player holding a ghost) through the same path
    void OpenShops(Entity<ShopRuleComponent> rule, EntityUid scope)
    {
        if (ScopeMap(scope) is not { } map)
        {
            return;
        }
        var bodies = new Dictionary<PlayerId, EntityUid>();
        var controlled = EntityQueryEnumerator<ControlledBy>();
        while (controlled.MoveNext(out var body, out var by))
        {
            bodies.TryAdd(by.Value, body);
        }
        var wallets = _wallets.ByPlayer();
        CatalogStyle? style = null;
        var sessions = EntityQueryEnumerator<SessionPlayer, Participant>();
        while (sessions.MoveNext(out var connection, out var session, out _))
        {
            var player = session.Value;
            if (!bodies.TryGetValue(player, out var body))
            {
                var pos = _seats.ObserverSeat(map) ?? new Vector2i(0, 0);
                _ghostSystem.SpawnGhost(new TilePos(map, pos), player);
                continue;
            }
            if (!_ghosts.HasComp(body) || HasComp<ShopOffer>(connection))
            {
                continue;
            }
            if (!wallets.TryGetValue(player, out var record))
            {
                continue;
            }
            var wallet = record.Comp2;
            var balance = wallet.Balance;
            // A record without this match's stake joined late: they enter on the
            // starting wallet, not just the death floor
            if (!HasComp<StakedComponent>(record))
            {
                balance = Math.Max(balance, rule.Comp.Start);
                AddComp(record, new StakedComponent());
            }
            wallet.Balance = Math.Max(balance, rule.Comp.Floor);
            var offer = RollOffer(rule.Comp, Seed.Mix(NextSalt(), player.Value));
            if (offer.Rows.Count == 0)
            {
                Log($"the `{Catalog}` catalog has no listings; shoppers will be force-respawned bare-handed");
            }
            AddComp(connection, offer);
            AddComp(connection, new CartComponent { Player = player });
            // The seam's update carries the shelf; the dressing that makes it a
            // slot machine is this mode's own wire
            style ??= Style();
            RaiseNetworkEvent(style, connection);
            // A shopping ghost is parked on its death cam: no free flight while
            // the shelf stands. Movable comes back if the match closes the shop
            RemComp<Movable>(body);
        }
    }

    // Staples always in stock, plus rolled rarity-weighted draws without
    // replacement, all from this mode's catalog only. seed % total, never a
    // scaled float: the top of a 0..=1 range lands past the last slot
    ShopOffer RollOffer(ShopRuleComponent rule, ulong seed)
    {
        var listings = _shop.Listings(Catalog);
        var rows = listings.Where(l => l.Staple).Select(RowFor).ToList();
        rows.Sort((a, b) => a.Price != b.Price ? a.Price.CompareTo(b.Price) : string.CompareOrdinal(a.Listing, b.Listing));
        var pool = listings.Where(l => !l.Staple && l.Weight > 0).OrderBy(l => l.Id, StringComparer.Ordinal).ToList();
        for (uint n = 0; n < rule.Rolled; n++)
        {
            var total = pool.Aggregate(0UL, (sum, l) => sum + l.Weight);
            if (total == 0)
            {
                break;
            }
            var roll = Seed.Mix(seed, n) % total;
            var index = -1;
            for (var i = 0; i < pool.Count; i++)
            {
                if (roll < pool[i].Weight)
                {
                    index = i;
                    break;
                }
                roll -= pool[i].Weight;
            }
            if (index < 0)
            {
                break;
            }
            rows.Add(RowFor(pool[index]));
            pool.RemoveAt(index);
        }
        var now = Now;
        return new ShopOffer(rows, new Deadline(now + TimeSpan.FromSeconds(rule.LockSecs)), new Deadline(now + TimeSpan.FromSeconds(rule.BackstopSecs)));
    }

    OfferRow RowFor(ShopListing listing) =>
        new(listing.Id, listing.Product.Id, _protos.Name(listing.Product) ?? listing.Product.Id, _protos.Sprite(listing.Product), listing.Price, listing.Staple, listing.Shelf, false);

    // The whole catalog dressed for the reveal, staples included, so the spin
    // is a tour of everything the shelf could hold
    CatalogStyle Style()
    {
        var listings = _shop.Listings(Catalog);
        var heaviest = listings.Count == 0 ? 0 : listings.Max(l => l.Weight);
        var entries = listings
            .Select(l => new CatalogEntry { Listing = l.Id, Rarity = RarityOf(l, heaviest), Sprite = _protos.Sprite(l.Product) })
            .OrderBy(e => e.Listing, StringComparer.Ordinal)
            .ToList();
        return new CatalogStyle { Entries = entries };
    }

    // Tier bands relative to the pool's heaviest weight, because draw weights
    // only mean anything against each other. Staples (weight 0) are common:
    // always there, never celebrated
    static Rarity RarityOf(ShopListing listing, uint heaviest)
    {
        if (listing.Weight == 0)
        {
            return Rarity.Common;
        }
        if (listing.Weight * 8 <= heaviest)
        {
            return Rarity.Rare;
        }
        return listing.Weight * 2 <= heaviest ? Rarity.Uncommon : Rarity.Common;
    }

    // An elapsed offer closes like a Done; this mode's policy, an uplink shelf
    // just stands
    void RunBackstops(Entity<ShopRuleComponent> rule, EntityUid scope)
    {
        var now = Now;
        var elapsed = new List<(EntityUid connection, PlayerId player)>();
        var offers = EntityQueryEnumerator<ShopOffer, SessionPlayer>();
        while (offers.MoveNext(out var connection, out var offer, out var session))
        {
            if (offer.ClosesAt is { } closes && now >= closes.EndsAt)
            {
                elapsed.Add((connection, session.Value));
            }
        }
        foreach (var (connection, player) in elapsed)
        {
            _shop.CloseOffer(connection, player);
        }
    }

    public bool Arms(string product) => _protos.Declares(product, "MeleeWeapon") || _protos.Declares(product, "Gun");
}

// The cart on a shopper's connection: billed at the buy, delivered or
// refunded when the shelf comes down
public sealed partial class CartSystem : EntitySystem
{
    // Tiles nearer the fight than the farthest free seat a respawn may land,
    // so it is not always the one wall a camper pre-aims
    const int RespawnSlackTiles = 3;
    // Seats a respawn chooses among when the map has that many to spare
    const int RespawnMinChoices = 3;
    // How far from every living player a seat must be to pad the choices out
    const int RespawnSafeTiles = 3;

    [Dependency] WalletSystem _wallets = default!;
    [Dependency] ShopRuleSystem _rule = default!;
    [Dependency] ShopSystem _shop = default!;
    [Dependency] SeatSystem _seats = default!;
    [Dependency] IPrototypeManager _protos = default!;
    [Dependency] IGameTiming _timing = default!;
    [Dependency] MindSystem _minds = default!;
    [Dependency] EntityQuery<Ghost> _ghosts = default!;
    [Dependency] EntityQuery<Dead> _dead = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CartComponent, Purchased>(StockCart);
        SubscribeLocalEvent<CartComponent, ShopDone>(BodyTheBuyer);
        SubscribeLocalEvent<ShopOffer, ComponentRemove>(RefundDroppedCart);
    }

    // Every buy is owed the moment it is billed, so a slow shopper is still
    // armed when the deadline hits
    void StockCart(Entity<CartComponent> cart, ref Purchased args)
    {
        cart.Comp.Products.Add(args.Product);
        cart.Comp.Spent += args.Price;
    }

    // A shelf that goes down with the cart undelivered, a disconnect or a match
    // ending mid-shop, gives the money back
    void RefundDroppedCart(Entity<ShopOffer> connection, ref ComponentRemove args)
    {
        if (!TryComp<CartComponent>(connection, out var cart))
        {
            return;
        }
        if (!cart.Closed)
        {
            Refund(cart.Player, cart.Spent);
        }
        RemComp<CartComponent>(connection);
    }

    void Refund(PlayerId player, uint spent)
    {
        if (spent == 0 || !_wallets.TryFind(player, out var record))
        {
            return;
        }
        record.Comp2.Balance = WalletSystem.Saturate(record.Comp2.Balance, spent);
    }

    // The close bodies the buyer with everything in the cart, the cheapest
    // staple forced in when nothing arms them, on a seat far from the fight
    void BodyTheBuyer(Entity<CartComponent> connection, ref ShopDone args)
    {
        var cart = connection.Comp;
        if (cart.Closed)
        {
            return;
        }
        cart.Closed = true;
        var spent = cart.Spent;
        var products = cart.Products;
        RemComp<CartComponent>(connection);
        var player = args.Player;
        if (HasBody(player))
        {
            Refund(player, spent);
            return;
        }
        if (_rule.LiveScope() is not { } scope || _rule.ScopeMap(scope) is not { } map)
        {
            Log($"{player} left the shop with no live match; they stay a ghost");
            Refund(player, spent);
            return;
        }
        if (_protos.RoleBody(ShopRuleSystem.DefaultRole) is not { } bodyProto)
        {
            Log($"role `{ShopRuleSystem.DefaultRole}` is not registered; cannot body {player}");
            Refund(player, spent);
            return;
        }
        var claims = Resource<ClaimedSeats>();
        if (claims.Tick != _timing.CurTick)
        {
            claims.Tick = _timing.CurTick;
            claims.Seats = [];
        }
        var taken = new List<Vector2i>();
        var enemies = new List<Vector2i>();
        var mobs = EntityQueryEnumerator<TilePos, Mob>();
        while (mobs.MoveNext(out var mob, out var at, out _))
        {
            if (at.Map != map || _ghosts.HasComp(mob))
            {
                continue;
            }
            taken.Add(at.Pos);
            if (!_dead.HasComp(mob))
            {
                enemies.Add(at.Pos);
            }
        }
        taken.AddRange(claims.Seats.Where(s => s.Map == map).Select(s => s.Pos));
        if (RespawnSeat(map, taken, enemies, Seed.Mix(_rule.NextSalt(), player.Value)) is not { } pos)
        {
            Log($"nowhere to respawn {player}");
            Refund(player, spent);
            return;
        }
        claims.Seats.Add(new TilePos(map, pos));
        if (!TrySpawn(bodyProto, out var body))
        {
            Log($"`{bodyProto}` prototype missing; cannot body {player}");
            Refund(player, spent);
            return;
        }
        // Past every refund: the forced buy's debit is queued, and a refund
        // read before it landed would credit it back
        if (!products.Any(_rule.Arms))
        {
            ForceCheapestStaple(connection, products, player);
        }
        AddComp(body, new TilePos(map, pos));
        AddComp(body, new Role(ShopRuleSystem.DefaultRole));
        if (products.Count > 0)
        {
            AddComp(body, new PendingLoadoutComponent { Products = products });
        }
        var ghost = _rule.GhostOf(player);
        _minds.TransferControl(player, ghost, body);
        if (ghost is { } old)
        {
            QueueDel(old);
        }
    }

    bool HasBody(PlayerId player)
    {
        var controlled = EntityQueryEnumerator<ControlledBy>();
        while (controlled.MoveNext(out var body, out var by))
        {
            if (by.Value == player && !_ghosts.HasComp(body))
            {
                return true;
            }
        }
        return false;
    }

    // The backstop buys the cheapest thing that arms, staples first; what it
    // cannot afford it does not buy, and says so
    void ForceCheapestStaple(EntityUid connection, List<string> products, PlayerId player)
    {
        if (!TryComp<ShopOffer>(connection, out var offer))
        {
            return;
        }
        int? Cheapest(bool staplesOnly)
        {
            int? best = null;
            for (var i = 0; i < offer.Rows.Count; i++)
            {
                var row = offer.Rows[i];
                if (row.Sold || (staplesOnly && !row.Staple) || !_rule.Arms(row.Product))
                {
                    continue;
                }
                if (best is null || row.Price < offer.Rows[best.Value].Price)
                {
                    best = i;
                }
            }
            return best;
        }
        if ((Cheapest(true) ?? Cheapest(false)) is not { } index)
        {
            Log($"offer for {player} has no weapon; forcing a respawn without one");
            return;
        }
        if (!_wallets.TryFind(player, out var record))
        {
            return;
        }
        var wallet = record.Comp2;
        var row = offer.Rows[index];
        if (products.Count == 0 && wallet.Balance < row.Price)
        {
            Log($"backstop cannot afford `{row.Listing}`; the floor no longer covers the cheapest staple");
        }
        _shop.ExecutePurchase(connection, player, (uint)index);
        products.Add(row.Product);
    }

    Vector2i? RespawnSeat(EntityUid map, List<Vector2i> taken, List<Vector2i> enemies, ulong seed)
    {
        var seats = _seats.SeatsFor(map, ShopRuleSystem.DefaultRole);
        if (seats.Count == 0)
        {
            return null;
        }
        if (enemies.Count == 0)
        {
            return _seats.SpreadOver(seats, taken) ?? seats[0];
        }
        var free = seats.Where(pos => !taken.Contains(pos)).ToList();
        if (free.Count == 0)
        {
            return _seats.SpreadOver(seats, []) ?? seats[0];
        }
        var far = FarSeats(free, enemies);
        return far[(int)(seed % (ulong)far.Count)];
    }

    // The farthest free seats from any enemy, with slack so one wall is not the
    // only answer, padded out to a few choices when the map has them
    static List<Vector2i> FarSeats(List<Vector2i> free, List<Vector2i> enemies)
    {
        int Nearest(Vector2i pos) => enemies.Count == 0 ? 0 : enemies.Min(enemy => Chebyshev(pos, enemy));
        free = free.OrderByDescending(Nearest).ThenBy(pos => pos.X).ThenBy(pos => pos.Y).ToList();
        if (free.Count == 0)
        {
            return free;
        }
        var best = Nearest(free[0]);
        var reach = best - Math.Min(RespawnSlackTiles, best / 2);
        var nearBest = free.TakeWhile(pos => Nearest(pos) >= reach).Count();
        var safe = free.TakeWhile(pos => Nearest(pos) >= RespawnSafeTiles).Count();
        var keep = Math.Max(nearBest, Math.Min(safe, RespawnMinChoices));
        return free.Take(keep).ToList();
    }

    public static int Chebyshev(Vector2i a, Vector2i b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
}

// The products a fresh body is owed go into its free hands, weapons first
// and the active hand first; what no hand takes lands at its feet
public sealed partial class LoadoutSystem : EntitySystem
{
    [Dependency] GameTicker _ticker = default!;
    [Dependency] ShopRuleSystem _rule = default!;
    [Dependency] EntityQuery<Gripping> _gripping = default!;

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(ShopRuleSystem));
        Every(TimeSpan.Zero, DeliverLoadouts);
    }

    void DeliverLoadouts()
    {
        if (!Rules.RoundRunning(_ticker) || !_ticker.IsGameRuleActive<ShopRuleComponent>())
        {
            return;
        }
        var stored = new HashSet<EntityUid>();
        var containers = EntityQueryEnumerator<Stored>();
        while (containers.MoveNext(out _, out var item))
        {
            stored.Add(item.Container);
        }
        var slots = new List<(EntityUid slot, HandSlot hand)>();
        var hands = EntityQueryEnumerator<HandSlot>();
        while (hands.MoveNext(out var slot, out var hand))
        {
            if (!_gripping.HasComp(slot))
            {
                slots.Add((slot, hand));
            }
        }
        var owed = EntityQueryEnumerator<PendingLoadoutComponent>();
        while (owed.MoveNext(out var body, out var loadout))
        {
            if (!TryComp<TilePos>(body, out var at))
            {
                continue;
            }
            var active = CompOrNull<Hands>(body)?.Active ?? Hand.Left;
            var free = slots.Where(row => row.hand.Owner == body && !stored.Contains(row.slot)).OrderBy(row => row.hand.Side != active).Select(row => row.slot).ToList();
            if (free.Count == 0)
            {
                continue;
            }
            var next = 0;
            foreach (var product in loadout.Products.OrderBy(product => !_rule.Arms(product)))
            {
                if (!TrySpawn(product, out var item))
                {
                    Log($"product `{product}` is not a prototype; the buyer got nothing");
                    continue;
                }
                if (next < free.Count)
                {
                    AddComp(item, new Stored(free[next++]));
                }
                else
                {
                    AddComp(item, at);
                }
            }
            RemComp<PendingLoadoutComponent>(body);
        }
    }
}

// A parked ghost whose shelf is gone for good, after the round or when the
// player left the match, flies again. Every phase, since the stranded cases
// are exactly the ones a round gate would skip
public sealed partial class StrandedGhostSystem : EntitySystem
{
    [Dependency] GameTicker _ticker = default!;
    [Dependency] EntityQuery<Movable> _movable = default!;

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(DeathCamSystem));
        Every(TimeSpan.Zero, UnparkStranded);
    }

    void UnparkStranded()
    {
        var shopRuns = _ticker.IsGameRuleActive<ShopRuleComponent>();
        var participants = new HashSet<PlayerId>();
        var sessions = EntityQueryEnumerator<SessionPlayer, Participant>();
        while (sessions.MoveNext(out _, out var session, out _))
        {
            participants.Add(session.Value);
        }
        var parked = new List<EntityUid>();
        var ghosts = EntityQueryEnumerator<ControlledBy, Ghost>();
        while (ghosts.MoveNext(out var ghost, out var controlled, out _))
        {
            if (_movable.HasComp(ghost) || (shopRuns && participants.Contains(controlled.Value)))
            {
                continue;
            }
            parked.Add(ghost);
        }
        foreach (var ghost in parked)
        {
            AddComp(ghost, new Movable());
        }
    }
}

static class Rules
{
    // Not IsRoundLive, which skips RoundStarting and covers PostRound
    public static bool RoundRunning(GameTicker ticker) => ticker.RunLevel is RoundState.RoundStarting or RoundState.InRound;
}
