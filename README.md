# OpenSYW

A remake of Seven Years War RTS (1997) on the [OpenRA](https://github.com/OpenRA/OpenRA) engine. It is not a 100%
adaptation of the original mechanics, lots of things have been modernized.

## Original Game Description

Seven Years War is based on the historical conflict between Korea and Japan in the late 16th century. Similar to other
real-time strategy games the workers harvest vegetables which are used to construct buildings. In turn these are used
to build walking, sailing or flying combat units which are used to defeat the enemy.

## Features

- Korea and Japan, each with its own infantry, cavalry, spellcasters, vehicles, ships and aircraft.
- All 18 original skirmish maps, converted from your copy of the game.
- Skirmish against Medium and Hard General bots, with a chosen or random faction.
- Day and night (units see less at night) and rain showers.
- The original art, voices and sound effects.

## Requirements

- The [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0).
- Your own copy of Seven Years War.

The OpenRA engine (`release-20250330`) is downloaded automatically by the first build.

## Installing the game assets

This repository contains no art, sound or maps from the original game. The installer converts the files of your own
copy into the mod.

1. Build the mod once: `make.cmd` (Windows) or `make` (Linux / macOS).
2. Run the installer, pointing it at the original game folder (the one holding `syw.exe`, `FNT1`, `fst`, `Ani`,
   `effect` and `cusmap`):
   - Windows: `install-assets.cmd C:\Games\SYWAR`
   - Linux / macOS: `./install-assets.sh ~/Games/SYWAR`

   Without a folder it looks for `SYWAR` next to this repository.
3. Start the game with `launch-game.cmd` or `launch-game.sh`.

The converted files are written into `mods/syw` (sounds and voices into `mods/syw/audio`) and are ignored by git;
`mods/syw/install-manifest.txt` lists every file the installer wrote. Run the installer again after updating the mod.

Options:
- `--list` shows the stages (sprites, buildings, effects, terrain and maps, chrome, map objects, audio).
- `--only units,chrome` runs some of them.
- `--write-yaml` also updates the generated YAML files kept in the repository: the three terrain tilesets, the crop
  sequences, the map-object rules and sequences, the cursors and the main-menu fire.

The converter lives in `OpenRA.Mods.Syw/AssetInstaller`.

## Development

| Windows               | Linux / macOS            | Purpose
| --------------------- | ------------------------ | ------------- |
| make.cmd              | Makefile                 | Compiles the mod and fetches dependencies (including the OpenRA engine).
| launch-game.cmd       | launch-game.sh           | Launches the game from the repository.
| launch-dedicated.cmd  | launch-dedicated.sh      | Launches a dedicated server.
| utility.cmd           | utility.sh               | Launches the OpenRA Utility for the mod.
| &lt;not available&gt; | packaging/package-all.sh | Generates release installers.

Checks:
- `utility.cmd syw --check-yaml` (or `./utility.sh syw --check-yaml`) lints the rules, maps and chrome.
- `python run_tests.py` builds the mod, runs the YAML check and plays every automated test map (`--only start-test,combat-test`
  picks some, `--no-build --no-check` skips the first two steps). The test maps need the converted game assets; logs
  and a summary go to `test-results/`.

The repository is based on the [OpenRA Mod SDK](https://github.com/OpenRA/OpenRAModSDK); see its
[FAQ](https://github.com/OpenRA/OpenRAModSDK/wiki/FAQ) and the guide to
[updating to a new engine version](https://github.com/OpenRA/OpenRAModSDK/wiki/Updating-to-a-new-SDK-or-Engine-version).

## License

The OpenRA engine and SDK scripts are made available under the [GPLv3](https://github.com/OpenRA/OpenRA/blob/bleed/COPYING)
license, and any executable code developed by a mod and loaded by the engine (i.e. custom mod DLLs, lua scripts) must
be released under a compatible license.
