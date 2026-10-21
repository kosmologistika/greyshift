using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rs13.Mods;

/// <summary>
/// An entity as the host names it, the bits every own value and slot carries.
/// Bits 0 are never an entity, so <c>default</c> is a safe none
/// </summary>
[JsonConverter(typeof(EntityUidJson))]
public readonly record struct EntityUid(ulong Bits)
{
    public static readonly EntityUid Invalid = default;

    public bool Valid => Bits != 0;

    public override string ToString() => Valid ? $"entity {Bits}" : "no entity";
}

/// <summary>An entity is its bits in JSON, as a value and as a dictionary key</summary>
public sealed class EntityUidJson : JsonConverter<EntityUid>
{
    public override EntityUid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetUInt64());

    public override void Write(Utf8JsonWriter writer, EntityUid value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Bits);

    public override EntityUid ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(ulong.Parse(reader.GetString()!, CultureInfo.InvariantCulture));

    public override void WriteAsPropertyName(Utf8JsonWriter writer, EntityUid value, JsonSerializerOptions options) =>
        writer.WritePropertyName(value.Bits.ToString(CultureInfo.InvariantCulture));
}
