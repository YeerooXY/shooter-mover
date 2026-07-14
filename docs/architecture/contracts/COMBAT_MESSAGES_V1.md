# Combat Messages v1

## Status and scope

Combat Messages v1 defines the immutable, engine-independent messages shared by
movement, weapons, enemies, HUD, and diagnostics. The implementation lives in
`ShooterMover.Contracts.Combat` and references only the plain-C# Domain
assembly for `StableId`.

This contract describes validated combat facts and outcomes. It does not apply
damage, perform collision detection, simulate weapons, tick status effects,
choose random modifiers, or own mutable combat state.

## Identity envelope

Every combat event message implements `ICombatEventMessage` and carries:

- `EventId`: a `StableId` identifying one logical combat event;
- `SourceId`: the producer or attacker identity;
- `TargetId`: the intended or affected target identity;
- `Channel`: one declared `CombatChannel` value.

Null IDs and undefined enum values are rejected. Self-targeting is not rejected
because explicit self-damage, self-status, and system snapshots may be valid.

`CombatEventIdentity.Classify` compares two envelopes:

- different event IDs are `Distinct`;
- the same event ID with the same source, target, and channel is `Duplicate`;
- the same event ID with a different source, target, or channel is
  `ConflictingDuplicate`.

The helper classifies identity only. A consumer that persists or caches full
payloads must still reject a duplicate whose payload differs from the accepted
payload. Event IDs are not inferred from frame number, object reference, or
Unity instance ID.

## Channels

Combat Messages v1 accepts these explicit channels:

- `Kinetic`
- `Thermal`
- `Electrical`
- `Explosive`
- `Contact`
- `Environmental`
- `System`

Zero and all undeclared numeric values are invalid. `System` is allowed for
non-damage snapshots and authored system messages, but `DamageMessage` rejects
it as a damage-bearing channel. `WeightMessage` requires `Contact`.

Adding, removing, renaming, or changing the meaning of a channel requires a new
contract version or an explicit compatible extension review.

## Vital state

`VitalState` is a validated immutable snapshot with:

- current and maximum health;
- current and maximum shield;
- derived destruction state (`Health == 0`).

Rules:

- all values must be finite;
- current and maximum values cannot be negative;
- maximum health must be positive;
- current health cannot exceed maximum health;
- current shield cannot exceed maximum shield;
- a destroyed state cannot retain shield in v1.

There is no writable `IsDestroyed` flag. Destruction is derived, preventing a
payload from claiming both positive health and destruction or zero health and
an active state.

`VitalMessage` pairs a snapshot with `VitalResult.Active` or
`VitalResult.Destroyed`; the declared result must match the snapshot.

## Damage results and shield overflow

`DamageMessage` records a supplied result. It does not calculate or apply that
result. Producers provide:

- requested damage;
- `Before` and `After` vital snapshots;
- shield damage applied;
- shield overflow amount;
- health damage applied;
- unapplied amount;
- one `DamageResult`.

`DamageResult` is one of:

- `Applied`
- `Blocked`
- `DuplicateEventIgnored`
- `TargetAlreadyDestroyed`

For `Applied`, these equalities must hold exactly:

```text
shield damage applied = before shield - after shield
health damage applied = before health - after health
requested damage = shield damage applied + shield overflow
shield overflow = health damage applied + unapplied damage
```

Additional rules:

- maximum health and maximum shield cannot change inside one damage result;
- health and shield cannot increase;
- at least one of health or shield must change;
- overflow cannot be reported while shield remains;
- health damage cannot be reported while shield remains.

Representative shield-overflow result:

```text
requested:              13
before:                  health 20/20, shield 5/5
shield damage applied:   5
shield overflow:         8
health damage applied:   8
unapplied:               0
after:                   health 12/20, shield 0/5
result:                  Applied
```

Representative overkill result:

```text
requested:              30
before:                  health 20/20, shield 5/5
shield damage applied:   5
shield overflow:         25
health damage applied:   20
unapplied:               5
after:                   health 0/20, shield 0/5
result:                  Applied
```

For `Blocked`, `DuplicateEventIgnored`, and `TargetAlreadyDestroyed`, before
and after snapshots are identical, applied and overflow quantities are zero,
and the complete request is `UnappliedAmount`. `TargetAlreadyDestroyed`
requires an already destroyed before-state. This gives late projectiles,
contact callbacks, and delayed effects an explicit terminal outcome instead of
silently dropping them or applying them twice.

The contract does not select resistances, armor, bypass rules, critical hits,
difficulty multipliers, random modifiers, or damage order. A future mechanic
that requires shield bypass or another incompatible invariant must extend or
version the contract rather than weakening v1 validation.

## Hits

`HitMessage` represents geometric or authored hit resolution separately from
damage application. Its result is one of:

- `Confirmed`
- `Blocked`
- `Missed`
- `DuplicateEventIgnored`
- `TargetAlreadyDestroyed`

A confirmed hit may lead to a `DamageMessage`, `StatusMessage`, both, or neither
according to the owning combat rule. Sharing an event ID is allowed when the
producer intentionally treats those messages as one logical event; consumers
must use the documented producer convention consistently.

## Contacts

`ContactMessage` carries one `ContactClassification`:

- `BodyImpact`
- `SustainedBodyContact`
- `ProjectileImpact`
- `AreaOverlap`
- `HazardOverlap`

Its result is one of:

- `Accepted`
- `GracePeriodIgnored`
- `BlockedByWeight`
- `DuplicateEventIgnored`
- `TargetAlreadyDestroyed`

This separates contact classification from consequences. Movement can report a
body impact and grace-period rejection without deciding damage. A projectile
can report projectile impact before a weapon rule emits damage. Hazards and
areas do not masquerade as body collisions.

## Weight comparisons

`WeightMessage` represents one contact-time comparison between:

- `Light`
- `Standard`
- `Heavy`
- `Immovable`

The explicit result must agree with the two classes:

- `SourceLighter`
- `Equal`
- `SourceHeavier`
- `TargetImmovable`

A non-immovable source against an immovable target always reports
`TargetImmovable`. The message does not apply shove, recoil, reflection, stun,
or damage. Movement and enemy code consume the result to decide their own
accepted behavior without creating competing weight DTOs.

## Status results

`StatusMessage` carries one known `CombatStatus`:

- `Stunned`
- `Slowed`
- `Burning`
- `Marked`

Its result is one of:

- `Applied`
- `Refreshed`
- `Removed`
- `Resisted`
- `DuplicateEventIgnored`
- `TargetAlreadyDestroyed`

All durations and magnitudes must be finite and non-negative. Applied and
refreshed statuses require a positive duration. Removed, resisted, duplicate,
and late-target outcomes carry zero duration and zero magnitude. Status ticking,
stacking, immunity, refresh policy, and damage-over-time application remain
outside this contract.

Unknown status and result enum values are rejected rather than mapped to a
fallback status.

## Representative consumers

### Movement

Movement emits `ContactMessage` for body impacts and sustained contact. It may
emit or consume `WeightMessage` to distinguish light shove, heavy blocking, and
immovable geometry. Contact grace uses `GracePeriodIgnored`; it does not invent
a local contact-result type or directly mutate health.

### Weapons

A weapon or projectile adapter emits `HitMessage` with one stable event ID. The
owning combat rule may then publish a validated `DamageMessage` and optional
`StatusMessage`. Weapon cadence, projectile behavior, critical-hit policy, and
power-bank logic remain separate from the shared DTOs.

### Enemies

Enemy contact attacks, projectiles, area attacks, and hazards use the common
contact and hit classifications. Enemy destruction is represented through
`VitalMessage` and late callbacks use `TargetAlreadyDestroyed` rather than
reopening combat state.

### HUD

The HUD consumes `VitalMessage` and `DamageMessage` as read-only facts for
health, shield, damage numbers, destruction presentation, and shield-overflow
feedback. It does not calculate authoritative health or keep a competing mutable
vital object.

### Diagnostics

Diagnostics records event ID, source, target, channel, result, and validated
quantities. Duplicate and conflicting event identities can be surfaced without
logging Unity object references or reconstructing combat rules from presentation
state.

## Immutability and Unity boundary

All public message and value properties are getter-only. Constructors validate
all supplied values before assigning them. There is no mutable universal combat
object, property bag, dictionary payload, `UnityEngine.Object`, vector, collider,
scene reference, or ScriptableObject dependency.

The Contracts assembly has `noEngineReferences: true`; Unity adapters translate
engine callbacks into these messages at the boundary.

## Rejection summary

Combat Messages v1 rejects:

- null event, source, target, state, or message values;
- unknown channels, results, contact classes, weight classes, or statuses;
- zero or negative requested damage;
- negative, NaN, or infinite quantities;
- health or shield above capacity;
- zero or negative maximum health;
- destroyed health with remaining shield;
- contradictory before/after damage quantities;
- overflow or health damage while shield remains;
- non-applied outcomes that change vitals or report applied damage;
- late-hit results whose before-state is active;
- weight results that disagree with declared weight classes;
- applied/refreshed statuses with zero duration;
- terminal status results carrying duration or magnitude.

## Versioning and non-goals

Changing enum meanings, vital invariants, duplicate identity semantics, damage
accounting equations, contact classes, weight ordering, or status payload rules
requires a reviewed contract-version decision.

Combat Messages v1 does not implement:

- a damage system;
- collision or hit detection;
- resistance, armor, critical-hit, or shield-bypass rules;
- randomized weapon modifiers;
- status ticking or stacking;
- enemy or player mutable combat state;
- HUD, diagnostics, movement, weapon, or enemy implementations;
- persistence or journaling of ordinary damage ticks.
