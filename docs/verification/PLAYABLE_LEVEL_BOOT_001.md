# PLAYABLE-LEVEL-BOOT-001 verification

## Repository baseline

- Repository: `YeerooXY/shooter-mover`
- Starting `main` SHA: `9a20e3707ae3f504eb0f383be3183cf0d5457bf5`
- Branch: `agent/playable-level-boot-001`
- `main` was checked again before PR preparation and still pointed at the starting SHA.

## Discovery record

### Production scene path and transition flow

The retained production path is:

`Bootstrap` -> `MainMenu` -> `CharacterSelect` -> `HubFlow` -> `PlaySelection` -> `LevelSelection` -> `PlayableLevel` -> `HubFlow`

Existing production scene transitions remain owned by `ProductionFlowCoordinatorV1` and its transition boundary. The playable level returns through `TryReturnToHub` rather than loading the Hub scene directly.

### Level Selection availability

`LevelSelectionControllerV1` builds the immutable level catalogue and delegates selection to `LevelSelectionServiceV1`. Locked entries are represented by `LevelAvailabilityV1.Locked`; the view renders those entries as a disabled `UNAVAILABLE` action. Unknown stable IDs are rejected by the service without emitting a scene route.

This change exposes exactly one unlocked live gameplay entry through the default production catalogue.

### Authored room content

The existing Level 1 package is retained under:

`Assets/ShooterMover/Content/Definitions/Missions/Rooms/Json/Level1/`

The runtime content asset is:

`Assets/ShooterMover/Resources/ProductionLevels/Level1RoomContent.asset`

It references the existing manifest and JSON sidecars by Unity asset reference. Room geometry is not duplicated in the gameplay scene.

### JSON bootstrap and runtime composition

The implementation reuses:

- `JsonRoomContentDefinition2D`
- `RoomContentJsonImporterV1`
- the existing importer validation and object catalogue
- `JsonRoomRuntimeBootstrap2D`
- `RoomRuntimeComposition2D`
- `RoomPresentationScene2D`
- `JsonRoomVisualPresentation2D`
- `RoomEnemySpawner2D`

The bootstrap received an explicit one-time production configuration method. It still imports through the existing importer and only commits the existing room runtime after a valid bundle is available.

### Retained movement and camera discovery

No retained production-ready player movement or camera-follow component remained connected after the deleted Stage1/Visible Slice gameplay removal. The change therefore adds only traversal adapters:

- bounded normalized keyboard input applied through `Rigidbody2D`
- one explicitly bound orthographic follow camera

No deleted gameplay composition, combat authority, health system, or Stage1 controller was restored.

### Authored player spawn

The room schema already supported stable spawn identity, position, rotation, and `RoomSpawnPointKindV1.Player`. No schema extension was necessary.

The exact authored initial spawn is:

- room: `room.level1-entry`
- spawn ID: `player-start`
- kind: `player`
- position: `[-10, 0]`
- rotation: `0`

The gameplay composition rejects a missing or duplicated player spawn before creating a player.

### Authored exit

The room schema already supported `RoomLiveLinkKindV1.FinalExit`. No level-specific exit controller was added.

The exact authored completion exit is:

- room: `room.level1-terminal`
- door ID: `hub-exit`
- link kind: `final-exit`
- exit type: `progression`

The traversal slice changes the existing Level 1 encounter door gates to `always` so the room can be traversed without implementing combat.

### Character route and runtime graph handoff

The selected level is captured in the existing `LevelSelectionRouteContextV1` before scene presentation. Gameplay then resolves the current `ProductionCharacterRuntimeGraphV1` from `ProductionCharacterAccountCompositionV1` and verifies that its route payload equals both the selected route payload and the current profile payload.

Gameplay adopts, without cloning or reconstruction:

- the existing character instance identity
- the existing class definition identity
- the exact route payload and fingerprint
- the exact `PlayerHoldingsService` instance
- the exact `ProductionInventoryLoadoutAuthorityV1` instance

The final exit checks reference equality for holdings and loadout plus the unchanged route fingerprint before requesting the Hub return.

### Existing disconnected gameplay scene

No retained production gameplay scene existed in build settings. The deleted Stage1 and Visible Slice scenes were not restored. A single generic production scene was added:

`Assets/ShooterMover/Scenes/Gameplay/PlayableLevel.unity`

## Level definition

- Stable level ID: `level.authored-json-1`
- Display name: `LEVEL 1`
- Gameplay scene: `Assets/ShooterMover/Scenes/Gameplay/PlayableLevel.unity`
- Room content resource: `ProductionLevels/Level1RoomContent`
- Enemy catalogue resource: `ProductionLevels/Level1EnemyCatalog`
- Player presentation ID: `presentation.player-production-default`

`ProductionPlayableLevelCatalogV1` is immutable and contains no runtime state. Selection and gameplay resolve through the stable ID and registered definition; there is no `if`/`switch` branch for Level 1.

## Spawn and camera handling

After the existing room runtime commits the imported graph:

1. the start room is checked for exactly one authored `player` spawn;
2. the scene is checked for an existing playable-player marker;
3. one configured Rigidbody2D player prefab is instantiated;
4. the exact current character context and authority references are bound to its marker;
5. its body is moved to the runtime's current authored spawn position and rotation;
6. one orthographic camera is created and explicitly bound to the player transform;
7. room rebuilds keep the player object and camera, move the player to the new authored spawn, and rebuild only gameplay traversal bindings.

Movement uses the Input System keyboard, normalizes diagonal input with `Vector2.ClampMagnitude`, and writes velocity in `FixedUpdate` using the project's 2D physics style.

## Collision handling

- Authored blocking prop and enemy presentations use generic colliders through the existing room presentation catalogue.
- Door blocking colliders remain controlled by `RoomDoorInstance2D.SetOpen`.
- Generic boundary colliders are derived from each authored room's validated `RoomBoundsV1`; no level-specific world coordinates are embedded in the controller.
- Door traversal uses a trigger bound directly to each spawned `RoomDoorInstance2D`.

## Exit and cleanup handling

The authored final-exit door calls the existing room authority's `Traverse` operation. `RoomRuntimeComposition2D` emits `FinalExitReached` only after the authority accepts the final exit.

The production traversal composition then:

1. accepts completion once;
2. rejects/logs duplicate completion requests;
3. verifies the same exact holdings and loadout authorities and route fingerprint;
4. requests `ProductionFlowCoordinatorV1.Transitions.TryReturnToHub` with the existing route payload;
5. releases scene-owned player, camera, presentation, collider and event bindings when the gameplay scene unloads.

No XP, currency, scrap, equipment, strongbox, reward, result, save, or starter-inventory mutation is performed by level entry or completion.

## Fail-closed diagnostics

The composition emits specific diagnostics for:

- missing level definition
- missing JSON content asset
- rejected JSON import, including the first structured importer issue
- rejected room, visual, or enemy composition
- missing or duplicated player spawn
- missing player prefab, Rigidbody2D, or collider
- missing character context or mismatched route payload
- missing or duplicated authored final exit
- duplicate player creation
- duplicate completion request
- missing current room/spawn or door presentation
- changed character holdings/loadout authority
- rejected Hub return

No arbitrary level, room, player coordinate, inventory, or scene fallback is fabricated.

## Files changed

### Production definitions and authored content

- `Assets/ShooterMover/Content/Definitions/Levels/Selection/LevelSelectionCatalogDefinitionV1.cs`
- `Assets/ShooterMover/Content/Definitions/Missions/Rooms/Json/Level1/level1.entry.layout.json`
- `Assets/ShooterMover/Content/Definitions/Missions/Rooms/Json/Level1/level1.entry.encounter.json`
- `Assets/ShooterMover/Content/Definitions/Missions/Rooms/Json/Level1/level1.terminal.layout.json`
- `Assets/ShooterMover/Content/Definitions/Missions/Rooms/Json/Level1/level1.terminal.encounter.json`

### Existing runtime integration points

- `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/RoomPresentationCatalog2D.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Missions/Rooms/JsonRoomRuntimeBootstrap2D.cs`
- `Assets/ShooterMover/UI/LevelSelection/LevelSelectionControllerV1.cs`
- `Assets/ShooterMover/UI/ProductionFlow/ShooterMover.UI.ProductionFlow.asmdef`

### Generic gameplay composition and Unity assets

- `Assets/ShooterMover/UI/ProductionFlow/ProductionPlayableLevelControllerV1.cs`
- `Assets/ShooterMover/Scenes/Gameplay/PlayableLevel.unity`
- `Assets/ShooterMover/Resources/ProductionLevels/Level1RoomContent.asset`
- `Assets/ShooterMover/Resources/ProductionLevels/Level1EnemyCatalog.asset`
- `Assets/ShooterMover/Resources/ProductionLevels/Level1PresentationCatalog.asset`
- `Assets/ShooterMover/Resources/ProductionLevels/ProductionPlayerPresentation.prefab`
- `Assets/ShooterMover/Resources/ProductionLevels/GenericRuntimePresentation.prefab`
- `Assets/ShooterMover/Resources/ProductionLevels/GenericSolidPresentation.prefab`
- corresponding Unity `.meta` files
- `ProjectSettings/EditorBuildSettings.asset`

## Systems deliberately not implemented

- enemy AI, target search, movement, attacks, health, damage, death, revive, lives
- weapon input, timing, projectiles, firing, weapon collision
- XP, money, scrap, mission rewards, strongboxes, drops, pickups, Keep/Sell
- inventory, shop, crafting, armour, augment, skill or ability redesign
- procedural generation, multiple objectives, results statistics

Enemies continue through the existing authored placement and PR #319 runtime binding path but receive no new ticking or combat behaviour.

## Static validation performed

Performed by repository/source and serialized-asset inspection:

- reconfirmed `main` at `9a20e3707ae3f504eb0f383be3183cf0d5457bf5` before PR preparation;
- branch compares ahead of that SHA and is not behind it;
- gameplay scene is registered in `EditorBuildSettings.asset`;
- every script GUID used by the new scene was matched to its existing or newly added `.meta` file;
- resource assets reference the existing authored JSON files and production enemy catalogue by GUID;
- JSON field names used by the edited level content match `RoomContentJsonDtosV1` (`id`, `kind`, `target_spawn`, `door_id`, `open_when`);
- no repository-relative filesystem loading was introduced; runtime content uses Unity `Resources` asset references;
- no Level 1 controller, level-ID switch, Stage1 authority, Visible Slice authority, replacement profile, replacement holdings authority, or replacement loadout authority was introduced;
- movement input is bounded and camera binding is explicit rather than searched every frame;
- room presentation and enemy binding continue through the existing revision-aware generic rebuild paths.

## Static validation not performed

A Unity Editor/player executable was not available through the connected GitHub environment. Therefore the following were **not** performed and are not claimed:

- Unity script compilation with zero errors;
- Unity asset import/serialization validation;
- Unity missing-script scan;
- play-mode runtime log inspection.

The manually authored Unity YAML assets require normal Unity import validation before merge.

## Manual acceptance results

The requested in-game acceptance run was **not performed** because no Unity Editor/player session was available.

| Step | Result |
| --- | --- |
| Start from production bootstrap | Not performed |
| Select/create character and enter Hub | Not performed |
| Open Play and Level Selection | Not performed |
| Confirm exactly one level visibly available | Not performed |
| Load authored JSON room | Not performed |
| Confirm exactly one player at authored spawn | Not performed |
| Move in all directions with bounded diagonal movement | Not performed |
| Confirm authored collision | Not performed |
| Confirm camera follows | Not performed |
| Confirm enemies are not duplicated | Not performed |
| Reach authored exit and return once to Hub | Not performed |
| Verify exact equipment-instance bindings remain | Not performed |
| Repeat with another character | Not performed |

## Known limitations

- Unity import, compile and play-mode acceptance remain mandatory before merge.
- Presentation prefabs are intentionally minimal traversal placeholders; final room/player art is outside this task.
- Camera bounds are not implemented.
- The first level's door gates are authored as always open for this traversal-only slice; combat-gated progression is intentionally deferred.
- Keyboard movement is included; gamepad movement is not part of this first slice.

## Adding the next static JSON level

1. Add a new authored manifest and room JSON sidecars using the existing schema and object catalogue.
2. Add a `JsonRoomContentDefinition2D` asset that references those files; place the asset under a Unity runtime-addressable content boundary such as `Resources/ProductionLevels`.
3. Add any new immutable presentation/object registrations needed by the generic room presentation catalogue.
4. Register one new `ProductionPlayableLevelDefinitionV1` entry with a new stable level ID, display metadata, the shared `PlayableLevel.unity` scene path, and the new content reference.
5. Do not add or modify a production gameplay controller for the new level.

The intended workflow is therefore:

`add level JSON -> add content asset/reference -> register immutable level definition -> Level Selection displays it -> shared gameplay composition loads it`
