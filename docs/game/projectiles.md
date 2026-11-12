# Projectiles: aiming, flight and impact

How shots travel and land in the original game, and whether they can miss. Recovered statically from `syw.exe`
(SHA256 `4074920e…af5dc`); addresses are virtual addresses in that build. Damage arithmetic after a hit
(attribute matrix, defense, splash power) is in [combat.md](combat.md) and [verification-audit.md](verification-audit.md).

## Summary

- **Projectiles are not homing and never "roll to hit".** There is no hit chance or accuracy stat. A shot flies in
  a straight line to a fixed point and then damages **whoever stands in that cell** when it arrives.
- The fixed point is the **center of the cell the target occupied when the attack began**, not the target itself.
- **So arrows and bullets do miss**, purely by movement. A unit standing still is always hit. A unit walking
  steadily is often missed, and at medium or long range almost always, because by the time the arrow lands it has
  left that cell.
- A missed shot is not wasted if someone else is in the cell: it hits that unit instead, **friend or foe**. The
  check at impact never looks at the owner, so friendly fire is possible.
- Cannon, Fire Car and Fighter shots get a small random scatter, but it is too small to leave the target cell. It
  changes where the shot is drawn, not who it hits.

## The attack cycle

The common attack state machine is `0x4032a0` (actor field `+0x1b4` holds the state).

1. **State `0x28`, aim.** The target must still be valid (`0x42a5f0`) and in range (vtable `+0xa8`). The attacker
   then:
   - stores the target's cell in `+0x88/+0x8c`;
   - calls the target's vtable `+0x68`, which writes the aim point into the attacker's `+0xa0/+0xa4`;
   - stores the target's layer (`+0x18`, see below) in `+0x108`;
   - turns to face the aim point, then switches to state `0x32`.
2. **State `0x32`, fire.** Each tick it calls the unit's fire routine (vtable `+0x30`). If the cooldown (`+0x110`
   counting up to `+0x10c`) is not ready, the routine returns at once and the machine goes back to `0x28`, so **the
   aim is refreshed every other tick until the shot actually starts**. Once the cooldown is ready, the attack
   animation plays (one frame every `+0x11c` ticks), and the projectile is created on a fixed frame.

The aim-point method `0x401910` is shared by units and buildings:

```text
aimX = cellX * 32 + 16
aimY = cellY * 32 + 16
```

There is no sub-cell offset and no lead for a moving target. A cell is 32 × 32 pixels.

A walking unit's cell (`+0x10/+0x14`) switches **halfway** between cells. The movement routine `0x4063a0` adds the
speed `+0x220` to a pixel offset `+0xa8/+0xac`. When the offset passes +16 the cell advances and the offset becomes
−16, so the cell is always the one the unit is mostly standing in. Most combat units move 4 px per tick
(infantry base `0x429760`, ship base `0x434ce0`, aircraft base `0x43a1b0`); workers move 2 px per tick (`0x42d220`).

### Release frame, per unit

| Unit | Fire routine | Frame time `+0x11c` | Released on | Ticks from aim to release |
|---|---|---:|---|---:|
| Archer | `0x429840` | 2 | frame 2 | 4 |
| Gunner | `0x44bc50` | 3 | frame 1 | 3 |
| Commander (Korean) | `0x443530` | 2 | frame 3 | 6 |
| Patrol Ship | `0x43df80` | 4 | volley counter (`+0x250` < `+0x254`, 1 arrow) | about 1 |
| Fire Car | `0x44eca0` | (volley counter) | 3 shots per attack (`+0x23c` < 3) | — |
| Cannon | `0x44f230` | (volley counter) | 1 shot | — |

The power drain `0x401580` runs just before the projectile is created, even when no projectile can be created (see
the pool limit below).

## Flight

All projectiles share the base `0x447e10` (size `0xd54`) and are kept in one pool of **49 slots**
(`0x57c578`, flags `0x481660`, finder `0x4498e0`). Each game tick, the main loop (`0x40fdf0`) calls every live
projectile's vtable `+4` (tick) and deletes the projectile when it returns 0. Vtable `+0` draws it.

At creation, `0x448530` precomputes the path: a Bresenham line from the launch pixel to the aim pixel, keeping one
waypoint every `+0x3c` pixels along the longer axis (at most 416 waypoints). The tick advances one waypoint per
game tick. When it reaches the last waypoint, it calls vtable `+8` (impact) and returns 0.

| Projectile | Constructor | Vtable | Tick | Impact | Waypoint step (px/tick) |
|---|---|---|---|---|---:|
| Arrow (Archer, Commander, Patrol Ship) | `0x448af0` | `0x45f050` | `0x448b40` | `0x448c50` | 27 |
| Bullet (Gunner) | `0x4490b0` | `0x45f070` | `0x449100` | `0x4491e0` | 35 |
| Fire bolt (Fire Car, Fighter) | `0x448d40` | `0x45f060` | `0x448d90` | `0x448ec0` | 16 |
| Cannonball (Cannon, Turtle Ship) | `0x4494f0` | `0x45f0b0` | `0x449540` | `0x449680` → `0x4488a0` | 19 |
| Monk / Shaman bolt | `0x449690` | `0x45f0c0` | `0x4496e0` | `0x449810` | 20 |

Flight time in ticks is `floor(d / step) + 1`, where `d` is the distance in pixels along the longer axis. Infantry
shoot from their own cell center, so `d` is 32 × the cell distance. Because the step is measured on the longer axis,
a diagonal shot covers up to 1.41× more ground per tick.

## Impact: who gets hit

The arrow (`0x448c50`) and the bullet (`0x4491e0`) are identical except for the attribute row (2 and 1):

```text
cell = (aimX / 32, aimY / 32)                 # the stored aim point, not the last waypoint
if layer == 1: victim = airGrid[cell]          # 0x419380
else:          victim = groundGrid[cell]       # 0x419350: unit grid 0x419410, else second grid 0x419450
if victim and victim is alive and +0x1a4 set:  # 0x42a5f0 -> 0x429dc0
    power = attribute(row, victim.targetClass, power)
    damage(victim, power)                      # 0x402e40
    notify(victim, shooter)                    # 0x401820
```

- **The layer comes from the target at aim time** (`+0x18`): 1 for aircraft (`0x43a1b0`), 2 for everything else
  (default in `0x401000`). A shot at an aircraft only checks the air grid, and a shot at a ground unit only checks
  the ground grids. An arrow aimed at a plane cannot hit the soldier under it, and the reverse.
- **No owner or alliance check.** `0x429dc0` tests only the ID range, the "exists" flag, the actor pointer and
  HP > 0, and `0x42a5f0` adds `+0x1a4`. Any living actor in the cell is hit: the intended target, another enemy,
  or one of the shooter's own units.
- On the ground layer, the unit grid is checked first and the second grid only when no unit is there. The second
  grid is most likely buildings (unconfirmed), so a shot that misses a unit beside a building can still damage the
  building if the aimed cell is part of its footprint.

### Cannon, Fire Car and Fighter: scatter and splash

These fire routines jitter the aim point per axis:

```text
spread = 8 if |dx| + |dy| < 5 cells else 16          # Manhattan distance to the target cell
aim    = cellCenter + spread / 2 - (random % spread)  # +4..-3 px, or +8..-7 px
```

The random source is the game's LCG `0x42a8a0` (`seed = seed * 0xff83 % 0xfffb`). Because the cell center is
16 px from every edge, **the jittered point is always inside the target cell**. It moves where the shot is drawn
and where the explosion appears, not which actor is hit.

- **Cannonball** (`0x4488a0`): damages the impact cell, then the eight neighbours at a third of the (mutating)
  power. A target that walked one cell away still takes splash damage. See [verification-audit.md](verification-audit.md).
- **Fire bolt** (`0x448ec0`): checks the impact cell plus nearby cells, removing duplicates. In the decompile only
  the impact cell, the cell to the south and the cell to the south-east are distinct; the cell to the east looks
  overwritten, which may be an original bug and needs a disassembly check. It aims at an aircraft through the air
  grid and **falls back to the ground grid**, so a Fighter's bolts that miss a plane can hit units below. On the
  ground, one bolt in three can also set a fire effect (`0x447d70`).

### Melee for comparison

The Footman (`0x438370`) hits whoever stands in the adjacent cell it faces, on the hit frame. Like projectiles, this
checks cell occupancy rather than the chosen target. See [combat.md](combat.md#footman-melee-verified-hit-path).

## How often a moving target is missed

The shot misses a walking target if the target leaves the aimed cell before impact. Assume:

- the target walks in a straight line at speed `v` px per tick;
- its offset inside the cell is uniform in [−16, 16) when the aim is taken;
- `L` = ticks from aim to impact = release delay + flight time.

Then the target is still in the cell at impact with probability `max(0, 1 − v·L / 32)`. For an enemy walking at the
usual 4 px per tick:

| Distance (cells) | Archer: L | Archer: hit | Gunner: L | Gunner: hit |
|---:|---:|---:|---:|---:|
| 1 | 6 | 25% | 4 | 50% |
| 2 | 7 | 12% | 5 | 38% |
| 3 | 8 | 0% | 6 | 25% |
| 4 | 9 | 0% | 7 | 12% |
| 5 | 10 | 0% | 8 | 0% |

Against a worker (2 px per tick), the archer's chances are much better: 62% at 1 cell, 50% at 3 cells and 38% at 5.

These figures are static estimates, accurate to about ±1 tick. The order of updates inside a tick has not been
traced: a new projectile may tick in the same frame it is created, and the aim may be one tick older. Treat them
as "close shots on walkers sometimes hit, long shots on walkers miss". **Standing, fighting or building targets are
always hit.**

## Other limits

- **Pool limit.** Only 49 projectiles can be in flight at once, shared by all players. When the pool is full, the
  factory (`0x449900`, `0x449e80`, …) returns 0 and the shot silently never exists. The shooter's power has already
  been drained by `0x401580`, and the attack cycle continues as if it had fired. In big battles, some shots simply
  vanish.
- **Range.** It is checked only at aim time (vtable `+0xa8` against `+0x78`). A target that walks out of range
  after the aim is still shot at.

## Comparison with the OpenRA mod (2026-11-12)

The mod's weapons (`mods/syw/weapons/`) use the engine's `Bullet` projectile, with no `Inaccuracy` and no
`Blockable`. Engine `release-20250330` behaves like this:

- **Aim:** `Armament.FireBarrel` snapshots the target's position when the attack starts. The projectile is spawned
  `FireDelay` ticks later and flies to that snapshot. This is the same frozen aim as the original, but at the exact
  position instead of the cell center.
- **Impact:** `Bullet.Explode` applies the warheads at a position, not to an actor. `SpreadDamage` damages every
  actor whose `HitShape` is near the impact point. The default shape is a circle of radius 426. Outside the shape,
  damage falls off over `Spread` steps (default falloff 100/37/14/5/0 %). **A near miss therefore does partial
  damage**, where the original's hit is all or nothing.

Expected damage of one shot at an infantryman walking in a straight line:
- **Mod:** walk 55 per tick, `FireDelay` 6 (Archer) or 3 (Gunner), speed 400 or 700, hit radius 426 plus the
  falloff.
- **Original:** hit chance from the table above.

| Distance | Archer original | Archer mod | Gunner original | Gunner mod |
|---:|---:|---:|---:|---:|
| 1 cell | 25% | 91% | 50% | 100% |
| 2 | 12% | 19% | 38% | 100% |
| 3 | 0% | 6% | 25% | 100% |
| 4 | 0% | 0% | 12% | 91% |
| 5 | 0% | 0% | 0% | 31% |

**What already matches:**
- arrows (apart from point-blank shots);
- no scatter on any weapon;
- the aim is frozen before release;
- no blocking;
- friendly fire for arrows, bullets and fireballs (the warhead default `ValidRelationships` includes allies).

**What differs:**

| Item | Original | Mod |
|---|---|---|
| Gunner accuracy | Misses walkers from 2–3 cells | At bullet speed 700 it hit walkers to about 4 cells; at 500 the expected damage is 100 / 100 / 57 / 19 / 6% at 1–5 cells |
| Projectile speeds (relative to infantry walking speed; original px × 13.75 = mod world units) | Arrow ≈ 370, bullet ≈ 480, fire bolt ≈ 220, cannonball ≈ 260, monk bolt ≈ 275 | 400, 500, 300 (rockets), 310 (cannon), 300; bullets, rockets and cannon were slowed on 2026-11-12 from 700, 500, 500 |
| Fire Car volley | 3 bolts per attack (`+0x23c < 3` in `0x44eca0`), each at the shooter's full power | `Burst: 2` at 2,500 each |
| Cannon and rocket splash | Center cell full; the 8 neighbours a third of the mutating power; hits friends too | Full damage to 1 cell beyond the hit shape, 50% / 25% further out; `Enemy, Neutral` only |
| Near misses | All or nothing, judged by cell | Partial damage by distance |
| Release delay (original ticks × 4/3, the scale the mod uses for reload) | Archer 4 → 5, Gunner 3 → 4, Commander 6 → 8 | 6, 3, 6 |

A faithful port could be a custom warhead or projectile that resolves the target cell at impact. It would damage
the actor whose center is in that cell (any owner), with no falloff, and aim at the cell center captured when the
attack starts. Cheaper approximations within stock traits: slow the bullets and fire bolts to the speeds above,
shrink infantry `HitShape` radius, and replace the default falloff with a sharp edge (`Falloff: 100, 0`). Whether
any of this is wanted for gameplay is a separate decision. The mod's balance has deliberately moved away from the
original before.

## Open questions

- The tick length in real time, and the order inside a tick (actor updates, then projectiles?), to firm up the
  estimates above.
- Whether the second ground grid `0x530954` is buildings only, or also holds other objects.
- The Fire Car's volley timing (`+0x240/+0x244`) and the exact fire-bolt splash cells.
- The Japanese units' fire routines. The projectile classes are shared, but release frames may differ.
- An in-game test to confirm the moving-target misses: archers at 4–5 cells against a walking unit.
