# ANM format

Reverse-engineered from the single `.anm` file in the game data,
`SYWAR/Ani/ani.anm` - a 56-frame, 640x400 full-screen fire animation.
Implemented in opensy `SYWtoORA/tools/anm_extract.py`.

Note: `SYWAR/Ani/` also contains `LOGO2.AVI`, a real 320x240 AVI (a
company/publisher logo intro). That file is unrelated to `.anm` - different
resolution, different content (a logo bumper vs. a looping flame effect) -
and needs no reverse engineering since it's a standard AVI.

## Layout

```
[0:16]              header: 4x little-endian uint32
                       [0] frame_count       - 56 in the observed file
                       [1] width             - frame width in pixels (640)
                       [2] height            - frame height in pixels (400)
                       [3] total_data_size   - total bytes of pixel data
                                                following the frame table

[16:16+N*8]          frame table: N = frame_count entries of
                       (offset: uint32, size: uint32), relative to the
                       start of the pixel-data region (i.e. relative to
                       byte 16+N*8). offset[i] == sum(size[0..i-1]) always -
                       the table is redundant (size alone would suffice)
                       but both fields are present, same as SPR/STG.

[16+N*8 : EOF]       pixel data: one PCX-style RLE stream per frame, back
                     to back (same scheme as `.fst`, see `fst.md`):
                       byte >= 0xC0: repeat count = byte & 0x3F; the
                                     FOLLOWING byte is the palette-index
                                     value to repeat that many times
                       byte <  0xC0: a single literal palette-index pixel
                     Each frame decodes to exactly width*height bytes.
```

`anm_extract.py` reuses `fst_extract.decode_frame` directly rather than
re-implementing the RLE scheme, since it's byte-for-byte identical.

## Verification

**Objective (strongest signal in this project so far)**: the frame table
gives each frame's exact compressed byte size up front (unlike `.fst`,
which has no such table and only gets a near-exact match with a small
trailer). Decoding every one of the 56 frames in `ani.anm` consumes
*exactly* `size` bytes and produces *exactly* `width*height` pixels - zero
leftover, on every single frame. This confirms the RLE scheme and the
frame table's offset/size bookkeeping simultaneously.
`tests/test_anm_extract.py::test_the_real_anm_file_decodes_with_zero_leftover_on_every_frame`
enforces this.

**Visual**: all 56 decoded frames form a coherent, correctly-animating
loop of rising/flickering flames (confirmed by inspecting several frames
across the sequence and an assembled GIF preview, `--gif` CLI flag).

## Palette

Like `.fst`, no palette is embedded in the file, and only two palettes
exist anywhere in the game data (`Knight.pal`, `TITLE.PAL`). `TITLE.pal`
gives a plausible dusky-pink sky behind the flames; `Knight.pal` renders
the same background as flat white. Neither has been confirmed against an
in-game screenshot, so which one (if either) is "correct" for this
specific asset is an open question - but `TITLE.pal` is the working
default since it's the palette built for full-screen dramatic art rather
than in-game UI chrome.

## Open questions

- **Where/how this animation is used in-game** is unconfirmed (a menu
  background loop, a defeat/burning-building scene, a torch effect,
  etc.) - no exe string reference to `ani.anm` or `Ani\` has been found
  yet (unlike most `.fst` screens, which are confirmed via exe strings).
- **Correct palette** is unconfirmed (see above).
- Only one `.anm` file exists in the extracted game data, so the format
  is confirmed against a single example; multi-file corroboration (like
  the "all 18 files" or "all 38 files" checks used for FST/MAP/STG) isn't
  possible here.
