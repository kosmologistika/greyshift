using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rs13.Mods;

/// <summary>
/// A <see cref="TimeSpan"/> in an own value is serde's <c>Duration</c> on the
/// wire, <c>{"secs":1,"nanos":500000000}</c>, so a prototype and the host read
/// it the same way. Durations are unsigned there, so a negative span throws
/// </summary>
public sealed class SerdeDurationJson : JsonConverter<TimeSpan>
{
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("a duration is an object with secs and nanos");
        }
        ulong secs = 0;
        uint nanos = 0;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var key = reader.GetString();
            reader.Read();
            switch (key)
            {
                case "secs":
                    secs = reader.GetUInt64();
                    break;
                case "nanos":
                    nanos = reader.GetUInt32();
                    break;
                default:
                    throw new JsonException($"a duration has secs and nanos, not '{key}'");
            }
        }
        if (nanos >= SerdeDuration.NanosPerSecond)
        {
            throw new JsonException($"a duration's nanos are under a second, not {nanos}");
        }
        return SerdeDuration.From(secs, nanos);
    }

    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
    {
        if (value < TimeSpan.Zero)
        {
            throw new JsonException($"a duration cannot be negative: {value}");
        }
        var (secs, nanos) = SerdeDuration.To(value);
        writer.WriteStartObject();
        writer.WriteNumber("secs", secs);
        writer.WriteNumber("nanos", nanos);
        writer.WriteEndObject();
    }
}

/// <summary>
/// serde's <c>Duration</c> and a <see cref="TimeSpan"/>. The engine's
/// <c>Deadline::never()</c> is <c>Duration::MAX</c>, far past what a TimeSpan
/// holds, so anything past the range saturates to <see cref="TimeSpan.MaxValue"/>
/// and that one value goes back as the maximum
/// </summary>
public static class SerdeDuration
{
    public const uint NanosPerSecond = 1_000_000_000;
    const long NanosPerTick = 100;
    static readonly ulong MaxSecs = (ulong)(TimeSpan.MaxValue.Ticks / TimeSpan.TicksPerSecond);

    public static TimeSpan From(ulong secs, uint nanos) =>
        secs >= MaxSecs ? TimeSpan.MaxValue : TimeSpan.FromTicks((long)secs * TimeSpan.TicksPerSecond + nanos / NanosPerTick);

    public static (ulong secs, uint nanos) To(TimeSpan span)
    {
        if (span < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(span), "a duration is never negative");
        }
        return span == TimeSpan.MaxValue
            ? (ulong.MaxValue, NanosPerSecond - 1)
            : ((ulong)(span.Ticks / TimeSpan.TicksPerSecond), (uint)(span.Ticks % TimeSpan.TicksPerSecond * NanosPerTick));
    }
}
