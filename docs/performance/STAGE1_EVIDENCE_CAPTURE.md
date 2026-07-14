# Stage 1 Evidence Performance Capture

## Status and scope

This document defines the **provisional, evidence-only** Stage 1 performance capture procedure implemented by `EvidencePerformanceProbe` and configured by `tools/evidence/fixtures/performance-budget-stage1-v1.json`.

The probe observes caller-supplied timestamps and counters. It does not read or change `Time.timeScale`, Unity quality settings, simulation rules, collision, scene contents, enemy or weapon populations, rewards, progression, or any other gameplay state. The fixture values are review thresholds for representative evidence; they are not a hardware certification or a hidden tuning profile.

## Dependencies

EH-007 consumes the merged implementations without modifying them:

- CS-012 Diagnostics and Run Validity v1 — PR #43;
- EH-003 bounded local diagnostics recorder — PR #59;
- EH-004 Stage 1 benchmark arena shell — PR #61;
- EH-005 Stage 1 short-route shell — PR #66.

The optional `EvidenceDiagnosticsRecorder` integration emits only accepted CS-012 payloads. EH-004 and EH-005 remain scene owners; the probe accepts their externally observed load durations and configured object counters but never edits either scene.

## Versioned provisional budget

`performance-budget-stage1-v1.json` records:

| Field | Stage 1 v1 value | Purpose |
| --- | ---: | --- |
| Warm-up | 2 seconds | Excluded from frame, allocation, memory, and object-counter samples |
| Capture | 10 seconds | Bounded representative sample window |
| Completion overrun tolerance | 0.25 seconds | Allows small scheduling delay before an overrun invalidity |
| Maximum frame samples | 1,200 | Hard payload bound independent of frame rate |
| Maximum quality observations | 16 | Bounds profile-change annotations |
| Maximum object counters | 16 | Bounds configured and per-frame counter payloads |
| p95 frame-time warning | 16.667 ms | Provisional review threshold |
| p99 frame-time warning | 33.333 ms | Provisional review threshold |
| Total managed allocation warning | 1,048,576 bytes | Capture-window aggregate |
| Per-frame managed allocation warning | 262,144 bytes | Peak accepted frame sample |
| Scene-load warning | 5,000 ms | Total recorded load duration |
| Memory warning | 536,870,912 bytes | Peak caller-supplied memory observation |

The object counters are stable IDs with explicit maxima:

- `object.arena-fixture`: 1;
- `object.short-route-fixture`: 1;
- `object.evidence-markers`: 20.

`budgetBreachInvalidatesTechnicalEvidence` is `false` in v1. A threshold breach therefore creates a CS-012 warning but does not, by itself, claim the evidence session is technically invalid. Measurement-integrity failures always invalidate.

## Capture lifecycle

1. Load the canonical EH-002 evidence configuration and start an EH-003 diagnostics session when a formal run is being recorded.
2. Load either the EH-004 arena shell or EH-005 short-route shell through its existing owner API. Record the externally observed start and end timestamps with `RecordSceneLoad`.
3. Construct `EvidencePerformanceProbe` with the versioned budget and, optionally, the active `EvidenceDiagnosticsRecorder`.
4. Call `Begin(monotonicSeconds, qualityProfileId)`. The profile ID is an annotation only and must use bounded lowercase ASCII identifiers.
5. During warm-up, calls to `RecordFrame` are accepted as clock observations but excluded from all capture metrics.
6. During the capture window, call `RecordFrame` with total frame time, managed allocation bytes, memory bytes, and the configured object-counter samples. Unconfigured, duplicate, negative, or oversized counter payloads are rejected.
7. Call `RecordQualityProfile` only when the externally observed profile identity changes. The method records the identity and timestamp; it has no API that can apply a profile or change gameplay.
8. Call `Complete` at the capture boundary. Completion is idempotent and returns one immutable canonical summary.
9. Finalize and export the EH-003 session locally through its existing bounded workflow. Do not upload or transmit the capture.

## Recorded summary

The immutable summary contains:

- configured warm-up and capture duration;
- observed capture duration and accepted frame count;
- p50, p95, and p99 total-frame-time percentiles;
- total and peak per-frame managed allocation bytes;
- total scene-load duration and scene-load sample count;
- peak memory bytes;
- maximum observed value, configured maximum, and availability for every object-budget counter;
- initial quality profile and bounded profile-change observations;
- stable issue codes and typed CS-012 payloads.

Percentiles sort the accepted capture samples, calculate rank `p * (sampleCount - 1)`, and linearly interpolate between adjacent values. Warm-up samples are never included.

## CS-012 warning and invalidity mapping

| Condition | Stable issue code | CS-012 payload | Technical outcome |
| --- | --- | --- | --- |
| Capture completed before its required duration | `performance.incomplete-capture-duration` | Invalidating performance warning | `PerformanceBudgetBreach` |
| No accepted frame samples | `performance.incomplete-frame-samples` | Invalidating performance warning | `PerformanceBudgetBreach` |
| No scene-load observation | `performance.incomplete-scene-load` | Invalidating performance warning | `PerformanceBudgetBreach` |
| Clock is non-finite, negative, or moves backward | `performance.invalid-clock` | Invalidating performance warning | `PerformanceBudgetBreach` |
| Frame, allocation, or quality observation capacity reached | corresponding `performance.*-capacity` code | Invalidating performance warning | `PerformanceBudgetBreach` |
| Configured object counter unavailable | `performance.object-counter-missing` plus counter subject ID | Invalidating performance warning | `PerformanceBudgetBreach` |
| Capture sample or completion exceeds the bounded window | `performance.capture-overrun` | Invalidating timeout | `Timeout` |
| Provisional numeric threshold exceeded | metric-specific `performance.*-budget` code | Performance warning | Controlled by the versioned fixture |

CS-012 v1 has no dedicated object-count metric enum. Object-budget warnings therefore retain the stable `performance.object-budget` code, the counter stable ID as the subject, and unit `count` in the accepted performance-warning envelope. Consumers must use those typed annotations rather than interpreting it as a byte measurement.

Invalidity is monotonic because the emitted payloads are consumed by the existing CS-012 `RunValidityAccumulator`. Later good samples or quality changes cannot clear an integrity failure.

## Required representative proof

The implementation test suite covers warm-up exclusion, percentile calculation, capture bounds, incomplete data, invalid clocks, overruns, object counters, managed allocations, memory, scene loads, CS-012 publication, and quality-profile recording.

The following proof remains pending until it is actually run in the pinned Unity editor or Windows evidence entrypoint:

- `EvidencePerformanceProbeTests` execution log;
- empty EH-004 arena-shell baseline summary;
- empty EH-005 short-route-shell baseline summary;
- reviewer confirmation that the locally exported summaries match the versioned budget and configuration identity.

No Unity execution, representative gameplay population, optimization result, or hardware performance pass is claimed by this document.

## Limitations

- The probe does not select a platform-specific clock or profiler API. The caller must provide one monotonic timestamp source and consistent counter definitions.
- GPU timing is not synthesized. A later representative adapter may add an explicit supported GPU observation without changing this capture contract.
- Object-budget values describe configured evidence-shell counters only. They are not stress-population targets.
- Thresholds are provisional and must be changed only by replacing the versioned fixture through review, never by hidden runtime behavior.

## Rollback

Remove `EvidencePerformanceProbe.cs`, its focused test, the v1 budget fixture, this document, and their inseparable Unity metadata. No gameplay, scene, save, registry, or quality asset requires migration because none depends on the probe.
