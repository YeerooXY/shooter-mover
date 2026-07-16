# Progression Context v1

## Status and scope

`PRG-001` defines the single engine-independent progression context consumed by
runtime gameplay, reward generation, drops, shops, crafting, upgrades, tests,
and balancing simulation.

This contract supplies context values and provider boundaries only. It does not
implement XP, calculate levels, choose balance curves, discover a player in a
scene, save data, or create Unity content.

The implementation is split across the existing inward-only assemblies:

- `ShooterMover.Domain` owns `ProgressionContext` and explicit validation;
- `ShooterMover.Contracts` owns the read-only provider port, immutable session
  snapshot, and replacement change fact; and
- `ShooterMover.Application` owns direct and mutable session providers.

All three layers remain free of `UnityEngine` references.

## Context model

One immutable `ProgressionContext` contains:

```text
character_level
region_level
difficulty_id
difficulty_value
progression_tags[]
```

### Character and region levels

`character_level` and `region_level` are non-negative signed 32-bit integers.
Zero is a valid explicit value for authored bootstrap, tests, simulations, or a
product policy that begins counting from zero. Negative values are rejected.

The contract imposes no gameplay maximum. `int.MaxValue` is accepted. The CLR
integer boundary is a representation boundary, not a balance cap or an
assertion that production content should reach that value.

A future XP or leveling authority may supply these values through the provider
port, but it must not change this context's ownership or infer values from
presentation state.

### Difficulty

Difficulty has two explicit fields:

- `difficulty_id` is a non-null canonical `StableId`;
- `difficulty_value` is a non-negative integer available to numerical consumers.

The identity lets authored systems distinguish named modes without using display
text. The numeric value lets curves consume an explicit coordinate without
switching on scene names or UI labels. No namespace or production difficulty
catalog is selected by this contract.

### Progression tags

Tags are optional canonical `StableId` values.

- A null tag collection means no tags.
- A null entry is invalid and rejects the complete candidate.
- Exact duplicate IDs are collapsed.
- Remaining IDs are sorted by `StableId` ordinal canonical ordering.
- The input collection is copied before publication.

Consequently, tag order and duplicate spelling in caller input cannot change
context equality, canonical text, hashing, or fingerprinting.

## Explicit validation

Use:

```csharp
ProgressionContext.TryCreate(..., out context, out validation)
```

for non-throwing admission. The stable validation codes are:

```text
None
ContextMissing
CharacterLevelNegative
RegionLevelNegative
DifficultyIdentityMissing
DifficultyValueNegative
ProgressionTagMissing
```

`ProgressionContext.Create(...)` is a convenience boundary for already trusted
inputs and throws `ArgumentException` with the same field and message when
validation fails.

Validation is engine-independent. It does not consult assets, scenes, clocks,
random state, player objects, HUD text, or balance curves.

## Canonical snapshot and fingerprint

`ProgressionContext.ToCanonicalString()` emits LF-only ordered text:

```text
schema=progression-context-v1
character_level=<invariant decimal>
region_level=<invariant decimal>
difficulty_id=<canonical StableId>
difficulty_value=<invariant decimal>
tag_count=<invariant decimal>
tag=<first canonical StableId>
tag=<next canonical StableId>
...
```

The ordered tags are part of the canonical text. Decimal conversion uses
`CultureInfo.InvariantCulture`; identity comparison is ordinal.

`ProgressionContext.Fingerprint` is:

```text
sha256:<64 lowercase hexadecimal characters>
```

computed from the UTF-8 bytes of the canonical text. Equality compares canonical
text ordinally. `GetHashCode()` uses deterministic 32-bit FNV-1a over that same
text and is suitable for in-memory hash collections, not durable identity.

`ProgressionContextSnapshot` pairs one context with a non-negative session
sequence. Its canonical text and SHA-256 fingerprint include both sequence and
the context fingerprint. Snapshot equality includes sequence and context.

## Provider port

Consumers may receive a `ProgressionContext` directly or depend on:

```csharp
public interface IProgressionContextProvider
{
    ProgressionContext CurrentContext { get; }
}
```

The port is read-only and contains no global discovery method.

### Direct provider

`DirectProgressionContextProvider` stores one required immutable context and
returns that exact instance. It is intended for simulations, tests, command
composition, and explicitly authored fixed sessions.

### Session provider

`SessionProgressionContextProvider` starts at sequence `0` and exposes:

```text
CurrentContext
CurrentSnapshot
TryReplace(raw explicit values)
TryReplace(valid immutable context)
```

Replacement is synchronized and produces one immutable
`ProgressionContextChangeFact`:

| Status | State effect | Sequence effect | Validation |
|---|---|---|---|
| `Applied` | current context becomes the accepted different context | increments exactly once | valid |
| `DuplicateNoChange` | exact equal context remains current | unchanged | valid |
| `Rejected` | previous context and snapshot remain current | unchanged | explicit failure |

An exact duplicate is equality-based, not reference-based. Rebuilding the same
context with a different input tag order or duplicate tags therefore remains a
no-change replacement.

Invalid raw values are validated before the provider lock is used to publish a
new snapshot. A failed candidate never partially mutates character level,
region level, difficulty, tags, sequence, or fingerprint.

The returned fact carries both previous and current immutable snapshots. For
`DuplicateNoChange` and `Rejected`, both references point to the unchanged
snapshot. No event bus, static singleton, service locator, or Unity lifecycle is
introduced.

## Consumer rules

Generation, drops, shops, crafting, upgrades, gameplay, tests, and simulation
must use either an explicit `ProgressionContext` parameter or
`IProgressionContextProvider`.

Consumers must not derive progression context from:

- scene object lookup;
- Unity player discovery;
- object, hierarchy, room, or scene names;
- HUD or localized display text;
- static mutable fields or singletons;
- clock, frame, or random state;
- simulator-only replacement models; or
- private copies of progression curves.

`RNG-001` owns random services and `Progression/Curves/**`. This contract neither
references nor implements those paths. Curve services may consume the context;
the context never calls a curve service.

## Deterministic examples

These inputs are equal despite order and duplicate differences:

```text
character_level=7
region_level=11
difficulty_id=difficulty.veteran
difficulty_value=3
tags=[progression-tag.zulu, progression-tag.alpha, progression-tag.zulu]
```

and:

```text
character_level=7
region_level=11
difficulty_id=difficulty.veteran
difficulty_value=3
tags=[progression-tag.alpha, progression-tag.zulu]
```

Their frozen context fingerprint is:

```text
sha256:ea4b009b80c92f5e98323526b3466b761c5bac43e084041573ec88ea879f85e5
```

## Verification obligations

The focused EditMode fixture covers:

- zero and `int.MaxValue` levels;
- negative character, region, and difficulty values;
- missing difficulty and null tag entries;
- tag deduplication, ordering, and input-copy immutability;
- equality, canonical text, deterministic hash, and frozen SHA-256 fingerprint;
- direct provider exact-instance behavior;
- applied session replacement and sequence increment;
- invalid replacement preserving the previous snapshot;
- exact duplicate replacement producing no change;
- null replacement rejection; and
- no Unity assembly, static provider, random, or progression-curve type on the
  public context/provider surface.

Repository layout, assembly graph validation, Unity cold compilation, and the
focused EditMode fixture remain required before the draft PR may become ready.

## Versioning

Changing any of the following requires a reviewed v2 contract or an explicit
compatible extension:

- field meaning or level domain;
- tag canonicalization or duplicate handling;
- canonical field order or text encoding;
- fingerprint algorithm or schema identifier;
- snapshot sequence semantics;
- duplicate replacement behavior; or
- provider ownership/discovery rules.

Adding production balance values, XP calculation, persistence, or Unity authoring
does not modify v1 automatically; each needs its separately owned task and must
continue to supply this same context boundary.

## Non-goals

Progression Context v1 does not add:

- XP gain or automatic leveling;
- player attributes or combat statistics;
- production level or difficulty catalogs;
- soft eligibility, quality, crafting, or retention curves;
- PRNGs or named random substreams;
- save files, migrations, profile state, or networking;
- UI, localization, scenes, prefabs, or ScriptableObjects;
- generator, drop, shop, crafting, upgrade, or reward logic; or
- Stage 1 integration changes.
