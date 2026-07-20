# PLAYER_ACCOUNT_SAVE_V1

## Purpose

`PLAYER_ACCOUNT_SAVE_V1` defines the durable aggregate above the existing XP, holdings, wallet, skills, loadout, strongbox, achievement, event, and future multiplayer authorities.

It does not replace those authorities. It stores their immutable snapshots under stable component identities and applies complete component replacements atomically.

## Aggregate

```text
PlayerAccountSnapshotV1
  account identity
  revision
  six nullable character positions
  account components

CharacterInstanceSnapshotV1
  concrete character-instance identity
  class-definition identity
  exact slot index
  display name
  revision
  character components
```

Class is data-defined. A healer, striker, juggernaut, or future class is represented by `ClassDefinitionStableId`; adding a class does not require another `CharacterInstance` subclass.

## Component extension model

A `SaveComponentSnapshotV1` contains:

- stable component identity;
- component schema version;
- content version;
- canonical payload;
- deterministic SHA-256 fingerprint.

Expected character component identities include:

- `character.experience`;
- `character.holdings`;
- `character.money`;
- `character.scrap`;
- `character.skills`;
- `character.loadout`;
- `character.unopened-strongboxes`;
- `character.statistics`.

Expected account component identities include:

- `account.achievements`;
- `account.collections`;
- `account.entitlements`;
- `account.daily-challenge-state`;
- `account.seasonal-state`;
- future account-level multiplayer state.

Adding another component does not require modifying the account aggregate or its mutation authority. The owning subsystem supplies serialization, validation, migration, and import/export adapters.

## Mutation authority

`PlayerAccountSaveAuthorityV1` supports:

- creating one exact character in one empty slot;
- replacing one subsystem snapshot for one exact character instance;
- deleting one exact character instance from one slot;
- replacing one account-level subsystem snapshot;
- expected-revision rejection;
- exact duplicate replay;
- conflicting operation-ID rejection;
- authority snapshot export/import with replay history.

All accepted mutations replace the immutable account snapshot in one step. Failed imports do not partially mutate the live authority.

## Explicit non-goals

This foundation does not yet:

- serialize the canonical payloads to JSON or disk;
- adapt the existing XP, holdings, wallet, skills, loadout, or BOX snapshots;
- replace `PlayerPrefsProductionFlowProfileStoreV1`;
- persist transient mission health, position, cooldowns, active effects, or bullets;
- implement cloud synchronization or multiplayer conflict resolution;
- compact old replay records.

Those are follow-up adapter and storage tasks. Runtime cooldowns remain run-local and equipment instances remain immutable inventory facts.

## Intended follow-up

1. Add typed component adapters over each existing authority snapshot.
2. Add a versioned JSON file store with temp-file write, flush, atomic replacement, backup, and corruption recovery.
3. Migrate the six existing PlayerPrefs route profiles into six `CharacterInstanceSnapshotV1` records.
4. Compose a selected character's authorities from its saved component set before entering the Hub.
5. Commit mission rewards and unopened boxes into one updated character save after authoritative run completion.

## Verification

Focused EditMode filter:

```text
ShooterMover.Tests.EditMode.Persistence.Accounts.PlayerAccountSaveAuthorityV1Tests
```
