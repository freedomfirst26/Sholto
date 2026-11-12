# Definition of done: job c138-controller-load-with-search

Frozen by `$T brief` at 2026-11-12T07:18. A changed outcome is a new job.

## Job items
- **J1** [reviewer] TrackLoader.Handle(LoadSelectedIntoDeck) picks the library SelectedTrack when command.Origin.InterfaceId is InterfaceIds.Controller, even if ISearchPick.Active
- **J2** [reviewer] Keyboard, MainUI and other origins still load the search pick while search is active, and ReanalyzeSelected is unchanged
- **J3** [reviewer] SearchPickTests gains a red-then-green test (controller origin with an active pick loads the highlight) and CHANGELOG.md Unreleased gets a Fixed line
