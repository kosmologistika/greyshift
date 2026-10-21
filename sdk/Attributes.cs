namespace Rs13.Mods;

/// <summary>
/// A component of the module's own, registered at build by the SDK generator
/// under <c>&lt;assembly&gt;::&lt;Name&gt;</c> with a trailing <c>Component</c> dropped:
/// <c>CoinPileComponent</c> is <c>blood_money::CoinPile</c>
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RegisterComponentAttribute : Attribute;

/// <summary>Beside <c>[RegisterComponent]</c>: the component rides a slot to every client that sees the entity</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class NetworkedComponentAttribute : Attribute;

/// <summary>A singleton of the module's own, serialized like a component</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RegisterResourceAttribute : Attribute;

/// <summary>An event of the module's own, JSON on the wire to and from clients</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class NetSerializableAttribute : Attribute;

/// <summary>
/// Another package's public type, mirrored by hand under the path its package
/// declared. Read-only, and read leniently: a key the mirror lacks is skipped
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class MirrorComponentAttribute(string path) : Attribute
{
    public string Path { get; } = path;
}

/// <summary>
/// Stored state of an own type. The key is the member's name in snake_case;
/// <c>[JsonPropertyName]</c> beside it picks another
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class DataFieldAttribute : Attribute;

/// <summary>
/// A field of an <see cref="EntitySystem"/> the generator fills before
/// <c>Initialize</c> runs: another of the module's systems. A plain field,
/// not readonly, as in Robust
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class DependencyAttribute : Attribute;
