# Updates a portable OpenSYW folder outside the repository (by default ..\OpenSYW-portable, next to the checkout)
# from the current working copy: builds the game with package-windows.ps1 (no installer), mirrors it into the folder
# and refreshes the converted game content bundled in its Support\Content. The folder's own Support data (settings,
# replays, saves, logs) is kept. With -Zip it installs an existing portable zip instead of building, e.g. one
# downloaded from a GitHub release.
#
#   powershell -ExecutionPolicy Bypass -File packaging\windows\update-portable.ps1 [-Destination PATH]
#       [-Content PATH] [-Tag NAME] [-Zip OpenSYW-...-winportable.zip] [-SkipContent]
#
# The converted content comes from this PC's OpenRA content folder (installed by the game's first launch or by
# install-assets.cmd). It never enters the repository: the portable folder is outside it and git ignores the build
# folder. A portable folder is one with a Support folder beside OpenSYW.exe, which the engine then uses instead of
# %APPDATA%\OpenRA.
param(
	[string]$Destination,
	[string]$Content = (Join-Path $env:APPDATA 'OpenRA\Content\syw\v1'),
	[string]$Tag,
	[string]$Zip,
	[switch]$SkipContent
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if (-not $Destination) { $Destination = Join-Path (Split-Path $root) 'OpenSYW-portable' }

function Step([string]$text) { Write-Host "==> $text" -ForegroundColor Cyan }

# Robocopy: exit codes below 8 mean success (0 nothing to do, 1 files copied, 2-7 extra or mismatched files handled).
function Invoke-Mirror([string]$from, [string]$to, [string[]]$extra) {
	& robocopy $from $to /MIR /NFL /NDL /NJH /NP /R:2 /W:1 @extra | Out-Host
	if ($LASTEXITCODE -ge 8) { throw "robocopy $from -> $to failed with exit code $LASTEXITCODE" }
}

$Destination = [IO.Path]::GetFullPath($Destination)
if ($Destination.StartsWith($root.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
	throw "The portable folder must be outside the repository ($root), so its game content can never be committed."
}

# Files of a running game can't be replaced.
$running = Get-Process -Name OpenSYW -ErrorAction SilentlyContinue |
	Where-Object { $_.Path -and $_.Path.StartsWith($Destination, [StringComparison]::OrdinalIgnoreCase) }
if ($running) { throw "OpenSYW is running from $Destination. Close it first." }

if (-not $Tag) {
	Push-Location $root
	try {
		$commit = git rev-parse --short HEAD
		$dirty = if (git status --porcelain) { '-modified' } else { '' }
	}
	finally { Pop-Location }
	$Tag = "portable-$commit$dirty"
}

$work = Join-Path ([IO.Path]::GetTempPath()) "opensyw-portable-$PID"
try {
	if (-not $Zip) {
		Step "Building the game ($Tag)"
		& (Join-Path $PSScriptRoot 'package-windows.ps1') -Tag $Tag -NoInstaller -OutputDir $work
		$Zip = (Get-ChildItem (Join-Path $work '*-winportable.zip') | Select-Object -First 1).FullName
	}

	Step "Unpacking $Zip"
	$game = Join-Path $work 'game'
	Add-Type -AssemblyName System.IO.Compression.FileSystem
	[IO.Compression.ZipFile]::ExtractToDirectory((Resolve-Path $Zip).Path, $game)
	if (-not (Test-Path (Join-Path $game 'OpenSYW.exe'))) { throw "$Zip is not an OpenSYW portable zip (no OpenSYW.exe)." }

	Step "Updating the game in $Destination"
	# Everything but Support mirrors the new game (files dropped from the game are removed here too); Support is the
	# player's data and the bundled content, never touched by this step.
	Invoke-Mirror $game $Destination @('/XD', (Join-Path $Destination 'Support'))
	New-Item -ItemType Directory -Force (Join-Path $Destination 'Support') | Out-Null
}
finally {
	if (Test-Path $work) { Remove-Item -Recurse -Force $work }
}

if (-not $SkipContent) {
	$target = Join-Path $Destination 'Support\Content\syw\v1'
	if (Test-Path (Join-Path $Content 'install-manifest.txt')) {
		Step "Updating the bundled game content from $Content"
		# Without the developer test maps install-assets.cmd adds (AssetInstaller/Maps.cs); players don't get them.
		$devMaps = 'bot-test', 'ffa-spectate'
		Invoke-Mirror $Content $target (@('/XD') + ($devMaps | ForEach-Object { Join-Path $Content "maps\$_" }))
		foreach ($map in $devMaps) {
			$path = Join-Path $target "maps\$map"
			if (Test-Path $path) { Remove-Item -Recurse -Force $path }
		}
	}
	elseif (Test-Path (Join-Path $target 'install-manifest.txt')) {
		Write-Warning "No converted content in $Content; keeping the content already in $target."
	}
	else {
		Write-Warning ("No converted content in $Content. The portable game will ask for the original Seven Years " +
			"War files on first launch; to bundle them, run install-assets.cmd (or the game once) and update again.")
	}
}

Step "Done: $Destination ($Tag)"
