land: landed 31d2585

## Evidence
J1 — tests/Sholto.Interface.MainUI.Tests/DeckViewModelTests.cs: wc -l = 285.
J2 — tests/Sholto.Interface.MainUI.Tests/MainViewModelTests.cs: wc -l = 377.
J3 — DeckViewModel{TuneEditorCommands,ChipLevels,RingColour,TempoDisplay,AnalysisDisplay,SectionMap}Tests.cs created, 31 tests moved.
J4 — MainViewModel{Toast,TrackActions,SystemReport,FaceplateGuide}Tests.cs created, 22 tests moved.
J5 — dotnet test tests/Sholto.Interface.MainUI.Tests --list-tests: sorted lists before/after identical (800 lines, class name of the two families normalised); bodies only changed field/helper -> rig.X.
J6 — dotnet build Sholto.slnx -nologo (0 errors) && dotnet test Sholto.slnx --nologo: all projects passed (MainUI 800/800).

## Decisions
- Rig helpers EmptyAnalysis and MissingTool are instance methods (no statics rule).
- An_action_changes_nothing... went to TuneEditorCommands (calls ToggleEditor); An_idle_deck... and the not-landed test went to AnalysisDisplay (assert HasAnalysis); While_loading and Content_for_other_deck stayed (assert only own members); RefreshThemeBindings went to RingColour; Esc_with_two_modals went to TrackActions.
- Test classes joined only the new collections; none builds XAML.
