# VS-004 General Combat HUD

Temporary reusable combat HUD for the Stage 1 visible slice.

## Authority boundary

The package consumes only immutable injected presentation inputs:

- CS-004 `VitalState` for player health;
- MT-011 `ThrusterStatusSnapshot`;
- EN-002 `EnemyActorState` for the optional focused enemy;
- CS-004 `HitMessage` facts, where only `HitResult.Confirmed` may show hit confirmation;
- session-only room, objective, restart, reticle, reduced-effects, and restart-generation values.

`IGeneralCombatHudStateSource` and `IGeneralCombatHudHitFactSource` expose reads only. The HUD never applies damage, changes movement or thruster state, advances objectives, restarts gameplay, writes saves, grants rewards, configures weapons, or changes enemy state.

## WP-010 exclusion

WP-010 keeps exclusive ownership of the four-slot weapon strip. This package:

- does not reference, wrap, duplicate, restyle, or test `Stage1WeaponStatusStrip`;
- reserves the bottom 196 screen pixels, covering WP-010's 170 px strip, 14 px bottom margin, and separation guard;
- draws no HUD panel, reticle, hit confirmation, or restart hint inside that reservation.

## Composition

VS-007 may add `VisibleSliceGeneralCombatHud` to its owned integration scene and bind explicit read-only source implementations. No scene is modified by VS-004.

The view is immediate-mode and temporary. Critical information is repeated in text:

- numeric health plus `ACTIVE`, `CRITICAL`, or `DESTROYED`;
- thruster state plus numeric charge count;
- focused-enemy numeric health plus lifecycle state;
- `HIT CONFIRMED +`;
- explicit room, objective, restart, and reduced-effects labels.

Color is not required to interpret any critical state.

## Focused PlayMode command

```powershell
& '<PINNED_UNITY_EDITOR>' `
  -batchmode -nographics `
  -projectPath '<REPOSITORY>' `
  -runTests `
  -testPlatform PlayMode `
  -testFilter ShooterMover.Tests.PlayMode.VisibleSliceGeneralCombatHud.VisibleSliceGeneralCombatHudTests `
  -testResults 'Artifacts/TestResults/VS-004-GeneralCombatHud-PlayMode.xml' `
  -logFile 'Artifacts/Logs/VS-004-GeneralCombatHud-PlayMode.log' `
  -quit
```

## Manual proof checklist

Capture the integrated HUD at:

1. 1920x1080;
2. 1280x720 or another smaller 16:9 resolution;
3. reduced-effects enabled;
4. grayscale.

For each capture verify:

- player health, thruster, reticle, focused-enemy health, room/objective, restart, and confirmed-hit feedback are readable;
- keyboard text is readable at desk distance and controller text at controller/couch distance;
- the bottom WP-010 strip is unobstructed;
- long objective text stays bounded;
- no focused enemy shows `NO FOCUSED ENEMY / NO TARGET`;
- restart generation clears an active hit-confirmation transient.

## Pending proof

Connector-only implementation cannot launch Unity 6000.3.19f1 or produce screenshots. Keep the PR draft until the focused XML/log and required captures are attached or recorded.

## Rollback

Remove:

- `Assets/ShooterMover/UI/VisibleSliceGeneralCombatHud/`
- `Assets/ShooterMover/Tests/PlayMode/VisibleSliceGeneralCombatHud/`

Combat, movement, enemy, objective, persistence, and WP-010 behavior remain unchanged.
