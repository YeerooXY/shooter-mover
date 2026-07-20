# LEGACY-AUDIT-002 — Post-cutover legacy gameplay cleanup audit

## Audit boundary

- **Repository:** `YeerooXY/shooter-mover`
- **Base branch:** `main`
- **Exact current main SHA:** `8b4d4e401827735d10082f6f3b04a5d1b2103920`
- **Audit branch:** `agent/legacy-audit-002`
- **Output:** `docs/architecture/audits/LEGACY_CLEANUP_002.md`
- **Prior audit:** draft PR #228 / `LEGACY_CLEANUP_001.md`, based on `1e7f835657afcf479f132db32d0032a2f0d899c3`
- **Production behavior changed:** none
- **Tests added or run:** none
- **Unity Library copied or created:** no
- **Scenes, prefabs, ScriptableObjects, sprites, binaries, project settings, generated files, and logs changed:** none
- **PR #225:** remains unmerged and is not used as replacement evidence in this audit.

This is a static repository audit after the playable cutover and subsequent production-flow, loadout, combat, room, UI, and profile fixes. A legacy-looking name is not removal evidence. Each finding is classified exactly once as `LIVE`, `TRANSITIONAL`, `DUPLICATE`, `DEAD`, or `UNKNOWN`.

## Executive conclusion

The repository is materially healthier than it was at LEGACY-AUDIT-001.

The current production route now uses:

- `PlayerActorAuthority` through the Level 1 player runtime;
- inventory-backed mounted weapon execution and exact equipment-instance identity;
- `RoomRuntimeComposition2D` and the accepted live room graph;
- accepted moving-droid and turret authorities;
- mission-result and production-flow authorities;
- one canonical dedicated-scene UI flow.

The main architectural problem is no longer that the playable route directly trusts the old gameplay loop. `Stage1PlayableLoopCompositionV1` disables the retained controller after its `Awake` bootstrap and takes over production input, weapon execution, room traversal, mission completion, and Results routing.

The remaining debt is concentrated in four areas:

1. **The retained controller is still a 2,000-plus-line object factory and compatibility surface.** Most of its gameplay loop is disabled in production, but old direct-scene tests still execute it.
2. **Two competing player damage-routing cutovers coexist.** The live compatibility component inherits the `EnemyToPlayerDamageRouterV1` implementation, while a second full player adapter, a second projectile router, a test, and documentation assert a different canonical path.
3. **Cutover bootstrapping creates temporary authority state before adopting the real Hub loadout.** A presentation component performs the authority handoff on a later `Update`.
4. **Production adapters still replace canonical presentation owners.** Stage 1 disables the canonical Results controller, has three overlapping scene-install hooks, and contains two projectile-visual preparation pipelines.

A rewrite is not justified. The correct next phase is deletion-oriented consolidation: choose one live player routing path, remove redundant installation and alias layers, compose directly from Hub-owned state, route through canonical Results, replace legacy direct-scene tests, and then shrink the retained controller to a focused scene factory before replacing it entirely.

## Delta from LEGACY-AUDIT-001

### Resolved

- The complete obsolete MENU-001 cluster identified as safe-now in PR #228 was removed by merged PR #236.
- The old embedded Main Menu, Level Selection, Skills, Inventory, Shop, Crafting, Settings, and Results owner no longer exists.
- The production gameplay path now invokes inventory-backed weapon execution instead of the retained controller weapon switch.
- The production gameplay path now uses ROOM-LIVE state and terminal facts instead of the retained controller room authority.
- The current scene serializes `shootingSandbox: 0`; the sandbox branch is no longer the production mode.
- Player health, death, healing, generation, and restart truth remain authority-backed.

### Partially resolved

- The old controller weapon, room, and compact-HUD loops remain in source but are disabled on the production route.
- The old fixed-loadout selector and weapon strip are still constructed by the retained controller and then deactivated after Hub loadout adoption.
- The accepted room runtime is live, but old physical door objects and exact coordinate thresholds are still used as its scene projection.
- Canonical production Results exists, but Stage 1 disables it and installs a second Results controller.

### New post-cutover cleanup findings

- Two competing Level 1 player adapters and two enemy-projectile routing systems coexist after overlapping cutover PRs.
- Three `AfterSceneLoad` installation paths independently add the same Stage 1 components.
- A temporary holdings/equipment/weapon graph is created and then replaced by the real Hub graph.
- Projectile presentation is prepared twice for the same emitted effects.
- A current test and architecture document describe a player-adapter inheritance relationship that is false on current `main`.

## Reference-search summary

### Current route and serialization

- `LevelSelectionCatalogDefinitionV1.Stage1ScenePath` routes Level 1 to `Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity`.
- `EditorBuildSettings.asset` includes that Stage 1 scene as an enabled build scene.
- The scene serializes one enabled `Stage1VisibleSliceController` with GUID `7a007000000000000000000000000002`.
- The scene does **not** serialize `Stage1PlayerLiveAuthorityAdapterV1`; its GUID is `b069b9b08034cd1c5fede8b9fb2283fd`, and the controller adds that type dynamically during `Awake`.
- The scene still serializes the room presentation, two room content definitions, turret presenter, projectile sprites, player sprite, and destruction-animation assets.
- The scene now serializes `shootingSandbox: 0`.

### Current production cutover

- `Stage1PlayableLoopCompositionV1.Start` waits for the retained controller bootstrap, then resolves the production flow profile.
- `RetireLegacyGameplayLoop` reflects the private `roomMissionLayout` field to `null` and disables the controller before its first normal production `Update`.
- Production input then calls `InventoryWeaponRuntimeComposition.TryFireAtTarget`.
- Production room state is created through `RoomRuntimeComposition2D` and the accepted live room graph.
- Production traversal, mission end, XP projection, weapon effects, and Stage 1 HUD are driven from the cutover composition.

### Player damage routing conflict

- The current `Stage1PlayerLiveAuthorityAdapterV1` directly inherits `ShooterMover.Production.Level1.Level1PlayerRuntimeAdapterV1`.
- That live implementation owns `EnemyToPlayerDamageRouterV1` and `EnemyProjectileDamageSourceBinderV1`.
- A second file, `Assets/ShooterMover/Production/Level1/Level1PlayerRuntimeSceneAdapterV1.cs`, implements the same player-runtime responsibilities using `EnemyProjectileImpactRouter2D`.
- `EnemyProjectileDamageRoutingPlayModeTests` expects the compatibility shell to inherit the second implementation, which is not true on current `main`.
- `LEVEL1_ENEMY_DAMAGE_CUTOVER_V1.md` also states that the second implementation is canonical and the shell derives from it; current source contradicts that statement.

### Test-route mismatch

- `Stage1VisibleSliceIntegrationTests` loads `Stage1VisibleSlice.unity` directly without creating a production flow profile.
- The cutover composition rejects that missing production profile before disabling the old controller.
- Those tests therefore continue to execute the legacy controller gameplay loop, including old weapon fire helpers, old room traversal, old door state, old HUD state, and broad restart projection.
- These tests are useful historical package tests, but they are no longer proof of the routed production gameplay architecture and currently prevent deletion of disabled production code.

### Search limitations

GitHub code-search indexing returned no results even for known current symbols, so negative indexed searches were not treated as proof. The audit used current source reads, current scene YAML, script GUIDs, build settings, merged PR inventories/patches, direct callers, and current tests. No local Unity Editor dependency database, exhaustive prefab/asset GUID index, or binary import scan was available. Unproven orphan candidates remain `UNKNOWN`.

## Summary counts

| Classification | Count |
|---|---:|
| LIVE | 10 |
| TRANSITIONAL | 11 |
| DUPLICATE | 10 |
| DEAD | 2 |
| UNKNOWN | 4 |
| **Total** | **37** |

## LIVE findings — keep

| ID | Exact path/type | Why it remains live |
|---|---|---|
| L-01 | `Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity` and `Stage1VisibleSliceController.Awake/BuildSession` | The current Level 1 route still uses this serialized scene and bootstrap to construct player presentation, movement, enemies, room roots, hazards, camera, walls, props, and door visuals. |
| L-02 | `Assets/ShooterMover/Production/Stage1/Level1PlayerRuntimeAdapterV1.cs`, `EnemyProjectileDamageSourceBinderV1.cs`, and `Runtime/UnityAdapters/Players/EnemyToPlayerDamageRouterV1.cs` | This is the player adapter/routing stack actually selected by the current compatibility component. |
| L-03 | `PlayerActorAuthority`, `PlayerRuntimeComposition`, immutable player snapshots/results, and existing healing/damage contracts | Canonical player state authority. |
| L-04 | `ProductionHubLoadoutCompositionV1`, holdings/loadout authorities, exact route-profile equipment identities, and mount policy | Canonical inventory/loadout source used after Hub adoption. |
| L-05 | `WeaponExecutionCore`, `InventoryBackedWeaponExecutionAdapter`, mounted runtime composition, target-locking execution, and effect emitter | Canonical live weapon authority and execution path. Current Stage 1 effect realization must remain until replaced by a reusable effect sink. |
| L-06 | `MobileBlasterDroidRuntime2D`, `BlasterTurretPackage`, and their accepted enemy authorities | Current physical enemy actors and accepted combat state. |
| L-07 | `RoomRuntimeComposition2D`, ROOM-LIVE authorities/contracts, and `Level1LiveRoomGraphDefinitionV1` | Canonical live room state and authored graph. |
| L-08 | `Assets/ShooterMover/Production/Stage1/Stage1PlayableLoopCompositionV1*.cs` | Current production composition root. Individual duplicate/transitional responsibilities should be removed incrementally; do not delete the root wholesale yet. |
| L-09 | `ProductionFlowCoordinatorV1`, `ProductionSceneTransitionCoordinatorV1`, canonical screen controllers, and current flow scenes | Canonical production routing and dedicated-screen ownership. |
| L-10 | `MissionRunResultAuthorityV1`, XP/reward application services, strongbox authorities, and canonical mission/result facts | Current durable run/result boundaries. Stage 1 should integrate with them more directly, not replace them. |

## Safe to remove or consolidate now

### 1. `CLEANUP-PLAYER-002` — choose one Level 1 player routing stack

Current production selects `Level1PlayerRuntimeAdapterV1` plus `EnemyToPlayerDamageRouterV1`. If that choice is intentional, remove the competing cluster atomically:

- `Assets/ShooterMover/Production/Level1/Level1PlayerRuntimeSceneAdapterV1.cs` and metadata;
- `Assets/ShooterMover/ContentPackages/Weapons/Shared/Runtime/EnemyProjectileImpactRouter2D.cs` and metadata, after confirming no non-duplicate consumer;
- `Assets/ShooterMover/Tests/PlayMode/VisibleSliceIntegration/EnemyProjectileDamageRoutingPlayModeTests.cs` and metadata;
- update or retire `docs/architecture/gameplay/LEVEL1_ENEMY_DAMAGE_CUTOVER_V1.md`.

If the later `EnemyProjectileImpactRouter2D` design is preferred instead, migrate the compatibility shell and production callers first, then remove the older stack. Do not keep both.

### 2. `CLEANUP-COMPAT-001` — remove zero-logic aliases

- Change `Stage1VisibleSliceController` to add/store/expose `Level1PlayerRuntimeAdapterV1` directly, then delete `Stage1PlayerLiveAuthorityAdapterV1.cs` and metadata. No current scene GUID points to this shell.
- Replace current `Level1AuthorableRoomDefinitionV1` callers with `Level1LiveRoomGraphDefinitionV1`, then delete the compatibility facade and metadata.

### 3. `CLEANUP-BOOTSTRAP-001` — retain one scene installer

Keep one explicit installer, preferably `Stage1HubLoadoutInstallerV1`, because it documents ordering. Remove the duplicate static scene hooks from:

- `Stage1PlayableLoopCompositionV1`;
- `Stage1WeaponPresentationRepairV1`.

The installer should add both components once and remain the sole owner of installation order.

### 4. `CLEANUP-DOC-001` — remove stale proof claims

`EnemyProjectileDamageRoutingPlayModeTests` and `LEVEL1_ENEMY_DAMAGE_CUTOVER_V1.md` currently assert a base type and canonical path that current source does not use. Repair them together with the player-router decision rather than leaving contradictory proof in the repository.

## Remove after the next cutover step

The following are now genuine removal targets, but they should be deleted only after their stated dependency is complete:

- the temporary local holdings/equipment/weapon catalog built before Hub adoption;
- the `Update`-polled Hub adoption performed by `Stage1WeaponPresentationRepairV1`;
- the custom Stage 1 Results controller and private-field reflection into the canonical Results controller;
- one of the two projectile presentation pipelines;
- old fixed-loadout fixtures, selector, status strip, and their closed tests;
- the retained controller’s disabled weapon switch, cooldown, projectile and direct-hit methods;
- the retained controller’s disabled room graph, room-clear, door and coordinate-traversal methods;
- the retained controller’s disabled compact HUD loop;
- broad compatibility properties and restart projection after focused player/room/camera/HUD participants replace them;
- `TestSupport`, `VisibleSlice`, `VS007`, `DEMO-CUTOVER`, `Prototypes`, and demo/checkpoint naming only when the routed scene and serialized root are deliberately migrated.

## Do not touch yet

- Do not delete `Stage1VisibleSliceController` as a whole; production still consumes objects constructed in its `Awake` path.
- Do not delete or move `Stage1VisibleSlice.unity` until another routed scene serializes or composes all required presentation dependencies.
- Do not remove current enemy prefabs, room assets, player art, projectile art, destruction assets, camera, wall, prop, or hazard presentation merely because their current owner has a legacy name.
- Do not remove `Stage1WeaponPresentationRepairV1` as a whole while it owns current Hub adoption, arc realization, ricochet behavior, and repaired projectile presentation.
- Do not remove `Stage1RoomEnemyAuthorityProjection2D` until ROOM-LIVE binds directly to the real authored enemy instances rather than marker proxies.
- Do not remove canonical authorities listed in L-03 through L-10.
- Do not use unmerged PR #225 as the enemy replacement.
- Do not delete any `UNKNOWN` item without a complete source plus serialized-reference scan.

## Detailed cleanup register

Every row below is one finding and uses exactly one required classification.

| ID | Exact file path and symbol/asset | Class | Direct references found | Scene/prefab/asmdef references | Current replacement authority | Safe/unsafe and dependencies first | Proposed cleanup PR | Risk |
|---|---|---|---|---|---|---|---|---|
| T-01 | `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1PlayerLiveAuthorityAdapterV1.cs` — zero-logic compatibility subclass | TRANSITIONAL | Added/stored by `Stage1VisibleSliceController`; tests find it by type name | Its GUID is not present on the current Stage 1 scene component list | `Assets/ShooterMover/Production/Stage1/Level1PlayerRuntimeAdapterV1.cs` | Low-risk atomic caller/type migration; update reflection-based tests before deletion | `CLEANUP-COMPAT-001` | low |
| T-02 | `Assets/ShooterMover/Content/Definitions/Missions/Rooms/Level1AuthorableRoomDefinitionV1.cs` | TRANSITIONAL | Current Stage 1 composition partials call its forwarded IDs and `Create()` | No scene component; static source compatibility API | `Level1LiveRoomGraphDefinitionV1` | Mechanical caller migration, then delete facade and metadata | `CLEANUP-COMPAT-001` | low |
| T-03 | `Stage1VisibleSliceController.cs`, `Stage1VisibleSlice.unity`, route constants — `TestSupport`, `VisibleSlice`, `Prototypes`, `VS007`, demo naming | TRANSITIONAL | Current build route and production components still refer to exact names/path | Controller GUID and scene path are current serialized/build data | future focused Level 1 scene root | Unsafe as cosmetic cleanup. Rename/move only with scene, route, GUID, build-setting, and test migration | `LEVEL1-SCENE-CUTOVER-001` | high |
| T-04 | `Stage1VisibleSliceController.cs` — `Awake`, `BuildSession`, construction fields and broad public property bag | TRANSITIONAL | Current production composition and player adapter consume many constructed objects/properties | Controller is the only serialized Stage 1 script | focused player/enemy/room/presentation installers | Keep bootstrap behavior now; extract one construction seam at a time before shrinking/deleting controller | `LEVEL1-SCENE-CUTOVER-001` | high |
| T-05 | `Stage1PlayableLoopCompositionV1.cs` — `RetireLegacyGameplayLoop` private-field reflection and `controller.enabled = false` | TRANSITIONAL | Called during every successful production compose | No serialized binding; runtime-attached composition | typed bootstrap contract or replacement scene root | Replace reflection with a typed scene-factory boundary, then remove disabled legacy lifecycle methods | `CLEANUP-STAGE1-LOOP-001` | high |
| T-06 | `Stage1PlayableLoopCompositionV1.cs` — initial bootstrap followed by later `TryAdoptHubLoadout` | TRANSITIONAL | `Compose` builds temporary state; `Stage1WeaponPresentationRepairV1.Update` later adopts Hub state | Runtime-created components | `ProductionHubLoadoutCompositionV1` | Compose directly from Hub state in one transaction; fail closed if unavailable | `CLEANUP-WPN-BOOTSTRAP-001` | high |
| T-07 | `Stage1WeaponPresentationRepairV1.cs` — `Update` polling and `composition.TryAdoptHubLoadout()` | TRANSITIONAL | Runs until installation succeeds, then becomes effect presentation/behavior owner | Runtime-added by installers | explicit typed composition during Stage 1 startup | Move authority adoption out of presentation; retain only effect realization responsibilities | `CLEANUP-WPN-BOOTSTRAP-001` | medium |
| T-08 | `Stage1VisibleSliceController.BuildUi` — creates `VisibleSliceLoadoutSelector`, `VisibleSliceGeneralCombatHud`, and `Stage1WeaponStatusStrip` | TRANSITIONAL | Controller bootstrap creates them; Hub integration deactivates selector/strip; general HUD component is disabled | Runtime-created, not scene-serialized | Hub loadout UI and one production gameplay HUD | Stop constructing unused owners before deleting packages; replace direct-scene tests first | `CLEANUP-LEVEL1-UI-001` | medium |
| T-09 | `Stage1PlayableLoopCompositionV1.Flow.cs` — `DoorLaneHalfWidth`, `DoorTraversalX`, `HandleRoomTraversal`, `MirrorDoor` | TRANSITIONAL | Current production loop uses position thresholds and mirrors ROOM-LIVE state into old doors | Old doors are runtime-created by controller | ROOM-LIVE exits plus a typed physical door/traversal adapter | Keep until physical trigger/door presentation is bound directly to ROOM-LIVE | `ROOM-PRESENTATION-CUTOVER-001` | high |
| T-10 | `Assets/ShooterMover/Production/Stage1/Stage1RoomEnemyAuthorityProjection2D.cs` | TRANSITIONAL | ROOM-LIVE marker objects resolve and proxy the real droid/turret authorities through static composition lookup | Runtime marker prefab catalog; no authored scene component | direct generic terminal binding to real authored enemy instances | Remove after room presentation spawns/binds real authorities with exact placed identities | `ROOM-ENEMY-BINDING-001` | medium |
| T-11 | `Stage1VisibleSliceController.cs` — `PlayerHealth`, `RestartGeneration` fallback, `ApplyAcceptedPlayerRestart`, `TryRead`, counters and broad projection methods | TRANSITIONAL | Current player adapter, Stage 1 composition, old tests, camera/HUD/presentation consume them | Controller serialized | immutable player/room/enemy projections and explicit restart participants | Migrate callers by responsibility; do not replace with another broad facade | `CLEANUP-PLAYER-PROJECTION-001` | high |
| D-01 | `Assets/ShooterMover/Production/Level1/Level1PlayerRuntimeSceneAdapterV1.cs` | DUPLICATE | Used by the conflicting `EnemyProjectileDamageRoutingPlayModeTests`; current compatibility shell derives from another implementation | No current Stage 1 scene component; no current installer adds this type | live `Level1PlayerRuntimeAdapterV1` path, unless an explicit decision reverses the choice | Choose one implementation. If current route is authoritative, remove this file with its router/test/doc cluster | `CLEANUP-PLAYER-002` | medium |
| D-02 | `Assets/ShooterMover/ContentPackages/Weapons/Shared/Runtime/EnemyProjectileImpactRouter2D.cs` and its binding/impact types | DUPLICATE | Consumed by D-01 and conflicting test/doc cluster; current live adapter owns `EnemyToPlayerDamageRouterV1` | No serialized component; engine-independent source | `EnemyToPlayerDamageRouterV1` plus current binder, unless architecture decision selects this router instead | Do not preserve both ledgers/routes. Compile/reference scan before deleting package-neutral types | `CLEANUP-PLAYER-002` | medium |
| D-03 | Scene hooks in `Stage1PlayableLoopCompositionV1`, `Stage1HubLoadoutInstallerV1`, and `Stage1WeaponPresentationRepairV1` | DUPLICATE | All subscribe to `SceneManager.sceneLoaded`; all find Stage 1 and add/check the same components | Runtime initialization only | one ordered Stage 1 installer | Keep one installer and remove duplicate subscriptions/install methods | `CLEANUP-BOOTSTRAP-001` | low |
| D-04 | `Assets/ShooterMover/Production/Stage1/Stage1ReadOnlyResultsControllerV1.cs` and `Stage1ReadOnlyResultsProjectionV1` | DUPLICATE | Stage 1 scene hook disables `ProductionResultsControllerV1`, reads its private background by reflection, and creates this controller | Results scene serializes the canonical production controller | `ProductionResultsControllerV1`, `ProductionResultsContextV1`, mission/BOX authorities | Route authoritative result/context through canonical Results before deleting | `CLEANUP-RESULTS-001` | high |
| D-05 | `Stage1PlayableLoopCompositionV1.cs` / `.Catalogs.cs` — `BuildInventoryAndWeaponAuthority`, hardcoded equipment/weapon catalog, temporary `PlayerHoldingsService`, first `BeginRun` | DUPLICATE | Built during `Compose`; later replaced by `TryAdoptHubLoadout`, which swaps holdings/catalogs/runtime and begins the run again | Runtime-created only | Hub-owned holdings, equipment catalog, weapon catalog and mount set | Remove after synchronous Hub adoption is part of `Compose`; no fallback authority graph | `CLEANUP-WPN-BOOTSTRAP-001` | high |
| D-06 | `Stage1VisibleSliceController.cs` — disabled `ReadCombatInput`, `FireSelectedLoadout`, `FireWeapon`, `FireProjectile`, `FireBlaster`, cooldown/tuning/direct-hit logic | DUPLICATE | Production controller is disabled before Update; old direct-scene tests still call test fire helpers | Source remains inside serialized controller | mounted inventory execution and effect pipeline | Replace legacy direct-scene tests with production-route tests, then delete the complete old fire path | `CLEANUP-STAGE1-LOOP-001` | high |
| D-07 | `Stage1VisibleSliceController.cs` — disabled `roomMissionLayout`, `DemoRoomProjection`, `RefreshArenaFlow`, `SwitchRoom`, `BuildExitDoor`, hardcoded traversal | DUPLICATE | Production loop nulls legacy layout and disables controller; old direct-scene tests still execute it | Old doors/room roots are runtime-created by controller | ROOM-LIVE and current production traversal adapter | First migrate tests and physical room presentation, then delete old room state loop | `CLEANUP-STAGE1-LOOP-001` | high |
| D-08 | `Stage1VisibleSliceController.OnGUI`, compact styles/objectives and legacy `RefreshHud` versus `Stage1PlayableLoopCompositionV1.Flow.OnGUI` | DUPLICATE | Old controller HUD runs only when direct-scene tests bypass production flow; production loop draws separate HUD | Controller is serialized; HUD objects runtime-created | one projection-driven production gameplay HUD | Replace direct-scene tests and select one HUD owner before deletion | `CLEANUP-LEVEL1-UI-001` | medium |
| D-09 | `Stage1WeaponLoadoutFixtures.cs`, `UI/VisibleSliceLoadoutSelector/**`, `Stage1WeaponStatusStrip.cs` and related tests | DUPLICATE | Old controller/tests use fixture identities; current production adopts exact Hub equipment instances and deactivates selector/strip | Runtime-created by old controller | production Hub Inventory/Loadout and mounted runtime | Remove as an atomic old-loadout cluster after direct-scene tests stop requiring it | `CLEANUP-LEVEL1-UI-001` | medium |
| D-10 | `Stage1PlayableLoopCompositionV1.Combat.cs::PrepareEmittedEffects/AddProjectilePresentation` and `Stage1WeaponPresentationRepairV1::LateUpdate/PrepareProjectile/WeaponVisualProfile` | DUPLICATE | Both track the same emitted effects, create sprites/visuals, and maintain separate caches; repair disables renderers created by the first path and adds replacements | Runtime-created only | one reusable Unity weapon effect presentation pipeline | Consolidate visuals and collision/behavior preparation in one typed effect sink | `CLEANUP-WPN-PRESENTATION-001` | medium |
| X-01 | `Assets/ShooterMover/Tests/PlayMode/VisibleSliceIntegration/EnemyProjectileDamageRoutingPlayModeTests.cs` | DEAD | Asserts compatibility shell base type equals `Level1PlayerRuntimeSceneAdapterV1`; current source derives from `Level1PlayerRuntimeAdapterV1` | Test only | focused coverage for the chosen player routing stack | Remove or rewrite atomically with D-01/D-02; current assertion is stale | `CLEANUP-PLAYER-002` | low |
| X-02 | `docs/architecture/gameplay/LEVEL1_ENEMY_DAMAGE_CUTOVER_V1.md` — claims the D-01 path is canonical and serialized compatibility is retained for scene GUID | DEAD | Contradicted by current inheritance and current scene YAML; shell is dynamically added, not serialized | Documentation only | documentation for the chosen live player route | Replace or archive with player-route consolidation | `CLEANUP-DOC-001` | low |

## UNKNOWN findings — no removal recommendation

| ID | Exact path or scope | Why evidence is insufficient | Required proof |
|---|---|---|---|
| U-01 | `Assets/ShooterMover/ContentPackages/Encounters/Stage1ShortRoute/Stage1ShortRouteComposition.cs` and `Stage1ShortRouteSession.cs` | They still look like an alternate Stage 1 route model, but current source, test, authoring, and external consumers were not exhaustively resolved | complete symbol/asmdef/test reference scan and comparison with the routed mission model |
| U-02 | temporary enemy presentation components and older visible-slice turret/droid presentation assets | Some are still instantiated by the retained bootstrap; others may be test-only, but prefab GUID dependencies were not exhaustively indexed | Unity dependency database or complete prefab/scene GUID scan |
| U-03 | isolated private helpers inside `Stage1VisibleSliceController`, including `ApplyPlayerProjectileDamageToTurret`, parameterless `FireBlaster`, and generated-mount helpers | No direct caller was found in inspected source ranges, but negative GitHub search is unreliable | compiler-assisted removal or complete Roslyn/reference scan |
| U-04 | unused asmdefs, orphan prefabs/ScriptableObjects/sprites, and abandoned bootstrap assets outside the inspected paths | No local recursive tree plus Unity import/dependency database was available | full repository inventory, GUID reverse index, asmdef dependency graph, and Unity asset dependency scan |

## Weapon cleanup map

### Current production path

```text
Hub profile and exact equipment-instance bindings
  -> ProductionHubLoadoutCompositionV1
  -> mounted InventoryWeaponRuntimeComposition
  -> InventoryBackedWeaponExecutionAdapter
  -> WeaponExecutionCore
  -> immutable effect batch
  -> InventoryWeaponEffectEmitter2D
  -> Stage 1 collision/damage and presentation adapters
```

### Remaining duplicate path

```text
Stage1VisibleSliceController direct-scene mode
  -> Stage1WeaponLoadoutFixture
  -> local cooldown
  -> weapon-ID switch and silent fallback
  -> local projectile construction/direct enemy commands
```

The duplicate path is no longer used when Level 1 is entered through production flow, but old direct-scene tests still keep it executable.

### Highest-value weapon cleanup

1. Adopt Hub loadout synchronously during composition.
2. Delete the temporary hardcoded catalog/holdings graph.
3. Merge the two emitted-effect presentation pipelines.
4. Move ricochet/arc/gameplay behavior out of a component named `PresentationRepair` into typed reusable effect behavior.
5. Replace old direct-scene tests, then delete fixture loadouts and the retained controller fire path.

## Player cleanup map

| Surface | Current status | Direction |
|---|---|---|
| `PlayerActorAuthority` / runtime composition | LIVE | keep |
| `Level1PlayerRuntimeAdapterV1` + `EnemyToPlayerDamageRouterV1` | LIVE current route | keep unless explicit decision selects the competing router |
| `Level1PlayerRuntimeSceneAdapterV1` + `EnemyProjectileImpactRouter2D` | DUPLICATE | choose or delete; do not preserve two canonical routes |
| `Stage1PlayerLiveAuthorityAdapterV1` | TRANSITIONAL zero-logic alias | replace dynamic type reference and delete |
| controller health/generation/HUD/restart facade | TRANSITIONAL | split into immutable projections and explicit restart participants |
| direct-scene legacy player/combat tests | test migration blocker | replace with Bootstrap/production-profile route coverage |

## Enemy cleanup map

| Surface | Classification | Direction |
|---|---|---|
| real moving-droid and turret authorities | LIVE | keep |
| player damage router duplication | DUPLICATE | choose one generic many-source route |
| `Stage1RoomEnemyAuthorityProjection2D` | TRANSITIONAL | remove after ROOM-LIVE binds to real placed enemy identities directly |
| retained controller direct player-to-enemy hit methods | DUPLICATE | delete after production-route tests replace old helpers |
| package-specific enemy construction in retained bootstrap | TRANSITIONAL | keep until scene authoring/presentation creates the accepted physical actors without the god object |

## Room cleanup map

ROOM-LIVE is now genuinely active. The remaining room debt is projection and scene wiring rather than a second production room authority.

Remove in order:

1. Replace `Level1AuthorableRoomDefinitionV1` alias with the canonical live graph name.
2. Replace marker enemy proxies with direct real enemy identity binding.
3. Bind physical doors/triggers to ROOM-LIVE instead of mirroring state into old controller doors.
4. Replace coordinate thresholds with typed trigger/exit adapters.
5. Delete the disabled controller room graph/clear/traversal methods after legacy tests are retired.
6. Replace the retained controller room factory with authored/presentation installers.

## Flow and Results cleanup map

### Improved

- The old embedded Main Menu cluster is gone.
- Dedicated production scenes and one persistent flow coordinator are active.
- Profile slot selection/deletion work is on the current base and does not reintroduce old screen ownership.

### Remaining duplication

- Stage 1 disables the serialized canonical Results controller and installs `Stage1ReadOnlyResultsControllerV1`.
- Stage 1 reads the canonical Results controller's private background field by reflection.
- Three runtime scene hooks compete to install Stage 1 composition components.

Target:

- one ordered Stage 1 installer;
- one canonical Results controller receiving the immutable mission result and exact strongbox context;
- no private-field reflection or replacement screen owner.

## Test cleanup map

The old direct-scene suite is now the main deletion blocker.

### Keep from those tests

Preserve coverage for:

- serialized asset integrity;
- player/enemy physical presentation;
- movement and camera ownership;
- enemy projectile travel;
- restart object-count stability;
- accessibility projection;
- door/room traversal and retained enemy state.

### Change the harness

Create the production flow/profile/Hub loadout context first, then enter Level 1 through the same route as the game. Tests should assert the canonical production composition, not rely on the cutover rejecting initialization and leaving the legacy controller enabled.

After equivalent production-route coverage exists, remove tests that directly call:

- `FireAtTurretForTests`;
- `FireAtMobileDroidForTests`;
- `ConfirmDefaultLoadout`;
- old `CurrentRoomStableId`/door state as controller-owned truth;
- old compact HUD and fixture-loadout state.

## Proposed cleanup order

1. **`CLEANUP-PLAYER-002`** — select one player projectile routing stack; remove conflicting implementation/test/doc.
2. **`CLEANUP-COMPAT-001`** — delete the zero-logic player shell and room-definition alias.
3. **`CLEANUP-BOOTSTRAP-001`** — consolidate three scene hooks into one installer.
4. **`TEST-CUTOVER-LEVEL1-001`** — replace direct-scene legacy gameplay tests with production-route tests.
5. **`CLEANUP-WPN-BOOTSTRAP-001`** — compose directly from Hub holdings/catalog/mounts; delete temporary authority graph.
6. **`CLEANUP-WPN-PRESENTATION-001`** — unify projectile/effect presentation and move gameplay behavior to typed effect adapters.
7. **`CLEANUP-RESULTS-001`** — route through canonical Results and delete the Stage 1 replacement screen.
8. **`ROOM-PRESENTATION-CUTOVER-001`** — direct enemy/door/exit presentation binding to ROOM-LIVE.
9. **`CLEANUP-STAGE1-LOOP-001`** — delete disabled controller weapon/room/HUD loops.
10. **`LEVEL1-SCENE-CUTOVER-001`** — replace the retained scene god object and retire historical naming/path only after serialized migration proof.

## Files that must remain for the current playable route

At minimum, the following must remain until their listed cutovers complete:

- `Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity`;
- `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs` and metadata;
- current serialized room/presentation/sprite/destruction assets referenced by that scene;
- `Assets/ShooterMover/Production/Stage1/Stage1PlayableLoopCompositionV1*.cs`;
- `Assets/ShooterMover/Production/Stage1/Stage1HubLoadoutInstallerV1.cs`;
- `Assets/ShooterMover/Production/Stage1/Stage1WeaponPresentationRepairV1.cs` and its current effect behavior helpers;
- the selected live Level 1 player adapter/router/binder stack;
- current enemy authorities and prefabs;
- ROOM-LIVE runtime, contracts, live graph and room presentation types;
- production Hub loadout, weapon execution/effect, flow, mission-result, XP and Results authorities;
- current Bootstrap and canonical flow scenes/build settings.

## Changed-file list

This audit PR changes only:

- `docs/architecture/audits/LEGACY_CLEANUP_002.md`

No implementation deletion is performed in this audit.