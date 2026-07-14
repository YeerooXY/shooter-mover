# Identity Manifest v1

## Status and scope

Identity Manifest v1 defines two immutable, engine-independent value contracts:

- `ContentVersion`, which identifies one accepted content catalog snapshot; and
- `BuildIdentity`, which identifies the source, toolchain, content, save schema,
  and final artifact associated with one build.

The implementation lives in `ShooterMover.Contracts.Identity`. It has no
`UnityEngine` dependency and performs no file, Git, package, content, save, or
build-pipeline access. Producers calculate the accepted values outside these
contracts and then construct or parse the validated identity fully offline.

This task does not implement a build pipeline, package change, content registry,
save migration, artifact writer, or release-signing system.

## Canonical text rules

Each value serializes as ordered `name=value` lines through
`ToCanonicalString()`. Canonical bytes are UTF-8 without a BOM, use one ASCII LF
between fields, and have no trailing newline. Parsing is strict:

- field names and field order must match this document exactly;
- missing, duplicate, reordered, unknown, or additional fields are rejected;
- CRLF, a trailing newline, whitespace, trimming, and case normalization are not
  accepted aliases;
- positive integer fields use decimal text without a sign or leading zeroes;
- fingerprints and checksums use `sha256:` followed by exactly 64 lowercase
  hexadecimal characters;
- an all-zero Git SHA, fingerprint, or checksum is a rejected placeholder.

Equality is exact ordinal equality of every accepted value. Hash codes are the
deterministic 32-bit FNV-1a hash of the complete canonical text. The canonical
text, not the 32-bit hash, is the persisted identity.

## ContentVersion

### Accepted fields and order

| Position | Field | Rule |
|---:|---|---|
| 1 | `catalog_version` | Positive integer, starting at 1 and incremented only by the owning catalog process. |
| 2 | `definition_fingerprint` | SHA-256 identity of the canonical accepted definition snapshot. |

Canonical form:

```text
catalog_version=<positive integer>
definition_fingerprint=sha256:<64 lowercase hex characters>
```

`ContentVersion.Create(...)` validates already-separated values.
`ContentVersion.ParseCanonical(...)` validates the complete ordered text.
Changing either field changes equality and the canonical representation.

## BuildIdentity

`BuildIdentity` has two non-interchangeable kinds:

- `formal-release` is eligible to identify a formal artifact; and
- `development` is explicitly non-formal, even when its source is clean and it
  happens to have a checksum.

`source_state` is `clean` or `dirty`. This state is kept separate from
`source_commit`, so the commit remains an exact Git object identity rather than
an ambiguous value such as `<sha>-dirty`.

### Accepted fields and order

| Position | Field | Rule |
|---:|---|---|
| 1 | `identity_kind` | `formal-release` or `development`. |
| 2 | `source_state` | `clean` or `dirty`. |
| 3 | `source_commit` | Complete 40-character lowercase hexadecimal Git commit SHA. |
| 4 | `unity_version` | Canonical Unity editor version such as `6000.3.19f1`; revision annotations are not part of this field. |
| 5 | `package_lock_fingerprint` | SHA-256 identity of the accepted package-lock bytes or canonical package-lock snapshot. |
| 6 | `content_fingerprint` | SHA-256 identity of the accepted build content input set. |
| 7 | `save_schema` | Positive integer save-schema version. |
| 8 | `artifact_checksum` | SHA-256 checksum of the final artifact, or the literal `null` only for a development identity. |

Canonical form:

```text
identity_kind=<formal-release|development>
source_state=<clean|dirty>
source_commit=<40 lowercase hex characters>
unity_version=<canonical Unity version>
package_lock_fingerprint=sha256:<64 lowercase hex characters>
content_fingerprint=sha256:<64 lowercase hex characters>
save_schema=<positive integer>
artifact_checksum=<sha256 value|null>
```

### Formal-release invariants

`BuildIdentity.CreateFormal(...)` and canonical parsing reject a formal identity
unless all of the following are true:

1. `identity_kind` is `formal-release`;
2. `source_state` is `clean`;
3. every required field is present and canonical; and
4. `artifact_checksum` contains a final non-placeholder SHA-256 value.

A dirty working tree cannot be represented as formal. A release candidate whose
artifact has not yet received its final checksum also cannot be represented as
formal. Values such as `unknown`, `todo`, shortened SHAs, uppercase hashes,
all-zero hashes, or `null` formal checksums are rejected rather than repaired.

### Development invariants

`BuildIdentity.CreateDevelopment(...)` always records
`identity_kind=development`. It records source state explicitly and may use
`artifact_checksum=null` while no final artifact checksum exists. Such an
identity remains non-formal after round-trip parsing, including when its source
is clean or a checksum is later supplied.

This distinction lets local and CI development work carry useful identity
without accidentally satisfying a formal-release check.

## Representative canonical manifest fixture

The following two named records form a representative fixture. The headings are
labels for this document; the bytes inside each code block are the exact
canonical record payloads. Fingerprints are illustrative valid SHA-256-shaped
values and do not claim to be fingerprints of the current repository files.

### `content_version`

```text
catalog_version=1
definition_fingerprint=sha256:8c1e3a5f7b9d0f2a4c6e8b1d3f5a7c9e0b2d4f6a8c1e3b5d7f9a0c2e4b6d8f1a
```

### `build_identity`

```text
identity_kind=formal-release
source_state=clean
source_commit=eb80374cb669f0f8c9e36210c45f935f28c3acc2
unity_version=6000.3.19f1
package_lock_fingerprint=sha256:4d2f9c0e6b8a1d3f5e7c9a0b2d4f6e8c1a3b5d7f9e0c2a4b6d8f1e3c5a7b9d0f
content_fingerprint=sha256:c7a91e4d2f6b8c0a5d3e7f1b9a4c6e8d0f2b5d7a9c1e3f6b8d0a2c4e7f9b1d3a
save_schema=1
artifact_checksum=sha256:e3b7c1d95f0a2c4e6b8d1f3a7c9e0b2d4f6a8c1e3b5d7f9a0c2e4b6d8f1a3c5e
```

A dirty development fixture differs explicitly at the state boundary and may
lack the final checksum:

```text
identity_kind=development
source_state=dirty
source_commit=eb80374cb669f0f8c9e36210c45f935f28c3acc2
unity_version=6000.3.19f1
package_lock_fingerprint=sha256:4d2f9c0e6b8a1d3f5e7c9a0b2d4f6e8c1a3b5d7f9e0c2a4b6d8f1e3c5a7b9d0f
content_fingerprint=sha256:c7a91e4d2f6b8c0a5d3e7f1b9a4c6e8d0f2b5d7a9c1e3f6b8d0a2c4e7f9b1d3a
save_schema=1
artifact_checksum=null
```

## Producer responsibilities

A future owned build or evidence task may calculate the package-lock, content,
and artifact SHA-256 values and pass them into these contracts. That producer
must define the exact byte/canonicalization boundary it hashes and must not
silently substitute a Git blob SHA, timestamp, mutable label, or partial digest.

Identity Manifest v1 itself does not inspect the working tree. A producer that
knows the tree is dirty must call the development factory with `isDirty=true`;
it cannot obtain a formal identity through an alternate constructor.

## Versioning and rollback

Changing field names, field order, grammar, equality, hashing, formal-release
invariants, or the meaning of a fingerprint requires a new identity contract
version and an explicit consumer migration.

Rollback CS-002 by reverting the owned `Identity/` contract subtree, the paired
EditMode test and metadata, and this document together. No package, project
setting, content asset, save, registry, or build artifact requires rollback.
