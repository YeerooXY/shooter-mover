# CHARACTER-COMPOSITION-001 — Account-to-Hub character composition

## Launch boundary

- Repository: `YeerooXY/shooter-mover`
- Launch branch: `main`
- Exact launch SHA: `208bc89be4ce34750213139c80399ea7983e70e5`
- Work branch: `agent/character-composition-001-account-to-hub`
- Required dependency present at launch: merged SAVE-ADAPTERS-001 / PR #260

## Ownership

The composition layer owns only binding, selection lifecycle, migration coordination, and durable save coordination.

It does **not** introduce replacement authorities for:

- player experience;
- holdings or inventory;
- money or scrap wallets;
- ranked skills;
- exact-instance loadout;
- strongboxes;
- the six-slot player account aggregate.

The selected runtime graph is built from the existing production authority implementations. Their state is restored and exported through the merged `ISaveComponentAdapterV1` contracts. The existing `PlayerAccountSaveAuthorityV1` remains the only aggregate mutation boundary, and `AtomicPlayerAccountStoreV1` remains the durable file protocol.

## Selection and switching

1. Character Select supplies the exact six-slot index.
2. The account aggregate resolves the exact `CharacterInstanceSnapshotV1` in that slot.
3. Any previous runtime graph is fully unbound and disposed.
4. A fresh graph of the existing subsystem authorities is created.
5. `PlayerAccountRestoreCoordinatorV1` validates the complete account, stages the selected character components, and restores the graph through the merged adapters.
6. Non-selected character slots are bound with no runtime adapters, so their immutable snapshots stay opaque and untouched.
7. Hub, inventory, and gameplay-facing production adapters resolve the selected restored graph instead of reconstructing starter state from route payloads.

A failed restore leaves the account snapshot unchanged, publishes no partially restored graph, and exposes the restore diagnostic.

## Explicit persistence

Confirmed mutations export immutable component snapshots from the active authority graph. Only changed component fingerprints are installed into the selected exact slot through `PlayerAccountSaveAuthorityV1`. The resulting aggregate is committed through `AtomicPlayerAccountStoreV1`.

If aggregate mutation or durable file replacement fails, the account save authority is rolled back to its previous snapshot and replay state. Save operation IDs are deterministic from the mutation scope and immutable result fingerprint, preserving idempotent retry behavior.

The current concrete UI cutover persists confirmed exact-instance loadout changes. The coordinator also persists the active graph before slot switches and on application pause, quit, or composition teardown, so mutations made by connected existing subsystem authorities are exported through the same adapter path.

Strongbox state is supported through the same graph adapter collection when a real character-owned strongbox authority is supplied. No local or duplicate strongbox authority is manufactured by this task.

## PlayerPrefs migration

PlayerPrefs is a one-time migration input only when no account file exists.

For every occupied legacy slot:

- the source character definition, class/loadout profile, slot index, account identity, and legacy payload fingerprint derive one deterministic exact character-instance ID;
- the existing production authority graph creates starter state;
- the merged adapters export the valid XP, holdings, money, scrap, skill, and exact-loadout components;
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

- two characters mutating differently, switching, restarting, and retaining isolated values;
- restoration of every known character component, including an optional strongbox adapter;
- disposal of the old graph before the next graph factory runs;
- corrupt selected-character restore with no account-slot mutation and no stale graph publication;
- rollback after a durable store failure;
- one-time idempotent legacy migration with class preservation and no duplicate starter components;
- real production authority/adaptor round-trip with exact equipment-instance bindings.

## Manual verification checklist

1. Start with legacy PlayerPrefs profiles and no `player-account-v1.save`.
2. Enter Character Select and confirm all occupied legacy slots are projected from the new account file.
3. Select character A, change an exact weapon binding, confirm, return to Hub, and restart.
4. Verify character A retains the binding and exact equipment-instance ID.
5. Select character B and verify A's XP, holdings, wallets, skills, loadout, and optional box state are not visible in B.
6. Switch repeatedly between A and B and verify no duplicate starter equipment or stale screen subscriptions.
7. Corrupt one selected character component in a test save and verify an explicit restore diagnostic, no partial graph, and unchanged other slots.
8. Interrupt an atomic save and verify the active file or last-known-good backup restores without a partial aggregate.
