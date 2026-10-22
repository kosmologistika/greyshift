using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Rs13.Packs;

enum OwnKind
{
    Component,
    Resource,
    Message,
    Mirror,
}

/// <summary>
/// What <c>ModEntry.Build</c> registered for one of the module's types: its
/// path and its metadata from the context the props wrote, so no reflection
/// runs in the trimmed module
/// </summary>
static class OwnType<T> where T : class
{
    static (string path, JsonTypeInfo<T> info, OwnKind kind, bool networked)? filled;

    internal static void Fill(string path, JsonSerializerContext? context, OwnKind kind, bool networked)
    {
        var info = context?.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>
            ?? throw new InvalidOperationException($"{typeof(T).Name} is in no JSON context: the pre-compile scan in Rs13.Packs.props missed its attribute");
        filled = (path, info, kind, networked);
    }

    public static string Path => Entry.path;

    public static OwnKind Kind => Entry.kind;

    public static bool Networked => Entry.networked;

    public static T Read(byte[] bytes) =>
        JsonSerializer.Deserialize(bytes, Entry.info) ?? throw new InvalidOperationException($"{typeof(T).Name} read as null");

    public static byte[] Write(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Entry.info);

    static (string path, JsonTypeInfo<T> info, OwnKind kind, bool networked) Entry =>
        filled ?? throw new InvalidOperationException($"{typeof(T).Name} is not one of the module's types: mark it [RegisterComponent], [RegisterResource], [NetSerializable] or [MirrorComponent]");
}
