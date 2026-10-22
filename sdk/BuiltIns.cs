using Wit = TypedModuleWorld.wit.Imports.rs13.mods.v0_1_0.ITypedEcsImports;

namespace Rs13.Packs;

/// <summary>A service of the SDK a <c>[Dependency]</c> field may hold, filled at setup like a system</summary>
public interface IBuiltIn;

/// <summary>The round's phase, the engine's own names</summary>
public enum RoundState
{
    PreRoundLobby,
    RoundStarting,
    InRound,
    PostRound,
}

/// <summary>The server clock and frame count, valid in a handler</summary>
public interface IGameTiming : IBuiltIn
{
    /// <summary>The server clock, since the UNIX epoch</summary>
    TimeSpan CurTime { get; }

    /// <summary>The server's frame count, for a gate that fires once per tick</summary>
    ulong CurTick { get; }
}

sealed class GameTiming : IGameTiming
{
    public TimeSpan CurTime => ModRuntime.Now();

    public ulong CurTick => ModRuntime.Tick();
}

/// <summary>The round and its rules, valid in a handler</summary>
public sealed class GameTicker : IBuiltIn
{
    public RoundState RunLevel => ModRuntime.RoundState();

    /// <summary>A round is being played: what a per-tick rule gates on</summary>
    public bool IsRoundLive => ModRuntime.RoundLive();

    /// <summary>A rule of the type sits under a scope that has not ended</summary>
    public bool IsGameRuleActive<T>() where T : class, IOwnComponent
    {
        foreach (var _ in ModRuntime.ActiveRules<T>())
        {
            return true;
        }
        return false;
    }
}

/// <summary>A prototype id: what <c>Spawn</c> lays on a fresh entity</summary>
public readonly record struct EntProtoId(string Id)
{
    public static implicit operator EntProtoId(string id) => new(id);

    public override string ToString() => Id;
}

/// <summary>What the prototypes declare, valid in a handler</summary>
public interface IPrototypeManager : IBuiltIn
{
    string? Name(EntProtoId id);

    RsiRef? Sprite(EntProtoId id);

    /// <summary>Whether the prototype carries a component, by the short name a prototype file uses</summary>
    bool Declares(EntProtoId id, string component);

    /// <summary>The body prototype a role spawns</summary>
    EntProtoId? RoleBody(string role);
}

sealed class PrototypeManager : IPrototypeManager
{
    public string? Name(EntProtoId id)
    {
        ModRuntime.InCall();
        return Wit.PrototypeName(id.Id);
    }

    public RsiRef? Sprite(EntProtoId id)
    {
        ModRuntime.InCall();
        return Wit.PrototypeSprite(id.Id) is { } sprite ? EngineWit.From(sprite) : null;
    }

    public bool Declares(EntProtoId id, string component)
    {
        ModRuntime.InCall();
        return Wit.PrototypeDeclares(id.Id, component);
    }

    public EntProtoId? RoleBody(string role)
    {
        ModRuntime.InCall();
        return Wit.RoleBody(role) is { } body ? new EntProtoId(body) : default(EntProtoId?);
    }
}

/// <summary>One listing of a shop catalog</summary>
public readonly record struct ShopListing(string Id, EntProtoId Product, uint Price, uint Weight, bool Staple, string Shelf);

/// <summary>The engine's shop: the same money and <see cref="Purchased"/> a client's own buy moves</summary>
public sealed class ShopSystem : IBuiltIn
{
    /// <summary>The listings of a catalog, by id</summary>
    public List<ShopListing> Listings(string catalog)
    {
        ModRuntime.InCall();
        return Wit.ShopListings(catalog).Select(l => new ShopListing(l.id, l.product, l.price, l.weight, l.staple, l.shelf)).ToList();
    }

    /// <summary>Buys the offer's row at the index for the player on the connection. The wallet the handler changed is written first</summary>
    public void ExecutePurchase(EntityUid connection, PlayerId player, uint index)
    {
        ModRuntime.Writing();
        Wit.ExecutePurchase(connection.Bits, player.Value, index);
    }

    /// <summary>Closes a player's shop: <see cref="ShopDone"/>, then the offer comes off the connection</summary>
    public void CloseOffer(EntityUid connection, PlayerId player)
    {
        ModRuntime.Writing();
        Wit.CloseOffer(connection.Bits, player.Value);
    }
}

/// <summary>Who a player controls</summary>
public sealed class MindSystem : IBuiltIn
{
    /// <summary>Moves a player's control on to the target and off the origin; either may be null</summary>
    public void TransferControl(PlayerId player, EntityUid? origin, EntityUid? target)
    {
        ModRuntime.Writing();
        Wit.TransferControl(player.Value, origin?.Bits, target?.Bits);
    }
}

public sealed class GhostSystem : IBuiltIn
{
    /// <summary>A ghost for a player at a tile, the engine's own spawn, controlled by that player</summary>
    public void SpawnGhost(TilePos at, PlayerId player)
    {
        ModRuntime.Writing();
        Wit.SpawnGhost(EngineWit.To(at), player.Value);
    }
}

/// <summary>Where a body lands</summary>
public sealed class SeatSystem : IBuiltIn
{
    /// <summary>Where a body of the role may land on the map: its declared seats, or every open floor tile</summary>
    public List<Vector2i> SeatsFor(EntityUid map, string role)
    {
        ModRuntime.InCall();
        return Wit.SeatsFor(map.Bits, role).Select(seat => EngineWit.From(seat)).ToList();
    }

    public Vector2i? ObserverSeat(EntityUid map)
    {
        ModRuntime.InCall();
        return Wit.ObserverSeat(map.Bits) is { } seat ? EngineWit.From(seat) : null;
    }

    /// <summary>A seat among the candidates spread away from the taken ones, the engine's rule</summary>
    public Vector2i? SpreadOver(IEnumerable<Vector2i> candidates, IEnumerable<Vector2i> taken)
    {
        ModRuntime.InCall();
        return Wit.SpreadOver(candidates.Select(c => EngineWit.To(c)).ToList(), taken.Select(t => EngineWit.To(t)).ToList()) is { } seat ? EngineWit.From(seat) : null;
    }
}

public sealed class FlightSystem : IBuiltIn
{
    /// <summary>Puts an entity into flight from the origin along the angle (radians) for the range in tiles, the way a gun or a throw does</summary>
    public void LaunchRay(EntityUid entity, TilePos origin, float angle, float range, float speed, EntityUid launcher, FlightRules rules)
    {
        ModRuntime.Writing();
        Wit.LaunchRay(entity.Bits, EngineWit.To(origin), angle, range, speed, launcher.Bits, EngineWit.To(rules));
    }
}

public sealed class AudioSystem : IBuiltIn
{
    /// <summary>A sound at a tile, by its asset path</summary>
    public void Play(string path, TilePos at)
    {
        ModRuntime.Writing();
        Wit.PlaySound(path, EngineWit.To(at));
    }
}

public sealed class TransformSystem : IBuiltIn
{
    /// <summary>The tile under an entity however deep it is stored, null when it is nowhere</summary>
    public TilePos? GridPosition(EntityUid entity)
    {
        ModRuntime.InCall();
        return Wit.GridPosition(entity.Bits) is { } at ? EngineWit.From(at) : null;
    }
}

/// <summary>Seeded rolls with no rand: the same inputs give the same roll</summary>
public static class Seed
{
    public static ulong Mix(ulong a, ulong b)
    {
        var x = a * 0x9E3779B97F4A7C15UL ^ (b + 0x632BE59BD9B4E019UL);
        x ^= x >> 30;
        x *= 0xBF58476D1CE4E5B9UL;
        x ^= x >> 27;
        x *= 0x94D049BB133111EBUL;
        return x ^ (x >> 31);
    }

    /// <summary>The seed as a number in [0, 1]</summary>
    public static float Unit(ulong seed) => (seed % 1024) / 1023f;

    /// <summary>The seed as a number in [-1, 1]</summary>
    public static float UnitSigned(ulong seed) => Unit(seed) * 2f - 1f;
}

static class BuiltIns
{
    /// <summary>Every built-in by the type a <c>[Dependency]</c> names it as</summary>
    public static IEnumerable<(Type type, object instance)> All()
    {
        yield return (typeof(IGameTiming), new GameTiming());
        yield return (typeof(GameTicker), new GameTicker());
        yield return (typeof(IPrototypeManager), new PrototypeManager());
        yield return (typeof(ShopSystem), new ShopSystem());
        yield return (typeof(MindSystem), new MindSystem());
        yield return (typeof(GhostSystem), new GhostSystem());
        yield return (typeof(SeatSystem), new SeatSystem());
        yield return (typeof(FlightSystem), new FlightSystem());
        yield return (typeof(AudioSystem), new AudioSystem());
        yield return (typeof(TransformSystem), new TransformSystem());
    }
}
