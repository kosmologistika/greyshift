// Best-of series: most match wins take the round, coins earned break ties

use std::collections::BTreeMap;

use serde::{Deserialize, Serialize};

use rs13_mod_sdk::*;

pub fn setup(app: &mut ModApp) {
    app.register_component::<Series>();
    app.observe(on_rule_started);
    app.observe(on_match_ending);
}

/// Match wins so far, and how many matches have finished - draws count as
/// played, or a drawn series would never end
#[derive(Serialize, Deserialize)]
struct Series {
    wins: BTreeMap<PlayerId, u32>,
    played: u32,
    board: Entity,
}

impl Type for Series {
    const PATH: &'static str = "blood_money::Series";
}

/// A fresh round starts a fresh series and a fresh earned tally - the
/// tiebreak must not remember last round's scrounging
fn on_rule_started(on: RuleStarted, world: &mut World) {
    if world.get::<BestOfRule>(on.entity).is_none() {
        return;
    }
    world.trigger(ClearEarned);
    let board = world.spawn((board(Vec::new()), ChildOf(on.entity), Replicated));
    world.insert(on.entity, Series { wins: BTreeMap::new(), played: 0, board });
}

/// Credits each match's winner, and after the fixed number of matches ends
/// the round: most match wins takes it, total coins earned across the round
/// breaks ties. A match you did not win still was not wasted
fn on_match_ending(on: ScopeEnding, world: &mut World) {
    let Some(ChildOf(ending_parent)) = world.get(on.entity) else {
        return;
    };
    let winner = on.outcome.places.first().map(|place| place.player);

    for (rule_entity, (rule, ChildOf(scope), mut series)) in world.query::<(BestOfRule, ChildOf, Series)>() {
        if ending_parent != scope || world.get::<ScopeEnded>(scope).is_some() {
            continue;
        }

        for (_, (session, _)) in world.query::<(SessionPlayer, Participant)>() {
            series.wins.entry(session.0).or_default();
        }
        if let Some(winner) = winner {
            *series.wins.entry(winner).or_default() += 1;
        }
        series.played += 1;

        let ranking = series_ranking(&series.wins, world);
        let rows = ranking
            .iter()
            .map(|place| ScoreRow {
                name: place.name.clone(),
                score: place.score,
                highlight: winner == Some(place.player),
                player: place.player,
            })
            .collect();
        world.insert(series.board, board(rows));

        if series.played >= rule.matches {
            let outcome = ScopeOutcome { headline: "gamemode-round-won".into(), places: ranking };
            world.trigger(EndScope { entity: scope, outcome });
        }
        world.insert(rule_entity, series);
    }
}

fn series_ranking(wins: &BTreeMap<PlayerId, u32>, world: &World) -> Vec<OutcomePlace> {
    let records = world.query::<(PlayerRecord, Wallet)>();
    let mut places: Vec<(PlayerId, u32, u32, String)> = wins
        .iter()
        .map(|(player, wins)| {
            let (name, earned) = records
                .iter()
                .find(|(_, (record, _))| record.id == *player)
                .map(|(_, (record, wallet))| (record.username.clone(), wallet.earned))
                .unwrap_or_default();
            (*player, *wins, earned, name)
        })
        .collect();
    places.sort_by(|a, b| b.1.cmp(&a.1).then_with(|| b.2.cmp(&a.2)).then_with(|| a.3.cmp(&b.3)));
    places
        .into_iter()
        .map(|(player, wins, _, name)| OutcomePlace { player, name, score: wins as i32 })
        .collect()
}

fn board(rows: Vec<ScoreRow>) -> HudScoreboard {
    HudScoreboard { zone: HudZone::TopLeft, title: "gamemode-scoreboard-series".into(), rows }
}
