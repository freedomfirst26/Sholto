---
description: Cut a Sholto release — checks, version bump, changelog, and the exact commands to run
---

Cut a release. Work it out yourself; do not ask the user what version to use.

## 1. Gather facts

```bash
git describe --tags --abbrev=0                      # last tag
git log --oneline $(git describe --tags --abbrev=0)..HEAD
git status --porcelain
sed -n '/^## Unreleased/,/^## v/p' CHANGELOG.md
```

## 2. Run the checks. Report every failure before doing anything else.

- **Uncommitted changes?** List them. A release must be cut from a clean tree — say so and stop.
- **Is `## Unreleased` empty?** Then there is nothing to release. Stop.
- **Controller behaviour changed without the guide following?**
  ```bash
  LAST=$(git describe --tags --abbrev=0)
  git diff --name-only $LAST..HEAD -- src/Sholto.Controller/Gestures/ src/Sholto.App/Orchestrator.cs src/Sholto.Controller/Mappings/
  git diff --name-only $LAST..HEAD -- src/Sholto.Faceplate/Devices/
  ```
  First list non-empty and second empty means a gesture probably changed and the in-app
  guide did not follow. Check the gesture's arm in `Orchestrator.HandleGesture` against its
  sentence in `ddj-flx4.guide.json` before continuing — the switch is the only authority on
  what a gesture does, and comments about it have been stale before.
- **Does the changelog still tell the truth?** Read every line under `## Unreleased`.
  Each one claims something about the shipped app. Spot-check the doubtful ones against
  the code. A changelog line that was true when written and false now is worse than no
  line — it is the release notes users read.
- **Does it build and test?**
  ```bash
  pkill -9 -f "bin/Debug/net10.0/Sholto.App"
  dotnet build src/Sholto.App/Sholto.App.csproj -nologo
  dotnet test Sholto.slnx -nologo 2>&1 | grep -E "Passed!|Failed!"
  ```
  `DeckFaderInitTests.NewDeck_StartsFaderDown_SoNothingPlaysUntilPickedUp` fails on main
  and is known — any other failure stops the release.

## 3. Decide the version yourself

Pre-1.0 semver, from the last tag:
- Anything under `### New` → bump the **minor** (`v0.2.0` → `v0.3.0`).
- Only `### Fixed` / `### Improved` / `### Housekeeping` → bump the **patch**.

State the version and the one-line reason.

## 4. Edit the changelog

Rename `## Unreleased` to `## vX.Y.Z` and insert a fresh empty block above it:

```markdown
## Unreleased

## vX.Y.Z
```

Change nothing else. Do not reword entries at this point — if one is wrong, that is a
check failure from step 2, not a silent edit here.

## 5. Stop, and hand over

**Never run `git add`, `git commit`, `git tag` or `git push`.** Print the exact commands
for the user to run:

```bash
git add CHANGELOG.md && git commit -m "changelog: cut vX.Y.Z"
git tag -a vX.Y.Z -m "vX.Y.Z"
git push origin main vX.Y.Z
```

Then say what happens next: pushing the tag fires `.github/workflows/release.yml`, which
builds a self-contained linux-x64 single-file binary, bundles README, LICENSE and
install-deps.sh into `sholto-vX.Y.Z-linux-x64.tar.gz`, takes the `## vX.Y.Z` section of
the changelog as the release body, and creates the GitHub Release.

## Report format

Keep it short. Checks that passed get one line total, not one line each. Spend the words
on anything that failed, the version you chose and why, and the commands to run.
