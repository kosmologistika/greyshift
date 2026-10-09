using System.Text.Json.Serialization;
using Rs13.Mods;

// Blood Money: balance is the score. Hits knock coins out of the victim onto
// the floor, anyone walks over them, and buying your way out of the death
// shop is the only way back. Design: docs/design/blood_money.md

[assembly: Mod(typeof(BloodMoney))]

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, UseStringEnumConverter = true)]
[JsonSerializable(typeof(Series))]
[JsonSerializable(typeof(Race))]
partial class ModJson : JsonSerializerContext;

static class BloodMoney
{
    public static void Setup(ModApp app)
    {
        BestOf.Setup(app);
        CoinTarget.Setup(app);
    }
}
