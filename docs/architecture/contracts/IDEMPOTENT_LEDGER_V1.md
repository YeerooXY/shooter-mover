# Idempotent Ledger v1

## Status and scope

`LED-001` defines the engine-independent exact-once mutation primitive shared
internally by later money, scrap, and holdings authorities. It owns transaction
admission, replay detection, sequence checks, immutable snapshots, deterministic
fingerprints, and atomic snapshot import.

It does **not** define money, scrap, item, capacity, reward, persistence, scene,
or cross-authority atomicity semantics. `RAP-001` remains the later coordinator
for all-or-none application across separate public authorities.

The implementation lives in:

- `ShooterMover.Domain.Economy.Ledger`;
- `Assets/ShooterMover/Runtime/Domain/Economy/Ledger/IdempotentLedger.cs`.

It has no `UnityEngine`, scene, file-system, clock, random, locale-sensitive, or
ambient static-state dependency.

## Typed vocabulary boundary

Every ledger and its commands use an authority-owned generic vocabulary marker:

```csharp
IdempotentLedger<MoneyLedgerVocabulary>
LedgerEntry<MoneyLedgerVocabulary>
LedgerMutation<MoneyLedgerVocabulary>
LedgerSnapshot<MoneyLedgerVocabulary>
```

A money entry therefore cannot be passed to an
`IdempotentLedger<ScrapLedgerVocabulary>` or holdings ledger without an explicit,
review-visible conversion. The generic marker is the compile-time authority
boundary. It does not merge public money, scrap, or holdings services.

Each entry has:

- canonical `entry_type_id` as a `StableId`;
- canonical `target_id` as a `StableId`;
- an authority-defined canonical payload string;
- a signed quantity delta supplied by the mutation.

Canonical payloads are exact printable ASCII strings from 0 through 1024
characters. The ledger never normalizes, trims, parses, or assigns product
meaning to the payload.

## Mutation contract

A mutation carries:

- one stable `transaction_id`;
- one typed ledger entry;
- one non-zero signed `quantity_delta`;
- optional `expected_sequence`;
- a deterministic SHA-256 payload fingerprint computed by the primitive.

The mutation fingerprint covers, using length-prefixed canonical tokens:

1. format marker `mutation-v1`;
2. entry type ID;
3. target ID;
4. canonical payload;
5. signed quantity delta in invariant decimal;
6. expected sequence in invariant decimal, or an explicit null token.

The transaction ID is the lookup key rather than part of its own payload
fingerprint.

### Admission order

`IdempotentLedger.Apply` follows this order:

1. detect an existing transaction ID;
2. reject changed reuse as `ConflictingDuplicate`;
3. return an exact replay as `DuplicateNoChange`;
4. validate command structure;
5. check optional expected sequence;
6. compute the proposed quantity with checked arithmetic;
7. run the composing authority's explicit validator;
8. run the composing authority's explicit mutation policy;
9. mutate state and advance sequence once.

Validation and policy run before any state mutation.

The constructor requires both an explicit validator and policy. The primitive has
no default insufficient-funds, non-negative-balance, capacity, uniqueness, item,
currency, or removal rule. A composing authority may reject a proposed quantity
or deliberately permit negative quantities according to its own contract.

## Result semantics

Public mutation statuses are:

| Status | Meaning |
|---|---|
| `Applied` | State changed once and sequence advanced once |
| `DuplicateNoChange` | The exact admitted transaction was already processed; state did not change |
| `ConflictingDuplicate` | The transaction ID exists with a different payload fingerprint |
| `SequenceConflict` | The supplied expected sequence did not equal the current sequence |
| `ValidationRejected` | Structural, arithmetic, or authority validation rejected the mutation |
| `PolicyRejected` | The explicit composing-authority policy rejected the mutation |

A duplicate result exposes the original terminal status, original sequence
before/after, original previous/current quantity, and original rejection code.
An exact retry therefore returns the original accepted or rejected fact without
re-evaluating current state, validation, or policy.

Structurally malformed commands that cannot form a valid durable transaction
fact, such as a null entry or zero delta, return `ValidationRejected` but are not
admitted to the transaction record. Structurally valid sequence, validator,
policy, and checked-arithmetic rejections are recorded. Reusing one of those
transaction IDs with changed payload is a conflict; replaying it exactly returns
`DuplicateNoChange` with the original rejection.

## State and sequence

State is keyed canonically by:

```text
entry_type_id + target_id + canonical_payload
```

The internal key uses length-prefixing and ordinal text. Quantities use signed
64-bit checked arithmetic. A resulting zero quantity removes the key from the
snapshot; positive or negative non-zero quantities remain according to the
explicit policy that admitted them.

Sequence starts at zero and increments exactly once for every `Applied`
mutation. Rejections and duplicates never advance it.

## Snapshot format v1

`LedgerSnapshot<TVocabulary>.CurrentSchemaVersion` is `1`.

A snapshot contains:

- schema version;
- applied mutation sequence;
- canonically ordered non-zero entries;
- canonically ordered original transaction records;
- deterministic lowercase SHA-256 fingerprint.

Entries are ordered by:

1. entry type ID, ordinal;
2. target ID, ordinal;
3. canonical payload, ordinal.

Transaction records are ordered by transaction ID, ordinal.

Snapshot constructors defensively copy and wrap all collections. Exported
collections are read-only and detached from future ledger mutation.

The snapshot fingerprint uses length-prefixed tokens and covers every schema,
sequence, entry, transaction, result, and rejection field. Collection input
order is normalized before fingerprinting.

## Snapshot import

Import validates into temporary state before replacing the live ledger. It
rejects:

- null snapshots;
- unsupported schema versions;
- negative sequence;
- null or duplicate entries;
- non-canonical `StableId` identities;
- invalid canonical payloads;
- zero stored quantities;
- null or duplicate transaction records;
- zero transaction deltas or negative expected sequence;
- invalid or mismatched mutation fingerprints;
- impossible original statuses;
- invalid accepted/rejected sequence and quantity facts;
- gaps or duplicates in the applied sequence;
- state that cannot be replayed from accepted transaction facts;
- invalid or mismatched snapshot fingerprint.

Only a fully validated import replaces balances, transaction facts, and sequence.
Every failure leaves the existing ledger unchanged.

## Determinism and portability

Fingerprints use SHA-256 over UTF-8 length-prefixed canonical text. All numeric
text uses `InvariantCulture`; identity and ordering use ordinal comparison.
Dictionary enumeration order is never a fingerprint input.

The primitive has no random, clock, filesystem, locale, Unity, serialization
package, or process-global dependency.

## Consumer obligations

Later authorities must:

- define a distinct vocabulary marker type;
- construct canonical entry type and target IDs;
- define the canonical payload format they own;
- provide explicit validation and policy delegates;
- keep their public commands, results, snapshots, and product semantics separate;
- persist/import the complete typed snapshot if persistence is later approved;
- reuse the original transaction ID for retries;
- treat changed reuse of a transaction ID as a hard conflict.

## Non-goals

This contract does not implement:

- a money or scrap wallet;
- holdings or inventory models;
- currency or item validation;
- reward generation, claim, or application;
- capacity or insufficient-funds policy;
- cross-authority atomic transactions;
- persistence files, save backends, migration transport, or serializers;
- Unity adapters, UI, scenes, shops, boxes, crafting, or upgrades.
