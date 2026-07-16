# Progression Curves v1

## Status and scope

This contract freezes one pure, parameterized soft-progression curve family for
item eligibility, quality/tier availability, old-item retention, source weighting,
and delayed crafting availability.

The mathematics lives in `ShooterMover.Domain.Progression.Curves`. Immutable
cross-boundary observations live in
`ShooterMover.Contracts.Progression.Curves`. Neither assembly references
`UnityEngine`, scene state, a progression-context provider, generator code, or
Unity `AnimationCurve` assets.

This version defines shape and validation only. Every numerical value used below is
an explicit caller-supplied parameter. There are no production defaults or hidden
maximum character levels, item levels, tier counts, quality counts, or augment
levels.

## Level domain

Character/current, item, nominal activation, quality, and crafting levels are
non-negative signed 64-bit integers. Negative levels fail closed. Relative level
differences are computed before conversion to `double`, preserving nearby
differences even when absolute levels are very large.

The curve output is deterministic for equal inputs. Availability and retention are
in `[0, 1]`; eligibility/source results are non-negative weights and may exceed one.
Weights are not probabilities until a later owning generator normalizes or samples
them.

## Soft activation

`SoftActivationCurveParameters` contains:

- early-tail weight `e`, finite with `0 < e < 1`;
- early-tail span `Learly`, a positive level count;
- post-nominal activation span `Lpost`, a positive level count.

For current level `C` and nominal activation level `N`, define relative level:

```text
r = C - N
start = -Learly
end = Lpost
```

Then:

```text
r <= start: availability = e
r >= end:   availability = 1
otherwise:
    t = (r - start) / (end - start)
    smooth = t^2 * (3 - 2t)
    availability = e + (1 - e) * smooth
```

Consequences:

- an item has a configurable, strictly positive chance/weight before nominal
  activation;
- there is no hard unlock boundary;
- the transition has zero slope at both endpoints;
- availability continues at one above the activation region; and
- the same family can represent item, quality, or arbitrary future tier growth.

`N` is an authored/catalog input owned by later definition tasks. This contract
does not decide what any nominal level should be.

## Old-item retention and decay

`ObsolescenceCurveParameters` contains:

- non-negative delay `Ddecay` before decay begins;
- finite positive half-life `H` in levels;
- finite retention floor `F` with `0 < F <= 1`.

For current level `C` and item level `I`:

```text
age = C - I - Ddecay
```

If `age <= 0`, retention is one. Otherwise:

```text
retention = F + (1 - F) * 0.5^(age / H)
```

The result is clamped defensively to `[F, 1]`. Old items therefore decay smoothly
but never disappear from eligibility solely because of age.

## Item eligibility weight

`ItemEligibilityCurveParameters` combines:

- one soft-activation parameter set;
- one obsolescence parameter set;
- finite positive base weight `B`; and
- finite positive source bias `S`.

The weight is:

```text
activation(C, nominal) * retention(C, item) * B * S
```

A positive source bias can raise or lower a candidate's relative weight but cannot
create a zero-probability gate. The result remains a weight; source bias does not
clamp it to one.

`ApplySourceBias(weight, S)` is also exposed for sources/boxes that apply bias after
other later-owned weighting steps. It requires a finite non-negative input weight
and finite positive bias.

## Quality and tier availability

`EvaluateQualityAvailability` uses the exact soft-activation formula with a
caller-supplied nominal quality/tier level. The API accepts arbitrary level values
and does not encode a fixed number of qualities or tiers.

A later generator must supply stable candidate ordering and use deterministic
random substreams. This curve family only computes availability values.

## Delayed crafting availability

`CraftingAvailabilityCurveParameters` contains:

- one soft-activation parameter set; and
- positive crafting delay `Dcraft`.

Crafting evaluates the same activation family at a shifted nominal level:

```text
crafting availability(C, N) = activation(C, N + Dcraft)
```

The implementation evaluates this as a relative-level subtraction to avoid integer
overflow. Because `Dcraft` must be positive and the activation function is
monotonic, crafting availability is always behind or equal to natural availability
at a given level and reaches the same mature value later. The early tail remains
positive; delay is soft availability, not an absolute hard gate.

Recipe ownership, scrap cost, transaction admission, guarantees, and whether a
product additionally requires a threshold are later crafting decisions.

## Immutable evaluation contract

`ProgressionCurveEvaluation` is an immutable cross-boundary snapshot containing:

- current level;
- nominal activation level;
- natural availability;
- old-item retention;
- source-biased weight; and
- crafting availability.

It validates value domains but contains no alternate formulas. Runtime services and
the simulator may carry this shape in explainable traces while invoking the exact
domain mathematics.

## Validation and failure behavior

The following fail closed with argument exceptions:

- negative levels;
- zero or negative early/post activation spans;
- zero, one, negative, NaN, or infinite early-tail weights;
- zero, negative, NaN, or infinite half-life;
- zero, negative, NaN, or infinite retention floor, or a floor above one;
- zero, negative, NaN, or infinite base/source weights;
- zero or negative crafting delay; and
- null parameter objects.

A non-finite result caused by multiplying extreme caller-supplied weights throws
instead of silently returning infinity.

## Versioning and rollback

Changing formulas, endpoint behavior, validation domains, or the meaning of any
parameter requires a new reviewed curve contract version. Production tuning may
change authored parameter values without changing this algorithm, but it must not
replace these functions with copied product-specific mathematics.

Rollback consists of restoring prior parameter data while continuing to invoke the
same versioned formulas. Generated commitments and simulator reports should record
the relevant parameter/content fingerprint through their later-owned trace formats.

## Non-goals

No production balance values, XP authority, progression-context provider, item
catalog, generator, strongbox, shop, crafting transaction, wallet, holdings,
Unity curve, prefab, scene, or Stage 1 integration is included.
