# PROFILE-SLOT-DELETE-FIX verification

## Launch boundary

- Repository: `YeerooXY/shooter-mover`
- Branch: `agent/profile-slot-delete-fix`
- Exact launch SHA: `d0d6f0b8f09bd777340d1d2a9466abfc3b6a3595`
- Target: `main`

## Corrected behavior

- Character-slot rendering no longer overwrites the selected slot while iterating over all six cards.
- Create and play actions receive the exact clicked slot index.
- Character-creation failure no longer reports every rejected callback as a busy route transition.
- Selecting an existing profile replaces the routed payload with that exact profile before entering the Hub.
- Occupied slots expose a two-step `DELETE PROFILE` / `CONFIRM DELETE` action.
- Deletion clears only the selected PlayerPrefs slot, preserves every other slot, and reloads Character Selection through the canonical transition coordinator.
- Invalid persisted data clears only its own corrupt slot instead of deleting all profiles.

## Focused automated proof to run

Unity version: `6000.3.19f1`

```text
Unity -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testFilter ShooterMover.Tests.PlayMode.Flow.ProductionFlow.ProductionFlowPlayModeTests -testResults Temp/profile-slot-delete-fix-playmode.xml -logFile Temp/profile-slot-delete-fix-playmode.log
```

Do not add `-quit`; `-runTests` exits Unity after the test run.

Focused additions cover:

- explicit clicked-slot identity surviving character creation when slot 6 is occupied;
- two-step deletion targeting one exact slot;
- PlayerPrefs deletion preserving a different occupied slot.

## Manual acceptance

1. Create characters in at least two non-adjacent slots.
2. Select an empty slot other than slot 6, create a character, and confirm that exact slot becomes occupied.
3. Select each existing character and confirm the Hub receives that character's exact profile/loadout payload.
4. Press `DELETE PROFILE` once and confirm nothing is deleted yet.
5. Press `BACK` and confirm the pending deletion is cancelled.
6. Press `DELETE PROFILE`, then `CONFIRM DELETE`, and confirm only that exact slot is removed.
7. Return to the screen or restart Play Mode and confirm the deleted slot remains empty while all other profiles persist.
8. Create a replacement character in the deleted slot and confirm creation succeeds without the misleading `The route transition is busy.` message.

## Verification status

Connector-side source review and ahead-only branch comparison completed. Unity compilation and XML test execution are not available in this connected environment and are not claimed.
