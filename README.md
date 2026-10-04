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

## Playing (Windows)

1. Download the latest `OpenSYW-<version>-x64.exe` installer (or the portable `.zip`) from the
   [Releases](https://github.com/iakraptis/OpenSYW/releases) page and run it. Nothing else is needed: .NET is included.
2. On first launch OpenSYW asks for your own copy of Seven Years War. It finds common folders such as `C:\SYWAR` by
   itself; otherwise click **Browse...** and choose the folder holding `syw.exe`. **Install** converts the art, sounds
   and maps in a few seconds, then the game starts.

The original game files are not included. The converted content is stored in `%APPDATA%\OpenRA\Content\syw`; the main
menu's **Manage Content** button converts it again (for example from another copy).

## Building from source

Requirements:
- The [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0).
- Your own copy of Seven Years War.

The OpenRA engine (`release-20250330`) is downloaded automatically by the first build.

1. Build the mod once: `make.cmd` (Windows) or `make` (Linux / macOS).
2. Start the game with `launch-game.cmd` or `launch-game.sh`. On first launch it asks for the original game folder and
   converts it, as above (the Browse button is Windows-only; on Linux and macOS use `install-assets.sh` below).

The converter can also run from the command line, pointing it at the original game folder (the one holding
`syw.exe`, `FNT1`, `fst`, `Ani`, `effect` and `cusmap`):
- Windows: `install-assets.cmd C:\Games\SYWAR`
- Linux / macOS: `./install-assets.sh ~/Games/SYWAR`

Without a folder it looks for `SYWAR` next to this repository. Unlike the in-game installer it also builds our test
maps (bot-test, ffa-spectate), which `run_tests.py` needs. Run it again after a change to the converter.

Both write into the player content folder (`%APPDATA%\OpenRA\Content\syw1`, or `~/.config/openra/Content/syw/v1`);
`install-manifest.txt` there lists every file written. Options:
- `--list` shows the stages (sprites, buildings, effects, terrain and maps, chrome, map objects, audio, test maps).
- `--only units,chrome` runs some of them.
- `--out FOLDER` writes somewhere else.
- `--write-yaml` also updates the generated YAML files kept in the repository: the three terrain tilesets, the crop
  sequences, the map-object rules and sequences, the cursors and the main-menu fire.

The converter lives in `OpenRA.Mods.Syw/AssetInstaller`, the first-launch screen in `mods/syw-content` and
`OpenRA.Mods.Syw/Widgets/Logic/SywContentLogic.cs`.

## Development

| Windows               | Linux / macOS            | Purpose
| --------------------- | ------------------------ | ------------- |
| make.cmd              | Makefile                 | Compiles the mod and fetches dependencies (including the OpenRA engine).
| launch-game.cmd       | launch-game.sh           | Launches the game from the repository.
| launch-dedicated.cmd  | launch-dedicated.sh      | Launches a dedicated server.
| utility.cmd           | utility.sh               | Launches the OpenRA Utility for the mod.
| packaging/windows/package-windows.ps1 | packaging/package-all.sh | Generates release installers.

Checks:
- `utility.cmd syw --check-yaml` (or `./utility.sh syw --check-yaml`) lints the rules, maps and chrome.
- `python run_tests.py` builds the mod, runs the YAML check and plays every automated test map (`--only start-test,combat-test`
  picks some, `--no-build --no-check` skips the first two steps). The test maps need the converted game assets; logs
  and a summary go to `test-results/`.

Releases (Windows):
- `powershell -ExecutionPolicy Bypass -File packaging\windows\package-windows.ps1 -Tag v0.1` builds the installer and
  a portable zip into `dist/`. It needs [NSIS](https://nsis.sourceforge.io) for the installer (`-NoInstaller` builds
  only the zip). Only files under `mods/` that git does not ignore are packaged, so converted game assets never end up
  in a release.
- Pushing a tag such as `v0.1` makes GitHub Actions (`.github/workflows/release-windows.yml`) build both and publish
  them as a GitHub Release. Running that workflow by hand only uploads them as a build artifact.
- The application icon is drawn by `packaging/artwork/make-icons.ps1`.

The repository is based on the [OpenRA Mod SDK](https://github.com/OpenRA/OpenRAModSDK); see its
[FAQ](https://github.com/OpenRA/OpenRAModSDK/wiki/FAQ) and the guide to
[updating to a new engine version](https://github.com/OpenRA/OpenRAModSDK/wiki/Updating-to-a-new-SDK-or-Engine-version).

## License

The OpenRA engine and SDK scripts are made available under the [GPLv3](https://github.com/OpenRA/OpenRA/blob/bleed/COPYING)
license, and any executable code developed by a mod and loaded by the engine (i.e. custom mod DLLs, lua scripts) must
be released under a compatible license.
