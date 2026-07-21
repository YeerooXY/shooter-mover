# Terminal gameplay fact to deterministic drop generation v1

## Ownership boundary

`DROP-FACT-BIND-001` introduces one engine-neutral boundary from an already accepted terminal gameplay fact to one immutable generated reward batch and one separate idempotent admission boundary for pending, uncollected batches.

The source authorities remain unchanged:

- the enemy runtime owns health, terminal transition and `EnemyDeathFactV1`;
- the prop runtime owns health, destruction and `PropFactBatchV1`;
- enemy and prop definitions own the configured drop-profile reference;
- Run Session owns the active run identity, lifecycle generation and frozen deterministic seed context;
- DROP owns the canonical `RewardOperationRequestV1` identity contract;
- GEN owns reward selection, quantities, ordering and explicit no-drop behavior;
- `PendingTerminalDropAdmissionAuthorityV1` owns only pending-batch admission identity.

`TerminalDropGenerationAuthorityV1` owns terminal-event replay records and the immutable binding result. Neither authority owns health, definitions, inventories, strongboxes, mission results, physical pickups or persistence.

## Authoritative flow

```text
accepted EnemyDeathFactV1 or PropFactBatchV1
    -> registered terminal-fact adapter
    -> definition-owned drop-profile resolution
    -> exact Run Session lifecycle/context validation
    -> canonical RewardOperationRequestV1
    -> existing RewardGenerationServiceV1
    -> immutable GeneratedTerminalDropResultV1
    -> idempotent pending-batch admission by DROP operation ID
```

The generated result remains a pending, uncollected run-local fact. It is not a physical pickup and it is not a permanent reward grant.

## Adapter and registration extension point

`TerminalDropFactAdapterRegistryV1` is keyed by the exact terminal fact CLR type and carries an explicit stable fact-kind identity. Registrations are sorted before the registry fingerprint is calculated, so input order cannot change canonical behavior.

The built-in complete adapters are:

- `ContextResolvedEnemyDeathTerminalDropFactAdapterV1` for `EnemyDeathFactV1`;
- `PropDestructionTerminalDropFactAdapterV1` for `PropFactBatchV1`.

Enemy definition/profile validation lives in the internal `EnemyDeathTerminalDropDefinitionProjectorV1`. It is not an `ITerminalDropFactAdapterV1` and cannot construct a `TerminalDropSourceFactV1`; only the context-resolved enemy adapter can combine catalog facts with production-owned Run Session lifecycle context.

Adding another source that uses the same DROP/GEN mechanics normally requires one adapter registration, definition/profile data and focused tests. The shared generation authority contains no enemy-ID, prop-ID or drop-profile-ID switch.

Unknown terminal fact types fail closed with `UnsupportedFactType` and generate nothing.

## Definition and profile resolution

The enemy projector resolves the exact `EnemyDefinitionV1` from `EnemyCatalogV1`. The definition's `DropProfileId` is authoritative. A profile identity already present on the death fact may confirm the definition but may not override or conflict with it.

The prop adapter resolves the exact `PropDefinitionV1` from `PropCatalogV1` and reads the `DropOnDestroy` capability's `profile-id`. A runtime `DropRequest` fact, when present, must belong to the same prop/source and match the definition-owned profile.

`PropTerminalFactV1.PropParticipantId` is the authoritative prop source entity. Resolver-provided source context is rejected if its `SourceEntityStableId` identifies a different prop, and accepted source facts always use the terminal fact's participant identity.

A definition with no drop profile is converted to a deterministic synthetic `RewardProfileV1.CreateExplicitNoDrop` profile. This keeps no-drop inside the existing DROP/GEN contract instead of treating an absent profile as accidental success or inventing a fallback table.

A configured but missing profile rejects with `MissingDropProfile`. Invalid or incompatible profile references reject before GEN is called.

## Source and Run Session context

Enemy death facts preserve run, room, placement, entity, enemy lifecycle and attribution facts, but they do not own the Run Session lifecycle generation. `IEnemyTerminalSourceContextResolverV1` supplies that separate production fact and validates the entity, placement and enemy source generation against the death fact.

Prop terminal facts do not contain run/placement lifecycle data, so `IPropTerminalSourceContextResolverV1` is the narrow typed lookup port that projects those facts from the existing production prop composition.

`ITerminalDropRunContextResolverV1` validates the exact run and expected lifecycle. `RunSessionTerminalDropContextResolverV1` is the read-only bridge to `RunSessionAuthorityV1`; it rejects missing, stale/future or ended runs and exposes the frozen run seed plus progression/event context through a dedicated provider. It performs no Run Session mutation.

The explicit unattributed policy in v1 is fail-closed: a terminal fact without an attributed run participant rejects with `UnattributedTerminalFact`. No arbitrary player is selected.

## Operation identity and deterministic seed

The DROP operation ID is derived with the existing `RewardApplicationCanonicalV1.DeriveStableId` convention from immutable authoritative material including:

- terminal event identity;
- run identity and Run Session lifecycle generation;
- source entity and placement identity;
- source lifecycle generation;
- source definition identity;
- resolved profile identity;
- attributed participant identity;
- immutable upstream fact fingerprint;
- frozen Run Session generation-context fingerprint.

The GEN root seed is derived deterministically from the frozen Run Session root seed, the canonical DROP request fingerprint and the immutable source-fact fingerprint. No wall clock, Unity frame count, object hash code, collection order or global random state participates.

## Generation replay, exception containment and atomicity

Generation replay is keyed by the exact terminal event identity.

- First successful delivery stores one immutable accepted/no-drop generation result.
- Exact replay returns `ExactReplay` with the same operation identity, generation seed, generated batch, child identities and canonical batch fingerprint.
- A reused terminal event identity with different immutable source facts returns `ConflictingDuplicate` and mutates nothing.
- A rejected or retryable failure is not cached as success or no-drop, so an exact retry can safely resolve the same immutable source fact again.

The complete uncommitted pipeline is exception-contained. Adapter/source-context, Run Session resolution, profile resolution, operation construction, generation-request construction, GEN execution and batch finalization exceptions return immutable stage-diagnostic rejections. No exception path creates a replay record or partial accepted batch.

## Pending-batch admission and lost-response recovery

Generation idempotency and downstream admission idempotency are separate responsibilities.

`PendingTerminalDropAdmissionAuthorityV1` keys pending entries by the canonical `RewardOperationRequestV1.SourceOperationStableId` and returns:

- `Accepted` when the operation is first admitted;
- `ExactReplay` when the same operation and batch fingerprint are redelivered;
- `ConflictingDuplicate` when an operation ID is reused with a different batch fingerprint;
- `Rejected` for null, rejected or incomplete generation results.

Typed enemy and prop consumers always redeliver generation results to this admission boundary, including `ExactReplay`. This allows recovery when the first publication failed, while the pending authority guarantees that two routes, two deliveries or a rebuilt generation authority cannot create two pending entries.

## Generated batch contract

`GeneratedTerminalDropResultV1` preserves:

- terminal event and upstream source facts;
- source entity/placement, definition and lifecycle identity;
- run and lifecycle identity;
- attributed participant;
- resolved profile;
- canonical DROP operation;
- deterministic generation seed;
- complete existing GEN envelope/result;
- immutable ordered child reward facts;
- canonical batch fingerprint;
- accepted, explicit-no-drop, exact-replay, conflict or rejection status.

Stackable grants remain one ordered child with their generated quantity. Strongbox and equipment-reference quantities are expanded to exact per-instance child identities, derived from the operation, grant and unit ordinal. Two same-tier strongboxes from distinct terminal facts therefore retain the same content definition but have distinct stable instance identities.

## Physical pickup and persistence boundary

This task deliberately stops at the immutable pending batch.

`PICKUP-LIVE-001` may later realize each admitted child identity as a physical pickup without rerolling. `DROP-PERSIST-PROOF-001` may later prove collection, mission-result transfer and permanent persistence.

This implementation does not:

- create Unity objects, sprites, colliders or coroutines;
- mark a pickup collected;
- mutate Run Session collected-reward state;
- add money, scrap, equipment or strongboxes to permanent holdings;
- write mission results or account saves;
- open strongboxes;
- edit `Stage1VisibleSliceController.cs`.
