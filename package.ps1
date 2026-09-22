<#
    GHShield - build and package for Rhino Package Manager / Food4Rhino.

    Usage, from the folder containing GHShield.sln / GHShield.slnx:

        powershell -ExecutionPolicy Bypass -File package.ps1

    Produces:  package\ghshield-<version>-rh7_0-win.yak

    It refuses to run while Rhino is open, because Rhino holds a lock on
    GHShield.gha and the build would silently package a stale file.
#>

param(
    [string] $Configuration = "Release",
    [string] $Yak = "C:\Program Files\Rhino 7\System\Yak.exe"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition

function Fail($msg) { Write-Host "`n  FAILED: $msg`n" -ForegroundColor Red; exit 1 }
function Step($msg) { Write-Host "`n=== $msg" -ForegroundColor Cyan }
function Ok($msg)   { Write-Host "    ok   $msg" -ForegroundColor Green }

# ---------------------------------------------------------------- guards
Step "Checking prerequisites"

if (Get-Process -Name "Rhino" -ErrorAction SilentlyContinue) {
    Fail "Rhino is running. It locks GHShield.gha, so the build would package a stale file. Close Rhino and run again."
}
Ok "Rhino is closed"

if (-not (Test-Path $Yak)) {
    Fail "Yak.exe not found at $Yak. Pass -Yak <path> if Rhino is installed elsewhere."
}
Ok "Yak found"

# ---------------------------------------------------------------- build
Step "Building ($Configuration)"

$proj = Join-Path $root "GHShield\GHShield.csproj"
if (-not (Test-Path $proj)) { Fail "GHShield.csproj not found at $proj" }

& dotnet build $proj -c $Configuration
if ($LASTEXITCODE -ne 0) { Fail "Build failed. Fix the errors and run again." }
Ok "Build succeeded"

# ---------------------------------------------------------------- collect
Step "Assembling the package"

$bin = Join-Path $root "GHShield\bin\$Configuration"
$stage = Join-Path $root "package"

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage | Out-Null

# 0Harmony.dll is NOT optional. Without it the plugin loads but the delete
# guard cannot install, so deletions get reverted instead of refused.
$required = @(
    @{ From = Join-Path $bin "GHShield.gha";                     Name = "GHShield.gha" },
    @{ From = Join-Path $root "GHShield\Resources\ShieldIcon48.png"; Name = "ShieldIcon48.png" },
    @{ From = Join-Path $root "manifest.yml";                    Name = "manifest.yml" },
    @{ From = Join-Path $root "README.md";                       Name = "README.md" },
    @{ From = Join-Path $root "LICENSE";                         Name = "LICENSE" }
)

foreach ($f in $required) {
    if (-not (Test-Path $f.From)) { Fail "Missing required file: $($f.From)" }
    Copy-Item $f.From (Join-Path $stage $f.Name) -Force
    Ok $f.Name
}

# Harmony: one build per .NET runtime, in harmony\<runtime>\ - NOT beside the
# .gha. Rhino 7 runs .NET Framework (net48); Rhino 8 normally runs modern .NET
# (net8.0, or net6.0 on older .NET 7 builds). The net48 build on Rhino 8 fails with "Method not
# found: ILGenerator.MarkSequencePoint" and no patch installs. Startup.cs picks
# the right folder at load time.
$harmonyVersion = "2.4.2"
$harmonyLib = Join-Path $env:USERPROFILE ".nuget\packages\lib.harmony\$harmonyVersion\lib"
if (-not (Test-Path $harmonyLib)) {
    Fail "Harmony $harmonyVersion not found at $harmonyLib. Build once in Visual Studio so NuGet downloads it."
}
# net48 (Rhino 7) and net8.0 (current Rhino 8) are required. net6.0 covers
# older Rhino 8 service releases that still run on .NET 7.
foreach ($tfm in @("net48", "net8.0", "net6.0")) {
    $src = Join-Path $harmonyLib "$tfm\0Harmony.dll"
    if (-not (Test-Path $src)) {
        if ($tfm -eq "net6.0") { Write-Host "    skip harmony\net6.0 (not in this Harmony package)" -ForegroundColor DarkYellow; continue }
        Fail "Missing Harmony build: $src"
    }
    $dst = Join-Path $stage "harmony\$tfm"
    New-Item -ItemType Directory -Path $dst -Force | Out-Null
    Copy-Item $src (Join-Path $dst "0Harmony.dll") -Force
    Ok "harmony\$tfm\0Harmony.dll"
}

# RhinoCommon and Grasshopper must never be shipped inside a package.
foreach ($banned in @("RhinoCommon.dll", "Grasshopper.dll", "GH_IO.dll", "Eto.dll")) {
    if (Test-Path (Join-Path $stage $banned)) {
        Fail "$banned ended up in the package. Rhino supplies it; shipping it will break the plugin on other machines."
    }
}
Ok "No Rhino assemblies bundled"

# ---------------------------------------------------------------- yak
Step "Building the Yak package"

Push-Location $stage
try {
    & $Yak build
    if ($LASTEXITCODE -ne 0) { Fail "yak build failed. Check manifest.yml." }
}
finally { Pop-Location }

$yakFile = Get-ChildItem $stage -Filter *.yak | Select-Object -First 1
if (-not $yakFile) { Fail "yak build reported success but produced no .yak file." }

Write-Host "`n=== Done" -ForegroundColor Cyan
Write-Host "    $($yakFile.FullName)`n" -ForegroundColor Green

Write-Host "Next, in this order:" -ForegroundColor Yellow
Write-Host ""
Write-Host "  1. Test the package on a CLEAN profile first."
Write-Host "     Remove bin\Debug from GrasshopperDeveloperSettings and delete any"
Write-Host "     GHShield copy from %APPDATA%\Grasshopper\Libraries, then:"
Write-Host ""
Write-Host "         `"$Yak`" install --source `"$stage`" ghshield"
Write-Host ""
Write-Host "  2. Work through RELEASE.md sections 2-5 against that install."
Write-Host ""
Write-Host "  3. Only then publish:"
Write-Host ""
Write-Host "         `"$Yak`" login"
Write-Host "         `"$Yak`" push `"$($yakFile.FullName)`""
Write-Host ""
