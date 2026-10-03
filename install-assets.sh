#!/bin/sh
# Converts the art, palettes, music, sounds and maps of an original Seven Years War folder into the mod.
#   ./install-assets.sh [GAME_FOLDER] [--only STAGE,...] [--write-yaml] [--list]
# GAME_FOLDER defaults to a SYWAR folder next to this repository. Run `make` first so the engine is built.
set -e

if [ $# -gt 0 ] && [ "${1#-}" = "$1" ]; then
	GAME=$(cd "$1" && pwd)
	shift
	cd "$(dirname "$0")"
	exec ./utility.sh --install-assets "${GAME}" "$@"
fi

cd "$(dirname "$0")"
exec ./utility.sh --install-assets "$@"
