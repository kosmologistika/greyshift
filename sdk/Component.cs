namespace Rs13.Mods;

/// <summary>
/// Base of a module's own components. Its state is what <c>[DataField]</c>
/// marks, snake_case JSON held host-side. The instance a read hands out is the
/// call's single one for that entity, and changing it is the write: the
/// runtime writes it back when the handler returns and before every write to
/// the host, so a read after it sees the change. A throw drops the change
/// </summary>
public abstract class Component;

/// <summary>An entity with one of its components read, as Robust hands them out</summary>
public readonly record struct Entity<T>(EntityUid Owner, T Comp)
{
    public static implicit operator EntityUid(Entity<T> entity) => entity.Owner;

    public static implicit operator Entity<T>((EntityUid Owner, T Comp) pair) => new(pair.Owner, pair.Comp);
}

public readonly record struct Entity<T1, T2>(EntityUid Owner, T1 Comp1, T2 Comp2)
{
    public static implicit operator EntityUid(Entity<T1, T2> entity) => entity.Owner;

    public static implicit operator Entity<T1, T2>((EntityUid Owner, T1 Comp1, T2 Comp2) row) => new(row.Owner, row.Comp1, row.Comp2);
}

public readonly record struct Entity<T1, T2, T3>(EntityUid Owner, T1 Comp1, T2 Comp2, T3 Comp3)
{
    public static implicit operator EntityUid(Entity<T1, T2, T3> entity) => entity.Owner;
}

public readonly record struct Entity<T1, T2, T3, T4>(EntityUid Owner, T1 Comp1, T2 Comp2, T3 Comp3, T4 Comp4)
{
    public static implicit operator EntityUid(Entity<T1, T2, T3, T4> entity) => entity.Owner;
}

/// <summary>A component came to an entity: <c>SubscribeLocalEvent&lt;InFlight, ComponentAdd&gt;</c></summary>
public readonly record struct ComponentAdd(EntityUid Entity) : IDirectedEvent
{
    EntityUid IDirectedEvent.Target => Entity;
}

/// <summary>A component is leaving an entity, its value still on it: <c>SubscribeLocalEvent&lt;InFlight, ComponentRemove&gt;</c></summary>
public readonly record struct ComponentRemove(EntityUid Entity) : IDirectedEvent
{
    EntityUid IDirectedEvent.Target => Entity;
}

/// <summary>Who sent a network event: the connection entity of the player</summary>
public readonly record struct EntitySessionEventArgs(EntityUid SenderSession);

public delegate void EventRefHandler<TEvent>(ref TEvent args) where TEvent : struct;

public delegate void EntityEventRefHandler<TComp, TEvent>(Entity<TComp> entity, ref TEvent args) where TEvent : struct;

public delegate void NetworkEventHandler<TMessage>(TMessage message, EntitySessionEventArgs args);
