# MT-013 Movement Playground

## Purpose

`MovementPlayground.unity` is a test-only manual and PlayMode verification scene for the accepted Stage 1 movement stack. It composes the merged MT-007 movement input adapter and MT-010 movement lifecycle without changing either dependency or introducing production scene authority.

The playground is deliberately disposable. It is not a production level, Bootstrap composition root, build-settings entry, tuning asset, content package, camera-bounds system, or gameplay acceptance scene.

## Scene composition

The scene contains exactly:

- one explicit test player at the origin;
- one dynamic `Rigidbody2D` with gravity disabled, continuous collision detection, interpolation, and frozen rotation;
- the existing `PlayerMovementIntentAdapter`, `MovementContact2DAdapter`, and `MovementActorLifecycle` on that same player object;
- the accepted MT-007 `ShooterMoverMovement.inputactions` asset referenced directly by the test harness;
- four non-trigger `BoxCollider2D` walls, each exposing the explicit MT-009 wall-contact contract;
- one orthographic camera that follows the player without clamping;
- reversible runtime-only placeholder sprite rendering for the player, thruster indicator, and walls.

The harness owns one fixed test tuning profile in code. It creates no ScriptableObject, content definition, registry entry, save data, prefab, or persistent tuning asset.

## Room and camera dimensions

The camera has an orthographic size of `5.4`. At the reference 16:9 viewport this gives an approximately `19.2 x 10.8` world-unit view.

The room interior is `48 x 27` world units, or approximately `2.5` camera-view extents on each axis. This is only a roomy movement-test envelope; it does not claim final level scale or camera-bound behavior.

## Controls

The playground uses the existing MT-007 bindings without modification:

| Action | Keyboard and mouse | Gamepad |
|---|---|---|
| Move | W, A, S, D | Left stick |
| Aim input | Mouse delta | Right stick |
| Thruster | Space | South button |

Aim input is accepted by the existing adapter but this movement-only scene does not add weapons, aiming presentation, or combat behavior.

## Authority boundaries

`MovementActorLifecycle` remains the sole movement-driving `FixedUpdate` owner. The harness does not implement `FixedUpdate`, does not write `Rigidbody2D.linearVelocity`, and does not instantiate `MovementFixedStepDriver`.

For focused tests, `MovementPlaygroundHarness.StepForTest` delegates directly to the public MT-010 deterministic `ExecuteFixedStep` seam. Normal manual play continues through the lifecycle's one existing Unity callback.

The harness may reset the test player's transform during restart/re-entry setup, but final velocity is always produced or cleared by the accepted MT-010/MT-008 path.

## Automated PlayMode proof

Run the focused fixture:

`ShooterMover.Tests.PlayMode.Movement.MovementPlaygroundTests`

The fixture proves:

1. serialized scene load, one explicit player, one lifecycle, one camera, four walls, and a room measuring roughly two to three reference views;
2. MT-007 keyboard input produces movement through MT-010 and the accepted `Rigidbody2D` projection;
3. thruster input consumes exactly one charge, enters a non-ready burst phase, and activates the test indicator;
4. the orthographic camera remains centered on the player;
5. restart clears velocity and returns the player to the canonical test spawn;
6. repeated harness disable/re-entry stops and restarts the same lifecycle without duplicate bodies, cameras, placeholder assets, movement lifecycles, or drivers;
7. the only final player velocity writer is the single accepted `MovementActorLifecycle`, with zero `MovementFixedStepDriver` components and no harness `FixedUpdate` method.

## Manual playable check

Use Unity `6000.3.19f1` and perform the following review:

1. Open `Assets/ShooterMover/Tests/PlayMode/Movement/Scenes/MovementPlayground.unity` directly. Do not add it to build settings.
2. Enter Play Mode and confirm there is one cyan placeholder player, four grey room walls, and one camera.
3. Move with WASD or the gamepad left stick. Confirm acceleration, braking, and directional response are readable.
4. Press Space or the gamepad south button while moving. Confirm the orange thruster indicator appears and the burst is visibly faster than base movement.
5. Move in several directions and confirm the player remains centered while the room moves through the camera view.
6. Contact each wall during ordinary movement and during thruster exit. Confirm the player remains bounded and no duplicate player appears.
7. Stop Play Mode, enter again, and repeat movement plus thruster input. Confirm the player starts at the origin with zero retained velocity or held-input activation.
8. During Play Mode, disable and re-enable the `Movement Playground` harness component once. Confirm velocity clears, the same player is reused, and input resumes only after a fresh neutral boundary.

Record the focused PlayMode test log and a brief pass/fail note or capture for movement, thruster, camera centering, wall contact, and clean stop/re-entry in the MT-013 pull request before moving the draft to review-ready.

## Non-goals

This task intentionally adds no:

- Bootstrap or build-settings change;
- production scene or final level layout;
- shared input, movement, physics, contact, or camera adapter change;
- combat, weapon, enemy, encounter, HUD, persistence, save, registry, analytics, or networking behavior;
- content package, user-supplied art, final sprite, audio, particles, or production presentation;
- camera clamp, dead zone, smoothing policy, or final camera system.

## Rollback

Remove the MT-013 scene, harness, focused PlayMode test, this document, and their paired Unity metadata. No production scene, shared runtime, content registry, save schema, build setting, or Bootstrap repair is required.
