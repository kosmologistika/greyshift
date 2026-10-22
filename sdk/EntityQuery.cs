namespace Rs13.Packs;

/// <summary>
/// A <c>[Dependency]</c> that answers which entities hold a component. The
/// set is fetched once per call, on first use, so a check per row of an
/// enumeration costs nothing; it sees the call's own adds and removes
/// </summary>
public sealed class EntityQuery<T> where T : class, IReadable
{
    HashSet<ulong>? holders;
    int fetched = -1;

    public bool HasComp(EntityUid uid)
    {
        if (ModRuntime.Removed<T>(uid))
        {
            return false;
        }
        return ModRuntime.Added<T>(uid) || Holders.Contains(uid.Bits);
    }

    public bool TryComp(EntityUid uid, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out T? comp)
    {
        comp = CompOrNull(uid);
        return comp is not null;
    }

    public T Comp(EntityUid uid) => CompOrNull(uid) ?? throw new InvalidOperationException($"{uid} has no {typeof(T).Name}");

    public T? CompOrNull(EntityUid uid) => HasComp(uid) ? ModRuntime.CompOrNull<T>(uid) : null;

    HashSet<ulong> Holders
    {
        get
        {
            if (holders is null || fetched != ModRuntime.Generation)
            {
                holders = ModRuntime.Holders<T>();
                fetched = ModRuntime.Generation;
            }
            return holders;
        }
    }
}
