# BOX-PERSIST-001 — Durable unopened strongboxes and crash-safe opening

Launch SHA: `7b21fcf66d69a60b25b305b617af24a909054613`

## Scope

This change completes the existing CHARACTER-COMPOSITION-001 strongbox path. It does not add a second holdings, BOX, reward, run, or persistence authority.

The canonical production run entry point remains `ProductionConditionBoundRunSessionStartSourceV1`. Its non-condition runtime factory is decorated downstream with durable terminal-result application; condition clock, lifecycle, status-effect ownership, and condition facts are unchanged.

## Ownership

- Run Session owns exact physical collection facts, frozen selected-character identity, lifecycle generation, and immutable terminal mission result.
- `PlayerHoldingsService` owns exact held strongbox instances and grant/source provenance.
- `StrongboxOpeningServiceV1` owns registered contexts, deterministic generated outcomes, opening replay, reward admission, and terminal opening facts.
- Existing RAP, equipment, money, scrap, and holdings authorities apply exact rewards and consumption.
- `CharacterCompositionCoordinatorV1`, save-component adapters, `PlayerAccountSaveAuthorityV1`, and `AtomicPlayerAccountStoreV1` own aggregate persistence and durable publication.
- Results and Strongbox Opening remain selection, animation, projection, and routing adapters only.

## Terminal transfer sequence

1. The existing mission-result port freezes the immutable terminal result from exact run-local holdings and BOX snapshots.
2. `PersistentMissionResultRunPortV1` resolves those exact source snapshots and submits one typed `StrongboxMissionResultApplicationCommandV1`.
3. `StrongboxMissionResultApplicationCoordinatorV1` validates the run/result/route/character/account revisions, complete unopened set, unique identities, source holdings provenance, BOX registration context, and absence of conflicting opening state before mutation.
4. It captures exact target holdings and BOX snapshots.
5. Every exact box is reconciled into the selected character with existing holdings and BOX commands.
6. The complete selected-character adapter graph is exported and persisted by `CharacterCompositionCoordinatorV1` through the existing atomic account store.
7. Run Session receives a successful terminal result only after durable persistence succeeds.
8. Any holdings, registration, or save failure imports the exact pre-transfer holdings and BOX snapshots and returns a stable rejection. A later distinct valid retry remains possible.

Operation IDs are deterministic from the run, terminal result, selected character, and lifecycle generation. Exact operation replay returns the original immutable result; changed facts under the same operation ID reject as a conflicting duplicate.

## Opening and recovery sequence

`StrongboxDurableOpeningCoordinatorV1` validates the exact selected character, box instance, definition, holdings provenance, BOX context, command authority IDs, and existing opening command before invoking the existing BOX service.

- Before terminal success, it captures the complete selected-character component graph represented by existing save adapters.
- A rejected, exceptional, or save-failed attempt is restored through `PlayerAccountRestoreCoordinatorV1`; the box remains retryable and presentation receives no terminal success.
- Authority-owned pending BOX phases are persisted so restart can resume the same frozen request/result. The existing reward application authority is rehydrated by replaying the persisted BOX commit/claim commands through its idempotent command surface.
- A successful existing BOX/RAP/consume result is persisted with `CharacterCompositionCoordinatorV1` before being projected to the opening screen.
- The durable account is verified against every exported selected-character component payload before terminal success is returned.
- Exact terminal replay accepts the already-frozen generated outcome even though the consumed box is no longer held; no reward or equipment instance is generated again.

`ProductionStrongboxDurableOpeningBootstrapV1` late-binds the existing canonical screen to this durable executor after the normal flow coordinator has established the exact immutable binding. It does not own selection or state.

## Crash model

All authoritative opening mutations occur in memory before the single atomic account save. If the process terminates before the save, the prior active/last-known-good account remains durable. If the save succeeds, the complete selected-character graph—including holdings, wallets, equipment instances, and BOX snapshot—is published atomically. The screen never reports success before that publication.

## Duplicate definitions and isolation

Equipment generation remains inside the existing BOX/GEN/RAP path. Separate box/opening operation identities yield separate concrete equipment-instance IDs even when deterministic generation chooses the same equipment definition.

The transfer and opening coordinators bind commands to the exact selected character and active slot. Character switches, stale commands, wrong holdings authorities, wrong BOX authorities, or mismatched collection provenance reject. All other account slots remain untouched and unknown optional components remain opaque and retained by the existing account aggregate.

## Focused tests

`StrongboxPersistenceCoordinatorV1Tests` covers:

- exact one-box transfer, durable restore, and exact replay;
- complete-batch rejection when one box lacks source authority facts;
- save-failure compensation and repaired retry;
- durable opening, restart, and no-second-award replay;
- full component-graph restore after terminal-save failure;
- two same-tier boxes producing the same definition with distinct equipment instances;
- selected-character slot isolation.

Existing SAVE-ADAPTERS-001, CHARACTER-COMPOSITION-001, RUN-SESSION-001, BOX, Results, and opening suites remain the required regression set.

## Explicit exclusions

No Room JSON cutover, enemy catalog/pattern work, active abilities, combat presentation, health bars, death VFX, health authority, Combat Hit Policy, condition runtime internals, Stage 1 controller, weapon behavior, unrelated scene, or prefab changes are included.
