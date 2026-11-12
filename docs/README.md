# Seven Years War: the original game

What we know about the original game (1997, `syw.exe`): its file formats and its rules. Most of it was recovered
with Ghidra from the executable, some from the data files. Everything here refers to the executable with SHA256
`4074920e28311dd2f558a29ecc0019debf309fa0bfbc7f0c8eacff3bf56af5dc`; addresses are virtual addresses in that build.

## Game rules (`game/`)

| Document | Contents |
|---|---|
| [combat.md](game/combat.md) | Actor fields, damage and defense formulas, power drain and recovery, the 6 × 6 attribute matrix, Footman melee |
| [projectiles.md](game/projectiles.md) | Aiming, flight and impact of arrows, bullets, cannonballs and bolts; why shots miss moving units; friendly fire |
| [verification-audit.md](game/verification-audit.md) | Each unit's normal attack path, splash rules, what has and has not been verified |
| [korean-units.md](game/korean-units.md) | Korean roster: HP, power, defense, classes, cooldowns, constructors |
| [japanese-units.md](game/japanese-units.md) | Japanese roster, the same fields |
| [matchups.md](game/matchups.md) | Korean unit-versus-unit multipliers ([CSV](game/data/korean-matchups.csv)) |
| [spells.md](game/spells.md) | Monk, Priest, Shaman and Witch spells: handler, costs, targeting |
| [data/](game/data/) | Machine-readable extracts: per-field constructor values with source addresses, vtables, store audit |

## File formats (`formats/`)

| Document | Format |
|---|---|
| [exe.md](formats/exe.md) | First reconnaissance of `syw.exe` (older; [combat.md](game/combat.md) supersedes parts of it) |
| [spr.md](formats/spr.md) | Sprite banks (`.SPR`) |
| [pal.md](formats/pal.md) | Palettes |
| [anm.md](formats/anm.md) | Animation tables |
| [exp.md](formats/exp.md) | `EXP.SPR` explosion sheet |
| [fst.md](formats/fst.md) | `fst` archives |
| [map.md](formats/map.md) | Skirmish maps |
| [stg.md](formats/stg.md) | Campaign stages and map objects |

The shipping readers are C# in [OpenRA.Mods.Syw/AssetInstaller](../OpenRA.Mods.Syw/AssetInstaller). The format
documents still name the Python prototypes of the original research checkout (`opensy`, `SYWtoORA/tools/`).

## Rules for this folder

- **No original game data in git.** No art, audio, text dumps, maps, decompiled or disassembled code. Numbers,
  formulas and addresses are fine. Previews and decompiles stay in the gitignored `research/` folder.
- **Every claim names its evidence**: a function or field address, a script that reproduces it, or an in-game test.
  Say what is unverified.
- Decompiler names and types are provisional. A field's meaning comes from what reads and writes it.

## Research workspace (`research/`, gitignored)

Set up locally; see `research/README.md` there. It holds:

- the Ghidra project (`research/ghidra/project/SYWAR.gpr`), opened with `research/tools/ghidra/Open-SywGhidra.ps1`;
- decompiled exports (`research/ghidra/out/<name>/<address>.c`), made with `Run-SywAnalysis.ps1`;
- probe scripts: `research/tools/disasm.py` and `vtable.py` (read `syw.exe` directly), and the extractors behind
  `game/data/`.

Ghidra 12.1.3 and its JDK are not copied. The scripts use the portable installs in `../opensy/.local-tools`
(pass `-Tools` for others). The executable is read from `../SYWAR/syw.exe`, or from `SYW_EXE`.
