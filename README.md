# OpenSY

A remake of Seven Years War RTS (1997) on the [OpenRA](https://github.com/OpenRA/OpenRA) engine.

## Installing the game assets

This repository contains no art, music or maps from the original game. You need your own copy of Seven Years War;
the installer converts its files into the mod.

1. Build the mod once: `make.cmd` (Windows) or `make` (Linux / macOS).
2. Run the installer, pointing it at the original game folder (the one holding `syw.exe`, `FNT1`, `fst`, `cusmap`
   and `Ani`):
   - Windows: `install-assets.cmd C:\Games\SYWAR`
   - Linux / macOS: `./install-assets.sh ~/Games/SYWAR`

   Without a folder it looks for `SYWAR` next to this repository.
3. Start the game with `launch-game.cmd` or `launch-game.sh`.

The converted files are written into `mods/syw` and are ignored by git; sounds and voices go to `mods/syw/audio`. Run the installer again after updating the
mod. Options: `--list` shows the stages, `--only units,chrome` runs some of them, and `--write-yaml` updates the few
generated YAML files kept in the repository (the FIELD3 tileset, crop sequences, cursors and menu fire regions). The
converter is `OpenRA.Mods.Syw/AssetInstaller`; `mods/syw/install-manifest.txt` lists every file it wrote.

## Original Game Description
Seven Years War is based on the historical conflict between Korea and Japan in the late 16th century. Similar to other real-time strategy games the workers harvest vegetables which are used to construct buildings. In turn these are used to build walking, sailing or flying combat units which are used to defeat the enemy.

## Development environment

This repository contains a bare development environment for creating a new mod/game on the [OpenRA](https://github.com/OpenRA/OpenRA) engine.

These scripts and support files wrap and automatically manage a copy of the OpenRA game engine and common files during development, and generates Windows installers, macOS .app bundles, and Linux [AppImages](https://appimage.org/) for distribution.

The key scripts in this SDK are:

| Windows               | Linux / macOS            | Purpose
| --------------------- | ------------------------ | ------------- |
| make.cmd              | Makefile                 | Compiles your project and fetches dependencies (including the OpenRA engine).
| launch-game.cmd       | launch-game.sh           | Launches your project from the SDK directory.
| launch-server.cmd     | launch-server.sh         | Launches a dedicated server for your project from the SDK directory.
| utility.cmd           | utility.sh         | Launches the OpenRA Utility for your project.
| &lt;not available&gt; | packaging/package-all.sh | Generates release installers for your project.

To launch your project from the development environment you must first compile the project by running `make.cmd` (Windows), or opening a terminal in the SDK directory and running `make` (Linux / macOS).  You can then run `launch-game.cmd` (Windows) or `launch-game.sh` (Linux / macOS) to run your game.

The `example` mod included in this repository provides the bare minimum structure to launch to the in-game main menu for the sole purpose of demonstrating the SDK.  See [Getting Started](https://github.com/OpenRA/OpenRAModTemplate/wiki/Getting-Started) on the Wiki for instructions on how to adapt this template for your own projects.  For common questions, please see the [FAQ](https://github.com/OpenRA/OpenRAModSDK/wiki/FAQ).  See [Updating to a new SDK or Engine version](https://github.com/OpenRA/OpenRAModSDK/wiki/Updating-to-a-new-SDK-or-Engine-version) for a guide on updating your mod a newer OpenRA release.

The OpenRA engine and SDK scripts are made available under the [GPLv3](https://github.com/OpenRA/OpenRA/blob/bleed/COPYING) license, and any executable code developed by a mod and loaded by the engine (i.e. custom mod DLLs, lua scripts) must be released under a compatible license. 
