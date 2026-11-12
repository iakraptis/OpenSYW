# Combat verification audit

Scope: all 17 documented Korean constructor records, the six-by-six attribute
routine, normal attack dispatch, damage arithmetic and the generated unit matrix.
This is an executable/static-analysis audit plus isolated CPU emulation, not a
claim that every game system or an original-game match has been tested.

## Verification results

| Check | Result | Evidence / practical limit |
|---|---|---|
| Original executable | SHA256 matches the recorded original | `4074920e28311dd2f558a29ecc0019debf309fa0bfbc7f0c8eacff3bf56af5dc` |
| Constructor fields | 230 values matched original x86 stores | [ConstructorStoreAudit.tsv](data/constructor-store-audit.tsv); verifies cited stores, not every possible later write |
| Normal virtual methods | All 17 vtable entries recovered | [CombatVirtualMethods.json](data/combat-virtual-methods.json); missing functions explicitly created/decompiled in Ghidra |
| Attribute arithmetic | 432 assertions passed | All 36 pairings at powers 0, 1, 2, 3, 17, 34, 44, 49, 60, 77, 105, 250 |
| Damage and HP clamp | 140 assertions passed | HP 0/1/10/250; defense 0/20/30/60/100; incoming 0/1/2/35/44/73/500 |
| Defense and recovery | 18 assertions passed | Six positive HP values, three base defenses; zero-counter recovery branch |
| Footman melee | Vtable, power load and direct-damage calls pass | FootmanMeleeDisassembly.txt (local only: `research/evidence/`) |
| Unit matrix | Regenerated from traced normal attack paths | 204 attribute cells and 85 N/A cells; target legality is separate |

The 590 additional emulation assertions supplement the previous 36 matrix
observations and four asserted armor cases. They validate ordinary nonnegative
gameplay inputs, not all possible signed integers or overflow behavior.
The recovery test checks a zero-counter update, not a full match's scheduling.

## Corrected normal attack paths

Names retain the existing roster convention; the ship identity caveat below
still applies. Addresses are original virtual addresses.

| Unit(s) | Normal attack method | Damage path | Actual attribute step |
|---|---|---|---|
| Peasant, Bull, Miner, Transporter | `0x401580` | Attack bookkeeping/depletion only | N/A for this method; abilities separate |
| Occupier | `0x42c100` | Ownership transfer | N/A; no ordinary HP damage |
| Footman | `0x438370` | Direct `0x402e40` | Bypass, effective x1 |
| Archer | `0x429840` | `0x449900` -> `0x448af0` -> `0x448c50` | Class 2 |
| Gunner | `0x44bc50` | `0x449e80` -> `0x4490b0` -> `0x4491e0` | Class 1 |
| Commander | `0x443530`, Korean branch | Arrow factory `0x449900` | Class 2 |
| Patrol Ship / arrow ship | `0x43df80` | Arrow factory `0x449900` | Class 2 despite stored 0 |
| Fire Car | `0x44eca0` | `0x449a00` -> `0x448d40` -> `0x448ec0` | Class 3 |
| Fighter | `0x43cec0` | Same factory and impact as Fire Car | **Class 3 despite stored 4** |
| Cannon | `0x44f230` | `0x449ae0` -> `0x4494f0` -> `0x449680` -> `0x4488a0` | Class 4 |
| Turtle Ship; provisional Transport Ship | `0x4379b0`, Korean branch | Same cannon factory | Class 4 |
| Monk, Shaman | `0x43f1d0` | `0x449be0` -> `0x449690` -> `0x449810` | Class 6 or bypass; both neutral |

The Japanese branch of Commander's shared method is melee and bypasses the
matrix. That does not change the Korean arrow behavior. Similarly, the shared
ship method has a Japanese type-23 arrow branch; the two Korean constructors
take its cannon branch.

**The constructor class is not the authoritative weapon matchup.** Footman,
Patrol Ship and Fighter demonstrate three distinct exceptions. The class-5
matrix row still exists, but these findings do not establish a normal Korean
weapon that actually uses it.

## Splash damage: why the table is only the attribute step

The recovered impact methods mutate projectile power between victims. The
following summarizes observed decompiled control/data flow; it has not yet been
tested by emulating a complete multi-actor impact or running the original game.

### Class-3 impact: Fire Car and Fighter

`0x448ec0` samples candidate cells and suppresses duplicate actor hits. For an
accepted victim at candidate index `i`, with mutable projectile power `P`:

```text
input = P if i == 0 else floor(P / 2)
P = attribute(3, victim.targetClass, input)
applyDamage(victim, P)
```

The first candidate index is significant: if it is empty, a later candidate
still takes the halving branch. Because `P` is overwritten, later victim damage
depends on prior victims and integer rounding. It is not simply original power
times one fixed class multiplier for everyone in the splash.

### Class-4 explosion: Cannon and cannon ships

`0x449680` calls virtual slot `+0x0c`; the cannon projectile's vtable resolves
that slot to `0x4488a0`. That routine scans the center and eight neighbors,
filters invalid actors and suppresses duplicates.

```text
if center victim exists:
    P = attribute(4, center.targetClass, P)
    applyDamage(center, P)

for each accepted neighboring victim in scan order:
    P = attribute(4, victim.targetClass, P)
    applyDamage(victim, floor(P / 3))
```

Class 4 is x1.5 for all six target classes, so repeated victim processing can
increase `P`. Example with initial `P=77` and valid center plus two distinct
neighbors: pre-armor incoming values are **115**, **57**, **86** in that order
(`115 -> 172 -> 258` stored power). These are predictions from the routine,
not an in-game splash measurement. They should not be silently replaced with
a conventional fixed falloff in a faithful port.

## Support attacks and depletion correction

The shared Monk/Shaman normal attack method `0x43f1d0` calls common depletion
and then **subtracts another 1** before creating its projectile. A full-power
60 actor therefore supplies **58**, not 59, on that path. The generic
`+0x74 = 1` constructor field is correct, but does not describe the whole action.
Healing and other spells are separate and cannot be inferred from that power.

## What is not verified yet

- **Ship display names:** the existing mapping calls `0x437df0` Transport Ship,
  but it has a cannon attack method. This might be an armed transport or an
  incorrect roster mapping. Earlier production labels also conflict with the
  portrait-based names. Keep that identity provisional and use constructor/type
  IDs as the precise identifiers. The attack facts above belong to those IDs.
- **Costs/prerequisites:** copied from the user's `TechTree.md`; this audit did
  not establish the executable's effective purchase prices or unlock rules.
- **Movement, range, sight and attack speed in seconds:** raw fields/counters
  are recorded, but complete scheduling and targeting rules remain unverified.
- **Target permission:** the matrix does not authorize shooting aircraft or
  reaching ships. Neither constructor flags nor arithmetic alone prove that.
- **Mines, capture restrictions, healing, spells, cargo, upgrades, later stat
  modifications and complete splash behavior:** not fully audited. N/A means
  no HP damage in the inspected normal attack method, not no damage ever.

## Reproduction

From the repository root, with the portable Ghidra project already imported:

```powershell
python -I research/tools/ghidra/audit_constructor_stores.py
powershell -NoProfile -ExecutionPolicy Bypass -File research/tools/ghidra/Run-SywAnalysis.ps1 -VerifyCombat -OutputName verified
python -I research/tools/ghidra/build_unit_matchups.py
```

Check for `EXTENDED COMBAT PASS: 590 asserted cases`; a zero Ghidra exit code
alone is insufficient. `AuditCombatPaths.java` recovers/decompiles explicit
method addresses, including ones absent from the original full export. Current
exports are in `research/ghidra/out/audit/`. The reviewed core exports are
also preserved under evidence (local only: `research/evidence/`) for inspection without rerunning tools.
