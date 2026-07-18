# LEVELRUN-001 production run foundation

## Status

This draft introduces the engine-independent run foundation and production routing seams for LEVELRUN-001B. It is intentionally **not** presented as the completed production flow yet.

PR #210 was already merged before this branch was created. The branch starts from main at merge commit `923a2bcdb3b8c9a2c80d8154a9d65291d71a514c`, which contains PR #210 head `2fcfabf8cdfbf6f3c0caad84e65a7b266e4b0bb5`.

## Included in this draft

- Exact equipment-instance loadout resolution through `IPlayerHoldingsAuthorityV1`.
- Runtime weapon reference resolution through the existing immutable equipment catalog.
- A run-scoped coordinator with a unique-run identity factory and explicit restart boundary.
- Fail-closed Stage 1 composition for invalid payloads, unsupported levels, unsupported play modes, and missing authorities.
- Generic room enemy registration with idempotent room completion.
- SourceId-based per-player kill attribution.
- Stable ordered contribution summaries.
- Existing XP reward service integration, with no second XP authority.
- Exactly-once extraction through `MissionRunResultAuthorityV1`.
- Frozen Results route handoff that does not recalculate, open, consume, or grant rewards.
- Production Character Select, Hub, Play Selection, Level Selection, Results, and Results-to-Hub route seams.
- A four-slot production weapon projection retaining exact equipment-instance identities.
- Active-slot changes delegated to `LevelRunCoordinatorV1`; the Unity projection owns no second loadout state.
- A production keyboard bridge for weapon slots 1-4, including numpad equivalents.
- Focused EditMode tests for run, routing, composition, session, and slot-selection contracts.

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

`Stage1ProductionWeaponSlotSelectionV1` is a read/command projection over the coordinator. When coordinator-backed, it reads the coordinator's active index and submits slot changes through `TrySelectActiveSlot`. It never edits route payloads, holdings, or equipment instances.

Those durable concerns remain behind their existing authorities.

## Deliberately remaining before LEVELRUN-001B can be accepted

- Replace the production scene's `TestSupport` controller ownership through extraction, rather than duplicating its runtime packages.
- Consume and validate `LevelSelectionRouteContextV1` in the production Stage 1 scene installer.
- Introduce the production cross-scene authority provider and concrete starter-equipment seeding.
- Resolve each selected runtime weapon reference into its accepted execution adapter and remove the demo chooser from valid routed runs.
- Connect moving-droid and turret destruction notifications to the production run binding.
- Connect physical strongbox collection facts and the physical extraction trigger.
- Wire the production run adapter and weapon-input bridge into the retained Stage 1 scene.
- Run the required EditMode and PlayMode suites and retain zero-failure XML.
- Perform the complete Bootstrap -> Results -> same Hub manual acceptance route.

No Unity XML or manual-runtime proof is claimed by this draft.
