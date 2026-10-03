## player.yaml
notification-victory = Victory! All enemy units and buildings have been destroyed.
notification-defeat = Defeat! All your units and buildings have been destroyed.
notification-rain-start = It started raining. Potatoes grow faster.
notification-rain-stop = The rain has stopped.
notification-night = Night falls. Units see less far.
notification-day = Day breaks.
options-game-speed-test = Test (5x, AI matches)

bot-korean =
    .name = Korean General

bot-japanese =
    .name = Japanese General

## world.yaml
faction-random =
    .name = Random
    .description = Random Faction
     A random faction will be chosen when the game starts.

faction-korea =
    .name = Korea
    .description = Seven Years War Korea
     A work-in-progress Korea faction rebuilt from the original Seven Years War assets.

faction-japan =
    .name = Japan
    .description = Seven Years War Japan
     Guns, melee cavalry, bombers and hidden submarines, rebuilt from the original Seven Years War assets.

## infantry.yaml
actor-peasant =
    .name = Peasant

actor-archer =
    .name = Archer

actor-gunner =
    .name = Gunner

actor-footman =
    .name = Footman

## structures.yaml
actor-hq =
    .name = Headquarters

actor-barracks =
    .name = Barracks

actor-mill =
    .name = Mill

actor-beaconmound =
    .name = Beacon Mound

resource-potato = Potatoes
resource-rice = Rice

actor-firecar =
    .name = Fire Car

actor-heavyarmsworkshop =
    .name = Heavy Arms Workshop

actor-cannon =
    .name = Cannon

actor-planeworks =
    .name = Planeworks

actor-barracks2 =
    .name = Barracks2

actor-fighter =
    .name = Fighter

actor-transporter =
    .name = Transporter

label-transport-cargo-title = Passengers ({ $used }/{ $capacity })
label-transport-cargo-empty = No units aboard

actor-temple =
    .name = Temple

actor-monk =
    .name = Monk

actor-shamanhouse =
    .name = Shaman's House

actor-shaman =
    .name = Shaman

spell-mana = Mana: { $current } / { $maximum }
spell-heal = Heal ({ $cost } mana)
spell-mass-heal = Mass Heal: +{ $amount } HP to allies within { $range } cells (needs full mana, uses all)
spell-disturb = Disturb: reveal invisible enemies within { $range } cells for { $seconds } s ({ $cost } mana)
spell-lightning = Lightning ({ $cost } mana)
spell-transform = Transform a Peasant into random infantry ({ $cost } mana)
spell-select-peasant = Select one of your Peasants
spell-select-target = Select a unit
spell-low-mana = Not enough mana
spell-cooldown = Recharging
spell-ready = Ready to cast
spell-cancel = Right-click to cancel

selection-stats-count = { $count } selected
selection-stats-health = HP { $hp } / { $max }
selection-stats-attack = Attack { $damage }  Range { $range }
selection-stats-armor = Armor { $armor }
selection-stats-defense = Defense { $defense }%
selection-stats-speed = Speed { $speed }
selection-stats-cost = Cost { $cost }
selection-stats-mana = Mana { $mana } / { $max }
selection-stats-sell = Sell for ${ $refund }
selection-stats-repair = Repair: +{ $hp } HP for ${ $cost } per second until healed (click again to stop)

actor-stable =
    .name = Stable

actor-commander =
    .name = Commander

actor-occupier =
    .name = Occupier

actor-miner =
    .name = Miner
actor-syw-mine =
    .name = Mine
actor-resource-chest =
    .name = Chest
spell-mine-single = Mine at the Miner's feet ({ $mana } mana, ${ $cost })
spell-lay-mine = Minefield: { $count } mines in a line ({ $mana } mana, ${ $cost })
spell-mine-ready = Ready to lay mines
spell-mine-unavailable = Not enough mana or credits
spell-mine-place = Click where to lay the line
spell-mine-rotate = Tab / mouse wheel: rotate, right-click: cancel

actor-arrowtower =
    .name = Arrow Tower
actor-cannontower =
    .name = Cannon Tower

actor-bull =
    .name = Bull

## japan.yaml (Japanese actors with the same name as their Korean counterpart reuse its string)
actor-general =
    .name = General
actor-thief =
    .name = Thief
actor-priest =
    .name = Priest
actor-witch =
    .name = Witch
actor-armoredcar =
    .name = Armored Car
actor-bomber =
    .name = Bomber
actor-arrowship =
    .name = Arrow Ship
actor-submarine =
    .name = Submarine
actor-torpedo =
    .name = Torpedo
actor-jbeacon =
    .name = Beacon
actor-jheavyarms =
    .name = Heavy Arms
actor-airport =
    .name = Airport
actor-witchhouse =
    .name = Witch House
spell-earthquake = Earthquake: heavy damage to the 5x5 area at a ground target ({ $cost } mana)
spell-bewilder = Bewilderment: an enemy unit joins you ({ $chance }% chance, { $cost } mana)
spell-detect-mines = Detect mine: clear all mines within { $range } cells ({ $cost } mana)
spell-select-ground = Select a location
spell-select-enemy = Select an enemy unit

actor-shipyard =
    .name = Shipyard

actor-patrolship =
    .name = Patrol Ship

actor-cannonship =
    .name = Cannon Ship

actor-transportship =
    .name = Transport Ship
