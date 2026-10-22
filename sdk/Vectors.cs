using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rs13.Packs;

/// <summary>A tile offset or position, the engine's <c>IVec2</c></summary>
[JsonConverter(typeof(Vector2iJson))]
public readonly record struct Vector2i(int X, int Y)
{
    public static readonly Vector2i Zero = default;

    public static Vector2i operator +(Vector2i a, Vector2i b) => new(a.X + b.X, a.Y + b.Y);

    public static Vector2i operator -(Vector2i a, Vector2i b) => new(a.X - b.X, a.Y - b.Y);

    public override string ToString() => $"({X}, {Y})";
}

/// <summary>Serde writes a glam vector as the array <c>[x, y]</c></summary>
public sealed class Vector2iJson : JsonConverter<Vector2i>
{
    public override Vector2i Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var (x, y) = VectorJson.ReadPair(ref reader, (ref Utf8JsonReader r) => r.GetInt32());
        return new(x, y);
    }

    public override void Write(Utf8JsonWriter writer, Vector2i value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteEndArray();
    }
}

/// <summary>The same array shape for the engine's <c>Vec2</c>, which is <see cref="Vector2"/> on this side</summary>
public sealed class Vector2Json : JsonConverter<Vector2>
{
    public override Vector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var (x, y) = VectorJson.ReadPair(ref reader, (ref Utf8JsonReader r) => r.GetSingle());
        return new(x, y);
    }

    public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteEndArray();
    }
}

static class VectorJson
{
    public delegate T Number<T>(ref Utf8JsonReader reader);

    public static (T, T) ReadPair<T>(ref Utf8JsonReader reader, Number<T> number)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("a vector is [x, y]");
        }
        var x = Next(ref reader, number);
        var y = Next(ref reader, number);
        if (!reader.Read() || reader.TokenType != JsonTokenType.EndArray)
        {
            throw new JsonException("a vector is [x, y], with nothing after y");
        }
        return (x, y);
    }

    static T Next<T>(ref Utf8JsonReader reader, Number<T> number)
    {
        if (!reader.Read() || reader.TokenType != JsonTokenType.Number)
        {
            throw new JsonException("a vector is [x, y], two numbers");
        }
        return number(ref reader);
    }
}
