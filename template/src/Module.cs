using Rs13.Mods;

namespace Template;

// Authored by the rule prototype as template::GreetingRule: the assembly
// name, then the class name with a trailing Component dropped
[RegisterComponent]
public sealed partial class GreetingRuleComponent : Component
{
    [DataField] public string Title = "";
}

// Left on the rule once its banner is up, and sent to clients
[RegisterComponent, NetworkedComponent]
public sealed partial class GreetedComponent : Component
{
    [DataField] public EntityUid Banner;
}

// A rule runs over the component its prototype names; Started fires when the
// round brings the rule up
public sealed partial class GreetingRuleSystem : GameRuleSystem<GreetingRuleComponent>
{
    [Dependency] IGameTiming _timing = default!;

    protected override void Started(Entity<GreetingRuleComponent> rule, ref RuleStarted args)
    {
        var banner = Spawn(new HudBanner(HudZone.TopCenter, rule.Comp.Title, "", null), new ChildOf(rule), new Replicated());
        AddComp(rule, new GreetedComponent { Banner = banner });
        Log($"greeted at {_timing.CurTime}");
    }
}
