# WEAPON-VISUAL-LIVE-001 — positional concurrent weapon mounts

## Current boundary

- Repository: `YeerooXY/shooter-mover`
- Unity target: `6000.3.19f1`
- Original visual repair launch SHA: `2c9d8c90b54e92bc50da2b8abd5d8fb0e5c4b07f`
- Hub-loadout cutover launch SHA: `324f3a1db786a83c4eb1cc65a6dd603ad3750f56`

## Current production path

`Stage1WeaponPresentationRepairV1` remains the Level 1 effect-presentation consumer, but it no longer owns or changes equipment selection.

- Every character/profile slot owns its own positional weapon bindings and holdings composition.
- Aggressive / Striker has two baseline mounts: Outer Left and Outer Right.
- Healer / Combat Medic has three baseline mounts: Outer Left, Center, and Outer Right.
- Defensive / Juggernaut has four baseline mounts: Outer Left, Inner Left, Inner Right, and Outer Right.
- The existing Hub Inventory/Loadout screen lists only the physical mounts configurable for the selected profile.
- Each binding contains one physical mount identity and one exact equipment-instance identity. It contains no class, skill, cooldown, timing, or ability rule.
- Unavailable compatibility positions remain explicitly unbound and do not reserve hidden equipment instances.
- Different exact equipment instances may resolve to the same weapon definition, so any owned combination is valid, including four copies of one definition on a four-mount profile.
- One exact equipment instance cannot occupy two mounts simultaneously.
- Confirm applies the bindings through `ProductionInventoryLoadoutAuthorityV1`, persists the immutable route payload, and returns to Hub.
- Level 1 converts the confirmed bindings into a currently enabled mount projection.
- One fire input executes every enabled mount concurrently. Each mount retains its own cooldown, replay state, deterministic seed, exact equipment identity, and physical muzzle origin.
- Number keys do not select a weapon in the production mounted path.
- Key `5`, slot-four substitution, the in-game loadout overlay, private-field reflection, and gameplay-time holdings/catalog rebuilding remain removed.
- The retained visible-slice controller is disabled by the production gameplay composition, and its loadout selector object is explicitly deactivated.
- Results freezes the same normalized profile payload and holdings authority used by weapon execution.

## Future timed-mount seam

The mount model deliberately keeps `ConfiguredBindings` separate from `EnabledBindings`.

For this version they are identical. A future Aggressive skill may allow a third weapon to remain configured while an accepted ability temporarily adds that mount to the enabled projection. That future ability must not rewrite the saved mount binding or move the equipment instance between positions.

## Position behavior

Physical position is authoritative rather than decorative.

- Outer mounts use wider lateral muzzle offsets.
- Inner mounts use narrower lateral muzzle offsets.
- The Healer center mount uses the centerline.
- Swapping Shotguns from outer mounts to inner mounts changes their physical projectile origins even when the equipped definitions are otherwise identical.

## Presentation retained

- Every mount fire request goes through `InventoryBackedWeaponExecutionAdapter` and WPN-CORE-002.
- Projectile visuals use cached generated 24x12 sprites, dedicated child transforms, higher sorting order, and readable trails.
- Projectile root scale remains `Vector3.one`; visual size does not change the trigger collider.
- Colliders receive explicit per-effect radii and rigidbodies use continuous collision detection plus interpolation.
- Rocket impact/expiry emits one bounded short-lived ring presentation.
- Arc Gun consumes `ChainArcEffect`, chooses deterministic in-cone targets by distance and stable room-instance identity, applies at most primary plus three hits, and renders a short-lived line.
- Ricochet Gun uses a direct WPN-CORE projectile and a Unity collision adapter capped at two non-target wall bounces. Enemy contact remains terminal.

## Starter migration

The existing production draft identities retain their original definition meaning:

- `equipment-instance.flow-draft-slot-1` — Blaster
- `equipment-instance.flow-draft-slot-2` — Shotgun
- `equipment-instance.flow-draft-slot-3` — Rocket Launcher
- `equipment-instance.flow-draft-slot-4` — Arc Gun

Missing starter definitions are restored as stable reserve instances, including Ricochet Gun. All five starter definitions remain owned even when only two or three mounts are configurable.

## Tests authored

### EditMode

- Aggressive, Healer, and Juggernaut expose baseline mount counts 2, 3, and 4.
- Aggressive normalizes to Outer Left and Outer Right while middle compatibility positions remain unbound.
- nullable/unbound route positions round-trip through the immutable fingerprinted envelope.
- Aggressive retains all five owned starter definitions while exposing only two configurable mounts.
- Healer maps to Outer Left, Center, and Outer Right.
- Juggernaut preserves ordered outer/inner/inner/outer assignments.
- inactive positions reject selection and do not reserve an exact equipment instance.

### PlayMode

- one fire command executes two mounted weapons together;
- different lateral mount offsets produce different physical effect origins;
- each mount receives a distinct derived fire-operation identity;
- exact replay does not emit the mounted effects twice;
- active-slot selection is a no-change operation in concurrent mounted mode.

## Required verification

1. Open the project with Unity `6000.3.19f1` and confirm zero compilation errors.
2. Run the focused Hub EditMode tests, including `ProductionPlayerLoadoutRuntimeV1Tests`, `ProductionStarterLoadoutMigrationV1Tests`, and `ProductionWeaponMountPolicyV1Tests`.
3. Run the focused weapon PlayMode suite including `ConcurrentMountsExecuteTogetherFromDistinctPhysicalOrigins`.
4. Create or select an Aggressive profile; verify Inventory shows only Outer Left and Outer Right and all five starter instances remain owned.
5. Enter Level 1 as Aggressive; hold fire and verify both configured mounts execute together while number keys do not change the mount set.
6. Create or select a Healer profile; verify Inventory shows Outer Left, Center, and Outer Right and all three execute together.
7. Create or select a Juggernaut profile; verify Inventory shows Outer Left, Inner Left, Inner Right, and Outer Right and all four execute together.
8. On Juggernaut, compare Shotguns on the outer pair versus the inner pair and verify projectile origins move with the physical mount assignments.
9. Equip multiple distinct instances sharing one weapon definition and verify they may fire together; verify one exact instance cannot be assigned twice.
10. Confirm a loadout, return to Hub, reopen Inventory, restart the application, and verify exact profile-local position/instance bindings persist.
11. Restart Level 1 with `R`; verify bindings remain unchanged while cooldown/effect state resets.
12. Complete the mission and verify Results carries the same normalized route payload fingerprint.

No Unity compilation, EditMode, PlayMode, XML, or manual runtime result is claimed from the connector-only implementation environment.
