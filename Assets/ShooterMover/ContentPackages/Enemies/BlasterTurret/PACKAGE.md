# Blaster Turret package

## Role

`enemy.blaster-turret` is the fixed-position ranged enemy for Stage 1. The package owns
its tuning, fixed map-facing attack cone, temporary presentation, deterministic
fire/recover policy, prefab root, authored placement adapter, and package-local combat
composition only.

## Accepted contracts consumed

- EN-001 `Stage1EnemyPackageDescriptor`: ordinary, Kinetic, Immovable, stationary
  positioning, accepted Blaster projectile, safe recovery, and line-of-fire telegraph.
- EN-002 `EnemyActorState` / `EnemyActorStepper`: authoritative health, death,
  idempotency, contact-disabled policy, and restart state.
- EN-003 enemy Unity 2D ports: zero-velocity decision projection, target intake, contact
  boundary, and fixed-step lifecycle.
- WP-003 `BlasterMachineGunPackage`: the normal one-projectile Blaster behavior module
  and immutable runtime profile.
- CB-009 `WeaponMount2DAdapter`: validated plan-to-Physics2D execution.
- WP-002 `ProjectileExecutionPlanAdapter` / `BoundedProjectile2D`: finite projectile
  spawning, owner exclusion, confirmed-hit translation, and session cleanup.
- OBJ-001 `GameplaySceneScope2D` and placed-participant registration: explicit or
  nearest-parent scope binding, authored stable identity, isolated registries, and
  deterministic duplicate rejection.

No weapon implementation, shared adapter, scene, registry output, persistence, reward,
or other enemy is modified.

## Tracking and directional eligibility

The turret captures one cardinal home facing from its authored transform. The prefab
can either remain a fixed-direction hazard or rotate toward an in-range player at a
bounded authored angular speed. It fires only when the target is inside the configured
cone around its current barrel direction. Projectiles remain non-homing and preserve
the barrel direction they had when fired.

When tracking is enabled and the player leaves range, the turret returns toward its
authored home facing at a separately configurable speed.

Moving outside the cone stops new attacks and resets pending cadence, but projectiles
already in flight remain active. Walls and solid props stop those projectiles through
ordinary Physics2D collision.

## Destroyed collision

`BlasterTurretAuthoring2D` exposes `Keep Collider When Destroyed`. Disable it for a
non-blocking wreck or enable it when the destroyed turret should remain solid cover.
Restart restores the authored collider state.

## Drag-anywhere authoring and stable identity

`BlasterTurret.prefab` includes `BlasterTurretAuthoring2D`. A level author can drag any
number of copies into a scene, choose Right/Up/Left/Down facing, and move them freely.
The component snaps each copy to its configured grid size and locks rotation to the
chosen cardinal direction in edit mode and again when play starts.

Every persistent copy must have a deliberate canonical `Authored Placed Instance Id`.
The prefab carries the template value `placed.blaster-turret-template`; duplicated
placements remain invalid until the serialized ID is changed. Runtime code never hashes,
repairs, or regenerates this value. Renaming, moving, rotating, reparenting, or changing
sibling order therefore cannot alter turret identity.

A copied duplicate ID fails closed before `BlasterTurretPackage.Configure` runs. The
OBJ-001 scope registry retains the first owner and reports the existing and attempted
locations. The rejected copy does not register for player shots or become an active
combat package.

## Scope and combat-port binding

A turret binds in this exact order:

1. serialized `Scene Scope Override`, when assigned; otherwise
2. the nearest compatible parent `GameplaySceneScope2D`.

The selected generic scope must have a co-located, configured
`BlasterTurretSceneContext2D`. That package-specific component exposes only the player
target, hit translation, and damage-routing ports needed by this enemy. The turret
self-registers with both the generic scope and the package context.

There is no `FindFirstObjectByType`, `FindObjectsByType`, tag, scene-name, object-name,
static registry, or controller fallback. Missing, cross-scene, incompatible, conflicting,
or unconfigured scopes fail closed with `LastBindingDiagnostic`. Separate gameplay
scopes own isolated registration dictionaries and independently register their own
distinct authored placements; normal duplication still requires a new deliberate ID.

## Deterministic cadence

The package supports two presentation modes:

1. A positive warning duration enters Warning before firing and retains the legacy
   color-independent warning geometry.
2. A zero warning duration fires immediately when eligible, without showing a warning.
3. Recovery must finish before another shot can begin.

Target loss, range loss, facing-cone loss, and point-blank ambiguity reset pending
cadence. Disable, death, and restart additionally cancel package-owned projectiles.

## Stationary identity

The Rigidbody2D is kinematic and FreezeAll. EN-003 receives an explicit decision source
that always returns `(0, 0)` velocity, and the package restores its configured anchor on
fixed and late updates. The temporary silhouette is a broad rectangular fixed base with
a single barrel, deliberately distinct from the mobile shooter.

## Color-independent warning

The warning is not encoded by hue alone. It combines:

- one continuous line-of-fire rail;
- four repeated perpendicular ticks; and
- the visible barrel-to-rail connection.

The same geometry remains understandable in grayscale or with color channels removed.
This is temporary presentation and expires when final art replaces it.

## Focused verification

Run with the pinned editor:

```text
"C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testFilter ShooterMover.Tests.PlayMode.Enemies.BlasterTurretPackageTests -testResults Artifacts\TestResults\BlasterTurretPackageTests.xml -logFile Artifacts\Logs\BlasterTurretPackageTests.log -quit
```

The focused fixture covers the retained descriptor, cadence, facing, bounds, and source
surface plus:

- two independently registered and restarted turret placements in one scope;
- distinct authored placements under two independent scopes;
- duplicate authored-ID rejection before the second package configures;
- explicit-scope precedence over nearest-parent binding;
- conflicting compatible scopes at the nearest parent failing closed;
- rename, transform, sibling-order, reparent, unbind, and rebind identity stability;
- malformed identity and missing-scope fail-closed behavior; and
- absence of global scene-discovery APIs in production turret authoring/context code.

## Manual proof

Create two compatible gameplay roots, each with `GameplaySceneScope2D` and a configured
`BlasterTurretSceneContext2D`.

1. Place two turrets under one root and assign distinct authored IDs. Confirm both track,
   warn, fire visible physical projectiles, take player-shot damage independently, obey
   their wreck-collider settings, and restart independently.
2. Duplicate one turret without changing its ID. Confirm the duplicate stays inactive
   and its diagnostic names duplicate identity; then assign a new ID and confirm it binds.
3. Put a turret below scope A and assign scope B explicitly. Confirm B wins. Clear the
   override and confirm the nearest compatible parent A wins.
4. Rename, move, reorder, and reparent a disabled turret, then re-enable it. Confirm its
   `ActorId` is unchanged and it registers only in the new selected scope.
5. Remove the reachable scope or its package context, and separately place two compatible
   scopes on the same nearest parent. Confirm both configurations fail closed without
   creating private authority or a global fallback.

For retained combat readability, capture normal-color and grayscale frames for idle,
warning, shot, obstruction cancellation, destroyed-passable/solid variants, and restart.

## INT-001 migration handoff

The current Stage 1 test-support composition creates `BlasterTurretSceneContext2D` and
the instantiated turret as siblings beneath `Stage1VisibleSliceController`. NORM-001
intentionally does not edit that serialized/integration-owned path. INT-001 must make a
configured `GameplaySceneScope2D` plus `BlasterTurretSceneContext2D` the turret's actual
ancestor, or assign that generic scope through the turret's serialized/runtime explicit
scope seam before calling `TryConfigureNow`.

INT-001 must also replace the prefab template ID with a deliberate unique Stage 1 placed
ID. It must not restore global discovery, hierarchy hashing, or controller-owned identity.

## Limitations

- Temporary generated line geometry is not final art.
- No tracking beam persists outside the optional warning phase.
- No homing, target prediction, burst, or area projectile is used.
- The package does not own reward profiles, mission state, the player health authority,
  final Stage 1 placement, or shared scene-scope configuration.
- A prefab template cannot safely provide a globally unique persistent placement ID;
  every scene placement must receive its own deliberate authored ID.

## Rollback

Revert the NORM-001 changes to this package's authoring component, scene context, prefab,
focused test, and this document together. No shared OBJ-001 contract, scene, registry,
save, reward, weapon, or other-enemy cleanup is required.
