using Rs13.Packs;

namespace BloodMoney;

// Match wins so far, and how many matches have finished: draws count as
// played, or a drawn series would never end
[RegisterComponent]
public sealed partial class SeriesComponent : Component
{
    [DataField] public Dictionary<PlayerId, uint> Wins = [];
    [DataField] public uint Played;
    [DataField] public EntityUid Board;
}

// Best-of series: most match wins take the round, coins earned break ties
public sealed partial class BestOfRuleSystem : GameRuleSystem<BestOfRuleComponent>
{
    [Dependency] WalletSystem _wallets = default!;

    // A fresh round starts a fresh series and a fresh earned tally: the
    // tiebreak must not remember last round's scrounging
    protected override void Started(Entity<BestOfRuleComponent> rule, ref RuleStarted args)
    {
        RaiseLocalEvent(new ClearEarned());
        var board = Spawn(Board([]), new ChildOf(rule), new Replicated());
        AddComp(rule, new SeriesComponent { Board = board });
    }

    // Credits each match's winner, and after the fixed number of matches ends
    // the round: most match wins takes it, total coins earned across the round
    // breaks ties. A match you did not win still was not wasted
    protected override void NestedScopeEnding(Entity<BestOfRuleComponent> rule, EntityUid scope, ref ScopeEnding args)
    {
        if (!TryComp<SeriesComponent>(rule, out var series))
        {
            return;
        }
        PlayerId? winner = args.Outcome.Places.Count > 0 ? args.Outcome.Places[0].Player : null;

        var sessions = EntityQueryEnumerator<SessionPlayer, Participant>();
        while (sessions.MoveNext(out _, out var session, out _))
        {
            series.Wins.TryAdd(session.Value, 0);
        }
        if (winner is { } w)
        {
            series.Wins[w] = series.Wins.GetValueOrDefault(w) + 1;
        }
        series.Played += 1;

        var ranking = Ranking(series.Wins);
        var rows = ranking.Select(place => new ScoreRow(place.Name, place.Score, place.Player == winner, place.Player)).ToList();
        AddComp(series.Board, Board(rows));

        if (series.Played >= rule.Comp.Matches)
        {
            RaiseLocalEvent(new EndScope(scope, new ScopeOutcome("gamemode-round-won", ranking)));
        }
    }

    List<OutcomePlace> Ranking(Dictionary<PlayerId, uint> wins)
    {
        var wallets = _wallets.ByPlayer();
        var places = wins.Select(entry =>
        {
            var found = wallets.TryGetValue(entry.Key, out var record);
            return (player: entry.Key, wins: entry.Value, earned: found ? record.Comp2.Earned : 0u, name: found ? record.Comp1.Username : "");
        }).ToList();
        places.Sort((a, b) =>
        {
            var byWins = b.wins.CompareTo(a.wins);
            if (byWins != 0) return byWins;
            var byEarned = b.earned.CompareTo(a.earned);
            return byEarned != 0 ? byEarned : string.CompareOrdinal(a.name, b.name);
        });
        return places.Select(p => new OutcomePlace(p.player, p.name, (int)p.wins)).ToList();
    }

    static HudScoreboard Board(List<ScoreRow> rows) => new(HudZone.TopLeft, "gamemode-scoreboard-series", rows);
}
