# LEVELRUN-001 production run foundation

## Status

This draft introduces the engine-independent run foundation and production routing seams for LEVELRUN-001B. It is intentionally **not** presented as the completed production flow yet.

PR #210 was already merged before this branch was created. The branch starts from main at merge commit `923a2bcdb3b8c9a2c80d8154a9d65291d71a514c`, which contains PR #210 head `2fcfabf8cdfbf6f3c0caad84e65a7b266e4b0bb5`.

## Included in this draft

- Exact equipment-instance loadout resolution through `IPlayerHoldingsAuthorityV1`.
- Concrete four-slot starter equipment seeded through `StarterRouteProfileFactoryV1` and `PlayerHoldingsService`.
- A Bootstrap-owned production equipment catalog, holdings service, XP authority, enemy reward service, and mission-result authority.
- A persistent Bootstrap composition owner that survives scene changes without retaining the Bootstrap camera.
- A bounded `ProductionSessionAuthorityContextV1` that projects the immutable route payload and prepares the exact Stage 1 authority bundle.
- Hub startup uses the Bootstrap-seeded route payload instead of placeholder equipment-instance identities.
- Character and Hub route changes update the bounded production route projection without replacing holdings.
- Level Selection prepares the same Bootstrap-owned authority objects immediately before loading Stage 1.
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
- A production presentation host that fail-closes the retained Stage 1 presenter.
- A production-owned `Stage1PlayerPresentationV1` boundary that captures and validates the exact retained player, movement lifecycle, collision, target, input, renderer, and boost-trail projection without creating replacements.
- Focused EditMode tests for run, routing, composition, session, authority handoff, slot selection, presentation lifecycle, and player-projection rejection contracts.

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

`ProductionSessionAuthorityOwnerV1` owns the application-lifetime instances of the existing production authorities. `ProductionSessionAuthorityContextV1` exposes only the immutable route payload and a bounded Stage 1 dependency handoff; it is not a service locator and cannot mutate holdings, XP, results, or equipment.

`Stage1ProductionWeaponSlotSelectionV1` is a read/command projection over the coordinator. When coordinator-backed, it reads the coordinator's active index and submits slot changes through `TrySelectActiveSlot`. It never edits route payloads, holdings, or equipment instances.

`Stage1ProductionPresentationHostV1` owns only the retained presentation lifecycle. Rejected production startup disables the exact retained presenter; valid startup enables it only after route, authority, composition, and scene-adapter validation.

`Stage1PlayerPresentationV1` is the first internal extraction seam. It captures the exact `PlayerMover` object after the retained presenter creates it and rejects missing or incomplete projections. It exposes restart-ready player/movement handles but does not yet replace the retained controller's construction, boost refresh, or restart calls.

The current mission-result port rejects physical strongbox collection because the physical pickup authority is not connected yet. It may project an exact empty collection at extraction; it does not grant, store, open, or simulate boxes.

## Deliberately remaining before LEVELRUN-001B can be accepted

- Delegate retained player construction, boost refresh, and restart to `Stage1PlayerPresentationV1`, then remove the corresponding private fields and methods from the historical controller.
- Extract rooms/environment, enemies/combat, HUD/camera, and weapon presentation into production-owned components and retire the historical CLR namespace.
- Resolve each selected runtime weapon reference into its accepted execution adapter and remove the demo chooser from valid routed runs.
- Connect moving-droid and turret destruction notifications to the production run binding.
- Replace the temporary empty strongbox projection port with the real physical pickup/opening authority bridge.
- Connect physical extraction to frozen Results routing.
- Run the required EditMode and PlayMode suites and retain zero-failure XML.
- Perform the complete Bootstrap -> Results -> same Hub manual acceptance route.

No Unity XML or manual-runtime proof is claimed by this draft.
