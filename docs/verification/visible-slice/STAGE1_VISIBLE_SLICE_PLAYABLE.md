# Stage 1 Visible Slice — Playable Prototype

Task: `VS-007`

Open `Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity` in Unity
`6000.3.19f1` and press Play. This scene is intentionally not added to build
settings: VS-007 owns prototype composition, not the production boot route.

## What is on screen

- the accepted VS-002 dark industrial room with the replacement floor, door,
  crate, and explosive sprites from the VS-001 art refresh;
- the accepted stationary EN-007 Blaster Turret authority with VS-003's clean
  transparent turret presentation and redundant warning state;
- the accepted VS-004 general HUD, VS-005 fixed-loadout selector, unmodified
  WP-010 four-slot strip, and VS-006 orthographic camera rig;
- one MT-010 movement actor and session-only player vital composed inside the
  VS-007 TestSupport boundary.

## Controls

| Action | Keyboard and mouse | Controller |
| --- | --- | --- |
| Choose loadout | Arrow keys | D-pad / shoulders |
| Confirm loadout | Enter or Space | South button |
| Move | WASD | Left stick |
| Thruster burst | Left Shift | Right shoulder |
| Aim | Mouse | Right stick |
| Fire | Left mouse button | Right trigger |
| Quick restart | R | TestSupport API for now |
| Reduced effects | F2 | — |
| Grayscale proof | F3 | — |

The temporary player fire seam submits damage through EN-002's accepted enemy
authority. The turret runs the accepted EN-007 warning/fire/recovery cadence and
projectile execution; VS-007 owns only the disposable session-health response.

## Expected route

1. The loadout selector opens before room entry. Confirm one of WP-008's fixed
   four-slot fixtures.
2. Move around the room and use the thruster. The camera follows without
   exposing outside the room presentation.
3. The turret cycles through warning, fire, and recovery. The HUD reports player
   and focused-enemy health.
4. Fire five times to reduce the turret's accepted 30 HP to zero. The HUD changes
   the objective to `ROOM CLEAR` and the turret presentation shows destroyed.
5. Press `R`. Health, loadout selection, turret cadence/health, projectiles,
   transient hit cues, and camera history reset without duplicating owners.

## Focused verification

Run the repository's no-`-quit` Unity test route with this fixture:

```text
ShooterMover.Tests.PlayMode.VisibleSliceIntegration.Stage1VisibleSliceIntegrationTests
```

The focused suite covers scene composition, loadout confirmation, accepted
enemy-health damage and destruction, reduced-effects/grayscale projection, a
no-durable-state audit, and fifty consecutive restarts. The restart test emits
one summary containing object, callback, projectile, enemy, HUD, camera,
selected-loadout, session-health, and generation counts.

Reference evidence should be captured at 1920×1080 in three modes:

1. default during turret warning;
2. reduced effects (`F2`) during the same readable warning;
3. grayscale (`F3`) with warning shape, glyph, count, and timing still legible.

## Ownership boundary

The scene does not edit build settings, registries, saves, inventory, rewards,
unlocks, mission persistence, or `MissionRunState`. Deleting the VS-007 scene,
TestSupport folder, integration-test folder, and this document removes the
entire composition while leaving every accepted component package intact.
