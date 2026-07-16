# Shared reward and equipment generator v1

## Status and ownership

`GEN-001` owns the single engine-independent deterministic generation service used
by later drop, strongbox, shop, random-crafting, test, and simulation callers.
The implementation is split between:

- `ShooterMover.Domain.Rewards.Generation` for immutable generation policy and
  explainable trace values; and
- `ShooterMover.Application.Rewards.Generation` for reward-profile resolution and
  immutable equipment-instance generation.

The service consumes, without redefining, the accepted `REW-001`, `EQP-001`,
`RNG-001`, and `PRG-001` contracts. It has no `UnityEngine` dependency and owns no
catalog assets, production balance, wallet, inventory, pickup, strongbox, shop,
crafting, persistence, scene, or simulator state.

## Deterministic request boundary

Every reward request carries:

- the complete `RewardOperationRequestV1` identity envelope;
- the matching immutable `RewardProfileV1`;
- one immutable `ProgressionContext`;
- an explicit unsigned root seed and RNG algorithm version; and
- optional canonically ordered values for authored `SourceTier` or `Custom`
  scaling descriptors.

Every equipment request carries:

- one operation StableId and one destination equipment-instance StableId;
- one immutable `EquipmentGenerationPolicyV1`;
- one already validated `EquipmentCatalog`;
- one immutable `ProgressionContext`; and
- an explicit unsigned root seed and RNG algorithm version.

Caller collection order is not significant. Policy candidates and explicit scaling
values are copied, validated, deduplicated, and sorted by StableId before use.
No request reads clock, frame, scene, object-name, dictionary-order, Unity random,
or ambient `System.Random` state.

## Reward-profile behavior

A configured reward profile is evaluated in canonical contract order:

1. every guaranteed specification is admitted;
2. every independent roll uses its own named chance substream;
3. every exclusive group selects exactly one weighted outcome; and
4. every admitted grant samples its quantity through a grant-specific quantity
   substream.

An explicit-no-drop profile creates an explicit-no-drop `RewardResultV1`. A
configured profile whose optional decisions produce no grants also creates an
explicit-no-drop result; an empty successful grant list is never constructed.

Quantity ranges are positive and inclusive. Version 1 resolves scaling descriptors
as deterministic additive integer inputs after the authored base quantity is
sampled:

| Descriptor kind | Resolved input |
|---|---|
| `CharacterLevel` | `ProgressionContext.CharacterLevel` |
| `RegionLevel` | `ProgressionContext.RegionLevel` |
| `Difficulty` | `ProgressionContext.DifficultyValue` |
| `SourceTier` | matching explicit request value |
| `Custom` | matching explicit request value |

A missing explicit value or checked quantity overflow returns the deterministic
`ImpossiblePolicy` status with no partial reward result. The service does not
silently substitute zero or retry another outcome.

## Equipment policy and generation

`EquipmentGenerationPolicyV1` contains only caller-authored policy inputs:

- equipment definition candidates with explicit character/region ranges,
  required progression tags, generated item-level range, nominal activation
  level, base weight, and positive source bias;
- quality candidates with arbitrary StableIds, nominal availability levels, and
  positive weights;
- augment candidates with explicit character-level ranges and positive weights;
- configurable minimum/maximum augment slot counts and an exact-slot policy; and
- accepted soft-activation and obsolescence parameter objects.

There are no built-in three-quality, three-slot, three-tier, ten-level, or fixed
maximum-level assumptions.

Generation performs these pure steps:

1. filter equipment candidates from catalog definitions and progression context;
2. evaluate accepted progression eligibility and source-biased weights;
3. select one definition by deterministic weighted sampling;
4. sample an item level from the intersection of policy and definition ranges;
5. filter and weight supported quality candidates with the accepted quality
   availability curve;
6. sample a slot count within policy and definition capacity;
7. before each augment selection, build a provisional immutable instance and call
   `EquipmentCatalog.ValidateInstance`; and
8. select the compatible augment, sample authored tier and level ranges, construct
   the final immutable instance, and validate it again.

Compatibility, duplicate-definition, exclusion-group, slot-capacity, quality,
tier, and level rules therefore remain owned by `EQP-001`. GEN-001 does not copy
or weaken those rules. If a required slot has no valid candidate, or the final
instance is rejected, generation returns `ImpossiblePolicy`; it never retries
until a convenient result appears. If no equipment definition is eligible, the
status is `NoEligibleCandidate`.

## Named random substreams

All gameplay-affecting samples use `DeterministicRandom` versioned named forks.
Fork ordinals are deterministic FNV-1a projections of durable StableIds or StableIds
derived from the operation and slot index.

| Purpose StableId | Decision |
|---|---|
| `rng.reward-independent` | independent reward chance |
| `rng.reward-exclusive` | exclusive-group weighted outcome |
| `rng.reward-quantity` | inclusive grant quantity |
| `rng.equipment-candidate` | equipment-definition weighted selection |
| `rng.equipment-level` | generated item level |
| `rng.equipment-quality` | quality weighted selection |
| `rng.equipment-slots` | augment slot count |
| `rng.augment-selection` | compatible augment weighted selection |
| `rng.augment-tier` | augment tier |
| `rng.augment-level` | augment level |

Forking never advances a parent stream. Adding trace detail, inspecting RNG trace
state, adding an ineligible candidate, or adding a candidate filtered out by the
catalog cannot consume or shift another decision stream. A deliberate change to a
purpose ID or ordinal derivation is a generator compatibility change.

Weighted sampling uses the RNG contract's unbiased bounded integer sampler.
Positive floating progression weights are deterministically projected to integer
millionths, rounded to nearest with a minimum of one. Non-finite values, overflow,
and totals outside the signed trace domain fail closed.

## Explainable trace format

`RewardGenerationTraceV1` canonical text starts with:

```text
schema=reward-generator-trace-v1
algorithm_version=<integer>
root_seed=<unsigned invariant decimal>
content_fingerprint=sha256:<64 lowercase hex>
context_fingerprint=sha256:<64 lowercase hex>
result_fingerprint=sha256:<64 lowercase hex>
entry_count=<integer>
```

Entries follow in contiguous ordinal order. Each entry records:

```text
ordinal=<integer>
step_id=<StableId>
subject_id=<StableId>
decision=<versioned integer enum>
substream_purpose_id=<StableId or none>
substream_ordinal=<unsigned invariant decimal>
samples_consumed=<unsigned invariant decimal>
input_value=<signed invariant decimal>
output_value=<signed invariant decimal>
detail=<LF-escaped diagnostic text>
```

The decision vocabulary covers eligibility, weighted selection, independent
chance, exclusive selection, quantity, scaling input, quality, slot count,
augment selection/tier/level, explicit no-drop, validation, and grant production.
Trace construction and fingerprinting occur after gameplay sampling and consume no
random values.

Reward generation also emits the accepted `RewardTraceV1` projection so later
reward commitment/application code can retain the shared REW-001 trace contract.
The richer GEN-001 trace supplements that projection with RNG purpose, ordinal,
and consumption evidence.

## Fingerprints

All GEN-001 content, context, result, failure, and trace fingerprints use:

```text
sha256:<64 lowercase hexadecimal characters>
```

- Reward content covers the accepted operation content fingerprint and complete
  reward profile fingerprint. Context, root seed, algorithm version, and explicit
  scaling values remain separate request/trace inputs.
- Equipment content covers the complete policy canonical text and validated
  catalog canonical text.
- Context is the exact `ProgressionContext.Fingerprint`.
- A successful reward result uses `RewardResultV1.Fingerprint`.
- A successful equipment result covers the complete canonical request and complete
  immutable equipment value.
- Rejections fingerprint the complete request, explicit status, and reason.
- The trace fingerprint covers the complete header and ordered entries.

Gameplay result fingerprints do not include diagnostic trace detail, so improving
trace wording cannot change a generated reward or equipment commitment. Content
fingerprints intentionally change when authored policy or catalog content changes.

## Failure and rollback

The service has four explicit statuses:

- `Generated`;
- `ExplicitNoDrop`;
- `NoEligibleCandidate`; and
- `ImpossiblePolicy`.

Failures return no partial reward or equipment value. Their reason, content
fingerprint, context fingerprint, result/failure fingerprint, and trace remain
stable for equal inputs. Rollback consists of restoring the previous caller-owned
policy/catalog/profile data while replaying the same RNG algorithm version and
root seed.

## Verification lock

Focused EditMode tests cover:

- exact equality for equal inputs and traces;
- a frozen reward quantity/result fingerprint vector;
- canonical input-order independence;
- guaranteed, independent, weighted-exclusive, quantity, scaling, and no-drop
  behavior;
- named-substream isolation from ineligible or incompatible candidates;
- low/high progression contexts and no-eligible status;
- catalog compatibility filtering and impossible duplicate-augment prevention;
- final catalog validation; and
- quality rank, slot count, augment tier, and augment level values beyond common
  three/ten assumptions.
