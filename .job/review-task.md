### Spec Compliance
- ✅ Spec compliant (J1-J5 verified from diff; J6 per report)
- J1 ✅ DeckViewModelTests.cs 285 lines. J2 ✅ MainViewModelTests.cs 377 lines.
- J3 ✅ six DeckViewModel*Tests.cs files exist, each with a [Collection]. J4 ✅ four MainViewModel*Tests.cs files exist.
- J5 ✅ Deck: 50 [Fact]/[Theory] before, 50 across split files, method names identical. Main: 47 before; the 47 names match after (the 4 extras in a glob are pre-existing MainViewModelCollaboratorTypesTests.cs). Diff of test bodies shows only field/helper -> rig.X changes.
- ⚠️ J6 build/test pass not re-run (per brief); relies on report (MainUI 800/800).
### Strengths
- Shared setup moved to a rig (DeckViewModelRig.cs, MainViewModelRig.cs) as instance members, no statics.
- Collections keep the former serial execution (DeckViewModelCollection.cs), preserving behaviour.
### Issues
#### Critical
None.
#### Important
None.
#### Minor
- Split files copy the full using block (e.g. MainViewModelToastTests.cs:1-14); likely unused usings.
### Assessment
**Task quality:** Approved
**Reasoning:** Line limits, file placement and test set are met, with bodies unchanged apart from rig access.
