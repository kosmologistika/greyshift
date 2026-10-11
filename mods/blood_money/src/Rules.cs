using Rs13.Mods;

namespace BloodMoney;

// The mode's prototype-named components, authored as `blood_money::X` in a
// prototype and read back as these

// Ends the round after `matches` matches with the most match wins taking it,
// total coins earned across the round breaking ties
[RegisterComponent]
public sealed partial class BestOfRuleComponent : Component
{
    [DataField] public uint Matches;
}

// Ends its match when a wallet reaches `target`, or when `secs` run out with
// the richest wallet winning
[RegisterComponent]
public sealed partial class CoinTargetRuleComponent : Component
{
    [DataField] public uint Target;
    [DataField] public ulong Secs;
}

[RegisterComponent]
public sealed partial class CoinEconomyRuleComponent : Component
{
    [DataField] public uint DripPerSec;
    [DataField] public uint DripBelow;
    [DataField] public uint Crumb;
    [DataField] public uint Bounty;
    [DataField] public uint PileValue;
    [DataField] public ulong CoinLifeSecs;
}

[RegisterComponent]
public sealed partial class ShopRuleComponent : Component
{
    [DataField] public uint Start;
    [DataField] public uint Floor;
    [DataField] public uint Rolled;
    [DataField] public ulong LockSecs;
    [DataField] public ulong BackstopSecs;
}

// A handful of coins on the floor; replicated, so a client sees what a pile is worth
[RegisterComponent, NetworkedComponent]
public sealed partial class CoinPileComponent : Component
{
    [DataField] public uint Value;
}

// On a weapon: what its hits do to the money. Knockout scales the coins a
// hit shakes out of the victim, Refund is the share the attacker pockets
[RegisterComponent]
public sealed partial class CoinHooksComponent : Component
{
    [DataField] public float Knockout = 1f;
    [DataField] public float Refund;
}

// A payload: when the item is triggered it showers `value` in coin piles
[RegisterComponent]
public sealed partial class CoinBurstComponent : Component
{
    [DataField] public uint Value;
}
