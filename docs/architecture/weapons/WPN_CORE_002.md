# WPN-CORE-002 — Atomic weapon execution core

## Scope

This change introduces one engine-neutral, game-wide application boundary for weapon execution. It does not modify `Stage1VisibleSliceController`, spawn Unity objects, change inventory/loadout authority, or alter scenes and prefabs.

Starting point: `b2bf4348ab6f827a737add53278d57568684f552` from `origin/main`.

## Reconciliation findings

The accepted `WeaponCatalog` / `WeaponDefinitionData` model remains the sole weapon-definition source. `WeaponCatalogRuntimeProfileResolver` projects a live catalog definition into a validated runtime firing profile; it does not create a second catalog. The equipment-to-weapon link is isolated behind `IEquipmentWeaponDefinitionIdResolver`, with the default using `EquipmentDefinition.RuntimeWeaponReferenceId`.

PR #206 contributed useful catalog-driven cadence, projectile count, spread, range and damage ideas, independent equipment state, deterministic spread, and fail-closed behavior. Its Stage1-only runtime and controller integration were not retained.

PR #212 contributed public behavior registration, rejection before cooldown/replay commit, and distinct projectile/explosion/chain descriptions. Its `object` identities, `UnityEngine.Vector3`, Stage1-prefixed contracts, per-effect sink calls, and executor-owned shared mutable state were not retained.

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
3. Gather origin and aim as immutable scalar `WeaponVector2` values.
4. Create one unique fire operation and call `WeaponExecutionCore.TryExecute`.
5. Implement one transactional sink over existing systems:
   - stage direct projectile spawns and impact payloads;
   - stage explosive projectile spawn plus bounded detonation data;
   - resolve chain candidates through scene/physics adapters and stable-sort targets by entity identity;
   - reject before any spawn or damage when any effect/value/binding is unsupported;
   - commit all staged effects together and persist the composite batch identity;
   - return `AlreadyAccepted` for an exact replay without repeating effects.
6. Translate the result to HUD/debug output.
7. Remove the retained Stage 1 weapon-ID branch and blaster fallback only in WPN-LIVE-001.

The adapter must not become a second inventory, equipment, damage, projectile, explosion, chain, participant-ownership, or lifecycle authority.

## Focused tests

Fixture: `ShooterMover.Tests.EditMode.Weapons.Execution.WeaponExecutionCoreTests`

Coverage includes exact equipment identity, authority-derived participant identity, unknown and preview definitions, missing equipment, invalid aim/tuning, unsupported effects, deterministic spread, shotgun count and sequence, atomic rejection/retry, accepted replay, independent equipment cooldowns, lifecycle restart, explosive/chain descriptions, and fifth-behavior registration.

Suggested Unity command for Unity `6000.3.19f1`:

```bash
Unity -batchmode -projectPath <project> -runTests -testPlatform EditMode \
  -testFilter ShooterMover.Tests.EditMode.Weapons.Execution.WeaponExecutionCoreTests \
  -testResults artifacts/test-results/WPN-CORE-002-EditMode.xml \
  -logFile artifacts/test-results/WPN-CORE-002-EditMode.log
```

Unity was not available in the connector environment used to author this change. No Unity compilation or zero-failure XML proof is claimed.
