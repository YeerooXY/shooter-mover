# Economy transactions v1

## Status

This document defines the REW-001 immutable economy transaction vocabulary.
LED-001, MON-001, SCR-001, INV-001, RAP-001, BOX-001, SHOP-001, CRA-001, and
AUG-001 consume it without creating private duplicate/result enums.

It defines commands, results, payload fingerprints, sequence language, and
duplicate classification only. It is not a wallet, ledger, holdings authority,
or atomic application implementation.

## Command identity

`EconomyTransactionCommandV1` carries:

- `TransactionStableId`: idempotency identity for one authority mutation;
- `OperationStableId`: aggregate/source/product operation that requested it;
- `AuthorityStableId`: intended money, scrap, or holdings authority;
- operation kind;
- resource kind;
- `ResourceStableId`;
- optional unique `InstanceStableId`;
- positive quantity; and
- optional expected sequence.

The payload fingerprint includes every field. A retry of one transaction must
reuse the original transaction ID and complete payload. Reusing the ID with a
changed operation, authority, resource, instance, quantity, or expected sequence
is a conflicting duplicate.

## Operation vocabulary

`EconomyTransactionOperationV1` defines:

- `Credit`;
- `Debit`;
- `AddStack`;
- `RemoveStack`;
- `AddUnique`; and
- `RemoveUnique`.

`EconomyResourceKindV1` defines:

- `Currency`;
- `Item`;
- `Strongbox`; and
- `EquipmentReference`.

The valid shape matrix is:

| Resource kind | Allowed operation family | Instance ID | Quantity |
|---|---|---|---|
| `Currency` | `Credit` / `Debit` | forbidden | positive |
| `Item` | `AddStack` / `RemoveStack` | forbidden | positive |
| `Strongbox` | `AddUnique` / `RemoveUnique` | required | exactly one |
| `EquipmentReference` | `AddUnique` / `RemoveUnique` | required | exactly one |

`ResourceStableId` is the concrete currency, item, strongbox definition, or
equipment definition. `InstanceStableId` names a unique owned box/equipment
instance. Equipment concrete types remain owned by EQP-001.

The command validates shape but does not decide whether the target authority
accepts a particular currency namespace, owns an instance, has sufficient value,
or has capacity.

## Expected sequence

`ExpectedSequence` is optional. When present it is a non-negative admission
expectation against the target authority's current sequence.

A later authority returns `ExpectedSequenceConflict` without mutation when the
expectation does not match. This contract does not read or store sequences.

## Duplicate classification

`EconomyTransactionIdentityV1.Classify` compares two immutable commands:

| Condition | Classification |
|---|---|
| Transaction IDs differ | `DistinctTransaction` |
| Transaction IDs match and payload fingerprints match | `ExactDuplicateNoChange` |
| Transaction IDs match and payload fingerprints differ | `ConflictingDuplicate` |

An exact duplicate is a successful idempotent retry and produces no additional
change. A conflicting duplicate fails closed. It must never be treated as an
exact retry merely because the transaction ID matches.

The classifier is pure. LED-001 owns storage and lookup of accepted transaction
fingerprints.

## Result vocabulary

`EconomyTransactionStatusV1` defines the shared result language:

- `Applied`;
- `ExactDuplicateNoChange`;
- `ConflictingDuplicate`;
- `InvalidRequest`;
- `InsufficientValue`;
- `InsufficientCapacity`; and
- `ExpectedSequenceConflict`.

`EconomyTransactionResultV1` contains:

- transaction ID;
- status;
- canonical command fingerprint;
- previous sequence;
- current sequence;
- resulting non-negative value or quantity; and
- result fingerprint.

Sequence invariants are explicit:

- `Applied` advances sequence by exactly one;
- every duplicate or rejection leaves sequence unchanged.

`InsufficientValue` covers later bounded debit/removal admission.
`InsufficientCapacity` covers later holdings/capacity admission. The contract
does not choose limits or perform admission.

## Authority separation

Money, scrap, and holdings remain separate public authorities even though they
share this transaction vocabulary and later share LED-001 mechanics.

- Money authority accepts only its configured money currency IDs.
- Scrap authority accepts only its configured scrap currency IDs.
- Holdings authority owns stackable items and unique strongbox/equipment
  instances.
- Product services submit commands; they do not keep mirrored balances or private
  item lists.
- RAP-001 coordinates all-or-none mixed reward application; the command itself
  does not coordinate multiple authorities.

## Canonicalization and fingerprinting

Canonical text uses invariant decimal formatting, enum numeric values, canonical
StableId strings, explicit `null` / `none` tokens, and fixed field order.

Fingerprints use SHA-256 over canonical UTF-8 text and canonical
`sha256:<64 lowercase hex>` form. They do not depend on current culture,
dictionary order, Unity, ambient time, or random state.

Changing any identity or payload field changes the command fingerprint. A result
fingerprint additionally covers status, sequences, and resulting value.

## Validation failures

Construction rejects at least:

- null transaction, operation, authority, or resource IDs;
- undefined operation/resource/status enum values;
- zero or negative quantities;
- negative expected sequence;
- currency with holdings operations;
- non-currency with credit/debit operations;
- stack item with unique operations;
- strongbox/equipment reference without unique operations;
- missing instance ID for unique operations;
- instance ID on non-unique operations;
- unique quantity other than one;
- malformed command fingerprints;
- negative sequences or resulting values;
- applied result without exactly one sequence increment; and
- duplicate/rejected result that changes sequence.

## Non-goals

Economy transactions v1 does not implement:

- transaction storage, lookup, import, export, or pruning;
- balance/holding state;
- credits, debits, additions, removals, or rollback;
- affordability, ownership, capacity, sequence admission, or atomic commit;
- money/scrap namespace policy or production currency IDs;
- reward application, strongbox opening, shop purchase, crafting, or upgrades;
- persistence, UI, Unity assets, adapters, scenes, or balance values.
