# DEV-001 — Debug Reward Run Panel

## Purpose

`DEV-001` is a development/editor-only composition surface for deterministic
strongbox-run testing. It does not own reward generation, pickup collection,
holdings, mission results, or strongbox opening.

The accepted runtime path is:

`DEV request -> DROP operation -> GEN -> physical PICK -> RAP -> INV -> RUN -> Results`

The panel never adds a strongbox directly to holdings and never constructs a
Results payload itself.

## Inputs

- **Strongbox count:** `0..64`.
- **Strongbox tier:** one canonical authored `StableId`; no tier count or enum is
  frozen in this package.
- **Deterministic seed:** unsigned 64-bit input. The seed participates in the
  stable DROP source/profile/grant identities, so identical run, route, tier,
  count, and seed inputs resolve byte-identically.

One mission run accepts one batch. Exact replay returns the existing physical
pickup projections. Reusing the same operation identity with a different
payload is rejected as a conflicting duplicate. Change the seed to author a
different deterministic batch.

## Authority-derived counters

- **Requested** comes from the accepted immutable debug request.
- **Spawned** counts exact physical `RewardPickup2D` projections accepted by
  DROP/GEN/PICK/RAP.
- **Collected** counts physical pickups whose exact holdings provenance was
  verified and recorded by RUN-001.
- **Pending** is `spawned - collected`.

Each spawned fact retains the exact definition, strongbox-instance, reward-grant,
DROP source-operation, pickup, and RUN collection-operation identities.

## End Run

The presentation session caches the first End Run result. Repeated confirm,
retry, or callback input returns that cached result and does not call RUN-001
again. Before ending, the bridge refreshes physical pickup state and records all
new verified collection facts. RUN-001 then freezes the terminal result from
current INV and BOX snapshots. Only that immutable `MissionResultPayloadV1` is
placed in `MissionResultsSessionV1` and sent to the injected Results route sink.

## Build boundary

The IMGUI panel is compiled only when `UNITY_EDITOR` or `DEVELOPMENT_BUILD` is
defined. The runtime bridge also fails closed through `RunDebugBuildGuardV1`
outside those builds.

No production scene is modified by this package. In particular,
`Stage1VisibleSlice.unity` remains untouched; a later integration owner may add
the panel to a dedicated development scene or development-only bootstrap.

## Runtime binding

Configure `RunDebugRewardBridge2D` with:

1. the stable run identity and immutable `PlayerRouteProfilePayloadV1`;
2. the existing `IPlayerHoldingsAuthorityV1`;
3. the existing BOX snapshot exporter;
4. the existing `MissionRunResultAuthorityV1`;
5. the configured `RewardPickupDropFactory2D`;
6. an optional Results route callback accepting `MissionResultsSessionV1`.

Assign the bridge to `RunDebugPanel2D`. The pickup factory must already be wired
to the shared GEN service, progression context, RAP adapter, and gameplay restart
scope.

## Focused validation commands

```bash
"$UNITY" -batchmode -nographics -quit -projectPath . \
  -runTests -testPlatform EditMode \
  -testFilter ShooterMover.Tests.EditMode.Development.RunDebug \
  -testResults artifacts/test-results/DEV-001-EditMode.xml

"$UNITY" -batchmode -nographics -quit -projectPath . \
  -runTests -testPlatform PlayMode \
  -testFilter ShooterMover.Tests.PlayMode.Development.RunDebug \
  -testResults artifacts/test-results/DEV-001-PlayMode.xml
```

A Unity proof claim is valid only when both XML files report a completed run with
zero failures.

## Manual checklist

- [ ] Panel is visible in Editor and a Development Build.
- [ ] Panel is absent/disabled in a non-development player.
- [ ] Count, tier, and seed validation fail closed.
- [ ] Zero-box End Run routes an empty immutable Results payload.
- [ ] Multiple requested boxes appear as separate physical pickups.
- [ ] Walking the claimant into one pickup applies it through RAP/INV.
- [ ] Requested/spawned/collected/pending counts follow authority state.
- [ ] Same input reuses identical pickup and box identities.
- [ ] Duplicate/collision input never awards twice.
- [ ] Repeated End Run calls RUN-001 once.
- [ ] Results contains only exact collected, unopened box instances.
- [ ] `Stage1VisibleSlice.unity` has no diff.
