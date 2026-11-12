# Caster spells in syw.exe

The Monk, Shaman, Priest and Witch share one caster class (base `0x43e4d0`) and one spell handler, `0x43f400`, which switches on the command ID. Casters pay with their current power field (`+0x64`, base 60), which works as their mana.

## Who has which spell

Each caster's constructor stores its button list as indices into the command registration table `0x41a150` (fields `+0x1d4` onwards). The spell names come from `language.gtf`, whose 32-byte name slots follow the command order.

| Caster | Constructor | Buttons (registration index -> command) |
|---|---|---|
| Korean **Monk** | `0x4400c0` | 0x12 -> `0x1b` Treatment (Heal), 0x13 -> `0x1c` Mass Treatment, **0x0d -> `0x13` Disturb** |
| Japanese **Priest** | `0x440180` | 0x12 -> `0x1b` Treatment (Heal), 0x13 -> `0x1c` Mass Treatment, **0x0e -> `0x21` Detect mine** |
| Korean **Shaman** | `0x440240` | 0x14 -> `0x1d` Lightning, 0x16 -> `0x1f` Transform |
| Japanese **Witch** | `0x440300` | 0x15 -> `0x1e` Earthquake, 0x17 -> `0x20` Bewilderment (Betray) |

The Priest and Witch share the Monk's and Shaman's code (identical vtables). They get **different spells** only through their button lists.

| Command | Name (`language.gtf`) | Voice | FIRE icon | Targeting | Cost | Caster |
|---|---|---|---:|---|---:|---|
| `0x1b` | Treatment | `vheal` | 195 | Unit | - | Monk, Priest |
| `0x1c` | Mass Treatment | `vmassheal` | 196 | Instant | - | Monk, Priest |
| `0x13` | **Disturb** | - | **191** | Instant | 25 | Monk |
| `0x21` | **Detect mine** | `vminedetect` | **205** | Instant | 25 | Priest |
| `0x1d` | Lightning | `vthunder` | 197 | Ground | 35 | Shaman |
| `0x1f` | Transform | - | 199 | Unit | 35 | Shaman |
| `0x1e` | Earthquake | `vearth` | 198 | Ground | 35 | Witch |
| `0x20` | Bewilderment | `vbetray` | 200 | Unit | 35 | Witch |

On the mod's 100-mana scale, 25 of 60 is about 42 and 35 of 60 about 58.

## Disturb (Korean Monk, `0x13`, `0x43e960`)

Disturb **hides the area around the Monk from the enemy**:
1. It needs more than 24 power and costs 25.
2. It covers the **9x9 square** centred on the Monk (-4 to +4 cells).
3. For every **non-allied** player (`0x42a9e0`), it sets their map-knowledge cell to `0xffff`, the unexplored black-shroud value. The reveal routine `0x429e10` writes 0 for seen cells and treats `0xffff` as unexplored. If the local player is affected, the fog display is redrawn.
4. It sets the "recompute sight" flag `+0x1b0` on every unit and building in the area. On the next update (`0x4021b0`) each of them re-reveals its own sight radius for its owner (`0x429d20`).

**Net effect:** enemies lose all sight and exploration of that area, apart from what their own units there can still see. What the Monk's side has there disappears back into black shroud for them. It reveals nothing; it's a counter-scouting tool, like a C&C Gap Generator pulse.

## Detect mine (Japanese Priest, `0x21`)

Detect mine **destroys mines around the Priest**:
1. It needs more than 25 power and costs 25.
2. It scans the **11x11 square** centred on the caster (-5 to +5 cells).
3. Every living object with **type ID 9** (the mine, 1 HP, laid by the Korean Miner) has its HP set to 0 (`0x4017e0(obj, 0)`), which destroys it. It doesn't check owners.

Japan has no Miner, so in practice it clears Korean minefields.

## Other spells in the handler

- **Lightning** (`0x1d`): costs 35. It strikes the target cell, or with a 1/3 chance a random cell up to 1 away, for power **1,050** (`0x42b110`).
- **Earthquake** (`0x1e`): costs 35. `0x42b340` hits the 5x5 area around the target cell for power **105**, with random ground effects.
- **Transform** (`0x1f`): the target must be the caster's own **Peasant** (type 16). It costs 35 and gives 30% Footman, 30% Archer, 30% Gunner, and 10% failure. **On failure the Peasant dies.** The mod currently picks from 8 unit types with no failure.
- **Bewilderment** (`0x20`): costs 35. The target must be an **enemy** unit with flag `0x40000`. There is a 20% failure chance; otherwise the unit **changes owner to the caster** (mind control).
- **Treatment / Mass Treatment** (`0x43e780`, `0x43e870`) were not re-traced here.

## Evidence trail

- Sound IDs: loader `0x441090` stores handles at `0x556500+`; switch `0x440720` maps IDs to handles; command handler `0x40fdf0` plays the voice per command.
- Button icons: registration calls in `0x41a150` (`0x41a0f0(index, command, ..., icon, ...)`).
- Button lists: `0x41bc60` builds the on-screen command array from each selected unit's registration indices.
