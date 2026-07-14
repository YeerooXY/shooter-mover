# Diagnostics and Run Validity v1

## Status and scope

Diagnostics and Run Validity v1 is the engine-independent contract for bounded,
local, offline diagnostic facts and evidence validity. It defines values only. It
does not write logs, rotate files, capture exceptions, execute commands, export
files, upload data, or classify whether play was fun.

The contract consumes:

- **Identity v1**: `BuildIdentity` is recorded at run start and in support-bundle
  manifests;
- **Mission Messages v1**: diagnostic command facts bind to
  `MissionPayloadVersion` and the observed `MissionSequence` rather than creating
  a second mission protocol.

The only accepted diagnostics schema is `diagnostics_schema_version=1`.
Unsupported versions fail closed.

## Contract families

### Bounded diagnostic events

`DiagnosticEventEnvelope` carries:

1. schema version;
2. event ID and run ID;
3. a positive, run-local event sequence;
4. typed severity and event kind;
5. one closed immutable payload;
6. zero or more canonically ordered structured attributes.

`DiagnosticBounds` explicitly caps retained event count, attributes per event,
public attribute value length, support-bundle item count, and support-bundle
bytes. Hard v1 maxima prevent an untrusted configuration from declaring
unbounded values.

`DiagnosticEventBatch` copies and orders shuffled input by event sequence,
requires one schema/bounds/run identity, rejects duplicate sequences, and
requires an event-count-full batch to end in an explicit `capacity-reached`
event. Other recorder-owned capacity dimensions are represented by typed
`payload-bytes`, `file-bytes`, and `retained-rotations` capacity events. CS-012
does not implement their storage or rotation mechanics.

V1 event kinds are:

- run started, ended, and restarted;
- diagnostic command;
- performance warning;
- exception and timeout;
- missing asset;
- crash;
- capacity reached;
- evidence validity.

The list is intentionally closed. A new event kind requires a reviewed contract
version rather than an opaque free-text event.

### Privacy-safe attributes

A public `DiagnosticAttribute` accepts only a bounded canonical ASCII value and
rejects obvious personal identifiers, URLs, query strings, traversal-like text,
and machine-local paths. Sensitive values use `CreateRedacted(key, reason)`.
That factory does not accept the original value, so the contract cannot retain
raw personal data accidentally.

Redaction reasons are typed:

- personal data;
- machine-local path;
- secret or credential;
- external identifier;
- unnecessary free text.

Representative canonical attribute:

```text
field.contact=[redacted:personal-data]
```

The contract is deliberately stricter than a general logging API. Producers
should map details to stable error/operation/asset IDs and keep unrelated free
text out of evidence.

## Diagnostic commands

A diagnostic command event records:

- stable diagnostic command ID;
- typed command kind;
- explicit evidence effect;
- Mission Messages v1 payload/content version;
- observed mission sequence.

`deterministic-setup`, `inspection`, and `performance-capture` commands may be
marked evidence-safe when they do not alter authoritative state. The following
commands must be marked `invalidates-technical-evidence`:

- fault injection;
- mission-state override;
- progression override.

The constructor rejects a state-altering command labelled evidence-safe.
Executing or authorizing commands remains outside this contract.

## Performance warnings

A `PerformanceWarningDiagnosticPayload` records a typed metric, finite observed
value, threshold, unit, and explicit evidence effect. The observed value must be
above the warning threshold.

A warning may remain informational for a non-gate capture, or it may invalidate
technical evidence when it breaches the frozen evidence budget. This choice is
explicit in the payload; severity alone never changes validity.

V1 metrics include CPU, GPU, and total frame time, managed allocation, memory,
and loading duration. Broader performance storage or sampling belongs to later
verification owners.

## Monotonic technical validity

`RunValidityAccumulator` consumes events in strictly increasing sequence order
for exactly one run ID. Every `Apply` returns a new immutable value. Once an
invalidity reason is accumulated, no later event removes it.

Final technical validity is `valid` only when the canonical invalidity-reason
set is empty. V1 reasons cover:

- duplicate/missing/misordered run lifecycle events;
- crash before run end and explicit aborted end;
- invalidating diagnostic commands, fault injection, and state/progression
  overrides;
- performance budget breach;
- unhandled exception and invalidating timeout;
- missing required asset;
- diagnostics capacity reached.

A crash observed without a run-end event finalizes as
`crash-before-run-end`, not as a silently incomplete run. A non-crash incomplete
run finalizes as `missing-run-end`. Missing start is also recorded explicitly.

An `EvidenceValidityDiagnosticPayload` records a derived
`RunTechnicalValidity`. It is a summary fact and does not clear or manufacture
validity.

Representative invalid result:

```text
run_id=run.factory-evidence-0001
technical_validity=invalid
invalidity_reason_count=2
invalidity_reason[0]=fault-injection-used
invalidity_reason[1]=performance-budget-breach
```

## Technical evidence is not fun evidence

`EvidenceAssessment` contains two independent values:

- `RunTechnicalValidity`: whether the session is admissible as technical
  evidence;
- `HumanFunEvidence`: a manually supplied positive, mixed, negative, or
  not-recorded outcome with an optional stable observation code.

A technically invalid session may still contain useful voluntary human feedback.
A technically valid session may receive negative fun feedback. Neither value
rewrites the other. There is no automated fun classifier, score inference,
sentiment analysis, or gameplay judgment in v1.

## Privacy-safe support-bundle manifest

`SupportBundleManifest` describes a bounded, explicit, local private export. It
performs no export itself.

The manifest binds:

- support-bundle manifest version and Diagnostics v1 schema;
- `BuildIdentity`;
- run ID and final technical validity;
- configured bounds;
- canonically ordered logical bundle items.

A bundle item uses a `StableId`, typed kind, and disposition:

- **included**: positive byte length and canonical SHA-256 fingerprint;
- **redacted**: no content facts, with a typed redaction reason;
- **omitted**: no content facts, with a typed reason.

The manifest accepts no filesystem path, username, email address, endpoint,
remote destination, or raw free text. It states:

```text
export_scope=local-explicit-private
contains_raw_personal_data=false
```

Included bytes and item count are checked against `DiagnosticBounds`. Duplicate
logical item IDs are rejected. The manifest is suitable for review before an
explicit tester-driven local export; it is not permission to transmit data.

## Canonical ordering and equality

All order-sensitive outputs use ordinal, deterministic ordering:

- events by `DiagnosticEventSequence`;
- attributes by attribute-key `StableId`;
- invalidity reasons by v1 reason order;
- support-bundle items by item kind, then logical item ID.

Inputs are defensively copied. Public collections are read-only views. Equality
and deterministic hashes are based on canonical values, not object identity or
machine-local state.

## Local/offline/private boundary

Diagnostics v1 explicitly does **not** provide:

- a logger, file writer, database, ring buffer, or rotation implementation;
- crash-reporting SaaS, analytics, telemetry, or background transmission;
- upload endpoints, account identity, device fingerprinting, or remote support
  service;
- command execution, cheats, gameplay-state authority, mission mutation, or
  persistence;
- automatic fun, sentiment, skill, achievement, challenge, or record
  classification.

Future adapters must remain local by default, honor the declared bounds and
redaction contract, and require an explicit user/tester export action.

## Rollback and extension

CS-012 owns only the Diagnostics contract subtree, its focused EditMode test,
and this document. Rollback removes those files together; it requires no save
migration, registry regeneration, scene repair, package change, or remote data
cleanup.

Any new event, invalidity reason, command kind, bundle item kind, public-value
rule, or canonical field requires a reviewed versioned extension. V1 data must
never be silently reinterpreted.
