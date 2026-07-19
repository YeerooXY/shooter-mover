# LEGACY-AUDIT-001 — Removable legacy gameplay code audit

## Audit boundary

- **Repository:** `YeerooXY/shooter-mover`
- **Base branch:** `main`
- **Exact current main SHA:** `1e7f835657afcf479f132db32d0032a2f0d899c3`
- **Audit branch:** `agent/legacy-audit-001`
- **Changed files in this audit:** `docs/architecture/audits/LEGACY_CLEANUP_001.md`
- **Production behavior changed:** none
- **Tests added or run:** none
- **Unity Library copied or created:** no
- **Scenes, prefabs, ScriptableObjects, sprites, binaries, and generated files modified:** none
- **Excluded by instruction:** PR #225 and its duplicate `EnemyCombatRuntime` were not inspected and are not recommendations in this report.

This is a static removal audit. A `DEAD` classification is used only where the inspected current route, serialized scene binding, source dependency closure, and assembly boundary support removal. A filename containing `Stage1`, `Demo`, `Temporary`, `Prototype`, or `VisibleSlice` is not treated as evidence by itself.

## Classification counts

| Classification | Findings |
|---|---:|
| LIVE | 10 |
| TRANSITIONAL | 7 |
| DUPLICATE | 11 |
| DEAD | 4 |
| UNKNOWN | 5 |
| **Total** | **37** |

## Executive conclusion

The current repository has one low-risk, safe-now cleanup unit: the superseded MENU-001 embedded Main Menu implementation and its closed test/assembly dependency cluster. The larger legacy gameplay surface is concentrated inside `Stage1VisibleSliceController`. Much of that code is genuinely duplicated by accepted player, weapon, room, flow, and mission authorities, but it remains on the current playable route and therefore must not be deleted yet.

The correct cleanup strategy is:

1. remove the proven-dead Main Menu cluster;
2. wire the Stage 1 scene to inventory-backed weapon execution and delete the local weapon path in the same follow-up;
3. migrate the scene to `RoomRuntimeComposition2D` and delete the direct room/door/occupant projection path;
4. finish player presentation/restart projection extraction;
5. remove the remaining compact HUD, sandbox hooks, compatibility mirrors, and historical naming only after no current route or focused test requires them.

## Reference-search summary

### Current route and serialized evidence

- `LevelSelectionCatalogDefinitionV1.Stage1ScenePath` routes Level 1 to `Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity`.
- `Stage1VisibleSlice.unity` contains one enabled `Stage1VisibleSliceController` component and serializes its room presentation, two room definitions, turret presenter, projectile sprites, player sprite, and destruction-animation assets.
- `Stage1VisibleSlice.unity` still serializes `shootingSandbox: 1`; sandbox-specific behavior is therefore part of the current playable scene, not dead test code.
- `EditorBuildSettings.asset` contains the canonical Bootstrap/Main Menu/Character Selection/Hub/Play Selection/Level Selection/Inventory/Skills/Shop/Crafting/Results/Strongbox Opening flow. Gameplay is entered through the level catalog’s explicit scene path.
- FLOW-UI-001 replaced the old embedded Main Menu owner with `ProductionMainMenuControllerV1`, installed `ProductionFlowCoordinatorV1` in Bootstrap, and removed the old Bootstrap camera.
- The Main Menu scene patch removed serialized script GUID `6cfba4f8dd3a40f198353ed92c6f631d` (`MainMenuController`) and replaced `MainMenuArtworkController` with `ProductionMainMenuControllerV1`.

### C# and authority evidence

- `Stage1VisibleSliceController` still directly owns or coordinates input polling, old loadout fixtures, local fire cooldown, weapon-ID branching, pellet/spread calculation, projectile construction, direct enemy hit translation, hardcoded enemy construction, room-clear polling, runtime doors, traversal thresholds, compact HUD, camera/HUD refresh, and accepted-restart scene projection.
- Player health/death/restart truth now comes from `PlayerActorAuthority` through `PlayerRuntimeComposition` and `Stage1PlayerLiveAuthorityAdapterV1`. The old mutable integer health authority is not present on current main; `PlayerHealth` is a rounded compatibility read from the authority snapshot.
- WPN-LIVE-001 provides `WeaponExecutionCore`, `InventoryBackedWeaponExecutionAdapter`, `RouteProfileActiveWeaponSource`, `PlayerRuntimeWeaponStateAdapter`, `PlayerInventoryWeaponRuntimeCompositionRoot`, and the transactional Unity effect sink. The retained controller does not call this path and still contains its own second weapon implementation.
- ROOM-LIVE-001 provides `RoomRuntimeComposition2D` and `RoomLiveRuntimeAuthorityV1`, while explicitly documenting that the Stage 1 scene/controller was not migrated. The controller still directly uses `RoomMissionLayoutV1`, `DemoRoomProjection`, hardcoded room IDs, runtime door construction, and exact enemy-package branches.
- `ProductionFlowCoordinatorV1` is the persistent current flow owner and binds the canonical screen controllers. Its assembly does not reference `ShooterMover.UI.MainMenu`.

### Asmdef and test evidence

- `ShooterMover.UI.ProductionFlow.asmdef` references the canonical destination-screen assemblies and does not reference `ShooterMover.UI.MainMenu`.
- `ShooterMover.UI.MainMenu.asmdef` contains only the superseded MENU-001 UI cluster and remains auto-referenced.
- The old Main Menu types are directly covered by `MainMenuFlowStateTests`, `MainMenuControllerTests`, and `MainMenuArtworkControllerTests`; those tests become obsolete together with the closed implementation cluster.
- No test-only reference is used as proof that current playable code is live. Conversely, production-looking code is not called dead merely because only old tests were found.

### Search limitations

The audit used current file reads, merged-PR file inventories/patches, route constants, current scenes, serialized script GUIDs, and asmdef references through the GitHub connector. Repository code-search indexing returned no matches even for known current symbols/GUIDs, so negative code-search results were not used as dead-code proof. No local clone, Unity editor, exhaustive prefab dependency database, or binary asset scan was available. Candidates lacking enough evidence are `UNKNOWN` and have no removal recommendation.

## Safe to remove now

Remove the following as one atomic cleanup unit, including metadata and obsolete tests:

- `Assets/ShooterMover/Runtime/Application/Menu/MainMenuFlowState.cs`
- `Assets/ShooterMover/UI/MainMenu/MainMenuController.cs`
- `Assets/ShooterMover/UI/MainMenu/MainMenuArtworkController.cs`
- `Assets/ShooterMover/UI/MainMenu/MainMenuPlatformActions.cs`
- `Assets/ShooterMover/UI/MainMenu/ShooterMover.UI.MainMenu.asmdef`
- corresponding `.meta` files
- `Assets/ShooterMover/Tests/EditMode/Menu/MainMenuFlowStateTests.cs`
- `Assets/ShooterMover/Tests/PlayMode/Menu/MainMenuControllerTests.cs`
- `Assets/ShooterMover/Tests/PlayMode/Menu/MainMenuArtworkControllerTests.cs`
- test asmdefs/metadata that become empty or unreferenced

Do **not** delete the shared artwork assets in the same PR. FLOW-UI-001 states that supplied screen assets remain active presentation inputs, and current canonical scenes serialize several of them.

## Remove after playable cutover

The following are removal targets, but not safe while Level 1 still loads `Stage1VisibleSlice.unity` with `Stage1VisibleSliceController`:

- the controller’s `Stage1WeaponLoadoutFixture` selection path;
- `FireSelectedLoadout`, `FireWeapon`, `FireProjectile`, and `FireBlaster`;
- local fire cooldown and weapon tuning constants;
- direct droid/turret damage translation in the controller;
- hardcoded moving-droid/turret projector branches and runtime definitions;
- `DemoRoomProjection`, direct room-clear polling, runtime door construction, and coordinate/room-ID traversal branches;
- duplicate compact `OnGUI` combat HUD;
- sandbox-only production hooks and historical demo IDs;
- player compatibility read mirrors and broad restart projection once all callers consume immutable authority snapshots and explicit restart participants.

## Keep

- `PlayerActorAuthority`, `PlayerRuntimeComposition`, and their immutable snapshots/results.
- `WeaponExecutionCore`, `InventoryBackedWeaponExecutionAdapter`, and inventory-backed runtime/effect composition.
- existing accepted enemy authorities, including `MobileBlasterDroidRuntime2D` and `BlasterTurretPackage`.
- `RoomMissionLayoutV1` as the accepted graph traversal/completion state owner used by ROOM-LIVE-001; remove only the controller’s bypass/direct ownership, not this authority.
- `RoomRuntimeComposition2D`, `RoomLiveRuntimeAuthorityV1`, authorable room definitions, generic terminal relays, and room presentation catalog.
- `ProductionFlowCoordinatorV1`, `ProductionSceneTransitionCoordinatorV1`, `HubNavigationServiceV1`, canonical screen controllers, and current build-settings scenes.
- mission/run/results facts and authorities, `ProductionResultsControllerV1`, and `StrongboxOpeningServiceV1`.
- current Stage 1 enemy, room, projectile, player, and destruction presentation assets serialized by `Stage1VisibleSlice.unity` until the replacement scene/composition serializes alternatives.

## Do not touch

- `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs` in this audit or before a replacement playable composition is proven.
- `Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity`, its serialized prefab/ScriptableObject/sprite references, or its metadata in a source-only cleanup PR.
- canonical authority code listed above.
- current flow scenes or Bootstrap ownership.
- PR #225 or its duplicate `EnemyCombatRuntime`.
- any `UNKNOWN` item without a complete C# plus serialized-reference scan.

## Detailed cleanup register

Every row below is a proposed removal. `Unsafe now` means the finding is removable only after its listed dependency is complete.

| ID | Exact path and symbol | Class | Direct references found | Scene/prefab/asmdef evidence | Replacement authority | Safety rationale and prerequisites | Proposed cleanup PR | Risk |
|---|---|---|---|---|---|---|---|---|
| T-01 | `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs` — `StartingPlayerHealth`, `PlayerHealth` fallback/rounded read | TRANSITIONAL | HUD/debug/test-facing properties and compact GUI read it; truth is exported by `Stage1PlayerLiveAuthorityAdapterV1` | Controller is serialized in the current Stage 1 scene | `PlayerActorAuthority` snapshot | Unsafe now. Remove after every HUD/debug/test reader consumes immutable `PlayerHudHealthSnapshot`; retain no fallback health value | `CLEANUP-PLAYER-001` | medium |
| T-02 | same file — `restartGeneration`, `playerShotSequence`, `damageSequence`, `droidDamageOrder`, observed-hit counters | TRANSITIONAL | Event IDs, VFX/debug observation, restart projection, old projectile/enemy bridges | Current scene uses the controller | player lifecycle generation plus canonical weapon/enemy operation identities | Unsafe now. First route fire/hit operations through canonical runtimes and replace observation-only counters with immutable projections | `CLEANUP-PLAYER-001` / `CLEANUP-WPN-001` | high |
| T-03 | same file — `ApplyAcceptedPlayerRestart` broad scene reset | TRANSITIONAL | Called by live player adapter after accepted authority restart; resets room, enemies, projectiles, movement, UI, camera | Current scene/controller binding | `PlayerActorAuthority` plus explicit restart participants; ROOM-LIVE restart | Unsafe now. Extract participants and prove one accepted restart projects once before deleting the monolithic reset method | `CUTOVER-PLAYER-002` | high |
| T-04 | `Assets/ShooterMover/ContentPackages/Weapons/Stage1Loadouts/Stage1WeaponLoadoutFixtures.cs`; `Assets/ShooterMover/UI/VisibleSliceLoadoutSelector/**`; `Assets/ShooterMover/ContentPackages/Weapons/Stage1Presentation/Stage1WeaponStatusStrip.cs` | TRANSITIONAL | Controller imports fixture/catalog, creates selector and strip, and resets them on restart | Current controller is serialized; selector/strip are runtime-created rather than scene-serialized | exact route-profile equipment-instance IDs and `RouteProfileActiveWeaponSource` | Unsafe now. Remove only after current Stage 1 uses the selected concrete inventory instance and production Inventory/Loadout UI | `CUTOVER-WPN-002`, then `CLEANUP-WPN-001` | high |
| T-05 | `Stage1VisibleSliceController.cs` — `BuildVoidHazard`, `Demo002VoidHazard`, `placed.demo002-void-hazard`, `checkpoint.demo002` | TRANSITIONAL | Built by `BuildSession`; player port is now authority-backed | Runtime-created by current scene; no serialized hazard prefab | accepted void-hazard package plus authorable room presentation | Unsafe now. Author the hazard with stable production identities in the new room composition first | `CUTOVER-ROOM-002` | medium |
| T-06 | `Stage1VisibleSliceController.cs` and `Stage1VisibleSlice.unity` — `TestSupport.VisibleSlice`, `Prototypes`, `VS007`, `demo002` naming surface | TRANSITIONAL | Current route constants and current scene directly use these names | Explicit current scene serialization | thin Stage 1 gameplay composition after cutovers | Unsafe now. Historical naming may be retired only with a deliberate scene/composition migration; do not rename as a cleanup shortcut | `CLEANUP-STAGE1-001` | high |
| T-07 | `Stage1VisibleSliceController.cs` — `shootingSandbox`, `ConfirmDefaultLoadout`, `FireAtTurretForTests`, `FireAtMobileDroidForTests` | TRANSITIONAL | PlayMode/test hooks and current sandbox branch; scene serializes `shootingSandbox: 1` | Direct current scene field value | production gameplay input and dedicated test harness adapters | Unsafe now. Move test control to test-owned adapters and set the playable route to production composition before removal | `CUTOVER-DEMO-006` | high |
| D-01 | `Stage1VisibleSliceController.cs` — `FireSelectedLoadout` | DUPLICATE | `ReadCombatInput` invokes it; loops all fixture slots rather than resolving one active equipment instance | Current scene/controller | `InventoryWeaponRuntimeComposition` and `RouteProfileActiveWeaponSource` | Unsafe now. Wire exact active equipment instance first, then delete in same PR | `CUTOVER-WPN-002` | high |
| D-02 | same file — `FireWeapon` weapon-ID switch and silent blaster fallback | DUPLICATE | Called by `FireSelectedLoadout`; branches Shotgun/Rocket/Arc/Ricochet/default | Current scene/controller | `InventoryBackedWeaponExecutionAdapter` + catalog-driven `WeaponExecutionCore` | Unsafe now. Canonical adapter must be the live caller; unknown definitions must fail closed rather than fall back | `CUTOVER-WPN-002` | high |
| D-03 | same file — `FireProjectile` and `FireBlaster` | DUPLICATE | Both instantiate/configure `BoundedProjectile2D`; test and input paths call them | Current scene/controller; projectile template runtime-created | transactional `InventoryWeaponEffectEmitter2D` and canonical effect batch | Unsafe now. Migrate physical emission, then remove both methods and name-based projectile cleanup | `CUTOVER-WPN-002` | high |
| D-04 | same file — `BlasterFireIntervalSeconds`, `nextBlasterShotTime` | DUPLICATE | `ReadCombatInput` admits shots locally and resets cooldown on restart | Current scene/controller | `WeaponExecutionCore` cooldown scope | Unsafe now. Delete only when all current fire attempts are admitted by the core | `CUTOVER-WPN-002` | high |
| D-05 | same file — `PlayerShotDamage`, projectile speed/lifetime/radius, Shotgun pellet/spread calculation, hardcoded Rocket/Arc/Ricochet values | DUPLICATE | `FireWeapon`, `FireProjectile`, direct enemy commands, destructible integration | Current scene/controller | JSON-derived weapon catalog and `WeaponExecutionCore` effect facts | Unsafe now. Prove Blaster/Shotgun/Rocket/Flamethrower or current starter definitions through inventory-backed execution, then remove all local tuning | `CUTOVER-WPN-002` | high |
| D-06 | same file — `HandlePlayerShotHit` and `droidDamageOrder` | DUPLICATE | subscribed during `BuildMobileBlasterDroid`; directly calls `EnemyTarget.ApplyHit` | Current runtime-created droid | accepted enemy combat authority and canonical weapon effect/hit route | Unsafe now. Bind emitted weapon effects to the generic enemy damage port before deleting | `CUTOVER-ENEMY-002` | high |
| D-07 | same file — non-sandbox branch of `FireAtTurretForTests` and `ApplyPlayerProjectileDamageToTurret` | DUPLICATE | directly constructs `EnemyActorCommand.Damage`; the latter had no indexed reference and is not relied on as dead proof | Controller is current; methods are source/test surface, not serialized separately | accepted enemy authority reached through canonical hit/effect execution | Unsafe now. Move test actions to a test-owned canonical command adapter; compile removal catches any hidden caller | `CLEANUP-ENEMY-001` | medium |
| D-08 | same file — exact `enemy.mobile-blaster-droid` / `enemy.blaster-turret` branches, `BuildMobileBlasterDroid`, `BuildTurret`, hardcoded runtime definitions | DUPLICATE | `BuildRooms` selects package-specific construction and tuning | Current scene supplies room definitions/prefabs; controller constructs packages | authorable room placements, presentation catalog, generic enemy terminal relay, existing enemy authorities | Unsafe now. `RoomRuntimeComposition2D` must build current Level 1 and bind accepted enemy packages first | `CUTOVER-ROOM-002` | high |
| D-09 | same file — `roomProjections`, `DemoRoomProjection`, `RefreshArenaFlow`, `SwitchRoom`, room-ID and x-coordinate traversal branches | DUPLICATE | Update/LateUpdate/restart/HUD read this state directly | Current scene/controller; room definitions serialized | `RoomRuntimeComposition2D` / `RoomLiveRuntimeAuthorityV1` | Unsafe now. Migrate Level 1 to authorable room graph, query snapshots, and explicit exits; then delete direct projection logic | `CUTOVER-ROOM-002` | high |
| D-10 | same file — `BuildExitDoor`, `entryExitDoor`, `terminalExitDoor`, direct `NotifyInteractionRequested` and completion-driven opening | DUPLICATE | room construction, clear flow, traversal, HUD, restart | Doors are runtime-created; no replacement serialized into current scene | ROOM-LIVE door definitions/gates and retained facts | Unsafe now. Author both doors and prove independent gate semantics/return traversal before deletion | `CUTOVER-ROOM-002` | high |
| D-11 | same file — compact `OnGUI`, compact styles, hardcoded objectives/enemy names | DUPLICATE | runs every rendered frame while `VisibleSliceGeneralCombatHud` is also created/refreshed | Current scene/controller; HUD is runtime-created | immutable HUD projections and one gameplay HUD owner | Unsafe now because the compact overlay is visibly active. Select one HUD, preserve required feedback, then delete duplicate presentation | `CLEANUP-UI-001` | medium |
| X-01 | `Assets/ShooterMover/UI/MainMenu/MainMenuController.cs` — `MainMenuController` | DEAD | referenced by `MainMenuArtworkController` and obsolete MENU-001 PlayMode tests; no canonical flow binding found | script GUID removed from `MainMenu.unity`; production-flow asmdef does not reference old UI asmdef | `ProductionMainMenuControllerV1`, `ProductionFlowCoordinatorV1`, canonical destination controllers | Safe as part of the complete old-menu cluster. Delete its tests and asmdef references atomically | `CLEANUP-FLOW-001` | low |
| X-02 | `Assets/ShooterMover/UI/MainMenu/MainMenuArtworkController.cs` — embedded Title/Level/Skills/Inventory/Shop/Crafting/Settings/Results owner | DEAD | requires/uses `MainMenuController`; referenced by obsolete artwork tests | removed from canonical Main Menu scene and replaced by `ProductionMainMenuControllerV1` | canonical separate flow scenes/controllers | Safe as part of cluster. Do not delete shared background assets merely because this owner used them | `CLEANUP-FLOW-001` | low |
| X-03 | `Assets/ShooterMover/UI/MainMenu/MainMenuPlatformActions.cs` — `UnityMainMenuPlatformActions`, reflection `MainMenuSceneSettingsBridge` | DEAD | used by old `MainMenuController`; hardcodes direct Stage 1 load and reflective presentation setters | no scene component; canonical flow owns scene requests | `ProductionSceneTransitionCoordinatorV1` and explicit scene controllers | Safe with old menu cluster. Remove reflection bridge and its tests/callers together | `CLEANUP-FLOW-001` | low |
| X-04 | `Assets/ShooterMover/Runtime/Application/Menu/MainMenuFlowState.cs` — `MainMenuFlowState`, `ArmoryLoadoutState`, `MenuWeaponOption`, settings state | DEAD | used by old menu UI and MENU-001 tests; it owns a parallel embedded screen history and hardcoded Stage 1 path | no serialized object; old UI asmdef depends on Application, canonical flow does not consume this model | `HubNavigationServiceV1`, `PlayerRouteProfilePayloadV1`, Inventory/Loadout authorities, level catalog | Safe only with the entire old cluster and obsolete tests removed in one PR; compile/focused flow checks must confirm no hidden caller | `CLEANUP-FLOW-001` | medium |

## Weapon-specific cleanup map

### Current duplicate path

```text
PlayerCombatIntentAdapter
  -> Stage1VisibleSliceController.ReadCombatInput
  -> nextBlasterShotTime
  -> Stage1WeaponLoadoutFixture
  -> FireSelectedLoadout (every fixture slot)
  -> FireWeapon (weapon-ID switch + fallback)
  -> FireProjectile / FireBlaster
  -> BoundedProjectile2D / direct enemy bridge
```

### Canonical target

```text
route profile exact equipment-instance ID
  -> RouteProfileActiveWeaponSource
  -> InventoryWeaponRuntimeComposition
  -> InventoryBackedWeaponExecutionAdapter
  -> WeaponExecutionCore
  -> immutable WeaponEffectBatch
  -> transactional InventoryWeaponEffectEmitter2D
```

### Cleanup rule

The live handoff and old-path deletion should be one reviewed PR. Leaving both paths callable after cutover preserves duplicate cooldown, spread, pellet, damage, projectile, replay, and fallback behavior.

## Player cleanup map

| Retained surface | Current status | Cleanup condition |
|---|---|---|
| `PlayerHealth` rounded property and `StartingPlayerHealth` fallback | compatibility read, not authority | every consumer reads immutable authority snapshots |
| `restartGeneration` fallback and scene counters | presentation/event migration state | canonical weapon/enemy operation IDs and restart projections no longer need local mirrors |
| `ApplyAcceptedPlayerRestart` | required current downstream projection | explicit restart participants cover room, enemy, projectile, movement, HUD, and camera exactly once |
| player construction/movement/input fields in controller | current playable composition | a reusable player presentation/composition component owns them without creating a second health authority |
| `Stage1PlayerLiveAuthorityAdapterV1` | LIVE | keep until the scene itself is composed directly from `PlayerRuntimeCompositionRoot` or an equivalent accepted root |

No old mutable integer player-health authority was found on current main. Do not create a cleanup item for a field that no longer exists.

## Enemy cleanup map

| Surface | Classification | Direction |
|---|---|---|
| `MobileBlasterDroidRuntime2D` and its accepted authority | LIVE | keep |
| `BlasterTurretPackage` and its accepted authority | LIVE | keep |
| controller package-specific construction/tuning branches | DUPLICATE | replace with authored placements/presentation catalog |
| controller direct droid/turret damage commands | DUPLICATE | route canonical weapon effects through accepted enemy damage ports |
| controller turret/droid presentation refresh | LIVE/scene projection today | keep until replacement presentation binds immutable enemy state |
| temporary enemy presentation components | UNKNOWN | scan prefab GUIDs before recommendation |

## Room cleanup map

ROOM-LIVE-001 does not make `RoomMissionLayoutV1` obsolete. It composes that accepted graph state owner with occupant, completion, retained-fact, door-gate, traversal, and immutable projection behavior. The removable duplication is the controller’s direct bypass around the coordinated runtime.

Remove after migration:

- `DemoRoomProjection` and `roomProjections`;
- exact package-name branching during room projection;
- `RefreshArenaFlow` clear polling;
- hardcoded room IDs, door lanes, and x-coordinate thresholds;
- runtime `BuildExitDoor` state;
- controller-owned `SwitchRoom` activation/reposition logic;
- manual room restart calls once ROOM-LIVE restart owns reconstruction.

Keep:

- `RoomMissionLayoutV1`;
- ROOM-RUNTIME/ROOM-LIVE authorities and contracts;
- authorable Level 1 definition;
- presentation catalog and generic terminal relay;
- current serialized room presentation assets until the new composition references replacements.

## Flow/UI cleanup map

### Proven dead

The old MENU-001 implementation is a closed superseded cluster. The canonical Main Menu scene has one `ProductionMainMenuControllerV1`; the old owner and artwork controller were removed from scene serialization. Bootstrap now owns one persistent `ProductionFlowCoordinatorV1`, and canonical screens are separate scenes.

### Still live

- production flow coordinator/session/path constants;
- current destination controllers;
- current Results exact-box binding;
- flow camera and gameplay camera as separate mode-specific owners;
- shared artwork assets serialized by current scenes.

### Later cleanup

- duplicate compact Stage 1 HUD;
- visible-slice loadout UI after Inventory/Loadout becomes the only selectable equipment path;
- historical `TestSupport/VisibleSlice` naming after replacement composition/scene exists.

## UNKNOWN findings — no removal recommendation

| ID | Exact path or scope | Why evidence is insufficient | Required proof |
|---|---|---|---|
| U-01 | `Assets/ShooterMover/ContentPackages/Encounters/Stage1ShortRoute/Stage1ShortRouteComposition.cs` | Looks like an older route/content model, but source/prefab/test consumers were not exhaustively resolved | complete symbol reference scan and current route/authoring comparison |
| U-02 | `Assets/ShooterMover/ContentPackages/Encounters/Stage1ShortRoute/Stage1ShortRouteSession.cs` | May be test/evidence-only or retained encounter logic; no safe negative proof | C# references, asmdef dependents, tests, and any serialized authoring references |
| U-03 | `Assets/ShooterMover/ContentPackages/Enemies/MobileBlasterDroid/MobileBlasterDroidTemporaryPresentation.cs` | “Temporary” is not evidence; it may still be attached to a current prefab or constructed by runtime | script GUID scan across prefabs/scenes plus construction references |
| U-04 | `Assets/ShooterMover/ContentPackages/Enemies/RamDroid/RamDroidTemporaryPresentation.cs` | same uncertainty; Ram Droid may be future/current package content | prefab/scene GUID and C# construction scan |
| U-05 | repository-wide orphaned prefabs/scenes/ScriptableObjects/sprites and apparently unused asmdefs outside the explicitly inspected routes | connector inspection cannot establish absence across every serialized YAML and binary/import dependency | local repository GUID index, asmdef graph, Editor dependency scan, and build report |

## Proposed cleanup order

1. **`CLEANUP-FLOW-001` — remove the dead MENU-001 cluster.** Delete the four dead source files, old UI asmdef/metadata, and obsolete focused tests/empty test asmdefs. Do not delete shared art. Run compile plus focused production-flow tests.
2. **`CUTOVER-WPN-002` — make inventory-backed execution the Stage 1 fire path and delete D-01 through D-05 in the same PR.** Preserve exact equipment identity, operation idempotency, catalog facts, and transactional effects.
3. **`CUTOVER-ENEMY-002` — bind canonical weapon effects to accepted enemy damage ports and remove D-06/D-07.** Do not add another enemy authority.
4. **`CUTOVER-ROOM-002` — instantiate the current Level 1 through `RoomRuntimeComposition2D`; remove D-08 through D-10 and the related restart bypass.** Preserve return traversal, final exit, defeated-enemy retention, and door gates.
5. **`CUTOVER-PLAYER-002` — extract explicit restart/presentation participants and reduce T-01 through T-03.** `PlayerActorAuthority` remains the sole health/death/generation authority.
6. **`CLEANUP-UI-001` — select one gameplay HUD and remove D-11 plus obsolete visible-slice loadout/status UI after the production Inventory/Loadout route is authoritative in gameplay.
7. **`CLEANUP-STAGE1-001` — retire historical names, sandbox hooks, test-support placement, and remaining compatibility APIs only after the replacement scene/composition is the current catalog route.
8. **`LEGACY-AUDIT-002` — run a local exhaustive GUID/asmdef/Editor dependency scan for U-01 through U-05.** Promote findings only with evidence.

## Files that must remain for the current playable demo

At minimum, retain the following until their replacements are both routed and proven:

- `Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity`
- `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs`
- `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1PlayerLiveAuthorityAdapterV1.cs`
- `Assets/ShooterMover/ContentPackages/Weapons/Stage1Loadouts/Stage1WeaponLoadoutFixtures.cs`
- `Assets/ShooterMover/UI/VisibleSliceLoadoutSelector/**`
- `Assets/ShooterMover/ContentPackages/Weapons/Stage1Presentation/**`
- current room presentation prefab and both room definition assets serialized in the Stage 1 scene
- current turret presenter prefab, projectile sprites, player sprite, and destruction-animation assets serialized in the Stage 1 scene
- `MobileBlasterDroidRuntime2D` and its current prefab/package
- `BlasterTurretPackage`, authoring/context, presenter, and current prefab/package
- `RoomMissionLayoutV1` and current Level 1 room graph/content definitions
- accepted projectile, hit, void-hazard, destructible-prop, movement, camera, and HUD packages currently constructed by the controller
- current production-flow, level-selection, mission/results, and strongbox-opening authorities/controllers

## Validation performed

- verified `main` remained exactly `1e7f835657afcf479f132db32d0032a2f0d899c3` immediately before branch creation;
- inspected current route constants and build settings;
- inspected current Stage 1 scene serialization and its script/asset GUID references;
- inspected current controller player/weapon/enemy/room/UI/restart paths;
- inspected PLAYER-LIVE-001, WPN-LIVE-001, ROOM-LIVE-001, and FLOW-UI-001 ownership documentation and current implementation files;
- inspected FLOW-UI-001 scene patches, including removal of old Main Menu script GUIDs and Bootstrap camera replacement;
- inspected old and current flow asmdefs;
- inspected the old MENU-001 source/test dependency cluster;
- classified uncertain candidates as `UNKNOWN` rather than recommending removal.

No Unity compilation, test suite, scene playthrough, asset import, or generated dependency report is claimed by this audit.

## Rollback

Revert the single documentation commit. No production source, scene, prefab, asset, test, or project setting is changed.