// Coin target: the first wallet at the target takes the match, the timer
// running out hands it to the richest

use std::time::Duration;

use serde::{Deserialize, Serialize};

use rs13_mod_sdk::*;

pub fn setup(app: &mut ModApp) {
    app.register_component::<Race>();
    app.observe(on_rule_started);
    app.every(Duration::from_millis(100), watch_the_race);
}

/// When the match times out, and the live board this rule keeps current
#[derive(Serialize, Deserialize)]
struct Race {
    expiry: Deadline,
    board: Entity,
}

impl Type for Race {
    const PATH: &'static str = "blood_money::Race";
}

fn on_rule_started(on: RuleStarted, world: &mut World) {
    let Some(rule) = world.get::<CoinTargetRule>(on.entity) else {
        return;
    };
    let expiry = Deadline { ends_at: world.now() + Duration::from_secs(rule.secs) };
    let board = world.spawn((
        HudScoreboard { zone: HudZone::TopRight, title: "gamemode-scoreboard-match".into(), rows: Vec::new() },
        ChildOf(on.entity),
        Replicated,
    ));
    world.spawn((
        HudBanner { zone: HudZone::TopCenter, title: "gamemode-match-timer".into(), subtitle: String::new(), countdown: Some(expiry) },
        ChildOf(on.entity),
        Replicated,
    ));
    world.insert(on.entity, Race { expiry, board });
}

/// Balance is the score. The first wallet at the target ends the match on
/// the next sample, a tenth of a second at most; the timer running out hands
/// it to the richest, and an exact tie at the top of a timeout is a draw that
/// credits nobody
fn watch_the_race(world: &mut World) {
    let now = world.now();
    for (_, (rule, ChildOf(scope), race)) in world.query::<(CoinTargetRule, ChildOf, Race)>() {
        // A rule outlives the end of its scope by a frame, and must not act in it
        if world.get::<ScopeEnded>(scope).is_some() {
            continue;
        }
        let records = world.query::<(PlayerRecord, Wallet)>();
        let name = |player: PlayerId| records.iter().find(|(_, (record, _))| record.id == player).map(|(_, (record, _))| record.username.clone()).unwrap_or_default();

        let mut standing: Vec<(PlayerId, u32)> = world
            .query::<(SessionPlayer, Participant)>()
            .into_iter()
            .map(|(_, (SessionPlayer(player), _))| {
                let balance = records.iter().find(|(_, (record, _))| record.id == player).map_or(0, |(_, (_, wallet))| wallet.balance);
                (player, balance)
            })
            .collect();
        standing.sort_by(|a, b| b.1.cmp(&a.1).then_with(|| name(a.0).cmp(&name(b.0))));

        let leader = standing.first().copied();
        let rows: Vec<ScoreRow> = standing
            .iter()
            .map(|(player, balance)| ScoreRow {
                name: name(*player),
                score: *balance as i32,
                highlight: leader.is_some_and(|(lead, _)| lead == *player),
                player: *player,
            })
            .collect();
        if world.get::<HudScoreboard>(race.board).is_some_and(|hud| hud.rows != rows) {
            world.mutate(race.board, HudScoreboard { zone: HudZone::TopRight, title: "gamemode-scoreboard-match".into(), rows });
        }

        let reached = leader.is_some_and(|(_, balance)| balance >= rule.target);
        let timed_out = now >= race.expiry.ends_at;
        if !reached && !timed_out {
            continue;
        }
        world.trigger(EndScope { entity: scope, outcome: race_outcome(&standing, reached, &name) });
    }
}

fn race_outcome(standing: &[(PlayerId, u32)], reached: bool, name: &dyn Fn(PlayerId) -> String) -> ScopeOutcome {
    let top_tied = !reached && standing.len() >= 2 && standing[0].1 == standing[1].1;
    if standing.is_empty() || top_tied {
        return ScopeOutcome { headline: "gamemode-match-draw".into(), places: Vec::new() };
    }
    let places = standing.iter().map(|(player, balance)| OutcomePlace { player: *player, name: name(*player), score: *balance as i32 }).collect();
    ScopeOutcome { headline: "gamemode-match-won".into(), places }
}
