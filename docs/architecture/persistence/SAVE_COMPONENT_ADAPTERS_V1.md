# SAVE-ADAPTERS-001 — authority snapshot components and atomic account files

## Scope

This package connects the immutable snapshots already owned by the XP, holdings,
money, scrap, ranked-skill, exact-instance loadout, and strongbox-opening systems
to `CharacterInstanceSnapshotV1` / `PlayerAccountSnapshotV1`. It does not create
replacement authorities and it does not reconstruct a selected character into
the Hub.

## Stable component identities

| Component ID | Wrapper schema | Content version | Canonical source snapshot |
| --- | ---: | --- | --- |
| `save-component.player-experience` | 1 | `player-experience-snapshot-v1` | `PlayerExperienceSnapshotV1` |
| `save-component.player-holdings` | 1 | `player-holdings-snapshot-v1` | `PlayerHoldingsSnapshotV1` |
| `save-component.money-wallet` | 1 | `money-wallet-snapshot-v1` | `MoneyWalletSnapshot` |
| `save-component.scrap-wallet` | 1 | `scrap-wallet-snapshot-v1` | `ScrapSnapshotV1` |
| `save-component.ranked-skill-allocation` | 1 | `ranked-skill-allocation-v2` | `RankedSkillAllocationSnapshotV2` |
| `save-component.exact-instance-loadout` | 1 | `inventory-loadout-snapshot-v1` | `InventoryLoadoutAuthoritySnapshotV1` |
| `save-component.strongbox-state` | 1 | `strongbox-opening-snapshot-v1` | `StrongboxOpeningSnapshotV1` |
| `save-component.character-statistics` | 1 | `character-statistics-snapshot-v1` | optional typed adapter only when a canonical statistics snapshot exists |

Unopened strongbox instance truth remains in the holdings snapshot. BOX opening
registration, stages, terminal facts, and replay records remain in the strongbox
opening snapshot. Restore composition must validate their cross-component
consistency before commit.

## Canonical component serialization

`CanonicalSnapshotCodecV1` serializes public immutable snapshot properties in
ordinal property order. Collections preserve their canonical order and map keys
are sorted by their encoded key. Values use invariant numeric formats and
length-prefixed nodes, so embedded newlines and delimiters are unambiguous.

The payload never carries a CLR type name. The typed component adapter supplies
the expected snapshot type, preventing a save file from selecting an arbitrary
type for activation. Decode reconstructs a new immutable object graph and then
requires byte-identical reserialization. Snapshot-native fingerprint checks
(`HasValidFingerprint`, `ComputeFingerprint`, or `CreateCanonical`) run before a
component can be prepared.

The payload contains the complete source snapshot, including transaction,
grant, claim, consume, and opening records exposed by that authority. Adapters
do not summarize replay history into balances or selected items.

## Authority and atomicity boundary

`AuthoritySnapshotSaveComponentAdapterV1<TSnapshot>` receives three typed
composition delegates:

1. export the current immutable source snapshot;
2. validate a decoded candidate without mutating live state;
3. apply an already validated snapshot at commit time.

The semantic validator should use the authority's existing import validator or a
throwaway/shadow composition of that authority. The adapter captures the current
snapshot for rollback but does not create or own a second live authority.

`PlayerAccountRestoreCoordinatorV1` prepares every account and character
component first. Required missing components, wrapper/content version mismatch,
corrupt payloads, fingerprint failures, and semantic rejection all stop before
any `Commit` call. Prepared components commit in deterministic account/slot/ID
order. A later commit failure rolls back earlier commits in reverse order.

Bindings include both slot index and exact character-instance ID. This prevents
one of the six account slots from restoring into another character authority.

Unknown component IDs are not interpreted. They remain inside the immutable
account aggregate and are returned as retained opaque components so a newer save
can survive a round trip through an older build. A known component with an
unknown schema/content version is not treated as an unknown optional component;
it rejects.

## Schema migration boundary

Wrapper schema `1` is strict. This package performs no implicit migration.
Future migrations must be explicit, deterministic functions that transform a
validated older component into the current component before authority preflight.
They must never mutate the original source payload and must produce a new
component fingerprint.

Content-version changes are also explicit. Catalog-dependent authorities such as
XP, skills, loadout, holdings, and strongboxes remain responsible for validating
curve/catalog/profile fingerprints and deciding whether a separately authored
migration is permitted.

## Atomic file protocol

`AtomicPlayerAccountStoreV1` depends only on `IAtomicSaveFilePortV1`; it does not
use `PlayerPrefs` or Unity APIs.

Save sequence:

1. validate the complete account with the injected aggregate validator;
2. encode the account into the versioned file envelope;
3. write only the temporary path;
4. read the temporary bytes back;
5. decode, fingerprint-check, and re-run aggregate validation;
6. ask the filesystem port to atomically replace active with temporary while
   moving the previous active file to the one last-known-good backup;
7. read and validate the active file after replacement.

If temporary writing/readback fails, active and backup are untouched. The
filesystem implementation is contractually responsible for atomic replacement;
a partial destination must never be visible.

Load reads active first. If active is absent or invalid, it validates and returns
the last-known-good backup with an explicit `RecoveredLastKnownGood` status. It
does not silently create a fresh account or overwrite the damaged active file.
The caller may surface recovery and choose when to write the recovered snapshot.

## Focused verification

EditMode fixtures:

```text
Assets/ShooterMover/Tests/EditMode/Persistence/Components/SaveAdaptersV1Tests.cs
Assets/ShooterMover/Tests/EditMode/Persistence/Components/StrongboxSaveAdapterReplayTests.cs
```

Pinned editor command:

```bash
"<UNITY_6000.3.19f1>" -batchmode -nographics -quit \
  -projectPath "<REPO>" \
  -runTests -testPlatform EditMode \
  -testFilter "ShooterMover.Tests.EditMode.Persistence.Components" \
  -testResults "<REPO>/TestResults/save-adapters-editmode.xml" \
  -logFile "<REPO>/TestResults/save-adapters-editmode.log"
```

The suite covers all supported component IDs in one character, six-slot
isolation, duplicate definitions with distinct equipment instance identities,
replay receipt preservation, pre-commit corrupt rejection, required/optional
behavior, unsupported-schema non-overwrite, temporary-read interruption,
unknown-component retention, rollback after a later commit failure, real XP and
money authority replay, and real strongbox-opening replay without re-running the
generator.

## Rollback

Remove:

- `Assets/ShooterMover/Runtime/Application/Persistence/Components/**`
- `Assets/ShooterMover/Tests/EditMode/Persistence/Components/**`
- this document

No production save file has been introduced by this task, so repository rollback
requires no data migration.
