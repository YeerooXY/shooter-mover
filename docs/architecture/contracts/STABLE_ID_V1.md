# StableId v1

## Status and scope

`StableId` v1 is the engine-independent identity primitive for content, rooms,
runs, events, transactions, persisted keys, and generated registry entries.
This contract defines representation and comparison only. It does not create
content, allocate runtime instance IDs, generate registries, or define gameplay
behavior.

The implementation lives in `ShooterMover.Domain.Common` and must not reference
`UnityEngine`.

## Canonical representation

A StableId is one namespace and one namespace-local value joined by exactly one
ASCII dot:

```text
<namespace>.<value>
```

Both components use the same grammar:

```text
stable-id = component "." component
component = segment *("-" segment)
segment   = 1*(lowercase-ascii / digit)
lowercase-ascii = "a" ... "z"
digit           = "0" ... "9"
```

Consequences:

- input is ASCII and lowercase only;
- the dot is the only namespace/value separator;
- a single hyphen separates non-empty segments inside either component;
- leading, trailing, or repeated hyphens are invalid;
- whitespace, underscores, slashes, backslashes, colons, percent escapes,
  Unicode letters, and additional dots are invalid;
- parsing never trims, case-folds, decodes, or otherwise normalizes input.

No two textual spellings are aliases. In particular, an uppercase or mixed-case
form is rejected rather than converted, preventing case-only duplicate IDs.

## Length boundaries

Lengths count ASCII characters in the canonical text.

| Part | Minimum | Maximum |
|---|---:|---:|
| Namespace | 1 | 32 |
| Value | 1 | 96 |
| Complete canonical StableId | 3 | 128 |

The complete limit is checked independently. Therefore a 32-character namespace
plus a 96-character value is invalid because the separating dot makes the full
form 129 characters.

## Frozen examples

Valid frozen examples from the accepted content plan include:

```text
weapon.blaster-machine-gun
enemy.pursuer-drone
factory.teleport-b-shop
```

Representative invalid forms include:

```text
Weapon.blaster-machine-gun   # uppercase ambiguity
weapon.blaster_machine_gun   # underscore separator
weapon.blaster--machine-gun  # empty hyphen segment
weapon..blaster              # extra dot / empty component
weapon.enemy/../boss         # traversal-like path syntax
weapon.%2e%2e                # encoded traversal-like syntax
```

## API and immutability

`StableId` is a sealed immutable value object. Its constructor is private, so a
non-null instance can exist only after validation by:

- `StableId.Parse(string)` for a complete canonical form;
- `StableId.TryParse(string, out StableId)` for non-throwing validation; or
- `StableId.Create(string namespaceName, string value)` for separated
  components.

`Parse` and `Create` throw `ArgumentNullException` for null arguments and
`FormatException` for all non-canonical values. `TryParse` returns `false` and a
null result for every invalid input, including null.

`Namespace` and `Value` expose the validated components. `ToString()` returns
the complete canonical form. Every accepted value must satisfy:

```text
StableId.Parse(id.ToString()).Equals(id)
```

## Equality and hashing

Equality is exact ordinal equality of the complete canonical string. There is no
culture-sensitive comparison and no case-insensitive mode.

`GetHashCode()` is the deterministic 32-bit FNV-1a hash of the canonical ASCII
form, using offset basis `2166136261` and prime `16777619`. Equal StableIds
therefore always have equal hashes across supported runtimes. The canonical
string remains the persisted identity; callers must not treat a 32-bit hash as
a unique or collision-free ID.

## Deterministic ordering

`CompareTo` and the relational operators compare the complete canonical strings
with ordinal comparison. Because the grammar is ASCII-only, this is equivalent
to unsigned byte-wise ordering of the canonical UTF-8 text and is independent
of locale. A non-null StableId sorts after null.

Sorting the frozen sample set produces:

```text
enemy.pursuer-drone
weapon.arc-gun
weapon.blaster-machine-gun
weapon.shotgun
```

Comparison returns zero exactly when equality is true.

## Rejection and safety rules

Reject rather than repair:

- null, empty, or whitespace-only input;
- leading, trailing, or embedded whitespace;
- uppercase and mixed-case forms;
- missing, repeated, leading, or trailing dot separators;
- leading, trailing, or repeated hyphen separators;
- path/traversal syntax such as `/`, `\`, `..`, or percent-encoded variants;
- non-ASCII and Unicode lookalikes;
- any component or canonical text outside the declared length boundaries.

These rules make IDs safe to compare and serialize as data keys. They do not by
themselves authorize using an ID as a filesystem path; filesystem consumers
must still use their own path containment rules.

## Versioning and non-goals

Changing grammar, length limits, normalization, equality, hashing, or ordering
requires a new StableId contract version and an explicit migration strategy.

StableId v1 does not add content assets, product IDs, generated registries,
gameplay logic, persistence implementation, or Unity serialization behavior.
