# RUNTIME_MODIFIERS_V1

## Goal

Ordinary additions should be content, not controller edits.

The shared modifier language supports numerical contributions from:

- ranked skills;
- gear and augments;
- class definitions;
- achievements;
- active special events;
- temporary combat effects;
- party or multiplayer auras;
- difficulty and mission modifiers.

Targets are stable open string identities rather than an enum. Adding `combat.critical-chance`, `rewards.strongbox-drop-weight`, or another future statistic does not require modifying the modifier engine.

## Evaluation

For one target, applicable contributions are evaluated deterministically as:

```text
(base + sum(flat)) * (1 + sum(percentage)) * product(multiplicative)
```

An optional minimum and maximum may clamp the final value. Probability consumers should normally request `0..1` bounds.

Conditional modifiers are applied only when their `conditionId` is active.

## Existing skill integration

`SkillEffectModifierAdapterV1` translates the existing `SkillEffectSnapshotV2` projection into `RuntimeModifierSnapshotV1`.

A crit-chance skill therefore needs an ordinary skill effect such as:

```text
target: combat.critical-chance
operation: flat
value: 0.01 per rank
```

No crit-specific skill controller is required. The combat resolver is responsible for consuming the final derived crit chance and for deterministic critical-hit rolls.

## Killing-spree and other fact-window conditions

`FactWindowConditionAuthorityV1` observes immutable typed facts and activates reusable conditions.

Example:

```text
condition: condition.killing-spree
fact type: fact.enemy-killed
required count: 3
window: 300 simulation ticks
active duration: 360 simulation ticks
```

A skill or item can then contribute a modifier conditioned on `condition.killing-spree`.

The same runtime can express:

- kills inside a time window;
- props destroyed inside a time window;
- teammates healed inside a time window;
- objectives completed inside a time window;
- any future immutable fact type.

Observed facts are idempotent by fact ID. Exact replay changes nothing; conflicting reuse is rejected; ticks must be non-decreasing.

## Special events

An event does not rewrite item definitions or enemy drop tables. It contributes conditional modifiers to the immutable generation context.

Example:

```text
source: event.double-drops-2026
target: rewards.strongbox-drop-weight
operation: multiplicative
value: 2.0
condition: event.double-drops-active
```

The reward system records the modifier snapshot and active-condition fingerprint used for the roll, preserving deterministic replay and auditability.

## Boundaries

This foundation does not yet:

- perform critical-hit RNG;
- alter BOX, DROP, GEN, enemy, or weapon algorithms directly;
- source the current event calendar;
- persist temporary fact-window state;
- replicate conditions over multiplayer;
- define final production balance.

Those consumers should depend on the modifier snapshot rather than maintain separate bonus calculations.

## Extension rule

Using an existing target and condition mechanism should require only definitions and focused validation.

A genuinely new condition family may add one reusable condition authority and register it once. It must not add one gameplay controller per skill, item, enemy, or event.

## Verification

Focused EditMode filter:

```text
ShooterMover.Tests.EditMode.Modifiers.RuntimeModifierFoundationV1Tests
```
