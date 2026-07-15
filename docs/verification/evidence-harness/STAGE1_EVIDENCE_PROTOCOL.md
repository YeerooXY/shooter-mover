# Stage 1 Evidence Protocol v1

## Purpose and boundary

This protocol defines how a Shooter Mover Stage 1 evidence session is prepared,
checked, reviewed, classified, and either admitted as technical evidence or
rejected for rerun. It consumes the immutable EH-008 manifest produced by an
EH-009 local smoke entrypoint and the CS-012 Diagnostics / Run Validity v1
contract.

The protocol proves evidence integrity and harness-shell behavior only. It does
not approve gameplay, determine whether the game is fun, repair evidence,
reclassify a session automatically, upload files, emit telemetry, or conduct an
external player study.

The only accepted protocol identity is:

```text
schema=shooter-mover.stage1-evidence-protocol
version=1
```

Unsupported versions fail closed.

## Roles and immutable boundary

The **operator** creates a fresh local evidence package through one accepted
EH-009 entrypoint. The **human reviewer** opens the manifested proof and writes
one separate canonical review JSON. The validator reads both and emits a report;
it never edits either input.

The review file stays outside the manifested package. Adding it to the package
after EH-008 generation would create checksum and inventory drift. Instead, the
review binds to the package by the immutable manifest digest, EH-001 identity
fingerprint and file checksum, EH-002 configuration checksum, session ID, and
attempt ID.

Exactly one review is accepted. Zero reviews are `missing-human-review`; more
than one is `duplicate-review`.

## Preparation

1. Select one missing or empty caller-owned output directory outside the
   repository. Never reuse a partial or previously reviewed directory.
2. Run exactly one accepted EH-009 entrypoint: EditMode, PlayMode, or Windows
   build smoke.
3. Do not modify the package after `evidence-manifest-v1.json` and
   `evidence-manifest-v1.sha256` are written.
4. Copy or author the review JSON outside the package.
5. Run the validator from the repository root:

```powershell
python -S tools/evidence/validate_evidence_session.py `
  --package-root "C:\evidence\shooter-mover\playmode-001" `
  --review "C:\evidence\reviews\playmode-001-review.json"
```

The command performs no network access and requires only the Python standard
library.

## Automated package checks

Before human answers are considered, the validator requires all of the
following:

- canonical EH-008 manifest and checksum bytes;
- exact manifest schema, tool identity, nested object shapes, and complete
  path-normalized inventory;
- no missing, extra, case-colliding, symlink, reparse, or checksum-drifted file;
- EH-001 identity record fingerprint, manifest identity fields, and exact player
  executable checksum agreement;
- EH-002 configuration checksum and `identityReference` agreement with EH-001;
- manifest, descriptor, diagnostics summary, performance summary, and UF-010
  build facts to agree;
- a complete successful Windows Development build binding even for EditMode and
  PlayMode packages, as required by EH-008 v1;
- passing `diagnostics/test-results.xml` and a non-empty manifested Unity log;
- for the Windows entrypoint, two player logs plus a manifested Windows smoke
  summary recording two startups, restart proof, two graceful-close requests,
  and exit codes `[0, 0]`.

A manifest can truthfully describe an invalid session. Integrity validation does
not turn that session valid.

## Required human review JSON

The review must be strict canonical JSON: UTF-8, sorted object keys, compact
separators, no duplicate keys, and exactly one final LF. Its top-level fields
are:

| Field | Meaning |
|---|---|
| `protocol` | Exact Stage 1 protocol schema and version |
| `reviewId`, `reviewerId` | Stable human review identities |
| `bindings` | Immutable package/session fingerprints |
| `preparation` | Confirmed fresh output, manifest, inventory, identity/config, and opened proof |
| `execution` | Diagnostic-command use, restart, lineage, end, and cleanup observations |
| `technicalClassification` | Immutable source reasons mapped one-for-one to CS-012 |
| `shellReview` | Human shell questions and stable failure codes |
| `gameplayObservation` | Independent optional positive/mixed/negative/not-recorded observation |
| `signoff` | Human decision and explicit no-automatic-approval acknowledgement |

Every preparation field must be `true`. Every valid session shell/execution
answer must be `true` and `failureCodes` must be empty. An invalid session may
have failed answers, but every failure must be represented by at least one
stable failure code.

## Human shell-review questions

The reviewer answers these without inferring gameplay quality:

1. Did the accepted shell start and expose the intended evidence surface?
2. Did the displayed/recorded identity and configuration match the manifested
   EH-001/EH-002 facts?
3. Was restart observed with a fresh attempt identity and retained parent audit
   lineage?
4. Did the session end cleanly with no stale marker, subscription, test object,
   intent, or shell-owner state?
5. Were diagnostic commands absent or restricted to evidence-safe deterministic
   setup, inspection, or performance capture?

Fault injection, mission-state override, or progression override is never
accepted as evidence-safe. Its observation must be classified with the relevant
CS-012 technical reason and the session must be rerun.

## Technical validity and monotonic classification

`technicalClassification.status` must exactly match the immutable manifest
`artifactStatus`. `sourceInvalidityReasons` must exactly preserve the complete,
sorted manifest reason list. For each source reason there must be exactly one
sorted classification object containing:

```json
{
  "classificationBasisCode": "classification.missing-required-proof",
  "cs012ReasonCode": "missing-required-asset",
  "sourceReasonCode": "evidence.missing-asset"
}
```

The basis code records why the reviewer selected the mapping. The source reason
cannot be removed, replaced, duplicated, or cleared by later observations.
Only the 17 CS-012 v1 reason tokens are accepted:

```text
duplicate-run-start
run-end-without-start
duplicate-run-end
event-after-run-end
missing-run-start
missing-run-end
crash-before-run-end
run-aborted
invalidating-diagnostic-command
fault-injection-used
mission-state-override-used
progression-override-used
performance-budget-breach
unhandled-exception
timeout
missing-required-asset
diagnostics-capacity-reached
```

A technically invalid session must set `rerunRequired=true` and sign off as
`reject-and-rerun`. A technically valid session must set `rerunRequired=false`
and may sign off as `technical-evidence-admissible`. Validation is not project
approval; `automaticApprovalNotGranted` must be `true` in both cases.

## Gameplay/fun observation remains separate

`gameplayObservation.outcome` may be `not-recorded`, `positive`, `mixed`, or
`negative`. It never changes technical validity:

- a valid session may have a negative gameplay observation and remain technically
  admissible;
- an invalid session may contain a positive observation, but the technical
  session remains rejected and must be rerun.

No gameplay threshold, sentiment inference, score, or automatic fun classifier
exists in this protocol.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Package and human review are complete; technical evidence is admissible |
| `2` | Package or review rejected as malformed, incomplete, inconsistent, or drifted |
| `3` | Review is complete, but the immutable session is technically invalid and rerun is mandatory |

The JSON result always states `automaticApprovalGranted:false`.

## Completed valid-session review example

This deterministic test fixture is technically valid while recording a negative
gameplay observation. The negative observation does not invalidate it.

```json
{"bindings":{"attemptId":"attempt.eh010-sample-1","configurationSha256":"sha256:134bcee4b92935cd6627b1d71d291202e07ab876c38e215abf6fa9aad2286204","identityRecordFingerprint":"sha256:b780b23c58155368e5ef4ea6fb65a9d1184148f96b8a6685854d9da89038429d","identitySha256":"sha256:e1700e3d372128e0dd8c66d05ce52068123d53c7427f988b40c4105e5822fdf5","manifestSha256":"sha256:d90c016150b388469b1cb37990c66edb23983e1686bb8f869a87e20cd7acdba8","sessionId":"session.eh010-sample"},"execution":{"cleanupObserved":true,"diagnosticCommandUse":"none-or-evidence-safe","freshAttemptIdObserved":true,"parentAuditTrailObserved":true,"restartObserved":true,"sessionEndedObserved":true},"gameplayObservation":{"observationCode":"observation.not-fun-yet","outcome":"negative"},"preparation":{"freshOutputConfirmed":true,"identityConfigurationMatchConfirmed":true,"inventoryConfirmed":true,"manifestChecksumConfirmed":true,"requiredArtifactsOpened":true},"protocol":{"schema":"shooter-mover.stage1-evidence-protocol","version":1},"reviewId":"review.eh010-sample-001","reviewerId":"reviewer.local-human","schema":"shooter-mover.stage1-evidence-review","shellReview":{"cleanEndAndCleanupObserved":true,"failureCodes":[],"identityAndConfigurationObserved":true,"restartAndLineageObserved":true,"startupAndShellObserved":true},"signoff":{"automaticApprovalNotGranted":true,"decision":"technical-evidence-admissible","humanConfirmed":true,"reviewComplete":true},"technicalClassification":{"classifications":[],"rerunRequired":false,"sourceInvalidityReasons":[],"status":"valid"},"version":1}
```

Validator command result, exit code `0`:

```json
{"attemptId":"attempt.eh010-sample-1","automaticApprovalGranted":false,"cs012ReasonCodes":[],"entrypoint":"playmode","gameplayObservation":{"observationCode":"observation.not-fun-yet","outcome":"negative"},"humanDecision":"technical-evidence-admissible","manifestSha256":"sha256:d90c016150b388469b1cb37990c66edb23983e1686bb8f869a87e20cd7acdba8","protocol":{"schema":"shooter-mover.stage1-evidence-protocol","version":1},"rerunRequired":false,"reviewId":"review.eh010-sample-001","reviewSha256":"sha256:c699ee2883fad0cd7e7aed9d37a341a9af428631d1fc92714f8bb895dda009da","schema":"shooter-mover.evidence-session-validation","sessionId":"session.eh010-sample","sourceInvalidityReasons":[],"technicalStatus":"valid","technicallyAdmissible":true,"tool":{"name":"validate_evidence_session.py","version":"1.0.0"},"validationOutcome":"review-complete","version":1}
```

## Verification

Run the complete focused suite:

```powershell
python -S -m unittest -v tools.evidence.tests.test_validate_evidence_session
```

Coverage includes valid review, checksum drift, missing proof, conflicting
validity, unsupported protocol, duplicate/missing/incomplete review, exact
identity/configuration binding, unmanifested files, missing classification basis,
unsupported CS-012 reasons, monotonic classification, Windows two-pass proof,
and CLI exit codes `0` and `3`.

## Rollback

Remove the validator, its focused tests, and these two EH-010 documents together.
Do not weaken EH-008 manifest integrity or EH-009 failure semantics as a
replacement. No runtime, gameplay, scene, content package, registry, save,
telemetry, or remote service depends on this protocol.
