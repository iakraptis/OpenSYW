# syw.exe reconnaissance

> Update: [Ghidra combat findings](../game/combat.md) recover actual unit constructors and damage arithmetic, and supersede several tentative interpretations below (especially the archer constructor and actor fields).


Initial probe of `SYWAR/syw.exe` (439,296 bytes) looking for gameplay values
(unit stats, costs, AI parameters). `ddraw.dll` intentionally not analyzed.
Raw string dump saved at `converted/exe_probe/strings.txt` (3,641 ASCII
strings, offset-tagged) for future reference.

## PE structure

Standard 32-bit PE executable (`machine=0x014C`/i386), compiled ~1998
(timestamp `886304043`), 5 sections:

| Section | Raw size | Raw file offset | Notes |
|---|---:|---:|---|
| `.text` | 366,592 | 0x400 | code |
| `.rdata` | 25,600 | 0x59C00 | read-only data, CRT/runtime strings |
| `.data` | 30,208 (virtual 1,188,080) | 0x60000 | **initialized globals - most interesting section**; virtual size much larger than raw size means most of `.data` is zero-initialized at runtime, not present in the file |
| `.idata` | 3,584 | 0x67600 | import table |
| `.rsrc` | 12,288 | 0x68400 | resources (icons/version info, not yet examined) |

The 30,208 bytes of *initialized* `.data` (file offset `0x60000`-`0x67600`)
is where embedded string literals and small constant tables live, and is
the most productive place to keep looking for more gameplay values.

## Confirmed: `CalcurateApowerWithAtb` lookup table

At file offset **0x62E9C**, immediately before five `"%04d CalcurateApowerWithAtb()"`
debug-trace strings (offsets 0x62F58-0x62FD8), a clean table of 21
`(uint32, uint32)` pairs:

```
Atb:    0   7  14  21  28  35  42  49  56  63  70  77  84  91  98 105 112 119 126 133 140
Value: 30  25  20  18  15  12  10   7   5   2   1   2   5   7  10  12  15  17  20  25  30
```

- Column 1 is a perfect arithmetic progression, step +7, 0 to 140.
- Column 2 is a near-perfectly symmetric U-shape, minimum (1) at the exact
  midpoint (Atb=70), rising to 30 at both extremes. (One entry, Atb=21, is
  18 rather than the mirror-expected 17 - either an intentional tweak or a
  transcription slip worth re-checking.)

This symmetry is the same kind of "too clean to be coincidental" signal
that confirmed the SPR pixel offset and the MAP header formula - this is a
real data table, not misread bytes. Function name translates to "Calculate
Attack Power With Attribute", so this is very likely a damage modifier
curve keyed on the difference between some attacker/defender attribute
value and a neutral point of 70 (e.g. a miss-chance or variance curve) -
exact semantics not yet confirmed by tracing the calling code.

**Not yet done**: disassembling the `CalcurateApowerWithAtb` call site to see
which two values are compared to produce the `Atb` index, and whether
`Value` is added to, subtracted from, or multiplied into a base attack
stat.

## Class / behavior inventory (from debug-trace strings)

Every string of the form `"%03d/%04d ClassName::Method()"` or
`"%03d/%04d MethodName()"` across the whole binary, deduplicated - these are
debug-trace call sites embedded by the original build, and give a strong
picture of the game's object structure even without stat values. (Trace IDs
are 3 or 4 digits depending on call site - an earlier pass only matched
4 digits and missed `_AirMaker`.)

**Buildings** (`ClassName::Make`): `Barrack`, `Hq`, `Shipyard`, `Stable`,
`Temple`, `_HeavyMaker`, `_AirMaker`

User-identified real building roster (names approximate): Headquarters,
Barracks, Mill, Radar, Barracks2, Heavyarms, Shipyard, Airport, Stable,
Temple, Temple2 (11 total). Mapping to internal classes so far:

| Building | Internal class |
|---|---|
| Headquarters | `Hq` |
| Barracks (+ Barracks2?) | `Barrack` |
| Heavyarms | `_HeavyMaker` |
| Shipyard | `Shipyard` |
| Airport | `_AirMaker` |
| Stable | `Stable` |
| Temple (+ Temple2?) | `Temple` |
| Mill | not found |
| Radar | not found |

`Barrack::Make()`/`Temple::Make()`/`Shipyard::Make()` each have multiple
distinct trace-ID call sites (e.g. Shipyard has 4: `0000,0001,0045,0110`),
consistent with one class being constructed from several code paths rather
than "2" being a separate class - so Barracks2/Temple2 are more likely a
tier-2 upgrade of the same building than a distinct class. Mill and Radar
have no `::Make()` trace at all - either untraced, or built via a shared/
generic building class rather than their own. Not yet confirmed either way.

`fst\pannel11.fst` ... `pannel33.fst` form a 3x3 grid (9 files) - likely the
build-menu UI, which would match 9 player-built structures (all of the
above except HQ, which is typically a starting building, not menu-built).

**Unit AI / player commands**: `Attack`, `BuildRepair`, `ComRide`,
`DecideCropWork`, `DestroyShipyard`, `GetTreasure`, `Harvest1`, `Help`,
`ShipPatrol`, `GOtoGuardPosition`, `MakeBuild`, `MakeBuildShipyard`,
`SetIDInAirMap`, `SHowMEnu`

**Visual effects** (`SetXxx` spawners): `SetArrow`, `SetBigFire`,
`SetBigSmoke`, `SetBlood`, `SetBomb`, `SetCannonBall`, `SetExplode`,
`SetFireHouseSmoke`, `SetGun`, `SetMediumFire`, `SetMineExplode`,
`SetSink`, `SetSmallExplode/Fire/Smoke`, `SetSmoke`, `SetTankArrow`,
`SetTankGunExplode`, `SetTankSmoke`, `SetTorpedoExplode`, `SetWaterExplode`,
`FireBuda`

**Other**: `Game`, `_Stage::SetChar`, `MoveBlock::SetBlockNumber`,
`_MoveBlock::AddSubBlock`, `LoadStage`, `LoadMapTile`, `MapSet`,
`_Ship::GetFirePosition`, `Tank2::Fire`

## Confirmed file-format cross-references

String literals confirm how the game loads assets already reverse-engineered
in this project, plus the purpose of formats not yet decoded:

- `fnt1\<name>.spr`, `fnt1\knight.pal`, `fnt1\title.pal` - matches `spr.md`/`pal.md`
- `map\<name>.stg`, `cusmap\<name>.stg`, `cusmap\*.stg` - matches the `.stg`
  files still to be reverse-engineered; loaded via `LoadStage()`
- `fst\option1.fst`, `fst\lose.fst`, `fst\win1.fst`/`win2.fst`,
  `fst\save.fst`, `fst\pannel11.fst`...`pannel33.fst` (a 3x3 grid of panel
  UI files), `fst\brief1.fst`/`brief2.fst`, `fst\staff.fst`, `fst\title.fst`
  - confirms `.fst` = UI screen/dialog definitions
- `language.gtf` - confirms `.gtf` = language/localization file
- `save0000.dat` ... `save0006.dat` - confirms `.dat` = save games (per
  user confirmation), 7 slots
- `ani\ani.anm`, `ani\open.avi` - `.anm` is a general animation resource;
  `open.avi` is the intro video
- `effect\*.wav` - matches the `.wav` inventory; filenames reveal purpose
  (`vbuild.wav`, `vtrain.wav`, `vattack.wav`, `vtorpedoattack.wav`,
  `win.wav`, `lose.wav`, `menumus.wav`, `teamsong.wav`, `briefmus.wav`,
  per-mission `jbrief01-10.wav`/`kbrief01-10.wav` voice briefings)
- `SOFTWARE\HQteam\SYW\1.0` - registry key; developer "HQ Team" (also named
  in an embedded credits block: Dino Studio, programmers/artists/composer
  listed by initials), version 1.0

## Other tables found near debug strings (probably NOT stats)

Applying the same "check bytes right before a debug-trace string" technique
that found `CalcurateApowerWithAtb` to other function names (`Barrack::Make`,
`Hq::Make`, `Tank2::Fire`, `_AirMaker::Make`) surfaced more tables, but they
don't look like gameplay stats:

- Small signed `int32` pairs in a narrow range (roughly -22 to +22), e.g.
  `(-22,22), (-22,22), (-5,22), (-8,22), (5,22), (14,5)...`
- **The exact same value set appears before both `Barrack::Make` and
  `Tank2::Fire`** (file offsets `0x647B0`-`0x64830`) - one shared table used
  by two gameplay-unrelated classes (a tank's turret-fire position and a
  barrack's construction have nothing in common mechanically). Combined with
  neighboring classes like `SetSmoke`, `SetFireHouseSmoke`, `SetGun`,
  `SetTankArrow` (all effect *spawners* that need to know **where** on a
  sprite to draw), this reads much more like a shared table of sprite
  anchor points / effect-offset coordinates than unit stats.
- `Farmer1::SetMenu`/`Farmer2::SetMenu` have no adjacent numeric table at
  all - the bytes immediately after are EUC-KR-encoded Korean text (byte
  values in the 0xA0-0xFE range), not data. Makes sense: this is a
  Korean-developed game (see credits block: Korean names, "Dino Studio").

Net result: `CalcurateApowerWithAtb` remains the only *confirmed* gameplay
value table so far. Unit stats (HP, attack, cost, speed, build time) were
not found via this technique - they're either elsewhere in `.data` (only a
few small windows have been manually inspected out of 30,208 bytes), loaded
from an external file at runtime rather than hardcoded, or require actual
disassembly (tracing what a constructor/`Attack()` call reads) rather than
pattern-scanning to locate.

## Constants table at 0x611A0 - confirmed real, traced via disassembly

A systematic sweep of every "gap" in `.data` between string literals (not
just spot-checks near known function names) turned up a 32 x `int32` block,
located precisely between two build-menu-related resources:

```
... fst\pannel31.fst, fst\pannel32.fst, fst\pannel33.fst   <- last of the 9 build-menu panel files
0x611A0: 150, 160, 40, 38, 18, 300, 3, 3, 10, 600, 5, 18, 200, 10,
         255, 30, 13, 35, 17, 20, 15, 2, 100, 100, -1, -1, 100, 8, 1, 1, 1, 0
0x61220: [16x16 byte grid, see below]
0x61340: font glyph-width table starts (ASCII ranges, unrelated)
```

Installed `capstone` (real x86 disassembler, added to `requirements.txt`)
to move past manual opcode-by-opcode reading. Using the confirmed
`CalcurateApowerWithAtb` table as a calibration reference (searching for
its absolute address as raw bytes in the file), the same technique applied
to this block's base address found exactly one direct code reference, and
checking *every one of the 32 fields individually* found that **every
single field is referenced by real code** - most many times over (e.g.
field[19]=20 has 35 distinct call sites, field[26]=100 has 100+, spread
across almost the entire `.text` section). No indexed (`base + reg*4`)
access to the table as a whole was found.

**This means it is not a single building/unit stat record** - array-indexed
access would show that; direct, individually-scattered references show
instead that this is a **shared pool of named engine-wide constants**
(compiled `const int` globals placed together in `.data`), each referenced
wherever needed by name, the same way a "#define"-style constant would be
used throughout a codebase.

One cluster of fields (2, 3, 4, 6, 7, 8, 9, 10) is used together inside a
single function at VA `0x41AD40` (file offset `0x1A140`), disassembled with
capstone:

```asm
mov  eax, [field2]         ; eax = 40
mov  ecx, [field10]        ; ecx = 5
mov  ebx, [esp+0x98]       ; ebx = a parameter ("param")
lea  ebp, [ecx+eax]        ; ebp = field10 + field2 = 45
lea  ebx, [ebx+ebx*8]      ; ebx = param * 9
imul ebp, eax_param        ; ebp = 45 * param
shl  ebx, 2                ; ebx = param * 36        <- byte stride
mov  esi, [field8]         ; esi = 10
add  ebp, esi              ; ebp = 45*param + 10
mov  edx, [ebx + 0x482240] ; indexed load: SomeArray[param].field0
```

`param` does double duty: it's an array index into what looks like a
production/build-queue struct array (36-byte stride, base `0x482240`), and
a multiplier in the linear formula `(field10+field2)*param + field8` =
`(5+40)*param + 10`. That's exactly the shape of a **"time until the Nth
queued item completes"** calculation. Combined with this table's location
- immediately after the `MakeBuild()`/`MakeBuildShipyard()` debug-trace
strings and the 9 build-menu panel files - this reads as genuine
**production/build-timing constants**, not a single building's full stat
block as first hypothesized.

The adjacent 16x16 byte grid at `0x61220` (where `0x19`=25 is overwhelmingly
the most common byte, with small values 1-18 in specific columns per row)
still looks like a build-menu slot/prerequisite matrix by inspection, but
has not yet been traced the same way.

**Status**: table location and "these are real, individually-used engine
constants" is now confirmed by disassembly, not just pattern-matching.

**Follow-up this session**: traced `call 0x413C00` and more of the
`0x482240` struct. The enclosing function (`0x41AD40`-`0x41AEDB`) turned
out to be a **build-queue UI slot renderer**, not a pure timing
calculation: alongside the `(field10+field2)*param+field8` timer formula,
it copies a C-string from the struct's `+0x00` field (a name pointer,
copied via `strlen`+`memcpy` into a fixed text buffer at `0x4815D8`) and
calls `0x459720` - an `itoa(value, buffer, base=10)`-shaped call (args
`push 0xa; push buffer; push value`) - then draws characters via two calls
to `0x413C00` at slightly offset x-positions each tick, i.e. a **scrolling
marquee effect** for a build-queue item's name/timer text. This confirms
the struct at base `0x48223C` (36-byte stride, `0x482240` is field `+4`
within it) is a **runtime build-queue slot array** (one entry per active
construction order, with a name-string pointer + scroll/counter state),
not a master per-unit-type stat table - tempering the very first read of
this struct. `0x413C00` itself is a low-level character/text-draw
primitive, not gameplay-specific. Exact semantics of every field are still
open, but the struct's *purpose* (queue-slot UI state, not type stats) is
now reasonably clear.

## Unit/building type table architecture (0x490920) - stats are likely data-driven, not hardcoded

Tracing the caller of the `0x611A0` constants function led to a much bigger
structure: a global array at VA `0x490920`, indexed as `TypeTable[id*4]`,
where `id` comes from an object's first field (`[esi]`, i.e. `this->type_id`).
Each entry is a pointer to a per-type struct with **843 references across
the whole binary** - by far the most pervasively-used piece of data found
so far, consistent with being the central unit/building type-info table.

Fields observed being both read and written on these structs (offsets from
the struct's own base): `+0x144`, `+0x150`, `+0x154`, `+0x174`, `+0x178`,
`+0x1c4` (at least - the struct is 456+ bytes based on the highest offset
seen). These are exactly the kind of slots that would hold HP, cost, attack,
sprite references, etc.

**However**: tracing the function that *writes* several of these fields
(entry ~VA `0x401960`) shows it is a **loop** over a runtime array
(`[edi + 0x20 + i*4]`, count from `[edi+0x20]`) that, for each element,
calls setter methods (`0x401710`, `0x401740`, `0x401780`, `0x401790`, ...)
on the corresponding `TypeTable[id]` entry, passing values read from that
runtime array - not literal constants embedded in this function. This
means the actual stat *values* most likely come from an external, loaded
data source (very plausibly `.stg`, which is exactly the kind of
per-scenario/per-unit data file that would feed a stats-loading routine
like this), rather than being hardcoded as immediates anywhere in `.text`.

**Practical implication**: continuing to search `.exe`/`.data` for a static
"unit stats table" is likely to have diminishing returns - the
architecture suggests these values aren't there at all in literal form.
`.stg` is the more promising next target for actually finding per-unit
numbers; the exe work here at least now tells us **what shape** that data
probably takes (a per-object-type record with fields including at least
`+0x144/0x150/0x154/0x174/0x178/0x1c4` in some internal struct layout) and
gives a landmark (`0x490920`, the type table) to cross-reference against
if/when `.stg`'s own structure is decoded.

## `CalcurateApowerWithAtb` corrected - it's a UI bounce-icon table, not combat math

Continued tracing (this session) found the earlier documented table location
was off by 4 bytes, and traced its one and only call site - overturning the
"attack power formula" hypothesis entirely.

- **Real table base is VA `0x464EA0`** (file offset `0x62EA0`), not
  `0x464E9C`/`0x62E9C` as first documented. The extra leading dword at
  `0x62E9C` is an unrelated zero, not part of the table; the interleaving is
  `(Atb, Value)` pairs of `int32` starting at `+4`, which is what makes the
  previously-published `Atb`/`Value` columns line up correctly - the table
  data itself was read right, only the base address (used to search for
  code references) was wrong, which is why no reference could be found
  before.
- **Exactly one direct code reference exists**, at VA `0x42B7BA`/`0x42B7C3`
  (`mov eax, [edx*8 + 0x464ea4]` / `mov ecx, [edx*8 + 0x464ea0]` - Value and
  Atb read via the same `edx` row index). The enclosing function
  (`0x42B780`-`0x42B7D6`) computes `edx = B % 21` from some global counter
  `B` (`[0x49144C]`) - the mod-21 confirms the table really does have 21
  rows - then calls `0x413EB0` with args `(x=Atb[edx], y=Value[edx]+A-70,
  w=32, h=32, sprite)` where `A=[0x462C00]` and `70` is the table's own
  documented midpoint constant (`0x46`).
- **`0x413EB0` is a sprite-blit routine**, not a math/stat function - it
  copies pixel bytes into a screen-sized buffer at `[0x481140]` using
  stride `[0x481550]`, with an inner loop that skips a run of transparent
  pixels whenever it sees byte value `1` followed by a count -
  **this is the exact same marker=1 transparent-skip RLE scheme documented
  in `spr.md`**, confirming that scheme is a genuine core engine blit
  primitive, not just an artifact of our own reverse-engineering of the
  `.spr` file format.
- **Conclusion**: despite the debug-trace name literally translating to
  "Calculate Attack Power With Attribute", the table's only confirmed use is
  as a set of 21 `(x, y)` offsets driving a bouncing/orbiting 32x32 icon
  (a cyclic animation keyed on a frame counter mod 21) - almost certainly a
  UI/HUD decoration, not a damage calculation. Either the name is misleading
  (repurposed/shared code), or the real "attack power" use of this data
  exists elsewhere and wasn't found by searching for direct references to
  the table's address. Either way, this table should **not** be relied on
  for combat-formula semantics in the OpenRA port.

## `LoadStage()` fully traced - confirms `.stg`'s file size and a header structure

Traced end to end (VA `0x4259D0`-`0x425C0C`, the function whose body
contains both `LoadStage()` debug-trace call sites). This is the single
most productive trace this session - it directly cross-validates the
independently reverse-engineered `.stg` format (`stg.md`) from the *other*
direction (code, not file bytes) and explains what its previously-undecoded
3488-byte header actually is.

**File I/O** (helper at VA `0x425990`, called twice from `LoadStage()`):
```
fopen(path, "rb")
fread(buffer=0x57C640, size=1, count=0x37E8, file)   ; 0x37E8 = 14312 decimal
fclose(file)
```
`0x37E8` = **14312 bytes exactly** - the fixed `.stg` file size documented
in `stg.md` from pure black-box analysis, now confirmed from the loader
code itself: it's a single flat `fread` of the whole file into one static
buffer, not a variable-length or streamed read.

**Mission table** at VA `0x4642D0`, indexed by a scenario-number parameter
`edx` passed into `LoadStage()`, is a literal array of 21 `.stg` path
strings:
```
0  cusmap\multi1.stg
1-10   map\kstg1.stg  ... map\kstg10.stg
11-20  map\jstg1.stg  ... map\jstg10.stg
```
Two 10-mission campaigns (`k`-prefixed and `j`-prefixed) plus one default
skirmish entry - this is the authoritative campaign/mission ordering,
confirms the `k`/`j` filename prefixes seen across the `.stg`/`.map`
inventory correspond to two separate campaigns (one per faction, matching
the `Knight.pal` naming for the `k` side), and gives exact load order for
anyone building a campaign-select UI in the OpenRA port.

**The `0x57C640` load buffer's layout, and what the `.stg` header actually
is**: after a successful `fread`, if the *primary* (mission-indexed) load
path was used, the code jumps straight past a large default-initialization
block to `0x425BBA`. If instead the *fallback* path is used (fixed
`cusmap\` prefix + a runtime-set scenario name, taken when the mission
index is 0/unset), a successful load is immediately followed by a block at
`0x425B33` that writes a long run of small hardcoded integer constants
(`1,2,3,4,5,6,8`, no big numbers) into **an array of exactly 8 records of
436 bytes each** - `8 * 436 = 3488`, which is **exactly** `stg.py`'s
`RECORD_START` constant, i.e. exactly the whole previously-undecoded
`.stg` header region documented as "header/player-slot sections
(undecoded)" in `stg.md`. This confirms:
- The `.stg` header is **8 fixed-size 436-byte slot records**, not the
  "5-slot" guess in `stg.md` - almost certainly per-player slots (8 is a
  plausible max-player count for a scenario), given every default value
  written into them is a small config-shaped integer (build/unit-count
  caps, flags), not anything stat-sized.
- Immediately after that (`0x425BBA`-`0x425BFD`, and this part *always*
  runs regardless of which load path succeeded), two more small tables get
  written into the same buffer: a 15-entry power-of-two bitmask table
  (`1<<n` for `n=0..14`, generic engine bit-flags, not file-specific) and,
  more interestingly, **the byte at `+4` of each of the 8 slot records
  gets overwritten from a real embedded static table** at VA `0x463520`
  (file offset `0x61520`): raw bytes `[180, 180, 70, 23, 36, 164, 50,
  98]`, one per slot. Semantics unconfirmed (not a clean small enum), but
  it's a genuine per-slot default worth checking against real `.stg`
  files' bytes at offsets `4, 440, 876, ...` (`4 + i*436`).
- **Practical next step for `.stg`**: re-open `stg.md`'s header analysis
  now that the true record boundaries (8 x 436 bytes, not 5 x unknown) are
  known from code, rather than guessed from byte-difference runs - this
  should make it much more tractable to map real per-file header bytes to
  specific slot-record fields than blind pattern-scanning was.

## `Barrack::Make()` corrected - it's the unit-training dispatcher (user call)

The user pushed back on the initial "treasure pickup handler" read of
`Barrack::Make()`, suggesting it (and `Hq::Make()`) are more likely the
unit-training dispatch functions instead. Re-examining with that framing -
and, crucially, fixing an arithmetic slip in reading its `switch` (the
third case is `param == 0x2D`, not `0x2C` as first transcribed: `eax =
param - 0x23`, then `dec eax` twice more with intermediate zero-checks
gives cases `0x23`, `0x24`, `0x23+1+9 = 0x2D`) - led straight to a full
confirmation, via the type-registration table below.

`Barrack::Make()`'s one literal stat-shaped write, `mov dword ptr
[esi+0x1c4], 0x64` (100), still isn't a per-building stat (see the
88-site reuse of that offset noted previously) - it's set alongside a
queue-sequence-ID assignment and a `this+0x200` counter increment right
after the training-time lookup, i.e. it's a **construction-progress reset
to 100 (percent)** for the newly-queued training order, not a type stat.

## Type-registration table at `0x41A150`-`0x41ACB4` - the master unit/building roster

This is the best exe finding of the session, and directly confirms the
user's correction above. **88 calls total** to a small helper at
`0x41A0F0` (found by scanning `.text` for every relative `call` targeting
it - 88 is not just "a lot", it's the *complete* set; there are no other
call sites anywhere in the binary), each registering one entry into an
array of 36-byte records starting at VA `0x482238` (this is the same
struct the *prior* session partially reverse-engineered from the read
side as a "build-queue slot" with a name-string field at `+4` relative to
`0x48223C` - `0x482238+4 = 0x48223C`, so the two analyses agree exactly on
field `+0x00`, and the write side below supersedes the read side's
guesses for the rest of the struct).

`0x41A0F0`'s body (disassembled) shows it does nothing but store its
arguments into `record[index]`, confirming the field layout:

```
record[index] at VA 0x482238 + index*36:
  +0x00  arg2  category code            (int, compared directly by e.g. Barrack::Make's switch)
  +0x04  arg7  descriptor pointer       (into a runtime, 32-byte-stride struct array at 0x57AA88+ -
                                          presumably per-type stats/sprite refs, but populated at
                                          runtime so not readable statically)
  +0x08  arg3  constant 8               (every one of the 88 calls passes exactly 8 - meaning unclear)
  +0x0C  arg5  sequential unique ID     ("uid" below)
  +0x10  arg4  small flag               (0/1 for entries 0-0x3a, jumps to a uniform 3 from 0x3b on)
  +0x14  arg6  sub-index, almost always 1
  +0x20  arg8  per-type sub-table ptr   (into a runtime, 16-byte-stride array at 0x46A798+ - only
                                          set, i.e. nonzero, for entries from idx 0x18 onward)
```

Extracted the full table (index, category, uid, sub-table-pointer) with a
capstone-based linear scan that tracks `mov reg,X` immediately before each
`push reg`, to resolve register-indirect pushes back to their source. Key
findings from the data:

- **`index` runs 0-0x58 (88 slots)**, but not in strict order - a handful
  of indices (`0xe`, `0x41`-`0x4a`, `0x4d`-`0x4e`) are registered out of
  sequence, later in the code, as if backfilling gaps left by an earlier
  pass (possibly conditional/faction-specific registration).
- **Indices 0-23 look like buildings**: no per-type sub-table pointer set
  (`arg8`/`+0x20` stays literal `0`), and several pairs of consecutive
  indices share the *same* descriptor pointer (e.g. idx 0,1 both use
  `0x57AA88`; idx 2,4 both use `0x57AAA8`). 24 entries / 2 (a plausible
  per-faction pairing) = 12, close to the user-confirmed 11-building
  roster (Headquarters, Barracks, Mill, Radar, Barracks2, Heavyarms,
  Shipyard, Airport, Stable, Temple, Temple2).
- **Indices 24-88 look like units**: from here on every entry gets its own
  distinct descriptor pointer (cleanly incrementing by `0x20`=32 bytes)
  *and* its own per-type sub-table pointer (cleanly incrementing by
  `0x10`=16 bytes, into the `0x46A798+` array). Category codes `0x22`
  through `0x44` (35 distinct categories) appear almost entirely in
  **pairs of two consecutive indices** - overwhelmingly consistent with
  one entry per faction (matching the `k`/`j` two-campaign structure
  `LoadStage()`'s mission table confirmed independently, above), with a
  few unpaired singles presumably faction-exclusive units.
- **This directly confirms `Barrack::Make()`'s dispatch**: category `0x23`
  is registered at index `0x1A` with sub-table pointer `0x46A7B8`;
  category `0x24` at index `0x1C` with pointer `0x46A7D8`; category `0x2D`
  at index `0x1E` with pointer `0x46A7F8` - **exactly** the three
  addresses `Barrack::Make()` reads a tier-indexed training duration from
  (`[this->field_0xfc * 16 + <pointer>]`). This is not a coincidence; it's
  the registration and the consumption of the same three per-unit
  training-time sub-tables, cross-confirmed from two independent code
  paths.
- **The "uid" field (arg5) is a near-perfectly sequential ID**, running
  `0xB7` (183) up to `0x11E` (286), skipping only `0xF6`-`0x103`
  (246-259, 14 unused values) and with two exact duplicates (`0xB7` at
  idx 0/1, `0xBB` at idx 7/8). This *looks* like a master type-ID space -
  **but it does not match `.stg`'s observed `type_id` range**, which is
  overwhelmingly small values `0`-`147` (104 distinct values total, see
  `stg.md`) with no overlap with `183`-`286`. So either `.stg`'s
  `type_id` indexes a *different*, lower-numbered registration (this
  88-entry table would then only be a subset of the full roster,
  registered via a still-unfound sibling of `0x41A0F0` for IDs `0`-`182`),
  or this table's `uid` isn't the same numbering space as `.stg`'s
  `type_id` at all (e.g. it could number a visual/effects subsystem
  instead). **Open** - see below.

**Practical upshot**: the user's correction was right, and tracing it
found the actual roster-registration mechanism, including firm proof that
`Barrack::Make()` (and by the same pattern, presumably `Hq::Make()` and
the other building `::Make()` methods) really is the unit-training
dispatcher, not a generic constructor. The real per-type *stat values*
(HP, cost, attack) still haven't been located as literal data - they live
in the runtime-populated 32-byte descriptor structs this table points to
(`0x57AA88`+), which are zero-initialized in the file and therefore not
readable statically; finding what populates *those* is the natural next
step.

## Unit stat values extracted - confirmed costs for the whole roster, incl. the 3 Barrack units

Direct follow-up to the type-registration finding above, and to the user's
confirmation that Barrack trains exactly 3 units (Archer, Gunner, Footman) -
matching the 3 categories (`0x23`, `0x24`, `0x2D`) `Barrack::Make()`
switches on. This is the first session to extract literal, high-confidence
per-unit stat *values* (not just architecture) directly from `.text`.

**Found the setter function**: `0x40A7C0`, called **67 times** from a
contiguous block at `0x40A800`-`0x40AEC8`. Its body:

```
index = arg1*2 + arg2                       ; arg1=slot pair id, arg2=faction (0/1)
record@ (0x46A590 + index*16):
  +0x00 = arg3   (small int, 1-3 - a class/tier tag, identical for both faction variants)
  +0x04 = arg4   (always equal to arg3 in every call observed)
  +0x08 = arg5   (see below)
  +0x0C = arg6   (the same 32-byte descriptor pointer used by the type-registration table)
```

**Cross-validated against the type-registration table by descriptor
pointer**: every `record.+0x0C` value (the last pushed arg) is one of the
exact same `0x57AA88`+ pointers the earlier `0x41A0F0` registration calls
used, letting the two tables be merged 1:1. This also resolves *where*
`Barrack::Make()`'s three per-tier training-lookup addresses
(`0x46A7B8`/`0x46A7D8`/`0x46A7F8`, see above) come from: they're each
exactly `record.+0x08`'s address for the matching category - i.e.
`arg5` **is** the value `Barrack::Make()` reads.

**`arg5` is almost certainly the unit's build/training cost.** Every one
of the 67 values is an exact multiple of 100 (100, 200, 400, 500, 600,
700, 800, 900, 1000, 1100, 1300, 1400, 1500, 1600, 1700, 2000, 2200) -
classic round RTS resource-cost numbers, not noise, and (see below) they
divide *exactly* through the game's own division-by-100 formula.

**The 3 Barrack categories, merged from both tables**:

| Category | Registration idx (faction A / B) | Cost (both factions) | Unit |
|---|---|---:|---|
| `0x23` | `0x1A` / `0x1B` | **400** | Archer or Footman (unresolved) |
| `0x24` | `0x1C` / `0x1D` | **700** | **Gunner** (user-confirmed priciest) |
| `0x2D` | `0x1E` / `0x1F` | **400** | Archer or Footman (unresolved) |

No string, sprite-load site, or other reference ties a category code
directly to a unit name by itself (checked: `fnt1\archer.spr` and
`fnt1\footman.spr` both exist and load via an unrelated generic
asset-preload sequence with no visible type/category tag at the load
site; no `gunner.spr` file exists at all, so "Gunner" - if that's the
literal in-game display name - either reuses another sprite or is named
differently internally, consistent with the earlier finding that display
names likely live in `language.gtf`, not `.spr` filenames). **The user
confirmed Gunner was the priciest of the three to train**, which pins
category `0x24` (cost 700) as Gunner - matching the "gunpowder = advanced/
expensive tech" convention guessed beforehand. Archer vs. Footman between
the two 400-cost categories (`0x23`/`0x2D`) is still unresolved (tied
cost, no distinguishing signal found yet).

**Training time**: traced how `Barrack::Make()` consumes this cost value
(disassembly recap from the earlier section): it's divided by exactly 100
(confirmed by computing what the `0x51EB851F`+`sar 5` instruction sequence
actually divides by: `2^37 / 0x51EB851F ≈ 100.0000003`, i.e. this is the
standard compiler-generated idiom for signed division by 100, not by 60 as
might be assumed for a "time" value) and stored to `this+0x168`, while
`this+0x1c4` is separately reset to a flat `100`. The architecturally
sensible reading of these two numbers together: **training likely runs for
a fixed number of ticks (100) for every Barrack-tier unit, with
`cost/100` being the per-tick resource payment deducted from the player**
(so that by tick 100, exactly `cost` total resources have been spent) -
this would mean Barrack unit training time is *constant across all 3
units* (archer/gunner/footman all take the same real-world time to train),
and only the resource *cost* differs between them, rather than cost and
time both varying per unit. This reading is **not independently confirmed**
by tracing what consumes `this+0x168`/`this+0x1c4` afterward (i.e. the
actual per-tick game loop that increments progress and eventually
completes the unit) - only inferred from the shape of the numbers. The raw
tick-to-real-seconds conversion rate is also unconfirmed.

**Practical upshot for OpenRA conversion**: high confidence in the 3 raw
cost numbers (400/700/400) and in the general shape of the roster (67
distinct type entries, each with a cost, a class/tier tag, and a
descriptor pointer for further stats like HP/attack still not found -
those would live in the 32-byte descriptor struct itself, not in this
16-byte cost record). Lower confidence in which literal number belongs to
which named unit, and in the exact translation of the training-progress
mechanism into real seconds.

## Extended roster tracing - Hq, Shipyard, Heavy Arms

Same technique as `Barrack::Make()` above (find the `::Make()` trace
string, disassemble its switch on `param`, cross-reference the category
codes against the type-registration table's `uid`/`extra` and the cost
table's `arg5`), applied to the other buildings' `::Make()` functions.

**`Hq::Make()`** - VA `0x438BC0` (trace string `"0000 Hq::Make()"` at file
offset `0x633E8`). Switches on exactly 2 categories - matches the user's
"HQ trains a worker and a harvester" (2 units):

| Category | Cost | Unit (user-confirmed) |
|---|---:|---|
| `0x22` | 700 | Peasant (worker) |
| `0x2A` | 1400 | Bull (harvester) |

(`this+0x1c4` is reset to `0x64`/100 here too, same as `Barrack::Make()` -
second independent confirmation of the "fixed 100-tick training,
cost/100 = per-tick drain" pattern.)

**`Shipyard::Make()`** - VA `0x446260` (3 debug-trace strings at file
offsets `0x643F8`/`0x64410`/`0x64428`, one per invalid-`param` branch -
this class has multiple separate "unrecognized category" trace call
sites rather than one shared one, unlike `Barrack`/`Hq`, but they all
serve the same purpose: fire when `param` isn't a valid category).
Dispatches via a 7-entry jump table on `param - 0x26` (valid range
`0x26`-`0x2C`); 3 of those 7 values (`0x28`, `0x29`, `0x2A`) fall through
to an invalid-category handler, leaving **4 real categories**:

| Category | Cost (reg. idx pair) | Unit (user-confirmed) |
|---|---|---|
| `0x26` | 2000 / 1300 | Cannon Ship |
| `0x27` | 1000 / 1000 | Transport |
| `0x2B` | 1300 / 700 | Arrow Ship |
| `0x2C` | 500 (single registration) | Submarine - **Japan-exclusive** |

**`_HeavyMaker::Make()`** - VA `0x446EE0` (trace string `"0000
_HeavyMaker::Make()"` at file offset `0x64440`). Switches on exactly 2
categories, matching "Armored Car + Cannon":

| Category | Registration | Cost | Unit (user-confirmed) |
|---|---|---:|---|
| `0x35` | **one** descriptor (`0x57B108`), shared by both factions | 1400 | Cannon - same unit both factions |
| `0x25` | **two distinct** descriptors (`0x57AE48` Korean, `0x57AE68` Japan) | 1200 / 1300 | Armored Car - **a genuinely different unit per faction**, not a cost variant of one type |

The registration-count difference is the key signal here: a category with
only one descriptor pointer (like `0x35`/Cannon here, and `0x2C`/Submarine
above) means the engine treats it as a single shared type; a category
with two distinct descriptor pointers (like `0x25`/Armored Car) means the
two factions each have their own separate type record - different sprite/
stat data, not just a different price tag on the same unit. This is a
more reliable signal than raw cost alone for telling "same unit,
different price" apart from "different unit entirely".

**`_AirMaker::Make()`** - VA `0x428E40` (trace string `"000
_AirMaker::Make()"` at file offset `0x62A40`). Switches on 3 categories,
matching "Airport trains Transport, Fighter, Bomber":

| Category | Registration | Cost (Korean / Japan) | Unit (user-confirmed) |
|---|---|---:|---|
| `0x32` | one descriptor (`0x57B028`), shared by both factions | 1700 / 1700 | Bomber - same type both factions, but **Japan-exclusive to actually build** (the single-shared-descriptor pattern doesn't by itself imply exclusivity - the `0x2C`/Submarine case above was *also* single-descriptor yet exclusive to Japan only, so exclusivity is enforced by other logic not visible in this table; it was user-confirmed here, not inferred) |
| `0x31` | two distinct descriptors (`0x57AFC8` Korean, `0x57AFE8` Japan) | 1500 / 1600 | Transport or Fighter (unresolved) |
| `0x33` | two distinct descriptors (`0x57B048` Korean, `0x57B068` Japan) | 2000 / 2200 | Transport or Fighter (unresolved) |

Note on the "single descriptor = exclusive" heuristic from the
`_HeavyMaker` section above: it does **not** reliably predict
faction-exclusivity by itself - `0x35`/Cannon (single descriptor) is
shared by both factions, while `0x2C`/Submarine and `0x32`/Bomber (both
also single descriptor) are Japan-exclusive. The single-vs-double
descriptor split reliably distinguishes "one unit design" from "two
different unit designs sharing a building slot", but *which* single-design
units are faction-restricted requires other information (user knowledge,
or tracing whatever check gates the build menu by faction - not yet
found).

**`Stable::Make()`** - VA `0x433D20` (trace strings `"0001 Stable::Make()"`
at file offset `0x6327C` and `"0000 Stable::Make()"` at `0x63290`).
Switches on a **single** valid category, `0x28` - matches the user's "one
unit, but different per faction" framing exactly:

| Category | Registration | Cost (Korean / Japan) | Unit (user-confirmed) |
|---|---|---:|---|
| `0x28` | two distinct descriptors (`0x57B148` idx `0x37`, `0x57B168` idx `0x38`) | 900 / 900 | General |

Same "two distinct type records under one category code" shape as
Armored Car and the Airport's Transport/Fighter pair, but here both
factions' cost is identical (900/900) rather than differing - so the
double-descriptor pattern really does mean "different unit per faction"
in general, independent of whether the cost happens to match. "General"
matches `GENERAL.SPR` in the asset inventory (see `spr.md`'s file list).

**`Temple::Make()`** - VA `0x445520` (trace strings `"0001
Temple::Make()"` at file offset `0x643B8` and `"0000 Temple::Make()"` at
`0x643CC`). Switches on 2 categories:

| Category | Registration | Unit (user-confirmed, verified in-game) | Cost (user-confirmed) |
|---|---|---|---:|
| `0x29` | two distinct descriptors (`0x57B0C8` idx `0x34`, `0x57B0E8` idx `0x35`) | Priest | 900 |
| `0x34` | two distinct descriptors (`0x57B088` idx `0x32`, `0x57B0A8` idx `0x33`) | Shaman/Witch (Temple2 tier) | 800 |

**Discrepancy, unresolved**: this session's own raw cost-table extraction
(matching descriptor pointers `0x57B0C8`/`0x57B0E8` to a cost-setter call)
found `0x29` = 800 and `0x34` = 900 - the *opposite* of the user's
in-game-verified values above. The user's numbers are what's recorded as
ground truth (they checked the live game), but the code-side mismatch
isn't explained - possible causes not yet checked: an off-by-one in the
idx-to-descriptor pairing used by the extraction script, a second cost
overwrite elsewhere for these same descriptors, or the two categories'
labels being swapped somewhere between registration and cost tables.
Worth re-tracing directly (disassemble the actual `call 0x40A7C0` sites
for these two descriptors again by hand) if this matters later.

`0x29`'s descriptor pointers (`0x57B0C8`/`0x57B0E8`) are the *exact same*
addresses used by a completely unrelated Heavy Arms registration entry
(category `0x43`, idx `0x55`/`0x56` - see the `_HeavyMaker` roster
extraction above) - i.e. the type-registration table has two different
category codes, in two different buildings' dispatch tables, both
pointing at the same underlying 32-byte descriptor struct. This is a
genuine data-level oddity, not a parsing artifact (independently
re-verified by re-querying the raw registration/cost call records for
both descriptor addresses). No equivalent sharing was found for any of
Barracks' categories. Whether this reflects deliberate asset/stat reuse
between Temple2 and some Heavy Arms unit, leftover/dead registration data,
or something else entirely is unresolved.

All 7 buildings with `::Make()` trace strings have now been traced.

## Barracks2 - a genuinely separate, untraceable-by-string class

The user asked about Barracks2 (trains Miner + Occupier/Thief) - the
prior session had guessed it shares the `Barrack` class via a type
parameter, since no `"Barrack2::Make()"`-style string exists. That guess
turned out wrong. Found by widening the search beyond debug-trace
strings: every `::Make()`-style dispatcher references the same global
sequence counter at VA `0x462D18` (used to hand out queue-slot IDs).
Enumerating **all** 26 references to that address in `.text` (not just
the ones near known trace strings) turned up one dispatcher with no
matching class-name string:

**Dispatcher at VA `0x44C3A0`** - same shape as every other building's
`Make()` (switch via `sub eax, BASE; je`, per-tier training-time lookup,
`this+0x1c4` reset to `0x64`), but its "unrecognized category" branch
prints a leftover placeholder string, `"this is test!!!!!!!!!!!!!!!!!!!"`
(file offset `0x4666CC`) - the developer never gave this class a real
trace name, unlike the other 7. Switches on exactly 2 categories:

| Category | Registration | Cost (Korean / Japan) | Unit (user-confirmed) |
|---|---|---:|---|
| `0x30` | one shared type record | 800 / 600 | Miner - **Korea-exclusive**; the 600 "Japan" cost-table entry is dead data, never reached in play |
| `0x2E` | two distinct type records | 800 / 800 | Occupier/Thief - available to both factions |

Cross-checked by matching the dispatcher's per-tier lookup base addresses
(`0x46A8B8` for `0x30`, `0x46A898` for `0x2E`) directly against the
type-registration table's `extra` field for those categories - exact
match, same technique used for every other building.

**Side note, a genuine per-instance constructor found while chasing this**:
searching for hardcoded `uid` values led to VA `0x42CE56`-ish, a real
object constructor (sets a vtable pointer `0x45BE30` and hardcodes
`[esi+0x1F4] = 0xB7` - Archer's exact `uid`). A second, differently-vtabled
constructor at `0x446D2A` *also* hardcodes `0xB7` at the same offset,
which weakens confidence that `+0x1F4` is a reliable, unique "type id"
field across all classes (could be two states/variants of Archer, or
could be an unrelated field colliding by coincidence - not resolved).
Direct-immediate searches for the Miner/Occupier `uid` values (`0xE5`,
`0xE6`, `0xE7`) at this same offset found nothing, so this path didn't
pan out for finding their stats directly, but the technique (search for
literal `uid` immediates to find per-type constructors) may still be
useful for other categories.

## No separate class for a Witch/Shaman House

Follow-up user question: since Barracks2 turned out to be a genuinely
separate, unnamed class, does the same apply to a "Witch House" (the
building implied by Temple2/Shaman-Witch)? Re-ran the same audit that
found Barracks2 - enumerated **every one** of the 26 references to the
shared queue-counter `0x462D18`, not just the ones adjacent to known
trace strings, and checked each previously-unaccounted-for address
(`0x42FE5A`, `0x4346D7`, `0x44C218`, `0x44F3E2`, `0x4395AC`, `0x44603D`,
plus the two already-noted `uid`-hardcoding constructors). **All of them
are simple, straight-line per-instance constructors** - none contain a
`sub eax, BASE; je`-style category switch, which is the one structural
feature every real `::Make()` dispatcher (including the unnamed
Barracks2 one) has. **Conclusion: no hidden dispatcher exists** - Temple2's
output (category `0x29`, probably Shaman/Witch) really is handled inside
`Temple::Make()` alongside Priest, exactly as already documented, unlike
Barracks2 which genuinely needed its own function. This also means
Barracks2 is very likely the *only* building with this "shares no debug
name, dispatches separately" quirk among the 7 documented buildings -
every other category is accounted for by one of the 8 known dispatchers
(7 named + Barracks2).

**Incidental finding from this audit**: several of the per-instance
constructors checked here (`0x4346D7`→sets `+0x1F4`=`0xC`; `0x4395AC`→
`0x5C`; `0x44603D`→`0xC0`) hardcode *small* values at the same `+0x1F4`
offset the Archer constructor used for its (much larger) `uid`. This
further confirms `+0x1F4` is not a stable cross-class "type id" field -
different classes reuse the offset for different, unrelated small
integers (possibly a real gameplay stat, given the values are
HP/attack-plausible magnitudes, but not yet identified which one).

## Hunting for Barracks' unit stats (HP/attack) - architecture clarified, literal values still not found

User asked to search specifically for Archer/Gunner/Footman's stats
beyond cost. Confirmed the 32-byte descriptor structs at `0x57AA88`+
(one per roster entry, referenced by both the registration table's `+4`
field and the cost table's `+0xC` field) are **never referenced by a
third, literal address** anywhere in `.text` - checked directly for all
of Barracks' descriptors (`0x57AD88`, `0x57ADA8`, `0x57ADC8`, `0x57ADE8`,
`0x57AE08`, `0x57AE28`) and the array base (`0x57AA88`): exactly 2 refs
each, always the same registration+cost pair already documented. So
whatever writes real stat values into these structs does so through a
**runtime-fetched pointer**, not a literal address - which means it can't
be found by scanning for the struct's address as raw bytes, only by
tracing register values forward from wherever the pointer gets loaded.

### Follow-up session: traced the actual unit-spawn pipeline for Archer specifically

Pushed further by finding real `TypeTable[id]` object construction, not
just registration bookkeeping. Chain of discovery:

1. Found a real per-unit **object constructor** at VA `0x42CDC0` by
   tracing who calls the earlier-noted "hardcodes `uid=0xB7`" code
   (`0x42CE17`, inside this same function). Its caller, VA `0x421090`, is
   a small wrapper that `malloc`s exactly **0x264 (612) bytes**, calls
   `0x42CDC0` as the constructor, and stores the result into
   `TypeTable[id]` (`mov [esi*4+0x490920], eax`) - i.e. **612 bytes is
   the confirmed real size of a live unit-type object**, larger than the
   "at least 456 bytes" estimated in the prior session.
2. `0x42CDC0` itself (the Archer constructor, fully disassembled this
   time) turned out to be **generic map-placement/rendering logic** -
   footprint bounds (`+0x254/+0x258/+0x25C/+0x260`), a nested loop over
   tile positions calling known rendering helpers (`0x419580`,
   `0x419300`, `0x419280` - the same family used by `Barrack::Make()`'s
   "search nearby tiles" loop), and only small class/tier-tag constants
   (`0xC`, `3`, `2`, `1`, `0x20`, `5`, `0x3C`, plus the `uid=0xB7`) - none
   of these are HP/attack-sized values, and field `+0x144` here is
   fed as a **parameter** into a rendering call, not a combat stat.
3. `0x421090` (Archer's wrapper) is itself one of **35 entries in a
   dispatch table** at VA `0x40F5FC`, indexed by a small parameter
   (1-35) - confirmed by reading the jump table directly (not by
   assuming code order, which turned out unreliable: the block for
   index 1 sits textually next to an unrelated function). **Archer =
   index 3** in this table. The enclosing function (starting somewhere
   before VA `0x40F0A0`, not yet fully bounded) reads from more
   zero-initialized runtime tables (`0x46B5D2`, `0x46B5F8`, `0x57EFE8`+,
   24-byte stride) and looks like scenario/AI unit-spawn logic, passing
   the same `0x64` (100) constant into every wrapper call (consistent
   with the training-progress-target pattern, not a new stat).

**Assessment after this trace**: HP/attack are not simple hardcoded
immediates anywhere in this pipeline (`Make()` → training queue → this
35-slot spawn dispatch → per-type wrapper → constructor). Every literal
value found along the way is either a UI/bookkeeping constant (`100`,
class/tier tags `1`-`3`) or a rendering/placement parameter, never a
value in a HP/attack-plausible range tied to a specific unit. Combined
with the earlier finding that `TypeTable`'s own writer function loops
over a **runtime-populated array** (not literal constants) to call
setter methods, this is now fairly strong convergent evidence that combat
stats are genuinely data-driven from an external, still-unidentified
source (not `.stg`, not `.gtf`, not a static `.exe` table) - or are
computed from the small class/tier tags via a formula not yet traced,
rather than being stored anywhere as a flat per-unit number.

Widened the search to the registration array's descriptor-pointer field
itself (`+4`, i.e. VA `0x48223C`) and its base (`0x482238`, `0x482240`).
This surfaced a genuinely new, useful connection: a function around VA
`0x4101F1` reads `TypeTable[id]->field_0x178` and compares it against
`RegistrationArray[idx].category` (offset 0) - **meaning
`TypeTable[id]->+0x178` stores that unit's own category code**, a direct
link between the two systems that wasn't confirmed before. A second
cluster (`0x41BC00`-`0x41BEB0`) turned out to be **build-menu/tech-tree
prerequisite-checking UI logic**: it walks trained units, checks
`TypeTable[id]->+0x1c4 < 0x64` (100) - i.e. **"still training, not yet
100% complete"**, matching the training-progress hypothesis from the
`Barrack::Make()` trace above - then checks `TypeTable[id]->+0x178`
against hardcoded building-category constants (`5`, `0xB`, `0xC`, `0xD`,
`0x15`, `0x1C`, ...) to decide which build-menu options to enable, and
reads `TypeTable[id]->+0x1c8` as a trained/owned-count.

**Net result - a genuinely useful negative finding**: the six fields
flagged in the prior session as "exactly the kind of slots that would
hold HP, cost, attack" (`+0x144`, `+0x150`, `+0x154`, `+0x174`, `+0x178`,
`+0x1c4`) are now mostly explained as **bookkeeping, not stats**:
`+0x178` = category/type back-reference, `+0x1c4` = training progress
(0-100), `+0x1c8` = owned-unit count. None of the fields actually traced
so far turn out to be a combat stat. The real HP/attack values, if they
exist as literal data at all, are most likely inside the untraced part of
the 32-byte descriptor struct or elsewhere in the `TypeTable` struct's
remaining fields (`+0x144`, `+0x150`, `+0x154`, `+0x174` are still
unexplained) - tracing those specifically, or tracing forward from
wherever the descriptor pointer first gets loaded into a register, is the
concrete next step. This session's search was thorough (checked every
literal reference to the descriptor addresses and their containing
array, traced two substantial call-graphs stemming from those checks) but
did not turn up literal stat values.

## Open questions / next steps

- **Find real HP/attack stat values** - now downgraded to "probably not a
  flat table at all" after tracing the full spawn pipeline for Archer
  (`Make()` → 35-slot spawn dispatch at `0x40F5FC` → per-type wrapper →
  `0x42CDC0` constructor → `TypeTable[id]`, see "Follow-up session" above)
  without finding a single HP/attack-plausible literal anywhere in it.
  Remaining concrete leads, roughly in order of promise: (1) trace
  `TypeTable`'s still-unexplained fields `+0x144`/`+0x150`/`+0x154`/
  `+0x174` the same way `+0x178`/`+0x1c4`/`+0x1c8` just got resolved; (2)
  find the caller of the 35-slot dispatcher's enclosing function (starts
  before VA `0x40F0A0`, not yet bounded) to see what feeds it and whether
  it's scenario-load-time-only or also used for combat/AI unit creation;
  (3) trace whether the small class/tier tags (`1`-`3`, `0xC`, etc.) seen
  hardcoded in every constructor get fed into a formula elsewhere (like
  `CalcurateApowerWithAtb`'s table) that derives HP/attack at runtime
  rather than storing them as flat numbers.
- **Resolve Archer vs. Footman** (`0x23` vs `0x2D`, both cost 400) - Gunner
  is now confirmed as category `0x24` (user recalled it as the priciest of
  the three, matching the 700-cost category). Candidate approaches not yet
  tried: visually inspect `ARCHER.SPR` and `FOOTMAN.SPR` frames (both
  extractable via `spr_extract.py`) for a tell (e.g. one might have visibly
  more animation frames/variants than the other, or a HP/speed difference
  once the descriptor structs are decoded); trace the build-menu
  panel-to-category binding (the `pannelNN.fst` 3x3 grid files) to see if
  panel slot position ties to a category code.
- **Trace what consumes `this+0x168`/`this+0x1c4`** after `Barrack::Make()`
  sets them, to confirm or refute the "constant 100-tick duration, cost is
  a per-tick drain rate" reading of the training-time mechanism above.
- **Find the 32-byte descriptor structs' own fields** (`0x57AA88`+, the
  pointer stored at each roster entry's `+0x0C`/`+0x04` respectively in
  the two tables) - these are the most likely home for HP, attack, and
  other combat stats not yet found. Zero-initialized in the file, so this
  needs a code-reference search the same way `0x46A7B8` etc. were found.
- ~~Trace `Hq::Make()`, `Shipyard::Make()`, `_HeavyMaker::Make()`,
  `_AirMaker::Make()`, `Stable::Make()`, `Temple::Make()`~~ **done** - see
  "Extended roster tracing" above. All 7 buildings with `::Make()` trace
  strings are now traced.
- **New**: figure out what the `0x29`/Heavy-Arms-`0x43` descriptor-sharing
  oddity (found while tracing `Temple::Make()`) actually means - is it
  deliberate reuse, dead data, or something else? No other category shows
  this pattern.
- ~~Trace `CalcurateApowerWithAtb`'s call site~~ **done** - see above; it's
  a UI icon-bounce table, not a combat formula, so this is no longer a
  lead for stat values.
- ~~Trace `LoadStage()`~~ **done** - see above; confirmed `.stg`'s exact
  file size and explained its 3488-byte header as 8x436-byte slot records.
  **Follow-up**: go back to `stg.md` and re-map its header bytes now that
  the true 8x436 record boundaries are known from code, and check real
  `.stg` files' bytes at offset `4 + i*436` against the `[180, 180, 70, 23,
  36, 164, 50, 98]` default table found at VA `0x463520`.
- ~~`Barrack::Make()` is a dead end~~ **corrected** - it's the unit-training
  dispatcher, confirmed via the `0x41A0F0` type-registration table above.
- **New, not yet done**: find what populates the runtime 32-byte descriptor
  structs (`0x57AA88`+) that the registration table's `+0x04` pointer
  refers to - that's where the actual per-type stat values most likely
  live. Since those structs are zero-initialized in the file, the write
  site has to be found by code-tracing (search for direct references to
  `0x57AA88` and the other descriptor addresses, the same technique used
  to find `0x46A7B8` etc. from `Barrack::Make()`), not by reading `.data`.
- **Reconcile the `uid` numbering gap**: this table's `uid` field (183-286)
  doesn't overlap `.stg`'s `type_id` range (0-147) - find the sibling
  registration covering IDs 0-182 (presumably a near-identical call chain
  to `0x41A0F0`, elsewhere in `.text`), which should include the building
  IDs that would let `.stg` placements finally be named.
- Search near other named functions (`Harvest1`, `DecideCropWork`) for
  similar adjacent tables - the same "small table right before its
  debug-trace string" pattern may repeat, though it produced one UI table
  and one dead end (Barrack) so far, so this technique alone has a mixed
  track record.
- `.rsrc` section (12,288 bytes) not yet examined - may hold dialog/version
  resources.
- Full unit roster now has a **confirmed registration structure** (88
  entries, category codes, building/unit split, per-faction pairing - see
  above), but still needs the actual per-unit stat *values* (HP, cost,
  speed) - not found yet. The `.stg` "leading candidate" hypothesis from
  the prior session is now **downgraded**: `.stg`'s placement records only
  carry `(x, y, type_id, owner)`, and its header is now known to be small
  per-slot config records (8x436 bytes), not large enough for a full
  per-type stat table. Stats most likely live in the runtime descriptor
  structs at `0x57AA88`+ (see above) - finding what populates those is now
  the single most promising lead.
