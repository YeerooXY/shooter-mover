# SAVE-ADAPTERS-001 — explicit authority component codecs and atomic restore

## Scope

This package persists the immutable snapshots already owned by the XP, holdings,
money, scrap, ranked-skill, exact-instance loadout, and strongbox-opening
systems inside `CharacterInstanceSnapshotV1` / `PlayerAccountSnapshotV1`.

It does not introduce a replacement gameplay authority, reconstruct the selected
character into the Hub, add PlayerPrefs authority, or alter a scene/controller.

## Registered durable component contracts

Each known component has one stable ID, one wrapper schema, one content version,
one explicit codec, one deterministic field order, one typed factory path, and
one validation boundary.

| Component ID | Wrapper schema | Content version | Typed snapshot |
| --- | ---: | --- | --- |
| `save-component.player-experience` | 1 | `player-experience-explicit-v1` | `PlayerExperienceSnapshotV1` |
| `save-component.player-holdings` | 1 | `player-holdings-explicit-v1` | `PlayerHoldingsSnapshotV1` |
| `save-component.money-wallet` | 1 | `money-wallet-explicit-v1` | `MoneyWalletSnapshot` |
| `save-component.scrap-wallet` | 1 | `scrap-wallet-explicit-v1` | `ScrapSnapshotV1` |
| `save-component.ranked-skill-allocation` | 1 | `ranked-skill-allocation-explicit-v2` | `RankedSkillAllocationSnapshotV2` |
| `save-component.exact-instance-loadout` | 1 | `inventory-loadout-explicit-v1` | `InventoryLoadoutAuthoritySnapshotV1` |
| `save-component.strongbox-state` | 1 | `strongbox-opening-explicit-v1` | `StrongboxOpeningSnapshotV1` |

The earlier arbitrary `CharacterStatistics<TSnapshot>` seam was removed.
Statistics may become durable only after a canonical statistics snapshot is
registered with its own stable ID/content version and explicit codec. Two
unrelated snapshot types cannot share one durable contract.

## Persisted fields are explicit

`CanonicalNodeCodecV1` provides only bounded scalar/list/object framing. It does
not discover snapshot properties, constructors, parameter names, or CLR types.
Every known codec manually declares persisted fields in stable order and calls a
known public constructor or canonical factory.

Examples:

- XP persists authority ID, sequence, curve fingerprint, cumulative XP,
  progression context, and accepted grant history.
- Holdings persists authority/capacity, the complete idempotent ledger,
  equipment/strongbox/stack projections, exact instance IDs, provenance, and
  transaction records.
- Wallets persist balances plus accepted/rejected transaction histories.
- Skills persist profile/class/version/catalog versions and ordered rank inputs.
- Loadout persists sequence and exact slot-to-equipment-instance bindings.
- Strongboxes persist registered exact box contexts, opening commands/stages,
  frozen reward/generation facts, application payloads, consume commands, and
  terminal replay facts.

Adding a convenience/computed public property to an authority snapshot therefore
does not change durable bytes. A persisted-field change requires an intentional
content-version change and migration.

Payloads never persist CLR type names and cannot select arbitrary runtime types.
`KnownSaveComponentCodecsV1` directly references all seven codec types, so the
production path has no reflection/private-constructor discovery dependency.

## AOT / IL2CPP boundary

The codec registry and every decode factory are statically referenced. No
reflection preservation metadata is required for this implementation. The
focused tests include a direct registry smoke test and real round trips through
all seven codecs.

A Unity IL2CPP-capable player smoke run is still required before the PR leaves
draft. EditMode success alone is not the final production proof.

## Canonical framing and corrupt-input limits

The node grammar uses length-prefixed scalars and explicit list/object counts.
Objects preserve codec-authored field order; unordered domain inputs are sorted
before encoding by their owning codec/factory.

Hard limits:

- account file: 16 MiB;
- decoded account payload: 12 MiB;
- individual component payload: 2 MiB;
- node depth: 48;
- list count: 8,192;
- object property count: 128;
- scalar length: 1 MiB.

Oversize, excessive-depth/count, malformed, duplicate-field, non-canonical, and
numeric-overflow inputs return stable rejection codes. The parser does not
allocate a collection before its declared count has passed the bound.

## Known and unknown component versions

Unknown component IDs remain opaque `SaveComponentSnapshotV1` payloads. The
account codec preserves their ID, schema, content version, and bytes without
interpreting them, allowing a newer optional component to survive an older
build's read/write cycle.

Known component IDs are different: unsupported wrapper schemas or content
versions reject. `KnownSaveComponentVersionGuardV1` is the durable store policy
for this boundary. A known unsupported component is never downgraded to an
unknown optional component.

## Aggregate semantic consistency

`PlayerAccountComponentSemanticsV1` validates relationships that cannot be
proven by one component alone:

- every equipped loadout instance must exist as an exact equipment instance in
  restored holdings;
- every held unopened strongbox must have the same exact registered context;
- held definition/tier and registration tier must match;
- holding grant provenance and registration collection provenance must match;
- opening records require their exact registered box context;
- generated opening requests must match exact box identity, tier and definition
  fingerprint;
- opened boxes must no longer be held;
- registered unopened boxes must still be held;
- an injected catalog resolver may require the current definition fingerprint.

This task proves authority consistency only. The wider Results/Hub/run lifecycle
remains `BOX-PERSIST-001` / later composition work.

## Compensating aggregate restore

`AuthoritySnapshotSaveComponentAdapterV1<TSnapshot>` captures the current
immutable snapshot and its explicit encoded bytes during prepare. Apply/import
delegates should be internally atomic whenever possible, but aggregate
correctness does not assume that they are.

Commit behavior:

1. mark the component commit as attempted;
2. invoke apply for the next snapshot;
3. if apply rejects or throws—even after mutating—immediately apply the captured
   previous snapshot;
4. re-export and compare explicit codec bytes to confirm compensation;
5. the coordinator then rolls back every earlier successful component in reverse
   order and confirms each previous snapshot the same way.

The coordinator reports four distinct failure outcomes:

- `CommitFailedRolledBack`: failing component and every earlier component are
  confirmed restored;
- `CommitFailedCompensationIncomplete`: earlier rollback completed, but the
  failing component was not confirmed restored;
- `CommitFailedEarlierRollbackIncomplete`: failing compensation completed, but
  an earlier component was not confirmed restored;
- `CommitFailedCompensationAndRollbackIncomplete`: both phases were incomplete.

It never reports ordinary rolled-back success based solely on an apply return
value.

Restore bindings include both exact slot index and character-instance ID, keeping
all six account slots isolated. Components commit in deterministic
account/slot/restore-order/component-ID order.

## Replay preservation notes

XP, holdings, money, scrap, and strongbox snapshots include their underlying
accepted/rejected transaction or opening histories, so exact retries after
restore return their existing no-change/frozen facts.

The current ranked-skill snapshot stores allocation truth but its authority does
not expose the private operation replay dictionary. After restore, replay of an
old command is rejected by the restored allocation version and cannot mutate the
allocation. Adding full skill-operation replay persistence requires an authority-
owned canonical snapshot extension, not an adapter-owned duplicate history.

The current production loadout snapshot stores exact slot bindings and sequence;
it has no separate active-selector field. The focused test restores a real
sequence-one authority state by replaying its exact binding command and proves
that command is then an exact no-change replay. No selector model was invented.

## Atomic file protocol and recovery

`AtomicPlayerAccountStoreV1` depends only on `IAtomicSaveFilePortV1` and does not
use Unity APIs or PlayerPrefs.

Save sequence:

1. validate the complete account and known component versions;
2. encode the explicit account envelope;
3. write only the temporary path;
4. read and validate the exact temporary bytes;
5. atomically replace active with temporary while retaining the previous active
   file as one last-known-good backup;
6. read and validate active after replacement.

Temporary-write/readback failure leaves active and backup untouched. The
filesystem port contract requires atomic replacement and must never expose a
partially written active destination.

Load validates active first. If active is absent or invalid, it validates and
returns backup with `RecoveredLastKnownGood`. Recovery does not silently create
a fresh account or overwrite the damaged active file.

## Verification paths

Focused EditMode namespace:

```text
ShooterMover.Tests.EditMode.Persistence.Components
```

Coverage includes:

- real XP, holdings, money, scrap, skills, loadout, and strongbox snapshots;
- complete account-file encode/decode before each real authority restore;
- accepted/rejected replay behavior;
- duplicate-definition equipment with distinct exact instance IDs;
- exact unopened strongbox ownership;
- accepted strongbox frozen replay without a second generator/RAP/consume pass;
- strongbox/loadout cross-component mismatch rejection;
- mutate-then-reject and mutate-then-throw compensation;
- all four compensation/rollback result classes;
- unknown opaque component and six-slot round trips;
- known unsupported version non-overwrite;
- active/temp/backup interruption and recovery;
- bounded malicious/corrupt payload rejection;
- direct AOT-visible codec registration and canonical/golden payload stability.

Pinned command:

```bash
"<UNITY_6000.3.19f1>" -batchmode -nographics -quit \
  -projectPath "<REPO>" \
  -runTests -testPlatform EditMode \
  -testFilter "ShooterMover.Tests.EditMode.Persistence.Components" \
  -testResults "<REPO>/TestResults/save-adapters-editmode.xml" \
  -logFile "<REPO>/TestResults/save-adapters-editmode.log"
```

The PR must remain draft until focused zero-failure XML, full Unity compilation,
and an IL2CPP-capable codec smoke result exist.

## Rollback

Remove the task-owned runtime/test component folders and this document. No
production save file, Hub composition, scene edit, or migration was introduced.
