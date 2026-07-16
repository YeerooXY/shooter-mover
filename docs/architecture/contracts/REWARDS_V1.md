# Rewards v1

## Status

This document defines the REW-001 immutable reward vocabulary. It is normative
for reward source authoring, shared generation, holdings, reward application,
strongbox opening, shops, crafting, pickups, and simulation.

The contracts are engine-independent. They define data, validation,
canonicalization, identity, and duplicate classification only. They do not roll,
apply, claim, persist, display, or balance rewards.

## Assembly ownership

- `ShooterMover.Domain.Rewards.Model` owns authored reward values and pure source
  override resolution.
- `ShooterMover.Contracts.Rewards` owns operation requests, generated grant
  results, trace facts, and strongbox opening envelopes.
- Every durable identity uses `ShooterMover.Domain.Common.StableId`.
- Equipment is referenced only through stable definition and instance IDs. This
  contract has no dependency on EQP-001 concrete types.

## Reward grant vocabulary

`RewardGrantKindV1` is the closed cross-product grant vocabulary:

| Kind | Meaning of `ContentStableId` |
|---|---|
| `Money` | Currency definition accepted by the money authority |
| `Scrap` | Currency definition accepted by the scrap authority |
| `Strongbox` | Strongbox definition/tier reference |
| `EquipmentReference` | Equipment definition reference; generated instance shape belongs to EQP-001 |
| `PremiumAmmo` | Premium-ammunition item definition |
| `Miscellaneous` | Generic stackable or future item definition |

The enum is product-independent. Adding a new enemy, prop, shop, or box does not
add a private DTO or switch branch. A new fundamentally different durable grant
kind requires a reviewed versioned contract change.

`RewardGrantSpecificationV1` contains:

- `GrantStableId`;
- `RewardGrantKindV1`;
- `ContentStableId`;
- one positive inclusive `RewardQuantityRangeV1`; and
- zero or more sorted `RewardScalingInputDescriptorV1` values.

A scaling descriptor identifies an explicit character-level, region-level,
difficulty, source-tier, or custom input. It contains no curve and performs no
calculation. PRG-001 and GEN-001 own the meaning and application of those inputs.

## Profile composition

`RewardProfileV1` has one profile StableId and exactly one disposition:

- `Configured`; or
- `ExplicitNoDrop`.

A configured profile may contain all three sections at once:

1. `GuaranteedEntries`: every grant specification is included by a later
   generator.
2. `IndependentRolls`: each `IndependentRewardRollV1` carries a unique roll ID,
   an integer probability in millionths, and one grant specification.
3. `ExclusiveGroups`: each `ExclusiveRewardGroupV1` carries positive weighted
   outcomes. A later generator selects exactly one outcome from the group.

`WeightedRewardOutcomeV1` is either:

- a grant outcome; or
- `ExplicitNoDrop` with its own outcome StableId and positive weight.

Probability millionths use the inclusive range `1..1,000,000`. Weight and
quantity values are positive integers. Floating-point probability text is not a
contract input.

An empty configured profile is rejected. Intentional absence must use
`RewardProfileV1.CreateExplicitNoDrop`, so no-drop cannot be confused with a
missing list, failed import, or accidental authoring omission.

## Duplicate identities

Within one profile:

- guaranteed grant IDs are unique;
- independent roll IDs are unique;
- exclusive group IDs are unique;
- weighted outcome IDs are unique inside their group; and
- grant IDs are unique across guaranteed entries, independent rolls, and every
  grant-bearing weighted outcome.

Appending a grant whose ID already exists in the inherited profile is rejected.
A grant ID remains the durable identity of that logical grant from generation
through projection, claim, and application.

## Source overrides

`RewardSourceOverrideV1` defines four explicit modes:

| Mode | Resolution |
|---|---|
| `InheritDefault` | Return the inherited profile unchanged |
| `NoReward` | Return a new explicit-no-drop profile using the author-supplied result profile ID |
| `ReplaceEntirely` | Return the supplied replacement profile |
| `AppendGuaranteedEntries` | Create a result profile with inherited sections plus author-supplied guaranteed grants |

Resolution is pure and deterministic. It performs no random sampling, source
claim, reward application, or persistence. No mode is inferred from null values;
each factory validates the exact data shape allowed by its mode.

## Reward operation identity

`RewardOperationRequestV1` carries:

- run ID;
- source instance ID;
- source operation ID;
- commitment ID;
- reward profile ID; and
- accepted content-definition fingerprint.

Its fingerprint covers every field. Retrying one logical source resolution must
reuse the original request. `RewardOperationIdentityV1.Classify` returns:

- `DistinctOperation` when operation IDs differ;
- `ExactDuplicateNoChange` when the operation ID and complete request fingerprint
  match; or
- `ConflictingDuplicate` when the operation ID is reused with a changed payload.

The classifier is a pure vocabulary helper. The source-claim authority that
stores prior requests belongs to later tasks.

## Generated results

`RewardGrantV1` contains one grant ID, grant kind, content ID, and positive fixed
quantity.

`RewardResultV1` contains commitment ID, source operation ID, and either:

- one or more canonically ordered generated grants; or
- explicit no-drop.

An empty grant result is rejected. Duplicate generated grant IDs are rejected.
The result fingerprint changes when commitment identity, operation identity,
grant identity, kind, content, or quantity changes.

## Explainable traces

`RewardTraceEntryV1` records:

- trace-entry ID;
- explicit non-negative ordinal;
- step ID;
- subject ID;
- decision kind;
- integer input value; and
- integer output value.

Decision kinds cover guaranteed admission, independent chance, exclusive
selection, quantity, scaling input, explicit no-drop, and grant production.

`RewardTraceV1` carries the source operation ID and unique entries. Entries are
canonically ordered by ordinal and then identity. Duplicate entry IDs and
duplicate ordinals are rejected. Constructing a trace does not consume random
state.

## Strongbox opening envelopes

`StrongboxOpeningRequestV1` represents an opening intent without implementing it.
It carries:

- run ID;
- opening operation ID;
- transaction ID;
- strongbox instance ID;
- strongbox definition ID;
- commitment ID;
- reward profile ID;
- content-definition fingerprint; and
- optional expected sequence.

`StrongboxOpeningResultV1` represents these future outcomes:

- `Opened`;
- `ExactDuplicateNoChange`;
- `ConflictingDuplicate`;
- `InvalidRequest`;
- `StrongboxNotOwned`;
- `InsufficientCapacity`; and
- `ExpectedSequenceConflict`.

Opened results advance sequence by exactly one and carry a reward result plus
trace whose operation IDs match the opening operation. Exact duplicates carry
the previously accepted reward and trace without advancing sequence. Rejections
carry neither and do not advance sequence.

Ownership checks, box removal, generation, mandatory scrap policy, atomic reward
application, and persistence belong to BOX-001, INV-001, GEN-001, and RAP-001.

## Canonical ordering and fingerprints

All collection inputs are copied, validated, and sorted by explicit StableId or
trace ordinal. Caller collection order, dictionary order, current culture,
ambient time, Unity state, and random state do not affect canonical text.

Fingerprints use:

```text
sha256:<64 lowercase hexadecimal characters>
```

The preimage is the complete canonical UTF-8 text, including type-discriminating
field names, enum numeric values, list counts, stable IDs, quantities, weights,
probabilities, and nested canonical values.

Canonical equality is structural equality. Fingerprints are evidence and
idempotency payload identities; they are not random seeds and are not permission
to mutate an authority.

## Representable examples

These examples describe shapes, not production balance:

- money-only: one guaranteed `Money` grant;
- strongbox-only: one guaranteed `Strongbox` grant;
- miscellaneous-only: one or more `PremiumAmmo` / `Miscellaneous` grants;
- mixed: money, scrap, strongbox, equipment reference, ammunition, and
  miscellaneous grants in the same profile/result;
- guaranteed plus chance plus exclusive: all three profile sections populated;
- no-drop: explicit profile/result disposition;
- source-specific guaranteed box: append one guaranteed strongbox grant;
- source suppression: `NoReward` override;
- complete source replacement: `ReplaceEntirely` override.

## Validation failures

Construction rejects at least:

- null durable IDs;
- undefined enum values;
- zero or negative quantities;
- quantity maximum below minimum;
- probabilities outside `1..1,000,000`;
- zero or negative weights;
- empty exclusive groups;
- accidental empty configured profiles/results;
- null collection entries;
- duplicate roll, group, outcome, trace, or grant identities;
- duplicate trace ordinals;
- malformed fingerprint text;
- invalid override data shapes; and
- mismatched operation identity in strongbox result envelopes.

## Non-goals

Rewards v1 does not implement:

- random number generation or sampling;
- production probabilities, quantities, prices, tiers, or item lists;
- equipment definitions or generated equipment internals;
- wallets, ledgers, holdings, source-claim stores, or commitment lifecycle;
- reward application, pickups, shops, crafting, upgrades, or strongbox runtime;
- persistence, migration, UI, ScriptableObjects, prefabs, scenes, or Unity adapters.
