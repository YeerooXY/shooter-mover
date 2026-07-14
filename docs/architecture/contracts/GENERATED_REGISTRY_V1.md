# Generated Registry Formats v1

## Status and scope

Generated Registry Formats v1 defines two immutable, engine-independent output
contracts:

- a canonical machine registry containing the complete validated Content
  Definitions v1 descriptor set; and
- a deterministic, diff-friendly human review snapshot derived from that
  machine registry.

The contracts live in
`Assets/ShooterMover/Runtime/Contracts/Content/GeneratedRegistryContracts.cs`.
Their JSON Schemas live in:

- `Assets/ShooterMover/Generated/schemas/generated-registry-v1.schema.json`;
- `Assets/ShooterMover/Generated/schemas/generated-registry-review-v1.schema.json`.

This task consumes:

- Identity v1 `ContentVersion` and its canonical SHA-256 fingerprint form; and
- Content Definitions v1 descriptors, validation modes, typed references,
  canonical kind tokens, and deterministic ordering.

CS-010 defines and materializes the in-memory format only. It does not scan
Unity assets, discover packages, choose files, write generated artifacts, create
a production catalog, or resolve conflicts. CS-011 remains the sole generated
registry writer and generator owner.

## Authority boundary

A generated registry is derived evidence about accepted content definitions. It
is not runtime gameplay state and cannot own or mutate:

- health, damage, projectiles, enemies, rooms, encounters, or loaded scene state;
- checkpoints, rewards, objectives, route progress, mission completion, or saves;
- Unity objects, scene references, asset paths, instance IDs, delegates, or random
  state;
- machine-local paths, usernames, timestamps, temporary directories, or editor
  caches.

Package and definition tasks contribute authoritative immutable descriptors.
The registry format orders and represents those inputs but does not become a
parallel authoring source.

## Validation precondition

`GeneratedMachineRegistry.Create(...)` copies the supplied descriptor sequence,
orders it canonically, and validates the complete copy through Content
Definitions v1 using the requested `release` or `prototype` mode.

Creation is rejected when Content Definitions v1 reports any error, including:

duplicate IDs, missing definitions, wrong kinds, unsupported versions, cycles,
missing provenance, tombstoned IDs, or release-ineligible prototype-only
content. The registry never chooses the first duplicate, drops an invalid
entry, repairs a version, or substitutes another definition.

Because the machine registry can exist only after successful validation, the v1
human snapshot records the explicit post-validation summary:

```text
is_valid=true
error_count=0
```

Invalid catalogs retain their structured CS-009 validation result and do not
produce a misleading partial registry or review snapshot.

## Canonical byte rules

Both output documents obey the same byte rules:

1. encoding is UTF-8 without a byte-order mark;
2. all structural line endings are one ASCII LF byte (`0a`);
3. CR and CRLF are never emitted;
4. every document ends with exactly one LF;
5. indentation is two ASCII spaces per JSON object level;
6. property names, property order, array order, commas, and whitespace are part
   of the v1 canonical representation;
7. strings use deterministic JSON escaping and are never culture-formatted;
8. integers use invariant unsigned-looking decimal text without grouping;
9. booleans are lowercase JSON `true` or `false`;
10. no environment newline, locale, dictionary order, input collection order,
    or machine path participates in output.

`ToCanonicalJson()` returns the canonical text.
`GetCanonicalUtf8Bytes()` returns its exact no-BOM UTF-8 bytes.

Changing these byte rules is a format-version change even when a permissive JSON
parser would consider two variants equivalent.

## Canonical definition ordering

Machine entries and review entries use Content Definitions v1 comparison:

1. ordinal canonical definition-kind token;
2. ordinal StableId canonical text;
3. definition version as a final deterministic tie-breaker.

For the closed v1 taxonomy, the kind order is therefore:

1. `enemy`;
2. `encounter`;
3. `environment`;
4. `room`;
5. `shared-module`;
6. `weapon`.

References inside each entry are already copied, deduplicated, and ordered by
CS-009 using expected kind, referenced StableId, and expected version.

Shuffling descriptor or reference input cannot change bytes, fingerprints, or
review order. Duplicate definition IDs are invalid rather than tie-resolved by
input position.

## Machine registry v1

### Identity

- schema ID: `urn:shooter-mover:schema:generated-registry:1`;
- schema version: `1`;
- contract type: `GeneratedMachineRegistry`.

### Top-level fields and order

| Position | Field | Rule |
|---:|---|---|
| 1 | `$schema` | Exact machine-registry schema ID. |
| 2 | `schema_version` | Integer `1`. |
| 3 | `validation_mode` | `release` or `prototype`. |
| 4 | `catalog_version` | Positive Identity v1 catalog version supplied by the owning catalog process. |
| 5 | `definition_fingerprint` | Identity v1 SHA-256 of the canonical ordered descriptor set. |
| 6 | `registry_fingerprint` | SHA-256 of the machine-registry semantic preimage defined below. |
| 7 | `entry_count` | Exact number of entries. |
| 8 | `entries` | Complete canonically ordered descriptor array. |

Each machine entry contains, in order:

1. `definition_kind`;
2. `definition_id`;
3. `definition_version`;
4. `provenance_id`;
5. `prototype_only`;
6. `reference_count`;
7. `references`.

Each typed reference contains, in order:

1. `definition_kind`;
2. `definition_id`;
3. `definition_version`.

The machine schema rejects unknown properties, unknown kinds, malformed
StableIds, non-v1 definition versions, malformed fingerprints, negative counts,
and missing required fields. Count equality and canonical array order are
semantic invariants enforced by the contract and tests; JSON Schema alone cannot
express all cross-field count/order relationships without duplicating generator
logic.

## Human review snapshot v1

### Identity

- schema ID: `urn:shooter-mover:schema:generated-registry-review:1`;
- schema version: `1`;
- contract type: `GeneratedRegistryReviewSnapshot`.

### Purpose

The review snapshot is deliberately redundant and compact. It gives reviewers a
stable summary and one line per entry without replacing the complete machine
registry. It includes:

- its own schema version and the source machine-registry schema version;
- explicit validation mode, successful validation state, and zero error count;
- catalog, definition, machine-registry, and snapshot fingerprints;
- total entry, prototype-only, and typed-reference counts;
- one count for every v1 definition kind in canonical kind order; and
- a canonically ordered entry list containing kind, StableId, definition
  version, and reference count.

The review snapshot is produced only from an immutable
`GeneratedMachineRegistry`. It cannot be constructed from an unrelated list or
manually supplied summary values.

## Fingerprint algorithms

All formal fingerprints use:

```text
sha256:<64 lowercase hexadecimal characters>
```

The bytes hashed are UTF-8 without BOM and use LF exactly as written below.
Fingerprint preimages do not include a terminal newline unless the grammar below
explicitly creates one.

### Definition fingerprint

The definition fingerprint becomes
`ContentVersion.DefinitionFingerprint`. Its preimage begins:

```text
format=generated-registry-definition-set-v1
entry_count=<decimal count>
```

For each canonical descriptor index `NNNNNN`, append:

```text
entry_NNNNNN_utf8_length=<decimal byte count of descriptor canonical text>
entry_NNNNNN=<exact CS-009 descriptor canonical text>
```

The six-digit index and UTF-8 byte length make concatenation boundaries
unambiguous even though descriptor canonical text contains LF-delimited fields.
The empty catalog hashes only the format and zero-count header.

### Machine registry fingerprint

The registry fingerprint preimage is:

```text
format=generated-machine-registry-v1
schema_version=1
validation_mode=<release|prototype>
content_version_utf8_length=<decimal byte count>
content_version=<exact Identity v1 ContentVersion canonical text>
definition_set_utf8_length=<decimal byte count>
definition_set=<exact definition-fingerprint preimage>
```

This fingerprint changes when schema version, validation mode, catalog version,
definition fingerprint, or any canonical descriptor fact changes. It is not a
hash of pretty-printing alone; the canonical JSON byte rules are separately
versioned and tested.

### Review snapshot fingerprint

The review fingerprint preimage starts with:

```text
format=generated-registry-review-v1
schema_version=1
registry_fingerprint=<machine registry fingerprint>
entry_count=<decimal count>
prototype_only_count=<decimal count>
reference_count=<decimal count>
validation_mode=<release|prototype>
validation_is_valid=true
validation_error_count=0
```

It then appends all six kind counts in canonical kind order and all review entry
rows in canonical descriptor order. Including the source registry fingerprint
binds the summary to the complete machine document, including provenance,
prototype flags, and exact reference targets that the compact row list does not
repeat.

## Empty-catalog behavior

An empty descriptor collection is a valid deterministic catalog when Content
Definitions v1 reports no errors. It produces:

- `entry_count=0`;
- an empty machine entry array;
- zero prototype-only and reference counts;
- six explicit zero kind counts in the review snapshot; and
- stable non-placeholder definition, registry, and snapshot SHA-256 values.

`null` descriptor collections and collections containing `null` descriptors are
malformed input and are rejected.

## Schema ownership and production outputs

The two files under `Assets/ShooterMover/Generated/schemas/` are the only
CS-010-owned generated-root text files. They are versioned schemas, not a
hand-authored production catalog and not an example registry to be shipped.

Future CS-011 output must:

1. consume accepted descriptors and this contract;
2. write complete machine and review files atomically to its owned output paths;
3. compare independently generated bytes when proving reproducibility;
4. resolve conflicts in authoritative inputs or generator code;
5. regenerate the complete output; and
6. never manually splice, merge, or repair generated JSON.

Machine-local paths, timestamps, process IDs, worktree roots, asset database
instance IDs, and discovery order are forbidden generator inputs.

## Representative fixture coverage

`GeneratedRegistryContractTests` constructs representative fixtures for:

- shuffled enemies, a room, a shared module, and a weapon dependency;
- a prototype-mode Arc Gun/module pair and release enemy;
- an empty catalog;
- a changed provenance identity;
- an invalid missing-provenance descriptor; and
- both checked-in schemas.

The tests compare complete UTF-8 byte arrays, not only parsed values. They also
assert canonical kind/StableId order, one terminal LF, no CR/BOM, explicit
fingerprint shape and sensitivity, defensive input copying, schema IDs and
structure, getter-only contracts, no Unity assembly reference, and absence of
machine paths or runtime-authority fields.

These are test fixtures only. They do not create or authorize a production
catalog.

## Versioning

A new reviewed format version is required when changing any of the following:

- schema ID or schema version;
- property names, order, requiredness, JSON shape, indentation, or terminal LF;
- definition-kind or StableId ordering;
- fingerprint preimage labels, framing, field order, or hash algorithm;
- validation preconditions or the meaning of review summary fields; or
- the ownership boundary between authoritative inputs and generated output.

Adding optional properties to v1 is not backward-compatible because v1 schemas
use `additionalProperties: false` and canonical property order is exact.

## Limitations and non-goals

Generated Registry Formats v1 intentionally does not provide:

- Unity asset scanning or ScriptableObject adapters;
- a registry generator, file writer, watcher, build hook, or import hook;
- runtime lookup services, caches, mutable catalog state, or hot reload;
- machine-local provenance paths or source-file discovery data;
- hand-authored production entries or example output committed as authority;
- manual conflict resolution rules for generated files; or
- migration of a future production registry to another schema version.

## Rollback

Rollback CS-010 by reverting together:

- `GeneratedRegistryContracts.cs` and its paired metadata;
- the two schema files and required generated-folder/file metadata;
- `GeneratedRegistryContractTests.cs` and its paired metadata; and
- this contract document.

No scene, prefab, package, project setting, runtime state, save data, build
artifact, or production catalog requires migration or cleanup. If CS-011 or a
later consumer has already adopted v1, revert or migrate that dependent work
before removing the contract and schemas.
