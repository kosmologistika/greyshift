using System.Text.Json.Serialization;
using Rs13.Legacy;
using static TypedModuleWorld.wit.Imports.rs13.mods.v0_1_0.ITypedEcsImports;

// Blood Money: balance is the score. Hits knock coins out of the victim onto
// the floor, anyone walks over them, and buying your way out of the death
// shop is the only way back. Design: docs/design/blood_money.md

[assembly: Mod(typeof(BloodMoney), typeof(ModJson))]

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, UseStringEnumConverter = true)]
[JsonSerializable(typeof(Series))]
[JsonSerializable(typeof(Race))]
[JsonSerializable(typeof(PendingSpills))]
[JsonSerializable(typeof(CoinSoundGate))]
[JsonSerializable(typeof(PileDecay))]
[JsonSerializable(typeof(SpilledBy))]
[JsonSerializable(typeof(Salt))]
[JsonSerializable(typeof(DripAnchor))]
[JsonSerializable(typeof(Staked))]
[JsonSerializable(typeof(Cart))]
[JsonSerializable(typeof(PendingLoadout))]
[JsonSerializable(typeof(KilledBy))]
[JsonSerializable(typeof(CamSettled))]
[JsonSerializable(typeof(ClaimedSeats))]
[JsonSerializable(typeof(ShopSalt))]
[JsonSerializable(typeof(CatalogStyle))]
[JsonSerializable(typeof(BestOfRule))]
[JsonSerializable(typeof(CoinTargetRule))]
[JsonSerializable(typeof(CoinEconomyRule))]
[JsonSerializable(typeof(ShopRule))]
[JsonSerializable(typeof(CoinPile))]
[JsonSerializable(typeof(CoinHooks))]
[JsonSerializable(typeof(CoinBurst))]
partial class ModJson : JsonSerializerContext;

// The mode's prototype-named components, authored as `blood_money::X` in a
// prototype and read back as these

// Ends the round after `matches` matches with the most match wins taking it,
// total coins earned across the round breaking ties
class BestOfRule
{
    public uint Matches { get; set; }
}

// Ends its match when a wallet reaches `target`, or when `secs` run out with
// the richest wallet winning
class CoinTargetRule
{
    public uint Target { get; set; }
    public ulong Secs { get; set; }
}

class CoinEconomyRule
{
    public uint DripPerSec { get; set; }
    public uint DripBelow { get; set; }
    public uint Crumb { get; set; }
    public uint Bounty { get; set; }
    public uint PileValue { get; set; }
    public ulong CoinLifeSecs { get; set; }
}

class ShopRule
{
    public uint Start { get; set; }
    public uint Floor { get; set; }
    public uint Rolled { get; set; }
    public ulong LockSecs { get; set; }
    public ulong BackstopSecs { get; set; }
}

// A handful of coins on the floor; replicated, so a client sees what a pile is worth
class CoinPile
{
    public uint Value { get; set; }
}

// On a weapon: what its hits do to the money. Knockout scales the coins a
// hit shakes out of the victim, Refund is the share the attacker pockets
class CoinHooks
{
    public float Knockout { get; set; } = 1f;
    public float Refund { get; set; }
}

// A payload: when the item is triggered it showers `value` in coin piles
class CoinBurst
{
    public uint Value { get; set; }
}

static class Durations
{
    // A WIT duration from a span since the epoch, what a Deadline holds
    public static Duration From(TimeSpan span) =>
        new((ulong)Math.Floor(span.TotalSeconds), (uint)((span.Ticks % TimeSpan.TicksPerSecond) * 100));

    public static ulong Ms(Duration duration) => duration.secs * 1000 + duration.nanos / 1_000_000;
}

static class BloodMoney
{
    public static void Setup(ModApp app)
    {
        app.RegisterComponent<BestOfRule>();
        app.RegisterComponent<CoinTargetRule>();
        app.RegisterComponent<CoinEconomyRule>();
        app.RegisterComponent<ShopRule>();
        app.RegisterComponent<CoinPile>(replicated: true);
        app.RegisterComponent<CoinHooks>();
        app.RegisterComponent<CoinBurst>();
        BestOf.Setup(app);
        CoinTarget.Setup(app);
        Economy.Setup(app);
        Shop.Setup(app);
    }
}
