# Offline evidence identity capture v1

## Purpose and boundary

EH-001 composes one immutable identity for a local evidence session. The record
answers which source, editor, package lock, build-content set, content version,
save schema, build shape, artifact, and tuning profile produced the evidence.

The implementation is deliberately split into two small adapters:

- `EvidenceIdentityCapture.cs` is an I/O-free deterministic composer. It consumes
  canonical `BuildIdentity v1`, canonical `ContentVersion v1`, an explicit dirty
  policy, build target/configuration, and a tuning-profile `StableId`.
- `capture_build_identity.py` is a standard-library-only offline wrapper. It
  additionally verifies the exact supplied `Packages/packages-lock.json` bytes
  and pinned `ProjectSettings/ProjectVersion.txt` before emitting the same
  canonical record.

Neither adapter performs a build, replaces the UF-010 build entrypoint, writes a
registry, edits content, reads credentials, sends telemetry, contacts a remote
service, or decides gameplay state. The C# adapter performs no file or Git access.
The Python wrapper reads only explicitly supplied local files and writes only the
optional local output path.

## Required inputs

| Input | Rule |
|---|---|
| `BuildIdentity v1` | Strict canonical CS-002 text. A final `artifact_checksum` is mandatory for evidence, including development builds. Its package-lock, build-content, source, editor, and save-schema fields are retained exactly. |
| `ContentVersion v1` | Strict canonical CS-002 text. Its catalog version and definition fingerprint are retained exactly. |
| Dirty-state policy | Exactly `reject-dirty` or `allow-dirty-development`. |
| Unity/editor version | Taken from `BuildIdentity.unity_version`; the CLI requires it to equal the pinned `m_EditorVersion`. |
| Package-lock fingerprint | Taken from `BuildIdentity.package_lock_fingerprint`; the CLI requires it to equal SHA-256 of the exact supplied package-lock bytes. |
| Save schema | Positive CS-002 `BuildIdentity.save_schema`. |
| Build target | One non-provisional 1–64 character ASCII token using letters, digits, `.`, `_`, or `-`. |
| Build configuration | Same token rules as build target. |
| Tuning-profile ID | Canonical StableId v1, for example `movement-tuning.stage1-baseline`. |

`BuildIdentity.content_fingerprint` and
`ContentVersion.definition_fingerprint` identify different CS-002 boundaries:
the complete accepted build-content input set and the accepted definition
snapshot. Evidence identity v1 records both independently and does not invent an
unstated equality or derivation rule between them.

## Dirty-source policy

`reject-dirty` accepts only a clean `BuildIdentity`.

`allow-dirty-development` accepts only an explicitly dirty, development
`BuildIdentity`. It never makes that identity formal. A clean identity paired
with the permissive policy is rejected as internally inconsistent rather than
silently normalized.

The record always includes both `source_state` and `dirty_state_policy`. A dirty
source is therefore visible in the fingerprint and cannot be confused with a
clean capture at the same commit.

## Canonical record

The first fifteen lines are the fingerprint payload. UTF-8 bytes are hashed
exactly as shown: ASCII LF separators, no BOM, no trailing newline, no trimming,
and no case normalization. The sixteenth line stores
`sha256(<first fifteen UTF-8 lines>)`.

```text
evidence_identity_schema=1
build_identity_kind=<formal-release|development>
source_commit=<40 lowercase hexadecimal Git SHA>
source_state=<clean|dirty>
dirty_state_policy=<reject-dirty|allow-dirty-development>
unity_version=<canonical Unity editor version>
package_lock_fingerprint=sha256:<64 lowercase hex characters>
build_content_fingerprint=sha256:<64 lowercase hex characters>
content_catalog_version=<positive integer>
content_definition_fingerprint=sha256:<64 lowercase hex characters>
save_schema_version=<positive integer>
artifact_checksum=sha256:<64 lowercase hex characters>
build_target=<canonical token>
build_configuration=<canonical token>
tuning_profile_id=<StableId v1>
record_fingerprint=sha256:<64 lowercase hex characters>
```

There is no timestamp, machine name, path, username, locale, random value, or
unordered serialization in the fingerprint. Two captures from the same frozen
inputs therefore serialize byte-for-byte identically. Changing any required
field changes the fifteen-line payload and therefore changes the fingerprint.

## Fail-closed classifications

Invalid input produces no record. The C# adapter returns
`EvidenceIdentityCaptureResult.IsValid == false`, `Record == null`, and a stable
error code. The Python wrapper prints `invalid evidence: <code>: <message>` to
stderr, emits no record, and exits with status `2`.

Representative classifications include:

| Condition | Error code |
|---|---|
| Missing or non-canonical BuildIdentity | `missing-build-identity` / `malformed-build-identity` |
| Development identity with `artifact_checksum=null` | `provisional-build-identity` |
| Missing or non-canonical ContentVersion | `missing-content-version` / `malformed-content-version` |
| Dirty identity under `reject-dirty` | `inconsistent-dirty-state-policy` |
| Permissive policy without a dirty development identity | `inconsistent-dirty-state-policy` |
| Unsupported or absent policy | `unsupported-dirty-state-policy` / `missing-dirty-state-policy` |
| Package-lock bytes disagree with BuildIdentity | `inconsistent-package-lock` |
| Pinned editor disagrees with BuildIdentity | `inconsistent-unity-version` |
| Missing, malformed, or provisional target/configuration | field-specific `missing-*`, `malformed-*`, or `provisional-*` code |
| Missing, malformed, or provisional tuning StableId | `missing-tuning-profile-id`, `malformed-tuning-profile-id`, or `provisional-tuning-profile-id` |

Reserved provisional values include `unknown`, `unset`, `todo`, `tbd`, `null`,
`none`, and values containing `provisional` or `placeholder`. The existing
CS-002 validators separately reject shortened, uppercase, all-zero, or otherwise
malformed commits and SHA-256 values.

## Offline CLI

Prepare strict canonical CS-002 files, then run from the repository root:

```powershell
python .\tools\evidence\capture_build_identity.py `
  --build-identity .\local-evidence\build-identity.txt `
  --content-version .\local-evidence\content-version.txt `
  --dirty-state-policy reject-dirty `
  --build-target StandaloneWindows64 `
  --build-configuration Development `
  --tuning-profile-id movement-tuning.stage1-baseline `
  --output .\local-evidence\evidence-identity.txt
```

By default the wrapper verifies:

```text
ProjectSettings/ProjectVersion.txt
Packages/packages-lock.json
```

Alternative local paths may be passed with `--project-version` and
`--package-lock`. The package fingerprint is SHA-256 of the exact file bytes;
it must already be recorded in `BuildIdentity` using the `sha256:` prefix. A
working-tree rewrite therefore fails closed instead of being accepted as the
frozen repository input.

When `--output` is omitted, exact canonical UTF-8 bytes are written to stdout.
The output has no trailing newline. Redirect diagnostics separately; stderr is
reserved for invalid-evidence messages.

## Determinism proof shape

The focused EditMode fixture captures the same frozen inputs twice and asserts
identical canonical text and identical fingerprints. It then changes source
commit, editor version, package lock, build-content fingerprint, content-version
fingerprint, save schema, target, configuration, tuning-profile ID, and
dirty-source policy cases and asserts that every valid changed record has a
different fingerprint.

For manual proof, run the CLI twice without changing any input and compare the
files byte-for-byte. Then change only the tuning-profile ID to another accepted
StableId and run again. The first pair must be identical; the changed-tuning
record must have a different `tuning_profile_id` and `record_fingerprint`.
These generated files are evidence outputs and must remain outside tracked
source unless a later task explicitly owns an evidence artifact path.

## Editor and Windows-build comparison

An editor-side producer and the UF-010 Windows development build may be compared
only when they supply the same source commit, source state/policy, editor
version, package-lock fingerprint, build-content fingerprint, content version,
save schema, and tuning ID. Their build target/configuration and artifact
checksum remain explicit, so two different artifacts do not collapse to one
identity.

EH-001 does not modify UF-010. A later integration may call the Python wrapper
against UF-010's local fingerprint outputs, but it must not invent a second
record grammar or weaken any fail-closed rule here.

## Validation and limitations

Static review can verify that:

- all required fields occur once in fixed order;
- SHA-256 covers the complete fifteen-line payload and not the final fingerprint
  line;
- the C# record/result types expose get-only properties;
- invalid results carry no partial record;
- the Python tool imports only the standard library and contains no network,
  credential, telemetry, registry-generation, or build invocation code.

The C# tests require the pinned Unity editor because the adapter is compiled by
Unity and the focused fixture runs under the EditMode Test Framework. Connector-
only implementation environments cannot substitute a source inspection for the
required Unity test log.

## Rollback

Revert or remove the four EH-001 owned files and their inseparable Unity metadata
as one unit. No package resolution, project-setting restoration, generated
registry repair, content migration, save migration, build cleanup, credential
revocation, or remote-service teardown is required.
