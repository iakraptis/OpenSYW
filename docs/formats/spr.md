# SPR format

Reverse-engineered from `FNT1/ARCHER.SPR`, `BUDA.SPR`, `BUILD1.SPR`, `FOOTMAN.SPR`,
`TREE.SPR` and validated across all 35 `.spr` files in `SYWAR/FNT1/`.
Implemented in opensy `SYWtoORA/tools/spr_extract.py`.

## Container layout

```
[0:16]   header: 4x little-endian uint32
           [0] group_count - number of animation groups/sequences
           [1] width       - frame width in pixels
           [2] height      - frame height in pixels
           [3] reserved    - size, in uint32 units, of a zero-filled block
                             that immediately follows the header

[16 : 16+reserved*4]
         zero block, all-zero in every observed file. Purpose unknown
         (possibly a runtime cache/duration table written as zeros on disk).

[table_start ...]
         frame offset table: little-endian uint32 byte offsets into the
         pixel-data region, monotonically non-decreasing. The table ends at
         the first entry that decreases or is >= file size.

         Each entry is the START of a frame, so N entries == N frames.
         Frame i spans off[i]..off[i+1]; the LAST frame runs from off[-1]
         to end of file.

[table_end : 3060]
         additional per-group/per-frame metadata (not yet decoded, not
         needed for extraction).

[3060 : EOF]
         pixel-data region, one RLE stream per frame.
```

### The pixel-data offset is a fixed 3060

`pixel_base` is **not** derived from the file size - it is a fixed constant
`3060` for every one of the 35 shipped `.spr` files, regardless of frame count
or frame size.

An earlier version computed `pixel_base = file_size - offsets[-1]`, which
lands 64-80 bytes too late (exactly one trailing empty frame). That
misalignment made every frame start mid-stream, which decoded into sprites
that appeared **split across the buffer edges** and had **black bars punched
through them** (e.g. ARCHER frame 83's hat detached from the head).

The correct constant is verified by an objective, unambiguous metric: with
`PIXEL_DATA_OFFSET = 3060`, **all ~5,000 frames across all 35 files decode to
exactly `width * height` pixels and consume their byte range exactly** - no
short frames, no overruns, no leftover bytes. `tests/test_spr_extract.py::
test_every_frame_decodes_exactly` enforces this and fails loudly if the
offset regresses.

Example header (ARCHER/BUDA): `group_count=9, width=40, height=40,
reserved=300`. BUILD1/2/3: `group_count=9, width=32, height=32,
reserved=300`.

## Per-frame RLE decoding

Each frame's raw byte stream decodes to `width * height` palette-index
bytes:

- byte `0x01` followed by a count byte `N` -> emit `N` transparent pixels
  (written as index 0, but see below)
- any other byte -> emit one literal palette-index pixel, **including a
  literal `0x00`**

Decoding stops once `width * height` pixels have been produced. With the
correct pixel-data offset the stream always lands exactly on the last pixel,
so the short-stream padding path never triggers on the shipped files.

**A literal `0x00` byte is not transparency - it's opaque black.** Index 0
is `(0, 0, 0)` in `Knight.pal`, and is used as a real drawn color for
outlines, tile-grout lines, and seams (visible on e.g. the Beacon Mound's
roof-tile grid and stone-block seams). Only pixels produced by the
`0x01 + count` skip-run mechanism are actually transparent. An earlier
version of `spr_extract.py` conflated "decoded byte value is 0" with
"transparent" for both extraction modes (RGBA alpha and indexed-PNG tRNS),
turning every literal-black pixel into a transparent hole - reported by the
user as "seams are transparent" on a stitched building. Fixed by tracking
*provenance* per pixel during decode (`_decode_frame_impl` returns
`(pixels, alpha)`, alpha only 0 for skip-run pixels) rather than inferring
transparency from the decoded value. For indexed output this also requires
remapping any *opaque* index-0 pixel to a different index, since a PNG's
`tRNS` transparency is per-index, not per-pixel, and index 0 must stay
reserved for true transparency.

That different index **must be a real second `(0, 0, 0)` entry already in
the palette** (`Knight.pal` has one: index 31, the bottom of the neutral
gray shading ramp) - not an arbitrary unused index whose color is then
overridden only inside that one PNG's own embedded palette table. The first
attempt at this fix did the latter and rendered the seams **white** instead
of black: OpenRA (like, presumably, the original game) renders indexed
sprites by looking up colors in its own copy of the palette
(`PaletteFromFile`, loaded once from `palettes/Knight.pal`), and never
looks at a sprite's individually-embedded palette - so the override was
invisible to it, and the chosen index rendered as whatever color that
index actually has in the real palette. `extract_spr.py` now searches the
given palette for an existing duplicate-black index and raises rather than
inventing one if none exists. The chosen index is fixed once per `.spr`
file (not per frame), so every frame's embedded palette stays
byte-identical - required for `spr_stitch_grid.py`, which combines tiles
from different frames of the same file and asserts their palettes match.

## No post-processing needed

Frames decode directly into correct, correctly-positioned images. **No
shifting, rolling, centering or other correction is applied.**

This is worth stating explicitly because earlier iterations chased a
phantom: sprites appeared wrapped around the buffer edges, and a per-frame
"un-wrap" roll (circular centroid centering) was added to compensate. That
apparent torus addressing was **entirely an artefact of the wrong
`pixel_base`**. Once the pixel-data offset was corrected the wrapping
vanished, and the centering code was removed.

The lesson: the symptom (split sprites) was several layers removed from the
cause (a 64-80 byte offset error), and the objective decode-exactness metric
found the cause where visual tuning could not.

## Frame sizes observed

| Size    | Files |
|---------|-------|
| 32x32   | BUILD1-3/5, DEFAULT1/2, EXP, Field*, FIRE, MOUSE, RAIN, TREE |
| 40x40   | ARCHER, BUDA, FARMER, FOOTMAN |
| 48x48   | TANK |
| 52x52   | BULL, GENERAL |
| 64x48   | PORTRAIT |
| 72x72   | Fly1-3, SHIP1-3 |
| 96x96   | FIRE96 |

## Open questions

- The "additional metadata" region between the offset table and the
  pixel-data region is not decoded. It may encode per-frame/per-group
  animation timing or grouping, but is not required for static frame
  extraction.
- `group_count` (animation group/sequence count) is read but not yet used
  to split frames into named animations. Confirmed **not** related to
  multi-tile building layouts (see below) despite both being 9 for
  `BUILD1/2/3.SPR` - `ARCHER.SPR` (a plain non-tiled infantry sprite) also
  has `group_count=9`, so the match is coincidental.

## Multi-tile building sprites

Buildings bigger than one cell (e.g. `BUILD1-3/5.SPR`, `BUDA.SPR`) are
stored as a 3x3 grid of normal-sized (32x32) tiles, spread across the
frame index space rather than stored contiguously. Reverse-engineered from
Korea's Headquarters and Mill in `BUILD2.SPR` (user hand-identified the
finished Headquarters' 9 frames; the full pattern below was confirmed by
stitching and visually inspecting all 6 combinations):

```
frame_index = base + row * 20 + col      (row, col in 0..2)
```

tiles pasted at `(col*32, row*32)` (row 0 = back/top of the isometric
building). The first 60 frames of `BUILD2.SPR` hold 2 buildings x 3
construction states, packed as one 20-frame "row band" per tile-row
(3 tile-rows x 20 = 60), each band laid out as 6 groups of 3 + 2 padding
frames:

| band offset | content |
|---|---|
| 0-2   | Headquarters constructing |
| 3-5   | Headquarters finished |
| 6-8   | Headquarters damaged |
| 9-11  | Mill constructing |
| 12-14 | Mill finished |
| 15-17 | Mill damaged |
| 18-19 | unused padding |

So each building+state's 3x3 `base` is just its band offset (0, 3, 6, 9,
12, 15), with the grid's `row_stride` fixed at 20. Stitching tool:
`tools/spr_stitch_grid.py`.

**Second block confirmed** (user-identified): frames 80-137 hold Barracks
and "Beacon Mound" (a stone watchtower/kiln), same 6-groups-of-3-plus-2-pad
layout, just shifted - row-bands at 80/100/120 instead of 0/20/40:

| band offset (+80/+100/+120) | content |
|---|---|
| 0-2   | Barracks constructing |
| 3-5   | Barracks finished |
| 6-8   | Barracks damaged |
| 9-11  | Beacon Mound constructing |
| 12-14 | Beacon Mound finished |
| 15-17 | Beacon Mound damaged |
| 18-19 | unused padding |

Note the **gap**: block 1's last used row-band starts at 40 (ends at 59),
block 2's first row-band starts at 80, not 60 - frames 60-79 are unaccounted
for (not yet checked whether they hold a 3rd building's data in a different
shape, or are genuinely unused). So blocks are NOT tightly packed every 60
frames; each confirmed block so far is 2 buildings x 3 states, but the gap
between block starts (0 -> 80) doesn't yet have an explained size. Not yet
confirmed whether this whole pattern continues further into `BUILD2.SPR`
(300 frames total) or applies to `BUILD1/3/5.SPR`.

**Third block confirmed** (user-identified): the Heavy Arms Workshop's row
band starts at 180 - `base=180` constructing (frames 180-182/200-202/
220-222), `base=183` finished (183-185/203-205/223-225), `base=186` damaged
(186-188/206-208/226-228) - same 3-states-of-3-columns layout as blocks 1-2,
just this time only the first half (offsets 0-8) of the 20-wide band was
identified; offsets 9-17 (frames 189-191/209-211/229-231 etc.) would hold a
second building's states if this band follows the same paired layout as
blocks 1-2, but that has not been checked. There is now also an unexplained
gap between block 2 (ends at 137) and block 3 (starts at 180): frames
138-179 are unaccounted for, same open status as the 60-79 gap above.

Team color: the finished Headquarters' roof and the Peasant's clothes share
reserved palette indices for the in-game "house color" - confirms it's a
universal player-color band, not infantry-specific. **Full band is 176-191**
(16 shades, exactly matching the stock C&C palette's own reserved range) -
an initial pass only found 181-191 because the smaller Peasant/Headquarters
art didn't happen to use the brighter end of the ramp; the wider Barracks/
Beacon Mound (Radar) art does. There is a second, separate, similar-looking
ramp at indices 160-175 (desaturated blue-gray, e.g. Beacon Mound's stone
tower shading) which is **not** team color and must not be included in the
remap - easy to mistake for the same band since both are "blue-ish" by a
naive R/G/B-channel heuristic. See `pal.md` / the mod's
`rules/palettes.yaml`.
