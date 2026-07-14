# Registry generation and drift validation

## Status and ownership

CS-011 is the sole writer for the central Generated Registry v1 outputs:

- `Assets/ShooterMover/Generated/content-registry.json`;
- `Assets/ShooterMover/Generated/content-review-snapshot.json`.

The generator lives in `tools/content-validation/content_registry.py`. Package,
definition, and shared-module tasks contribute approved descriptor inputs in their
own paths. They never edit, splice, or merge either central output. Generated-file
conflicts are resolved in an authoritative descriptor or in this generator, then
the complete pair is regenerated.

This boundary consumes:

- Generated Registry Formats v1 from CS-010; and
- the UF-005 ownership rule that `Assets/ShooterMover/Generated/` is
  regenerate-only and owned by the generated-registry workflow.

The output is derived evidence about immutable content definitions. It is not
gameplay state, a save format, a content-authoring database, or a runtime lookup
service.

## Authoritative descriptor inputs

The default scan roots are:

- `Assets/ShooterMover/Content/Definitions/`;
- `Assets/ShooterMover/Content/SharedModules/`;
- `Assets/ShooterMover/ContentPackages/`.

Only files whose names end in `.content-descriptor.json` are scanned. Discovery
order and file names never affect output order or fingerprints. A later content
task owns the descriptor file in its accepted package or definition subtree; it
does not gain ownership of the generator or outputs.

Each input is strict UTF-8 JSON with no BOM, no duplicate properties, no unknown
properties, and this exact shape:

```json
{
  "$schema": "urn:shooter-mover:schema:content-definition-descriptor-input:1",
  "definition_kind": "weapon",
  "definition_id": "weapon.arc-gun",
  "definition_version": 1,
  "provenance_id": "provenance.arc-gun-approved",
  "prototype_only": false,
  "references": [
    {
      "definition_kind": "shared-module",
      "definition_id": "module.arc-core",
      "definition_version": 1
    }
  ]
}
```

The descriptor input schema identifier is a scanner boundary, not a third central
registry format. The generated documents continue to use only the two CS-010
schemas. A future change to descriptor shape or interpretation requires reviewed
versioning of this input boundary and corresponding generator tests.

The accepted StableId, definition-kind, version, provenance, prototype, and typed
reference values remain the authority. Machine-local paths, timestamps, process
IDs, worktree roots, Unity instance IDs, import order, locale, and environment
newlines are forbidden inputs.

## Commands

Run from the repository root with Python 3.10 or newer. The tool uses only the
Python standard library.

```text
python tools/content-validation/content_registry.py validate
python tools/content-validation/content_registry.py generate
python tools/content-validation/content_registry.py check
```

- `validate` scans and validates authoritative descriptors and computes the two
  documents without writing them.
- `generate` validates the existing generated pair, scans descriptors, computes
  both complete outputs, and replaces the pair through the atomic-write protocol.
- `check` computes expected bytes from current inputs and fails if either checked-
  in output is absent or byte-different.

Defaults are `--catalog-version 1` and `--mode release`. The supported validation
modes are `release` and `prototype`. Tests and isolated tooling may repeat
`--descriptor-root PATH` and may override `--registry-output` and
`--review-output`; production generation uses the default approved roots and
central paths.

Exit codes are stable:

| Code | Meaning |
|---:|---|
| 0 | Validation, generation, or drift check succeeded. |
| 2 | Descriptor input or Content Definitions v1 validation failed. |
| 3 | Generated output is missing, incomplete, stale, reordered, non-canonical, mismatched, or manually edited. |
| 4 | Another invocation owns the generation lock. |
| 5 | Tool, filesystem, temporary-file, or recovery operation failed. |

Successful commands print the raw SHA-256 checksum of each complete output and
the formal definition, registry, and snapshot fingerprints. Failures print
stable, sorted validation or drift details and do not suppress later errors.

## Validation and canonical order

Before output exists, the complete descriptor set is validated for:

- duplicate definition IDs;
- missing referenced definitions;
- wrong referenced kinds;
- unsupported definition or reference versions;
- cyclic typed-reference components;
- missing provenance; and
- release-ineligible prototype-only definitions.

No invalid entry is dropped, repaired, selected from a duplicate group, or
silently downgraded. Validation errors use the CS-009 code order and deterministic
StableId/detail tie-breakers.

Valid descriptors are ordered by:

1. ordinal canonical definition-kind token;
2. ordinal StableId canonical text;
3. definition version.

The v1 order is `enemy`, `encounter`, `environment`, `room`, `shared-module`, then
`weapon`. References within one descriptor are ordered by expected kind,
referenced StableId, and expected version. Shuffling files, descriptors, or
references cannot change bytes or checksums.

The generator reproduces the exact CS-010 canonical JSON, LF/no-BOM encoding,
property order, whitespace, terminal LF, and SHA-256 preimages. It does not use a
permissive reserialization as an equivalent format. Reordered properties or
entries therefore fail byte validation even when generic JSON parsing succeeds.

## Drift and manual-edit policy

`check` is the required CI/review drift gate. It compares each complete checked-in
file with independently regenerated bytes from current descriptor inputs. Any
manual character, changed whitespace, reordered array, stale catalog, count
mismatch, fingerprint mismatch, missing file, or one-sided output update fails.

Ordinary `generate` also fails closed when an existing pair is incomplete,
non-canonical, internally mismatched, or fingerprint-invalid. It will not use a
damaged output as an authoring source. After a reviewed incident, the designated
generator owner may use:

```text
python tools/content-validation/content_registry.py generate --repair-drift
```

`--repair-drift` still scans and fully validates authoritative inputs; it merely
permits replacing a damaged existing pair. It is not a conflict-resolution or
error-suppression switch. The resulting complete diff and a succeeding `check`
must be reviewed.

## Atomic pair protocol and concurrency

Generation acquires the exclusive
`tools/content-validation/.registry-generation.lock` file with create-if-absent
semantics. A concurrent invocation fails before touching either output. The lock
contains no machine identity and is removed when the owning process exits
normally. A stale lock after abnormal termination is an explicit failure that a
reviewer must investigate and remove; the tool never guesses that another writer
is dead.

For both outputs, the generator:

1. computes and validates all bytes before writing;
2. writes separate same-directory temporary files with exclusive creation;
3. flushes and `fsync`s each complete temporary file;
4. atomically replaces each destination with `os.replace`; and
5. rolls back destinations already replaced if an ordinary exception occurs
   before the pair is complete.

A two-file transaction cannot be committed by one portable filesystem syscall.
A process or machine crash between the two atomic replacements can therefore
leave a complete old/new mixed pair, never a partially written JSON file. The
next `check` detects that mismatch deterministically. Recovery is the reviewed
`generate --repair-drift` flow, followed by `check`.

Stale `.cs011.tmp` or `.cs011.rollback` files are not silently reused. Their
presence or a recovery failure stops generation for investigation.

## Baseline outputs

No approved production descriptor manifests exist at the CS-011 baseline, so the
checked-in release catalog is the valid deterministic empty catalog. It contains
zero entries, six explicit zero kind counts, and non-placeholder fingerprints.
Later content tasks add their owned descriptors and regenerate the complete pair;
they do not hand-edit the empty baseline into a populated registry.

The baseline raw file checksums are:

```text
content-registry.json
  d38116bd5ea8a003de1373e03c89fd8bb4b9948302df34cfe6b44e44b9afb2ca
content-review-snapshot.json
  762358376b62cafa2e04ea89cf99cddb777b79bd5824ff7481eb791321fb5fd9
```

## Verification

Run the tool tests:

```text
python -m unittest discover -s tools/content-validation -p "test_*.py" -v
```

The test suite covers:

- byte-identical output from shuffled descriptors and references;
- frozen empty-catalog checksums and canonical byte rules;
- duplicate, missing, wrong-kind, unsupported-version, cycle, provenance, and
  prototype validation behavior;
- deterministic error ordering without suppression;
- generation followed by an exact `check`;
- hand-edited and reordered output drift failures;
- explicit reviewed repair;
- concurrent invocation failure; and
- rollback when replacement of the second output is interrupted.

Repository proof is completed with:

```text
python tools/content-validation/content_registry.py generate
python tools/content-validation/content_registry.py check
```

Reviewers should confirm that a representative later content task changes only
its owned `.content-descriptor.json` input plus package-owned files, then invokes
this tool to produce the central output diff. The content task must never claim
or directly merge either output.

## Limitations and non-goals

CS-011 intentionally does not add:

- gameplay content or balance values;
- Unity asset-database or ScriptableObject scanning;
- tombstone-file discovery, migrations, runtime lookup, hot reload, or watchers;
- build hooks, network services, remote telemetry, or machine-local provenance;
- suppressed validation errors or partial registries; or
- manual conflict resolution for generated files.

Tombstoned identifiers remain a Content Definitions v1 validation concept; a
future accepted tombstone input source must be separately owned and versioned
before this scanner may consume it.

## Rollback

Rollback CS-011 by reverting together:

- `tools/content-validation/content_registry.py`;
- `tools/content-validation/test_content_registry.py`;
- `Assets/ShooterMover/Generated/content-registry.json`;
- `Assets/ShooterMover/Generated/content-review-snapshot.json`; and
- this document.

Do not leave one generated output, a consumer command, or a descriptor convention
behind without the generator. Rollback has no save migration, scene, prefab,
ScriptableObject, gameplay-state, or package-lock impact.
