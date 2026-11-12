# PAL format

Reverse-engineered from the 4 `.pal` files shipped in `SYWAR/`. Implemented in
opensy `SYWtoORA/tools/pal_extract.py` (extraction) and
opensy `SYWtoORA/tools/pal_preview.py` (visual QA).

## Files on disk

```
SYWAR/FNT1/Knight.pal
SYWAR/FNT1/TITLE.PAL
SYWAR/fst/Knight.pal
SYWAR/fst/TITLE.PAL
```

All four are exactly 768 bytes. `FNT1/Knight.pal` and `fst/Knight.pal` are
byte-for-byte identical, as are the two `TITLE.PAL` copies (verified with
`diff`) - so there are really only **two distinct palettes**, `Knight` and
`TITLE`, each duplicated under two directories.

## Layout

A flat array of 256 RGB triples, no header:

```
[0:768] = 256 x (R, G, B), 1 byte per channel
```

Channel values are **6-bit VGA DAC** range, i.e. `0..63` rather than
`0..255` - confirmed by the max byte value across every `.pal` file being
exactly `0x3F` (63), and by the first two palette entries being pure black
(`00 00 00`) followed by pure white (`3F 3F 3F`), the classic index-0/index-1
convention for a VGA palette.

## Tooling

### `tools/pal_extract.py` - extraction

```
python -m tools.pal_extract <source> <outdir> [--recursive]
```

- `find_palette_blocks(data)` scans for candidate 768-byte blocks, rejecting
  flat/constant runs (heuristic, in case a palette is ever embedded inside a
  larger container file rather than being the whole file).
- `palette_from_block(block)` splits the block into 256 `(r, g, b)` tuples
  and rescales 6-bit -> 8-bit (`c * 255 / 63`, rounded) whenever the block's
  max byte is `<= 63`.
- `dump_palette(palette, outbase)` writes both `<name>.json`
  (`{"palette": [[r,g,b], ...]}`) and `<name>.act` (raw 768-byte RGB) to the
  output directory.
- A file can contain more than one detected block; when it does, each extra
  palette gets an `_off<offset>` suffix. **Caveat:** output filenames are
  otherwise just `path.stem`, so running `--recursive` over both `FNT1/` and
  `fst/` extracts into the same `Knight.json`/`TITLE.json` twice, the second
  write silently overwriting the first. Harmless today since the two copies
  are identical, but would silently lose data if they ever diverge.

Current output: `converted/pal/{Knight,TITLE}.{json,act}`.

### `tools/pal_preview.py` - visual QA

```
python -m tools.pal_preview <source> <outdir> [--recursive] [--display]
```

Loads a `.json` or `.act` palette and renders it as a 16x16 color-swatch grid
PNG (or opens it directly with `--display`). Used to confirm palettes decode
to sane colors rather than noise - `converted/pal/preview/Knight.png` shows a
clean gray ramp, primary-color ramps, and skin/earth tones, consistent with a
real VGA game palette (no corruption).

## Consumption

`spr_extract.load_palette()` reads the same `{"palette": [[r,g,b], ...]}`
JSON shape `dump_palette()` writes (and also accepts a raw `.act` directly),
so sprite extraction consumes these palettes as-is - see
[`spr.md`](spr.md). `converted/pal/Knight.json` is the palette used for all
current `.spr` frame renders.

## Tests

`tests/test_pal_extract.py` covers block detection on synthetic data, 6-bit
scaling, and that real extracted `.json`/`.act` pairs round-trip correctly
(3/3 passing).

## Open items

- No known files embed a palette inside a larger container - every `.pal`
  file found so far *is* a 768-byte palette on its own, so the multi-block
  detection path in `find_palette_blocks` is untested against real data.
- The stem-collision overwrite behavior above is unresolved; not urgent
  while `FNT1`/`fst` copies stay identical.
