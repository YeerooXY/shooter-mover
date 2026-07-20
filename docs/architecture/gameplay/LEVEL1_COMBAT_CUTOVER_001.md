# LEVEL1-COMBAT-CUTOVER-001

## Purpose

Remove enemy-type-specific player-damage handlers from the retained Level 1 bridge and move the live implementation behind canonical Level 1 names.

## Ownership

`EnemyToPlayerDamageRouterV1` is the reusable admission boundary. It owns:

- registration of any enemy `CombatHit2DAdapter` with definition-derived damage;
- immutable hit-event to lifecycle-generation admission facts;
- forwarding one `PlayerDamageRequest` through `PlayerRuntimeComposition`;
- duplicate, missing-ledger, conflicting-generation, and lifecycle-clear behavior.

It contains no Level 1, turret, Mobile Blaster Droid, package, hierarchy-name, or weapon-name branch.

`EnemyProjectileDamageSourceBinderV1` is a Unity/package composition adapter. It observes existing projectile emission facts, tracks physical projectile completion, and registers every current enemy source through the same router. The existing combat-event identity format is parsed only at this compatibility boundary; the router receives typed lifecycle generation.

`Level1PlayerRuntimeAdapterV1` is the canonical live player composition. The serialized `Stage1PlayerLiveAuthorityAdapterV1` remains as a thin hidden compatibility subclass so the current scene GUID and controller reference do not break. It contains no gameplay implementation.

## Current Level 1 registration

The retained scene currently contributes two ranged sources:

- Blaster Turret;
- Mobile Blaster Droid.

Both are registered through `EnemyProjectileDamageSourceBinderV1.RegisterSource`. Adding another projectile enemy requires source registration data only; it does not require another hit handler or another player-health path.

## Deliberately deferred serialized rename

This task does not rename `Stage1VisibleSlice.unity` or replace its serialized controller GUID. That is a separate Unity serialization migration. The production player runtime implementation no longer lives in the Stage 1 compatibility component, which makes that later scene rename mechanical rather than combat-authority-sensitive.

## Required verification

- Unity `6000.3.19f1` compilation.
- Focused PlayMode tests for `EnemyToPlayerDamageRouterV1Tests`.
- Existing `Stage1PlayerLiveAuthorityPlayModeTests`.
- Manual Level 1 proof that both Mobile Blaster Droid and Blaster Turret damage the player from range through the live authority.
