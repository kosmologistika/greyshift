using System.Text.Json.Serialization;
using Rs13.Mods;
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
partial class ModJson : JsonSerializerContext;

static class Durations
{
    // A WIT duration from a span since the epoch, what a Deadline holds
    public static Duration From(TimeSpan span) =>
        new((ulong)Math.Floor(span.TotalSeconds), (uint)((span.Ticks % TimeSpan.TicksPerSecond) * 100));
}

static class BloodMoney
{
    public static void Setup(ModApp app)
    {
        BestOf.Setup(app);
        CoinTarget.Setup(app);
        Economy.Setup(app);
    }
}
