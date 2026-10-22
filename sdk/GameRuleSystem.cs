using System.Runtime.CompilerServices;

namespace Rs13.Packs;

/// <summary>
/// A system driving one rule component. A rule is active while the scope it
/// is a <see cref="ChildOf"/> has no <see cref="ScopeEnded"/>. The host puts
/// <c>ScopeEnded</c> on a scope before it raises <see cref="ScopeEnding"/>,
/// so <see cref="Ending"/> runs with the rule already inactive and
/// <see cref="NestedScopeEnding"/> only while it is still live. An override
/// of <see cref="Initialize"/> calls the base, or nothing here subscribes
/// </summary>
public abstract partial class GameRuleSystem<T> : EntitySystem where T : Component, IOwnComponent
{
    [Dependency] protected GameTicker GameTicker = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<T, RuleStarted>(OnStarted);
        SubscribeLocalEvent<ScopeEnding>(OnScopeEnding);
        Every(TimeSpan.Zero, OnTick, "ActiveTick");
    }

    /// <summary>The rule entity is live under its scope</summary>
    protected virtual void Started(Entity<T> rule, ref RuleStarted args) { }

    /// <summary>The rule's own scope is ending; it already carries <see cref="ScopeEnded"/></summary>
    protected virtual void Ending(Entity<T> rule, EntityUid scope, ref ScopeEnding args) { }

    /// <summary>A scope directly inside the rule's live scope is ending: a match inside the round. <paramref name="scope"/> is the rule's</summary>
    protected virtual void NestedScopeEnding(Entity<T> rule, EntityUid scope, ref ScopeEnding args) { }

    /// <summary>Every tick, once per active rule</summary>
    protected virtual void ActiveTick(Entity<T> rule, EntityUid scope, float frameTime) { }

    /// <summary>Runs every period, once per active rule, under the handler's name in the <c>mods</c> console</summary>
    protected void EveryActive(TimeSpan period, Action<Entity<T>, EntityUid> handler, [CallerArgumentExpression(nameof(handler))] string name = "")
    {
        Every(period, () =>
        {
            foreach (var (rule, scope) in ModRuntime.ActiveRules<T>())
            {
                handler(rule, scope);
            }
        }, name);
    }

    void OnStarted(Entity<T> rule, ref RuleStarted args) => Started(rule, ref args);

    void OnScopeEnding(ref ScopeEnding args)
    {
        var ending = args.Entity;
        var parent = CompOrNull<ChildOf>(ending)?.Value;
        foreach (var (rule, scope) in ModRuntime.Rules<T>())
        {
            if (scope == ending)
            {
                Ending(rule, scope, ref args);
            }
            else if (scope == parent && CompOrNull<ScopeEnded>(scope) is null)
            {
                NestedScopeEnding(rule, scope, ref args);
            }
        }
    }

    void OnTick()
    {
        var frame = ActiveFrame();
        foreach (var (rule, scope) in ModRuntime.ActiveRules<T>())
        {
            ActiveTick(rule, scope, frame);
        }
    }
}
