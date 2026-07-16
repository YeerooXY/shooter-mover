# Player holdings v1

## Status and scope

`INV-001` defines the sole engine-independent durable ownership authority for:

- generated weapon and armor equipment instances;
- owned strongbox instances;
- premium-ammunition stacks; and
- arbitrary future miscellaneous item stacks.

The implementation lives only below:

- `Assets/ShooterMover/Runtime/Domain/Holdings/`;
- `Assets/ShooterMover/Runtime/Contracts/Holdings/`;
- `Assets/ShooterMover/Runtime/Application/Holdings/`; and
- `Assets/ShooterMover/Tests/EditMode/Holdings/`.

It composes the `LED-001` `IdempotentLedger<HoldingsLedgerVocabularyV1>` and
consumes the immutable `REW-001` economy/reward vocabulary plus the `EQP-001`
equipment validation port. It has no Unity, scene, UI, random, clock, filesystem,
save-backend, shop, crafting, pickup, strongbox-opening, wallet, or loadout
dependency.

## Authority boundary

`PlayerHoldingsService` is the only mutable ownership authority. Product systems
submit immutable typed commands through `IPlayerHoldingsAuthorityV1`; they do not
keep private box lists, equipment lists, ammunition counts, or miscellaneous item
maps.

The service owns one configured canonical `authority_stable_id` and one explicit
positive `maximum_stack_quantity`. Commands addressed to another authority fail
closed. Equipping and loadout slots are deliberately outside this contract.

## Typed command model

`PlayerHoldingsCommandV1` contains:

- one immutable `EconomyTransactionCommandV1`;
- one `RewardGrantKindV1`;
- immutable `HoldingProvenanceV1`; and
- an immutable `EquipmentInstance` only for equipment additions.

The economy command carries transaction, operation, authority, resource,
instance, quantity, and optional expected-sequence identities. Provenance adds
the durable grant and source identities. Their complete canonical payload is
fingerprinted, so changing any identity, kind, quantity, expected sequence,
equipment payload, grant, or source while reusing a transaction ID is a
conflicting duplicate.

Typed factories cover:

- add/remove equipment;
- add/remove strongbox;
- add/remove premium-ammunition stack; and
- add/remove miscellaneous stack.

The open miscellaneous identity is a `StableId`, not a closed enum. Adding a new
miscellaneous item definition therefore requires no holdings code change.

## Unique holdings

Equipment and strongboxes use globally collision-checked instance IDs inside one
holdings authority. A unique instance ID may be admitted only once for the
lifetime of an imported/exported authority history. Removing an instance does
not make its ID reusable. This prevents a strongbox ID from later becoming an
equipment ID and prevents a removed equipment instance from being regranted as a
new object.

Equipment additions must carry an immutable `EquipmentInstance` whose definition
and instance IDs match the economy command. Every addition is validated through
`IEquipmentInstanceValidator`; holdings never repairs, normalizes, or partially
accepts an invalid EQP-001 instance. Equipment removal references the stored
immutable instance by definition and instance ID and does not submit a replacement
payload.

A missing removal, definition mismatch, kind mismatch, or unique collision is
recorded as a deterministic no-mutation terminal transaction fact.

## Stack holdings

Premium ammunition and miscellaneous items use positive signed-64-bit quantities
with a configured positive maximum. Additions use checked arithmetic before
mutation. The authority rejects:

- arithmetic overflow;
- quantity above the configured bound;
- removal from a missing stack;
- removal larger than the current quantity; and
- reuse of one item identity under another stack reward kind.

A zero result removes the current stack projection, but the item-to-kind history
remains. A future addition may restore the same kind while a type-changing reuse
continues to fail closed.

## Exact-once behavior

Every command is translated into one typed LED-001 mutation. Admission follows
the shared ledger order:

1. transaction-ID replay lookup;
2. changed-payload conflict rejection;
3. exact replay no-change;
4. structural validation;
5. expected-sequence validation;
6. checked ledger arithmetic;
7. holdings validation;
8. mutation and one sequence increment.

An exact duplicate never revalidates equipment or current holdings and returns
`ExactDuplicateNoChange` with the original holdings terminal status. A rejected
first attempt is durable too: replaying the exact rejected command returns its
original rejection without reevaluating later state. A conflicting duplicate
never mutates state.

Only `Applied` advances the public holdings sequence. All missing-item, type,
capacity, validation, sequence, duplicate, and conflict outcomes leave ownership
unchanged.

## Public results

`PlayerHoldingsMutationResultV1` exposes:

- transaction identity;
- current and original status;
- canonical command fingerprint;
- previous/current authority sequence;
- previous/current typed holding quantity;
- rejection code; and
- deterministic result fingerprint.

Status vocabulary includes applied, exact duplicate, conflicting duplicate,
invalid request, wrong authority, wrong reward type, type mismatch, unique
collision, missing item, insufficient value, insufficient capacity, equipment
validation rejection, expected-sequence conflict, and arithmetic overflow.

## Snapshot v1

`PlayerHoldingsSnapshotV1.CurrentSchemaVersion` is `1`. A snapshot contains:

- schema version;
- authority ID;
- configured maximum stack quantity;
- the complete typed LED-001 snapshot;
- canonically ordered current unique holdings;
- canonically ordered current stacks;
- canonically ordered original holdings transaction records; and
- a lowercase SHA-256 fingerprint.

Current unique projections retain definition/instance IDs, immutable equipment
payload where applicable, add provenance, and their own fingerprints. Current
stack projections retain reward kind, open item ID, positive quantity, and their
own fingerprints. Transaction records retain every first-attempt command,
LED-001 terminal fact, holding quantities, and rejection code. Exact duplicate
calls do not create additional records.

Canonical ordering is ordinal and independent of dictionary iteration, caller
collection order, culture, current time, Unity state, or random state.

## Atomic validated import

Import validates into temporary state before replacing the live authority. It
rejects at least:

- null snapshots;
- unsupported schema versions;
- authority or configured-bound mismatch;
- malformed or mismatched holdings fingerprint;
- any LED-001 snapshot failure;
- missing, duplicate, or mismatched transaction records;
- gaps or duplicates in applied sequence;
- invalid applied command shape or equipment instance;
- impossible unique/type history;
- mismatched recorded holding quantities; and
- current projections that cannot be rebuilt from applied transaction facts.

Only after every check passes are the ledger, current holdings, collision/type
history, and transaction records swapped together. Every failure leaves the
previous live state and sequence unchanged.

## Required invariants

1. Unique equipment/armor and strongboxes add and remove exactly once.
2. Premium ammunition and arbitrary miscellaneous stacks add and remove exactly
   once.
3. One unique instance ID cannot collide across equipment, armor, or strongboxes,
   even after removal and snapshot import.
4. Equipment additions pass EQP-001 validation before ownership mutation.
5. Stack arithmetic is checked and bounded explicitly.
6. Missing, underflow, overflow, wrong type, wrong authority, and sequence
   failures cause no partial mutation.
7. Exact duplicate and conflicting duplicate identities remain distinct.
8. Equivalent snapshots have identical canonical fingerprints.
9. A corrupt import leaves the previous authority unchanged.
10. No Unity-facing object, UI, product service, or ScriptableObject becomes a
    second holdings authority.

## Non-goals

Player holdings v1 does not implement:

- equipping, loadout slots, inventory UI, or presentation;
- reward generation, commitment, claim, or mixed-authority application;
- money or scrap balances;
- strongbox opening;
- crafting, upgrades, shops, salvage, or pickups;
- persistence files, save backends, migration transport, or networking;
- production balance/catalog values; or
- scenes, prefabs, ScriptableObjects, settings, or packages.
