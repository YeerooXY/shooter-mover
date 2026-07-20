# LEVEL1-ENEMY-DAMAGE-CUTOVER-001

## Launch boundary

- Repository: `YeerooXY/shooter-mover`
- Original exact base: `850762ef3628d356a1856a859d32981eebfd5c56`
- Original branch: `agent/level1-enemy-damage-cutover-001`
- Canonical-route consolidation base: `8b4d4e401827735d10082f6f3b04a5d1b2103920`
- Canonical-route consolidation branch: `agent/cleanup-player-routing-002`
- Target: `main`

## Purpose

Replace the retained Level 1 player bridge's turret-specific and Mobile Blaster Droid-specific projectile handlers with one reusable many-source enemy projectile impact route.

The consolidation follow-up also removes the overlapping `EnemyToPlayerDamageRouterV1` implementation so the repository has one canonical Level 1 enemy-projectile-to-player route.

This work does not change Hub loadout IDs, weapon names, equipment persistence, player weapon execution, projectile presentation, weapon catalogs, or the PR #237 travel repair.

## Previous coupling

`Stage1PlayerLiveAuthorityAdapterV1` previously owned separate fields and callbacks for exactly two enemy types:

- turret hit adapter and projectile adapter;
- Mobile Blaster Droid hit adapter and projectile adapter;
- `HandleTurretHit` with a controller-owned damage constant;
- `HandleDroidHit` with a Blaster package damage constant.

It also decoded the player lifecycle generation from text tokens embedded inside `StableId` values. A valid physical hit was silently ignored when the event ID did not match that parser's expected spelling.

Adding another ranged enemy would have required editing the player bridge and adding another enemy-specific callback.

## Shared route

`EnemyProjectileImpactRouter2D` is package-neutral and reusable.

Each enemy source registers one immutable `EnemyProjectileDamageBinding2D` containing:

- exact source actor identity;
- its existing `CombatHit2DAdapter`;
- its existing `ProjectileExecutionPlanAdapter`;
- configured damage.

The router then applies one path for every registered source:

1. observe the immutable projectile emission;
2. capture the target player's lifecycle generation directly from the supplied generation reader;
3. ledger the exact physical hit event and projectile instance;
4. consume only a confirmed translation from the registered hit adapter;
5. verify exact source identity and target identity;
6. emit one immutable `EnemyProjectileImpactFact2D`;
7. clear pending emission facts on projectile completion, restart, or disposal.

No lifecycle data is decoded from IDs.

## Player authority boundary

The router does not reference `PlayerRuntimeComposition` or `PlayerActorAuthority`.

The Level 1 scene adapter consumes the immutable impact and creates the existing `PlayerDamageRequest`. Damage, duplicate admission, conflicting replay, stale lifecycle rejection, death, and restart remain owned by the existing player authority.

Trusted participant attribution remains resolved from the exact source actor identity registered by the Level 1 composition. Client-supplied participant claims remain unused.

## Canonical production path

The runtime implementation has one canonical production path and name:

- `Assets/ShooterMover/Production/Level1/Level1PlayerRuntimeSceneAdapterV1.cs`;
- `ShooterMover.UnityAdapters.Production.Level1.Level1PlayerRuntimeSceneAdapterV1`;
- `Assets/ShooterMover/ContentPackages/Weapons/Shared/Runtime/EnemyProjectileImpactRouter2D.cs`.

The historical component remains temporarily as a zero-logic runtime compatibility subclass:

- `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1PlayerLiveAuthorityAdapterV1.cs`;
- `Stage1PlayerLiveAuthorityAdapterV1`.

The Stage 1 scene does not serialize this compatibility component. `Stage1VisibleSliceController` adds it dynamically and still exposes the historical concrete type to callers and tests. The shell therefore remains only until that controller API is migrated; it contains no player state, enemy registration, projectile ledger, damage routing, HUD routing, restart logic, or lifecycle parsing.

The overlapping route introduced by `LEVEL1-COMBAT-CUTOVER-001` was removed during canonical-route consolidation:

- `Level1PlayerRuntimeAdapterV1`;
- `EnemyProjectileDamageSourceBinderV1`;
- `EnemyToPlayerDamageRouterV1`;
- their focused test and superseded architecture/verification documents.

## Serialized scene-root migration deliberately isolated

The broader rename remains a separate migration:

- `Stage1VisibleSliceController`;
- `Stage1VisibleSlice.unity`;
- Visible Slice camera, HUD, room-presentation, and loadout-presentation names;
- `Stage1PlayableLoopCompositionV1` and its direct controller dependency;
- the reflection-based legacy-loop retirement seam.

That migration must preserve the actual serialized controller GUID, update build settings and scene paths, remove the production dependency on `ShooterMover.TestSupport.*`, and pass an Editor scene-open plus Bootstrap-to-Level-1 smoke run. Moving that large serialized root inside the combat fix would mix behavior change, source ownership, scene routing, and serialization risk in one unprovable patch.

## Runtime registrations in this version

Level 1 registers:

- Mobile Blaster Droid;
- Blaster Turret.

Both use the same router. Registering a future ranged enemy requires supplying another binding; no router or player-authority modification is required.

## Regression coverage

`EnemyProjectileDamageRoutingPlayModeTests` proves:

- the runtime compatibility component is a subclass of the canonical production implementation;
- the canonical implementation owns one shared router;
- no `HandleTurretHit` callback remains;
- no `HandleDroidHit` callback remains;
- no lifecycle-generation ID parser remains;
- exactly two live enemy sources are registered in the current Level 1 scene;
- a real Mobile Blaster Droid impact carries its exact source identity;
- the impact targets the exact live player actor;
- configured damage remains 10;
- target lifecycle generation is captured as a typed value;
- the confirmed impact reaches the live player authority and lowers health.

Existing PLAYER-LIVE PlayMode coverage continues to prove damage replay, conflicting duplicates, death, restart, stale lifecycle rejection, HUD projection, turret contact, and physical void damage.

## Required Unity verification

Unity execution is required before readiness:

```text
Unity -batchmode -nographics -projectPath . \
  -runTests -testPlatform PlayMode \
  -testFilter ShooterMover.Tests.PlayMode.VisibleSliceIntegration \
  -testResults Temp/level1-enemy-damage-cutover-playmode.xml \
  -logFile Temp/level1-enemy-damage-cutover-playmode.log
```

Do not add `-quit`; `-runTests` exits Unity after the test run.

Manual acceptance:

1. Enter Level 1 through the production Bootstrap route.
2. Stand several world units from the Mobile Blaster Droid.
3. Let one droid projectile travel into the player.
4. Confirm health decreases by the configured amount exactly once.
5. Enter the turret room and confirm turret projectile damage follows the same authority behavior.
6. Restart while enemy projectiles are active and confirm stale impacts do not affect the new player generation.
7. Confirm Hub weapon IDs, equipped names, mounts, and PR #237 player-projectile travel are unchanged by this branch.
