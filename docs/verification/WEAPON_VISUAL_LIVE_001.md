# WEAPON-VISUAL-LIVE-001 — Hub-selected live weapon presentation

## Current boundary

- Repository: `YeerooXY/shooter-mover`
- Unity target: `6000.3.19f1`
- Original visual repair launch SHA: `2c9d8c90b54e92bc50da2b8abd5d8fb0e5c4b07f`
- Hub-loadout cutover launch SHA: `324f3a1db786a83c4eb1cc65a6dd603ad3750f56`

## Current production path

`Stage1WeaponPresentationRepairV1` remains the Level 1 effect-presentation consumer, but it no longer owns or changes equipment selection.

- The production profile owns one holdings service containing five starter weapon instances.
- The existing Hub Inventory/Loadout screen chooses four exact equipment-instance identities.
- Confirm applies the loadout through `ProductionInventoryLoadoutAuthorityV1`, persists the new immutable route payload, and returns to Hub.
- Level 1 adopts those exact four route equipment identities through a typed composition seam.
- Keys `1` through `4` only select among the four mission-entry slots.
- Key `5`, slot-four substitution, the in-game loadout overlay, private-field reflection, and gameplay-time holdings/catalog rebuilding are removed.
- The retained visible-slice controller is disabled by the production gameplay composition, and its loadout selector object is explicitly deactivated.
- Results freezes the same Hub-selected route payload and the same holdings authority used by weapon execution.

## Presentation retained

- Every fire request goes through `InventoryBackedWeaponExecutionAdapter` and WPN-CORE-002.
- Projectile visuals use cached generated 24x12 sprites, dedicated child transforms, higher sorting order, and readable trails.
- Projectile root scale remains `Vector3.one`; visual size does not change the trigger collider.
- Colliders receive explicit per-effect radii and rigidbodies use continuous collision detection plus interpolation.
- Rocket impact/expiry emits one bounded short-lived ring presentation.
- Arc Gun consumes `ChainArcEffect`, chooses deterministic in-cone targets by distance and stable room-instance identity, applies at most primary plus three hits, and renders a short-lived line.
- Ricochet Gun uses a direct WPN-CORE projectile and a Unity collision adapter capped at two non-target wall bounces. Enemy contact remains terminal.

## Starter migration

The existing production draft identities retain their original definition meaning even after reordering:

- `equipment-instance.flow-draft-slot-1` — Blaster
- `equipment-instance.flow-draft-slot-2` — Shotgun
- `equipment-instance.flow-draft-slot-3` — Rocket Launcher
- `equipment-instance.flow-draft-slot-4` — Arc Gun

Missing starter definitions are restored as stable reserve instances, including Ricochet Gun. This keeps all five starter weapons owned after save/reload while only four are equipped.

## Required verification

1. Open the project with Unity `6000.3.19f1` and confirm zero compilation errors.
2. Run the focused Hub EditMode tests, including `ProductionPlayerLoadoutRuntimeV1Tests` and `ProductionStarterLoadoutMigrationV1Tests`.
3. Open `Assets/ShooterMover/Scenes/Bootstrap/Bootstrap.unity` and route to Hub.
4. Open Inventory and confirm all five concrete starter instances are listed.
5. Replace any equipped slot with Ricochet Gun, confirm, return to Hub, and reopen Inventory; verify exact slot and instance identity retention.
6. Enter Level 1 and verify the HUD lists the four Hub-selected names in order.
7. Press keys `1` through `4` and verify each selected exact instance executes its definition.
8. Verify key `5` performs no weapon selection and no in-game loadout UI can be opened.
9. Verify Blaster, Shotgun, Rocket Launcher, Arc Gun, and Ricochet Gun presentations when each is equipped from Hub.
10. Restart with `R`; verify the selected four identities remain unchanged while cooldown/effect state resets.
11. Complete the mission and verify Results carries the same route payload fingerprint.
12. Reload the application and verify a reordered loadout reconstructs five owned definitions and the same four equipped instance IDs.

No Unity execution result is claimed from the connector-only implementation environment.
