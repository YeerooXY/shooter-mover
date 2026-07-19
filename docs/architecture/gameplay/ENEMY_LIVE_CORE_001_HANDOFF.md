# ENEMY-LIVE-CORE-001 — Mobile Blaster Droid live decision handoff

Starting point: `b2bf4348ab6f827a737add53278d57568684f552` (`origin/main`, merge of PR #217).

## Ownership after this change

- `EnemyActorState` and `EnemyActorStepper` remain the only health, damage replay, terminal lifecycle, and encounter-resolution authority.
- `MobileBlasterDroidRuntime2D` gathers Unity facts only: actor/target position, transform-facing, `Physics2D` line-of-sight, and the current fixed-step/cadence phase.
- `EnemyPerceptionBuilder` creates the immutable live perception snapshot.
- `EnemyDecisionPolicy` selects movement and may emit `EnemyAttackIntent`.
- The droid package converts only an accepted intent into its existing Blaster Machine Gun execution plan. It never writes player health directly.
- `EnemyTarget2DAdapter` remains the droid hit target and submits damage to the existing actor authority.
- `EnemyRuntimeProjection.BlocksRoomClear` remains definition-role plus canonical actor-state projection. No object name participates in room-clear classification.

## Definition-driven live tuning

`MobileBlasterDroidDefinition` now owns the live values for:

- detection radius;
- vision arc;
- attack arc;
- minimum, preferred, and maximum attack range;
- preferred movement distance and positioning tolerance;
- movement speed;
- wind-up and recovery cadence.

The existing runtime factory overload remains compatible and supplies the package defaults. A longer overload exists for focused runtime tests and future authoring tools. Existing assets require no scene or prefab change because the new serialized fields have package defaults.

## Intent and debug surfaces

`MobileBlasterDroidRuntime2D` exposes:

- `LastDecisionEvaluation`;
- `LiveDebugSnapshot`, which is the exact `EnemyDecisionEvaluation.Debug` instance and is never recomputed;
- `LastAcceptedAttackIntent`;
- `TryDequeueAttackIntent(out EnemyAttackIntent)` for ordered, exactly-once observation by an integration owner;
- `LastDestroyedNotification` from the canonical actor step result;
- `CurrentRuntimeProjection` and `BlocksRoomClear`.

The internal projectile path remains active so the package stays functional before Stage 1 is patched. Consumers must not execute a second projectile from the dequeued intent while this internal execution remains enabled.

## Exact Stage 1 integration patch

`Stage1VisibleSliceController` was intentionally not edited. A later narrow controller patch should:

1. Keep the concrete `MobileBlasterDroidRuntime2D` reference created by the existing enemy composition path. Do not identify it by `GameObject.name`.
2. After the droid's authoritative fixed-step boundary, drain `TryDequeueAttackIntent` and record each `EnemyAttackIntent.DecisionId` in the run-scoped replay-safe operation ledger. Treat the intent as an observation while package-local execution remains enabled.
3. When attack execution is centralized, move only the existing Blaster plan dispatch behind the Stage 1 intent consumer, then disable package-local dispatch in the same patch. Never execute both paths for one decision ID.
4. When `LastDestroyedNotification` becomes non-null, resolve `SourceEntityId` to `SourceRunParticipantId` through the authoritative run participant registry and create `EnemyAttributedDeathFact`. The enemy package must not infer participant attribution from a client or from a GameObject.
5. Read `CurrentRuntimeProjection.BlocksRoomClear` for room completion. A required droid blocks only while its canonical actor state is active; destruction immediately clears the projection.
6. On restart, clear the controller's consumed decision/death-operation ledger for the old lifecycle generation and use the new `Generation`. Stable actor ID and lifecycle generation remain separate.

No XP, drops, money, kill count, or player-health change belongs in this patch.

## Verification status

Focused EditMode/PlayMode tests were authored for policy usage, detection, attack arc, LOS, cadence, intent acceptance, damage/destruction, terminal stop, restart, room-clear projection, and exact debug equality.

Unity 6000.3.19f1 was not available in the execution environment, so no Unity compilation or test result is claimed here. The PR must remain draft until the listed filters are run in the shared project checkout.

Suggested commands:

```text
Unity -batchmode -projectPath <project> -runTests -testPlatform EditMode -testFilter ShooterMover.Tests.EditMode.Enemies.EnemyRuntimeFoundationTests -testResults <temp>/enemy-foundation.xml -logFile <temp>/enemy-foundation.log
Unity -batchmode -projectPath <project> -runTests -testPlatform PlayMode -testFilter ShooterMover.Tests.PlayMode.Enemies.MobileBlasterDroidPackageTests -testResults <temp>/mobile-droid-package.xml -logFile <temp>/mobile-droid-package.log
Unity -batchmode -projectPath <project> -runTests -testPlatform PlayMode -testFilter ShooterMover.Tests.PlayMode.Enemies.MobileBlasterDroidLiveDecisionTests -testResults <temp>/mobile-droid-live.xml -logFile <temp>/mobile-droid-live.log
```
