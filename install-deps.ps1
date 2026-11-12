# Sholto - install ONLY the runtime tools the app needs on Windows, for people
# running the prebuilt Windows release. No .NET SDK, no build, no admin rights.
# The Windows twin of install-deps.sh. Safe to re-run: each step is skipped when
# its tool already works.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File install-deps.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File install-deps.ps1 -Verify
#
#   -Verify   only run the checks; exit 1 if Beat This! (or demucs, unless
#             -NoStems) is broken
#   -NoStems  skip demucs (stem separation); it is optional
#
# Windows PowerShell 5.1, ASCII only - see the header of sholto-deps.ps1.
param(
    [switch]$Verify,
    [switch]$NoStems
)

[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
$ProgressPreference = "SilentlyContinue"

# -- Shared tool definitions --------------------------------------------------
# Pins and health checks live in sholto-deps.ps1, which ships next to this file.
$depsFile = Join-Path $PSScriptRoot "sholto-deps.ps1"
if (-not (Test-Path -LiteralPath $depsFile)) {
    Write-Host "  [x] sholto-deps.ps1 is missing from $PSScriptRoot - extract the release zip again." -ForegroundColor Yellow
    exit 1
}
. $depsFile

$rerun = "powershell -NoProfile -ExecutionPolicy Bypass -File `"$(Join-Path $PSScriptRoot 'install-deps.ps1')`""

# Tools installed since this window opened are only in the registry: start from it.
Update-SholtoSessionPath

if ($Verify) {
    Write-Host ""
    Write-Host "Sholto" -ForegroundColor Blue -NoNewline
    Write-Host " - verify" -ForegroundColor DarkGray
    exit (Invoke-SholtoDepsVerify -NoStems:$NoStems)
}

Write-Host ""
Write-Host "Sholto" -ForegroundColor Blue -NoNewline
Write-Host " - runtime dependencies" -ForegroundColor DarkGray

# 1. ffmpeg - the shared build, from winget, only when there is none on PATH.
if (-not (Find-SholtoOnPath "ffmpeg")) {
    if (-not (Get-Command "winget.exe" -CommandType Application -ErrorAction SilentlyContinue)) {
        Write-SholtoWarn "ffmpeg is not installed, and winget (which installs it) is missing."
        Write-SholtoWarn "Install 'App Installer' from the Microsoft Store, then run this again:"
        Write-SholtoWarn "  $rerun"
        Write-SholtoWarn "Or install a shared ffmpeg build yourself: https://ffmpeg.org/download.html"
        exit 1
    }
    Write-SholtoInfo "Installing ffmpeg (shared build) with winget..."
    & winget install --id $SholtoFfmpegWingetId -e --accept-source-agreements --accept-package-agreements
    # winget's exit codes include "already installed"; whether ffmpeg is now
    # findable is the real test, just below.
    Update-SholtoSessionPath
}
$ffmpeg = Find-SholtoFfmpeg
if (-not $ffmpeg) {
    Write-SholtoWarn "ffmpeg could not be installed. Install a shared ffmpeg build (https://ffmpeg.org/download.html),"
    Write-SholtoWarn "put its bin folder on your PATH, then run this again:"
    Write-SholtoWarn "  $rerun"
    exit 1
}
# torchcodec loads avcodec-*.dll from beside the REAL ffmpeg.exe, not from
# winget's Links folder, so that real bin folder has to be on PATH.
$ffmpegBin = Split-Path -Parent $ffmpeg
Add-SholtoUserPath $ffmpegBin
Add-SholtoSessionPath $ffmpegBin
Write-SholtoOk "ffmpeg  ($ffmpeg)"

# 2. uv - installs the Python analysis tools in their own isolated environments
#    without touching any system Python.
if (-not (Find-SholtoTool "uv")) {
    Write-SholtoInfo "Installing uv (Python tool manager)..."
    & powershell -NoProfile -ExecutionPolicy Bypass -Command "irm https://astral.sh/uv/install.ps1 | iex"
    Add-SholtoSessionPath $SholtoUserBin
}
$uv = Find-SholtoTool "uv"
if (-not $uv) {
    Write-SholtoWarn "uv could not be installed (see the messages above). Check your network, then run this again:"
    Write-SholtoWarn "  $rerun"
    exit 1
}
Write-SholtoOk "uv $((& $uv --version 2>$null) -replace '^uv\s+', '')"

# 3. Beat This! - REQUIRED. Without a beatgrid a track will not play. A broken
#    Beat This! fails the install, but only at the end, so demucs still gets
#    installed for a user who fixes Beat This! afterwards. madmom, the old beat
#    tracker, is never installed: its models are non-commercial only.
$beatThisBroken = $false
if (Test-SholtoBeatThis) {
    Write-SholtoOk "Beat This! (beat tracker) already working"
}
else {
    Install-SholtoBeatThis | Out-Null
    if (Test-SholtoBeatThis) {
        Write-SholtoOk "Beat This! (beat tracker) - verified on a test click track"
    }
    else {
        Write-SholtoWarn "Beat This! installed but failed its functional check ($SHOLTO_CHECK_DETAIL)"
        $beatThisBroken = $true
    }
}

# 4. demucs - OPTIONAL. Stem separation (drums / vocals / bass / other).
if ($NoStems) {
    Write-SholtoInfo "demucs (stems) skipped (-NoStems)"
}
elseif (Test-SholtoDemucs) {
    Write-SholtoOk "demucs (stems) already working"
}
else {
    Install-SholtoDemucs | Out-Null
    if (Test-SholtoDemucs) {
        Write-SholtoOk "demucs (stems) - verified with a test separation"
    }
    else {
        Write-SholtoWarn "demucs installed but failed its functional check ($SHOLTO_CHECK_DETAIL) - stems disabled"
    }
}

Write-Host ""
if ($beatThisBroken) {
    Write-SholtoWarn "Beat This! (beat tracker) is REQUIRED and still not working - Sholto will run"
    Write-SholtoWarn "but no track will get a beatgrid, so nothing will play."
    Write-SholtoWarn "Check the warning above, fix your network or ffmpeg, then run this again:"
    Write-SholtoWarn "  $rerun"
    Write-SholtoWarn "(add -Verify to the end to only recheck)"
    exit 1
}
Write-SholtoOk "Done."
exit 0
