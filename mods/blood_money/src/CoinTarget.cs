using Rs13.Mods;

namespace BloodMoney;

// When the match times out, and the live board this rule keeps current
[RegisterComponent]
public sealed partial class RaceComponent : Component
{
    [DataField] public Deadline EndsAt;
    [DataField] public EntityUid Board;
}

// Coin target: the first wallet at the target takes the match, the timer
// running out hands it to the richest
public sealed partial class CoinTargetRuleSystem : GameRuleSystem<CoinTargetRuleComponent>
{
    [Dependency] WalletSystem _wallets = default!;

    public override void Initialize()
    {
        base.Initialize();
        EveryActive(TimeSpan.FromMilliseconds(100), WatchTheRace);
    }

    protected override void Started(Entity<CoinTargetRuleComponent> rule, ref RuleStarted args)
    {
        var endsAt = new Deadline(Now + TimeSpan.FromSeconds(rule.Comp.Secs));
        var board = Spawn(Board([]), new ChildOf(rule), new Replicated());
        Spawn(new HudBanner(HudZone.TopCenter, "gamemode-match-timer", "", endsAt), new ChildOf(rule), new Replicated());
        AddComp(rule, new RaceComponent { EndsAt = endsAt, Board = board });
    }

    // Balance is the score. The first wallet at the target ends the match on
    // the next sample, a tenth of a second at most; the timer running out
    // hands it to the richest, and an exact tie at the top of a timeout is a
    // draw that credits nobody
    void WatchTheRace(Entity<CoinTargetRuleComponent> rule, EntityUid scope)
    {
        if (!TryComp<RaceComponent>(rule, out var race))
        {
            return;
        }
        var wallets = _wallets.ByPlayer();
        string Name(PlayerId player) => wallets.TryGetValue(player, out var found) ? found.Comp1.Username : "";

        var standing = new List<(PlayerId player, uint balance)>();
        var sessions = EntityQueryEnumerator<SessionPlayer, Participant>();
        while (sessions.MoveNext(out _, out var session, out _))
        {
            standing.Add((session.Value, wallets.TryGetValue(session.Value, out var found) ? found.Comp2.Balance : 0u));
        }
        standing.Sort((a, b) =>
        {
            var byBalance = b.balance.CompareTo(a.balance);
            return byBalance != 0 ? byBalance : string.CompareOrdinal(Name(a.player), Name(b.player));
        });

        var leader = standing.Count > 0 ? standing[0] : ((PlayerId player, uint balance)?)null;
        var rows = standing.Select(s => new ScoreRow(Name(s.player), (int)s.balance, leader?.player == s.player, s.player)).ToList();
        if (TryComp<HudScoreboard>(race.Board, out var hud) && !SameRows(hud.Rows, rows))
        {
            hud.Rows = rows;
        }

        var reached = leader is { } lead && lead.balance >= rule.Comp.Target;
        var timedOut = Now >= race.EndsAt.EndsAt;
        if (!reached && !timedOut)
        {
            return;
        }
        RaiseLocalEvent(new EndScope(scope, RaceOutcome(standing, reached, Name)));
    }

    static ScopeOutcome RaceOutcome(List<(PlayerId player, uint balance)> standing, bool reached, Func<PlayerId, string> name)
    {
        var topTied = !reached && standing.Count >= 2 && standing[0].balance == standing[1].balance;
        if (standing.Count == 0 || topTied)
        {
            return new ScopeOutcome("gamemode-match-draw", []);
        }
        var places = standing.Select(s => new OutcomePlace(s.player, name(s.player), (int)s.balance)).ToList();
        return new ScopeOutcome("gamemode-match-won", places);
    }

    static bool SameRows(IReadOnlyList<ScoreRow> a, IReadOnlyList<ScoreRow> b) =>
        a.Count == b.Count && a.Zip(b).All(pair => pair.First == pair.Second);

    static HudScoreboard Board(List<ScoreRow> rows) => new(HudZone.TopRight, "gamemode-scoreboard-match", rows);
}
