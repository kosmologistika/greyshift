using System.Runtime.CompilerServices;
using Wit = TypedModuleWorld.wit.Imports.rs13.mods.v0_1_0.ITypedEcsImports;

namespace Rs13.Mods;

/// <summary>
/// A system of the module, found by the SDK generator: a non-abstract
/// partial subclass with no constructor parameters. It is rebuilt on every
/// setup, so its only fields are <c>[Dependency]</c> ones; state lives in own
/// components and resources. <see cref="Initialize"/> subscribes, and the
/// handlers read and write the world:
/// <list type="bullet">
/// <item>a read hands out the call's one instance of an own value, and changing it is the write, flushed when the handler returns and before every write to the host</item>
/// <item>a point read sees the call's own adds, removes and edits; an enumeration sees the world as it was when the call began</item>
/// <item><c>AddComp</c> replaces, deletion is <c>QueueDel</c>, and <c>Now</c> counts from the UNIX epoch</item>
/// <item>a throw drops the call's writes, and the host discards its queue</item>
/// </list>
/// Systems run in <see cref="UpdatesAfter"/>/<see cref="UpdatesBefore"/> order, then by name; within a system, handlers run in subscription order with <see cref="Update"/> last
/// </summary>
public abstract partial class EntitySystem
{
    internal readonly List<Subscription> subscriptions = [];
    internal bool updates;
    TimeSpan lastTick;
    TimeSpan lastActiveTick;

    /// <summary>Systems this one runs after, by type</summary>
    public HashSet<Type> UpdatesAfter { get; } = [];

    /// <summary>Systems this one runs before, by type</summary>
    public HashSet<Type> UpdatesBefore { get; } = [];

    /// <summary>Runs inside the host's setup, with no world: subscribe here</summary>
    public virtual void Initialize() { }

    /// <summary>Runs every tick, after the system's other handlers, when overridden</summary>
    public virtual void Update(float frameTime) { }

    /// <summary>The generator fills the <c>[Dependency]</c> fields here</summary>
    protected virtual void InjectDependencies(SystemSet systems) { }

    internal void Inject(SystemSet systems) => InjectDependencies(systems);

    internal void ScheduleUpdate() => Every(TimeSpan.Zero, Tick, "Update");

    internal void Tick() => Update(Frame(ref lastTick));

    /// <summary>The frame for a rule system's own tick, which runs beside <see cref="Update"/></summary>
    internal float ActiveFrame() => Frame(ref lastActiveTick);

    static float Frame(ref TimeSpan last)
    {
        var now = ModRuntime.Now();
        var frame = last == TimeSpan.Zero ? TimeSpan.Zero : now - last;
        last = now;
        return (float)frame.TotalSeconds;
    }

    string Name => GetType().Name;

    // Subscriptions, valid in Initialize

    protected void SubscribeLocalEvent<TEvent>(EventRefHandler<TEvent> handler, [CallerArgumentExpression(nameof(handler))] string name = "")
        where TEvent : struct, IEngineEvent
    {
        var on = typeof(TEvent).Name;
        subscriptions.Add(new EventSubscription(EngineEvent<TEvent>.Kind, e => ModRuntime.Guarded(() =>
        {
            var args = EngineEvent<TEvent>.Unwrap(e);
            handler(ref args);
        }, Name, name, on)));
    }

    /// <summary>Runs only when the event's target holds <typeparamref name="TComp"/>; <c>ComponentAdd</c> and <c>ComponentRemove</c> are the component's own edges</summary>
    protected void SubscribeLocalEvent<TComp, TEvent>(EntityEventRefHandler<TComp, TEvent> handler, [CallerArgumentExpression(nameof(handler))] string name = "")
        where TComp : class, IReadable
        where TEvent : struct, IDirectedEvent
    {
        // RS13010 refuses an edge the host does not relay at compile time; this is the runtime's own check
        var on = $"{typeof(TEvent).Name} on {typeof(TComp).Name}";
        if (typeof(TEvent) == typeof(ComponentAdd) || typeof(TEvent) == typeof(ComponentRemove))
        {
            var (kind, entity) = EngineWit.Edge(typeof(TComp), added: typeof(TEvent) == typeof(ComponentAdd));
            subscriptions.Add(new EventSubscription(kind, e => ModRuntime.Guarded(() =>
            {
                var uid = entity(e);
                if (ModRuntime.CompOrNull<TComp>(uid) is { } comp)
                {
                    var args = (TEvent)(object)(typeof(TEvent) == typeof(ComponentAdd) ? new ComponentAdd(uid) : new ComponentRemove(uid));
                    handler(new Entity<TComp>(uid, comp), ref args);
                }
            }, Name, name, on)));
            return;
        }
        subscriptions.Add(new EventSubscription(EngineEvent<TEvent>.Kind, e => ModRuntime.Guarded(() =>
        {
            var args = EngineEvent<TEvent>.Unwrap(e);
            var uid = args.Target;
            if (ModRuntime.CompOrNull<TComp>(uid) is { } comp)
            {
                handler(new Entity<TComp>(uid, comp), ref args);
            }
        }, Name, name, on)));
    }

    /// <summary>A client's event, with the sender's connection entity. A client's bytes are input, not a mirror: one that does not parse is dropped and logged</summary>
    protected void SubscribeNetworkEvent<TMessage>(NetworkEventHandler<TMessage> handler, [CallerArgumentExpression(nameof(handler))] string name = "")
        where TMessage : class, INetSerializable
    {
        var on = typeof(TMessage).Name;
        subscriptions.Add(new ClientSubscription(OwnType<TMessage>.Path, (from, bytes) => ModRuntime.Guarded(() =>
        {
            TMessage message;
            try
            {
                message = OwnType<TMessage>.Read(bytes);
            }
            catch (Exception e)
            {
                ModRuntime.Log($"dropped a client {on} that does not parse: {e.Message}");
                return;
            }
            handler(message, new EntitySessionEventArgs(from));
        }, Name, name, on)));
    }

    /// <summary>Runs every period in every round phase; zero is every tick. Named after the handler in the <c>mods</c> console</summary>
    protected void Every(TimeSpan every, Action handler, [CallerArgumentExpression(nameof(handler))] string name = "")
    {
        subscriptions.Add(new TimerSubscription($"timer:{Name}.{name}", every, () => ModRuntime.Guarded(handler, Name, name, "timer")));
    }

    // The world, valid in a handler

    protected bool HasComp<T>(EntityUid uid) where T : class, IReadable => ModRuntime.CompOrNull<T>(uid) is not null;

    protected bool TryComp<T>(EntityUid uid, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out T? comp) where T : class, IReadable
    {
        comp = ModRuntime.CompOrNull<T>(uid);
        return comp is not null;
    }

    /// <summary>The component, or a throw when the entity lacks it</summary>
    protected T Comp<T>(EntityUid uid) where T : class, IReadable =>
        ModRuntime.CompOrNull<T>(uid) ?? throw new InvalidOperationException($"{uid} has no {typeof(T).Name}");

    protected T? CompOrNull<T>(EntityUid uid) where T : class, IReadable => ModRuntime.CompOrNull<T>(uid);

    /// <summary>The entity's own component, added fresh when it has none</summary>
    protected T EnsureComp<T>(EntityUid uid) where T : class, IOwnComponent, new()
    {
        if (ModRuntime.CompOrNull<T>(uid) is { } found)
        {
            return found;
        }
        var fresh = new T();
        ModRuntime.AddComp(uid, fresh);
        return fresh;
    }

    /// <summary>Puts the component on the entity, replacing one it already holds</summary>
    protected void AddComp<T>(EntityUid uid, T comp) where T : class, IInsertable => ModRuntime.AddComp(uid, comp);

    protected void RemComp<T>(EntityUid uid) where T : class, IInsertable => ModRuntime.RemComp<T>(uid);

    /// <summary>The module's singleton, fresh when it was never written</summary>
    protected T Resource<T>() where T : class, IOwnResource, new() => ModRuntime.Resource<T>();

    /// <summary>A fresh entity carrying engine components, one crossing</summary>
    protected EntityUid Spawn(params ReadOnlySpan<ISpawnable> components) => ModRuntime.Spawn(components);

    /// <summary>A fresh entity from a prototype, which gets its components when the call commits; an unknown id throws</summary>
    protected EntityUid Spawn(EntProtoId proto) =>
        ModRuntime.SpawnPrototype(proto) ?? throw new InvalidOperationException($"no prototype has the id {proto}");

    /// <summary>The quiet form of <see cref="Spawn(EntProtoId)"/></summary>
    protected bool TrySpawn(EntProtoId proto, out EntityUid uid)
    {
        uid = ModRuntime.SpawnPrototype(proto) ?? EntityUid.Invalid;
        return uid.Valid;
    }

    /// <summary>Writes every changed value now rather than when the handler returns</summary>
    protected void Dirty() => ModRuntime.Writing();

    /// <summary>Despawns the entity and what it holds when the call commits</summary>
    protected void QueueDel(EntityUid uid) => ModRuntime.QueueDel(uid);

    protected void RaiseLocalEvent<T>(T @event) where T : struct, ITriggerEvent => ModRuntime.RaiseLocalEvent(@event);

    /// <summary>A mod event to every client, under the type's path</summary>
    protected void RaiseNetworkEvent<T>(T message) where T : class => ModRuntime.RaiseNetworkEvent(message, null);

    /// <summary>A mod event to one client, by its connection entity</summary>
    protected void RaiseNetworkEvent<T>(T message, EntityUid connection) where T : class => ModRuntime.RaiseNetworkEvent(message, connection);

    /// <summary>A mod event to several clients, serialized once</summary>
    protected void RaiseNetworkEvent<T>(T message, IEnumerable<EntityUid> connections) where T : class => ModRuntime.RaiseNetworkEventTo(message, connections);

    /// <summary>The server clock, since the UNIX epoch</summary>
    protected TimeSpan Now => ModRuntime.Now();

    protected void Log(string message) => ModRuntime.Log(message);

    protected EntityQueryEnumerator<T1> EntityQueryEnumerator<T1>() where T1 : class, IReadable => new(ModRuntime.Rows<T1>());

    protected EntityQueryEnumerator<T1, T2> EntityQueryEnumerator<T1, T2>() where T1 : class, IReadable where T2 : class, IReadable =>
        new(ModRuntime.Rows<T1, T2>());

    protected EntityQueryEnumerator<T1, T2, T3> EntityQueryEnumerator<T1, T2, T3>() where T1 : class, IReadable where T2 : class, IReadable where T3 : class, IReadable =>
        new(ModRuntime.Rows<T1, T2, T3>());

    protected EntityQueryEnumerator<T1, T2, T3, T4> EntityQueryEnumerator<T1, T2, T3, T4>()
        where T1 : class, IReadable where T2 : class, IReadable where T3 : class, IReadable where T4 : class, IReadable =>
        new(ModRuntime.Rows<T1, T2, T3, T4>());
}

/// <summary>Every entity holding the components, as the world was when the call began</summary>
public sealed class EntityQueryEnumerator<T1>(IEnumerable<(EntityUid, T1)> rows)
{
    readonly IEnumerator<(EntityUid, T1)> rows = rows.GetEnumerator();

    public bool MoveNext(out EntityUid uid, out T1 comp1)
    {
        if (!rows.MoveNext())
        {
            (uid, comp1) = (default, default!);
            return false;
        }
        (uid, comp1) = rows.Current;
        return true;
    }
}

public sealed class EntityQueryEnumerator<T1, T2>(IEnumerable<(EntityUid, T1, T2)> rows)
{
    readonly IEnumerator<(EntityUid, T1, T2)> rows = rows.GetEnumerator();

    public bool MoveNext(out EntityUid uid, out T1 comp1, out T2 comp2)
    {
        if (!rows.MoveNext())
        {
            (uid, comp1, comp2) = (default, default!, default!);
            return false;
        }
        (uid, comp1, comp2) = rows.Current;
        return true;
    }
}

public sealed class EntityQueryEnumerator<T1, T2, T3>(IEnumerable<(EntityUid, T1, T2, T3)> rows)
{
    readonly IEnumerator<(EntityUid, T1, T2, T3)> rows = rows.GetEnumerator();

    public bool MoveNext(out EntityUid uid, out T1 comp1, out T2 comp2, out T3 comp3)
    {
        if (!rows.MoveNext())
        {
            (uid, comp1, comp2, comp3) = (default, default!, default!, default!);
            return false;
        }
        (uid, comp1, comp2, comp3) = rows.Current;
        return true;
    }
}

public sealed class EntityQueryEnumerator<T1, T2, T3, T4>(IEnumerable<(EntityUid, T1, T2, T3, T4)> rows)
{
    readonly IEnumerator<(EntityUid, T1, T2, T3, T4)> rows = rows.GetEnumerator();

    public bool MoveNext(out EntityUid uid, out T1 comp1, out T2 comp2, out T3 comp3, out T4 comp4)
    {
        if (!rows.MoveNext())
        {
            (uid, comp1, comp2, comp3, comp4) = (default, default!, default!, default!, default!);
            return false;
        }
        (uid, comp1, comp2, comp3, comp4) = rows.Current;
        return true;
    }
}
