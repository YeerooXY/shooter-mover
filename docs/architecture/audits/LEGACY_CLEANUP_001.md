# LEGACY-AUDIT-001 — Removable legacy gameplay code audit

## Audit boundary

- **Repository:** `YeerooXY/shooter-mover`
- **Base branch:** `main`
- **Exact current main SHA:** `1e7f835657afcf479f132db32d0032a2f0d899c3`
- **Audit branch:** `agent/legacy-audit-001`
- **Output:** `docs/architecture/audits/LEGACY_CLEANUP_001.md`
- **Production behavior changed:** none
- **Tests added or run:** none
- **Unity Library copied or created:** no
- **Scenes, prefabs, ScriptableObjects, sprites, binaries, or generated files changed:** none
- **Explicit exclusion:** PR #225 and its duplicate `EnemyCombatRuntime` were not inspected and are not recommendations in this report.

A legacy-looking name is not removal evidence. `DEAD` is used only when the inspected current route, serialized bindings, source dependency closure, and assembly boundary all support removal. Insufficient evidence is `UNKNOWN`.

## Reference-search summary

The audit inspected:

- current `main` and the exact base SHA;
- C# call sites in the current Stage 1 controller;
- current route constants and build settings;
- current Stage 1 scene YAML and serialized script/asset GUIDs;
- FLOW-UI-001 scene patches, including removed Main Menu components and Bootstrap camera replacement;
- current player, weapon, enemy, room, flow, mission/results authority paths;
- relevant asmdefs and old MENU-001 tests.

Key evidence:

- `LevelSelectionCatalogDefinitionV1.Stage1ScenePath` still routes Level 1 to `Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity`.
- That scene contains one enabled `Stage1VisibleSliceController`, serializes its room/presentation assets, and has `shootingSandbox: 1`.
- The controller still owns a local weapon-ID switch, silent blaster fallback, cooldown, spread/pellet/projectile tuning, direct enemy hit bridges, exact enemy-package construction branches, direct room-clear/door/traversal logic, compact HUD, and broad restart projection.
- Player health/death/generation truth now comes from `PlayerActorAuthority`; the old mutable integer health authority is absent on current main. `PlayerHealth` is a rounded compatibility read.
- WPN-LIVE-001 supplies `WeaponExecutionCore`, `InventoryBackedWeaponExecutionAdapter`, exact equipment-instance resolution, and transactional effect emission, but the current Stage 1 controller does not call that path.
- ROOM-LIVE-001 supplies `RoomRuntimeComposition2D` and the coordinated room authority, but explicitly did not migrate the Stage 1 scene/controller.
- FLOW-UI-001 removed the old `MainMenuController` GUID from `MainMenu.unity`, replaced the artwork owner with `ProductionMainMenuControllerV1`, and installed `ProductionFlowCoordinatorV1` in Bootstrap.
- `ShooterMover.UI.ProductionFlow.asmdef` does not reference `ShooterMover.UI.MainMenu`.

### Limitations

GitHub code-search indexing returned no matches even for known current symbols/GUIDs, so negative indexed searches were not used as dead-code proof. No local clone, Unity Editor dependency database, exhaustive prefab GUID index, or binary/import scan was available. Those gaps are reflected in `UNKNOWN` findings rather than guessed removals.

## Summary counts

| Classification | Count |
|---|---:|
| LIVE | 10 |
| TRANSITIONAL | 7 |
| DUPLICATE | 11 |
| DEAD | 4 |
| UNKNOWN | 5 |
| **Total** | **37** |

## LIVE findings — keep

| ID | Exact path/type | Why it remains live |
|---|---|---|
| L-01 | `Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity` and `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs` | Current Level 1 route and serialized playable composition. Do not remove or edit in a cleanup-only task. |
| L-02 | `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1PlayerLiveAuthorityAdapterV1.cs` | Current typed bridge from the scene to accepted player authority, damage, death, healing, generation, and restart. |
| L-03 | `Assets/ShooterMover/Runtime/GameplayEntities/PlayerActorAuthority.cs` and current `PlayerRuntimeComposition` | Canonical player health/death/healing/restart authority and immutable runtime state. |
| L-04 | `Assets/ShooterMover/Runtime/Application/Weapons/Execution/**`, `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/**`, and `PlayerInventoryWeaponRuntimeComposition.cs` | Canonical inventory-backed weapon execution foundation. It needs live wiring, not removal. |
| L-05 | `Assets/ShooterMover/ContentPackages/Enemies/MobileBlasterDroid/MobileBlasterDroidRuntime2D.cs` | Existing accepted moving-droid authority/runtime used by the current demo and ROOM-LIVE proof. |
| L-06 | `Assets/ShooterMover/ContentPackages/Enemies/BlasterTurret/**` | Existing accepted turret authority/runtime used by the current demo and ROOM-LIVE proof. |
| L-07 | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomMissionLayoutV1.cs` | Accepted graph traversal/completion state owner reused by ROOM-LIVE; only direct controller bypass is removable. |
| L-08 | `Assets/ShooterMover/Runtime/UnityAdapters/Missions/Rooms/RoomRuntimeComposition2D.cs` and ROOM-LIVE application/contracts | Canonical coordinated room runtime foundation awaiting Stage 1 cutover. |
| L-09 | `Assets/ShooterMover/UI/ProductionFlow/ProductionFlowCoordinatorV1.cs` and `ProductionFlowSessionV1.cs` | Canonical persistent flow and scene transition owner. |
| L-10 | current mission/run/results authorities, `ProductionResultsControllerV1`, and `StrongboxOpeningServiceV1` | Canonical immutable Results/box route; not part of legacy removal. |

## Safe to remove now

Remove the following as one atomic cleanup unit:

- `Assets/ShooterMover/Runtime/Application/Menu/MainMenuFlowState.cs`
- `Assets/ShooterMover/UI/MainMenu/MainMenuController.cs`
- `Assets/ShooterMover/UI/MainMenu/MainMenuArtworkController.cs`
- `Assets/ShooterMover/UI/MainMenu/MainMenuPlatformActions.cs`
- `Assets/ShooterMover/UI/MainMenu/ShooterMover.UI.MainMenu.asmdef`
- corresponding `.meta` files
- `Assets/ShooterMover/Tests/EditMode/Menu/MainMenuFlowStateTests.cs`
- `Assets/ShooterMover/Tests/PlayMode/Menu/MainMenuControllerTests.cs`
- `Assets/ShooterMover/Tests/PlayMode/Menu/MainMenuArtworkControllerTests.cs`
- obsolete/empty test asmdefs and metadata left by that cluster

Do **not** remove shared Menu/Skills/Results artwork in that PR. Current canonical scenes still use supplied artwork assets.

## Remove after playable cutover

- local Stage 1 loadout fixtures/selector/status strip;
- local weapon fire dispatcher, cooldown, fallback, projectile and tuning logic;
- direct controller-to-enemy damage bridges;
- package-specific enemy construction branches;
- `DemoRoomProjection`, runtime door ownership, room-clear polling, and hardcoded traversal;
- duplicate compact `OnGUI` HUD;
- sandbox-only production hooks, compatibility mirrors, and historical demo naming.

## Do not touch

- `Stage1VisibleSliceController.cs` as a whole until replacement gameplay composition is the routed playable path;
- `Stage1VisibleSlice.unity` or its serialized assets in a source-only cleanup;
- canonical authorities listed in L-03 through L-10;
- current flow scenes or Bootstrap ownership;
- PR #225 or its duplicate `EnemyCombatRuntime`;
- any `UNKNOWN` item without a complete source plus serialized-reference scan.

## Detailed removal register

Every row is one finding and uses exactly one required classification.

| ID | Exact file path and symbol/asset | Class | Direct references found | Scene/prefab/asmdef references | Current replacement authority | Safe/unsafe and dependencies first | Proposed cleanup PR | Risk |
|---|---|---|---|---|---|---|---|---|
| T-01 | `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs` — `StartingPlayerHealth`, rounded `PlayerHealth` fallback | TRANSITIONAL | Compact HUD/debug/test readers; authority snapshot already supplies truth | Controller serialized in current Stage 1 scene | `PlayerActorAuthority` snapshot | Unsafe now. First migrate every reader to immutable `PlayerHudHealthSnapshot` | `CLEANUP-PLAYER-001` | medium |
| T-02 | `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs` — `restartGeneration`, shot/damage/hit-order counters | TRANSITIONAL | Old event IDs, VFX/debug observation, projectile/enemy bridges, restart | Current scene/controller | player lifecycle generation plus canonical weapon/enemy operation IDs | Unsafe now. Remove after WPN/ENEMY live routes no longer need local mirrors | `CLEANUP-PLAYER-001` / `CLEANUP-WPN-001` | high |
| T-03 | `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs` — `ApplyAcceptedPlayerRestart` | TRANSITIONAL | Called after accepted player restart; resets rooms, enemies, projectiles, movement, UI, camera | Current scene/controller | `PlayerActorAuthority` plus explicit restart participants and ROOM-LIVE restart | Unsafe now. Extract and prove exactly-once restart participants first | `CUTOVER-PLAYER-002` | high |
| T-04 | `Assets/ShooterMover/ContentPackages/Weapons/Stage1Loadouts/Stage1WeaponLoadoutFixtures.cs`; `Assets/ShooterMover/UI/VisibleSliceLoadoutSelector/**`; `Assets/ShooterMover/ContentPackages/Weapons/Stage1Presentation/Stage1WeaponStatusStrip.cs` | TRANSITIONAL | Current controller creates/selects/resets them | Runtime-created by current controller; related asmdefs/tests remain | route-profile exact equipment IDs and `RouteProfileActiveWeaponSource` | Unsafe now. First make production Inventory/Loadout and active equipment instance the gameplay source | `CUTOVER-WPN-002`, then `CLEANUP-WPN-001` | high |
| T-05 | `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs` — `BuildVoidHazard`, `Demo002VoidHazard`, demo/checkpoint IDs | TRANSITIONAL | Called by `BuildSession`; player port is authority-backed | Runtime-created; no serialized hazard prefab | accepted void-hazard package plus authorable room presentation | Unsafe now. Author the hazard with durable production IDs in ROOM-LIVE first | `CUTOVER-ROOM-002` | medium |
| T-06 | `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs` and `Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity` — `TestSupport`, `Prototypes`, `VS007`, `demo002` naming | TRANSITIONAL | Current route constants and source IDs use them | Explicit current scene serialization | thin Stage 1 production composition | Unsafe now. Rename/move only with a deliberate scene/composition migration | `CLEANUP-STAGE1-001` | high |
| T-07 | `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs` — `shootingSandbox`, `ConfirmDefaultLoadout`, `FireAtTurretForTests`, `FireAtMobileDroidForTests` | TRANSITIONAL | PlayMode/test hooks and current sandbox path | Scene serializes `shootingSandbox: 1` | production input plus test-owned adapters | Unsafe now. Move test control out and route production gameplay first | `CUTOVER-DEMO-006` | high |
| D-01 | `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs` — `FireSelectedLoadout` | DUPLICATE | Called by `ReadCombatInput`; fires every fixture slot | Current controller serialized | `InventoryWeaponRuntimeComposition` and active exact equipment source | Unsafe now. Wire canonical runtime, then delete in the same PR | `CUTOVER-WPN-002` | high |
| D-02 | `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs` — `FireWeapon` ID switch and silent blaster fallback | DUPLICATE | Called by `FireSelectedLoadout`; Shotgun/Rocket/Arc/Ricochet/default branches | Current controller serialized | `InventoryBackedWeaponExecutionAdapter` + catalog-driven `WeaponExecutionCore` | Unsafe now. Canonical adapter must be live and unknown definitions must fail closed | `CUTOVER-WPN-002` | high |
| D-03 | `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs` — `FireProjectile`, `FireBlaster`, name-based cancellation | DUPLICATE | Input/test paths instantiate `BoundedProjectile2D` through both methods | Runtime-created projectile template | transactional `InventoryWeaponEffectEmitter2D` and immutable effect batch | Unsafe now. Migrate physical emission first | `CUTOVER-WPN-002` | high |
| D-04 | `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs` — `BlasterFireIntervalSeconds`, `nextBlasterShotTime` | DUPLICATE | Local fire admission and restart reset | Current controller serialized | `WeaponExecutionCore` cooldown scope | Unsafe now. Delete only when every fire attempt is admitted by the core | `CUTOVER-WPN-002` | high |
| D-05 | `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs` — `PlayerShotDamage`, speed/lifetime/radius, Shotgun pellet/spread and hardcoded weapon values | DUPLICATE | Used by fire methods, direct enemy commands, destructible integration | Current controller serialized | JSON weapon catalog and `WeaponExecutionCore` effect facts | Unsafe now. Prove current starter definitions through live adapter first | `CUTOVER-WPN-002` | high |
| D-06 | `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs` — `HandlePlayerShotHit`, `droidDamageOrder` | DUPLICATE | Subscribed during droid construction; directly calls enemy target hit | Current runtime-created droid | accepted enemy authority via canonical hit/effect route | Unsafe now. Bind weapon effects to generic enemy damage port first | `CUTOVER-ENEMY-002` | high |
| D-07 | `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs` — direct turret `EnemyActorCommand.Damage` paths, including `ApplyPlayerProjectileDamageToTurret` | DUPLICATE | Test branch directly constructs damage; no indexed caller for helper, so it is not classified DEAD | Current controller source/test surface | accepted enemy authority reached through canonical effects | Unsafe now. Move tests to canonical adapter and compile-check deletion | `CLEANUP-ENEMY-001` | medium |
| D-08 | `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs` — exact droid/turret content-ID branches, `BuildMobileBlasterDroid`, `BuildTurret`, hardcoded definitions | DUPLICATE | `BuildRooms` performs package-specific construction/tuning | Current scene supplies room definitions/prefabs | authorable room placements, presentation catalog, generic terminal relay, existing enemy authorities | Unsafe now. Current Level 1 must be built by `RoomRuntimeComposition2D` first | `CUTOVER-ROOM-002` | high |
| D-09 | `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs` — `roomProjections`, `DemoRoomProjection`, `RefreshArenaFlow`, `SwitchRoom`, room-ID/x-coordinate branches | DUPLICATE | Update/LateUpdate/restart/HUD read and mutate this state | Current controller and serialized room definitions | `RoomRuntimeComposition2D` / `RoomLiveRuntimeAuthorityV1` | Unsafe now. Migrate to authorable graph, immutable query and explicit exits first | `CUTOVER-ROOM-002` | high |
| D-10 | `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs` — `BuildExitDoor`, door fields, direct completion-driven opening | DUPLICATE | Room build, clear, traversal, HUD and restart | Doors runtime-created by controller | ROOM-LIVE authored doors, gates and retained facts | Unsafe now. Author and prove return/final door semantics first | `CUTOVER-ROOM-002` | high |
| D-11 | `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs` — compact `OnGUI`, styles, hardcoded objectives/enemy names | DUPLICATE | Runs every frame while `VisibleSliceGeneralCombatHud` is also created/refreshed | Current controller; HUD runtime-created | one immutable-projection gameplay HUD | Unsafe now because compact overlay is visible. Preserve required feedback before deletion | `CLEANUP-UI-001` | medium |
| X-01 | `Assets/ShooterMover/UI/MainMenu/MainMenuController.cs` — `MainMenuController` | DEAD | Only old artwork owner and obsolete MENU-001 tests found | Script GUID removed from current `MainMenu.unity`; production-flow asmdef has no old UI reference | `ProductionMainMenuControllerV1` and canonical destination controllers | Safe as part of whole old-menu cluster; remove tests/asmdef atomically | `CLEANUP-FLOW-001` | low |
| X-02 | `Assets/ShooterMover/UI/MainMenu/MainMenuArtworkController.cs` — embedded multi-screen owner | DEAD | Requires old controller; obsolete artwork tests | Removed from current Main Menu scene and replaced by production controller | separate canonical scenes/controllers | Safe with cluster. Keep shared artwork assets | `CLEANUP-FLOW-001` | low |
| X-03 | `Assets/ShooterMover/UI/MainMenu/MainMenuPlatformActions.cs` — direct Stage 1 loader and reflection settings bridge | DEAD | Used by old controller only in inspected cluster | No current scene component; canonical flow owns loads | `ProductionSceneTransitionCoordinatorV1` and explicit screen/gameplay adapters | Safe with cluster; remove reflection bridge/callers together | `CLEANUP-FLOW-001` | low |
| X-04 | `Assets/ShooterMover/Runtime/Application/Menu/MainMenuFlowState.cs` — embedded screen history, armory model, hardcoded Stage 1 path | DEAD | Old UI and obsolete MENU-001 tests | No serialized object; canonical flow does not consume old UI asmdef | `HubNavigationServiceV1`, route profile, Inventory/Loadout, level catalog | Safe only as one cluster; compile/focused flow checks must catch hidden callers | `CLEANUP-FLOW-001` | medium |

## UNKNOWN findings — do not recommend removal

| ID | Exact path/scope | Why UNKNOWN | Proof required |
|---|---|---|---|
| U-01 | `Assets/ShooterMover/ContentPackages/Encounters/Stage1ShortRoute/Stage1ShortRouteComposition.cs` | Older-looking route/content model, but consumers were not exhaustively resolved | complete C# reference and current authoring comparison |
| U-02 | `Assets/ShooterMover/ContentPackages/Encounters/Stage1ShortRoute/Stage1ShortRouteSession.cs` | May be retained encounter/evidence logic | symbol, asmdef, test, scene/prefab reference scan |
| U-03 | `Assets/ShooterMover/ContentPackages/Enemies/MobileBlasterDroid/MobileBlasterDroidTemporaryPresentation.cs` | “Temporary” is not proof; may be attached to current prefab/runtime | script GUID scan across scenes/prefabs plus construction references |
| U-04 | `Assets/ShooterMover/ContentPackages/Enemies/RamDroid/RamDroidTemporaryPresentation.cs` | Same uncertainty; possible current/future package content | script GUID and C# construction scan |
| U-05 | repository-wide orphaned prefabs/scenes/ScriptableObjects/sprites and apparently unused asmdefs outside inspected routes | No exhaustive Unity dependency index was available | local GUID index, asmdef graph, Editor dependency scan and build report |

## Weapon cleanup map

Current duplicate path:

```text
PlayerCombatIntentAdapter
  -> Stage1VisibleSliceController.ReadCombatInput
  -> local cooldown
  -> Stage1WeaponLoadoutFixture
  -> FireSelectedLoadout (all fixture slots)
  -> FireWeapon (ID switch + fallback)
  -> FireProjectile / FireBlaster
  -> local projectile/direct enemy bridge
```

Canonical target:

```text
route profile exact equipment-instance ID
  -> RouteProfileActiveWeaponSource
  -> InventoryWeaponRuntimeComposition
  -> InventoryBackedWeaponExecutionAdapter
  -> WeaponExecutionCore
  -> immutable WeaponEffectBatch
  -> transactional InventoryWeaponEffectEmitter2D
```

Live handoff and deletion of D-01 through D-05 should be one PR; otherwise two cooldown, spread, pellet, damage, projectile, and replay models remain callable.

## Player cleanup map

- Keep `PlayerActorAuthority`, `PlayerRuntimeComposition`, and the current live adapter.
- Remove T-01 only when all HUD/debug/test readers consume immutable snapshots.
- Remove T-02 after canonical operation identity replaces local counters.
- Replace T-03 with explicit restart participants; do not let UI/collision code regain health authority.
- No old mutable integer health field exists on current main; do not invent a cleanup item for one.

## Enemy cleanup map

- Keep accepted moving-droid and turret authorities.
- Remove D-06/D-07 after canonical weapon effects reach generic enemy damage ports.
- Remove D-08 after authorable room presentation constructs accepted enemy packages without package-name branches.
- Temporary presentations remain U-03/U-04 until prefab GUID proof exists.

## Room cleanup map

`RoomMissionLayoutV1` is not legacy: ROOM-LIVE composes it. The duplication is the controller’s direct bypass.

After ROOM-LIVE cutover remove:

- `DemoRoomProjection` and direct clear polling;
- package-name construction branches;
- runtime door ownership;
- hardcoded room IDs, door lanes and coordinates;
- manual room activation/reposition and restart calls.

Keep authorable definitions, presentation catalog, generic terminal relay, coordinated authority, and current serialized room assets until replacements are referenced.

## Flow/UI cleanup map

- The old MENU-001 cluster X-01 through X-04 is safe now.
- Keep production flow, current destination controllers, exact Results/box binding, and shared artwork.
- The flow camera and gameplay camera are different mode owners; do not classify them as duplicates.
- Remove D-11 and T-04 only after one gameplay HUD and production equipment selection are live.

## Proposed cleanup order

1. **`CLEANUP-FLOW-001`** — remove X-01 through X-04, old UI asmdef/meta, obsolete tests, and empty test asmdefs. Keep artwork. Run compile plus focused production-flow tests.
2. **`CUTOVER-WPN-002`** — make inventory-backed execution the Stage 1 fire path and delete D-01 through D-05 in the same PR.
3. **`CUTOVER-ENEMY-002`** — bind canonical effects to accepted enemy damage ports; delete D-06/D-07.
4. **`CUTOVER-ROOM-002`** — instantiate current Level 1 through `RoomRuntimeComposition2D`; delete D-08 through D-10 and related direct restart bypass.
5. **`CUTOVER-PLAYER-002`** — extract restart/presentation participants and retire T-01 through T-03 when callers are migrated.
6. **`CLEANUP-UI-001`** — remove D-11 and obsolete visible-slice loadout/status UI after production equivalents are authoritative.
7. **`CLEANUP-STAGE1-001`** — retire sandbox hooks, compatibility APIs, and historical naming only after replacement composition is the current catalog route.
8. **`LEGACY-AUDIT-002`** — perform local exhaustive GUID/asmdef/Editor dependency analysis for U-01 through U-05.

## Files that must remain for the playable demo

At minimum:

- `Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity`
- `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs`
- `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1PlayerLiveAuthorityAdapterV1.cs`
- `Assets/ShooterMover/ContentPackages/Weapons/Stage1Loadouts/Stage1WeaponLoadoutFixtures.cs`
- `Assets/ShooterMover/UI/VisibleSliceLoadoutSelector/**`
- `Assets/ShooterMover/ContentPackages/Weapons/Stage1Presentation/**`
- current room presentation prefab and both serialized room definition assets
- current turret presenter, projectile sprites, player sprite, and destruction animations
- current moving-droid and turret packages/prefabs
- `RoomMissionLayoutV1` and current Level 1 graph/content definitions
- current projectile, hit, void-hazard, destructible-prop, movement, camera and HUD packages constructed by the controller
- current production-flow, level-selection, mission/results and strongbox-opening authorities/controllers

## Changed-file list

- `docs/architecture/audits/LEGACY_CLEANUP_001.md`

## Validation performed

- verified `main` was still exactly `1e7f835657afcf479f132db32d0032a2f0d899c3` immediately before branch creation;
- inspected current C# ownership/call paths in the focus areas;
- inspected current route constants, build settings and Stage 1 scene serialization;
- inspected relevant serialized script GUID changes and asmdef directions;
- inspected PLAYER-LIVE-001, WPN-LIVE-001, ROOM-LIVE-001 and FLOW-UI-001 boundaries;
- did not run Unity, compilation, EditMode, PlayMode, large suites, or generated dependency scans.

## Rollback

Revert the documentation-only commits. No production source or asset is changed.