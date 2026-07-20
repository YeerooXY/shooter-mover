# WPN-IDENTITY-AIM-001 — Exact weapon instances and mounted cursor convergence

## Launch boundary

- Repository: `YeerooXY/shooter-mover`
- Branch: `agent/wpn-identity-aim-001`
- Target: `main`
- Exact launch SHA: `9057df98cdb44c426328de0cf2f62a1dc399502f`
- Launch state: current main immediately after merged PR #241
- Unity baseline: `6000.3.19f1`

## Exact equipment rule

The loadout is keyed by concrete equipment-instance identity.

Two items may share one weapon definition and still occupy different mounts:

```text
Outer Left  -> equipment-instance.blaster-plain
Outer Right -> equipment-instance.blaster-augmented
```

This is valid because the instance IDs are different. Their item level, quality,
augment collection, fingerprint, cooldown state, and physical mount position remain
attached to the individual instance.

Assigning one concrete item to two mounts remains invalid:

```text
Outer Left  -> equipment-instance.blaster-augmented
Outer Right -> equipment-instance.blaster-augmented
```

Weapon-definition uniqueness is not required. Exact equipment-instance uniqueness is.
Swapping two weapons changes only the mount bindings; it does not clone, reinterpret,
or replace either item.

## Identity reconstruction defect

The starter runtime can reconstruct the five known starter instances from its compact
route payload. Historically, an unknown route instance could fall through to a
slot-index starter definition. That could reinterpret an unknown Rocket in slot one as
a Blaster, or an unknown Shotgun in slot four as an Arc Gun.

The live Hub composition now prevents that behavior:

- exact profile runtimes are cached per character for the active application session;
- profile switching reuses the exact holdings authority, equipment instances, augments,
  and loadout authority when the route bindings match;
- a fresh starter-only reconstruction is allowed only when every bound instance is one
  of the known starter identities;
- an unknown instance without an authoritative holdings runtime fails closed instead of
  being inferred from its slot position.

This change does not add full disk persistence for arbitrary reward/crafted holdings.
That persistence boundary still needs to store the complete authoritative equipment
instances, not only route bindings. Until then, a cold reload with an arbitrary unknown
instance is rejected rather than silently converted into another weapon.

## Mounted aiming defect

The previous mounted path shifted each muzzle sideways but forwarded one direction
calculated from the actor centre. The projectiles therefore travelled in parallel and
could pass on either side of a target underneath the cursor.

The live Stage 1 input path now preserves the locked world-space cursor point. For each
enabled mount it derives:

```text
mount origin    = actor origin + perpendicular * live lateral offset
mount direction = normalize(cursor target - mount origin)
```

Every mount retains a distinct physical muzzle and operation identity, while all
unspread centre-lines converge on the same target point. Weapon-specific spread is
applied afterward by WPN-CORE.

The live presentation scale reduces current outer mount spacing from `0.9` to `0.45`
and inner spacing from `0.3` to `0.15` without changing persisted mount identities.

## Inventory presentation

Physical mount cards now show:

- the real equipment display name;
- augment count;
- shortened exact equipment-instance identity.

Owned equipment cards show the same augment count and exact-instance suffix. Two
Blasters with different augments are therefore visibly distinguishable before and
after equipping.

## Authored regression coverage

### EditMode — `ProductionExactWeaponInstanceLoadoutTests`

The fixture owns exactly four items:

- one unaugmented Blaster;
- one augmented Blaster sharing the same definition;
- one Shotgun;
- one Rocket Launcher.

It verifies:

- plain Blaster + augmented Blaster;
- plain Blaster + Rocket Launcher;
- plain Blaster + Shotgun;
- augmented Blaster + Shotgun;
- augmented Blaster + Rocket Launcher;
- exact left/right position preservation;
- swapping the two Blaster instances;
- augment and fingerprint identity remaining attached to the correct instance;
- rejection when one concrete instance is assigned to both mounts.

### PlayMode — `MountedMuzzlesConvergeOnOneLockedTargetPoint`

It verifies:

- two distinct mounted definitions execute together;
- outer live origins are `-0.45` and `+0.45` laterally;
- the two direction vectors are different and converge on the same locked target;
- both physical projectiles move toward that target after fixed updates;
- exact operation replay emits no duplicate effects.

## Required Unity proof

Unity is unavailable in the connected authoring environment. No compilation or test
pass is claimed. Run:

```text
Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testFilter ShooterMover.Tests.EditMode.Flow.Hub.ProductionExactWeaponInstanceLoadoutTests -testResults Temp/wpn-identity-aim-001-editmode.xml -logFile Temp/wpn-identity-aim-001-editmode.log
```

```text
Unity -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testFilter ShooterMover.Tests.PlayMode.Weapons.Live.InventoryWeaponRuntimePlayModeTests -testResults Temp/wpn-identity-aim-001-playmode.xml -logFile Temp/wpn-identity-aim-001-playmode.log
```

Do not add `-quit`; `-runTests` exits Unity after the test run.

## Manual acceptance

1. Supply two distinct Blaster instances, with an augment on only one of them.
2. Equip both on a Striker and confirm both mount cards show the correct augment state.
3. Swap Outer Left and Outer Right; confirm only the positions change.
4. Try assigning the augmented Blaster instance to both mounts; confirm rejection.
5. Equip either Blaster with the Shotgun, then with the Rocket Launcher.
6. Enter Level 1 and aim directly at the moving droid from several world units away.
7. Confirm both muzzle centre-lines converge on the cursor instead of passing beside it.
8. Confirm Shotgun spread remains centred around its converged mount direction.
9. Confirm Rocket, Arc, Ricochet, replay, cooldown, and restart behavior remain distinct.
10. Switch profile slots and return; confirm exact instances and augment state remain intact during the session.

Draft only. Do not merge automatically.
