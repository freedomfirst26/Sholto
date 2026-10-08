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
  git diff --name-only $LAST..HEAD -- src/Sholto.Interface.Controller/Gestures/ src/Sholto.Interface.Controller.Mappings/ src/Sholto.App/
  git diff --name-only $LAST..HEAD -- src/Sholto.Interface.Faceplate.Devices/
  ```
  First list non-empty and second empty means a gesture probably changed and the in-app
  guide did not follow. Gestures are recognised in `GestureRecognizer` and turned into
  commands in `GestureCommandTranslator` (both in `src/Sholto.Interface.Controller/Gestures/`);
  the device mappings live in `src/Sholto.Interface.Controller.Mappings/`. Check the
  gesture's arm in the translator, and the command handlers in `src/Sholto.App` it
  dispatches to, against its sentence in `ddj-flx4.guide.json` (under
  `src/Sholto.Interface.Faceplate.Devices/DdjFlx4/`) before continuing — the translator
  plus those handlers are the only authority on what a gesture does, and comments about
  it have been stale before.
- **Does the changelog still tell the truth?** Read every line under `## Unreleased`.
  Each one claims something about the shipped app. Spot-check the doubtful ones against
  the code. A changelog line that was true when written and false now is worse than no
  line — it is the release notes users read.
- **Does it build and test?**
  ```bash
  pkill -9 -f "[S]holto\.dll"    # bracket keeps the pattern from matching its own shell
  dotnet build Sholto.slnx -nologo
  # test projects are detached from the slnx, so run every one
  for p in tests/*/*.csproj; do dotnet test "$p" -nologo -nodeReuse:false; done
  ```
  Known failures that do not block a release:
  `GestureRecognizerTests.Every_declared_id_is_reachable_from_some_event` (`pad.padfx1.roll`),
  `FaceplateDocTests.Every_control_in_the_data_file_has_a_shape_in_the_layout` and
  `FaceplateDocTests.Every_gesture_the_recognizer_can_emit_has_a_description`.
  Any other failure stops the release.

## 3. Decide the version yourself

Semver from `v1.0.0` on, from the last tag (`v1.0.0` was the first major: the
Interface / Data / App rebuild):
- A breaking change or major rewrite → bump the **major** (`v1.4.2` → `v2.0.0`).
- Anything under `### New` → bump the **minor** (`v1.0.0` → `v1.1.0`).
- Only `### Fixed` / `### Improved` / `### Housekeeping` → bump the **patch**.

State the version and the one-line reason.

Licence statements in README, docs/license.md, THIRD-PARTY-NOTICES.md, Directory.Build.props
and the changelog say the PolyForm Shield switch happens "from v1.1.0". If the version
chosen here is not `v1.1.0`, stop and fix those statements to the real version before
going on.

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
builds a self-contained linux-x64 single-file binary, bundles README, LICENSE, THIRD-PARTY-NOTICES, docs/license.md and
install-deps.sh into `sholto-vX.Y.Z-linux-x64.tar.gz`, takes the `## vX.Y.Z` section of
the changelog as the release body, and creates the GitHub Release.

## Report format

Keep it short. Checks that passed get one line total, not one line each. Spend the words
on anything that failed, the version you chose and why, and the commands to run.
