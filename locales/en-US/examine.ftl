examine-tab-overview = Overview
examine-tab-health = Health
examine-tab-armor = Armor
examine-tab-weapon = Weapon
examine-door-open = It is open.
examine-door-opening = It is sliding open.
examine-door-closing = It is sliding shut.
examine-door-shut = It is shut.
examine-door-bolted = The bolts are down.
examine-door-emergency = The emergency access light is lit.
examine-door-unsafe = Its safety sensor is dead.
examine-armor-covers = Covers the { $zone ->
    [torso] torso
    [head] head
    [arm] arms
   *[leg] legs
    }.
examine-armor-blocks = Blocks { $percent }% of { $type ->
    [blunt] blunt
    [slash] slash
    [piercing] piercing
   *[heat] heat
    } damage.
examine-armor-reflects = Turns back { $percent }% of energy bolts.
examine-wall-mounted = Something is bolted to it.
examine-body-identity = { $sex ->
    [female] She
    [male] He
   *[none] It
    } is { $band ->
    [young] a young
    [middle] a middle-aged
    [old] an old
   *[unknown] a
    } { $species ->
   *[human] human
    }.
examine-body-dead = { $sex ->
    [female] She
    [male] He
   *[none] It
    } is dead.
examine-body-ssd = { $sex ->
    [female] She has a blank, absent-minded stare and appears completely unresponsive to anything. She
    [male] He has a blank, absent-minded stare and appears completely unresponsive to anything. He
   *[none] It has a blank, absent-minded stare and appears completely unresponsive to anything. It
    } may snap out of it soon.
examine-body-holding = { $sex ->
    [female] She is holding { $item } in her
    [male] He is holding { $item } in his
   *[none] It is holding { $item } in its
    } { $hand ->
    [left] left
   *[right] right
    } hand.
examine-body-missing = { $sex ->
    [female] Her
    [male] His
   *[none] Its
    } { $side ->
    [left] left
    [right] right
   *[none] { "" }
    } { $zone ->
    [head] head
    [arm] arm
   *[leg] leg
    } is missing!
examine-health-too-far = You would need a closer look.
examine-health-clean = No visible wounds.
examine-health-wound = { $sex ->
    [female] She
    [male] He
   *[none] It
    } has { $band ->
    [minor] minor
    [moderate] moderate
   *[severe] severe
    } { $type ->
    [blunt] bruising
    [slash] cuts
    [piercing] punctures
   *[heat] burns
    }.
examine-health-mangled = { $sex ->
    [female] Her
    [male] His
   *[none] Its
    } { $part ->
    [head] head
    [left-arm] left arm
    [right-arm] right arm
    [left-leg] left leg
   *[right-leg] right leg
    } is badly mangled.
examine-item-size = It is { $size ->
    [tiny] a tiny
    [small] a small
    [medium] a medium-sized
   *[large] a large
    } item.
examine-weapon-hit = Hits for { $amount } { $type ->
    [blunt] blunt
    [slash] slash
    [piercing] piercing
   *[heat] heat
    } damage.
examine-weapon-swings = { $rate } swings per second.
examine-weapon-fire-rate = Fires { $rate } shots per second.
examine-weapon-mode = Set to { $mode ->
    [semi] semi-auto
    [auto] full auto
   *[burst] { $count }-round burst
    }{ $modes ->
    [one] { "" }
   *[other] , one of { $modes } modes
    }.
examine-weapon-shot = Each shot deals { $amount } { $type ->
    [blunt] blunt
    [slash] slash
    [piercing] piercing
   *[heat] heat
    } damage{ $pellets ->
    [one] { "" }
   *[other] , in { $pellets } pellets
    }.
examine-weapon-shots = It holds { $shots } of { $capacity } shots.
examine-weapon-empty = It is not loaded.
examine-weapon-shots-too-far = You would need a closer look to count its shots.
