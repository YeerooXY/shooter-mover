# ENEMY-FACTORY-001 — Placement-driven enemy runtime composition

## Launch boundary

- Repository: `YeerooXY/shooter-mover`
- Exact launch `main` SHA: `208bc89be4ce34750213139c80399ea7983e70e5`
- Branch: `agent/enemy-factory-001-placement-runtime`
- Target: `main`
- Full Level 1 JSON renderer cutover is intentionally excluded.
- Combat Hit Policy remains a separate authority; this runtime exposes only narrow future integration ports.

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

The runtime keeps four behavior boundaries independent:

- **Movement policy** converts a decision into typed movement intent.
- **Movement realizer** consumes that intent with typed speed/collision configuration and an optional environment-query port.
- **Decision policy** selects targets, movement, and requested attacks from immutable perception facts.
- **Targeting/aim policy** commits direction and target point from an internally reconstructed context. Callers cannot substitute perception or difficulty after decision issuance. Predictive aim can consume richer authoritative perception later without changing that ownership rule.
- **Attack capability adapter** converts the committed intent and typed descriptor into an immutable execution request. The attack-effect port remains the narrow adapter point for later combat-hit integration.

The shipped registrations intentionally implement direct movement and locked non-predictive aim. Advanced movement or predictive aim remain replaceable registrations rather than enemy-type branches.

## Issued-decision authority

Every successful `Evaluate` call creates an immutable decision projection and records a deterministic SHA-256 fingerprint inside that runtime and lifecycle. The fingerprint includes:

- enemy identity and lifecycle generation;
- simulation tick, observer position, and observer facing;
- every perceived target identity, faction, relationship, position, velocity, distance, direction, detection membership, vision membership, and LOS fact;
- selected target and attack;
- requested movement and attack intent;
- committed origin, direction, and target point;
- execution-relevant decision and debug fields.

Target collections are canonicalized before hashing. `RealizeMovement` and `TryExecuteAttack` accept only a byte-equivalent immutable projection whose fingerprint was issued by that runtime. Reference equality is not required, but fabricated, foreign, stale, or altered decisions reject.

## Accepted-execution authority

A successful first attack execution records a canonical accepted-execution entry keyed by attack operation ID. Its fingerprint contains:

- operation ID, full enemy/run-participant identity, and lifecycle generation;
- occurrence time;
- full attack descriptor and nested projectile/area/melee values;
- committed intent, direction, point, and target;
- item/equipment instance identity;
- execution kind, resolved damage, and resolved cooldown;
- the issued-decision fingerprint that authorized the attack.

`RoutePlayerImpact` requires that ledger entry and an exact execution fingerprint. A caller-created request with matching IDs but altered damage, cooldown, descriptor, intent, source, or lifecycle cannot reach the player-damage port.

## Replay, death, and multi-hit behavior

- Attack replay signatures include the issued-decision fingerprint, full targeting/perception facts, occurrence time, difficulty context and resolved scaling, descriptor, aim configuration, and capability configuration.
- Exact attack-operation replay returns the original execution request and emits the attack effect once.
- Conflicting operation reuse rejects without emitting a second effect or changing cooldown.
- A valid accepted execution remains authorized after ordinary enemy death. A projectile fired before death can therefore still impact later.
- Death does not authorize new attacks: a new execution operation attempted after terminal state rejects.
- Recomposition creates a fresh runtime ledger. Old decisions and old projectiles reject through lifecycle generation.
- Impact idempotency remains keyed by the distinct hit-event operation ID. Exact hit replay routes damage once, conflicting reuse rejects, and multiple distinct hit IDs can reference one accepted execution for projectile count, pierce, area, chain, or damage-over-time behavior.

## Perception configuration

The unused `RequireMatchingObserverPosition` field was removed. The engine-neutral adapter has no duplicate position authority and therefore no longer exposes configuration that promises an unenforced invariant. A compatibility constructor accepts `false` only and rejects `true`; new registrations use the policy-ID-only constructor.

## Terminal facts and room composition

Incoming enemy damage mutates only canonical `EnemyActorState` through `EnemyActorStepper`. Lethal damage emits one immutable attributed `EnemyDeathFactV1`, which is delivered once to room terminal, collision terminal, XP, drop, and kill-stat consumers. The runtime does not grant XP, roll drops, mutate inventory, or own room-clear authority.

`CreateRoom` remains all-or-nothing. It rejects mixed rooms, mixed run/room-runtime contexts, duplicate derived actor identities, or unresolved registrations before producing occupancy. Required/objective occupants block room clear; optional/non-participating occupants do not.

## Focused EditMode coverage

The existing `EnemyPlacementRuntimeFactoryV1Tests` suite retains its 10 composition tests. The prefixed authority-boundary fixture adds 20 focused tests covering fabricated and altered decisions, exact decision copies, attack replay conflicts, fabricated and altered executions, post-death projectiles, lifecycle restart rejection, terminal attack rejection, exact/conflicting hit replay, multi-hit execution, and the removed perception option.

## Unity proof commands

Focused EditMode suite and prefixed authority tests:

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

The connector execution environment has no repository checkout, Unity Editor, .NET SDK, or C# compiler. Source/API, metadata, delimiter, deterministic-fingerprint, changed-path, and forbidden-switch audits are performed before publication, but no Unity compilation or XML proof is claimed. The PR remains draft until Unity validation succeeds.
