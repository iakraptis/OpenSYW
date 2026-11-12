# Korean faction unit stats

## Complete Korean roster

All 17 units in `TechTree.md` are covered below. Costs come from that document; they are **not newly verified executable costs**. HP, power and defense are constructor defaults, including inherited assignments. The transport ship name remains provisional (see identity notes).

| Unit | Cost (TechTree) | HP | Base power field | Base defense % | Constructor |
|---|---:|---:|---:|---:|---|
| Peasant | 700 | 250 | 0 | 20 | `0x0042f080` |
| Bull | 1400 | 250 | 0 | 20 | `0x0042f690` |
| Footman | 400 | 350 | 45 | 30 | `0x004387f0` |
| Archer | 400 | 250 | 35 | 20 | `0x00429bf0` |
| Gunner | 700 | 300 | 50 | 30 | `0x0044c070` |
| Fire Car | 1200 | 500 | 50 | 40 | `0x0044eb70` |
| Cannon | 1400 | 450 | 80 | 30 | `0x0044f0e0` |
| Transport Ship (provisional) | 1000 | 600 | 75 | 50 | `0x00437df0` |
| Patrol Ship | 1300 | 700 | 50 | 50 | `0x0043de40` |
| Turtle Ship | 2000 | 600 | 75 | 60 | `0x00437b20` |
| Occupier | 800 | 150 | 0 | 20 | `0x0042c850` |
| Miner | 800 | 250 | 0 | 20 | `0x00428a60` |
| Commander | 900 | 350 | 50 | 40 | `0x00443a00` |
| Transporter | 1500 | 500 | 0 | 50 | `0x0043c8e0` |
| Fighter | 2000 | 300 | 50 | 40 | `0x0043d000` |
| Monk | 900 | 300 | 60 | 20 | `0x004400c0` |
| Shaman | 800 | 300 | 60 | 20 | `0x00440240` |

**Power is a stored field, not guaranteed weapon damage.** In particular, support-unit power 60 is not a verified heal amount or spell damage, and the provisional transport ship's power 75 does not establish that it can attack.

### Raw combat parameters

Attribute classes are numeric engine IDs. `+0x10c` is a cooldown threshold: the live update increments `+0x110` toward it and firing routines check it. These values are not seconds or complete attack intervals; animation and volley delays also apply. `+0x78/+0x7c` are retained as raw fields until their consumers establish exact range/vision semantics.

| Unit | Type ID | Portrait | Attack / target class | Power decrement | Cooldown threshold | +0x78 / +0x7c |
|---|---:|---:|---|---:|---:|---|
| Peasant | 16 | 0 | 0 / 1 | 1 | 5 | 0 / 3 |
| Bull | 34 | 16 | 0 / 1 | 1 | 5 | 0 / 3 |
| Footman | 19 | 7 | 5 / 1 | 1 | 5 | 1 / 4 |
| Archer | 17 | 1 | 2 / 1 | 1 | 20 | 5 / 5 |
| Gunner | 18 | 2 | 1 / 1 | 1 | 30 | 5 / 5 |
| Fire Car | 20 | 3 | 3 / 3 | 1 | 70 | 7 / 7 |
| Cannon | 32 | 14 | 4 / 3 | 3 | 110 | 10 / 10 |
| Transport Ship (provisional) | 23 | 6 | 4 / 4 | 1 | 140 | 8 / 8 |
| Patrol Ship | 22 | 5 | 0 / 4 | 1 | 35 | 6 / 6 |
| Turtle Ship | 21 | 4 | 4 / 4 | 1 | 110 | 8 / 8 |
| Occupier | 24 | 8 | 0 / 1 | 1 | 0 | 0 / 3 |
| Miner | 25 | 9 | 0 / 1 | 1 | 5 | 0 / 3 |
| Commander | 33 | 15 | 2 / 2 | 1 | 18 | 6 / 6 |
| Transporter | 26 | 10 | 0 / 6 | 1 | 0 | 0 / 5 |
| Fighter | 27 | 11 | 4 / 6 | 1 | 90 | 6 / 6 |
| Monk | 29 | 12 | 6 / 1 | 1 | 25 | 5 / 5 |
| Shaman | 30 | 13 | 6 / 1 | 1 | 25 | 5 / 5 |

### Identity and interpretation notes

- Peasant and Bull share worker base `0x42d220`; Bull overrides the sprite bank and portrait, but not HP or defense. Their worker-specific `+0x248` values are 200 and 1,200. These are candidate load limits, not yet verified resource-delivery amounts.
- Commander follows Stable production `0x433d20` -> allocation `0x41fe00` -> Korean constructor `0x443a00` -> base `0x443470`. The Korean subclass overrides power 45 to 50 and defense 30 to 40.
- Occupier follows Barracks2 production `0x44c3a0` -> `0x41f5a0` -> `0x42c850` -> base `0x42c050`. Miner uses `0x41f3e0` -> `0x428a60` -> base `0x428040`.
- Air production `0x428e40` selects `0x41ffa0` -> `0x43c8e0` for Transporter and `0x420160` -> `0x43d000` for Fighter. Faction-zero constructors and portraits 10/11 support the identities.
- Workshop production `0x446ee0` selects `0x4209f0` -> `0x44eb70` for Fire Car and `0x420950` -> `0x44f0e0` for Cannon. Cannon overrides the shared power decrement to 3.
- Portrait 12 visibly depicts the monk; portrait 13 depicts the shaman. Constructors `0x4400c0` and `0x440240` both inherit HP 300, power 60 and defense 20 from `0x43e4d0`. The older notes assigned their production category names oppositely; do not use those category labels as proof of unit identity.
- Ship portrait 4 depicts the turtle ship, portrait 5 the arrow ship, and portrait 6 the remaining ship. `0x43de40` (portrait 5) has an arrow-firing routine at `0x43df80`, supporting the working Patrol Ship assignment despite its inherited attack-class field being zero. Projectile behavior is stronger evidence than that unused constructor field.
- `0x437df0` (portrait 6) is provisionally mapped to the remaining Transport Ship entry in the tech tree. Its normal method is now traced to a cannon impact, but the Transport Ship display name still needs an original-game identity check. The older production-menu labels disagree with the portraits and arrow-fire trace. The numbers belong to the specified constructor regardless of the eventual display name.

### Evidence and reproduction

Per-field source addresses and constructor inheritance chains are saved in [KoreanUnitDefaults.json](data/korean-unit-defaults.json). Run `python -I research/tools/ghidra/extract_korean_defaults.py` from the repository root after generating the `full/` Ghidra export. This extractor collects literal assignments in reviewed constructors; it is not a general C interpreter or a runtime test. All 17 entries passed required-field and faction-zero checks. The previously emulated damage cases remain separate validation of the arithmetic.

Exact movement speed, range units, full attack cadence, healing, spells, mines, cargo rules and upgrade effects remain unverified. Buildings and Japanese-only units are outside this roster.

Damage formulas and arithmetic verification: [CombatFindings.md](combat.md).

Class meanings and weapon-path exceptions: [CombatFindings.md](combat.md#unit-classes-and-practical-counter-groups).

Normal Footman melee is now traced: stored class 5 is bypassed on its normal hit. It applies depleted current power directly to armor. See [verified melee path](combat.md#footman-melee-verified-hit-path).

## Unit-versus-unit multiplier matrix

See [KoreanUnitMatchups.md](matchups.md) for the 17-by-17 matrix and [CSV](data/korean-matchups.csv). Normal attack attribute steps are now traced for every roster entry. `N/A` means no HP damage in the inspected method; mines and other abilities remain separate. Splash requires the additional rules in the verification audit. Target eligibility and armor are separate.

Latest verification, corrections and limitations: [VerificationAudit.md](verification-audit.md).
