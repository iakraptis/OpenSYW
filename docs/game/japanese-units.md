# Japanese faction: units, buildings and stats

Recovered from `syw.exe` with the same method as [KoreanUnitStats.md](korean-units.md): constructor defaults (with inherited assignments), the production dispatchers' faction branches, the cost setter `0x40a7c0`, vtables and attack methods. Field meanings follow [CombatFindings.md](combat.md). Values are raw engine numbers. The mod's Korean scale is HP and power x100, then HP -10% and -15%; reload is cooldown x4/3 ticks.

Reproduce: `python -I research/tools/ghidra/extract_japanese_defaults.py` writes [JapaneseUnitDefaults.json](data/japanese-unit-defaults.json), with per-field source addresses. Extra decompiles from this pass are in `research/ghidra/out/japan` and `japan-audit` (gitignored).

## How the faction split works

- Every unit and building object has a faction field at `+0xfc`: 0 is Korea, 1 is Japan. Each production dispatcher (`Barrack::Make` `0x44f560`, `Hq::Make` `0x438bc0`, `_HeavyMaker::Make` `0x446ee0`, Barracks2 `0x44c3a0`, air `0x428e40`, `Temple::Make` `0x445520`, Stable `0x433d20`, `Shipyard::Make` `0x446260`) branches on the building's faction and calls a Korean or a Japanese constructor.
- **Costs** come from one table keyed by **type ID and faction** (`0x40a7c0(type, faction, tier, tier, cost, descriptor)`), so `cost = table[type*2 + faction]`.
- **Portraits:** Japanese units use PORTRAIT frames 50-69 (mostly Korean + 50) and buildings 70-83 (Korean + 40). All are present in `PORTRAIT.SPR`.
- **STG placement IDs 1-35 are these type IDs.** The stage spawner `0x40ed10` switches on the placement kind and calls the same allocators, so maps can place units and buildings of either faction.

## Units

| Type | Japanese unit | Cost J (K) | HP | Power | Def % | Attack / target class | Cooldown | Range (+0x78/+0x7c) | Portrait | Constructor |
|---:|---|---|---:|---:|---:|---|---:|---|---:|---|
| 16 | Peasant | 700 (700) | 250 | 0 | 20 | 0 / 1 | 5 | 0 / 3 | 50 | `0x42f390` |
| 34 | Bull | 1,400 (1,400) | 250 | 0 | 20 | 0 / 1 | 5 | 0 / 3 | 68 | `0x42f760` |
| 19 | Footman | 400 (400) | 350 | 45 | 30 | 5 / 1 | 5 | 1 / 4 | 57 | `0x438880` |
| 17 | Archer | 400 (400) | 250 | 35 | 20 | 2 / 1 | 20 | 5 / 5 | 51 | `0x429c80` |
| 18 | **Gunner** | 700 (700) | 300 | **60** (K 50) | 30 | 1 / 1 | 30 | **6 / 6** (K 5) | 52 | `0x44c110` |
| 20 | **Armored Car** | **1,300** (Fire Car 1,200) | 500 | **60** (K 50) | **50** (K 40) | **1** (K 3) / 3 | **25** (K 70) | 7 / 7 | 53 | `0x44edf0` |
| 32 | Cannon | **1,600** (1,400) | 450 | 80 | 30 | 4 / 3 | 110 | 10 / 10 | 14 (Korean) | `0x44f0e0` (shared) |
| 33 | **General** | 900 (900) | 350 | **60** (K 50) | 40 | **5 melee** (K 2) / 2 | **15** (K 18) | **1 / 4** (K 6) | 67 | `0x443ad0` |
| 24 | Thief (Occupier) | 800 (800) | 150 | 0 | 20 | 0 / 1 | - | 0 / 3 | 58 | `0x42c8e0` |
| 29 | Priest (Monk) | 900 (900) | 300 | 60 | 20 | 6 / 1 | 25 | 5 / 5 | 64 | `0x440180` |
| 30 | Witch (Shaman) | 800 (800) | 300 | 60 | 20 | 6 / 1 | 25 | 5 / 5 | 65 | `0x440300` |
| 26 | Transporter | **1,600** (1,500) | 500 | 0 | 50 | 0 / 6 | - | 0 / 5 | 61 | `0x43ca60` |
| 27 | **Fighter** | **2,200** (2,000) | 300 | 50 | 40 | 4 / 6 | **60** (K 90) | 6 / 6 | 62 | `0x43d130` |
| 28 | **Bomber** (Japan only) | 1,700 | 400 | 90 | 45 | 4 / 6 | 100 | 0 / 5 | 63 | `0x43cba0` |
| 21 | Cannon Ship | **1,300** (2,000) | 600 | 75 | 60 | 4 / 4 | 110 | 8 / 8 | 54 | `0x437c90` |
| 22 | **Transport Ship** | 1,000 (1,000) | 700 | 0 | 50 | unarmed / 4 | - | 0 / 5 | 55 | `0x43e040` |
| 23 | **Arrow Ship** | **700** (1,300) | 600 | 50 | 50 | 1 (arrows) / 4 | 35 | 7 / 7 | 56 | `0x437f50` |
| 31 | **Submarine** (Japan only) | 500 | **50** | 0 (blast 1,500) | 10 | 4 / 4 | 150 | 1 / 1 | 66 | `0x436ea0` |
| 35 | Lord (campaign only) | 2,000 | 250 | 0 | 20 | 0 / 1 | 5 | 0 / 3 | 69 | `0x4403c0` |
| 25 | Miner (unreachable) | 600 | 250 | 0 | 20 | 0 / 1 | 5 | 0 / 3 | 60 (blank) | `0x428b20` |

Bold marks differences from the Korean counterpart. Unmarked units have exactly the Korean values; only their portrait and sprite bank differ.

### Behaviour traced from attack methods (vtable slot 12)

- **Armored Car** (`0x44ef40`): a gun car, not a rocket car. It fires one Gunner bullet (`0x449e80` -> `0x4490b0`, class-1 impact `0x4491e0`) per 25-tick cooldown, from a spot one cell out in the turret direction. The turret then advances 4 of 16 facings (90°). The Korean Fire Car (`0x44eca0`) fires a 3-missile volley with random spread.
- **General** (`0x443530`, shared with the Commander): the Japanese branch is melee (see [VerificationAudit.md](verification-audit.md)). With range 1 and cooldown 15, it is a fast mounted swordsman.
- **Bomber** (`0x43cd80`): flies over the target and drops **6 bombs**, one every 6 ticks, in a 2x2 pattern under itself (offsets 10/42 px). Each bomb has power 90 and falls 70 px. Impact `0x449400` calls the class-4 3x3 splash routine `0x4488a0`, the same one as Cannon shells. Its 100-tick cooldown starts after the run. Range 0 means it must be overhead.
- **Submarine** (`0x437190`): a **suicide attacker**. When it attacks, it creates a blast at its own position with power **1,500** (`0x449db0` -> `0x4494a0`; impact `0x4494e0` -> `0x4488a0`, class-4 3x3 splash) and flags itself (`+0x268 = 1`). If the target belongs to the local player, a sound and a message play. At 50 HP and defence 10 it is fragile. The constructor also sets flag bits `+0x40 |= 0x200001` and `+0x140 |= 0x200`; whether these mean "submerged/invisible" is **not yet traced**.
- **Transport Ship** (type 22): attack slot is the no-op `0x401580`, and slot 7 is a cargo-aware draw method (`0x43e170`). It is the unarmed cargo ship.
- **Arrow Ship** (type 23): uses the shared ship method `0x4379b0`, which takes its Japanese type-23 **arrow** branch (the Korean ships take the cannon branch).
- **Priest and Witch:** they share the Monk's and Shaman's code (identical vtables, attack `0x43f1d0`), but their button lists give them **different spells**. The Priest has Heal, Mass Heal and **Detect mine** (destroys mines within 5 cells). The Witch has **Earthquake** and **Bewilderment** (takes over an enemy unit). See [CasterSpells.md](spells.md).
- **Lord (type 35):** no attack, and no building trains it. It is **Konishi**, the prisoner of mission J3: script `0x422f10` places it (player 2 at 73,54), and when the player has taken it west of x 57 (`+0x10` is the x position, not HP), six Korean Cannons spawn to chase it. See Campaign.md (opensy `SYWtoORA/docs/Campaign.md`).

### Production (which building builds what, for Japan)

| Building | Categories | Japanese result |
|---|---|---|
| HQ | `0x22`, `0x2a` | Peasant, Bull |
| Barracks | `0x23`, `0x24`, `0x2d` | Archer, Gunner, Footman (Japanese constructors) |
| Heavy Arms | `0x25`, `0x35` | Armored Car; Cannon (shared constructor) |
| Stable | `0x28` | General |
| Barracks2 | `0x2e`, `0x30` | Thief. The `0x30` branch always creates the **Korean** Miner, so a Japanese Miner is never trained; the Japanese Miner record is only reachable via STG placement |
| Airport | `0x31`, `0x32`, `0x33` | Transporter, **Bomber** (Japan only), Fighter |
| Temple | `0x29`, `0x34` | Witch, Priest |
| Shipyard | `0x26`, `0x27`, `0x2b`, `0x2c` | Cannon Ship (type 21), Transport Ship (22), Arrow Ship (23), **Submarine** (31; no faction branch) |

This resolves two earlier open questions: category `0x31` is the Transporter (1,500/1,600) and `0x33` the Fighter (2,000/2,200).

## Training time

Every production dispatcher uses the same loop:
- each call adds 1 to a counter (`+0x200`);
- when the counter reaches the step delay `+0x1fc`, the player pays cost/100 and progress (`+0x1c8`) goes up by 1;
- the unit is finished at progress 100.

A step waits while the player can't pay. The step delay is `DAT_00462d18 + k`, and `DAT_00462d18` is a read-only constant **2**. So training takes **100 x (2 + k)** production updates. How many updates there are per second has not been traced, so compare these values relative to each other.

| Building | Unit (Korea / Japan) | k | Updates to train | Faction difference |
|---|---|---:|---:|---|
| Barracks | Footman, Archer, Gunner | 1 | 300 | None |
| Temple | Monk / Priest, Shaman / Witch | 1 | 300 | None |
| HQ | Peasant | 2 | 400 | None |
| HQ | Bull | 3 | 500 | None |
| Heavy Arms | Fire Car / Armored Car, Cannon | 3 | 500 | None |
| Stable | Commander / General | 3 | 500 | None |
| Airport | Transporter | 2 | 400 | None |
| Airport | Bomber (Japan) | 3 | 500 | - |
| Airport | Fighter | 4 | 600 | None |
| Shipyard | Transport Ship; Patrol / Arrow Ship | 3 | 500 | None |
| Shipyard | **Turtle Ship / Cannon Ship** | Korea 5, Japan **4** | Korea 700, Japan **600** | **Japan about 14% faster** |
| Shipyard | Submarine (Japan) | 2 | 400 | - |
| Barracks2 | Occupier / Thief, Miner | building's own delay: 2 x 5 = 10 | 1,000 | None |

The Shipyard's category `0x26` is the only place a dispatcher picks the delay by faction (`+5` for faction 0, `+4` for faction 1). Barracks2 sets no per-unit delay; its base constructor `0x44c1b0` (shared by both factions) sets `+0x1fc = 2 x 5`.

## Buildings

Same 13 roles and type IDs as Korea. Costs are identical for both factions. Type 3 (Korean only, 600 HP, no cost entry) and type 9 (1 HP, 200/100) are not player buildings.

| Type | Building | Cost | HP | Def % | Notes | Portrait | Constructor |
|---:|---|---:|---:|---:|---|---:|---|
| 1 | Headquarters | 2,000 | 1,000 | 70 | | 70 | `0x439410` |
| 2 | Barracks | 1,000 | 700 | 50 | | 71 | `0x44fe80` |
| 4 | Mill | 900 | 800 | 70 | | 73 | `0x434c30` |
| 12 | Beacon | 1,100 | 800 | 70 | sight 10 | 80 | `0x439b30` |
| 7 | Heavy Arms | 1,000 | 700 | 50 | | 75 | `0x447620` |
| 13 | Stable | 1,400 | 700 | 50 | | 81 | `0x4344b0` |
| 8 | Barracks2 | 1,000 | 700 | 50 | | 77 | `0x44cad0` |
| 10 | Airport | 1,500 | 700 | 55 | | 78 | `0x429630` |
| 11 | Temple | 1,000 | 700 | 50 | | 79 | `0x445d00` |
| 14 | Witch House | 1,300 | 700 | 50 | | 82 | `0x445ec0` |
| 6 | Shipyard | 900 | 700 | 50 | | 76 | `0x446ba0` |
| 5 | **Arrow Tower** | 800 | 800 | 50 | power **70** (K 60), class **1** gun (K 2 arrow), cooldown **80** (K 85), range 7 | 74 | `0x430780` |
| 15 | **Cannon Tower** | 1,100 | 800 | 50 | power 100, class **1** (K 2), cooldown 200, range 9 | 83 | `0x430ca0` |

Building HP, defence and cost match Korea exactly. Only the two towers differ, and only in attack class and power.

## Side finding: Korean ship identities

The user confirmed the in-game Korean costs: Transport Ship 1,000, Patrol Ship 1,300, Turtle Ship 2,000. The exe cost table keys 1,000 to type 22 and 1,300 to type 23, which is the reverse of the current constructor-to-name mapping (type 22 `0x43de40` is treated as the Patrol Ship). The costs are settled; which Korean ship constructor belongs to which name may still be swapped, and should be checked if the ship stats are revisited.

## Still unknown

- Whether the Submarine is hidden (the meaning of the `+0x40` and `+0x140` flag bits), and what can target it.
- Movement speeds and sprite banks (`+0x1c4`-`+0x1dc`, `+0x1f8`/`+0x1fc`) for the Japanese units, and which SPR files hold their art. `BUILD3`, `SHIP2`, `Fly1` and the unused ranges of the infantry sheets are the candidates.
- Exact Bomber flight behaviour: how it picks and approaches targets.
