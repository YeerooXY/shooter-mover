# WPN-CORE-002 — Atomic weapon execution core

## Scope

This change introduces one engine-neutral, game-wide application boundary for weapon execution. It does not spawn Unity objects, change inventory/loadout authority, or alter scenes and prefabs.

Starting point: `b2bf4348ab6f827a737add53278d57568684f552` from `origin/main`.

## Reconciliation findings

The accepted `WeaponCatalog` / `WeaponDefinitionData` model remains the sole weapon-definition source. `WeaponCatalogRuntimeProfileResolver` projects a live catalog definition into a validated runtime firing profile; it does not create a second catalog. The equipment-to-weapon link is isolated behind `IEquipmentWeaponDefinitionIdResolver`, with the default using `EquipmentDefinition.RuntimeWeaponReferenceId`.

PR #206 contributed useful catalog-driven cadence, projectile count, spread, range and damage ideas, independent equipment state, deterministic spread, and fail-closed behavior. Its vertical-slice-only runtime and controller integration were not retained.

PR #212 contributed public behavior registration, rejection before cooldown/replay commit, and distinct projectile/explosion/chain descriptions. Its `object` identities, `UnityEngine.Vector3`, slice-prefixed contracts, per-effect sink calls, and executor-owned shared mutable state were not retained.

## Authoritative flow

```text
exact requested EquipmentInstanceId
  -> equipped-instance authority verifies the exact equipped instance
  -> equipment catalog validates the concrete instance
  -> equipment definition resolves the imported weapon definition ID
  -> live WeaponCatalog definition resolves a validated firing profile
  -> public WeaponBehaviorRegistry resolves the behavior
  -> behavior builds one immutable WeaponEffectBatch
  -> core validates every effect identity and payload
  -> IWeaponEffectBatchSink accepts/rejects the whole batch atomically
  -> only Accepted / AlreadyAccepted commits cooldown, sequence and replay state
```

Participant attribution is not accepted from the fire command. `IWeaponActorOwnershipResolver` supplies the authoritative participant for the actor and lifecycle generation.

## Identity boundaries

The execution model keeps actor instance, run participant, concrete equipment instance, imported weapon definition, registered behavior, fire operation, lifecycle generation, accepted shot sequence, and projectile ordinal separate.

Cooldown and accepted-operation state are keyed by actor instance, concrete equipment instance and lifecycle generation. Independent equipment instances never share state merely because they share a definition.

## Atomic batch contract

Every effect in one `WeaponEffectBatch` belongs to exactly one fire operation and carries the same actor, participant, equipment instance, weapon definition, lifecycle generation and shot sequence, with unique contiguous projectile ordinals.

The sink returns `Accepted`, `AlreadyAccepted`, or `Rejected`. Rejection means no effect was committed. The core does not mutate cooldown, shot sequence, or replay state until acceptance, so a rejected operation can be retried with the same deterministic batch.

Each accepted operation stores its immutable command fingerprint, batch fingerprint, and accepted shot sequence. An operation ID is reported as `ReplayAccepted` only when the incoming command and deterministically rebuilt batch both match the accepted fingerprints. Reusing the same operation ID with changed timing, deterministic seed, origin, aim, tuning, or behavior output is rejected as `ConflictingDuplicate` without calling the sink or mutating state.

## Supported descriptions

- direct projectile;
- deterministic multi-projectile/pellet batch;
- explosive projectile with area-detonation data;
- chain/arc request with target and range limits.

DoT, persistent pools, healing, multi-burst execution, and mixed chain-plus-explosion profiles fail closed until explicit descriptions and sink support exist.

## Deterministic spread

Spread uses only the explicit seed, fire operation ID, concrete equipment instance ID, accepted shot sequence, and projectile ordinal. No Unity random API or mutable global random state is used.

## Registration extension

`WeaponBehaviorRegistry.Register` is the public extension point. A fifth behavior requires an `IWeaponBehavior`, registration in composition, a selector/catalog binding, and focused tests. The core does not change.

## WPN-LIVE-001 handoff

A later Unity adapter should:

1. Read the active concrete equipment instance from the existing loadout authority.
2. Read actor identity and lifecycle generation from lifecycle authority.
3. Build one `WeaponFireCommand` with origin, aim and deterministic seed.
4. Route the atomic batch through existing projectile, explosion and chain adapters.
5. Persist or replay-protect the accepted composite batch identity.
6. Surface conflicting-duplicate diagnostics without partial side effects.
7. Remove the retained legacy ID branch and fallback from the live controller.

## Verification command

```bash
Unity -batchmode -projectPath <project> -runTests -testPlatform EditMode \
  -testFilter ShooterMover.Tests.EditMode.Weapons.Execution.WeaponExecutionCoreTests \
  -testResults artifacts/test-results/WPN-CORE-002-EditMode.xml \
  -logFile artifacts/test-results/WPN-CORE-002-EditMode.log
```

No Unity XML was produced in the connector environment. A passing claim requires a completed XML run with zero failures.
