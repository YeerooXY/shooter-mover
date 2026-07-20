# WEAPON-VISUAL-LIVE-001 — Live five-weapon presentation repair

## Launch boundary

- Repository: `YeerooXY/shooter-mover`
- Base: `main`
- Exact launch SHA: `2c9d8c90b54e92bc50da2b8abd5d8fb0e5c4b07f`
- Branch: `agent/weapon-visual-live-001-five-weapon-repair`
- Unity target: `6000.3.19f1`
- Dependency: merge or otherwise apply the assembly-boundary repair from draft PR #230 before Unity verification.

## Root causes

The DEMO-CUTOVER-001 integration replaced the retained Stage 1 projectile presentation with a runtime-created one-pixel sprite, then scaled the complete projectile object. Scaling the root also scaled its already tiny trigger collider. Fast pellets could therefore be difficult to see and unusually easy to miss physically.

The cutover catalog also exposed Blaster, Shotgun, Rocket Launcher, and Flamethrower only. The approved Arc Gun and Ricochet Gun packages were not represented in the live selection surface. `ChainArcEffect` had no live target/damage projection in the cutover, and no live ricochet collision adapter existed.

## Repair

`Stage1WeaponPresentationRepairV1` is installed only for the existing Stage 1 visible-slice scene.

- Blaster, Shotgun, Rocket Launcher, Arc Gun, and Ricochet Gun are available through the pre-arena loadout selector.
- The loadout is confirmed before entering the arena and remains immutable during combat. There are no in-arena weapon-switch keys.
- Every fire request still goes through `InventoryBackedWeaponExecutionAdapter` and WPN-CORE-002.
- Projectile visuals use generated 24x12 sprites, dedicated child transforms, higher sorting order, and readable trails.
- Projectile root scale remains `Vector3.one`; visual size no longer changes the trigger collider.
- Colliders receive explicit per-effect radii and rigidbodies use continuous collision detection plus interpolation.
- Rocket impact/expiry emits a bounded short-lived ring presentation. The existing one authoritative area-damage application remains unchanged.
- Arc Gun consumes `ChainArcEffect`, chooses deterministic in-cone targets by distance and stable room-instance identity, applies at most primary plus three hits, and renders a short-lived line.
- Ricochet Gun uses a direct WPN-CORE projectile and a Unity collision adapter capped at two non-target wall bounces. Enemy contact remains terminal and does not ricochet.

## Ownership and limitations

This is a transitional Stage 1 integration repair, not a new inventory, equipment, player-health, damage, room, or reward authority. It uses reflection only to bridge private fields in the already transitional DEMO-CUTOVER composition. The follow-up architectural cleanup should expose typed composition ports and remove this reflection seam.

Because the production route model intentionally has exactly four weapon slots, the fifth showcase weapon is an alternate for slot four in this scene. A future inventory/loadout screen should decide which four of the five are equipped rather than retaining the keyboard alternate.

## Required verification

1. Merge/apply PR #230 and open with Unity `6000.3.19f1`.
2. Confirm zero compilation errors.
3. Open `Assets/ShooterMover/Scenes/Bootstrap/Bootstrap.unity` and enter Play Mode.
4. Route to Level 1.
5. Press `1`; confirm the Blaster emits visible blue bolts and damages enemies consistently.
6. Press `2`; confirm seven visible shotgun pellets spread and hit with readable collision.
7. Press `3`; confirm a visible rocket, trail, one damage event, and one short explosion ring.
8. Press `4`; confirm Arc Gun draws an energized line and damages no more than primary plus three targets.
9. Press `5`; confirm Ricochet Gun visibly reflects from walls no more than twice and terminates on enemy contact.
10. Restart with `R`; confirm firing still works under the new lifecycle generation.
11. Repeat restart and firing; confirm no duplicate award, damage, or scene authority appears.

No Unity execution result is claimed from the connector-only environment.
