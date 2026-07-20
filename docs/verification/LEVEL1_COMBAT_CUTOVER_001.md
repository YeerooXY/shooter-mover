# LEVEL1-COMBAT-CUTOVER-001 verification

## Launch

- Base: `850762ef3628d356a1856a859d32981eebfd5c56`
- Base state: `main` immediately after merged PR #237
- Branch: `agent/level1-combat-cutover-001`

## Static proof

- `Stage1PlayerLiveAuthorityAdapterV1` retains its existing file and `.meta` GUID but contains only a hidden serialized compatibility subclass.
- Runtime implementation moved to `Level1PlayerRuntimeAdapterV1`.
- No `HandleTurretHit` or `HandleDroidHit` method remains in the live player composition.
- Both current ranged enemies register through `EnemyProjectileDamageSourceBinderV1.RegisterSource`.
- `EnemyToPlayerDamageRouterV1` has no reference to Level 1, turret, Mobile Blaster Droid, weapon package names, tags, hierarchy names, or scene paths.
- Projectile, contact, and pounce admission facts use the same router.
- Branch comparison is ahead-only from the exact launch SHA.

## Focused automated coverage added

`EnemyToPlayerDamageRouterV1Tests` covers:

- three independent enemy sources through one router;
- definition/configuration-derived damage per registered source;
- contact delivery through the same admission path;
- missing emission/admission fact rejection;
- stale lifecycle clear rejection;
- conflicting lifecycle-generation rejection;
- idempotent exact source registration;
- conflicting source registration rejection.

## Unity proof required

Unity is not available in the connected execution environment, so compilation and test success are not claimed.

Run with Unity `6000.3.19f1`:

```text
Unity -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testFilter ShooterMover.Tests.PlayMode.Players.EnemyToPlayerDamageRouterV1Tests -testResults Temp/level1-combat-cutover-router.xml -logFile Temp/level1-combat-cutover-router.log
```

```text
Unity -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testFilter ShooterMover.Tests.PlayMode.VisibleSliceIntegration.Stage1PlayerLiveAuthorityPlayModeTests -testResults Temp/level1-combat-cutover-level1.xml -logFile Temp/level1-combat-cutover-level1.log
```

Do not add `-quit`; `-runTests` exits Unity after completion.

## Manual acceptance

1. Enter Level 1 from the confirmed Hub flow.
2. Stand several world units from the Mobile Blaster Droid and remain in its locked attack path.
3. Confirm its projectile travels and reduces player health exactly once.
4. Repeat for the standing Blaster Turret.
5. Evade the locked attack direction and confirm a miss causes no damage.
6. Restart while an enemy projectile is live and confirm the old projectile cannot damage the new player lifecycle.
7. Confirm HUD health, death, and restart continue to project from `PlayerActorAuthority`.
