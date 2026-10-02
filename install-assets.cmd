@echo off
rem Converts the art, palettes, music and maps of an original Seven Years War folder into the mod.
rem   install-assets.cmd [GAME_FOLDER] [--only STAGE,...] [--write-yaml] [--list]
rem GAME_FOLDER defaults to a SYWAR folder next to this repository. Run "make" first so the engine is built.
setlocal EnableDelayedExpansion
set ROOT=%~dp0

set GAME=
set FIRST=%~1
if not "!FIRST!" == "" if not "!FIRST:~0,1!" == "-" (
	set GAME="%~f1"
	shift
)

set REST=
:collect
if "%~1" == "" goto run
set REST=!REST! %1
shift
goto collect

:run
cd /d "%ROOT%"
call "%ROOT%utility.cmd" syw --install-assets !GAME! !REST!
