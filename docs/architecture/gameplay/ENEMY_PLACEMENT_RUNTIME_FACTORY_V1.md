# ENEMY-FACTORY-001 — Placement-driven enemy runtime composition

## Launch boundary

- Repository: `YeerooXY/shooter-mover`
- Exact launch `main` SHA: `208bc89be4ce34750213139c80399ea7983e70e5`
- Branch: `agent/enemy-factory-001-placement-runtime`
- Target: `main`
- Full Level 1 JSON renderer cutover is intentionally excluded.
- Combat Hit Policy remains a separate authority; this runtime exposes only narrow future integration ports.

## Composition flow

`EnemyPlacementRuntimeFactoryV1` turns imported room placements into independent enemy runtimes by resolving the exact room object, enemy definition, presentation, level/difficulty scaling, stable actor and participant identities, lifecycle generation, registered behavior policies, canonical actor state, and room occupancy. It contains no enemy-type, room-number, prefab-name, or hierarchy-name dispatcher.

## Issued-decision authority

Every successful `Evaluate` records a deterministic immutable fingerprint covering enemy identity/lifecycle, complete perception facts, selected target and attack, movement intent, committed attack intent, and execution-relevant debug projection fields. Target collections are canonicalized before hashing.

`RealizeMovement` and `TryExecuteAttack` accept only an exact decision projection issued by the same runtime and lifecycle. Exact immutable copies remain valid; fabricated, altered, foreign, and stale decisions reject. Terminal enemies may still be evaluated for immutable state/debug presentation, but movement realization rejects before invoking movement policy or realization.

## Accepted-execution authority

A successful first attack execution records a canonical ledger entry keyed by attack operation ID. Its fingerprint includes full source identity/lifecycle, occurrence time, descriptor and nested capability values, committed intent, item identity, execution kind, resolved damage/cooldown, and the authorizing decision fingerprint.

`RoutePlayerImpact` requires an exact ledger-backed execution. Fabricated or altered execution facts reject before reaching the player-damage port.

## Player target lifecycle

The authoritative impact API requires:

```csharp
RoutePlayerImpact(
    execution,
    hitEventStableId,
    targetEntityStableId,
    observedTargetLifecycleGeneration)
```

`EnemyPlayerDamageRequestV1` carries both source lifecycle generation and `ObservedTargetLifecycleGeneration`. The observed target generation is included in impact replay identity and forwarded unchanged to the downstream PlayerActorAuthority / Combat Hit Policy adapter.

This allows a downstream authority adapter to reject a projectile that observed player generation 4 after the same stable player entity has restarted into generation 5. One hit-event operation ID also cannot be replayed across different target generations.

A temporary obsolete three-argument overload exists only so earlier test/caller code still compiles; new production adapters must pass the observed target generation explicitly.

## Replay, death, restart, and multi-hit behavior

- Exact attack replay emits once and returns the original immutable execution.
- Conflicting attack-operation reuse rejects without cooldown mutation.
- Exact hit replay routes player damage once.
- Conflicting hit-event reuse rejects.
- Distinct hit IDs may reference one accepted execution for projectile count, pierce, area, chain, or damage-over-time behavior.
- A projectile issued while the enemy was alive remains authorized after ordinary enemy death.
- Death does not permit new attack execution or movement realization.
- Enemy recomposition creates a fresh lifecycle ledger; old enemy decisions and executions reject.
- Player lifecycle freshness remains downstream authority: the factory carries the immutable observed generation rather than duplicating PlayerActorAuthority.

## Terminal and downstream facts

Incoming enemy damage mutates only canonical `EnemyActorState` through `EnemyActorStepper`. Lethal damage emits one attributed immutable death fact and fans it out once to room terminal, collision terminal, XP, drop, and kill-stat consumers. The runtime does not grant rewards, mutate inventory, or own room-clear authority.

## Focused EditMode coverage

Current focused inventory is 32 authored tests:

- 10 composition tests;
- 20 decision/execution/replay authority tests;
- 2 lifecycle-routing and terminal-movement regressions.

The lifecycle regressions prove that an impact observed against player generation 4 is rejected by a downstream adapter after the player authority has advanced to generation 5, while a generation-5 impact is accepted, and that a dead enemy cannot realize movement from a newly evaluated decision.

## Unity proof commands

```bash
"<UNITY_EDITOR>" -batchmode -nographics -projectPath . \
  -runTests -testPlatform EditMode \
  -testFilter ShooterMover.Tests.EditMode.Enemies.EnemyPlacementRuntimeFactoryV1Tests \
  -testResults artifacts/enemy-factory-001-editmode.xml \
  -logFile artifacts/enemy-factory-001-editmode.log -quit
```

```bash
"<UNITY_EDITOR>" -batchmode -nographics -projectPath . \
  -runTests -testPlatform EditMode \
  -testResults artifacts/enemy-factory-001-all-editmode.xml \
  -logFile artifacts/enemy-factory-001-all-editmode.log -quit
```

## Known limitation

This connector environment has no repository checkout, Unity Editor, .NET SDK, or C# compiler. Source/API, metadata, deterministic-fingerprint, changed-path, and forbidden-switch audits can be performed here, but no current-head Unity compilation or XML test proof is claimed. The PR remains draft until Unity validation succeeds.
