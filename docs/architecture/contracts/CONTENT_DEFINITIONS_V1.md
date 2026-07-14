# Content Definitions v1

## Status and scope

Content Definitions v1 is the engine-independent contract for identifying content definitions, declaring their typed dependencies, and returning deterministic structured validation results.

It consumes:

- StableId v1 for every definition, reference, provenance record, and tombstone key;
- Identity v1 as the downstream identity boundary: ordered validated descriptor text is registry fingerprint input, and the generated fingerprint becomes `ContentVersion.DefinitionFingerprint`;
- the accepted Stage 1 content plan and package taxonomy.

This contract does not create ScriptableObjects, packages, content assets, registries, generators, runtime instances, randomized modifiers, or mutable gameplay state.

## Accepted definition kinds

Content Definitions v1 is closed to these kinds:

| Enum | Canonical token | Intended use |
|---|---|---|
| `Weapon` | `weapon` | weapon package definitions such as `weapon.blaster-machine-gun` |
| `Enemy` | `enemy` | enemy package definitions such as `enemy.pursuer-drone` |
| `Room` | `room` | room definitions, including accepted `factory.*` room IDs |
| `Encounter` | `encounter` | benchmark, route, and later factory encounter definitions |
| `Environment` | `environment` | environment and bounded hazard definitions |
| `SharedModule` | `shared-module` | reusable authoring modules consumed by definitions |

`Unknown` is a sentinel only and is rejected. New kinds require a reviewed versioned contract extension; callers must not cast arbitrary integers into the enum. Source-art and license records are represented by a `ProvenanceId`, not by inventing an art-only content definition kind.

## Definition version

Version `1` is the only supported definition version.

A typed reference stores a positive expected version so future descriptors can be represented and rejected as `UnsupportedDefinitionVersion` rather than becoming malformed or silently downgraded. Validation never repairs, clamps, or substitutes versions.

The per-definition version is deliberately separate from Identity v1 `ContentVersion`:

- definition version controls compatibility of one descriptor contract;
- `ContentVersion.CatalogVersion` and `DefinitionFingerprint` identify one complete validated catalog snapshot.

Binding every internal reference to the complete catalog fingerprint would invalidate unrelated references after any catalog change, so v1 does not do that.

## `ContentReference`

A reference is immutable and contains exactly:

1. `DefinitionId` — canonical StableId;
2. `ExpectedKind` — one accepted definition kind;
3. `ExpectedVersion` — positive definition version.

Canonical line form is strict and ordered:

```text
definition_id=weapon.blaster-machine-gun
expected_kind=weapon
expected_version=1
```

Parsing rejects missing, reordered, extra, CRLF, unknown-kind, leading-zero, signed, zero, and malformed StableId fields. Equality and hashing cover all three fields. Canonical ordering is kind token, StableId, then expected version.

## `ContentDefinitionDescriptor`

A descriptor is an immutable registry input and contains:

- `DefinitionId`;
- `Kind`;
- `DefinitionVersion`;
- nullable `ProvenanceId` so a missing provenance record can remain a structured validation error;
- `IsPrototypeOnly`;
- an immutable, deduplicated, canonically ordered collection of typed references.

The constructor copies the supplied reference collection. Later mutation of the caller's list cannot change the descriptor. Duplicate typed references and null reference values are malformed descriptor construction and are rejected immediately.

`ToCanonicalString()` emits deterministic fields in this order:

1. definition kind;
2. definition ID;
3. definition version;
4. provenance ID or `null`;
5. prototype-only flag;
6. reference count;
7. zero-padded ordered reference entries.

This text is a future registry fingerprint input. CS-009 does not calculate `ContentVersion`, scan assets, or write generated output.

## Validation modes

`ContentValidationMode.Release` rejects prototype-only definitions.

`ContentValidationMode.Prototype` permits explicitly marked prototype-only definitions, while still enforcing identity, version, provenance, reference, duplicate, tombstone, and cycle rules.

A prototype flag is explicit metadata, not an implicit exception based on asset path or scene location.

## Resolution

`ContentValidationResult.TryResolve` succeeds only when all of the following are true:

- exactly one descriptor has the referenced StableId;
- the ID is not tombstoned;
- expected and actual kinds match;
- expected and actual versions are both supported v1;
- provenance is present;
- the descriptor is eligible for the validation mode.

It returns `false` for missing, duplicate, wrong-kind, unsupported, tombstoned, missing-provenance, and release-ineligible prototype definitions. It never chooses the first duplicate, coerces kind, substitutes another version, or falls back to a scene object.

## Structured error codes

The following failures remain distinct:

| Order | Code | Meaning |
|---:|---|---|
| 1 | `DuplicateDefinition` | more than one descriptor declares the same StableId |
| 2 | `MissingDefinition` | no descriptor or tombstone exists for a referenced ID |
| 3 | `WrongDefinitionKind` | the unique target exists but has a different kind |
| 4 | `UnsupportedDefinitionVersion` | a descriptor or reference requests a version other than supported v1, or versions do not match |
| 5 | `CyclicDependency` | one strongly connected dependency component contains multiple nodes or a self-reference |
| 6 | `MissingProvenance` | a descriptor has no provenance StableId |
| 7 | `TombstonedId` | a descriptor or reference uses an explicitly retired StableId |
| 8 | `PrototypeOnlyDefinition` | release validation contains a prototype-only definition |

Each `ContentValidationError` carries structured fields where relevant:

- source definition ID;
- referenced ID;
- expected and actual kind;
- expected and actual version;
- deterministic detail text;
- the canonically sorted member IDs of a cyclic strongly connected component.

Consumers must branch on `Code` and structured fields, not parse display prose.

## Error ordering

Validation materializes immutable descriptor and tombstone snapshots, then orders errors deterministically by:

1. the fixed error-code order above;
2. source definition StableId;
3. referenced StableId;
4. expected kind;
5. actual kind;
6. expected version;
7. actual version;
8. cycle members;
9. ordinal detail text.

Input descriptor order, dependency declaration order, dictionary iteration order, and tombstone input order cannot change the returned error sequence.

## Cycle semantics

Cycle detection runs over unique, non-tombstoned, supported descriptors and only exact kind/version reference edges. It uses strongly connected components:

- `A -> B -> A` produces one cyclic component containing `A` and `B`;
- `A -> A` is cyclic;
- missing, duplicate, wrong-kind, tombstoned, or unsupported edges do not create misleading secondary cycle errors.

Cycle members are sorted by canonical StableId before entering the result. The sorted component is evidence of membership, not a claim that one traversal order is the only possible cycle path.

## Adapter boundary

Unity content definitions may later be authored as ScriptableObjects, but those objects are adapters that produce these immutable descriptors. They are never stable identity, mutable runtime state, durable mission truth, or registry authority.

Package tasks contribute validated descriptor inputs only. CS-011 remains the sole generated-registry writer. Generated files are regenerated from inputs and never manually edited or merged.

## Representative examples

A weapon referencing a reusable module:

```text
definition_kind=weapon
definition_id=weapon.blaster-machine-gun
definition_version=1
provenance_id=provenance.blaster-machine-gun-source
prototype_only=false
reference_count=1
reference_0000=shared-module|module.automatic-projectile|1
```

A room may keep its accepted `factory.*` StableId while its typed kind is `Room`; StableId namespace and definition kind are related domain facts but are not required to be identical strings.

A retired `enemy.old-prototype` ID belongs in the supplied tombstone set. A reference to it returns `TombstonedId`, never `MissingDefinition`, so migrations and diagnostics can distinguish retired content from accidental absence.

## Rejection rules

Reject implementations that:

- store Unity objects, scene references, delegates, random state, runtime health, mutable collections, or package instances in these contracts;
- treat a StableId alone as sufficient when kind and version are expected;
- resolve duplicates by list order;
- classify tombstones as ordinary missing content;
- silently allow prototype-only definitions into release validation;
- omit provenance for release-bound definitions;
- hand-edit or generate registry output inside CS-009;
- add a universal catch-all kind to avoid reviewing a new content category.
