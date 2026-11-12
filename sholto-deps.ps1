# Sholto - shared definition of the Python analysis tools, Windows edition.
#
# DOT-SOURCED, never run. install-deps.ps1 dot-sources this file, so the pins and
# the health checks exist in one place on Windows. It is the Windows twin of
# sholto-deps.sh: the pins below MUST equal the ones there, and
# WindowsInstallerScriptsTests fails if they drift. To bump a pin, edit
# sholto-deps.sh (the reasoning behind every version lives there), copy the value
# here, then run install-deps.ps1 and install-deps.ps1 -Verify.
#
# Written for Windows PowerShell 5.1 (the one every Windows 10/11 has): no
# PowerShell 7 syntax, ASCII only (5.1 reads a BOM-less UTF-8 file as ANSI), and
# native commands are judged by $LASTEXITCODE, never by ErrorActionPreference Stop
# (5.1 turns a native program's stderr into error records).

$SHOLTO_PYTHON = "3.12"

# Beat This! - the REQUIRED beat tracker, CPU build (Sholto runs it with --gpu -1).
$SHOLTO_BEATTHIS_SPEC = "beat-this==1.1.0"
$SHOLTO_BEATTHIS_WITH = @("--with", "torch==2.14.0", "--with", "torchaudio==2.11.1", "--with", "torchcodec==0.16.0")
$SHOLTO_BEATTHIS_INDEX = "https://download.pytorch.org/whl/cpu"

# madmom - OPTIONAL fallback. Not installed by any installer; kept so the pins
# match sholto-deps.sh and -Verify can report it.
$SHOLTO_MADMOM_SPEC = "madmom-onnx==0.17.dev0"
$SHOLTO_MADMOM_WITH = @("--with", "numpy==2.4.5", "--with", "onnxruntime==1.26.0", "--with", "scipy==1.17.1")

# demucs - OPTIONAL stems. torchaudio is ABSENT ON PURPOSE - see sholto-deps.sh.
$SHOLTO_DEMUCS_SPEC = "demucs==4.1.0"
$SHOLTO_DEMUCS_WITH = @("--with", "torch==2.14.0", "--with", "torchcodec==0.16.0", "--with", "numpy==2.5.3")

# The winget package for ffmpeg. torchcodec (used by Beat This! and demucs) loads
# FFmpeg's shared DLLs (avcodec-*.dll ...) on Windows, which the static build lacks.
$SholtoFfmpegWingetId = "Gyan.FFmpeg.Shared"
$SholtoFfmpegSharedHint = "the ffmpeg build has no shared DLLs (no avcodec-*.dll next to ffmpeg.exe), which torchcodec needs: run winget install --id Gyan.FFmpeg.Shared -e"

# Where uv puts its tool shims on Windows, and where the app looks first.
$SholtoUserBin = Join-Path $env:USERPROFILE ".local\bin"

# Each check sets this to a short line naming the binary it actually tested (and
# any problem), so the installer can print which one it was.
$SHOLTO_CHECK_DETAIL = ""

# -- Output -------------------------------------------------------------------

function Write-SholtoOk([string]$Text) {
    Write-Host "  [ok] " -ForegroundColor Green -NoNewline
    Write-Host $Text
}

function Write-SholtoInfo([string]$Text) {
    Write-Host "  - " -ForegroundColor Cyan -NoNewline
    Write-Host $Text -ForegroundColor DarkGray
}

function Write-SholtoWarn([string]$Text) {
    Write-Host "  [x] " -ForegroundColor Yellow -NoNewline
    Write-Host $Text
}

# -- PATH ---------------------------------------------------------------------

# Rebuild this session's PATH from the registry (Machine, then User), so tools
# installed since the window opened are visible, then keep the uv shim folder.
function Update-SholtoSessionPath {
    $machine = [Environment]::GetEnvironmentVariable("Path", "Machine")
    $user = [Environment]::GetEnvironmentVariable("Path", "User")
    $env:Path = (@($machine, $user) | Where-Object { $_ }) -join ";"
    Add-SholtoSessionPath $SholtoUserBin
}

# Put a folder at the front of this session's PATH, once.
function Add-SholtoSessionPath([string]$Folder) {
    $parts = @($env:Path -split ";" | Where-Object { $_ -and ($_.TrimEnd("\") -ne $Folder.TrimEnd("\")) })
    $env:Path = (@($Folder) + $parts) -join ";"
}

# Add a folder to the User PATH in the registry, once, keeping the value's
# REG_EXPAND_SZ type and any %VARIABLES% in it (Environment.SetEnvironmentVariable
# would flatten both). Then nudge Windows to tell running programs, the same way
# astral's uv installer does: set and clear a dummy user variable, which
# broadcasts WM_SETTINGCHANGE.
function Add-SholtoUserPath([string]$Folder) {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey("Environment", $true)
    if ($null -eq $key) { return }
    try {
        $raw = [string]$key.GetValue("Path", "", [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
        foreach ($p in ($raw -split ";")) {
            if ($p -and ([Environment]::ExpandEnvironmentVariables($p).TrimEnd("\") -eq $Folder.TrimEnd("\"))) { return }
        }
        $new = (@($raw -split ";" | Where-Object { $_ }) + @($Folder)) -join ";"
        $key.SetValue("Path", $new, [Microsoft.Win32.RegistryValueKind]::ExpandString)
    }
    finally { $key.Close() }
    [Environment]::SetEnvironmentVariable("SHOLTO_PATH_REFRESH", "1", "User")
    [Environment]::SetEnvironmentVariable("SHOLTO_PATH_REFRESH", [NullString]::Value, "User")
}

# -- Binary resolution --------------------------------------------------------
# Resolve each tool the way the app resolves it on Windows
# (ExternalToolOptionsFactory.Windows): %USERPROFILE%\.local\bin\<name>.exe first,
# then PATH. A check can then never pass for a binary the app will not run.

# Print the path PATH resolves <name>.exe to, or nothing.
function Find-SholtoOnPath([string]$Name) {
    $cmd = Get-Command "$Name.exe" -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($cmd) { return $cmd.Source }
    return $null
}

# Print the path the app would run for <name>, or nothing.
function Find-SholtoTool([string]$Name) {
    $own = Join-Path $SholtoUserBin "$Name.exe"
    if (Test-Path -LiteralPath $own -PathType Leaf) { return $own }
    return Find-SholtoOnPath $Name
}

# Follow a symbolic link (winget's WinGet\Links entries are symlinks) to the real
# file. A path that is not a link comes back unchanged.
function Resolve-SholtoRealPath([string]$Path) {
    $item = Get-Item -LiteralPath $Path -ErrorAction SilentlyContinue
    if ($null -eq $item -or -not $item.LinkType) { return $Path }
    $target = @($item.Target) | Select-Object -First 1
    if (-not $target) { return $Path }
    if (-not [IO.Path]::IsPathRooted($target)) { $target = Join-Path (Split-Path -Parent $Path) $target }
    return $target
}

# Print the real ffmpeg.exe (behind any WinGet\Links symlink), or nothing. Falls
# back to winget's package folder, for a fresh install whose link or PATH entry
# this session cannot see yet.
function Find-SholtoFfmpeg {
    $onPath = Find-SholtoOnPath "ffmpeg"
    if ($onPath) { return Resolve-SholtoRealPath $onPath }
    $packages = Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Packages"
    if (Test-Path -LiteralPath $packages) {
        $found = Get-ChildItem -LiteralPath $packages -Directory -Filter "$SholtoFfmpegWingetId*" -ErrorAction SilentlyContinue |
            ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -Recurse -Filter "ffmpeg.exe" -ErrorAction SilentlyContinue } |
            Select-Object -First 1
        if ($found) { return $found.FullName }
    }
    return $null
}

# True when FFmpeg's shared DLLs sit next to the real ffmpeg.exe.
function Test-SholtoFfmpegShared([string]$FfmpegExe) {
    if (-not $FfmpegExe) { return $false }
    $dir = Split-Path -Parent $FfmpegExe
    return [bool](Get-ChildItem -LiteralPath $dir -Filter "avcodec-*.dll" -ErrorAction SilentlyContinue | Select-Object -First 1)
}

# -- Installs -----------------------------------------------------------------

# uv's output goes to the console (Out-Host), so these return only their $true/$false.

# Install the pinned Beat This! with CPU-only torch. If the CPU index cannot be
# reached or resolved, fall back to the same pins from PyPI. Returns $true when
# one of the two installs succeeded.
function Install-SholtoBeatThis {
    $uv = Find-SholtoTool "uv"
    if (-not $uv) { return $false }
    & $uv tool install --force --python $SHOLTO_PYTHON $SHOLTO_BEATTHIS_SPEC @SHOLTO_BEATTHIS_WITH --index $SHOLTO_BEATTHIS_INDEX | Out-Host
    if ($LASTEXITCODE -eq 0) { return $true }
    Write-SholtoInfo "The CPU package index did not work - trying the same versions from PyPI (a bigger download)..."
    & $uv tool install --force --python $SHOLTO_PYTHON $SHOLTO_BEATTHIS_SPEC @SHOLTO_BEATTHIS_WITH | Out-Host
    return ($LASTEXITCODE -eq 0)
}

# Install the pinned demucs. Returns $true when uv succeeded.
function Install-SholtoDemucs {
    $uv = Find-SholtoTool "uv"
    if (-not $uv) { return $false }
    & $uv tool install --force --python $SHOLTO_PYTHON $SHOLTO_DEMUCS_SPEC @SHOLTO_DEMUCS_WITH | Out-Host
    return ($LASTEXITCODE -eq 0)
}

# -- Functional checks --------------------------------------------------------
# A file existing in .local\bin does not prove the tool works. These run each
# tool for real, with the arguments the app uses.

function New-SholtoTempFolder {
    $dir = Join-Path ([IO.Path]::GetTempPath()) ("sholto-check-" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    return $dir
}

# Beat This! failed: record why, plus the shared-DLL hint when ffmpeg is the
# static build (the most likely cause on Windows).
function Set-SholtoBeatThisFailure([string]$Text) {
    $ffmpeg = Find-SholtoFfmpeg
    if ($ffmpeg -and -not (Test-SholtoFfmpegShared $ffmpeg)) { $Text = "$Text; $SholtoFfmpegSharedHint" }
    $script:SHOLTO_CHECK_DETAIL = $Text
}

# Run Beat This! for real on eight seconds of 120 BPM clicks, exactly as the app
# runs it (BeatThisTool.BuildArgs: <input> -o <file> --gpu -1), and assert at
# least one "TIME<TAB>BEAT_NUMBER" line. The first run also downloads the model
# checkpoint (~78 MB), so after this check the app never has to.
function Test-SholtoBeatThis {
    $script:SHOLTO_CHECK_DETAIL = ""
    $bin = Find-SholtoTool "beat_this"
    if (-not $bin) { Set-SholtoBeatThisFailure "not found"; return $false }
    $script:SHOLTO_CHECK_DETAIL = $bin

    $ffmpeg = Find-SholtoOnPath "ffmpeg"
    if (-not $ffmpeg) {
        $script:SHOLTO_CHECK_DETAIL = "$bin (cannot test: ffmpeg is not installed - run winget install --id Gyan.FFmpeg.Shared -e)"
        return $false
    }

    & $bin --help 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { Set-SholtoBeatThisFailure "$bin (does not run - broken install?)"; return $false }

    Write-SholtoInfo "Verifying Beat This! on a test click track (the first run downloads its ~78 MB model)..."
    $tmp = New-SholtoTempFolder
    try {
        $click = Join-Path $tmp "click.wav"
        $beats = Join-Path $tmp "click.beats"
        & $ffmpeg -f lavfi -i 'aevalsrc=if(lt(mod(t\,0.5)\,0.02)\,sin(2*PI*1000*t)\,0):s=44100:d=8' -y $click -loglevel error 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $click) -or (Get-Item -LiteralPath $click).Length -eq 0) {
            $script:SHOLTO_CHECK_DETAIL = "$bin (could not generate test audio - ffmpeg failed, not Beat This!)"
            return $false
        }

        & $bin $click -o $beats --gpu -1 2>&1 | Out-Null

        $found = $false
        if (Test-Path -LiteralPath $beats) {
            foreach ($line in (Get-Content -LiteralPath $beats)) {
                if ($line -match '^[0-9.]+\t[0-9]+\s*$') { $found = $true; break }
            }
        }
        if (-not $found) { Set-SholtoBeatThisFailure "$bin (ran, but found no beats in the test clicks)"; return $false }
        return $true
    }
    finally { Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue }
}

# madmom is the optional fallback: a check that it starts, for -Verify only.
function Test-SholtoMadmom {
    $script:SHOLTO_CHECK_DETAIL = ""
    $bin = Find-SholtoTool "DBNDownBeatTracker"
    if (-not $bin) { $script:SHOLTO_CHECK_DETAIL = "not found"; return $false }
    $script:SHOLTO_CHECK_DETAIL = $bin
    & $bin -h 2>&1 | Out-Null
    return ($LASTEXITCODE -eq 0)
}

# Separate one second of silence and assert the four files the app will look
# for: the app's --out / --filename arguments and its <out>\htdemucs\<stem>.wav
# layout. "Some wavs somewhere" would pass for a model that writes a differently
# named folder, which the app would then fail to find.
function Test-SholtoDemucs {
    $script:SHOLTO_CHECK_DETAIL = ""
    $bin = Find-SholtoTool "demucs"
    if (-not $bin) { $script:SHOLTO_CHECK_DETAIL = "not found"; return $false }
    $script:SHOLTO_CHECK_DETAIL = $bin

    $ffmpeg = Find-SholtoOnPath "ffmpeg"
    if (-not $ffmpeg) { $script:SHOLTO_CHECK_DETAIL = "$bin (cannot test: ffmpeg is not installed)"; return $false }

    # Cheap pre-check: does the binary even start?
    & $bin --help 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { $script:SHOLTO_CHECK_DETAIL = "$bin (does not run - broken install?)"; return $false }

    Write-SholtoInfo "Verifying demucs with a real test separation (loads the model - can take tens of seconds)..."
    $tmp = New-SholtoTempFolder
    try {
        $silence = Join-Path $tmp "silence.wav"
        $out = Join-Path $tmp "out"
        & $ffmpeg -f lavfi -i "anullsrc=r=44100:cl=stereo" -t 1 -y $silence -loglevel error 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $silence) -or (Get-Item -LiteralPath $silence).Length -eq 0) {
            $script:SHOLTO_CHECK_DETAIL = "$bin (could not generate test audio - ffmpeg failed, not demucs)"
            return $false
        }

        & $bin --out $out --filename '{stem}.{ext}' $silence 2>&1 | Out-Null

        $missing = @()
        foreach ($stem in @("vocals", "drums", "bass", "other")) {
            $file = Join-Path $out "htdemucs\$stem.wav"
            if (-not (Test-Path -LiteralPath $file -PathType Leaf) -or (Get-Item -LiteralPath $file).Length -eq 0) { $missing += $stem }
        }
        if ($missing.Count -gt 0) {
            $script:SHOLTO_CHECK_DETAIL = "$bin (no output for: $($missing -join ' '))"
            return $false
        }
    }
    finally { Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue }

    # A demucs that works but is not the one we pinned is worth saying out loud:
    # it is the most likely explanation for a working check and a broken app.
    $own = Join-Path $SholtoUserBin "demucs.exe"
    if ($bin -ne $own) { $script:SHOLTO_CHECK_DETAIL = "$bin - NOTE: not the uv-managed install at $own" }
    return $true
}

# -- Shared -Verify entry point -----------------------------------------------
# Returns 1 if a tool the app depends on is broken, else 0. madmom is optional:
# its absence is reported, not failed. With -NoStems, demucs is reported only.
function Invoke-SholtoDepsVerify([switch]$NoStems) {
    $rc = 0

    if (Test-SholtoBeatThis) { Write-SholtoOk "Beat This! working  ($SHOLTO_CHECK_DETAIL) - found beats in a test click track" }
    else { Write-SholtoWarn "Beat This! NOT working  ($(if ($SHOLTO_CHECK_DETAIL) { $SHOLTO_CHECK_DETAIL } else { 'unknown' })) - tracks will not get a beatgrid"; $rc = 1 }

    if (Test-SholtoMadmom) { Write-SholtoOk "madmom-onnx (optional fallback) present  ($SHOLTO_CHECK_DETAIL)" }
    else { Write-SholtoInfo "madmom-onnx (optional fallback) not installed - fine, Beat This! finds the beats" }

    if (Test-SholtoDemucs) { Write-SholtoOk "demucs working  ($SHOLTO_CHECK_DETAIL) - test separation produced 4 stems" }
    elseif ($NoStems) { Write-SholtoInfo "demucs not working  ($SHOLTO_CHECK_DETAIL) - fine with -NoStems, stems are off" }
    else { Write-SholtoWarn "demucs NOT working  ($(if ($SHOLTO_CHECK_DETAIL) { $SHOLTO_CHECK_DETAIL } else { 'unknown' })) - stems will be missing"; $rc = 1 }

    return $rc
}
