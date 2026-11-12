# Korean unit-versus-unit damage multipliers

Rows attack; columns defend. These are **pre-armor matchup multipliers**, not final damage, DPS or predictions of who wins.

**Targeting is not represented.** A number describes the damage arithmetic if that attack reaches that target. It does not mean the unit can attack aircraft, reach a ship, or issue that attack order. In particular, the Footman aircraft cells do not imply anti-air melee.

`1` = neutral; `3/2` = 1.5 times power; `2/3` = two-thirds power. `N/A` = no HP damage in the inspected normal attack method; this does not exclude mines or other abilities. No row is inferred solely from its stored attack class. **Splash rows show the attribute step, not the total splash multiplier**; projectile power can change between victims (see audit).

| Attacker \ Target | PE | BU | FO | AR | GU | FC | CA | TS | PS | TU | OC | MI | CO | TR | FI | MO | SH |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Peasant | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A |
| Bull | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A |
| Footman | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 |
| Archer | 1 | 1 | 1 | 1 | 1 | 2/3 | 2/3 | 2/3 | 2/3 | 2/3 | 1 | 1 | 1 | 3/2 | 3/2 | 1 | 1 |
| Gunner | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 1 | 1 | 1 | 1 | 1 | 3/2 | 3/2 | 3/2 | 2/3 | 2/3 | 3/2 | 3/2 |
| Fire Car | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 1 | 1 | 1 | 1 | 1 | 3/2 | 3/2 | 2/3 | 1 | 1 | 3/2 | 3/2 |
| Cannon | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 |
| Transport Ship (provisional) | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 |
| Patrol Ship | 1 | 1 | 1 | 1 | 1 | 2/3 | 2/3 | 2/3 | 2/3 | 2/3 | 1 | 1 | 1 | 3/2 | 3/2 | 1 | 1 |
| Turtle Ship | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 |
| Occupier | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A |
| Miner | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A |
| Commander | 1 | 1 | 1 | 1 | 1 | 2/3 | 2/3 | 2/3 | 2/3 | 2/3 | 1 | 1 | 1 | 3/2 | 3/2 | 1 | 1 |
| Transporter | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A |
| Fighter | 3/2 | 3/2 | 3/2 | 3/2 | 3/2 | 1 | 1 | 1 | 1 | 1 | 3/2 | 3/2 | 2/3 | 1 | 1 | 3/2 | 3/2 |
| Monk | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 |
| Shaman | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 |

## Column key

| ID | Unit | Target class |
|---|---|---:|
| PE | Peasant | 1 |
| BU | Bull | 1 |
| FO | Footman | 1 |
| AR | Archer | 1 |
| GU | Gunner | 1 |
| FC | Fire Car | 3 |
| CA | Cannon | 3 |
| TS | Transport Ship (provisional) | 4 |
| PS | Patrol Ship | 4 |
| TU | Turtle Ship | 4 |
| OC | Occupier | 1 |
| MI | Miner | 1 |
| CO | Commander | 2 |
| TR | Transporter | 6 |
| FI | Fighter | 6 |
| MO | Monk | 1 |
| SH | Shaman | 1 |

## Evidence and limits

- Archer and Patrol Ship arrows use row 2 at impact (`0x448c50`). Patrol Ship stores attack class 0, which is not used for its arrow matchup.
- Gunner bullets use row 1 at impact (`0x4491e0`).
- Footman normal melee (`0x438370`) bypasses the matrix. Its `1` cells represent that bypass, not its stored class-5 row.
- Commander uses arrows in its Korean faction branch (`0x443530`). Fire Car and Fighter use class 3 at impact (`0x448ec0`), including Fighter despite its stored class 4.
- Cannon and the two cannon-ship constructors use class 4 through `0x449680` -> `0x4488a0`. These are splash attacks; later victims can receive different amounts because the projectile power is overwritten.
- Monk and Shaman normal attack projectiles use neutral class 6 or bypass the matrix (`0x449810`); their separate healing/spells are not these rows.
- The five N/A rows have no direct HP-damage call in their normal virtual attack method. Occupier changes ownership. Miner mines remain separate.
- Transport Ship identity remains provisional; its recovered constructor is a class-4 target regardless of the eventual name.
- Buildings are outside this unit roster. Verified arrow and bullet multipliers against building class 5 are 3/2 and 1 respectively; normal Footman melee bypasses the matrix there too.

For a single-hit attribute step: `incoming = floor(projectile power × multiplier)` (Footman uses current power directly), then `loss = max(1, incoming - floor(current defense × incoming / 100))`. Current power may already have decreased before the hit; current defense depends on remaining health. Remaining HP is clamped to zero. For splash, apply the per-victim rules in [VerificationAudit.md](verification-audit.md), not this simplified formula.

Source: [CombatFindings.md](combat.md), [constructor evidence](data/korean-unit-defaults.json) and the isolated combat-emulation TSV. Regenerate with `python -I research/tools/ghidra/build_unit_matchups.py`. A spreadsheet version is [KoreanUnitMatchups.csv](data/korean-matchups.csv).
