# Crop resources

`rice.png` and `potato.png` are exported from the original `SYWAR/FNT1/Field33.spr`
growth-stage blocks by `SYWtoORA/tools/crop_resources_export.py` (source checksum
in `crops.json`). Each has one 4-frame sequence per 5x5 field position
(young, young, ripe, ripe; frame chosen by density).

- Rice: young block rows 0-4 / cols 1-5, ripe block rows 0-4 / cols 7-11. Worth 50 per bale.
- Potato: sprouts block rows 6-10 / cols 7-11, mature block rows 6-10 / cols 1-5. Worth 25
  per bale; the map's original potato cells regrow (`RegrowsResources`).

`FieldResourceRenderer` (OpenRA.Mods.Syw) picks the position from the bare-field terrain
tile underneath; cells outside a field block use the interior sprite. Values and the
regrowth rate are provisional - no original numbers have been recovered.
