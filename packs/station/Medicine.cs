using Rs13.Packs;

namespace Station;

// Field care by damage type: a consumable that takes Amount off a body's
// wounds across Kinds in order, over a windup of Secs, Uses times
[RegisterComponent]
public sealed partial class TreatsComponent : Component
{
    [DataField] public List<DamageType> Kinds = [];
    [DataField] public uint Amount;
    [DataField] public float Secs;
    [DataField] public uint Uses;
}

// On a care windup: what is being applied to whom
[RegisterComponent]
public sealed partial class CaringComponent : Component
{
    [DataField] public EntityUid Item;
    [DataField] public EntityUid Patient;
}

public sealed partial class CareSystem : EntitySystem
{
    [Dependency] DoAfterSystem _doAfters = default!;
    [Dependency] PopupSystem _popups = default!;
    [Dependency] BodySystem _bodies = default!;
    [Dependency] EntityQuery<Dead> _dead = default!;
    [Dependency] EntityQuery<Critical> _critical = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<Applied>(OnApplied);
        SubscribeLocalEvent<CaringComponent, DoAfterFinished>(OnFinished);
    }

    // The engine's Apply verb passed reach and life; the care checks the wound
    // is one this item treats, then runs the windup that breaks on moving, on
    // the hand changing and on the carer taking a blow
    void OnApplied(ref Applied args)
    {
        if (!TryComp<TreatsComponent>(args.Item, out var treats))
        {
            return;
        }
        if (_dead.HasComp(args.Entity))
        {
            _popups.Note(args.User, "medicine-care-dead");
            return;
        }
        var ledger = _bodies.Ledger(args.Entity);
        if (!treats.Kinds.Any(kind => ledger.TryGetValue(kind, out var amount) && amount > 0))
        {
            _popups.Note(args.User, "medicine-care-nothing");
            return;
        }
        if (_doAfters.TryStart(args.User, treats.Secs, Breaks.All) is not { } windup)
        {
            _popups.Note(args.User, "medicine-care-busy");
            return;
        }
        AddComp(windup, new CaringComponent { Item = args.Item, Patient = args.Entity });
    }

    // Landing takes the care off the ledger, spends a use, and holds a downed
    // patient's tick; the last use takes the item with it
    void OnFinished(Entity<CaringComponent> windup, ref DoAfterFinished args)
    {
        if (args.Cancelled)
        {
            return;
        }
        var (item, patient) = (windup.Comp.Item, windup.Comp.Patient);
        if (!TryComp<TreatsComponent>(item, out var treats) || _dead.HasComp(patient))
        {
            return;
        }
        // The windup breaks on the active hand changing, not on the item leaving
        // it, and the patient may have been dragged off: both are checked again
        if (!HeldBy(item, args.Actor) || !InReach(args.Actor, patient))
        {
            return;
        }
        RaiseLocalEvent(new HealDamage(patient, treats.Kinds, treats.Amount));
        if (_critical.HasComp(patient))
        {
            AddComp(patient, new Stabilised());
        }
        treats.Uses = treats.Uses == 0 ? 0 : treats.Uses - 1;
        if (treats.Uses == 0)
        {
            QueueDel(item);
        }
    }

    bool HeldBy(EntityUid item, EntityUid actor) =>
        TryComp<Stored>(item, out var stored) && TryComp<HandSlot>(stored.Container, out var hand) && hand.Owner == actor;

    bool InReach(EntityUid actor, EntityUid patient)
    {
        if (actor == patient)
        {
            return true;
        }
        return TryComp<TilePos>(actor, out var at) && TryComp<TilePos>(patient, out var there)
            && at.Map == there.Map && Math.Max(Math.Abs(at.Pos.X - there.Pos.X), Math.Abs(at.Pos.Y - there.Pos.Y)) <= 1;
    }
}
