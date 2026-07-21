# ENEMY-FACTORY-001 — Placement-driven enemy runtime composition

## Launch boundary

- Repository: `YeerooXY/shooter-mover`
- Exact launch `main` SHA: `208bc89be4ce34750213139c80399ea7983e70e5`
- Branch: `agent/enemy-factory-001-placement-runtime`
- Target: `main`
- Full Level 1 JSON renderer cutover is intentionally excluded.

## Composition flow

`EnemyPlacementRuntimeFactoryV1` consumes one imported `RoomEnemyPlacementContentV1` and resolves:

1. exact room object ID;
2. exact enemy definition and presentation ID;
3. level health plus a typed difficulty-scaling policy;
4. deterministic actor and enemy-participant identities from run, room-runtime, room, and placement facts;
5. a lifecycle identity derived from that actor identity and the authoritative lifecycle generation;
6. independently registered perception, movement, decision, targeting/aim, and attack-capability boundaries;
7. canonical `EnemyActorState`, `GameplayEntityIdentity`, `EnemyRuntimeProjection`, and room-occupant registration;
8. narrow attack-effect, player-damage, room-terminal, collision-terminal, XP, drop, and kill-stat ports.

No room number, prefab name, hierarchy name, or enemy-definition ID is switched on by the factory.

## Policy boundaries

The runtime deliberately keeps four behavior boundaries independent:

- **Movement policy** converts a decision into typed movement intent.
- **Movement realizer** consumes that intent with typed speed/collision configuration and an optional environment-query port. Obstacle-aware navigation, flying movement, or another realization strategy can be registered without editing ordinary enemy definitions or a central controller.
- **Decision policy** selects targets, movement, and requested attacks from immutable perception facts.
- **Targeting/aim policy** commits the locked direction and target point. Its typed context already carries difficulty and the policy configuration carries prediction horizon/distance, so predictive aiming or predictive pounces can be added as new registrations rather than enemy branches.
- **Attack capability adapter** converts the committed intent and typed descriptor into an immutable execution request. The attack-effect port is the narrow adapter point for `WeaponExecutionCore`, melee/contact presentation, or another registered effect executor.

The shipped registrations intentionally implement only direct movement and locked non-predictive aim. The advanced behaviors named above are extension seams, not hidden partial implementations.

## Lifecycle, replay, and terminal facts

- Attack operations cache an immutable signature and result.
- Exact operation replay returns the original execution request.
- Conflicting operation reuse rejects without changing cooldown state.
- Cooldown is owned by each independent runtime instance and is difficulty-scaled through typed policy output.
- Attack execution requests carry source entity, source participant, lifecycle generation, committed direction/point, resolved damage, and resolved cooldown.
- Player-impact routing rejects stale lifecycle generations before invoking the player-damage port.
- Incoming enemy damage mutates only canonical `EnemyActorState` through `EnemyActorStepper`.
- Lethal damage emits one immutable `EnemyDeathFactV1` with killer/source participant attribution.
- The same death fact is delivered once to room terminal, collision terminal, XP, drop, and kill-stat consumers. The enemy runtime does not grant XP, roll drops, mutate inventory, or own room-clear authority.
- Recomposition with a later lifecycle generation preserves actor/participant identity, restores authored health/cooldown/replay state, and invalidates old decisions and attack emissions.

## Room composition

`CreateRoom` is all-or-nothing. It rejects mixed rooms, mixed run/room-runtime contexts, duplicate derived actor identities, or any unresolved registration before producing occupancy. Required/objective occupants block room clear; optional/non-participating occupants do not.

## Focused EditMode coverage

`ShooterMover.Tests.EditMode.Enemies.EnemyPlacementRuntimeFactoryV1Tests` covers:

- ten repeated placements with distinct actor and participant identities;
- ranged, turret, pursuit/contact, and pounce fixtures through registrations;
- movement intent realization through a replaceable realizer;
- independent targeting/aim and attack-capability invocation;
- cooldown, exact replay, and conflicting operation rejection;
- independent vision and attack arcs;
- atomic failure on an unregistered capability;
- exactly-once attributed death fan-out to room, collision, XP, drop, and kill consumers;
- blocking versus optional room occupants;
- restart identity/state reconstruction and stale intent/projectile rejection;
- typed difficulty scaling;
- range-aware multi-attack selection.

## Unity proof commands

Focused EditMode suite:

```bash
"<UNITY_EDITOR>" -batchmode -nographics -projectPath . \
  -runTests -testPlatform EditMode \
  -testFilter ShooterMover.Tests.EditMode.Enemies.EnemyPlacementRuntimeFactoryV1Tests \
  -testResults artifacts/enemy-factory-001-editmode.xml \
  -logFile artifacts/enemy-factory-001-editmode.log -quit
```

Full EditMode compilation/test discovery:

```bash
"<UNITY_EDITOR>" -batchmode -nographics -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults artifacts/enemy-factory-001-all-editmode.xml \
  -logFile artifacts/enemy-factory-001-all-editmode.log -quit
```

## Known limitation

This connector-only execution environment has no repository checkout, Unity Editor, .NET SDK, or C# compiler. Source/API, assembly JSON, metadata, delimiter, stable-ID, changed-path, and forbidden enemy-switch checks are performed before publication, but no Unity XML result is claimed here. The PR remains draft for Unity validation.
