# Repository Naming Removal Audit

- Types requiring rename: **1794**
- Files carrying warning names or version suffixes: **1148**
- Candidate collision groups: **44**

## Collision groups requiring a deliberate name

### `AcceptedEmission`
- `AcceptedEmissionRuntimeAdapter` — `Assets/ShooterMover/Runtime/Application/Weapons/Execution/AcceptedEmissionRuntimeAdapter.cs`
- existing `AcceptedEmission` — `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponFiringAcceptedSchedule.cs`
- existing `AcceptedEmission` — `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/InventoryWeaponUnityEffectSink2D.cs`

### `AugmentUpgrade`
- `AugmentUpgradeServiceV1` — `Assets/ShooterMover/Runtime/Application/Equipment/Upgrades/AugmentUpgradeConfirmationServiceV1.cs`
- `AugmentUpgradeServiceV1` — `Assets/ShooterMover/Runtime/Application/Equipment/Upgrades/AugmentUpgradeExecutionV1.cs`
- `AugmentUpgradeServiceV1` — `Assets/ShooterMover/Runtime/Application/Equipment/Upgrades/AugmentUpgradePreparationV1.cs`
- `AugmentUpgradeServiceV1` — `Assets/ShooterMover/Runtime/Application/Equipment/Upgrades/AugmentUpgradePreparedRecordV1.cs`
- `AugmentUpgradeServiceV1` — `Assets/ShooterMover/Runtime/Application/Equipment/Upgrades/AugmentUpgradeServiceV1.cs`
- `AugmentUpgradeCanonicalV1` — `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgradeCanonicalV1.cs`

### `BoundsDto`
- `RuntimeBoundsDto` — `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridV2PlayableExporterUtilities.cs`
- `RuntimeBoundsDto` — `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/LevelGridV2CompilerDtos.cs`
- existing `BoundsDto` — `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentJsonDtosV1.cs`

### `CatalogDto`
- `CatalogDtoV1` — `Assets/ShooterMover/Runtime/Application/Enemies/Catalog/EnemyCatalogJsonDtosV1.cs`
- existing `CatalogDto` — `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/WeaponCatalogJsonDtos.cs`

### `CharacterStrongbox`
- `ProductionCharacterStrongboxRuntimeV1` — `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionCharacterStrongboxCompositionV1.cs`
- `ProductionCharacterStrongboxCompositionV1` — `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionCharacterStrongboxCompositionV1.cs`

### `CollectedRunRewardPreparedTransfer`
- `CollectedRunRewardPreparedTransferV1` — `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardPreparedTransfersV1.cs`
- `CollectedRunRewardPreparedTransferAuthorityV1` — `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardPreparedTransfersV1.cs`

### `CollectedRunRewardTransfer`
- `CollectedRunRewardTransferCanonicalV1` — `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferContractsV1.cs`
- `CollectedRunRewardTransferCoordinatorV2` — `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferCoordinatorV1.cs`
- `ProductionCollectedRunRewardTransferServiceV2` — `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/ProductionCollectedRunRewardPersistenceV2.cs`

### `CollectedRunRewardTransferReceipt`
- `CollectedRunRewardTransferReceiptV1` — `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferContractsV1.cs`
- `CollectedRunRewardTransferReceiptAuthorityV1` — `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferReceiptsV1.cs`

### `CollectedRunRewardTransferStatus`
- `CollectedRunRewardTransferAuthorityStatusV1` — `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferPortsV1.cs`
- `CollectedRunRewardTransferStatusV1` — `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferPortsV1.cs`

### `Crafting`
- `CraftingServiceV1` — `Assets/ShooterMover/Runtime/Application/Crafting/CraftingServiceV1.cs`
- `CraftingCanonicalV1` — `Assets/ShooterMover/Runtime/Domain/Crafting/CraftingRecipeV1.cs`

### `CraftingRecipe`
- `CraftingRecipeProjectionV1` — `Assets/ShooterMover/Runtime/Application/Crafting/Presentation/CraftingScreenPresentationV1.cs`
- `CraftingRecipeV1` — `Assets/ShooterMover/Runtime/Domain/Crafting/CraftingRecipeV1.cs`

### `DefinitionDto`
- `DefinitionDtoV1` — `Assets/ShooterMover/Runtime/Application/Enemies/Catalog/EnemyCatalogJsonDtosV1.cs`
- existing `DefinitionDto` — `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/WeaponCatalogJsonDtos.cs`

### `DoorDto`
- `DoorDtoV2` — `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs`
- existing `DoorDto` — `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridV2PlayableExporterUtilities.cs`
- existing `DoorDto` — `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/LevelGridV2CompilerDtos.cs`
- existing `DoorDto` — `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomAccessJsonImporterV1.cs`
- existing `DoorDto` — `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentJsonDtosV1.cs`

### `DoorsDto`
- `DoorsDtoV2` — `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs`
- existing `DoorsDto` — `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridV2PlayableExporterUtilities.cs`
- existing `DoorsDto` — `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/LevelGridV2CompilerDtos.cs`

### `EndpointDto`
- `EndpointDtoV2` — `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs`
- existing `EndpointDto` — `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridV2PlayableExporterUtilities.cs`
- existing `EndpointDto` — `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/LevelGridV2CompilerDtos.cs`

### `EnemyAttackCapabilityRegistration`
- `EnemyAttackCapabilityRegistrationV1` — `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogModelV1.cs`
- `EnemyAttackCapabilityRuntimeRegistrationV1` — `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimePolicyRegistryV1.cs`

### `EnemyDefinition`
- `EnemyDefinitionV1` — `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogModelV1.cs`
- `EnemyDefinitionProjection` — `Assets/ShooterMover/Runtime/GameplayEntities/Enemies/EnemyRuntimeProjection.cs`

### `EquipmentCatalog`
- `ProductionEquipmentCatalogAdapterV1` — `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionPlayerLoadoutRuntimeV1.cs`
- existing `EquipmentCatalog` — `Assets/ShooterMover/Runtime/Domain/Equipment/EquipmentModel.cs`

### `Fixture`
- `CoordinatorFixture` — `Assets/ShooterMover/Tests/EditMode/Combat/FourMountStatusProjectorTests.cs`
- `FixtureAdapter` — `Assets/ShooterMover/Tests/EditMode/TerminalDropBinding/TerminalDropGenerationAuthorityV1Tests.cs`
- existing `Fixture` — `Assets/ShooterMover/Tests/EditMode/Combat/FourMountCombatStepperTests.cs`
- existing `Fixture` — `Assets/ShooterMover/Tests/EditMode/Combat/FourMountStatusProjectorTests.cs`
- existing `Fixture` — `Assets/ShooterMover/Tests/EditMode/Crafting/CraftingServiceV1Tests.cs`
- existing `Fixture` — `Assets/ShooterMover/Tests/EditMode/Crafting/Integration/CraftingInventoryEquipServiceV1Tests.cs`
- existing `Fixture` — `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyPlacementRuntimeAuthorityBoundaryV1Tests.cs`
- existing `Fixture` — `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyPlacementRuntimeLifecycleRoutingV1Tests.cs`
- existing `Fixture` — `Assets/ShooterMover/Tests/EditMode/Equipment/Upgrades/AugmentUpgradeRetryAndIntegrationTests.cs`
- existing `Fixture` — `Assets/ShooterMover/Tests/EditMode/Flow/Hub/ProductionExactWeaponInstanceLoadoutTests.cs`
- existing `Fixture` — `Assets/ShooterMover/Tests/EditMode/Inventory/LoadoutScreen/InventoryLoadoutScreenServiceTests.cs`
- existing `Fixture` — `Assets/ShooterMover/Tests/EditMode/Persistence/Composition/CollectedRunRewardPreparationAndPlanTests.cs`
- existing `Fixture` — `Assets/ShooterMover/Tests/EditMode/PlayerRuntime/PlayerLiveAuthorityTests.cs`
- existing `Fixture` — `Assets/ShooterMover/Tests/EditMode/PlayerRuntime/PlayerRuntimeCompositionTests.cs`
- existing `Fixture` — `Assets/ShooterMover/Tests/EditMode/Rewards/Application/RewardApplicationServiceV1Tests.cs`
- existing `Fixture` — `Assets/ShooterMover/Tests/EditMode/Rewards/Strongboxes/StrongboxOpeningServiceV1Tests.cs`
- existing `Fixture` — `Assets/ShooterMover/Tests/EditMode/RunConditionBinding/RunConditionBindingV1Tests.cs`
- existing `Fixture` — `Assets/ShooterMover/Tests/EditMode/RunPickups/RunLocalPickupAuthorityTestSupport.cs`
- existing `Fixture` — `Assets/ShooterMover/Tests/EditMode/RunSessions/RunSessionDurableEndV1Tests.cs`
- existing `Fixture` — `Assets/ShooterMover/Tests/EditMode/Shops/Presentation/ShopScreenPresentationV1Tests.Fixtures.cs`
- existing `Fixture` — `Assets/ShooterMover/Tests/EditMode/Shops/ShopRuntimeServiceV1Tests.Fixtures.cs`
- existing `Fixture` — `Assets/ShooterMover/Tests/PlayMode/Flow/InventoryLoadout/InventoryLoadoutScreenControllerTests.cs`
- existing `Fixture` — `Assets/ShooterMover/Tests/PlayMode/Flow/Shop/ShopScreenControllerV1Tests.Fixtures.cs`
- existing `Fixture` — `Assets/ShooterMover/Tests/PlayMode/Movement/MovementActorLifecycleTests.cs`
- existing `Fixture` — `Assets/ShooterMover/Tests/PlayMode/RunPickups/LootPickupRunProjectionPlayModeTests.cs`
- existing `Fixture` — `Assets/ShooterMover/Tests/PlayMode/Weapons/Live/InventoryWeaponRuntimePlayModeFakes.cs`

### `GeneratedEquipmentAugmentSignature`
- `GeneratedEquipmentAugmentSignatureAuthorityV1` — `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/GeneratedEquipmentAugmentSignatureAuthorityV1.cs`
- `GeneratedEquipmentAugmentSignatureV1` — `Assets/ShooterMover/Runtime/Domain/Equipment/GeneratedEquipmentAugmentSignatureV1.cs`

### `ICollectedRunEquipmentPayloadSource`
- `ICollectedRunEquipmentPayloadSourceV2` — `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/ProductionCollectedRunRewardPreparationV2.cs`
- existing `ICollectedRunEquipmentPayloadSource` — `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/ProductionCollectedRunRewardRegistryCompatibility.cs`

### `InventoryLoadout`
- `ProductionInventoryLoadoutAuthorityV1` — `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionPlayerLoadoutRuntimeV1.cs`
- `InventoryLoadoutCanonicalV1` — `Assets/ShooterMover/Runtime/Application/Inventory/LoadoutScreen/InventoryLoadoutScreenV1.cs`

### `LevelDto`
- `LevelDtoV2` — `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs`
- existing `LevelDto` — `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridV2PlayableExporterUtilities.cs`
- existing `LevelDto` — `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/LevelGridV2CompilerDtos.cs`

### `LevelIdentityDto`
- `LevelIdentityDtoV2` — `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs`
- existing `LevelIdentityDto` — `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelGridV2RoomFolderMigration.cs`

### `MapDto`
- `MapDtoV2` — `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs`
- existing `MapDto` — `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridV2PlayableExporterUtilities.cs`
- existing `MapDto` — `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/LevelGridV2CompilerDtos.cs`

### `MapNodeDto`
- `MapNodeDtoV2` — `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs`
- existing `MapNodeDto` — `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridV2PlayableExporterUtilities.cs`
- existing `MapNodeDto` — `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/LevelGridV2CompilerDtos.cs`

### `MissionRunResult`
- `MissionRunResultAuthorityV1` — `Assets/ShooterMover/Runtime/Application/Missions/Results/MissionRunResultAuthorityV1.cs`
- `MissionRunAuthorityResultV1` — `Assets/ShooterMover/Runtime/Contracts/Missions/Results/MissionRunPayloadsV1.cs`

### `ObjectiveFact`
- `ObjectiveFactAdapter` — `Assets/ShooterMover/Tests/EditMode/ConditionRuntime/ConditionRuntimeAuthorityV1TestSupport.cs`
- existing `ObjectiveFact` — `Assets/ShooterMover/Tests/EditMode/ConditionRuntime/ConditionRuntimeAuthorityV1TestSupport.cs`

### `RewardApplication`
- `RewardApplicationServiceV1` — `Assets/ShooterMover/Runtime/Application/Rewards/Application/RewardApplicationServiceV1.Persistence.cs`
- `RewardApplicationServiceV1` — `Assets/ShooterMover/Runtime/Application/Rewards/Application/RewardApplicationServiceV1.cs`
- `RewardApplicationCanonicalV1` — `Assets/ShooterMover/Runtime/Domain/Rewards/Application/RewardApplicationModelV1.cs`

### `Room`
- `RoomRuntimeAuthorityV1` — `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomRuntimeAuthorityV1.cs`
- `RoomRuntimeProjectionV1` — `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomOccupancyContractsV1.cs`

### `RoomDto`
- `RoomDtoV2` — `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs`
- existing `RoomDto` — `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridV2PlayableExporterUtilities.cs`
- existing `RoomDto` — `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/LevelGridV2CompilerDtos.cs`

### `RoomIdentityDto`
- `RoomIdentityDtoV2` — `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs`
- existing `RoomIdentityDto` — `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelGridV2RoomFolderMigration.cs`

### `RoomIndexDto`
- `RoomIndexDtoV2` — `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs`
- existing `RoomIndexDto` — `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridV2PlayableExporterUtilities.cs`
- existing `RoomIndexDto` — `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/LevelGridV2CompilerDtos.cs`

### `RoomLive`
- `RoomLiveRuntimeAuthorityV1` — `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveRuntimeAuthorityCoreV1.cs`
- `RoomLiveRuntimeProjectionV1` — `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveRuntimeProjectionsV1.cs`

### `Shop`
- `ShopRuntimeServiceV1` — `Assets/ShooterMover/Runtime/Application/Shops/ShopRuntimeServiceV1.RefreshPersistence.cs`
- `ShopRuntimeServiceV1` — `Assets/ShooterMover/Runtime/Application/Shops/ShopRuntimeServiceV1.State.cs`
- `ShopRuntimeServiceV1` — `Assets/ShooterMover/Runtime/Application/Shops/ShopRuntimeServiceV1.Transactions.cs`
- `ShopRuntimeServiceV1` — `Assets/ShooterMover/Runtime/Application/Shops/ShopRuntimeServiceV1.cs`
- `ShopCanonicalV1` — `Assets/ShooterMover/Runtime/Domain/Shops/ShopDefinitionV1.cs`

### `StatusEffectCommand`
- `StatusEffectCommandV1` — `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectDefinitionsV1.cs`
- `StatusEffectCommandCanonicalV1` — `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectDefinitionsV1.cs`

### `StrongboxOpeningResult`
- `StrongboxOpeningResultRuntimeV1` — `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxOpeningModelsV1.cs`
- `StrongboxOpeningResultV1` — `Assets/ShooterMover/Runtime/Contracts/Rewards/StrongboxOpeningContractsV1.cs`

### `StrongboxOpeningStatus`
- `StrongboxOpeningRuntimeStatusV1` — `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxOpeningModelsV1.cs`
- `StrongboxOpeningStatusV1` — `Assets/ShooterMover/Runtime/Contracts/Rewards/StrongboxOpeningContractsV1.cs`

### `Test`
- `TestProjection` — `Assets/ShooterMover/Tests/EditMode/Contracts/RoomContractTests.cs`
- `TestAuthority` — `Assets/ShooterMover/Tests/EditMode/Persistence/Components/AtomicSaveAndCompensationV1Tests.cs`

### `WeaponCatalogue`
- `ProductionWeaponCatalogueV1` — `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/ProductionWeaponCatalogueV1.Content.cs`
- `ProductionWeaponCatalogueProjectionV1` — `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/ProductionWeaponCatalogueV1.Contracts.cs`
- `ProductionWeaponCatalogueV1` — `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/ProductionWeaponCatalogueV1.EquipmentProjection.cs`
- `ProductionWeaponCatalogueV1` — `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/ProductionWeaponCatalogueV1.FlatProjection.cs`

### `WeaponInventoryState`
- `ProductionWeaponInventoryStateV1` — `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponOnboardingV1.cs`
- `ProductionWeaponInventoryStateV2` — `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponOnboardingV2.cs`

### `WeaponMountBinding`
- `WeaponMountBindingV2` — `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponMountLoadoutV2.cs`
- `ProductionWeaponMountBindingV1` — `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponMountPolicyV1.cs`

### `WeaponMountLoadout`
- `ProductionWeaponMountLoadoutAuthorityV2` — `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponMountLoadoutV2.cs`
- `ProductionWeaponMountLoadoutProjectionV2` — `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponMountLoadoutV2.cs`

### `WeaponOnboarding`
- `ProductionWeaponOnboardingV1` — `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponOnboardingV1.cs`
- `ProductionWeaponOnboardingV2` — `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponOnboardingV2.cs`

## Proposed type renames

| Current | Proposed | Kind | Warning words | Version | File |
|---|---|---|---|---|---|
| `BuiltInCharacterSelectionCatalogV1` | `BuiltInCharacterSelectionCatalog` | class | — | yes | `Assets/ShooterMover/Content/Definitions/Characters/Selection/BuiltInCharacterSelectionCatalogV1.cs` |
| `CraftingWeightedDefinitionAuthoringV1` | `CraftingWeightedDefinitionAuthoring` | class | — | yes | `Assets/ShooterMover/Content/Definitions/Crafting/CraftingRecipeDefinitionAssetV1.cs` |
| `CraftingRecipeAssetBuildResultV1` | `CraftingRecipeAssetBuildResult` | class | — | yes | `Assets/ShooterMover/Content/Definitions/Crafting/CraftingRecipeDefinitionAssetV1.cs` |
| `CraftingRecipeDefinitionAssetV1` | `CraftingRecipeDefinitionAsset` | class | — | yes | `Assets/ShooterMover/Content/Definitions/Crafting/CraftingRecipeDefinitionAssetV1.cs` |
| `BuiltInEnemyCatalogRegistryV1` | `BuiltInEnemyCatalogRegistry` | class | — | yes | `Assets/ShooterMover/Content/Definitions/Enemies/BuiltInEnemyCatalogRegistryV1.cs` |
| `PlayModeDefinitionRecordV1` | `PlayModeDefinitionRecord` | class | — | yes | `Assets/ShooterMover/Content/Definitions/Flow/PlayModes/PlayModeCatalogDefinitionV1.cs` |
| `PlayModeCatalogDefinitionV1` | `PlayModeCatalogDefinition` | class | — | yes | `Assets/ShooterMover/Content/Definitions/Flow/PlayModes/PlayModeCatalogDefinitionV1.cs` |
| `LevelSelectionDefinitionRecordV1` | `LevelSelectionDefinitionRecord` | class | — | yes | `Assets/ShooterMover/Content/Definitions/Levels/Selection/LevelSelectionCatalogDefinitionV1.cs` |
| `ProductionPlayableLevelDefinitionV1` | `PlayableLevelDefinition` | class | Production | yes | `Assets/ShooterMover/Content/Definitions/Levels/Selection/LevelSelectionCatalogDefinitionV1.cs` |
| `ProductionPlayableLevelCatalogV1` | `PlayableLevelCatalog` | class | Production | yes | `Assets/ShooterMover/Content/Definitions/Levels/Selection/LevelSelectionCatalogDefinitionV1.cs` |
| `LevelSelectionCatalogDefinitionV1` | `LevelSelectionCatalogDefinition` | class | — | yes | `Assets/ShooterMover/Content/Definitions/Levels/Selection/LevelSelectionCatalogDefinitionV1.cs` |
| `BuiltInRoomContentObjectCatalogV1` | `BuiltInRoomContentObjectCatalog` | class | — | yes | `Assets/ShooterMover/Content/Definitions/Missions/Rooms/BuiltInRoomContentObjectCatalogV1.cs` |
| `Level1AuthorableRoomDefinitionV1` | `Level1AuthorableRoomDefinition` | class | — | yes | `Assets/ShooterMover/Content/Definitions/Missions/Rooms/Level1AuthorableRoomDefinitionV1.cs` |
| `Level1LiveRoomGraphDefinitionV1` | `Level1LiveRoomGraphDefinition` | class | — | yes | `Assets/ShooterMover/Content/Definitions/Missions/Rooms/Level1LiveRoomGraphDefinitionV1.cs` |
| `Level1RoomGraphDefinitionV1` | `Level1RoomGraphDefinition` | class | — | yes | `Assets/ShooterMover/Content/Definitions/Missions/Rooms/Level1RoomGraphDefinitionV1.cs` |
| `ShopEquipmentCandidateAuthoringV1` | `ShopEquipmentCandidateAuthoring` | class | — | yes | `Assets/ShooterMover/Content/Definitions/Shops/ShopDefinitionAsset.cs` |
| `ShopQualityCandidateAuthoringV1` | `ShopQualityCandidateAuthoring` | class | — | yes | `Assets/ShooterMover/Content/Definitions/Shops/ShopDefinitionAsset.cs` |
| `ShopAugmentCandidateAuthoringV1` | `ShopAugmentCandidateAuthoring` | class | — | yes | `Assets/ShooterMover/Content/Definitions/Shops/ShopDefinitionAsset.cs` |
| `ShopPricingPolicyAuthoringV1` | `ShopPricingPolicyAuthoring` | class | — | yes | `Assets/ShooterMover/Content/Definitions/Shops/ShopDefinitionAsset.cs` |
| `ShopDefinitionAssetBuildResultV1` | `ShopDefinitionAssetBuildResult` | class | — | yes | `Assets/ShooterMover/Content/Definitions/Shops/ShopDefinitionAsset.cs` |
| `StrongboxDefinitionSetV1` | `StrongboxDefinitionSet` | class | — | yes | `Assets/ShooterMover/Content/Definitions/Strongboxes/StrongboxDefinitionSetV1.cs` |
| `DoorConditionComposition` | `DoorCondition` | enum | Composition | no | `Assets/ShooterMover/ContentPackages/Environment/Doors/DoorConditionModel.cs` |
| `DoorRuntimeState` | `DoorState` | enum | Runtime | no | `Assets/ShooterMover/ContentPackages/Environment/Doors/DoorController2D.cs` |
| `DestructiblePropAuthority` | `DestructibleProp` | class | Authority | no | `Assets/ShooterMover/ContentPackages/Props/DestructibleProps/DestructiblePropAuthority.cs` |
| `DestructiblePropTerminalProvenanceV1` | `DestructiblePropTerminalProvenance` | class | — | yes | `Assets/ShooterMover/ContentPackages/Props/DestructibleProps/DestructiblePropTerminalProvenanceV1.cs` |
| `ProjectileExecutionPlanAdapter` | `ProjectileExecutionPlan` | class | Adapter | no | `Assets/ShooterMover/ContentPackages/Weapons/Shared/Runtime/ProjectileExecutionPlanAdapter.cs` |
| `AuthoritativeStrongboxSimulationGatewayFactoryV1` | `AuthoritativeStrongboxSimulationGatewayFactory` | class | — | yes | `Assets/ShooterMover/Editor/BalanceSimulator/AuthoritativeStrongboxSimulationGatewayFactoryV1.cs` |
| `AuthoritativeStrongboxSimulationProductionGatewayV1` | `AuthoritativeStrongboxSimulationGateway` | class | Production | yes | `Assets/ShooterMover/Editor/BalanceSimulator/AuthoritativeStrongboxSimulationProductionGatewayV1.cs` |
| `AuthoritativeStrongboxSimulationRunnerV1` | `AuthoritativeStrongboxSimulationRunner` | class | — | yes | `Assets/ShooterMover/Editor/BalanceSimulator/AuthoritativeStrongboxSimulationRunnerV1.cs` |
| `AuthoritativeStrongboxPreparedOpenV1` | `AuthoritativeStrongboxPreparedOpen` | class | — | yes | `Assets/ShooterMover/Editor/BalanceSimulator/AuthoritativeStrongboxSimulatorRuntimeV1.cs` |
| `AuthoritativeStrongboxSimulatorRuntimeV1` | `AuthoritativeStrongboxSimulator` | class | Runtime | yes | `Assets/ShooterMover/Editor/BalanceSimulator/AuthoritativeStrongboxSimulatorRuntimeV1.cs` |
| `BalanceSimulationModeV1` | `BalanceSimulationMode` | enum | — | yes | `Assets/ShooterMover/Editor/BalanceSimulator/BalanceSimulationModelsV1.cs` |
| `BalanceSimulationRequestV1` | `BalanceSimulationRequest` | class | — | yes | `Assets/ShooterMover/Editor/BalanceSimulator/BalanceSimulationModelsV1.cs` |
| `BalanceSimulationIterationRequestV1` | `BalanceSimulationIterationRequest` | class | — | yes | `Assets/ShooterMover/Editor/BalanceSimulator/BalanceSimulationModelsV1.cs` |
| `BalanceRewardObservationV1` | `BalanceRewardObservation` | class | — | yes | `Assets/ShooterMover/Editor/BalanceSimulator/BalanceSimulationModelsV1.cs` |
| `BalanceEquipmentObservationV1` | `BalanceEquipmentObservation` | class | — | yes | `Assets/ShooterMover/Editor/BalanceSimulator/BalanceSimulationModelsV1.cs` |
| `BalanceRejectionV1` | `BalanceRejection` | class | — | yes | `Assets/ShooterMover/Editor/BalanceSimulator/BalanceSimulationModelsV1.cs` |
| `BalanceSimulationIterationResultV1` | `BalanceSimulationIterationResult` | class | — | yes | `Assets/ShooterMover/Editor/BalanceSimulator/BalanceSimulationModelsV1.cs` |
| `BalanceCountV1` | `BalanceCount` | class | — | yes | `Assets/ShooterMover/Editor/BalanceSimulator/BalanceSimulationModelsV1.cs` |
| `BalanceSimulationReportV1` | `BalanceSimulationReport` | class | — | yes | `Assets/ShooterMover/Editor/BalanceSimulator/BalanceSimulationModelsV1.cs` |
| `IBalanceSimulationRuntimeV1` | `IBalanceSimulation` | interface | Runtime | yes | `Assets/ShooterMover/Editor/BalanceSimulator/BalanceSimulationServiceV1.cs` |
| `BalanceSimulationServiceV1` | `BalanceSimulation` | class | Service | yes | `Assets/ShooterMover/Editor/BalanceSimulator/BalanceSimulationServiceV1.cs` |
| `DropSourceSimulationRequestV1` | `DropSourceSimulationRequest` | class | — | yes | `Assets/ShooterMover/Editor/BalanceSimulator/DropSourceSimulationRequestV1.cs` |
| `DropSourceSimulationRuntimeV1` | `DropSourceSimulation` | class | Runtime | yes | `Assets/ShooterMover/Editor/BalanceSimulator/DropSourceSimulationRuntimeV1.cs` |
| `LootboxGeneratedItemV1` | `LootboxGeneratedItem` | class | — | yes | `Assets/ShooterMover/Editor/BalanceSimulator/LootboxSimulatorRuntimeV1.cs` |
| `LootboxOddsEntryV1` | `LootboxOddsEntry` | class | — | yes | `Assets/ShooterMover/Editor/BalanceSimulator/LootboxSimulatorRuntimeV1.cs` |
| `LootboxOddsReportV1` | `LootboxOddsReport` | class | — | yes | `Assets/ShooterMover/Editor/BalanceSimulator/LootboxSimulatorRuntimeV1.cs` |
| `LootboxSimulatorRuntimeV1` | `LootboxSimulator` | class | Runtime | yes | `Assets/ShooterMover/Editor/BalanceSimulator/LootboxSimulatorRuntimeV1.cs` |
| `MultiplayerDropSimulationRuntimeV1` | `MultiplayerDropSimulation` | class | Runtime | yes | `Assets/ShooterMover/Editor/BalanceSimulator/MultiplayerDropSimulationRuntimeV1.cs` |
| `RewardSimulationParticipantInputV1` | `RewardSimulationParticipantInput` | class | — | yes | `Assets/ShooterMover/Editor/BalanceSimulator/RewardSimulationParticipantInputV1.cs` |
| `RewardSimulationParticipantReportV1` | `RewardSimulationParticipantReport` | class | — | yes | `Assets/ShooterMover/Editor/BalanceSimulator/RewardSimulationParticipantReportV1.cs` |
| `RewardSimulationReportV1` | `RewardSimulationReport` | class | — | yes | `Assets/ShooterMover/Editor/BalanceSimulator/RewardSimulationReportV1.cs` |
| `RuntimeBalanceScenarioV1` | `BalanceScenario` | class | Runtime | yes | `Assets/ShooterMover/Editor/BalanceSimulator/RuntimeBalanceScenarioV1.cs` |
| `StrongboxLevelQueueEntryV1` | `StrongboxLevelQueueEntry` | class | — | yes | `Assets/ShooterMover/Editor/BalanceSimulator/StrongboxLevelComparisonWindow.cs` |
| `StrongboxLevelComparisonResultV1` | `StrongboxLevelComparisonResult` | class | — | yes | `Assets/ShooterMover/Editor/BalanceSimulator/StrongboxLevelComparisonWindow.cs` |
| `WeaponLootCardEditorDrawerV1` | `WeaponLootCardEditorDrawer` | class | — | yes | `Assets/ShooterMover/Editor/BalanceSimulator/WeaponLootCardEditorDrawerV1.cs` |
| `WeaponLootCardProjectionV1` | `WeaponLootCard` | class | Projection | yes | `Assets/ShooterMover/Editor/BalanceSimulator/WeaponLootCardProjectionV1.cs` |
| `EnemyReadinessWindowV1` | `EnemyReadinessWindow` | class | — | yes | `Assets/ShooterMover/Editor/EnemyReadiness/EnemyReadinessWindowV1.cs` |
| `ReadinessRowV1` | `ReadinessRow` | class | — | yes | `Assets/ShooterMover/Editor/EnemyReadiness/EnemyReadinessWindowV1.cs` |
| `LevelGridProblemsWindowV2` | `LevelGridProblemsWindow` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2Editor.cs` |
| `LevelIdentityDtoV2` | `LevelIdentityDto` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs` |
| `LevelDtoV2` | `LevelDto` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs` |
| `RoomIndexDtoV2` | `RoomIndexDto` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs` |
| `MapDtoV2` | `MapDto` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs` |
| `MapNodeDtoV2` | `MapNodeDto` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs` |
| `MapConnectionDtoV2` | `MapConnectionDto` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs` |
| `EndpointDtoV2` | `EndpointDto` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs` |
| `RoomIdentityDtoV2` | `RoomIdentityDto` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs` |
| `RoomDtoV2` | `RoomDto` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs` |
| `DoorsDtoV2` | `DoorsDto` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs` |
| `DoorDtoV2` | `DoorDto` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs` |
| `FloorScaffoldDtoV2` | `FloorScaffoldDto` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs` |
| `EnemiesScaffoldDtoV2` | `EnemiesScaffoldDto` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs` |
| `PropsScaffoldDtoV2` | `PropsScaffoldDto` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs` |
| `DecorScaffoldDtoV2` | `DecorScaffoldDto` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs` |
| `EncounterScaffoldDtoV2` | `EncounterScaffoldDto` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs` |
| `LevelGridDoorOperationsV2` | `LevelGridDoorOperations` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridDoorOperationsV2.cs` |
| `LevelGridEditorOperationsV2` | `LevelGridEditorOperations` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridEditorOperationsV2.cs` |
| `LevelGridEditorRoomProjectionV2` | `LevelGridEditorRoom` | class | Projection | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridEditorProjectionV2.cs` |
| `LevelGridEditorProjectionV2` | `LevelGridEditor` | class | Projection | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridEditorProjectionV2.cs` |
| `LevelGridEditorProblemLocatorV2` | `LevelGridEditorProblemLocator` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridEditorProjectionV2.cs` |
| `LevelGridEditorWindowV2` | `LevelGridEditorWindow` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridEditorWindowV2.Canvas.cs` |
| `LevelGridEditorWindowV2` | `LevelGridEditorWindow` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridEditorWindowV2.EntryPoints.cs` |
| `LevelGridEditorWindowV2` | `LevelGridEditorWindow` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridEditorWindowV2.Panels.cs` |
| `LevelGridEditorWindowV2` | `LevelGridEditorWindow` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridEditorWindowV2.Playable.cs` |
| `LevelGridEditorWindowV2` | `LevelGridEditorWindow` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridEditorWindowV2.State.cs` |
| `LevelGridEditorWindowV2` | `LevelGridEditorWindow` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridEditorWindowV2.cs` |
| `LevelGridLegacySurfaceGuardsV2` | `LevelGridLegacySurfaceGuards` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridLegacySurfaceGuardsV2.cs` |
| `LevelGridPlayableAssetChangeWatcherV2` | `LevelGridPlayableAssetChangeWatcher` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableAssetChangeWatcherV2.cs` |
| `LevelGridEditorWindowV2` | `LevelGridEditorWindow` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableAssetChangeWatcherV2.cs` |
| `LevelGridPlayableBuildResultV2` | `LevelGridPlayableBuildResult` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableBuildFacadeV2.cs` |
| `LevelGridPlayableBuildFacadeV2` | `LevelGridPlayableBuildFacade` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableBuildFacadeV2.cs` |
| `LevelGridPlayableBuildPathsV2` | `LevelGridPlayableBuildPaths` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableBuildPathsV2.cs` |
| `LevelGridPlayableMetadataOperationsV2` | `LevelGridPlayableMetadataOperations` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableMetadataOperationsV2.cs` |
| `LevelGridPlayableProvenanceRecordV2` | `LevelGridPlayableProvenanceRecord` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableProvenanceV2.cs` |
| `LevelGridPlayableProvenanceV2` | `LevelGridPlayableProvenance` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableProvenanceV2.cs` |
| `LevelGridPlayableStatusKindV2` | `LevelGridPlayableStatusKind` | enum | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableStatusV2.cs` |
| `LevelGridPlayableStatusV2` | `LevelGridPlayableStatus` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableStatusV2.cs` |
| `LevelGridPlayableStatusEvaluatorV2` | `LevelGridPlayableStatusEvaluator` | class | — | yes | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableStatusV2.cs` |
| `RuntimeBoundsDto` | `BoundsDto` | class | Runtime | no | `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridV2PlayableExporterUtilities.cs` |
| `IEnemyAttackPatternLineOfSightV1` | `IEnemyAttackPatternLineOfSight` | interface | — | yes | `Assets/ShooterMover/Production/EnemyAttackPatterns/EnemyAttackPatternProductionController2D.cs` |
| `PhysicsEnemyAttackPatternLineOfSightV1` | `PhysicsEnemyAttackPatternLineOfSight` | class | — | yes | `Assets/ShooterMover/Production/EnemyAttackPatterns/EnemyAttackPatternProductionController2D.cs` |
| `EnemyAttackPatternProductionController2D` | `EnemyAttackPatternController2D` | class | Production | no | `Assets/ShooterMover/Production/EnemyAttackPatterns/EnemyAttackPatternProductionController2D.cs` |
| `IEnemyAttackPatternProjectilePrefabResolverV1` | `IEnemyAttackPatternProjectilePrefabResolver` | interface | — | yes | `Assets/ShooterMover/Production/EnemyAttackPatterns/EnemyAttackPatternUnityBindingsV1.cs` |
| `EnemyAttackPatternProjectilePrefabRegistryV1` | `EnemyAttackPatternProjectilePrefabRegistry` | class | — | yes | `Assets/ShooterMover/Production/EnemyAttackPatterns/EnemyAttackPatternUnityBindingsV1.cs` |
| `EnemyAttackPatternTargetBindingV1` | `EnemyAttackPatternTargetBinding` | class | — | yes | `Assets/ShooterMover/Production/EnemyAttackPatterns/EnemyAttackPatternUnityBindingsV1.cs` |
| `IEnemyAttackPatternPounceMotionV1` | `IEnemyAttackPatternPounceMotion` | interface | — | yes | `Assets/ShooterMover/Production/EnemyAttackPatterns/EnemyAttackPatternUnityBindingsV1.cs` |
| `EnemyAttackPatternUnitySourceBindingV1` | `EnemyAttackPatternUnitySourceBinding` | class | — | yes | `Assets/ShooterMover/Production/EnemyAttackPatterns/EnemyAttackPatternUnityBindingsV1.cs` |
| `EnemyAttackPatternUnitySourceRegistryV1` | `EnemyAttackPatternUnitySourceRegistry` | class | — | yes | `Assets/ShooterMover/Production/EnemyAttackPatterns/EnemyAttackPatternUnityBindingsV1.cs` |
| `EnemyAttackPatternUnityEmissionRealizerV1` | `EnemyAttackPatternUnityEmissionRealizer` | class | — | yes | `Assets/ShooterMover/Production/EnemyAttackPatterns/EnemyAttackPatternUnityEmissionRealizerV1.cs` |
| `CharacterSelectionOperationStatusV1` | `CharacterSelectionOperationStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Characters/Selection/CharacterSelectionServiceV1.cs` |
| `CharacterSelectionRouteStatusV1` | `CharacterSelectionRouteStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Characters/Selection/CharacterSelectionServiceV1.cs` |
| `CharacterSelectionOperationResultV1` | `CharacterSelectionOperationResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Characters/Selection/CharacterSelectionServiceV1.cs` |
| `CharacterSelectionSnapshotV1` | `CharacterSelectionSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Characters/Selection/CharacterSelectionServiceV1.cs` |
| `CharacterSelectionRouteResultV1` | `CharacterSelectionRouteResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Characters/Selection/CharacterSelectionServiceV1.cs` |
| `ICharacterSelectionRouteSinkV1` | `ICharacterSelectionRouteSink` | interface | — | yes | `Assets/ShooterMover/Runtime/Application/Characters/Selection/CharacterSelectionServiceV1.cs` |
| `CharacterSelectionServiceV1` | `CharacterSelection` | class | Service | yes | `Assets/ShooterMover/Runtime/Application/Characters/Selection/CharacterSelectionServiceV1.cs` |
| `CraftingResultStatusV1` | `CraftingResultStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Crafting/CraftingServiceV1.cs` |
| `CraftEquipmentCommandV1` | `CraftEquipmentCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Crafting/CraftingServiceV1.cs` |
| `CraftingResultV1` | `CraftingResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Crafting/CraftingServiceV1.cs` |
| `CraftingServiceV1` | `Crafting` | class | Service | yes | `Assets/ShooterMover/Runtime/Application/Crafting/CraftingServiceV1.cs` |
| `CraftingScrapSpendRewardChildAuthorityV1` | `CraftingScrapSpendRewardChild` | class | Authority | yes | `Assets/ShooterMover/Runtime/Application/Crafting/CraftingServiceV1.cs` |
| `CraftingUnusedMoneyRewardChildAuthorityV1` | `CraftingUnusedMoneyRewardChild` | class | Authority | yes | `Assets/ShooterMover/Runtime/Application/Crafting/CraftingServiceV1.cs` |
| `CraftingRewardApplicationFactoryV1` | `CraftingRewardApplicationFactory` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Crafting/CraftingServiceV1.cs` |
| `CraftedEquipmentEquipStatusV1` | `CraftedEquipmentEquipStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Crafting/Integration/CraftingInventoryEquipServiceV1.cs` |
| `CraftedEquipmentEquipCommandV1` | `CraftedEquipmentEquipCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Crafting/Integration/CraftingInventoryEquipServiceV1.cs` |
| `CraftedEquipmentEquipResultV1` | `CraftedEquipmentEquipResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Crafting/Integration/CraftingInventoryEquipServiceV1.cs` |
| `ICraftedEquipmentLoadoutPortV1` | `ICraftedEquipmentLoadoutPort` | interface | — | yes | `Assets/ShooterMover/Runtime/Application/Crafting/Integration/CraftingInventoryEquipServiceV1.cs` |
| `CraftAndEquipCommandV1` | `CraftAndEquipCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Crafting/Integration/CraftingInventoryEquipServiceV1.cs` |
| `CraftingInventoryEquipStatusV1` | `CraftingInventoryEquipStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Crafting/Integration/CraftingInventoryEquipServiceV1.cs` |
| `CraftingInventoryEquipResultV1` | `CraftingInventoryEquipResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Crafting/Integration/CraftingInventoryEquipServiceV1.cs` |
| `CraftingIntegrationIdentityV1` | `CraftingIntegrationIdentity` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Crafting/Integration/CraftingInventoryEquipServiceV1.cs` |
| `CraftingInventoryEquipServiceV1` | `CraftingInventoryEquip` | class | Service | yes | `Assets/ShooterMover/Runtime/Application/Crafting/Integration/CraftingInventoryEquipServiceV1.cs` |
| `CraftingRecipeAvailabilityV1` | `CraftingRecipeAvailability` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Crafting/Presentation/CraftingScreenPresentationV1.cs` |
| `CraftingScreenStatusV1` | `CraftingScreenStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Crafting/Presentation/CraftingScreenPresentationV1.cs` |
| `CraftingPresentationAuthoritySnapshotV1` | `CraftingPresentationSnapshot` | class | Authority | yes | `Assets/ShooterMover/Runtime/Application/Crafting/Presentation/CraftingScreenPresentationV1.cs` |
| `CraftingPresentationAuthorityResultV1` | `CraftingPresentationResult` | class | Authority | yes | `Assets/ShooterMover/Runtime/Application/Crafting/Presentation/CraftingScreenPresentationV1.cs` |
| `ICraftingPresentationAuthorityPortV1` | `ICraftingPresentationPort` | interface | Authority | yes | `Assets/ShooterMover/Runtime/Application/Crafting/Presentation/CraftingScreenPresentationV1.cs` |
| `CraftingServicePresentationAuthorityPortV1` | `CraftingPresentationPort` | class | Service, Authority | yes | `Assets/ShooterMover/Runtime/Application/Crafting/Presentation/CraftingScreenPresentationV1.cs` |
| `CraftingRecipeProjectionV1` | `CraftingRecipe` | class | Projection | yes | `Assets/ShooterMover/Runtime/Application/Crafting/Presentation/CraftingScreenPresentationV1.cs` |
| `CraftingScreenSnapshotV1` | `CraftingScreenSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Crafting/Presentation/CraftingScreenPresentationV1.cs` |
| `CraftingScreenResultV1` | `CraftingScreenResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Crafting/Presentation/CraftingScreenPresentationV1.cs` |
| `CraftingScreenServiceV1` | `CraftingScreen` | class | Service | yes | `Assets/ShooterMover/Runtime/Application/Crafting/Presentation/CraftingScreenPresentationV1.cs` |
| `RunDebugBuildGuardV1` | `RunDebugBuildGuard` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Development/RunDebug/RunDebugContractsV1.cs` |
| `RunDebugSpawnBatchStatusV1` | `RunDebugSpawnBatchStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Development/RunDebug/RunDebugContractsV1.cs` |
| `RunDebugSpawnRequestV1` | `RunDebugSpawnRequest` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Development/RunDebug/RunDebugContractsV1.cs` |
| `RunDebugBoxPlanV1` | `RunDebugBoxPlan` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Development/RunDebug/RunDebugContractsV1.cs` |
| `RunDebugPlannerV1` | `RunDebugPlanner` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Development/RunDebug/RunDebugContractsV1.cs` |
| `RunDebugBoxFactV1` | `RunDebugBoxFact` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Development/RunDebug/RunDebugContractsV1.cs` |
| `RunDebugSnapshotV1` | `RunDebugSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Development/RunDebug/RunDebugContractsV1.cs` |
| `RunDebugSpawnBatchResultV1` | `RunDebugSpawnBatchResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Development/RunDebug/RunDebugContractsV1.cs` |
| `RunDebugEndResultV1` | `RunDebugEndResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Development/RunDebug/RunDebugContractsV1.cs` |
| `IRunDebugRuntimePortV1` | `IRunDebugPort` | interface | Runtime | yes | `Assets/ShooterMover/Runtime/Application/Development/RunDebug/RunDebugContractsV1.cs` |
| `RunDebugPanelSessionV1` | `RunDebugPanelSession` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Development/RunDebug/RunDebugPanelSessionV1.cs` |
| `MoneyWalletService` | `MoneyWallet` | class | Service | no | `Assets/ShooterMover/Runtime/Application/Economy/Money/MoneyWalletPersistence.cs` |
| `MoneyWalletService` | `MoneyWallet` | class | Service | no | `Assets/ShooterMover/Runtime/Application/Economy/Money/MoneyWalletService.cs` |
| `ScrapChangeFactV1` | `ScrapChangeFact` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Economy/Scrap/ScrapWalletServiceV1.cs` |
| `ScrapTransactionResultV1` | `ScrapTransactionResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Economy/Scrap/ScrapWalletServiceV1.cs` |
| `ScrapSnapshotImportResultV1` | `ScrapSnapshotImportResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Economy/Scrap/ScrapWalletServiceV1.cs` |
| `ScrapWalletServiceV1` | `ScrapWallet` | class | Service | yes | `Assets/ShooterMover/Runtime/Application/Economy/Scrap/ScrapWalletServiceV1.cs` |
| `EnemyCatalogImportResultV1` | `EnemyCatalogImportResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Enemies/Catalog/EnemyCatalogImportResultV1.cs` |
| `EnemyCatalogJsonImporterV1` | `EnemyCatalogJsonImporter` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Enemies/Catalog/EnemyCatalogJsonDtosV1.cs` |
| `EnemyCatalogMappingExceptionV1` | `EnemyCatalogMappingException` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Enemies/Catalog/EnemyCatalogJsonDtosV1.cs` |
| `CatalogDtoV1` | `CatalogDto` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Enemies/Catalog/EnemyCatalogJsonDtosV1.cs` |
| `DefinitionDtoV1` | `DefinitionDto` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Enemies/Catalog/EnemyCatalogJsonDtosV1.cs` |
| `LevelScalingDtoV1` | `LevelScalingDto` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Enemies/Catalog/EnemyCatalogJsonDtosV1.cs` |
| `PerceptionDtoV1` | `PerceptionDto` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Enemies/Catalog/EnemyCatalogJsonDtosV1.cs` |
| `AttackDtoV1` | `AttackDto` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Enemies/Catalog/EnemyCatalogJsonDtosV1.cs` |
| `ShootingPatternDtoV1` | `ShootingPatternDto` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Enemies/Catalog/EnemyCatalogJsonDtosV1.cs` |
| `ProjectilePayloadDtoV1` | `ProjectilePayloadDto` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Enemies/Catalog/EnemyCatalogJsonDtosV1.cs` |
| `MeleePatternDtoV1` | `MeleePatternDto` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Enemies/Catalog/EnemyCatalogJsonDtosV1.cs` |
| `ProjectileDtoV1` | `ProjectileDto` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Enemies/Catalog/EnemyCatalogJsonDtosV1.cs` |
| `AreaDtoV1` | `AreaDto` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Enemies/Catalog/EnemyCatalogJsonDtosV1.cs` |
| `MeleeDtoV1` | `MeleeDto` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Enemies/Catalog/EnemyCatalogJsonDtosV1.cs` |
| `EnemyCatalogJsonImporterV1` | `EnemyCatalogJsonImporter` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Enemies/Catalog/EnemyCatalogJsonImporterV1.cs` |
| `AugmentUpgradeServiceV1` | `AugmentUpgrade` | class | Service | yes | `Assets/ShooterMover/Runtime/Application/Equipment/Upgrades/AugmentUpgradeConfirmationServiceV1.cs` |
| `AugmentUpgradeServiceV1` | `AugmentUpgrade` | class | Service | yes | `Assets/ShooterMover/Runtime/Application/Equipment/Upgrades/AugmentUpgradeExecutionV1.cs` |
| `AugmentUpgradeServiceV1` | `AugmentUpgrade` | class | Service | yes | `Assets/ShooterMover/Runtime/Application/Equipment/Upgrades/AugmentUpgradePreparationV1.cs` |
| `AugmentUpgradeServiceV1` | `AugmentUpgrade` | class | Service | yes | `Assets/ShooterMover/Runtime/Application/Equipment/Upgrades/AugmentUpgradePreparedRecordV1.cs` |
| `AugmentUpgradeServiceV1` | `AugmentUpgrade` | class | Service | yes | `Assets/ShooterMover/Runtime/Application/Equipment/Upgrades/AugmentUpgradeServiceV1.cs` |
| `HubNavigationStatusV1` | `HubNavigationStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/Hub/HubNavigationServiceV1.cs` |
| `HubRouteRecordV1` | `HubRouteRecord` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/Hub/HubNavigationServiceV1.cs` |
| `HubNavigationSnapshotV1` | `HubNavigationSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/Hub/HubNavigationServiceV1.cs` |
| `HubNavigationResultV1` | `HubNavigationResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/Hub/HubNavigationServiceV1.cs` |
| `IHubRouteDestinationAdapterV1` | `IHubRouteDestination` | interface | Adapter | yes | `Assets/ShooterMover/Runtime/Application/Flow/Hub/HubNavigationServiceV1.cs` |
| `IHubRouteTransactionPortV1` | `IHubRouteTransactionPort` | interface | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/Hub/HubNavigationServiceV1.cs` |
| `HubNavigationServiceV1` | `HubNavigation` | class | Service | yes | `Assets/ShooterMover/Runtime/Application/Flow/Hub/HubNavigationServiceV1.cs` |
| `ILevelSelectionRouteAdapterV1` | `ILevelSelectionRoute` | interface | Adapter | yes | `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/ILevelSelectionRouteAdapterV1.cs` |
| `LevelRecommendationV1` | `LevelRecommendation` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelRecommendationV1.cs` |
| `LevelSelectionCatalogV1` | `LevelSelectionCatalog` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelSelectionCatalogV1.cs` |
| `LevelSelectionDefinitionV1` | `LevelSelectionDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelSelectionDefinitionV1.cs` |
| `LevelAvailabilityV1` | `LevelAvailability` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelSelectionEnumsV1.cs` |
| `LevelReleaseStateV1` | `LevelReleaseState` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelSelectionEnumsV1.cs` |
| `LevelRouteKindV1` | `LevelRouteKind` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelSelectionEnumsV1.cs` |
| `LevelSelectionRouteV1` | `LevelSelectionRoute` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelSelectionEnumsV1.cs` |
| `LevelSelectionStatusV1` | `LevelSelectionStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelSelectionEnumsV1.cs` |
| `LevelSelectionResultV1` | `LevelSelectionResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelSelectionResultV1.cs` |
| `LevelSelectionServiceV1` | `LevelSelection` | class | Service | yes | `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelSelectionServiceV1.cs` |
| `PlayModeAvailabilityV1` | `PlayModeAvailability` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/PlaySelection/PlaySelectionServiceV1.cs` |
| `PlayModeDestinationV1` | `PlayModeDestination` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/PlaySelection/PlaySelectionServiceV1.cs` |
| `PlaySelectionRouteV1` | `PlaySelectionRoute` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/PlaySelection/PlaySelectionServiceV1.cs` |
| `PlaySelectionStatusV1` | `PlaySelectionStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/PlaySelection/PlaySelectionServiceV1.cs` |
| `PlayModeDefinitionV1` | `PlayModeDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/PlaySelection/PlaySelectionServiceV1.cs` |
| `PlayModeCatalogV1` | `PlayModeCatalog` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/PlaySelection/PlaySelectionServiceV1.cs` |
| `PlaySelectionResultV1` | `PlaySelectionResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/PlaySelection/PlaySelectionServiceV1.cs` |
| `IPlaySelectionRouteAdapterV1` | `IPlaySelectionRoute` | interface | Adapter | yes | `Assets/ShooterMover/Runtime/Application/Flow/PlaySelection/PlaySelectionServiceV1.cs` |
| `PlaySelectionServiceV1` | `PlaySelection` | class | Service | yes | `Assets/ShooterMover/Runtime/Application/Flow/PlaySelection/PlaySelectionServiceV1.cs` |
| `CanonicalFirstPlayerHoldingsAuthorityV2` | `FirstPlayerHoldings` | class | Canonical, Authority | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/CanonicalFirstPlayerHoldingsAuthorityV2.cs` |
| `CanonicalWeaponInventoryCardV2` | `WeaponInventoryCard` | class | Canonical | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/CanonicalWeaponInventoryScreenV2.cs` |
| `CanonicalWeaponInventoryMountV2` | `WeaponInventoryMount` | class | Canonical | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/CanonicalWeaponInventoryScreenV2.cs` |
| `CanonicalWeaponInventorySnapshotV2` | `WeaponInventorySnapshot` | class | Canonical | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/CanonicalWeaponInventoryScreenV2.cs` |
| `CanonicalWeaponInventoryScreenServiceV2` | `WeaponInventoryScreen` | class | Canonical, Service | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/CanonicalWeaponInventoryScreenV2.cs` |
| `ProductionCharacterAuthorityAdaptersV1` | `CharacterAdapters` | class | Production, Authority | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionCharacterAuthorityAdaptersV1.cs` |
| `ProductionCharacterRuntimeGraphV1` | `CharacterGraph` | class | Production, Runtime | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionCharacterRuntimeGraphV1.cs` |
| `ProductionCharacterRuntimeGraphFactoryV1` | `CharacterGraphFactory` | class | Production, Runtime | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionCharacterRuntimeGraphV1.cs` |
| `IProductionCharacterStrongboxBridgeV1` | `ICharacterStrongboxBridge` | interface | Production | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionCharacterStrongboxBridgeV1.cs` |
| `ProductionCharacterStrongboxBridgeRegistryV1` | `CharacterStrongboxBridgeRegistry` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionCharacterStrongboxBridgeV1.cs` |
| `ProductionCharacterStrongboxRuntimeV1` | `CharacterStrongbox` | class | Production, Runtime | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionCharacterStrongboxCompositionV1.cs` |
| `ProductionCharacterStrongboxCompositionV1` | `CharacterStrongbox` | class | Production, Composition | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionCharacterStrongboxCompositionV1.cs` |
| `ProductionFlowScenePathsV1` | `FlowScenePaths` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionFlowSessionV1.cs` |
| `ProductionFlowProfileRecordV1` | `FlowProfileRecord` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionFlowSessionV1.cs` |
| `IProductionFlowProfileStoreV1` | `IFlowProfileStore` | interface | Production | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionFlowSessionV1.cs` |
| `InMemoryProductionFlowProfileStoreV1` | `InMemoryFlowProfileStore` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionFlowSessionV1.cs` |
| `IProductionSceneLoadPortV1` | `ISceneLoadPort` | interface | Production | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionFlowSessionV1.cs` |
| `ProductionSceneTransitionCoordinatorV1` | `SceneTransition` | class | Production, Coordinator | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionFlowSessionV1.cs` |
| `ProductionStrongboxOpeningBindingV1` | `StrongboxOpeningBinding` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionFlowSessionV1.cs` |
| `ProductionResultsContextV1` | `ResultsContext` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionFlowSessionV1.cs` |
| `ProductionPlayerLoadoutRuntimeV1` | `PlayerLoadout` | class | Production, Runtime | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionPlayerLoadoutRuntimeV1.cs` |
| `ProductionEquipmentCatalogAdapterV1` | `EquipmentCatalog` | class | Production, Adapter | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionPlayerLoadoutRuntimeV1.cs` |
| `ProductionInventoryLoadoutImportResultV1` | `InventoryLoadoutImportResult` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionPlayerLoadoutRuntimeV1.cs` |
| `ProductionInventoryLoadoutAuthorityV1` | `InventoryLoadout` | class | Production, Authority | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionPlayerLoadoutRuntimeV1.cs` |
| `ProductionWeaponCatalogProvider` | `WeaponCatalogProvider` | class | Production | no | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponCatalogProvider.cs` |
| `WeaponHoldingsSnapshotV2` | `WeaponHoldingsSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponHoldingsV2.cs` |
| `WeaponHoldingsImportResultV2` | `WeaponHoldingsImportResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponHoldingsV2.cs` |
| `ProductionWeaponHoldingsAuthorityV2` | `WeaponHoldings` | class | Production, Authority | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponHoldingsV2.cs` |
| `ProductionWeaponHoldingsMigrationV2` | `WeaponHoldingsMigration` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponHoldingsV2.cs` |
| `WeaponHoldingsComponentCodecV2` | `WeaponHoldingsComponentCodec` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponHoldingsV2.cs` |
| `WeaponHoldingsSaveComponentV2` | `WeaponHoldingsSaveComponent` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponHoldingsV2.cs` |
| `CanonicalWeaponInstanceLookupV2` | `WeaponInstanceLookup` | class | Canonical | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponHoldingsV2.cs` |
| `ProductionWeaponMountLoadoutRegistryV2` | `WeaponMountLoadoutRegistry` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponMountLoadoutRegistryV2.cs` |
| `WeaponMountBindingV2` | `WeaponMountBinding` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponMountLoadoutV2.cs` |
| `WeaponMountLoadoutSnapshotV2` | `WeaponMountLoadoutSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponMountLoadoutV2.cs` |
| `WeaponMountLoadoutImportResultV2` | `WeaponMountLoadoutImportResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponMountLoadoutV2.cs` |
| `ProductionWeaponMountLoadoutAuthorityV2` | `WeaponMountLoadout` | class | Production, Authority | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponMountLoadoutV2.cs` |
| `ProductionWeaponMountLoadoutProjectionV2` | `WeaponMountLoadout` | class | Production, Projection | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponMountLoadoutV2.cs` |
| `WeaponMountLoadoutComponentCodecV2` | `WeaponMountLoadoutComponentCodec` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponMountLoadoutV2.cs` |
| `WeaponMountLoadoutSaveComponentV2` | `WeaponMountLoadoutSaveComponent` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponMountLoadoutV2.cs` |
| `ProductionWeaponMountAvailabilityV1` | `WeaponMountAvailability` | enum | Production | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponMountPolicyV1.cs` |
| `ProductionWeaponMountPositionV1` | `WeaponMountPosition` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponMountPolicyV1.cs` |
| `ProductionWeaponMountBindingV1` | `WeaponMountBinding` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponMountPolicyV1.cs` |
| `ProductionWeaponMountLayoutV1` | `WeaponMountLayout` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponMountPolicyV1.cs` |
| `ProductionWeaponMountSetV1` | `WeaponMountSet` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponMountPolicyV1.cs` |
| `ProductionWeaponMountPolicyV1` | `WeaponMountPolicy` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponMountPolicyV1.cs` |
| `ProductionWeaponInventoryStateV1` | `WeaponInventoryState` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponOnboardingV1.cs` |
| `ProductionWeaponOnboardingV1` | `WeaponOnboarding` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponOnboardingV1.cs` |
| `ProductionWeaponInventoryStateV2` | `WeaponInventoryState` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponOnboardingV2.cs` |
| `ProductionWeaponOnboardingV2` | `WeaponOnboarding` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponOnboardingV2.cs` |
| `RequiredCharacterComponentBackfillResultV1` | `RequiredCharacterComponentBackfillResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/RequiredCharacterComponentBackfillV1.cs` |
| `RequiredCharacterComponentBackfillV1` | `RequiredCharacterComponentBackfill` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/RequiredCharacterComponentBackfillV1.cs` |
| `RetiredWeaponSaveMigrationResultV1` | `RetiredWeaponSaveMigrationResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/RetiredWeaponSaveMigrationV1.cs` |
| `RetiredWeaponSaveMigrationV1` | `RetiredWeaponSaveMigration` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Flow/Production/RetiredWeaponSaveMigrationV1.cs` |
| `PlayerHoldingsService` | `PlayerHoldings` | class | Service | no | `Assets/ShooterMover/Runtime/Application/Holdings/PlayerHoldingsService.Persistence.cs` |
| `PlayerHoldingsService` | `PlayerHoldings` | class | Service | no | `Assets/ShooterMover/Runtime/Application/Holdings/PlayerHoldingsService.SnapshotValidation.cs` |
| `PlayerHoldingsService` | `PlayerHoldings` | class | Service | no | `Assets/ShooterMover/Runtime/Application/Holdings/PlayerHoldingsService.ValidationAndSnapshots.cs` |
| `PlayerHoldingsService` | `PlayerHoldings` | class | Service | no | `Assets/ShooterMover/Runtime/Application/Holdings/PlayerHoldingsService.cs` |
| `InventoryLoadoutSlotKindV1` | `InventoryLoadoutSlotKind` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Inventory/LoadoutScreen/InventoryLoadoutScreenV1.cs` |
| `InventoryLoadoutAuthorityMutationStatusV1` | `InventoryLoadoutMutationStatus` | enum | Authority | yes | `Assets/ShooterMover/Runtime/Application/Inventory/LoadoutScreen/InventoryLoadoutScreenV1.cs` |
| `InventoryLoadoutScreenStatusV1` | `InventoryLoadoutScreenStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Inventory/LoadoutScreen/InventoryLoadoutScreenV1.cs` |
| `InventoryLoadoutSlotIdsV1` | `InventoryLoadoutSlotIds` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Inventory/LoadoutScreen/InventoryLoadoutScreenV1.cs` |
| `InventoryLoadoutSlotDescriptorV1` | `InventoryLoadoutSlotDescriptor` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Inventory/LoadoutScreen/InventoryLoadoutScreenV1.cs` |
| `InventoryLoadoutSlotsV1` | `InventoryLoadoutSlots` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Inventory/LoadoutScreen/InventoryLoadoutScreenV1.cs` |
| `InventoryLoadoutSlotBindingV1` | `InventoryLoadoutSlotBinding` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Inventory/LoadoutScreen/InventoryLoadoutScreenV1.cs` |
| `InventoryLoadoutAuthoritySnapshotV1` | `InventoryLoadoutSnapshot` | class | Authority | yes | `Assets/ShooterMover/Runtime/Application/Inventory/LoadoutScreen/InventoryLoadoutScreenV1.cs` |
| `InventoryLoadoutAuthorityCommandV1` | `InventoryLoadoutCommand` | class | Authority | yes | `Assets/ShooterMover/Runtime/Application/Inventory/LoadoutScreen/InventoryLoadoutScreenV1.cs` |
| `InventoryLoadoutAuthorityResultV1` | `InventoryLoadoutResult` | class | Authority | yes | `Assets/ShooterMover/Runtime/Application/Inventory/LoadoutScreen/InventoryLoadoutScreenV1.cs` |
| `IInventoryLoadoutAuthorityPortV1` | `IInventoryLoadoutPort` | interface | Authority | yes | `Assets/ShooterMover/Runtime/Application/Inventory/LoadoutScreen/InventoryLoadoutScreenV1.cs` |
| `InventoryLoadoutEquipmentProjectionV1` | `InventoryLoadoutEquipment` | class | Projection | yes | `Assets/ShooterMover/Runtime/Application/Inventory/LoadoutScreen/InventoryLoadoutScreenV1.cs` |
| `InventoryLoadoutSelectionProjectionV1` | `InventoryLoadoutSelection` | class | Projection | yes | `Assets/ShooterMover/Runtime/Application/Inventory/LoadoutScreen/InventoryLoadoutScreenV1.cs` |
| `InventoryLoadoutScreenSnapshotV1` | `InventoryLoadoutScreenSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Inventory/LoadoutScreen/InventoryLoadoutScreenV1.cs` |
| `InventoryLoadoutScreenResultV1` | `InventoryLoadoutScreenResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Inventory/LoadoutScreen/InventoryLoadoutScreenV1.cs` |
| `InventoryLoadoutScreenServiceV1` | `InventoryLoadoutScreen` | class | Service | yes | `Assets/ShooterMover/Runtime/Application/Inventory/LoadoutScreen/InventoryLoadoutScreenV1.cs` |
| `InventoryLoadoutCanonicalV1` | `InventoryLoadout` | class | Canonical | yes | `Assets/ShooterMover/Runtime/Application/Inventory/LoadoutScreen/InventoryLoadoutScreenV1.cs` |
| `MissionResultsSessionV1` | `MissionResultsSession` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Missions/Results/MissionResultsSessionV1.cs` |
| `MissionRunCollectionVerificationV1` | `MissionRunCollectionVerification` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Missions/Results/MissionRunAuthorityPortsV1.cs` |
| `MissionRunStrongboxProjectionV1` | `MissionRunStrongbox` | class | Projection | yes | `Assets/ShooterMover/Runtime/Application/Missions/Results/MissionRunAuthorityPortsV1.cs` |
| `IMissionRunExistingAuthorityPortV1` | `IMissionRunExistingPort` | interface | Authority | yes | `Assets/ShooterMover/Runtime/Application/Missions/Results/MissionRunAuthorityPortsV1.cs` |
| `MissionRunExistingAuthorityPortV1` | `MissionRunExistingPort` | class | Authority | yes | `Assets/ShooterMover/Runtime/Application/Missions/Results/MissionRunExistingAuthorityPortV1.cs` |
| `MissionRunResultAuthorityV1` | `MissionRunResult` | class | Authority | yes | `Assets/ShooterMover/Runtime/Application/Missions/Results/MissionRunResultAuthorityV1.cs` |
| `RuntimeBoundsDto` | `BoundsDto` | class | Runtime | no | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/LevelGridV2CompilerDtos.cs` |
| `RoomAccessImportIssueV1` | `RoomAccessImportIssue` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomAccessJsonImporterV1.cs` |
| `RoomAccessImportResultV1` | `RoomAccessImportResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomAccessJsonImporterV1.cs` |
| `RoomAccessJsonImporterV1` | `RoomAccessJsonImporter` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomAccessJsonImporterV1.cs` |
| `RoomContentJsonImporterV1` | `RoomContentJsonImporter` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentJsonDtosV1.cs` |
| `RoomContentJsonImporterV1` | `RoomContentJsonImporter` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentJsonImporterV1.cs` |
| `RoomContentObjectKindV1` | `RoomContentObjectKind` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentModelV1.cs` |
| `RoomContentVisualLayerV1` | `RoomContentVisualLayer` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentModelV1.cs` |
| `RoomContentObjectDefinitionV1` | `RoomContentObjectDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentModelV1.cs` |
| `IRoomContentObjectCatalogV1` | `IRoomContentObjectCatalog` | interface | — | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentModelV1.cs` |
| `RoomContentObjectCatalogV1` | `RoomContentObjectCatalog` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentModelV1.cs` |
| `RoomContentJsonPackageV1` | `RoomContentJsonPackage` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentModelV1.cs` |
| `RoomEnemyPlacementContentV1` | `RoomEnemyPlacementContent` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentModelV1.cs` |
| `RoomPropPlacementContentV1` | `RoomPropPlacementContent` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentModelV1.cs` |
| `RoomVisualPlacementContentV1` | `RoomVisualPlacementContent` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentModelV1.cs` |
| `RoomContentBundleV1` | `RoomContentBundle` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentModelV1.cs` |
| `RoomContentImportIssueV1` | `RoomContentImportIssue` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentModelV1.cs` |
| `RoomContentImportResultV1` | `RoomContentImportResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentModelV1.cs` |
| `RoomAccessAuthorityV1` | `RoomAccess` | class | Authority | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomAccessAuthorityV1.cs` |
| `RoomLiveAccessFactProjectionV1` | `RoomLiveAccessFact` | class | Projection | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveAccessFactProjectionV1.cs` |
| `RoomLiveRuntimeAuthorityV1` | `RoomLive` | class | Runtime, Authority | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveRuntimeAuthorityCoreV1.cs` |
| `RoomOperationInspectionV1` | `RoomOperationInspection` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveRuntimeHelpersV1.cs` |
| `RoomOperationJournalV1` | `RoomOperationJournal` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveRuntimeHelpersV1.cs` |
| `RoomRetainedFactStoreV1` | `RoomRetainedFactStore` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveRuntimeHelpersV1.cs` |
| `RoomCompletionEvaluationV1` | `RoomCompletionEvaluation` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveRuntimeHelpersV1.cs` |
| `RoomCompletionEvaluatorV1` | `RoomCompletionEvaluator` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveRuntimeHelpersV1.cs` |
| `RoomDoorGatePolicyV1` | `RoomDoorGatePolicy` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveRuntimeHelpersV1.cs` |
| `RoomLiveProjectionBuilderV1` | `RoomLiveBuilder` | class | Projection | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveRuntimeHelpersV1.cs` |
| `RoomLiveOperationStatusV1` | `RoomLiveOperationStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveRuntimeProjectionsV1.cs` |
| `IRoomLiveRuntimeQueryV1` | `IRoomLiveQuery` | interface | Runtime | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveRuntimeProjectionsV1.cs` |
| `RoomLiveRoomProjectionV1` | `RoomLiveRoom` | class | Projection | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveRuntimeProjectionsV1.cs` |
| `RoomLiveRuntimeProjectionV1` | `RoomLive` | class | Runtime, Projection | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveRuntimeProjectionsV1.cs` |
| `RoomLiveOperationResultV1` | `RoomLiveOperationResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveRuntimeProjectionsV1.cs` |
| `RoomTraversalResultV1` | `RoomTraversalResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveRuntimeTraversalV1.cs` |
| `RoomTraversalCoordinatorV1` | `RoomTraversal` | class | Coordinator | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveRuntimeTraversalV1.cs` |
| `RoomMissionLayoutV1` | `RoomMissionLayout` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomMissionLayoutV1.cs` |
| `RoomRuntimeAuthorityV1` | `Room` | class | Runtime, Authority | yes | `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomRuntimeAuthorityV1.cs` |
| `IAuthoritativeEventClockV1` | `IAuthoritativeEventClock` | interface | — | yes | `Assets/ShooterMover/Runtime/Application/Modifiers/Events/ActiveEventModifierProjectionServiceV1.cs` |
| `ActiveEventProjectionStatusV1` | `ActiveEventStatus` | enum | Projection | yes | `Assets/ShooterMover/Runtime/Application/Modifiers/Events/ActiveEventModifierProjectionServiceV1.cs` |
| `ActiveEventProjectionResultV1` | `ActiveEventResult` | class | Projection | yes | `Assets/ShooterMover/Runtime/Application/Modifiers/Events/ActiveEventModifierProjectionServiceV1.cs` |
| `ActiveEventModifierProjectionServiceV1` | `ActiveEventModifier` | class | Projection, Service | yes | `Assets/ShooterMover/Runtime/Application/Modifiers/Events/ActiveEventModifierProjectionServiceV1.cs` |
| `EventProjectionCanonicalV1` | `Event` | class | Projection, Canonical | yes | `Assets/ShooterMover/Runtime/Application/Modifiers/Events/ActiveEventModifierProjectionServiceV1.cs` |
| `EventStampedCommandKindV1` | `EventStampedCommandKind` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Modifiers/Events/EventStampedCommandEnvelopeV1.cs` |
| `EventStampedCommandEnvelopeV1` | `EventStampedCommandEnvelope` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Modifiers/Events/EventStampedCommandEnvelopeV1.cs` |
| `RuntimeObservedFactStatusV1` | `ObservedFactStatus` | enum | Runtime | yes | `Assets/ShooterMover/Runtime/Application/Modifiers/FactWindowConditionAuthorityV1.cs` |
| `RuntimeObservedFactV1` | `ObservedFact` | class | Runtime | yes | `Assets/ShooterMover/Runtime/Application/Modifiers/FactWindowConditionAuthorityV1.cs` |
| `RuntimeConditionActivationFactV1` | `ConditionActivationFact` | class | Runtime | yes | `Assets/ShooterMover/Runtime/Application/Modifiers/FactWindowConditionAuthorityV1.cs` |
| `RuntimeObservedFactResultV1` | `ObservedFactResult` | class | Runtime | yes | `Assets/ShooterMover/Runtime/Application/Modifiers/FactWindowConditionAuthorityV1.cs` |
| `FactWindowConditionAuthorityV1` | `FactWindowCondition` | class | Authority | yes | `Assets/ShooterMover/Runtime/Application/Modifiers/FactWindowConditionAuthorityV1.cs` |
| `SkillEffectModifierAdapterV1` | `SkillEffectModifier` | class | Adapter | yes | `Assets/ShooterMover/Runtime/Application/Modifiers/FactWindowConditionAuthorityV1.cs` |
| `ModifierApplicationFingerprintV1` | `ModifierApplicationFingerprint` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Modifiers/FactWindowConditionAuthorityV1.cs` |
| `FactWindowStatusEffectBindingV1` | `FactWindowStatusEffectBinding` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Modifiers/StatusEffects/FactWindowStatusEffectBridgeV1.cs` |
| `FactWindowStatusEffectBridgeV1` | `FactWindowStatusEffectBridge` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Modifiers/StatusEffects/FactWindowStatusEffectBridgeV1.cs` |
| `StatusEffectAuthorityV1` | `StatusEffect` | class | Authority | yes | `Assets/ShooterMover/Runtime/Application/Modifiers/StatusEffects/StatusEffectAuthorityV1.Commands.cs` |
| `StatusEffectAuthorityV1` | `StatusEffect` | class | Authority | yes | `Assets/ShooterMover/Runtime/Application/Modifiers/StatusEffects/StatusEffectAuthorityV1.Core.cs` |
| `StatusEffectAuthorityV1` | `StatusEffect` | class | Authority | yes | `Assets/ShooterMover/Runtime/Application/Modifiers/StatusEffects/StatusEffectAuthorityV1.Snapshots.cs` |
| `StatusEffectAuthorityV1` | `StatusEffect` | class | Authority | yes | `Assets/ShooterMover/Runtime/Application/Modifiers/StatusEffects/StatusEffectAuthorityV1.Stacking.cs` |
| `StatusEffectLocalHashV1` | `StatusEffectLocalHash` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Modifiers/StatusEffects/StatusEffectLocalHashV1.cs` |
| `PlayerAccountSaveCommandKindV1` | `PlayerAccountSaveCommandKind` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Accounts/PlayerAccountSaveAuthorityV1.cs` |
| `PlayerAccountSaveStatusV1` | `PlayerAccountSaveStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Accounts/PlayerAccountSaveAuthorityV1.cs` |
| `PlayerAccountSaveCommandV1` | `PlayerAccountSaveCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Accounts/PlayerAccountSaveAuthorityV1.cs` |
| `PlayerAccountSaveResultV1` | `PlayerAccountSaveResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Accounts/PlayerAccountSaveAuthorityV1.cs` |
| `PlayerAccountSaveReplayRecordV1` | `PlayerAccountSaveReplayRecord` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Accounts/PlayerAccountSaveAuthorityV1.cs` |
| `PlayerAccountSaveAuthoritySnapshotV1` | `PlayerAccountSaveSnapshot` | class | Authority | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Accounts/PlayerAccountSaveAuthorityV1.cs` |
| `PlayerAccountSaveAuthorityV1` | `PlayerAccountSave` | class | Authority | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Accounts/PlayerAccountSaveAuthorityV1.cs` |
| `SaveAuthorityFingerprintV1` | `SaveFingerprint` | class | Authority | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Accounts/PlayerAccountSaveAuthorityV1.cs` |
| `IAtomicSaveFilePortV1` | `IAtomicSaveFilePort` | interface | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/AtomicPlayerAccountStoreV1.cs` |
| `PlayerAccountStoreStatusV1` | `PlayerAccountStoreStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/AtomicPlayerAccountStoreV1.cs` |
| `PlayerAccountStoreResultV1` | `PlayerAccountStoreResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/AtomicPlayerAccountStoreV1.cs` |
| `PlayerAccountFileCodecV1` | `PlayerAccountFileCodec` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/AtomicPlayerAccountStoreV1.cs` |
| `AtomicPlayerAccountStoreV1` | `AtomicPlayerAccountStore` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/AtomicPlayerAccountStoreV1.cs` |
| `SavePersistenceLimitsV1` | `SavePersistenceLimits` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/CanonicalSnapshotCodecV1.cs` |
| `CanonicalPayloadExceptionV1` | `PayloadException` | class | Canonical | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/CanonicalSnapshotCodecV1.cs` |
| `CanonicalNodeKindV1` | `NodeKind` | enum | Canonical | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/CanonicalSnapshotCodecV1.cs` |
| `CanonicalFieldV1` | `Field` | class | Canonical | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/CanonicalSnapshotCodecV1.cs` |
| `CanonicalNodeV1` | `Node` | class | Canonical | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/CanonicalSnapshotCodecV1.cs` |
| `CanonicalNodeCodecV1` | `NodeCodec` | class | Canonical | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/CanonicalSnapshotCodecV1.cs` |
| `CanonicalObjectReaderV1` | `ObjectReader` | class | Canonical | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/CanonicalSnapshotCodecV1.cs` |
| `CanonicalValueV1` | `Value` | class | Canonical | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/CanonicalSnapshotCodecV1.cs` |
| `CollectedRunRewardPersistenceExpectationV1` | `CollectedRunRewardPersistenceExpectation` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/CollectedRunRewardPersistenceExpectationV1.cs` |
| `GeneratedEquipmentAugmentSignatureSaveComponentV1` | `GeneratedEquipmentAugmentSignatureSaveComponent` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/GeneratedEquipmentAugmentSignatureSaveComponentV1.cs` |
| `GeneratedEquipmentAugmentSignatureComponentCodecV1` | `GeneratedEquipmentAugmentSignatureComponentCodec` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/GeneratedEquipmentAugmentSignatureSaveComponentV1.cs` |
| `PlayerExperienceComponentCodecV1` | `PlayerExperienceComponentCodec` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownExperienceMoneyCodecsV1.cs` |
| `MoneyWalletComponentCodecV1` | `MoneyWalletComponentCodec` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownExperienceMoneyCodecsV1.cs` |
| `PlayerHoldingsComponentCodecV1` | `PlayerHoldingsComponentCodec` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownHoldingsCodecV1.cs` |
| `LedgerSnapshotCodecV1` | `LedgerSnapshotCodec` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownLedgerScrapSkillLoadoutCodecsV1.cs` |
| `ScrapWalletComponentCodecV1` | `ScrapWalletComponentCodec` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownLedgerScrapSkillLoadoutCodecsV1.cs` |
| `RankedSkillAllocationComponentCodecV1` | `RankedSkillAllocationComponentCodec` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownLedgerScrapSkillLoadoutCodecsV1.cs` |
| `ExactInstanceLoadoutComponentCodecV1` | `ExactInstanceLoadoutComponentCodec` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownLedgerScrapSkillLoadoutCodecsV1.cs` |
| `ExplicitSaveComponentCodecV1` | `ExplicitSaveComponentCodec` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownSaveComponentCodecCoreV1.cs` |
| `KnownSaveComponentCodecsV1` | `KnownSaveComponentCodecs` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownSaveComponentCodecCoreV1.cs` |
| `ExplicitCodecValuesV1` | `ExplicitCodecValues` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownSaveComponentCodecCoreV1.cs` |
| `PlayerAccountAggregateCodecV1` | `PlayerAccountAggregateCodec` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownSaveComponentCodecCoreV1.cs` |
| `KnownSaveComponentVersionGuardV1` | `KnownSaveComponentVersionGuard` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownSaveComponentVersionGuardV1.cs` |
| `StrongboxOpeningComponentCodecV1` | `StrongboxOpeningComponentCodec` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownStrongboxCodecV1.cs` |
| `PlayerAccountComponentSemanticsV1` | `PlayerAccountComponentSemantics` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/PlayerAccountComponentSemanticsV1.cs` |
| `CharacterSaveRestoreBindingV1` | `CharacterSaveRestoreBinding` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/PlayerAccountRestoreCoordinatorV1.cs` |
| `PlayerAccountRestoreStatusV1` | `PlayerAccountRestoreStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/PlayerAccountRestoreCoordinatorV1.cs` |
| `RetainedUnknownSaveComponentV1` | `RetainedUnknownSaveComponent` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/PlayerAccountRestoreCoordinatorV1.cs` |
| `PlayerAccountRestoreResultV1` | `PlayerAccountRestoreResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/PlayerAccountRestoreCoordinatorV1.cs` |
| `PlayerAccountRestoreCoordinatorV1` | `PlayerAccountRestore` | class | Coordinator | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/PlayerAccountRestoreCoordinatorV1.cs` |
| `SaveComponentValidationStatusV1` | `SaveComponentValidationStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/SaveComponentAdaptersV1.cs` |
| `SaveComponentValidationResultV1` | `SaveComponentValidationResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/SaveComponentAdaptersV1.cs` |
| `SaveComponentApplyResultV1` | `SaveComponentApplyResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/SaveComponentAdaptersV1.cs` |
| `SaveComponentDefinitionV1` | `SaveComponentDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/SaveComponentAdaptersV1.cs` |
| `ISaveComponentPayloadCodecV1` | `ISaveComponentPayloadCodec` | interface | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/SaveComponentAdaptersV1.cs` |
| `SaveComponentCommitStatusV1` | `SaveComponentCommitStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/SaveComponentAdaptersV1.cs` |
| `SaveComponentCommitResultV1` | `SaveComponentCommitResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/SaveComponentAdaptersV1.cs` |
| `SaveComponentRollbackResultV1` | `SaveComponentRollbackResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/SaveComponentAdaptersV1.cs` |
| `IPreparedSaveComponentRestoreV1` | `IPreparedSaveComponentRestore` | interface | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/SaveComponentAdaptersV1.cs` |
| `SaveComponentPrepareResultV1` | `SaveComponentPrepareResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/SaveComponentAdaptersV1.cs` |
| `ISaveComponentAdapterV1` | `ISaveComponent` | interface | Adapter | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/SaveComponentAdaptersV1.cs` |
| `AuthoritySnapshotSaveComponentAdapterV1` | `SnapshotSaveComponent` | class | Authority, Adapter | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/SaveComponentAdaptersV1.cs` |
| `KnownSaveComponentDefinitionsV1` | `KnownSaveComponentDefinitions` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/SaveComponentAdaptersV1.cs` |
| `KnownSaveComponentAdaptersV1` | `KnownSaveComponentAdapters` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Components/SaveComponentAdaptersV1.cs` |
| `ICharacterRuntimeGraphV1` | `ICharacterGraph` | interface | Runtime | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Composition/CharacterCompositionCoordinatorV1.cs` |
| `ICharacterRuntimeGraphFactoryV1` | `ICharacterGraphFactory` | interface | Runtime | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Composition/CharacterCompositionCoordinatorV1.cs` |
| `IStarterCharacterRuntimeGraphFactoryV1` | `IStarterCharacterGraphFactory` | interface | Runtime | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Composition/CharacterCompositionCoordinatorV1.cs` |
| `CharacterCompositionStatusV1` | `CharacterStatus` | enum | Composition | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Composition/CharacterCompositionCoordinatorV1.cs` |
| `CharacterCompositionResultV1` | `CharacterResult` | class | Composition | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Composition/CharacterCompositionCoordinatorV1.cs` |
| `CharacterCompositionCoordinatorV1` | `Character` | class | Composition, Coordinator | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Composition/CharacterCompositionCoordinatorV1.cs` |
| `LegacyCharacterProfileV1` | `LegacyCharacterProfile` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Composition/LegacyCharacterProfileMigrationV1.cs` |
| `LegacyCharacterProfileMigrationResultV1` | `LegacyCharacterProfileMigrationResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Composition/LegacyCharacterProfileMigrationV1.cs` |
| `LegacyCharacterProfileMigrationV1` | `LegacyCharacterProfileMigration` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Persistence/Composition/LegacyCharacterProfileMigrationV1.cs` |
| `EnemyExperienceRewardIdsV1` | `EnemyExperienceRewardIds` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Progression/Experience/EnemyRewards/EnemyExperienceRewardDefinitionsV1.cs` |
| `EnemyExperienceRewardBandV1` | `EnemyExperienceRewardBand` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Progression/Experience/EnemyRewards/EnemyExperienceRewardDefinitionsV1.cs` |
| `IEnemyExperienceRewardDefinitionV1` | `IEnemyExperienceRewardDefinition` | interface | — | yes | `Assets/ShooterMover/Runtime/Application/Progression/Experience/EnemyRewards/EnemyExperienceRewardDefinitionsV1.cs` |
| `EnemyExperienceRewardDefinitionV1` | `EnemyExperienceRewardDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Progression/Experience/EnemyRewards/EnemyExperienceRewardDefinitionsV1.cs` |
| `EnemyExperienceRewardCatalogV1` | `EnemyExperienceRewardCatalog` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Progression/Experience/EnemyRewards/EnemyExperienceRewardDefinitionsV1.cs` |
| `EnemyExperienceRewardOperationIdentityV1` | `EnemyExperienceRewardOperationIdentity` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Progression/Experience/EnemyRewards/EnemyExperienceRewardOperationV1.cs` |
| `EnemyExperienceRewardStatusV1` | `EnemyExperienceRewardStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Progression/Experience/EnemyRewards/EnemyExperienceRewardOperationV1.cs` |
| `EnemyExperienceRewardFactV1` | `EnemyExperienceRewardFact` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Progression/Experience/EnemyRewards/EnemyExperienceRewardOperationV1.cs` |
| `EnemyExperienceRewardServiceV1` | `EnemyExperienceReward` | class | Service | yes | `Assets/ShooterMover/Runtime/Application/Progression/Experience/EnemyRewards/EnemyExperienceRewardServiceV1.cs` |
| `PlayerExperienceAuthorityV1` | `PlayerExperience` | class | Authority | yes | `Assets/ShooterMover/Runtime/Application/Progression/Experience/PlayerExperienceAuthorityV1.cs` |
| `SkillAllocationRejectionV2` | `SkillAllocationRejection` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Progression/Skills/RankedSkillAuthorityV2.cs` |
| `AllocateSkillRankCommandV2` | `AllocateSkillRankCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Progression/Skills/RankedSkillAuthorityV2.cs` |
| `SkillAllocationResultV2` | `SkillAllocationResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Progression/Skills/RankedSkillAuthorityV2.cs` |
| `RankedSkillAllocationAuthorityV2` | `RankedSkillAllocation` | class | Authority | yes | `Assets/ShooterMover/Runtime/Application/Progression/Skills/RankedSkillAuthorityV2.cs` |
| `ISkillRespecPaymentAuthorityV2` | `ISkillRespecPayment` | interface | Authority | yes | `Assets/ShooterMover/Runtime/Application/Progression/Skills/RankedSkillAuthorityV2.cs` |
| `SkillRespecPaymentResultV2` | `SkillRespecPaymentResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Progression/Skills/RankedSkillAuthorityV2.cs` |
| `ISkillRespecCostPolicyV2` | `ISkillRespecCostPolicy` | interface | — | yes | `Assets/ShooterMover/Runtime/Application/Progression/Skills/RankedSkillAuthorityV2.cs` |
| `SkillRespecQuoteV2` | `SkillRespecQuote` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Progression/Skills/RankedSkillAuthorityV2.cs` |
| `SkillRespecRejectionV2` | `SkillRespecRejection` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Progression/Skills/RankedSkillAuthorityV2.cs` |
| `SkillRespecReceiptV2` | `SkillRespecReceipt` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Progression/Skills/RankedSkillAuthorityV2.cs` |
| `SkillRespecOrchestratorV2` | `SkillRespecOrchestrator` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Progression/Skills/RankedSkillAuthorityV2.cs` |
| `SkillMigrationResultV2` | `SkillMigrationResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Progression/Skills/RankedSkillAuthorityV2.cs` |
| `SkillAllocationMigratorV2` | `SkillAllocationMigrator` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Progression/Skills/RankedSkillAuthorityV2.cs` |
| `SkillRuntimeReconciliationV2` | `SkillReconciliation` | class | Runtime | yes | `Assets/ShooterMover/Runtime/Application/Progression/Skills/RankedSkillAuthorityV2.cs` |
| `SkillProgressionAuthorityV1` | `SkillProgression` | class | Authority | yes | `Assets/ShooterMover/Runtime/Application/Progression/Skills/SkillProgressionAuthorityV1.cs` |
| `MoneyRewardChildAuthorityV1` | `MoneyRewardChild` | class | Authority | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Application/RewardApplicationAuthorityAdaptersV1.cs` |
| `ScrapRewardChildAuthorityV1` | `ScrapRewardChild` | class | Authority | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Application/RewardApplicationAuthorityAdaptersV1.cs` |
| `PlayerHoldingsRewardChildAuthorityV1` | `PlayerHoldingsRewardChild` | class | Authority | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Application/RewardApplicationAuthorityAdaptersV1.cs` |
| `RewardAuthorityAdapterOrderingV1` | `RewardOrdering` | class | Authority, Adapter | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Application/RewardApplicationAuthorityAdaptersV1.cs` |
| `RewardApplicationServiceV1` | `RewardApplication` | class | Service | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Application/RewardApplicationServiceV1.Persistence.cs` |
| `RewardApplicationServiceV1` | `RewardApplication` | class | Service | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Application/RewardApplicationServiceV1.cs` |
| `CollectedRunRewardAtomicPlanV2` | `CollectedRunRewardAtomicPlan` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardAtomicPlanV2.cs` |
| `CollectedRunRewardPreparedTransferStateV1` | `CollectedRunRewardPreparedTransferState` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardPreparedTransfersV1.cs` |
| `CollectedRunRewardPreparedTransferV1` | `CollectedRunRewardPreparedTransfer` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardPreparedTransfersV1.cs` |
| `CollectedRunRewardPreparedTransferSnapshotV1` | `CollectedRunRewardPreparedTransferSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardPreparedTransfersV1.cs` |
| `CollectedRunRewardPreparedTransferAuthorityV1` | `CollectedRunRewardPreparedTransfer` | class | Authority | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardPreparedTransfersV1.cs` |
| `CollectedRunRewardPreparedTransferSaveComponentV1` | `CollectedRunRewardPreparedTransferSaveComponent` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardPreparedTransfersV1.cs` |
| `CollectedRunRewardTransferCanonicalV1` | `CollectedRunRewardTransfer` | class | Canonical | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferContractsV1.cs` |
| `CollectedRunRewardTransferItemV1` | `CollectedRunRewardTransferItem` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferContractsV1.cs` |
| `CollectedRunRewardTransferBatchV1` | `CollectedRunRewardTransferBatch` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferContractsV1.cs` |
| `CollectedRunRewardTransferReceiptV1` | `CollectedRunRewardTransferReceipt` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferContractsV1.cs` |
| `CollectedRunRewardTransferCoordinatorV2` | `CollectedRunRewardTransfer` | class | Coordinator | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferCoordinatorV1.cs` |
| `CollectedRunRewardTransferAuthorityStatusV1` | `CollectedRunRewardTransferStatus` | enum | Authority | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferPortsV1.cs` |
| `CollectedRunRewardTransferPersistenceStatusV1` | `CollectedRunRewardTransferPersistenceStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferPortsV1.cs` |
| `CollectedRunRewardTransferStatusV1` | `CollectedRunRewardTransferStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferPortsV1.cs` |
| `PermanentRewardTransferStateV1` | `PermanentRewardTransferState` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferPortsV1.cs` |
| `CollectedRunRewardTransferPreflightResultV1` | `CollectedRunRewardTransferPreflightResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferPortsV1.cs` |
| `CollectedRunRewardAtomicApplyResultV1` | `CollectedRunRewardAtomicApplyResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferPortsV1.cs` |
| `CollectedRunRewardTransferReceiptRecordResultV1` | `CollectedRunRewardTransferReceiptRecordResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferPortsV1.cs` |
| `CollectedRunRewardTransferRestoreResultV1` | `CollectedRunRewardTransferRestoreResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferPortsV1.cs` |
| `CollectedRunRewardTransferPersistenceResultV1` | `CollectedRunRewardTransferPersistenceResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferPortsV1.cs` |
| `ICollectedRunRewardTransferCompensationV1` | `ICollectedRunRewardTransferCompensation` | interface | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferPortsV1.cs` |
| `ICollectedRunRewardAtomicBatchAuthorityPortV1` | `ICollectedRunRewardAtomicBatchPort` | interface | Authority | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferPortsV1.cs` |
| `ICollectedRunRewardTransferPersistencePortV1` | `ICollectedRunRewardTransferPersistencePort` | interface | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferPortsV1.cs` |
| `CollectedRunRewardTransferResultV1` | `CollectedRunRewardTransferResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferPortsV1.cs` |
| `CollectedRunRewardTransferReceiptSnapshotV1` | `CollectedRunRewardTransferReceiptSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferReceiptsV1.cs` |
| `CollectedRunRewardTransferReceiptAuthorityV1` | `CollectedRunRewardTransferReceipt` | class | Authority | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferReceiptsV1.cs` |
| `CollectedRunRewardTransferReceiptSaveComponentV1` | `CollectedRunRewardTransferReceiptSaveComponent` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferReceiptsV1.cs` |
| `CollectedRunRewardTransferResultsProjectionV1` | `CollectedRunRewardTransferResults` | class | Projection | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferResultsV1.cs` |
| `RetryCollectedRunRewardTransferCommandV1` | `RetryCollectedRunRewardTransferCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferResultsV1.cs` |
| `ProductionCollectedRunRewardResultsBridge` | `CollectedRunRewardResultsBridge` | class | Production | no | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferResultsV1.cs` |
| `ProductionCollectedRunRewardCompensationV2` | `CollectedRunRewardCompensation` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/ProductionCollectedRunRewardAtomicAuthorityV2.cs` |
| `ProductionCollectedRunRewardAtomicAuthorityV2` | `CollectedRunRewardAtomic` | class | Production, Authority | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/ProductionCollectedRunRewardAtomicAuthorityV2.cs` |
| `ProductionCollectedRunRewardPersistenceV2` | `CollectedRunRewardPersistence` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/ProductionCollectedRunRewardPersistenceV2.cs` |
| `ProductionCollectedRunRewardTransferServiceV2` | `CollectedRunRewardTransfer` | class | Production, Service | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/ProductionCollectedRunRewardPersistenceV2.cs` |
| `CollectedRunRewardGenerationContextV2` | `CollectedRunRewardGenerationContext` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/ProductionCollectedRunRewardPreparationV2.cs` |
| `ICollectedRunEquipmentPayloadSourceV2` | `ICollectedRunEquipmentPayloadSource` | interface | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/ProductionCollectedRunRewardPreparationV2.cs` |
| `RejectingCollectedRunEquipmentPayloadSourceV2` | `RejectingCollectedRunEquipmentPayloadSource` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/ProductionCollectedRunRewardPreparationV2.cs` |
| `CollectedRunRewardTransferPreparationFactoryV2` | `CollectedRunRewardTransferPreparationFactory` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/ProductionCollectedRunRewardPreparationV2.cs` |
| `ProductionCollectedRunRewardTransferRuntimeRegistry` | `CollectedRunRewardTransferRegistry` | class | Production, Runtime | no | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/ProductionCollectedRunRewardRegistryCompatibility.cs` |
| `ProductionCollectedRunRewardRuntimeRegistryV2` | `CollectedRunRewardRegistry` | class | Production, Runtime | yes | `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/ProductionCollectedRunRewardRuntimeRegistryV2.cs` |
| `IParticipantDropPacingStateStoreV1` | `IParticipantDropPacingStateStore` | interface | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Drops/IParticipantDropPacingStateStoreV1.cs` |
| `IPersonalRewardDeliveryOutboxV1` | `IPersonalRewardDeliveryOutbox` | interface | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Drops/IPersonalRewardDeliveryOutboxV1.cs` |
| `ParticipantDropPacingAuthorityV1` | `ParticipantDropPacing` | class | Authority | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Drops/ParticipantDropPacingAuthorityV1.cs` |
| `PersonalRewardDeliveryStateV1` | `PersonalRewardDeliveryState` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Drops/PersonalRewardDeliveryEnvelopeV1.cs` |
| `PersonalRewardDeliveryEnvelopeV1` | `PersonalRewardDeliveryEnvelope` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Drops/PersonalRewardDeliveryEnvelopeV1.cs` |
| `PersonalRewardGenerationRandomV1` | `PersonalRewardGenerationRandom` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Drops/PersonalRewardGenerationRandomV1.cs` |
| `PersonalRewardGenerationServiceV1` | `PersonalRewardGeneration` | class | Service | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Drops/PersonalRewardGenerationServiceV1.cs` |
| `PersonalRewardGroupGenerationV1` | `PersonalRewardGroupGeneration` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Drops/PersonalRewardGroupGenerationV1.cs` |
| `PersonalStrongboxRewardGenerationV1` | `PersonalStrongboxRewardGeneration` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Drops/PersonalStrongboxRewardGenerationV1.cs` |
| `ProductionRewardOverrideCatalogV1` | `RewardOverrideCatalog` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Drops/ProductionRewardOverrideCatalogV1.cs` |
| `ProductionRewardSourceCatalogV1` | `RewardSourceCatalog` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Drops/ProductionRewardSourceCatalogV1.cs` |
| `ProductionRunDropPacingCatalogV1` | `RunDropPacingCatalog` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Drops/ProductionRunDropPacingCatalogV1.cs` |
| `ProductionStrongboxTierSelectionCatalogV1` | `StrongboxTierSelectionCatalog` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Drops/ProductionStrongboxTierSelectionCatalogV1.cs` |
| `RewardContextOverrideResolutionV1` | `RewardContextOverrideResolution` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Drops/RewardContextOverrideResolutionV1.cs` |
| `IRewardGrantHandlerV1` | `IRewardGrantHandler` | interface | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Drops/RewardGrantHandlerRegistryV1.cs` |
| `RewardGrantHandlerRegistryV1` | `RewardGrantHandlerRegistry` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Drops/RewardGrantHandlerRegistryV1.cs` |
| `RewardProfileResolverV1` | `RewardProfileResolver` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Drops/RewardProfileResolverV1.cs` |
| `GameplayDropOverrideModeV1` | `GameplayDropOverrideMode` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/GameplayDrops/GameplayDropOperationV1.cs` |
| `GameplayDropOverrideV1` | `GameplayDropOverride` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/GameplayDrops/GameplayDropOperationV1.cs` |
| `GameplayDropOperationV1` | `GameplayDropOperation` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/GameplayDrops/GameplayDropOperationV1.cs` |
| `GameplayDropOperationFactoryV1` | `GameplayDropOperationFactory` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/GameplayDrops/GameplayDropOperationV1.cs` |
| `RewardGenerationScalingValueV1` | `RewardGenerationScalingValue` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Generation/RewardGenerationModelsV1.cs` |
| `RewardGenerationRequestV1` | `RewardGenerationRequest` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Generation/RewardGenerationModelsV1.cs` |
| `RewardGenerationResultEnvelopeV1` | `RewardGenerationResultEnvelope` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Generation/RewardGenerationModelsV1.cs` |
| `EquipmentGenerationRequestV1` | `EquipmentGenerationRequest` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Generation/RewardGenerationModelsV1.cs` |
| `EquipmentGenerationResultV1` | `EquipmentGenerationResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Generation/RewardGenerationModelsV1.cs` |
| `RewardGenerationServiceV1` | `RewardGeneration` | class | Service | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Generation/RewardGenerationServiceV1.Core.cs` |
| `RewardGenerationServiceV1` | `RewardGeneration` | class | Service | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Generation/RewardGenerationServiceV1.Equipment.Helpers.cs` |
| `RewardGenerationServiceV1` | `RewardGeneration` | class | Service | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Generation/RewardGenerationServiceV1.Equipment.cs` |
| `RewardGenerationServiceV1` | `RewardGeneration` | class | Service | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Generation/RewardGenerationServiceV1.Rewards.cs` |
| `GeneratedAugmentSignaturePlayerHoldingsRewardChildAuthorityV1` | `GeneratedAugmentSignaturePlayerHoldingsRewardChild` | class | Authority | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/GeneratedAugmentSignaturePlayerHoldingsRewardChildAuthorityV1.cs` |
| `GeneratedEquipmentAugmentSignatureRecordStatusV1` | `GeneratedEquipmentAugmentSignatureRecordStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/GeneratedEquipmentAugmentSignatureAuthorityV1.cs` |
| `GeneratedEquipmentAugmentSignatureRecordResultV1` | `GeneratedEquipmentAugmentSignatureRecordResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/GeneratedEquipmentAugmentSignatureAuthorityV1.cs` |
| `GeneratedEquipmentAugmentSignatureAuthorityV1` | `GeneratedEquipmentAugmentSignature` | class | Authority | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/GeneratedEquipmentAugmentSignatureAuthorityV1.cs` |
| `GeneratedEquipmentAugmentSignatureSnapshotV1` | `GeneratedEquipmentAugmentSignatureSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/GeneratedEquipmentAugmentSignatureSnapshotV1.cs` |
| `StrongboxDurableOpeningCoordinatorV1` | `StrongboxDurableOpening` | class | Coordinator | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxDurableOpeningCoordinatorV1.cs` |
| `IStrongboxDurableOpeningExecutorV1` | `IStrongboxDurableOpeningExecutor` | interface | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxDurableOpeningExecutorV1.cs` |
| `StrongboxDurableOpeningCoordinatorV1` | `StrongboxDurableOpening` | class | Coordinator | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxDurableOpeningRecoveryV1.cs` |
| `StrongboxDurableOpeningCoordinatorV1` | `StrongboxDurableOpening` | class | Coordinator | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxDurableOpeningStateV1.cs` |
| `IStrongboxMissionResultApplicationAuthorityPortV1` | `IStrongboxMissionResultApplicationPort` | interface | Authority | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationAuthorityPortV1.cs` |
| `ExistingStrongboxMissionResultApplicationAuthorityPortV1` | `ExistingStrongboxMissionResultApplicationPort` | class | Authority | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationAuthorityPortV1.cs` |
| `StrongboxMissionResultApplicationCoordinatorV1` | `StrongboxMissionResultApplication` | class | Coordinator | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationCompensationV1.cs` |
| `StrongboxMissionResultApplicationStatusV1` | `StrongboxMissionResultApplicationStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationContractsV1.cs` |
| `StrongboxMissionResultApplicationCommandV1` | `StrongboxMissionResultApplicationCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationContractsV1.cs` |
| `StrongboxMissionResultApplicationResultV1` | `StrongboxMissionResultApplicationResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationContractsV1.cs` |
| `StrongboxMissionResultApplicationCoordinatorV1` | `StrongboxMissionResultApplication` | class | Coordinator | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationCoordinatorV1.cs` |
| `StrongboxMissionResultApplicationCoordinatorV1` | `StrongboxMissionResultApplication` | class | Coordinator | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationExecutionV1.cs` |
| `StrongboxMissionResultApplicationCoordinatorV1` | `StrongboxMissionResultApplication` | class | Coordinator | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationPlanV1.cs` |
| `StrongboxMissionResultApplicationCoordinatorV1` | `StrongboxMissionResultApplication` | class | Coordinator | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationValidationV1.cs` |
| `StrongboxOpeningRecoveryStatusV1` | `StrongboxOpeningRecoveryStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxOpeningRecoveryPortV1.cs` |
| `StrongboxOpeningRecoveryResultV1` | `StrongboxOpeningRecoveryResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxOpeningRecoveryPortV1.cs` |
| `IStrongboxOpeningRecoveryPortV1` | `IStrongboxOpeningRecoveryPort` | interface | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxOpeningRecoveryPortV1.cs` |
| `ExistingStrongboxOpeningRecoveryPortV1` | `ExistingStrongboxOpeningRecoveryPort` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxOpeningRecoveryPortV1.cs` |
| `ProductionStrongboxTierV1` | `StrongboxTier` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/ProductionStrongboxCatalogV1.cs` |
| `ProductionStrongboxCatalogV1` | `StrongboxCatalog` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/ProductionStrongboxCatalogV1.cs` |
| `ProductionStrongboxHybridLootCatalogV1` | `StrongboxHybridLootCatalog` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/ProductionStrongboxHybridLootCatalogV1.cs` |
| `StrongboxProductionFingerprints` | `StrongboxFingerprints` | class | Production | no | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Simulation/StrongboxSimulationContracts.cs` |
| `IStrongboxSimulationProductionGateway` | `IStrongboxSimulationGateway` | interface | Production | no | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Simulation/StrongboxSimulationContracts.cs` |
| `StrongboxSimulationCoordinator` | `StrongboxSimulation` | class | Coordinator | no | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Simulation/StrongboxSimulationCoordinators.cs` |
| `IStrongboxEquipmentGenerationDefinitionProviderV1` | `IStrongboxEquipmentGenerationDefinitionProvider` | interface | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxEquipmentGenerationResolverV1.cs` |
| `StrongboxEquipmentGenerationDefinitionV1` | `StrongboxEquipmentGenerationDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxEquipmentGenerationResolverV1.cs` |
| `StrongboxEquipmentGenerationDefinitionCatalogV1` | `StrongboxEquipmentGenerationDefinitionCatalog` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxEquipmentGenerationResolverV1.cs` |
| `StrongboxEquipmentGenerationResolverV1` | `StrongboxEquipmentGenerationResolver` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxEquipmentGenerationResolverV1.cs` |
| `StrongboxHybridEquipmentGenerationResolverV1` | `StrongboxHybridEquipmentGenerationResolver` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxHybridEquipmentGenerationResolverV1.cs` |
| `IStrongboxRewardGeneratorV1` | `IStrongboxRewardGenerator` | interface | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxOpeningModelsV1.cs` |
| `SharedStrongboxRewardGeneratorV1` | `SharedStrongboxRewardGenerator` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxOpeningModelsV1.cs` |
| `IStrongboxEquipmentPayloadResolverV1` | `IStrongboxEquipmentPayloadResolver` | interface | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxOpeningModelsV1.cs` |
| `StrongboxGrantPayloadResolutionV1` | `StrongboxGrantPayloadResolution` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxOpeningModelsV1.cs` |
| `IStrongboxGrantPayloadResolverV1` | `IStrongboxGrantPayloadResolver` | interface | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxOpeningModelsV1.cs` |
| `DeterministicStrongboxGrantPayloadResolverV1` | `DeterministicStrongboxGrantPayloadResolver` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxOpeningModelsV1.cs` |
| `StrongboxRegistrationStatusV1` | `StrongboxRegistrationStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxOpeningModelsV1.cs` |
| `StrongboxRegistrationResultV1` | `StrongboxRegistrationResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxOpeningModelsV1.cs` |
| `StrongboxOpenCommandV1` | `StrongboxOpenCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxOpeningModelsV1.cs` |
| `StrongboxOpeningRuntimeStatusV1` | `StrongboxOpeningStatus` | enum | Runtime | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxOpeningModelsV1.cs` |
| `StrongboxOpeningStageV1` | `StrongboxOpeningStage` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxOpeningModelsV1.cs` |
| `StrongboxGeneratedOutcomeV1` | `StrongboxGeneratedOutcome` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxOpeningModelsV1.cs` |
| `StrongboxOpeningResultRuntimeV1` | `StrongboxOpeningResult` | class | Runtime | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxOpeningModelsV1.cs` |
| `StrongboxOpeningRecordSnapshotV1` | `StrongboxOpeningRecordSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxOpeningModelsV1.cs` |
| `StrongboxOpeningSnapshotV1` | `StrongboxOpeningSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxOpeningModelsV1.cs` |
| `StrongboxOpeningImportStatusV1` | `StrongboxOpeningImportStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxOpeningModelsV1.cs` |
| `StrongboxOpeningImportResultV1` | `StrongboxOpeningImportResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxOpeningModelsV1.cs` |
| `StrongboxOpeningServiceV1` | `StrongboxOpening` | class | Service | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxOpeningServiceV1.cs` |
| `TransactionalStrongboxGrantPayloadResolverV1` | `TransactionalStrongboxGrantPayloadResolver` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/TransactionalStrongboxGrantPayloadResolverV1.cs` |
| `ExistingStatusEffectRunPortV1` | `ExistingStatusEffectRunPort` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/ExistingRunAuthorityPortsV1.cs` |
| `ExistingMissionResultRunPortV1` | `ExistingMissionResultRunPort` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/ExistingRunAuthorityPortsV1.cs` |
| `DelegatedRunLifecyclePortV1` | `DelegatedRunLifecyclePort` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/ExistingRunAuthorityPortsV1.cs` |
| `DelegatedConditionalFactRunPortV1` | `DelegatedConditionalFactRunPort` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/ExistingRunAuthorityPortsV1.cs` |
| `DelegatedRoomRunPortV1` | `DelegatedRoomRunPort` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/ExistingRunAuthorityPortsV1.cs` |
| `EmptyActiveAbilityRunPortV1` | `EmptyActiveAbilityRunPort` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/ExistingRunAuthorityPortsV1.cs` |
| `ProductionRunStatInputResolutionV1` | `RunStatInputResolution` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/ProductionRunSessionCompositionV1.cs` |
| `IProductionRunStatInputResolverV1` | `IRunStatInputResolver` | interface | Production | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/ProductionRunSessionCompositionV1.cs` |
| `ProductionCharacterRunSessionStartSourceV1` | `CharacterRunSessionStartSource` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/ProductionRunSessionCompositionV1.cs` |
| `RunConditionCheckpointV1` | `RunConditionCheckpoint` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunConditionCheckpointV1.cs` |
| `IRunMissionStrongboxSnapshotSourceV1` | `IRunMissionStrongboxSnapshotSource` | interface | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunMissionResultPersistencePortsV1.cs` |
| `IRunMissionResultLifecycleBindingV1` | `IRunMissionResultLifecycleBinding` | interface | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunMissionResultPersistencePortsV1.cs` |
| `IRunMissionResultEndRetryPolicyV1` | `IRunMissionResultEndRetryPolicy` | interface | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunMissionResultPersistencePortsV1.cs` |
| `RunRewardEnvironmentSnapshotV1` | `RunRewardEnvironmentSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunRewardEnvironmentSnapshotV1.cs` |
| `RunRewardParticipantStateV1` | `RunRewardParticipantState` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunRewardParticipantStateV1.cs` |
| `RunRewardRuntimeSnapshotV1` | `RunRewardSnapshot` | class | Runtime | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunRewardRuntimeSnapshotV1.cs` |
| `RunSessionAggregateV1` | `RunSessionAggregate` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionAggregate.ConditionCheckpointV1.cs` |
| `RunSessionAggregateV1` | `RunSessionAggregate` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionAggregate.ConditionRuntimeV1.cs` |
| `RunSessionAuthorityV1` | `RunSession` | class | Authority | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionAuthorityV1.cs` |
| `RunSessionAggregateV1` | `RunSessionAggregate` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionAuthorityV1.cs` |
| `RunSessionAggregateV1` | `RunSessionAggregate` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionCollectedRewardAuthorityV1.cs` |
| `RunSessionRewardCollectionStatusV1` | `RunSessionRewardCollectionStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionCollectedRewardContractsV1.cs` |
| `RunSessionCollectedRewardV1` | `RunSessionCollectedReward` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionCollectedRewardContractsV1.cs` |
| `RunSessionRewardCollectionResultV1` | `RunSessionRewardCollectionResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionCollectedRewardContractsV1.cs` |
| `IRunSessionCollectedRewardAuthorityV1` | `IRunSessionCollectedReward` | interface | Authority | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionCollectedRewardContractsV1.cs` |
| `RunSessionLifecycleStateV1` | `RunSessionLifecycleState` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionCommandsV1.cs` |
| `RunSessionStartStatusV1` | `RunSessionStartStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionCommandsV1.cs` |
| `RunSessionRestartStatusV1` | `RunSessionRestartStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionCommandsV1.cs` |
| `RunSessionEndStatusV1` | `RunSessionEndStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionCommandsV1.cs` |
| `RunSessionFactKindV1` | `RunSessionFactKind` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionCommandsV1.cs` |
| `RunSessionFactAdmissionStatusV1` | `RunSessionFactAdmissionStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionCommandsV1.cs` |
| `RunLocalMutationKindV1` | `RunLocalMutationKind` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionCommandsV1.cs` |
| `StartRunSessionCommandV1` | `StartRunSessionCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionCommandsV1.cs` |
| `RunRestartPolicyV1` | `RunRestartPolicy` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionCommandsV1.cs` |
| `RestartRunSessionCommandV1` | `RestartRunSessionCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionCommandsV1.cs` |
| `RunSessionFactEnvelopeV1` | `RunSessionFactEnvelope` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionCommandsV1.cs` |
| `RunStrongboxCollectionRequestV1` | `RunStrongboxCollectionRequest` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionCommandsV1.cs` |
| `RunLocalMutationCommandV1` | `RunLocalMutationCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionCommandsV1.cs` |
| `EndRunSessionCommandV1` | `EndRunSessionCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionCommandsV1.cs` |
| `RunSessionFingerprintV1` | `RunSessionFingerprint` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionCommandsV1.cs` |
| `RunConditionDeliveryStatusV1` | `RunConditionDeliveryStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionConditionContractsV1.cs` |
| `RunConditionAdvanceStatusV1` | `RunConditionAdvanceStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionConditionContractsV1.cs` |
| `RunConditionGameplayFactCommandV1` | `RunConditionGameplayFactCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionConditionContractsV1.cs` |
| `RunConditionAdvanceCommandV1` | `RunConditionAdvanceCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionConditionContractsV1.cs` |
| `RunConditionParticipantSnapshotV1` | `RunConditionParticipantSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionConditionContractsV1.cs` |
| `RunConditionRuntimeSnapshotV1` | `RunConditionSnapshot` | class | Runtime | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionConditionContractsV1.cs` |
| `RunConditionDeliveryResultV1` | `RunConditionDeliveryResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionConditionContractsV1.cs` |
| `RunConditionAdvanceResultV1` | `RunConditionAdvanceResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionConditionContractsV1.cs` |
| `IRunConditionRuntimePortV1` | `IRunConditionPort` | interface | Runtime | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionConditionContractsV1.cs` |
| `RunConditionHashV1` | `RunConditionHash` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionConditionContractsV1.cs` |
| `RunSessionDurableAcceptanceStatusV1` | `RunSessionDurableAcceptanceStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionDurableEndV1.cs` |
| `RunSessionDurableEndStateV1` | `RunSessionDurableEndState` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionDurableEndV1.cs` |
| `RunSessionDurableAcceptanceResultV1` | `RunSessionDurableAcceptanceResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionDurableEndV1.cs` |
| `RunSessionAggregateV1` | `RunSessionAggregate` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionDurableEndV1.cs` |
| `RunSessionParticipantDropPacingStateStoreV1` | `RunSessionParticipantDropPacingStateStore` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionParticipantDropPacingStateStoreV1.cs` |
| `RunSessionPersonalRewardDeliveryOutboxV1` | `RunSessionPersonalRewardDeliveryOutbox` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionPersonalRewardDeliveryOutboxV1.cs` |
| `RunRuntimePortRestartResultV1` | `RunPortRestartResult` | class | Runtime | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionPortsV1.cs` |
| `IRunLifecycleRuntimePortV1` | `IRunLifecyclePort` | interface | Runtime | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionPortsV1.cs` |
| `RunPlayerRuntimeSnapshotV1` | `RunPlayerSnapshot` | class | Runtime | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionPortsV1.cs` |
| `IRunPlayerRuntimePortV1` | `IRunPlayerPort` | interface | Runtime | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionPortsV1.cs` |
| `IRunWeaponRuntimePortV1` | `IRunWeaponPort` | interface | Runtime | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionPortsV1.cs` |
| `IRunStatusEffectRuntimePortV1` | `IRunStatusEffectPort` | interface | Runtime | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionPortsV1.cs` |
| `IRunConditionalFactRuntimePortV1` | `IRunConditionalFactPort` | interface | Runtime | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionPortsV1.cs` |
| `IRunActiveAbilityRuntimePortV1` | `IRunActiveAbilityPort` | interface | Runtime | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionPortsV1.cs` |
| `IRunRoomRuntimePortV1` | `IRunRoomPort` | interface | Runtime | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionPortsV1.cs` |
| `IRunMissionResultPortV1` | `IRunMissionResultPort` | interface | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionPortsV1.cs` |
| `RunSessionRuntimePortsV1` | `RunSessionPorts` | class | Runtime | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionPortsV1.cs` |
| `FrozenRunEquipmentV1` | `FrozenRunEquipment` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionPortsV1.cs` |
| `FrozenCharacterRunInputsV1` | `FrozenCharacterRunInputs` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionPortsV1.cs` |
| `RunSessionStartMaterialV1` | `RunSessionStartMaterial` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionPortsV1.cs` |
| `IRunSessionStartSourceV1` | `IRunSessionStartSource` | interface | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionPortsV1.cs` |
| `IRunSessionRuntimePortFactoryV1` | `IRunSessionPortFactory` | interface | Runtime | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionPortsV1.cs` |
| `RunSessionAggregateV1` | `RunSessionAggregate` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionRewardRuntimeStateV1.cs` |
| `RunLocalStateSnapshotV1` | `RunLocalStateSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionSnapshotsV1.cs` |
| `RunHudSnapshotV1` | `RunHudSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionSnapshotsV1.cs` |
| `RunDebugSnapshotV1` | `RunDebugSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionSnapshotsV1.cs` |
| `RunRecoveryDiagnosticSnapshotV1` | `RunRecoveryDiagnosticSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionSnapshotsV1.cs` |
| `RunCheckpointV1` | `RunCheckpoint` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionSnapshotsV1.cs` |
| `RunSessionStartResultV1` | `RunSessionStartResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionSnapshotsV1.cs` |
| `RunSessionRestartResultV1` | `RunSessionRestartResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionSnapshotsV1.cs` |
| `RunSessionEndReceiptV1` | `RunSessionEndReceipt` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionSnapshotsV1.cs` |
| `RunSessionEndResultV1` | `RunSessionEndResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionSnapshotsV1.cs` |
| `RunSessionFactAdmissionResultV1` | `RunSessionFactAdmissionResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionSnapshotsV1.cs` |
| `RunLocalMutationResultV1` | `RunLocalMutationResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionSnapshotsV1.cs` |
| `RunSessionTimeAdvanceStatusV1` | `RunSessionTimeAdvanceStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionTimeV1.cs` |
| `AdvanceRunSessionTimeCommandV1` | `AdvanceRunSessionTimeCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionTimeV1.cs` |
| `RunSessionTimeAdvanceResultV1` | `RunSessionTimeAdvanceResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionTimeV1.cs` |
| `RunSessionAggregateV1` | `RunSessionAggregate` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionTimeV1.cs` |
| `ShopScreenActionStatusV1` | `ShopScreenActionStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Shops/Presentation/ShopScreenPresentationV1.cs` |
| `ShopScreenFeedbackKindV1` | `ShopScreenFeedbackKind` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Shops/Presentation/ShopScreenPresentationV1.cs` |
| `ShopScreenRouteV1` | `ShopScreenRoute` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Shops/Presentation/ShopScreenPresentationV1.cs` |
| `IShopScreenRouteAdapterV1` | `IShopScreenRoute` | interface | Adapter | yes | `Assets/ShooterMover/Runtime/Application/Shops/Presentation/ShopScreenPresentationV1.cs` |
| `ShopScreenPurchaseInputV1` | `ShopScreenPurchaseInput` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Shops/Presentation/ShopScreenPresentationV1.cs` |
| `ShopScreenStockCardV1` | `ShopScreenStockCard` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Shops/Presentation/ShopScreenPresentationV1.cs` |
| `ShopScreenProjectionV1` | `ShopScreen` | class | Projection | yes | `Assets/ShooterMover/Runtime/Application/Shops/Presentation/ShopScreenPresentationV1.cs` |
| `ShopScreenActionResultV1` | `ShopScreenActionResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Shops/Presentation/ShopScreenPresentationV1.cs` |
| `ShopScreenRouteResultV1` | `ShopScreenRouteResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Shops/Presentation/ShopScreenPresentationV1.cs` |
| `ShopScreenSessionV1` | `ShopScreenSession` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Shops/Presentation/ShopScreenSessionV1.Projection.cs` |
| `ShopScreenSessionV1` | `ShopScreenSession` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Shops/Presentation/ShopScreenSessionV1.cs` |
| `ShopRuntimeServiceV1` | `Shop` | class | Runtime, Service | yes | `Assets/ShooterMover/Runtime/Application/Shops/ShopRuntimeServiceV1.RefreshPersistence.cs` |
| `ShopRuntimeServiceV1` | `Shop` | class | Runtime, Service | yes | `Assets/ShooterMover/Runtime/Application/Shops/ShopRuntimeServiceV1.State.cs` |
| `ShopRuntimeServiceV1` | `Shop` | class | Runtime, Service | yes | `Assets/ShooterMover/Runtime/Application/Shops/ShopRuntimeServiceV1.Transactions.cs` |
| `ShopRuntimeServiceV1` | `Shop` | class | Runtime, Service | yes | `Assets/ShooterMover/Runtime/Application/Shops/ShopRuntimeServiceV1.cs` |
| `RankedSkillsPersistenceResultV2` | `RankedSkillsPersistenceResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Skills/Presentation/RankedSkillsScreenSessionV2.cs` |
| `IRankedSkillsPersistencePortV2` | `IRankedSkillsPersistencePort` | interface | — | yes | `Assets/ShooterMover/Runtime/Application/Skills/Presentation/RankedSkillsScreenSessionV2.cs` |
| `RankedSkillsScreenSessionV2` | `RankedSkillsScreenSession` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Skills/Presentation/RankedSkillsScreenSessionV2.cs` |
| `SkillsScreenSkillStateV1` | `SkillsScreenSkillState` | enum | — | yes | `Assets/ShooterMover/Runtime/Application/Skills/Presentation/SkillsScreenPresentationV1.cs` |
| `SkillsScreenSkillProjectionV1` | `SkillsScreenSkill` | class | Projection | yes | `Assets/ShooterMover/Runtime/Application/Skills/Presentation/SkillsScreenPresentationV1.cs` |
| `SkillsScreenProjectionV1` | `SkillsScreen` | class | Projection | yes | `Assets/ShooterMover/Runtime/Application/Skills/Presentation/SkillsScreenPresentationV1.cs` |
| `SkillsScreenAllocationResultV1` | `SkillsScreenAllocationResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Skills/Presentation/SkillsScreenPresentationV1.cs` |
| `SkillsScreenBackResultV1` | `SkillsScreenBackResult` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Skills/Presentation/SkillsScreenPresentationV1.cs` |
| `SkillsScreenSessionV1` | `SkillsScreenSession` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Skills/Presentation/SkillsScreenPresentationV1.cs` |
| `ProductionWeaponCatalogueV1` | `WeaponCatalogue` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/ProductionWeaponCatalogueV1.Content.cs` |
| `ProductionWeaponMarkV1` | `WeaponMark` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/ProductionWeaponCatalogueV1.Contracts.cs` |
| `ProductionWeaponFamilyV1` | `WeaponFamily` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/ProductionWeaponCatalogueV1.Contracts.cs` |
| `ProductionWeaponCatalogueProjectionV1` | `WeaponCatalogue` | class | Production, Projection | yes | `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/ProductionWeaponCatalogueV1.Contracts.cs` |
| `ProductionWeaponCatalogueV1` | `WeaponCatalogue` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/ProductionWeaponCatalogueV1.EquipmentProjection.cs` |
| `ProductionWeaponCatalogueV1` | `WeaponCatalogue` | class | Production | yes | `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/ProductionWeaponCatalogueV1.FlatProjection.cs` |
| `WeaponCatalogCanonicalJson` | `WeaponCatalogJson` | class | Canonical | no | `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/WeaponCatalogCanonicalJson.cs` |
| `CanonicalWriter` | `Writer` | class | Canonical | no | `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/WeaponCatalogCanonicalJson.cs` |
| `AcceptedEmissionRuntimeAdapter` | `AcceptedEmission` | class | Runtime, Adapter | no | `Assets/ShooterMover/Runtime/Application/Weapons/Execution/AcceptedEmissionRuntimeAdapter.cs` |
| `AcceptedEmissionRuntimeAdapterStatus` | `AcceptedEmissionStatus` | enum | Runtime, Adapter | no | `Assets/ShooterMover/Runtime/Application/Weapons/Execution/AcceptedEmissionRuntimeAdapterContracts.cs` |
| `AcceptedEmissionRuntimeAdapterResult` | `AcceptedEmissionResult` | class | Runtime, Adapter | no | `Assets/ShooterMover/Runtime/Application/Weapons/Execution/AcceptedEmissionRuntimeAdapterContracts.cs` |
| `ProjectileExplosionResolutionAdapter` | `ProjectileExplosionResolution` | class | Adapter | no | `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponExplosionResolver.cs` |
| `WeaponCatalogRuntimeProfileResolver` | `WeaponCatalogProfileResolver` | class | Runtime | no | `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponProfileResolver.Runtime.cs` |
| `WeaponCatalogRuntimeProfileResolver` | `WeaponCatalogProfileResolver` | class | Runtime | no | `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponProfileResolver.Validation.cs` |
| `RuntimeReferenceWeaponDefinitionIdResolver` | `ReferenceWeaponDefinitionIdResolver` | class | Runtime | no | `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponProfileResolver.cs` |
| `WeaponRicochetRuntimeState` | `WeaponRicochetState` | class | Runtime | no | `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponRicochetRuntimeState.cs` |
| `WeaponArtReferenceProjectionV1` | `WeaponArtReference` | class | Projection | yes | `Assets/ShooterMover/Runtime/Application/Weapons/Presentation/WeaponArtReferenceResolverV1.cs` |
| `WeaponArtReferenceResolverV1` | `WeaponArtReferenceResolver` | class | — | yes | `Assets/ShooterMover/Runtime/Application/Weapons/Presentation/WeaponArtReferenceResolverV1.cs` |
| `BootstrapCompositionRoot` | `BootstrapRoot` | class | Composition | no | `Assets/ShooterMover/Runtime/Bootstrap/BootstrapCompositionRoot.cs` |
| `BootstrapSceneAdapter` | `BootstrapScene` | class | Adapter | no | `Assets/ShooterMover/Runtime/Bootstrap/Unity/BootstrapSceneAdapter.cs` |
| `WeaponEffectHitPolicyAdapterV1` | `WeaponEffectHitPolicy` | class | Adapter | yes | `Assets/ShooterMover/Runtime/CombatHitPolicy/CombatHitPolicyAdaptersV1.cs` |
| `CombatHitDamageCommandAdapterV1` | `CombatHitDamageCommand` | class | Adapter | yes | `Assets/ShooterMover/Runtime/CombatHitPolicy/CombatHitPolicyAdaptersV1.cs` |
| `CombatHitPropDamageCommandAdapterV1` | `CombatHitPropDamageCommand` | class | Adapter | yes | `Assets/ShooterMover/Runtime/CombatHitPolicy/CombatHitPolicyAdaptersV1.cs` |
| `CombatHitPolicyIdsV1` | `CombatHitPolicyIds` | class | — | yes | `Assets/ShooterMover/Runtime/CombatHitPolicy/CombatHitPolicyV1.cs` |
| `CombatHitCapabilityIdsV1` | `CombatHitCapabilityIds` | class | — | yes | `Assets/ShooterMover/Runtime/CombatHitPolicy/CombatHitPolicyV1.cs` |
| `CombatEffectGeometryKindV1` | `CombatEffectGeometryKind` | enum | — | yes | `Assets/ShooterMover/Runtime/CombatHitPolicy/CombatHitPolicyV1.cs` |
| `CombatWorldBlockerBehaviorV1` | `CombatWorldBlockerBehavior` | enum | — | yes | `Assets/ShooterMover/Runtime/CombatHitPolicy/CombatHitPolicyV1.cs` |
| `CombatHitContactKindV1` | `CombatHitContactKind` | enum | — | yes | `Assets/ShooterMover/Runtime/CombatHitPolicy/CombatHitPolicyV1.cs` |
| `CombatRelationRuleV1` | `CombatRelationRule` | enum | — | yes | `Assets/ShooterMover/Runtime/CombatHitPolicy/CombatHitPolicyV1.cs` |
| `CombatHitDispositionV1` | `CombatHitDisposition` | enum | — | yes | `Assets/ShooterMover/Runtime/CombatHitPolicy/CombatHitPolicyV1.cs` |
| `CombatHitRejectionCodeV1` | `CombatHitRejectionCode` | enum | — | yes | `Assets/ShooterMover/Runtime/CombatHitPolicy/CombatHitPolicyV1.cs` |
| `CombatActorSnapshotV1` | `CombatActorSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/CombatHitPolicy/CombatHitPolicyV1.cs` |
| `CombatEffectSnapshotV1` | `CombatEffectSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/CombatHitPolicy/CombatHitPolicyV1.cs` |
| `CombatHitContactV1` | `CombatHitContact` | class | — | yes | `Assets/ShooterMover/Runtime/CombatHitPolicy/CombatHitPolicyV1.cs` |
| `CombatHitTargetCountV1` | `CombatHitTargetCount` | class | — | yes | `Assets/ShooterMover/Runtime/CombatHitPolicy/CombatHitPolicyV1.cs` |
| `CombatHitHistorySnapshotV1` | `CombatHitHistorySnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/CombatHitPolicy/CombatHitPolicyV1.cs` |
| `CombatHitPolicyDefinitionV1` | `CombatHitPolicyDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/CombatHitPolicy/CombatHitPolicyV1.cs` |
| `CombatHitPolicyRegistryV1` | `CombatHitPolicyRegistry` | class | — | yes | `Assets/ShooterMover/Runtime/CombatHitPolicy/CombatHitPolicyV1.cs` |
| `CombatHitPolicyInputV1` | `CombatHitPolicyInput` | class | — | yes | `Assets/ShooterMover/Runtime/CombatHitPolicy/CombatHitPolicyV1.cs` |
| `CombatHitPolicyResultV1` | `CombatHitPolicyResult` | class | — | yes | `Assets/ShooterMover/Runtime/CombatHitPolicy/CombatHitPolicyV1.cs` |
| `ICombatHitPolicyV1` | `ICombatHitPolicy` | interface | — | yes | `Assets/ShooterMover/Runtime/CombatHitPolicy/CombatHitPolicyV1.cs` |
| `CombatHitPolicyV1` | `CombatHitPolicy` | class | — | yes | `Assets/ShooterMover/Runtime/CombatHitPolicy/CombatHitPolicyV1.cs` |
| `CombatActorSnapshotFactoryV1` | `CombatActorSnapshotFactory` | class | — | yes | `Assets/ShooterMover/Runtime/CombatHitPolicy/CombatHitPolicyV1.cs` |
| `ConditionRuntimeAuthorityV1` | `Condition` | class | Runtime, Authority | yes | `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionRuntimeAuthorityV1.cs` |
| `ParticipantRuntime` | `Participant` | class | Runtime | no | `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionRuntimeAuthorityV1.cs` |
| `ConditionRuntimeFactTypeIdsV1` | `ConditionFactTypeIds` | class | Runtime | yes | `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionRuntimeContractsV1.cs` |
| `IConditionRunClockV1` | `IConditionRunClock` | interface | — | yes | `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionRuntimeContractsV1.cs` |
| `IConditionRunLifecycleV1` | `IConditionRunLifecycle` | interface | — | yes | `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionRuntimeContractsV1.cs` |
| `ConditionRunLifecycleSnapshotV1` | `ConditionRunLifecycleSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionRuntimeContractsV1.cs` |
| `ConditionEffectRuntimeDefinitionV1` | `ConditionEffectDefinition` | class | Runtime | yes | `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionRuntimeContractsV1.cs` |
| `ConditionRuntimeParticipantDefinitionV1` | `ConditionParticipantDefinition` | class | Runtime | yes | `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionRuntimeContractsV1.cs` |
| `ConditionRunDefinitionV1` | `ConditionRunDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionRuntimeContractsV1.cs` |
| `AcceptedGameplayFactDeliveryV1` | `AcceptedGameplayFactDelivery` | class | — | yes | `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionRuntimeContractsV1.cs` |
| `ConditionObservedGameplayFactV1` | `ConditionObservedGameplayFact` | class | — | yes | `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionRuntimeContractsV1.cs` |
| `IAcceptedGameplayFactAdapterV1` | `IAcceptedGameplayFact` | interface | Adapter | yes | `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionRuntimeContractsV1.cs` |
| `AcceptedGameplayFactAdapterRegistryV1` | `AcceptedGameplayFactRegistry` | class | Adapter | yes | `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionRuntimeContractsV1.cs` |
| `ConditionFactIngestionStatusV1` | `ConditionFactIngestionStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionRuntimeContractsV1.cs` |
| `ConditionFactIngestionResultV1` | `ConditionFactIngestionResult` | class | — | yes | `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionRuntimeContractsV1.cs` |
| `ConditionParticipantSnapshotV1` | `ConditionParticipantSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionRuntimeContractsV1.cs` |
| `ConditionRuntimeSnapshotV1` | `ConditionSnapshot` | class | Runtime | yes | `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionRuntimeContractsV1.cs` |
| `ConditionRunReconstructionCommandV1` | `ConditionRunReconstructionCommand` | class | — | yes | `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionRuntimeContractsV1.cs` |
| `ConditionRunReconstructionResultV1` | `ConditionRunReconstructionResult` | class | — | yes | `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionRuntimeContractsV1.cs` |
| `FactWindowEffectFixtureV1` | `FactWindowEffectFixture` | class | — | yes | `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionRuntimeContractsV1.cs` |
| `ConditionRuntimeHashV1` | `ConditionHash` | class | Runtime | yes | `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionRuntimeContractsV1.cs` |
| `IAcceptedGameplayFactSourceFingerprintV1` | `IAcceptedGameplayFactSourceFingerprint` | interface | — | yes | `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionSourceFactFingerprintV1.cs` |
| `ConditionSourceFactFingerprintV1` | `ConditionSourceFactFingerprint` | class | — | yes | `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionSourceFactFingerprintV1.cs` |
| `EnemyDeathConditionFactAdapterV1` | `EnemyDeathConditionFact` | class | Adapter | yes | `Assets/ShooterMover/Runtime/ConditionRuntime/EnemyDeathConditionFactAdapterV1.cs` |
| `RuntimeSpawnIdentityInput` | `SpawnIdentityInput` | class | Runtime | no | `Assets/ShooterMover/Runtime/Contracts/Authoring/ObjectAuthoringContracts.cs` |
| `EconomyTransactionOperationV1` | `EconomyTransactionOperation` | enum | — | yes | `Assets/ShooterMover/Runtime/Contracts/Economy/EconomyTransactionContractsV1.cs` |
| `EconomyResourceKindV1` | `EconomyResourceKind` | enum | — | yes | `Assets/ShooterMover/Runtime/Contracts/Economy/EconomyTransactionContractsV1.cs` |
| `EconomyTransactionCommandV1` | `EconomyTransactionCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Economy/EconomyTransactionContractsV1.cs` |
| `EconomyTransactionStatusV1` | `EconomyTransactionStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Contracts/Economy/EconomyTransactionContractsV1.cs` |
| `EconomyTransactionIdentityComparisonV1` | `EconomyTransactionIdentityComparison` | enum | — | yes | `Assets/ShooterMover/Runtime/Contracts/Economy/EconomyTransactionContractsV1.cs` |
| `EconomyTransactionIdentityV1` | `EconomyTransactionIdentity` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Economy/EconomyTransactionContractsV1.cs` |
| `EconomyTransactionResultV1` | `EconomyTransactionResult` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Economy/EconomyTransactionContractsV1.cs` |
| `EncounterRuntimeIdentity` | `EncounterIdentity` | class | Runtime | no | `Assets/ShooterMover/Runtime/Contracts/Encounters/EncounterMessages.cs` |
| `HubRouteV1` | `HubRoute` | enum | — | yes | `Assets/ShooterMover/Runtime/Contracts/Flow/Session/PlayerRouteProfilePayloadV1.cs` |
| `PlayerRouteProfileValidationStatusV1` | `PlayerRouteProfileValidationStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Contracts/Flow/Session/PlayerRouteProfilePayloadV1.cs` |
| `PlayerRouteWeaponSlotEnvelopeV1` | `PlayerRouteWeaponSlotEnvelope` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Flow/Session/PlayerRouteProfilePayloadV1.cs` |
| `PlayerRouteProfileEnvelopeV1` | `PlayerRouteProfileEnvelope` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Flow/Session/PlayerRouteProfilePayloadV1.cs` |
| `PlayerRouteWeaponSlotV1` | `PlayerRouteWeaponSlot` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Flow/Session/PlayerRouteProfilePayloadV1.cs` |
| `PlayerRouteProfileValidationResultV1` | `PlayerRouteProfileValidationResult` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Flow/Session/PlayerRouteProfilePayloadV1.cs` |
| `PlayerRouteProfilePayloadV1` | `PlayerRouteProfilePayload` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Flow/Session/PlayerRouteProfilePayloadV1.cs` |
| `PlayerHoldingsImportResultV1` | `PlayerHoldingsImportResult` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Holdings/PlayerHoldingsAuthorityV1.cs` |
| `IPlayerHoldingsAuthorityV1` | `IPlayerHoldings` | interface | Authority | yes | `Assets/ShooterMover/Runtime/Contracts/Holdings/PlayerHoldingsAuthorityV1.cs` |
| `PlayerHoldingsMutationStatusV1` | `PlayerHoldingsMutationStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Contracts/Holdings/PlayerHoldingsContractsV1.cs` |
| `PlayerHoldingsImportStatusV1` | `PlayerHoldingsImportStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Contracts/Holdings/PlayerHoldingsContractsV1.cs` |
| `PlayerHoldingsCommandV1` | `PlayerHoldingsCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Holdings/PlayerHoldingsContractsV1.cs` |
| `PlayerHoldingsMutationResultV1` | `PlayerHoldingsMutationResult` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Holdings/PlayerHoldingsMutationResultV1.cs` |
| `PlayerHoldingsSnapshotV1` | `PlayerHoldingsSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Holdings/PlayerHoldingsSnapshotV1.cs` |
| `PlayerHoldingsTransactionRecordV1` | `PlayerHoldingsTransactionRecord` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Holdings/PlayerHoldingsTransactionRecordV1.cs` |
| `MissionRunCollectStrongboxCommandV1` | `MissionRunCollectStrongboxCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Results/MissionRunCommandsV1.cs` |
| `EndMissionRunCommandV1` | `EndMissionRunCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Results/MissionRunCommandsV1.cs` |
| `MissionRunPayloadV1` | `MissionRunPayload` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Results/MissionRunPayloadsV1.cs` |
| `MissionResultPayloadV1` | `MissionResultPayload` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Results/MissionRunPayloadsV1.cs` |
| `MissionRunAuthorityResultV1` | `MissionRunResult` | class | Authority | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Results/MissionRunPayloadsV1.cs` |
| `MissionRunCompletionStateV1` | `MissionRunCompletionState` | enum | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Results/MissionRunResultContractsV1.cs` |
| `MissionRunStrongboxStateV1` | `MissionRunStrongboxState` | enum | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Results/MissionRunResultContractsV1.cs` |
| `MissionRunAuthorityStatusV1` | `MissionRunStatus` | enum | Authority | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Results/MissionRunResultContractsV1.cs` |
| `MissionRunCanonicalV1` | `MissionRun` | class | Canonical | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Results/MissionRunResultContractsV1.cs` |
| `MissionRunStrongboxCollectionV1` | `MissionRunStrongboxCollection` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Results/MissionRunResultContractsV1.cs` |
| `MissionRunStrongboxResultV1` | `MissionRunStrongboxResult` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Results/MissionRunResultContractsV1.cs` |
| `AuthorableRoomDefinitionV1` | `AuthorableRoomDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/AuthorableRoomDefinitionV1.cs` |
| `AuthorableRoomGraphDefinitionV1` | `AuthorableRoomGraphDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/AuthorableRoomGraphDefinitionV1.cs` |
| `RoomAccessConditionKindV1` | `RoomAccessConditionKind` | enum | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAccessContractsV1.cs` |
| `RoomAccessOperationStatusV1` | `RoomAccessOperationStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAccessContractsV1.cs` |
| `RoomHoldingConsumeStatusV1` | `RoomHoldingConsumeStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAccessContractsV1.cs` |
| `RoomAccessConditionDefinitionV1` | `RoomAccessConditionDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAccessContractsV1.cs` |
| `RoomDoorAccessDefinitionV1` | `RoomDoorAccessDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAccessContractsV1.cs` |
| `RoomAccessDefinitionV1` | `RoomAccessDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAccessContractsV1.cs` |
| `RoomAccessFactSnapshotV1` | `RoomAccessFactSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAccessContractsV1.cs` |
| `RoomRunHoldingSnapshotV1` | `RoomRunHoldingSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAccessContractsV1.cs` |
| `RoomHoldingConsumeCommandV1` | `RoomHoldingConsumeCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAccessContractsV1.cs` |
| `RoomHoldingConsumeResultV1` | `RoomHoldingConsumeResult` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAccessContractsV1.cs` |
| `IRoomRunHoldingPortV1` | `IRoomRunHoldingPort` | interface | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAccessContractsV1.cs` |
| `IRoomAccessFactPortV1` | `IRoomAccessFactPort` | interface | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAccessContractsV1.cs` |
| `UnlockRoomDoorCommandV1` | `UnlockRoomDoorCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAccessContractsV1.cs` |
| `RoomDoorAccessProjectionV1` | `RoomDoorAccess` | class | Projection | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAccessContractsV1.cs` |
| `RoomAccessSnapshotV1` | `RoomAccessSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAccessContractsV1.cs` |
| `RoomAccessOperationResultV1` | `RoomAccessOperationResult` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAccessContractsV1.cs` |
| `RoomAccessReferenceKindV1` | `RoomAccessReferenceKind` | enum | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAccessReferenceCatalogV1.cs` |
| `RoomAccessReferenceSourceV1` | `RoomAccessReferenceSource` | enum | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAccessReferenceCatalogV1.cs` |
| `RoomAccessReferenceRegistrationV1` | `RoomAccessReferenceRegistration` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAccessReferenceCatalogV1.cs` |
| `IRoomAccessReferenceRegistryV1` | `IRoomAccessReferenceRegistry` | interface | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAccessReferenceCatalogV1.cs` |
| `RoomAccessReferenceCatalogV1` | `RoomAccessReferenceCatalog` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAccessReferenceCatalogV1.cs` |
| `RoomLivePlacementKindV1` | `RoomLivePlacementKind` | enum | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAuthoringPrimitivesV1.cs` |
| `RoomSpawnPointKindV1` | `RoomSpawnPointKind` | enum | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAuthoringPrimitivesV1.cs` |
| `RoomCompletionConditionKindV1` | `RoomCompletionConditionKind` | enum | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAuthoringPrimitivesV1.cs` |
| `RoomLiveLinkKindV1` | `RoomLiveLinkKind` | enum | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAuthoringPrimitivesV1.cs` |
| `RoomVector2V1` | `RoomVector2` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAuthoringPrimitivesV1.cs` |
| `RoomBoundsV1` | `RoomBounds` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAuthoringPrimitivesV1.cs` |
| `RoomSpawnPointDefinitionV1` | `RoomSpawnPointDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAuthoringPrimitivesV1.cs` |
| `RoomPlacedEntityDefinitionV1` | `RoomPlacedEntityDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAuthoringPrimitivesV1.cs` |
| `RoomDoorDefinitionV1` | `RoomDoorDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAuthoringPrimitivesV1.cs` |
| `RoomExitLinkDefinitionV1` | `RoomExitLinkDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAuthoringPrimitivesV1.cs` |
| `RoomCompletionConditionDefinitionV1` | `RoomCompletionConditionDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAuthoringPrimitivesV1.cs` |
| `RoomLiveJsonV1` | `RoomLiveJson` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAuthoringPrimitivesV1.cs` |
| `RoomAvailabilityStateV1` | `RoomAvailabilityState` | enum | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomGraphContractsV1.cs` |
| `RoomGraphOperationStatusV1` | `RoomGraphOperationStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomGraphContractsV1.cs` |
| `RoomGraphImportStatusV1` | `RoomGraphImportStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomGraphContractsV1.cs` |
| `RoomRuntimeStateV1` | `RoomState` | class | Runtime | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomGraphContractsV1.cs` |
| `RoomExitRuntimeStateV1` | `RoomExitState` | class | Runtime | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomGraphContractsV1.cs` |
| `RoomStateSnapshotV1` | `RoomStateSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomGraphContractsV1.cs` |
| `RoomExitStateSnapshotV1` | `RoomExitStateSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomGraphContractsV1.cs` |
| `RoomGraphSnapshotV1` | `RoomGraphSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomGraphContractsV1.cs` |
| `RoomGraphOperationResultV1` | `RoomGraphOperationResult` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomGraphContractsV1.cs` |
| `RoomGraphImportResultV1` | `RoomGraphImportResult` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomGraphContractsV1.cs` |
| `IRoomMissionLayoutV1` | `IRoomMissionLayout` | interface | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomGraphContractsV1.cs` |
| `RoomOccupantClearRoleV1` | `RoomOccupantClearRole` | enum | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomOccupancyContractsV1.cs` |
| `RoomRuntimeOperationStatusV1` | `RoomOperationStatus` | enum | Runtime | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomOccupancyContractsV1.cs` |
| `RoomOccupantRegistrationV1` | `RoomOccupantRegistration` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomOccupancyContractsV1.cs` |
| `RoomOccupantProjectionV1` | `RoomOccupant` | class | Projection | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomOccupancyContractsV1.cs` |
| `RoomExitEligibilityProjectionV1` | `RoomExitEligibility` | class | Projection | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomOccupancyContractsV1.cs` |
| `RoomOccupancyProjectionV1` | `RoomOccupancy` | class | Projection | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomOccupancyContractsV1.cs` |
| `RoomRuntimeProjectionV1` | `Room` | class | Runtime, Projection | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomOccupancyContractsV1.cs` |
| `RoomClearTransitionV1` | `RoomClearTransition` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomOccupancyContractsV1.cs` |
| `RegisterRoomOccupantsCommandV1` | `RegisterRoomOccupantsCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomOccupancyContractsV1.cs` |
| `ActivateRoomCommandV1` | `ActivateRoomCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomOccupancyContractsV1.cs` |
| `ReportRoomOccupantTerminalCommandV1` | `ReportRoomOccupantTerminalCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomOccupancyContractsV1.cs` |
| `RestartRoomRuntimeCommandV1` | `RestartRoomCommand` | class | Runtime | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomOccupancyContractsV1.cs` |
| `RoomRuntimeOperationResultV1` | `RoomOperationResult` | class | Runtime | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomOccupancyContractsV1.cs` |
| `IRoomRuntimeAuthorityV1` | `IRoom` | interface | Runtime, Authority | yes | `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomOccupancyContractsV1.cs` |
| `PlayerExperienceGrantStatusV1` | `PlayerExperienceGrantStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Contracts/Progression/Experience/PlayerExperienceContractsV1.cs` |
| `PlayerExperienceImportStatusV1` | `PlayerExperienceImportStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Contracts/Progression/Experience/PlayerExperienceContractsV1.cs` |
| `PlayerExperienceGrantRequestV1` | `PlayerExperienceGrantRequest` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Progression/Experience/PlayerExperienceContractsV1.cs` |
| `PlayerLevelUpFactV1` | `PlayerLevelUpFact` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Progression/Experience/PlayerExperienceContractsV1.cs` |
| `PlayerExperienceGrantSnapshotV1` | `PlayerExperienceGrantSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Progression/Experience/PlayerExperienceContractsV1.cs` |
| `PlayerExperienceSnapshotV1` | `PlayerExperienceSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Progression/Experience/PlayerExperienceContractsV1.cs` |
| `PlayerExperienceGrantFactV1` | `PlayerExperienceGrantFact` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Progression/Experience/PlayerExperienceContractsV1.cs` |
| `PlayerExperienceImportResultV1` | `PlayerExperienceImportResult` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Progression/Experience/PlayerExperienceContractsV1.cs` |
| `IPlayerExperienceAuthorityV1` | `IPlayerExperience` | interface | Authority | yes | `Assets/ShooterMover/Runtime/Contracts/Progression/Experience/PlayerExperienceContractsV1.cs` |
| `RewardGrantApplicationPayloadV1` | `RewardGrantApplicationPayload` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/Application/RewardApplicationCommandsV1.cs` |
| `RewardCommitCommandV1` | `RewardCommitCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/Application/RewardApplicationCommandsV1.cs` |
| `RewardProjectCommandV1` | `RewardProjectCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/Application/RewardApplicationCommandsV1.cs` |
| `RewardClaimCommandV1` | `RewardClaimCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/Application/RewardApplicationCommandsV1.cs` |
| `RewardRetryClaimCommandV1` | `RewardRetryClaimCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/Application/RewardApplicationCommandsV1.cs` |
| `RewardCancelCommandV1` | `RewardCancelCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/Application/RewardApplicationCommandsV1.cs` |
| `RewardChildGrantCommandV1` | `RewardChildGrantCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/Application/RewardApplicationCommandsV1.cs` |
| `RewardApplicationResultStatusV1` | `RewardApplicationResultStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/Application/RewardApplicationResultsV1.cs` |
| `RewardAuthorityAdmissionStatusV1` | `RewardAdmissionStatus` | enum | Authority | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/Application/RewardApplicationResultsV1.cs` |
| `RewardChildApplyStatusV1` | `RewardChildApplyStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/Application/RewardApplicationResultsV1.cs` |
| `RewardApplicationImportStatusV1` | `RewardApplicationImportStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/Application/RewardApplicationResultsV1.cs` |
| `RewardAuthorityPreflightFactV1` | `RewardPreflightFact` | class | Authority | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/Application/RewardApplicationResultsV1.cs` |
| `RewardAuthorityPreflightResultV1` | `RewardPreflightResult` | class | Authority | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/Application/RewardApplicationResultsV1.cs` |
| `RewardChildApplyResultV1` | `RewardChildApplyResult` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/Application/RewardApplicationResultsV1.cs` |
| `IRewardChildAuthorityV1` | `IRewardChild` | interface | Authority | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/Application/RewardApplicationResultsV1.cs` |
| `RewardChildApplicationSnapshotV1` | `RewardChildApplicationSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/Application/RewardApplicationResultsV1.cs` |
| `RewardCommitmentSnapshotV1` | `RewardCommitmentSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/Application/RewardApplicationResultsV1.cs` |
| `RewardApplicationSnapshotV1` | `RewardApplicationSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/Application/RewardApplicationResultsV1.cs` |
| `RewardApplicationResultV1` | `RewardApplicationResult` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/Application/RewardApplicationResultsV1.cs` |
| `RewardApplicationImportResultV1` | `RewardApplicationImportResult` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/Application/RewardApplicationResultsV1.cs` |
| `PersonalRewardGenerationStatusV1` | `PersonalRewardGenerationStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/Drops/PersonalRewardGenerationResultV1.cs` |
| `PersonalRewardDecisionV1` | `PersonalRewardDecision` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/Drops/PersonalRewardGenerationResultV1.cs` |
| `PersonalRewardGenerationResultV1` | `PersonalRewardGenerationResult` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/Drops/PersonalRewardGenerationResultV1.cs` |
| `RewardOperationRequestV1` | `RewardOperationRequest` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/RewardOperationContractsV1.cs` |
| `RewardOperationIdentityComparisonV1` | `RewardOperationIdentityComparison` | enum | — | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/RewardOperationContractsV1.cs` |
| `RewardOperationIdentityV1` | `RewardOperationIdentity` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/RewardOperationContractsV1.cs` |
| `RewardGrantV1` | `RewardGrant` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/RewardOperationContractsV1.cs` |
| `RewardResultDispositionV1` | `RewardResultDisposition` | enum | — | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/RewardOperationContractsV1.cs` |
| `RewardResultV1` | `RewardResult` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/RewardOperationContractsV1.cs` |
| `RewardTraceDecisionKindV1` | `RewardTraceDecisionKind` | enum | — | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/RewardOperationContractsV1.cs` |
| `RewardTraceEntryV1` | `RewardTraceEntry` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/RewardOperationContractsV1.cs` |
| `RewardTraceV1` | `RewardTrace` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/RewardOperationContractsV1.cs` |
| `RewardContractFormatV1` | `RewardContractFormat` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/RewardOperationContractsV1.cs` |
| `StrongboxOpeningRequestV1` | `StrongboxOpeningRequest` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/StrongboxOpeningContractsV1.cs` |
| `StrongboxOpeningStatusV1` | `StrongboxOpeningStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/StrongboxOpeningContractsV1.cs` |
| `StrongboxOpeningResultV1` | `StrongboxOpeningResult` | class | — | yes | `Assets/ShooterMover/Runtime/Contracts/Rewards/StrongboxOpeningContractsV1.cs` |
| `RoomProjectionReadStatus` | `RoomReadStatus` | enum | Projection | no | `Assets/ShooterMover/Runtime/Contracts/Rooms/RoomProjectionContracts.cs` |
| `RoomProjectionIdentity` | `RoomIdentity` | class | Projection | no | `Assets/ShooterMover/Runtime/Contracts/Rooms/RoomProjectionContracts.cs` |
| `RoomProjectionKey` | `RoomKey` | class | Projection | no | `Assets/ShooterMover/Runtime/Contracts/Rooms/RoomProjectionContracts.cs` |
| `RoomProjectionReadResult` | `RoomReadResult` | class | Projection | no | `Assets/ShooterMover/Runtime/Contracts/Rooms/RoomProjectionContracts.cs` |
| `IRoomProjectionStateReader` | `IRoomStateReader` | interface | Projection | no | `Assets/ShooterMover/Runtime/Contracts/Rooms/RoomProjectionContracts.cs` |
| `RoomProjectionServices` | `RoomServices` | class | Projection | no | `Assets/ShooterMover/Runtime/Contracts/Rooms/RoomProjectionContracts.cs` |
| `RoomProjectionLifecyclePhase` | `RoomLifecyclePhase` | enum | Projection | no | `Assets/ShooterMover/Runtime/Contracts/Rooms/RoomProjectionLifecycle.cs` |
| `RoomProjectionLifecycleOperation` | `RoomLifecycleOperation` | enum | Projection | no | `Assets/ShooterMover/Runtime/Contracts/Rooms/RoomProjectionLifecycle.cs` |
| `RoomProjectionTransitionKind` | `RoomTransitionKind` | enum | Projection | no | `Assets/ShooterMover/Runtime/Contracts/Rooms/RoomProjectionLifecycle.cs` |
| `RoomProjectionTransitionRejection` | `RoomTransitionRejection` | enum | Projection | no | `Assets/ShooterMover/Runtime/Contracts/Rooms/RoomProjectionLifecycle.cs` |
| `RoomProjectionLifecycle` | `RoomLifecycle` | class | Projection | no | `Assets/ShooterMover/Runtime/Contracts/Rooms/RoomProjectionLifecycle.cs` |
| `RoomProjectionTransition` | `RoomTransition` | class | Projection | no | `Assets/ShooterMover/Runtime/Contracts/Rooms/RoomProjectionLifecycle.cs` |
| `CriticalHitPolicyIdsV1` | `CriticalHitPolicyIds` | class | — | yes | `Assets/ShooterMover/Runtime/CriticalHits/CriticalHitResolutionV1.cs` |
| `CriticalHitPolicyDefinitionV1` | `CriticalHitPolicyDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/CriticalHits/CriticalHitResolutionV1.cs` |
| `CriticalHitPolicyRegistryV1` | `CriticalHitPolicyRegistry` | class | — | yes | `Assets/ShooterMover/Runtime/CriticalHits/CriticalHitResolutionV1.cs` |
| `CriticalHitEffectFactsV1` | `CriticalHitEffectFacts` | class | — | yes | `Assets/ShooterMover/Runtime/CriticalHits/CriticalHitResolutionV1.cs` |
| `CriticalHitResolutionStatusV1` | `CriticalHitResolutionStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/CriticalHits/CriticalHitResolutionV1.cs` |
| `CriticalHitRejectionCodeV1` | `CriticalHitRejectionCode` | enum | — | yes | `Assets/ShooterMover/Runtime/CriticalHits/CriticalHitResolutionV1.cs` |
| `CriticalHitResolutionCommandV1` | `CriticalHitResolutionCommand` | class | — | yes | `Assets/ShooterMover/Runtime/CriticalHits/CriticalHitResolutionV1.cs` |
| `CriticalHitPolicyApplicationV1` | `CriticalHitPolicyApplication` | class | — | yes | `Assets/ShooterMover/Runtime/CriticalHits/CriticalHitResolutionV1.cs` |
| `CriticalHitRollDomainV1` | `CriticalHitRollDomain` | class | — | yes | `Assets/ShooterMover/Runtime/CriticalHits/CriticalHitResolutionV1.cs` |
| `CriticalHitResolvedDamageV1` | `CriticalHitResolvedDamage` | class | — | yes | `Assets/ShooterMover/Runtime/CriticalHits/CriticalHitResolutionV1.cs` |
| `CriticalHitResolutionResultV1` | `CriticalHitResolutionResult` | class | — | yes | `Assets/ShooterMover/Runtime/CriticalHits/CriticalHitResolutionV1.cs` |
| `ICriticalHitResolutionAuthorityV1` | `ICriticalHitResolution` | interface | Authority | yes | `Assets/ShooterMover/Runtime/CriticalHits/CriticalHitResolutionV1.cs` |
| `CriticalHitResolutionAuthorityV1` | `CriticalHitResolution` | class | Authority | yes | `Assets/ShooterMover/Runtime/CriticalHits/CriticalHitResolutionV1.cs` |
| `CriticalHitDamageCommandAdapterV1` | `CriticalHitDamageCommand` | class | Adapter | yes | `Assets/ShooterMover/Runtime/CriticalHits/CriticalHitResolutionV1.cs` |
| `CriticalHitFingerprintV1` | `CriticalHitFingerprint` | class | — | yes | `Assets/ShooterMover/Runtime/CriticalHits/CriticalHitResolutionV1.cs` |
| `DerivedStatTargetIdsV1` | `DerivedStatTargetIds` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Characters/DerivedCharacterStatsV1.cs` |
| `DerivedStatSourcePrioritiesV1` | `DerivedStatSourcePriorities` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Characters/DerivedCharacterStatsV1.cs` |
| `DerivedStatRuleV1` | `DerivedStatRule` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Characters/DerivedCharacterStatsV1.cs` |
| `DerivedStatPolicyV1` | `DerivedStatPolicy` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Characters/DerivedCharacterStatsV1.cs` |
| `DerivedStatModifierSourceV1` | `DerivedStatModifierSource` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Characters/DerivedCharacterStatsV1.cs` |
| `CharacterBaseStatProfileV1` | `CharacterBaseStatProfile` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Characters/DerivedCharacterStatsV1.cs` |
| `DerivedCharacterStatInputV1` | `DerivedCharacterStatInput` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Characters/DerivedCharacterStatsV1.cs` |
| `RunCombatProfileInputV1` | `RunCombatProfileInput` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Characters/DerivedCharacterStatsV1.cs` |
| `DerivedCharacterStatsSnapshotV1` | `DerivedCharacterStatsSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Characters/DerivedCharacterStatsV1.cs` |
| `RunCombatProfileV1` | `RunCombatProfile` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Characters/DerivedCharacterStatsV1.cs` |
| `IDerivedCharacterStatComposerV1` | `IDerivedCharacterStatComposer` | interface | — | yes | `Assets/ShooterMover/Runtime/Domain/Characters/DerivedCharacterStatsV1.cs` |
| `DefaultDerivedCharacterStatComposerV1` | `DefaultDerivedCharacterStatComposer` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Characters/DerivedCharacterStatsV1.cs` |
| `DerivedStatFingerprintV1` | `DerivedStatFingerprint` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Characters/DerivedCharacterStatsV1.cs` |
| `CharacterSelectionCatalogStatusV1` | `CharacterSelectionCatalogStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Characters/Selection/CharacterSelectionCatalogV1.cs` |
| `CharacterSelectionCatalogResultV1` | `CharacterSelectionCatalogResult` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Characters/Selection/CharacterSelectionCatalogV1.cs` |
| `CharacterSelectionCatalogV1` | `CharacterSelectionCatalog` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Characters/Selection/CharacterSelectionCatalogV1.cs` |
| `CharacterClassKindV1` | `CharacterClassKind` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Characters/Selection/CharacterSelectionDefinitionsV1.cs` |
| `CharacterVisualMetadataV1` | `CharacterVisualMetadata` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Characters/Selection/CharacterSelectionDefinitionsV1.cs` |
| `CharacterSelectionDefinitionV1` | `CharacterSelectionDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Characters/Selection/CharacterSelectionDefinitionsV1.cs` |
| `CharacterClassProfileDefinitionV1` | `CharacterClassProfileDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Characters/Selection/CharacterSelectionDefinitionsV1.cs` |
| `WeaponRuntimeProfile` | `WeaponProfile` | class | Runtime | no | `Assets/ShooterMover/Runtime/Domain/Combat/WeaponRuntimeProfile.cs` |
| `WeaponRuntimeProfileValidator` | `WeaponProfileValidator` | class | Runtime | no | `Assets/ShooterMover/Runtime/Domain/Combat/WeaponRuntimeProfileValidator.cs` |
| `CraftingQualityPolicyKindV1` | `CraftingQualityPolicyKind` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Crafting/CraftingRecipeV1.cs` |
| `CraftingDelayVarianceV1` | `CraftingDelayVariance` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Crafting/CraftingRecipeV1.cs` |
| `CraftingWeightedDefinitionV1` | `CraftingWeightedDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Crafting/CraftingRecipeV1.cs` |
| `CraftingGeneratorPolicyV1` | `CraftingGeneratorPolicy` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Crafting/CraftingRecipeV1.cs` |
| `CraftingRecipeV1` | `CraftingRecipe` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Crafting/CraftingRecipeV1.cs` |
| `CraftingRecipeCatalogV1` | `CraftingRecipeCatalog` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Crafting/CraftingRecipeV1.cs` |
| `CraftingCanonicalV1` | `Crafting` | class | Canonical | yes | `Assets/ShooterMover/Runtime/Domain/Crafting/CraftingRecipeV1.cs` |
| `LedgerCanonicalText` | `LedgerText` | class | Canonical | no | `Assets/ShooterMover/Runtime/Domain/Economy/Ledger/IdempotentLedger.cs` |
| `MoneyWalletIdsV1` | `MoneyWalletIds` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Economy/Money/MoneyWalletModel.cs` |
| `ScrapMutationKindV1` | `ScrapMutationKind` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Economy/Scrap/ScrapWalletModelV1.cs` |
| `ScrapIdentityV1` | `ScrapIdentity` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Economy/Scrap/ScrapWalletModelV1.cs` |
| `ScrapProvenanceV1` | `ScrapProvenance` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Economy/Scrap/ScrapWalletModelV1.cs` |
| `ScrapTransactionCommandV1` | `ScrapTransactionCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Economy/Scrap/ScrapWalletModelV1.cs` |
| `ScrapLedgerPayloadV1` | `ScrapLedgerPayload` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Economy/Scrap/ScrapWalletModelV1.cs` |
| `ScrapSnapshotV1` | `ScrapSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Economy/Scrap/ScrapWalletModelV1.cs` |
| `ScrapFingerprintV1` | `ScrapFingerprint` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Economy/Scrap/ScrapWalletModelV1.cs` |
| `EnemyAttackDescriptorCompatibilityV1` | `EnemyAttackDescriptorCompatibility` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyAttackDescriptorCompatibilityV1.cs` |
| `EnemyAttackParameterKindsV1` | `EnemyAttackParameterKinds` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogModelV1.cs` |
| `EnemyCatalogRoomClearRoleV1` | `EnemyCatalogRoomClearRole` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogModelV1.cs` |
| `EnemySequenceAimPolicyV1` | `EnemySequenceAimPolicy` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogModelV1.cs` |
| `EnemyAttackInterruptionPolicyV1` | `EnemyAttackInterruptionPolicy` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogModelV1.cs` |
| `EnemyMeleeAimCommitPolicyV1` | `EnemyMeleeAimCommitPolicy` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogModelV1.cs` |
| `EnemyMeleeTerminalOnImpactPolicyV1` | `EnemyMeleeTerminalOnImpactPolicy` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogModelV1.cs` |
| `EnemyAttackCapabilityRegistrationV1` | `EnemyAttackCapabilityRegistration` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogModelV1.cs` |
| `IEnemyCatalogRegistryV1` | `IEnemyCatalogRegistry` | interface | — | yes | `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogModelV1.cs` |
| `EnemyCatalogRegistryV1` | `EnemyCatalogRegistry` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogModelV1.cs` |
| `EnemyLevelScalingProfileV1` | `EnemyLevelScalingProfile` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogModelV1.cs` |
| `EnemyAreaPayloadV1` | `EnemyAreaPayload` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogModelV1.cs` |
| `EnemyProjectilePayloadV1` | `EnemyProjectilePayload` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogModelV1.cs` |
| `EnemyShootingPatternV1` | `EnemyShootingPattern` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogModelV1.cs` |
| `EnemyMeleePatternV1` | `EnemyMeleePattern` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogModelV1.cs` |
| `EnemyProjectileAttackParametersV1` | `EnemyProjectileAttackParameters` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogModelV1.cs` |
| `EnemyAreaAttackParametersV1` | `EnemyAreaAttackParameters` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogModelV1.cs` |
| `EnemyMeleeAttackParametersV1` | `EnemyMeleeAttackParameters` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogModelV1.cs` |
| `EnemyAttackCapabilityDescriptorV1` | `EnemyAttackCapabilityDescriptor` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogModelV1.cs` |
| `EnemyDefinitionV1` | `EnemyDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogModelV1.cs` |
| `EnemyCatalogV1` | `EnemyCatalog` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogV1.cs` |
| `EnemyCatalogFingerprintV1` | `EnemyCatalogFingerprint` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogV1.cs` |
| `EnemyCatalogIssueV1` | `EnemyCatalogIssue` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogValidationTypesV1.cs` |
| `EnemyCatalogValidationResultV1` | `EnemyCatalogValidationResult` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogValidationTypesV1.cs` |
| `EnemyCatalogValidatorV1` | `EnemyCatalogValidator` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogValidatorAttacksV1.cs` |
| `EnemyCatalogValidatorV1` | `EnemyCatalogValidator` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogValidatorV1.cs` |
| `GeneratedEquipmentAugmentSignatureV1` | `GeneratedEquipmentAugmentSignature` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Equipment/GeneratedEquipmentAugmentSignatureV1.cs` |
| `AugmentUpgradeCanonicalV1` | `AugmentUpgrade` | class | Canonical | yes | `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgradeCanonicalV1.cs` |
| `AugmentUpgradeConfirmationV1` | `AugmentUpgradeConfirmation` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgradeConfirmationV1.cs` |
| `AugmentUpgradeRetryCommandV1` | `AugmentUpgradeRetryCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgradeConfirmationV1.cs` |
| `AugmentUpgradeIdentityContextV1` | `AugmentUpgradeIdentityContext` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgradeConfirmationV1.cs` |
| `AugmentUpgradeFactV1` | `AugmentUpgradeFact` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgradeFactV1.cs` |
| `AugmentUpgradeCostStatusV1` | `AugmentUpgradeCostStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgradeModelV1.cs` |
| `AugmentUpgradeQuoteStatusV1` | `AugmentUpgradeQuoteStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgradeModelV1.cs` |
| `AugmentUpgradeConfirmationStatusV1` | `AugmentUpgradeConfirmationStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgradeModelV1.cs` |
| `AugmentTierCostCurveV1` | `AugmentTierCostCurve` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgradeModelV1.cs` |
| `AugmentUpgradeCostPolicyV1` | `AugmentUpgradeCostPolicy` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgradeModelV1.cs` |
| `AugmentUpgradeQuoteRequestV1` | `AugmentUpgradeQuoteRequest` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgradeQuoteV1.cs` |
| `AugmentUpgradeQuoteV1` | `AugmentUpgradeQuote` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgradeQuoteV1.cs` |
| `AugmentUpgradeQuoteResultV1` | `AugmentUpgradeQuoteResult` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgradeQuoteV1.cs` |
| `HoldingsLedgerVocabularyV1` | `HoldingsLedgerVocabulary` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Holdings/PlayerHoldingsModelV1.cs` |
| `HoldingsEntryTypeIdsV1` | `HoldingsEntryTypeIds` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Holdings/PlayerHoldingsModelV1.cs` |
| `HoldingProvenanceV1` | `HoldingProvenance` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Holdings/PlayerHoldingsModelV1.cs` |
| `UniqueHoldingSnapshotV1` | `UniqueHoldingSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Holdings/PlayerHoldingsModelV1.cs` |
| `StackHoldingSnapshotV1` | `StackHoldingSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Holdings/PlayerHoldingsModelV1.cs` |
| `HoldingsCanonicalV1` | `Holdings` | class | Canonical | yes | `Assets/ShooterMover/Runtime/Domain/Holdings/PlayerHoldingsModelV1.cs` |
| `RoomInitialAvailabilityV1` | `RoomInitialAvailability` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Missions/Rooms/RoomGraphDefinitionV1.cs` |
| `RoomConnectionDirectionalityV1` | `RoomConnectionDirectionality` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Missions/Rooms/RoomGraphDefinitionV1.cs` |
| `RoomExitTypeV1` | `RoomExitType` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Missions/Rooms/RoomGraphDefinitionV1.cs` |
| `RoomGraphValidationCodeV1` | `RoomGraphValidationCode` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Missions/Rooms/RoomGraphDefinitionV1.cs` |
| `RoomGraphValidationIssueV1` | `RoomGraphValidationIssue` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Missions/Rooms/RoomGraphDefinitionV1.cs` |
| `RoomGraphValidationResultV1` | `RoomGraphValidationResult` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Missions/Rooms/RoomGraphDefinitionV1.cs` |
| `RoomDefinitionV1` | `RoomDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Missions/Rooms/RoomGraphDefinitionV1.cs` |
| `RoomEntryDefinitionV1` | `RoomEntryDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Missions/Rooms/RoomGraphDefinitionV1.cs` |
| `RoomExitDefinitionV1` | `RoomExitDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Missions/Rooms/RoomGraphDefinitionV1.cs` |
| `RoomDoorLinkDefinitionV1` | `RoomDoorLinkDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Missions/Rooms/RoomGraphDefinitionV1.cs` |
| `RoomConnectionDefinitionV1` | `RoomConnectionDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Missions/Rooms/RoomGraphDefinitionV1.cs` |
| `RoomGraphDefinitionV1` | `RoomGraphDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Missions/Rooms/RoomGraphDefinitionV1.cs` |
| `RoomGraphFormatV1` | `RoomGraphFormat` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Missions/Rooms/RoomGraphDefinitionV1.cs` |
| `EventModifierTargetIdsV1` | `EventModifierTargetIds` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/Events/SpecialEventModifierContextV1.cs` |
| `SpecialEventOverlapModeV1` | `SpecialEventOverlapMode` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/Events/SpecialEventModifierContextV1.cs` |
| `EventActivationWindowV1` | `EventActivationWindow` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/Events/SpecialEventModifierContextV1.cs` |
| `EventModifierDescriptorV1` | `EventModifierDescriptor` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/Events/SpecialEventModifierContextV1.cs` |
| `SpecialEventDefinitionV1` | `SpecialEventDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/Events/SpecialEventModifierContextV1.cs` |
| `SpecialEventCatalogV1` | `SpecialEventCatalog` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/Events/SpecialEventModifierContextV1.cs` |
| `ActiveEventDescriptorV1` | `ActiveEventDescriptor` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/Events/SpecialEventModifierContextV1.cs` |
| `ActiveEventModifierSnapshotV1` | `ActiveEventModifierSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/Events/SpecialEventModifierContextV1.cs` |
| `FrozenEventModifierContextV1` | `FrozenEventModifierContext` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/Events/SpecialEventModifierContextV1.cs` |
| `SpecialEventConflictV1` | `SpecialEventConflict` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/Events/SpecialEventModifierContextV1.cs` |
| `EventModifierCanonicalV1` | `EventModifier` | class | Canonical | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/Events/SpecialEventModifierContextV1.cs` |
| `RuntimeModifierOperationV1` | `ModifierOperation` | enum | Runtime | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/RuntimeModifierFoundationV1.cs` |
| `RuntimeModifierDefinitionV1` | `ModifierDefinition` | class | Runtime | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/RuntimeModifierFoundationV1.cs` |
| `RuntimeModifierEvaluationV1` | `ModifierEvaluation` | class | Runtime | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/RuntimeModifierFoundationV1.cs` |
| `RuntimeModifierSnapshotV1` | `ModifierSnapshot` | class | Runtime | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/RuntimeModifierFoundationV1.cs` |
| `FactWindowConditionDefinitionV1` | `FactWindowConditionDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/RuntimeModifierFoundationV1.cs` |
| `RuntimeModifierFingerprintV1` | `ModifierFingerprint` | class | Runtime | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/RuntimeModifierFoundationV1.cs` |
| `ActiveStatusEffectSnapshotV1` | `ActiveStatusEffectSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/ActiveStatusEffectSnapshotV1.cs` |
| `ActiveStatusEffectStackSnapshotV1` | `ActiveStatusEffectStackSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/ActiveStatusEffectStackSnapshotV1.cs` |
| `StatusEffectCommandResultV1` | `StatusEffectCommandResult` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectCommandResultV1.cs` |
| `StatusEffectStackingPolicyV1` | `StatusEffectStackingPolicy` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectDefinitionsV1.cs` |
| `StatusEffectDefinitionV1` | `StatusEffectDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectDefinitionsV1.cs` |
| `StatusEffectCatalogV1` | `StatusEffectCatalog` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectDefinitionsV1.cs` |
| `StatusEffectCommandV1` | `StatusEffectCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectDefinitionsV1.cs` |
| `ApplyStatusEffectCommandV1` | `ApplyStatusEffectCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectDefinitionsV1.cs` |
| `AdvanceStatusEffectTickCommandV1` | `AdvanceStatusEffectTickCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectDefinitionsV1.cs` |
| `DispelStatusEffectsCommandV1` | `DispelStatusEffectsCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectDefinitionsV1.cs` |
| `RestartStatusEffectLifecycleCommandV1` | `RestartStatusEffectLifecycleCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectDefinitionsV1.cs` |
| `StatusEffectCommandCanonicalV1` | `StatusEffectCommand` | class | Canonical | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectDefinitionsV1.cs` |
| `StatusEffectFingerprintV1` | `StatusEffectFingerprint` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectFingerprintV1.cs` |
| `StatusEffectReplayRecordSnapshotV1` | `StatusEffectReplayRecordSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectReplaySnapshotsV1.cs` |
| `StatusEffectAuthoritySnapshotV1` | `StatusEffectSnapshot` | class | Authority | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectReplaySnapshotsV1.cs` |
| `StatusEffectCommandStatusV1` | `StatusEffectCommandStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectResultEnumsV1.cs` |
| `StatusEffectCommandActionV1` | `StatusEffectCommandAction` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectResultEnumsV1.cs` |
| `StatusEffectStateSnapshotV1` | `StatusEffectStateSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectStateSnapshotV1.cs` |
| `SaveComponentSnapshotV1` | `SaveComponentSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Persistence/Accounts/PlayerAccountSnapshotV1.cs` |
| `CharacterInstanceSnapshotV1` | `CharacterInstanceSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Persistence/Accounts/PlayerAccountSnapshotV1.cs` |
| `PlayerAccountSnapshotV1` | `PlayerAccountSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Persistence/Accounts/PlayerAccountSnapshotV1.cs` |
| `PlayerAccountSnapshotFingerprintV1` | `PlayerAccountSnapshotFingerprint` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Persistence/Accounts/PlayerAccountSnapshotV1.cs` |
| `PlayerExperienceIdsV1` | `PlayerExperienceIds` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Progression/Experience/PlayerExperienceModelV1.cs` |
| `PlayerExperienceCurveV1` | `PlayerExperienceCurve` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Progression/Experience/PlayerExperienceModelV1.cs` |
| `PlayerExperienceStateV1` | `PlayerExperienceState` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Progression/Experience/PlayerExperienceModelV1.cs` |
| `PlayerExperienceFormatV1` | `PlayerExperienceFormat` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Progression/Experience/PlayerExperienceModelV1.cs` |
| `SkillModifierKindV2` | `SkillModifierKind` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Progression/Skills/RankedSkillFoundationV2.cs` |
| `SkillEffectDescriptorV2` | `SkillEffectDescriptor` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Progression/Skills/RankedSkillFoundationV2.cs` |
| `SkillRankMilestoneV2` | `SkillRankMilestone` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Progression/Skills/RankedSkillFoundationV2.cs` |
| `SkillClassOverrideV2` | `SkillClassOverride` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Progression/Skills/RankedSkillFoundationV2.cs` |
| `RankedSkillDefinitionV2` | `RankedSkillDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Progression/Skills/RankedSkillFoundationV2.cs` |
| `SkillSynergyRequirementV2` | `SkillSynergyRequirement` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Progression/Skills/RankedSkillFoundationV2.cs` |
| `SkillSynergyDefinitionV2` | `SkillSynergyDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Progression/Skills/RankedSkillFoundationV2.cs` |
| `RankedSkillCatalogV2` | `RankedSkillCatalog` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Progression/Skills/RankedSkillFoundationV2.cs` |
| `SkillFingerprintV2` | `SkillFingerprint` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Progression/Skills/RankedSkillFoundationV2.cs` |
| `RankedSkillAllocationSnapshotV2` | `RankedSkillAllocationSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Progression/Skills/RankedSkillFoundationV2.cs` |
| `SkillEffectContributionV2` | `SkillEffectContribution` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Progression/Skills/RankedSkillFoundationV2.cs` |
| `SkillEffectSnapshotV2` | `SkillEffectSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Progression/Skills/RankedSkillFoundationV2.cs` |
| `SkillEffectProjectorV2` | `SkillEffectProjector` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Progression/Skills/RankedSkillFoundationV2.cs` |
| `RankedSkillSampleCatalogV2` | `RankedSkillSampleCatalog` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Progression/Skills/RankedSkillFoundationV2.cs` |
| `SkillPrerequisiteV1` | `SkillPrerequisite` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Progression/Skills/SkillProgressionV1.cs` |
| `SkillCategoryInvestmentRequirementV1` | `SkillCategoryInvestmentRequirement` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Progression/Skills/SkillProgressionV1.cs` |
| `SkillDefinitionV1` | `SkillDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Progression/Skills/SkillProgressionV1.cs` |
| `SkillTreeDefinitionV1` | `SkillTreeDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Progression/Skills/SkillProgressionV1.cs` |
| `SkillCategoryKeyV1` | `SkillCategoryKey` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Progression/Skills/SkillProgressionV1.cs` |
| `SkillCatalogV1` | `SkillCatalog` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Progression/Skills/SkillProgressionV1.cs` |
| `SkillCategoryInvestmentV1` | `SkillCategoryInvestment` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Progression/Skills/SkillProgressionV1.cs` |
| `SkillProgressionSnapshotV1` | `SkillProgressionSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Progression/Skills/SkillProgressionV1.cs` |
| `SkillMutationStatusV1` | `SkillMutationStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Progression/Skills/SkillProgressionV1.cs` |
| `SkillRejectionReasonV1` | `SkillRejectionReason` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Progression/Skills/SkillProgressionV1.cs` |
| `SkillMutationFactV1` | `SkillMutationFact` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Progression/Skills/SkillProgressionV1.cs` |
| `PropDestructibilityModeV1` | `PropDestructibilityMode` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Props/PropCapabilitiesV1.cs` |
| `PropDamageAlignmentV1` | `PropDamageAlignment` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Props/PropCapabilitiesV1.cs` |
| `PropCapabilityIdsV1` | `PropCapabilityIds` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Props/PropCapabilitiesV1.cs` |
| `PropCapabilityV1` | `PropCapability` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Props/PropCapabilitiesV1.cs` |
| `PropCapabilitiesV1` | `PropCapabilities` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Props/PropCapabilitiesV1.cs` |
| `PropCapabilityRegistryV1` | `PropCapabilityRegistry` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Props/PropCapabilityRegistryV1.cs` |
| `PropDefinitionV1` | `PropDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Props/PropCatalogV1.cs` |
| `PropCatalogV1` | `PropCatalog` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Props/PropCatalogV1.cs` |
| `PropPlacementV1` | `PropPlacement` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Props/PropRuntimeContractsV1.cs` |
| `PropDamageEligibilityContextV1` | `PropDamageEligibilityContext` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Props/PropRuntimeContractsV1.cs` |
| `IPropDamageEligibilityPolicyV1` | `IPropDamageEligibilityPolicy` | interface | — | yes | `Assets/ShooterMover/Runtime/Domain/Props/PropRuntimeContractsV1.cs` |
| `PropDamageCommandV1` | `PropDamageCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Props/PropRuntimeContractsV1.cs` |
| `PropDamageStatusV1` | `PropDamageStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Props/PropRuntimeContractsV1.cs` |
| `PropFactKindIdsV1` | `PropFactKindIds` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Props/PropRuntimeContractsV1.cs` |
| `PropFactIdentityV1` | `PropFactIdentity` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Props/PropRuntimeContractsV1.cs` |
| `PropTerminalFactV1` | `PropTerminalFact` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Props/PropRuntimeContractsV1.cs` |
| `PropTriggeredFactV1` | `PropTriggeredFact` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Props/PropRuntimeContractsV1.cs` |
| `PropFactBatchV1` | `PropFactBatch` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Props/PropRuntimeContractsV1.cs` |
| `PropRuntimeSnapshotV1` | `PropSnapshot` | class | Runtime | yes | `Assets/ShooterMover/Runtime/Domain/Props/PropRuntimeContractsV1.cs` |
| `PropDamageResultV1` | `PropDamageResult` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Props/PropRuntimeContractsV1.cs` |
| `PropInteractionCommandV1` | `PropInteractionCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Props/PropRuntimeContractsV1.cs` |
| `PropInteractionStatusV1` | `PropInteractionStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Props/PropRuntimeContractsV1.cs` |
| `PropInteractionResultV1` | `PropInteractionResult` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Props/PropRuntimeContractsV1.cs` |
| `PropRuntimeCreationStatusV1` | `PropCreationStatus` | enum | Runtime | yes | `Assets/ShooterMover/Runtime/Domain/Props/PropRuntimeFactoryV1.cs` |
| `PropRuntimeCreationResultV1` | `PropCreationResult` | class | Runtime | yes | `Assets/ShooterMover/Runtime/Domain/Props/PropRuntimeFactoryV1.cs` |
| `IPropRuntimeFactoryV1` | `IPropFactory` | interface | Runtime | yes | `Assets/ShooterMover/Runtime/Domain/Props/PropRuntimeFactoryV1.cs` |
| `PropRuntimeFactoryV1` | `PropFactory` | class | Runtime | yes | `Assets/ShooterMover/Runtime/Domain/Props/PropRuntimeFactoryV1.cs` |
| `PropFingerprintV1` | `PropFingerprint` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Props/PropRuntimeFactoryV1.cs` |
| `PropRuntimeV1` | `Prop` | class | Runtime | yes | `Assets/ShooterMover/Runtime/Domain/Props/PropRuntimeV1.cs` |
| `RewardCommitmentStateV1` | `RewardCommitmentState` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Application/RewardApplicationModelV1.cs` |
| `RewardChildResolutionStateV1` | `RewardChildResolutionState` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Application/RewardApplicationModelV1.cs` |
| `RewardApplicationCanonicalV1` | `RewardApplication` | class | Canonical | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Application/RewardApplicationModelV1.cs` |
| `ParticipantDropPacingStateV1` | `ParticipantDropPacingState` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/ParticipantDropPacingStateV1.cs` |
| `PersonalRewardRollContextV1` | `PersonalRewardRollContext` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/PersonalRewardRollContextV1.cs` |
| `RewardOutcomeDispositionV1` | `RewardOutcomeDisposition` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RewardOutcomeV1.cs` |
| `RewardOutcomeV1` | `RewardOutcome` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RewardOutcomeV1.cs` |
| `RewardProfileOverrideOperationV1` | `RewardProfileOverrideOperation` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RewardProfileOverrideV1.cs` |
| `RewardProfileOverrideV1` | `RewardProfileOverride` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RewardProfileOverrideV1.cs` |
| `RewardProfileResolutionV1` | `RewardProfileResolution` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RewardProfileResolutionV1.cs` |
| `RewardRollGroupBehaviorV1` | `RewardRollGroupBehavior` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RewardRollGroupV1.cs` |
| `RewardBoxPacingModeV1` | `RewardBoxPacingMode` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RewardRollGroupV1.cs` |
| `RewardRollGroupV1` | `RewardRollGroup` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RewardRollGroupV1.cs` |
| `RewardSourceProfileV1` | `RewardSourceProfile` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RewardSourceProfileV1.cs` |
| `DropSaturationBandV1` | `DropSaturationBand` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RunDropPacingPolicyV1.cs` |
| `RunDropPacingPolicyV1` | `RunDropPacingPolicy` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RunDropPacingPolicyV1.cs` |
| `StrongboxTierWeightV1` | `StrongboxTierWeight` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/StrongboxTierSelectionProfileV1.cs` |
| `StrongboxTierContextModifierV1` | `StrongboxTierContextModifier` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/StrongboxTierSelectionProfileV1.cs` |
| `StrongboxTierSelectionProfileV1` | `StrongboxTierSelectionProfile` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/StrongboxTierSelectionProfileV1.cs` |
| `RewardGenerationStatusV1` | `RewardGenerationStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Generation/RewardGenerationPolicyV1.cs` |
| `RewardGenerationTraceDecisionV1` | `RewardGenerationTraceDecision` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Generation/RewardGenerationPolicyV1.cs` |
| `EquipmentGenerationCandidateV1` | `EquipmentGenerationCandidate` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Generation/RewardGenerationPolicyV1.cs` |
| `EquipmentQualityCandidateV1` | `EquipmentQualityCandidate` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Generation/RewardGenerationPolicyV1.cs` |
| `AugmentGenerationCandidateV1` | `AugmentGenerationCandidate` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Generation/RewardGenerationPolicyV1.cs` |
| `EquipmentGenerationPolicyV1` | `EquipmentGenerationPolicy` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Generation/RewardGenerationPolicyV1.cs` |
| `RewardGenerationTraceEntryV1` | `RewardGenerationTraceEntry` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Generation/RewardGenerationPolicyV1.cs` |
| `RewardGenerationTraceV1` | `RewardGenerationTrace` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Generation/RewardGenerationPolicyV1.cs` |
| `RewardGenerationFingerprintV1` | `RewardGenerationFingerprint` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Generation/RewardGenerationPolicyV1.cs` |
| `RewardGrantKindV1` | `RewardGrantKind` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Model/RewardGrantModelV1.cs` |
| `RewardScalingInputKindV1` | `RewardScalingInputKind` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Model/RewardGrantModelV1.cs` |
| `RewardQuantityRangeV1` | `RewardQuantityRange` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Model/RewardGrantModelV1.cs` |
| `RewardScalingInputDescriptorV1` | `RewardScalingInputDescriptor` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Model/RewardGrantModelV1.cs` |
| `RewardGrantSpecificationV1` | `RewardGrantSpecification` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Model/RewardGrantModelV1.cs` |
| `RewardModelFormatV1` | `RewardModelFormat` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Model/RewardGrantModelV1.cs` |
| `IndependentRewardRollV1` | `IndependentRewardRoll` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Model/RewardProfileV1.cs` |
| `WeightedRewardOutcomeKindV1` | `WeightedRewardOutcomeKind` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Model/RewardProfileV1.cs` |
| `WeightedRewardOutcomeV1` | `WeightedRewardOutcome` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Model/RewardProfileV1.cs` |
| `ExclusiveRewardGroupV1` | `ExclusiveRewardGroup` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Model/RewardProfileV1.cs` |
| `RewardProfileDispositionV1` | `RewardProfileDisposition` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Model/RewardProfileV1.cs` |
| `RewardProfileV1` | `RewardProfile` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Model/RewardProfileV1.cs` |
| `RewardSourceOverrideModeV1` | `RewardSourceOverrideMode` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Model/RewardProfileV1.cs` |
| `RewardSourceOverrideV1` | `RewardSourceOverride` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Model/RewardProfileV1.cs` |
| `StrongboxAugmentSignatureV1` | `StrongboxAugmentSignature` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxAugmentSignatureV1.cs` |
| `StrongboxDefinitionRarityIdsV1` | `StrongboxDefinitionRarityIds` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxDefinitionRarityIdsV1.cs` |
| `StrongboxDistanceWeightV1` | `StrongboxDistanceWeight` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxDistanceWeightV1.cs` |
| `StrongboxHybridLootPolicyV1` | `StrongboxHybridLootPolicy` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxHybridLootPolicyV1.cs` |
| `StrongboxHybridLootPolicyValidationV1` | `StrongboxHybridLootPolicyValidation` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxHybridLootPolicyValidationV1.cs` |
| `StrongboxHybridLootRandomV1` | `StrongboxHybridLootRandom` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxHybridLootRandomV1.cs` |
| `StrongboxInstanceLevelRollV1` | `StrongboxInstanceLevelRoll` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxInstanceLevelRollV1.cs` |
| `StrongboxRarityProfileV1` | `StrongboxRarityProfile` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxRarityProfileV1.cs` |
| `StrongboxTargetLevelRollV1` | `StrongboxTargetLevelRoll` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxTargetLevelRollV1.cs` |
| `StrongboxWeightedIntOutcomeV1` | `StrongboxWeightedIntOutcome` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxWeightedIntOutcomeV1.cs` |
| `StrongboxCanonicalV1` | `Strongbox` | class | Canonical | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/StrongboxModelsV1.cs` |
| `StrongboxRewardCountPolicyV1` | `StrongboxRewardCountPolicy` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/StrongboxModelsV1.cs` |
| `StrongboxMandatoryScrapPolicyV1` | `StrongboxMandatoryScrapPolicy` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/StrongboxModelsV1.cs` |
| `StrongboxDefinitionV1` | `StrongboxDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/StrongboxModelsV1.cs` |
| `StrongboxDefinitionCatalogV1` | `StrongboxDefinitionCatalog` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/StrongboxModelsV1.cs` |
| `StrongboxInstanceContextV1` | `StrongboxInstanceContext` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/StrongboxModelsV1.cs` |
| `StrongboxPowerBudgetPolicyV1` | `StrongboxPowerBudgetPolicy` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/StrongboxPowerBudgetV1.cs` |
| `StrongboxItemLevelRollV1` | `StrongboxItemLevelRoll` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/StrongboxPowerBudgetV1.cs` |
| `StrongboxEquipmentRollPlanV1` | `StrongboxEquipmentRollPlan` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/StrongboxPowerBudgetV1.cs` |
| `ShopProgressionContextPolicyV1` | `ShopProgressionContextPolicy` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Shops/ShopDefinitionV1.cs` |
| `ShopRefreshPolicyV1` | `ShopRefreshPolicy` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Shops/ShopDefinitionV1.cs` |
| `ShopPricingPolicyV1` | `ShopPricingPolicy` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Shops/ShopDefinitionV1.cs` |
| `ShopDefinitionV1` | `ShopDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Shops/ShopDefinitionV1.cs` |
| `ShopLockCapacityQueryV1` | `ShopLockCapacityQuery` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Shops/ShopDefinitionV1.cs` |
| `IShopLockCapacityExtensionV1` | `IShopLockCapacityExtension` | interface | — | yes | `Assets/ShooterMover/Runtime/Domain/Shops/ShopDefinitionV1.cs` |
| `ShopCanonicalV1` | `Shop` | class | Canonical | yes | `Assets/ShooterMover/Runtime/Domain/Shops/ShopDefinitionV1.cs` |
| `ShopInventoryOpenStatusV1` | `ShopInventoryOpenStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Shops/ShopRuntimeModelV1.cs` |
| `ShopStockEntryStateV1` | `ShopStockEntryState` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Shops/ShopRuntimeModelV1.cs` |
| `ShopPurchaseStatusV1` | `ShopPurchaseStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Shops/ShopRuntimeModelV1.cs` |
| `ShopRefreshStatusV1` | `ShopRefreshStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/Domain/Shops/ShopRuntimeModelV1.cs` |
| `ShopStockEntryV1` | `ShopStockEntry` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Shops/ShopRuntimeModelV1.cs` |
| `ShopInventoryViewV1` | `ShopInventoryView` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Shops/ShopRuntimeModelV1.cs` |
| `ShopInventoryOpenResultV1` | `ShopInventoryOpenResult` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Shops/ShopRuntimeModelV1.cs` |
| `ShopPurchaseCommandV1` | `ShopPurchaseCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Shops/ShopRuntimeModelV1.cs` |
| `ShopPurchaseFactV1` | `ShopPurchaseFact` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Shops/ShopRuntimeModelV1.cs` |
| `ShopRefreshCommandV1` | `ShopRefreshCommand` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Shops/ShopRuntimeModelV1.cs` |
| `ShopRefreshFactV1` | `ShopRefreshFact` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Shops/ShopRuntimeModelV1.cs` |
| `ShopRunInventorySnapshotV1` | `ShopRunInventorySnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/Domain/Shops/ShopRuntimeModelV1.cs` |
| `ShopRuntimeSnapshotV1` | `ShopSnapshot` | class | Runtime | yes | `Assets/ShooterMover/Runtime/Domain/Shops/ShopRuntimeModelV1.cs` |
| `CanonicalProjectileLaunchEffect` | `ProjectileLaunchEffect` | class | Canonical | no | `Assets/ShooterMover/Runtime/Domain/Weapons/Execution/WeaponEffectBatch.cs` |
| `WeaponRuntimeFiringProfile` | `WeaponFiringProfile` | class | Runtime | no | `Assets/ShooterMover/Runtime/Domain/Weapons/Execution/WeaponExecutionModel.cs` |
| `CanonicalWeaponOperationAvailabilityV1` | `WeaponOperationAvailability` | class | Canonical | yes | `Assets/ShooterMover/Runtime/Domain/Weapons/WeaponEquipmentInstance.cs` |
| `CanonicalWeaponSafetyPolicyV1` | `WeaponSafetyPolicy` | class | Canonical | yes | `Assets/ShooterMover/Runtime/Domain/Weapons/WeaponEquipmentInstance.cs` |
| `IEnemyAttackPatternCombatContextV1` | `IEnemyAttackPatternCombatContext` | interface | — | yes | `Assets/ShooterMover/Runtime/EnemyAttackPatternUnityIntegration/EnemyAttackPatternHitRouterV1.cs` |
| `IEnemyAttackPatternDamageChannelMapV1` | `IEnemyAttackPatternDamageChannelMap` | interface | — | yes | `Assets/ShooterMover/Runtime/EnemyAttackPatternUnityIntegration/EnemyAttackPatternHitRouterV1.cs` |
| `BuiltInEnemyAttackPatternDamageChannelMapV1` | `BuiltInEnemyAttackPatternDamageChannelMap` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyAttackPatternUnityIntegration/EnemyAttackPatternHitRouterV1.cs` |
| `EnemyAttackPatternHitRouteStatusV1` | `EnemyAttackPatternHitRouteStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/EnemyAttackPatternUnityIntegration/EnemyAttackPatternHitRouterV1.cs` |
| `EnemyAttackPatternHitRouteResultV1` | `EnemyAttackPatternHitRouteResult` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyAttackPatternUnityIntegration/EnemyAttackPatternHitRouterV1.cs` |
| `EnemyAttackPatternHitRouterV1` | `EnemyAttackPatternHitRouter` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyAttackPatternUnityIntegration/EnemyAttackPatternHitRouterV1.cs` |
| `BuiltInEnemyRuntimePolicyRegistryV1` | `BuiltInEnemyPolicyRegistry` | class | Runtime | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/BuiltInEnemyRuntimePoliciesV1.cs` |
| `EnemyAttackPatternAuthorityV1` | `EnemyAttackPattern` | class | Authority | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternAuthorityV1.cs` |
| `EnemyAttackPatternOperationStatusV1` | `EnemyAttackPatternOperationStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternContractsV1.cs` |
| `EnemyAttackPatternRejectionCodeV1` | `EnemyAttackPatternRejectionCode` | enum | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternContractsV1.cs` |
| `EnemyAttackSequenceIdentityV1` | `EnemyAttackSequenceIdentity` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternContractsV1.cs` |
| `EnemyAttackScheduledShotV1` | `EnemyAttackScheduledShot` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternContractsV1.cs` |
| `EnemyAttackScheduledProjectileV1` | `EnemyAttackScheduledProjectile` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternContractsV1.cs` |
| `EnemyAttackScheduledMeleeStrikeV1` | `EnemyAttackScheduledMeleeStrike` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternContractsV1.cs` |
| `EnemyAttackSequenceV1` | `EnemyAttackSequence` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternContractsV1.cs` |
| `EnemyAttackPatternDispatchRejectionCodeV1` | `EnemyAttackPatternDispatchRejectionCode` | enum | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternDispatchContractsV1.cs` |
| `EnemyAttackPatternDispatchResultV1` | `EnemyAttackPatternDispatchResult` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternDispatchContractsV1.cs` |
| `EnemyAttackSequenceDispatchV1` | `EnemyAttackSequenceDispatch` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternDispatchContractsV1.cs` |
| `IEnemyAttackPatternEffectPortV1` | `IEnemyAttackPatternEffectPort` | interface | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternDispatchContractsV1.cs` |
| `EnemyAttackEffectEmissionDispatchV1` | `EnemyAttackEffectEmissionDispatch` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternDispatchV1.cs` |
| `EnemyAttackEffectEmissionKindV1` | `EnemyAttackEffectEmissionKind` | enum | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternEmissionsV1.cs` |
| `EnemyAttackEffectEmissionV1` | `EnemyAttackEffectEmission` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternEmissionsV1.cs` |
| `EnemyAttackEffectEmissionProjectorV1` | `EnemyAttackEffectEmissionProjector` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternEmissionsV1.cs` |
| `EnemyAttackPatternFingerprintV1` | `EnemyAttackPatternFingerprint` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternFingerprintV1.cs` |
| `EnemyAttackPatternStartResultV1` | `EnemyAttackPatternStartResult` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternResultsV1.cs` |
| `EnemyAttackLifecycleCancellationCommandV1` | `EnemyAttackLifecycleCancellationCommand` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternResultsV1.cs` |
| `EnemyAttackSequenceCancellationFactV1` | `EnemyAttackSequenceCancellationFact` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternResultsV1.cs` |
| `EnemyAttackPatternCancellationResultV1` | `EnemyAttackPatternCancellationResult` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternResultsV1.cs` |
| `EnemyAttackPatternSchedulerV1` | `EnemyAttackPatternScheduler` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternSchedulerV1.cs` |
| `EnemyPlacementRuntimeInstanceV1` | `EnemyPlacementInstance` | class | Runtime | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyPlacementRuntimeAttackPatternAuthorityV1.cs` |
| `EnemyPlacementRuntimeInstanceV1` | `EnemyPlacementInstance` | class | Runtime | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyPlacementRuntimeCombatAuthorityV1.cs` |
| `EnemyPlacementRuntimeFactoryRejectionV1` | `EnemyPlacementFactoryRejection` | enum | Runtime | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyPlacementRuntimeFactoryV1.cs` |
| `EnemyPlacementRuntimeRequestV1` | `EnemyPlacementRequest` | class | Runtime | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyPlacementRuntimeFactoryV1.cs` |
| `EnemyRuntimeAttackBindingV1` | `EnemyAttackBinding` | class | Runtime | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyPlacementRuntimeFactoryV1.cs` |
| `EnemyPlacementRuntimeFactoryResultV1` | `EnemyPlacementFactoryResult` | class | Runtime | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyPlacementRuntimeFactoryV1.cs` |
| `EnemyRoomPlacementCompositionResultV1` | `EnemyRoomPlacementResult` | class | Composition | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyPlacementRuntimeFactoryV1.cs` |
| `EnemyPlacementRuntimeFactoryV1` | `EnemyPlacementFactory` | class | Runtime | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyPlacementRuntimeFactoryV1.cs` |
| `EnemyPlacementRuntimeInstanceV1` | `EnemyPlacementInstance` | class | Runtime | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyPlacementRuntimeInstanceV1.cs` |
| `EnemyPlacementRuntimeInstanceV1` | `EnemyPlacementInstance` | class | Runtime | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyPlacementRuntimeStateAuthorityV1.cs` |
| `EnemyRuntimeAuthorityFingerprintV1` | `EnemyFingerprint` | class | Runtime, Authority | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeAuthorityFingerprintV1.cs` |
| `EnemyRuntimeIdentityV1` | `EnemyIdentity` | class | Runtime | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` |
| `IEnemyRuntimeIdentityDeriverV1` | `IEnemyIdentityDeriver` | interface | Runtime | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` |
| `DeterministicEnemyRuntimeIdentityDeriverV1` | `DeterministicEnemyIdentityDeriver` | class | Runtime | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` |
| `EnemyDifficultyContextV1` | `EnemyDifficultyContext` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` |
| `EnemyDifficultyScalingConfigurationV1` | `EnemyDifficultyScalingConfiguration` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` |
| `EnemyDifficultyScalingV1` | `EnemyDifficultyScaling` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` |
| `IEnemyDifficultyScalingPolicyV1` | `IEnemyDifficultyScalingPolicy` | interface | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` |
| `ScalarEnemyDifficultyScalingPolicyV1` | `ScalarEnemyDifficultyScalingPolicy` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` |
| `EnemyPerceptionPolicyConfigurationV1` | `EnemyPerceptionPolicyConfiguration` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` |
| `IEnemyPerceptionRuntimeAdapterV1` | `IEnemyPerception` | interface | Runtime, Adapter | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` |
| `ValidatedEnemyPerceptionRuntimeAdapterV1` | `ValidatedEnemyPerception` | class | Runtime, Adapter | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` |
| `EnemyPerceptionRuntimeRegistrationV1` | `EnemyPerceptionRegistration` | class | Runtime | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` |
| `EnemyDifficultyRuntimeRegistrationV1` | `EnemyDifficultyRegistration` | class | Runtime | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` |
| `EnemyRuntimeOperationStatusV1` | `EnemyOperationStatus` | enum | Runtime | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` |
| `EnemyRuntimeRejectionCodeV1` | `EnemyRejectionCode` | enum | Runtime | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` |
| `EnemyPlacementDecisionV1` | `EnemyPlacementDecision` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` |
| `EnemyAttackExecutionContextV1` | `EnemyAttackExecutionContext` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` |
| `EnemyAttackExecutionResultV1` | `EnemyAttackExecutionResult` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` |
| `EnemyPlayerDamageRequestV1` | `EnemyPlayerDamageRequest` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` |
| `EnemyPlayerDamagePortResultV1` | `EnemyPlayerDamagePortResult` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` |
| `IEnemyAttackEffectPortV1` | `IEnemyAttackEffectPort` | interface | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` |
| `IEnemyPlayerDamagePortV1` | `IEnemyPlayerDamagePort` | interface | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` |
| `EnemyTerminalCollisionFactV1` | `EnemyTerminalCollisionFact` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` |
| `EnemyDeathFactV1` | `EnemyDeathFact` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` |
| `IEnemyRoomTerminalPortV1` | `IEnemyRoomTerminalPort` | interface | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` |
| `IEnemyExperienceFactConsumerV1` | `IEnemyExperienceFactConsumer` | interface | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` |
| `IEnemyDropFactConsumerV1` | `IEnemyDropFactConsumer` | interface | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` |
| `IEnemyKillStatFactConsumerV1` | `IEnemyKillStatFactConsumer` | interface | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` |
| `IEnemyTerminalCollisionAdapterV1` | `IEnemyTerminalCollision` | interface | Adapter | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` |
| `EnemyRuntimeDownstreamPortsV1` | `EnemyDownstreamPorts` | class | Runtime | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` |
| `NoOpEnemyRuntimePortV1` | `NoOpEnemyPort` | class | Runtime | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` |
| `EnemyRuntimeDamageCommandV1` | `EnemyDamageCommand` | class | Runtime | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` |
| `EnemyRuntimeDamageResultV1` | `EnemyDamageResult` | class | Runtime | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` |
| `EnemyAimCommitmentModeV1` | `EnemyAimCommitmentMode` | enum | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimePolicyRegistryV1.cs` |
| `EnemyAttackExecutionKindV1` | `EnemyAttackExecutionKind` | enum | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimePolicyRegistryV1.cs` |
| `EnemyMovementPolicyConfigurationV1` | `EnemyMovementPolicyConfiguration` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimePolicyRegistryV1.cs` |
| `EnemyDecisionPolicyConfigurationV1` | `EnemyDecisionPolicyConfiguration` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimePolicyRegistryV1.cs` |
| `EnemyTargetingAimPolicyConfigurationV1` | `EnemyTargetingAimPolicyConfiguration` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimePolicyRegistryV1.cs` |
| `EnemyAttackCapabilityConfigurationV1` | `EnemyAttackCapabilityConfiguration` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimePolicyRegistryV1.cs` |
| `EnemyMovementPolicyIntentV1` | `EnemyMovementPolicyIntent` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimePolicyRegistryV1.cs` |
| `IEnemyMovementEnvironmentQueryV1` | `IEnemyMovementEnvironmentQuery` | interface | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimePolicyRegistryV1.cs` |
| `EnemyMovementRealizationContextV1` | `EnemyMovementRealizationContext` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimePolicyRegistryV1.cs` |
| `EnemyTargetingAimContextV1` | `EnemyTargetingAimContext` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimePolicyRegistryV1.cs` |
| `EnemyMovementRealizationV1` | `EnemyMovementRealization` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimePolicyRegistryV1.cs` |
| `EnemyAttackExecutionRequestV1` | `EnemyAttackExecutionRequest` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimePolicyRegistryV1.cs` |
| `IEnemyDecisionRuntimePolicyV1` | `IEnemyDecisionPolicy` | interface | Runtime | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimePolicyRegistryV1.cs` |
| `IEnemyMovementRuntimePolicyV1` | `IEnemyMovementPolicy` | interface | Runtime | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimePolicyRegistryV1.cs` |
| `IEnemyMovementIntentRealizerV1` | `IEnemyMovementIntentRealizer` | interface | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimePolicyRegistryV1.cs` |
| `IEnemyTargetingAimPolicyV1` | `IEnemyTargetingAimPolicy` | interface | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimePolicyRegistryV1.cs` |
| `IEnemyAttackCapabilityAdapterV1` | `IEnemyAttackCapability` | interface | Adapter | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimePolicyRegistryV1.cs` |
| `EnemyMovementPolicyRegistrationV1` | `EnemyMovementPolicyRegistration` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimePolicyRegistryV1.cs` |
| `EnemyDecisionPolicyRegistrationV1` | `EnemyDecisionPolicyRegistration` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimePolicyRegistryV1.cs` |
| `EnemyTargetingAimPolicyRegistrationV1` | `EnemyTargetingAimPolicyRegistration` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimePolicyRegistryV1.cs` |
| `EnemyAttackCapabilityRuntimeRegistrationV1` | `EnemyAttackCapabilityRegistration` | class | Runtime | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimePolicyRegistryV1.cs` |
| `EnemyRuntimePolicyRegistryV1` | `EnemyPolicyRegistry` | class | Runtime | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimePolicyRegistryV1.cs` |
| `FoundationEnemyDecisionRuntimePolicyV1` | `FoundationEnemyDecisionPolicy` | class | Runtime | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimePolicyRegistryV1.cs` |
| `RangeAwareEnemyDecisionRuntimePolicyV1` | `RangeAwareEnemyDecisionPolicy` | class | Runtime | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimePolicyRegistryV1.cs` |
| `DecisionMovementRuntimePolicyV1` | `DecisionMovementPolicy` | class | Runtime | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimePolicyRegistryV1.cs` |
| `DirectEnemyMovementIntentRealizerV1` | `DirectEnemyMovementIntentRealizer` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimePolicyRegistryV1.cs` |
| `LockedEnemyTargetingAimPolicyV1` | `LockedEnemyTargetingAimPolicy` | class | — | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimePolicyRegistryV1.cs` |
| `RequestEnemyAttackCapabilityAdapterV1` | `RequestEnemyAttackCapability` | class | Adapter | yes | `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimePolicyRegistryV1.cs` |
| `EnemyDefinitionProjection` | `EnemyDefinition` | class | Projection | no | `Assets/ShooterMover/Runtime/GameplayEntities/Enemies/EnemyRuntimeProjection.cs` |
| `EnemyRuntimeProjection` | `Enemy` | class | Runtime, Projection | no | `Assets/ShooterMover/Runtime/GameplayEntities/Enemies/EnemyRuntimeProjection.cs` |
| `PlayerActorAuthority` | `PlayerActor` | class | Authority | no | `Assets/ShooterMover/Runtime/GameplayEntities/PlayerActorAuthority.cs` |
| `ProductionConditionBoundRunSessionStartSourceV1` | `ConditionBoundRunSessionStartSource` | class | Production | yes | `Assets/ShooterMover/Runtime/RunConditionIntegration/ProductionConditionBoundRunSessionStartSourceV1.cs` |
| `RunConditionParticipantSeedV1` | `RunConditionParticipantSeed` | class | — | yes | `Assets/ShooterMover/Runtime/RunConditionIntegration/RunConditionIntegrationV1.cs` |
| `IRunConditionParticipantSeedProviderV1` | `IRunConditionParticipantSeedProvider` | interface | — | yes | `Assets/ShooterMover/Runtime/RunConditionIntegration/RunConditionIntegrationV1.cs` |
| `SelectedPlayerRunConditionParticipantSeedProviderV1` | `SelectedPlayerRunConditionParticipantSeedProvider` | class | — | yes | `Assets/ShooterMover/Runtime/RunConditionIntegration/RunConditionIntegrationV1.cs` |
| `IRunConditionDefinitionProviderV1` | `IRunConditionDefinitionProvider` | interface | — | yes | `Assets/ShooterMover/Runtime/RunConditionIntegration/RunConditionIntegrationV1.cs` |
| `RunSessionNonConditionRuntimePortsV1` | `RunSessionNonConditionPorts` | class | Runtime | yes | `Assets/ShooterMover/Runtime/RunConditionIntegration/RunConditionIntegrationV1.cs` |
| `IRunSessionNonConditionRuntimePortFactoryV1` | `IRunSessionNonConditionPortFactory` | interface | Runtime | yes | `Assets/ShooterMover/Runtime/RunConditionIntegration/RunConditionIntegrationV1.cs` |
| `ProductionConditionBoundRunSessionRuntimePortFactoryV1` | `ConditionBoundRunSessionPortFactory` | class | Production, Runtime | yes | `Assets/ShooterMover/Runtime/RunConditionIntegration/RunConditionIntegrationV1.cs` |
| `ExistingConditionRuntimeRunPortV1` | `ExistingConditionRunPort` | class | Runtime | yes | `Assets/ShooterMover/Runtime/RunConditionIntegration/RunConditionIntegrationV1.cs` |
| `OwningRunClockV1` | `OwningRunClock` | class | — | yes | `Assets/ShooterMover/Runtime/RunConditionIntegration/RunConditionIntegrationV1.cs` |
| `OwningRunLifecycleV1` | `OwningRunLifecycle` | class | — | yes | `Assets/ShooterMover/Runtime/RunConditionIntegration/RunConditionIntegrationV1.cs` |
| `ConditionOwnedStatusEffectRunPortV1` | `ConditionOwnedStatusEffectRunPort` | class | — | yes | `Assets/ShooterMover/Runtime/RunConditionIntegration/RunConditionIntegrationV1.cs` |
| `RunMissionStrongboxSnapshotSourceResolverV1` | `RunMissionStrongboxSnapshotSourceResolver` | class | — | yes | `Assets/ShooterMover/Runtime/RunConditionIntegration/StrongboxPersistentRunIntegrationV1.cs` |
| `PersistentMissionResultRunPortV1` | `PersistentMissionResultRunPort` | class | — | yes | `Assets/ShooterMover/Runtime/RunConditionIntegration/StrongboxPersistentRunIntegrationV1.cs` |
| `StrongboxPersistentNonConditionRuntimePortFactoryV1` | `StrongboxPersistentNonConditionPortFactory` | class | Runtime | yes | `Assets/ShooterMover/Runtime/RunConditionIntegration/StrongboxPersistentRunIntegrationV1.cs` |
| `ExistingRunSessionPickupPortV1` | `ExistingRunSessionPickupPort` | class | — | yes | `Assets/ShooterMover/Runtime/RunPickups/ExistingRunSessionPickupPortV1.cs` |
| `RunPickupLiveCompositionV1` | `RunPickupLive` | class | Composition | yes | `Assets/ShooterMover/Runtime/RunPickups/ExistingRunSessionPickupPortV1.cs` |
| `RunLocalPickupAuthorityV1` | `RunLocalPickup` | class | Authority | yes | `Assets/ShooterMover/Runtime/RunPickups/RunLocalPickupAuthorityExportsV1.cs` |
| `RunLocalPickupAuthorityV1` | `RunLocalPickup` | class | Authority | yes | `Assets/ShooterMover/Runtime/RunPickups/RunLocalPickupAuthorityV1.cs` |
| `RunLocalPickupAuthorityV1` | `RunLocalPickup` | class | Authority | yes | `Assets/ShooterMover/Runtime/RunPickups/RunLocalPickupCollectionAuthorityV1.cs` |
| `RunPickupCollectionCommandV1` | `RunPickupCollectionCommand` | class | — | yes | `Assets/ShooterMover/Runtime/RunPickups/RunPickupCollectionContractsV1.cs` |
| `RunPickupCollectionFactV1` | `RunPickupCollectionFact` | class | — | yes | `Assets/ShooterMover/Runtime/RunPickups/RunPickupCollectionContractsV1.cs` |
| `RunPickupSessionRecordResultV1` | `RunPickupSessionRecordResult` | class | — | yes | `Assets/ShooterMover/Runtime/RunPickups/RunPickupCollectionContractsV1.cs` |
| `RunPickupCollectionResultV1` | `RunPickupCollectionResult` | class | — | yes | `Assets/ShooterMover/Runtime/RunPickups/RunPickupCollectionContractsV1.cs` |
| `RunPickupStateV1` | `RunPickupState` | enum | — | yes | `Assets/ShooterMover/Runtime/RunPickups/RunPickupContractsV1.cs` |
| `RunPickupRealizationStatusV1` | `RunPickupRealizationStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/RunPickups/RunPickupContractsV1.cs` |
| `RunPickupCollectionStatusV1` | `RunPickupCollectionStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/RunPickups/RunPickupContractsV1.cs` |
| `RunPickupSessionRecordStatusV1` | `RunPickupSessionRecordStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/RunPickups/RunPickupContractsV1.cs` |
| `RunPickupWorldSpawnContextV1` | `RunPickupWorldSpawnContext` | class | — | yes | `Assets/ShooterMover/Runtime/RunPickups/RunPickupContractsV1.cs` |
| `RunPickupGeneratedRewardV1` | `RunPickupGeneratedReward` | class | — | yes | `Assets/ShooterMover/Runtime/RunPickups/RunPickupContractsV1.cs` |
| `RunPickupGeneratedBatchV1` | `RunPickupGeneratedBatch` | class | — | yes | `Assets/ShooterMover/Runtime/RunPickups/RunPickupContractsV1.cs` |
| `IRunPickupSourcePositionPortV1` | `IRunPickupSourcePositionPort` | interface | — | yes | `Assets/ShooterMover/Runtime/RunPickups/RunPickupPortsAndIdentityV1.cs` |
| `RunPickupRunSessionContextV1` | `RunPickupRunSessionContext` | class | — | yes | `Assets/ShooterMover/Runtime/RunPickups/RunPickupPortsAndIdentityV1.cs` |
| `IRunPickupRunSessionPortV1` | `IRunPickupRunSessionPort` | interface | — | yes | `Assets/ShooterMover/Runtime/RunPickups/RunPickupPortsAndIdentityV1.cs` |
| `IRunPickupCollectionAuthorityV1` | `IRunPickupCollection` | interface | Authority | yes | `Assets/ShooterMover/Runtime/RunPickups/RunPickupPortsAndIdentityV1.cs` |
| `RunPickupIdentityV1` | `RunPickupIdentity` | class | — | yes | `Assets/ShooterMover/Runtime/RunPickups/RunPickupPortsAndIdentityV1.cs` |
| `RunPickupCanonicalV1` | `RunPickup` | class | Canonical | yes | `Assets/ShooterMover/Runtime/RunPickups/RunPickupPortsAndIdentityV1.cs` |
| `RunPickupSnapshotV1` | `RunPickupSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/RunPickups/RunPickupSnapshotContractsV1.cs` |
| `RunPickupRealizationResultV1` | `RunPickupRealizationResult` | class | — | yes | `Assets/ShooterMover/Runtime/RunPickups/RunPickupSnapshotContractsV1.cs` |
| `TerminalDropRunPickupAdapterV1` | `TerminalDropRunPickup` | class | Adapter | yes | `Assets/ShooterMover/Runtime/RunPickups/TerminalDropRunPickupAdapterV1.cs` |
| `PendingTerminalDropPickupConsumerV1` | `PendingTerminalDropPickupConsumer` | class | — | yes | `Assets/ShooterMover/Runtime/RunPickups/TerminalDropRunPickupAdapterV1.cs` |
| `EnemyTerminalSourceContextV1` | `EnemyTerminalSourceContext` | class | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/EnemyTerminalSourceContextAdapterV1.cs` |
| `IEnemyTerminalSourceContextResolverV1` | `IEnemyTerminalSourceContextResolver` | interface | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/EnemyTerminalSourceContextAdapterV1.cs` |
| `ContextResolvedEnemyDeathTerminalDropFactAdapterV1` | `ContextResolvedEnemyDeathTerminalDropFact` | class | Adapter | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/EnemyTerminalSourceContextAdapterV1.cs` |
| `PendingTerminalDropAdmissionStatusV1` | `PendingTerminalDropAdmissionStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/PendingTerminalDropAdmissionV1.cs` |
| `PendingTerminalDropAdmissionResultV1` | `PendingTerminalDropAdmissionResult` | class | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/PendingTerminalDropAdmissionV1.cs` |
| `IGeneratedTerminalDropPendingAdmissionV1` | `IGeneratedTerminalDropPendingAdmission` | interface | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/PendingTerminalDropAdmissionV1.cs` |
| `PendingTerminalDropAdmissionAuthorityV1` | `PendingTerminalDropAdmission` | class | Authority | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/PendingTerminalDropAdmissionV1.cs` |
| `IRunRewardProgressionContextProviderV1` | `IRunRewardProgressionContextProvider` | interface | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/RunSessionTerminalDropContextResolverV1.cs` |
| `RunSessionTerminalDropContextResolverV1` | `RunSessionTerminalDropContextResolver` | class | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/RunSessionTerminalDropContextResolverV1.cs` |
| `RunSessionTerminalRewardEnvironmentResolverV1` | `RunSessionTerminalRewardEnvironmentResolver` | class | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/RunSessionTerminalRewardEnvironmentResolverV1.cs` |
| `RunSessionTerminalRewardOverrideResolverV1` | `RunSessionTerminalRewardOverrideResolver` | class | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/RunSessionTerminalRewardOverrideResolverV1.cs` |
| `RunSessionTerminalRewardParticipantResolverV1` | `RunSessionTerminalRewardParticipantResolver` | class | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/RunSessionTerminalRewardParticipantResolverV1.cs` |
| `IPropTerminalDropFactConsumerV1` | `IPropTerminalDropFactConsumer` | interface | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropBindingCompositionV1.cs` |
| `IPendingTerminalDropAdmissionConsumerV1` | `IPendingTerminalDropAdmissionConsumer` | interface | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropBindingCompositionV1.cs` |
| `TerminalDropPendingPublicationPolicyV1` | `TerminalDropPendingPublicationPolicy` | class | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropBindingCompositionV1.cs` |
| `EnemyTerminalDropFactConsumerV1` | `EnemyTerminalDropFactConsumer` | class | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropBindingCompositionV1.cs` |
| `PropTerminalDropFactConsumerV1` | `PropTerminalDropFactConsumer` | class | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropBindingCompositionV1.cs` |
| `TerminalDropBindingCompositionV1` | `TerminalDropBinding` | class | Composition | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropBindingCompositionV1.cs` |
| `TerminalDropFactKindIdsV1` | `TerminalDropFactKindIds` | class | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropBindingContractsV1.cs` |
| `TerminalDropBindingStatusV1` | `TerminalDropBindingStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropBindingContractsV1.cs` |
| `TerminalDropRejectionCodeV1` | `TerminalDropRejectionCode` | enum | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropBindingContractsV1.cs` |
| `TerminalDropSourceFactV1` | `TerminalDropSourceFact` | class | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropBindingContractsV1.cs` |
| `TerminalDropAdaptationResultV1` | `TerminalDropAdaptationResult` | class | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropBindingContractsV1.cs` |
| `ITerminalDropFactAdapterV1` | `ITerminalDropFact` | interface | Adapter | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropBindingContractsV1.cs` |
| `IRewardProfileResolverV1` | `IRewardProfileResolver` | interface | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropBindingContractsV1.cs` |
| `TerminalDropRunGenerationContextV1` | `TerminalDropRunGenerationContext` | class | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropBindingContractsV1.cs` |
| `ITerminalDropRunContextResolverV1` | `ITerminalDropRunContextResolver` | interface | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropBindingContractsV1.cs` |
| `IRewardGenerationExecutorV1` | `IRewardGenerationExecutor` | interface | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropBindingContractsV1.cs` |
| `ExistingRewardGenerationExecutorV1` | `ExistingRewardGenerationExecutor` | class | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropBindingContractsV1.cs` |
| `GeneratedTerminalDropRewardV1` | `GeneratedTerminalDropReward` | class | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropBindingContractsV1.cs` |
| `GeneratedTerminalDropResultV1` | `GeneratedTerminalDropResult` | class | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropBindingContractsV1.cs` |
| `TerminalDropCanonicalV1` | `TerminalDrop` | class | Canonical | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropBindingContractsV1.cs` |
| `TerminalDropFactAdapterRegistryV1` | `TerminalDropFactRegistry` | class | Adapter | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropFactAdaptersV1.cs` |
| `RewardProfileCatalogResolverV1` | `RewardProfileCatalogResolver` | class | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropFactAdaptersV1.cs` |
| `EnemyDeathTerminalDropDefinitionProjectionV1` | `EnemyDeathTerminalDropDefinition` | class | Projection | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropFactAdaptersV1.cs` |
| `EnemyDeathTerminalDropDefinitionProjectionResultV1` | `EnemyDeathTerminalDropDefinitionResult` | class | Projection | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropFactAdaptersV1.cs` |
| `EnemyDeathTerminalDropDefinitionProjectorV1` | `EnemyDeathTerminalDropDefinitionProjector` | class | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropFactAdaptersV1.cs` |
| `PropTerminalSourceContextV1` | `PropTerminalSourceContext` | class | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropFactAdaptersV1.cs` |
| `IPropTerminalSourceContextResolverV1` | `IPropTerminalSourceContextResolver` | interface | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropFactAdaptersV1.cs` |
| `PropDestructionTerminalDropFactAdapterV1` | `PropDestructionTerminalDropFact` | class | Adapter | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropFactAdaptersV1.cs` |
| `TerminalDropGenerationAuthorityV1` | `TerminalDropGeneration` | class | Authority | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropGenerationAuthorityV1.cs` |
| `TerminalRewardPlacementContextV1` | `TerminalRewardPlacementContext` | class | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalPersonalRewardContractsV1.cs` |
| `TerminalRewardParticipantV1` | `TerminalRewardParticipant` | class | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalPersonalRewardContractsV1.cs` |
| `TerminalRewardEligibilityPolicyV1` | `TerminalRewardEligibilityPolicy` | class | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalPersonalRewardContractsV1.cs` |
| `TerminalRewardEnvironmentV1` | `TerminalRewardEnvironment` | class | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalPersonalRewardContractsV1.cs` |
| `TerminalRewardOverrideSetV1` | `TerminalRewardOverrideSet` | class | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalPersonalRewardContractsV1.cs` |
| `ITerminalRewardParticipantResolverV1` | `ITerminalRewardParticipantResolver` | interface | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalPersonalRewardContractsV1.cs` |
| `ITerminalRewardEnvironmentResolverV1` | `ITerminalRewardEnvironmentResolver` | interface | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalPersonalRewardContractsV1.cs` |
| `ITerminalRewardOverrideResolverV1` | `ITerminalRewardOverrideResolver` | interface | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalPersonalRewardContractsV1.cs` |
| `TerminalPersonalRewardBatchStatusV1` | `TerminalPersonalRewardBatchStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalPersonalRewardContractsV1.cs` |
| `TerminalPersonalRewardBatchV1` | `TerminalPersonalRewardBatch` | class | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalPersonalRewardContractsV1.cs` |
| `AttributedTerminalRewardParticipantResolverV1` | `AttributedTerminalRewardParticipantResolver` | class | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalPersonalRewardDefaultsV1.cs` |
| `DefaultTerminalRewardEnvironmentResolverV1` | `DefaultTerminalRewardEnvironmentResolver` | class | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalPersonalRewardDefaultsV1.cs` |
| `EmptyTerminalRewardOverrideResolverV1` | `EmptyTerminalRewardOverrideResolver` | class | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalPersonalRewardDefaultsV1.cs` |
| `TerminalPersonalRewardGenerationAuthorityV1` | `TerminalPersonalRewardGeneration` | class | Authority | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalPersonalRewardGenerationAuthorityV1.cs` |
| `TerminalPersonalRewardTransportAdapterV1` | `TerminalPersonalRewardTransport` | class | Adapter | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalPersonalRewardTransportAdapterV1.cs` |
| `ITerminalRewardPlacementFactV1` | `ITerminalRewardPlacementFact` | interface | — | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalRewardPlacementFactV1.cs` |
| `TerminalRunMinimumGenerationAuthorityV1` | `TerminalRunMinimumGeneration` | class | Authority | yes | `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalRunMinimumGenerationAuthorityV1.cs` |
| `ILevelDoorPackageAdapter` | `ILevelDoorPackage` | interface | Adapter | no | `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelDesignAuthoringModel.cs` |
| `LevelDoorSideV2` | `LevelDoorSide` | enum | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelGridAuthoringV2Model.cs` |
| `LevelDoorPlacementModeV2` | `LevelDoorPlacementMode` | enum | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelGridAuthoringV2Model.cs` |
| `LevelGridValidationPurposeV2` | `LevelGridValidationPurpose` | enum | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelGridAuthoringV2Model.cs` |
| `LevelGridProblemCodeV2` | `LevelGridProblemCode` | enum | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelGridAuthoringV2Model.cs` |
| `LevelGridRoomRecordV2` | `LevelGridRoomRecord` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelGridAuthoringV2Model.cs` |
| `LevelGridDoorRecordV2` | `LevelGridDoorRecord` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelGridAuthoringV2Model.cs` |
| `LevelGridConnectionRecordV2` | `LevelGridConnectionRecord` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelGridAuthoringV2Model.cs` |
| `LevelGridProblemV2` | `LevelGridProblem` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelGridAuthoringV2Model.cs` |
| `LevelGridValidationResultV2` | `LevelGridValidationResult` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelGridAuthoringV2Model.cs` |
| `LevelGridPlayableMetadataV2` | `LevelGridPlayableMetadata` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelGridPlayableMetadataV2.cs` |
| `LevelGridPlayableValidationV2` | `LevelGridPlayableValidation` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelGridPlayableValidationV2.cs` |
| `CombatHit2DAdapter` | `CombatHit2D` | class | Adapter | no | `Assets/ShooterMover/Runtime/UnityAdapters/Combat/CombatHit2DAdapter.cs` |
| `PlayerCombatIntentAdapter` | `PlayerCombatIntent` | class | Adapter | no | `Assets/ShooterMover/Runtime/UnityAdapters/Combat/PlayerCombatIntentAdapter.cs` |
| `WeaponMount2DAdapter` | `WeaponMount2D` | class | Adapter | no | `Assets/ShooterMover/Runtime/UnityAdapters/Combat/WeaponMount2DAdapter.cs` |
| `SpriteAnimationCombatDeathVfxDefinitionV1` | `SpriteAnimationCombatDeathVfxDefinition` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/CombatPresentation/CombatDeathVfxPool2D.cs` |
| `ICombatPresentationLifecycleSourceV1` | `ICombatPresentationLifecycleSource` | interface | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/CombatPresentation/CombatEnemyPresentationRegistration2D.cs` |
| `CombatHealthBarRefreshStatusV1` | `CombatHealthBarRefreshStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/CombatPresentation/CombatHealthBarPresenter2D.cs` |
| `CombatHealthPresentationStateV1` | `CombatHealthPresentationState` | enum | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/CombatPresentation/CombatHealthPresentationV1.cs` |
| `CombatPresentationAnchorFactsV1` | `CombatPresentationAnchorFacts` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/CombatPresentation/CombatHealthPresentationV1.cs` |
| `CombatHealthBarSnapshotV1` | `CombatHealthBarSnapshot` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/CombatPresentation/CombatHealthPresentationV1.cs` |
| `ICombatHealthBarSnapshotSourceV1` | `ICombatHealthBarSnapshotSource` | interface | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/CombatPresentation/CombatHealthPresentationV1.cs` |
| `PlayerHudCombatHealthSnapshotSourceV1` | `PlayerHudCombatHealthSnapshotSource` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/CombatPresentation/CombatHealthPresentationV1.cs` |
| `EnemyActorCombatHealthSnapshotSourceV1` | `EnemyActorCombatHealthSnapshotSource` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/CombatPresentation/CombatHealthPresentationV1.cs` |
| `EnemyRuntimeCombatHealthSnapshotSourceV1` | `EnemyCombatHealthSnapshotSource` | class | Runtime | yes | `Assets/ShooterMover/Runtime/UnityAdapters/CombatPresentation/CombatHealthPresentationV1.cs` |
| `CombatPresentationEnemyActorAuthority2D` | `CombatPresentationEnemyActor2D` | class | Authority | no | `Assets/ShooterMover/Runtime/UnityAdapters/CombatPresentation/CombatPresentationEnemyActorAuthority2D.cs` |
| `EnemyTerminalPresentationFactV1` | `EnemyTerminalPresentationFact` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/CombatPresentation/EnemyDeathVfxPresenter2D.cs` |
| `EnemyTerminalPresentationFactProjectorV1` | `EnemyTerminalPresentationFactProjector` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/CombatPresentation/EnemyDeathVfxPresenter2D.cs` |
| `EnemyDeathVfxScaleConfigurationV1` | `EnemyDeathVfxScaleConfiguration` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/CombatPresentation/EnemyDeathVfxPresenter2D.cs` |
| `EnemyDeathVfxPresentationStatusV1` | `EnemyDeathVfxPresentationStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/CombatPresentation/EnemyDeathVfxPresenter2D.cs` |
| `RuntimeBox` | `Box` | class | Runtime | no | `Assets/ShooterMover/Runtime/UnityAdapters/Development/RunDebug/RunDebugRewardBridge2D.cs` |
| `IEnemyActor2DAuthority` | `IEnemyActor2D` | interface | Authority | no | `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyActor2DAdapter.cs` |
| `EnemyActor2DAdapter` | `EnemyActor2D` | class | Adapter | no | `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyActor2DAdapter.cs` |
| `IEnemyAttackPatternRunTimeV1` | `IEnemyAttackPatternRunTime` | interface | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyAttackPatternLiveSchedulerV1.cs` |
| `IEnemyAttackPatternEmissionRealizerV1` | `IEnemyAttackPatternEmissionRealizer` | interface | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyAttackPatternLiveSchedulerV1.cs` |
| `EnemyAttackPatternRealizationStatusV1` | `EnemyAttackPatternRealizationStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyAttackPatternLiveSchedulerV1.cs` |
| `EnemyAttackPatternRealizationResultV1` | `EnemyAttackPatternRealizationResult` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyAttackPatternLiveSchedulerV1.cs` |
| `IEnemyAttackPatternTransactionalRealizerV1` | `IEnemyAttackPatternTransactionalRealizer` | interface | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyAttackPatternLiveSchedulerV1.cs` |
| `EnemyAttackPatternTransactionalRealizerV1` | `EnemyAttackPatternTransactionalRealizer` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyAttackPatternLiveSchedulerV1.cs` |
| `EnemyAttackPatternLiveStateV1` | `EnemyAttackPatternLiveState` | enum | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyAttackPatternLiveSchedulerV1.cs` |
| `EnemyAttackPatternLiveRecordV1` | `EnemyAttackPatternLiveRecord` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyAttackPatternLiveSchedulerV1.cs` |
| `EnemyAttackPatternLiveSchedulerV1` | `EnemyAttackPatternLiveScheduler` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyAttackPatternLiveSchedulerV1.cs` |
| `IEnemyAttackPatternMeleeContactReporterV1` | `IEnemyAttackPatternMeleeContactReporter` | interface | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyAttackPatternMeleeContact2D.cs` |
| `EnemyAttackPortV1` | `EnemyAttackPort` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyAttackPortV1.cs` |
| `RoomEnemyAttackPresentationPortV1` | `RoomEnemyAttackPresentationPort` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyAttackPortV1.cs` |
| `EnemyCommittedAttackPatternStatusV1` | `EnemyCommittedAttackPatternStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyCommittedAttackPatternExecutorV1.cs` |
| `EnemyCommittedAttackPatternResultV1` | `EnemyCommittedAttackPatternResult` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyCommittedAttackPatternExecutorV1.cs` |
| `IEnemyCommittedAttackPatternPortV1` | `IEnemyCommittedAttackPatternPort` | interface | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyCommittedAttackPatternExecutorV1.cs` |
| `EnemyCommittedAttackPatternExecutorV1` | `EnemyCommittedAttackPatternExecutor` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyCommittedAttackPatternExecutorV1.cs` |
| `EnemyContact2DAdapter` | `EnemyContact2D` | class | Adapter | no | `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyContact2DAdapter.cs` |
| `EnemyHitV1` | `EnemyHit` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyHitV1.cs` |
| `EnemyTarget2DAdapter` | `EnemyTarget2D` | class | Adapter | no | `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyTarget2DAdapter.cs` |
| `EnemyAttackPresentationProjection2D` | `EnemyAttackPresentation2D` | class | Projection | no | `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/Presentation/EnemyAttackPresentationProjection2D.cs` |
| `EnemyAttackPresentationPlanV1` | `EnemyAttackPresentationPlan` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/Presentation/EnemyAttackPresentationProjection2D.cs` |
| `EnemyAttackPresentationPulseV1` | `EnemyAttackPresentationPulse` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/Presentation/EnemyAttackPresentationProjection2D.cs` |
| `IEnemyRuntimeMechanicsReadiness2D` | `IEnemyMechanicsReadiness2D` | interface | Runtime | no | `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/Presentation/EnemyPresentationAdapter2D.cs` |
| `EnemyPresentationAdapter2D` | `EnemyPresentation2D` | class | Adapter | no | `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/Presentation/EnemyPresentationAdapter2D.cs` |
| `IEnemyAttackPatternSourceLifecycleV1` | `IEnemyAttackPatternSourceLifecycle` | interface | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/RunSessionEnemyAttackPatternTimeV1.cs` |
| `RunSessionEnemyAttackPatternTimeV1` | `RunSessionEnemyAttackPatternTime` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/RunSessionEnemyAttackPatternTimeV1.cs` |
| `PlayerMovementIntentAdapter` | `PlayerMovementIntent` | class | Adapter | no | `Assets/ShooterMover/Runtime/UnityAdapters/Input/PlayerMovementIntentAdapter.cs` |
| `JsonRoomRuntimeBootstrap2D` | `JsonRoomBootstrap2D` | class | Runtime | no | `Assets/ShooterMover/Runtime/UnityAdapters/Missions/Rooms/JsonRoomRuntimeBootstrap2D.cs` |
| `RoomRuntimeComposition2D` | `Room2D` | class | Runtime, Composition | no | `Assets/ShooterMover/Runtime/UnityAdapters/Missions/Rooms/RoomRuntimeComposition2D.cs` |
| `MovementBody2DAdapter` | `MovementBody2D` | class | Adapter | no | `Assets/ShooterMover/Runtime/UnityAdapters/Physics/MovementBody2DAdapter.cs` |
| `IMovementContactAuthority` | `IMovementContact` | interface | Authority | no | `Assets/ShooterMover/Runtime/UnityAdapters/Physics/MovementContact2DAdapter.cs` |
| `MovementContact2DAdapter` | `MovementContact2D` | class | Adapter | no | `Assets/ShooterMover/Runtime/UnityAdapters/Physics/MovementContact2DAdapter.cs` |
| `CanonicalPlayerWeaponSourceV2` | `PlayerWeaponSource` | class | Canonical | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Players/CanonicalPlayerWeaponSourceV2.cs` |
| `MovementActorPlayerRuntimeAdapter` | `MovementActorPlayer` | class | Runtime, Adapter | no | `Assets/ShooterMover/Runtime/UnityAdapters/Players/MovementActorPlayerRuntimeAdapter.cs` |
| `PlayerRuntimeWeaponStateAdapter` | `PlayerWeaponState` | class | Runtime, Adapter | no | `Assets/ShooterMover/Runtime/UnityAdapters/Players/PlayerInventoryWeaponRuntimeComposition.cs` |
| `PlayerInventoryWeaponRuntimeCompositionRoot` | `PlayerInventoryWeaponRoot` | class | Runtime, Composition | no | `Assets/ShooterMover/Runtime/UnityAdapters/Players/PlayerInventoryWeaponRuntimeComposition.cs` |
| `PlayerRuntimeCompositionRoot` | `PlayerRoot` | class | Runtime, Composition | no | `Assets/ShooterMover/Runtime/UnityAdapters/Players/PlayerRuntimeComposition.cs` |
| `PlayerRuntimeComposition` | `Player` | class | Runtime, Composition | no | `Assets/ShooterMover/Runtime/UnityAdapters/Players/PlayerRuntimeComposition.cs` |
| `PlayerRuntimeConstructionStatus` | `PlayerConstructionStatus` | enum | Runtime | no | `Assets/ShooterMover/Runtime/UnityAdapters/Players/PlayerRuntimeContracts.cs` |
| `PlayerRuntimeConstructionRejectionCode` | `PlayerConstructionRejectionCode` | enum | Runtime | no | `Assets/ShooterMover/Runtime/UnityAdapters/Players/PlayerRuntimeContracts.cs` |
| `PlayerRuntimeConfiguration` | `PlayerConfiguration` | class | Runtime | no | `Assets/ShooterMover/Runtime/UnityAdapters/Players/PlayerRuntimeContracts.cs` |
| `PlayerRuntimeSnapshot` | `PlayerSnapshot` | class | Runtime | no | `Assets/ShooterMover/Runtime/UnityAdapters/Players/PlayerRuntimeContracts.cs` |
| `PlayerRuntimeRestartStatus` | `PlayerRestartStatus` | enum | Runtime | no | `Assets/ShooterMover/Runtime/UnityAdapters/Players/PlayerRuntimeContracts.cs` |
| `PlayerRuntimeRestartRejectionCode` | `PlayerRestartRejectionCode` | enum | Runtime | no | `Assets/ShooterMover/Runtime/UnityAdapters/Players/PlayerRuntimeContracts.cs` |
| `PlayerRuntimeRestartCommand` | `PlayerRestartCommand` | class | Runtime | no | `Assets/ShooterMover/Runtime/UnityAdapters/Players/PlayerRuntimeContracts.cs` |
| `PlayerRuntimeRestartResult` | `PlayerRestartResult` | class | Runtime | no | `Assets/ShooterMover/Runtime/UnityAdapters/Players/PlayerRuntimeContracts.cs` |
| `IPlayerMovementRuntime` | `IPlayerMovement` | interface | Runtime | no | `Assets/ShooterMover/Runtime/UnityAdapters/Players/PlayerRuntimeContracts.cs` |
| `IPlayerPresentationRuntime` | `IPlayerPresentation` | interface | Runtime | no | `Assets/ShooterMover/Runtime/UnityAdapters/Players/PlayerRuntimeContracts.cs` |
| `IPlayerInputRuntime` | `IPlayerInput` | interface | Runtime | no | `Assets/ShooterMover/Runtime/UnityAdapters/Players/PlayerRuntimeContracts.cs` |
| `IPlayerRunCoordinator` | `IPlayerRun` | interface | Coordinator | no | `Assets/ShooterMover/Runtime/UnityAdapters/Players/PlayerRuntimeContracts.cs` |
| `PlayerRuntimeAttachments` | `PlayerAttachments` | class | Runtime | no | `Assets/ShooterMover/Runtime/UnityAdapters/Players/PlayerRuntimeContracts.cs` |
| `PlayerRuntimeConstructionResult` | `PlayerConstructionResult` | class | Runtime | no | `Assets/ShooterMover/Runtime/UnityAdapters/Players/PlayerRuntimeContracts.cs` |
| `ExistingPlayerRuntimeRunPortV1` | `ExistingPlayerRunPort` | class | Runtime | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Players/RunSessions/ExistingPlayerAndWeaponRunPortsV1.cs` |
| `ExistingWeaponExecutionRunPortV1` | `ExistingWeaponExecutionRunPort` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Players/RunSessions/ExistingPlayerAndWeaponRunPortsV1.cs` |
| `WeaponArtSpriteResolutionV1` | `WeaponArtSpriteResolution` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Presentation/Weapons/WeaponArtSpriteRegistryV1.cs` |
| `WeaponArtSpriteRegistryV1` | `WeaponArtSpriteRegistry` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Presentation/Weapons/WeaponArtSpriteRegistryV1.cs` |
| `EnemyExperienceRewardBandAuthoringV1` | `EnemyExperienceRewardBandAuthoring` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Progression/Experience/EnemyRewards/EnemyExperienceRewardCatalogAssetV1.cs` |
| `EnemyExperienceRewardCatalogAssetV1` | `EnemyExperienceRewardCatalogAsset` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Progression/Experience/EnemyRewards/EnemyExperienceRewardCatalogAssetV1.cs` |
| `EnemyExperienceRewardingAuthorityV1` | `EnemyExperienceRewarding` | class | Authority | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Progression/Experience/EnemyRewards/EnemyExperienceRewardingAuthorityV1.cs` |
| `IGameplayDropSourceV1` | `IGameplayDropSource` | interface | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/GameplayDrops/GameplayDropContracts.cs` |
| `RewardPickupApplicationAuthority2D` | `RewardPickupApplication2D` | class | Authority | no | `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/Pickups/RewardPickupApplicationAuthority2D.cs` |
| `RewardPickupCategoryV1` | `RewardPickupCategory` | enum | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/Pickups/RewardPickupContracts.cs` |
| `RewardPickupCategoryMapV1` | `RewardPickupCategoryMap` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/Pickups/RewardPickupContracts.cs` |
| `RewardPickupPresentationStyleV1` | `RewardPickupPresentationStyle` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/Pickups/RewardPickupContracts.cs` |
| `RewardPickupPayloadV1` | `RewardPickupPayload` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/Pickups/RewardPickupContracts.cs` |
| `RewardPickupCollectStatusV1` | `RewardPickupCollectStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/Pickups/RewardPickupContracts.cs` |
| `RewardPickupCollectResultV1` | `RewardPickupCollectResult` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/Pickups/RewardPickupContracts.cs` |
| `RewardPickupSpawnStatusV1` | `RewardPickupSpawnStatus` | enum | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/Pickups/RewardPickupContracts.cs` |
| `RewardPickupSpawnResultV1` | `RewardPickupSpawnResult` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/Pickups/RewardPickupContracts.cs` |
| `IRewardPickupLifecycleAuthorityV1` | `IRewardPickupLifecycle` | interface | Authority | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/Pickups/RewardPickupContracts.cs` |
| `IRewardPickupEquipmentPayloadResolverV1` | `IRewardPickupEquipmentPayloadResolver` | interface | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/Pickups/RewardPickupContracts.cs` |
| `RewardPickupPayloadBuilderV1` | `RewardPickupPayloadBuilder` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/Pickups/RewardPickupContracts.cs` |
| `PickupDeliveryDispositionV1` | `PickupDeliveryDisposition` | enum | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/RunPickups/PendingAdmissionPickupBridgeV1.cs` |
| `PickupDeliveryResultV1` | `PickupDeliveryResult` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/RunPickups/PendingAdmissionPickupBridgeV1.cs` |
| `PickupSourcePositionV1` | `PickupSourcePosition` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/RunPickups/PendingAdmissionPickupBridgeV1.cs` |
| `IPickupSourcePositionResolverV1` | `IPickupSourcePositionResolver` | interface | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/RunPickups/PendingAdmissionPickupBridgeV1.cs` |
| `TransformPickupSourcePositionResolverV1` | `TransformPickupSourcePositionResolver` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/RunPickups/PendingAdmissionPickupBridgeV1.cs` |
| `FixedPickupSourcePositionResolverV1` | `FixedPickupSourcePositionResolver` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/RunPickups/PendingAdmissionPickupBridgeV1.cs` |
| `IPickupAdmissionRuntimeV1` | `IPickupAdmission` | interface | Runtime | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/RunPickups/PendingAdmissionPickupBridgeV1.cs` |
| `UnityPickupAdmissionRuntimeV1` | `UnityPickupAdmission` | class | Runtime | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/RunPickups/PendingAdmissionPickupBridgeV1.cs` |
| `PendingAdmissionPickupBridgeV1` | `PendingAdmissionPickupBridge` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/RunPickups/PendingAdmissionPickupBridgeV1.cs` |
| `PickupBridgeFingerprintV1` | `PickupBridgeFingerprint` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/RunPickups/PendingAdmissionPickupBridgeV1.cs` |
| `RunPickupAuthorityHost2D` | `RunPickupHost2D` | class | Authority | no | `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/RunPickups/RunPickupAuthorityHost2D.cs` |
| `IRunRewardPickupProjectionBinderV1` | `IRunRewardPickupBinder` | interface | Projection | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/RunPickups/RunPickupPresentationLifecycleV1.cs` |
| `IRunRewardPickupAcceptedFeedbackV1` | `IRunRewardPickupAcceptedFeedback` | interface | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/RunPickups/RunPickupPresentationLifecycleV1.cs` |
| `RunPickupPresentationEntryV1` | `RunPickupPresentationEntry` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/RunPickups/RunPickupPresentationRegistry2D.cs` |
| `RunPickupPresentationSyncResultV1` | `RunPickupPresentationSyncResult` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/RunPickups/RunPickupPresenter2D.cs` |
| `CanonicalWeaponEquipmentProjectionLookupV2` | `WeaponEquipmentLookup` | class | Canonical, Projection | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/CanonicalWeaponEquipmentProjectionLookupV2.cs` |
| `InventoryBackedWeaponExecutionAdapter` | `InventoryBackedWeaponExecution` | class | Adapter | no | `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/InventoryBackedWeaponExecutionAdapter.cs` |
| `WeaponLiveExceptionPolicyV1` | `WeaponLiveExceptionPolicy` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/InventoryWeaponEffectiveResolver.cs` |
| `ICanonicalWeaponBlueprintResolver` | `IWeaponBlueprintResolver` | interface | Canonical | no | `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/InventoryWeaponEffectiveResolver.cs` |
| `InventoryWeaponMountedAimExecutionV1` | `InventoryWeaponMountedAimExecution` | class | — | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/InventoryWeaponMountedAimExecutionV1.cs` |
| `InventoryWeaponMountedRuntimeV1` | `InventoryWeaponMounted` | class | Runtime | yes | `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/InventoryWeaponRuntimeComposition.cs` |
| `InventoryWeaponRuntimeComposition` | `InventoryWeapon` | class | Runtime, Composition | no | `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/InventoryWeaponRuntimeComposition.cs` |
| `ProductionCanonicalNormalProjectile2D` | `NormalProjectile2D` | class | Production, Canonical | no | `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/ProductionCanonicalNormalProjectile2D.cs` |
| `CanonicalProjectileSourceIdentity2D` | `ProjectileSourceIdentity2D` | class | Canonical | no | `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/ProductionCanonicalProjectileEffectSink2D.cs` |
| `ProductionCanonicalProjectileEffectSink2D` | `ProjectileEffectSink2D` | class | Production, Canonical | no | `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/ProductionCanonicalProjectileEffectSink2D.cs` |
| `BalanceSimulationServiceV1Tests` | `BalanceSimulationV1Tests` | class | Service | no | `Assets/ShooterMover/Tests/EditMode/BalanceSimulator/BalanceSimulationServiceV1Tests.cs` |
| `RejectingRuntime` | `Rejecting` | class | Runtime | no | `Assets/ShooterMover/Tests/EditMode/BalanceSimulator/BalanceSimulationServiceV1Tests.cs` |
| `LootboxSimulatorRuntimeV1Tests` | `LootboxSimulatorV1Tests` | class | Runtime | no | `Assets/ShooterMover/Tests/EditMode/BalanceSimulator/LootboxSimulatorRuntimeV1.AuthoritativeTests.cs` |
| `LootboxSimulatorRuntimeV1Tests` | `LootboxSimulatorV1Tests` | class | Runtime | no | `Assets/ShooterMover/Tests/EditMode/BalanceSimulator/LootboxSimulatorRuntimeV1Tests.cs` |
| `ProductionStrongboxCatalogV1Tests` | `StrongboxCatalogV1Tests` | class | Production | no | `Assets/ShooterMover/Tests/EditMode/BalanceSimulator/ProductionStrongboxCatalogV1Tests.cs` |
| `CanonicalWeaponProjectileSourceIdentityTests` | `WeaponProjectileSourceIdentityTests` | class | Canonical | no | `Assets/ShooterMover/Tests/EditMode/CanonicalWeaponProjectileSourceIdentityTests.cs` |
| `ExactCanonicalBlueprintResolver` | `ExactBlueprintResolver` | class | Canonical | no | `Assets/ShooterMover/Tests/EditMode/CanonicalWeaponProjectileSourceIdentityTests.cs` |
| `CharacterSelectionServiceV1Tests` | `CharacterSelectionV1Tests` | class | Service | no | `Assets/ShooterMover/Tests/EditMode/Characters/Selection/CharacterSelectionServiceV1Tests.cs` |
| `CoordinatorFixture` | `Fixture` | class | Coordinator | no | `Assets/ShooterMover/Tests/EditMode/Combat/FourMountStatusProjectorTests.cs` |
| `WeaponRuntimeProfileTests` | `WeaponProfileTests` | class | Runtime | no | `Assets/ShooterMover/Tests/EditMode/Combat/WeaponRuntimeProfileTests.cs` |
| `FakeEnemyAuthority2D` | `FakeEnemy2D` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/CombatPresentation/CombatPresentationV1Tests.cs` |
| `ConditionRuntimeAuthorityV1Tests` | `ConditionV1Tests` | class | Runtime, Authority | no | `Assets/ShooterMover/Tests/EditMode/ConditionRuntime/ConditionRuntimeAuthorityV1ReplayHardeningTests.cs` |
| `ConditionRuntimeAuthorityV1Tests` | `ConditionV1Tests` | class | Runtime, Authority | no | `Assets/ShooterMover/Tests/EditMode/ConditionRuntime/ConditionRuntimeAuthorityV1TestSupport.cs` |
| `ObjectiveFactAdapter` | `ObjectiveFact` | class | Adapter | no | `Assets/ShooterMover/Tests/EditMode/ConditionRuntime/ConditionRuntimeAuthorityV1TestSupport.cs` |
| `ConditionRuntimeAuthorityV1Tests` | `ConditionV1Tests` | class | Runtime, Authority | no | `Assets/ShooterMover/Tests/EditMode/ConditionRuntime/ConditionRuntimeAuthorityV1Tests.cs` |
| `TestProjection` | `Test` | class | Projection | no | `Assets/ShooterMover/Tests/EditMode/Contracts/RoomContractTests.cs` |
| `FakeProjectionReader` | `FakeReader` | class | Projection | no | `Assets/ShooterMover/Tests/EditMode/Contracts/RoomContractTests.cs` |
| `CraftingServiceV1Tests` | `CraftingV1Tests` | class | Service | no | `Assets/ShooterMover/Tests/EditMode/Crafting/CraftingServiceV1Tests.cs` |
| `FailOnceApplyAuthority` | `FailOnceApply` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Crafting/CraftingServiceV1Tests.cs` |
| `CraftingInventoryEquipServiceV1Tests` | `CraftingInventoryEquipV1Tests` | class | Service | no | `Assets/ShooterMover/Tests/EditMode/Crafting/Integration/CraftingInventoryEquipServiceV1Tests.cs` |
| `FailOnceApplyAuthority` | `FailOnceApply` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Crafting/Integration/CraftingInventoryEquipServiceV1Tests.cs` |
| `FakeCraftingAuthority` | `FakeCrafting` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Crafting/Presentation/FakeCraftingAuthority.cs` |
| `FakeRuntimePort` | `FakePort` | class | Runtime | no | `Assets/ShooterMover/Tests/EditMode/Development/RunDebug/RunDebugPlannerAndSessionTests.cs` |
| `ScrapWalletServiceV1Tests` | `ScrapWalletV1Tests` | class | Service | no | `Assets/ShooterMover/Tests/EditMode/Economy/Scrap/ScrapWalletServiceV1Tests.cs` |
| `LevelDoorAuthorityV2Tests` | `LevelDoorV2Tests` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/EditorTooling/LevelDoorAuthorityV2Tests.cs` |
| `LevelGridEditorRuntimeIntegrationV2Tests` | `LevelGridEditorIntegrationV2Tests` | class | Runtime | no | `Assets/ShooterMover/Tests/EditMode/EditorTooling/LevelGridEditorRuntimeIntegrationV2Tests.cs` |
| `EnemyAttackPatternAuthorityV1Tests` | `EnemyAttackPatternV1Tests` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyAttackPatternCatalogV1Tests.cs` |
| `EnemyAttackPatternAuthorityV1Tests` | `EnemyAttackPatternV1Tests` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyAttackPatternLegacyCutoverV1Tests.cs` |
| `EnemyAttackPatternAuthorityV1Tests` | `EnemyAttackPatternV1Tests` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyAttackPatternLiveSchedulerV1Tests.cs` |
| `EnemyAttackPatternAuthorityV1Tests` | `EnemyAttackPatternV1Tests` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyAttackPatternRuntimeV1Tests.cs` |
| `EnemyAttackPatternAuthorityV1Tests` | `EnemyAttackPatternV1Tests` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyAttackPatternTestFixturesV1.cs` |
| `EnemyAttackPatternAuthorityV1Tests` | `EnemyAttackPatternV1Tests` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyAttackPatternTransactionalFailureV1Tests.cs` |
| `EnemyPlacementRuntimeFactoryV1Tests_AuthorityBoundaries` | `EnemyPlacementFactoryV1TestsBoundaries` | class | Runtime, Authority | no | `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyPlacementRuntimeAuthorityBoundaryV1Tests.cs` |
| `EnemyPlacementRuntimeFactoryV1Tests` | `EnemyPlacementFactoryV1Tests` | class | Runtime | no | `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyPlacementRuntimeFactoryV1Tests.cs` |
| `EnemyPlacementRuntimeFactoryV1Tests_LifecycleRouting` | `EnemyPlacementFactoryV1TestsLifecycleRouting` | class | Runtime | no | `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyPlacementRuntimeLifecycleRoutingV1Tests.cs` |
| `EnemyRuntimeFoundationTests` | `EnemyFoundationTests` | class | Runtime | no | `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyRuntimeFoundationTests.cs` |
| `AugmentUpgradeServiceV1Tests` | `AugmentUpgradeV1Tests` | class | Service | no | `Assets/ShooterMover/Tests/EditMode/Equipment/Upgrades/AugmentUpgradeRetryAndIntegrationTests.cs` |
| `ThrowOnceRewardChildAuthority` | `ThrowOnceRewardChild` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Equipment/Upgrades/AugmentUpgradeRetryAndIntegrationTests.cs` |
| `AugmentUpgradeServiceV1Tests` | `AugmentUpgradeV1Tests` | class | Service | no | `Assets/ShooterMover/Tests/EditMode/Equipment/Upgrades/AugmentUpgradeSequenceAndConfigurationTests.cs` |
| `AugmentUpgradeServiceV1Tests` | `AugmentUpgradeV1Tests` | class | Service | no | `Assets/ShooterMover/Tests/EditMode/Equipment/Upgrades/AugmentUpgradeServiceV1Tests.cs` |
| `AugmentUpgradeServiceV1Tests` | `AugmentUpgradeV1Tests` | class | Service | no | `Assets/ShooterMover/Tests/EditMode/Equipment/Upgrades/AugmentUpgradeValidationTests.cs` |
| `CanonicalReceiptFixture` | `ReceiptFixture` | class | Canonical | no | `Assets/ShooterMover/Tests/EditMode/Equipment/Upgrades/InventoryEconomySafetyGateTests.cs` |
| `LegacyFirstWeaponHoldingsAdapterRetirementTests` | `LegacyFirstWeaponHoldingsRetirementTests` | class | Adapter | no | `Assets/ShooterMover/Tests/EditMode/Equipment/Upgrades/LegacyFirstWeaponHoldingsAdapterRetirementTests.cs` |
| `FixedClockV1` | `FixedClock` | class | — | yes | `Assets/ShooterMover/Tests/EditMode/ExtensibilityGuardrailsV1Tests.cs` |
| `CanonicalFirstPlayerHoldingsAuthorityV2Tests` | `FirstPlayerHoldingsV2Tests` | class | Canonical, Authority | no | `Assets/ShooterMover/Tests/EditMode/Flow/Hub/CanonicalFirstPlayerHoldingsAuthorityV2Tests.cs` |
| `MutatingThrowingReceiptAuthority` | `MutatingThrowingReceipt` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Flow/Hub/CanonicalFirstPlayerHoldingsAuthorityV2Tests.cs` |
| `ProductionExactWeaponInstanceLoadoutTests` | `ExactWeaponInstanceLoadoutTests` | class | Production | no | `Assets/ShooterMover/Tests/EditMode/Flow/Hub/ProductionExactWeaponInstanceLoadoutTests.cs` |
| `ProductionFlowSessionV1Tests` | `FlowSessionV1Tests` | class | Production | no | `Assets/ShooterMover/Tests/EditMode/Flow/Hub/ProductionFlowSessionV1Tests.cs` |
| `ProductionOpaqueWeaponInstanceIdentityTests` | `OpaqueWeaponInstanceIdentityTests` | class | Production | no | `Assets/ShooterMover/Tests/EditMode/Flow/Hub/ProductionOpaqueWeaponInstanceIdentityTests.cs` |
| `ProductionWeaponMountPolicyV1Tests` | `WeaponMountPolicyV1Tests` | class | Production | no | `Assets/ShooterMover/Tests/EditMode/Flow/Hub/ProductionWeaponMountPolicyV1Tests.cs` |
| `PlaySelectionServiceTests` | `PlaySelectionTests` | class | Service | no | `Assets/ShooterMover/Tests/EditMode/Flow/PlaySelection/PlaySelectionServiceTests.cs` |
| `ProductionPlayableLevelCatalogAvailabilityTests` | `PlayableLevelCatalogAvailabilityTests` | class | Production | no | `Assets/ShooterMover/Tests/EditMode/Flow/ProductionPlayableLevelCatalogAvailabilityTests.cs` |
| `BootstrapCompositionRootTests` | `BootstrapRootTests` | class | Composition | no | `Assets/ShooterMover/Tests/EditMode/Foundation/BootstrapCompositionRootTests.cs` |
| `PlayerActorAuthorityTests` | `PlayerActorTests` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/GameplayEntities/PlayerActorAuthorityTests.cs` |
| `PlayerHoldingsServiceTests` | `PlayerHoldingsTests` | class | Service | no | `Assets/ShooterMover/Tests/EditMode/Holdings/PlayerHoldingsServiceTests.cs` |
| `InventoryLoadoutScreenServiceTests` | `InventoryLoadoutScreenTests` | class | Service | no | `Assets/ShooterMover/Tests/EditMode/Inventory/LoadoutScreen/InventoryLoadoutScreenServiceTests.cs` |
| `CatalogAdapter` | `Catalog` | class | Adapter | no | `Assets/ShooterMover/Tests/EditMode/Inventory/LoadoutScreen/InventoryLoadoutScreenServiceTests.cs` |
| `RecordingLoadoutAuthority` | `RecordingLoadout` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Inventory/LoadoutScreen/InventoryLoadoutScreenServiceTests.cs` |
| `MissionRunResultAuthorityV1Tests` | `MissionRunResultV1Tests` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Missions/Results/MissionRunResultAuthorityV1Tests.Fixtures.cs` |
| `FakeExistingAuthorityPort` | `FakeExistingPort` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Missions/Results/MissionRunResultAuthorityV1Tests.Fixtures.cs` |
| `MissionRunResultAuthorityV1Tests` | `MissionRunResultV1Tests` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Missions/Results/MissionRunResultAuthorityV1Tests.cs` |
| `JsonRoomRuntimeBootstrapCompositionTests` | `JsonRoomBootstrapTests` | class | Runtime, Composition | no | `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/JsonRoomRuntimeBootstrapCompositionTests.cs` |
| `RoomAccessAuthorityV1Tests` | `RoomAccessV1Tests` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/RoomAccessAuthorityV1Tests.cs` |
| `RoomLiveRuntimeAuthorityTests` | `RoomLiveTests` | class | Runtime, Authority | no | `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/RoomLiveRuntimeAuthorityTests.Part02.cs` |
| `RoomLiveRuntimeAuthorityTests` | `RoomLiveTests` | class | Runtime, Authority | no | `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/RoomLiveRuntimeAuthorityTests.Part03.cs` |
| `RoomLiveRuntimeAuthorityTests` | `RoomLiveTests` | class | Runtime, Authority | no | `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/RoomLiveRuntimeAuthorityTests.cs` |
| `RoomRuntimeAuthorityTests` | `RoomTests` | class | Runtime, Authority | no | `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/RoomRuntimeAuthorityTests.cs` |
| `ActiveEventModifierProjectionV1Tests` | `ActiveEventModifierV1Tests` | class | Projection | no | `Assets/ShooterMover/Tests/EditMode/Modifiers/Events/ActiveEventModifierProjectionV1Tests.cs` |
| `MutableClockV1` | `MutableClock` | class | — | yes | `Assets/ShooterMover/Tests/EditMode/Modifiers/Events/ActiveEventModifierProjectionV1Tests.cs` |
| `FixedClockV1` | `FixedClock` | class | — | yes | `Assets/ShooterMover/Tests/EditMode/Modifiers/Events/EventStampedCommandEnvelopeV1Tests.cs` |
| `RuntimeModifierFoundationV1Tests` | `ModifierFoundationV1Tests` | class | Runtime | no | `Assets/ShooterMover/Tests/EditMode/Modifiers/RuntimeModifierFoundationV1Tests.cs` |
| `StatusEffectAuthorityV1Tests` | `StatusEffectV1Tests` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Modifiers/StatusEffects/StatusEffectAuthorityV1ApplyPolicyTests.cs` |
| `StatusEffectAuthorityV1Tests` | `StatusEffectV1Tests` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Modifiers/StatusEffects/StatusEffectAuthorityV1BridgeCatalogTests.cs` |
| `StatusEffectAuthorityV1Tests` | `StatusEffectV1Tests` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Modifiers/StatusEffects/StatusEffectAuthorityV1ReplayLifecycleTests.cs` |
| `StatusEffectAuthorityV1Tests` | `StatusEffectV1Tests` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Modifiers/StatusEffects/StatusEffectAuthorityV1StackingTests.cs` |
| `PlayerAccountSaveAuthorityV1Tests` | `PlayerAccountSaveV1Tests` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Persistence/Accounts/PlayerAccountSaveAuthorityV1Tests.cs` |
| `TestAuthority` | `Test` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Persistence/Components/AtomicSaveAndCompensationV1Tests.cs` |
| `EconomyTransactionStatusV1` | `EconomyTransactionStatus` | class | — | yes | `Assets/ShooterMover/Tests/EditMode/Persistence/Components/PersistenceTestEconomyTransactionStatusV1.cs` |
| `HoldingProvenanceV1` | `HoldingProvenance` | class | — | yes | `Assets/ShooterMover/Tests/EditMode/Persistence/Components/PersistenceTestHoldingProvenanceV1.cs` |
| `RealAuthoritySaveAdaptersV1Tests` | `RealSaveAdaptersV1Tests` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Persistence/Components/SaveAdaptersV1Tests.cs` |
| `StrongboxSaveAdapterReplayTests` | `StrongboxSaveReplayTests` | class | Adapter | no | `Assets/ShooterMover/Tests/EditMode/Persistence/Components/StrongboxSaveAdapterReplayTests.cs` |
| `CountingChildAuthority` | `CountingChild` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Persistence/Components/StrongboxSaveAdapterReplayTests.cs` |
| `CharacterCompositionCoordinatorV1Tests` | `CharacterV1Tests` | class | Composition, Coordinator | no | `Assets/ShooterMover/Tests/EditMode/Persistence/Composition/CharacterCompositionCoordinatorV1Tests.cs` |
| `CollectedRunRewardAtomicCoordinatorTests` | `CollectedRunRewardAtomicTests` | class | Coordinator | no | `Assets/ShooterMover/Tests/EditMode/Persistence/Composition/CollectedRunRewardAtomicCoordinatorTests.cs` |
| `FakeAtomicAuthority` | `FakeAtomic` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Persistence/Composition/CollectedRunRewardAtomicCoordinatorTests.cs` |
| `ProductionWeaponOnboardingAndMigrationTests` | `WeaponOnboardingAndMigrationTests` | class | Production | no | `Assets/ShooterMover/Tests/EditMode/Persistence/Composition/ProductionWeaponOnboardingAndMigrationTests.cs` |
| `StrongboxPersistenceCoordinatorV1Tests` | `StrongboxPersistenceV1Tests` | class | Coordinator | no | `Assets/ShooterMover/Tests/EditMode/Persistence/Composition/StrongboxPersistenceCoordinatorV1TestFixture.cs` |
| `PlayerLiveAuthorityTests` | `PlayerLiveTests` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/PlayerRuntime/PlayerLiveAuthorityTests.cs` |
| `FakeRunCoordinator` | `FakeRun` | class | Coordinator | no | `Assets/ShooterMover/Tests/EditMode/PlayerRuntime/PlayerLiveAuthorityTests.cs` |
| `PlayerRuntimeCompositionTests` | `PlayerTests` | class | Runtime, Composition | no | `Assets/ShooterMover/Tests/EditMode/PlayerRuntime/PlayerRuntimeCompositionTests.cs` |
| `FakeRunCoordinator` | `FakeRun` | class | Coordinator | no | `Assets/ShooterMover/Tests/EditMode/PlayerRuntime/PlayerRuntimeCompositionTests.cs` |
| `PlayerExperienceAuthorityTests` | `PlayerExperienceTests` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Progression/Experience/PlayerExperienceAuthorityTests.cs` |
| `SkillProgressionAuthorityTests` | `SkillProgressionTests` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Progression/Skills/SkillProgressionAuthorityTests.cs` |
| `DestructiblePropAuthorityTests` | `DestructiblePropTests` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Props/DestructiblePropAuthorityTests.cs` |
| `PropRuntimeV1Tests` | `PropV1Tests` | class | Runtime | no | `Assets/ShooterMover/Tests/EditMode/Props/PropRuntimeV1Tests.cs` |
| `RewardApplicationServiceV1Tests` | `RewardApplicationV1Tests` | class | Service | no | `Assets/ShooterMover/Tests/EditMode/Rewards/Application/RewardApplicationServiceV1Tests.cs` |
| `DeterministicAuthority` | `Deterministic` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Rewards/Application/RewardApplicationServiceV1Tests.cs` |
| `RunRewardRuntimeSnapshotV1Tests` | `RunRewardSnapshotV1Tests` | class | Runtime | no | `Assets/ShooterMover/Tests/EditMode/Rewards/Drops/RunRewardRuntimeSnapshotV1Tests.cs` |
| `RewardGenerationServiceV1Tests` | `RewardGenerationV1Tests` | class | Service | no | `Assets/ShooterMover/Tests/EditMode/Rewards/Generation/RewardGenerationServiceV1Tests.cs` |
| `StrongboxOpeningServiceV1Tests` | `StrongboxOpeningV1Tests` | class | Service | no | `Assets/ShooterMover/Tests/EditMode/Rewards/Strongboxes/StrongboxOpeningServiceV1Tests.cs` |
| `RewardAuthorityDecorator` | `RewardDecorator` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Rewards/Strongboxes/StrongboxOpeningServiceV1Tests.cs` |
| `RejectFirstPreflightAuthority` | `RejectFirstPreflight` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Rewards/Strongboxes/StrongboxOpeningServiceV1Tests.cs` |
| `RejectFirstApplyAuthority` | `RejectFirstApply` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Rewards/Strongboxes/StrongboxOpeningServiceV1Tests.cs` |
| `RunLocalPickupAuthorityV1Tests` | `RunLocalPickupV1Tests` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/RunPickups/ExistingRunSessionPickupPortV1Tests.cs` |
| `FakeCollectedRewardAuthority` | `FakeCollectedReward` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/RunPickups/ExistingRunSessionPickupPortV1Tests.cs` |
| `FakeRuntime` | `Fake` | class | Runtime | no | `Assets/ShooterMover/Tests/EditMode/RunPickups/PendingAdmissionPickupBridgeV1Tests.cs` |
| `RunLocalPickupAuthorityV1Tests` | `RunLocalPickupV1Tests` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/RunPickups/RunLocalPickupAuthorityCollectionTests.cs` |
| `RunLocalPickupAuthorityV1Tests` | `RunLocalPickupV1Tests` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/RunPickups/RunLocalPickupAuthorityTestSupport.cs` |
| `RunLocalPickupAuthorityV1Tests` | `RunLocalPickupV1Tests` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/RunPickups/RunLocalPickupAuthorityV1Tests.cs` |
| `RunLocalPickupAuthorityV1Tests` | `RunLocalPickupV1Tests` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/RunPickups/RunPickupLifecycleRegressionTests.cs` |
| `RunSessionAuthorityV1Tests` | `RunSessionV1Tests` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/RunSessions/RunSessionAuthorityV1Tests.cs` |
| `FakeRuntimeBundle` | `FakeBundle` | class | Runtime | no | `Assets/ShooterMover/Tests/EditMode/RunSessions/RunSessionAuthorityV1Tests.cs` |
| `FakeRuntimeBundle` | `FakeBundle` | class | Runtime | no | `Assets/ShooterMover/Tests/EditMode/RunSessions/RunSessionDurableEndV1Tests.cs` |
| `TransientHoldingsAuthority` | `TransientHoldings` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Shops/Presentation/ShopScreenPresentationV1Tests.Fixtures.cs` |
| `ShopRuntimeServiceV1Tests` | `ShopV1Tests` | class | Runtime, Service | no | `Assets/ShooterMover/Tests/EditMode/Shops/ShopRuntimeServiceV1Tests.Fixtures.cs` |
| `TransientHoldingsAuthority` | `TransientHoldings` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Shops/ShopRuntimeServiceV1Tests.Fixtures.cs` |
| `ShopRuntimeServiceV1Tests` | `ShopV1Tests` | class | Runtime, Service | no | `Assets/ShooterMover/Tests/EditMode/Shops/ShopRuntimeServiceV1Tests.More.cs` |
| `ShopRuntimeServiceV1Tests` | `ShopV1Tests` | class | Runtime, Service | no | `Assets/ShooterMover/Tests/EditMode/Shops/ShopRuntimeServiceV1Tests.cs` |
| `EnemyDeathTerminalDropFactAdapterV1` | `EnemyDeathTerminalDropFact` | class | Adapter | yes | `Assets/ShooterMover/Tests/EditMode/TerminalDropBinding/EnemyDeathTerminalDropFactAdapterV1.cs` |
| `EnemyTerminalSourceContextAdapterV1Tests` | `EnemyTerminalSourceContextV1Tests` | class | Adapter | no | `Assets/ShooterMover/Tests/EditMode/TerminalDropBinding/EnemyTerminalSourceContextAdapterV1Tests.cs` |
| `TerminalDropGenerationAuthorityV1Tests` | `TerminalDropGenerationV1Tests` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/TerminalDropBinding/TerminalDropGenerationAuthorityV1Tests.cs` |
| `FixtureAdapter` | `Fixture` | class | Adapter | no | `Assets/ShooterMover/Tests/EditMode/TerminalDropBinding/TerminalDropGenerationAuthorityV1Tests.cs` |
| `AlternateAdapter` | `Alternate` | class | Adapter | no | `Assets/ShooterMover/Tests/EditMode/TerminalDropBinding/TerminalDropGenerationAuthorityV1Tests.cs` |
| `PipelineAdapter` | `Pipeline` | class | Adapter | no | `Assets/ShooterMover/Tests/EditMode/TerminalDropBinding/TerminalDropReviewBlockerTests.cs` |
| `ThrowOnceAdapter` | `ThrowOnce` | class | Adapter | no | `Assets/ShooterMover/Tests/EditMode/TerminalDropBinding/TerminalDropReviewBlockerTests.cs` |
| `InventoryBackedWeaponExecutionAdapterTests` | `InventoryBackedWeaponExecutionTests` | class | Adapter | no | `Assets/ShooterMover/Tests/EditMode/Weapons/Live/InventoryBackedWeaponCatalogFixtures.cs` |
| `InventoryBackedWeaponExecutionAdapterTests` | `InventoryBackedWeaponExecutionTests` | class | Adapter | no | `Assets/ShooterMover/Tests/EditMode/Weapons/Live/InventoryBackedWeaponExecutionAdapterFakes.cs` |
| `CountingHoldingsAuthority` | `CountingHoldings` | class | Authority | no | `Assets/ShooterMover/Tests/EditMode/Weapons/Live/InventoryBackedWeaponExecutionAdapterFakes.cs` |
| `InventoryBackedWeaponExecutionAdapterTests` | `InventoryBackedWeaponExecutionTests` | class | Adapter | no | `Assets/ShooterMover/Tests/EditMode/Weapons/Live/InventoryBackedWeaponExecutionAdapterFixtures.cs` |
| `InventoryBackedWeaponExecutionAdapterTests` | `InventoryBackedWeaponExecutionTests` | class | Adapter | no | `Assets/ShooterMover/Tests/EditMode/Weapons/Live/InventoryBackedWeaponExecutionAdapterReplayTests.cs` |
| `InventoryBackedWeaponExecutionAdapterTests` | `InventoryBackedWeaponExecutionTests` | class | Adapter | no | `Assets/ShooterMover/Tests/EditMode/Weapons/Live/InventoryBackedWeaponExecutionAdapterTests.cs` |
| `RuntimeTypes` | `Types` | class | Runtime | no | `Assets/ShooterMover/Tests/PlayMode/Combat/BoundedProjectile2DTests.cs` |
| `PlayerCombatIntentAdapterTests` | `PlayerCombatIntentTests` | class | Adapter | no | `Assets/ShooterMover/Tests/PlayMode/Combat/PlayerCombatIntentAdapterTests.cs` |
| `WeaponMount2DAdapterTests` | `WeaponMount2DTests` | class | Adapter | no | `Assets/ShooterMover/Tests/PlayMode/Combat/WeaponMount2DAdapterTests.cs` |
| `FakeEnemyAuthority2D` | `FakeEnemy2D` | class | Authority | no | `Assets/ShooterMover/Tests/PlayMode/CombatPresentation/CombatPresentationPlayModeSmokeTests.cs` |
| `RecordingChildAuthority` | `RecordingChild` | class | Authority | no | `Assets/ShooterMover/Tests/PlayMode/Development/RunDebug/RunDebugPhysicalPickupTests.cs` |
| `EnemyActor2DAdapterTests` | `EnemyActor2DTests` | class | Adapter | no | `Assets/ShooterMover/Tests/PlayMode/Enemies/EnemyActor2DAdapterTests.cs` |
| `TestEnemyAuthority` | `TestEnemy` | class | Authority | no | `Assets/ShooterMover/Tests/PlayMode/Enemies/EnemyActor2DAdapterTests.cs` |
| `ControllerFakeAuthority` | `ControllerFake` | class | Authority | no | `Assets/ShooterMover/Tests/PlayMode/Flow/Crafting/CraftingScreenControllerTests.cs` |
| `RecordingAdapter` | `Recording` | class | Adapter | no | `Assets/ShooterMover/Tests/PlayMode/Flow/Hub/HubFlowControllerTests.cs` |
| `InventoryLoadoutAuthorityConnectionTests` | `InventoryLoadoutConnectionTests` | class | Authority | no | `Assets/ShooterMover/Tests/PlayMode/Flow/InventoryLoadout/InventoryLoadoutAuthorityConnectionTests.cs` |
| `CatalogAdapter` | `Catalog` | class | Adapter | no | `Assets/ShooterMover/Tests/PlayMode/Flow/InventoryLoadout/InventoryLoadoutScreenControllerTests.cs` |
| `RecordingLoadoutAuthority` | `RecordingLoadout` | class | Authority | no | `Assets/ShooterMover/Tests/PlayMode/Flow/InventoryLoadout/InventoryLoadoutScreenControllerTests.cs` |
| `CanonicalUiOwnershipPlayModeTests` | `UiOwnershipPlayModeTests` | class | Canonical | no | `Assets/ShooterMover/Tests/PlayMode/Flow/ProductionFlow/CanonicalUiOwnershipPlayModeTests.cs` |
| `ProductionFlowPlayModeTests` | `FlowPlayModeTests` | class | Production | no | `Assets/ShooterMover/Tests/PlayMode/Flow/ProductionFlow/ProductionFlowPlayModeTests.cs` |
| `TransientHoldingsAuthority` | `TransientHoldings` | class | Authority | no | `Assets/ShooterMover/Tests/PlayMode/Flow/Shop/ShopScreenControllerV1Tests.Fixtures.cs` |
| `MovementBody2DAdapterTests` | `MovementBody2DTests` | class | Adapter | no | `Assets/ShooterMover/Tests/PlayMode/Movement/MovementBody2DAdapterTests.cs` |
| `MovementContact2DAdapterTests` | `MovementContact2DTests` | class | Adapter | no | `Assets/ShooterMover/Tests/PlayMode/Movement/MovementContact2DAdapterTests.cs` |
| `FakeMovementContactAuthority` | `FakeMovementContact` | class | Authority | no | `Assets/ShooterMover/Tests/PlayMode/Movement/MovementContact2DAdapterTests.cs` |
| `PlayerMovementIntentAdapterTests` | `PlayerMovementIntentTests` | class | Adapter | no | `Assets/ShooterMover/Tests/PlayMode/Movement/PlayerMovementIntentAdapterTests.cs` |
| `EnemyExperienceRewardingAuthorityTests` | `EnemyExperienceRewardingTests` | class | Authority | no | `Assets/ShooterMover/Tests/PlayMode/Progression/Experience/EnemyRewards/EnemyExperienceRewardingAuthorityTests.cs` |
| `TestEnemyAuthority` | `TestEnemy` | class | Authority | no | `Assets/ShooterMover/Tests/PlayMode/Progression/Experience/EnemyRewards/EnemyExperienceRewardingAuthorityTests.cs` |
| `TestAuthoritySet` | `TestSet` | class | Authority | no | `Assets/ShooterMover/Tests/PlayMode/Rewards/Pickups/RewardPickupTestSupport.cs` |
| `RecordingRewardChildAuthority` | `RecordingRewardChild` | class | Authority | no | `Assets/ShooterMover/Tests/PlayMode/Rewards/Pickups/RewardPickupTestSupport.cs` |
| `LootPickupRunProjectionPlayModeTests` | `LootPickupRunPlayModeTests` | class | Projection | no | `Assets/ShooterMover/Tests/PlayMode/RunPickups/LootPickupRunProjectionPlayModeTests.cs` |
| `CanonicalWeaponGameplayResolutionPlayModeTests` | `WeaponGameplayResolutionPlayModeTests` | class | Canonical | no | `Assets/ShooterMover/Tests/PlayMode/Weapons/Live/CanonicalWeaponGameplayResolutionPlayModeTests.cs` |
| `InventoryWeaponRuntimePlayModeTests` | `InventoryWeaponPlayModeTests` | class | Runtime | no | `Assets/ShooterMover/Tests/PlayMode/Weapons/Live/InventoryWeaponConcurrentMountPlayModeTests.cs` |
| `InventoryWeaponRuntimePlayModeTests` | `InventoryWeaponPlayModeTests` | class | Runtime | no | `Assets/ShooterMover/Tests/PlayMode/Weapons/Live/InventoryWeaponMountedAimPlayModeTests.cs` |
| `InventoryWeaponRuntimePlayModeTests` | `InventoryWeaponPlayModeTests` | class | Runtime | no | `Assets/ShooterMover/Tests/PlayMode/Weapons/Live/InventoryWeaponRuntimePlayModeFakes.cs` |
| `InventoryWeaponRuntimePlayModeTests` | `InventoryWeaponPlayModeTests` | class | Runtime | no | `Assets/ShooterMover/Tests/PlayMode/Weapons/Live/InventoryWeaponRuntimePlayModeFixtures.cs` |
| `InventoryWeaponRuntimePlayModeTests` | `InventoryWeaponPlayModeTests` | class | Runtime | no | `Assets/ShooterMover/Tests/PlayMode/Weapons/Live/InventoryWeaponRuntimePlayModeTests.cs` |
| `CharacterSelectStageV1` | `CharacterSelectStage` | enum | — | yes | `Assets/ShooterMover/UI/CharacterSelect/CharacterSelectControllerV1.cs` |
| `CharacterSelectionRecordingRouteSinkV1` | `CharacterSelectionRecordingRouteSink` | class | — | yes | `Assets/ShooterMover/UI/CharacterSelect/CharacterSelectControllerV1.cs` |
| `CharacterSelectControllerV1` | `CharacterSelectController` | class | — | yes | `Assets/ShooterMover/UI/CharacterSelect/CharacterSelectControllerV1.cs` |
| `CraftingScreenControllerV1` | `CraftingScreenController` | class | — | yes | `Assets/ShooterMover/UI/Crafting/CraftingScreenControllerV1.cs` |
| `HubRoutePlaceholderAdapterV1` | `HubRoutePlaceholder` | class | Adapter | yes | `Assets/ShooterMover/UI/Hub/HubFlowControllerV1.cs` |
| `HubFlowControllerV1` | `HubFlowController` | class | — | yes | `Assets/ShooterMover/UI/Hub/HubFlowControllerV1.cs` |
| `InventoryLoadoutScreenControllerV1` | `InventoryLoadoutScreenController` | class | — | yes | `Assets/ShooterMover/UI/InventoryLoadout/InventoryLoadoutScreenControllerV1.cs` |
| `WeaponInventoryCardPresentationV1` | `WeaponInventoryCardPresentation` | class | — | yes | `Assets/ShooterMover/UI/InventoryLoadout/WeaponInventoryCardPresentationV1.cs` |
| `LevelSelectionControllerV1` | `LevelSelectionController` | class | — | yes | `Assets/ShooterMover/UI/LevelSelection/LevelSelectionControllerV1.cs` |
| `ILevelSelectionSceneLoaderV1` | `ILevelSelectionSceneLoader` | interface | — | yes | `Assets/ShooterMover/UI/LevelSelection/LevelSelectionRoutingV1.cs` |
| `UnityLevelSelectionSceneLoaderV1` | `UnityLevelSelectionSceneLoader` | class | — | yes | `Assets/ShooterMover/UI/LevelSelection/LevelSelectionRoutingV1.cs` |
| `LevelSelectionRouteContextV1` | `LevelSelectionRouteContext` | class | — | yes | `Assets/ShooterMover/UI/LevelSelection/LevelSelectionRoutingV1.cs` |
| `UnityLevelSelectionRouteAdapterV1` | `UnityLevelSelectionRoute` | class | Adapter | yes | `Assets/ShooterMover/UI/LevelSelection/LevelSelectionRoutingV1.cs` |
| `RecordingLevelSelectionRouteAdapterV1` | `RecordingLevelSelectionRoute` | class | Adapter | yes | `Assets/ShooterMover/UI/LevelSelection/LevelSelectionRoutingV1.cs` |
| `LevelSelectionViewV1` | `LevelSelectionView` | class | — | yes | `Assets/ShooterMover/UI/LevelSelection/LevelSelectionViewV1.cs` |
| `RecordingPlaySelectionRouteAdapterV1` | `RecordingPlaySelectionRoute` | class | Adapter | yes | `Assets/ShooterMover/UI/PlaySelection/PlaySelectionControllerV1.cs` |
| `PlaySelectionControllerV1` | `PlaySelectionController` | class | — | yes | `Assets/ShooterMover/UI/PlaySelection/PlaySelectionControllerV1.cs` |
| `SystemIoAtomicSaveFilePortV1` | `SystemIoAtomicSaveFilePort` | class | — | yes | `Assets/ShooterMover/UI/ProductionFlow/CharacterAccount.cs` |
| `EnemyPlayerDamageChannelMapV1` | `EnemyPlayerDamageChannelMap` | class | — | yes | `Assets/ShooterMover/UI/ProductionFlow/EnemyPlayerDamageContractsV1.cs` |
| `EnemyPublisherResolutionStatusV1` | `EnemyPublisherResolutionStatus` | enum | — | yes | `Assets/ShooterMover/UI/ProductionFlow/EnemyPlayerDamageContractsV1.cs` |
| `EnemyPublisherReconciliationV1` | `EnemyPublisherReconciliation` | class | — | yes | `Assets/ShooterMover/UI/ProductionFlow/EnemyPlayerDamageContractsV1.cs` |
| `EnemyHitSubscriptionSetV1` | `EnemyHitSubscriptionSet` | class | — | yes | `Assets/ShooterMover/UI/ProductionFlow/EnemyPlayerDamageContractsV1.cs` |
| `EnemyPlayerDamageIntegrationInstallerV1` | `EnemyPlayerDamageIntegrationInstaller` | class | — | yes | `Assets/ShooterMover/UI/ProductionFlow/EnemyPlayerDamageIntegrationV1.cs` |
| `EnemyPlayerDamageIntegrationV1` | `EnemyPlayerDamageIntegration` | class | — | yes | `Assets/ShooterMover/UI/ProductionFlow/EnemyPlayerDamageIntegrationV1.cs` |
| `UnitySceneLoadPortV1` | `UnitySceneLoadPort` | class | — | yes | `Assets/ShooterMover/UI/ProductionFlow/GameFlow.cs` |
| `RankedSkillsPersistenceAdapterV2` | `RankedSkillsPersistence` | class | Adapter | yes | `Assets/ShooterMover/UI/ProductionFlow/GameFlow.cs` |
| `SkillsNavigationAdapter` | `SkillsNavigation` | class | Adapter | no | `Assets/ShooterMover/UI/ProductionFlow/GameFlow.cs` |
| `ShopNavigationAdapter` | `ShopNavigation` | class | Adapter | no | `Assets/ShooterMover/UI/ProductionFlow/GameFlow.cs` |
| `PlayNavigationAdapter` | `PlayNavigation` | class | Adapter | no | `Assets/ShooterMover/UI/ProductionFlow/GameFlow.cs` |
| `LevelNavigationAdapter` | `LevelNavigation` | class | Adapter | no | `Assets/ShooterMover/UI/ProductionFlow/GameFlow.cs` |
| `IPlayablePlayerDamageReceiverV1` | `IPlayablePlayerDamageReceiver` | interface | — | yes | `Assets/ShooterMover/UI/ProductionFlow/PlayablePlayerVitalsV1.cs` |
| `PlayablePlayerDamageCommandFactoryV1` | `PlayablePlayerDamageCommandFactory` | class | — | yes | `Assets/ShooterMover/UI/ProductionFlow/PlayablePlayerVitalsV1.cs` |
| `IPlayablePlayerHubReturnRequestV1` | `IPlayablePlayerHubReturnRequest` | interface | — | yes | `Assets/ShooterMover/UI/ProductionFlow/PlayablePlayerVitalsV1.cs` |
| `PlayablePlayerHubReturnAuthorityGuardV1` | `PlayablePlayerHubReturnGuard` | class | Authority | yes | `Assets/ShooterMover/UI/ProductionFlow/PlayablePlayerVitalsV1.cs` |
| `ProductionPlayablePlayerHubReturnRequestV1` | `PlayablePlayerHubReturnRequest` | class | Production | yes | `Assets/ShooterMover/UI/ProductionFlow/PlayablePlayerVitalsV1.cs` |
| `PlayablePlayerDefeatedFactV1` | `PlayablePlayerDefeatedFact` | class | — | yes | `Assets/ShooterMover/UI/ProductionFlow/PlayablePlayerVitalsV1.cs` |
| `PlayablePlayerVitalsInstallerV1` | `PlayablePlayerVitalsInstaller` | class | — | yes | `Assets/ShooterMover/UI/ProductionFlow/PlayablePlayerVitalsV1.cs` |
| `PlayerPrefsProductionFlowProfileStoreV1` | `PlayerPrefsFlowProfileStore` | class | Production | yes | `Assets/ShooterMover/UI/ProductionFlow/PlayerPrefsProductionFlowProfileStoreV1.cs` |
| `BoundCanonicalWeaponBlueprintResolverV1` | `BoundWeaponBlueprintResolver` | class | Canonical | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionCanonicalWeaponFireControllerV1.cs` |
| `ProductionCanonicalWeaponActorStateV1` | `WeaponActorState` | class | Production, Canonical | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionCanonicalWeaponFireControllerV1.cs` |
| `ProductionEquippedGunV1` | `EquippedGun` | class | Production | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionCanonicalWeaponFireControllerV1.cs` |
| `ProductionCanonicalWeaponFireInstallerV1` | `WeaponFireInstaller` | class | Production, Canonical | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionCanonicalWeaponFireControllerV1.cs` |
| `ProductionCanonicalWeaponFireControllerV1` | `WeaponFireController` | class | Production, Canonical | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionCanonicalWeaponFireControllerV1.cs` |
| `ProductionCanonicalWeaponGameplayBindingV2` | `WeaponGameplayBinding` | class | Production, Canonical | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionCanonicalWeaponGameplayBindingV2.cs` |
| `ProductionCharacterSelectionStageV1` | `CharacterSelectionStage` | enum | Production | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionCharacterSelectionControllerV1.cs` |
| `ProductionCharacterSelectionControllerV1` | `CharacterSelectionController` | class | Production | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionCharacterSelectionControllerV1.cs` |
| `ProductionCharacterStrongboxBridgeV1` | `CharacterStrongboxBridge` | class | Production | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionCharacterStrongboxBridgeV1.cs` |
| `ProductionCollectedRunRewardRecoveryV2` | `CollectedRunRewardRecovery` | class | Production | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionCollectedRunRewardRecoveryV2.cs` |
| `ProductionCollectedRunRewardResultsOverlay` | `CollectedRunRewardResultsOverlay` | class | Production | no | `Assets/ShooterMover/UI/ProductionFlow/ProductionCollectedRunRewardResultsOverlay.cs` |
| `ProductionCollectedRunRewardTerminalNoticeV1` | `CollectedRunRewardTerminalNotice` | class | Production | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionCollectedRunRewardTerminalNoticeV1.cs` |
| `ProductionMainMenuControllerV1` | `MainMenuController` | class | Production | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionMainMenuControllerV1.cs` |
| `ProductionPlayableLevelControllerV1` | `PlayableLevelController` | class | Production | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionPlayableLevelControllerV1.cs` |
| `ProductionResultsSummaryV1` | `ResultsSummary` | class | Production | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionResultsControllerV1.cs` |
| `ProductionReadOnlyResultsBridgeV1` | `ReadOnlyResultsBridge` | class | Production | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionResultsControllerV1.cs` |
| `ProductionResultsControllerV1` | `ResultsController` | class | Production | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionResultsControllerV1.cs` |
| `ProductionRunRewardRuntimeV1` | `RunReward` | class | Production, Runtime | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionRunRewardRuntimeV1.cs` |
| `PendingRunRewardProjectionV1` | `PendingRunReward` | class | Projection | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionRunRewardRuntimeV1.cs` |
| `PendingAdmissionProjectionConsumerV1` | `PendingAdmissionConsumer` | class | Projection | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionRunRewardRuntimeV1.cs` |
| `ExplicitNoOpExperienceConsumerV1` | `ExplicitNoOpExperienceConsumer` | class | — | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionRunRewardRuntimeV1.cs` |
| `ExplicitNoOpKillStatisticsConsumerV1` | `ExplicitNoOpKillStatisticsConsumer` | class | — | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionRunRewardRuntimeV1.cs` |
| `TransactionalRunRewardEnemyConsumerV1` | `TransactionalRunRewardEnemyConsumer` | class | — | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionRunRewardRuntimeV1.cs` |
| `ExactRunEnemySourceContextResolverV1` | `ExactRunEnemySourceContextResolver` | class | — | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionRunRewardRuntimeV1.cs` |
| `UnsupportedPropSourceContextResolverV1` | `UnsupportedPropSourceContextResolver` | class | — | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionRunRewardRuntimeV1.cs` |
| `FrozenRunProgressionContextProviderV1` | `FrozenRunProgressionContextProvider` | class | — | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionRunRewardRuntimeV1.cs` |
| `ProductionProofOverlayRewardOverrideResolverV1` | `ProofOverlayRewardOverrideResolver` | class | Production | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionRunRewardRuntimeV1.cs` |
| `DeterministicProofRewardOverrideResolverV1` | `DeterministicProofRewardOverrideResolver` | class | — | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionRunRewardRuntimeV1.cs` |
| `ProductionRunFingerprintV1` | `RunFingerprint` | class | Production | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionRunRewardRuntimeV1.cs` |
| `ProductionRunRewardSceneCompositionV1` | `RunRewardScene` | class | Production, Composition | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionRunRewardSceneCompositionV1.cs` |
| `ProductionPlayableLevelStatInputResolverV1` | `PlayableLevelStatInputResolver` | class | Production | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionRunSessionPortsV1.cs` |
| `ProductionPlayableLevelRuntimePortFactoryV1` | `PlayableLevelPortFactory` | class | Production, Runtime | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionRunSessionPortsV1.cs` |
| `ImmutableRunLifecyclePortV1` | `ImmutableRunLifecyclePort` | class | — | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionRunSessionPortsV1.cs` |
| `SnapshotPlayerRunPortV1` | `SnapshotPlayerRunPort` | class | — | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionRunSessionPortsV1.cs` |
| `SnapshotWeaponRunPortV1` | `SnapshotWeaponRunPort` | class | — | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionRunSessionPortsV1.cs` |
| `SnapshotStatusRunPortV1` | `SnapshotStatusRunPort` | class | — | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionRunSessionPortsV1.cs` |
| `SnapshotConditionalRunPortV1` | `SnapshotConditionalRunPort` | class | — | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionRunSessionPortsV1.cs` |
| `SnapshotAbilityRunPortV1` | `SnapshotAbilityRunPort` | class | — | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionRunSessionPortsV1.cs` |
| `SnapshotRoomRunPortV1` | `SnapshotRoomRunPort` | class | — | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionRunSessionPortsV1.cs` |
| `UnsupportedMissionResultRunPortV1` | `UnsupportedMissionResultRunPort` | class | — | yes | `Assets/ShooterMover/UI/ProductionFlow/ProductionRunSessionPortsV1.cs` |
| `ShopScreenControllerV1` | `ShopScreenController` | class | — | yes | `Assets/ShooterMover/UI/Shop/ShopScreenControllerV1.cs` |
| `RecordingShopScreenRouteAdapterV1` | `RecordingShopScreenRoute` | class | Adapter | yes | `Assets/ShooterMover/UI/Shop/ShopScreenRuntimeHandoffV1.cs` |
| `ShopScreenRuntimeHandoffV1` | `ShopScreenHandoff` | class | Runtime | yes | `Assets/ShooterMover/UI/Shop/ShopScreenRuntimeHandoffV1.cs` |
| `ISkillsScreenPresenterV1` | `ISkillsScreenPresenter` | interface | — | yes | `Assets/ShooterMover/UI/Skills/SkillsScreenHubAdapterV1.cs` |
| `ISkillsScreenNavigationPortV1` | `ISkillsScreenNavigationPort` | interface | — | yes | `Assets/ShooterMover/UI/Skills/SkillsScreenHubAdapterV1.cs` |
| `DelegateSkillsScreenNavigationPortV1` | `DelegateSkillsScreenNavigationPort` | class | — | yes | `Assets/ShooterMover/UI/Skills/SkillsScreenHubAdapterV1.cs` |
| `SkillsHubDestinationAdapterV1` | `SkillsHubDestination` | class | Adapter | yes | `Assets/ShooterMover/UI/Skills/SkillsScreenHubAdapterV1.cs` |
| `LootPickupPresentationKindV1` | `LootPickupPresentationKind` | enum | — | yes | `Assets/ShooterMover/UI/StrongboxOpening/LootPickupPresentationModelsV1.cs` |
| `LootPickupPresentationV1` | `LootPickupPresentation` | class | — | yes | `Assets/ShooterMover/UI/StrongboxOpening/LootPickupPresentationModelsV1.cs` |
| `RunLootTotalsPresentationV1` | `RunLootTotalsPresentation` | class | — | yes | `Assets/ShooterMover/UI/StrongboxOpening/LootPickupPresentationModelsV1.cs` |
| `RunLootTotalsProjectorV1` | `RunLootTotalsProjector` | class | — | yes | `Assets/ShooterMover/UI/StrongboxOpening/LootPickupPresentationModelsV1.cs` |
| `LootPickupRunProjection2D` | `LootPickupRun2D` | class | Projection | no | `Assets/ShooterMover/UI/StrongboxOpening/LootPickupRunProjection2D.cs` |
| `DevelopmentPickupCollectionResultV1` | `DevelopmentPickupCollectionResult` | class | — | yes | `Assets/ShooterMover/UI/StrongboxOpening/LootPresentationDevelopmentPickupFixtureV1.cs` |
| `DevelopmentPickupAuthorityFixtureV1` | `DevelopmentPickupFixture` | class | Authority | yes | `Assets/ShooterMover/UI/StrongboxOpening/LootPresentationDevelopmentPickupFixtureV1.cs` |
| `LootRunHudViewV1` | `LootRunHudView` | class | — | yes | `Assets/ShooterMover/UI/StrongboxOpening/LootRunHudViewV1.cs` |
| `OwnedStrongboxGroupsViewV1` | `OwnedStrongboxGroupsView` | class | — | yes | `Assets/ShooterMover/UI/StrongboxOpening/OwnedStrongboxGroupsViewV1.cs` |
| `OwnedStrongboxInstancePresentationV1` | `OwnedStrongboxInstancePresentation` | class | — | yes | `Assets/ShooterMover/UI/StrongboxOpening/StrongboxGroupingPresentationV1.cs` |
| `OwnedStrongboxGroupPresentationV1` | `OwnedStrongboxGroupPresentation` | class | — | yes | `Assets/ShooterMover/UI/StrongboxOpening/StrongboxGroupingPresentationV1.cs` |
| `StrongboxGroupingProjectorV1` | `StrongboxGroupingProjector` | class | — | yes | `Assets/ShooterMover/UI/StrongboxOpening/StrongboxGroupingPresentationV1.cs` |
| `ExactStrongboxSelectionV1` | `ExactStrongboxSelection` | class | — | yes | `Assets/ShooterMover/UI/StrongboxOpening/StrongboxGroupingPresentationV1.cs` |
| `StrongboxPresentationPlaybackV1` | `StrongboxPresentationPlayback` | class | — | yes | `Assets/ShooterMover/UI/StrongboxOpening/StrongboxGroupingPresentationV1.cs` |
| `StrongboxRevealStageV1` | `StrongboxRevealStage` | enum | — | yes | `Assets/ShooterMover/UI/StrongboxOpening/StrongboxOpeningController.cs` |
| `StrongboxRewardPresentationKindV1` | `StrongboxRewardPresentationKind` | enum | — | yes | `Assets/ShooterMover/UI/StrongboxOpening/StrongboxOpeningController.cs` |
| `StrongboxOpeningPreviewConfigurationV1` | `StrongboxOpeningPreviewConfiguration` | class | — | yes | `Assets/ShooterMover/UI/StrongboxOpening/StrongboxOpeningController.cs` |
| `StrongboxRewardRevealItemV1` | `StrongboxRewardRevealItem` | class | — | yes | `Assets/ShooterMover/UI/StrongboxOpening/StrongboxOpeningController.cs` |
| `StrongboxOpeningPresentationResultV1` | `StrongboxOpeningPresentationResult` | class | — | yes | `Assets/ShooterMover/UI/StrongboxOpening/StrongboxOpeningController.cs` |
| `StrongboxOpeningRuntimePortV1` | `StrongboxOpeningPort` | class | Runtime | yes | `Assets/ShooterMover/UI/StrongboxOpening/StrongboxOpeningController.cs` |
| `StrongboxRewardRevealProjectorV1` | `StrongboxRewardRevealProjector` | class | — | yes | `Assets/ShooterMover/UI/StrongboxOpening/StrongboxOpeningController.cs` |
| `StrongboxOpeningSceneSessionV1` | `StrongboxOpeningSceneSession` | class | — | yes | `Assets/ShooterMover/UI/StrongboxOpening/StrongboxOpeningController.cs` |
| `StrongboxOpeningPresentationViewV1` | `StrongboxOpeningPresentationView` | class | — | yes | `Assets/ShooterMover/UI/StrongboxOpening/StrongboxOpeningPresentationViewV1.cs` |
| `StrongboxRewardCardsViewV1` | `StrongboxRewardCardsView` | class | — | yes | `Assets/ShooterMover/UI/StrongboxOpening/StrongboxRewardCardsViewV1.cs` |

## Files requiring matching moves

- `Assets/ShooterMover/Content/Definitions/Characters/Selection/BuiltInCharacterSelectionCatalogV1.cs` → `BuiltInCharacterSelectionCatalog`
- `Assets/ShooterMover/Content/Definitions/Characters/Selection/BuiltInCharacterSelectionCatalogV1.cs.meta` → `BuiltInCharacterSelectionCatalog`
- `Assets/ShooterMover/Content/Definitions/Crafting/CraftingRecipeDefinitionAssetV1.cs` → `CraftingRecipeDefinitionAsset`
- `Assets/ShooterMover/Content/Definitions/Crafting/CraftingRecipeDefinitionAssetV1.cs.meta` → `CraftingRecipeDefinitionAsset`
- `Assets/ShooterMover/Content/Definitions/Enemies/BuiltInEnemyCatalogRegistryV1.cs` → `BuiltInEnemyCatalogRegistry`
- `Assets/ShooterMover/Content/Definitions/Enemies/BuiltInEnemyCatalogRegistryV1.cs.meta` → `BuiltInEnemyCatalogRegistry`
- `Assets/ShooterMover/Content/Definitions/Flow/PlayModes/PlayModeCatalogDefinitionV1.cs` → `PlayModeCatalogDefinition`
- `Assets/ShooterMover/Content/Definitions/Flow/PlayModes/PlayModeCatalogDefinitionV1.cs.meta` → `PlayModeCatalogDefinition`
- `Assets/ShooterMover/Content/Definitions/Levels/Selection/LevelSelectionCatalogDefinitionV1.cs` → `LevelSelectionCatalogDefinition`
- `Assets/ShooterMover/Content/Definitions/Levels/Selection/LevelSelectionCatalogDefinitionV1.cs.meta` → `LevelSelectionCatalogDefinition`
- `Assets/ShooterMover/Content/Definitions/Missions/Rooms/BuiltInRoomContentObjectCatalogV1.cs` → `BuiltInRoomContentObjectCatalog`
- `Assets/ShooterMover/Content/Definitions/Missions/Rooms/BuiltInRoomContentObjectCatalogV1.cs.meta` → `BuiltInRoomContentObjectCatalog`
- `Assets/ShooterMover/Content/Definitions/Missions/Rooms/GridV2.meta` → `Grid`
- `Assets/ShooterMover/Content/Definitions/Missions/Rooms/Level1AuthorableRoomDefinitionV1.cs` → `Level1AuthorableRoomDefinition`
- `Assets/ShooterMover/Content/Definitions/Missions/Rooms/Level1AuthorableRoomDefinitionV1.cs.meta` → `Level1AuthorableRoomDefinition`
- `Assets/ShooterMover/Content/Definitions/Missions/Rooms/Level1LiveRoomGraphDefinitionV1.cs` → `Level1LiveRoomGraphDefinition`
- `Assets/ShooterMover/Content/Definitions/Missions/Rooms/Level1LiveRoomGraphDefinitionV1.cs.meta` → `Level1LiveRoomGraphDefinition`
- `Assets/ShooterMover/Content/Definitions/Missions/Rooms/Level1RoomGraphDefinitionV1.cs` → `Level1RoomGraphDefinition`
- `Assets/ShooterMover/Content/Definitions/Missions/Rooms/Level1RoomGraphDefinitionV1.cs.meta` → `Level1RoomGraphDefinition`
- `Assets/ShooterMover/Content/Definitions/Strongboxes/StrongboxDefinitionSetV1.cs` → `StrongboxDefinitionSet`
- `Assets/ShooterMover/Content/Definitions/Strongboxes/StrongboxDefinitionSetV1.cs.meta` → `StrongboxDefinitionSet`
- `Assets/ShooterMover/Content/Generated/Missions/Rooms/GridV2.meta` → `Grid`
- `Assets/ShooterMover/ContentPackages/Props/DestructibleProps/DestructiblePropAuthority.cs` → `DestructibleProp`
- `Assets/ShooterMover/ContentPackages/Props/DestructibleProps/DestructiblePropAuthority.cs.meta` → `DestructibleProp`
- `Assets/ShooterMover/ContentPackages/Props/DestructibleProps/DestructiblePropTerminalProvenanceV1.cs` → `DestructiblePropTerminalProvenance`
- `Assets/ShooterMover/ContentPackages/Props/DestructibleProps/DestructiblePropTerminalProvenanceV1.cs.meta` → `DestructiblePropTerminalProvenance`
- `Assets/ShooterMover/ContentPackages/Weapons/Shared/Runtime/ProjectileExecutionPlanAdapter.cs` → `ProjectileExecutionPlan`
- `Assets/ShooterMover/ContentPackages/Weapons/Shared/Runtime/ProjectileExecutionPlanAdapter.cs.meta` → `ProjectileExecutionPlan`
- `Assets/ShooterMover/ContentPackages/Weapons/Shared/Runtime.meta` → ``
- `Assets/ShooterMover/Editor/BalanceSimulator/AuthoritativeStrongboxSimulationGatewayFactoryV1.cs` → `AuthoritativeStrongboxSimulationGatewayFactory`
- `Assets/ShooterMover/Editor/BalanceSimulator/AuthoritativeStrongboxSimulationGatewayFactoryV1.cs.meta` → `AuthoritativeStrongboxSimulationGatewayFactory`
- `Assets/ShooterMover/Editor/BalanceSimulator/AuthoritativeStrongboxSimulationProductionGatewayV1.cs` → `AuthoritativeStrongboxSimulationGateway`
- `Assets/ShooterMover/Editor/BalanceSimulator/AuthoritativeStrongboxSimulationProductionGatewayV1.cs.meta` → `AuthoritativeStrongboxSimulationGateway`
- `Assets/ShooterMover/Editor/BalanceSimulator/AuthoritativeStrongboxSimulationRunnerV1.cs` → `AuthoritativeStrongboxSimulationRunner`
- `Assets/ShooterMover/Editor/BalanceSimulator/AuthoritativeStrongboxSimulationRunnerV1.cs.meta` → `AuthoritativeStrongboxSimulationRunner`
- `Assets/ShooterMover/Editor/BalanceSimulator/AuthoritativeStrongboxSimulatorRuntimeV1.cs` → `AuthoritativeStrongboxSimulator`
- `Assets/ShooterMover/Editor/BalanceSimulator/AuthoritativeStrongboxSimulatorRuntimeV1.cs.meta` → `AuthoritativeStrongboxSimulator`
- `Assets/ShooterMover/Editor/BalanceSimulator/BalanceSimulationModelsV1.cs` → `BalanceSimulationModels`
- `Assets/ShooterMover/Editor/BalanceSimulator/BalanceSimulationModelsV1.cs.meta` → `BalanceSimulationModels`
- `Assets/ShooterMover/Editor/BalanceSimulator/BalanceSimulationServiceV1.cs` → `BalanceSimulation`
- `Assets/ShooterMover/Editor/BalanceSimulator/BalanceSimulationServiceV1.cs.meta` → `BalanceSimulation`
- `Assets/ShooterMover/Editor/BalanceSimulator/DropSourceSimulationRequestV1.cs` → `DropSourceSimulationRequest`
- `Assets/ShooterMover/Editor/BalanceSimulator/DropSourceSimulationRequestV1.cs.meta` → `DropSourceSimulationRequest`
- `Assets/ShooterMover/Editor/BalanceSimulator/DropSourceSimulationRuntimeV1.cs` → `DropSourceSimulation`
- `Assets/ShooterMover/Editor/BalanceSimulator/DropSourceSimulationRuntimeV1.cs.meta` → `DropSourceSimulation`
- `Assets/ShooterMover/Editor/BalanceSimulator/LootboxSimulatorRuntimeV1.cs` → `LootboxSimulator`
- `Assets/ShooterMover/Editor/BalanceSimulator/LootboxSimulatorRuntimeV1.cs.meta` → `LootboxSimulator`
- `Assets/ShooterMover/Editor/BalanceSimulator/MultiplayerDropSimulationRuntimeV1.cs` → `MultiplayerDropSimulation`
- `Assets/ShooterMover/Editor/BalanceSimulator/MultiplayerDropSimulationRuntimeV1.cs.meta` → `MultiplayerDropSimulation`
- `Assets/ShooterMover/Editor/BalanceSimulator/RewardSimulationParticipantInputV1.cs` → `RewardSimulationParticipantInput`
- `Assets/ShooterMover/Editor/BalanceSimulator/RewardSimulationParticipantInputV1.cs.meta` → `RewardSimulationParticipantInput`
- `Assets/ShooterMover/Editor/BalanceSimulator/RewardSimulationParticipantReportV1.cs` → `RewardSimulationParticipantReport`
- `Assets/ShooterMover/Editor/BalanceSimulator/RewardSimulationParticipantReportV1.cs.meta` → `RewardSimulationParticipantReport`
- `Assets/ShooterMover/Editor/BalanceSimulator/RewardSimulationReportV1.cs` → `RewardSimulationReport`
- `Assets/ShooterMover/Editor/BalanceSimulator/RewardSimulationReportV1.cs.meta` → `RewardSimulationReport`
- `Assets/ShooterMover/Editor/BalanceSimulator/RuntimeBalanceScenarioV1.cs` → `BalanceScenario`
- `Assets/ShooterMover/Editor/BalanceSimulator/RuntimeBalanceScenarioV1.cs.meta` → `BalanceScenario`
- `Assets/ShooterMover/Editor/BalanceSimulator/WeaponLootCardEditorDrawerV1.cs` → `WeaponLootCardEditorDrawer`
- `Assets/ShooterMover/Editor/BalanceSimulator/WeaponLootCardEditorDrawerV1.cs.meta` → `WeaponLootCardEditorDrawer`
- `Assets/ShooterMover/Editor/BalanceSimulator/WeaponLootCardProjectionV1.cs` → `WeaponLootCard`
- `Assets/ShooterMover/Editor/BalanceSimulator/WeaponLootCardProjectionV1.cs.meta` → `WeaponLootCard`
- `Assets/ShooterMover/Editor/EnemyReadiness/EnemyReadinessWindowV1.cs` → `EnemyReadinessWindow`
- `Assets/ShooterMover/Editor/EnemyReadiness/EnemyReadinessWindowV1.cs.meta` → `EnemyReadinessWindow`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridDoorOperationsV2.cs` → `LevelGridDoorOperations`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridDoorOperationsV2.cs.meta` → `LevelGridDoorOperations`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridEditorOperationsV2.cs` → `LevelGridEditorOperations`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridEditorOperationsV2.cs.meta` → `LevelGridEditorOperations`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridEditorProjectionV2.cs` → `LevelGridEditor`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridEditorProjectionV2.cs.meta` → `LevelGridEditor`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridEditorWindowV2.cs` → `LevelGridEditorWindow`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridEditorWindowV2.cs.meta` → `LevelGridEditorWindow`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridLegacySurfaceGuardsV2.cs` → `LevelGridLegacySurfaceGuards`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridLegacySurfaceGuardsV2.cs.meta` → `LevelGridLegacySurfaceGuards`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableAssetChangeWatcherV2.cs` → `LevelGridPlayableAssetChangeWatcher`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableAssetChangeWatcherV2.cs.meta` → `LevelGridPlayableAssetChangeWatcher`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableBuildFacadeV2.cs` → `LevelGridPlayableBuildFacade`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableBuildFacadeV2.cs.meta` → `LevelGridPlayableBuildFacade`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableBuildPathsV2.cs` → `LevelGridPlayableBuildPaths`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableBuildPathsV2.cs.meta` → `LevelGridPlayableBuildPaths`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableMetadataOperationsV2.cs` → `LevelGridPlayableMetadataOperations`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableMetadataOperationsV2.cs.meta` → `LevelGridPlayableMetadataOperations`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableProvenanceV2.cs` → `LevelGridPlayableProvenance`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableProvenanceV2.cs.meta` → `LevelGridPlayableProvenance`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableStatusV2.cs` → `LevelGridPlayableStatus`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableStatusV2.cs.meta` → `LevelGridPlayableStatus`
- `Assets/ShooterMover/Production/EnemyAttackPatterns/EnemyAttackPatternProductionController2D.cs` → `EnemyAttackPatternController2D`
- `Assets/ShooterMover/Production/EnemyAttackPatterns/EnemyAttackPatternProductionController2D.cs.meta` → `EnemyAttackPatternController2D`
- `Assets/ShooterMover/Production/EnemyAttackPatterns/EnemyAttackPatternUnityBindingsV1.cs` → `EnemyAttackPatternUnityBindings`
- `Assets/ShooterMover/Production/EnemyAttackPatterns/EnemyAttackPatternUnityBindingsV1.cs.meta` → `EnemyAttackPatternUnityBindings`
- `Assets/ShooterMover/Production/EnemyAttackPatterns/EnemyAttackPatternUnityEmissionRealizerV1.cs` → `EnemyAttackPatternUnityEmissionRealizer`
- `Assets/ShooterMover/Production/EnemyAttackPatterns/EnemyAttackPatternUnityEmissionRealizerV1.cs.meta` → `EnemyAttackPatternUnityEmissionRealizer`
- `Assets/ShooterMover/Resources/ProductionLevels/GenericRuntimePresentation.prefab.meta` → `GenericPresentationprefab`
- `Assets/ShooterMover/Resources/ProductionLevels/ProductionCoverCrate.png.meta` → `CoverCratepng`
- `Assets/ShooterMover/Resources/ProductionLevels/ProductionIndustrialFloor.png.meta` → `IndustrialFloorpng`
- `Assets/ShooterMover/Resources/ProductionLevels/ProductionPlayerPresentation.prefab.meta` → `PlayerPresentationprefab`
- `Assets/ShooterMover/Resources/ProductionLevels/ProductionRoomDoor.png.meta` → `RoomDoorpng`
- `Assets/ShooterMover/Resources/ProductionLevels/ProductionRoomDoorPresentation.prefab.meta` → `RoomDoorPresentationprefab`
- `Assets/ShooterMover/Resources/ProductionLevels.meta` → `Levels`
- `Assets/ShooterMover/Runtime/Application/Characters/Selection/CharacterSelectionServiceV1.cs` → `CharacterSelection`
- `Assets/ShooterMover/Runtime/Application/Characters/Selection/CharacterSelectionServiceV1.cs.meta` → `CharacterSelection`
- `Assets/ShooterMover/Runtime/Application/Crafting/CraftingServiceV1.cs` → `Crafting`
- `Assets/ShooterMover/Runtime/Application/Crafting/CraftingServiceV1.cs.meta` → `Crafting`
- `Assets/ShooterMover/Runtime/Application/Crafting/Integration/CraftingInventoryEquipServiceV1.cs` → `CraftingInventoryEquip`
- `Assets/ShooterMover/Runtime/Application/Crafting/Integration/CraftingInventoryEquipServiceV1.cs.meta` → `CraftingInventoryEquip`
- `Assets/ShooterMover/Runtime/Application/Crafting/Presentation/CraftingScreenPresentationV1.cs` → `CraftingScreenPresentation`
- `Assets/ShooterMover/Runtime/Application/Crafting/Presentation/CraftingScreenPresentationV1.cs.meta` → `CraftingScreenPresentation`
- `Assets/ShooterMover/Runtime/Application/Development/RunDebug/RunDebugContractsV1.cs` → `RunDebugContracts`
- `Assets/ShooterMover/Runtime/Application/Development/RunDebug/RunDebugContractsV1.cs.meta` → `RunDebugContracts`
- `Assets/ShooterMover/Runtime/Application/Development/RunDebug/RunDebugPanelSessionV1.cs` → `RunDebugPanelSession`
- `Assets/ShooterMover/Runtime/Application/Development/RunDebug/RunDebugPanelSessionV1.cs.meta` → `RunDebugPanelSession`
- `Assets/ShooterMover/Runtime/Application/Economy/Money/MoneyWalletService.cs` → `MoneyWallet`
- `Assets/ShooterMover/Runtime/Application/Economy/Money/MoneyWalletService.cs.meta` → `MoneyWallet`
- `Assets/ShooterMover/Runtime/Application/Economy/Scrap/ScrapWalletServiceV1.cs` → `ScrapWallet`
- `Assets/ShooterMover/Runtime/Application/Economy/Scrap/ScrapWalletServiceV1.cs.meta` → `ScrapWallet`
- `Assets/ShooterMover/Runtime/Application/Enemies/Catalog/EnemyCatalogImportResultV1.cs` → `EnemyCatalogImportResult`
- `Assets/ShooterMover/Runtime/Application/Enemies/Catalog/EnemyCatalogImportResultV1.cs.meta` → `EnemyCatalogImportResult`
- `Assets/ShooterMover/Runtime/Application/Enemies/Catalog/EnemyCatalogJsonDtosV1.cs` → `EnemyCatalogJsonDtos`
- `Assets/ShooterMover/Runtime/Application/Enemies/Catalog/EnemyCatalogJsonDtosV1.cs.meta` → `EnemyCatalogJsonDtos`
- `Assets/ShooterMover/Runtime/Application/Enemies/Catalog/EnemyCatalogJsonImporterV1.cs` → `EnemyCatalogJsonImporter`
- `Assets/ShooterMover/Runtime/Application/Enemies/Catalog/EnemyCatalogJsonImporterV1.cs.meta` → `EnemyCatalogJsonImporter`
- `Assets/ShooterMover/Runtime/Application/Equipment/Upgrades/AugmentUpgradeConfirmationServiceV1.cs` → `AugmentUpgradeConfirmation`
- `Assets/ShooterMover/Runtime/Application/Equipment/Upgrades/AugmentUpgradeConfirmationServiceV1.cs.meta` → `AugmentUpgradeConfirmation`
- `Assets/ShooterMover/Runtime/Application/Equipment/Upgrades/AugmentUpgradeExecutionV1.cs` → `AugmentUpgradeExecution`
- `Assets/ShooterMover/Runtime/Application/Equipment/Upgrades/AugmentUpgradeExecutionV1.cs.meta` → `AugmentUpgradeExecution`
- `Assets/ShooterMover/Runtime/Application/Equipment/Upgrades/AugmentUpgradePreparationV1.cs` → `AugmentUpgradePreparation`
- `Assets/ShooterMover/Runtime/Application/Equipment/Upgrades/AugmentUpgradePreparationV1.cs.meta` → `AugmentUpgradePreparation`
- `Assets/ShooterMover/Runtime/Application/Equipment/Upgrades/AugmentUpgradePreparedRecordV1.cs` → `AugmentUpgradePreparedRecord`
- `Assets/ShooterMover/Runtime/Application/Equipment/Upgrades/AugmentUpgradePreparedRecordV1.cs.meta` → `AugmentUpgradePreparedRecord`
- `Assets/ShooterMover/Runtime/Application/Equipment/Upgrades/AugmentUpgradeServiceV1.cs` → `AugmentUpgrade`
- `Assets/ShooterMover/Runtime/Application/Equipment/Upgrades/AugmentUpgradeServiceV1.cs.meta` → `AugmentUpgrade`
- `Assets/ShooterMover/Runtime/Application/Flow/Hub/HubNavigationServiceV1.cs` → `HubNavigation`
- `Assets/ShooterMover/Runtime/Application/Flow/Hub/HubNavigationServiceV1.cs.meta` → `HubNavigation`
- `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/ILevelSelectionRouteAdapterV1.cs` → `ILevelSelectionRoute`
- `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/ILevelSelectionRouteAdapterV1.cs.meta` → `ILevelSelectionRoute`
- `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelRecommendationV1.cs` → `LevelRecommendation`
- `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelRecommendationV1.cs.meta` → `LevelRecommendation`
- `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelSelectionCatalogV1.cs` → `LevelSelectionCatalog`
- `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelSelectionCatalogV1.cs.meta` → `LevelSelectionCatalog`
- `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelSelectionDefinitionV1.cs` → `LevelSelectionDefinition`
- `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelSelectionDefinitionV1.cs.meta` → `LevelSelectionDefinition`
- `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelSelectionEnumsV1.cs` → `LevelSelectionEnums`
- `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelSelectionEnumsV1.cs.meta` → `LevelSelectionEnums`
- `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelSelectionResultV1.cs` → `LevelSelectionResult`
- `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelSelectionResultV1.cs.meta` → `LevelSelectionResult`
- `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelSelectionServiceV1.cs` → `LevelSelection`
- `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelSelectionServiceV1.cs.meta` → `LevelSelection`
- `Assets/ShooterMover/Runtime/Application/Flow/PlaySelection/PlaySelectionServiceV1.cs` → `PlaySelection`
- `Assets/ShooterMover/Runtime/Application/Flow/PlaySelection/PlaySelectionServiceV1.cs.meta` → `PlaySelection`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/CanonicalFirstPlayerHoldingsAuthorityV2.cs` → `FirstPlayerHoldings`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/CanonicalFirstPlayerHoldingsAuthorityV2.cs.meta` → `FirstPlayerHoldings`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/CanonicalWeaponInventoryScreenV2.cs` → `WeaponInventoryScreen`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/CanonicalWeaponInventoryScreenV2.cs.meta` → `WeaponInventoryScreen`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionCharacterAuthorityAdaptersV1.cs` → `CharacterAdapters`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionCharacterAuthorityAdaptersV1.cs.meta` → `CharacterAdapters`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionCharacterRuntimeGraphV1.cs` → `CharacterGraph`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionCharacterRuntimeGraphV1.cs.meta` → `CharacterGraph`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionCharacterStrongboxBridgeV1.cs` → `CharacterStrongboxBridge`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionCharacterStrongboxBridgeV1.cs.meta` → `CharacterStrongboxBridge`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionCharacterStrongboxCompositionV1.cs` → `CharacterStrongbox`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionCharacterStrongboxCompositionV1.cs.meta` → `CharacterStrongbox`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionFlowSessionV1.cs` → `FlowSession`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionFlowSessionV1.cs.meta` → `FlowSession`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionPlayerLoadoutRuntimeV1.cs` → `PlayerLoadout`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionPlayerLoadoutRuntimeV1.cs.meta` → `PlayerLoadout`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponCatalogProvider.cs` → `WeaponCatalogProvider`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponCatalogProvider.cs.meta` → `WeaponCatalogProvider`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponHoldingsV2.cs` → `WeaponHoldings`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponHoldingsV2.cs.meta` → `WeaponHoldings`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponMountLoadoutRegistryV2.cs` → `WeaponMountLoadoutRegistry`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponMountLoadoutRegistryV2.cs.meta` → `WeaponMountLoadoutRegistry`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponMountLoadoutV2.cs` → `WeaponMountLoadout`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponMountLoadoutV2.cs.meta` → `WeaponMountLoadout`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponMountPolicyV1.cs` → `WeaponMountPolicy`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponMountPolicyV1.cs.meta` → `WeaponMountPolicy`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponOnboardingV1.cs` → `WeaponOnboarding`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponOnboardingV1.cs.meta` → `WeaponOnboarding`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponOnboardingV2.cs` → `WeaponOnboarding`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponOnboardingV2.cs.meta` → `WeaponOnboarding`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/RequiredCharacterComponentBackfillV1.cs` → `RequiredCharacterComponentBackfill`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/RequiredCharacterComponentBackfillV1.cs.meta` → `RequiredCharacterComponentBackfill`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/RetiredWeaponSaveMigrationV1.cs` → `RetiredWeaponSaveMigration`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/RetiredWeaponSaveMigrationV1.cs.meta` → `RetiredWeaponSaveMigration`
- `Assets/ShooterMover/Runtime/Application/Flow/Production.meta` → ``
- `Assets/ShooterMover/Runtime/Application/Holdings/PlayerHoldingsService.Persistence.cs` → `PlayerHoldingsPersistence`
- `Assets/ShooterMover/Runtime/Application/Holdings/PlayerHoldingsService.Persistence.cs.meta` → `PlayerHoldingsPersistence`
- `Assets/ShooterMover/Runtime/Application/Holdings/PlayerHoldingsService.SnapshotValidation.cs` → `PlayerHoldingsSnapshotValidation`
- `Assets/ShooterMover/Runtime/Application/Holdings/PlayerHoldingsService.SnapshotValidation.cs.meta` → `PlayerHoldingsSnapshotValidation`
- `Assets/ShooterMover/Runtime/Application/Holdings/PlayerHoldingsService.ValidationAndSnapshots.cs` → `PlayerHoldingsValidationAndSnapshots`
- `Assets/ShooterMover/Runtime/Application/Holdings/PlayerHoldingsService.ValidationAndSnapshots.cs.meta` → `PlayerHoldingsValidationAndSnapshots`
- `Assets/ShooterMover/Runtime/Application/Holdings/PlayerHoldingsService.cs` → `PlayerHoldings`
- `Assets/ShooterMover/Runtime/Application/Holdings/PlayerHoldingsService.cs.meta` → `PlayerHoldings`
- `Assets/ShooterMover/Runtime/Application/Inventory/LoadoutScreen/InventoryLoadoutScreenV1.cs` → `InventoryLoadoutScreen`
- `Assets/ShooterMover/Runtime/Application/Inventory/LoadoutScreen/InventoryLoadoutScreenV1.cs.meta` → `InventoryLoadoutScreen`
- `Assets/ShooterMover/Runtime/Application/Missions/Results/MissionResultsSessionV1.cs` → `MissionResultsSession`
- `Assets/ShooterMover/Runtime/Application/Missions/Results/MissionResultsSessionV1.cs.meta` → `MissionResultsSession`
- `Assets/ShooterMover/Runtime/Application/Missions/Results/MissionRunAuthorityPortsV1.cs` → `MissionRunPorts`
- `Assets/ShooterMover/Runtime/Application/Missions/Results/MissionRunAuthorityPortsV1.cs.meta` → `MissionRunPorts`
- `Assets/ShooterMover/Runtime/Application/Missions/Results/MissionRunExistingAuthorityPortV1.cs` → `MissionRunExistingPort`
- `Assets/ShooterMover/Runtime/Application/Missions/Results/MissionRunExistingAuthorityPortV1.cs.meta` → `MissionRunExistingPort`
- `Assets/ShooterMover/Runtime/Application/Missions/Results/MissionRunResultAuthorityV1.cs` → `MissionRunResult`
- `Assets/ShooterMover/Runtime/Application/Missions/Results/MissionRunResultAuthorityV1.cs.meta` → `MissionRunResult`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomAccessJsonImporterV1.cs` → `RoomAccessJsonImporter`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomAccessJsonImporterV1.cs.meta` → `RoomAccessJsonImporter`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentJsonDtosV1.cs` → `RoomContentJsonDtos`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentJsonDtosV1.cs.meta` → `RoomContentJsonDtos`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentJsonImporterV1.cs` → `RoomContentJsonImporter`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentJsonImporterV1.cs.meta` → `RoomContentJsonImporter`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentModelV1.cs` → `RoomContentModel`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentModelV1.cs.meta` → `RoomContentModel`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomAccessAuthorityV1.cs` → `RoomAccess`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomAccessAuthorityV1.cs.meta` → `RoomAccess`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveAccessFactProjectionV1.cs` → `RoomLiveAccessFact`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveAccessFactProjectionV1.cs.meta` → `RoomLiveAccessFact`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveRuntimeAuthorityCoreV1.cs` → `RoomLiveCore`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveRuntimeAuthorityCoreV1.cs.meta` → `RoomLiveCore`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveRuntimeHelpersV1.cs` → `RoomLiveHelpers`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveRuntimeHelpersV1.cs.meta` → `RoomLiveHelpers`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveRuntimeProjectionsV1.cs` → `RoomLiveProjections`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveRuntimeProjectionsV1.cs.meta` → `RoomLiveProjections`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveRuntimeTraversalV1.cs` → `RoomLiveTraversal`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveRuntimeTraversalV1.cs.meta` → `RoomLiveTraversal`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomMissionLayoutV1.cs` → `RoomMissionLayout`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomMissionLayoutV1.cs.meta` → `RoomMissionLayout`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomRuntimeAuthorityV1.cs` → `Room`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomRuntimeAuthorityV1.cs.meta` → `Room`
- `Assets/ShooterMover/Runtime/Application/Modifiers/Events/ActiveEventModifierProjectionServiceV1.cs` → `ActiveEventModifier`
- `Assets/ShooterMover/Runtime/Application/Modifiers/Events/ActiveEventModifierProjectionServiceV1.cs.meta` → `ActiveEventModifier`
- `Assets/ShooterMover/Runtime/Application/Modifiers/Events/EventStampedCommandEnvelopeV1.cs` → `EventStampedCommandEnvelope`
- `Assets/ShooterMover/Runtime/Application/Modifiers/Events/EventStampedCommandEnvelopeV1.cs.meta` → `EventStampedCommandEnvelope`
- `Assets/ShooterMover/Runtime/Application/Modifiers/FactWindowConditionAuthorityV1.cs` → `FactWindowCondition`
- `Assets/ShooterMover/Runtime/Application/Modifiers/FactWindowConditionAuthorityV1.cs.meta` → `FactWindowCondition`
- `Assets/ShooterMover/Runtime/Application/Modifiers/StatusEffects/FactWindowStatusEffectBridgeV1.cs` → `FactWindowStatusEffectBridge`
- `Assets/ShooterMover/Runtime/Application/Modifiers/StatusEffects/FactWindowStatusEffectBridgeV1.cs.meta` → `FactWindowStatusEffectBridge`
- `Assets/ShooterMover/Runtime/Application/Modifiers/StatusEffects/StatusEffectAuthorityV1.Commands.cs` → `StatusEffectV1Commands`
- `Assets/ShooterMover/Runtime/Application/Modifiers/StatusEffects/StatusEffectAuthorityV1.Commands.cs.meta` → `StatusEffectV1Commands`
- `Assets/ShooterMover/Runtime/Application/Modifiers/StatusEffects/StatusEffectAuthorityV1.Core.cs` → `StatusEffectV1Core`
- `Assets/ShooterMover/Runtime/Application/Modifiers/StatusEffects/StatusEffectAuthorityV1.Core.cs.meta` → `StatusEffectV1Core`
- `Assets/ShooterMover/Runtime/Application/Modifiers/StatusEffects/StatusEffectAuthorityV1.Snapshots.cs` → `StatusEffectV1Snapshots`
- `Assets/ShooterMover/Runtime/Application/Modifiers/StatusEffects/StatusEffectAuthorityV1.Snapshots.cs.meta` → `StatusEffectV1Snapshots`
- `Assets/ShooterMover/Runtime/Application/Modifiers/StatusEffects/StatusEffectAuthorityV1.Stacking.cs` → `StatusEffectV1Stacking`
- `Assets/ShooterMover/Runtime/Application/Modifiers/StatusEffects/StatusEffectAuthorityV1.Stacking.cs.meta` → `StatusEffectV1Stacking`
- `Assets/ShooterMover/Runtime/Application/Modifiers/StatusEffects/StatusEffectLocalHashV1.cs` → `StatusEffectLocalHash`
- `Assets/ShooterMover/Runtime/Application/Modifiers/StatusEffects/StatusEffectLocalHashV1.cs.meta` → `StatusEffectLocalHash`
- `Assets/ShooterMover/Runtime/Application/Persistence/Accounts/PlayerAccountSaveAuthorityV1.cs` → `PlayerAccountSave`
- `Assets/ShooterMover/Runtime/Application/Persistence/Accounts/PlayerAccountSaveAuthorityV1.cs.meta` → `PlayerAccountSave`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/AtomicPlayerAccountStoreV1.cs` → `AtomicPlayerAccountStore`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/AtomicPlayerAccountStoreV1.cs.meta` → `AtomicPlayerAccountStore`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/CanonicalSnapshotCodecV1.cs` → `SnapshotCodec`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/CanonicalSnapshotCodecV1.cs.meta` → `SnapshotCodec`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/CollectedRunRewardPersistenceExpectationV1.cs` → `CollectedRunRewardPersistenceExpectation`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/CollectedRunRewardPersistenceExpectationV1.cs.meta` → `CollectedRunRewardPersistenceExpectation`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/GeneratedEquipmentAugmentSignatureSaveComponentV1.cs` → `GeneratedEquipmentAugmentSignatureSaveComponent`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/GeneratedEquipmentAugmentSignatureSaveComponentV1.cs.meta` → `GeneratedEquipmentAugmentSignatureSaveComponent`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownExperienceMoneyCodecsV1.cs` → `KnownExperienceMoneyCodecs`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownExperienceMoneyCodecsV1.cs.meta` → `KnownExperienceMoneyCodecs`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownHoldingsCodecV1.cs` → `KnownHoldingsCodec`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownHoldingsCodecV1.cs.meta` → `KnownHoldingsCodec`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownLedgerScrapSkillLoadoutCodecsV1.cs` → `KnownLedgerScrapSkillLoadoutCodecs`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownLedgerScrapSkillLoadoutCodecsV1.cs.meta` → `KnownLedgerScrapSkillLoadoutCodecs`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownSaveComponentCodecCoreV1.cs` → `KnownSaveComponentCodecCore`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownSaveComponentCodecCoreV1.cs.meta` → `KnownSaveComponentCodecCore`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownSaveComponentVersionGuardV1.cs` → `KnownSaveComponentVersionGuard`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownSaveComponentVersionGuardV1.cs.meta` → `KnownSaveComponentVersionGuard`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownStrongboxCodecV1.cs` → `KnownStrongboxCodec`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownStrongboxCodecV1.cs.meta` → `KnownStrongboxCodec`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/PlayerAccountComponentSemanticsV1.cs` → `PlayerAccountComponentSemantics`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/PlayerAccountComponentSemanticsV1.cs.meta` → `PlayerAccountComponentSemantics`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/PlayerAccountRestoreCoordinatorV1.cs` → `PlayerAccountRestore`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/PlayerAccountRestoreCoordinatorV1.cs.meta` → `PlayerAccountRestore`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/SaveComponentAdaptersV1.cs` → `SaveComponentAdapters`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/SaveComponentAdaptersV1.cs.meta` → `SaveComponentAdapters`
- `Assets/ShooterMover/Runtime/Application/Persistence/Composition/CharacterCompositionCoordinatorV1.cs` → `Character`
- `Assets/ShooterMover/Runtime/Application/Persistence/Composition/CharacterCompositionCoordinatorV1.cs.meta` → `Character`
- `Assets/ShooterMover/Runtime/Application/Persistence/Composition/LegacyCharacterProfileMigrationV1.cs` → `LegacyCharacterProfileMigration`
- `Assets/ShooterMover/Runtime/Application/Persistence/Composition/LegacyCharacterProfileMigrationV1.cs.meta` → `LegacyCharacterProfileMigration`
- `Assets/ShooterMover/Runtime/Application/Persistence/Composition.meta` → ``
- `Assets/ShooterMover/Runtime/Application/Progression/Experience/EnemyRewards/EnemyExperienceRewardDefinitionsV1.cs` → `EnemyExperienceRewardDefinitions`
- `Assets/ShooterMover/Runtime/Application/Progression/Experience/EnemyRewards/EnemyExperienceRewardDefinitionsV1.cs.meta` → `EnemyExperienceRewardDefinitions`
- `Assets/ShooterMover/Runtime/Application/Progression/Experience/EnemyRewards/EnemyExperienceRewardOperationV1.cs` → `EnemyExperienceRewardOperation`
- `Assets/ShooterMover/Runtime/Application/Progression/Experience/EnemyRewards/EnemyExperienceRewardOperationV1.cs.meta` → `EnemyExperienceRewardOperation`
- `Assets/ShooterMover/Runtime/Application/Progression/Experience/EnemyRewards/EnemyExperienceRewardServiceV1.cs` → `EnemyExperienceReward`
- `Assets/ShooterMover/Runtime/Application/Progression/Experience/EnemyRewards/EnemyExperienceRewardServiceV1.cs.meta` → `EnemyExperienceReward`
- `Assets/ShooterMover/Runtime/Application/Progression/Experience/PlayerExperienceAuthorityV1.cs` → `PlayerExperience`
- `Assets/ShooterMover/Runtime/Application/Progression/Experience/PlayerExperienceAuthorityV1.cs.meta` → `PlayerExperience`
- `Assets/ShooterMover/Runtime/Application/Progression/Skills/RankedSkillAuthorityV2.cs` → `RankedSkill`
- `Assets/ShooterMover/Runtime/Application/Progression/Skills/SkillProgressionAuthorityV1.cs` → `SkillProgression`
- `Assets/ShooterMover/Runtime/Application/Rewards/Application/RewardApplicationAuthorityAdaptersV1.cs` → `RewardApplicationAdapters`
- `Assets/ShooterMover/Runtime/Application/Rewards/Application/RewardApplicationAuthorityAdaptersV1.cs.meta` → `RewardApplicationAdapters`
- `Assets/ShooterMover/Runtime/Application/Rewards/Application/RewardApplicationServiceV1.Persistence.cs` → `RewardApplicationV1Persistence`
- `Assets/ShooterMover/Runtime/Application/Rewards/Application/RewardApplicationServiceV1.Persistence.cs.meta` → `RewardApplicationV1Persistence`
- `Assets/ShooterMover/Runtime/Application/Rewards/Application/RewardApplicationServiceV1.cs` → `RewardApplication`
- `Assets/ShooterMover/Runtime/Application/Rewards/Application/RewardApplicationServiceV1.cs.meta` → `RewardApplication`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardAtomicPlanV2.cs` → `CollectedRunRewardAtomicPlan`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardAtomicPlanV2.cs.meta` → `CollectedRunRewardAtomicPlan`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardPreparedTransfersV1.cs` → `CollectedRunRewardPreparedTransfers`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardPreparedTransfersV1.cs.meta` → `CollectedRunRewardPreparedTransfers`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferContractsV1.cs` → `CollectedRunRewardTransferContracts`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferContractsV1.cs.meta` → `CollectedRunRewardTransferContracts`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferCoordinatorV1.cs` → `CollectedRunRewardTransfer`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferCoordinatorV1.cs.meta` → `CollectedRunRewardTransfer`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferPortsV1.cs` → `CollectedRunRewardTransferPorts`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferPortsV1.cs.meta` → `CollectedRunRewardTransferPorts`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferReceiptsV1.cs` → `CollectedRunRewardTransferReceipts`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferReceiptsV1.cs.meta` → `CollectedRunRewardTransferReceipts`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferResultsV1.cs` → `CollectedRunRewardTransferResults`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferResultsV1.cs.meta` → `CollectedRunRewardTransferResults`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/ProductionCollectedRunRewardAtomicAuthorityV2.cs` → `CollectedRunRewardAtomic`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/ProductionCollectedRunRewardAtomicAuthorityV2.cs.meta` → `CollectedRunRewardAtomic`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/ProductionCollectedRunRewardPersistenceV2.cs` → `CollectedRunRewardPersistence`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/ProductionCollectedRunRewardPersistenceV2.cs.meta` → `CollectedRunRewardPersistence`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/ProductionCollectedRunRewardPreparationV2.cs` → `CollectedRunRewardPreparation`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/ProductionCollectedRunRewardPreparationV2.cs.meta` → `CollectedRunRewardPreparation`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/ProductionCollectedRunRewardRegistryCompatibility.cs` → `CollectedRunRewardRegistryCompatibility`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/ProductionCollectedRunRewardRegistryCompatibility.cs.meta` → `CollectedRunRewardRegistryCompatibility`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/ProductionCollectedRunRewardRuntimeRegistryV2.cs` → `CollectedRunRewardRegistry`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/ProductionCollectedRunRewardRuntimeRegistryV2.cs.meta` → `CollectedRunRewardRegistry`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/IParticipantDropPacingStateStoreV1.cs` → `IParticipantDropPacingStateStore`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/IParticipantDropPacingStateStoreV1.cs.meta` → `IParticipantDropPacingStateStore`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/IPersonalRewardDeliveryOutboxV1.cs` → `IPersonalRewardDeliveryOutbox`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/IPersonalRewardDeliveryOutboxV1.cs.meta` → `IPersonalRewardDeliveryOutbox`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/ParticipantDropPacingAuthorityV1.cs` → `ParticipantDropPacing`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/ParticipantDropPacingAuthorityV1.cs.meta` → `ParticipantDropPacing`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/PersonalRewardDeliveryEnvelopeV1.cs` → `PersonalRewardDeliveryEnvelope`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/PersonalRewardDeliveryEnvelopeV1.cs.meta` → `PersonalRewardDeliveryEnvelope`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/PersonalRewardGenerationRandomV1.cs` → `PersonalRewardGenerationRandom`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/PersonalRewardGenerationRandomV1.cs.meta` → `PersonalRewardGenerationRandom`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/PersonalRewardGenerationServiceV1.cs` → `PersonalRewardGeneration`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/PersonalRewardGenerationServiceV1.cs.meta` → `PersonalRewardGeneration`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/PersonalRewardGroupGenerationV1.cs` → `PersonalRewardGroupGeneration`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/PersonalRewardGroupGenerationV1.cs.meta` → `PersonalRewardGroupGeneration`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/PersonalStrongboxRewardGenerationV1.cs` → `PersonalStrongboxRewardGeneration`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/PersonalStrongboxRewardGenerationV1.cs.meta` → `PersonalStrongboxRewardGeneration`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/ProductionRewardOverrideCatalogV1.cs` → `RewardOverrideCatalog`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/ProductionRewardOverrideCatalogV1.cs.meta` → `RewardOverrideCatalog`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/ProductionRewardSourceCatalogV1.cs` → `RewardSourceCatalog`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/ProductionRewardSourceCatalogV1.cs.meta` → `RewardSourceCatalog`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/ProductionRunDropPacingCatalogV1.cs` → `RunDropPacingCatalog`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/ProductionRunDropPacingCatalogV1.cs.meta` → `RunDropPacingCatalog`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/ProductionStrongboxTierSelectionCatalogV1.cs` → `StrongboxTierSelectionCatalog`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/ProductionStrongboxTierSelectionCatalogV1.cs.meta` → `StrongboxTierSelectionCatalog`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/RewardContextOverrideResolutionV1.cs` → `RewardContextOverrideResolution`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/RewardContextOverrideResolutionV1.cs.meta` → `RewardContextOverrideResolution`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/RewardGrantHandlerRegistryV1.cs` → `RewardGrantHandlerRegistry`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/RewardGrantHandlerRegistryV1.cs.meta` → `RewardGrantHandlerRegistry`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/RewardProfileResolverV1.cs` → `RewardProfileResolver`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/RewardProfileResolverV1.cs.meta` → `RewardProfileResolver`
- `Assets/ShooterMover/Runtime/Application/Rewards/GameplayDrops/GameplayDropOperationV1.cs` → `GameplayDropOperation`
- `Assets/ShooterMover/Runtime/Application/Rewards/GameplayDrops/GameplayDropOperationV1.cs.meta` → `GameplayDropOperation`
- `Assets/ShooterMover/Runtime/Application/Rewards/Generation/RewardGenerationModelsV1.cs` → `RewardGenerationModels`
- `Assets/ShooterMover/Runtime/Application/Rewards/Generation/RewardGenerationModelsV1.cs.meta` → `RewardGenerationModels`
- `Assets/ShooterMover/Runtime/Application/Rewards/Generation/RewardGenerationServiceV1.Core.cs` → `RewardGenerationV1Core`
- `Assets/ShooterMover/Runtime/Application/Rewards/Generation/RewardGenerationServiceV1.Core.cs.meta` → `RewardGenerationV1Core`
- `Assets/ShooterMover/Runtime/Application/Rewards/Generation/RewardGenerationServiceV1.Equipment.Helpers.cs` → `RewardGenerationV1EquipmentHelpers`
- `Assets/ShooterMover/Runtime/Application/Rewards/Generation/RewardGenerationServiceV1.Equipment.Helpers.cs.meta` → `RewardGenerationV1EquipmentHelpers`
- `Assets/ShooterMover/Runtime/Application/Rewards/Generation/RewardGenerationServiceV1.Equipment.cs` → `RewardGenerationV1Equipment`
- `Assets/ShooterMover/Runtime/Application/Rewards/Generation/RewardGenerationServiceV1.Equipment.cs.meta` → `RewardGenerationV1Equipment`
- `Assets/ShooterMover/Runtime/Application/Rewards/Generation/RewardGenerationServiceV1.Rewards.cs` → `RewardGenerationV1Rewards`
- `Assets/ShooterMover/Runtime/Application/Rewards/Generation/RewardGenerationServiceV1.Rewards.cs.meta` → `RewardGenerationV1Rewards`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/GeneratedAugmentSignaturePlayerHoldingsRewardChildAuthorityV1.cs` → `GeneratedAugmentSignaturePlayerHoldingsRewardChild`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/GeneratedAugmentSignaturePlayerHoldingsRewardChildAuthorityV1.cs.meta` → `GeneratedAugmentSignaturePlayerHoldingsRewardChild`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/GeneratedEquipmentAugmentSignatureAuthorityV1.cs` → `GeneratedEquipmentAugmentSignature`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/GeneratedEquipmentAugmentSignatureAuthorityV1.cs.meta` → `GeneratedEquipmentAugmentSignature`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/GeneratedEquipmentAugmentSignatureSnapshotV1.cs` → `GeneratedEquipmentAugmentSignatureSnapshot`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/GeneratedEquipmentAugmentSignatureSnapshotV1.cs.meta` → `GeneratedEquipmentAugmentSignatureSnapshot`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxDurableOpeningCoordinatorV1.cs` → `StrongboxDurableOpening`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxDurableOpeningCoordinatorV1.cs.meta` → `StrongboxDurableOpening`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxDurableOpeningExecutorV1.cs` → `StrongboxDurableOpeningExecutor`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxDurableOpeningExecutorV1.cs.meta` → `StrongboxDurableOpeningExecutor`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxDurableOpeningRecoveryV1.cs` → `StrongboxDurableOpeningRecovery`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxDurableOpeningRecoveryV1.cs.meta` → `StrongboxDurableOpeningRecovery`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxDurableOpeningStateV1.cs` → `StrongboxDurableOpeningState`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxDurableOpeningStateV1.cs.meta` → `StrongboxDurableOpeningState`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationAuthorityPortV1.cs` → `StrongboxMissionResultApplicationPort`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationAuthorityPortV1.cs.meta` → `StrongboxMissionResultApplicationPort`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationCompensationV1.cs` → `StrongboxMissionResultApplicationCompensation`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationCompensationV1.cs.meta` → `StrongboxMissionResultApplicationCompensation`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationContractsV1.cs` → `StrongboxMissionResultApplicationContracts`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationContractsV1.cs.meta` → `StrongboxMissionResultApplicationContracts`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationCoordinatorV1.cs` → `StrongboxMissionResultApplication`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationCoordinatorV1.cs.meta` → `StrongboxMissionResultApplication`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationExecutionV1.cs` → `StrongboxMissionResultApplicationExecution`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationExecutionV1.cs.meta` → `StrongboxMissionResultApplicationExecution`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationPlanV1.cs` → `StrongboxMissionResultApplicationPlan`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationPlanV1.cs.meta` → `StrongboxMissionResultApplicationPlan`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationValidationV1.cs` → `StrongboxMissionResultApplicationValidation`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationValidationV1.cs.meta` → `StrongboxMissionResultApplicationValidation`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxOpeningRecoveryPortV1.cs` → `StrongboxOpeningRecoveryPort`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxOpeningRecoveryPortV1.cs.meta` → `StrongboxOpeningRecoveryPort`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/ProductionStrongboxCatalogV1.cs` → `StrongboxCatalog`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/ProductionStrongboxCatalogV1.cs.meta` → `StrongboxCatalog`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/ProductionStrongboxHybridLootCatalogV1.cs` → `StrongboxHybridLootCatalog`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/ProductionStrongboxHybridLootCatalogV1.cs.meta` → `StrongboxHybridLootCatalog`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxEquipmentGenerationResolverV1.cs` → `StrongboxEquipmentGenerationResolver`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxEquipmentGenerationResolverV1.cs.meta` → `StrongboxEquipmentGenerationResolver`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxHybridEquipmentGenerationResolverV1.cs` → `StrongboxHybridEquipmentGenerationResolver`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxHybridEquipmentGenerationResolverV1.cs.meta` → `StrongboxHybridEquipmentGenerationResolver`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxOpeningModelsV1.cs` → `StrongboxOpeningModels`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxOpeningModelsV1.cs.meta` → `StrongboxOpeningModels`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxOpeningServiceV1.cs` → `StrongboxOpening`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxOpeningServiceV1.cs.meta` → `StrongboxOpening`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/TransactionalStrongboxGrantPayloadResolverV1.cs` → `TransactionalStrongboxGrantPayloadResolver`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/TransactionalStrongboxGrantPayloadResolverV1.cs.meta` → `TransactionalStrongboxGrantPayloadResolver`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/ExistingRunAuthorityPortsV1.cs` → `ExistingRunPorts`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/ExistingRunAuthorityPortsV1.cs.meta` → `ExistingRunPorts`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/ProductionRunSessionCompositionV1.cs` → `RunSession`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/ProductionRunSessionCompositionV1.cs.meta` → `RunSession`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunConditionCheckpointV1.cs` → `RunConditionCheckpoint`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunConditionCheckpointV1.cs.meta` → `RunConditionCheckpoint`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunMissionResultPersistencePortsV1.cs` → `RunMissionResultPersistencePorts`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunMissionResultPersistencePortsV1.cs.meta` → `RunMissionResultPersistencePorts`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunRewardEnvironmentSnapshotV1.cs` → `RunRewardEnvironmentSnapshot`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunRewardEnvironmentSnapshotV1.cs.meta` → `RunRewardEnvironmentSnapshot`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunRewardParticipantStateV1.cs` → `RunRewardParticipantState`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunRewardParticipantStateV1.cs.meta` → `RunRewardParticipantState`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunRewardRuntimeSnapshotV1.cs` → `RunRewardSnapshot`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunRewardRuntimeSnapshotV1.cs.meta` → `RunRewardSnapshot`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionAggregate.ConditionCheckpointV1.cs` → `RunSessionAggregateConditionCheckpoint`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionAggregate.ConditionCheckpointV1.cs.meta` → `RunSessionAggregateConditionCheckpoint`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionAggregate.ConditionRuntimeV1.cs` → `RunSessionAggregateCondition`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionAggregate.ConditionRuntimeV1.cs.meta` → `RunSessionAggregateCondition`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionAuthorityV1.cs` → `RunSession`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionAuthorityV1.cs.meta` → `RunSession`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionCollectedRewardAuthorityV1.cs` → `RunSessionCollectedReward`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionCollectedRewardAuthorityV1.cs.meta` → `RunSessionCollectedReward`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionCollectedRewardContractsV1.cs` → `RunSessionCollectedRewardContracts`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionCollectedRewardContractsV1.cs.meta` → `RunSessionCollectedRewardContracts`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionCommandsV1.cs` → `RunSessionCommands`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionCommandsV1.cs.meta` → `RunSessionCommands`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionConditionContractsV1.cs` → `RunSessionConditionContracts`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionConditionContractsV1.cs.meta` → `RunSessionConditionContracts`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionDurableEndV1.cs` → `RunSessionDurableEnd`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionDurableEndV1.cs.meta` → `RunSessionDurableEnd`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionParticipantDropPacingStateStoreV1.cs` → `RunSessionParticipantDropPacingStateStore`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionParticipantDropPacingStateStoreV1.cs.meta` → `RunSessionParticipantDropPacingStateStore`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionPersonalRewardDeliveryOutboxV1.cs` → `RunSessionPersonalRewardDeliveryOutbox`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionPersonalRewardDeliveryOutboxV1.cs.meta` → `RunSessionPersonalRewardDeliveryOutbox`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionPortsV1.cs` → `RunSessionPorts`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionPortsV1.cs.meta` → `RunSessionPorts`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionRewardRuntimeStateV1.cs` → `RunSessionRewardState`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionRewardRuntimeStateV1.cs.meta` → `RunSessionRewardState`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionSnapshotsV1.cs` → `RunSessionSnapshots`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionSnapshotsV1.cs.meta` → `RunSessionSnapshots`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionTimeV1.cs` → `RunSessionTime`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionTimeV1.cs.meta` → `RunSessionTime`
- `Assets/ShooterMover/Runtime/Application/Shops/Presentation/ShopScreenPresentationV1.cs` → `ShopScreenPresentation`
- `Assets/ShooterMover/Runtime/Application/Shops/Presentation/ShopScreenPresentationV1.cs.meta` → `ShopScreenPresentation`
- `Assets/ShooterMover/Runtime/Application/Shops/Presentation/ShopScreenSessionV1.Projection.cs` → `ShopScreenSessionV1`
- `Assets/ShooterMover/Runtime/Application/Shops/Presentation/ShopScreenSessionV1.Projection.cs.meta` → `ShopScreenSessionV1`
- `Assets/ShooterMover/Runtime/Application/Shops/Presentation/ShopScreenSessionV1.cs` → `ShopScreenSession`
- `Assets/ShooterMover/Runtime/Application/Shops/Presentation/ShopScreenSessionV1.cs.meta` → `ShopScreenSession`
- `Assets/ShooterMover/Runtime/Application/Shops/ShopRuntimeServiceV1.RefreshPersistence.cs` → `ShopV1RefreshPersistence`
- `Assets/ShooterMover/Runtime/Application/Shops/ShopRuntimeServiceV1.RefreshPersistence.cs.meta` → `ShopV1RefreshPersistence`
- `Assets/ShooterMover/Runtime/Application/Shops/ShopRuntimeServiceV1.State.cs` → `ShopV1State`
- `Assets/ShooterMover/Runtime/Application/Shops/ShopRuntimeServiceV1.State.cs.meta` → `ShopV1State`
- `Assets/ShooterMover/Runtime/Application/Shops/ShopRuntimeServiceV1.Transactions.cs` → `ShopV1Transactions`
- `Assets/ShooterMover/Runtime/Application/Shops/ShopRuntimeServiceV1.Transactions.cs.meta` → `ShopV1Transactions`
- `Assets/ShooterMover/Runtime/Application/Shops/ShopRuntimeServiceV1.cs` → `Shop`
- `Assets/ShooterMover/Runtime/Application/Shops/ShopRuntimeServiceV1.cs.meta` → `Shop`
- `Assets/ShooterMover/Runtime/Application/Skills/Presentation/RankedSkillsScreenSessionV2.cs` → `RankedSkillsScreenSession`
- `Assets/ShooterMover/Runtime/Application/Skills/Presentation/RankedSkillsScreenSessionV2.cs.meta` → `RankedSkillsScreenSession`
- `Assets/ShooterMover/Runtime/Application/Skills/Presentation/SkillsScreenPresentationV1.cs` → `SkillsScreenPresentation`
- `Assets/ShooterMover/Runtime/Application/Skills/Presentation/SkillsScreenPresentationV1.cs.meta` → `SkillsScreenPresentation`
- `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/ProductionWeaponCatalogueV1.Content.cs` → `WeaponCatalogueV1Content`
- `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/ProductionWeaponCatalogueV1.Content.cs.meta` → `WeaponCatalogueV1Content`
- `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/ProductionWeaponCatalogueV1.Contracts.cs` → `WeaponCatalogueV1Contracts`
- `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/ProductionWeaponCatalogueV1.Contracts.cs.meta` → `WeaponCatalogueV1Contracts`
- `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/ProductionWeaponCatalogueV1.EquipmentProjection.cs` → `WeaponCatalogueV1Equipment`
- `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/ProductionWeaponCatalogueV1.EquipmentProjection.cs.meta` → `WeaponCatalogueV1Equipment`
- `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/ProductionWeaponCatalogueV1.FlatProjection.cs` → `WeaponCatalogueV1Flat`
- `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/ProductionWeaponCatalogueV1.FlatProjection.cs.meta` → `WeaponCatalogueV1Flat`
- `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/WeaponCatalogCanonicalJson.cs` → `WeaponCatalogJson`
- `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/WeaponCatalogCanonicalJson.cs.meta` → `WeaponCatalogJson`
- `Assets/ShooterMover/Runtime/Application/Weapons/Execution/AcceptedEmissionRuntimeAdapter.cs` → `AcceptedEmission`
- `Assets/ShooterMover/Runtime/Application/Weapons/Execution/AcceptedEmissionRuntimeAdapter.cs.meta` → `AcceptedEmission`
- `Assets/ShooterMover/Runtime/Application/Weapons/Execution/AcceptedEmissionRuntimeAdapterContracts.cs` → `AcceptedEmissionContracts`
- `Assets/ShooterMover/Runtime/Application/Weapons/Execution/AcceptedEmissionRuntimeAdapterContracts.cs.meta` → `AcceptedEmissionContracts`
- `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponProfileResolver.Runtime.cs` → `WeaponProfileResolver`
- `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponProfileResolver.Runtime.cs.meta` → `WeaponProfileResolver`
- `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponRicochetRuntimeState.cs` → `WeaponRicochetState`
- `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponRicochetRuntimeState.cs.meta` → `WeaponRicochetState`
- `Assets/ShooterMover/Runtime/Application/Weapons/Presentation/WeaponArtReferenceResolverV1.cs` → `WeaponArtReferenceResolver`
- `Assets/ShooterMover/Runtime/Application/Weapons/Presentation/WeaponArtReferenceResolverV1.cs.meta` → `WeaponArtReferenceResolver`
- `Assets/ShooterMover/Runtime/Bootstrap/BootstrapCompositionRoot.cs` → `BootstrapRoot`
- `Assets/ShooterMover/Runtime/Bootstrap/BootstrapCompositionRoot.cs.meta` → `BootstrapRoot`
- `Assets/ShooterMover/Runtime/Bootstrap/Unity/BootstrapSceneAdapter.cs` → `BootstrapScene`
- `Assets/ShooterMover/Runtime/Bootstrap/Unity/BootstrapSceneAdapter.cs.meta` → `BootstrapScene`
- `Assets/ShooterMover/Runtime/CombatHitPolicy/CombatHitPolicyAdaptersV1.cs` → `CombatHitPolicyAdapters`
- `Assets/ShooterMover/Runtime/CombatHitPolicy/CombatHitPolicyAdaptersV1.cs.meta` → `CombatHitPolicyAdapters`
- `Assets/ShooterMover/Runtime/CombatHitPolicy/CombatHitPolicyV1.cs` → `CombatHitPolicy`
- `Assets/ShooterMover/Runtime/CombatHitPolicy/CombatHitPolicyV1.cs.meta` → `CombatHitPolicy`
- `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionRuntimeAuthorityV1.cs` → `Condition`
- `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionRuntimeAuthorityV1.cs.meta` → `Condition`
- `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionRuntimeContractsV1.cs` → `ConditionContracts`
- `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionRuntimeContractsV1.cs.meta` → `ConditionContracts`
- `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionSourceFactFingerprintV1.cs` → `ConditionSourceFactFingerprint`
- `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionSourceFactFingerprintV1.cs.meta` → `ConditionSourceFactFingerprint`
- `Assets/ShooterMover/Runtime/ConditionRuntime/EnemyDeathConditionFactAdapterV1.cs` → `EnemyDeathConditionFact`
- `Assets/ShooterMover/Runtime/ConditionRuntime/EnemyDeathConditionFactAdapterV1.cs.meta` → `EnemyDeathConditionFact`
- `Assets/ShooterMover/Runtime/ConditionRuntime/ShooterMover.ConditionRuntime.asmdef.meta` → `ShooterMoverConditionasmdef`
- `Assets/ShooterMover/Runtime/ConditionRuntime.meta` → `Condition`
- `Assets/ShooterMover/Runtime/Contracts/Economy/EconomyTransactionContractsV1.cs` → `EconomyTransactionContracts`
- `Assets/ShooterMover/Runtime/Contracts/Economy/EconomyTransactionContractsV1.cs.meta` → `EconomyTransactionContracts`
- `Assets/ShooterMover/Runtime/Contracts/Flow/Session/PlayerRouteProfilePayloadV1.cs` → `PlayerRouteProfilePayload`
- `Assets/ShooterMover/Runtime/Contracts/Flow/Session/PlayerRouteProfilePayloadV1.cs.meta` → `PlayerRouteProfilePayload`
- `Assets/ShooterMover/Runtime/Contracts/Holdings/PlayerHoldingsAuthorityV1.cs` → `PlayerHoldings`
- `Assets/ShooterMover/Runtime/Contracts/Holdings/PlayerHoldingsAuthorityV1.cs.meta` → `PlayerHoldings`
- `Assets/ShooterMover/Runtime/Contracts/Holdings/PlayerHoldingsContractsV1.cs` → `PlayerHoldingsContracts`
- `Assets/ShooterMover/Runtime/Contracts/Holdings/PlayerHoldingsContractsV1.cs.meta` → `PlayerHoldingsContracts`
- `Assets/ShooterMover/Runtime/Contracts/Holdings/PlayerHoldingsMutationResultV1.cs` → `PlayerHoldingsMutationResult`
- `Assets/ShooterMover/Runtime/Contracts/Holdings/PlayerHoldingsMutationResultV1.cs.meta` → `PlayerHoldingsMutationResult`
- `Assets/ShooterMover/Runtime/Contracts/Holdings/PlayerHoldingsSnapshotV1.cs` → `PlayerHoldingsSnapshot`
- `Assets/ShooterMover/Runtime/Contracts/Holdings/PlayerHoldingsSnapshotV1.cs.meta` → `PlayerHoldingsSnapshot`
- `Assets/ShooterMover/Runtime/Contracts/Holdings/PlayerHoldingsTransactionRecordV1.cs` → `PlayerHoldingsTransactionRecord`
- `Assets/ShooterMover/Runtime/Contracts/Holdings/PlayerHoldingsTransactionRecordV1.cs.meta` → `PlayerHoldingsTransactionRecord`
- `Assets/ShooterMover/Runtime/Contracts/Missions/Results/MissionRunCommandsV1.cs` → `MissionRunCommands`
- `Assets/ShooterMover/Runtime/Contracts/Missions/Results/MissionRunCommandsV1.cs.meta` → `MissionRunCommands`
- `Assets/ShooterMover/Runtime/Contracts/Missions/Results/MissionRunPayloadsV1.cs` → `MissionRunPayloads`
- `Assets/ShooterMover/Runtime/Contracts/Missions/Results/MissionRunPayloadsV1.cs.meta` → `MissionRunPayloads`
- `Assets/ShooterMover/Runtime/Contracts/Missions/Results/MissionRunResultContractsV1.cs` → `MissionRunResultContracts`
- `Assets/ShooterMover/Runtime/Contracts/Missions/Results/MissionRunResultContractsV1.cs.meta` → `MissionRunResultContracts`
- `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/AuthorableRoomDefinitionV1.cs` → `AuthorableRoomDefinition`
- `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/AuthorableRoomDefinitionV1.cs.meta` → `AuthorableRoomDefinition`
- `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/AuthorableRoomGraphDefinitionV1.cs` → `AuthorableRoomGraphDefinition`
- `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/AuthorableRoomGraphDefinitionV1.cs.meta` → `AuthorableRoomGraphDefinition`
- `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAccessContractsV1.cs` → `RoomAccessContracts`
- `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAccessContractsV1.cs.meta` → `RoomAccessContracts`
- `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAccessReferenceCatalogV1.cs` → `RoomAccessReferenceCatalog`
- `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAccessReferenceCatalogV1.cs.meta` → `RoomAccessReferenceCatalog`
- `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAuthoringPrimitivesV1.cs` → `RoomAuthoringPrimitives`
- `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAuthoringPrimitivesV1.cs.meta` → `RoomAuthoringPrimitives`
- `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomGraphContractsV1.cs` → `RoomGraphContracts`
- `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomGraphContractsV1.cs.meta` → `RoomGraphContracts`
- `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomOccupancyContractsV1.cs` → `RoomOccupancyContracts`
- `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomOccupancyContractsV1.cs.meta` → `RoomOccupancyContracts`
- `Assets/ShooterMover/Runtime/Contracts/Progression/Experience/PlayerExperienceContractsV1.cs` → `PlayerExperienceContracts`
- `Assets/ShooterMover/Runtime/Contracts/Progression/Experience/PlayerExperienceContractsV1.cs.meta` → `PlayerExperienceContracts`
- `Assets/ShooterMover/Runtime/Contracts/Rewards/Application/RewardApplicationCommandsV1.cs` → `RewardApplicationCommands`
- `Assets/ShooterMover/Runtime/Contracts/Rewards/Application/RewardApplicationCommandsV1.cs.meta` → `RewardApplicationCommands`
- `Assets/ShooterMover/Runtime/Contracts/Rewards/Application/RewardApplicationResultsV1.cs` → `RewardApplicationResults`
- `Assets/ShooterMover/Runtime/Contracts/Rewards/Application/RewardApplicationResultsV1.cs.meta` → `RewardApplicationResults`
- `Assets/ShooterMover/Runtime/Contracts/Rewards/Drops/PersonalRewardGenerationResultV1.cs` → `PersonalRewardGenerationResult`
- `Assets/ShooterMover/Runtime/Contracts/Rewards/Drops/PersonalRewardGenerationResultV1.cs.meta` → `PersonalRewardGenerationResult`
- `Assets/ShooterMover/Runtime/Contracts/Rewards/RewardOperationContractsV1.cs` → `RewardOperationContracts`
- `Assets/ShooterMover/Runtime/Contracts/Rewards/RewardOperationContractsV1.cs.meta` → `RewardOperationContracts`
- `Assets/ShooterMover/Runtime/Contracts/Rewards/StrongboxOpeningContractsV1.cs` → `StrongboxOpeningContracts`
- `Assets/ShooterMover/Runtime/Contracts/Rewards/StrongboxOpeningContractsV1.cs.meta` → `StrongboxOpeningContracts`
- `Assets/ShooterMover/Runtime/Contracts/Rooms/RoomProjectionContracts.cs` → `RoomContracts`
- `Assets/ShooterMover/Runtime/Contracts/Rooms/RoomProjectionContracts.cs.meta` → `RoomContracts`
- `Assets/ShooterMover/Runtime/Contracts/Rooms/RoomProjectionLifecycle.cs` → `RoomLifecycle`
- `Assets/ShooterMover/Runtime/Contracts/Rooms/RoomProjectionLifecycle.cs.meta` → `RoomLifecycle`
- `Assets/ShooterMover/Runtime/CriticalHits/CriticalHitResolutionV1.cs` → `CriticalHitResolution`
- `Assets/ShooterMover/Runtime/CriticalHits/CriticalHitResolutionV1.cs.meta` → `CriticalHitResolution`
- `Assets/ShooterMover/Runtime/Domain/Characters/DerivedCharacterStatsV1.cs` → `DerivedCharacterStats`
- `Assets/ShooterMover/Runtime/Domain/Characters/DerivedCharacterStatsV1.cs.meta` → `DerivedCharacterStats`
- `Assets/ShooterMover/Runtime/Domain/Characters/Selection/CharacterSelectionCatalogV1.cs` → `CharacterSelectionCatalog`
- `Assets/ShooterMover/Runtime/Domain/Characters/Selection/CharacterSelectionCatalogV1.cs.meta` → `CharacterSelectionCatalog`
- `Assets/ShooterMover/Runtime/Domain/Characters/Selection/CharacterSelectionDefinitionsV1.cs` → `CharacterSelectionDefinitions`
- `Assets/ShooterMover/Runtime/Domain/Characters/Selection/CharacterSelectionDefinitionsV1.cs.meta` → `CharacterSelectionDefinitions`
- `Assets/ShooterMover/Runtime/Domain/Combat/WeaponRuntimeProfile.cs` → `WeaponProfile`
- `Assets/ShooterMover/Runtime/Domain/Combat/WeaponRuntimeProfile.cs.meta` → `WeaponProfile`
- `Assets/ShooterMover/Runtime/Domain/Combat/WeaponRuntimeProfileValidator.cs` → `WeaponProfileValidator`
- `Assets/ShooterMover/Runtime/Domain/Combat/WeaponRuntimeProfileValidator.cs.meta` → `WeaponProfileValidator`
- `Assets/ShooterMover/Runtime/Domain/Crafting/CraftingRecipeV1.cs` → `CraftingRecipe`
- `Assets/ShooterMover/Runtime/Domain/Crafting/CraftingRecipeV1.cs.meta` → `CraftingRecipe`
- `Assets/ShooterMover/Runtime/Domain/Economy/Scrap/ScrapWalletModelV1.cs` → `ScrapWalletModel`
- `Assets/ShooterMover/Runtime/Domain/Economy/Scrap/ScrapWalletModelV1.cs.meta` → `ScrapWalletModel`
- `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyAttackDescriptorCompatibilityV1.cs` → `EnemyAttackDescriptorCompatibility`
- `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyAttackDescriptorCompatibilityV1.cs.meta` → `EnemyAttackDescriptorCompatibility`
- `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogModelV1.cs` → `EnemyCatalogModel`
- `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogModelV1.cs.meta` → `EnemyCatalogModel`
- `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogV1.cs` → `EnemyCatalog`
- `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogV1.cs.meta` → `EnemyCatalog`
- `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogValidationTypesV1.cs` → `EnemyCatalogValidationTypes`
- `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogValidationTypesV1.cs.meta` → `EnemyCatalogValidationTypes`
- `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogValidatorAttacksV1.cs` → `EnemyCatalogValidatorAttacks`
- `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogValidatorAttacksV1.cs.meta` → `EnemyCatalogValidatorAttacks`
- `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogValidatorV1.cs` → `EnemyCatalogValidator`
- `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogValidatorV1.cs.meta` → `EnemyCatalogValidator`
- `Assets/ShooterMover/Runtime/Domain/Equipment/GeneratedEquipmentAugmentSignatureV1.cs` → `GeneratedEquipmentAugmentSignature`
- `Assets/ShooterMover/Runtime/Domain/Equipment/GeneratedEquipmentAugmentSignatureV1.cs.meta` → `GeneratedEquipmentAugmentSignature`
- `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgradeCanonicalV1.cs` → `AugmentUpgrade`
- `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgradeCanonicalV1.cs.meta` → `AugmentUpgrade`
- `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgradeConfirmationV1.cs` → `AugmentUpgradeConfirmation`
- `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgradeConfirmationV1.cs.meta` → `AugmentUpgradeConfirmation`
- `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgradeFactV1.cs` → `AugmentUpgradeFact`
- `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgradeFactV1.cs.meta` → `AugmentUpgradeFact`
- `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgradeModelV1.cs` → `AugmentUpgradeModel`
- `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgradeModelV1.cs.meta` → `AugmentUpgradeModel`
- `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgradeQuoteV1.cs` → `AugmentUpgradeQuote`
- `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgradeQuoteV1.cs.meta` → `AugmentUpgradeQuote`
- `Assets/ShooterMover/Runtime/Domain/Holdings/PlayerHoldingsModelV1.cs` → `PlayerHoldingsModel`
- `Assets/ShooterMover/Runtime/Domain/Holdings/PlayerHoldingsModelV1.cs.meta` → `PlayerHoldingsModel`
- `Assets/ShooterMover/Runtime/Domain/Missions/Rooms/RoomGraphDefinitionV1.cs` → `RoomGraphDefinition`
- `Assets/ShooterMover/Runtime/Domain/Missions/Rooms/RoomGraphDefinitionV1.cs.meta` → `RoomGraphDefinition`
- `Assets/ShooterMover/Runtime/Domain/Modifiers/Events/SpecialEventModifierContextV1.cs` → `SpecialEventModifierContext`
- `Assets/ShooterMover/Runtime/Domain/Modifiers/Events/SpecialEventModifierContextV1.cs.meta` → `SpecialEventModifierContext`
- `Assets/ShooterMover/Runtime/Domain/Modifiers/RuntimeModifierFoundationV1.cs` → `ModifierFoundation`
- `Assets/ShooterMover/Runtime/Domain/Modifiers/RuntimeModifierFoundationV1.cs.meta` → `ModifierFoundation`
- `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/ActiveStatusEffectSnapshotV1.cs` → `ActiveStatusEffectSnapshot`
- `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/ActiveStatusEffectSnapshotV1.cs.meta` → `ActiveStatusEffectSnapshot`
- `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/ActiveStatusEffectStackSnapshotV1.cs` → `ActiveStatusEffectStackSnapshot`
- `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/ActiveStatusEffectStackSnapshotV1.cs.meta` → `ActiveStatusEffectStackSnapshot`
- `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectCommandResultV1.cs` → `StatusEffectCommandResult`
- `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectCommandResultV1.cs.meta` → `StatusEffectCommandResult`
- `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectDefinitionsV1.cs` → `StatusEffectDefinitions`
- `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectDefinitionsV1.cs.meta` → `StatusEffectDefinitions`
- `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectFingerprintV1.cs` → `StatusEffectFingerprint`
- `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectFingerprintV1.cs.meta` → `StatusEffectFingerprint`
- `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectReplaySnapshotsV1.cs` → `StatusEffectReplaySnapshots`
- `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectReplaySnapshotsV1.cs.meta` → `StatusEffectReplaySnapshots`
- `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectResultEnumsV1.cs` → `StatusEffectResultEnums`
- `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectResultEnumsV1.cs.meta` → `StatusEffectResultEnums`
- `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectStateSnapshotV1.cs` → `StatusEffectStateSnapshot`
- `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectStateSnapshotV1.cs.meta` → `StatusEffectStateSnapshot`
- `Assets/ShooterMover/Runtime/Domain/Persistence/Accounts/PlayerAccountSnapshotV1.cs` → `PlayerAccountSnapshot`
- `Assets/ShooterMover/Runtime/Domain/Persistence/Accounts/PlayerAccountSnapshotV1.cs.meta` → `PlayerAccountSnapshot`
- `Assets/ShooterMover/Runtime/Domain/Progression/Experience/PlayerExperienceModelV1.cs` → `PlayerExperienceModel`
- `Assets/ShooterMover/Runtime/Domain/Progression/Experience/PlayerExperienceModelV1.cs.meta` → `PlayerExperienceModel`
- `Assets/ShooterMover/Runtime/Domain/Progression/Skills/RankedSkillFoundationV2.cs` → `RankedSkillFoundation`
- `Assets/ShooterMover/Runtime/Domain/Progression/Skills/SkillProgressionV1.cs` → `SkillProgression`
- `Assets/ShooterMover/Runtime/Domain/Props/PropCapabilitiesV1.cs` → `PropCapabilities`
- `Assets/ShooterMover/Runtime/Domain/Props/PropCapabilitiesV1.cs.meta` → `PropCapabilities`
- `Assets/ShooterMover/Runtime/Domain/Props/PropCapabilityRegistryV1.cs` → `PropCapabilityRegistry`
- `Assets/ShooterMover/Runtime/Domain/Props/PropCapabilityRegistryV1.cs.meta` → `PropCapabilityRegistry`
- `Assets/ShooterMover/Runtime/Domain/Props/PropCatalogV1.cs` → `PropCatalog`
- `Assets/ShooterMover/Runtime/Domain/Props/PropCatalogV1.cs.meta` → `PropCatalog`
- `Assets/ShooterMover/Runtime/Domain/Props/PropRuntimeContractsV1.cs` → `PropContracts`
- `Assets/ShooterMover/Runtime/Domain/Props/PropRuntimeContractsV1.cs.meta` → `PropContracts`
- `Assets/ShooterMover/Runtime/Domain/Props/PropRuntimeFactoryV1.cs` → `PropFactory`
- `Assets/ShooterMover/Runtime/Domain/Props/PropRuntimeFactoryV1.cs.meta` → `PropFactory`
- `Assets/ShooterMover/Runtime/Domain/Props/PropRuntimeV1.cs` → `Prop`
- `Assets/ShooterMover/Runtime/Domain/Props/PropRuntimeV1.cs.meta` → `Prop`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Application/RewardApplicationModelV1.cs` → `RewardApplicationModel`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Application/RewardApplicationModelV1.cs.meta` → `RewardApplicationModel`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/ParticipantDropPacingStateV1.cs` → `ParticipantDropPacingState`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/ParticipantDropPacingStateV1.cs.meta` → `ParticipantDropPacingState`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/PersonalRewardRollContextV1.cs` → `PersonalRewardRollContext`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/PersonalRewardRollContextV1.cs.meta` → `PersonalRewardRollContext`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RewardOutcomeV1.cs` → `RewardOutcome`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RewardOutcomeV1.cs.meta` → `RewardOutcome`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RewardProfileOverrideV1.cs` → `RewardProfileOverride`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RewardProfileOverrideV1.cs.meta` → `RewardProfileOverride`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RewardProfileResolutionV1.cs` → `RewardProfileResolution`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RewardProfileResolutionV1.cs.meta` → `RewardProfileResolution`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RewardRollGroupV1.cs` → `RewardRollGroup`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RewardRollGroupV1.cs.meta` → `RewardRollGroup`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RewardSourceProfileV1.cs` → `RewardSourceProfile`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RewardSourceProfileV1.cs.meta` → `RewardSourceProfile`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RunDropPacingPolicyV1.cs` → `RunDropPacingPolicy`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RunDropPacingPolicyV1.cs.meta` → `RunDropPacingPolicy`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/StrongboxTierSelectionProfileV1.cs` → `StrongboxTierSelectionProfile`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/StrongboxTierSelectionProfileV1.cs.meta` → `StrongboxTierSelectionProfile`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Generation/RewardGenerationPolicyV1.cs` → `RewardGenerationPolicy`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Generation/RewardGenerationPolicyV1.cs.meta` → `RewardGenerationPolicy`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Model/RewardGrantModelV1.cs` → `RewardGrantModel`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Model/RewardGrantModelV1.cs.meta` → `RewardGrantModel`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Model/RewardProfileV1.cs` → `RewardProfile`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Model/RewardProfileV1.cs.meta` → `RewardProfile`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxAugmentSignatureV1.cs` → `StrongboxAugmentSignature`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxAugmentSignatureV1.cs.meta` → `StrongboxAugmentSignature`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxDefinitionRarityIdsV1.cs` → `StrongboxDefinitionRarityIds`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxDefinitionRarityIdsV1.cs.meta` → `StrongboxDefinitionRarityIds`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxDistanceWeightV1.cs` → `StrongboxDistanceWeight`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxDistanceWeightV1.cs.meta` → `StrongboxDistanceWeight`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxHybridLootPolicyV1.cs` → `StrongboxHybridLootPolicy`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxHybridLootPolicyV1.cs.meta` → `StrongboxHybridLootPolicy`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxHybridLootPolicyValidationV1.cs` → `StrongboxHybridLootPolicyValidation`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxHybridLootPolicyValidationV1.cs.meta` → `StrongboxHybridLootPolicyValidation`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxHybridLootRandomV1.cs` → `StrongboxHybridLootRandom`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxHybridLootRandomV1.cs.meta` → `StrongboxHybridLootRandom`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxInstanceLevelRollV1.cs` → `StrongboxInstanceLevelRoll`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxInstanceLevelRollV1.cs.meta` → `StrongboxInstanceLevelRoll`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxRarityProfileV1.cs` → `StrongboxRarityProfile`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxRarityProfileV1.cs.meta` → `StrongboxRarityProfile`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxTargetLevelRollV1.cs` → `StrongboxTargetLevelRoll`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxTargetLevelRollV1.cs.meta` → `StrongboxTargetLevelRoll`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxWeightedIntOutcomeV1.cs` → `StrongboxWeightedIntOutcome`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxWeightedIntOutcomeV1.cs.meta` → `StrongboxWeightedIntOutcome`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/StrongboxModelsV1.cs` → `StrongboxModels`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/StrongboxModelsV1.cs.meta` → `StrongboxModels`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/StrongboxPowerBudgetV1.cs` → `StrongboxPowerBudget`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/StrongboxPowerBudgetV1.cs.meta` → `StrongboxPowerBudget`
- `Assets/ShooterMover/Runtime/Domain/Shops/ShopDefinitionV1.cs` → `ShopDefinition`
- `Assets/ShooterMover/Runtime/Domain/Shops/ShopDefinitionV1.cs.meta` → `ShopDefinition`
- `Assets/ShooterMover/Runtime/Domain/Shops/ShopRuntimeModelV1.cs` → `ShopModel`
- `Assets/ShooterMover/Runtime/Domain/Shops/ShopRuntimeModelV1.cs.meta` → `ShopModel`
- `Assets/ShooterMover/Runtime/EnemyAttackPatternUnityIntegration/EnemyAttackPatternHitRouterV1.cs` → `EnemyAttackPatternHitRouter`
- `Assets/ShooterMover/Runtime/EnemyAttackPatternUnityIntegration/EnemyAttackPatternHitRouterV1.cs.meta` → `EnemyAttackPatternHitRouter`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/BuiltInEnemyRuntimePoliciesV1.cs` → `BuiltInEnemyPolicies`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/BuiltInEnemyRuntimePoliciesV1.cs.meta` → `BuiltInEnemyPolicies`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternAuthorityV1.cs` → `EnemyAttackPattern`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternAuthorityV1.cs.meta` → `EnemyAttackPattern`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternContractsV1.cs` → `EnemyAttackPatternContracts`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternContractsV1.cs.meta` → `EnemyAttackPatternContracts`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternDispatchContractsV1.cs` → `EnemyAttackPatternDispatchContracts`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternDispatchContractsV1.cs.meta` → `EnemyAttackPatternDispatchContracts`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternDispatchV1.cs` → `EnemyAttackPatternDispatch`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternDispatchV1.cs.meta` → `EnemyAttackPatternDispatch`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternEmissionsV1.cs` → `EnemyAttackPatternEmissions`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternEmissionsV1.cs.meta` → `EnemyAttackPatternEmissions`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternFingerprintV1.cs` → `EnemyAttackPatternFingerprint`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternFingerprintV1.cs.meta` → `EnemyAttackPatternFingerprint`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternResultsV1.cs` → `EnemyAttackPatternResults`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternResultsV1.cs.meta` → `EnemyAttackPatternResults`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternSchedulerV1.cs` → `EnemyAttackPatternScheduler`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternSchedulerV1.cs.meta` → `EnemyAttackPatternScheduler`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyPlacementRuntimeAttackPatternAuthorityV1.cs` → `EnemyPlacementAttackPattern`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyPlacementRuntimeAttackPatternAuthorityV1.cs.meta` → `EnemyPlacementAttackPattern`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyPlacementRuntimeCombatAuthorityV1.cs` → `EnemyPlacementCombat`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyPlacementRuntimeCombatAuthorityV1.cs.meta` → `EnemyPlacementCombat`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyPlacementRuntimeFactoryV1.cs` → `EnemyPlacementFactory`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyPlacementRuntimeFactoryV1.cs.meta` → `EnemyPlacementFactory`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyPlacementRuntimeInstanceV1.cs` → `EnemyPlacementInstance`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyPlacementRuntimeInstanceV1.cs.meta` → `EnemyPlacementInstance`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyPlacementRuntimeStateAuthorityV1.cs` → `EnemyPlacementState`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyPlacementRuntimeStateAuthorityV1.cs.meta` → `EnemyPlacementState`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeAuthorityFingerprintV1.cs` → `EnemyFingerprint`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeAuthorityFingerprintV1.cs.meta` → `EnemyFingerprint`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionAssemblyInfo.cs` → `EnemyAssemblyInfo`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionAssemblyInfo.cs.meta` → `EnemyAssemblyInfo`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` → `EnemyContracts`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs.meta` → `EnemyContracts`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimePolicyRegistryV1.cs` → `EnemyPolicyRegistry`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimePolicyRegistryV1.cs.meta` → `EnemyPolicyRegistry`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/ShooterMover.EnemyRuntimeComposition.asmdef.meta` → `ShooterMoverEnemyasmdef`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition.meta` → `Enemy`
- `Assets/ShooterMover/Runtime/GameplayEntities/Enemies/EnemyRuntimeProjection.cs` → `Enemy`
- `Assets/ShooterMover/Runtime/GameplayEntities/Enemies/EnemyRuntimeProjection.cs.meta` → `Enemy`
- `Assets/ShooterMover/Runtime/GameplayEntities/PlayerActorAuthority.cs` → `PlayerActor`
- `Assets/ShooterMover/Runtime/GameplayEntities/PlayerActorAuthority.cs.meta` → `PlayerActor`
- `Assets/ShooterMover/Runtime/RunConditionIntegration/ProductionConditionBoundRunSessionStartSourceV1.cs` → `ConditionBoundRunSessionStartSource`
- `Assets/ShooterMover/Runtime/RunConditionIntegration/ProductionConditionBoundRunSessionStartSourceV1.cs.meta` → `ConditionBoundRunSessionStartSource`
- `Assets/ShooterMover/Runtime/RunConditionIntegration/RunConditionIntegrationV1.cs` → `RunConditionIntegration`
- `Assets/ShooterMover/Runtime/RunConditionIntegration/RunConditionIntegrationV1.cs.meta` → `RunConditionIntegration`
- `Assets/ShooterMover/Runtime/RunConditionIntegration/StrongboxPersistentRunIntegrationV1.cs` → `StrongboxPersistentRunIntegration`
- `Assets/ShooterMover/Runtime/RunConditionIntegration/StrongboxPersistentRunIntegrationV1.cs.meta` → `StrongboxPersistentRunIntegration`
- `Assets/ShooterMover/Runtime/RunPickups/ExistingRunSessionPickupPortV1.cs` → `ExistingRunSessionPickupPort`
- `Assets/ShooterMover/Runtime/RunPickups/ExistingRunSessionPickupPortV1.cs.meta` → `ExistingRunSessionPickupPort`
- `Assets/ShooterMover/Runtime/RunPickups/RunLocalPickupAuthorityExportsV1.cs` → `RunLocalPickupExports`
- `Assets/ShooterMover/Runtime/RunPickups/RunLocalPickupAuthorityExportsV1.cs.meta` → `RunLocalPickupExports`
- `Assets/ShooterMover/Runtime/RunPickups/RunLocalPickupAuthorityV1.cs` → `RunLocalPickup`
- `Assets/ShooterMover/Runtime/RunPickups/RunLocalPickupAuthorityV1.cs.meta` → `RunLocalPickup`
- `Assets/ShooterMover/Runtime/RunPickups/RunLocalPickupCollectionAuthorityV1.cs` → `RunLocalPickupCollection`
- `Assets/ShooterMover/Runtime/RunPickups/RunLocalPickupCollectionAuthorityV1.cs.meta` → `RunLocalPickupCollection`
- `Assets/ShooterMover/Runtime/RunPickups/RunPickupCollectionContractsV1.cs` → `RunPickupCollectionContracts`
- `Assets/ShooterMover/Runtime/RunPickups/RunPickupCollectionContractsV1.cs.meta` → `RunPickupCollectionContracts`
- `Assets/ShooterMover/Runtime/RunPickups/RunPickupContractsV1.cs` → `RunPickupContracts`
- `Assets/ShooterMover/Runtime/RunPickups/RunPickupContractsV1.cs.meta` → `RunPickupContracts`
- `Assets/ShooterMover/Runtime/RunPickups/RunPickupPortsAndIdentityV1.cs` → `RunPickupPortsAndIdentity`
- `Assets/ShooterMover/Runtime/RunPickups/RunPickupPortsAndIdentityV1.cs.meta` → `RunPickupPortsAndIdentity`
- `Assets/ShooterMover/Runtime/RunPickups/RunPickupSnapshotContractsV1.cs` → `RunPickupSnapshotContracts`
- `Assets/ShooterMover/Runtime/RunPickups/RunPickupSnapshotContractsV1.cs.meta` → `RunPickupSnapshotContracts`
- `Assets/ShooterMover/Runtime/RunPickups/TerminalDropRunPickupAdapterV1.cs` → `TerminalDropRunPickup`
- `Assets/ShooterMover/Runtime/RunPickups/TerminalDropRunPickupAdapterV1.cs.meta` → `TerminalDropRunPickup`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/EnemyTerminalSourceContextAdapterV1.cs` → `EnemyTerminalSourceContext`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/EnemyTerminalSourceContextAdapterV1.cs.meta` → `EnemyTerminalSourceContext`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/PendingTerminalDropAdmissionV1.cs` → `PendingTerminalDropAdmission`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/PendingTerminalDropAdmissionV1.cs.meta` → `PendingTerminalDropAdmission`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/RunSessionTerminalDropContextResolverV1.cs` → `RunSessionTerminalDropContextResolver`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/RunSessionTerminalDropContextResolverV1.cs.meta` → `RunSessionTerminalDropContextResolver`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/RunSessionTerminalRewardEnvironmentResolverV1.cs` → `RunSessionTerminalRewardEnvironmentResolver`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/RunSessionTerminalRewardEnvironmentResolverV1.cs.meta` → `RunSessionTerminalRewardEnvironmentResolver`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/RunSessionTerminalRewardOverrideResolverV1.cs` → `RunSessionTerminalRewardOverrideResolver`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/RunSessionTerminalRewardOverrideResolverV1.cs.meta` → `RunSessionTerminalRewardOverrideResolver`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/RunSessionTerminalRewardParticipantResolverV1.cs` → `RunSessionTerminalRewardParticipantResolver`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/RunSessionTerminalRewardParticipantResolverV1.cs.meta` → `RunSessionTerminalRewardParticipantResolver`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropBindingCompositionV1.cs` → `TerminalDropBinding`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropBindingCompositionV1.cs.meta` → `TerminalDropBinding`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropBindingContractsV1.cs` → `TerminalDropBindingContracts`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropBindingContractsV1.cs.meta` → `TerminalDropBindingContracts`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropFactAdaptersV1.cs` → `TerminalDropFactAdapters`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropFactAdaptersV1.cs.meta` → `TerminalDropFactAdapters`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropGenerationAuthorityV1.cs` → `TerminalDropGeneration`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropGenerationAuthorityV1.cs.meta` → `TerminalDropGeneration`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalPersonalRewardContractsV1.cs` → `TerminalPersonalRewardContracts`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalPersonalRewardContractsV1.cs.meta` → `TerminalPersonalRewardContracts`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalPersonalRewardDefaultsV1.cs` → `TerminalPersonalRewardDefaults`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalPersonalRewardDefaultsV1.cs.meta` → `TerminalPersonalRewardDefaults`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalPersonalRewardGenerationAuthorityV1.cs` → `TerminalPersonalRewardGeneration`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalPersonalRewardGenerationAuthorityV1.cs.meta` → `TerminalPersonalRewardGeneration`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalPersonalRewardTransportAdapterV1.cs` → `TerminalPersonalRewardTransport`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalPersonalRewardTransportAdapterV1.cs.meta` → `TerminalPersonalRewardTransport`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalRewardPlacementFactV1.cs` → `TerminalRewardPlacementFact`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalRewardPlacementFactV1.cs.meta` → `TerminalRewardPlacementFact`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalRunMinimumGenerationAuthorityV1.cs` → `TerminalRunMinimumGeneration`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalRunMinimumGenerationAuthorityV1.cs.meta` → `TerminalRunMinimumGeneration`
- `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelGridPlayableMetadataV2.cs` → `LevelGridPlayableMetadata`
- `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelGridPlayableMetadataV2.cs.meta` → `LevelGridPlayableMetadata`
- `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelGridPlayableValidationV2.cs` → `LevelGridPlayableValidation`
- `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelGridPlayableValidationV2.cs.meta` → `LevelGridPlayableValidation`
- `Assets/ShooterMover/Runtime/UnityAdapters/Combat/CombatHit2DAdapter.cs` → `CombatHit2D`
- `Assets/ShooterMover/Runtime/UnityAdapters/Combat/CombatHit2DAdapter.cs.meta` → `CombatHit2D`
- `Assets/ShooterMover/Runtime/UnityAdapters/Combat/PlayerCombatIntentAdapter.cs` → `PlayerCombatIntent`
- `Assets/ShooterMover/Runtime/UnityAdapters/Combat/PlayerCombatIntentAdapter.cs.meta` → `PlayerCombatIntent`
- `Assets/ShooterMover/Runtime/UnityAdapters/Combat/WeaponMount2DAdapter.cs` → `WeaponMount2D`
- `Assets/ShooterMover/Runtime/UnityAdapters/Combat/WeaponMount2DAdapter.cs.meta` → `WeaponMount2D`
- `Assets/ShooterMover/Runtime/UnityAdapters/CombatPresentation/CombatHealthPresentationV1.cs` → `CombatHealthPresentation`
- `Assets/ShooterMover/Runtime/UnityAdapters/CombatPresentation/CombatHealthPresentationV1.cs.meta` → `CombatHealthPresentation`
- `Assets/ShooterMover/Runtime/UnityAdapters/CombatPresentation/CombatPresentationEnemyActorAuthority2D.cs` → `CombatPresentationEnemyActor2D`
- `Assets/ShooterMover/Runtime/UnityAdapters/CombatPresentation/CombatPresentationEnemyActorAuthority2D.cs.meta` → `CombatPresentationEnemyActor2D`
- `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyActor2DAdapter.cs` → `EnemyActor2D`
- `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyActor2DAdapter.cs.meta` → `EnemyActor2D`
- `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyAttackPatternLiveSchedulerV1.cs` → `EnemyAttackPatternLiveScheduler`
- `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyAttackPatternLiveSchedulerV1.cs.meta` → `EnemyAttackPatternLiveScheduler`
- `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyAttackPortV1.cs` → `EnemyAttackPort`
- `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyAttackPortV1.cs.meta` → `EnemyAttackPort`
- `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyCommittedAttackPatternExecutorV1.cs` → `EnemyCommittedAttackPatternExecutor`
- `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyCommittedAttackPatternExecutorV1.cs.meta` → `EnemyCommittedAttackPatternExecutor`
- `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyContact2DAdapter.cs` → `EnemyContact2D`
- `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyContact2DAdapter.cs.meta` → `EnemyContact2D`
- `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyHitV1.cs` → `EnemyHit`
- `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyHitV1.cs.meta` → `EnemyHit`
- `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyTarget2DAdapter.cs` → `EnemyTarget2D`
- `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyTarget2DAdapter.cs.meta` → `EnemyTarget2D`
- `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/Presentation/EnemyAttackPresentationProjection2D.cs` → `EnemyAttackPresentation2D`
- `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/Presentation/EnemyAttackPresentationProjection2D.cs.meta` → `EnemyAttackPresentation2D`
- `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/Presentation/EnemyPresentationAdapter2D.cs` → `EnemyPresentation2D`
- `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/Presentation/EnemyPresentationAdapter2D.cs.meta` → `EnemyPresentation2D`
- `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/RunSessionEnemyAttackPatternTimeV1.cs` → `RunSessionEnemyAttackPatternTime`
- `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/RunSessionEnemyAttackPatternTimeV1.cs.meta` → `RunSessionEnemyAttackPatternTime`
- `Assets/ShooterMover/Runtime/UnityAdapters/Input/PlayerMovementIntentAdapter.cs` → `PlayerMovementIntent`
- `Assets/ShooterMover/Runtime/UnityAdapters/Input/PlayerMovementIntentAdapter.cs.meta` → `PlayerMovementIntent`
- `Assets/ShooterMover/Runtime/UnityAdapters/Missions/Rooms/JsonRoomRuntimeBootstrap2D.cs` → `JsonRoomBootstrap2D`
- `Assets/ShooterMover/Runtime/UnityAdapters/Missions/Rooms/JsonRoomRuntimeBootstrap2D.cs.meta` → `JsonRoomBootstrap2D`
- `Assets/ShooterMover/Runtime/UnityAdapters/Missions/Rooms/RoomRuntimeComposition2D.cs` → `Room2D`
- `Assets/ShooterMover/Runtime/UnityAdapters/Missions/Rooms/RoomRuntimeComposition2D.cs.meta` → `Room2D`
- `Assets/ShooterMover/Runtime/UnityAdapters/Missions/Rooms/RoomRuntimePresentationInstances2D.cs` → `RoomPresentationInstances2D`
- `Assets/ShooterMover/Runtime/UnityAdapters/Missions/Rooms/RoomRuntimePresentationInstances2D.cs.meta` → `RoomPresentationInstances2D`
- `Assets/ShooterMover/Runtime/UnityAdapters/Physics/MovementBody2DAdapter.cs` → `MovementBody2D`
- `Assets/ShooterMover/Runtime/UnityAdapters/Physics/MovementBody2DAdapter.cs.meta` → `MovementBody2D`
- `Assets/ShooterMover/Runtime/UnityAdapters/Physics/MovementContact2DAdapter.cs` → `MovementContact2D`
- `Assets/ShooterMover/Runtime/UnityAdapters/Physics/MovementContact2DAdapter.cs.meta` → `MovementContact2D`
- `Assets/ShooterMover/Runtime/UnityAdapters/Players/CanonicalPlayerWeaponSourceV2.cs` → `PlayerWeaponSource`
- `Assets/ShooterMover/Runtime/UnityAdapters/Players/CanonicalPlayerWeaponSourceV2.cs.meta` → `PlayerWeaponSource`
- `Assets/ShooterMover/Runtime/UnityAdapters/Players/MovementActorPlayerRuntimeAdapter.cs` → `MovementActorPlayer`
- `Assets/ShooterMover/Runtime/UnityAdapters/Players/MovementActorPlayerRuntimeAdapter.cs.meta` → `MovementActorPlayer`
- `Assets/ShooterMover/Runtime/UnityAdapters/Players/PlayerInventoryWeaponRuntimeComposition.cs` → `PlayerInventoryWeapon`
- `Assets/ShooterMover/Runtime/UnityAdapters/Players/PlayerInventoryWeaponRuntimeComposition.cs.meta` → `PlayerInventoryWeapon`
- `Assets/ShooterMover/Runtime/UnityAdapters/Players/PlayerRuntimeComposition.cs` → `Player`
- `Assets/ShooterMover/Runtime/UnityAdapters/Players/PlayerRuntimeComposition.cs.meta` → `Player`
- `Assets/ShooterMover/Runtime/UnityAdapters/Players/PlayerRuntimeContracts.cs` → `PlayerContracts`
- `Assets/ShooterMover/Runtime/UnityAdapters/Players/PlayerRuntimeContracts.cs.meta` → `PlayerContracts`
- `Assets/ShooterMover/Runtime/UnityAdapters/Players/RunSessions/ExistingPlayerAndWeaponRunPortsV1.cs` → `ExistingPlayerAndWeaponRunPorts`
- `Assets/ShooterMover/Runtime/UnityAdapters/Players/RunSessions/ExistingPlayerAndWeaponRunPortsV1.cs.meta` → `ExistingPlayerAndWeaponRunPorts`
- `Assets/ShooterMover/Runtime/UnityAdapters/Players/ShooterMover.PlayerRuntime.asmdef.meta` → `ShooterMoverPlayerasmdef`
- `Assets/ShooterMover/Runtime/UnityAdapters/Presentation/Weapons/WeaponArtSpriteRegistryV1.cs` → `WeaponArtSpriteRegistry`
- `Assets/ShooterMover/Runtime/UnityAdapters/Presentation/Weapons/WeaponArtSpriteRegistryV1.cs.meta` → `WeaponArtSpriteRegistry`
- `Assets/ShooterMover/Runtime/UnityAdapters/Progression/Experience/EnemyRewards/EnemyExperienceRewardCatalogAssetV1.cs` → `EnemyExperienceRewardCatalogAsset`
- `Assets/ShooterMover/Runtime/UnityAdapters/Progression/Experience/EnemyRewards/EnemyExperienceRewardCatalogAssetV1.cs.meta` → `EnemyExperienceRewardCatalogAsset`
- `Assets/ShooterMover/Runtime/UnityAdapters/Progression/Experience/EnemyRewards/EnemyExperienceRewardingAuthorityV1.cs` → `EnemyExperienceRewarding`
- `Assets/ShooterMover/Runtime/UnityAdapters/Progression/Experience/EnemyRewards/EnemyExperienceRewardingAuthorityV1.cs.meta` → `EnemyExperienceRewarding`
- `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/Pickups/RewardPickupApplicationAuthority2D.cs` → `RewardPickupApplication2D`
- `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/Pickups/RewardPickupApplicationAuthority2D.cs.meta` → `RewardPickupApplication2D`
- `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/RunPickups/PendingAdmissionPickupBridgeV1.cs` → `PendingAdmissionPickupBridge`
- `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/RunPickups/PendingAdmissionPickupBridgeV1.cs.meta` → `PendingAdmissionPickupBridge`
- `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/RunPickups/RunPickupAuthorityHost2D.cs` → `RunPickupHost2D`
- `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/RunPickups/RunPickupAuthorityHost2D.cs.meta` → `RunPickupHost2D`
- `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/RunPickups/RunPickupPresentationLifecycleV1.cs` → `RunPickupPresentationLifecycle`
- `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/RunPickups/RunPickupPresentationLifecycleV1.cs.meta` → `RunPickupPresentationLifecycle`
- `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/CanonicalWeaponEquipmentProjectionLookupV2.cs` → `WeaponEquipmentLookup`
- `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/CanonicalWeaponEquipmentProjectionLookupV2.cs.meta` → `WeaponEquipmentLookup`
- `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/InventoryBackedWeaponExecutionAdapter.cs` → `InventoryBackedWeaponExecution`
- `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/InventoryBackedWeaponExecutionAdapter.cs.meta` → `InventoryBackedWeaponExecution`
- `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/InventoryWeaponMountedAimExecutionV1.cs` → `InventoryWeaponMountedAimExecution`
- `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/InventoryWeaponMountedAimExecutionV1.cs.meta` → `InventoryWeaponMountedAimExecution`
- `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/InventoryWeaponRuntimeComposition.cs` → `InventoryWeapon`
- `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/InventoryWeaponRuntimeComposition.cs.meta` → `InventoryWeapon`
- `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/ProductionCanonicalNormalProjectile2D.cs` → `NormalProjectile2D`
- `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/ProductionCanonicalNormalProjectile2D.cs.meta` → `NormalProjectile2D`
- `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/ProductionCanonicalProjectileEffectSink2D.cs` → `ProjectileEffectSink2D`
- `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/ProductionCanonicalProjectileEffectSink2D.cs.meta` → `ProjectileEffectSink2D`
- `Assets/ShooterMover/Tests/EditMode/BalanceSimulator/BalanceSimulationServiceV1Tests.cs` → `BalanceSimulationV1Tests`
- `Assets/ShooterMover/Tests/EditMode/BalanceSimulator/BalanceSimulationServiceV1Tests.cs.meta` → `BalanceSimulationV1Tests`
- `Assets/ShooterMover/Tests/EditMode/BalanceSimulator/LootboxSimulatorRuntimeV1.AuthoritativeTests.cs` → `LootboxSimulatorV1AuthoritativeTests`
- `Assets/ShooterMover/Tests/EditMode/BalanceSimulator/LootboxSimulatorRuntimeV1.AuthoritativeTests.cs.meta` → `LootboxSimulatorV1AuthoritativeTests`
- `Assets/ShooterMover/Tests/EditMode/BalanceSimulator/LootboxSimulatorRuntimeV1Tests.cs` → `LootboxSimulatorV1Tests`
- `Assets/ShooterMover/Tests/EditMode/BalanceSimulator/LootboxSimulatorRuntimeV1Tests.cs.meta` → `LootboxSimulatorV1Tests`
- `Assets/ShooterMover/Tests/EditMode/BalanceSimulator/ProductionStrongboxCatalogV1Tests.cs` → `StrongboxCatalogV1Tests`
- `Assets/ShooterMover/Tests/EditMode/BalanceSimulator/ProductionStrongboxCatalogV1Tests.cs.meta` → `StrongboxCatalogV1Tests`
- `Assets/ShooterMover/Tests/EditMode/CanonicalWeaponProjectileSourceIdentityTests.cs` → `WeaponProjectileSourceIdentityTests`
- `Assets/ShooterMover/Tests/EditMode/CanonicalWeaponProjectileSourceIdentityTests.cs.meta` → `WeaponProjectileSourceIdentityTests`
- `Assets/ShooterMover/Tests/EditMode/Characters/Selection/CharacterSelectionServiceV1Tests.cs` → `CharacterSelectionV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Characters/Selection/CharacterSelectionServiceV1Tests.cs.meta` → `CharacterSelectionV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Combat/WeaponRuntimeProfileTests.cs` → `WeaponProfileTests`
- `Assets/ShooterMover/Tests/EditMode/Combat/WeaponRuntimeProfileTests.cs.meta` → `WeaponProfileTests`
- `Assets/ShooterMover/Tests/EditMode/ConditionRuntime/ConditionRuntimeAuthorityV1ReplayHardeningTests.cs` → `ConditionV1ReplayHardeningTests`
- `Assets/ShooterMover/Tests/EditMode/ConditionRuntime/ConditionRuntimeAuthorityV1ReplayHardeningTests.cs.meta` → `ConditionV1ReplayHardeningTests`
- `Assets/ShooterMover/Tests/EditMode/ConditionRuntime/ConditionRuntimeAuthorityV1TestSupport.cs` → `ConditionV1TestSupport`
- `Assets/ShooterMover/Tests/EditMode/ConditionRuntime/ConditionRuntimeAuthorityV1TestSupport.cs.meta` → `ConditionV1TestSupport`
- `Assets/ShooterMover/Tests/EditMode/ConditionRuntime/ConditionRuntimeAuthorityV1Tests.cs` → `ConditionV1Tests`
- `Assets/ShooterMover/Tests/EditMode/ConditionRuntime/ConditionRuntimeAuthorityV1Tests.cs.meta` → `ConditionV1Tests`
- `Assets/ShooterMover/Tests/EditMode/ConditionRuntime/ShooterMover.Tests.EditMode.ConditionRuntime.asmdef.meta` → `ShooterMoverTestsEditModeConditionasmdef`
- `Assets/ShooterMover/Tests/EditMode/ConditionRuntime.meta` → `Condition`
- `Assets/ShooterMover/Tests/EditMode/Crafting/CraftingServiceV1Tests.cs` → `CraftingV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Crafting/CraftingServiceV1Tests.cs.meta` → `CraftingV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Crafting/Integration/CraftingInventoryEquipServiceV1Tests.cs` → `CraftingInventoryEquipV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Crafting/Integration/CraftingInventoryEquipServiceV1Tests.cs.meta` → `CraftingInventoryEquipV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Crafting/Presentation/FakeCraftingAuthority.cs` → `FakeCrafting`
- `Assets/ShooterMover/Tests/EditMode/Crafting/Presentation/FakeCraftingAuthority.cs.meta` → `FakeCrafting`
- `Assets/ShooterMover/Tests/EditMode/Economy/Scrap/ScrapWalletServiceV1Tests.cs` → `ScrapWalletV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Economy/Scrap/ScrapWalletServiceV1Tests.cs.meta` → `ScrapWalletV1Tests`
- `Assets/ShooterMover/Tests/EditMode/EditorTooling/LevelDoorAuthorityV2Tests.cs` → `LevelDoorV2Tests`
- `Assets/ShooterMover/Tests/EditMode/EditorTooling/LevelDoorAuthorityV2Tests.cs.meta` → `LevelDoorV2Tests`
- `Assets/ShooterMover/Tests/EditMode/EditorTooling/LevelGridEditorRuntimeIntegrationV2Tests.cs` → `LevelGridEditorIntegrationV2Tests`
- `Assets/ShooterMover/Tests/EditMode/EditorTooling/LevelGridEditorRuntimeIntegrationV2Tests.cs.meta` → `LevelGridEditorIntegrationV2Tests`
- `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyAttackPatternLiveIntegrationPortsV1.cs` → `EnemyAttackPatternLiveIntegrationPorts`
- `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyAttackPatternLiveIntegrationPortsV1.cs.meta` → `EnemyAttackPatternLiveIntegrationPorts`
- `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyAttackPatternRuntimeV1Tests.cs` → `EnemyAttackPatternV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyAttackPatternRuntimeV1Tests.cs.meta` → `EnemyAttackPatternV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyAttackPatternTestFixturesV1.cs` → `EnemyAttackPatternTestFixtures`
- `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyAttackPatternTestFixturesV1.cs.meta` → `EnemyAttackPatternTestFixtures`
- `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyPlacementRuntimeAuthorityBoundaryV1Tests.cs` → `EnemyPlacementBoundaryV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyPlacementRuntimeAuthorityBoundaryV1Tests.cs.meta` → `EnemyPlacementBoundaryV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyPlacementRuntimeFactoryV1Tests.cs` → `EnemyPlacementFactoryV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyPlacementRuntimeFactoryV1Tests.cs.meta` → `EnemyPlacementFactoryV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyPlacementRuntimeLifecycleRoutingV1Tests.cs` → `EnemyPlacementLifecycleRoutingV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyPlacementRuntimeLifecycleRoutingV1Tests.cs.meta` → `EnemyPlacementLifecycleRoutingV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyRuntimeFoundationTests.cs` → `EnemyFoundationTests`
- `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyRuntimeFoundationTests.cs.meta` → `EnemyFoundationTests`
- `Assets/ShooterMover/Tests/EditMode/Equipment/Upgrades/AugmentUpgradeServiceV1Tests.cs` → `AugmentUpgradeV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Equipment/Upgrades/AugmentUpgradeServiceV1Tests.cs.meta` → `AugmentUpgradeV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Equipment/Upgrades/LegacyFirstWeaponHoldingsAdapterRetirementTests.cs` → `LegacyFirstWeaponHoldingsRetirementTests`
- `Assets/ShooterMover/Tests/EditMode/Equipment/Upgrades/LegacyFirstWeaponHoldingsAdapterRetirementTests.cs.meta` → `LegacyFirstWeaponHoldingsRetirementTests`
- `Assets/ShooterMover/Tests/EditMode/Flow/Hub/CanonicalFirstPlayerHoldingsAuthorityV2Tests.cs` → `FirstPlayerHoldingsV2Tests`
- `Assets/ShooterMover/Tests/EditMode/Flow/Hub/CanonicalFirstPlayerHoldingsAuthorityV2Tests.cs.meta` → `FirstPlayerHoldingsV2Tests`
- `Assets/ShooterMover/Tests/EditMode/Flow/Hub/ProductionExactWeaponInstanceLoadoutTests.cs` → `ExactWeaponInstanceLoadoutTests`
- `Assets/ShooterMover/Tests/EditMode/Flow/Hub/ProductionExactWeaponInstanceLoadoutTests.cs.meta` → `ExactWeaponInstanceLoadoutTests`
- `Assets/ShooterMover/Tests/EditMode/Flow/Hub/ProductionFlowSessionV1Tests.cs` → `FlowSessionV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Flow/Hub/ProductionFlowSessionV1Tests.cs.meta` → `FlowSessionV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Flow/Hub/ProductionOpaqueWeaponInstanceIdentityTests.cs` → `OpaqueWeaponInstanceIdentityTests`
- `Assets/ShooterMover/Tests/EditMode/Flow/Hub/ProductionOpaqueWeaponInstanceIdentityTests.cs.meta` → `OpaqueWeaponInstanceIdentityTests`
- `Assets/ShooterMover/Tests/EditMode/Flow/Hub/ProductionWeaponMountPolicyV1Tests.cs` → `WeaponMountPolicyV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Flow/Hub/ProductionWeaponMountPolicyV1Tests.cs.meta` → `WeaponMountPolicyV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Flow/PlaySelection/PlaySelectionServiceTests.cs` → `PlaySelectionTests`
- `Assets/ShooterMover/Tests/EditMode/Flow/PlaySelection/PlaySelectionServiceTests.cs.meta` → `PlaySelectionTests`
- `Assets/ShooterMover/Tests/EditMode/Flow/ProductionPlayableLevelCatalogAvailabilityTests.cs` → `PlayableLevelCatalogAvailabilityTests`
- `Assets/ShooterMover/Tests/EditMode/Flow/ProductionPlayableLevelCatalogAvailabilityTests.cs.meta` → `PlayableLevelCatalogAvailabilityTests`
- `Assets/ShooterMover/Tests/EditMode/Foundation/BootstrapCompositionRootTests.cs` → `BootstrapRootTests`
- `Assets/ShooterMover/Tests/EditMode/Foundation/BootstrapCompositionRootTests.cs.meta` → `BootstrapRootTests`
- `Assets/ShooterMover/Tests/EditMode/GameplayEntities/PlayerActorAuthorityTests.cs` → `PlayerActorTests`
- `Assets/ShooterMover/Tests/EditMode/GameplayEntities/PlayerActorAuthorityTests.cs.meta` → `PlayerActorTests`
- `Assets/ShooterMover/Tests/EditMode/Holdings/PlayerHoldingsServiceTests.cs` → `PlayerHoldingsTests`
- `Assets/ShooterMover/Tests/EditMode/Holdings/PlayerHoldingsServiceTests.cs.meta` → `PlayerHoldingsTests`
- `Assets/ShooterMover/Tests/EditMode/Inventory/LoadoutScreen/InventoryLoadoutScreenServiceTests.cs` → `InventoryLoadoutScreenTests`
- `Assets/ShooterMover/Tests/EditMode/Inventory/LoadoutScreen/InventoryLoadoutScreenServiceTests.cs.meta` → `InventoryLoadoutScreenTests`
- `Assets/ShooterMover/Tests/EditMode/Missions/Results/MissionRunResultAuthorityV1Tests.Fixtures.cs` → `MissionRunResultV1TestsFixtures`
- `Assets/ShooterMover/Tests/EditMode/Missions/Results/MissionRunResultAuthorityV1Tests.Fixtures.cs.meta` → `MissionRunResultV1TestsFixtures`
- `Assets/ShooterMover/Tests/EditMode/Missions/Results/MissionRunResultAuthorityV1Tests.cs` → `MissionRunResultV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Missions/Results/MissionRunResultAuthorityV1Tests.cs.meta` → `MissionRunResultV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/JsonRoomRuntimeBootstrapCompositionTests.cs` → `JsonRoomBootstrapTests`
- `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/JsonRoomRuntimeBootstrapCompositionTests.cs.meta` → `JsonRoomBootstrapTests`
- `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/RoomAccessAuthorityV1Tests.cs` → `RoomAccessV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/RoomAccessAuthorityV1Tests.cs.meta` → `RoomAccessV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/RoomLiveRuntimeAuthorityTests.Part02.cs` → `RoomLiveTestsPart02`
- `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/RoomLiveRuntimeAuthorityTests.Part02.cs.meta` → `RoomLiveTestsPart02`
- `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/RoomLiveRuntimeAuthorityTests.Part03.cs` → `RoomLiveTestsPart03`
- `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/RoomLiveRuntimeAuthorityTests.Part03.cs.meta` → `RoomLiveTestsPart03`
- `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/RoomLiveRuntimeAuthorityTests.cs` → `RoomLiveTests`
- `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/RoomLiveRuntimeAuthorityTests.cs.meta` → `RoomLiveTests`
- `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/RoomRuntimeAuthorityTests.cs` → `RoomTests`
- `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/RoomRuntimeAuthorityTests.cs.meta` → `RoomTests`
- `Assets/ShooterMover/Tests/EditMode/Modifiers/Events/ActiveEventModifierProjectionV1Tests.cs` → `ActiveEventModifierV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Modifiers/Events/ActiveEventModifierProjectionV1Tests.cs.meta` → `ActiveEventModifierV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Modifiers/RuntimeModifierFoundationV1Tests.cs` → `ModifierFoundationV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Modifiers/RuntimeModifierFoundationV1Tests.cs.meta` → `ModifierFoundationV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Modifiers/StatusEffects/StatusEffectAuthorityV1ApplyPolicyTests.cs` → `StatusEffectV1ApplyPolicyTests`
- `Assets/ShooterMover/Tests/EditMode/Modifiers/StatusEffects/StatusEffectAuthorityV1ApplyPolicyTests.cs.meta` → `StatusEffectV1ApplyPolicyTests`
- `Assets/ShooterMover/Tests/EditMode/Modifiers/StatusEffects/StatusEffectAuthorityV1BridgeCatalogTests.cs` → `StatusEffectV1BridgeCatalogTests`
- `Assets/ShooterMover/Tests/EditMode/Modifiers/StatusEffects/StatusEffectAuthorityV1BridgeCatalogTests.cs.meta` → `StatusEffectV1BridgeCatalogTests`
- `Assets/ShooterMover/Tests/EditMode/Modifiers/StatusEffects/StatusEffectAuthorityV1ReplayLifecycleTests.cs` → `StatusEffectV1ReplayLifecycleTests`
- `Assets/ShooterMover/Tests/EditMode/Modifiers/StatusEffects/StatusEffectAuthorityV1ReplayLifecycleTests.cs.meta` → `StatusEffectV1ReplayLifecycleTests`
- `Assets/ShooterMover/Tests/EditMode/Modifiers/StatusEffects/StatusEffectAuthorityV1StackingTests.cs` → `StatusEffectV1StackingTests`
- `Assets/ShooterMover/Tests/EditMode/Modifiers/StatusEffects/StatusEffectAuthorityV1StackingTests.cs.meta` → `StatusEffectV1StackingTests`
- `Assets/ShooterMover/Tests/EditMode/Persistence/Accounts/PlayerAccountSaveAuthorityV1Tests.cs` → `PlayerAccountSaveV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Persistence/Accounts/PlayerAccountSaveAuthorityV1Tests.cs.meta` → `PlayerAccountSaveV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Persistence/Components/PersistenceTestEconomyTransactionStatusV1.cs` → `PersistenceTestEconomyTransactionStatus`
- `Assets/ShooterMover/Tests/EditMode/Persistence/Components/PersistenceTestEconomyTransactionStatusV1.cs.meta` → `PersistenceTestEconomyTransactionStatus`
- `Assets/ShooterMover/Tests/EditMode/Persistence/Components/PersistenceTestHoldingProvenanceV1.cs` → `PersistenceTestHoldingProvenance`
- `Assets/ShooterMover/Tests/EditMode/Persistence/Components/PersistenceTestHoldingProvenanceV1.cs.meta` → `PersistenceTestHoldingProvenance`
- `Assets/ShooterMover/Tests/EditMode/Persistence/Components/StrongboxSaveAdapterReplayTests.cs` → `StrongboxSaveReplayTests`
- `Assets/ShooterMover/Tests/EditMode/Persistence/Components/StrongboxSaveAdapterReplayTests.cs.meta` → `StrongboxSaveReplayTests`
- `Assets/ShooterMover/Tests/EditMode/Persistence/Composition/CharacterCompositionCoordinatorV1Tests.cs` → `CharacterV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Persistence/Composition/CharacterCompositionCoordinatorV1Tests.cs.meta` → `CharacterV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Persistence/Composition/CollectedRunRewardAtomicCoordinatorTests.cs` → `CollectedRunRewardAtomicTests`
- `Assets/ShooterMover/Tests/EditMode/Persistence/Composition/CollectedRunRewardAtomicCoordinatorTests.cs.meta` → `CollectedRunRewardAtomicTests`
- `Assets/ShooterMover/Tests/EditMode/Persistence/Composition/ProductionWeaponOnboardingAndMigrationTests.cs` → `WeaponOnboardingAndMigrationTests`
- `Assets/ShooterMover/Tests/EditMode/Persistence/Composition/ProductionWeaponOnboardingAndMigrationTests.cs.meta` → `WeaponOnboardingAndMigrationTests`
- `Assets/ShooterMover/Tests/EditMode/Persistence/Composition/ShooterMover.Tests.EditMode.Persistence.Composition.asmdef.meta` → `ShooterMoverTestsEditModePersistenceasmdef`
- `Assets/ShooterMover/Tests/EditMode/Persistence/Composition/StrongboxPersistenceCoordinatorV1TestFixture.cs` → `StrongboxPersistenceV1TestFixture`
- `Assets/ShooterMover/Tests/EditMode/Persistence/Composition/StrongboxPersistenceCoordinatorV1TestFixture.cs.meta` → `StrongboxPersistenceV1TestFixture`
- `Assets/ShooterMover/Tests/EditMode/Persistence/Composition.meta` → ``
- `Assets/ShooterMover/Tests/EditMode/PlayerRuntime/PlayerLiveAuthorityTests.cs` → `PlayerLiveTests`
- `Assets/ShooterMover/Tests/EditMode/PlayerRuntime/PlayerLiveAuthorityTests.cs.meta` → `PlayerLiveTests`
- `Assets/ShooterMover/Tests/EditMode/PlayerRuntime/PlayerRuntimeCompositionTests.cs` → `PlayerTests`
- `Assets/ShooterMover/Tests/EditMode/PlayerRuntime/PlayerRuntimeCompositionTests.cs.meta` → `PlayerTests`
- `Assets/ShooterMover/Tests/EditMode/PlayerRuntime/ShooterMover.Tests.EditMode.PlayerRuntime.asmdef.meta` → `ShooterMoverTestsEditModePlayerasmdef`
- `Assets/ShooterMover/Tests/EditMode/PlayerRuntime.meta` → `Player`
- `Assets/ShooterMover/Tests/EditMode/Progression/Experience/PlayerExperienceAuthorityTests.cs` → `PlayerExperienceTests`
- `Assets/ShooterMover/Tests/EditMode/Progression/Experience/PlayerExperienceAuthorityTests.cs.meta` → `PlayerExperienceTests`
- `Assets/ShooterMover/Tests/EditMode/Progression/Skills/SkillProgressionAuthorityTests.cs` → `SkillProgressionTests`
- `Assets/ShooterMover/Tests/EditMode/Props/DestructiblePropAuthorityTests.cs` → `DestructiblePropTests`
- `Assets/ShooterMover/Tests/EditMode/Props/DestructiblePropAuthorityTests.cs.meta` → `DestructiblePropTests`
- `Assets/ShooterMover/Tests/EditMode/Props/PropRuntimeV1Tests.cs` → `PropV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Props/PropRuntimeV1Tests.cs.meta` → `PropV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Rewards/Application/RewardApplicationServiceV1Tests.cs` → `RewardApplicationV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Rewards/Application/RewardApplicationServiceV1Tests.cs.meta` → `RewardApplicationV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Rewards/Drops/RunRewardRuntimeSnapshotV1Tests.cs` → `RunRewardSnapshotV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Rewards/Drops/RunRewardRuntimeSnapshotV1Tests.cs.meta` → `RunRewardSnapshotV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Rewards/Generation/RewardGenerationServiceV1Tests.cs` → `RewardGenerationV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Rewards/Generation/RewardGenerationServiceV1Tests.cs.meta` → `RewardGenerationV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Rewards/Strongboxes/StrongboxOpeningServiceV1Tests.cs` → `StrongboxOpeningV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Rewards/Strongboxes/StrongboxOpeningServiceV1Tests.cs.meta` → `StrongboxOpeningV1Tests`
- `Assets/ShooterMover/Tests/EditMode/RunPickups/RunLocalPickupAuthorityCollectionTests.cs` → `RunLocalPickupCollectionTests`
- `Assets/ShooterMover/Tests/EditMode/RunPickups/RunLocalPickupAuthorityCollectionTests.cs.meta` → `RunLocalPickupCollectionTests`
- `Assets/ShooterMover/Tests/EditMode/RunPickups/RunLocalPickupAuthorityTestSupport.cs` → `RunLocalPickupTestSupport`
- `Assets/ShooterMover/Tests/EditMode/RunPickups/RunLocalPickupAuthorityTestSupport.cs.meta` → `RunLocalPickupTestSupport`
- `Assets/ShooterMover/Tests/EditMode/RunPickups/RunLocalPickupAuthorityV1Tests.cs` → `RunLocalPickupV1Tests`
- `Assets/ShooterMover/Tests/EditMode/RunPickups/RunLocalPickupAuthorityV1Tests.cs.meta` → `RunLocalPickupV1Tests`
- `Assets/ShooterMover/Tests/EditMode/RunSessions/RunSessionAuthorityV1Tests.cs` → `RunSessionV1Tests`
- `Assets/ShooterMover/Tests/EditMode/RunSessions/RunSessionAuthorityV1Tests.cs.meta` → `RunSessionV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Shops/ShopRuntimeServiceV1Tests.Fixtures.cs` → `ShopV1TestsFixtures`
- `Assets/ShooterMover/Tests/EditMode/Shops/ShopRuntimeServiceV1Tests.Fixtures.cs.meta` → `ShopV1TestsFixtures`
- `Assets/ShooterMover/Tests/EditMode/Shops/ShopRuntimeServiceV1Tests.More.cs` → `ShopV1TestsMore`
- `Assets/ShooterMover/Tests/EditMode/Shops/ShopRuntimeServiceV1Tests.More.cs.meta` → `ShopV1TestsMore`
- `Assets/ShooterMover/Tests/EditMode/Shops/ShopRuntimeServiceV1Tests.cs` → `ShopV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Shops/ShopRuntimeServiceV1Tests.cs.meta` → `ShopV1Tests`
- `Assets/ShooterMover/Tests/EditMode/TerminalDropBinding/EnemyDeathTerminalDropFactAdapterV1.cs` → `EnemyDeathTerminalDropFact`
- `Assets/ShooterMover/Tests/EditMode/TerminalDropBinding/EnemyDeathTerminalDropFactAdapterV1.cs.meta` → `EnemyDeathTerminalDropFact`
- `Assets/ShooterMover/Tests/EditMode/TerminalDropBinding/EnemyTerminalSourceContextAdapterV1Tests.cs` → `EnemyTerminalSourceContextV1Tests`
- `Assets/ShooterMover/Tests/EditMode/TerminalDropBinding/EnemyTerminalSourceContextAdapterV1Tests.cs.meta` → `EnemyTerminalSourceContextV1Tests`
- `Assets/ShooterMover/Tests/EditMode/TerminalDropBinding/TerminalDropGenerationAuthorityV1Tests.cs` → `TerminalDropGenerationV1Tests`
- `Assets/ShooterMover/Tests/EditMode/TerminalDropBinding/TerminalDropGenerationAuthorityV1Tests.cs.meta` → `TerminalDropGenerationV1Tests`
- `Assets/ShooterMover/Tests/EditMode/Weapons/Live/InventoryBackedWeaponExecutionAdapterFakes.cs` → `InventoryBackedWeaponExecutionFakes`
- `Assets/ShooterMover/Tests/EditMode/Weapons/Live/InventoryBackedWeaponExecutionAdapterFakes.cs.meta` → `InventoryBackedWeaponExecutionFakes`
- `Assets/ShooterMover/Tests/EditMode/Weapons/Live/InventoryBackedWeaponExecutionAdapterFixtures.cs` → `InventoryBackedWeaponExecutionFixtures`
- `Assets/ShooterMover/Tests/EditMode/Weapons/Live/InventoryBackedWeaponExecutionAdapterFixtures.cs.meta` → `InventoryBackedWeaponExecutionFixtures`
- `Assets/ShooterMover/Tests/EditMode/Weapons/Live/InventoryBackedWeaponExecutionAdapterReplayTests.cs` → `InventoryBackedWeaponExecutionReplayTests`
- `Assets/ShooterMover/Tests/EditMode/Weapons/Live/InventoryBackedWeaponExecutionAdapterReplayTests.cs.meta` → `InventoryBackedWeaponExecutionReplayTests`
- `Assets/ShooterMover/Tests/EditMode/Weapons/Live/InventoryBackedWeaponExecutionAdapterTests.cs` → `InventoryBackedWeaponExecutionTests`
- `Assets/ShooterMover/Tests/EditMode/Weapons/Live/InventoryBackedWeaponExecutionAdapterTests.cs.meta` → `InventoryBackedWeaponExecutionTests`
- `Assets/ShooterMover/Tests/PlayMode/Combat/PlayerCombatIntentAdapterTests.cs` → `PlayerCombatIntentTests`
- `Assets/ShooterMover/Tests/PlayMode/Combat/PlayerCombatIntentAdapterTests.cs.meta` → `PlayerCombatIntentTests`
- `Assets/ShooterMover/Tests/PlayMode/Combat/WeaponMount2DAdapterTests.cs` → `WeaponMount2DTests`
- `Assets/ShooterMover/Tests/PlayMode/Combat/WeaponMount2DAdapterTests.cs.meta` → `WeaponMount2DTests`
- `Assets/ShooterMover/Tests/PlayMode/Enemies/EnemyActor2DAdapterTests.cs` → `EnemyActor2DTests`
- `Assets/ShooterMover/Tests/PlayMode/Enemies/EnemyActor2DAdapterTests.cs.meta` → `EnemyActor2DTests`
- `Assets/ShooterMover/Tests/PlayMode/Flow/InventoryLoadout/InventoryLoadoutAuthorityConnectionTests.cs` → `InventoryLoadoutConnectionTests`
- `Assets/ShooterMover/Tests/PlayMode/Flow/InventoryLoadout/InventoryLoadoutAuthorityConnectionTests.cs.meta` → `InventoryLoadoutConnectionTests`
- `Assets/ShooterMover/Tests/PlayMode/Flow/ProductionFlow/CanonicalUiOwnershipPlayModeTests.cs` → `UiOwnershipPlayModeTests`
- `Assets/ShooterMover/Tests/PlayMode/Flow/ProductionFlow/CanonicalUiOwnershipPlayModeTests.cs.meta` → `UiOwnershipPlayModeTests`
- `Assets/ShooterMover/Tests/PlayMode/Flow/ProductionFlow/ProductionFlowPlayModeTests.cs` → `FlowPlayModeTests`
- `Assets/ShooterMover/Tests/PlayMode/Flow/ProductionFlow/ProductionFlowPlayModeTests.cs.meta` → `FlowPlayModeTests`
- `Assets/ShooterMover/Tests/PlayMode/Flow/ProductionFlow/ShooterMover.Tests.PlayMode.Flow.ProductionFlow.asmdef.meta` → `ShooterMoverTestsPlayModeFlowFlowasmdef`
- `Assets/ShooterMover/Tests/PlayMode/Flow/ProductionFlow.meta` → `Flow`
- `Assets/ShooterMover/Tests/PlayMode/Movement/MovementBody2DAdapterTests.cs` → `MovementBody2DTests`
- `Assets/ShooterMover/Tests/PlayMode/Movement/MovementBody2DAdapterTests.cs.meta` → `MovementBody2DTests`
- `Assets/ShooterMover/Tests/PlayMode/Movement/MovementContact2DAdapterTests.cs` → `MovementContact2DTests`
- `Assets/ShooterMover/Tests/PlayMode/Movement/MovementContact2DAdapterTests.cs.meta` → `MovementContact2DTests`
- `Assets/ShooterMover/Tests/PlayMode/Movement/PlayerMovementIntentAdapterTests.cs` → `PlayerMovementIntentTests`
- `Assets/ShooterMover/Tests/PlayMode/Movement/PlayerMovementIntentAdapterTests.cs.meta` → `PlayerMovementIntentTests`
- `Assets/ShooterMover/Tests/PlayMode/Progression/Experience/EnemyRewards/EnemyExperienceRewardingAuthorityTests.cs` → `EnemyExperienceRewardingTests`
- `Assets/ShooterMover/Tests/PlayMode/Progression/Experience/EnemyRewards/EnemyExperienceRewardingAuthorityTests.cs.meta` → `EnemyExperienceRewardingTests`
- `Assets/ShooterMover/Tests/PlayMode/RunPickups/LootPickupRunProjectionPlayModeTests.cs` → `LootPickupRunPlayModeTests`
- `Assets/ShooterMover/Tests/PlayMode/RunPickups/LootPickupRunProjectionPlayModeTests.cs.meta` → `LootPickupRunPlayModeTests`
- `Assets/ShooterMover/Tests/PlayMode/Weapons/Live/CanonicalWeaponGameplayResolutionPlayModeTests.cs` → `WeaponGameplayResolutionPlayModeTests`
- `Assets/ShooterMover/Tests/PlayMode/Weapons/Live/CanonicalWeaponGameplayResolutionPlayModeTests.cs.meta` → `WeaponGameplayResolutionPlayModeTests`
- `Assets/ShooterMover/Tests/PlayMode/Weapons/Live/InventoryWeaponRuntimePlayModeFakes.cs` → `InventoryWeaponPlayModeFakes`
- `Assets/ShooterMover/Tests/PlayMode/Weapons/Live/InventoryWeaponRuntimePlayModeFakes.cs.meta` → `InventoryWeaponPlayModeFakes`
- `Assets/ShooterMover/Tests/PlayMode/Weapons/Live/InventoryWeaponRuntimePlayModeFixtures.cs` → `InventoryWeaponPlayModeFixtures`
- `Assets/ShooterMover/Tests/PlayMode/Weapons/Live/InventoryWeaponRuntimePlayModeFixtures.cs.meta` → `InventoryWeaponPlayModeFixtures`
- `Assets/ShooterMover/Tests/PlayMode/Weapons/Live/InventoryWeaponRuntimePlayModeTests.cs` → `InventoryWeaponPlayModeTests`
- `Assets/ShooterMover/Tests/PlayMode/Weapons/Live/InventoryWeaponRuntimePlayModeTests.cs.meta` → `InventoryWeaponPlayModeTests`
- `Assets/ShooterMover/UI/CharacterSelect/CharacterSelectControllerV1.cs` → `CharacterSelectController`
- `Assets/ShooterMover/UI/CharacterSelect/CharacterSelectControllerV1.cs.meta` → `CharacterSelectController`
- `Assets/ShooterMover/UI/Crafting/CraftingScreenControllerV1.cs` → `CraftingScreenController`
- `Assets/ShooterMover/UI/Crafting/CraftingScreenControllerV1.cs.meta` → `CraftingScreenController`
- `Assets/ShooterMover/UI/Hub/HubFlowControllerV1.cs` → `HubFlowController`
- `Assets/ShooterMover/UI/Hub/HubFlowControllerV1.cs.meta` → `HubFlowController`
- `Assets/ShooterMover/UI/InventoryLoadout/InventoryLoadoutScreenControllerV1.cs` → `InventoryLoadoutScreenController`
- `Assets/ShooterMover/UI/InventoryLoadout/InventoryLoadoutScreenControllerV1.cs.meta` → `InventoryLoadoutScreenController`
- `Assets/ShooterMover/UI/InventoryLoadout/WeaponInventoryCardPresentationV1.cs` → `WeaponInventoryCardPresentation`
- `Assets/ShooterMover/UI/InventoryLoadout/WeaponInventoryCardPresentationV1.cs.meta` → `WeaponInventoryCardPresentation`
- `Assets/ShooterMover/UI/LevelSelection/LevelSelectionControllerV1.cs` → `LevelSelectionController`
- `Assets/ShooterMover/UI/LevelSelection/LevelSelectionControllerV1.cs.meta` → `LevelSelectionController`
- `Assets/ShooterMover/UI/LevelSelection/LevelSelectionRoutingV1.cs` → `LevelSelectionRouting`
- `Assets/ShooterMover/UI/LevelSelection/LevelSelectionRoutingV1.cs.meta` → `LevelSelectionRouting`
- `Assets/ShooterMover/UI/LevelSelection/LevelSelectionViewV1.cs` → `LevelSelectionView`
- `Assets/ShooterMover/UI/LevelSelection/LevelSelectionViewV1.cs.meta` → `LevelSelectionView`
- `Assets/ShooterMover/UI/PlaySelection/PlaySelectionControllerV1.cs` → `PlaySelectionController`
- `Assets/ShooterMover/UI/PlaySelection/PlaySelectionControllerV1.cs.meta` → `PlaySelectionController`
- `Assets/ShooterMover/UI/ProductionFlow/EnemyPlayerDamageContractsV1.cs` → `EnemyPlayerDamageContracts`
- `Assets/ShooterMover/UI/ProductionFlow/EnemyPlayerDamageContractsV1.cs.meta` → `EnemyPlayerDamageContracts`
- `Assets/ShooterMover/UI/ProductionFlow/EnemyPlayerDamageIntegrationV1.cs` → `EnemyPlayerDamageIntegration`
- `Assets/ShooterMover/UI/ProductionFlow/EnemyPlayerDamageIntegrationV1.cs.meta` → `EnemyPlayerDamageIntegration`
- `Assets/ShooterMover/UI/ProductionFlow/PlayablePlayerVitalsV1.cs` → `PlayablePlayerVitals`
- `Assets/ShooterMover/UI/ProductionFlow/PlayablePlayerVitalsV1.cs.meta` → `PlayablePlayerVitals`
- `Assets/ShooterMover/UI/ProductionFlow/PlayerPrefsProductionFlowProfileStoreV1.cs` → `PlayerPrefsFlowProfileStore`
- `Assets/ShooterMover/UI/ProductionFlow/PlayerPrefsProductionFlowProfileStoreV1.cs.meta` → `PlayerPrefsFlowProfileStore`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionCanonicalWeaponFireControllerV1.cs` → `WeaponFireController`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionCanonicalWeaponFireControllerV1.cs.meta` → `WeaponFireController`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionCanonicalWeaponGameplayBindingV2.cs` → `WeaponGameplayBinding`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionCanonicalWeaponGameplayBindingV2.cs.meta` → `WeaponGameplayBinding`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionCharacterSelectionControllerV1.cs` → `CharacterSelectionController`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionCharacterSelectionControllerV1.cs.meta` → `CharacterSelectionController`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionCharacterStrongboxBridgeV1.cs` → `CharacterStrongboxBridge`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionCharacterStrongboxBridgeV1.cs.meta` → `CharacterStrongboxBridge`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionCollectedRunRewardRecoveryV2.cs` → `CollectedRunRewardRecovery`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionCollectedRunRewardRecoveryV2.cs.meta` → `CollectedRunRewardRecovery`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionCollectedRunRewardResultsOverlay.cs` → `CollectedRunRewardResultsOverlay`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionCollectedRunRewardResultsOverlay.cs.meta` → `CollectedRunRewardResultsOverlay`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionCollectedRunRewardTerminalNoticeV1.cs` → `CollectedRunRewardTerminalNotice`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionCollectedRunRewardTerminalNoticeV1.cs.meta` → `CollectedRunRewardTerminalNotice`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionMainMenuControllerV1.cs` → `MainMenuController`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionMainMenuControllerV1.cs.meta` → `MainMenuController`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionPlayableLevelControllerV1.cs` → `PlayableLevelController`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionPlayableLevelControllerV1.cs.meta` → `PlayableLevelController`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionResultsControllerV1.cs` → `ResultsController`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionResultsControllerV1.cs.meta` → `ResultsController`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionRunRewardRuntimeV1.cs` → `RunReward`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionRunRewardRuntimeV1.cs.meta` → `RunReward`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionRunRewardSceneCompositionV1.cs` → `RunRewardScene`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionRunRewardSceneCompositionV1.cs.meta` → `RunRewardScene`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionRunSessionPortsV1.cs` → `RunSessionPorts`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionRunSessionPortsV1.cs.meta` → `RunSessionPorts`
- `Assets/ShooterMover/UI/ProductionFlow/ShooterMover.UI.ProductionFlow.asmdef.meta` → `ShooterMoverFlowasmdef`
- `Assets/ShooterMover/UI/ProductionFlow.meta` → `Flow`
- `Assets/ShooterMover/UI/Shop/ShopScreenControllerV1.cs` → `ShopScreenController`
- `Assets/ShooterMover/UI/Shop/ShopScreenControllerV1.cs.meta` → `ShopScreenController`
- `Assets/ShooterMover/UI/Shop/ShopScreenRuntimeHandoffV1.cs` → `ShopScreenHandoff`
- `Assets/ShooterMover/UI/Shop/ShopScreenRuntimeHandoffV1.cs.meta` → `ShopScreenHandoff`
- `Assets/ShooterMover/UI/Skills/SkillsScreenHubAdapterV1.cs` → `SkillsScreenHub`
- `Assets/ShooterMover/UI/Skills/SkillsScreenHubAdapterV1.cs.meta` → `SkillsScreenHub`
- `Assets/ShooterMover/UI/StrongboxOpening/LootPickupPresentationModelsV1.cs` → `LootPickupPresentationModels`
- `Assets/ShooterMover/UI/StrongboxOpening/LootPickupPresentationModelsV1.cs.meta` → `LootPickupPresentationModels`
- `Assets/ShooterMover/UI/StrongboxOpening/LootPickupRunProjection2D.cs` → `LootPickupRun2D`
- `Assets/ShooterMover/UI/StrongboxOpening/LootPickupRunProjection2D.cs.meta` → `LootPickupRun2D`
- `Assets/ShooterMover/UI/StrongboxOpening/LootPresentationDevelopmentPickupFixtureV1.cs` → `LootPresentationDevelopmentPickupFixture`
- `Assets/ShooterMover/UI/StrongboxOpening/LootPresentationDevelopmentPickupFixtureV1.cs.meta` → `LootPresentationDevelopmentPickupFixture`
- `Assets/ShooterMover/UI/StrongboxOpening/LootRunHudViewV1.cs` → `LootRunHudView`
- `Assets/ShooterMover/UI/StrongboxOpening/LootRunHudViewV1.cs.meta` → `LootRunHudView`
- `Assets/ShooterMover/UI/StrongboxOpening/OwnedStrongboxGroupsViewV1.cs` → `OwnedStrongboxGroupsView`
- `Assets/ShooterMover/UI/StrongboxOpening/OwnedStrongboxGroupsViewV1.cs.meta` → `OwnedStrongboxGroupsView`
- `Assets/ShooterMover/UI/StrongboxOpening/StrongboxGroupingPresentationV1.cs` → `StrongboxGroupingPresentation`
- `Assets/ShooterMover/UI/StrongboxOpening/StrongboxGroupingPresentationV1.cs.meta` → `StrongboxGroupingPresentation`
- `Assets/ShooterMover/UI/StrongboxOpening/StrongboxOpeningPresentationViewV1.cs` → `StrongboxOpeningPresentationView`
- `Assets/ShooterMover/UI/StrongboxOpening/StrongboxOpeningPresentationViewV1.cs.meta` → `StrongboxOpeningPresentationView`
- `Assets/ShooterMover/UI/StrongboxOpening/StrongboxRewardCardsViewV1.cs` → `StrongboxRewardCardsView`
- `Assets/ShooterMover/UI/StrongboxOpening/StrongboxRewardCardsViewV1.cs.meta` → `StrongboxRewardCardsView`
