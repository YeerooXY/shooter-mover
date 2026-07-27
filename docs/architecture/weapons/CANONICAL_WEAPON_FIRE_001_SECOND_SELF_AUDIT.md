# CANONICAL-WEAPON-FIRE-001 Second Self-Audit

## Scope and baseline

- Repository: `YeerooXY/shooter-mover`
- Pull request: #350
- Branch: `agent/canonical-weapon-fire-001`
- Exact refreshed `main` baseline: `67c4b756fd0cd8bd2a032e8f173c83a5f7844438`
- Review method: hostile static review of the complete branch diff, current production scene composition, canonical firing/scheduler/outbox contracts, Unity 2D physics ordering, enemy presentation, damage replay, cleanup and assembly references.
- Unity compilation, tests and gameplay were not available and are not claimed.

## Repairs made during this pass

### Final-range physics sweep

The projectile previously called `MovePosition` and resolved range expiry in the same `FixedUpdate`. Unity simulates the requested move after `FixedUpdate`, so the object could be destroyed before its final swept segment produced an enemy or wall contact.

Range expiry is now deferred until the following fixed tick. A contact from the completed physics step wins first; pending enemy-damage delivery also remains higher priority than range expiry.

### Trigger volumes are not hurt surfaces

Projectile contact previously treated every collider beneath `RoomEnemyActor2D`, including trigger volumes, as an enemy body hit. The projectile now ignores trigger colliders completely. Enemy damage requires a non-trigger collider under the bound enemy actor; non-enemy non-trigger colliders remain blocking walls.

### Physics root no longer inherits presentation scale

The initial visual renderer was attached to the projectile root and scaled through `renderer.transform`, shrinking the CircleCollider with it. The renderer now lives on a child named `Visual`; the Rigidbody2D and CircleCollider2D remain on a unit-scale root.

### Participant and inner-effect identity preservation

`CanonicalProjectileSourceIdentity2D` now projects `RunParticipantId` as well as actor, lifecycle, mount, equipment instance and definition.

The sink validates that the inner `CanonicalProjectileLaunchEffect.Identity` exactly matches the outer `InventoryWeaponEffectBatch.Identity`. The current five-argument controller binding remains source-compatible; the participant is latched from the first scheduler-authorized canonical batch and enforced for all later batches. A conflicting participant cannot rebind an already-created projectile identity projection.

## Confirmed integration blocker

### Current `main` enemies have no collision geometry

`Level1PresentationCatalog.asset` maps both production enemy presentations to `GenericRuntimePresentation.prefab`. That prefab contains only a Transform and SpriteRenderer. `RoomEnemyActor2D.Bind` attaches the canonical enemy runtime but does not add a collider or Rigidbody2D.

Therefore PR #350 can create and move visible projectiles on its current base, but the current-base enemy cannot physically generate `OnTriggerEnter2D`. The enemy-health and room-clear acceptance route is not executable until a canonical non-trigger enemy body collider lands.

Preferred resolution:

1. merge the enemy-presentation readiness lane (#351) or another reviewed presentation change that supplies the canonical non-trigger body collider;
2. refresh/rebase PR #350 without taking ownership of enemy presentation;
3. execute the complete Unity acceptance route.

PR #350 must not fabricate a weapon-owned fallback collider, infer a hurtbox from sprite bounds, or use coordinates/hierarchy names as enemy identity.

## Remaining hardening gaps

### Room-revision cleanup

Scene unload and controller destruction retire projectiles, but same-scene room presentation rebuild is not currently an explicit projectile lifecycle boundary. A projectile fired immediately before traversal could survive into the next room revision. This must be resolved or proven harmless before production acceptance; the preferred design is retirement from the authoritative room-revision event rather than position-based filtering.

### Player-defeat cleanup

The current slice has no authoritative player-defeat callback. Disable/destruction cleanup exists, but defeat-specific retirement has not been demonstrated.

### Two inherited fatal-exception catches remain on the activated path

- `AcceptedEmissionRuntimeAdapter.AdaptCanonicalProjectile` converts a broad exception during canonical launch construction into an ordinary invalid-launch rejection.
- `InventoryWeaponRuntimeComposition.DrainDueLocked` converts a broad exception during delivered-receipt commit into an ordinary retryable failure.

The earlier self-audit repaired the three broad catches in `InventoryBackedWeaponExecutionAdapter`; these two deeper shared boundaries still need the same fatal-exception policy before the PR can claim complete propagation.

### Redundant same-tick drain

The controller calls `UpdateTriggerInput`, which already drains due emissions, and then calls `Advance` for the same simulation tick. Replay/outbox guards prevent duplicate accepted presentation, but the second call can cause unnecessary same-tick retry pressure and duplicate diagnostics after a retryable sink rejection. This should be removed during hardening.

### Participant identity is execution-local

The current participant identity is deterministic for the scene-local actor lifecycle and is preserved through launch and damage. It is not yet the durable player-run participant authority that future XP, kill-stat and reward attribution should consume.

## Validation state

### Performed

- current-base production presentation catalog and prefab inspection;
- current `RoomEnemyActor2D` binding and terminal behavior inspection;
- full changed-source static review;
- Unity assembly-reference review;
- canonical scheduler/outbox replay trace;
- projectile movement/contact/damage retry trace;
- source, participant, envelope and inner-effect identity review;
- branch remains draft and unmerged.

### Not performed

- Unity import or C# compilation;
- EditMode or PlayMode test execution;
- manual projectile movement/contact observation;
- enemy health/death/room-clear observation;
- room-transition and player-defeat cleanup observation;
- performance testing.

## Updated acceptance prerequisites

Before PR #350 can leave draft:

1. a reviewed non-trigger canonical enemy body collider must be present on the refreshed base;
2. Unity must import and compile cleanly;
3. focused EditMode tests must pass;
4. max-range enemy and wall contacts must be tested;
5. trigger-only enemy child volumes must not receive damage;
6. the projectile root must remain unit scale while its child visual is scaled;
7. room transition and player defeat must retire stale presentation;
8. exact Rattler damage, terminal publication, room clear and exit unlock must be observed;
9. the two remaining fatal-exception boundaries and redundant same-tick drain must be resolved or explicitly superseded.

## Conclusion

The second pass found and repaired four projectile-level defects, but it also disproved the current PR description's implicit assumption that the base enemy is physically hittable. PR #350 is not merge-ready. Its correct status is a draft weapon vertical slice awaiting the canonical enemy collider dependency and the listed lifecycle/exception hardening work.
