using Rs13.Mods;
using static TypedModuleWorld.wit.Imports.rs13.mods.v0_1_0.ITypedEcsImports;

// This record's wallet was staked for the current match. Stamped on every
// record at match open; a record without it joined late and gets the
// starting stake, not just the death floor
class Staked
{
}

// What this shopper bought, billed at once and owed as one loadout at the
// close; Closed marks delivery, anything else that drops the shelf refunds
class Cart
{
    public ulong Player { get; set; }
    public List<string> Products { get; set; } = [];
    public uint Spent { get; set; }
    public bool Closed { get; set; }
}

// The products a fresh body is owed, delivered once its hand slots exist:
// they spawn a tick after the mob
class PendingLoadout
{
    public List<string> Products { get; set; } = [];
}

// On the victim's record: who to point their death cam at. Consumed by the
// first ghost the player controls
class KilledBy
{
    public ulong Player { get; set; }
}

// The death cam was settled for this ghost; a player who then walks off it
// chose to, and is not re-attached
class CamSettled
{
}

// Seats handed out by BodyTheBuyer this tick: a second close in the same
// tick cannot see the first one's body yet, and without this every
// backstop-bought player lands on one tile
class ClaimedSeats
{
    public ulong Tick { get; set; }
    public List<Seat> Seats { get; set; } = [];
}

class Seat
{
    public ulong Map { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
}

class ShopSalt
{
    public ulong Value { get; set; }
}

// This mode's shelf dressing, sent beside the shop seam's own update: every
// listing in the catalog with its tier and art, so the client can spin decoys
// and tint landings. Covering the whole catalog leaks nothing about what rolled
class CatalogStyle
{
    public List<CatalogEntry> Entries { get; set; } = [];
}

class CatalogEntry
{
    public string Listing { get; set; } = "";
    public Rarity Rarity { get; set; }
    public CatalogSprite? Sprite { get; set; }
}

class CatalogSprite
{
    public string Rsi { get; set; } = "";
    public string State { get; set; } = "";
}

// How loudly the reveal celebrates a listing. Derived from the draw weight,
// never authored: a thing is gold exactly because it barely rolls
enum Rarity
{
    Common,
    Uncommon,
    Rare,
}

// The shop: every bodiless participant of the match is put into it, with a
// ghost to watch through, a floor-topped wallet and a rolled shelf, and the
// body they pay for spawns at the close. Buying is the only way back
static class Shop
{
    const string Catalog = "blood_money";
    const string DefaultRole = "role_default";
    // Tiles nearer the fight than the farthest free seat a respawn may land,
    // so it is not always the one wall a camper pre-aims
    const int RespawnSlackTiles = 3;
    // Seats a respawn chooses among when the map has that many to spare
    const int RespawnMinChoices = 3;
    // How far from every living player a seat must be to pad the choices out
    const int RespawnSafeTiles = 3;

    public static void Setup(ModApp app)
    {
        app.RegisterComponent<Staked>();
        app.RegisterComponent<Cart>();
        app.RegisterComponent<PendingLoadout>();
        app.RegisterComponent<KilledBy>();
        app.RegisterComponent<CamSettled>();
        app.RegisterResource<ClaimedSeats>();
        app.RegisterResource<ShopSalt>();
        app.RegisterEvent<CatalogStyle>();
        app.Observe<RuleStarted>(ResetWalletsOnStart);
        app.Observe<ScopeEnding>(CloseShopsWithTheMatch);
        app.Observe<Killed>(RememberKiller);
        app.Observe<Purchased>(StockCart);
        app.ObserveRemoved<ShopOffer>(RefundDroppedCart);
        app.Observe<ShopDone>(BodyTheBuyer);
        app.Every(TimeSpan.Zero, OpenShops);
        app.Every(TimeSpan.Zero, RunBackstops);
        app.Every(TimeSpan.Zero, DeliverLoadouts);
        app.Every(TimeSpan.Zero, AttachDeathCam);
        app.Every(TimeSpan.Zero, UnparkStranded);
    }

    // The live shop rule and its scope, none once the scope has ended
    static (ShopRule rule, ulong scope)? Rule(World world)
    {
        foreach (var (_, rule, childOf) in world.Query<ShopRule, ChildOf>())
        {
            if (world.Get<ScopeEnded>(childOf.Value) is null)
            {
                return (rule, childOf.Value);
            }
        }
        return null;
    }

    // The map a scope owns, once its map rule has loaded one
    static ulong? ScopeMap(World world, ulong scope)
    {
        foreach (var (map, _, childOf) in world.Query<Map, ChildOf>())
        {
            if (childOf.Value == scope)
            {
                return map;
            }
        }
        return null;
    }

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

    static ulong? RecordOf(World world, ulong player) => WalletOf(world, player)?.entity;

    static ulong? BodyOf(World world, ulong player)
    {
        foreach (var (entity, controlled) in world.Query<ControlledBy>())
        {
            if (controlled.Value == player)
            {
                return entity;
            }
        }
        return null;
    }

    static ulong? GhostOf(World world, ulong player)
    {
        foreach (var (entity, controlled, _) in world.Query<ControlledBy, Ghost>())
        {
            if (controlled.Value == player)
            {
                return entity;
            }
        }
        return null;
    }

    static ulong NextSalt(World world)
    {
        var salt = (world.GetResource<ShopSalt>()?.Value ?? 0) + 1;
        world.InsertResource(new ShopSalt { Value = salt });
        return salt;
    }

    // Every wallet opens the match at the same number, and no offer survives
    // from the previous match. Earned is left alone: it is the round's
    // tiebreak and outlives the match on purpose
    static void ResetWalletsOnStart(RuleStarted on, World world)
    {
        if (world.Get<ShopRule>(on.entity) is not { } rule)
        {
            return;
        }
        // The stake below supersedes any cart; close them first or the drop
        // refunds on top of it
        foreach (var (connection, _) in world.Query<ShopOffer>())
        {
            if (world.GetOwn<Cart>(connection) is { } cart)
            {
                cart.Closed = true;
                world.InsertOwn(connection, cart);
            }
        }
        foreach (var (record, _, wallet) in world.Query<PlayerRecord, Wallet>())
        {
            world.Mutate(record, new Wallet(rule.start, wallet.earned));
            // A fresh match also forgets who killed whom, or a stale stamp points
            // next match's first death cam at last match's killer
            world.InsertOwn(record, new Staked());
            world.RemoveOwn<KilledBy>(record);
        }
        foreach (var (connection, _) in world.Query<ShopOffer>())
        {
            world.Remove<ShopOffer>(connection);
            world.RemoveOwn<Cart>(connection);
        }
    }

    // A match that ends mid-shop takes its offers down with it: a stale shelf
    // would buy a body into the wrong match. The shelf is what parks a ghost,
    // so each shopper's ghost gets its wings back; the shelf drop refunds the cart
    static void CloseShopsWithTheMatch(ScopeEnding on, World world)
    {
        if (!world.Query<ShopRule, ChildOf>().Any(row => row.b.Value == on.entity))
        {
            return;
        }
        foreach (var (connection, _, session) in world.Query<ShopOffer, SessionPlayer>())
        {
            world.Remove<ShopOffer>(connection);
            if (GhostOf(world, session.Value) is { } ghost)
            {
                world.Insert(ghost, new Movable());
            }
        }
    }

    // Puts every bodiless participant of the rule's match into the shop. Covers
    // the match opening (nobody has a body) and every death (the eject leaves
    // the player holding a ghost) through the same path
    static void OpenShops(World world)
    {
        if (!world.RoundRunning || Rule(world) is not var (rule, scope) || ScopeMap(world, scope) is not { } map)
        {
            return;
        }
        CatalogStyle? style = null;
        foreach (var (connection, session, _) in world.Query<SessionPlayer, Participant>())
        {
            var player = session.Value;
            var body = BodyOf(world, player);
            if (body is null)
            {
                var pos = world.ObserverSeat(map) ?? new IVec2(0, 0);
                world.SpawnGhost(new TilePos(map, pos), player);
                continue;
            }
            if (world.Get<Ghost>(body.Value) is null || world.Get<ShopOffer>(connection) is not null)
            {
                continue;
            }
            if (WalletOf(world, player) is not var (record, wallet))
            {
                continue;
            }
            var balance = wallet.balance;
            // A record without this match's stake joined late: they enter on the
            // starting wallet, not just the death floor
            if (world.GetOwn<Staked>(record) is null)
            {
                balance = Math.Max(balance, rule.start);
                world.InsertOwn(record, new Staked());
            }
            balance = Math.Max(balance, rule.floor);
            world.Mutate(record, new Wallet(balance, wallet.earned));
            var offer = RollOffer(world, rule, Mix(NextSalt(world), player));
            if (offer.rows.Count == 0)
            {
                world.Log($"the `{Catalog}` catalog has no listings; shoppers will be force-respawned bare-handed");
            }
            world.Insert(connection, offer);
            world.InsertOwn(connection, new Cart { Player = player });
            // The seam's update carries the shelf; the dressing that makes it a
            // slot machine is this mode's own wire
            style ??= Style(world);
            world.SendToClient(connection, style);
            // A shopping ghost is parked on its death cam: no free flight while
            // the shelf stands. Movable comes back if the match closes the shop
            world.Remove<Movable>(body.Value);
        }
    }

    // Staples always in stock, plus rolled rarity-weighted draws without
    // replacement, all from this mode's catalog only. seed % total, never a
    // scaled float: the top of a 0..=1 range lands past the last slot
    static ShopOffer RollOffer(World world, ShopRule rule, ulong seed)
    {
        var listings = world.ShopListings(Catalog);
        var rows = listings.Where(l => l.staple).Select(l => RowFor(world, l)).ToList();
        rows.Sort((a, b) => a.price != b.price ? a.price.CompareTo(b.price) : string.CompareOrdinal(a.listing, b.listing));
        var pool = listings.Where(l => !l.staple && l.weight > 0).OrderBy(l => l.id, StringComparer.Ordinal).ToList();
        for (uint n = 0; n < rule.rolled; n++)
        {
            var total = pool.Aggregate(0UL, (sum, l) => sum + l.weight);
            if (total == 0)
            {
                break;
            }
            var roll = Mix(seed, n) % total;
            var index = -1;
            for (var i = 0; i < pool.Count; i++)
            {
                if (roll < pool[i].weight)
                {
                    index = i;
                    break;
                }
                roll -= pool[i].weight;
            }
            if (index < 0)
            {
                break;
            }
            rows.Add(RowFor(world, pool[index]));
            pool.RemoveAt(index);
        }
        var now = world.Now;
        return new ShopOffer(
            rows,
            new Deadline(Durations.From(now + TimeSpan.FromSeconds(rule.lockSecs))),
            new Deadline(Durations.From(now + TimeSpan.FromSeconds(rule.backstopSecs))));
    }

    static OfferRow RowFor(World world, ShopListing listing) =>
        new(listing.id, listing.product, world.PrototypeName(listing.product) ?? listing.product, world.PrototypeSprite(listing.product), listing.price, listing.staple, listing.shelf, false);

    // The whole catalog dressed for the reveal, staples included, so the spin
    // is a tour of everything the shelf could hold
    static CatalogStyle Style(World world)
    {
        var listings = world.ShopListings(Catalog);
        var heaviest = listings.Count == 0 ? 0 : listings.Max(l => l.weight);
        var entries = listings
            .Select(l => new CatalogEntry
            {
                Listing = l.id,
                Rarity = RarityOf(l, heaviest),
                Sprite = world.PrototypeSprite(l.product) is { } sprite ? new CatalogSprite { Rsi = sprite.rsi, State = sprite.state } : null,
            })
            .OrderBy(e => e.Listing, StringComparer.Ordinal)
            .ToList();
        return new CatalogStyle { Entries = entries };
    }

    // Tier bands relative to the pool's heaviest weight, because draw weights
    // only mean anything against each other. Staples (weight 0) are common:
    // always there, never celebrated
    static Rarity RarityOf(ShopListing listing, uint heaviest)
    {
        if (listing.weight == 0)
        {
            return Rarity.Common;
        }
        if (listing.weight * 8 <= heaviest)
        {
            return Rarity.Rare;
        }
        return listing.weight * 2 <= heaviest ? Rarity.Uncommon : Rarity.Common;
    }

    // An elapsed offer closes like a Done; this mode's policy, an uplink shelf
    // just stands
    static void RunBackstops(World world)
    {
        if (!world.RoundRunning)
        {
            return;
        }
        var nowMs = (ulong)world.Now.TotalMilliseconds;
        foreach (var (connection, offer, session) in world.Query<ShopOffer, SessionPlayer>())
        {
            if (offer.closesAt is { } closes && nowMs >= Durations.Ms(closes.endsAt))
            {
                world.CloseOffer(connection, session.Value);
            }
        }
    }

    // Every buy is owed the moment it is billed, so a slow shopper is still
    // armed when the deadline hits
    static void StockCart(Purchased on, World world)
    {
        if (world.GetOwn<Cart>(on.connection) is not { } cart)
        {
            return;
        }
        cart.Products.Add(on.product);
        cart.Spent += on.price;
        world.InsertOwn(on.connection, cart);
    }

    // A shelf that goes down with the cart undelivered, a disconnect or a match
    // ending mid-shop, gives the money back
    static void RefundDroppedCart(ulong connection, World world)
    {
        if (world.GetOwn<Cart>(connection) is not { } cart)
        {
            return;
        }
        if (!cart.Closed)
        {
            Refund(world, cart.Player, cart.Spent);
        }
        world.RemoveOwn<Cart>(connection);
    }

    static void Refund(World world, ulong player, uint spent)
    {
        if (spent == 0 || WalletOf(world, player) is not var (entity, wallet))
        {
            return;
        }
        world.Mutate(entity, new Wallet((uint)Math.Min((ulong)wallet.balance + spent, uint.MaxValue), wallet.earned));
    }

    // The close bodies the buyer with everything in the cart, the cheapest
    // staple forced in when nothing arms them, on a seat far from the fight
    static void BodyTheBuyer(ShopDone on, World world)
    {
        if (world.GetOwn<Cart>(on.connection) is not { } cart || cart.Closed)
        {
            return;
        }
        cart.Closed = true;
        world.InsertOwn(on.connection, cart);
        if (!cart.Products.Any(product => Arms(world, product)))
        {
            ForceCheapestStaple(world, cart, on);
        }
        var products = cart.Products;
        var spent = cart.Spent;
        world.RemoveOwn<Cart>(on.connection);
        if (world.Query<ControlledBy>().Any(row => row.a.Value == on.player && world.Get<Ghost>(row.entity) is null))
        {
            Refund(world, on.player, spent);
            return;
        }
        if (Rule(world) is not var (_, scope) || ScopeMap(world, scope) is not { } map)
        {
            world.Log($"{on.player} left the shop with no live match; they stay a ghost");
            Refund(world, on.player, spent);
            return;
        }
        if (world.RoleBody(DefaultRole) is not { } bodyProto)
        {
            world.Log($"role `{DefaultRole}` is not registered; cannot body {on.player}");
            Refund(world, on.player, spent);
            return;
        }
        var claims = world.GetResource<ClaimedSeats>() ?? new ClaimedSeats();
        if (claims.Tick != world.Tick)
        {
            claims = new ClaimedSeats { Tick = world.Tick };
        }
        var mobs = world.Query<TilePos, Mob>().Where(row => row.a.map == map).ToList();
        var taken = mobs.Where(row => world.Get<Ghost>(row.entity) is null).Select(row => row.a.pos).ToList();
        taken.AddRange(claims.Seats.Where(s => s.Map == map).Select(s => new IVec2(s.X, s.Y)));
        var enemies = mobs.Where(row => world.Get<Dead>(row.entity) is null && world.Get<Ghost>(row.entity) is null).Select(row => row.a.pos).ToList();
        if (RespawnSeat(world, map, taken, enemies, Mix(NextSalt(world), on.player)) is not { } pos)
        {
            world.Log($"nowhere to respawn {on.player}");
            Refund(world, on.player, spent);
            return;
        }
        claims.Seats.Add(new Seat { Map = map, X = pos.x, Y = pos.y });
        world.InsertResource(claims);
        if (world.SpawnPrototype(bodyProto) is not { } body)
        {
            world.Log($"`{bodyProto}` prototype missing; cannot body {on.player}");
            Refund(world, on.player, spent);
            return;
        }
        world.Insert(body, new TilePos(map, pos));
        world.Insert(body, new Role(DefaultRole));
        if (products.Count > 0)
        {
            world.InsertOwn(body, new PendingLoadout { Products = products });
        }
        var ghost = GhostOf(world, on.player);
        world.TransferControl(on.player, ghost, body);
        if (ghost is { } old)
        {
            world.Despawn(old);
        }
    }

    // The backstop buys the cheapest thing that arms, staples first; what it
    // cannot afford it does not buy, and says so
    static void ForceCheapestStaple(World world, Cart cart, ShopDone on)
    {
        if (world.Get<ShopOffer>(on.connection) is not { } offer)
        {
            return;
        }
        int? Cheapest(bool staplesOnly)
        {
            int? best = null;
            for (var i = 0; i < offer.rows.Count; i++)
            {
                var row = offer.rows[i];
                if (row.sold || (staplesOnly && !row.staple) || !Arms(world, row.product))
                {
                    continue;
                }
                if (best is null || row.price < offer.rows[best.Value].price)
                {
                    best = i;
                }
            }
            return best;
        }
        if ((Cheapest(true) ?? Cheapest(false)) is not { } index)
        {
            world.Log($"offer for {on.player} has no weapon; forcing a respawn without one");
            return;
        }
        if (WalletOf(world, on.player) is not var (_, wallet))
        {
            return;
        }
        var row = offer.rows[index];
        if (cart.Products.Count == 0 && wallet.balance < row.price)
        {
            world.Log($"backstop cannot afford `{row.listing}`; the floor no longer covers the cheapest staple");
        }
        world.ExecutePurchase(on.connection, on.player, (uint)index);
        cart.Products.Add(row.product);
        cart.Spent += Math.Min(wallet.balance, row.price);
    }

    static bool Arms(World world, string product) =>
        world.PrototypeDeclares(product, "MeleeWeapon") || world.PrototypeDeclares(product, "Gun");

    // A parked ghost whose shelf is gone for good, after the round or when the
    // player left the match, flies again. Every phase, since the stranded cases
    // are exactly the ones a round gate would skip
    static void UnparkStranded(World world)
    {
        var shopRuns = Rule(world) is not null;
        var participants = world.Query<SessionPlayer, Participant>().Select(row => row.a.Value).ToHashSet();
        foreach (var (ghost, controlled, _) in world.Query<ControlledBy, Ghost>())
        {
            if (world.Get<Movable>(ghost) is not null)
            {
                continue;
            }
            if (shopRuns && participants.Contains(controlled.Value))
            {
                continue;
            }
            world.Insert(ghost, new Movable());
        }
    }

    static IVec2? RespawnSeat(World world, ulong map, List<IVec2> taken, List<IVec2> enemies, ulong seed)
    {
        var seats = world.SeatsFor(map, DefaultRole);
        if (seats.Count == 0)
        {
            return null;
        }
        if (enemies.Count == 0)
        {
            return world.SpreadOver(seats, taken) ?? seats[0];
        }
        var free = seats.Where(pos => !taken.Any(t => t.x == pos.x && t.y == pos.y)).ToList();
        if (free.Count == 0)
        {
            return world.SpreadOver(seats, []) ?? seats[0];
        }
        var far = FarSeats(free, enemies);
        return far[(int)(seed % (ulong)far.Count)];
    }

    // The farthest free seats from any enemy, with slack so one wall is not the
    // only answer, padded out to a few choices when the map has them
    static List<IVec2> FarSeats(List<IVec2> free, List<IVec2> enemies)
    {
        int Nearest(IVec2 pos) => enemies.Count == 0 ? 0 : enemies.Min(enemy => Chebyshev(pos, enemy));
        free = free.OrderByDescending(Nearest).ThenBy(pos => pos.x).ThenBy(pos => pos.y).ToList();
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

    static int Chebyshev(IVec2 a, IVec2 b) => Math.Max(Math.Abs(a.x - b.x), Math.Abs(a.y - b.y));

    // The products a fresh body is owed go into its free hands, weapons first
    // and the active hand first; what no hand takes lands at its feet
    static void DeliverLoadouts(World world)
    {
        if (!world.RoundRunning)
        {
            return;
        }
        var stored = world.Query<Stored>().Select(row => row.a.container).ToHashSet();
        var slots = world.Query<HandSlot>().Where(row => world.Get<Gripping>(row.entity) is null).ToList();
        foreach (var (body, loadout) in world.QueryOwn<PendingLoadout>().ToList())
        {
            if (world.Get<TilePos>(body) is not { } at)
            {
                continue;
            }
            var active = world.Get<Hands>(body)?.active ?? Hand.LEFT;
            var free = slots.Where(row => row.a.owner == body && !stored.Contains(row.entity)).OrderBy(row => row.a.side != active).Select(row => row.entity).ToList();
            if (free.Count == 0)
            {
                continue;
            }
            var next = 0;
            foreach (var product in loadout.Products.OrderBy(product => !IsWeapon(world, product)))
            {
                if (world.SpawnPrototype(product) is not { } item)
                {
                    world.Log($"product `{product}` is not a prototype; the buyer got nothing");
                    continue;
                }
                if (next < free.Count)
                {
                    world.Insert(item, new Stored(free[next++]));
                }
                else
                {
                    world.Insert(item, at);
                }
            }
            world.RemoveOwn<PendingLoadout>(body);
        }
    }

    static bool IsWeapon(World world, string product) => Arms(world, product);

    static void RememberKiller(Killed on, World world)
    {
        if (on.killer is not { } killer || RecordOf(world, on.victim) is not { } record)
        {
            return;
        }
        world.InsertOwn(record, new KilledBy { Player = killer });
    }

    // A fresh ghost is pointed at its killer, or the nearest living body on its
    // map, and parked there; once settled it is never re-attached
    static void AttachDeathCam(World world)
    {
        if (!world.RoundRunning)
        {
            return;
        }
        var participants = world.Query<SessionPlayer, Participant>().Select(row => row.a.Value).ToHashSet();
        var standing = world.Query<ControlledBy, TilePos, Mob>().Where(row => world.Get<Dead>(row.entity) is null && world.Get<Ghost>(row.entity) is null).ToList();
        foreach (var (ghost, controlled, here, _) in world.Query<ControlledBy, TilePos, Ghost>().ToList())
        {
            var player = controlled.Value;
            if (!participants.Contains(player) || world.Get<Following>(ghost) is not null || world.GetOwn<CamSettled>(ghost) is not null)
            {
                continue;
            }
            var record = RecordOf(world, player);
            var killer = record is { } r ? world.GetOwn<KilledBy>(r)?.Player : null;
            ulong? target = killer is { } k ? standing.FirstOrDefault(row => row.a.Value == k).entity : null;
            if (target is null or 0)
            {
                var nearest = standing.Where(row => row.b.map == here.map).OrderBy(row => Chebyshev(row.b.pos, here.pos)).FirstOrDefault();
                target = nearest.entity == 0 ? null : nearest.entity;
            }
            if (target is not { } body)
            {
                continue;
            }
            world.Insert(ghost, new Following(body));
            world.Insert(ghost, new Facing(Dir.SOUTH));
            world.InsertOwn(ghost, new CamSettled());
            if (record is { } rec)
            {
                world.RemoveOwn<KilledBy>(rec);
            }
        }
    }

    static ulong Mix(ulong a, ulong b)
    {
        var x = a * 0x9E3779B97F4A7C15UL ^ (b + 0x632BE59BD9B4E019UL);
        x ^= x >> 30;
        x *= 0xBF58476D1CE4E5B9UL;
        x ^= x >> 27;
        x *= 0x94D049BB133111EBUL;
        return x ^ (x >> 31);
    }
}
