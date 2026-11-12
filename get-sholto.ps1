# Sholto - download the latest Windows release, install its runtime tools, add a
# Start-menu shortcut, and run it. Paste into PowerShell:
#
#   irm https://raw.githubusercontent.com/freedomfirst26/Sholto/main/get-sholto.ps1 | iex
#
# Installs the app into %LOCALAPPDATA%\Sholto\app (your library and analysis cache
# live next to it in %LOCALAPPDATA%\Sholto, and an update replaces only app\).
# Re-run any time to update to the newest release. No admin rights needed.
#
# `iex` cannot pass parameters, so the options are environment variables:
#   $env:SHOLTO_DIR = "D:\Sholto"   install somewhere other than %LOCALAPPDATA%\Sholto
#   $env:SHOLTO_NO_RUN = "1"        install without launching
#
# This runs inside the user's own PowerShell window, so it never calls `exit`
# (that would close their window): everything is in one function, and errors
# are thrown. Windows PowerShell 5.1, ASCII only.

function Install-Sholto {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    $ProgressPreference = "SilentlyContinue"

    $repo = "freedomfirst26/Sholto"
    $root = if ($env:SHOLTO_DIR) { $env:SHOLTO_DIR } else { Join-Path $env:LOCALAPPDATA "Sholto" }
    $app = Join-Path $root "app"
    $exe = Join-Path $app "Sholto.exe"
    $versionFile = Join-Path $root ".version"
    $deps = Join-Path $app "install-deps.ps1"
    $verify = "powershell -NoProfile -ExecutionPolicy Bypass -File `"$deps`" -Verify"
    $depsExit = $null   # stays $null when the installed version is already current

    Write-Host "Sholto - install"

    # Refuse to replace files under a running Sholto rather than kill it.
    $running = Get-Process -Name "Sholto" -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and $_.Path.StartsWith($app + "\", [StringComparison]::OrdinalIgnoreCase) }
    if ($running) { throw "Sholto is running from $app - close Sholto and run the line again." }

    Write-Host "  finding the latest release..."
    try {
        $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/latest" `
            -Headers @{ "User-Agent" = "sholto-installer"; "Accept" = "application/vnd.github+json" } -ErrorAction Stop
    }
    catch {
        $status = $null
        if ($_.Exception.Response) { $status = [int]$_.Exception.Response.StatusCode }
        if ($status -eq 404) { throw "no stable Windows release yet - see https://github.com/$repo/releases" }
        throw "could not reach GitHub to find the latest release: $($_.Exception.Message)"
    }
    $asset = @($release.assets) | Where-Object { $_.name -like "*windows-x64.zip" } | Select-Object -First 1
    if (-not $asset) { throw "no stable Windows release yet - see https://github.com/$repo/releases" }
    $tag = $release.tag_name

    $installed = $null
    if (Test-Path -LiteralPath $versionFile) { $installed = (Get-Content -LiteralPath $versionFile -TotalCount 1) }

    if ($installed -eq $tag -and (Test-Path -LiteralPath $exe)) {
        Write-Host "  $tag is already installed in $app"
    }
    else {
        Write-Host "  downloading $tag into $app"
        New-Item -ItemType Directory -Path $root -Force -ErrorAction Stop | Out-Null
        # Stage inside $root, so the final move never crosses drives.
        $stage = Join-Path $root ("app-new-" + [guid]::NewGuid().ToString("N"))
        $zip = "$stage.zip"
        try {
            Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zip -UseBasicParsing -ErrorAction Stop
            Expand-Archive -LiteralPath $zip -DestinationPath $stage -Force -ErrorAction Stop
            # Clear the downloaded-from-the-internet mark, or Windows blocks the scripts.
            Get-ChildItem -LiteralPath $stage -Recurse -File | Unblock-File
            if (-not (Test-Path -LiteralPath (Join-Path $stage "Sholto.exe"))) { throw "the release zip has no Sholto.exe at its root" }
            if (-not (Test-Path -LiteralPath (Join-Path $stage "install-deps.ps1"))) { throw "the release zip has no install-deps.ps1 at its root" }

            # Replace only app\ - the library and caches beside it stay.
            Remove-Item -LiteralPath $versionFile -Force -ErrorAction SilentlyContinue
            if (Test-Path -LiteralPath $app) { Remove-Item -LiteralPath $app -Recurse -Force -ErrorAction Stop }
            Move-Item -LiteralPath $stage -Destination $app -ErrorAction Stop
        }
        finally {
            Remove-Item -LiteralPath $zip -Force -ErrorAction SilentlyContinue
            if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue }
        }

        Write-Host "  installing runtime tools (ffmpeg, uv, Beat This!, demucs) - this can take a while"
        & powershell -NoProfile -ExecutionPolicy Bypass -File $deps
        $depsExit = $LASTEXITCODE
    }

    # The shortcut goes in whatever install-deps returned: the app is installed.
    $programs = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
    New-Item -ItemType Directory -Path $programs -Force | Out-Null
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut((Join-Path $programs "Sholto.lnk"))
    $link.TargetPath = $exe
    $link.WorkingDirectory = $app
    $link.Description = "Sholto DJ"
    $link.Save()
    Write-Host "  Start menu shortcut: Sholto"

    if ($null -ne $depsExit) {
        if ($depsExit -ne 0) {
            throw "Sholto is installed, but its analysis tools are not working (see above). Fix the problem, then recheck with: $verify"
        }
        Set-Content -LiteralPath $versionFile -Value $tag -Encoding Ascii
    }

    Write-Host "  done: $exe"
    if ($env:SHOLTO_NO_RUN -eq "1") { return }

    # Tools installed above are in the registry's PATH, not yet in this window's.
    $machinePath = [Environment]::GetEnvironmentVariable("Path", "Machine")
    $userPath = [Environment]::GetEnvironmentVariable("Path", "User")
    $env:Path = (@($machinePath, $userPath) | Where-Object { $_ }) -join ";"

    Write-Host "  launching..."
    Start-Process -FilePath $exe -WorkingDirectory $app
}

Install-Sholto
