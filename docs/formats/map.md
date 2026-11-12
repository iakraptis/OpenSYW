# MAP format

Reverse-engineered from all 38 `.map` files in `SYWAR/MAP/` and
`SYWAR/cusmap/`. Implemented in
opensy `SYWtoORA/tools/map_extract.py`.

## Layout

```
[0:4]   width  - map width in tiles, little-endian uint32
[4:8]   height - map height in tiles, little-endian uint32
[8:12]  zero in every observed file
[12:16] mode - observed values 0, 1, or 2 (meaning unknown; possibly player
        count or a skirmish/campaign map-type marker)
[16:52] mostly zero; a handful of maps carry small nonzero uint32 values
        here (not yet decoded - possibly player start slots)

[52 : 52 + width*height*4]
        tile grid, row-major, 4 bytes per tile:
          byte 0: tile ID (0-255)
          byte 1: variant (observed range 0-3) - high byte of the tile index
          byte 2: unused in all samples so far (always 0)
          byte 3: unused in all samples so far (always 0)
```

## Tile graphics - SOLVED for the grass set (kmulti1)

A cell draws **global frame `variant * 256 + tile_id`** of the terrain set's
three 300-frame sheets loaded back to back (`syw.exe` references exactly
`field{1,2,3}{1,2,3}.spr`):

| global frame | sheet | content |
|---|---|---|
| 0-299 | `FIELD31.SPR` | grass, water, shores, bridges, walls |
| 300-599 | `FIELD32.SPR` | dirt plains, trails, ridges, stones, wells |
| 600-899 | `Field33.spr` | farm fields, rocks in water |

So variant 1 IDs are FIELD32 frame `id - 44`, which is why they never go below
45 in kmulti1. Confirmed by hand against the real game (cell 119,116 =
`1:236` = FIELD32 frame 192). The earlier "variant picks the sheet" reading
put the right art in only the variant-0 cells.
`Field1x`/`Field2x` are the dirt and snow versions of the same layout. Which set
a map uses is not yet decoded (the header `mode` is 2 for kmulti1).

Field33 contains six 5x5 farm blocks with identical outlines. They are crop
growth stages: young rice, ripe rice, harvested potato holes, mature potatoes,
sprouts, and bare dirt. kmulti1's fields use the ripe-rice and
mature-potato blocks, and these are the map's resources.
`tools/original_map_export.py` converts a map on this basis, and
`tools/map_reference.py` renders labeled comparison images.

`filesize == 52 + width*height*4` **exactly**, verified across all 38 files
with no exceptions (`tests/test_map_extract.py::
test_every_map_file_matches_header_formula`). This is the same kind of
unambiguous, zero-tolerance check that caught the pixel-offset bug in the
SPR format - see `spr.md`.

## Verification

Rendering the tile-ID byte as grayscale (`map_extract.render_preview`)
produces unmistakable real terrain, not noise: organic coastlines, distinct
landmasses/islands, forest/hill patches with clean boundaries, and dithered
water/ground textures. `jstg10.map` in particular shows two separate
landmasses joined by what reads as a narrow land bridge, with a visible
in-tile-data label near one landmass (rendered as light pixel dots forming
"C2", possibly a start-position marker baked into unused/high tile IDs) and
open water at the map's southern edge.

Four tile IDs (`123, 124, 143, 144`) dominate every map by a wide margin
(e.g. over half of `jstg10.map`'s 21,904 tiles) - almost certainly a base
"default ground" terrain rendered as a 2x2 auto-tile block, a common pattern
in tile-based RTS engines of this era.

## Confirmed terrain families

Cross-referenced against `cusmap/kmulti1.map` by having a human who
recognizes the real skirmish map identify specific features (a diagonal
river, its 4 bridges, and grassland in the top corners) by tile coordinate,
then reading the actual `tile_id` values at those coordinates:

| Terrain | Tile IDs | Notes |
|---|---|---|
| Water/river | `123, 124, 143, 144` | Also the dominant tile on most maps (>50% on some) - these are naval-capable skirmish maps, mostly open water |
| Bridge | `166, 168, 170, 171, 186, 190, 191, 207, 208, 209, 229` | All 11 IDs together form each bridge's footprint (deck + approach/railing); confirmed at all 4 bridges on `kmulti1.map`. The same IDs also appear as isolated stray tiles elsewhere on the map - likely unrelated stone/well/wall features reusing nearby tileset graphics, not extra bridges |
| Grass | `54, 55, 74, 75, 82, 83, 102, 103, 114` | Confirmed in both top corners of `kmulti1.map` |

Verification method: render `tile_id` as raw grayscale (no color choice
involved - direct byte value as brightness), then have a human confirm
against known map geometry. This is stronger evidence than any statistical
self-check, since it's ground truth from someone who has seen the real map.
`map_extract.py --semantic` reproduces this coloring
(`render_semantic_preview`, `TERRAIN_COLORS`).

These 24 IDs are confirmed for `kmulti1.map` specifically; not yet checked
whether the same IDs mean the same terrain on every other map (plausible,
since all maps likely share one tileset, but unverified).

## Open questions

- **Passability**: the art is solved (see above), but which tiles block movement
  is inferred from pixel colour: water by its blue fraction, stones by their
  grey fraction. Whether dirt ridges are cliffs has not yet been checked
  against the game. The 160-224 block is plain grass.
- **`mode` field** (offset 12): meaning unknown. Values 0/1/2 observed;
  distribution across the 38 files is roughly 8x `0`, 6x `1`, 24x `2`.
- **Bytes 2-3 of each tile**: always zero in every file checked so far -
  possibly used for unit/resource overlays in files not yet sampled, or
  simply unused padding for 4-byte alignment.
- **Header bytes 16-52**: mostly zero; a few maps (e.g. `jstg5.map`) have a
  small run of nonzero uint32s here. Not yet decoded.
- **Relationship to `.stg`**: `.stg` files are a *fixed* 14312 bytes
  regardless of map size, strongly suggesting scenario/unit-placement data
  (not terrain) that references a `.map` by name. Two `.stg` files diffed
  showed ~700 differing bytes clustered at specific 4-byte-aligned offsets -
  consistent with a placement/entity table. Not yet reverse-engineered.

## Tile attributes and terrain set - SOLVED 2026-10-03

Map conversion needs no hand-made lists: the game's own passability is stored in the terrain sheets.

- **Terrain set:** the header's 4th uint32 (`mode`, offset 12) is read with the header into `0x57fe28`
  (width, height, zero, mode), so it lands on `0x57fe34`, which `LoadMapTile()` (`0x4186d0`) uses to load
  `field1x` (0, dirt), `field2x` (1, snow) or `field3x` (2, grass). 24 maps use 2, 8 use 0 and 6 use 1.
- **Attributes:** every terrain `.spr` has a 300-entry uint32 table right after its 16-byte header (the block
  `spr.md` calls "reserved"; it is zero only in non-terrain sheets). `LoadMapTile` loads the three sheets
  back to back (`0x5583b8`, `0x558fb0`, `0x559ba8`, 0xbf8 = 766 entries apart) and `0x419750` fills the movement
  grid from `table[sheet][frame]` at map load:

| value | meaning | evidence |
|---|---|---|
| 0 | walkable | |
| 256 | walkable (bridges, walkable shore) | 0x419750 stores it as walkable |
| 2 | blocked: cliffs, rock, walls, wells | all 61 hand-confirmed cliff frames; trees write 2 too |
| 4, 512 | water: ships and shipyards accept both | 0x4198f0, 0x419c20 (shipyard needs land + water) |
| 10 | ripe rice, harvestable | 0x419490 |
| 11, 12 | potatoes growing (11 -> 12 -> 13 on the rain event) | 0x42c980 |
| 13 | ripe potatoes, harvestable | 0x419490 |

  The three sets share the same tables (the dirt set has no rocks in water). Crop values cover the 21-cell
  diamond of each 5x5 field block; the young-rice and bare blocks have none.
- **Starts:** a type-1 placement creates the player's HQ (`0x420e70` -> `0x439300` -> base building
  `0x438920`), a 3x3 **centred** on the start cell. OpenRA puts the base actor's top-left on the spawn, so
  converted maps keep the original start and set `BaseActorOffset: -1,-1`.

`tools/auto_map_export.py` converts every map from this alone (`converted/auto_maps/`, `report.md`,
`all_maps.png`). Still open: whether ships may cross value 4 (shore pieces and rocks in water); every check
found treats 4 and 512 alike, the ship pathfinder itself was not traced.
