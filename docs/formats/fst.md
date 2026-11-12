# FST format

Reverse-engineered from all 18 `.fst` files in `SYWAR/fst/`. Implemented in
opensy `SYWtoORA/tools/fst_extract.py`.

Each `.fst` is a single full-screen UI image (options, win/lose, title,
save, staff/credits, and 7 build-menu side panels) - confirmed by
`syw.exe` string references (`fst\option1.fst`, `fst\win1.fst`,
`fst\pannel11.fst`, etc., see `exe.md`).

## Layout

```
[0:16]   header: 4x little-endian uint32
           [0] const     - always 1 in every observed file
           [1] width     - image width in pixels
           [2] height    - image height in pixels
           [3] reserved  - always 13 in every observed file (meaning unknown)

[16:48]  metadata (32 bytes) - not decoded. Contains hash/pointer-like
         values and a small table with a per-file size-class value;
         doesn't affect decoding, since pixel data always starts at a
         fixed offset regardless of this region's content.

[48:EOF] pixel data: PCX-style RLE, one continuous stream (no per-frame
         table - each file is a single image):
           byte >= 0xC0: repeat count = byte & 0x3F; the FOLLOWING byte is
                         the palette-index value to repeat that many times
           byte <  0xC0: a single literal palette-index pixel
         Decoding stops once width*height pixels have been produced.
```

Common dimensions observed: `640x480` (title/briefing/fullscreen dialogs),
`400x300` and `520x400` (smaller dialogs), `160x480/600/768` (build-menu
side panels).

## Verification

**Objective**: for every one of the 18 files, the RLE decoder produces at
least `width*height` pixels and consumes essentially all of the available
post-header bytes - leftover in every case is a handful of trailer bytes
(under 20), never a large unconsumed remainder. This is the same kind of
unambiguous, near-zero-tolerance signal that confirmed the SPR pixel
offset and the MAP header formula.
`tests/test_fst_extract.py::test_every_fst_file_decodes_nearly_exactly`
enforces this across all 18 files.

**Visual**: every rendered screen is immediately recognizable and correct:
`Option1.fst` is a complete options screen (speed/volume sliders, sword/CD
icons); `WIN1.FST`/`LOSE.FST` are victory/defeat screens; `Title.fst` is a
polished title screen ("Seven Years War" logotype, a rendered ship, the HQ
Team logo) once given its own palette; `SAVE.FST` is a 7-slot save-game
screen; `STAFF.FST` is a credits screen; the `PannelNN.fst` files are
build-menu side panels.

## Palette

Unlike `.spr`, there is no per-file palette reference found inside the
`.fst` data itself - the caller must supply the matching palette (see
`pal.md`). Most UI screens render correctly with the general `Knight.pal`;
`Title.fst` specifically needs its own `fst/TITLE.PAL` - using the wrong
palette produces a structurally-correct but visually garbled (wrong-color)
image, which is how the palette mismatch was first caught. Which other
`.fst` files (if any) need a non-Knight palette is not yet checked
individually.

## Open questions

- **Metadata bytes 16-47** are not decoded. Likely includes some kind of
  identifier/checksum and a size-class value, but doesn't block extraction.
- **Per-screen palette assignment**: only `Title.fst`'s palette need has
  been confirmed; whether other screens (e.g. `SAVE.FST`, `STAFF.FST`) use
  a non-Knight palette natively hasn't been individually verified - the
  renders look plausible with Knight's palette but haven't been
  cross-checked against a screenshot or other ground truth the way
  `Title.fst` was.
