using Rs13.Legacy;
using static TypedModuleWorld.wit.Imports.rs13.mods.v0_1_0.ITypedEcsImports;

// When the match times out, and the live board this rule keeps current
class Race
{
    public ulong EndsAtMs { get; set; }
    public ulong Board { get; set; }
}

// Coin target: the first wallet at the target takes the match, the timer
// running out hands it to the richest
static class CoinTarget
{
    public static void Setup(ModApp app)
    {
        app.RegisterComponent<Race>();
        app.Observe<RuleStarted>(OnRuleStarted);
        app.Every(TimeSpan.FromMilliseconds(100), WatchTheRace);
    }

    static void OnRuleStarted(RuleStarted on, World world)
    {
        if (world.GetOwn<CoinTargetRule>(on.entity) is not { } rule)
        {
            return;
        }
        var endsAt = world.Now + TimeSpan.FromSeconds(rule.Secs);
        var board = world.Spawn(world.Bundle(Board([])), world.Bundle(new ChildOf(on.entity)), world.Bundle(new Replicated()));
        world.Spawn(
            world.Bundle(new HudBanner(HudZone.TOP_CENTER, "gamemode-match-timer", "", new Deadline(Durations.From(endsAt)))),
            world.Bundle(new ChildOf(on.entity)),
            world.Bundle(new Replicated()));
        world.InsertOwn(on.entity, new Race { EndsAtMs = (ulong)endsAt.TotalMilliseconds, Board = board });
    }

    // Balance is the score. The first wallet at the target ends the match on
    // the next sample, a tenth of a second at most; the timer running out
    // hands it to the richest, and an exact tie at the top of a timeout is a
    // draw that credits nobody
    static void WatchTheRace(World world)
    {
        if (!world.RoundRunning)
        {
            return;
        }
        var now = (ulong)world.Now.TotalMilliseconds;
        foreach (var (entity, rule) in world.QueryOwn<CoinTargetRule>().ToList())
        {
            if (world.Get<ChildOf>(entity) is not { } childOf)
            {
                continue;
            }
            var scope = childOf.Value;
            // A rule outlives the end of its scope by a frame, and must not act in it
            if (world.Get<ScopeEnded>(scope) is not null || world.GetOwn<Race>(entity) is not { } race)
            {
                continue;
            }
            var records = world.Query<PlayerRecord, Wallet>().ToList();
            string Name(ulong player) => records.FirstOrDefault(row => row.a.id == player).a.username ?? "";

            var standing = world.Query<SessionPlayer, Participant>()
                .Select(row => (player: row.a.Value, balance: records.FirstOrDefault(r => r.a.id == row.a.Value).b.balance))
                .ToList();
            standing.Sort((a, b) =>
            {
                var byBalance = b.balance.CompareTo(a.balance);
                return byBalance != 0 ? byBalance : string.CompareOrdinal(Name(a.player), Name(b.player));
            });

            var leader = standing.Count > 0 ? standing[0] : ((ulong player, uint balance)?)null;
            var rows = standing.Select(s => new ScoreRow(Name(s.player), (int)s.balance, leader?.player == s.player, s.player)).ToList();
            if (world.Get<HudScoreboard>(race.Board) is { } hud && !SameRows(hud.rows, rows))
            {
                world.Mutate(race.Board, Board(rows));
            }

            var reached = leader is { } lead && lead.balance >= rule.Target;
            var timedOut = now >= race.EndsAtMs;
            if (!reached && !timedOut)
            {
                continue;
            }
            world.Trigger(new EndScope(scope, RaceOutcome(standing, reached, Name)));
        }
    }

    static ScopeOutcome RaceOutcome(List<(ulong player, uint balance)> standing, bool reached, Func<ulong, string> name)
    {
        var topTied = !reached && standing.Count >= 2 && standing[0].balance == standing[1].balance;
        if (standing.Count == 0 || topTied)
        {
            return new ScopeOutcome("gamemode-match-draw", []);
        }
        var places = standing.Select(s => new OutcomePlace(s.player, name(s.player), (int)s.balance)).ToList();
        return new ScopeOutcome("gamemode-match-won", places);
    }

    static bool SameRows(List<ScoreRow> a, List<ScoreRow> b) =>
        a.Count == b.Count && a.Zip(b).All(pair =>
            pair.First.name == pair.Second.name && pair.First.score == pair.Second.score
            && pair.First.highlight == pair.Second.highlight && pair.First.player == pair.Second.player);


    static HudScoreboard Board(List<ScoreRow> rows) => new(HudZone.TOP_RIGHT, "gamemode-scoreboard-match", rows);
}
