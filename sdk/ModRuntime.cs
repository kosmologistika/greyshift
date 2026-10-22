using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wit = TypedModuleWorld.wit.Imports.rs13.mods.v0_1_0.ITypedEcsImports;

namespace Rs13.Packs;

/// <summary>
/// What <c>ModEntry.Build</c> fills: the module's own types and its systems.
/// The setup-only host calls happen once in <see cref="ModRuntime.Start"/>
/// </summary>
public sealed class ModBuilder
{
    internal readonly List<EntitySystem> systems = [];
    internal readonly List<(string path, bool networked)> components = [];

    /// <summary>The strict context the props write for the module's own types</summary>
    public JsonSerializerContext? Json { get; set; }

    /// <summary>The lenient context for mirrors of other packs' types</summary>
    public JsonSerializerContext? Mirrors { get; set; }

    public void Component<T>(string path, bool networked) where T : class
    {
        OwnType<T>.Fill(path, Json, OwnKind.Component, networked);
        components.Add((path, networked));
    }

    public void Resource<T>(string path) where T : class => OwnType<T>.Fill(path, Json, OwnKind.Resource, false);

    public void Message<T>(string path) where T : class => OwnType<T>.Fill(path, Json, OwnKind.Message, false);

    public void Mirror<T>(string path) where T : class => OwnType<T>.Fill(path, Mirrors, OwnKind.Mirror, false);

    public void System(EntitySystem system, bool updates)
    {
        system.updates = updates;
        systems.Add(system);
    }
}

/// <summary>The module's systems and the SDK's built-ins by type, for <c>[Dependency]</c> injection</summary>
public sealed class SystemSet
{
    readonly Dictionary<Type, object> held = [];

    internal SystemSet(IEnumerable<EntitySystem> all, IEnumerable<(Type type, object instance)> builtIns)
    {
        foreach (var (type, instance) in builtIns)
        {
            held[type] = instance;
        }
        foreach (var system in all)
        {
            held[system.GetType()] = system;
        }
    }

    public T Get<T>() where T : class =>
        held.TryGetValue(typeof(T), out var found) ? (T)found : throw new InvalidOperationException($"no {typeof(T).Name} in this module");
}

/// <summary>What a handler asked of the host and what it was answering when it threw</summary>
public sealed class ModHandlerException(string message, Exception inner) : Exception(message, inner);

/// <summary>
/// The systems runtime: one call at a time, the own values it handed out,
/// and the subscriptions in dispatch order. Writes to own values land when
/// the call returns and before every write to the host, so program order
/// holds; a throw skips them and the host discards the call's queue
/// </summary>
static class ModRuntime
{
    static List<EntitySystem> systems = [];
    static readonly Dictionary<Wit.EventKind, List<Action<Wit.Event>>> handlers = [];
    static readonly Dictionary<string, Action> timers = [];
    static readonly Dictionary<string, List<Action<EntityUid, byte[]>>> clientHandlers = [];
    static readonly Dictionary<(ulong entity, Type type), Tracked> tracked = [];
    // What the call added and removed, so a later point read sees it
    static readonly HashSet<(ulong entity, Type type)> added = [];
    static readonly HashSet<(ulong entity, Type type)> removed = [];
    static bool inCall;
    static int generation;

    /// <summary>Counts the calls, so a per-call cache knows it is stale</summary>
    internal static int Generation => generation;

    internal static void Start(ModBuilder b)
    {
        foreach (var (path, networked) in b.components)
        {
            Wit.RegisterComponent(path);
            if (networked)
            {
                Wit.ReplicateComponent(path);
            }
        }
        var set = new SystemSet(b.systems, BuiltIns.All());
        foreach (var system in b.systems)
        {
            system.Inject(set);
        }
        foreach (var system in b.systems)
        {
            system.Initialize();
            if (system.updates)
            {
                system.ScheduleUpdate();
            }
        }
        systems = Sorted(b.systems);
        foreach (var system in systems)
        {
            foreach (var subscription in system.subscriptions)
            {
                Subscribe(subscription);
            }
        }
    }

    static void Subscribe(Subscription subscription)
    {
        switch (subscription)
        {
            case EventSubscription(var kind, var run):
                if (!handlers.TryGetValue(kind, out var list))
                {
                    handlers[kind] = list = [];
                    Wit.Observe(kind);
                }
                list.Add(run);
                break;
            case TimerSubscription(var name, var every, var run):
                if (timers.ContainsKey(name))
                {
                    throw new InvalidOperationException($"{name} is scheduled twice: an Every handler is named by its method, so each needs its own");
                }
                Wit.Schedule(name, (ulong)every.TotalMilliseconds);
                timers[name] = run;
                break;
            case ClientSubscription(var kind, var run):
                if (!clientHandlers.TryGetValue(kind, out var clients))
                {
                    clientHandlers[kind] = clients = [];
                    Wit.AcceptClientEvent(kind);
                }
                clients.Add(run);
                break;
        }
    }

    /// <summary>UpdatesAfter and UpdatesBefore first, the full name settling the rest</summary>
    static List<EntitySystem> Sorted(List<EntitySystem> all)
    {
        var byType = all.ToDictionary(s => s.GetType());
        var after = all.ToDictionary(s => s, _ => new HashSet<EntitySystem>());
        foreach (var system in all)
        {
            foreach (var type in system.UpdatesAfter)
            {
                after[system].Add(Named(byType, type, system));
            }
            foreach (var type in system.UpdatesBefore)
            {
                after[Named(byType, type, system)].Add(system);
            }
        }
        var order = new List<EntitySystem>();
        var ready = new SortedList<string, EntitySystem>(StringComparer.Ordinal);
        var pending = new Dictionary<EntitySystem, int>();
        foreach (var system in all)
        {
            pending[system] = after[system].Count;
            if (after[system].Count == 0)
            {
                ready.Add(FullName(system), system);
            }
        }
        while (ready.Count > 0)
        {
            var next = ready.Values[0];
            ready.RemoveAt(0);
            order.Add(next);
            foreach (var other in all)
            {
                if (after[other].Remove(next) && --pending[other] == 0)
                {
                    ready.Add(FullName(other), other);
                }
            }
        }
        if (order.Count < all.Count)
        {
            var stuck = string.Join(", ", all.Where(s => !order.Contains(s)).Select(FullName));
            throw new InvalidOperationException($"UpdatesAfter and UpdatesBefore form a cycle through {stuck}");
        }
        return order;
    }

    static EntitySystem Named(Dictionary<Type, EntitySystem> byType, Type type, EntitySystem from) =>
        byType.TryGetValue(type, out var found) ? found : throw new InvalidOperationException($"{FullName(from)} orders against {type.Name}, which is not a system of this module");

    static string FullName(EntitySystem system) => system.GetType().FullName ?? system.GetType().Name;

    // The exports

    internal static void Dispatch(Wit.Event @event) =>
        Call(() =>
        {
            if (handlers.TryGetValue((Wit.EventKind)@event.Tag, out var list))
            {
                foreach (var handler in list)
                {
                    handler(@event);
                }
            }
        });

    internal static void DispatchTimer(string name) =>
        Call(() =>
        {
            if (timers.TryGetValue(name, out var handler))
            {
                handler();
            }
        });

    internal static void DispatchClient(string kind, ulong from, byte[] bytes) =>
        Call(() =>
        {
            if (clientHandlers.TryGetValue(kind, out var list))
            {
                foreach (var handler in list)
                {
                    handler(new EntityUid(from), bytes);
                }
            }
        });

    static void Call(Action body)
    {
        inCall = true;
        generation++;
        try
        {
            body();
            Flush();
        }
        finally
        {
            inCall = false;
            tracked.Clear();
            added.Clear();
            removed.Clear();
        }
    }

    /// <summary>Runs one handler, naming it when it throws</summary>
    internal static void Guarded(Action handler, string system, string name, string on)
    {
        try
        {
            handler();
        }
        catch (Exception e) when (e is not ModHandlerException)
        {
            throw new ModHandlerException($"{system}.{name} on {on}: {e.Message}", e);
        }
    }

    // Write-back

    abstract class Tracked
    {
        public abstract object Value { get; }

        public abstract void FlushTo();
    }

    /// <summary>An own value against the bytes the host sent, so an untouched read costs no write</summary>
    sealed class OwnTracked(object value, byte[]? snapshot, Func<object, byte[]> serialize, Action<byte[]> write) : Tracked
    {
        public override object Value { get; } = value;

        public override void FlushTo()
        {
            var bytes = serialize(Value);
            if (snapshot is null || !bytes.AsSpan().SequenceEqual(snapshot))
            {
                write(bytes);
                snapshot = bytes;
            }
        }
    }

    /// <summary>An engine record against a shallow copy: record equality notices a changed member or a replaced list</summary>
    sealed class EngineTracked(EntityUid uid, IMutable value) : Tracked
    {
        object snapshot = value.Snapshot();

        public override object Value { get; } = value;

        public override void FlushTo()
        {
            if (!value.Equals(snapshot))
            {
                Wit.Mutate(uid.Bits, EngineWit.wraps[value.GetType()](value));
                snapshot = value.Snapshot();
            }
        }
    }

    static void Flush()
    {
        foreach (var entry in tracked.Values)
        {
            entry.FlushTo();
        }
    }

    /// <summary>Every write to the host flushes first, so a handler's writes land in program order</summary>
    internal static void Writing()
    {
        InCall();
        Flush();
    }

    internal static void InCall()
    {
        if (!inCall)
        {
            throw new InvalidOperationException("a world call outside a handler: Initialize subscribes, handlers read and write");
        }
    }

    static T Track<T>(EntityUid uid, T value, byte[]? snapshot) where T : class
    {
        tracked[(uid.Bits, typeof(T))] = new OwnTracked(
            value,
            snapshot,
            v => OwnType<T>.Write((T)v),
            uid.Valid ? bytes => Wit.InsertOwn(uid.Bits, OwnType<T>.Path, bytes) : bytes => Wit.InsertResource(OwnType<T>.Path, bytes));
        return value;
    }

    /// <summary>The call's one instance of an engine record on the entity: a mutable one is tracked, the rest are init-only</summary>
    static T Engine<T>(EntityUid uid, Wit.Component component) where T : class
    {
        if (!Shape<T>.Mutable)
        {
            return EngineComponent<T>.Unwrap(component);
        }
        if (tracked.TryGetValue((uid.Bits, typeof(T)), out var held))
        {
            return (T)held.Value;
        }
        var value = EngineComponent<T>.Unwrap(component);
        tracked[(uid.Bits, typeof(T))] = new EngineTracked(uid, (IMutable)value);
        return value;
    }

    // Reads

    /// <summary>Own types and engine types take different host calls; the marker says which</summary>
    static class Shape<T>
    {
        public static readonly bool Engine = typeof(IEngineComponent).IsAssignableFrom(typeof(T));
        public static readonly bool Mutable = typeof(IMutable).IsAssignableFrom(typeof(T));
    }

    internal static T? CompOrNull<T>(EntityUid uid) where T : class, IReadable
    {
        InCall();
        if (removed.Contains((uid.Bits, typeof(T))))
        {
            return null;
        }
        if (tracked.TryGetValue((uid.Bits, typeof(T)), out var held))
        {
            return (T)held.Value;
        }
        if (Shape<T>.Engine)
        {
            return Wit.Get(uid.Bits, EngineComponent<T>.Kind) is { } found ? Engine<T>(uid, found) : null;
        }
        if (Wit.GetOwn(uid.Bits, OwnType<T>.Path) is not { } bytes)
        {
            return null;
        }
        var value = OwnType<T>.Read(bytes);
        return OwnType<T>.Kind == OwnKind.Mirror ? value : Track(uid, value, bytes);
    }

    internal static bool Added<T>(EntityUid uid) => added.Contains((uid.Bits, typeof(T)));

    internal static bool Removed<T>(EntityUid uid) => removed.Contains((uid.Bits, typeof(T)));

    /// <summary>The entities holding the component as the world was when the call began</summary>
    internal static HashSet<ulong> Holders<T>() where T : class, IReadable
    {
        InCall();
        var holders = new HashSet<ulong>();
        if (Shape<T>.Engine)
        {
            foreach (var row in Wit.Query([EngineComponent<T>.Kind]))
            {
                holders.Add(row.entity);
            }
            return holders;
        }
        foreach (var (entity, _) in Wit.QueryOwn(OwnType<T>.Path))
        {
            holders.Add(entity);
        }
        return holders;
    }

    /// <summary>Every rule of the type with the scope it sits under</summary>
    internal static IEnumerable<(Entity<T> rule, EntityUid scope)> Rules<T>() where T : class, IOwnComponent
    {
        foreach (var (uid, value) in Rows<T>())
        {
            if (CompOrNull<ChildOf>(uid) is { } scope)
            {
                yield return (new Entity<T>(uid, value), scope.Value);
            }
        }
    }

    /// <summary>The rules whose scope has not ended</summary>
    internal static IEnumerable<(Entity<T> rule, EntityUid scope)> ActiveRules<T>() where T : class, IOwnComponent
    {
        foreach (var (rule, scope) in Rules<T>())
        {
            if (CompOrNull<ScopeEnded>(scope) is null)
            {
                yield return (rule, scope);
            }
        }
    }

    internal static ulong Tick()
    {
        InCall();
        return Wit.Tick();
    }

    internal static bool RoundLive()
    {
        InCall();
        return Wit.RoundLive();
    }

    internal static RoundState RoundState()
    {
        InCall();
        var state = Wit.RoundState();
        return state switch
        {
            "PreRoundLobby" => Packs.RoundState.PreRoundLobby,
            "RoundStarting" => Packs.RoundState.RoundStarting,
            "InRound" => Packs.RoundState.InRound,
            "PostRound" => Packs.RoundState.PostRound,
            _ => throw new InvalidOperationException($"the host names a round state the SDK does not know: {state}"),
        };
    }

    internal static T Resource<T>() where T : class, IOwnResource, new()
    {
        InCall();
        if (tracked.TryGetValue((0, typeof(T)), out var held))
        {
            return (T)held.Value;
        }
        var bytes = Wit.GetResource(OwnType<T>.Path);
        return Track(EntityUid.Invalid, bytes is null ? new T() : OwnType<T>.Read(bytes), bytes);
    }

    // Writes

    internal static void AddComp<T>(EntityUid uid, T value) where T : class, IInsertable
    {
        Writing();
        removed.Remove((uid.Bits, typeof(T)));
        added.Add((uid.Bits, typeof(T)));
        if (Shape<T>.Engine)
        {
            Wit.Insert(uid.Bits, EngineComponent<T>.Wrap(value));
            if (Shape<T>.Mutable)
            {
                tracked[(uid.Bits, typeof(T))] = new EngineTracked(uid, (IMutable)value);
            }
            else
            {
                tracked.Remove((uid.Bits, typeof(T)));
            }
            return;
        }
        var bytes = OwnType<T>.Write(value);
        Wit.InsertOwn(uid.Bits, OwnType<T>.Path, bytes);
        Track(uid, value, bytes);
    }

    internal static void RemComp<T>(EntityUid uid) where T : class, IInsertable
    {
        Writing();
        tracked.Remove((uid.Bits, typeof(T)));
        added.Remove((uid.Bits, typeof(T)));
        removed.Add((uid.Bits, typeof(T)));
        if (Shape<T>.Engine)
        {
            Wit.Remove(uid.Bits, EngineComponent<T>.Kind);
            return;
        }
        Wit.RemoveOwn(uid.Bits, OwnType<T>.Path);
    }

    internal static EntityUid Spawn(ReadOnlySpan<ISpawnable> components)
    {
        Writing();
        var wrapped = new List<Wit.Component>(components.Length);
        foreach (var component in components)
        {
            wrapped.Add(EngineWit.Wrap(component));
        }
        var uid = new EntityUid(Wit.Spawn(wrapped));
        foreach (var component in components)
        {
            added.Add((uid.Bits, component.GetType()));
            if (component is IMutable mutable)
            {
                tracked[(uid.Bits, component.GetType())] = new EngineTracked(uid, mutable);
            }
        }
        return uid;
    }

    internal static EntityUid? SpawnPrototype(EntProtoId proto)
    {
        Writing();
        return Wit.SpawnPrototype(proto.Id) is { } entity ? new EntityUid(entity) : null;
    }

    internal static void QueueDel(EntityUid uid)
    {
        Writing();
        Wit.Despawn(uid.Bits);
    }

    internal static void RaiseLocalEvent<T>(T @event) where T : struct, ITriggerEvent
    {
        Writing();
        Wit.Trigger(EngineTrigger<T>.Wrap(@event));
    }

    internal static void RaiseNetworkEvent<T>(T message, EntityUid? connection) where T : class
    {
        Writing();
        var bytes = OwnType<T>.Write(message);
        if (connection is { } to)
        {
            Wit.SendToClient(to.Bits, OwnType<T>.Path, bytes);
        }
        else
        {
            Wit.SendToClients(OwnType<T>.Path, bytes);
        }
    }

    internal static void RaiseNetworkEventTo<T>(T message, IEnumerable<EntityUid> connections) where T : class
    {
        Writing();
        var bytes = OwnType<T>.Write(message);
        foreach (var to in connections)
        {
            Wit.SendToClient(to.Bits, OwnType<T>.Path, bytes);
        }
    }

    internal static void Log(string message)
    {
        InCall();
        Wit.Log(message);
    }

    internal static TimeSpan Now()
    {
        InCall();
        return TimeSpan.FromMilliseconds(Wit.Now());
    }

    // Enumeration: every engine type in one query, else the first own type
    // drives and the rest are read per row

    internal static IEnumerable<(EntityUid, T1)> Rows<T1>() where T1 : class, IReadable
    {
        InCall();
        if (Shape<T1>.Engine)
        {
            foreach (var row in Wit.Query([EngineComponent<T1>.Kind]))
            {
                yield return (new EntityUid(row.entity), Engine<T1>(new EntityUid(row.entity), row.values[0]));
            }
            yield break;
        }
        foreach (var (uid, value) in OwnRows<T1>())
        {
            yield return (uid, value);
        }
    }

    internal static IEnumerable<(EntityUid, T1, T2)> Rows<T1, T2>() where T1 : class, IReadable where T2 : class, IReadable
    {
        InCall();
        if (Shape<T1>.Engine && Shape<T2>.Engine)
        {
            foreach (var row in Wit.Query([EngineComponent<T1>.Kind, EngineComponent<T2>.Kind]))
            {
                yield return (new EntityUid(row.entity), Engine<T1>(new EntityUid(row.entity), row.values[0]), Engine<T2>(new EntityUid(row.entity), row.values[1]));
            }
            yield break;
        }
        foreach (var uid in Driver<T1, T2>())
        {
            if (CompOrNull<T1>(uid) is { } c1 && CompOrNull<T2>(uid) is { } c2)
            {
                yield return (uid, c1, c2);
            }
        }
    }

    internal static IEnumerable<(EntityUid, T1, T2, T3)> Rows<T1, T2, T3>() where T1 : class, IReadable where T2 : class, IReadable where T3 : class, IReadable
    {
        InCall();
        if (Shape<T1>.Engine && Shape<T2>.Engine && Shape<T3>.Engine)
        {
            foreach (var row in Wit.Query([EngineComponent<T1>.Kind, EngineComponent<T2>.Kind, EngineComponent<T3>.Kind]))
            {
                yield return (new EntityUid(row.entity), Engine<T1>(new EntityUid(row.entity), row.values[0]), Engine<T2>(new EntityUid(row.entity), row.values[1]), Engine<T3>(new EntityUid(row.entity), row.values[2]));
            }
            yield break;
        }
        foreach (var uid in Driver<T1, T2, T3>())
        {
            if (CompOrNull<T1>(uid) is { } c1 && CompOrNull<T2>(uid) is { } c2 && CompOrNull<T3>(uid) is { } c3)
            {
                yield return (uid, c1, c2, c3);
            }
        }
    }

    internal static IEnumerable<(EntityUid, T1, T2, T3, T4)> Rows<T1, T2, T3, T4>()
        where T1 : class, IReadable where T2 : class, IReadable where T3 : class, IReadable where T4 : class, IReadable
    {
        InCall();
        if (Shape<T1>.Engine && Shape<T2>.Engine && Shape<T3>.Engine && Shape<T4>.Engine)
        {
            foreach (var row in Wit.Query([EngineComponent<T1>.Kind, EngineComponent<T2>.Kind, EngineComponent<T3>.Kind, EngineComponent<T4>.Kind]))
            {
                yield return (
                    new EntityUid(row.entity),
                    Engine<T1>(new EntityUid(row.entity), row.values[0]),
                    Engine<T2>(new EntityUid(row.entity), row.values[1]),
                    Engine<T3>(new EntityUid(row.entity), row.values[2]),
                    Engine<T4>(new EntityUid(row.entity), row.values[3]));
            }
            yield break;
        }
        foreach (var uid in Driver<T1, T2, T3, T4>())
        {
            if (CompOrNull<T1>(uid) is { } c1 && CompOrNull<T2>(uid) is { } c2 && CompOrNull<T3>(uid) is { } c3 && CompOrNull<T4>(uid) is { } c4)
            {
                yield return (uid, c1, c2, c3, c4);
            }
        }
    }

    static IEnumerable<EntityUid> Driver<T1, T2>() where T1 : class, IReadable where T2 : class, IReadable =>
        !Shape<T1>.Engine ? Entities<T1>() : Entities<T2>();

    static IEnumerable<EntityUid> Driver<T1, T2, T3>() where T1 : class, IReadable where T2 : class, IReadable where T3 : class, IReadable =>
        !Shape<T1>.Engine ? Entities<T1>() : Driver<T2, T3>();

    static IEnumerable<EntityUid> Driver<T1, T2, T3, T4>() where T1 : class, IReadable where T2 : class, IReadable where T3 : class, IReadable where T4 : class, IReadable =>
        !Shape<T1>.Engine ? Entities<T1>() : Driver<T2, T3, T4>();

    /// <summary>The entities holding an own type, each value tracked so the per-row read finds it</summary>
    static IEnumerable<EntityUid> Entities<T>() where T : class, IReadable
    {
        foreach (var (uid, _) in OwnRows<T>())
        {
            yield return uid;
        }
    }

    static IEnumerable<(EntityUid, T)> OwnRows<T>() where T : class, IReadable
    {
        foreach (var (entity, bytes) in Wit.QueryOwn(OwnType<T>.Path))
        {
            var uid = new EntityUid(entity);
            if (removed.Contains((entity, typeof(T))))
            {
                continue;
            }
            if (tracked.TryGetValue((entity, typeof(T)), out var held))
            {
                yield return (uid, (T)held.Value);
                continue;
            }
            var value = OwnType<T>.Read(bytes);
            yield return (uid, OwnType<T>.Kind == OwnKind.Mirror ? value : Track(uid, value, bytes));
        }
    }
}

abstract record Subscription;

sealed record EventSubscription(TypedModuleWorld.wit.Imports.rs13.mods.v0_1_0.ITypedEcsImports.EventKind Kind, Action<TypedModuleWorld.wit.Imports.rs13.mods.v0_1_0.ITypedEcsImports.Event> Run) : Subscription;

sealed record TimerSubscription(string Name, TimeSpan Every, Action Run) : Subscription;

sealed record ClientSubscription(string Kind, Action<EntityUid, byte[]> Run) : Subscription;
