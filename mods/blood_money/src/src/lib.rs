// Blood Money: balance is the score. Hits knock coins out of the victim onto
// the floor, anyone walks over them, and buying your way out of the death
// shop is the only way back. Design: docs/design/blood_money.md

use rs13_mod_sdk::*;

mod best_of;
mod coin_target;

module!(setup);

fn setup(app: &mut ModApp) {
    best_of::setup(app);
    coin_target::setup(app);
}
