# Review: c234-windows-installer-install-deps-ps1 (f639f4f)

### Spec Compliance

- ✅ Spec compliant. J1 to J6 are all met (traced per id below).
- ⚠️ Cannot verify from the diff: `.github/workflows/release.yml:21` only mentions win-x64 in a comment. Nothing in it builds a `*win-x64.zip`, and nothing bundles `install-deps.ps1`/`sholto-deps.ps1` at the zip root, which `get-sholto.ps1:76-77` requires. Until the release job does both, the one-liner throws "no stable Windows release yet". This task owns only the new files, so the controller should confirm that another job (C160/C232) ships the zip with both .ps1 files at its root.
- ⚠️ Cannot verify here: J5(3) and the J6 Windows PowerShell 5.1 proof run only on CI's windows-latest leg (`.github/workflows/ci.yml:19` has `os: [ubuntu-latest, windows-latest]`, so the job exists). Confirm that the leg is green for this commit.

Per J item:
- **J1 met.** Pins in `sholto-deps.ps1:15-29` match `sholto-deps.sh:40-65` value for value (I checked by eye against `grep '^SHOLTO_' sholto-deps.sh`: 8 pins, and the CPU index matches).
  - Tool lookup: `Find-SholtoTool` `sholto-deps.ps1:111-115` checks %USERPROFILE%\.local\bin\<name>.exe first, then PATH. That matches `ExternalToolOptionsFactory.Windows()` (`src/Sholto.App.ExternalTools/ExternalToolOptionsFactory.cs:95-103`, one SearchDirectory `.local\bin` plus `.exe`), which I read to test the named risk "the check resolves a different binary than the app".
  - Install: CPU index first, PyPI fallback (`:158-166`).
  - Beat This! check: 8 s aevalsrc click, `$click -o $beats --gpu -1` (`:224`), and a TIME<TAB>N regex (`:229`).
  - madmom check: `-h` (`:244`).
  - demucs check: 1 s anullsrc, four non-empty `htdemucs\<stem>.wav` (`:279-282`), and a NOTE when the binary is not `.local\bin\demucs.exe` (`:292-293`).
  - Shared-DLL hint: appended when no avcodec-*.dll sits next to the real ffmpeg.exe (`:188-191`, `:145-149`, `:34`). It names `winget install --id Gyan.FFmpeg.Shared -e`.
- **J2 met** (`install-deps.ps1`).
  - PATH is refreshed first (`:34`, after dot-source).
  - ffmpeg: winget runs only when ffmpeg.exe is not on PATH (`:48-61`). The real bin folder then goes to the User PATH and the session PATH (`:71-73`).
  - uv: astral install.ps1 runs only when uv is missing (`:78-82`).
  - Beat This!: installed only when its check fails (`:96-108`).
  - demucs: installed unless -NoStems, and a failure only warns (`:111-125`).
  - No madmom install anywhere.
  - On a broken Beat This!, exit 1 with the `-File "<dir>\install-deps.ps1"` command plus the -Verify note (`:128-135`).
- **J3 met.** `-Verify` runs only `Invoke-SholtoDepsVerify` (`install-deps.ps1:36-41`), which prints three lines (`sholto-deps.ps1:300-314`). rc=1 on a broken Beat This!, or a broken demucs without -NoStems. madmom is info only, and with -NoStems demucs is printed as info. The return codes match `sholto-deps.sh:236-249`, except for -NoStems, which J3 asks for and the .sh lacks.
- **J4 met** (`get-sholto.ps1`).
  - One-liner header (`:4`). Everything is inside `Install-Sholto`, and there is no `exit`.
  - Asset filter `*win-x64.zip`. A 404 or a missing asset throws "no stable Windows release yet" (`:52`, `:56`).
  - Unpacks to a staging folder, then replaces only app\ (`:69-87`), and refuses while Sholto is running from app\ (`:40-42`).
  - Runs install-deps in a child `powershell -NoProfile -ExecutionPolicy Bypass -File` (`:90`).
  - The shortcut is created whatever install-deps returns (`:95-103`).
  - `.version` is written only on exit 0. Otherwise it throws with the -Verify command before launching (`:105-110`).
  - SHOLTO_NO_RUN skips the launch (`:113`), and PATH is refreshed from the registry before Start-Process (`:116-121`).
- **J5 met.** `WindowsInstallerScriptsTests.cs` has three facts:
  - Pins: CR is stripped (`:109`), CHECK_DETAIL is skipped (`:81`), `>= 7` is asserted (`:20`), and arrays are compared element by element via `Assert.Equal(string[], string[])` (`:24`).
  - ASCII check over all three .ps1 files (`:28-37`).
  - Windows-only 5.1 `Parser.ParseFile` that asserts `ERRORS=0` (`:39-71`).
  - The report's Evidence line records build 0 errors, 15/15 test projects passing, and the drift proof (numpy 2.5.4 made it fail).
- **J6 met.** The Evidence line records pwsh 7.4.6 `ParseFile` returning ERRORS=0 on each file. The 5.1 proof is assigned to J5(3) on windows-latest, as J6 allows.

### Strengths

- The tools are resolved exactly as the app resolves them, and the reason is documented. A check therefore cannot pass for a binary the app will not run (`sholto-deps.ps1:98-115`).
- `get-sholto.ps1` is careful about running under `iex`: no `exit`, throws instead. It stages inside the install root so the final Move-Item never crosses drives, deletes `.version` before app\ is replaced so a half-done update forces a retry, and runs Unblock-File so the scripts are not blocked as downloaded files.
- `Add-SholtoUserPath` keeps REG_EXPAND_SZ and %VARS%, is idempotent, and broadcasts the change (`sholto-deps.ps1:82-96`). A re-run repairs a lost PATH entry.
- Native commands are judged by `$LASTEXITCODE`, and uv output goes to `Out-Host`, so the functions return clean bools. This avoids the 5.1 pipeline-pollution trap.
- The pin test has a real drift proof, and its parser handles both quoting styles.

### Issues

#### Critical (Must Fix)
None.

#### Important (Should Fix)
None.

#### Minor (Nice to Have)

1. **The "running" guard misses a Sholto whose Path is null.** `get-sholto.ps1:40-41` keeps only processes whose `.Path` is non-null and under app\. A Sholto running elevated returns a null `.Path` to a non-elevated shell, so the guard passes. Then `Remove-Item -Recurse` (`:81`) partially deletes app\ around the locked exe and throws. The next run recovers, because `.version` was already removed, but the user gets an error instead of "close Sholto". Fix: also refuse when any `Sholto` process has a null Path, or test whether `$exe` can be opened for exclusive access before deleting.
2. **The two Beat This! helpers look for ffmpeg differently.** `Test-SholtoBeatThis` (`sholto-deps.ps1:204`) uses `Find-SholtoOnPath "ffmpeg"`, but `Set-SholtoBeatThisFailure` (`:189`) uses `Find-SholtoFfmpeg`, which also searches the WinGet Packages folder. They can disagree on whether ffmpeg exists. The effect is harmless today, because install-deps puts the bin folder on the session PATH first, but one finder would be clearer.
3. **The failure message can be misleading.** When `Install-SholtoBeatThis` returns `$false` (uv failed outright), install-deps still prints "Beat This! installed but failed its functional check" (`install-deps.ps1:102-106`). The same applies to demucs (`:119-123`). The return value is discarded with `Out-Null`, so the user is never told that the install itself failed. Fix: branch on the return value: "install failed" versus "installed but check failed".
4. **The Windows-only test passes instead of skipping on Linux.** `Windows_powershell_parses_every_windows_script_without_errors` returns early on non-Windows (`WindowsInstallerScriptsTests.cs:42-43`), so the Linux leg reports it as passed rather than skipped. J5 mandates "on Windows only", so this is within spec. A skip, for example `Skip.IfNot` or a conditional fact, would make the gap visible in test output.
5. **The build has 68 warnings.** The report records 68 build warnings across the solution. The report states that none are in the new file, so none are attributable to this diff; noted for pristine-output hygiene only.

### Assessment

**Task quality:** Approved

**Reasoning:** Every J item traces to specific lines, and the PowerShell avoids the usual 5.1 traps: pipeline pollution, the `exit`-under-`iex` trap, and REG_EXPAND_SZ flattening. The remaining issues are edge-case messaging and robustness. The one real dependency is outside this task: a release zip that actually carries the .ps1 files.
