# EH-006 Session Reset and Restart

## Status and scope

EH-006 adds a test-owned evidence-session lifecycle and rapid restart probe. It
is limited to session orchestration for Stage 1 evidence runs. It does not add
save data, death or respawn behavior, progression, scoring, gameplay authority,
scenes, prefabs, or project settings.

The lifecycle consumes the merged contracts and evidence shells:

- **CS-003 Player Intent v1**: attempt cleanup converts the current intent to the
  focus-loss boundary frame and then installs `PlayerIntentFrame.Neutral` before
  another attempt may start.
- **CS-012 Diagnostics and Run Validity v1**: completed restarts retain a typed
  `RunRestarted` audit fact with the previous attempt ID; completed endings retain
  the accepted `RunEndKind` fact.
- **EH-002 deterministic evidence configuration**: one immutable configuration
  and fingerprint are retained for the whole parent session.
- **EH-003 local diagnostics recorder**: EH-006 produces canonical lifecycle and
  typed diagnostic audit facts that a recorder owner may consume. EH-006 does not
  create files or replace EH-003 storage ownership.
- **EH-004 benchmark arena shell**: the canonical start binds to
  `socket.player.primary` without retaining a Unity object reference.
- **EH-005 short-route shell**: the canonical start binds to `route.start` and
  mirrors the shell's explicit restart/reset ownership.

## State model

The explicit states are:

1. `Configured`
2. `Starting`
3. `Running`
4. `Restarting`
5. `Ending`
6. `Ended`
7. `Invalid`

Normal execution follows:

```text
Configured -> Starting -> Running
Running -> Restarting -> Running
Running -> Ending -> Ended
```

Cancellation or interrupted work may enter `Ending` from `Configured`,
`Starting`, `Restarting`, or `Invalid`. `CompleteEnd` is idempotent after
`Ended`. Other illegal requests are rejected and fail closed to `Invalid`
(except that terminal `Ended` remains terminal).

An explicit `Invalidate(reasonCode)` records a stable error code, clears any
pending restart attempt, and moves the session to `Invalid`. An invalid session
must pass through `Ending` before `Ended`.

## Canonical identity and attempt lineage

`EvidenceSessionStartIdentity` is immutable and contains only stable values:

- EH-002 configuration fingerprint;
- build/content identity reference;
- run seed;
- intent fixture version;
- EH-004 player-start marker ID;
- EH-005 route-start marker ID.

The parent `sessionId` and this canonical start identity never change during a
quick restart. Every accepted restart must provide a fresh `attemptId`. The new
attempt receives:

- an incremented ordinal;
- the same parent session ID;
- the same configuration object and fingerprint;
- the same canonical start identity;
- the previous attempt ID as `parentAttemptId`.

Attempt IDs are never reused, including an interrupted pending attempt. This
keeps the audit lineage unambiguous.

## Restart cleanup probe

`EvidenceRestartProbe` owns only disposable PlayMode test resources:

- one attempt root object;
- three stable marker objects (`session.start`, `route.start`, and
  `socket.player.primary`);
- one attempt-local signal subscription;
- one current CS-003 intent frame.

Beginning a restart releases all current attempt resources before the pending
attempt can become `Running`. Cleanup:

1. creates the CS-003 focus-loss boundary frame;
2. replaces intent with `PlayerIntentFrame.Neutral`;
3. unsubscribes the attempt callback;
4. destroys every attempt-owned Unity object immediately;
5. clears the marker set.

The replacement attempt is activated only when no marker, subscription, or
owned object remains. Duplicate marker creation and resource activation over a
live attempt fail closed.

The probe exposes local and global sentinels for active probes, subscriptions,
owned Unity objects, retired-object leaks, marker uniqueness, observed callback
count, and stale intent. It uses `HideAndDontSave` objects and does not create or
modify a scene asset.

## Focused PlayMode coverage

`EvidenceSessionLifecycleTests` covers:

- all legal start, run, restart, ending, and ended transitions;
- rejected premature completion and duplicate attempt IDs;
- terminal end idempotency;
- canonical identity and configuration preservation across restart;
- parent-attempt audit lineage and CS-012 restart payloads;
- CS-003 focus-loss clearing of held intent;
- replacement of markers, subscription, and owned Unity objects;
- interrupted restart invalidation and deterministic cleanup;
- fifty consecutive synchronous quick restarts with exact marker,
  subscription, object, callback, audit, and stale-intent sentinels.

The fifty-cycle test expects:

```text
cycles=50
finalAttemptOrdinal=51
beginRestartAuditCount=50
completeRestartAuditCount=50
runRestartedDiagnosticCount=50
activeMarkers=3 while running, 0 after end
activeSubscriptions=1 while running, 0 after end
activeOwnedObjects=4 while running, 0 after end
retiredObjectLeaks=0
staleIntent=false
```

It also applies a five-second local PlayMode sentinel to the fifty synchronous
restart calls. This is a regression sentinel, not a shipping performance budget.

## Validation status

The focused PlayMode tests are authored at:

`Assets/ShooterMover/Tests/PlayMode/EvidenceHarness/EvidenceSessionLifecycleTests.cs`

This change was prepared through the authenticated GitHub connector. Unity Test
Runner execution is **pending** because the connector does not provide a Unity
editor or project test process. No Unity execution is claimed here.

Required proof before promotion from draft:

1. run the focused PlayMode test class in the pinned Unity editor;
2. attach the test log showing every EH-006 test passing;
3. attach the emitted fifty-cycle summary;
4. perform a human review of start, quick restart, interrupted restart, and end;
5. confirm no `__EH006_` objects remain after the test class completes.

## Limitations

- EH-006 does not load, unload, or mutate EH-004/EH-005 scenes. It binds only to
  their stable accepted marker identities.
- EH-006 does not instantiate `EvidenceDiagnosticsRecorder`; it emits canonical
  audit facts and typed CS-012 payloads without claiming EH-003 file ownership.
- Attempt IDs are caller-supplied stable IDs so test orchestration remains
  deterministic and audit-friendly.
- The harness is test support, not player-facing restart, death, respawn, save,
  progression, or scoring behavior.

## Rollback

Rollback removes these files and their Unity metadata together:

- `Assets/ShooterMover/TestSupport/EvidenceHarness/EvidenceSessionLifecycle.cs`
- `Assets/ShooterMover/TestSupport/EvidenceHarness/EvidenceRestartProbe.cs`
- `Assets/ShooterMover/Tests/PlayMode/EvidenceHarness/EvidenceSessionLifecycleTests.cs`
- `docs/verification/evidence-harness/SESSION_RESET_AND_RESTART.md`

No save migration, scene repair, prefab repair, registry update, package change,
or remote-data cleanup is required.
