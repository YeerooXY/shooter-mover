# Run Validity and Local Diagnostics

## Status

EH-003 implements the local/offline recorder that consumes:

- **CS-002 Identity v1** through `BuildIdentity`;
- **CS-012 Diagnostics and Run Validity v1** through typed diagnostic payloads,
  envelopes, redactions, capacity events, and monotonic validity accumulation;
- **EH-002 Evidence Configuration v1** through the exact
  `EvidenceRunConfiguration.Diagnostics` bounds and configuration fingerprint.

The recorder is evidence support only. It is not gameplay authority, telemetry,
analytics, persistence, or a crash-reporting service.

## Owned implementation

The recorder lives at:

```text
Assets/ShooterMover/TestSupport/EvidenceHarness/EvidenceDiagnosticsRecorder.cs
```

One recorder instance owns one `StableId` run and one local directory. Construction
creates a new part-000 log with `FileMode.CreateNew`; existing evidence is never
overwritten. A caller supplies the already-loaded canonical EH-002 configuration
and the local directory. The directory is operational input and is never serialized
inside an event or log header.

## Captured facts

The public recorder methods emit the corresponding CS-012 payloads:

| Recorder operation | CS-012 payload |
|---|---|
| `StartSession` | `RunStartedDiagnosticPayload` with CS-002 `BuildIdentity` |
| `EndSession` | `RunEndedDiagnosticPayload` |
| `RestartSession` | `RunRestartedDiagnosticPayload` |
| `RecordDiagnosticCommand` | `DiagnosticCommandDiagnosticPayload` |
| `RecordException` | `ExceptionDiagnosticPayload` |
| `RecordTimeout` | `TimeoutDiagnosticPayload` |
| `RecordMissingAsset` | `MissingAssetDiagnosticPayload` |
| `RecordPerformanceWarning` | `PerformanceWarningDiagnosticPayload` |
| `FinalizeSession` | `EvidenceValidityDiagnosticPayload`, unless capacity already closed the log |

`Record` also accepts any existing v1 `DiagnosticEventPayload`. `Append` accepts an
already-built envelope only when schema, run ID, configured contract bounds, and
next sequence match exactly. Rejected malformed events do not mutate the file,
event list, or validity accumulator.

Ordering is the strictly increasing `DiagnosticEventSequence`. Wall-clock time is
not recorded, so clock corrections cannot reorder or rewrite evidence.

## Local log format

Each retained part begins with a small LF-only header:

```text
log_schema=shooter-mover.evidence-diagnostics-log
log_version=1
run_id=<StableId>
configuration_fingerprint=<EH-002 SHA-256>
part_index=<zero-based index>
---
```

Each append is length-framed:

```text
event_byte_count=<UTF-8 byte count>
<DiagnosticEventEnvelope.ToCanonicalString()>
--event-end--
```

The event byte count covers the complete canonical CS-012 envelope. The file uses
UTF-8 without a BOM and LF line endings. An event is assembled completely before
one append operation. No event is rewritten in place.

## Bounds and rotation

EH-002 provides four storage limits:

- `maxEventCount`;
- `maxEventPayloadBytes`;
- `maxLogBytes`;
- `retainedLogCount`.

The recorder maps `maxEventCount` into CS-012 `DiagnosticBounds` and rejects a
configuration above the CS-012 v1 hard maximum. Payload bytes are measured over
the typed payload plus canonical attributes. File bytes include the header,
framing, and full envelope.

`retainedLogCount` is the maximum total number of retained parts, including the
active part. Rotation creates the next numbered part and never deletes an older
part. Every ordinary write reserves enough bytes for a final capacity event.
Therefore:

- event-count overflow appends `CapacityReached(EventCount, limit)`;
- payload overflow appends `CapacityReached(PayloadBytes, limit)`;
- an event that cannot fit an otherwise empty bounded part appends
  `CapacityReached(FileBytes, limit)`;
- a required rotation beyond the retained-part count appends
  `CapacityReached(RetainedRotations, limit)`.

A capacity event is final, marks `WasTruncated`, adds
`diagnostics-capacity-reached`, and closes the recorder to further events. Loss is
never silently hidden by deleting or wrapping old records.

If the local filesystem rejects the final write, the recorder closes fail-safe,
retains an in-memory `CapacityReached(FileBytes, limit)` event, and exposes
`WriteFailureCode=local-write-failed`. The file cannot truthfully claim bytes that
the operating system refused to store; callers must treat that explicit failure
as invalid evidence and surface it in the harness result.

## Redaction

`EvidenceDiagnosticField` is the only raw-field entry point. Before creating the
CS-012 attribute, the recorder:

1. honors an explicit typed redaction;
2. redacts secret/credential markers;
3. redacts email-like personal data;
4. redacts machine-local paths and traversal forms;
5. redacts URLs/query identifiers;
6. falls back to `unnecessary-free-text` when the value is outside the CS-012
   privacy-safe canonical ASCII subset or configured public-value bound.

Unsafe raw text is not retained in the envelope, in-memory event list, or local
file. Safe values still pass through `DiagnosticAttribute.CreatePublic`, so
CS-012 remains the final privacy grammar.

Do not place arbitrary exception messages, stack traces, usernames, device names,
IP addresses, email addresses, tokens, endpoints, or filesystem paths into public
attributes. Use stable error/operation/asset codes and typed redaction.

## Technical validity versus human evidence

Technical validity is accumulated by immutable `RunValidityAccumulator` values.
Every retained event is applied in sequence. A later safe event cannot remove a
previous reason. Finalization adds lifecycle reasons such as missing start/end or
crash-before-end exactly as defined by CS-012.

`HumanFunEvidence` is supplied separately to `FinalizeSession`. A negative or
mixed observation does not invalidate an otherwise technically valid run.
Conversely, positive feedback does not repair a technically invalid run.
`EvidenceAssessment` carries both values without classifying one from the other.

The tracked fixture
`tools/evidence/fixtures/run-validity-cases-v1.json` freezes representative valid,
invalid, overflow, lifecycle-error, and negative-fun cases. It is review input,
not gameplay state or a generated registry.

## Explicit export

`ExportTo(localDirectory)` is available only after finalization. It copies every
retained part with `FileMode.CreateNew`, never overwrites an existing export, and
accepts no endpoint. Export performs no upload and no background transmission.

## Automated verification

Run the focused Unity 6000.3.19f1 EditMode fixture:

```text
ShooterMover.Tests.EditMode.EvidenceHarness.EvidenceDiagnosticsRecorderTests
```

Coverage includes:

- event sequence and typed capture of identity, lifecycle, commands, exceptions,
  timeouts, missing assets, and performance warnings;
- bounded rotation and per-file byte caps;
- automatic redaction with raw-byte absence;
- explicit overflow/capacity closure;
- duplicate start;
- end without start;
- monotonic invalidity and technical/fun separation;
- malformed envelope rejection before mutation;
- explicit local export without overwrite.

The valid/invalid test writes complete example logs to `TestContext` for review.

## Manual/offline proof

With networking disabled:

1. Load the exact EH-002 fixture and substitute the actual EH-001 identity
   fingerprint in a local copy.
2. Create a run with start, one non-invalidating performance warning, completed
   end, and final validity. Inspect every retained part and confirm technical
   validity is `valid`.
3. Create a second run with start, one `MissingAsset` event, completed end, and
   final validity. Confirm `missing-required-asset` remains present.
4. Supply negative human feedback to the valid run and confirm technical validity
   stays valid.
5. Record a path and token through `EvidenceDiagnosticField.Public`; confirm only
   typed redaction markers appear.
6. Reduce each EH-002 bound in a temporary local fixture and inspect event-count,
   payload, file, and retained-rotation capacity events.
7. Confirm no network access is required and no older log part is deleted.

Attach the focused test log plus one valid and one intentionally invalid local log
before moving the PR out of draft.

## Limitations and non-goals

- No analytics SDK, crash-reporting SaaS, upload, network transport, remote
  endpoint, database, or background worker.
- No gameplay-state mutation, mission authority, save state, registry, content
  discovery, or durable progression.
- No general-purpose log parser or arbitrary free-text logger.
- No guarantee can make a failed disk write persist; the recorder reports the
  failure and invalidates in memory rather than claiming false bytes.
- The recorder is sequential and process-local. Cross-process writers must use
  separate run IDs/directories.
- Bounds are evidence-safety limits, not gameplay or performance tuning.

## Rollback

Remove the recorder and paired metadata, focused tests and paired metadata,
fixture, and this document. No gameplay consumer, save data, scene, prefab,
content registry, package, project setting, or durable mission state depends on
EH-003.
