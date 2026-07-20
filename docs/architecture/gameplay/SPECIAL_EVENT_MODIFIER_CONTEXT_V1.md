# Special Event Modifier Context V1

## Purpose

`EVENT-MODIFIER-001` adds a deterministic, engine-neutral boundary for timed special events such as double-drop weekends. Events contribute ordinary `RuntimeModifierDefinitionV1` values. They do not rewrite weapon, enemy, reward, strongbox, or room catalogs.

## Ownership

- `SpecialEventDefinitionV1` describes one versioned event, activation window, priority, overlap policy, explicit exclusions, and modifier descriptors.
- `SpecialEventCatalogV1` validates references, canonicalizes definition order, and owns the content fingerprint.
- `IAuthoritativeEventClockV1` supplies the only time value used by active-event selection.
- `ActiveEventModifierProjectionServiceV1` selects active definitions once for one injected instant and either projects a snapshot or rejects overlapping conflicts.
- `ActiveEventModifierSnapshotV1` is the immutable active-event projection and contains the merged `RuntimeModifierSnapshotV1`.
- `FrozenEventModifierContextV1` is the command/result boundary used by reward generation, drop generation, strongbox opening, and mission-result freezing.

No event class owns reward generation, item selection, wallet mutation, XP mutation, strongbox opening, or mission completion.

## Time semantics

Activation windows use Unix seconds with a start-inclusive/end-exclusive interval:

```text
start <= authoritative instant < end
```

Domain and application event code never reads `DateTime.Now`, `DateTime.UtcNow`, Unity time, or the local operating-system clock. Production composition must inject an implementation of `IAuthoritativeEventClockV1`.

The projection service reads the clock exactly once per projection. That instant is included in the snapshot fingerprint.

## Versioning and canonical identity

V1 records:

- event schema version;
- event content version;
- stable event ID;
- activation window;
- priority;
- overlap mode;
- explicit excluded event IDs;
- open target modifier descriptors;
- catalog schema/content version and fingerprint.

Definitions, exclusions, modifiers, active events, and conflicts are sorted canonically before fingerprinting. Input collection order therefore does not change the result.

## Overlap policy

An active pair is rejected when either condition is true:

1. either definition explicitly excludes the other event ID;
2. either definition uses `Exclusive` overlap mode.

Otherwise both events combine through the existing runtime modifier language. Priority determines deterministic active-event ordering; it does not silently suppress a lower-priority event. A future policy may add priority-winner behavior as a new version rather than changing V1 semantics.

Conflict results contain sorted event IDs, a stable reason code, and a deterministic result fingerprint.

## Modifier targets

The supplied common target constants are:

- `rewards.strongbox-drop-weight`;
- `rewards.money-quantity`;
- `rewards.xp-quantity`.

Targets remain open strings. Unknown future target IDs remain present in the modifier snapshot and have no effect until a consumer explicitly evaluates that exact target. No central event-target enum or switch is introduced.

## Freezing generation and opening results

A reward, drop, opening, or mission command should obtain one successful `ActiveEventModifierSnapshotV1`, call `FreezeForCommand()`, and include at least `FrozenEventModifierContextV1.ActiveEventSnapshotFingerprint` in its own canonical command text.

When later evaluation is required, the command/record retains the complete frozen context. It must not ask the event service to project again. This guarantees that:

- an opening prepared during an event keeps the same modifier set after expiry;
- retry and replay use the exact original fingerprint;
- a changed event catalog cannot reroll an already frozen result;
- downstream persistence can prove which event context was applied.

The event snapshot is an input fact. It is not an instruction to regenerate a terminal reward.

## Offline and future server-authoritative boundary

For offline play, composition may bind `IAuthoritativeEventClockV1` to a trusted application clock. Local wall-clock manipulation is therefore a trust limitation of the offline mode and should be documented to players if event rewards matter competitively.

For future multiplayer or account-backed events:

1. the server supplies a signed or authenticated authoritative instant and event catalog/version;
2. the client projects only from that supplied context, or consumes a server-produced snapshot;
3. reward/opening commands record the exact event snapshot fingerprint;
4. the server validates that fingerprint before accepting result application.

No network transport, remote event service, signing scheme, monetization backend, or calendar UI is implemented in V1.

## Focused verification

```text
Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testFilter ShooterMover.Tests.EditMode.Modifiers.Events.ActiveEventModifierProjectionV1Tests -testResults Temp/event-modifier-001-editmode.xml -logFile Temp/event-modifier-001-editmode.log
```
