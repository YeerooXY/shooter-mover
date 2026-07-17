# Mission results authority v1 (RUN-001)

## Scope

RUN-001 owns the immutable end-of-run fact and replay policy. It does not own reward generation, reward application, strongbox opening, wallets, XP, inventory, pickup presentation, or equipment creation.

The authoritative flow is:

`DROP -> physical PICK -> RAP -> INV -> RUN collection fact -> End Run -> immutable Results payload`

A collection fact is admitted only after the existing holdings authority exposes the exact strongbox instance with the expected PICK/RAP provenance. At End Run, the adapter reads current INV and BOX snapshots to classify each previously collected physical instance:

- present in INV and not terminally opened by BOX: `Unopened`;
- absent from INV and terminally opened by BOX for the same run and instance: `Opened`;
- present in both, absent from both, or mismatched provenance: reject without mutation.

## Contracts

`MissionRunCollectStrongboxCommandV1` carries:

- stable operation and run identities;
- the immutable `PlayerRouteProfilePayloadV1`;
- exact strongbox definition and instance identities;
- PICK/RAP grant and source provenance;
- expected RUN and INV sequence/fingerprint values.

`EndMissionRunCommandV1` carries:

- stable operation and run identities;
- the immutable route/profile payload;
- completion state (`Completed`, `Failed`, or `Abandoned`);
- expected RUN, INV, and BOX sequence/fingerprint values;
- a semantic intent fingerprint independent of retry operation identity and mutable authority sequences.

`MissionRunPayloadV1` is schema-versioned and immutable. It retains the stable contract and run identities, the exact route/profile payload, every verified collection fact ordered by concrete strongbox instance identity, the RUN sequence, and a deterministic SHA-256 fingerprint. Every accepted collection returns the new immutable run snapshot; End Run carries the last accepted run snapshot into the terminal fact.

`MissionResultPayloadV1` is independently schema-versioned and immutable. It retains:

- stable contract and run identities;
- the exact route/profile payload;
- completion state;
- every collected physical strongbox instance;
- opened/unopened state and terminal BOX fingerprint where opened;
- exact unopened instance identities;
- source RUN, INV, and BOX sequences/fingerprints;
- a deterministic SHA-256 fingerprint.

Collections and results are canonically ordered by concrete strongbox instance identity. Definition identity is never used to collapse physical instances.

## Idempotency and conflicts

The first accepted End Run advances RUN sequence once and freezes one `MissionResultPayloadV1` alongside the exact immutable `MissionRunPayloadV1` that preceded termination.

- Repeating the same semantic End Run for the same run returns the exact cached result object, even when the retry uses a new operation identity or stale expected sequence values.
- Reusing an operation identity with a different command fingerprint is a conflicting duplicate.
- Re-ending a terminal run with a different route or completion state is a conflicting replay.
- Stale RUN/INV/BOX inputs are rejected before mutation.
- Collection after terminal End Run is rejected.

Rejected calls do not alter RUN sequence, collection state, or the frozen result.

## Results presentation boundary

`MissionResultsSessionV1` is a read-only handoff around the immutable result. Reading its snapshot or counts performs no authority callback. Therefore displaying Results cannot:

- open or consume a strongbox;
- call GEN or reroll contents;
- call RAP or grant rewards;
- mutate INV, SCR, money, XP, or equipment;
- collapse duplicate definitions into one instance.

## Existing-authority adapter

`MissionRunExistingAuthorityPortV1` composes existing read-only APIs:

- `IPlayerHoldingsAuthorityV1.ExportSnapshot()` and sequence for ownership/provenance;
- `StrongboxOpeningSnapshotV1` for terminal BOX opening facts.

It has no mutation method and no dependency on reward generators. PICK and RAP remain authoritative indirectly through the immutable holdings provenance they produced.

## Validation matrix

Focused EditMode coverage includes zero/one/multiple boxes, same-definition physical instance preservation, exact instance identity, repeated End Run, conflicting replay, stale input, route-payload preservation, collection-operation conflict, and duplicate weapon-definition equipment instances remaining separate outside Results.

Focused PlayMode coverage includes zero/one/multiple Results handoffs, repeated reads and repeated End Run, conflicting replay across frames, exact unopened identity, exact route payload, and zero open/consume/grant callbacks.

## Manual checklist

1. Complete a mission with no strongbox pickups and End Run twice; confirm the same empty result fingerprint is shown.
2. Collect one physical strongbox through the normal pickup path; End Run; confirm Results shows its exact instance ID as unopened.
3. Collect multiple boxes of the same tier; confirm every concrete instance remains listed separately.
4. Open one collected box through BOX before End Run; confirm only that instance is marked opened and no reward is generated by Results.
5. Leave one collected box unopened; enter and revisit Results; confirm no BOX, RAP, INV, SCR, money, or XP sequence changes.
6. Replay End Run with a different completion state or route payload; confirm rejection and unchanged frozen result.
7. Open two separate boxes that resolve to the same weapon definition; confirm BOX/INV retain two distinct equipment instance IDs and Results does not recreate either reward.

## Rollback

Revert the RUN-001 commit. Existing DROP, PICK, RAP, INV, BOX, SCR, money, XP, and equipment authorities remain unchanged and independently authoritative.
