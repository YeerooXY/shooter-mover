# BOX-PERSIST-001 — Durable unopened strongboxes and crash-safe opening

Launch SHA: `7b21fcf66d69a60b25b305b617af24a909054613`

## Scope

This change completes the existing CHARACTER-COMPOSITION-001 strongbox path. It does not add a second holdings, BOX, reward, run, or persistence authority.

The canonical production run entry point remains `ConditionBoundRunSessionStartSource`. Its condition-bound composition uses the underlying mission-result port directly; condition clock, lifecycle, status-effect ownership, and condition facts are unchanged.

`RewardClaimTransferPreparationFactory` and the collected-run prepared-custody/receipt flow are the single live mission-end persistence owner for money, scrap, equipment, and strongboxes. The former strongbox-only mission-result coordinator and run-port decorator are retired.

## Ownership

- Run Session owns exact physical collection facts, frozen selected-character identity, lifecycle generation, and immutable terminal mission result.
- `PlayerHoldingsActions` owns exact held strongbox instances and grant/source provenance.
- `StrongboxOpeningActions` owns registered contexts, deterministic generated outcomes, opening replay, reward admission, and terminal opening facts.
- Existing RAP, equipment, money, scrap, and holdings authorities apply exact rewards and consumption.
- `CharacterSetupFlow`, save-component adapters, `PlayerAccountSaveState`, and `AtomicPlayerAccountStore` own aggregate persistence and durable publication.
- Results and Strongbox Opening remain selection, animation, projection, and routing adapters only.

## Terminal transfer sequence

1. Physical pickup records one exact collected-run reward child and its pickup collection operation.
2. Before Run End, `RewardClaimTransferPreparationFactory` freezes the complete collected journal, exact payloads, unopened BOX contexts, and authority fingerprints into durable awaiting custody.
3. The accepted immutable mission result promotes that same custody to a prepared atomic plan; it does not create a second strongbox transfer.
4. The collected-run atomic state applies RAP grants, BOX registrations, transfer receipt, and prepared-custody state as one compensated batch.
5. `CharacterSetupFlow` publishes the complete selected-character component graph in one terminal durable account save.
6. Rejected-before-replacement persistence restores the captured in-memory state. Durable uncertainty retains prepared custody for exact recovery and never reports completion.

Canonical collected-run strongboxes use the generated reward-child ID as both holding instance ID and holding grant provenance. The BOX context retains the exact pickup collection-operation ID and original drop operation. Save validation also accepts the historical representation where holding grant provenance equals BOX collection provenance; that alternate form is compatibility for existing saves only and is not a live transfer path. Tier, instance, registration, definition fingerprint, and provenance combinations outside those two forms reject.

Prepared transfers are immutable. Recovery rebuilds only the already-frozen plan. A transfer whose frozen holdings still match can continue through canonical validation without a duplicate grant. `frozen-authority-mismatch:holdings` remains quarantined unless an exact receipt proves completion; recovery never rebases fingerprints, guesses authority state, or silently grants or deletes rewards.

Operation IDs are deterministic from the run, terminal result, selected character, and lifecycle generation. Exact operation replay returns the original immutable result; changed facts under the same operation ID reject as a conflicting duplicate.

## Opening and recovery sequence

`StrongboxDurableOpeningFlow` validates the exact selected character, box instance, definition, holdings provenance, BOX context, command authority IDs, and existing opening command before invoking the existing BOX service.

- Before terminal success, it captures the complete selected-character component graph represented by existing save adapters.
- A rejected, exceptional, or save-failed attempt is restored through `PlayerAccountRestoreFlow`; the box remains retryable and presentation receives no terminal success.
- Authority-owned pending BOX phases are persisted so restart can resume the same frozen request/result. The existing reward application authority is rehydrated by replaying the persisted BOX commit/claim commands through its idempotent command surface.
- A successful existing BOX/RAP/consume result is persisted with `CharacterSetupFlow` before being projected to the opening screen.
- The durable account is verified against every exported selected-character component payload before terminal success is returned.
- Exact terminal replay accepts the already-frozen generated outcome even though the consumed box is no longer held; no reward or equipment instance is generated again.

`ProductionStrongboxDurableOpeningBootstrapV1` late-binds the existing canonical screen to this durable executor after the normal flow coordinator has established the exact immutable binding. It does not own selection or state.

## Crash model

All authoritative opening mutations occur in memory before the single atomic account save. If the process terminates before the save, the prior active/last-known-good account remains durable. If the save succeeds, the complete selected-character graph—including holdings, wallets, equipment instances, and BOX snapshot—is published atomically. The screen never reports success before that publication.

## Duplicate definitions and isolation

Equipment generation remains inside the existing BOX/GEN/RAP path. Separate box/opening operation identities yield separate concrete equipment-instance IDs even when deterministic generation chooses the same equipment definition.

The collected-run transfer and opening coordinator bind commands to the exact selected character and active slot. Character switches, stale commands, wrong holdings authorities, wrong BOX authorities, or mismatched collection provenance reject. All other account slots remain untouched and unknown optional components remain opaque and retained by the existing account aggregate.

## Focused tests

Collected-run, save-rule, and durable-opening suites cover:

- exact one-box collected-run transfer, durable restore, and exact replay;
- canonical, historical, and invalid mixed provenance validation;
- complete-batch rejection when one box lacks source authority facts;
- save-failure compensation and exact prepared-custody retry;
- durable opening, restart, and no-second-award replay;
- full component-graph restore after terminal-save failure;
- two same-tier boxes producing the same definition with distinct equipment instances;
- selected-character slot isolation.

Existing SAVE-ADAPTERS-001, CHARACTER-COMPOSITION-001, RUN-SESSION-001, BOX, Results, and opening suites remain the required regression set.

## Explicit exclusions

No Room JSON cutover, enemy catalog/pattern work, active abilities, combat presentation, health bars, death VFX, health authority, Combat Hit Policy, condition runtime internals, Stage 1 controller, weapon behavior, unrelated scene, or prefab changes are included.
