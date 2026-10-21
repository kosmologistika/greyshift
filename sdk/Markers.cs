namespace Rs13.Mods;

// What a type may do is a constraint, so a grant the host would refuse at
// runtime does not compile. sdk.rs puts these on the engine's types from
// their grants

/// <summary>An engine component as the facade shapes it</summary>
public interface IEngineComponent;

/// <summary>An engine event a module may observe</summary>
public interface IEngineEvent;

/// <summary>An event that happens to one entity, the one a directed subscription keys on</summary>
public interface IDirectedEvent
{
    EntityUid Target { get; }
}

/// <summary>An event a module may raise</summary>
public interface ITriggerEvent;

/// <summary>The host answers a read</summary>
public interface IReadable;

/// <summary>The host takes an insert and a remove</summary>
public interface IInsertable;

/// <summary>The host takes a mutate, so the members are settable. The runtime keeps a shallow copy of what a read handed out and writes the value back when it differs</summary>
public interface IMutable
{
    object Snapshot();
}

/// <summary>What a spawn may carry</summary>
public interface ISpawnable;

/// <summary>A module may observe the component being added to an entity</summary>
public interface IAddObservable;

/// <summary>A module may observe the component leaving an entity, the value still on it</summary>
public interface IRemoveObservable;

// The SDK generator puts these on the module's own types from their attributes

/// <summary>A component of the module's own: JSON on the wire, held host-side, written back after a change</summary>
public interface IOwnComponent : IReadable, IInsertable;

/// <summary>A singleton of the module's own, held the same way</summary>
public interface IOwnResource;

/// <summary>An event of the module's own, JSON to and from clients</summary>
public interface INetSerializable;

/// <summary>Another package's public component, read leniently and never written</summary>
public interface IMirrorComponent : IReadable;
