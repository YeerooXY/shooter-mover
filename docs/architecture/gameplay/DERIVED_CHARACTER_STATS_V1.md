# DERIVED_CHARACTER_STATS_V1

## Goal

`DERIVED-STATS-001` adds one engine-neutral, stateless composition service for permanent character statistics and immutable run-start combat profiles.

The service does not own equipment, augments, skills, account progression, events, or mutable combat state. Existing authorities project their accepted effects into `RuntimeModifierSnapshotV1` and provide their own immutable input fingerprint. The derived layer consumes those projections and never reinterprets subsystem state.

## Contracts

- `CharacterBaseStatProfileV1` carries the data-defined class identity, level, authoritative class/level base values, and the source definition fingerprint.
- `DerivedStatModifierSourceV1` carries one upstream authority fingerprint and its existing `RuntimeModifierSnapshotV1` projection.
- `DerivedStatPolicyV1` declares defaults, required explicit bases, clamps, and whole-number constraints.
- `DerivedCharacterStatInputV1` contains permanent character inputs only.
- `DerivedCharacterStatsSnapshotV1` is the immutable permanent derived snapshot.
- `RunCombatProfileInputV1` adds run context, event/run modifier sources, and active condition IDs.
- `RunCombatProfileV1` is the immutable run-start profile.
- `IDerivedCharacterStatComposerV1` / `DefaultDerivedCharacterStatComposerV1` perform full deterministic recomputation.

Derived output is a projection. It must not be persisted as primary character truth.

## Stable targets

The default policy includes stable targets for:

- maximum health, movement speed, and armor;
- physical, energy, thermal, and chemical resistance channels;
- outgoing damage, critical chance, and critical multiplier;
- healing output and received healing;
- contact damage and knockback;
- weapon and ability capacity;
- weapon damage, fire-rate, and reload-speed multipliers;
- reward, drop, and strongbox-drop modifiers.

Target identities remain open strings. A future ordinary stat is added by authoring a target and explicit policy rule, not by adding a calculator branch.

## Deterministic composition

Permanent sources are ordered by numeric priority, source ID, upstream fingerprint, and modifier fingerprint. The documented built-in priority lanes are:

1. class and level;
2. equipment;
3. augments;
4. skills;
5. account progression;
6. achievements;
7. events;
8. run conditions.

Class/level values form the base profile. All permanent modifier snapshots are merged and evaluated through the existing runtime-modifier rule:

```text
(base + sum(flat)) * (1 + sum(percentage)) * product(multiplicative)
```

Run-only event and condition modifiers are then evaluated as a second explicit layer over the immutable permanent snapshot. This keeps permanent inputs separate from run-local active conditions and makes the ordering auditable.

Dictionary and input enumeration order do not affect fingerprints or values. Every output fingerprint includes the class/base definition, level, policy, upstream authority fingerprints, modifier fingerprints, active condition IDs where applicable, and final sorted values.

## Policy and failure behavior

`DerivedStatPolicyV1` owns all clamps. The default policy requires explicit class/level bases for maximum health and movement speed, clamps critical chance to `0..1`, resistance channels to `-1..0.95`, capacities to `0..64`, and other quantities to documented broad safety bounds.

The calculator fails closed when:

- a base or modifier target has no policy rule;
- a required base value is missing;
- a capacity resolves to a fractional value;
- a permanent character source contains a conditional modifier;
- a run profile uses a policy different from the policy that produced its character snapshot.

Class-specific skill caps, rank curves, prerequisites, installation rules, and respec authority remain upstream. The derived service consumes the resulting `SkillEffectSnapshotV2` adapter projection and does not recalculate or bypass those rules.

## Lifecycle and caching

The calculator is stateless and performs a full rebuild. A composition owner may cache by `InputFingerprint`, but must invalidate and recompute at authoritative lifecycle boundaries such as:

- level or class/base-definition changes;
- equipment or exact-instance loadout changes;
- augment changes;
- skill allocation, installation, mastery, or respec changes;
- account/achievement modifier changes;
- event calendar or active run-condition changes;
- player/run initialization and restart.

Do not poll or recompute these values per frame. Runtime proc evaluation, active ability execution, critical-hit RNG, damage application, status-effect lifetime, Unity HUD changes, and multiplayer replication remain downstream.

## Verification

Focused EditMode filter:

```text
ShooterMover.Tests.EditMode.Characters.Stats.DerivedCharacterStatsV1Tests
```

Coverage includes deterministic fingerprints, source/input ordering, core stat stacking, event-condition separation, explicit clamping, whole-number capacities, exact equipment fingerprint sensitivity, skill projection consumption, respec rebuilding, conditional-source rejection, and unknown-target failure.
