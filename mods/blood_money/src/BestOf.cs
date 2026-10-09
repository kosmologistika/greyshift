using Rs13.Mods;
using static TypedModuleWorld.wit.Imports.rs13.mods.v0_1_0.ITypedEcsImports;

// Match wins so far, and how many matches have finished: draws count as
// played, or a drawn series would never end. Lives host-side on the rule
// entity as the module's own component
class Series
{
    public Dictionary<ulong, uint> Wins { get; set; } = [];
    public uint Played { get; set; }
    public ulong Board { get; set; }
}

// Best-of series: most match wins take the round, coins earned break ties
static class BestOf
{
    public static void Setup(ModApp app)
    {
        app.RegisterComponent<Series>();
        app.Observe<RuleStarted>(OnRuleStarted);
        app.Observe<ScopeEnding>(OnMatchEnding);
    }

    // A fresh round starts a fresh series and a fresh earned tally: the
    // tiebreak must not remember last round's scrounging
    static void OnRuleStarted(RuleStarted on, World world)
    {
        if (world.GetOwn<BestOfRule>(on.entity) is null)
        {
            return;
        }
        world.Trigger(new ClearEarned());
        var board = world.Spawn(world.Bundle(Board([])), world.Bundle(new ChildOf(on.entity)), world.Bundle(new Replicated()));
        world.InsertOwn(on.entity, new Series { Board = board });
    }

    // Credits each match's winner, and after the fixed number of matches ends
    // the round: most match wins takes it, total coins earned across the round
    // breaks ties. A match you did not win still was not wasted
    static void OnMatchEnding(ScopeEnding on, World world)
    {
        if (world.Get<ChildOf>(on.entity) is not { } ending)
        {
            return;
        }
        ulong? winner = on.outcome.places.Count > 0 ? on.outcome.places[0].player : null;

        foreach (var (entity, rule) in world.QueryOwn<BestOfRule>().ToList())
        {
            if (world.Get<ChildOf>(entity) is not { } childOf)
            {
                continue;
            }
            var scope = childOf.Value;
            if (ending.Value != scope || world.Get<ScopeEnded>(scope) is not null || world.GetOwn<Series>(entity) is not { } series)
            {
                continue;
            }
            foreach (var (_, session, _) in world.Query<SessionPlayer, Participant>())
            {
                series.Wins.TryAdd(session.Value, 0);
            }
            if (winner is { } w)
            {
                series.Wins[w] = series.Wins.GetValueOrDefault(w) + 1;
            }
            series.Played += 1;

            var ranking = Ranking(series.Wins, world);
            var rows = ranking.Select(place => new ScoreRow(place.name, place.score, winner == place.player, place.player)).ToList();
            world.Insert(series.Board, Board(rows));

            if (series.Played >= rule.Matches)
            {
                world.Trigger(new EndScope(scope, new ScopeOutcome("gamemode-round-won", ranking)));
            }
            world.InsertOwn(entity, series);
        }
    }

    static List<OutcomePlace> Ranking(Dictionary<ulong, uint> wins, World world)
    {
        var records = world.Query<PlayerRecord, Wallet>().ToList();
        var places = wins.Select(entry =>
        {
            var (_, record, wallet) = records.FirstOrDefault(row => row.a.id == entry.Key);
            return (player: entry.Key, wins: entry.Value, earned: wallet.earned, name: record.username ?? "");
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

    static HudScoreboard Board(List<ScoreRow> rows) => new(HudZone.TOP_LEFT, "gamemode-scoreboard-series", rows);
}
