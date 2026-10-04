# Builds the Windows release of OpenSYW on Windows: a self-contained game folder (players need no .NET install), an
# NSIS setup.exe and a portable zip. It does what the SDK's buildpackage.sh does, without the Linux tools that needs.
#
#   powershell -ExecutionPolicy Bypass -File packaging\windows\package-windows.ps1 [-Tag v0.1] [-OutputDir dist] [-NoInstaller]
#
# Needs the .NET 6 SDK (or newer) and, for the setup.exe, NSIS (winget install NSIS.NSIS). Only files under mods/ that
# git does not ignore are packaged, so converted Seven Years War assets in a working copy can never end up in a
# release; the players convert their own copy on first launch (mods/syw-content).
param(
	[string]$Tag = "dev-$(Get-Date -Format yyyyMMdd)",
	[string]$OutputDir,
	[switch]$NoInstaller
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$platform = 'win-x64'

# mod.config holds KEY="value" lines shared with the SDK's shell scripts.
$config = @{}
foreach ($line in Get-Content (Join-Path $root 'mod.config')) {
	if ($line -match '^\s*([A-Z_]+)="(.*)"\s*$') { $config[$Matches[1]] = $Matches[2] }
}

$modId = $config['MOD_ID']
$engine = Join-Path $root $config['ENGINE_DIRECTORY']
$launcherName = $config['PACKAGING_WINDOWS_LAUNCHER_NAME']
if (-not $OutputDir) { $OutputDir = Join-Path $root 'dist' }
New-Item -ItemType Directory -Force $OutputDir | Out-Null
$OutputDir = (Resolve-Path $OutputDir).Path
$build = Join-Path $PSScriptRoot 'build'

function Step([string]$text) { Write-Host "==> $text" -ForegroundColor Cyan }

function Invoke-Checked([string]$exe, [string[]]$arguments) {
	& $exe @arguments
	if ($LASTEXITCODE -ne 0) { throw "$exe failed with exit code $LASTEXITCODE" }
}

if (-not (Test-Path (Join-Path $engine 'OpenRA.sln'))) {
	throw "Engine not found in $engine. Run make.cmd first."
}

$makensis = $null
if (-not $NoInstaller) {
	$makensis = (Get-Command makensis -ErrorAction SilentlyContinue).Source
	foreach ($candidate in "${env:ProgramFiles(x86)}\NSIS\makensis.exe", "$env:ProgramFiles\NSIS\makensis.exe") {
		if (-not $makensis -and (Test-Path $candidate)) { $makensis = $candidate }
	}

	if (-not $makensis) {
		throw "NSIS not found. Install it (winget install NSIS.NSIS) or pass -NoInstaller to build only the portable zip."
	}
}

Step "Cleaning $build"
if (Test-Path $build) { Remove-Item -Recurse -Force $build }
New-Item -ItemType Directory $build | Out-Null

Step 'Building the engine (self-contained)'
Push-Location $engine
try {
	Invoke-Checked dotnet @('publish', 'OpenRA.sln', '-c', 'Release', '-r', $platform, '--self-contained', 'true', '--nologo',
		"-p:TargetPlatform=$platform", '-p:CopyGenericLauncher=False', "-p:CopyCncDll=$($config['PACKAGING_COPY_CNC_DLL'])",
		"-p:CopyD2kDll=$($config['PACKAGING_COPY_D2K_DLL'])", "-p:PublishDir=$build\")
}
finally { Pop-Location }

Step 'Building the mod'
Invoke-Checked dotnet @('publish', (Join-Path $root 'OpenSYW.sln'), '-c', 'Release', '-r', $platform, '--self-contained', 'true',
	'--nologo', "-p:TargetPlatform=$platform", "-p:PublishDir=$build\")

Step 'Making the icon'
# A .ico with the artwork at each size as 32-bit bitmaps (the C# compiler, which embeds it in the launcher, can't read
# PNG-compressed icon entries).
Add-Type -AssemblyName System.Drawing
$icon = Join-Path $build "$modId.ico"
$entries = foreach ($size in 16, 24, 32, 48, 256) {
	$image = New-Object System.Drawing.Bitmap((Join-Path $root "packaging\artwork\icon_${size}x${size}.png"))
	$data = New-Object IO.MemoryStream
	$entry = New-Object IO.BinaryWriter($data)
	# BITMAPINFOHEADER; the height counts the colour rows and the transparency mask rows.
	$maskStride = [int][Math]::Ceiling($size / 32) * 4
	$entry.Write([uint32]40); $entry.Write([int32]$size); $entry.Write([int32](2 * $size))
	$entry.Write([uint16]1); $entry.Write([uint16]32); $entry.Write([uint32]0)
	$entry.Write([uint32]($size * $size * 4 + $maskStride * $size)); $entry.Write([int32]0); $entry.Write([int32]0)
	$entry.Write([uint32]0); $entry.Write([uint32]0)
	for ($y = $size - 1; $y -ge 0; $y--) {
		for ($x = 0; $x -lt $size; $x++) {
			$c = $image.GetPixel($x, $y)
			$entry.Write([byte]$c.B); $entry.Write([byte]$c.G); $entry.Write([byte]$c.R); $entry.Write([byte]$c.A)
		}
	}
	# All-zero AND mask: the alpha channel above decides transparency.
	$entry.Write((New-Object byte[] ($maskStride * $size)))
	$entry.Flush()
	$image.Dispose()
	[pscustomobject]@{ Size = $size; Bytes = $data.ToArray() }
}

$writer = New-Object IO.BinaryWriter([IO.File]::Create($icon))
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$entries.Count)
$offset = 6 + 16 * $entries.Count
foreach ($e in $entries) {
	$dimension = if ($e.Size -ge 256) { 0 } else { $e.Size }
	$writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
	$writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$e.Bytes.Length); $writer.Write([uint32]$offset)
	$offset += $e.Bytes.Length
}
foreach ($e in $entries) { $writer.Write($e.Bytes) }
$writer.Close()

Step "Building the $launcherName.exe launcher"
Invoke-Checked dotnet @('publish', (Join-Path $engine 'OpenRA.WindowsLauncher\OpenRA.WindowsLauncher.csproj'), '-c', 'Release',
	'-r', $platform, '--self-contained', 'true', '--nologo', "-p:TargetPlatform=$platform", "-p:LauncherName=$launcherName",
	"-p:ModID=$modId", "-p:FaqUrl=$($config['PACKAGING_FAQ_URL'])", "-p:InformationalVersion=$Tag", "-p:LauncherIcon=$icon",
	"-p:PublishDir=$build\")

Step 'Copying engine data'
# The engine's own build downloads this GeoIP database (fetch-geoip.sh); a fresh checkout may not have it yet.
$geoip = Join-Path $engine 'IP2LOCATION-LITE-DB1.IPV6.BIN.ZIP'
if (-not (Test-Path $geoip)) {
	Invoke-WebRequest -UseBasicParsing -OutFile $geoip `
		-Uri 'https://github.com/OpenRA/GeoIP-Database/releases/download/monthly/IP2LOCATION-LITE-DB1.IPV6.BIN.ZIP'
}

foreach ($file in 'AUTHORS', 'COPYING', 'IP2LOCATION-LITE-DB1.IPV6.BIN.ZIP', 'global mix database.dat') {
	Copy-Item -LiteralPath (Join-Path $engine $file) -Destination $build
}
Set-Content -Path (Join-Path $build 'VERSION') -Value $config['ENGINE_VERSION'] -NoNewline -Encoding ascii
Copy-Item -Recurse (Join-Path $engine 'glsl') (Join-Path $build 'glsl')
New-Item -ItemType Directory (Join-Path $build 'mods') | Out-Null
# common for every mod; common-content for the content installer screen (mods/syw-content).
foreach ($mod in 'common', 'common-content') {
	Copy-Item -Recurse (Join-Path $engine "mods\$mod") (Join-Path $build "mods\$mod")
}

Step 'Copying the mod (files git does not ignore, without the test maps)'
Push-Location $root
try {
	# Tracked and new files, never ignored ones (the converted game assets are gitignored).
	$tracked = git ls-files --cached --others --exclude-standard -- mods
	if ($LASTEXITCODE -ne 0) { throw 'git ls-files failed' }
}
finally { Pop-Location }

$copied = 0
foreach ($file in $tracked) {
	# The automated test maps (start-test, combat-test, ...) need the test harness and would only clutter the map list.
	if ($file -match '^mods/[^/]+/maps/[^/]+-test/') { continue }
	$target = Join-Path $build $file
	New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
	Copy-Item -LiteralPath (Join-Path $root $file) -Destination $target
	$copied++
}
Write-Host "    $copied files"

foreach ($manifest in "mods\$modId\mod.yaml", "mods\$modId-content\mod.yaml") {
	$path = Join-Path $build $manifest
	if (Test-Path $path) {
		# WriteAllText: UTF-8 without the byte order mark Windows PowerShell's Set-Content would add.
		[IO.File]::WriteAllText($path, ((Get-Content -Raw $path) -replace '(?m)^(\tVersion:).*$', "`$1 $Tag"))
	}
}

$portable = Join-Path $OutputDir "$($config['PACKAGING_INSTALLER_NAME'])-$Tag-x64-winportable.zip"
Step "Packing $portable"
if (Test-Path $portable) { Remove-Item $portable }
# Entries written one by one: Compress-Archive and ZipFile.CreateFromDirectory both store backslashes in the paths
# under Windows PowerShell, which other unzip tools mishandle.
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::Open($portable, [IO.Compression.ZipArchiveMode]::Create)
try {
	foreach ($file in Get-ChildItem -Recurse -File $build) {
		$name = $file.FullName.Substring($build.Length + 1).Replace('\', '/')
		[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, $name, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
	}
}
finally { $zip.Dispose() }

if ($makensis) {
	$setup = Join-Path $OutputDir "$($config['PACKAGING_INSTALLER_NAME'])-$Tag-x64.exe"
	Step "Building $setup"
	Invoke-Checked $makensis @('-V2', "-DSRCDIR=$build", "-DTAG=$Tag", "-DMOD_ID=$modId",
		"-DPACKAGING_WINDOWS_INSTALL_DIR_NAME=$($config['PACKAGING_WINDOWS_INSTALL_DIR_NAME'])",
		"-DPACKAGING_WINDOWS_LAUNCHER_NAME=$launcherName", "-DPACKAGING_DISPLAY_NAME=$($config['PACKAGING_DISPLAY_NAME'])",
		"-DPACKAGING_WEBSITE_URL=$($config['PACKAGING_WEBSITE_URL'])", "-DPACKAGING_AUTHORS=$($config['PACKAGING_AUTHORS'])",
		"-DPACKAGING_WINDOWS_REGISTRY_KEY=$($config['PACKAGING_WINDOWS_REGISTRY_KEY'])",
		"-DPACKAGING_WINDOWS_LICENSE_FILE=$(Join-Path $root $config['PACKAGING_WINDOWS_LICENSE_FILE'])",
		"-DOUTFILE=$setup", (Join-Path $PSScriptRoot 'buildpackage.nsi'))
}

Remove-Item -Recurse -Force $build
Step "Done. Output in $OutputDir"
