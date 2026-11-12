# Original combat stats recovered with Ghidra

Analyzed `SYWAR/syw.exe`, SHA256
`4074920e28311dd2f558a29ecc0019debf309fa0bfbc7f0c8eacff3bf56af5dc`.
Addresses below are virtual addresses in that executable. These are constructor
defaults and traced arithmetic, not measurements from a running match.

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

## Unit classes and practical counter groups

Each actor has **two separate classes**: its attack field at `+0x50` and its
**target class at `+0x54`**. These are different namespaces: attack class 2 means
an arrow-type attack, while target class 2 groups cavalry. Names below are our
descriptive labels inferred from units and weapon paths, not recovered original
enum symbols.

### Target classes: the matrix columns

| ID | Descriptive group | Korean units / evidence |
|---:|---|---|
| 1 | Infantry and workers | Peasant, Bull, Footman, Archer, Gunner, Occupier, Miner, Monk, Shaman |
| 2 | Cavalry | Commander (`0x443a00`) |
| 3 | Vehicles | Fire Car (`0x44eb70`), Cannon (`0x44f0e0`) |
| 4 | Ships | Turtle Ship, Patrol Ship and the provisional Transport Ship; inherited from `0x434ce0` |
| 5 | Buildings | Building base `0x404950`; also explicitly set by tower constructors |
| 6 | Aircraft | Transporter and Fighter; inherited from `0x43a1b0` |

Bull is class **1**, despite being a larger resource unit. Commander is class
**2**, while Footman is class **1**. Unit role, size or appearance alone does not
predict the counter group.

### Each Korean unit's attack and target fields

"Stored" is the constructor field. A projectile may select a different matrix
row at impact. A zero field therefore does not by itself prove an actor cannot
attack, and a nonzero field does not establish usable weapons or target legality.

| Unit | Target class | Stored attack class | Interpretation / impact evidence |
|---|---|---:|---|
| Peasant | 1: Infantry / workers | 0 | 0; worker, ordinary attack not established |
| Bull | 1: Infantry / workers | 0 | 0; worker, ordinary attack not established |
| Footman | 1: Infantry / workers | 5 | Melee field; normal hit at `0x438370` bypasses the matchup routine and applies current power directly |
| Archer | 1: Infantry / workers | 2 | Arrow; impact `0x448c50` explicitly uses row 2 |
| Gunner | 1: Infantry / workers | 1 | Gun / bullet; impact `0x4491e0` explicitly uses row 1 |
| Fire Car | 3: Vehicles | 3 | Class 3 impact verified at `0x448ec0`; splash mutates power between victims |
| Cannon | 3: Vehicles | 4 | Class 4 explosion verified through `0x449680` -> `0x4488a0`; splash rules apply |
| Transport Ship (provisional) | 4: Ships | 4 | Class 4 cannon impact verified for this constructor; display identity still provisional |
| Patrol Ship | 4: Ships | 0 | Arrow; fire `0x43df80` creates arrows, whose impact uses row 2 **despite stored 0** |
| Turtle Ship | 4: Ships | 4 | Class 4 cannon impact verified through `0x4379b0` |
| Occupier | 1: Infantry / workers | 0 | 0; capture ability is separate |
| Miner | 1: Infantry / workers | 0 | 0; placed mines require a separate damage trace |
| Commander | 2: Cavalry | 2 | Korean branch `0x443530` fires arrows: actual class 2 |
| Transporter | 6: Aircraft | 0 | 0; transport role |
| Fighter | 6: Aircraft | 4 | Stored class 4, but actual impact `0x448ec0` uses class 3; splash rules apply |
| Monk | 1: Infantry / workers | 6 | Normal projectile: class 6 or bypass, both neutral; separate abilities differ |
| Shaman | 1: Infantry / workers | 6 | Normal projectile: class 6 or bypass, both neutral; separate abilities differ |

### Matchup matrix with named target columns

These are the original routine's ratios, previously emulated with input power
60. They describe what happens **when that attack row is passed to the routine**;
constructor rows alone do not prove a unit uses them for every action.

| Attack row | Infantry / workers (1) | Cavalry (2) | Vehicles (3) | Ships (4) | Buildings (5) | Aircraft (6) |
|---|---:|---:|---:|---:|---:|---:|
| 1: gun / bullet | 1.5 | 1.5 | 1 | 1 | 1 | 2/3 |
| 2: arrow | 1 | 1 | 2/3 | 2/3 | 1.5 | 1.5 |
| 3: Fire Car field | 1.5 | 2/3 | 1 | 1 | 1 | 1 |
| 4: heavy-projectile field | 1.5 | 1.5 | 1.5 | 1.5 | 1.5 | 1.5 |
| 5: melee field | 1.5 | 1.5 | 2/3 | 2/3 | 2/3 | 1 |
| 6: support / magic | 1 | 1 | 1 | 1 | 1 | 1 |

The unexpected arrow bonus against buildings and aircraft is present in the
emulated routine. A multiplier does not grant permission to target aircraft;
range, air/ground targeting and other checks are separate.

### Why the constructor matrix is not the complete combat system

The Patrol Ship exception is traced end to end: fire `0x43df80` calls arrow
factory `0x449900`, which creates `0x448af0`; arrow impact `0x448c50` passes
literal attack class **2**, not the shooter's `+0x50` field.

Fire Car and Fighter share projectile factory `0x449a00` despite storing
classes **3** and **4**. Their recovered impact `0x448ec0` uses class **3** for
both. Its per-victim power mutations also affect splash damage. See the
[verification audit](verification-audit.md) for the exact processing rules.

The shared support attack at `0x43f1d0` creates a projectile via `0x449be0` ->
`0x449690`. Its impact routine `0x449810` applies row **6** in one branch and
calls damage directly in another. Row 6 is neutral, but neither branch proves
all Monk/Shaman abilities share that behavior. Separate effects `0x42b110` and
`0x42b340` call damage directly, bypassing the matchup routine; `0x43f400`
invokes them. Do not apply a universal matchup multiplier to every spell or mine.

All 17 target-class assignments and stored attack fields are recovered with
per-field source addresses in [KoreanUnitDefaults.json](data/korean-unit-defaults.json).
Normal weapon dispatch is now traced in the verification audit. Remaining
work concerns abilities, target permission, identities and full gameplay behavior. No OpenRA balance or targeting rules were changed in this investigation.

## Footman melee: verified hit path

The Korean Footman constructor `0x4387f0` installs vtable `0x45d248`.
Its function pointer at offset `+0x30` is **`0x438370`**, the normal melee
attack routine. This routine was missing from the earlier full C export;
following the vtable and disassembling the original PE bytes recovered it.

**The stored attack class 5 is not used to modify this normal melee hit.**
The class-5 row exists in the matchup routine, but applying it to Footman sword
damage would be an incorrect port of this path. This corrects the earlier
suggestion that the Footman necessarily gets the matrix's melee bonuses.

### Hit sequence

1. The routine waits until cooldown counter `+0x110` reaches threshold
   `+0x10c` (Footman default **5**).
2. It increments animation counter `+0x120`; at threshold `+0x11c` (**2**),
   it resets that counter and advances frame `+0x1b8` modulo **4**.
3. Facing selects one of the eight neighboring cells, including diagonals.
4. On frame index **3**, it plays an effect/sound, then calls power depletion
   `0x401580` at instruction `0x438537`.
5. It looks up the actor occupying the facing-adjacent cell via `0x419410`
   and checks it with `0x42a5f0`. This checks actor validity and `+0x1a4`;
   it is not evidence that the complete attack-order system permits all targets.
6. At `0x438559`, it loads current power directly from attacker `+0x64`.
   At `0x438564`, it calls **`0x402e40` directly** with that power and the
   target actor. No attack-class or target-class multiplier is applied here.
7. When animation wraps to frame **0**, it clears the cooldown counter and
   attack flag `+0x134` and returns completion.

Depletion happens before the cell lookup, so a swing reaching the hit frame can
lose power even if its target has moved away. The hit uses cell occupancy at
that moment; it does not simply subtract HP from the originally selected ID.
Facing-cell selection is evidence of adjacent melee reach, not a complete audit
of pathfinding, attack-order range checks or air/ground targeting.

### Actual normal-melee formula

```text
if currentPower > floor(basePower / 2):
    currentPower -= powerDecrement

# Only if the adjacent actor passes the target check:
incoming = currentPower                    # no class-5 transform
loss = max(1, incoming - floor(defense * incoming / 100))
targetHP = max(0, targetHP - loss)
```

For a fresh Footman, base power **45** becomes **44** on the first swing:

| Target current defense | HP loss from that hit |
|---:|---:|
| 0% | 44 |
| 20% | 36 |
| 30% | 31 |
| 50% | 22 |
| 60% | 18 |

These are arithmetic examples conditional on a valid melee hit, not proof that
Footmen can reach every actor with those defenses. Target class does not enter
this path's arithmetic. With repeated swings and no recovery, power reaches
**22**; against defense 30 that deals **16** HP, before any later defense change.

The cooldown and animation counters interact; **5 is not an established
five-tick total attack period**, and no seconds/DPS value is asserted here.
Other units with stored class 5 still require their own hit-path checks.

### Reproduce the static verification

Run `python -I research/tools/ghidra/audit_footman_melee.py`.
The audit checks the executable SHA256, maps PE virtual addresses correctly,
verifies the Korean vtable entry, and checks the depletion/direct-damage calls
and current-power load. It passed against the original executable.
FootmanMeleeDisassembly.txt (local only: `research/evidence/`) preserves the complete
routine through its final return, excluding the following jump-table data.
This is static instruction verification, not an original-game runtime test.

## Fields and damage

| Actor offset | Meaning | Evidence |
|---|---|---|
| `+0x44` | Maximum HP | Constructors and health-bar routine `0x402470` |
| `+0x48` | Current HP | Health bar and damage routine `0x402e40` |
| `+0x50` | Attack attribute class | Constructors and attack paths |
| `+0x54` | Target attribute class | Projectile impact reads |
| `+0x64` | Current attack power | Projectile creation and recovery |
| `+0x68` | Base attack power / recovery cap | Constructors and `0x401fa0` |
| `+0x6c` | Current defense percentage | Damage routine |
| `+0x70` | Base defense percentage | Constructors and `0x401fa0` |

For positive integer gameplay values, damage routine `0x402e40` computes:

```text
damage = max(1, incoming - floor(currentDefense * incoming / 100))
currentHP = max(0, currentHP - damage)
```

The update at `0x401fa0` changes defense as health falls:

```text
healthPercent = floor(currentHP * 100 / maxHP)
currentDefense = floor(healthPercent * baseDefense / 100)
```

Base attack power is not a fixed per-hit damage value. `0x401580`, called before
archer/gunner projectile creation, reduces current power by `+0x74` while it is
above half the base power. `0x401fa0` recovers power toward its cap while state
`+0x178` is zero, using a modulo-50 counter at `+0x120`. The decrement amount,
state semantics, tick duration and upgrades still need tracing. The normal
Footman melee path is now verified above; other melee actors remain separate.
The constructor decrement has now been recovered: 1 for this roster except
Cannon, which uses 3. The raw value alone does not prove every unit calls the
depletion routine for every action.

### Attack power depletion and recovery

The relevant part of `0x401580`, before projectile creation, is:

```text
if currentPower > floor(basePower / 2):
    currentPower = currentPower - powerDecrement
```

This is a threshold check before subtraction, not a clamp to half power.
With no recovery or other writes, repeated calls therefore give:

| Base / decrement | Power after first call | Eventual resting value |
|---|---:|---:|
| Archer: 35 / 1 | 34 | 17 |
| Gunner: 50 / 1 | 49 | 25 |
| Footman: 45 / 1 | 44 | 22 |
| Cannon: 80 / 3 | 77 | 38 |

These are arithmetic predictions for repeated calls, not measured shot counts
or proof that each listed unit's complete attack path uses the routine.
For positive decrement `d`, the number of decrements from base `B` is
`ceil((B - floor(B / 2)) / d)`; the resulting power is `B - d * count`.

For a living actor, the recovery branch of `0x401fa0` is:

```text
if currentPower < basePower and state_178 == 0:
    if counter_120 == 0:
        currentPower += 1
    counter_120 = (counter_120 + 1) % 50
```

That is one point on a zero-counter update, followed by a modulo-50 cycle
while eligible. It is not necessarily a 50-update initial wait, and it cannot
yet be expressed in seconds. This counter is also used by attack animation.

### Worked damage examples

For a traced archer/gunner projectile, first take the power after depletion,
apply the attribute calculation, then apply the target's current defense:

```text
incoming = attributeTransform(projectilePower, attackClass, targetClass)
damage = max(1, incoming - floor(defense * incoming / 100))
remainingHP = max(0, HP - damage)
```

Assuming no intervening power changes or overrides:

- A fresh Archer passes power 34 after depletion. Against a full-health Peasant
  (target class 1, defense 20), class 2 leaves 34 unchanged. HP loss is
  `34 - floor(20 * 34 / 100) = 28`, leaving 222 HP.
- A fresh Korean Gunner passes 49. Against the same target, class 1 gives
  `floor(49 * 3 / 2) = 73`. HP loss is `73 - floor(20 * 73 / 100) = 59`,
  leaving 191 HP.
- After that target's next defense update, its defense becomes
  `floor(floor(191 * 100 / 250) * 20 / 100) = 15`. A later incoming 73 would
  then deal `73 - floor(15 * 73 / 100) = 63`. This example holds incoming
  power constant to isolate the defense effect; another gunner shot may have
  lower power due to depletion.

Rounding happens at each integer operation. Do not replace the two defense
divisions with one floating-point percentage and expect identical results.

Archer fire `0x429840` passes current power through `0x449900` -> `0x448af0`
-> projectile base `0x447e10`; impact `0x448c50` applies attribute class 2 then
calls damage. Gunner fire `0x44bc50` uses `0x449e80` -> `0x4490b0`; impact
`0x4491e0` uses class 1. Both use target `+0x54`.

## Attribute calculation and isolated emulation

`0x42ab10` is the actual attribute calculation referenced by the embedded
`CalcurateApowerWithAtb` diagnostics. Emulating its x86 instructions with power
60 produced this matrix. The named target groups are documented above. Peasant, Archer, Gunner and
Footman initialize target class 1.

| Attack class / target class | 1 | 2 | 3 | 4 | 5 | 6 |
|---|---:|---:|---:|---:|---:|---:|
| 1 (gunner) | 90 | 90 | 60 | 60 | 60 | 40 |
| 2 (archer) | 60 | 60 | 40 | 40 | 90 | 90 |
| 3 | 90 | 40 | 60 | 60 | 60 | 60 |
| 4 | 90 | 90 | 90 | 90 | 90 | 90 |
| 5 (footman constructor) | 90 | 90 | 40 | 40 | 40 | 60 |
| 6 | 60 | 60 | 60 | 60 | 60 | 60 |

Decompiler paths implement unchanged, `power * 3 / 2`, or `power * 2 / 3`
with integer arithmetic. The initial matrix was measured at 60. The expanded audit asserts all 36
pairings at 12 powers, including odd values and zero; it is not exhaustive
across all integers. Four asserted damage cases at incoming 35 and defense
0/20/30/100 produced HP losses 35/28/25/1. Emulation does not launch the game.

Reproduce from the repository root after importing the project:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\research\tools\ghidra\Run-SywAnalysis.ps1 -VerifyCombat -OutputName verified
```

Check for `COMBAT EMULATION PASS` in the output; Ghidra can return exit code zero
even on a script error. Results are in
`research/ghidra/out/verified/combat-emulation.tsv`.
The script also saves researcher-assigned function labels in the Ghidra project.
Full decompilation (1,085 functions) is available locally under the sibling
`full/` directory and can be regenerated with `-OutputName full -Addresses --all`.

## Corrections to earlier reconnaissance

The older `docs/formats/exe.md` contains exploratory hypotheses. The table at
`0x490920` is used as a live actor pointer table; do not treat every record as a
static unit definition. `+0x178` is used as runtime state, not a universal unit
category. The former tentative identification of `0x42cdc0` as an archer
constructor is contradicted by the recovered constructors above: it initializes
type 3, HP 600 and a 2x2 footprint. Its precise building identity remains open.
Menu cost IDs and live actor type IDs are different namespaces.

Next: trace attack power depletion and cadence, recover building constructors,
then compare a controlled original-game encounter before updating OpenRA balance.

## Unit-versus-unit multiplier matrix

See [KoreanUnitMatchups.md](matchups.md) for the 17-by-17 matrix and [CSV](data/korean-matchups.csv). Normal attack attribute steps are now traced for every roster entry. `N/A` means no HP damage in the inspected method; mines and other abilities remain separate. Splash requires the additional rules in the verification audit. Target eligibility and armor are separate.

Latest verification, corrections and limitations: [VerificationAudit.md](verification-audit.md).
