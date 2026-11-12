# EXP.SPR: explosions

`SYWAR/FNT1/EXP.SPR` holds 300 frames of 32x32 tiles, 234 of them used. Bigger explosions are split into tiles exactly like the building sprites: **20 tiles per row**, so a 2x2 frame is tiles `n, n+1, n+20, n+21` and a 3x3 frame spans three rows. Each animation fills a band of two (or three) tile rows from left to right, and long animations continue in the next band.

Pieced together on 2026-09-29 from the raw tile grid:

| Group | Frame size | Tiles | Frames | Looks like | Used in the mod |
|---|---|---|---:|---|---|
| **A: fire blast** | 2x2 (64 px) | rows 0/20 cols 0-19, then rows 40/60 cols 0-9 | 15 | White flash growing into a fireball, then dark smoke that breaks up | Yes: Bomber bombs and the Torpedo blast (`cannon-explosion`) |
| **B: flash and smoke** | 2x2 (64 px) | rows 80/100 cols 0-19, then rows 120/140 cols 0-11 | 16 | Small spark, big round flash with a shock ring, collapsing into grey smoke, then faint wisps | Yes: vehicle destruction, all 16 frames (`vehicle-explosion`) |
| **C: dust burst** | 2x2 (64 px) | rows 160/180 cols 0-19, then rows 200/220 cols 0-5 | 13 | Pale puff turning into an orange-brown dust cloud that thins out | Yes: building destruction (`building-explosion`) |
| **D1: dust burst, medium** | 1x1 (32 px) | row 200 cols 6-18 | 13 | Group C at about half size, frame for frame | Yes: cannon shell impacts (`cannon-impact`) |
| **D2: dust burst, small** | 1x1 (32 px) | row 220 cols 6-18 | 13 | Group C at about a third of the size | Yes: rocket impacts (`rocket-impact`) |
| **E: big flash** | 3x3 (96 px) | rows 240/260/280 cols 0-11 | 4 | Group B's flash frames 2-5 (the big round flash and its collapse) at 1.5x size | No |

Notes:
- **C, D1 and D2 are one animation in three sizes** (13 frames each), so the game likely picks the size by what was hit or destroyed.
- **E matches B's frames 2-5 at a larger size.** It's probably the start of the destruction of 3x3 buildings, with B's smaller flash for 2x2 towers. Whether E is followed by B's smoke frames in the original is not confirmed.
- Tiles inside a frame can be empty (for example 125 and 127 in B's wisps): the frame is still 2x2.
- All groups use the Knight palette; the flashes have a translucent-looking ring drawn with solid pale colours.

Wiring (user choices, 2026-09-29): B for vehicle, ship and aircraft destruction, C for building destruction, D1 for cannon shell impacts, D2 for rocket impacts. A stays on the Bomber's bombs and the Torpedo blast; E is unused. `explosion_export.py` exports them (`EXPLOSIONS`); the sequences are in the `explosion` image.

## Review images

All images are at 2x size. Each animation has a looping GIF (about 80 ms per frame) and a strip of every frame, labelled with its frame number and first tile. Where it should be used in the mod is still to be decided.

### Raw tile grid

The file as stored: 20 tiles per row, tile numbers on the left (row start) and top (column).

*(EXP.SPR raw tile grid: local preview `research/previews/exp/raw-grid.png`)*

### A: fire blast (15 frames, 2x2)

Used for the Bomber's bombs and the Torpedo blast.

*(A: fire blast, animated: local preview `research/previews/exp/a-fire-blast.gif`)*

*(A: fire blast, frames: local preview `research/previews/exp/a-fire-blast.png`)*

### B: flash and smoke (16 frames, 2x2)

Used for vehicle, ship and aircraft destruction (all 16 frames).

*(B: flash and smoke, animated: local preview `research/previews/exp/b-flash-smoke.gif`)*

*(B: flash and smoke, frames: local preview `research/previews/exp/b-flash-smoke.png`)*

### C: dust burst (13 frames, 2x2)

Used for building destruction.

*(C: dust burst, animated: local preview `research/previews/exp/c-dust-burst.gif`)*

*(C: dust burst, frames: local preview `research/previews/exp/c-dust-burst.png`)*

### D1: dust burst, medium (13 frames, 1x1)

Used for cannon shell impacts (Cannon, Cannon Tower, Cannon Ship).

*(D1: dust burst medium, animated: local preview `research/previews/exp/d1-dust-burst-medium.gif`)*

*(D1: dust burst medium, frames: local preview `research/previews/exp/d1-dust-burst-medium.png`)*

### D2: dust burst, small (13 frames, 1x1)

Used for rocket impacts (Fire Car, Fighter).

*(D2: dust burst small, animated: local preview `research/previews/exp/d2-dust-burst-small.gif`)*

*(D2: dust burst small, frames: local preview `research/previews/exp/d2-dust-burst-small.png`)*

### E: big flash (4 frames, 3x3)

Not used.

*(E: big flash, animated: local preview `research/previews/exp/e-big-flash.gif`)*

*(E: big flash, frames: local preview `research/previews/exp/e-big-flash.png`)*
