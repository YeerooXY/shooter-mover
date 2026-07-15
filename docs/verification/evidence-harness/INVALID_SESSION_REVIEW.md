# Invalid Session Review and Mandatory Rerun

## Rule

A technical invalidity is a reliability/evidence failure, not a negative vote on
Shooter Mover gameplay. Once an EH-008 manifest contains an invalidity reason,
EH-010 review is monotonic: the reviewer may map and explain the reason, but may
not remove, repair, override, downgrade, or relabel the session as valid.

Every technically invalid session is rejected as technical evidence and requires
an entirely new EH-009 run in a fresh empty output directory.

## Classification procedure

For each immutable manifest `invalidityReasons` entry:

1. preserve the exact source reason in `sourceInvalidityReasons`;
2. create exactly one classification entry;
3. select exactly one CS-012 v1 reason token;
4. provide a stable `classificationBasisCode` explaining the mapping;
5. record any failed shell question with a stable `failureCodes` entry;
6. set `technicalClassification.status` to `invalid`;
7. set `technicalClassification.rerunRequired` to `true`;
8. sign off with `decision=reject-and-rerun`.

A later safe event, positive gameplay observation, successful cleanup attempt, or
reviewer preference cannot clear an earlier reason. Missing or duplicated source
reasons, unsupported CS-012 tokens, or classification entries without a basis
code are rejected.

## CS-012 v1 reason guide

| CS-012 reason | Use when the manifested evidence shows |
|---|---|
| `duplicate-run-start` | More than one start for the same run |
| `run-end-without-start` | End recorded before any start |
| `duplicate-run-end` | More than one end for the same run |
| `event-after-run-end` | Technical event appended after terminal end |
| `missing-run-start` | Final package has no required run start |
| `missing-run-end` | Non-crash run never reached terminal end |
| `crash-before-run-end` | Crash occurred before terminal end |
| `run-aborted` | Run ended through explicit abort |
| `invalidating-diagnostic-command` | A command explicitly invalidated technical evidence |
| `fault-injection-used` | Fault injection was used during the frozen run |
| `mission-state-override-used` | Mission state was overridden |
| `progression-override-used` | Progression state was overridden |
| `performance-budget-breach` | Frozen evidence performance budget was breached |
| `unhandled-exception` | An unhandled exception occurred |
| `timeout` | An invalidating timeout occurred |
| `missing-required-asset` | Required shell/build/proof asset was unavailable |
| `diagnostics-capacity-reached` | Bounded diagnostics truncated or reached capacity |

Checksum drift, inventory drift, missing manifested proof, unsupported protocol
versions, and incomplete review are validator rejections rather than opportunities
to rewrite the source package. Correct the cause and rerun; do not regenerate a
manifest over altered evidence and present it as the same attempt.

## Rerun procedure

1. Preserve the invalid package and its review for audit; do not mutate either.
2. Diagnose the technical cause locally using the manifested files.
3. Correct the prerequisite, harness, command usage, asset, timeout, capacity, or
   lifecycle issue outside the invalid package.
4. Select a new missing or empty output directory.
5. Execute the appropriate EH-009 entrypoint again.
6. Require a new attempt ID. Restart lineage must remain visible where applicable.
7. Produce a new EH-008 manifest/checksum and a new, separately bound human review.
8. Validate the new package. The prior invalid session remains invalid forever.

No automatic repair, approval, reclassification, deletion, upload, telemetry, or
external study occurs.

## Completed intentionally invalid-session example

This example has immutable source reason `evidence.missing-asset`, explicitly
mapped to CS-012 `missing-required-asset`. Shell restart/cleanup observations are
failed and explained. The gameplay observation is deliberately positive to prove
that positive feedback cannot repair technical invalidity.

```json
{"bindings":{"attemptId":"attempt.eh010-sample-1","configurationSha256":"sha256:134bcee4b92935cd6627b1d71d291202e07ab876c38e215abf6fa9aad2286204","identityRecordFingerprint":"sha256:b780b23c58155368e5ef4ea6fb65a9d1184148f96b8a6685854d9da89038429d","identitySha256":"sha256:e1700e3d372128e0dd8c66d05ce52068123d53c7427f988b40c4105e5822fdf5","manifestSha256":"sha256:aceec360b6f51b91be230d459f49351b94e38fb4c7fd9b1685968e9b46605638","sessionId":"session.eh010-sample"},"execution":{"cleanupObserved":false,"diagnosticCommandUse":"none-or-evidence-safe","freshAttemptIdObserved":false,"parentAuditTrailObserved":false,"restartObserved":false,"sessionEndedObserved":true},"gameplayObservation":{"observationCode":"observation.not-fun-yet","outcome":"positive"},"preparation":{"freshOutputConfirmed":true,"identityConfigurationMatchConfirmed":true,"inventoryConfirmed":true,"manifestChecksumConfirmed":true,"requiredArtifactsOpened":true},"protocol":{"schema":"shooter-mover.stage1-evidence-protocol","version":1},"reviewId":"review.eh010-sample-001","reviewerId":"reviewer.local-human","schema":"shooter-mover.stage1-evidence-review","shellReview":{"cleanEndAndCleanupObserved":false,"failureCodes":["review.restart-cleanup-failed"],"identityAndConfigurationObserved":true,"restartAndLineageObserved":false,"startupAndShellObserved":true},"signoff":{"automaticApprovalNotGranted":true,"decision":"reject-and-rerun","humanConfirmed":true,"reviewComplete":true},"technicalClassification":{"classifications":[{"classificationBasisCode":"classification.missing-required-proof","cs012ReasonCode":"missing-required-asset","sourceReasonCode":"evidence.missing-asset"}],"rerunRequired":true,"sourceInvalidityReasons":["evidence.missing-asset"],"status":"invalid"},"version":1}
```

Validator command result, exit code `3`:

```json
{"attemptId":"attempt.eh010-sample-1","automaticApprovalGranted":false,"cs012ReasonCodes":["missing-required-asset"],"entrypoint":"playmode","gameplayObservation":{"observationCode":"observation.not-fun-yet","outcome":"positive"},"humanDecision":"reject-and-rerun","manifestSha256":"sha256:aceec360b6f51b91be230d459f49351b94e38fb4c7fd9b1685968e9b46605638","protocol":{"schema":"shooter-mover.stage1-evidence-protocol","version":1},"rerunRequired":true,"reviewId":"review.eh010-sample-001","reviewSha256":"sha256:c4e1113c4fa20b8c8184e209879440a8c8a92d5e002a04a7e991e999fe06ea32","schema":"shooter-mover.evidence-session-validation","sessionId":"session.eh010-sample","sourceInvalidityReasons":["evidence.missing-asset"],"technicalStatus":"invalid","technicallyAdmissible":false,"tool":{"name":"validate_evidence_session.py","version":"1.0.0"},"validationOutcome":"invalid-session-rerun-required","version":1}
```

The correct next action is a fresh rerun, not approval of this session.

## Rejection examples

The validator returns exit code `2` for malformed or unreviewable inputs,
including:

- manifest/checksum or artifact drift;
- missing required proof or unmanifested files;
- descriptor, diagnostics, identity, configuration, build, or validity conflicts;
- unsupported manifest, descriptor, diagnostics, Windows proof, review, or
  protocol version;
- zero or multiple human reviews;
- incomplete preparation, shell answers, failure codes, classification basis, or
  sign-off;
- dropped, duplicated, reordered, or unsupported invalidity classification;
- a valid decision applied to invalid evidence, or `rerunRequired=false` for an
  invalid session.

A rejection with `rerunRequired:true` means the evidence attempt itself cannot be
trusted and must be recreated. A review-format rejection can be corrected only by
writing a complete new review bound to the unchanged package; the validator still
never edits it automatically.
