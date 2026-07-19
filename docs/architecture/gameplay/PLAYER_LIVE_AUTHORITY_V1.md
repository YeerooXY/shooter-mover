# PLAYER-LIVE-001 — Stage 1 live player authority

## Launch point

- Base branch: `main`
- Exact launch SHA: `f00482a2a86232275517e8b992a9f290be07a152`
- Implementation branch: `agent/player-live-001-authority`
- Pull-request target: `main`

## Ownership

`PlayerActorAuthority`, created by the existing `PlayerRuntimeCompositionRoot`, is the only Stage 1 authority for:

- current and maximum health;
- accepted damage and healing operations;
- duplicate/conflicting operation handling;
- alive/dead lifecycle state;
- death facts;
- lifecycle generation;
- restart health restoration.

The retained `MovementActorLifecycle` remains the Unity movement/input projection. It does not own player health.

## Scene-local migration adapter

`Stage1PlayerLiveAuthorityAdapterV1` is installed at runtime when the existing
`Stage1VisibleSlice` scene loads. No scene or retained controller edit is required.

The adapter:

1. composes `PlayerRuntimeComposition` around the already-created Unity movement actor;
2. routes Blaster Turret and Mobile Blaster Droid projectile hit facts into
   `PlayerRuntimeComposition.ApplyDamage`;
3. replaces the player void-hazard combat port with an authority-backed
   `IVoidHazardCombatPort`;
4. exposes immutable runtime and HUD snapshots;
5. exposes healing only through the existing `PlayerHealingRequest` /
   `PlayerActorHealingResult` contract;
6. coordinates retained quick restart with player-authority restart while preserving
   the actor instance ID and advancing generation;
7. records the accepted death fact without granting rewards, XP, kills, or inventory.

The retained controller's private integer health is updated only as a compatibility
read mirror for existing debug/test readers. It is never consulted to decide damage,
healing, death, or restart outcomes.

## Idempotency

Damage event identity is supplied by the existing projectile/hazard event IDs.
`PlayerActorAuthority` applies the first matching command, returns `Duplicate` for an
exact replay, and rejects conflicting reuse of the same event ID without mutation.

Healing and restart retain their existing operation-ID replay behavior.

## Focused tests

EditMode:

`ShooterMover.Tests.EditMode.PlayerRuntime.PlayerLiveAuthorityTests`

- damage changes state once;
- exact duplicate damage is idempotent;
- conflicting duplicate damage is rejected;
- lethal replay emits one death fact;
- full-health healing returns `AcceptedNoEffect`;
- restart restores health, preserves actor identity, and advances generation;
- separate player/run-participant identities retain separate state.

PlayMode:

`ShooterMover.Tests.PlayMode.VisibleSliceIntegration.Stage1PlayerLiveAuthorityPlayModeTests`

- physical void contact damages the live authority and restart preserves identity;
- a physical turret projectile changes authority health only after contact.

Suggested Unity commands:

```bash
"$UNITY" -batchmode -nographics -quit -projectPath . \
  -runTests -testPlatform EditMode \
  -testFilter ShooterMover.Tests.EditMode.PlayerRuntime.PlayerLiveAuthorityTests \
  -testResults artifacts/test-results/PLAYER-LIVE-001-EditMode.xml

"$UNITY" -batchmode -nographics -quit -projectPath . \
  -runTests -testPlatform PlayMode \
  -testFilter ShooterMover.Tests.PlayMode.VisibleSliceIntegration.Stage1PlayerLiveAuthorityPlayModeTests \
  -testResults artifacts/test-results/PLAYER-LIVE-001-PlayMode.xml
```

## Changed-file boundary

No changes are made to:

- `Stage1VisibleSliceController.cs`;
- `Stage1VisibleSlice.unity`;
- any other scene;
- reward, XP, kill, inventory, or equipment authorities.


## Review repair

The initial polling bridge was replaced by a synchronous typed composition boundary:

- `QuickRestart` delegates to `PlayerRuntimeComposition.Restart` first. Only an
  accepted restart projects room, projectile, enemy, HUD and camera reset state.
- rapid same-frame restart calls therefore advance `0 -> 1 -> 2` without an
  Update-based catch-up race;
- the active compact HUD reads an immutable authority snapshot and preserves
  fractional health; the integer `PlayerHealth` property is compatibility-only;
- accepted death facts synchronously disable movement, real input, combat,
  targetability and outstanding player projectiles;
- turret, droid and void bindings use public typed ports rather than private-field
  reflection;
- projectile and void damage use the lifecycle generation encoded at emission,
  so stale callbacks cannot be relabelled with the current generation;
- void presentation counts increment only after accepted authority damage;
- trusted live source actor identities resolve to source run-participant identities.

`Stage1VisibleSliceController.cs` now contains only the minimal typed delegation
and downstream projection seams required to make the authority genuinely lead the
scene. The Stage 1 scene asset remains unchanged.
