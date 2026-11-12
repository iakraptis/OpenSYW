# STG format

Reverse-engineered from all 38 `.stg` files in `SYWAR/MAP/` and
`SYWAR/cusmap/`. Implemented in
opensy `SYWtoORA/tools/stg_extract.py`.

Each `.stg` is paired 1:1 by filename with a `.map` file (`jstg1.stg` <->
`jstg1.map`, etc.) - confirmed by `syw.exe` string references
(`map\jstg1.stg`, `LoadStage()`, see `exe.md`).

## Layout

```
[0 : 3488]    8 fixed-size records of 436 bytes each ("slot records") -
              record count and stride confirmed from `syw.exe`'s
              `LoadStage()` disassembly (see "Header structure" below and
              `exe.md`), which default-initializes exactly this shape
              before reading the real file over it. Field-level meaning
              still not decoded, but this is now known structure, not a
              guess: earlier revisions of this doc described "a 432-byte
              header" plus "up to 5 records on a 436-byte stride" found by
              manual byte-diffing - that was this same 8x436 array, just
              undercounted because most slots are unused/all-default in
              every observed file (bytes 0-448, i.e. all of slot 0 and the
              first 12 bytes of slot 1, are byte-identical across all 38
              files - only a minority of files configure any slot beyond
              that).

[3488 : ...]  placement table: 20-byte records, row-major:
                int32 x        - tile X on the paired .map
                int32 y        - tile Y on the paired .map
                int32 type_id  - unit/building/object type
                int32 owner    - player/faction id (1-4 dominate)
                int32 pad      - always 0

              Array ends at the first long run (>=10) of all-zero records.

[~10800+]     a different, undecoded data section (large non-coordinate
              values - possibly embedded strings). Deliberately excluded
              by the placement parser's zero-run boundary detection.
```

Every file is exactly **14312 bytes** regardless of the paired map's size -
unlike `.map`, which scales with `width*height` (see `map.md`). This is
consistent with `.stg` holding a fixed-capacity placement/scenario table
rather than terrain.

## Verification

**Objective**: for every one of the 38 stg/map pairs, every placement's
`(x, y)` was checked against the paired map's actual `(width, height)`.
2981 of 2983 parsed placements (37 of 38 files) fall cleanly within bounds,
and the coordinate ranges scale correctly with each map's real dimensions
(e.g. `jstg10.stg`, paired with a 148x148 map, has placements up to x=91;
`kstg1.stg`, paired with a 44x44 map, stays under 40). This is the same
kind of unambiguous, hard-to-fake signal that confirmed the SPR pixel
offset and the MAP header formula.

`kstg10.stg` is a known, documented exception: the zero-run boundary
heuristic picks up 2 extra records from the following undecoded data
section for that one file. `tests/test_stg_extract.py` encodes this exact
exception so it fails loudly if the count regresses.

**Visual**: overlaying placements on the paired map's tile-ID render
(`stg_extract.py --overlay`, color-coded by `owner`) shows exactly what
you'd expect:
- `cusmap/kmulti1.stg` (a skirmish map): a classic **4-corner symmetric
  starting-position layout** - one color per owner (1-4) in each corner,
  with unusual/other owner values clustered near the map center (plausibly
  neutral resource/treasure points).
- `MAP/jstg9.stg` (a campaign mission, 148x148, two landmasses separated by
  water): placements cluster along the coastlines of the two separate
  islands - opposing forces on separate landmasses, exactly what a
  naval-campaign mission would look like.

## Header structure - confirmed via `LoadStage()` disassembly

`syw.exe`'s `LoadStage()` (see `exe.md`) reads the whole 14312-byte file in
one `fread` into a static buffer, then - only on one of its two load-path
branches - default-initializes the first 3488 bytes of that same buffer as
**8 records of 436 bytes each**, writing small hardcoded integers
(`1,2,3,4,5,6,8`) into consistent per-record sub-offsets before the real
file contents (when present) take over. 8 is a plausible max-player count,
and every default value is config-shaped (not stat-sized), so these are
almost certainly **per-player scenario slots** - team/color/AI-difficulty
type settings - not terrain or unit data. One field (byte offset `+4`
within each record, i.e. absolute offsets `4, 440, 876, ...`) gets a
further default from a real embedded table in the exe: `[180, 180, 70, 23,
36, 164, 50, 98]`, one value per slot - not yet checked against real
`.stg` files' bytes at those offsets, but a natural next step given the
record boundaries are now known precisely rather than guessed.

## Slot fields and placement count - SOLVED 2026-10-04

The 8 slot records are the live per-player state (slot `p` = player `p`, slot 0 unused), read straight from the
file: `+0x04` colour, `+0x08` own bit, `+0x0c` alliance mask, `+0x10` faction (0 Korea, 1 Japan), `+0x14`
controller (1 human, 3 computer, 0/4 not playing), `+0x18` money. The placement count is the int32 at `0x2A68`
(so the zero-run heuristic below is no longer needed), and `type_id` is the placement kind: 1-15 buildings, 16-35
units. Details and the campaign logic: opensy `SYWtoORA/docs/Campaign.md`.

## Open questions

- **Per-field meaning of the 8 slot records** (see above) is still not
  decoded - the record *boundaries* are now confirmed from code, but which
  byte offsets hold e.g. team color vs. AI level vs. starting resources is
  still open. Cross-checking real files' bytes at `4 + i*436` against the
  `[180,180,70,23,36,164,50,98]` exe default table is the most promising
  concrete next step.
- **`type_id`** has 104 distinct values across all files - more than the
  ~7-11 known buildable building types (see `exe.md`), so this field likely
  also covers decorative props, resource nodes, and non-buildable objects,
  not just player-buildable units/buildings. No mapping from `type_id` to
  a specific unit/building name is confirmed yet.
- ~~**The undecoded section past the placement array**~~ (solved: map objects, see below). It
  contains large, non-coordinate-like values, possibly embedded strings
  (player/mission names?). Not yet investigated.
- Whether `type_id`/stat values here connect to the `syw.exe` unit-type
  table at VA `0x490920` (see `exe.md`) is unconfirmed - and now looks
  less likely than previously thought: `.stg`'s only large data region
  (the placement table) carries just `(x, y, type_id, owner)`, and its
  header is now known to be 8 small per-slot config records (436 bytes
  each), too small to hold a full multi-field record per one of the 104
  distinct `type_id` values. `.stg` no longer looks like the source of
  the TypeTable's runtime stat data - see `exe.md`'s "Open questions" for
  where that search is headed instead.

## Map objects (trees, totems, braziers, flags) - SOLVED 2026-10-03

The undecoded section past the placement table holds the map's **objects**. `syw.exe`'s stage loader
(`0x40ed10`) loads the whole `.stg` at `0x57c640` and reads them from fixed offsets:

```
[0x2A98]  int32 count (max 200)
[0x2A9C]  int32 type[200]
[0x2DBC]  int32 x[200]      tile coordinates on the paired .map
[0x30DC]  int32 y[200]
```

Just before them is the map file name and the map's title (`map\kmulti1.map`, `Fighter`).

The loader switches on the type and calls one setup function per object (`0x4271a0 + 0x90*k`, named by their
error strings `SetTree1()`-`SetTree22()`, `JangSung()`, `FireTable()`, `KoreaFlag()`, `JapanFlag()`):

| type | object | type | object |
|---|---|---|---|
| 50-56 | SetTree1-7 | 59 / 60 | KoreaFlag / JapanFlag |
| 57 | JangSung (village guardian totem) | 61-75 | SetTree8-22 |
| 58 | FireTable (brazier) | | |

Each setup function allocates a 0x48-byte object and calls its constructor (`0x4260d0`-`0x427050`), which sets the
`TREE.SPR` frames: `+0x20` first frame, `+0x14/+0x18` width/height in tiles, `+0x38/+0x3c` sprite top-left
(usually `(x, y - height + 1)`, so the object stands on its cell), `+0x2c` shadow frame, `+0x30/+0x34` shadow size,
`+0x24/+0x28` shadow top-left. Pieces are `frame = first + row * 20 + column`. Ordinary trees block their bottom
row (`width` cells from `(x, y)`); the thickets (types 71-75) are centred on the cell and block their whole area.
Type 52 is a straw hut, not a tree.

The full table and a renderer are in opensy `SYWtoORA/tools/map_objects.py`
(`python -m tools.map_objects --map kmulti1` writes `converted/map_objects/catalogue.png` and the map overlay).
`kmulti1` has 199 objects: 191 trees of 12 kinds and 8 Jangseung, all on land, huts beside the crop fields and
totems at road junctions. The decompiles are in `research/ghidra/out/trees*` and `tree_ctors`
(`tools/ghidra/RefsSyw.java`). The brazier and flag animation lengths (8 and 4 frames) are read off the sheet,
not traced in the exe.

The OpenSYW asset installer uses the same table (`OpenRA.Mods.Syw/AssetInstaller/MapObjects.cs`).
