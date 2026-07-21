# CHARACTER-COMPOSITION-001 — Account-to-Hub character composition

## Launch boundary

- Repository: `YeerooXY/shooter-mover`
- Launch branch: `main`
- Exact launch SHA: `208bc89be4ce34750213139c80399ea7983e70e5`
- Work branch: `agent/character-composition-001-account-to-hub`
- Required dependency present at launch: merged SAVE-ADAPTERS-001 / PR #260

## Ownership

The composition layer owns only binding, selection lifecycle, migration coordination, Results handoff, and durable save coordination.

It does **not** introduce replacement authorities for:

- player experience;
- holdings or inventory;
- money or scrap wallets;
- ranked skills;
- exact-instance loadout;
- strongboxes;
- the six-slot player account aggregate.

The selected runtime graph constructs the existing production authority implementations once for the exact selected character. Their state is restored and exported through the merged `ISaveComponentAdapterV1` contracts. The existing `PlayerAccountSaveAuthorityV1` remains the only aggregate mutation boundary, and `AtomicPlayerAccountStoreV1` remains the durable file protocol.

## Transactional selection and switching

The explicit existing-slot activation sequence is:

1. Character Select supplies the exact target six-slot index.
2. If another runtime graph is active, every current authority snapshot is exported and durably saved first.
3. A rejected export, aggregate mutation, or atomic file save rejects activation. The old graph and old published profile remain active.
4. Only after successful persistence is the previous runtime graph fully unbound and disposed.
5. The account aggregate resolves the exact `CharacterInstanceSnapshotV1` in the target slot.
6. A fresh graph of the existing subsystem authorities is created.
7. `PlayerAccountRestoreCoordinatorV1` validates the complete account, stages the selected character components, and restores the graph through the merged adapters.
8. Non-selected slots are bound with no runtime adapters, so their immutable snapshots stay opaque and untouched.
9. Hub, inventory, and gameplay-facing adapters publish the selected restored graph instead of reconstructing starter state from a route payload.

Selecting the already-active exact character is an identity-preserving no-op. This is required after a successful creation transaction because the Unity profile lifecycle projects the newly committed account character and invokes selection once more before updating the visible active slot.

## Transactional empty-slot creation

A new character is not installed by saving a migration result before the old character. `CharacterCompositionCoordinatorV1.CreateAndSelect` owns the complete sequence:

1. Persist the currently active graph, when present.
2. Reject immediately if that pre-save fails; the target slot remains empty and the old graph remains published.
3. Capture the complete post-pre-save account authority snapshot as the rollback point.
4. Build the starter graph from the existing production authority factory and export its merged save components.
5. Apply one deterministic `CreateCharacter` command to the account authority in memory only.
6. Restore and validate the candidate graph through `PlayerAccountRestoreCoordinatorV1` while the old graph remains active.
7. Durably save the aggregate containing the persisted old character and the restored new character.
8. If creation, restore, or durable save fails, restore the rollback authority snapshot, attempt a compensating save of that snapshot, dispose only the candidate graph, and keep the old graph published.
9. After durable success, dispose the old graph and publish the exact new slot.

This transaction also handles first-character creation. A single empty-slot request entering through the legacy-profile adapter delegates to this coordinator transaction. Multi-slot PlayerPrefs import remains a deterministic one-time batch migration because no existing selected runtime is being replaced.

A process interruption before the new aggregate commit leaves the previously persisted account authoritative. An interruption after the commit may leave the new character durably created, but the old character's latest accepted state is already present in that same aggregate.

## Explicit persistence

Confirmed mutations export immutable component snapshots from the active authority graph. Only changed component fingerprints are installed into the selected exact slot through `PlayerAccountSaveAuthorityV1`. The resulting aggregate is committed through `AtomicPlayerAccountStoreV1`.

If aggregate mutation or durable file replacement fails, the account save authority is rolled back to its previous snapshot and replay state. Save operation IDs are deterministic from the mutation scope and immutable result fingerprint, preserving idempotent retry behavior.

The UI cutover persists confirmed exact-instance loadout changes, every explicit character activation, confirmed strongbox openings, application pause, quit, and composition teardown. All paths use the same adapter export and account save transaction.

## Character-owned strongbox authority

`ProductionCharacterRuntimeGraphV1` now always contains the real `StrongboxOpeningServiceV1` and its merged `StrongboxState` save adapter. It shares the selected graph's existing:

- `PlayerHoldingsService`;
- `MoneyWalletService`;
- `ScrapWalletServiceV1`;
- `RewardApplicationServiceV1` child-authority bindings;
- GEN equipment resolver and production strongbox definition catalog.

The current starter equipment catalog has no augment definitions or augment capacity. Character BOX composition therefore clamps each tier's augment budget to the actual equipment catalog capacity, producing valid zero-augment equipment rather than an impossible roll. Future catalogs with real compatible augments automatically expose their candidates and capacity through the same composition.

At Results handoff, the exact run BOX contexts are merged into the selected character's BOX snapshot by exact context/opening identity. The opening screen receives the selected character's authority. After a confirmed opening, the complete character graph is durably persisted before Results refresh. The character BOX state is then projected back to the current run scope so the immutable run result marks only the exact selected box opened. Prior character BOX history is retained and conflicting identities reject.

Engine-neutral tests and non-Unity callers that do not install the character bridge retain the original supplied BOX service behavior. Once the production Unity bridge is installed, missing character BOX resolution or failed persistence is a hard rejection.

## PlayerPrefs migration

PlayerPrefs is a one-time migration input only when no account file exists.

For every occupied legacy slot:

- the source character definition, class/loadout profile, slot index, account identity, and legacy payload fingerprint derive one deterministic exact character-instance ID;
- the existing production authority graph creates starter state;
- the merged adapters export valid XP, holdings, money, scrap, skill, exact-loadout, and empty character-owned BOX components;
- one `CreateCharacter` command installs the character through the account save authority;
- one atomic account save commits all migrated slots.

Retries derive the same character and operation identities. Already migrated matching slots are exact no-ops; occupied conflicting slots are rejected rather than overwritten. Existing account files never re-import PlayerPrefs. After initialization, PlayerPrefs is only a UI projection/cache of account-owned slots.

## Exact-instance loadout restore

`ProductionInventoryLoadoutAuthorityV1.ImportSnapshot` is a restore-only path for the existing loadout authority. It:

- verifies the component fingerprint;
- validates every exact equipment binding against the already-restored holdings authority and equipment catalog;
- enforces class mount availability and slot kind;
- preserves the durable loadout sequence;
- clears only transient command replay state.

It does not replay an equip command or create a second loadout model.

## Regression coverage authored

`CharacterCompositionCoordinatorV1Tests` covers:

- six-slot state isolation and restart restoration;
- complete known-component restoration;
- disposal ordering;
- corrupt selected-character rejection;
- durable store rollback;
- deterministic one-time migration;
- real exact-instance loadout restoration.

`CharacterActivationAndStrongboxRegressionTests` additionally covers:

- mutating A, activating existing B directly without a manual save, restarting, and restoring A's mutation;
- failed pre-activation persistence rejecting the switch without disposing or unpublishing A;
- a real production BOX registration/open/save/restart/replay path over the character-owned holdings, money, scrap, GEN, RAP, and BOX authorities;
- exact duplicate opening replay returning no second award.

`CharacterCreationTransactionRegressionTests` covers the empty-slot failure window:

- mutate active A;
- begin creating B;
- successfully persist A;
- fail B's post-restore durable commit;
- compensate back to the post-A aggregate;
- keep A active and undisposed;
- restart with A's mutation restored and B absent.

## Manual verification checklist

1. Start with legacy PlayerPrefs profiles and no `player-account-v1.save`.
2. Enter Character Select and confirm all occupied legacy slots are projected from the new account file.
3. Select character A and mutate XP, money, scrap, skills, holdings, or loadout without manually saving.
4. Select existing character B directly, restart, return to A, and verify A's exact mutation survived.
5. Create B in an empty slot directly, restart, return to A, and verify A's exact mutation survived.
6. Simulate failure of B's post-restore account commit and verify A remains active, A's mutation survives restart, and B is absent.
7. Collect and open an exact strongbox for A, close/reload, and verify the box remains opened and its grants are not repeated.
8. Select B and verify A's XP, holdings, wallets, skills, loadout, and BOX history do not appear in B.
9. Switch repeatedly between A and B and verify no duplicate starter equipment or stale subscriptions.
10. Corrupt one selected character component and verify an explicit restore diagnostic, no partial graph, and unchanged other slots.
11. Interrupt an atomic save and verify the active file or last-known-good backup restores without a partial aggregate.
