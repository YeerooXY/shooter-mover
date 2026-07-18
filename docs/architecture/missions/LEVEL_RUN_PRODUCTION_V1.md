# LEVELRUN-001 production run foundation

## Status

This draft introduces the engine-independent run foundation for LEVELRUN-001. It is intentionally **not** presented as the completed production flow yet.

PR #210 was already merged before this branch was created. The branch starts from main at merge commit `923a2bcdb3b8c9a2c80d8154a9d65291d71a514c`, which contains PR #210 head `2fcfabf8cdfbf6f3c0caad84e65a7b266e4b0bb5`.

## Included in this draft

- Exact equipment-instance loadout resolution through `IPlayerHoldingsAuthorityV1`.
- Runtime weapon reference resolution through the existing immutable equipment catalog.
- A run-scoped coordinator with a unique-run identity factory and explicit restart boundary.
- Generic room enemy registration with idempotent room completion.
- SourceId-based per-player kill attribution.
- Stable ordered contribution summaries.
- Existing XP reward service integration, with no second XP authority.
- Exactly-once extraction through `MissionRunResultAuthorityV1`.
- Frozen Results route handoff that does not recalculate, open, consume, or grant rewards.
- Focused EditMode tests for the above contracts.

## Authority boundaries

`LevelRunCoordinatorV1` owns only run-scoped coordination:

- active weapon slot;
- room enemy registration and accepted destruction facts;
- per-player contribution totals;
- extraction request state.

It does not own:

- equipment or inventory;
- cumulative XP or levels;
- strongbox generation/opening;
- durable mission results;
- scene loading;
- Unity presentation.

Those remain behind their existing authorities.

## Deliberately remaining before LEVELRUN-001 can be accepted

- Replace the production scene's `TestSupport` controller ownership through extraction, not duplication.
- Wire `LevelSelectionRouteContextV1` validation into Stage 1 startup.
- Introduce the production cross-scene authority provider and concrete starter-equipment seeding.
- Connect real weapon execution adapters and 1-4 slot switching in the gameplay HUD.
- Connect moving-droid and turret destruction notifications to the coordinator.
- Add the physical extraction adapter.
- Create and wire `Scenes/Flow/Results/Results.unity`.
- Reuse `ResultsBackground.png.bytes` through a shared artwork decoder.
- Return the exact route payload to Hub and clear handoff state after capture.
- Update production build settings.
- Add and run the required PlayMode route/results tests and manual acceptance flow.

No Unity XML proof is claimed by this draft.
