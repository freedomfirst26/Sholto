# Definition of done: job c241-beat-jump-size-shift

Frozen by `$T brief` at 2026-11-12T09:28. A changed outcome is a new job.

## Job items
- **J1** [reviewer] A BEAT JUMP pad note (0x20-0x27) on a deck's Shift pad channel (9 for deck 1, 11 for deck 2) is translated as a shifted BEAT JUMP pad press for that deck and pad, and its release means nothing
- **J2** [reviewer] Shift + BEAT JUMP pad 7 sends HalveBeatJump and Shift + pad 8 sends DoubleBeatJump for that deck (also when Shift is held and the plain pad note arrives), and Shift + pads 1-6 send no jump and no size change
- **J3** [reviewer] After HalveBeatJump on a deck every plain BEAT JUMP pad on that deck moves the playhead half as far as before, after DoubleBeatJump twice as far, and the other deck's jumps are unchanged
- **J4** [reviewer] The jump scale stays within BeatJumpOptions MinScale 0.125 and MaxScale 8 (further halving or doubling past a bound leaves it unchanged), starts at 1 so pads jump -1 +1 -2 +2 -4 +4 -8 +8 beats as today, and a test pins those option defaults
- **J5** [reviewer] New gesture ids for Shift + pad 7 and Shift + pad 8 are in GestureIds, GestureCatalog and the FLX4 guide json so GestureIdsTests and FaceplateDocTests pass
- **J6** [reviewer] dotnet build Sholto.slnx -nologo && dotnet test Sholto.slnx --nologo passes
