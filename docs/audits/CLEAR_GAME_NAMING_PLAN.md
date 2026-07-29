# Clear Game Naming Plan

- Type declarations scanned: **3457**
- Declarations whose names change: **1884**
- Unique identifier replacements: **1776**
- Matching C# file moves: **674**
- Same-namespace collision groups: **28**

## Rules

- Remove `Production` and `Canonical`.
- Remove numeric `V1` / `V2` style tags.
- `Composition` → `Setup`.
- `Projection` → `View`.
- `Authority` → `State`.
- `Adapter` → `Bridge`.
- `Runtime` → `Live`.
- `Service` → `Actions`.
- `Coordinator` → `Flow`.

## Remaining collisions

### `ShooterMover.Application.Flow.Production.WeaponInventoryState`

- `ProductionWeaponInventoryStateV1`
- `ProductionWeaponInventoryStateV2`
  - `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponOnboardingV1.cs`
  - `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponOnboardingV2.cs`

### `ShooterMover.Application.Flow.Production.WeaponMountBinding`

- `ProductionWeaponMountBindingV1`
- `WeaponMountBindingV2`
  - `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponMountLoadoutV2.cs`
  - `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponMountPolicyV1.cs`

### `ShooterMover.Application.Flow.Production.WeaponOnboarding`

- `ProductionWeaponOnboardingV1`
- `ProductionWeaponOnboardingV2`
  - `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponOnboardingV1.cs`
  - `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponOnboardingV2.cs`

### `ShooterMover.Application.Missions.Rooms.RoomLiveState`

- `RoomLiveRuntimeAuthorityV1`
- `RoomRuntimeAuthorityV1`
  - `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveRuntimeAuthorityCoreV1.cs`
  - `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomRuntimeAuthorityV1.cs`

### `ShooterMover.Application.Missions.Rooms.Content.DecorDto`

- `DecorDto`
- `V1DecorDto`
  - `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/LevelGridV2CompilerDtos.cs`
  - `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentJsonDtosV1.cs`

### `ShooterMover.Application.Missions.Rooms.Content.DoorDto`

- `DoorDto`
- `V1DoorDto`
  - `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/LevelGridV2CompilerDtos.cs`
  - `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomAccessJsonImporterV1.cs`
  - `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentJsonDtosV1.cs`

### `ShooterMover.Application.Missions.Rooms.Content.DoorLinkDto`

- `DoorLinkDto`
- `V1DoorLinkDto`
  - `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/LevelGridV2CompilerDtos.cs`
  - `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentJsonDtosV1.cs`

### `ShooterMover.Application.Missions.Rooms.Content.EncounterDto`

- `EncounterDto`
- `V1EncounterDto`
  - `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/LevelGridV2CompilerDtos.cs`
  - `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentJsonDtosV1.cs`

### `ShooterMover.Application.Missions.Rooms.Content.EnemiesDto`

- `EnemiesDto`
- `V1EnemiesDto`
  - `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/LevelGridV2CompilerDtos.cs`
  - `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentJsonDtosV1.cs`

### `ShooterMover.Application.Missions.Rooms.Content.ManifestDto`

- `ManifestDto`
- `V1ManifestDto`
  - `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/LevelGridV2CompilerDtos.cs`
  - `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentJsonDtosV1.cs`

### `ShooterMover.Application.Missions.Rooms.Content.PropsDto`

- `PropsDto`
- `V1PropsDto`
  - `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/LevelGridV2CompilerDtos.cs`
  - `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentJsonDtosV1.cs`

### `ShooterMover.Application.Missions.Rooms.Content.RoomDocumentsDto`

- `RoomDocumentsDto`
- `V1RoomDocumentsDto`
  - `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/LevelGridV2CompilerDtos.cs`
  - `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentJsonDtosV1.cs`

### `ShooterMover.Application.Missions.Rooms.Content.RoomLayoutDto`

- `RoomLayoutDto`
- `V1RoomLayoutDto`
  - `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/LevelGridV2CompilerDtos.cs`
  - `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentJsonDtosV1.cs`

### `ShooterMover.Application.Missions.Rooms.Content.SpawnDto`

- `SpawnDto`
- `V1SpawnDto`
  - `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/LevelGridV2CompilerDtos.cs`
  - `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentJsonDtosV1.cs`

### `ShooterMover.Application.Rewards.CollectedRunTransfers.CollectedRunRewardPreparedTransferState`

- `CollectedRunRewardPreparedTransferAuthorityV1`
- `CollectedRunRewardPreparedTransferStateV1`
  - `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardPreparedTransfersV1.cs`

### `ShooterMover.Application.Rewards.CollectedRunTransfers.ICollectedRunEquipmentPayloadSource`

- `ICollectedRunEquipmentPayloadSource`
- `ICollectedRunEquipmentPayloadSourceV2`
  - `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/ProductionCollectedRunRewardPreparationV2.cs`
  - `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/ProductionCollectedRunRewardRegistryCompatibility.cs`

### `ShooterMover.ContentPackages.Props.DestructibleProps.DestructiblePropState`

- `DestructiblePropAuthority`
- `DestructiblePropState`
  - `Assets/ShooterMover/ContentPackages/Props/DestructibleProps/DestructiblePropAuthority.cs`

### `ShooterMover.Domain.Modifiers.StatusEffects.StatusEffectCommand`

- `StatusEffectCommandCanonicalV1`
- `StatusEffectCommandV1`
  - `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectDefinitionsV1.cs`

### `ShooterMover.Domain.Modifiers.StatusEffects.StatusEffectStateSnapshot`

- `StatusEffectAuthoritySnapshotV1`
- `StatusEffectStateSnapshotV1`
  - `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectReplaySnapshotsV1.cs`
  - `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectStateSnapshotV1.cs`

### `ShooterMover.Editor.LevelDesign.Foundation.DoorDto`

- `DoorDto`
- `DoorDtoV2`
  - `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs`
  - `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridV2PlayableExporterUtilities.cs`

### `ShooterMover.Editor.LevelDesign.Foundation.DoorsDto`

- `DoorsDto`
- `DoorsDtoV2`
  - `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs`
  - `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridV2PlayableExporterUtilities.cs`

### `ShooterMover.Editor.LevelDesign.Foundation.EndpointDto`

- `EndpointDto`
- `EndpointDtoV2`
  - `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs`
  - `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridV2PlayableExporterUtilities.cs`

### `ShooterMover.Editor.LevelDesign.Foundation.LevelDto`

- `LevelDto`
- `LevelDtoV2`
  - `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs`
  - `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridV2PlayableExporterUtilities.cs`

### `ShooterMover.Editor.LevelDesign.Foundation.MapDto`

- `MapDto`
- `MapDtoV2`
  - `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs`
  - `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridV2PlayableExporterUtilities.cs`

### `ShooterMover.Editor.LevelDesign.Foundation.MapNodeDto`

- `MapNodeDto`
- `MapNodeDtoV2`
  - `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs`
  - `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridV2PlayableExporterUtilities.cs`

### `ShooterMover.Editor.LevelDesign.Foundation.RoomDto`

- `RoomDto`
- `RoomDtoV2`
  - `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs`
  - `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridV2PlayableExporterUtilities.cs`

### `ShooterMover.Editor.LevelDesign.Foundation.RoomIndexDto`

- `RoomIndexDto`
- `RoomIndexDtoV2`
  - `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs`
  - `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridV2PlayableExporterUtilities.cs`

### `ShooterMover.Tests.EditMode.Missions.Rooms.RoomLiveStateTests`

- `RoomLiveRuntimeAuthorityTests`
- `RoomRuntimeAuthorityTests`
  - `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/RoomLiveRuntimeAuthorityTests.Part02.cs`
  - `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/RoomLiveRuntimeAuthorityTests.Part03.cs`
  - `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/RoomLiveRuntimeAuthorityTests.cs`
  - `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/RoomRuntimeAuthorityTests.cs`

## Identifier replacements

| Current | New |
|---|---|
| `AcceptedEmissionRuntimeAdapter` | `AcceptedEmissionLiveBridge` |
| `AcceptedEmissionRuntimeAdapterResult` | `AcceptedEmissionLiveBridgeResult` |
| `AcceptedEmissionRuntimeAdapterStatus` | `AcceptedEmissionLiveBridgeStatus` |
| `AcceptedGameplayFactAdapterRegistryV1` | `AcceptedGameplayFactBridgeRegistry` |
| `AcceptedGameplayFactDeliveryV1` | `AcceptedGameplayFactDelivery` |
| `AccountCompatibilityV1Tests` | `AccountCompatibilityTests` |
| `ActivateRoomCommandV1` | `ActivateRoomCommand` |
| `ActiveEventDescriptorV1` | `ActiveEventDescriptor` |
| `ActiveEventModifierProjectionServiceV1` | `ActiveEventModifierViewActions` |
| `ActiveEventModifierProjectionV1Tests` | `ActiveEventModifierViewTests` |
| `ActiveEventModifierSnapshotV1` | `ActiveEventModifierSnapshot` |
| `ActiveEventProjectionResultV1` | `ActiveEventViewResult` |
| `ActiveEventProjectionStatusV1` | `ActiveEventViewStatus` |
| `ActiveStatusEffectSnapshotV1` | `ActiveStatusEffectSnapshot` |
| `ActiveStatusEffectStackSnapshotV1` | `ActiveStatusEffectStackSnapshot` |
| `AdvanceRunSessionTimeCommandV1` | `AdvanceRunSessionTimeCommand` |
| `AdvanceStatusEffectTickCommandV1` | `AdvanceStatusEffectTickCommand` |
| `AllocateSkillRankCommandV2` | `AllocateSkillRankCommand` |
| `AlternateAdapter` | `AlternateBridge` |
| `ApplyStatusEffectCommandV1` | `ApplyStatusEffectCommand` |
| `AreaDtoV1` | `AreaDto` |
| `AtomicPlayerAccountStoreV1` | `AtomicPlayerAccountStore` |
| `AtomicSaveAndCompensationV1Tests` | `AtomicSaveAndCompensationTests` |
| `AttackDtoV1` | `AttackDto` |
| `AttributedTerminalRewardParticipantResolverV1` | `AttributedTerminalRewardParticipantResolver` |
| `AugmentGenerationCandidateV1` | `AugmentGenerationCandidate` |
| `AugmentTierCostCurveV1` | `AugmentTierCostCurve` |
| `AugmentUpgradeCanonicalV1` | `AugmentUpgrade` |
| `AugmentUpgradeConfirmationStatusV1` | `AugmentUpgradeConfirmationStatus` |
| `AugmentUpgradeConfirmationV1` | `AugmentUpgradeConfirmation` |
| `AugmentUpgradeCostPolicyV1` | `AugmentUpgradeCostPolicy` |
| `AugmentUpgradeCostStatusV1` | `AugmentUpgradeCostStatus` |
| `AugmentUpgradeFactV1` | `AugmentUpgradeFact` |
| `AugmentUpgradeIdentityContextV1` | `AugmentUpgradeIdentityContext` |
| `AugmentUpgradeQuoteRequestV1` | `AugmentUpgradeQuoteRequest` |
| `AugmentUpgradeQuoteResultV1` | `AugmentUpgradeQuoteResult` |
| `AugmentUpgradeQuoteStatusV1` | `AugmentUpgradeQuoteStatus` |
| `AugmentUpgradeQuoteV1` | `AugmentUpgradeQuote` |
| `AugmentUpgradeRetryCommandV1` | `AugmentUpgradeRetryCommand` |
| `AugmentUpgradeServiceV1` | `AugmentUpgradeActions` |
| `AugmentUpgradeServiceV1Tests` | `AugmentUpgradeActionsTests` |
| `AuthorableRoomDefinitionV1` | `AuthorableRoomDefinition` |
| `AuthorableRoomGraphDefinitionV1` | `AuthorableRoomGraphDefinition` |
| `AuthoritativeStrongboxPreparedOpenV1` | `AuthoritativeStrongboxPreparedOpen` |
| `AuthoritativeStrongboxSimulationGatewayFactoryV1` | `AuthoritativeStrongboxSimulationGatewayFactory` |
| `AuthoritativeStrongboxSimulationProductionGatewayV1` | `AuthoritativeStrongboxSimulationGateway` |
| `AuthoritativeStrongboxSimulationRunnerV1` | `AuthoritativeStrongboxSimulationRunner` |
| `AuthoritativeStrongboxSimulatorRuntimeV1` | `AuthoritativeStrongboxSimulatorLive` |
| `AuthoritySnapshotSaveComponentAdapterV1` | `StateSnapshotSaveComponentBridge` |
| `BalanceCountV1` | `BalanceCount` |
| `BalanceEquipmentObservationV1` | `BalanceEquipmentObservation` |
| `BalanceRejectionV1` | `BalanceRejection` |
| `BalanceRewardObservationV1` | `BalanceRewardObservation` |
| `BalanceSimulationIterationRequestV1` | `BalanceSimulationIterationRequest` |
| `BalanceSimulationIterationResultV1` | `BalanceSimulationIterationResult` |
| `BalanceSimulationModeV1` | `BalanceSimulationMode` |
| `BalanceSimulationReportV1` | `BalanceSimulationReport` |
| `BalanceSimulationRequestV1` | `BalanceSimulationRequest` |
| `BalanceSimulationServiceV1` | `BalanceSimulationActions` |
| `BalanceSimulationServiceV1Tests` | `BalanceSimulationActionsTests` |
| `BootstrapCompositionRoot` | `BootstrapSetupRoot` |
| `BootstrapCompositionRootTests` | `BootstrapSetupRootTests` |
| `BootstrapSceneAdapter` | `BootstrapSceneBridge` |
| `BoundCanonicalWeaponBlueprintResolverV1` | `BoundWeaponBlueprintResolver` |
| `BuiltInCharacterSelectionCatalogV1` | `BuiltInCharacterSelectionCatalog` |
| `BuiltInEnemyAttackPatternDamageChannelMapV1` | `BuiltInEnemyAttackPatternDamageChannelMap` |
| `BuiltInEnemyCatalogRegistryV1` | `BuiltInEnemyCatalogRegistry` |
| `BuiltInEnemyRuntimePolicyRegistryV1` | `BuiltInEnemyLivePolicyRegistry` |
| `BuiltInRoomContentObjectCatalogV1` | `BuiltInRoomContentObjectCatalog` |
| `CanonicalFieldV1` | `Field` |
| `CanonicalFirstPlayerHoldingsAuthorityV2` | `FirstPlayerHoldingsState` |
| `CanonicalFirstPlayerHoldingsAuthorityV2Tests` | `FirstPlayerHoldingsStateTests` |
| `CanonicalNodeCodecV1` | `NodeCodec` |
| `CanonicalNodeKindV1` | `NodeKind` |
| `CanonicalNodeV1` | `Node` |
| `CanonicalObjectReaderV1` | `ObjectReader` |
| `CanonicalPayloadExceptionV1` | `PayloadException` |
| `CanonicalPlayerWeaponSourceV2` | `PlayerWeaponSource` |
| `CanonicalProjectileLaunchEffect` | `ProjectileLaunchEffect` |
| `CanonicalProjectileSourceIdentity2D` | `ProjectileSourceIdentity2D` |
| `CanonicalReceiptFixture` | `ReceiptFixture` |
| `CanonicalUiOwnershipPlayModeTests` | `UiOwnershipPlayModeTests` |
| `CanonicalValueV1` | `Value` |
| `CanonicalWeaponEquipmentProjectionLookupV2` | `WeaponEquipmentViewLookup` |
| `CanonicalWeaponGameplayResolutionPlayModeTests` | `WeaponGameplayResolutionPlayModeTests` |
| `CanonicalWeaponInstanceLookupV2` | `WeaponInstanceLookup` |
| `CanonicalWeaponInventoryCardV2` | `WeaponInventoryCard` |
| `CanonicalWeaponInventoryMountV2` | `WeaponInventoryMount` |
| `CanonicalWeaponInventoryScreenServiceV2` | `WeaponInventoryScreenActions` |
| `CanonicalWeaponInventorySnapshotV2` | `WeaponInventorySnapshot` |
| `CanonicalWeaponOperationAvailabilityV1` | `WeaponOperationAvailability` |
| `CanonicalWeaponProjectileSourceIdentityTests` | `WeaponProjectileSourceIdentityTests` |
| `CanonicalWeaponSafetyPolicyV1` | `WeaponSafetyPolicy` |
| `CanonicalWriter` | `Writer` |
| `CatalogAdapter` | `CatalogBridge` |
| `CatalogDtoV1` | `CatalogDto` |
| `CharacterBaseStatProfileV1` | `CharacterBaseStatProfile` |
| `CharacterClassKindV1` | `CharacterClassKind` |
| `CharacterClassProfileDefinitionV1` | `CharacterClassProfileDefinition` |
| `CharacterCompositionCoordinatorV1` | `CharacterSetupFlow` |
| `CharacterCompositionCoordinatorV1Tests` | `CharacterSetupFlowTests` |
| `CharacterCompositionResultV1` | `CharacterSetupResult` |
| `CharacterCompositionStatusV1` | `CharacterSetupStatus` |
| `CharacterInstanceSnapshotV1` | `CharacterInstanceSnapshot` |
| `CharacterSaveRestoreBindingV1` | `CharacterSaveRestoreBinding` |
| `CharacterSelectControllerV1` | `CharacterSelectController` |
| `CharacterSelectControllerV1Tests` | `CharacterSelectControllerTests` |
| `CharacterSelectStageV1` | `CharacterSelectStage` |
| `CharacterSelectionCatalogResultV1` | `CharacterSelectionCatalogResult` |
| `CharacterSelectionCatalogStatusV1` | `CharacterSelectionCatalogStatus` |
| `CharacterSelectionCatalogV1` | `CharacterSelectionCatalog` |
| `CharacterSelectionDefinitionV1` | `CharacterSelectionDefinition` |
| `CharacterSelectionOperationResultV1` | `CharacterSelectionOperationResult` |
| `CharacterSelectionOperationStatusV1` | `CharacterSelectionOperationStatus` |
| `CharacterSelectionRecordingRouteSinkV1` | `CharacterSelectionRecordingRouteSink` |
| `CharacterSelectionRouteResultV1` | `CharacterSelectionRouteResult` |
| `CharacterSelectionRouteStatusV1` | `CharacterSelectionRouteStatus` |
| `CharacterSelectionServiceV1` | `CharacterSelectionActions` |
| `CharacterSelectionServiceV1Tests` | `CharacterSelectionActionsTests` |
| `CharacterSelectionSnapshotV1` | `CharacterSelectionSnapshot` |
| `CharacterVisualMetadataV1` | `CharacterVisualMetadata` |
| `CollectedRunRewardAtomicApplyResultV1` | `CollectedRunRewardAtomicApplyResult` |
| `CollectedRunRewardAtomicCoordinatorTests` | `CollectedRunRewardAtomicFlowTests` |
| `CollectedRunRewardAtomicPlanV2` | `CollectedRunRewardAtomicPlan` |
| `CollectedRunRewardGenerationContextV2` | `CollectedRunRewardGenerationContext` |
| `CollectedRunRewardPersistenceExpectationV1` | `CollectedRunRewardPersistenceExpectation` |
| `CollectedRunRewardPreparedTransferAuthorityV1` | `CollectedRunRewardPreparedTransferState` |
| `CollectedRunRewardPreparedTransferSaveComponentV1` | `CollectedRunRewardPreparedTransferSaveComponent` |
| `CollectedRunRewardPreparedTransferSnapshotV1` | `CollectedRunRewardPreparedTransferSnapshot` |
| `CollectedRunRewardPreparedTransferStateV1` | `CollectedRunRewardPreparedTransferState` |
| `CollectedRunRewardPreparedTransferV1` | `CollectedRunRewardPreparedTransfer` |
| `CollectedRunRewardTransferAuthorityStatusV1` | `CollectedRunRewardTransferStateStatus` |
| `CollectedRunRewardTransferBatchV1` | `CollectedRunRewardTransferBatch` |
| `CollectedRunRewardTransferCanonicalV1` | `CollectedRunRewardTransfer` |
| `CollectedRunRewardTransferCoordinatorV2` | `CollectedRunRewardTransferFlow` |
| `CollectedRunRewardTransferItemV1` | `CollectedRunRewardTransferItem` |
| `CollectedRunRewardTransferPersistenceResultV1` | `CollectedRunRewardTransferPersistenceResult` |
| `CollectedRunRewardTransferPersistenceStatusV1` | `CollectedRunRewardTransferPersistenceStatus` |
| `CollectedRunRewardTransferPreflightResultV1` | `CollectedRunRewardTransferPreflightResult` |
| `CollectedRunRewardTransferPreparationFactoryV2` | `CollectedRunRewardTransferPreparationFactory` |
| `CollectedRunRewardTransferReceiptAuthorityV1` | `CollectedRunRewardTransferReceiptState` |
| `CollectedRunRewardTransferReceiptRecordResultV1` | `CollectedRunRewardTransferReceiptRecordResult` |
| `CollectedRunRewardTransferReceiptSaveComponentV1` | `CollectedRunRewardTransferReceiptSaveComponent` |
| `CollectedRunRewardTransferReceiptSnapshotV1` | `CollectedRunRewardTransferReceiptSnapshot` |
| `CollectedRunRewardTransferReceiptV1` | `CollectedRunRewardTransferReceipt` |
| `CollectedRunRewardTransferRestoreResultV1` | `CollectedRunRewardTransferRestoreResult` |
| `CollectedRunRewardTransferResultV1` | `CollectedRunRewardTransferResult` |
| `CollectedRunRewardTransferResultsProjectionV1` | `CollectedRunRewardTransferResultsView` |
| `CollectedRunRewardTransferStatusV1` | `CollectedRunRewardTransferStatus` |
| `CombatActorSnapshotFactoryV1` | `CombatActorSnapshotFactory` |
| `CombatActorSnapshotV1` | `CombatActorSnapshot` |
| `CombatDeathVfxFactoryV1Tests` | `CombatDeathVfxFactoryTests` |
| `CombatEffectGeometryKindV1` | `CombatEffectGeometryKind` |
| `CombatEffectSnapshotV1` | `CombatEffectSnapshot` |
| `CombatHealthBarRefreshStatusV1` | `CombatHealthBarRefreshStatus` |
| `CombatHealthBarSnapshotV1` | `CombatHealthBarSnapshot` |
| `CombatHealthPresentationStateV1` | `CombatHealthPresentationState` |
| `CombatHit2DAdapter` | `CombatHit2DBridge` |
| `CombatHitCapabilityIdsV1` | `CombatHitCapabilityIds` |
| `CombatHitContactKindV1` | `CombatHitContactKind` |
| `CombatHitContactV1` | `CombatHitContact` |
| `CombatHitDamageCommandAdapterV1` | `CombatHitDamageCommandBridge` |
| `CombatHitDispositionV1` | `CombatHitDisposition` |
| `CombatHitHistorySnapshotV1` | `CombatHitHistorySnapshot` |
| `CombatHitPolicyDefinitionV1` | `CombatHitPolicyDefinition` |
| `CombatHitPolicyIdsV1` | `CombatHitPolicyIds` |
| `CombatHitPolicyInputV1` | `CombatHitPolicyInput` |
| `CombatHitPolicyRegistryV1` | `CombatHitPolicyRegistry` |
| `CombatHitPolicyResultV1` | `CombatHitPolicyResult` |
| `CombatHitPolicyV1` | `CombatHitPolicy` |
| `CombatHitPolicyV1Tests` | `CombatHitPolicyTests` |
| `CombatHitPropDamageCommandAdapterV1` | `CombatHitPropDamageCommandBridge` |
| `CombatHitRejectionCodeV1` | `CombatHitRejectionCode` |
| `CombatHitTargetCountV1` | `CombatHitTargetCount` |
| `CombatPresentationAnchorFactsV1` | `CombatPresentationAnchorFacts` |
| `CombatPresentationEnemyActorAuthority2D` | `CombatPresentationEnemyActorState2D` |
| `CombatPresentationV1Tests` | `CombatPresentationTests` |
| `CombatRelationRuleV1` | `CombatRelationRule` |
| `CombatWorldBlockerBehaviorV1` | `CombatWorldBlockerBehavior` |
| `ConditionEffectRuntimeDefinitionV1` | `ConditionEffectLiveDefinition` |
| `ConditionFactIngestionResultV1` | `ConditionFactIngestionResult` |
| `ConditionFactIngestionStatusV1` | `ConditionFactIngestionStatus` |
| `ConditionObservedGameplayFactV1` | `ConditionObservedGameplayFact` |
| `ConditionOwnedStatusEffectRunPortV1` | `ConditionOwnedStatusEffectRunPort` |
| `ConditionParticipantSnapshotV1` | `ConditionParticipantSnapshot` |
| `ConditionRunDefinitionV1` | `ConditionRunDefinition` |
| `ConditionRunLifecycleSnapshotV1` | `ConditionRunLifecycleSnapshot` |
| `ConditionRunReconstructionCommandV1` | `ConditionRunReconstructionCommand` |
| `ConditionRunReconstructionResultV1` | `ConditionRunReconstructionResult` |
| `ConditionRuntimeAuthorityV1` | `ConditionLiveState` |
| `ConditionRuntimeAuthorityV1Tests` | `ConditionLiveStateTests` |
| `ConditionRuntimeFactTypeIdsV1` | `ConditionLiveFactTypeIds` |
| `ConditionRuntimeHashV1` | `ConditionLiveHash` |
| `ConditionRuntimeParticipantDefinitionV1` | `ConditionLiveParticipantDefinition` |
| `ConditionRuntimeSnapshotV1` | `ConditionLiveSnapshot` |
| `ConditionSourceFactFingerprintV1` | `ConditionSourceFactFingerprint` |
| `ContextResolvedEnemyDeathTerminalDropFactAdapterV1` | `ContextResolvedEnemyDeathTerminalDropFactBridge` |
| `ControllerFakeAuthority` | `ControllerFakeState` |
| `CoordinatorFixture` | `FlowFixture` |
| `CountingChildAuthority` | `CountingChildState` |
| `CountingHoldingsAuthority` | `CountingHoldingsState` |
| `CraftAndEquipCommandV1` | `CraftAndEquipCommand` |
| `CraftEquipmentCommandV1` | `CraftEquipmentCommand` |
| `CraftedEquipmentEquipCommandV1` | `CraftedEquipmentEquipCommand` |
| `CraftedEquipmentEquipResultV1` | `CraftedEquipmentEquipResult` |
| `CraftedEquipmentEquipStatusV1` | `CraftedEquipmentEquipStatus` |
| `CraftingCanonicalV1` | `Crafting` |
| `CraftingDelayVarianceV1` | `CraftingDelayVariance` |
| `CraftingGeneratorPolicyV1` | `CraftingGeneratorPolicy` |
| `CraftingIntegrationIdentityV1` | `CraftingIntegrationIdentity` |
| `CraftingInventoryEquipResultV1` | `CraftingInventoryEquipResult` |
| `CraftingInventoryEquipServiceV1` | `CraftingInventoryEquipActions` |
| `CraftingInventoryEquipServiceV1Tests` | `CraftingInventoryEquipActionsTests` |
| `CraftingInventoryEquipStatusV1` | `CraftingInventoryEquipStatus` |
| `CraftingPresentationAuthorityResultV1` | `CraftingPresentationStateResult` |
| `CraftingPresentationAuthoritySnapshotV1` | `CraftingPresentationStateSnapshot` |
| `CraftingQualityPolicyKindV1` | `CraftingQualityPolicyKind` |
| `CraftingRecipeAssetBuildResultV1` | `CraftingRecipeAssetBuildResult` |
| `CraftingRecipeAvailabilityV1` | `CraftingRecipeAvailability` |
| `CraftingRecipeCatalogV1` | `CraftingRecipeCatalog` |
| `CraftingRecipeDefinitionAssetV1` | `CraftingRecipeDefinitionAsset` |
| `CraftingRecipeProjectionV1` | `CraftingRecipeView` |
| `CraftingRecipeV1` | `CraftingRecipe` |
| `CraftingResultStatusV1` | `CraftingResultStatus` |
| `CraftingResultV1` | `CraftingResult` |
| `CraftingRewardApplicationFactoryV1` | `CraftingRewardApplicationFactory` |
| `CraftingScrapSpendRewardChildAuthorityV1` | `CraftingScrapSpendRewardChildState` |
| `CraftingScreenControllerV1` | `CraftingScreenController` |
| `CraftingScreenResultV1` | `CraftingScreenResult` |
| `CraftingScreenServiceV1` | `CraftingScreenActions` |
| `CraftingScreenSnapshotV1` | `CraftingScreenSnapshot` |
| `CraftingScreenStatusV1` | `CraftingScreenStatus` |
| `CraftingServicePresentationAuthorityPortV1` | `CraftingActionsPresentationStatePort` |
| `CraftingServiceV1` | `CraftingActions` |
| `CraftingServiceV1Tests` | `CraftingActionsTests` |
| `CraftingUnusedMoneyRewardChildAuthorityV1` | `CraftingUnusedMoneyRewardChildState` |
| `CraftingWeightedDefinitionAuthoringV1` | `CraftingWeightedDefinitionAuthoring` |
| `CraftingWeightedDefinitionV1` | `CraftingWeightedDefinition` |
| `CriticalHitDamageCommandAdapterV1` | `CriticalHitDamageCommandBridge` |
| `CriticalHitEffectFactsV1` | `CriticalHitEffectFacts` |
| `CriticalHitFingerprintV1` | `CriticalHitFingerprint` |
| `CriticalHitOrdinalV1Tests` | `CriticalHitOrdinalTests` |
| `CriticalHitPolicyApplicationV1` | `CriticalHitPolicyApplication` |
| `CriticalHitPolicyDefinitionV1` | `CriticalHitPolicyDefinition` |
| `CriticalHitPolicyIdsV1` | `CriticalHitPolicyIds` |
| `CriticalHitPolicyRegistryV1` | `CriticalHitPolicyRegistry` |
| `CriticalHitRejectionCodeV1` | `CriticalHitRejectionCode` |
| `CriticalHitResolutionAuthorityV1` | `CriticalHitResolutionState` |
| `CriticalHitResolutionCommandV1` | `CriticalHitResolutionCommand` |
| `CriticalHitResolutionResultV1` | `CriticalHitResolutionResult` |
| `CriticalHitResolutionStatusV1` | `CriticalHitResolutionStatus` |
| `CriticalHitResolutionV1Tests` | `CriticalHitResolutionTests` |
| `CriticalHitResolvedDamageV1` | `CriticalHitResolvedDamage` |
| `CriticalHitRollDomainV1` | `CriticalHitRollDomain` |
| `DecisionMovementRuntimePolicyV1` | `DecisionMovementLivePolicy` |
| `DecorScaffoldDtoV2` | `DecorScaffoldDto` |
| `DefaultDerivedCharacterStatComposerV1` | `DefaultDerivedCharacterStatComposer` |
| `DefaultTerminalRewardEnvironmentResolverV1` | `DefaultTerminalRewardEnvironmentResolver` |
| `DefinitionDtoV1` | `DefinitionDto` |
| `DelegateSkillsScreenNavigationPortV1` | `DelegateSkillsScreenNavigationPort` |
| `DelegatedConditionalFactRunPortV1` | `DelegatedConditionalFactRunPort` |
| `DelegatedRoomRunPortV1` | `DelegatedRoomRunPort` |
| `DelegatedRunLifecyclePortV1` | `DelegatedRunLifecyclePort` |
| `DerivedCharacterStatInputV1` | `DerivedCharacterStatInput` |
| `DerivedCharacterStatsSnapshotV1` | `DerivedCharacterStatsSnapshot` |
| `DerivedCharacterStatsV1Tests` | `DerivedCharacterStatsTests` |
| `DerivedStatFingerprintV1` | `DerivedStatFingerprint` |
| `DerivedStatModifierSourceV1` | `DerivedStatModifierSource` |
| `DerivedStatPolicyV1` | `DerivedStatPolicy` |
| `DerivedStatRuleV1` | `DerivedStatRule` |
| `DerivedStatSourcePrioritiesV1` | `DerivedStatSourcePriorities` |
| `DerivedStatTargetIdsV1` | `DerivedStatTargetIds` |
| `DestructiblePropAuthority` | `DestructiblePropState` |
| `DestructiblePropAuthorityTests` | `DestructiblePropStateTests` |
| `DestructiblePropTerminalProvenanceV1` | `DestructiblePropTerminalProvenance` |
| `DeterministicAuthority` | `DeterministicState` |
| `DeterministicEnemyRuntimeIdentityDeriverV1` | `DeterministicEnemyLiveIdentityDeriver` |
| `DeterministicProofRewardOverrideResolverV1` | `DeterministicProofRewardOverrideResolver` |
| `DeterministicStrongboxGrantPayloadResolverV1` | `DeterministicStrongboxGrantPayloadResolver` |
| `DevelopmentPickupAuthorityFixtureV1` | `DevelopmentPickupStateFixture` |
| `DevelopmentPickupCollectionResultV1` | `DevelopmentPickupCollectionResult` |
| `DirectEnemyMovementIntentRealizerV1` | `DirectEnemyMovementIntentRealizer` |
| `DispelStatusEffectsCommandV1` | `DispelStatusEffectsCommand` |
| `DoorConditionComposition` | `DoorConditionSetup` |
| `DoorDtoV2` | `DoorDto` |
| `DoorRuntimeState` | `DoorLiveState` |
| `DoorsDtoV2` | `DoorsDto` |
| `DropSaturationBandV1` | `DropSaturationBand` |
| `DropSourceSimulationRequestV1` | `DropSourceSimulationRequest` |
| `DropSourceSimulationRuntimeV1` | `DropSourceSimulationLive` |
| `EconomyResourceKindV1` | `EconomyResourceKind` |
| `EconomyTransactionCommandV1` | `EconomyTransactionCommand` |
| `EconomyTransactionIdentityComparisonV1` | `EconomyTransactionIdentityComparison` |
| `EconomyTransactionIdentityV1` | `EconomyTransactionIdentity` |
| `EconomyTransactionOperationV1` | `EconomyTransactionOperation` |
| `EconomyTransactionResultV1` | `EconomyTransactionResult` |
| `EconomyTransactionStatusV1` | `EconomyTransactionStatus` |
| `EmptyActiveAbilityRunPortV1` | `EmptyActiveAbilityRunPort` |
| `EmptyTerminalRewardOverrideResolverV1` | `EmptyTerminalRewardOverrideResolver` |
| `EncounterRuntimeIdentity` | `EncounterLiveIdentity` |
| `EncounterScaffoldDtoV2` | `EncounterScaffoldDto` |
| `EndMissionRunCommandV1` | `EndMissionRunCommand` |
| `EndRunSessionCommandV1` | `EndRunSessionCommand` |
| `EndpointDtoV2` | `EndpointDto` |
| `EnemiesScaffoldDtoV2` | `EnemiesScaffoldDto` |
| `EnemyActor2DAdapter` | `EnemyActor2DBridge` |
| `EnemyActor2DAdapterTests` | `EnemyActor2DBridgeTests` |
| `EnemyActorCombatHealthSnapshotSourceV1` | `EnemyActorCombatHealthSnapshotSource` |
| `EnemyAimCommitmentModeV1` | `EnemyAimCommitmentMode` |
| `EnemyAreaAttackParametersV1` | `EnemyAreaAttackParameters` |
| `EnemyAreaPayloadV1` | `EnemyAreaPayload` |
| `EnemyAttackCapabilityConfigurationV1` | `EnemyAttackCapabilityConfiguration` |
| `EnemyAttackCapabilityDescriptorV1` | `EnemyAttackCapabilityDescriptor` |
| `EnemyAttackCapabilityRegistrationV1` | `EnemyAttackCapabilityRegistration` |
| `EnemyAttackCapabilityRuntimeRegistrationV1` | `EnemyAttackCapabilityLiveRegistration` |
| `EnemyAttackDescriptorCompatibilityV1` | `EnemyAttackDescriptorCompatibility` |
| `EnemyAttackEffectEmissionDispatchV1` | `EnemyAttackEffectEmissionDispatch` |
| `EnemyAttackEffectEmissionKindV1` | `EnemyAttackEffectEmissionKind` |
| `EnemyAttackEffectEmissionProjectorV1` | `EnemyAttackEffectEmissionProjector` |
| `EnemyAttackEffectEmissionV1` | `EnemyAttackEffectEmission` |
| `EnemyAttackExecutionContextV1` | `EnemyAttackExecutionContext` |
| `EnemyAttackExecutionKindV1` | `EnemyAttackExecutionKind` |
| `EnemyAttackExecutionRequestV1` | `EnemyAttackExecutionRequest` |
| `EnemyAttackExecutionResultV1` | `EnemyAttackExecutionResult` |
| `EnemyAttackInterruptionPolicyV1` | `EnemyAttackInterruptionPolicy` |
| `EnemyAttackLifecycleCancellationCommandV1` | `EnemyAttackLifecycleCancellationCommand` |
| `EnemyAttackParameterKindsV1` | `EnemyAttackParameterKinds` |
| `EnemyAttackPatternAuthorityV1` | `EnemyAttackPatternState` |
| `EnemyAttackPatternAuthorityV1Tests` | `EnemyAttackPatternStateTests` |
| `EnemyAttackPatternCancellationResultV1` | `EnemyAttackPatternCancellationResult` |
| `EnemyAttackPatternDispatchRejectionCodeV1` | `EnemyAttackPatternDispatchRejectionCode` |
| `EnemyAttackPatternDispatchResultV1` | `EnemyAttackPatternDispatchResult` |
| `EnemyAttackPatternEmissionFingerprintV1Tests` | `EnemyAttackPatternEmissionFingerprintTests` |
| `EnemyAttackPatternFingerprintV1` | `EnemyAttackPatternFingerprint` |
| `EnemyAttackPatternHitRouteResultV1` | `EnemyAttackPatternHitRouteResult` |
| `EnemyAttackPatternHitRouteStatusV1` | `EnemyAttackPatternHitRouteStatus` |
| `EnemyAttackPatternHitRouterV1` | `EnemyAttackPatternHitRouter` |
| `EnemyAttackPatternHitRouterV1Tests` | `EnemyAttackPatternHitRouterTests` |
| `EnemyAttackPatternLiveIntegrationV1Tests` | `EnemyAttackPatternLiveIntegrationTests` |
| `EnemyAttackPatternLiveRecordV1` | `EnemyAttackPatternLiveRecord` |
| `EnemyAttackPatternLiveSchedulerV1` | `EnemyAttackPatternLiveScheduler` |
| `EnemyAttackPatternLiveStateV1` | `EnemyAttackPatternLiveState` |
| `EnemyAttackPatternOperationStatusV1` | `EnemyAttackPatternOperationStatus` |
| `EnemyAttackPatternProductionController2D` | `EnemyAttackPatternController2D` |
| `EnemyAttackPatternProjectilePrefabRegistryV1` | `EnemyAttackPatternProjectilePrefabRegistry` |
| `EnemyAttackPatternRealizationResultV1` | `EnemyAttackPatternRealizationResult` |
| `EnemyAttackPatternRealizationStatusV1` | `EnemyAttackPatternRealizationStatus` |
| `EnemyAttackPatternRejectionCodeV1` | `EnemyAttackPatternRejectionCode` |
| `EnemyAttackPatternSchedulerV1` | `EnemyAttackPatternScheduler` |
| `EnemyAttackPatternStartResultV1` | `EnemyAttackPatternStartResult` |
| `EnemyAttackPatternTargetBindingV1` | `EnemyAttackPatternTargetBinding` |
| `EnemyAttackPatternTransactionalRealizerV1` | `EnemyAttackPatternTransactionalRealizer` |
| `EnemyAttackPatternUnityEmissionRealizerV1` | `EnemyAttackPatternUnityEmissionRealizer` |
| `EnemyAttackPatternUnitySourceBindingV1` | `EnemyAttackPatternUnitySourceBinding` |
| `EnemyAttackPatternUnitySourceRegistryV1` | `EnemyAttackPatternUnitySourceRegistry` |
| `EnemyAttackPortV1` | `EnemyAttackPort` |
| `EnemyAttackPresentationPlanV1` | `EnemyAttackPresentationPlan` |
| `EnemyAttackPresentationProjection2D` | `EnemyAttackPresentationView2D` |
| `EnemyAttackPresentationPulseV1` | `EnemyAttackPresentationPulse` |
| `EnemyAttackScheduledMeleeStrikeV1` | `EnemyAttackScheduledMeleeStrike` |
| `EnemyAttackScheduledProjectileV1` | `EnemyAttackScheduledProjectile` |
| `EnemyAttackScheduledShotV1` | `EnemyAttackScheduledShot` |
| `EnemyAttackSequenceCancellationFactV1` | `EnemyAttackSequenceCancellationFact` |
| `EnemyAttackSequenceDispatchV1` | `EnemyAttackSequenceDispatch` |
| `EnemyAttackSequenceIdentityV1` | `EnemyAttackSequenceIdentity` |
| `EnemyAttackSequenceV1` | `EnemyAttackSequence` |
| `EnemyCatalogFingerprintV1` | `EnemyCatalogFingerprint` |
| `EnemyCatalogImportResultV1` | `EnemyCatalogImportResult` |
| `EnemyCatalogIssueV1` | `EnemyCatalogIssue` |
| `EnemyCatalogJsonImporterV1` | `EnemyCatalogJsonImporter` |
| `EnemyCatalogJsonImporterV1Tests` | `EnemyCatalogJsonImporterTests` |
| `EnemyCatalogMappingExceptionV1` | `EnemyCatalogMappingException` |
| `EnemyCatalogRegistryV1` | `EnemyCatalogRegistry` |
| `EnemyCatalogRoomClearRoleV1` | `EnemyCatalogRoomClearRole` |
| `EnemyCatalogV1` | `EnemyCatalog` |
| `EnemyCatalogValidationResultV1` | `EnemyCatalogValidationResult` |
| `EnemyCatalogValidatorV1` | `EnemyCatalogValidator` |
| `EnemyCommittedAttackPatternExecutorV1` | `EnemyCommittedAttackPatternExecutor` |
| `EnemyCommittedAttackPatternResultV1` | `EnemyCommittedAttackPatternResult` |
| `EnemyCommittedAttackPatternStatusV1` | `EnemyCommittedAttackPatternStatus` |
| `EnemyContact2DAdapter` | `EnemyContact2DBridge` |
| `EnemyDeathConditionFactAdapterV1` | `EnemyDeathConditionFactBridge` |
| `EnemyDeathFactV1` | `EnemyDeathFact` |
| `EnemyDeathTerminalDropDefinitionProjectionResultV1` | `EnemyDeathTerminalDropDefinitionViewResult` |
| `EnemyDeathTerminalDropDefinitionProjectionV1` | `EnemyDeathTerminalDropDefinitionView` |
| `EnemyDeathTerminalDropDefinitionProjectorV1` | `EnemyDeathTerminalDropDefinitionProjector` |
| `EnemyDeathTerminalDropFactAdapterV1` | `EnemyDeathTerminalDropFactBridge` |
| `EnemyDeathVfxPresentationStatusV1` | `EnemyDeathVfxPresentationStatus` |
| `EnemyDeathVfxScaleConfigurationV1` | `EnemyDeathVfxScaleConfiguration` |
| `EnemyDecisionPolicyConfigurationV1` | `EnemyDecisionPolicyConfiguration` |
| `EnemyDecisionPolicyRegistrationV1` | `EnemyDecisionPolicyRegistration` |
| `EnemyDefinitionProjection` | `EnemyDefinitionView` |
| `EnemyDefinitionV1` | `EnemyDefinition` |
| `EnemyDifficultyContextV1` | `EnemyDifficultyContext` |
| `EnemyDifficultyRuntimeRegistrationV1` | `EnemyDifficultyLiveRegistration` |
| `EnemyDifficultyScalingConfigurationV1` | `EnemyDifficultyScalingConfiguration` |
| `EnemyDifficultyScalingV1` | `EnemyDifficultyScaling` |
| `EnemyExperienceRewardBandAuthoringV1` | `EnemyExperienceRewardBandAuthoring` |
| `EnemyExperienceRewardBandV1` | `EnemyExperienceRewardBand` |
| `EnemyExperienceRewardCatalogAssetV1` | `EnemyExperienceRewardCatalogAsset` |
| `EnemyExperienceRewardCatalogV1` | `EnemyExperienceRewardCatalog` |
| `EnemyExperienceRewardDefinitionV1` | `EnemyExperienceRewardDefinition` |
| `EnemyExperienceRewardFactV1` | `EnemyExperienceRewardFact` |
| `EnemyExperienceRewardIdsV1` | `EnemyExperienceRewardIds` |
| `EnemyExperienceRewardOperationIdentityV1` | `EnemyExperienceRewardOperationIdentity` |
| `EnemyExperienceRewardServiceV1` | `EnemyExperienceRewardActions` |
| `EnemyExperienceRewardStatusV1` | `EnemyExperienceRewardStatus` |
| `EnemyExperienceRewardingAuthorityTests` | `EnemyExperienceRewardingStateTests` |
| `EnemyExperienceRewardingAuthorityV1` | `EnemyExperienceRewardingState` |
| `EnemyHitSubscriptionSetV1` | `EnemyHitSubscriptionSet` |
| `EnemyHitV1` | `EnemyHit` |
| `EnemyLevelScalingProfileV1` | `EnemyLevelScalingProfile` |
| `EnemyMeleeAimCommitPolicyV1` | `EnemyMeleeAimCommitPolicy` |
| `EnemyMeleeAttackParametersV1` | `EnemyMeleeAttackParameters` |
| `EnemyMeleePatternV1` | `EnemyMeleePattern` |
| `EnemyMeleeTerminalOnImpactPolicyV1` | `EnemyMeleeTerminalOnImpactPolicy` |
| `EnemyMovementPolicyConfigurationV1` | `EnemyMovementPolicyConfiguration` |
| `EnemyMovementPolicyIntentV1` | `EnemyMovementPolicyIntent` |
| `EnemyMovementPolicyRegistrationV1` | `EnemyMovementPolicyRegistration` |
| `EnemyMovementRealizationContextV1` | `EnemyMovementRealizationContext` |
| `EnemyMovementRealizationV1` | `EnemyMovementRealization` |
| `EnemyPerceptionPolicyConfigurationV1` | `EnemyPerceptionPolicyConfiguration` |
| `EnemyPerceptionRuntimeRegistrationV1` | `EnemyPerceptionLiveRegistration` |
| `EnemyPlacementDecisionV1` | `EnemyPlacementDecision` |
| `EnemyPlacementRuntimeFactoryRejectionV1` | `EnemyPlacementLiveFactoryRejection` |
| `EnemyPlacementRuntimeFactoryResultV1` | `EnemyPlacementLiveFactoryResult` |
| `EnemyPlacementRuntimeFactoryV1` | `EnemyPlacementLiveFactory` |
| `EnemyPlacementRuntimeFactoryV1Tests` | `EnemyPlacementLiveFactoryTests` |
| `EnemyPlacementRuntimeFactoryV1Tests_AuthorityBoundaries` | `EnemyPlacementLiveFactoryTestsStateBoundaries` |
| `EnemyPlacementRuntimeFactoryV1Tests_LifecycleRouting` | `EnemyPlacementLiveFactoryTestsLifecycleRouting` |
| `EnemyPlacementRuntimeInstanceV1` | `EnemyPlacementLiveInstance` |
| `EnemyPlacementRuntimeRequestV1` | `EnemyPlacementLiveRequest` |
| `EnemyPlayerDamageChannelMapV1` | `EnemyPlayerDamageChannelMap` |
| `EnemyPlayerDamageIntegrationInstallerV1` | `EnemyPlayerDamageIntegrationInstaller` |
| `EnemyPlayerDamageIntegrationV1` | `EnemyPlayerDamageIntegration` |
| `EnemyPlayerDamagePortResultV1` | `EnemyPlayerDamagePortResult` |
| `EnemyPlayerDamageRequestV1` | `EnemyPlayerDamageRequest` |
| `EnemyPresentationAdapter2D` | `EnemyPresentationBridge2D` |
| `EnemyProjectileAttackParametersV1` | `EnemyProjectileAttackParameters` |
| `EnemyProjectilePayloadV1` | `EnemyProjectilePayload` |
| `EnemyPublisherReconciliationV1` | `EnemyPublisherReconciliation` |
| `EnemyPublisherResolutionStatusV1` | `EnemyPublisherResolutionStatus` |
| `EnemyReadinessWindowV1` | `EnemyReadinessWindow` |
| `EnemyRoomPlacementCompositionResultV1` | `EnemyRoomPlacementSetupResult` |
| `EnemyRuntimeAttackBindingV1` | `EnemyLiveAttackBinding` |
| `EnemyRuntimeAuthorityFingerprintV1` | `EnemyLiveStateFingerprint` |
| `EnemyRuntimeCombatHealthSnapshotSourceV1` | `EnemyLiveCombatHealthSnapshotSource` |
| `EnemyRuntimeDamageCommandV1` | `EnemyLiveDamageCommand` |
| `EnemyRuntimeDamageResultV1` | `EnemyLiveDamageResult` |
| `EnemyRuntimeDownstreamPortsV1` | `EnemyLiveDownstreamPorts` |
| `EnemyRuntimeFoundationTests` | `EnemyLiveFoundationTests` |
| `EnemyRuntimeIdentityV1` | `EnemyLiveIdentity` |
| `EnemyRuntimeOperationStatusV1` | `EnemyLiveOperationStatus` |
| `EnemyRuntimePolicyRegistryV1` | `EnemyLivePolicyRegistry` |
| `EnemyRuntimeProjection` | `EnemyLiveView` |
| `EnemyRuntimeRejectionCodeV1` | `EnemyLiveRejectionCode` |
| `EnemySequenceAimPolicyV1` | `EnemySequenceAimPolicy` |
| `EnemyShootingPatternV1` | `EnemyShootingPattern` |
| `EnemyTarget2DAdapter` | `EnemyTarget2DBridge` |
| `EnemyTargetingAimContextV1` | `EnemyTargetingAimContext` |
| `EnemyTargetingAimPolicyConfigurationV1` | `EnemyTargetingAimPolicyConfiguration` |
| `EnemyTargetingAimPolicyRegistrationV1` | `EnemyTargetingAimPolicyRegistration` |
| `EnemyTerminalCollisionFactV1` | `EnemyTerminalCollisionFact` |
| `EnemyTerminalDropFactConsumerV1` | `EnemyTerminalDropFactConsumer` |
| `EnemyTerminalPresentationFactProjectorV1` | `EnemyTerminalPresentationFactProjector` |
| `EnemyTerminalPresentationFactV1` | `EnemyTerminalPresentationFact` |
| `EnemyTerminalSourceContextAdapterV1Tests` | `EnemyTerminalSourceContextBridgeTests` |
| `EnemyTerminalSourceContextV1` | `EnemyTerminalSourceContext` |
| `EquipmentGenerationCandidateV1` | `EquipmentGenerationCandidate` |
| `EquipmentGenerationPolicyV1` | `EquipmentGenerationPolicy` |
| `EquipmentGenerationRequestV1` | `EquipmentGenerationRequest` |
| `EquipmentGenerationResultV1` | `EquipmentGenerationResult` |
| `EquipmentQualityCandidateV1` | `EquipmentQualityCandidate` |
| `EventActivationWindowV1` | `EventActivationWindow` |
| `EventModifierCanonicalV1` | `EventModifier` |
| `EventModifierDescriptorV1` | `EventModifierDescriptor` |
| `EventModifierTargetIdsV1` | `EventModifierTargetIds` |
| `EventProjectionCanonicalV1` | `EventView` |
| `EventStampedCommandEnvelopeV1` | `EventStampedCommandEnvelope` |
| `EventStampedCommandEnvelopeV1Tests` | `EventStampedCommandEnvelopeTests` |
| `EventStampedCommandKindV1` | `EventStampedCommandKind` |
| `ExactCanonicalBlueprintResolver` | `ExactBlueprintResolver` |
| `ExactInstanceLoadoutComponentCodecV1` | `ExactInstanceLoadoutComponentCodec` |
| `ExactRunEnemySourceContextResolverV1` | `ExactRunEnemySourceContextResolver` |
| `ExactStrongboxSelectionV1` | `ExactStrongboxSelection` |
| `ExclusiveRewardGroupV1` | `ExclusiveRewardGroup` |
| `ExistingConditionRuntimeRunPortV1` | `ExistingConditionLiveRunPort` |
| `ExistingMissionResultRunPortV1` | `ExistingMissionResultRunPort` |
| `ExistingPlayerRuntimeRunPortV1` | `ExistingPlayerLiveRunPort` |
| `ExistingRewardGenerationExecutorV1` | `ExistingRewardGenerationExecutor` |
| `ExistingRunSessionPickupPortV1` | `ExistingRunSessionPickupPort` |
| `ExistingStatusEffectRunPortV1` | `ExistingStatusEffectRunPort` |
| `ExistingStrongboxMissionResultApplicationAuthorityPortV1` | `ExistingStrongboxMissionResultApplicationStatePort` |
| `ExistingStrongboxOpeningRecoveryPortV1` | `ExistingStrongboxOpeningRecoveryPort` |
| `ExistingWeaponExecutionRunPortV1` | `ExistingWeaponExecutionRunPort` |
| `ExplicitCodecGoldenV1Tests` | `ExplicitCodecGoldenTests` |
| `ExplicitCodecValuesV1` | `ExplicitCodecValues` |
| `ExplicitNoOpExperienceConsumerV1` | `ExplicitNoOpExperienceConsumer` |
| `ExplicitNoOpKillStatisticsConsumerV1` | `ExplicitNoOpKillStatisticsConsumer` |
| `ExplicitSaveComponentCodecV1` | `ExplicitSaveComponentCodec` |
| `ExtensibilityGuardrailsV1Tests` | `ExtensibilityGuardrailsTests` |
| `FactWindowConditionAuthorityV1` | `FactWindowConditionState` |
| `FactWindowConditionDefinitionV1` | `FactWindowConditionDefinition` |
| `FactWindowEffectFixtureV1` | `FactWindowEffectFixture` |
| `FactWindowStatusEffectBindingV1` | `FactWindowStatusEffectBinding` |
| `FactWindowStatusEffectBridgeV1` | `FactWindowStatusEffectBridge` |
| `FailOnceApplyAuthority` | `FailOnceApplyState` |
| `FakeAtomicAuthority` | `FakeAtomicState` |
| `FakeCollectedRewardAuthority` | `FakeCollectedRewardState` |
| `FakeCraftingAuthority` | `FakeCraftingState` |
| `FakeEnemyAuthority2D` | `FakeEnemyState2D` |
| `FakeExistingAuthorityPort` | `FakeExistingStatePort` |
| `FakeMovementContactAuthority` | `FakeMovementContactState` |
| `FakeProjectionReader` | `FakeViewReader` |
| `FakeRunCoordinator` | `FakeRunFlow` |
| `FakeRuntime` | `FakeLive` |
| `FakeRuntimeBundle` | `FakeLiveBundle` |
| `FakeRuntimePort` | `FakeLivePort` |
| `FixedClockV1` | `FixedClock` |
| `FixedPickupSourcePositionResolverV1` | `FixedPickupSourcePositionResolver` |
| `FixtureAdapter` | `FixtureBridge` |
| `FloorScaffoldDtoV2` | `FloorScaffoldDto` |
| `FoundationEnemyDecisionRuntimePolicyV1` | `FoundationEnemyDecisionLivePolicy` |
| `FrozenCharacterRunInputsV1` | `FrozenCharacterRunInputs` |
| `FrozenEventModifierContextV1` | `FrozenEventModifierContext` |
| `FrozenRunEquipmentV1` | `FrozenRunEquipment` |
| `FrozenRunProgressionContextProviderV1` | `FrozenRunProgressionContextProvider` |
| `GameplayDropOperationFactoryV1` | `GameplayDropOperationFactory` |
| `GameplayDropOperationV1` | `GameplayDropOperation` |
| `GameplayDropOperationV1Tests` | `GameplayDropOperationTests` |
| `GameplayDropOverrideModeV1` | `GameplayDropOverrideMode` |
| `GameplayDropOverrideV1` | `GameplayDropOverride` |
| `GeneratedAugmentSignaturePlayerHoldingsRewardChildAuthorityV1` | `GeneratedAugmentSignaturePlayerHoldingsRewardChildState` |
| `GeneratedEquipmentAugmentSignatureAuthorityV1` | `GeneratedEquipmentAugmentSignatureState` |
| `GeneratedEquipmentAugmentSignatureComponentCodecV1` | `GeneratedEquipmentAugmentSignatureComponentCodec` |
| `GeneratedEquipmentAugmentSignatureRecordResultV1` | `GeneratedEquipmentAugmentSignatureRecordResult` |
| `GeneratedEquipmentAugmentSignatureRecordStatusV1` | `GeneratedEquipmentAugmentSignatureRecordStatus` |
| `GeneratedEquipmentAugmentSignatureSaveComponentV1` | `GeneratedEquipmentAugmentSignatureSaveComponent` |
| `GeneratedEquipmentAugmentSignatureSnapshotV1` | `GeneratedEquipmentAugmentSignatureSnapshot` |
| `GeneratedEquipmentAugmentSignatureV1` | `GeneratedEquipmentAugmentSignature` |
| `GeneratedTerminalDropResultV1` | `GeneratedTerminalDropResult` |
| `GeneratedTerminalDropRewardV1` | `GeneratedTerminalDropReward` |
| `HoldingProvenanceV1` | `HoldingProvenance` |
| `HoldingsCanonicalV1` | `Holdings` |
| `HoldingsEntryTypeIdsV1` | `HoldingsEntryTypeIds` |
| `HoldingsLedgerVocabularyV1` | `HoldingsLedgerVocabulary` |
| `HubFlowControllerV1` | `HubFlowController` |
| `HubNavigationResultV1` | `HubNavigationResult` |
| `HubNavigationServiceV1` | `HubNavigationActions` |
| `HubNavigationSnapshotV1` | `HubNavigationSnapshot` |
| `HubNavigationStatusV1` | `HubNavigationStatus` |
| `HubRoutePlaceholderAdapterV1` | `HubRoutePlaceholderBridge` |
| `HubRouteRecordV1` | `HubRouteRecord` |
| `HubRouteV1` | `HubRoute` |
| `IAcceptedGameplayFactAdapterV1` | `IAcceptedGameplayFactBridge` |
| `IAcceptedGameplayFactSourceFingerprintV1` | `IAcceptedGameplayFactSourceFingerprint` |
| `IAtomicSaveFilePortV1` | `IAtomicSaveFilePort` |
| `IAuthoritativeEventClockV1` | `IAuthoritativeEventClock` |
| `IBalanceSimulationRuntimeV1` | `IBalanceSimulationLive` |
| `ICanonicalWeaponBlueprintResolver` | `IWeaponBlueprintResolver` |
| `ICharacterRuntimeGraphFactoryV1` | `ICharacterLiveGraphFactory` |
| `ICharacterRuntimeGraphV1` | `ICharacterLiveGraph` |
| `ICharacterSelectionRouteSinkV1` | `ICharacterSelectionRouteSink` |
| `ICollectedRunEquipmentPayloadSourceV2` | `ICollectedRunEquipmentPayloadSource` |
| `ICollectedRunRewardAtomicBatchAuthorityPortV1` | `ICollectedRunRewardAtomicBatchStatePort` |
| `ICollectedRunRewardTransferCompensationV1` | `ICollectedRunRewardTransferCompensation` |
| `ICollectedRunRewardTransferPersistencePortV1` | `ICollectedRunRewardTransferPersistencePort` |
| `ICombatHealthBarSnapshotSourceV1` | `ICombatHealthBarSnapshotSource` |
| `ICombatHitPolicyV1` | `ICombatHitPolicy` |
| `ICombatPresentationLifecycleSourceV1` | `ICombatPresentationLifecycleSource` |
| `IConditionRunClockV1` | `IConditionRunClock` |
| `IConditionRunLifecycleV1` | `IConditionRunLifecycle` |
| `ICraftedEquipmentLoadoutPortV1` | `ICraftedEquipmentLoadoutPort` |
| `ICraftingPresentationAuthorityPortV1` | `ICraftingPresentationStatePort` |
| `ICriticalHitResolutionAuthorityV1` | `ICriticalHitResolutionState` |
| `IDerivedCharacterStatComposerV1` | `IDerivedCharacterStatComposer` |
| `IEnemyActor2DAuthority` | `IEnemyActor2DState` |
| `IEnemyAttackCapabilityAdapterV1` | `IEnemyAttackCapabilityBridge` |
| `IEnemyAttackEffectPortV1` | `IEnemyAttackEffectPort` |
| `IEnemyAttackPatternCombatContextV1` | `IEnemyAttackPatternCombatContext` |
| `IEnemyAttackPatternDamageChannelMapV1` | `IEnemyAttackPatternDamageChannelMap` |
| `IEnemyAttackPatternEffectPortV1` | `IEnemyAttackPatternEffectPort` |
| `IEnemyAttackPatternEmissionRealizerV1` | `IEnemyAttackPatternEmissionRealizer` |
| `IEnemyAttackPatternLineOfSightV1` | `IEnemyAttackPatternLineOfSight` |
| `IEnemyAttackPatternMeleeContactReporterV1` | `IEnemyAttackPatternMeleeContactReporter` |
| `IEnemyAttackPatternPounceMotionV1` | `IEnemyAttackPatternPounceMotion` |
| `IEnemyAttackPatternProjectilePrefabResolverV1` | `IEnemyAttackPatternProjectilePrefabResolver` |
| `IEnemyAttackPatternRunTimeV1` | `IEnemyAttackPatternRunTime` |
| `IEnemyAttackPatternSourceLifecycleV1` | `IEnemyAttackPatternSourceLifecycle` |
| `IEnemyAttackPatternTransactionalRealizerV1` | `IEnemyAttackPatternTransactionalRealizer` |
| `IEnemyCatalogRegistryV1` | `IEnemyCatalogRegistry` |
| `IEnemyCommittedAttackPatternPortV1` | `IEnemyCommittedAttackPatternPort` |
| `IEnemyDecisionRuntimePolicyV1` | `IEnemyDecisionLivePolicy` |
| `IEnemyDifficultyScalingPolicyV1` | `IEnemyDifficultyScalingPolicy` |
| `IEnemyDropFactConsumerV1` | `IEnemyDropFactConsumer` |
| `IEnemyExperienceFactConsumerV1` | `IEnemyExperienceFactConsumer` |
| `IEnemyExperienceRewardDefinitionV1` | `IEnemyExperienceRewardDefinition` |
| `IEnemyKillStatFactConsumerV1` | `IEnemyKillStatFactConsumer` |
| `IEnemyMovementEnvironmentQueryV1` | `IEnemyMovementEnvironmentQuery` |
| `IEnemyMovementIntentRealizerV1` | `IEnemyMovementIntentRealizer` |
| `IEnemyMovementRuntimePolicyV1` | `IEnemyMovementLivePolicy` |
| `IEnemyPerceptionRuntimeAdapterV1` | `IEnemyPerceptionLiveBridge` |
| `IEnemyPlayerDamagePortV1` | `IEnemyPlayerDamagePort` |
| `IEnemyRoomTerminalPortV1` | `IEnemyRoomTerminalPort` |
| `IEnemyRuntimeIdentityDeriverV1` | `IEnemyLiveIdentityDeriver` |
| `IEnemyRuntimeMechanicsReadiness2D` | `IEnemyLiveMechanicsReadiness2D` |
| `IEnemyTargetingAimPolicyV1` | `IEnemyTargetingAimPolicy` |
| `IEnemyTerminalCollisionAdapterV1` | `IEnemyTerminalCollisionBridge` |
| `IEnemyTerminalSourceContextResolverV1` | `IEnemyTerminalSourceContextResolver` |
| `IGameplayDropSourceV1` | `IGameplayDropSource` |
| `IGeneratedTerminalDropPendingAdmissionV1` | `IGeneratedTerminalDropPendingAdmission` |
| `IHubRouteDestinationAdapterV1` | `IHubRouteDestinationBridge` |
| `IHubRouteTransactionPortV1` | `IHubRouteTransactionPort` |
| `IInventoryLoadoutAuthorityPortV1` | `IInventoryLoadoutStatePort` |
| `ILevelDoorPackageAdapter` | `ILevelDoorPackageBridge` |
| `ILevelGridV2AssetCompilerFaultInjector` | `ILevelGridAssetCompilerFaultInjector` |
| `ILevelSelectionRouteAdapterV1` | `ILevelSelectionRouteBridge` |
| `ILevelSelectionSceneLoaderV1` | `ILevelSelectionSceneLoader` |
| `IMissionRunExistingAuthorityPortV1` | `IMissionRunExistingStatePort` |
| `IMovementContactAuthority` | `IMovementContactState` |
| `IParticipantDropPacingStateStoreV1` | `IParticipantDropPacingStateStore` |
| `IPendingTerminalDropAdmissionConsumerV1` | `IPendingTerminalDropAdmissionConsumer` |
| `IPersonalRewardDeliveryOutboxV1` | `IPersonalRewardDeliveryOutbox` |
| `IPickupAdmissionRuntimeV1` | `IPickupAdmissionLive` |
| `IPickupSourcePositionResolverV1` | `IPickupSourcePositionResolver` |
| `IPlaySelectionRouteAdapterV1` | `IPlaySelectionRouteBridge` |
| `IPlayablePlayerDamageReceiverV1` | `IPlayablePlayerDamageReceiver` |
| `IPlayablePlayerHubReturnRequestV1` | `IPlayablePlayerHubReturnRequest` |
| `IPlayerExperienceAuthorityV1` | `IPlayerExperienceState` |
| `IPlayerHoldingsAuthorityV1` | `IPlayerHoldingsState` |
| `IPlayerInputRuntime` | `IPlayerInputLive` |
| `IPlayerMovementRuntime` | `IPlayerMovementLive` |
| `IPlayerPresentationRuntime` | `IPlayerPresentationLive` |
| `IPlayerRunCoordinator` | `IPlayerRunFlow` |
| `IPreparedSaveComponentRestoreV1` | `IPreparedSaveComponentRestore` |
| `IProductionCharacterStrongboxBridgeV1` | `ICharacterStrongboxBridge` |
| `IProductionFlowProfileStoreV1` | `IFlowProfileStore` |
| `IProductionRunStatInputResolverV1` | `IRunStatInputResolver` |
| `IProductionSceneLoadPortV1` | `ISceneLoadPort` |
| `IPropDamageEligibilityPolicyV1` | `IPropDamageEligibilityPolicy` |
| `IPropRuntimeFactoryV1` | `IPropLiveFactory` |
| `IPropTerminalDropFactConsumerV1` | `IPropTerminalDropFactConsumer` |
| `IPropTerminalSourceContextResolverV1` | `IPropTerminalSourceContextResolver` |
| `IRankedSkillsPersistencePortV2` | `IRankedSkillsPersistencePort` |
| `IRewardChildAuthorityV1` | `IRewardChildState` |
| `IRewardGenerationExecutorV1` | `IRewardGenerationExecutor` |
| `IRewardGrantHandlerV1` | `IRewardGrantHandler` |
| `IRewardPickupEquipmentPayloadResolverV1` | `IRewardPickupEquipmentPayloadResolver` |
| `IRewardPickupLifecycleAuthorityV1` | `IRewardPickupLifecycleState` |
| `IRewardProfileResolverV1` | `IRewardProfileResolver` |
| `IRoomAccessFactPortV1` | `IRoomAccessFactPort` |
| `IRoomAccessReferenceRegistryV1` | `IRoomAccessReferenceRegistry` |
| `IRoomContentObjectCatalogV1` | `IRoomContentObjectCatalog` |
| `IRoomLiveRuntimeQueryV1` | `IRoomLiveQuery` |
| `IRoomMissionLayoutV1` | `IRoomMissionLayout` |
| `IRoomProjectionStateReader` | `IRoomViewStateReader` |
| `IRoomRunHoldingPortV1` | `IRoomRunHoldingPort` |
| `IRoomRuntimeAuthorityV1` | `IRoomLiveState` |
| `IRunActiveAbilityRuntimePortV1` | `IRunActiveAbilityLivePort` |
| `IRunConditionDefinitionProviderV1` | `IRunConditionDefinitionProvider` |
| `IRunConditionParticipantSeedProviderV1` | `IRunConditionParticipantSeedProvider` |
| `IRunConditionRuntimePortV1` | `IRunConditionLivePort` |
| `IRunConditionalFactRuntimePortV1` | `IRunConditionalFactLivePort` |
| `IRunDebugRuntimePortV1` | `IRunDebugLivePort` |
| `IRunLifecycleRuntimePortV1` | `IRunLifecycleLivePort` |
| `IRunMissionResultEndRetryPolicyV1` | `IRunMissionResultEndRetryPolicy` |
| `IRunMissionResultLifecycleBindingV1` | `IRunMissionResultLifecycleBinding` |
| `IRunMissionResultPortV1` | `IRunMissionResultPort` |
| `IRunMissionStrongboxSnapshotSourceV1` | `IRunMissionStrongboxSnapshotSource` |
| `IRunPickupCollectionAuthorityV1` | `IRunPickupCollectionState` |
| `IRunPickupRunSessionPortV1` | `IRunPickupRunSessionPort` |
| `IRunPickupSourcePositionPortV1` | `IRunPickupSourcePositionPort` |
| `IRunPlayerRuntimePortV1` | `IRunPlayerLivePort` |
| `IRunRewardPickupAcceptedFeedbackV1` | `IRunRewardPickupAcceptedFeedback` |
| `IRunRewardPickupProjectionBinderV1` | `IRunRewardPickupViewBinder` |
| `IRunRewardProgressionContextProviderV1` | `IRunRewardProgressionContextProvider` |
| `IRunRoomRuntimePortV1` | `IRunRoomLivePort` |
| `IRunSessionCollectedRewardAuthorityV1` | `IRunSessionCollectedRewardState` |
| `IRunSessionNonConditionRuntimePortFactoryV1` | `IRunSessionNonConditionLivePortFactory` |
| `IRunSessionRuntimePortFactoryV1` | `IRunSessionLivePortFactory` |
| `IRunSessionStartSourceV1` | `IRunSessionStartSource` |
| `IRunStatusEffectRuntimePortV1` | `IRunStatusEffectLivePort` |
| `IRunWeaponRuntimePortV1` | `IRunWeaponLivePort` |
| `ISaveComponentAdapterV1` | `ISaveComponentBridge` |
| `ISaveComponentPayloadCodecV1` | `ISaveComponentPayloadCodec` |
| `IShopLockCapacityExtensionV1` | `IShopLockCapacityExtension` |
| `IShopScreenRouteAdapterV1` | `IShopScreenRouteBridge` |
| `ISkillRespecCostPolicyV2` | `ISkillRespecCostPolicy` |
| `ISkillRespecPaymentAuthorityV2` | `ISkillRespecPaymentState` |
| `ISkillsScreenNavigationPortV1` | `ISkillsScreenNavigationPort` |
| `ISkillsScreenPresenterV1` | `ISkillsScreenPresenter` |
| `IStarterCharacterRuntimeGraphFactoryV1` | `IStarterCharacterLiveGraphFactory` |
| `IStrongboxDurableOpeningExecutorV1` | `IStrongboxDurableOpeningExecutor` |
| `IStrongboxEquipmentGenerationDefinitionProviderV1` | `IStrongboxEquipmentGenerationDefinitionProvider` |
| `IStrongboxEquipmentPayloadResolverV1` | `IStrongboxEquipmentPayloadResolver` |
| `IStrongboxGrantPayloadResolverV1` | `IStrongboxGrantPayloadResolver` |
| `IStrongboxMissionResultApplicationAuthorityPortV1` | `IStrongboxMissionResultApplicationStatePort` |
| `IStrongboxOpeningRecoveryPortV1` | `IStrongboxOpeningRecoveryPort` |
| `IStrongboxRewardGeneratorV1` | `IStrongboxRewardGenerator` |
| `IStrongboxSimulationProductionGateway` | `IStrongboxSimulationGateway` |
| `ITerminalDropFactAdapterV1` | `ITerminalDropFactBridge` |
| `ITerminalDropRunContextResolverV1` | `ITerminalDropRunContextResolver` |
| `ITerminalRewardEnvironmentResolverV1` | `ITerminalRewardEnvironmentResolver` |
| `ITerminalRewardOverrideResolverV1` | `ITerminalRewardOverrideResolver` |
| `ITerminalRewardParticipantResolverV1` | `ITerminalRewardParticipantResolver` |
| `ITerminalRewardPlacementFactV1` | `ITerminalRewardPlacementFact` |
| `ImmutableRunLifecyclePortV1` | `ImmutableRunLifecyclePort` |
| `InMemoryProductionFlowProfileStoreV1` | `InMemoryFlowProfileStore` |
| `IndependentRewardRollV1` | `IndependentRewardRoll` |
| `InventoryBackedWeaponExecutionAdapter` | `InventoryBackedWeaponExecutionBridge` |
| `InventoryBackedWeaponExecutionAdapterTests` | `InventoryBackedWeaponExecutionBridgeTests` |
| `InventoryLoadoutAuthorityCommandV1` | `InventoryLoadoutStateCommand` |
| `InventoryLoadoutAuthorityConnectionTests` | `InventoryLoadoutStateConnectionTests` |
| `InventoryLoadoutAuthorityMutationStatusV1` | `InventoryLoadoutStateMutationStatus` |
| `InventoryLoadoutAuthorityResultV1` | `InventoryLoadoutStateResult` |
| `InventoryLoadoutAuthoritySnapshotV1` | `InventoryLoadoutStateSnapshot` |
| `InventoryLoadoutCanonicalV1` | `InventoryLoadout` |
| `InventoryLoadoutEquipmentProjectionV1` | `InventoryLoadoutEquipmentView` |
| `InventoryLoadoutScreenControllerV1` | `InventoryLoadoutScreenController` |
| `InventoryLoadoutScreenResultV1` | `InventoryLoadoutScreenResult` |
| `InventoryLoadoutScreenServiceTests` | `InventoryLoadoutScreenActionsTests` |
| `InventoryLoadoutScreenServiceV1` | `InventoryLoadoutScreenActions` |
| `InventoryLoadoutScreenSnapshotV1` | `InventoryLoadoutScreenSnapshot` |
| `InventoryLoadoutScreenStatusV1` | `InventoryLoadoutScreenStatus` |
| `InventoryLoadoutSelectionProjectionV1` | `InventoryLoadoutSelectionView` |
| `InventoryLoadoutSlotBindingV1` | `InventoryLoadoutSlotBinding` |
| `InventoryLoadoutSlotDescriptorV1` | `InventoryLoadoutSlotDescriptor` |
| `InventoryLoadoutSlotIdsV1` | `InventoryLoadoutSlotIds` |
| `InventoryLoadoutSlotKindV1` | `InventoryLoadoutSlotKind` |
| `InventoryLoadoutSlotsV1` | `InventoryLoadoutSlots` |
| `InventoryWeaponMountedAimExecutionV1` | `InventoryWeaponMountedAimExecution` |
| `InventoryWeaponMountedRuntimeV1` | `InventoryWeaponMountedLive` |
| `InventoryWeaponRuntimeComposition` | `InventoryWeaponLiveSetup` |
| `InventoryWeaponRuntimePlayModeTests` | `InventoryWeaponLivePlayModeTests` |
| `JsonRoomRuntimeBootstrap2D` | `JsonRoomLiveBootstrap2D` |
| `JsonRoomRuntimeBootstrapCompositionTests` | `JsonRoomLiveBootstrapSetupTests` |
| `KnownSaveComponentAdaptersV1` | `KnownSaveComponentAdapters` |
| `KnownSaveComponentCodecsV1` | `KnownSaveComponentCodecs` |
| `KnownSaveComponentDefinitionsV1` | `KnownSaveComponentDefinitions` |
| `KnownSaveComponentVersionGuardV1` | `KnownSaveComponentVersionGuard` |
| `LedgerCanonicalText` | `LedgerText` |
| `LedgerSnapshotCodecV1` | `LedgerSnapshotCodec` |
| `LegacyCharacterProfileMigrationResultV1` | `LegacyCharacterProfileMigrationResult` |
| `LegacyCharacterProfileMigrationV1` | `LegacyCharacterProfileMigration` |
| `LegacyCharacterProfileV1` | `LegacyCharacterProfile` |
| `LegacyFirstWeaponHoldingsAdapterRetirementTests` | `LegacyFirstWeaponHoldingsBridgeRetirementTests` |
| `Level1AuthorableRoomDefinitionV1` | `Level1AuthorableRoomDefinition` |
| `Level1LiveRoomGraphDefinitionV1` | `Level1LiveRoomGraphDefinition` |
| `Level1RoomGraphDefinitionV1` | `Level1RoomGraphDefinition` |
| `LevelAvailabilityV1` | `LevelAvailability` |
| `LevelDoorAuthorityV2Tests` | `LevelDoorStateTests` |
| `LevelDoorPlacementModeV2` | `LevelDoorPlacementMode` |
| `LevelDoorSideV2` | `LevelDoorSide` |
| `LevelDtoV2` | `LevelDto` |
| `LevelGridAuthoringV2AuditTests` | `LevelGridAuthoringAuditTests` |
| `LevelGridAuthoringV2CompositeValidator` | `LevelGridAuthoringCompositeValidator` |
| `LevelGridAuthoringV2CreationMenu` | `LevelGridAuthoringCreationMenu` |
| `LevelGridAuthoringV2Gizmos` | `LevelGridAuthoringGizmos` |
| `LevelGridAuthoringV2JsonExporter` | `LevelGridAuthoringJsonExporter` |
| `LevelGridAuthoringV2LiveValidation` | `LevelGridAuthoringLiveValidation` |
| `LevelGridAuthoringV2Menu` | `LevelGridAuthoringMenu` |
| `LevelGridAuthoringV2Tests` | `LevelGridAuthoringTests` |
| `LevelGridAuthoringV2ThreeRoomExampleMenu` | `LevelGridAuthoringThreeRoomExampleMenu` |
| `LevelGridAuthoringV2Validator` | `LevelGridAuthoringValidator` |
| `LevelGridConnectionRecordV2` | `LevelGridConnectionRecord` |
| `LevelGridDoorOperationsV2` | `LevelGridDoorOperations` |
| `LevelGridDoorRecordV2` | `LevelGridDoorRecord` |
| `LevelGridEditorOperationsV2` | `LevelGridEditorOperations` |
| `LevelGridEditorProblemLocatorV2` | `LevelGridEditorProblemLocator` |
| `LevelGridEditorProjectionV2` | `LevelGridEditorView` |
| `LevelGridEditorRoomProjectionV2` | `LevelGridEditorRoomView` |
| `LevelGridEditorRuntimeIntegrationV2Tests` | `LevelGridEditorLiveIntegrationTests` |
| `LevelGridEditorSecondAuditV2Tests` | `LevelGridEditorSecondAuditTests` |
| `LevelGridEditorTargetedFixesV2Tests` | `LevelGridEditorTargetedFixesTests` |
| `LevelGridEditorWindowV2` | `LevelGridEditorWindow` |
| `LevelGridEditorWindowV2Tests` | `LevelGridEditorWindowTests` |
| `LevelGridLegacySurfaceGuardsV2` | `LevelGridLegacySurfaceGuards` |
| `LevelGridPlayableAssetChangeWatcherV2` | `LevelGridPlayableAssetChangeWatcher` |
| `LevelGridPlayableBuildFacadeV2` | `LevelGridPlayableBuildFacade` |
| `LevelGridPlayableBuildPathOwnershipV2Tests` | `LevelGridPlayableBuildPathOwnershipTests` |
| `LevelGridPlayableBuildPathsV2` | `LevelGridPlayableBuildPaths` |
| `LevelGridPlayableBuildResultV2` | `LevelGridPlayableBuildResult` |
| `LevelGridPlayableMetadataOperationsV2` | `LevelGridPlayableMetadataOperations` |
| `LevelGridPlayableMetadataV2` | `LevelGridPlayableMetadata` |
| `LevelGridPlayableProvenanceRecordV2` | `LevelGridPlayableProvenanceRecord` |
| `LevelGridPlayableProvenanceV2` | `LevelGridPlayableProvenance` |
| `LevelGridPlayableStatusEvaluatorV2` | `LevelGridPlayableStatusEvaluator` |
| `LevelGridPlayableStatusKindV2` | `LevelGridPlayableStatusKind` |
| `LevelGridPlayableStatusV2` | `LevelGridPlayableStatus` |
| `LevelGridPlayableValidationV2` | `LevelGridPlayableValidation` |
| `LevelGridProblemCodeV2` | `LevelGridProblemCode` |
| `LevelGridProblemV2` | `LevelGridProblem` |
| `LevelGridProblemsWindowV2` | `LevelGridProblemsWindow` |
| `LevelGridRoomRecordV2` | `LevelGridRoomRecord` |
| `LevelGridV2AssetCompiler` | `LevelGridAssetCompiler` |
| `LevelGridV2AssetCompilerPublicationTests` | `LevelGridAssetCompilerPublicationTests` |
| `LevelGridV2AssetCompilerPublishStep` | `LevelGridAssetCompilerPublishStep` |
| `LevelGridV2CompileIssue` | `LevelGridCompileIssue` |
| `LevelGridV2CompileResult` | `LevelGridCompileResult` |
| `LevelGridV2CompiledAssetPlayModeTests` | `LevelGridCompiledAssetPlayModeTests` |
| `LevelGridV2Compiler` | `LevelGridCompiler` |
| `LevelGridV2CompilerTests` | `LevelGridCompilerTests` |
| `LevelGridV2PlayableExporter` | `LevelGridPlayableExporter` |
| `LevelGridV2RoomFolderMigration` | `LevelGridRoomFolderMigration` |
| `LevelGridV2SecondAuditRegressionTests` | `LevelGridSecondAuditRegressionTests` |
| `LevelGridV2SourcePackage` | `LevelGridSourcePackage` |
| `LevelGridV2UnityMetadataRegressionTests` | `LevelGridUnityMetadataRegressionTests` |
| `LevelGridValidationPurposeV2` | `LevelGridValidationPurpose` |
| `LevelGridValidationResultV2` | `LevelGridValidationResult` |
| `LevelIdentityDtoV2` | `LevelIdentityDto` |
| `LevelNavigationAdapter` | `LevelNavigationBridge` |
| `LevelRecommendationV1` | `LevelRecommendation` |
| `LevelReleaseStateV1` | `LevelReleaseState` |
| `LevelRouteKindV1` | `LevelRouteKind` |
| `LevelScalingDtoV1` | `LevelScalingDto` |
| `LevelSelectionCatalogDefinitionV1` | `LevelSelectionCatalogDefinition` |
| `LevelSelectionCatalogV1` | `LevelSelectionCatalog` |
| `LevelSelectionControllerV1` | `LevelSelectionController` |
| `LevelSelectionDefinitionRecordV1` | `LevelSelectionDefinitionRecord` |
| `LevelSelectionDefinitionV1` | `LevelSelectionDefinition` |
| `LevelSelectionResultV1` | `LevelSelectionResult` |
| `LevelSelectionRouteContextV1` | `LevelSelectionRouteContext` |
| `LevelSelectionRouteV1` | `LevelSelectionRoute` |
| `LevelSelectionServiceV1` | `LevelSelectionActions` |
| `LevelSelectionStatusV1` | `LevelSelectionStatus` |
| `LevelSelectionViewV1` | `LevelSelectionView` |
| `LevelSystemStabilizationV2Tests` | `LevelSystemStabilizationTests` |
| `LockedEnemyTargetingAimPolicyV1` | `LockedEnemyTargetingAimPolicy` |
| `LootPickupPresentationKindV1` | `LootPickupPresentationKind` |
| `LootPickupPresentationV1` | `LootPickupPresentation` |
| `LootPickupRunProjection2D` | `LootPickupRunView2D` |
| `LootPickupRunProjectionPlayModeTests` | `LootPickupRunViewPlayModeTests` |
| `LootRunHudViewV1` | `LootRunHudView` |
| `LootboxGeneratedItemV1` | `LootboxGeneratedItem` |
| `LootboxOddsEntryV1` | `LootboxOddsEntry` |
| `LootboxOddsReportV1` | `LootboxOddsReport` |
| `LootboxSimulatorRuntimeV1` | `LootboxSimulatorLive` |
| `LootboxSimulatorRuntimeV1Tests` | `LootboxSimulatorLiveTests` |
| `MapConnectionDtoV2` | `MapConnectionDto` |
| `MapDtoV2` | `MapDto` |
| `MapNodeDtoV2` | `MapNodeDto` |
| `MeleeDtoV1` | `MeleeDto` |
| `MeleePatternDtoV1` | `MeleePatternDto` |
| `MissionResultPayloadV1` | `MissionResultPayload` |
| `MissionResultsSessionV1` | `MissionResultsSession` |
| `MissionRunAuthorityResultV1` | `MissionRunStateResult` |
| `MissionRunAuthorityStatusV1` | `MissionRunStateStatus` |
| `MissionRunCanonicalV1` | `MissionRun` |
| `MissionRunCollectStrongboxCommandV1` | `MissionRunCollectStrongboxCommand` |
| `MissionRunCollectionVerificationV1` | `MissionRunCollectionVerification` |
| `MissionRunCompletionStateV1` | `MissionRunCompletionState` |
| `MissionRunExistingAuthorityPortV1` | `MissionRunExistingStatePort` |
| `MissionRunPayloadV1` | `MissionRunPayload` |
| `MissionRunResultAuthorityV1` | `MissionRunResultState` |
| `MissionRunResultAuthorityV1Tests` | `MissionRunResultStateTests` |
| `MissionRunStrongboxCollectionV1` | `MissionRunStrongboxCollection` |
| `MissionRunStrongboxProjectionV1` | `MissionRunStrongboxView` |
| `MissionRunStrongboxResultV1` | `MissionRunStrongboxResult` |
| `MissionRunStrongboxStateV1` | `MissionRunStrongboxState` |
| `ModifierApplicationFingerprintV1` | `ModifierApplicationFingerprint` |
| `MoneyRewardChildAuthorityV1` | `MoneyRewardChildState` |
| `MoneyWalletComponentCodecV1` | `MoneyWalletComponentCodec` |
| `MoneyWalletIdsV1` | `MoneyWalletIds` |
| `MoneyWalletService` | `MoneyWalletActions` |
| `MovementActorPlayerRuntimeAdapter` | `MovementActorPlayerLiveBridge` |
| `MovementBody2DAdapter` | `MovementBody2DBridge` |
| `MovementBody2DAdapterTests` | `MovementBody2DBridgeTests` |
| `MovementContact2DAdapter` | `MovementContact2DBridge` |
| `MovementContact2DAdapterTests` | `MovementContact2DBridgeTests` |
| `MultiplayerDropSimulationRuntimeV1` | `MultiplayerDropSimulationLive` |
| `MutableClockV1` | `MutableClock` |
| `MutatingThrowingReceiptAuthority` | `MutatingThrowingReceiptState` |
| `NoOpEnemyRuntimePortV1` | `NoOpEnemyLivePort` |
| `ObjectiveFactAdapter` | `ObjectiveFactBridge` |
| `OwnedStrongboxGroupPresentationV1` | `OwnedStrongboxGroupPresentation` |
| `OwnedStrongboxGroupsViewV1` | `OwnedStrongboxGroupsView` |
| `OwnedStrongboxInstancePresentationV1` | `OwnedStrongboxInstancePresentation` |
| `OwningRunClockV1` | `OwningRunClock` |
| `OwningRunLifecycleV1` | `OwningRunLifecycle` |
| `ParticipantDropPacingAuthorityV1` | `ParticipantDropPacingState` |
| `ParticipantDropPacingStateV1` | `ParticipantDropPacingState` |
| `ParticipantRuntime` | `ParticipantLive` |
| `PendingAdmissionPickupBridgeV1` | `PendingAdmissionPickupBridge` |
| `PendingAdmissionPickupBridgeV1Tests` | `PendingAdmissionPickupBridgeTests` |
| `PendingAdmissionProjectionConsumerV1` | `PendingAdmissionViewConsumer` |
| `PendingRunRewardProjectionV1` | `PendingRunRewardView` |
| `PendingTerminalDropAdmissionAuthorityV1` | `PendingTerminalDropAdmissionState` |
| `PendingTerminalDropAdmissionResultV1` | `PendingTerminalDropAdmissionResult` |
| `PendingTerminalDropAdmissionStatusV1` | `PendingTerminalDropAdmissionStatus` |
| `PendingTerminalDropPickupConsumerV1` | `PendingTerminalDropPickupConsumer` |
| `PerceptionDtoV1` | `PerceptionDto` |
| `PermanentRewardTransferStateV1` | `PermanentRewardTransferState` |
| `PersistentMissionResultRunPortV1` | `PersistentMissionResultRunPort` |
| `PersonalRewardDecisionV1` | `PersonalRewardDecision` |
| `PersonalRewardDeliveryEnvelopeV1` | `PersonalRewardDeliveryEnvelope` |
| `PersonalRewardDeliveryStateV1` | `PersonalRewardDeliveryState` |
| `PersonalRewardGenerationRandomV1` | `PersonalRewardGenerationRandom` |
| `PersonalRewardGenerationResultV1` | `PersonalRewardGenerationResult` |
| `PersonalRewardGenerationServiceV1` | `PersonalRewardGenerationActions` |
| `PersonalRewardGenerationStatusV1` | `PersonalRewardGenerationStatus` |
| `PersonalRewardGenerationV1Tests` | `PersonalRewardGenerationTests` |
| `PersonalRewardGroupGenerationV1` | `PersonalRewardGroupGeneration` |
| `PersonalRewardRollContextV1` | `PersonalRewardRollContext` |
| `PersonalStrongboxRewardGenerationV1` | `PersonalStrongboxRewardGeneration` |
| `PhysicsEnemyAttackPatternLineOfSightV1` | `PhysicsEnemyAttackPatternLineOfSight` |
| `PickupBridgeFingerprintV1` | `PickupBridgeFingerprint` |
| `PickupDeliveryDispositionV1` | `PickupDeliveryDisposition` |
| `PickupDeliveryResultV1` | `PickupDeliveryResult` |
| `PickupSourcePositionV1` | `PickupSourcePosition` |
| `PipelineAdapter` | `PipelineBridge` |
| `PlayModeAvailabilityV1` | `PlayModeAvailability` |
| `PlayModeCatalogDefinitionV1` | `PlayModeCatalogDefinition` |
| `PlayModeCatalogV1` | `PlayModeCatalog` |
| `PlayModeDefinitionRecordV1` | `PlayModeDefinitionRecord` |
| `PlayModeDefinitionV1` | `PlayModeDefinition` |
| `PlayModeDestinationV1` | `PlayModeDestination` |
| `PlayNavigationAdapter` | `PlayNavigationBridge` |
| `PlaySelectionControllerV1` | `PlaySelectionController` |
| `PlaySelectionResultV1` | `PlaySelectionResult` |
| `PlaySelectionRouteV1` | `PlaySelectionRoute` |
| `PlaySelectionServiceTests` | `PlaySelectionActionsTests` |
| `PlaySelectionServiceV1` | `PlaySelectionActions` |
| `PlaySelectionStatusV1` | `PlaySelectionStatus` |
| `PlayablePlayerDamageCommandFactoryV1` | `PlayablePlayerDamageCommandFactory` |
| `PlayablePlayerDefeatedFactV1` | `PlayablePlayerDefeatedFact` |
| `PlayablePlayerHubReturnAuthorityGuardV1` | `PlayablePlayerHubReturnStateGuard` |
| `PlayablePlayerVitalsInstallerV1` | `PlayablePlayerVitalsInstaller` |
| `PlayerAccountAggregateCodecV1` | `PlayerAccountAggregateCodec` |
| `PlayerAccountComponentSemanticsV1` | `PlayerAccountComponentSemantics` |
| `PlayerAccountFileCodecV1` | `PlayerAccountFileCodec` |
| `PlayerAccountRestoreCoordinatorV1` | `PlayerAccountRestoreFlow` |
| `PlayerAccountRestoreResultV1` | `PlayerAccountRestoreResult` |
| `PlayerAccountRestoreStatusV1` | `PlayerAccountRestoreStatus` |
| `PlayerAccountSaveAuthoritySnapshotV1` | `PlayerAccountSaveStateSnapshot` |
| `PlayerAccountSaveAuthorityV1` | `PlayerAccountSaveState` |
| `PlayerAccountSaveAuthorityV1Tests` | `PlayerAccountSaveStateTests` |
| `PlayerAccountSaveCommandKindV1` | `PlayerAccountSaveCommandKind` |
| `PlayerAccountSaveCommandV1` | `PlayerAccountSaveCommand` |
| `PlayerAccountSaveReplayRecordV1` | `PlayerAccountSaveReplayRecord` |
| `PlayerAccountSaveResultV1` | `PlayerAccountSaveResult` |
| `PlayerAccountSaveStatusV1` | `PlayerAccountSaveStatus` |
| `PlayerAccountSnapshotFingerprintV1` | `PlayerAccountSnapshotFingerprint` |
| `PlayerAccountSnapshotV1` | `PlayerAccountSnapshot` |
| `PlayerAccountStoreResultV1` | `PlayerAccountStoreResult` |
| `PlayerAccountStoreStatusV1` | `PlayerAccountStoreStatus` |
| `PlayerActorAuthority` | `PlayerActorState` |
| `PlayerActorAuthorityTests` | `PlayerActorStateTests` |
| `PlayerCombatIntentAdapter` | `PlayerCombatIntentBridge` |
| `PlayerCombatIntentAdapterTests` | `PlayerCombatIntentBridgeTests` |
| `PlayerExperienceAuthorityTests` | `PlayerExperienceStateTests` |
| `PlayerExperienceAuthorityV1` | `PlayerExperienceState` |
| `PlayerExperienceComponentCodecV1` | `PlayerExperienceComponentCodec` |
| `PlayerExperienceCurveV1` | `PlayerExperienceCurve` |
| `PlayerExperienceFormatV1` | `PlayerExperienceFormat` |
| `PlayerExperienceGrantFactV1` | `PlayerExperienceGrantFact` |
| `PlayerExperienceGrantRequestV1` | `PlayerExperienceGrantRequest` |
| `PlayerExperienceGrantSnapshotV1` | `PlayerExperienceGrantSnapshot` |
| `PlayerExperienceGrantStatusV1` | `PlayerExperienceGrantStatus` |
| `PlayerExperienceIdsV1` | `PlayerExperienceIds` |
| `PlayerExperienceImportResultV1` | `PlayerExperienceImportResult` |
| `PlayerExperienceImportStatusV1` | `PlayerExperienceImportStatus` |
| `PlayerExperienceSnapshotV1` | `PlayerExperienceSnapshot` |
| `PlayerExperienceStateV1` | `PlayerExperienceState` |
| `PlayerHoldingsCommandV1` | `PlayerHoldingsCommand` |
| `PlayerHoldingsComponentCodecV1` | `PlayerHoldingsComponentCodec` |
| `PlayerHoldingsImportResultV1` | `PlayerHoldingsImportResult` |
| `PlayerHoldingsImportStatusV1` | `PlayerHoldingsImportStatus` |
| `PlayerHoldingsMutationResultV1` | `PlayerHoldingsMutationResult` |
| `PlayerHoldingsMutationStatusV1` | `PlayerHoldingsMutationStatus` |
| `PlayerHoldingsRewardChildAuthorityV1` | `PlayerHoldingsRewardChildState` |
| `PlayerHoldingsService` | `PlayerHoldingsActions` |
| `PlayerHoldingsServiceTests` | `PlayerHoldingsActionsTests` |
| `PlayerHoldingsSnapshotV1` | `PlayerHoldingsSnapshot` |
| `PlayerHoldingsTransactionRecordV1` | `PlayerHoldingsTransactionRecord` |
| `PlayerHudCombatHealthSnapshotSourceV1` | `PlayerHudCombatHealthSnapshotSource` |
| `PlayerInventoryWeaponRuntimeCompositionRoot` | `PlayerInventoryWeaponLiveSetupRoot` |
| `PlayerLevelUpFactV1` | `PlayerLevelUpFact` |
| `PlayerLiveAuthorityTests` | `PlayerLiveStateTests` |
| `PlayerMovementIntentAdapter` | `PlayerMovementIntentBridge` |
| `PlayerMovementIntentAdapterTests` | `PlayerMovementIntentBridgeTests` |
| `PlayerPrefsProductionFlowProfileStoreV1` | `PlayerPrefsFlowProfileStore` |
| `PlayerRouteProfileEnvelopeV1` | `PlayerRouteProfileEnvelope` |
| `PlayerRouteProfilePayloadV1` | `PlayerRouteProfilePayload` |
| `PlayerRouteProfileValidationResultV1` | `PlayerRouteProfileValidationResult` |
| `PlayerRouteProfileValidationStatusV1` | `PlayerRouteProfileValidationStatus` |
| `PlayerRouteWeaponSlotEnvelopeV1` | `PlayerRouteWeaponSlotEnvelope` |
| `PlayerRouteWeaponSlotV1` | `PlayerRouteWeaponSlot` |
| `PlayerRuntimeAttachments` | `PlayerLiveAttachments` |
| `PlayerRuntimeComposition` | `PlayerLiveSetup` |
| `PlayerRuntimeCompositionRoot` | `PlayerLiveSetupRoot` |
| `PlayerRuntimeCompositionTests` | `PlayerLiveSetupTests` |
| `PlayerRuntimeConfiguration` | `PlayerLiveConfiguration` |
| `PlayerRuntimeConstructionRejectionCode` | `PlayerLiveConstructionRejectionCode` |
| `PlayerRuntimeConstructionResult` | `PlayerLiveConstructionResult` |
| `PlayerRuntimeConstructionStatus` | `PlayerLiveConstructionStatus` |
| `PlayerRuntimeRestartCommand` | `PlayerLiveRestartCommand` |
| `PlayerRuntimeRestartRejectionCode` | `PlayerLiveRestartRejectionCode` |
| `PlayerRuntimeRestartResult` | `PlayerLiveRestartResult` |
| `PlayerRuntimeRestartStatus` | `PlayerLiveRestartStatus` |
| `PlayerRuntimeSnapshot` | `PlayerLiveSnapshot` |
| `PlayerRuntimeWeaponStateAdapter` | `PlayerLiveWeaponStateBridge` |
| `ProductionCanonicalNormalProjectile2D` | `NormalProjectile2D` |
| `ProductionCanonicalProjectileEffectSink2D` | `ProjectileEffectSink2D` |
| `ProductionCanonicalWeaponActorStateV1` | `WeaponActorState` |
| `ProductionCanonicalWeaponFireControllerV1` | `WeaponFireController` |
| `ProductionCanonicalWeaponFireInstallerV1` | `WeaponFireInstaller` |
| `ProductionCanonicalWeaponGameplayBindingV2` | `WeaponGameplayBinding` |
| `ProductionCharacterAuthorityAdaptersV1` | `CharacterStateAdapters` |
| `ProductionCharacterRunSessionStartSourceV1` | `CharacterRunSessionStartSource` |
| `ProductionCharacterRuntimeGraphFactoryV1` | `CharacterLiveGraphFactory` |
| `ProductionCharacterRuntimeGraphV1` | `CharacterLiveGraph` |
| `ProductionCharacterSelectionControllerV1` | `CharacterSelectionController` |
| `ProductionCharacterSelectionStageV1` | `CharacterSelectionStage` |
| `ProductionCharacterStrongboxBridgeRegistryV1` | `CharacterStrongboxBridgeRegistry` |
| `ProductionCharacterStrongboxBridgeV1` | `CharacterStrongboxBridge` |
| `ProductionCharacterStrongboxCompositionV1` | `CharacterStrongboxSetup` |
| `ProductionCharacterStrongboxRuntimeV1` | `CharacterStrongboxLive` |
| `ProductionCollectedRunRewardAtomicAuthorityV2` | `CollectedRunRewardAtomicState` |
| `ProductionCollectedRunRewardCompensationV2` | `CollectedRunRewardCompensation` |
| `ProductionCollectedRunRewardPersistenceV2` | `CollectedRunRewardPersistence` |
| `ProductionCollectedRunRewardRecoveryV2` | `CollectedRunRewardRecovery` |
| `ProductionCollectedRunRewardResultsBridge` | `CollectedRunRewardResultsBridge` |
| `ProductionCollectedRunRewardResultsOverlay` | `CollectedRunRewardResultsOverlay` |
| `ProductionCollectedRunRewardRuntimeRegistryV2` | `CollectedRunRewardLiveRegistry` |
| `ProductionCollectedRunRewardTerminalNoticeV1` | `CollectedRunRewardTerminalNotice` |
| `ProductionCollectedRunRewardTransferRuntimeRegistry` | `CollectedRunRewardTransferLiveRegistry` |
| `ProductionCollectedRunRewardTransferServiceV2` | `CollectedRunRewardTransferActions` |
| `ProductionConditionBoundRunSessionRuntimePortFactoryV1` | `ConditionBoundRunSessionLivePortFactory` |
| `ProductionConditionBoundRunSessionStartSourceV1` | `ConditionBoundRunSessionStartSource` |
| `ProductionEquipmentCatalogAdapterV1` | `EquipmentCatalogBridge` |
| `ProductionEquippedGunV1` | `EquippedGun` |
| `ProductionExactWeaponInstanceLoadoutTests` | `ExactWeaponInstanceLoadoutTests` |
| `ProductionFlowPlayModeTests` | `FlowPlayModeTests` |
| `ProductionFlowProfileRecordV1` | `FlowProfileRecord` |
| `ProductionFlowScenePathsV1` | `FlowScenePaths` |
| `ProductionFlowSessionV1Tests` | `FlowSessionTests` |
| `ProductionInventoryLoadoutAuthorityV1` | `InventoryLoadoutState` |
| `ProductionInventoryLoadoutImportResultV1` | `InventoryLoadoutImportResult` |
| `ProductionMainMenuControllerV1` | `MainMenuController` |
| `ProductionOpaqueWeaponInstanceIdentityTests` | `OpaqueWeaponInstanceIdentityTests` |
| `ProductionPlayableLevelCatalogAvailabilityTests` | `PlayableLevelCatalogAvailabilityTests` |
| `ProductionPlayableLevelCatalogV1` | `PlayableLevelCatalog` |
| `ProductionPlayableLevelControllerV1` | `PlayableLevelController` |
| `ProductionPlayableLevelDefinitionV1` | `PlayableLevelDefinition` |
| `ProductionPlayableLevelRuntimePortFactoryV1` | `PlayableLevelLivePortFactory` |
| `ProductionPlayableLevelStatInputResolverV1` | `PlayableLevelStatInputResolver` |
| `ProductionPlayablePlayerHubReturnRequestV1` | `PlayablePlayerHubReturnRequest` |
| `ProductionPlayerLoadoutRuntimeV1` | `PlayerLoadoutLive` |
| `ProductionProofOverlayRewardOverrideResolverV1` | `ProofOverlayRewardOverrideResolver` |
| `ProductionReadOnlyResultsBridgeV1` | `ReadOnlyResultsBridge` |
| `ProductionResultsContextV1` | `ResultsContext` |
| `ProductionResultsControllerV1` | `ResultsController` |
| `ProductionResultsSummaryV1` | `ResultsSummary` |
| `ProductionRewardOverrideCatalogV1` | `RewardOverrideCatalog` |
| `ProductionRewardSourceCatalogV1` | `RewardSourceCatalog` |
| `ProductionRunDropPacingCatalogV1` | `RunDropPacingCatalog` |
| `ProductionRunFingerprintV1` | `RunFingerprint` |
| `ProductionRunRewardRuntimeV1` | `RunRewardLive` |
| `ProductionRunRewardSceneCompositionV1` | `RunRewardSceneSetup` |
| `ProductionRunStatInputResolutionV1` | `RunStatInputResolution` |
| `ProductionSceneTransitionCoordinatorV1` | `SceneTransitionFlow` |
| `ProductionStrongboxCatalogV1` | `StrongboxCatalog` |
| `ProductionStrongboxCatalogV1Tests` | `StrongboxCatalogTests` |
| `ProductionStrongboxHybridLootCatalogV1` | `StrongboxHybridLootCatalog` |
| `ProductionStrongboxOpeningBindingV1` | `StrongboxOpeningBinding` |
| `ProductionStrongboxTierSelectionCatalogV1` | `StrongboxTierSelectionCatalog` |
| `ProductionStrongboxTierV1` | `StrongboxTier` |
| `ProductionWeaponCatalogProvider` | `WeaponCatalogProvider` |
| `ProductionWeaponCatalogueProjectionV1` | `WeaponCatalogueView` |
| `ProductionWeaponCatalogueV1` | `WeaponCatalogue` |
| `ProductionWeaponFamilyV1` | `WeaponFamily` |
| `ProductionWeaponHoldingsAuthorityV2` | `WeaponHoldingsState` |
| `ProductionWeaponHoldingsMigrationV2` | `WeaponHoldingsMigration` |
| `ProductionWeaponInventoryStateV1` | `WeaponInventoryState` |
| `ProductionWeaponInventoryStateV2` | `WeaponInventoryState` |
| `ProductionWeaponMarkV1` | `WeaponMark` |
| `ProductionWeaponMountAvailabilityV1` | `WeaponMountAvailability` |
| `ProductionWeaponMountBindingV1` | `WeaponMountBinding` |
| `ProductionWeaponMountLayoutV1` | `WeaponMountLayout` |
| `ProductionWeaponMountLoadoutAuthorityV2` | `WeaponMountLoadoutState` |
| `ProductionWeaponMountLoadoutProjectionV2` | `WeaponMountLoadoutView` |
| `ProductionWeaponMountLoadoutRegistryV2` | `WeaponMountLoadoutRegistry` |
| `ProductionWeaponMountPolicyV1` | `WeaponMountPolicy` |
| `ProductionWeaponMountPolicyV1Tests` | `WeaponMountPolicyTests` |
| `ProductionWeaponMountPositionV1` | `WeaponMountPosition` |
| `ProductionWeaponMountSetV1` | `WeaponMountSet` |
| `ProductionWeaponOnboardingAndMigrationTests` | `WeaponOnboardingAndMigrationTests` |
| `ProductionWeaponOnboardingV1` | `WeaponOnboarding` |
| `ProductionWeaponOnboardingV2` | `WeaponOnboarding` |
| `ProjectileDtoV1` | `ProjectileDto` |
| `ProjectileExecutionPlanAdapter` | `ProjectileExecutionPlanBridge` |
| `ProjectileExplosionResolutionAdapter` | `ProjectileExplosionResolutionBridge` |
| `ProjectilePayloadDtoV1` | `ProjectilePayloadDto` |
| `PropCapabilitiesV1` | `PropCapabilities` |
| `PropCapabilityIdsV1` | `PropCapabilityIds` |
| `PropCapabilityRegistryV1` | `PropCapabilityRegistry` |
| `PropCapabilityV1` | `PropCapability` |
| `PropCatalogV1` | `PropCatalog` |
| `PropDamageAlignmentV1` | `PropDamageAlignment` |
| `PropDamageCommandV1` | `PropDamageCommand` |
| `PropDamageEligibilityContextV1` | `PropDamageEligibilityContext` |
| `PropDamageResultV1` | `PropDamageResult` |
| `PropDamageStatusV1` | `PropDamageStatus` |
| `PropDefinitionV1` | `PropDefinition` |
| `PropDestructibilityModeV1` | `PropDestructibilityMode` |
| `PropDestructionTerminalDropFactAdapterV1` | `PropDestructionTerminalDropFactBridge` |
| `PropFactBatchV1` | `PropFactBatch` |
| `PropFactIdentityV1` | `PropFactIdentity` |
| `PropFactKindIdsV1` | `PropFactKindIds` |
| `PropFingerprintV1` | `PropFingerprint` |
| `PropInteractionCommandV1` | `PropInteractionCommand` |
| `PropInteractionResultV1` | `PropInteractionResult` |
| `PropInteractionStatusV1` | `PropInteractionStatus` |
| `PropPlacementV1` | `PropPlacement` |
| `PropRuntimeCreationResultV1` | `PropLiveCreationResult` |
| `PropRuntimeCreationStatusV1` | `PropLiveCreationStatus` |
| `PropRuntimeFactoryV1` | `PropLiveFactory` |
| `PropRuntimeSnapshotV1` | `PropLiveSnapshot` |
| `PropRuntimeV1` | `PropLive` |
| `PropRuntimeV1Tests` | `PropLiveTests` |
| `PropTerminalDropFactConsumerV1` | `PropTerminalDropFactConsumer` |
| `PropTerminalFactV1` | `PropTerminalFact` |
| `PropTerminalSourceContextV1` | `PropTerminalSourceContext` |
| `PropTriggeredFactV1` | `PropTriggeredFact` |
| `PropsScaffoldDtoV2` | `PropsScaffoldDto` |
| `RangeAwareEnemyDecisionRuntimePolicyV1` | `RangeAwareEnemyDecisionLivePolicy` |
| `RankedSkillAllocationAuthorityV2` | `RankedSkillAllocationState` |
| `RankedSkillAllocationComponentCodecV1` | `RankedSkillAllocationComponentCodec` |
| `RankedSkillAllocationSnapshotV2` | `RankedSkillAllocationSnapshot` |
| `RankedSkillCatalogV2` | `RankedSkillCatalog` |
| `RankedSkillDefinitionV2` | `RankedSkillDefinition` |
| `RankedSkillFoundationV2Tests` | `RankedSkillFoundationTests` |
| `RankedSkillSampleCatalogV2` | `RankedSkillSampleCatalog` |
| `RankedSkillsPersistenceAdapterV2` | `RankedSkillsPersistenceBridge` |
| `RankedSkillsPersistenceResultV2` | `RankedSkillsPersistenceResult` |
| `RankedSkillsSceneControllerV2Tests` | `RankedSkillsSceneControllerTests` |
| `RankedSkillsScreenSessionV2` | `RankedSkillsScreenSession` |
| `RankedSkillsScreenSessionV2Tests` | `RankedSkillsScreenSessionTests` |
| `ReadinessRowV1` | `ReadinessRow` |
| `RealAuthoritySaveAdaptersV1Tests` | `RealStateSaveAdaptersTests` |
| `RecordingAdapter` | `RecordingBridge` |
| `RecordingChildAuthority` | `RecordingChildState` |
| `RecordingLevelSelectionRouteAdapterV1` | `RecordingLevelSelectionRouteBridge` |
| `RecordingLoadoutAuthority` | `RecordingLoadoutState` |
| `RecordingPlaySelectionRouteAdapterV1` | `RecordingPlaySelectionRouteBridge` |
| `RecordingRewardChildAuthority` | `RecordingRewardChildState` |
| `RecordingShopScreenRouteAdapterV1` | `RecordingShopScreenRouteBridge` |
| `RegisterRoomOccupantsCommandV1` | `RegisterRoomOccupantsCommand` |
| `RejectFirstApplyAuthority` | `RejectFirstApplyState` |
| `RejectFirstPreflightAuthority` | `RejectFirstPreflightState` |
| `RejectingCollectedRunEquipmentPayloadSourceV2` | `RejectingCollectedRunEquipmentPayloadSource` |
| `RejectingRuntime` | `RejectingLive` |
| `ReportRoomOccupantTerminalCommandV1` | `ReportRoomOccupantTerminalCommand` |
| `RequestEnemyAttackCapabilityAdapterV1` | `RequestEnemyAttackCapabilityBridge` |
| `RequiredCharacterComponentBackfillResultV1` | `RequiredCharacterComponentBackfillResult` |
| `RequiredCharacterComponentBackfillV1` | `RequiredCharacterComponentBackfill` |
| `RequiredCharacterComponentBackfillV1Tests` | `RequiredCharacterComponentBackfillTests` |
| `RestartRoomRuntimeCommandV1` | `RestartRoomLiveCommand` |
| `RestartRunSessionCommandV1` | `RestartRunSessionCommand` |
| `RestartStatusEffectLifecycleCommandV1` | `RestartStatusEffectLifecycleCommand` |
| `RetainedUnknownSaveComponentV1` | `RetainedUnknownSaveComponent` |
| `RetiredWeaponSaveMigrationResultV1` | `RetiredWeaponSaveMigrationResult` |
| `RetiredWeaponSaveMigrationV1` | `RetiredWeaponSaveMigration` |
| `RetryCollectedRunRewardTransferCommandV1` | `RetryCollectedRunRewardTransferCommand` |
| `RewardApplicationCanonicalV1` | `RewardApplication` |
| `RewardApplicationImportResultV1` | `RewardApplicationImportResult` |
| `RewardApplicationImportStatusV1` | `RewardApplicationImportStatus` |
| `RewardApplicationResultStatusV1` | `RewardApplicationResultStatus` |
| `RewardApplicationResultV1` | `RewardApplicationResult` |
| `RewardApplicationServiceV1` | `RewardApplicationActions` |
| `RewardApplicationServiceV1Tests` | `RewardApplicationActionsTests` |
| `RewardApplicationSnapshotV1` | `RewardApplicationSnapshot` |
| `RewardAuthorityAdapterOrderingV1` | `RewardStateBridgeOrdering` |
| `RewardAuthorityAdmissionStatusV1` | `RewardStateAdmissionStatus` |
| `RewardAuthorityDecorator` | `RewardStateDecorator` |
| `RewardAuthorityPreflightFactV1` | `RewardStatePreflightFact` |
| `RewardAuthorityPreflightResultV1` | `RewardStatePreflightResult` |
| `RewardBoxPacingModeV1` | `RewardBoxPacingMode` |
| `RewardCancelCommandV1` | `RewardCancelCommand` |
| `RewardChildApplicationSnapshotV1` | `RewardChildApplicationSnapshot` |
| `RewardChildApplyResultV1` | `RewardChildApplyResult` |
| `RewardChildApplyStatusV1` | `RewardChildApplyStatus` |
| `RewardChildGrantCommandV1` | `RewardChildGrantCommand` |
| `RewardChildResolutionStateV1` | `RewardChildResolutionState` |
| `RewardClaimCommandV1` | `RewardClaimCommand` |
| `RewardCommitCommandV1` | `RewardCommitCommand` |
| `RewardCommitmentSnapshotV1` | `RewardCommitmentSnapshot` |
| `RewardCommitmentStateV1` | `RewardCommitmentState` |
| `RewardContextOverrideResolutionV1` | `RewardContextOverrideResolution` |
| `RewardContractFormatV1` | `RewardContractFormat` |
| `RewardGenerationFingerprintV1` | `RewardGenerationFingerprint` |
| `RewardGenerationRequestV1` | `RewardGenerationRequest` |
| `RewardGenerationResultEnvelopeV1` | `RewardGenerationResultEnvelope` |
| `RewardGenerationScalingValueV1` | `RewardGenerationScalingValue` |
| `RewardGenerationServiceV1` | `RewardGenerationActions` |
| `RewardGenerationServiceV1Tests` | `RewardGenerationActionsTests` |
| `RewardGenerationStatusV1` | `RewardGenerationStatus` |
| `RewardGenerationTraceDecisionV1` | `RewardGenerationTraceDecision` |
| `RewardGenerationTraceEntryV1` | `RewardGenerationTraceEntry` |
| `RewardGenerationTraceV1` | `RewardGenerationTrace` |
| `RewardGrantApplicationPayloadV1` | `RewardGrantApplicationPayload` |
| `RewardGrantHandlerRegistryV1` | `RewardGrantHandlerRegistry` |
| `RewardGrantKindV1` | `RewardGrantKind` |
| `RewardGrantSpecificationV1` | `RewardGrantSpecification` |
| `RewardGrantV1` | `RewardGrant` |
| `RewardModelFormatV1` | `RewardModelFormat` |
| `RewardOperationIdentityComparisonV1` | `RewardOperationIdentityComparison` |
| `RewardOperationIdentityV1` | `RewardOperationIdentity` |
| `RewardOperationRequestV1` | `RewardOperationRequest` |
| `RewardOutcomeDispositionV1` | `RewardOutcomeDisposition` |
| `RewardOutcomeV1` | `RewardOutcome` |
| `RewardPickupApplicationAuthority2D` | `RewardPickupApplicationState2D` |
| `RewardPickupCategoryMapV1` | `RewardPickupCategoryMap` |
| `RewardPickupCategoryV1` | `RewardPickupCategory` |
| `RewardPickupCollectResultV1` | `RewardPickupCollectResult` |
| `RewardPickupCollectStatusV1` | `RewardPickupCollectStatus` |
| `RewardPickupPayloadBuilderV1` | `RewardPickupPayloadBuilder` |
| `RewardPickupPayloadV1` | `RewardPickupPayload` |
| `RewardPickupPresentationStyleV1` | `RewardPickupPresentationStyle` |
| `RewardPickupSpawnResultV1` | `RewardPickupSpawnResult` |
| `RewardPickupSpawnStatusV1` | `RewardPickupSpawnStatus` |
| `RewardProfileCatalogResolverV1` | `RewardProfileCatalogResolver` |
| `RewardProfileDispositionV1` | `RewardProfileDisposition` |
| `RewardProfileOverrideOperationV1` | `RewardProfileOverrideOperation` |
| `RewardProfileOverrideV1` | `RewardProfileOverride` |
| `RewardProfileResolutionV1` | `RewardProfileResolution` |
| `RewardProfileResolverV1` | `RewardProfileResolver` |
| `RewardProfileV1` | `RewardProfile` |
| `RewardProjectCommandV1` | `RewardProjectCommand` |
| `RewardQuantityRangeV1` | `RewardQuantityRange` |
| `RewardResultDispositionV1` | `RewardResultDisposition` |
| `RewardResultV1` | `RewardResult` |
| `RewardRetryClaimCommandV1` | `RewardRetryClaimCommand` |
| `RewardRollGroupBehaviorV1` | `RewardRollGroupBehavior` |
| `RewardRollGroupV1` | `RewardRollGroup` |
| `RewardScalingInputDescriptorV1` | `RewardScalingInputDescriptor` |
| `RewardScalingInputKindV1` | `RewardScalingInputKind` |
| `RewardSimulationParticipantInputV1` | `RewardSimulationParticipantInput` |
| `RewardSimulationParticipantReportV1` | `RewardSimulationParticipantReport` |
| `RewardSimulationReportV1` | `RewardSimulationReport` |
| `RewardSourceOverrideModeV1` | `RewardSourceOverrideMode` |
| `RewardSourceOverrideV1` | `RewardSourceOverride` |
| `RewardSourceProfileV1` | `RewardSourceProfile` |
| `RewardTraceDecisionKindV1` | `RewardTraceDecisionKind` |
| `RewardTraceEntryV1` | `RewardTraceEntry` |
| `RewardTraceV1` | `RewardTrace` |
| `RoomAccessAuthorityV1` | `RoomAccessState` |
| `RoomAccessAuthorityV1Tests` | `RoomAccessStateTests` |
| `RoomAccessConditionDefinitionV1` | `RoomAccessConditionDefinition` |
| `RoomAccessConditionKindV1` | `RoomAccessConditionKind` |
| `RoomAccessDefinitionV1` | `RoomAccessDefinition` |
| `RoomAccessFactSnapshotV1` | `RoomAccessFactSnapshot` |
| `RoomAccessImportIssueV1` | `RoomAccessImportIssue` |
| `RoomAccessImportResultV1` | `RoomAccessImportResult` |
| `RoomAccessJsonImporterV1` | `RoomAccessJsonImporter` |
| `RoomAccessJsonImporterV1Tests` | `RoomAccessJsonImporterTests` |
| `RoomAccessOperationResultV1` | `RoomAccessOperationResult` |
| `RoomAccessOperationStatusV1` | `RoomAccessOperationStatus` |
| `RoomAccessReferenceCatalogV1` | `RoomAccessReferenceCatalog` |
| `RoomAccessReferenceKindV1` | `RoomAccessReferenceKind` |
| `RoomAccessReferenceRegistrationV1` | `RoomAccessReferenceRegistration` |
| `RoomAccessReferenceSourceV1` | `RoomAccessReferenceSource` |
| `RoomAccessSnapshotV1` | `RoomAccessSnapshot` |
| `RoomAvailabilityStateV1` | `RoomAvailabilityState` |
| `RoomBoundsV1` | `RoomBounds` |
| `RoomClearTransitionV1` | `RoomClearTransition` |
| `RoomCompletionConditionDefinitionV1` | `RoomCompletionConditionDefinition` |
| `RoomCompletionConditionKindV1` | `RoomCompletionConditionKind` |
| `RoomCompletionEvaluationV1` | `RoomCompletionEvaluation` |
| `RoomCompletionEvaluatorV1` | `RoomCompletionEvaluator` |
| `RoomConnectionDefinitionV1` | `RoomConnectionDefinition` |
| `RoomConnectionDirectionalityV1` | `RoomConnectionDirectionality` |
| `RoomContentBundleV1` | `RoomContentBundle` |
| `RoomContentImportIssueV1` | `RoomContentImportIssue` |
| `RoomContentImportResultV1` | `RoomContentImportResult` |
| `RoomContentJsonImporterV1` | `RoomContentJsonImporter` |
| `RoomContentJsonImporterV1Tests` | `RoomContentJsonImporterTests` |
| `RoomContentJsonPackageV1` | `RoomContentJsonPackage` |
| `RoomContentObjectCatalogV1` | `RoomContentObjectCatalog` |
| `RoomContentObjectDefinitionV1` | `RoomContentObjectDefinition` |
| `RoomContentObjectKindV1` | `RoomContentObjectKind` |
| `RoomContentVisualLayerV1` | `RoomContentVisualLayer` |
| `RoomDefinitionV1` | `RoomDefinition` |
| `RoomDoorAccessDefinitionV1` | `RoomDoorAccessDefinition` |
| `RoomDoorAccessProjectionV1` | `RoomDoorAccessView` |
| `RoomDoorDefinitionV1` | `RoomDoorDefinition` |
| `RoomDoorGatePolicyV1` | `RoomDoorGatePolicy` |
| `RoomDoorLinkDefinitionV1` | `RoomDoorLinkDefinition` |
| `RoomDtoV2` | `RoomDto` |
| `RoomEnemyAttackPresentationPortV1` | `RoomEnemyAttackPresentationPort` |
| `RoomEnemyPlacementContentV1` | `RoomEnemyPlacementContent` |
| `RoomEntryDefinitionV1` | `RoomEntryDefinition` |
| `RoomExitDefinitionV1` | `RoomExitDefinition` |
| `RoomExitEligibilityProjectionV1` | `RoomExitEligibilityView` |
| `RoomExitLinkDefinitionV1` | `RoomExitLinkDefinition` |
| `RoomExitRuntimeStateV1` | `RoomExitLiveState` |
| `RoomExitStateSnapshotV1` | `RoomExitStateSnapshot` |
| `RoomExitTypeV1` | `RoomExitType` |
| `RoomGraphDefinitionV1` | `RoomGraphDefinition` |
| `RoomGraphFormatV1` | `RoomGraphFormat` |
| `RoomGraphImportResultV1` | `RoomGraphImportResult` |
| `RoomGraphImportStatusV1` | `RoomGraphImportStatus` |
| `RoomGraphOperationResultV1` | `RoomGraphOperationResult` |
| `RoomGraphOperationStatusV1` | `RoomGraphOperationStatus` |
| `RoomGraphSnapshotV1` | `RoomGraphSnapshot` |
| `RoomGraphValidationCodeV1` | `RoomGraphValidationCode` |
| `RoomGraphValidationIssueV1` | `RoomGraphValidationIssue` |
| `RoomGraphValidationResultV1` | `RoomGraphValidationResult` |
| `RoomHoldingConsumeCommandV1` | `RoomHoldingConsumeCommand` |
| `RoomHoldingConsumeResultV1` | `RoomHoldingConsumeResult` |
| `RoomHoldingConsumeStatusV1` | `RoomHoldingConsumeStatus` |
| `RoomIdentityDtoV2` | `RoomIdentityDto` |
| `RoomIndexDtoV2` | `RoomIndexDto` |
| `RoomInitialAvailabilityV1` | `RoomInitialAvailability` |
| `RoomLiveAccessFactProjectionV1` | `RoomLiveAccessFactView` |
| `RoomLiveJsonV1` | `RoomLiveJson` |
| `RoomLiveLinkKindV1` | `RoomLiveLinkKind` |
| `RoomLiveOperationResultV1` | `RoomLiveOperationResult` |
| `RoomLiveOperationStatusV1` | `RoomLiveOperationStatus` |
| `RoomLivePlacementKindV1` | `RoomLivePlacementKind` |
| `RoomLiveProjectionBuilderV1` | `RoomLiveViewBuilder` |
| `RoomLiveRoomProjectionV1` | `RoomLiveRoomView` |
| `RoomLiveRuntimeAuthorityTests` | `RoomLiveStateTests` |
| `RoomLiveRuntimeAuthorityV1` | `RoomLiveState` |
| `RoomLiveRuntimeProjectionV1` | `RoomLiveView` |
| `RoomMissionLayoutV1` | `RoomMissionLayout` |
| `RoomOccupancyProjectionV1` | `RoomOccupancyView` |
| `RoomOccupantClearRoleV1` | `RoomOccupantClearRole` |
| `RoomOccupantProjectionV1` | `RoomOccupantView` |
| `RoomOccupantRegistrationV1` | `RoomOccupantRegistration` |
| `RoomOperationInspectionV1` | `RoomOperationInspection` |
| `RoomOperationJournalV1` | `RoomOperationJournal` |
| `RoomPlacedEntityDefinitionV1` | `RoomPlacedEntityDefinition` |
| `RoomProjectionIdentity` | `RoomViewIdentity` |
| `RoomProjectionKey` | `RoomViewKey` |
| `RoomProjectionLifecycle` | `RoomViewLifecycle` |
| `RoomProjectionLifecycleOperation` | `RoomViewLifecycleOperation` |
| `RoomProjectionLifecyclePhase` | `RoomViewLifecyclePhase` |
| `RoomProjectionReadResult` | `RoomViewReadResult` |
| `RoomProjectionReadStatus` | `RoomViewReadStatus` |
| `RoomProjectionServices` | `RoomViewServices` |
| `RoomProjectionTransition` | `RoomViewTransition` |
| `RoomProjectionTransitionKind` | `RoomViewTransitionKind` |
| `RoomProjectionTransitionRejection` | `RoomViewTransitionRejection` |
| `RoomPropPlacementContentV1` | `RoomPropPlacementContent` |
| `RoomRetainedFactStoreV1` | `RoomRetainedFactStore` |
| `RoomRunHoldingSnapshotV1` | `RoomRunHoldingSnapshot` |
| `RoomRuntimeAuthorityTests` | `RoomLiveStateTests` |
| `RoomRuntimeAuthorityV1` | `RoomLiveState` |
| `RoomRuntimeComposition2D` | `RoomLiveSetup2D` |
| `RoomRuntimeOperationResultV1` | `RoomLiveOperationResult` |
| `RoomRuntimeOperationStatusV1` | `RoomLiveOperationStatus` |
| `RoomRuntimeProjectionV1` | `RoomLiveView` |
| `RoomRuntimeStateV1` | `RoomLiveState` |
| `RoomSpawnPointDefinitionV1` | `RoomSpawnPointDefinition` |
| `RoomSpawnPointKindV1` | `RoomSpawnPointKind` |
| `RoomStateSnapshotV1` | `RoomStateSnapshot` |
| `RoomTraversalCoordinatorV1` | `RoomTraversalFlow` |
| `RoomTraversalResultV1` | `RoomTraversalResult` |
| `RoomVector2V1` | `RoomVector2` |
| `RoomVisualPlacementContentV1` | `RoomVisualPlacementContent` |
| `RunCheckpointV1` | `RunCheckpoint` |
| `RunCombatProfileInputV1` | `RunCombatProfileInput` |
| `RunCombatProfileV1` | `RunCombatProfile` |
| `RunConditionAdvanceCommandV1` | `RunConditionAdvanceCommand` |
| `RunConditionAdvanceResultV1` | `RunConditionAdvanceResult` |
| `RunConditionAdvanceStatusV1` | `RunConditionAdvanceStatus` |
| `RunConditionBindingV1Tests` | `RunConditionBindingTests` |
| `RunConditionCheckpointV1` | `RunConditionCheckpoint` |
| `RunConditionCheckpointV1Tests` | `RunConditionCheckpointTests` |
| `RunConditionDeliveryResultV1` | `RunConditionDeliveryResult` |
| `RunConditionDeliveryStatusV1` | `RunConditionDeliveryStatus` |
| `RunConditionGameplayFactCommandV1` | `RunConditionGameplayFactCommand` |
| `RunConditionHashV1` | `RunConditionHash` |
| `RunConditionParticipantSeedV1` | `RunConditionParticipantSeed` |
| `RunConditionParticipantSnapshotV1` | `RunConditionParticipantSnapshot` |
| `RunConditionRestartAtomicityV1Tests` | `RunConditionRestartAtomicityTests` |
| `RunConditionRuntimeSnapshotV1` | `RunConditionLiveSnapshot` |
| `RunDebugBoxFactV1` | `RunDebugBoxFact` |
| `RunDebugBoxPlanV1` | `RunDebugBoxPlan` |
| `RunDebugBuildGuardV1` | `RunDebugBuildGuard` |
| `RunDebugEndResultV1` | `RunDebugEndResult` |
| `RunDebugPanelSessionV1` | `RunDebugPanelSession` |
| `RunDebugPlannerV1` | `RunDebugPlanner` |
| `RunDebugSnapshotV1` | `RunDebugSnapshot` |
| `RunDebugSpawnBatchResultV1` | `RunDebugSpawnBatchResult` |
| `RunDebugSpawnBatchStatusV1` | `RunDebugSpawnBatchStatus` |
| `RunDebugSpawnRequestV1` | `RunDebugSpawnRequest` |
| `RunDropPacingPolicyV1` | `RunDropPacingPolicy` |
| `RunHudSnapshotV1` | `RunHudSnapshot` |
| `RunLocalMutationCommandV1` | `RunLocalMutationCommand` |
| `RunLocalMutationKindV1` | `RunLocalMutationKind` |
| `RunLocalMutationResultV1` | `RunLocalMutationResult` |
| `RunLocalPickupAuthorityV1` | `RunLocalPickupState` |
| `RunLocalPickupAuthorityV1Tests` | `RunLocalPickupStateTests` |
| `RunLocalStateSnapshotV1` | `RunLocalStateSnapshot` |
| `RunLootTotalsPresentationV1` | `RunLootTotalsPresentation` |
| `RunLootTotalsProjectorV1` | `RunLootTotalsProjector` |
| `RunMissionStrongboxSnapshotSourceResolverV1` | `RunMissionStrongboxSnapshotSourceResolver` |
| `RunPickupAuthorityHost2D` | `RunPickupStateHost2D` |
| `RunPickupCanonicalV1` | `RunPickup` |
| `RunPickupCollectionCommandV1` | `RunPickupCollectionCommand` |
| `RunPickupCollectionFactV1` | `RunPickupCollectionFact` |
| `RunPickupCollectionResultV1` | `RunPickupCollectionResult` |
| `RunPickupCollectionStatusV1` | `RunPickupCollectionStatus` |
| `RunPickupGeneratedBatchV1` | `RunPickupGeneratedBatch` |
| `RunPickupGeneratedRewardV1` | `RunPickupGeneratedReward` |
| `RunPickupIdentityV1` | `RunPickupIdentity` |
| `RunPickupLiveCompositionV1` | `RunPickupLiveSetup` |
| `RunPickupPresentationEntryV1` | `RunPickupPresentationEntry` |
| `RunPickupPresentationSyncResultV1` | `RunPickupPresentationSyncResult` |
| `RunPickupRealizationResultV1` | `RunPickupRealizationResult` |
| `RunPickupRealizationStatusV1` | `RunPickupRealizationStatus` |
| `RunPickupRunSessionContextV1` | `RunPickupRunSessionContext` |
| `RunPickupSessionRecordResultV1` | `RunPickupSessionRecordResult` |
| `RunPickupSessionRecordStatusV1` | `RunPickupSessionRecordStatus` |
| `RunPickupSnapshotV1` | `RunPickupSnapshot` |
| `RunPickupStateV1` | `RunPickupState` |
| `RunPickupWorldSpawnContextV1` | `RunPickupWorldSpawnContext` |
| `RunPlayerRuntimeSnapshotV1` | `RunPlayerLiveSnapshot` |
| `RunRecoveryDiagnosticSnapshotV1` | `RunRecoveryDiagnosticSnapshot` |
| `RunRestartPolicyV1` | `RunRestartPolicy` |
| `RunRewardEnvironmentSnapshotV1` | `RunRewardEnvironmentSnapshot` |
| `RunRewardParticipantStateV1` | `RunRewardParticipantState` |
| `RunRewardRuntimeSnapshotV1` | `RunRewardLiveSnapshot` |
| `RunRewardRuntimeSnapshotV1Tests` | `RunRewardLiveSnapshotTests` |
| `RunRuntimePortRestartResultV1` | `RunLivePortRestartResult` |
| `RunSessionAggregateV1` | `RunSessionAggregate` |
| `RunSessionAuthorityV1` | `RunSessionState` |
| `RunSessionAuthorityV1Tests` | `RunSessionStateTests` |
| `RunSessionCollectedRewardV1` | `RunSessionCollectedReward` |
| `RunSessionDurableAcceptanceResultV1` | `RunSessionDurableAcceptanceResult` |
| `RunSessionDurableAcceptanceStatusV1` | `RunSessionDurableAcceptanceStatus` |
| `RunSessionDurableEndStateV1` | `RunSessionDurableEndState` |
| `RunSessionDurableEndV1Tests` | `RunSessionDurableEndTests` |
| `RunSessionEndReceiptV1` | `RunSessionEndReceipt` |
| `RunSessionEndResultV1` | `RunSessionEndResult` |
| `RunSessionEndStatusV1` | `RunSessionEndStatus` |
| `RunSessionEnemyAttackPatternTimeV1` | `RunSessionEnemyAttackPatternTime` |
| `RunSessionFactAdmissionResultV1` | `RunSessionFactAdmissionResult` |
| `RunSessionFactAdmissionStatusV1` | `RunSessionFactAdmissionStatus` |
| `RunSessionFactEnvelopeV1` | `RunSessionFactEnvelope` |
| `RunSessionFactKindV1` | `RunSessionFactKind` |
| `RunSessionFingerprintV1` | `RunSessionFingerprint` |
| `RunSessionLifecycleStateV1` | `RunSessionLifecycleState` |
| `RunSessionNonConditionRuntimePortsV1` | `RunSessionNonConditionLivePorts` |
| `RunSessionParticipantDropPacingStateStoreV1` | `RunSessionParticipantDropPacingStateStore` |
| `RunSessionPersonalRewardDeliveryOutboxV1` | `RunSessionPersonalRewardDeliveryOutbox` |
| `RunSessionRestartResultV1` | `RunSessionRestartResult` |
| `RunSessionRestartStatusV1` | `RunSessionRestartStatus` |
| `RunSessionRewardCollectionResultV1` | `RunSessionRewardCollectionResult` |
| `RunSessionRewardCollectionStatusV1` | `RunSessionRewardCollectionStatus` |
| `RunSessionRuntimePortsV1` | `RunSessionLivePorts` |
| `RunSessionStartMaterialV1` | `RunSessionStartMaterial` |
| `RunSessionStartResultV1` | `RunSessionStartResult` |
| `RunSessionStartStatusV1` | `RunSessionStartStatus` |
| `RunSessionTerminalDropContextResolverV1` | `RunSessionTerminalDropContextResolver` |
| `RunSessionTerminalRewardEnvironmentResolverV1` | `RunSessionTerminalRewardEnvironmentResolver` |
| `RunSessionTerminalRewardOverrideResolverV1` | `RunSessionTerminalRewardOverrideResolver` |
| `RunSessionTerminalRewardParticipantResolverV1` | `RunSessionTerminalRewardParticipantResolver` |
| `RunSessionTimeAdvanceResultV1` | `RunSessionTimeAdvanceResult` |
| `RunSessionTimeAdvanceStatusV1` | `RunSessionTimeAdvanceStatus` |
| `RunStrongboxCollectionRequestV1` | `RunStrongboxCollectionRequest` |
| `RuntimeBalanceScenarioV1` | `LiveBalanceScenario` |
| `RuntimeBoundsDto` | `LiveBoundsDto` |
| `RuntimeBox` | `LiveBox` |
| `RuntimeConditionActivationFactV1` | `LiveConditionActivationFact` |
| `RuntimeModifierDefinitionV1` | `LiveModifierDefinition` |
| `RuntimeModifierEvaluationV1` | `LiveModifierEvaluation` |
| `RuntimeModifierFingerprintV1` | `LiveModifierFingerprint` |
| `RuntimeModifierFoundationV1Tests` | `LiveModifierFoundationTests` |
| `RuntimeModifierOperationV1` | `LiveModifierOperation` |
| `RuntimeModifierSnapshotV1` | `LiveModifierSnapshot` |
| `RuntimeObservedFactResultV1` | `LiveObservedFactResult` |
| `RuntimeObservedFactStatusV1` | `LiveObservedFactStatus` |
| `RuntimeObservedFactV1` | `LiveObservedFact` |
| `RuntimeReferenceWeaponDefinitionIdResolver` | `LiveReferenceWeaponDefinitionIdResolver` |
| `RuntimeSpawnIdentityInput` | `LiveSpawnIdentityInput` |
| `RuntimeTypes` | `LiveTypes` |
| `SaveAuthorityFingerprintV1` | `SaveStateFingerprint` |
| `SaveComponentApplyResultV1` | `SaveComponentApplyResult` |
| `SaveComponentCommitResultV1` | `SaveComponentCommitResult` |
| `SaveComponentCommitStatusV1` | `SaveComponentCommitStatus` |
| `SaveComponentDefinitionV1` | `SaveComponentDefinition` |
| `SaveComponentPrepareResultV1` | `SaveComponentPrepareResult` |
| `SaveComponentRollbackResultV1` | `SaveComponentRollbackResult` |
| `SaveComponentSnapshotV1` | `SaveComponentSnapshot` |
| `SaveComponentValidationResultV1` | `SaveComponentValidationResult` |
| `SaveComponentValidationStatusV1` | `SaveComponentValidationStatus` |
| `SavePersistenceLimitsV1` | `SavePersistenceLimits` |
| `ScalarEnemyDifficultyScalingPolicyV1` | `ScalarEnemyDifficultyScalingPolicy` |
| `ScrapChangeFactV1` | `ScrapChangeFact` |
| `ScrapFingerprintV1` | `ScrapFingerprint` |
| `ScrapIdentityV1` | `ScrapIdentity` |
| `ScrapLedgerPayloadV1` | `ScrapLedgerPayload` |
| `ScrapLedgerPayloadV1Tests` | `ScrapLedgerPayloadTests` |
| `ScrapMutationKindV1` | `ScrapMutationKind` |
| `ScrapProvenanceV1` | `ScrapProvenance` |
| `ScrapRewardChildAuthorityV1` | `ScrapRewardChildState` |
| `ScrapSnapshotImportResultV1` | `ScrapSnapshotImportResult` |
| `ScrapSnapshotV1` | `ScrapSnapshot` |
| `ScrapTransactionCommandV1` | `ScrapTransactionCommand` |
| `ScrapTransactionResultV1` | `ScrapTransactionResult` |
| `ScrapWalletComponentCodecV1` | `ScrapWalletComponentCodec` |
| `ScrapWalletServiceV1` | `ScrapWalletActions` |
| `ScrapWalletServiceV1Tests` | `ScrapWalletActionsTests` |
| `SelectedPlayerRunConditionParticipantSeedProviderV1` | `SelectedPlayerRunConditionParticipantSeedProvider` |
| `SharedStrongboxRewardGeneratorV1` | `SharedStrongboxRewardGenerator` |
| `ShootingPatternDtoV1` | `ShootingPatternDto` |
| `ShopAugmentCandidateAuthoringV1` | `ShopAugmentCandidateAuthoring` |
| `ShopCanonicalV1` | `Shop` |
| `ShopDefinitionAssetBuildResultV1` | `ShopDefinitionAssetBuildResult` |
| `ShopDefinitionV1` | `ShopDefinition` |
| `ShopEquipmentCandidateAuthoringV1` | `ShopEquipmentCandidateAuthoring` |
| `ShopInventoryOpenResultV1` | `ShopInventoryOpenResult` |
| `ShopInventoryOpenStatusV1` | `ShopInventoryOpenStatus` |
| `ShopInventoryViewV1` | `ShopInventoryView` |
| `ShopLockCapacityQueryV1` | `ShopLockCapacityQuery` |
| `ShopNavigationAdapter` | `ShopNavigationBridge` |
| `ShopPricingPolicyAuthoringV1` | `ShopPricingPolicyAuthoring` |
| `ShopPricingPolicyV1` | `ShopPricingPolicy` |
| `ShopProgressionContextPolicyV1` | `ShopProgressionContextPolicy` |
| `ShopPurchaseCommandV1` | `ShopPurchaseCommand` |
| `ShopPurchaseFactV1` | `ShopPurchaseFact` |
| `ShopPurchaseStatusV1` | `ShopPurchaseStatus` |
| `ShopQualityCandidateAuthoringV1` | `ShopQualityCandidateAuthoring` |
| `ShopRefreshCommandV1` | `ShopRefreshCommand` |
| `ShopRefreshFactV1` | `ShopRefreshFact` |
| `ShopRefreshPolicyV1` | `ShopRefreshPolicy` |
| `ShopRefreshStatusV1` | `ShopRefreshStatus` |
| `ShopRunInventorySnapshotV1` | `ShopRunInventorySnapshot` |
| `ShopRuntimeServiceV1` | `ShopLiveActions` |
| `ShopRuntimeServiceV1Tests` | `ShopLiveActionsTests` |
| `ShopRuntimeSnapshotV1` | `ShopLiveSnapshot` |
| `ShopScreenActionResultV1` | `ShopScreenActionResult` |
| `ShopScreenActionStatusV1` | `ShopScreenActionStatus` |
| `ShopScreenControllerV1` | `ShopScreenController` |
| `ShopScreenControllerV1Tests` | `ShopScreenControllerTests` |
| `ShopScreenFeedbackKindV1` | `ShopScreenFeedbackKind` |
| `ShopScreenPresentationV1Tests` | `ShopScreenPresentationTests` |
| `ShopScreenProjectionV1` | `ShopScreenView` |
| `ShopScreenPurchaseInputV1` | `ShopScreenPurchaseInput` |
| `ShopScreenRouteResultV1` | `ShopScreenRouteResult` |
| `ShopScreenRouteV1` | `ShopScreenRoute` |
| `ShopScreenRuntimeHandoffV1` | `ShopScreenLiveHandoff` |
| `ShopScreenSessionV1` | `ShopScreenSession` |
| `ShopScreenStockCardV1` | `ShopScreenStockCard` |
| `ShopStockEntryStateV1` | `ShopStockEntryState` |
| `ShopStockEntryV1` | `ShopStockEntry` |
| `SkillAllocationMigratorV2` | `SkillAllocationMigrator` |
| `SkillAllocationRejectionV2` | `SkillAllocationRejection` |
| `SkillAllocationResultV2` | `SkillAllocationResult` |
| `SkillCatalogV1` | `SkillCatalog` |
| `SkillCategoryInvestmentRequirementV1` | `SkillCategoryInvestmentRequirement` |
| `SkillCategoryInvestmentV1` | `SkillCategoryInvestment` |
| `SkillCategoryKeyV1` | `SkillCategoryKey` |
| `SkillClassOverrideV2` | `SkillClassOverride` |
| `SkillDefinitionV1` | `SkillDefinition` |
| `SkillEffectContributionV2` | `SkillEffectContribution` |
| `SkillEffectDescriptorV2` | `SkillEffectDescriptor` |
| `SkillEffectModifierAdapterV1` | `SkillEffectModifierBridge` |
| `SkillEffectProjectorV2` | `SkillEffectProjector` |
| `SkillEffectSnapshotV2` | `SkillEffectSnapshot` |
| `SkillFingerprintV2` | `SkillFingerprint` |
| `SkillMigrationResultV2` | `SkillMigrationResult` |
| `SkillModifierKindV2` | `SkillModifierKind` |
| `SkillMutationFactV1` | `SkillMutationFact` |
| `SkillMutationStatusV1` | `SkillMutationStatus` |
| `SkillPrerequisiteV1` | `SkillPrerequisite` |
| `SkillProgressionAuthorityTests` | `SkillProgressionStateTests` |
| `SkillProgressionAuthorityV1` | `SkillProgressionState` |
| `SkillProgressionSnapshotV1` | `SkillProgressionSnapshot` |
| `SkillRankMilestoneV2` | `SkillRankMilestone` |
| `SkillRejectionReasonV1` | `SkillRejectionReason` |
| `SkillRespecOrchestratorV2` | `SkillRespecOrchestrator` |
| `SkillRespecPaymentResultV2` | `SkillRespecPaymentResult` |
| `SkillRespecQuoteV2` | `SkillRespecQuote` |
| `SkillRespecReceiptV2` | `SkillRespecReceipt` |
| `SkillRespecRejectionV2` | `SkillRespecRejection` |
| `SkillRuntimeReconciliationV2` | `SkillLiveReconciliation` |
| `SkillSynergyDefinitionV2` | `SkillSynergyDefinition` |
| `SkillSynergyRequirementV2` | `SkillSynergyRequirement` |
| `SkillTreeDefinitionV1` | `SkillTreeDefinition` |
| `SkillsHubDestinationAdapterV1` | `SkillsHubDestinationBridge` |
| `SkillsNavigationAdapter` | `SkillsNavigationBridge` |
| `SkillsScreenAllocationResultV1` | `SkillsScreenAllocationResult` |
| `SkillsScreenBackResultV1` | `SkillsScreenBackResult` |
| `SkillsScreenProjectionV1` | `SkillsScreenView` |
| `SkillsScreenSessionV1` | `SkillsScreenSession` |
| `SkillsScreenSkillProjectionV1` | `SkillsScreenSkillView` |
| `SkillsScreenSkillStateV1` | `SkillsScreenSkillState` |
| `SnapshotAbilityRunPortV1` | `SnapshotAbilityRunPort` |
| `SnapshotConditionalRunPortV1` | `SnapshotConditionalRunPort` |
| `SnapshotPlayerRunPortV1` | `SnapshotPlayerRunPort` |
| `SnapshotRoomRunPortV1` | `SnapshotRoomRunPort` |
| `SnapshotStatusRunPortV1` | `SnapshotStatusRunPort` |
| `SnapshotWeaponRunPortV1` | `SnapshotWeaponRunPort` |
| `SpecialEventCatalogV1` | `SpecialEventCatalog` |
| `SpecialEventConflictV1` | `SpecialEventConflict` |
| `SpecialEventDefinitionV1` | `SpecialEventDefinition` |
| `SpecialEventOverlapModeV1` | `SpecialEventOverlapMode` |
| `SpriteAnimationCombatDeathVfxDefinitionV1` | `SpriteAnimationCombatDeathVfxDefinition` |
| `StackHoldingSnapshotV1` | `StackHoldingSnapshot` |
| `StartRunSessionCommandV1` | `StartRunSessionCommand` |
| `StatusEffectAuthoritySnapshotV1` | `StatusEffectStateSnapshot` |
| `StatusEffectAuthorityV1` | `StatusEffectState` |
| `StatusEffectAuthorityV1Tests` | `StatusEffectStateTests` |
| `StatusEffectCatalogV1` | `StatusEffectCatalog` |
| `StatusEffectCommandActionV1` | `StatusEffectCommandAction` |
| `StatusEffectCommandCanonicalV1` | `StatusEffectCommand` |
| `StatusEffectCommandResultV1` | `StatusEffectCommandResult` |
| `StatusEffectCommandStatusV1` | `StatusEffectCommandStatus` |
| `StatusEffectCommandV1` | `StatusEffectCommand` |
| `StatusEffectDefinitionV1` | `StatusEffectDefinition` |
| `StatusEffectFingerprintV1` | `StatusEffectFingerprint` |
| `StatusEffectLocalHashV1` | `StatusEffectLocalHash` |
| `StatusEffectReplayRecordSnapshotV1` | `StatusEffectReplayRecordSnapshot` |
| `StatusEffectStackingPolicyV1` | `StatusEffectStackingPolicy` |
| `StatusEffectStateSnapshotV1` | `StatusEffectStateSnapshot` |
| `StrongboxAugmentSignatureV1` | `StrongboxAugmentSignature` |
| `StrongboxCanonicalV1` | `Strongbox` |
| `StrongboxDefinitionCatalogV1` | `StrongboxDefinitionCatalog` |
| `StrongboxDefinitionRarityIdsV1` | `StrongboxDefinitionRarityIds` |
| `StrongboxDefinitionSetV1` | `StrongboxDefinitionSet` |
| `StrongboxDefinitionV1` | `StrongboxDefinition` |
| `StrongboxDistanceWeightV1` | `StrongboxDistanceWeight` |
| `StrongboxDurableOpeningCoordinatorV1` | `StrongboxDurableOpeningFlow` |
| `StrongboxEquipmentGenerationDefinitionCatalogV1` | `StrongboxEquipmentGenerationDefinitionCatalog` |
| `StrongboxEquipmentGenerationDefinitionV1` | `StrongboxEquipmentGenerationDefinition` |
| `StrongboxEquipmentGenerationResolverV1` | `StrongboxEquipmentGenerationResolver` |
| `StrongboxEquipmentRollPlanV1` | `StrongboxEquipmentRollPlan` |
| `StrongboxGeneratedOutcomeV1` | `StrongboxGeneratedOutcome` |
| `StrongboxGrantPayloadResolutionV1` | `StrongboxGrantPayloadResolution` |
| `StrongboxGroupingProjectorV1` | `StrongboxGroupingProjector` |
| `StrongboxHybridEquipmentGenerationResolverV1` | `StrongboxHybridEquipmentGenerationResolver` |
| `StrongboxHybridLootPolicyV1` | `StrongboxHybridLootPolicy` |
| `StrongboxHybridLootPolicyValidationV1` | `StrongboxHybridLootPolicyValidation` |
| `StrongboxHybridLootRandomV1` | `StrongboxHybridLootRandom` |
| `StrongboxHybridLootV1Tests` | `StrongboxHybridLootTests` |
| `StrongboxInstanceContextV1` | `StrongboxInstanceContext` |
| `StrongboxInstanceLevelRollV1` | `StrongboxInstanceLevelRoll` |
| `StrongboxItemLevelRollV1` | `StrongboxItemLevelRoll` |
| `StrongboxLevelComparisonResultV1` | `StrongboxLevelComparisonResult` |
| `StrongboxLevelQueueEntryV1` | `StrongboxLevelQueueEntry` |
| `StrongboxMandatoryScrapPolicyV1` | `StrongboxMandatoryScrapPolicy` |
| `StrongboxMissionResultApplicationCommandV1` | `StrongboxMissionResultApplicationCommand` |
| `StrongboxMissionResultApplicationCoordinatorV1` | `StrongboxMissionResultApplicationFlow` |
| `StrongboxMissionResultApplicationResultV1` | `StrongboxMissionResultApplicationResult` |
| `StrongboxMissionResultApplicationStatusV1` | `StrongboxMissionResultApplicationStatus` |
| `StrongboxOpenCommandV1` | `StrongboxOpenCommand` |
| `StrongboxOpeningComponentCodecV1` | `StrongboxOpeningComponentCodec` |
| `StrongboxOpeningImportResultV1` | `StrongboxOpeningImportResult` |
| `StrongboxOpeningImportStatusV1` | `StrongboxOpeningImportStatus` |
| `StrongboxOpeningPresentationResultV1` | `StrongboxOpeningPresentationResult` |
| `StrongboxOpeningPresentationViewV1` | `StrongboxOpeningPresentationView` |
| `StrongboxOpeningPreviewConfigurationV1` | `StrongboxOpeningPreviewConfiguration` |
| `StrongboxOpeningRecordSnapshotV1` | `StrongboxOpeningRecordSnapshot` |
| `StrongboxOpeningRecoveryResultV1` | `StrongboxOpeningRecoveryResult` |
| `StrongboxOpeningRecoveryStatusV1` | `StrongboxOpeningRecoveryStatus` |
| `StrongboxOpeningRequestV1` | `StrongboxOpeningRequest` |
| `StrongboxOpeningResultRuntimeV1` | `StrongboxOpeningResultLive` |
| `StrongboxOpeningResultV1` | `StrongboxOpeningResult` |
| `StrongboxOpeningRuntimePortV1` | `StrongboxOpeningLivePort` |
| `StrongboxOpeningRuntimeStatusV1` | `StrongboxOpeningLiveStatus` |
| `StrongboxOpeningSceneSessionV1` | `StrongboxOpeningSceneSession` |
| `StrongboxOpeningServiceV1` | `StrongboxOpeningActions` |
| `StrongboxOpeningServiceV1Tests` | `StrongboxOpeningActionsTests` |
| `StrongboxOpeningSnapshotV1` | `StrongboxOpeningSnapshot` |
| `StrongboxOpeningStageV1` | `StrongboxOpeningStage` |
| `StrongboxOpeningStatusV1` | `StrongboxOpeningStatus` |
| `StrongboxPersistenceCoordinatorV1Tests` | `StrongboxPersistenceFlowTests` |
| `StrongboxPersistentNonConditionRuntimePortFactoryV1` | `StrongboxPersistentNonConditionLivePortFactory` |
| `StrongboxPowerBudgetPolicyV1` | `StrongboxPowerBudgetPolicy` |
| `StrongboxPowerBudgetV1Tests` | `StrongboxPowerBudgetTests` |
| `StrongboxPresentationPlaybackV1` | `StrongboxPresentationPlayback` |
| `StrongboxProductionFingerprints` | `StrongboxFingerprints` |
| `StrongboxRarityProfileV1` | `StrongboxRarityProfile` |
| `StrongboxRegistrationResultV1` | `StrongboxRegistrationResult` |
| `StrongboxRegistrationStatusV1` | `StrongboxRegistrationStatus` |
| `StrongboxRevealStageV1` | `StrongboxRevealStage` |
| `StrongboxRewardCardsViewV1` | `StrongboxRewardCardsView` |
| `StrongboxRewardCountPolicyV1` | `StrongboxRewardCountPolicy` |
| `StrongboxRewardPresentationKindV1` | `StrongboxRewardPresentationKind` |
| `StrongboxRewardRevealItemV1` | `StrongboxRewardRevealItem` |
| `StrongboxRewardRevealProjectorV1` | `StrongboxRewardRevealProjector` |
| `StrongboxSaveAdapterReplayTests` | `StrongboxSaveBridgeReplayTests` |
| `StrongboxSimulationCoordinator` | `StrongboxSimulationFlow` |
| `StrongboxTargetLevelRollV1` | `StrongboxTargetLevelRoll` |
| `StrongboxTierContextModifierV1` | `StrongboxTierContextModifier` |
| `StrongboxTierSelectionProfileV1` | `StrongboxTierSelectionProfile` |
| `StrongboxTierWeightV1` | `StrongboxTierWeight` |
| `StrongboxWeightedIntOutcomeV1` | `StrongboxWeightedIntOutcome` |
| `SystemIoAtomicSaveFilePortV1` | `SystemIoAtomicSaveFilePort` |
| `TerminalDropAdaptationResultV1` | `TerminalDropAdaptationResult` |
| `TerminalDropBindingCompositionV1` | `TerminalDropBindingSetup` |
| `TerminalDropBindingStatusV1` | `TerminalDropBindingStatus` |
| `TerminalDropCanonicalV1` | `TerminalDrop` |
| `TerminalDropFactAdapterRegistryV1` | `TerminalDropFactBridgeRegistry` |
| `TerminalDropFactKindIdsV1` | `TerminalDropFactKindIds` |
| `TerminalDropGenerationAuthorityV1` | `TerminalDropGenerationState` |
| `TerminalDropGenerationAuthorityV1Tests` | `TerminalDropGenerationStateTests` |
| `TerminalDropPendingPublicationPolicyV1` | `TerminalDropPendingPublicationPolicy` |
| `TerminalDropPendingPublicationPolicyV1Tests` | `TerminalDropPendingPublicationPolicyTests` |
| `TerminalDropRejectionCodeV1` | `TerminalDropRejectionCode` |
| `TerminalDropRunGenerationContextV1` | `TerminalDropRunGenerationContext` |
| `TerminalDropRunPickupAdapterV1` | `TerminalDropRunPickupBridge` |
| `TerminalDropSourceFactV1` | `TerminalDropSourceFact` |
| `TerminalPersonalRewardBatchStatusV1` | `TerminalPersonalRewardBatchStatus` |
| `TerminalPersonalRewardBatchV1` | `TerminalPersonalRewardBatch` |
| `TerminalPersonalRewardGenerationAuthorityV1` | `TerminalPersonalRewardGenerationState` |
| `TerminalPersonalRewardTransportAdapterV1` | `TerminalPersonalRewardTransportBridge` |
| `TerminalRewardEligibilityPolicyV1` | `TerminalRewardEligibilityPolicy` |
| `TerminalRewardEnvironmentV1` | `TerminalRewardEnvironment` |
| `TerminalRewardOverrideSetV1` | `TerminalRewardOverrideSet` |
| `TerminalRewardParticipantV1` | `TerminalRewardParticipant` |
| `TerminalRewardPlacementContextV1` | `TerminalRewardPlacementContext` |
| `TerminalRunMinimumGenerationAuthorityV1` | `TerminalRunMinimumGenerationState` |
| `TestAuthority` | `TestState` |
| `TestAuthoritySet` | `TestStateSet` |
| `TestEnemyAuthority` | `TestEnemyState` |
| `TestProjection` | `TestView` |
| `ThrowOnceAdapter` | `ThrowOnceBridge` |
| `ThrowOnceRewardChildAuthority` | `ThrowOnceRewardChildState` |
| `TransactionalRunRewardEnemyConsumerV1` | `TransactionalRunRewardEnemyConsumer` |
| `TransactionalStrongboxGrantPayloadResolverV1` | `TransactionalStrongboxGrantPayloadResolver` |
| `TransformPickupSourcePositionResolverV1` | `TransformPickupSourcePositionResolver` |
| `TransientHoldingsAuthority` | `TransientHoldingsState` |
| `UniqueHoldingSnapshotV1` | `UniqueHoldingSnapshot` |
| `UnityLevelSelectionRouteAdapterV1` | `UnityLevelSelectionRouteBridge` |
| `UnityLevelSelectionSceneLoaderV1` | `UnityLevelSelectionSceneLoader` |
| `UnityPickupAdmissionRuntimeV1` | `UnityPickupAdmissionLive` |
| `UnitySceneLoadPortV1` | `UnitySceneLoadPort` |
| `UnlockRoomDoorCommandV1` | `UnlockRoomDoorCommand` |
| `UnsupportedMissionResultRunPortV1` | `UnsupportedMissionResultRunPort` |
| `UnsupportedPropSourceContextResolverV1` | `UnsupportedPropSourceContextResolver` |
| `V1DecorDto` | `DecorDto` |
| `V1DoorDto` | `DoorDto` |
| `V1DoorLinkDto` | `DoorLinkDto` |
| `V1EncounterDto` | `EncounterDto` |
| `V1EnemiesDto` | `EnemiesDto` |
| `V1ManifestDto` | `ManifestDto` |
| `V1PropsDto` | `PropsDto` |
| `V1RoomDocumentsDto` | `RoomDocumentsDto` |
| `V1RoomLayoutDto` | `RoomLayoutDto` |
| `V1SpawnDto` | `SpawnDto` |
| `ValidatedEnemyPerceptionRuntimeAdapterV1` | `ValidatedEnemyPerceptionLiveBridge` |
| `WeaponArtReferenceProjectionV1` | `WeaponArtReferenceView` |
| `WeaponArtReferenceResolverV1` | `WeaponArtReferenceResolver` |
| `WeaponArtSpriteRegistryV1` | `WeaponArtSpriteRegistry` |
| `WeaponArtSpriteResolutionV1` | `WeaponArtSpriteResolution` |
| `WeaponCatalogCanonicalJson` | `WeaponCatalogJson` |
| `WeaponCatalogRuntimeProfileResolver` | `WeaponCatalogLiveProfileResolver` |
| `WeaponEffectHitPolicyAdapterV1` | `WeaponEffectHitPolicyBridge` |
| `WeaponHoldingsComponentCodecV2` | `WeaponHoldingsComponentCodec` |
| `WeaponHoldingsImportResultV2` | `WeaponHoldingsImportResult` |
| `WeaponHoldingsSaveComponentV2` | `WeaponHoldingsSaveComponent` |
| `WeaponHoldingsSnapshotV2` | `WeaponHoldingsSnapshot` |
| `WeaponInventoryCardPresentationV1` | `WeaponInventoryCardPresentation` |
| `WeaponInventoryLiveV2PersistenceTests` | `WeaponInventoryLivePersistenceTests` |
| `WeaponLiveExceptionPolicyV1` | `WeaponLiveExceptionPolicy` |
| `WeaponLootCardEditorDrawerV1` | `WeaponLootCardEditorDrawer` |
| `WeaponLootCardProjectionV1` | `WeaponLootCardView` |
| `WeaponMount2DAdapter` | `WeaponMount2DBridge` |
| `WeaponMount2DAdapterTests` | `WeaponMount2DBridgeTests` |
| `WeaponMountBindingV2` | `WeaponMountBinding` |
| `WeaponMountLoadoutComponentCodecV2` | `WeaponMountLoadoutComponentCodec` |
| `WeaponMountLoadoutImportResultV2` | `WeaponMountLoadoutImportResult` |
| `WeaponMountLoadoutSaveComponentV2` | `WeaponMountLoadoutSaveComponent` |
| `WeaponMountLoadoutSnapshotV2` | `WeaponMountLoadoutSnapshot` |
| `WeaponRicochetRuntimeState` | `WeaponRicochetLiveState` |
| `WeaponRuntimeFiringProfile` | `WeaponLiveFiringProfile` |
| `WeaponRuntimeProfile` | `WeaponLiveProfile` |
| `WeaponRuntimeProfileTests` | `WeaponLiveProfileTests` |
| `WeaponRuntimeProfileValidator` | `WeaponLiveProfileValidator` |
| `WeightedRewardOutcomeKindV1` | `WeightedRewardOutcomeKind` |
| `WeightedRewardOutcomeV1` | `WeightedRewardOutcome` |

## Matching file moves

- `Assets/ShooterMover/Content/Definitions/Characters/Selection/BuiltInCharacterSelectionCatalogV1.cs` → `Assets/ShooterMover/Content/Definitions/Characters/Selection/BuiltInCharacterSelectionCatalog.cs`
- `Assets/ShooterMover/Content/Definitions/Crafting/CraftingRecipeDefinitionAssetV1.cs` → `Assets/ShooterMover/Content/Definitions/Crafting/CraftingRecipeDefinitionAsset.cs`
- `Assets/ShooterMover/Content/Definitions/Enemies/BuiltInEnemyCatalogRegistryV1.cs` → `Assets/ShooterMover/Content/Definitions/Enemies/BuiltInEnemyCatalogRegistry.cs`
- `Assets/ShooterMover/Content/Definitions/Flow/PlayModes/PlayModeCatalogDefinitionV1.cs` → `Assets/ShooterMover/Content/Definitions/Flow/PlayModes/PlayModeCatalogDefinition.cs`
- `Assets/ShooterMover/Content/Definitions/Levels/Selection/LevelSelectionCatalogDefinitionV1.cs` → `Assets/ShooterMover/Content/Definitions/Levels/Selection/LevelSelectionCatalogDefinition.cs`
- `Assets/ShooterMover/Content/Definitions/Missions/Rooms/BuiltInRoomContentObjectCatalogV1.cs` → `Assets/ShooterMover/Content/Definitions/Missions/Rooms/BuiltInRoomContentObjectCatalog.cs`
- `Assets/ShooterMover/Content/Definitions/Missions/Rooms/Level1AuthorableRoomDefinitionV1.cs` → `Assets/ShooterMover/Content/Definitions/Missions/Rooms/Level1AuthorableRoomDefinition.cs`
- `Assets/ShooterMover/Content/Definitions/Missions/Rooms/Level1LiveRoomGraphDefinitionV1.cs` → `Assets/ShooterMover/Content/Definitions/Missions/Rooms/Level1LiveRoomGraphDefinition.cs`
- `Assets/ShooterMover/Content/Definitions/Missions/Rooms/Level1RoomGraphDefinitionV1.cs` → `Assets/ShooterMover/Content/Definitions/Missions/Rooms/Level1RoomGraphDefinition.cs`
- `Assets/ShooterMover/Content/Definitions/Strongboxes/StrongboxDefinitionSetV1.cs` → `Assets/ShooterMover/Content/Definitions/Strongboxes/StrongboxDefinitionSet.cs`
- `Assets/ShooterMover/ContentPackages/Environment/VoidHazards/VoidHazardAuthoring2D.Routing.cs` → `Assets/ShooterMover/ContentPackages/Environment/VoidHazards/VoidHazardAuthoring2Routing.cs`
- `Assets/ShooterMover/ContentPackages/Environment/VoidHazards/VoidHazardAuthoring2D.Unity.cs` → `Assets/ShooterMover/ContentPackages/Environment/VoidHazards/VoidHazardAuthoring2Unity.cs`
- `Assets/ShooterMover/ContentPackages/Props/DestructibleProps/DestructiblePropAuthority.cs` → `Assets/ShooterMover/ContentPackages/Props/DestructibleProps/DestructiblePropState.cs`
- `Assets/ShooterMover/ContentPackages/Props/DestructibleProps/DestructiblePropTerminalProvenanceV1.cs` → `Assets/ShooterMover/ContentPackages/Props/DestructibleProps/DestructiblePropTerminalProvenance.cs`
- `Assets/ShooterMover/ContentPackages/Weapons/Shared/Runtime/ProjectileExecutionPlanAdapter.cs` → `Assets/ShooterMover/ContentPackages/Weapons/Shared/Runtime/ProjectileExecutionPlanBridge.cs`
- `Assets/ShooterMover/Editor/BalanceSimulator/AuthoritativeStrongboxSimulationGatewayFactoryV1.cs` → `Assets/ShooterMover/Editor/BalanceSimulator/AuthoritativeStrongboxSimulationGatewayFactory.cs`
- `Assets/ShooterMover/Editor/BalanceSimulator/AuthoritativeStrongboxSimulationProductionGatewayV1.cs` → `Assets/ShooterMover/Editor/BalanceSimulator/AuthoritativeStrongboxSimulationGateway.cs`
- `Assets/ShooterMover/Editor/BalanceSimulator/AuthoritativeStrongboxSimulationRunnerV1.cs` → `Assets/ShooterMover/Editor/BalanceSimulator/AuthoritativeStrongboxSimulationRunner.cs`
- `Assets/ShooterMover/Editor/BalanceSimulator/AuthoritativeStrongboxSimulatorRuntimeV1.cs` → `Assets/ShooterMover/Editor/BalanceSimulator/AuthoritativeStrongboxSimulatorLive.cs`
- `Assets/ShooterMover/Editor/BalanceSimulator/BalanceSimulationModelsV1.cs` → `Assets/ShooterMover/Editor/BalanceSimulator/BalanceSimulationModels.cs`
- `Assets/ShooterMover/Editor/BalanceSimulator/BalanceSimulationServiceV1.cs` → `Assets/ShooterMover/Editor/BalanceSimulator/BalanceSimulationActions.cs`
- `Assets/ShooterMover/Editor/BalanceSimulator/DropSourceSimulationRequestV1.cs` → `Assets/ShooterMover/Editor/BalanceSimulator/DropSourceSimulationRequest.cs`
- `Assets/ShooterMover/Editor/BalanceSimulator/DropSourceSimulationRuntimeV1.cs` → `Assets/ShooterMover/Editor/BalanceSimulator/DropSourceSimulationLive.cs`
- `Assets/ShooterMover/Editor/BalanceSimulator/LootboxSimulatorRuntimeV1.cs` → `Assets/ShooterMover/Editor/BalanceSimulator/LootboxSimulatorLive.cs`
- `Assets/ShooterMover/Editor/BalanceSimulator/MultiplayerDropSimulationRuntimeV1.cs` → `Assets/ShooterMover/Editor/BalanceSimulator/MultiplayerDropSimulationLive.cs`
- `Assets/ShooterMover/Editor/BalanceSimulator/RewardSimulationParticipantInputV1.cs` → `Assets/ShooterMover/Editor/BalanceSimulator/RewardSimulationParticipantInput.cs`
- `Assets/ShooterMover/Editor/BalanceSimulator/RewardSimulationParticipantReportV1.cs` → `Assets/ShooterMover/Editor/BalanceSimulator/RewardSimulationParticipantReport.cs`
- `Assets/ShooterMover/Editor/BalanceSimulator/RewardSimulationReportV1.cs` → `Assets/ShooterMover/Editor/BalanceSimulator/RewardSimulationReport.cs`
- `Assets/ShooterMover/Editor/BalanceSimulator/RuntimeBalanceScenarioV1.cs` → `Assets/ShooterMover/Editor/BalanceSimulator/LiveBalanceScenario.cs`
- `Assets/ShooterMover/Editor/BalanceSimulator/WeaponLootCardEditorDrawerV1.cs` → `Assets/ShooterMover/Editor/BalanceSimulator/WeaponLootCardEditorDrawer.cs`
- `Assets/ShooterMover/Editor/BalanceSimulator/WeaponLootCardProjectionV1.cs` → `Assets/ShooterMover/Editor/BalanceSimulator/WeaponLootCardView.cs`
- `Assets/ShooterMover/Editor/EnemyReadiness/EnemyReadinessWindowV1.cs` → `Assets/ShooterMover/Editor/EnemyReadiness/EnemyReadinessWindow.cs`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2CreationMenu.cs` → `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringCreationMenu.cs`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2Editor.cs` → `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringEditor.cs`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2JsonExporter.cs` → `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringJsonExporter.cs`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2LiveValidation.cs` → `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringLiveValidation.cs`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringV2ThreeRoomExampleMenu.cs` → `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAuthoringThreeRoomExampleMenu.cs`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridDoorOperationsV2.cs` → `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridDoorOperations.cs`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridEditorOperationsV2.cs` → `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridEditorOperations.cs`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridEditorProjectionV2.cs` → `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridEditorView.cs`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridEditorWindowV2.Canvas.cs` → `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridEditorWindowV2Canvas.cs`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridEditorWindowV2.EntryPoints.cs` → `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridEditorWindowV2EntryPoints.cs`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridEditorWindowV2.Panels.cs` → `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridEditorWindowV2Panels.cs`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridEditorWindowV2.Playable.cs` → `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridEditorWindowV2Playable.cs`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridEditorWindowV2.State.cs` → `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridEditorWindowV2State.cs`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridEditorWindowV2.cs` → `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridEditorWindow.cs`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridLegacySurfaceGuardsV2.cs` → `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridLegacySurfaceGuards.cs`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableAssetChangeWatcherV2.cs` → `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableAssetChangeWatcher.cs`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableBuildFacadeV2.cs` → `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableBuildFacade.cs`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableBuildPathsV2.cs` → `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableBuildPaths.cs`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableMetadataOperationsV2.cs` → `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableMetadataOperations.cs`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableProvenanceV2.cs` → `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableProvenance.cs`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableStatusV2.cs` → `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableStatus.cs`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridV2AssetCompiler.cs` → `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAssetCompiler.cs`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridV2AssetCompilerCleanup.cs` → `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAssetCompilerCleanup.cs`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridV2AssetCompilerPublication.cs` → `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAssetCompilerPublication.cs`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridV2AssetCompilerStatusFacade.cs` → `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridAssetCompilerStatusFacade.cs`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridV2PlayableExporter.cs` → `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableExporter.cs`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridV2PlayableExporterUtilities.cs` → `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableExporterUtilities.cs`
- `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridV2PlayableExporterWriting.cs` → `Assets/ShooterMover/Editor/LevelDesign/Foundation/LevelGridPlayableExporterWriting.cs`
- `Assets/ShooterMover/Production/EnemyAttackPatterns/EnemyAttackPatternProductionController2D.cs` → `Assets/ShooterMover/Production/EnemyAttackPatterns/EnemyAttackPatternController2D.cs`
- `Assets/ShooterMover/Production/EnemyAttackPatterns/EnemyAttackPatternUnityBindingsV1.cs` → `Assets/ShooterMover/Production/EnemyAttackPatterns/EnemyAttackPatternUnityBindings.cs`
- `Assets/ShooterMover/Production/EnemyAttackPatterns/EnemyAttackPatternUnityEmissionRealizerV1.cs` → `Assets/ShooterMover/Production/EnemyAttackPatterns/EnemyAttackPatternUnityEmissionRealizer.cs`
- `Assets/ShooterMover/Runtime/Application/Characters/Selection/CharacterSelectionServiceV1.cs` → `Assets/ShooterMover/Runtime/Application/Characters/Selection/CharacterSelectionActions.cs`
- `Assets/ShooterMover/Runtime/Application/Crafting/CraftingServiceV1.cs` → `Assets/ShooterMover/Runtime/Application/Crafting/CraftingActions.cs`
- `Assets/ShooterMover/Runtime/Application/Crafting/Integration/CraftingInventoryEquipServiceV1.cs` → `Assets/ShooterMover/Runtime/Application/Crafting/Integration/CraftingInventoryEquipActions.cs`
- `Assets/ShooterMover/Runtime/Application/Crafting/Presentation/CraftingScreenPresentationV1.cs` → `Assets/ShooterMover/Runtime/Application/Crafting/Presentation/CraftingScreenPresentation.cs`
- `Assets/ShooterMover/Runtime/Application/Development/RunDebug/RunDebugContractsV1.cs` → `Assets/ShooterMover/Runtime/Application/Development/RunDebug/RunDebugContracts.cs`
- `Assets/ShooterMover/Runtime/Application/Development/RunDebug/RunDebugPanelSessionV1.cs` → `Assets/ShooterMover/Runtime/Application/Development/RunDebug/RunDebugPanelSession.cs`
- `Assets/ShooterMover/Runtime/Application/Economy/Money/MoneyWalletService.cs` → `Assets/ShooterMover/Runtime/Application/Economy/Money/MoneyWalletActions.cs`
- `Assets/ShooterMover/Runtime/Application/Economy/Scrap/ScrapWalletServiceV1.cs` → `Assets/ShooterMover/Runtime/Application/Economy/Scrap/ScrapWalletActions.cs`
- `Assets/ShooterMover/Runtime/Application/Enemies/Catalog/EnemyCatalogImportResultV1.cs` → `Assets/ShooterMover/Runtime/Application/Enemies/Catalog/EnemyCatalogImportResult.cs`
- `Assets/ShooterMover/Runtime/Application/Enemies/Catalog/EnemyCatalogJsonDtosV1.cs` → `Assets/ShooterMover/Runtime/Application/Enemies/Catalog/EnemyCatalogJsonDtos.cs`
- `Assets/ShooterMover/Runtime/Application/Enemies/Catalog/EnemyCatalogJsonImporterV1.cs` → `Assets/ShooterMover/Runtime/Application/Enemies/Catalog/EnemyCatalogJsonImporter.cs`
- `Assets/ShooterMover/Runtime/Application/Equipment/Upgrades/AugmentUpgradeConfirmationServiceV1.cs` → `Assets/ShooterMover/Runtime/Application/Equipment/Upgrades/AugmentUpgradeConfirmationActions.cs`
- `Assets/ShooterMover/Runtime/Application/Equipment/Upgrades/AugmentUpgradeExecutionV1.cs` → `Assets/ShooterMover/Runtime/Application/Equipment/Upgrades/AugmentUpgradeExecution.cs`
- `Assets/ShooterMover/Runtime/Application/Equipment/Upgrades/AugmentUpgradePreparationV1.cs` → `Assets/ShooterMover/Runtime/Application/Equipment/Upgrades/AugmentUpgradePreparation.cs`
- `Assets/ShooterMover/Runtime/Application/Equipment/Upgrades/AugmentUpgradePreparedRecordV1.cs` → `Assets/ShooterMover/Runtime/Application/Equipment/Upgrades/AugmentUpgradePreparedRecord.cs`
- `Assets/ShooterMover/Runtime/Application/Equipment/Upgrades/AugmentUpgradeServiceV1.cs` → `Assets/ShooterMover/Runtime/Application/Equipment/Upgrades/AugmentUpgradeActions.cs`
- `Assets/ShooterMover/Runtime/Application/Flow/Hub/HubNavigationServiceV1.cs` → `Assets/ShooterMover/Runtime/Application/Flow/Hub/HubNavigationActions.cs`
- `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/ILevelSelectionRouteAdapterV1.cs` → `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/ILevelSelectionRouteBridge.cs`
- `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelRecommendationV1.cs` → `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelRecommendation.cs`
- `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelSelectionCatalogV1.cs` → `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelSelectionCatalog.cs`
- `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelSelectionDefinitionV1.cs` → `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelSelectionDefinition.cs`
- `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelSelectionEnumsV1.cs` → `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelSelectionEnums.cs`
- `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelSelectionResultV1.cs` → `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelSelectionResult.cs`
- `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelSelectionServiceV1.cs` → `Assets/ShooterMover/Runtime/Application/Flow/LevelSelection/LevelSelectionActions.cs`
- `Assets/ShooterMover/Runtime/Application/Flow/PlaySelection/PlaySelectionServiceV1.cs` → `Assets/ShooterMover/Runtime/Application/Flow/PlaySelection/PlaySelectionActions.cs`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/CanonicalFirstPlayerHoldingsAuthorityV2.cs` → `Assets/ShooterMover/Runtime/Application/Flow/Production/FirstPlayerHoldingsState.cs`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/CanonicalWeaponInventoryScreenV2.cs` → `Assets/ShooterMover/Runtime/Application/Flow/Production/WeaponInventoryScreen.cs`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionCharacterAuthorityAdaptersV1.cs` → `Assets/ShooterMover/Runtime/Application/Flow/Production/CharacterStateAdapters.cs`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionCharacterRuntimeGraphV1.cs` → `Assets/ShooterMover/Runtime/Application/Flow/Production/CharacterLiveGraph.cs`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionCharacterStrongboxBridgeV1.cs` → `Assets/ShooterMover/Runtime/Application/Flow/Production/CharacterStrongboxBridge.cs`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionCharacterStrongboxCompositionV1.cs` → `Assets/ShooterMover/Runtime/Application/Flow/Production/CharacterStrongboxSetup.cs`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionFlowSessionV1.cs` → `Assets/ShooterMover/Runtime/Application/Flow/Production/FlowSession.cs`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionPlayerLoadoutRuntimeV1.cs` → `Assets/ShooterMover/Runtime/Application/Flow/Production/PlayerLoadoutLive.cs`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponCatalogProvider.cs` → `Assets/ShooterMover/Runtime/Application/Flow/Production/WeaponCatalogProvider.cs`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponHoldingsV2.cs` → `Assets/ShooterMover/Runtime/Application/Flow/Production/WeaponHoldings.cs`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponMountLoadoutRegistryV2.cs` → `Assets/ShooterMover/Runtime/Application/Flow/Production/WeaponMountLoadoutRegistry.cs`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponMountLoadoutV2.cs` → `Assets/ShooterMover/Runtime/Application/Flow/Production/WeaponMountLoadout.cs`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponMountPolicyV1.cs` → `Assets/ShooterMover/Runtime/Application/Flow/Production/WeaponMountPolicy.cs`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponOnboardingV1.cs` → `Assets/ShooterMover/Runtime/Application/Flow/Production/WeaponOnboarding.cs`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/ProductionWeaponOnboardingV2.cs` → `Assets/ShooterMover/Runtime/Application/Flow/Production/WeaponOnboarding.cs`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/RequiredCharacterComponentBackfillV1.cs` → `Assets/ShooterMover/Runtime/Application/Flow/Production/RequiredCharacterComponentBackfill.cs`
- `Assets/ShooterMover/Runtime/Application/Flow/Production/RetiredWeaponSaveMigrationV1.cs` → `Assets/ShooterMover/Runtime/Application/Flow/Production/RetiredWeaponSaveMigration.cs`
- `Assets/ShooterMover/Runtime/Application/Holdings/PlayerHoldingsService.Persistence.cs` → `Assets/ShooterMover/Runtime/Application/Holdings/PlayerHoldingsActionsPersistence.cs`
- `Assets/ShooterMover/Runtime/Application/Holdings/PlayerHoldingsService.SnapshotValidation.cs` → `Assets/ShooterMover/Runtime/Application/Holdings/PlayerHoldingsActionsSnapshotValidation.cs`
- `Assets/ShooterMover/Runtime/Application/Holdings/PlayerHoldingsService.ValidationAndSnapshots.cs` → `Assets/ShooterMover/Runtime/Application/Holdings/PlayerHoldingsActionsValidationAndSnapshots.cs`
- `Assets/ShooterMover/Runtime/Application/Holdings/PlayerHoldingsService.cs` → `Assets/ShooterMover/Runtime/Application/Holdings/PlayerHoldingsActions.cs`
- `Assets/ShooterMover/Runtime/Application/Inventory/LoadoutScreen/InventoryLoadoutScreenV1.cs` → `Assets/ShooterMover/Runtime/Application/Inventory/LoadoutScreen/InventoryLoadoutScreen.cs`
- `Assets/ShooterMover/Runtime/Application/Missions/Results/MissionResultsSessionV1.cs` → `Assets/ShooterMover/Runtime/Application/Missions/Results/MissionResultsSession.cs`
- `Assets/ShooterMover/Runtime/Application/Missions/Results/MissionRunAuthorityPortsV1.cs` → `Assets/ShooterMover/Runtime/Application/Missions/Results/MissionRunStatePorts.cs`
- `Assets/ShooterMover/Runtime/Application/Missions/Results/MissionRunExistingAuthorityPortV1.cs` → `Assets/ShooterMover/Runtime/Application/Missions/Results/MissionRunExistingStatePort.cs`
- `Assets/ShooterMover/Runtime/Application/Missions/Results/MissionRunResultAuthorityV1.cs` → `Assets/ShooterMover/Runtime/Application/Missions/Results/MissionRunResultState.cs`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/LevelGridV2Compiler.cs` → `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/LevelGridCompiler.cs`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/LevelGridV2CompilerDtos.cs` → `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/LevelGridCompilerDtos.cs`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/LevelGridV2CompilerLoading.cs` → `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/LevelGridCompilerLoading.cs`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/LevelGridV2CompilerOutput.cs` → `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/LevelGridCompilerOutput.cs`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/LevelGridV2CompilerSerialization.cs` → `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/LevelGridCompilerSerialization.cs`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/LevelGridV2CompilerValidation.cs` → `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/LevelGridCompilerValidation.cs`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomAccessJsonImporterV1.cs` → `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomAccessJsonImporter.cs`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentJsonDtosV1.cs` → `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentJsonDtos.cs`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentJsonImporterV1.cs` → `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentJsonImporter.cs`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentModelV1.cs` → `Assets/ShooterMover/Runtime/Application/Missions/Rooms/Content/RoomContentModel.cs`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomAccessAuthorityV1.cs` → `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomAccessState.cs`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveAccessFactProjectionV1.cs` → `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveAccessFactView.cs`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveRuntimeAuthorityCoreV1.cs` → `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveStateCore.cs`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveRuntimeHelpersV1.cs` → `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveHelpers.cs`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveRuntimeProjectionsV1.cs` → `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveProjections.cs`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveRuntimeTraversalV1.cs` → `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveTraversal.cs`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomMissionLayoutV1.cs` → `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomMissionLayout.cs`
- `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomRuntimeAuthorityV1.cs` → `Assets/ShooterMover/Runtime/Application/Missions/Rooms/RoomLiveState.cs`
- `Assets/ShooterMover/Runtime/Application/Modifiers/Events/ActiveEventModifierProjectionServiceV1.cs` → `Assets/ShooterMover/Runtime/Application/Modifiers/Events/ActiveEventModifierViewActions.cs`
- `Assets/ShooterMover/Runtime/Application/Modifiers/Events/EventStampedCommandEnvelopeV1.cs` → `Assets/ShooterMover/Runtime/Application/Modifiers/Events/EventStampedCommandEnvelope.cs`
- `Assets/ShooterMover/Runtime/Application/Modifiers/FactWindowConditionAuthorityV1.cs` → `Assets/ShooterMover/Runtime/Application/Modifiers/FactWindowConditionState.cs`
- `Assets/ShooterMover/Runtime/Application/Modifiers/StatusEffects/FactWindowStatusEffectBridgeV1.cs` → `Assets/ShooterMover/Runtime/Application/Modifiers/StatusEffects/FactWindowStatusEffectBridge.cs`
- `Assets/ShooterMover/Runtime/Application/Modifiers/StatusEffects/StatusEffectAuthorityV1.Commands.cs` → `Assets/ShooterMover/Runtime/Application/Modifiers/StatusEffects/StatusEffectStateV1Commands.cs`
- `Assets/ShooterMover/Runtime/Application/Modifiers/StatusEffects/StatusEffectAuthorityV1.Core.cs` → `Assets/ShooterMover/Runtime/Application/Modifiers/StatusEffects/StatusEffectStateV1Core.cs`
- `Assets/ShooterMover/Runtime/Application/Modifiers/StatusEffects/StatusEffectAuthorityV1.Snapshots.cs` → `Assets/ShooterMover/Runtime/Application/Modifiers/StatusEffects/StatusEffectStateV1Snapshots.cs`
- `Assets/ShooterMover/Runtime/Application/Modifiers/StatusEffects/StatusEffectAuthorityV1.Stacking.cs` → `Assets/ShooterMover/Runtime/Application/Modifiers/StatusEffects/StatusEffectStateV1Stacking.cs`
- `Assets/ShooterMover/Runtime/Application/Modifiers/StatusEffects/StatusEffectLocalHashV1.cs` → `Assets/ShooterMover/Runtime/Application/Modifiers/StatusEffects/StatusEffectLocalHash.cs`
- `Assets/ShooterMover/Runtime/Application/Persistence/Accounts/PlayerAccountSaveAuthorityV1.cs` → `Assets/ShooterMover/Runtime/Application/Persistence/Accounts/PlayerAccountSaveState.cs`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/AtomicPlayerAccountStoreV1.cs` → `Assets/ShooterMover/Runtime/Application/Persistence/Components/AtomicPlayerAccountStore.cs`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/CanonicalSnapshotCodecV1.cs` → `Assets/ShooterMover/Runtime/Application/Persistence/Components/SnapshotCodec.cs`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/CollectedRunRewardPersistenceExpectationV1.cs` → `Assets/ShooterMover/Runtime/Application/Persistence/Components/CollectedRunRewardPersistenceExpectation.cs`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/GeneratedEquipmentAugmentSignatureSaveComponentV1.cs` → `Assets/ShooterMover/Runtime/Application/Persistence/Components/GeneratedEquipmentAugmentSignatureSaveComponent.cs`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownExperienceMoneyCodecsV1.cs` → `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownExperienceMoneyCodecs.cs`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownHoldingsCodecV1.cs` → `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownHoldingsCodec.cs`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownLedgerScrapSkillLoadoutCodecsV1.cs` → `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownLedgerScrapSkillLoadoutCodecs.cs`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownSaveComponentCodecCoreV1.cs` → `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownSaveComponentCodecCore.cs`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownSaveComponentVersionGuardV1.cs` → `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownSaveComponentVersionGuard.cs`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownStrongboxCodecV1.cs` → `Assets/ShooterMover/Runtime/Application/Persistence/Components/KnownStrongboxCodec.cs`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/PlayerAccountComponentSemanticsV1.cs` → `Assets/ShooterMover/Runtime/Application/Persistence/Components/PlayerAccountComponentSemantics.cs`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/PlayerAccountRestoreCoordinatorV1.cs` → `Assets/ShooterMover/Runtime/Application/Persistence/Components/PlayerAccountRestoreFlow.cs`
- `Assets/ShooterMover/Runtime/Application/Persistence/Components/SaveComponentAdaptersV1.cs` → `Assets/ShooterMover/Runtime/Application/Persistence/Components/SaveComponentAdapters.cs`
- `Assets/ShooterMover/Runtime/Application/Persistence/Composition/CharacterCompositionCoordinatorV1.cs` → `Assets/ShooterMover/Runtime/Application/Persistence/Composition/CharacterSetupFlow.cs`
- `Assets/ShooterMover/Runtime/Application/Persistence/Composition/LegacyCharacterProfileMigrationV1.cs` → `Assets/ShooterMover/Runtime/Application/Persistence/Composition/LegacyCharacterProfileMigration.cs`
- `Assets/ShooterMover/Runtime/Application/Progression/Experience/EnemyRewards/EnemyExperienceRewardDefinitionsV1.cs` → `Assets/ShooterMover/Runtime/Application/Progression/Experience/EnemyRewards/EnemyExperienceRewardDefinitions.cs`
- `Assets/ShooterMover/Runtime/Application/Progression/Experience/EnemyRewards/EnemyExperienceRewardOperationV1.cs` → `Assets/ShooterMover/Runtime/Application/Progression/Experience/EnemyRewards/EnemyExperienceRewardOperation.cs`
- `Assets/ShooterMover/Runtime/Application/Progression/Experience/EnemyRewards/EnemyExperienceRewardServiceV1.cs` → `Assets/ShooterMover/Runtime/Application/Progression/Experience/EnemyRewards/EnemyExperienceRewardActions.cs`
- `Assets/ShooterMover/Runtime/Application/Progression/Experience/PlayerExperienceAuthorityV1.cs` → `Assets/ShooterMover/Runtime/Application/Progression/Experience/PlayerExperienceState.cs`
- `Assets/ShooterMover/Runtime/Application/Progression/Skills/RankedSkillAuthorityV2.cs` → `Assets/ShooterMover/Runtime/Application/Progression/Skills/RankedSkillState.cs`
- `Assets/ShooterMover/Runtime/Application/Progression/Skills/SkillProgressionAuthorityV1.cs` → `Assets/ShooterMover/Runtime/Application/Progression/Skills/SkillProgressionState.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Application/RewardApplicationAuthorityAdaptersV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Application/RewardApplicationStateAdapters.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Application/RewardApplicationServiceV1.Persistence.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Application/RewardApplicationActionsV1Persistence.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Application/RewardApplicationServiceV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Application/RewardApplicationActions.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardAtomicPlanV2.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardAtomicPlan.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardPreparedTransfersV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardPreparedTransfers.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferContractsV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferContracts.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferCoordinatorV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferFlow.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferPortsV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferPorts.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferReceiptsV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferReceipts.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferResultsV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardTransferResults.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/ProductionCollectedRunRewardAtomicAuthorityV2.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardAtomicState.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/ProductionCollectedRunRewardPersistenceV2.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardPersistence.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/ProductionCollectedRunRewardPreparationV2.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardPreparation.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/ProductionCollectedRunRewardRegistryCompatibility.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardRegistryCompatibility.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/ProductionCollectedRunRewardRuntimeRegistryV2.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/CollectedRunTransfers/CollectedRunRewardLiveRegistry.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/IParticipantDropPacingStateStoreV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Drops/IParticipantDropPacingStateStore.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/IPersonalRewardDeliveryOutboxV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Drops/IPersonalRewardDeliveryOutbox.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/ParticipantDropPacingAuthorityV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Drops/ParticipantDropPacingState.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/PersonalRewardDeliveryEnvelopeV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Drops/PersonalRewardDeliveryEnvelope.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/PersonalRewardGenerationRandomV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Drops/PersonalRewardGenerationRandom.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/PersonalRewardGenerationServiceV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Drops/PersonalRewardGenerationActions.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/PersonalRewardGroupGenerationV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Drops/PersonalRewardGroupGeneration.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/PersonalStrongboxRewardGenerationV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Drops/PersonalStrongboxRewardGeneration.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/ProductionRewardOverrideCatalogV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Drops/RewardOverrideCatalog.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/ProductionRewardSourceCatalogV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Drops/RewardSourceCatalog.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/ProductionRunDropPacingCatalogV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Drops/RunDropPacingCatalog.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/ProductionStrongboxTierSelectionCatalogV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Drops/StrongboxTierSelectionCatalog.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/RewardContextOverrideResolutionV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Drops/RewardContextOverrideResolution.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/RewardGrantHandlerRegistryV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Drops/RewardGrantHandlerRegistry.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Drops/RewardProfileResolverV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Drops/RewardProfileResolver.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/GameplayDrops/GameplayDropOperationV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/GameplayDrops/GameplayDropOperation.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Generation/RewardGenerationModelsV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Generation/RewardGenerationModels.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Generation/RewardGenerationServiceV1.Core.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Generation/RewardGenerationActionsV1Core.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Generation/RewardGenerationServiceV1.Equipment.Helpers.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Generation/RewardGenerationActionsV1EquipmentHelpers.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Generation/RewardGenerationServiceV1.Equipment.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Generation/RewardGenerationActionsV1Equipment.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Generation/RewardGenerationServiceV1.Rewards.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Generation/RewardGenerationActionsV1Rewards.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/GeneratedAugmentSignaturePlayerHoldingsRewardChildAuthorityV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/GeneratedAugmentSignaturePlayerHoldingsRewardChildState.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/GeneratedEquipmentAugmentSignatureAuthorityV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/GeneratedEquipmentAugmentSignatureState.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/GeneratedEquipmentAugmentSignatureSnapshotV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/GeneratedEquipmentAugmentSignatureSnapshot.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxDurableOpeningCoordinatorV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxDurableOpeningFlow.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxDurableOpeningExecutorV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxDurableOpeningExecutor.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxDurableOpeningRecoveryV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxDurableOpeningRecovery.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxDurableOpeningStateV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxDurableOpeningState.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationAuthorityPortV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationStatePort.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationCompensationV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationCompensation.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationContractsV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationContracts.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationCoordinatorV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationFlow.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationExecutionV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationExecution.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationPlanV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationPlan.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationValidationV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxMissionResultApplicationValidation.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxOpeningRecoveryPortV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/Persistence/StrongboxOpeningRecoveryPort.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/ProductionStrongboxCatalogV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxCatalog.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/ProductionStrongboxHybridLootCatalogV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxHybridLootCatalog.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxEquipmentGenerationResolverV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxEquipmentGenerationResolver.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxHybridEquipmentGenerationResolverV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxHybridEquipmentGenerationResolver.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxOpeningModelsV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxOpeningModels.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxOpeningServiceV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/StrongboxOpeningActions.cs`
- `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/TransactionalStrongboxGrantPayloadResolverV1.cs` → `Assets/ShooterMover/Runtime/Application/Rewards/Strongboxes/TransactionalStrongboxGrantPayloadResolver.cs`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/ExistingRunAuthorityPortsV1.cs` → `Assets/ShooterMover/Runtime/Application/Runs/Session/ExistingRunStatePorts.cs`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/ProductionRunSessionCompositionV1.cs` → `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionSetup.cs`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunConditionCheckpointV1.cs` → `Assets/ShooterMover/Runtime/Application/Runs/Session/RunConditionCheckpoint.cs`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunMissionResultPersistencePortsV1.cs` → `Assets/ShooterMover/Runtime/Application/Runs/Session/RunMissionResultPersistencePorts.cs`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunRewardEnvironmentSnapshotV1.cs` → `Assets/ShooterMover/Runtime/Application/Runs/Session/RunRewardEnvironmentSnapshot.cs`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunRewardParticipantStateV1.cs` → `Assets/ShooterMover/Runtime/Application/Runs/Session/RunRewardParticipantState.cs`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunRewardRuntimeSnapshotV1.cs` → `Assets/ShooterMover/Runtime/Application/Runs/Session/RunRewardLiveSnapshot.cs`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionAggregate.ConditionCheckpointV1.cs` → `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionAggregateConditionCheckpoint.cs`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionAggregate.ConditionRuntimeV1.cs` → `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionAggregateConditionLive.cs`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionAuthorityV1.cs` → `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionState.cs`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionCollectedRewardAuthorityV1.cs` → `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionCollectedRewardState.cs`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionCollectedRewardContractsV1.cs` → `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionCollectedRewardContracts.cs`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionCommandsV1.cs` → `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionCommands.cs`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionConditionContractsV1.cs` → `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionConditionContracts.cs`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionDurableEndV1.cs` → `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionDurableEnd.cs`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionParticipantDropPacingStateStoreV1.cs` → `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionParticipantDropPacingStateStore.cs`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionPersonalRewardDeliveryOutboxV1.cs` → `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionPersonalRewardDeliveryOutbox.cs`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionPortsV1.cs` → `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionPorts.cs`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionRewardRuntimeStateV1.cs` → `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionRewardLiveState.cs`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionSnapshotsV1.cs` → `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionSnapshots.cs`
- `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionTimeV1.cs` → `Assets/ShooterMover/Runtime/Application/Runs/Session/RunSessionTime.cs`
- `Assets/ShooterMover/Runtime/Application/Shops/Presentation/ShopScreenPresentationV1.cs` → `Assets/ShooterMover/Runtime/Application/Shops/Presentation/ShopScreenPresentation.cs`
- `Assets/ShooterMover/Runtime/Application/Shops/Presentation/ShopScreenSessionV1.Projection.cs` → `Assets/ShooterMover/Runtime/Application/Shops/Presentation/ShopScreenSessionV1View.cs`
- `Assets/ShooterMover/Runtime/Application/Shops/Presentation/ShopScreenSessionV1.cs` → `Assets/ShooterMover/Runtime/Application/Shops/Presentation/ShopScreenSession.cs`
- `Assets/ShooterMover/Runtime/Application/Shops/ShopRuntimeServiceV1.RefreshPersistence.cs` → `Assets/ShooterMover/Runtime/Application/Shops/ShopLiveActionsV1RefreshPersistence.cs`
- `Assets/ShooterMover/Runtime/Application/Shops/ShopRuntimeServiceV1.State.cs` → `Assets/ShooterMover/Runtime/Application/Shops/ShopLiveActionsV1State.cs`
- `Assets/ShooterMover/Runtime/Application/Shops/ShopRuntimeServiceV1.Transactions.cs` → `Assets/ShooterMover/Runtime/Application/Shops/ShopLiveActionsV1Transactions.cs`
- `Assets/ShooterMover/Runtime/Application/Shops/ShopRuntimeServiceV1.cs` → `Assets/ShooterMover/Runtime/Application/Shops/ShopLiveActions.cs`
- `Assets/ShooterMover/Runtime/Application/Skills/Presentation/RankedSkillsScreenSessionV2.cs` → `Assets/ShooterMover/Runtime/Application/Skills/Presentation/RankedSkillsScreenSession.cs`
- `Assets/ShooterMover/Runtime/Application/Skills/Presentation/SkillsScreenPresentationV1.cs` → `Assets/ShooterMover/Runtime/Application/Skills/Presentation/SkillsScreenPresentation.cs`
- `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/ProductionWeaponCatalogueV1.Content.cs` → `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/WeaponCatalogueV1Content.cs`
- `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/ProductionWeaponCatalogueV1.Contracts.cs` → `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/WeaponCatalogueV1Contracts.cs`
- `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/ProductionWeaponCatalogueV1.EquipmentProjection.cs` → `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/WeaponCatalogueV1EquipmentView.cs`
- `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/ProductionWeaponCatalogueV1.FlatProjection.cs` → `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/WeaponCatalogueV1FlatView.cs`
- `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/WeaponCatalogBlueprintMapper.Authored.Diagnostics.cs` → `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/WeaponCatalogBlueprintMapperAuthoredDiagnostics.cs`
- `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/WeaponCatalogBlueprintMapper.Authored.cs` → `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/WeaponCatalogBlueprintMapperAuthored.cs`
- `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/WeaponCatalogBlueprintMapper.Combat.cs` → `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/WeaponCatalogBlueprintMapperCombat.cs`
- `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/WeaponCatalogBlueprintMapper.References.cs` → `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/WeaponCatalogBlueprintMapperReferences.cs`
- `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/WeaponCatalogCanonicalJson.cs` → `Assets/ShooterMover/Runtime/Application/Weapons/Catalog/WeaponCatalogJson.cs`
- `Assets/ShooterMover/Runtime/Application/Weapons/Execution/AcceptedEmissionRuntimeAdapter.cs` → `Assets/ShooterMover/Runtime/Application/Weapons/Execution/AcceptedEmissionLiveBridge.cs`
- `Assets/ShooterMover/Runtime/Application/Weapons/Execution/AcceptedEmissionRuntimeAdapterContracts.cs` → `Assets/ShooterMover/Runtime/Application/Weapons/Execution/AcceptedEmissionLiveBridgeContracts.cs`
- `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponExecutionCore.State.cs` → `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponExecutionCoreState.cs`
- `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponExecutionCore.Validation.cs` → `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponExecutionCoreValidation.cs`
- `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponFiringScheduler.Build.cs` → `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponFiringSchedulerBuild.cs`
- `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponFiringScheduler.Burst.cs` → `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponFiringSchedulerBurst.cs`
- `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponFiringScheduler.Identity.cs` → `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponFiringSchedulerIdentity.cs`
- `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponFiringScheduler.Timing.cs` → `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponFiringSchedulerTiming.cs`
- `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponFiringScheduler.Transitions.cs` → `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponFiringSchedulerTransitions.cs`
- `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponFiringScheduler.Validation.cs` → `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponFiringSchedulerValidation.cs`
- `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponImpactDecisionLogic.Termination.cs` → `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponImpactDecisionLogicTermination.cs`
- `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponProfileResolver.Runtime.cs` → `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponProfileResolverLive.cs`
- `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponProfileResolver.Validation.cs` → `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponProfileResolverValidation.cs`
- `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponRicochetRuntimeState.cs` → `Assets/ShooterMover/Runtime/Application/Weapons/Execution/WeaponRicochetLiveState.cs`
- `Assets/ShooterMover/Runtime/Application/Weapons/Presentation/WeaponArtReferenceResolverV1.cs` → `Assets/ShooterMover/Runtime/Application/Weapons/Presentation/WeaponArtReferenceResolver.cs`
- `Assets/ShooterMover/Runtime/Bootstrap/BootstrapCompositionRoot.cs` → `Assets/ShooterMover/Runtime/Bootstrap/BootstrapSetupRoot.cs`
- `Assets/ShooterMover/Runtime/Bootstrap/Unity/BootstrapSceneAdapter.cs` → `Assets/ShooterMover/Runtime/Bootstrap/Unity/BootstrapSceneBridge.cs`
- `Assets/ShooterMover/Runtime/CombatHitPolicy/CombatHitPolicyAdaptersV1.cs` → `Assets/ShooterMover/Runtime/CombatHitPolicy/CombatHitPolicyAdapters.cs`
- `Assets/ShooterMover/Runtime/CombatHitPolicy/CombatHitPolicyV1.cs` → `Assets/ShooterMover/Runtime/CombatHitPolicy/CombatHitPolicy.cs`
- `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionRuntimeAuthorityV1.cs` → `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionLiveState.cs`
- `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionRuntimeContractsV1.cs` → `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionLiveContracts.cs`
- `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionSourceFactFingerprintV1.cs` → `Assets/ShooterMover/Runtime/ConditionRuntime/ConditionSourceFactFingerprint.cs`
- `Assets/ShooterMover/Runtime/ConditionRuntime/EnemyDeathConditionFactAdapterV1.cs` → `Assets/ShooterMover/Runtime/ConditionRuntime/EnemyDeathConditionFactBridge.cs`
- `Assets/ShooterMover/Runtime/Contracts/Economy/EconomyTransactionContractsV1.cs` → `Assets/ShooterMover/Runtime/Contracts/Economy/EconomyTransactionContracts.cs`
- `Assets/ShooterMover/Runtime/Contracts/Flow/Session/PlayerRouteProfilePayloadV1.cs` → `Assets/ShooterMover/Runtime/Contracts/Flow/Session/PlayerRouteProfilePayload.cs`
- `Assets/ShooterMover/Runtime/Contracts/Holdings/PlayerHoldingsAuthorityV1.cs` → `Assets/ShooterMover/Runtime/Contracts/Holdings/PlayerHoldingsState.cs`
- `Assets/ShooterMover/Runtime/Contracts/Holdings/PlayerHoldingsContractsV1.cs` → `Assets/ShooterMover/Runtime/Contracts/Holdings/PlayerHoldingsContracts.cs`
- `Assets/ShooterMover/Runtime/Contracts/Holdings/PlayerHoldingsMutationResultV1.cs` → `Assets/ShooterMover/Runtime/Contracts/Holdings/PlayerHoldingsMutationResult.cs`
- `Assets/ShooterMover/Runtime/Contracts/Holdings/PlayerHoldingsSnapshotV1.cs` → `Assets/ShooterMover/Runtime/Contracts/Holdings/PlayerHoldingsSnapshot.cs`
- `Assets/ShooterMover/Runtime/Contracts/Holdings/PlayerHoldingsTransactionRecordV1.cs` → `Assets/ShooterMover/Runtime/Contracts/Holdings/PlayerHoldingsTransactionRecord.cs`
- `Assets/ShooterMover/Runtime/Contracts/Missions/Results/MissionRunCommandsV1.cs` → `Assets/ShooterMover/Runtime/Contracts/Missions/Results/MissionRunCommands.cs`
- `Assets/ShooterMover/Runtime/Contracts/Missions/Results/MissionRunPayloadsV1.cs` → `Assets/ShooterMover/Runtime/Contracts/Missions/Results/MissionRunPayloads.cs`
- `Assets/ShooterMover/Runtime/Contracts/Missions/Results/MissionRunResultContractsV1.cs` → `Assets/ShooterMover/Runtime/Contracts/Missions/Results/MissionRunResultContracts.cs`
- `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/AuthorableRoomDefinitionV1.cs` → `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/AuthorableRoomDefinition.cs`
- `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/AuthorableRoomGraphDefinitionV1.cs` → `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/AuthorableRoomGraphDefinition.cs`
- `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAccessContractsV1.cs` → `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAccessContracts.cs`
- `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAccessReferenceCatalogV1.cs` → `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAccessReferenceCatalog.cs`
- `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAuthoringPrimitivesV1.cs` → `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomAuthoringPrimitives.cs`
- `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomGraphContractsV1.cs` → `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomGraphContracts.cs`
- `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomOccupancyContractsV1.cs` → `Assets/ShooterMover/Runtime/Contracts/Missions/Rooms/RoomOccupancyContracts.cs`
- `Assets/ShooterMover/Runtime/Contracts/Progression/Experience/PlayerExperienceContractsV1.cs` → `Assets/ShooterMover/Runtime/Contracts/Progression/Experience/PlayerExperienceContracts.cs`
- `Assets/ShooterMover/Runtime/Contracts/Rewards/Application/RewardApplicationCommandsV1.cs` → `Assets/ShooterMover/Runtime/Contracts/Rewards/Application/RewardApplicationCommands.cs`
- `Assets/ShooterMover/Runtime/Contracts/Rewards/Application/RewardApplicationResultsV1.cs` → `Assets/ShooterMover/Runtime/Contracts/Rewards/Application/RewardApplicationResults.cs`
- `Assets/ShooterMover/Runtime/Contracts/Rewards/Drops/PersonalRewardGenerationResultV1.cs` → `Assets/ShooterMover/Runtime/Contracts/Rewards/Drops/PersonalRewardGenerationResult.cs`
- `Assets/ShooterMover/Runtime/Contracts/Rewards/RewardOperationContractsV1.cs` → `Assets/ShooterMover/Runtime/Contracts/Rewards/RewardOperationContracts.cs`
- `Assets/ShooterMover/Runtime/Contracts/Rewards/StrongboxOpeningContractsV1.cs` → `Assets/ShooterMover/Runtime/Contracts/Rewards/StrongboxOpeningContracts.cs`
- `Assets/ShooterMover/Runtime/Contracts/Rooms/RoomProjectionContracts.cs` → `Assets/ShooterMover/Runtime/Contracts/Rooms/RoomViewContracts.cs`
- `Assets/ShooterMover/Runtime/Contracts/Rooms/RoomProjectionLifecycle.cs` → `Assets/ShooterMover/Runtime/Contracts/Rooms/RoomViewLifecycle.cs`
- `Assets/ShooterMover/Runtime/CriticalHits/CriticalHitResolutionV1.cs` → `Assets/ShooterMover/Runtime/CriticalHits/CriticalHitResolution.cs`
- `Assets/ShooterMover/Runtime/Domain/Characters/DerivedCharacterStatsV1.cs` → `Assets/ShooterMover/Runtime/Domain/Characters/DerivedCharacterStats.cs`
- `Assets/ShooterMover/Runtime/Domain/Characters/Selection/CharacterSelectionCatalogV1.cs` → `Assets/ShooterMover/Runtime/Domain/Characters/Selection/CharacterSelectionCatalog.cs`
- `Assets/ShooterMover/Runtime/Domain/Characters/Selection/CharacterSelectionDefinitionsV1.cs` → `Assets/ShooterMover/Runtime/Domain/Characters/Selection/CharacterSelectionDefinitions.cs`
- `Assets/ShooterMover/Runtime/Domain/Combat/WeaponRuntimeProfile.cs` → `Assets/ShooterMover/Runtime/Domain/Combat/WeaponLiveProfile.cs`
- `Assets/ShooterMover/Runtime/Domain/Combat/WeaponRuntimeProfileValidator.cs` → `Assets/ShooterMover/Runtime/Domain/Combat/WeaponLiveProfileValidator.cs`
- `Assets/ShooterMover/Runtime/Domain/Crafting/CraftingRecipeV1.cs` → `Assets/ShooterMover/Runtime/Domain/Crafting/CraftingRecipe.cs`
- `Assets/ShooterMover/Runtime/Domain/Economy/Scrap/ScrapWalletModelV1.cs` → `Assets/ShooterMover/Runtime/Domain/Economy/Scrap/ScrapWalletModel.cs`
- `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyAttackDescriptorCompatibilityV1.cs` → `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyAttackDescriptorCompatibility.cs`
- `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogModelV1.cs` → `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogModel.cs`
- `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogV1.cs` → `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalog.cs`
- `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogValidationTypesV1.cs` → `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogValidationTypes.cs`
- `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogValidatorAttacksV1.cs` → `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogValidatorAttacks.cs`
- `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogValidatorV1.cs` → `Assets/ShooterMover/Runtime/Domain/Enemies/Catalog/EnemyCatalogValidator.cs`
- `Assets/ShooterMover/Runtime/Domain/Equipment/GeneratedEquipmentAugmentSignatureV1.cs` → `Assets/ShooterMover/Runtime/Domain/Equipment/GeneratedEquipmentAugmentSignature.cs`
- `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgradeCanonicalV1.cs` → `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgrade.cs`
- `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgradeConfirmationV1.cs` → `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgradeConfirmation.cs`
- `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgradeFactV1.cs` → `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgradeFact.cs`
- `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgradeModelV1.cs` → `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgradeModel.cs`
- `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgradeQuoteV1.cs` → `Assets/ShooterMover/Runtime/Domain/Equipment/Upgrades/AugmentUpgradeQuote.cs`
- `Assets/ShooterMover/Runtime/Domain/Holdings/PlayerHoldingsModelV1.cs` → `Assets/ShooterMover/Runtime/Domain/Holdings/PlayerHoldingsModel.cs`
- `Assets/ShooterMover/Runtime/Domain/Missions/Rooms/RoomGraphDefinitionV1.cs` → `Assets/ShooterMover/Runtime/Domain/Missions/Rooms/RoomGraphDefinition.cs`
- `Assets/ShooterMover/Runtime/Domain/Modifiers/Events/SpecialEventModifierContextV1.cs` → `Assets/ShooterMover/Runtime/Domain/Modifiers/Events/SpecialEventModifierContext.cs`
- `Assets/ShooterMover/Runtime/Domain/Modifiers/RuntimeModifierFoundationV1.cs` → `Assets/ShooterMover/Runtime/Domain/Modifiers/LiveModifierFoundation.cs`
- `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/ActiveStatusEffectSnapshotV1.cs` → `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/ActiveStatusEffectSnapshot.cs`
- `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/ActiveStatusEffectStackSnapshotV1.cs` → `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/ActiveStatusEffectStackSnapshot.cs`
- `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectCommandResultV1.cs` → `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectCommandResult.cs`
- `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectDefinitionsV1.cs` → `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectDefinitions.cs`
- `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectFingerprintV1.cs` → `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectFingerprint.cs`
- `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectReplaySnapshotsV1.cs` → `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectReplaySnapshots.cs`
- `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectResultEnumsV1.cs` → `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectResultEnums.cs`
- `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectStateSnapshotV1.cs` → `Assets/ShooterMover/Runtime/Domain/Modifiers/StatusEffects/StatusEffectStateSnapshot.cs`
- `Assets/ShooterMover/Runtime/Domain/Persistence/Accounts/PlayerAccountSnapshotV1.cs` → `Assets/ShooterMover/Runtime/Domain/Persistence/Accounts/PlayerAccountSnapshot.cs`
- `Assets/ShooterMover/Runtime/Domain/Progression/Experience/PlayerExperienceModelV1.cs` → `Assets/ShooterMover/Runtime/Domain/Progression/Experience/PlayerExperienceModel.cs`
- `Assets/ShooterMover/Runtime/Domain/Progression/Skills/RankedSkillFoundationV2.cs` → `Assets/ShooterMover/Runtime/Domain/Progression/Skills/RankedSkillFoundation.cs`
- `Assets/ShooterMover/Runtime/Domain/Progression/Skills/SkillProgressionV1.cs` → `Assets/ShooterMover/Runtime/Domain/Progression/Skills/SkillProgression.cs`
- `Assets/ShooterMover/Runtime/Domain/Props/PropCapabilitiesV1.cs` → `Assets/ShooterMover/Runtime/Domain/Props/PropCapabilities.cs`
- `Assets/ShooterMover/Runtime/Domain/Props/PropCapabilityRegistryV1.cs` → `Assets/ShooterMover/Runtime/Domain/Props/PropCapabilityRegistry.cs`
- `Assets/ShooterMover/Runtime/Domain/Props/PropCatalogV1.cs` → `Assets/ShooterMover/Runtime/Domain/Props/PropCatalog.cs`
- `Assets/ShooterMover/Runtime/Domain/Props/PropRuntimeContractsV1.cs` → `Assets/ShooterMover/Runtime/Domain/Props/PropLiveContracts.cs`
- `Assets/ShooterMover/Runtime/Domain/Props/PropRuntimeFactoryV1.cs` → `Assets/ShooterMover/Runtime/Domain/Props/PropLiveFactory.cs`
- `Assets/ShooterMover/Runtime/Domain/Props/PropRuntimeV1.cs` → `Assets/ShooterMover/Runtime/Domain/Props/PropLive.cs`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Application/RewardApplicationModelV1.cs` → `Assets/ShooterMover/Runtime/Domain/Rewards/Application/RewardApplicationModel.cs`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/ParticipantDropPacingStateV1.cs` → `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/ParticipantDropPacingState.cs`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/PersonalRewardRollContextV1.cs` → `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/PersonalRewardRollContext.cs`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RewardOutcomeV1.cs` → `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RewardOutcome.cs`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RewardProfileOverrideV1.cs` → `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RewardProfileOverride.cs`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RewardProfileResolutionV1.cs` → `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RewardProfileResolution.cs`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RewardRollGroupV1.cs` → `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RewardRollGroup.cs`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RewardSourceProfileV1.cs` → `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RewardSourceProfile.cs`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RunDropPacingPolicyV1.cs` → `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/RunDropPacingPolicy.cs`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/StrongboxTierSelectionProfileV1.cs` → `Assets/ShooterMover/Runtime/Domain/Rewards/Drops/StrongboxTierSelectionProfile.cs`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Generation/RewardGenerationPolicyV1.cs` → `Assets/ShooterMover/Runtime/Domain/Rewards/Generation/RewardGenerationPolicy.cs`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Model/RewardGrantModelV1.cs` → `Assets/ShooterMover/Runtime/Domain/Rewards/Model/RewardGrantModel.cs`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Model/RewardProfileV1.cs` → `Assets/ShooterMover/Runtime/Domain/Rewards/Model/RewardProfile.cs`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxAugmentSignatureV1.cs` → `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxAugmentSignature.cs`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxDefinitionRarityIdsV1.cs` → `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxDefinitionRarityIds.cs`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxDistanceWeightV1.cs` → `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxDistanceWeight.cs`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxHybridLootPolicyV1.cs` → `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxHybridLootPolicy.cs`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxHybridLootPolicyValidationV1.cs` → `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxHybridLootPolicyValidation.cs`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxHybridLootRandomV1.cs` → `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxHybridLootRandom.cs`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxInstanceLevelRollV1.cs` → `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxInstanceLevelRoll.cs`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxRarityProfileV1.cs` → `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxRarityProfile.cs`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxTargetLevelRollV1.cs` → `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxTargetLevelRoll.cs`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxWeightedIntOutcomeV1.cs` → `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/Hybrid/StrongboxWeightedIntOutcome.cs`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/StrongboxModelsV1.cs` → `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/StrongboxModels.cs`
- `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/StrongboxPowerBudgetV1.cs` → `Assets/ShooterMover/Runtime/Domain/Rewards/Strongboxes/StrongboxPowerBudget.cs`
- `Assets/ShooterMover/Runtime/Domain/Shops/ShopDefinitionV1.cs` → `Assets/ShooterMover/Runtime/Domain/Shops/ShopDefinition.cs`
- `Assets/ShooterMover/Runtime/Domain/Shops/ShopRuntimeModelV1.cs` → `Assets/ShooterMover/Runtime/Domain/Shops/ShopLiveModel.cs`
- `Assets/ShooterMover/Runtime/Domain/Weapons/Catalog/WeaponCatalogModel.Core.cs` → `Assets/ShooterMover/Runtime/Domain/Weapons/Catalog/WeaponCatalogModelCore.cs`
- `Assets/ShooterMover/Runtime/Domain/Weapons/Catalog/WeaponCatalogModel.Definitions.cs` → `Assets/ShooterMover/Runtime/Domain/Weapons/Catalog/WeaponCatalogModelDefinitions.cs`
- `Assets/ShooterMover/Runtime/Domain/Weapons/Catalog/WeaponCatalogValidator.Catalog.cs` → `Assets/ShooterMover/Runtime/Domain/Weapons/Catalog/WeaponCatalogValidatorCatalog.cs`
- `Assets/ShooterMover/Runtime/Domain/Weapons/Catalog/WeaponCatalogValidator.Definitions.cs` → `Assets/ShooterMover/Runtime/Domain/Weapons/Catalog/WeaponCatalogValidatorDefinitions.cs`
- `Assets/ShooterMover/Runtime/Domain/Weapons/Catalog/WeaponCatalogValidator.Helpers.cs` → `Assets/ShooterMover/Runtime/Domain/Weapons/Catalog/WeaponCatalogValidatorHelpers.cs`
- `Assets/ShooterMover/Runtime/EnemyAttackPatternUnityIntegration/EnemyAttackPatternHitRouterV1.cs` → `Assets/ShooterMover/Runtime/EnemyAttackPatternUnityIntegration/EnemyAttackPatternHitRouter.cs`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/BuiltInEnemyRuntimePoliciesV1.cs` → `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/BuiltInEnemyLivePolicies.cs`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternAuthorityV1.cs` → `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternState.cs`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternContractsV1.cs` → `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternContracts.cs`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternDispatchContractsV1.cs` → `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternDispatchContracts.cs`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternDispatchV1.cs` → `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternDispatch.cs`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternEmissionsV1.cs` → `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternEmissions.cs`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternFingerprintV1.cs` → `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternFingerprint.cs`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternResultsV1.cs` → `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternResults.cs`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternSchedulerV1.cs` → `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyAttackPatternScheduler.cs`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyPlacementRuntimeAttackPatternAuthorityV1.cs` → `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyPlacementLiveAttackPatternState.cs`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyPlacementRuntimeCombatAuthorityV1.cs` → `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyPlacementLiveCombatState.cs`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyPlacementRuntimeFactoryV1.cs` → `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyPlacementLiveFactory.cs`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyPlacementRuntimeInstanceV1.cs` → `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyPlacementLiveInstance.cs`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyPlacementRuntimeStateAuthorityV1.cs` → `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyPlacementLiveState.cs`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeAuthorityFingerprintV1.cs` → `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyLiveStateFingerprint.cs`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionAssemblyInfo.cs` → `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyLiveSetupAssemblyInfo.cs`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimeCompositionContractsV1.cs` → `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyLiveSetupContracts.cs`
- `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyRuntimePolicyRegistryV1.cs` → `Assets/ShooterMover/Runtime/EnemyRuntimeComposition/EnemyLivePolicyRegistry.cs`
- `Assets/ShooterMover/Runtime/GameplayEntities/Enemies/EnemyRuntimeProjection.cs` → `Assets/ShooterMover/Runtime/GameplayEntities/Enemies/EnemyLiveView.cs`
- `Assets/ShooterMover/Runtime/GameplayEntities/PlayerActorAuthority.cs` → `Assets/ShooterMover/Runtime/GameplayEntities/PlayerActorState.cs`
- `Assets/ShooterMover/Runtime/RunConditionIntegration/ProductionConditionBoundRunSessionStartSourceV1.cs` → `Assets/ShooterMover/Runtime/RunConditionIntegration/ConditionBoundRunSessionStartSource.cs`
- `Assets/ShooterMover/Runtime/RunConditionIntegration/RunConditionIntegrationV1.cs` → `Assets/ShooterMover/Runtime/RunConditionIntegration/RunConditionIntegration.cs`
- `Assets/ShooterMover/Runtime/RunConditionIntegration/StrongboxPersistentRunIntegrationV1.cs` → `Assets/ShooterMover/Runtime/RunConditionIntegration/StrongboxPersistentRunIntegration.cs`
- `Assets/ShooterMover/Runtime/RunPickups/ExistingRunSessionPickupPortV1.cs` → `Assets/ShooterMover/Runtime/RunPickups/ExistingRunSessionPickupPort.cs`
- `Assets/ShooterMover/Runtime/RunPickups/RunLocalPickupAuthorityExportsV1.cs` → `Assets/ShooterMover/Runtime/RunPickups/RunLocalPickupStateExports.cs`
- `Assets/ShooterMover/Runtime/RunPickups/RunLocalPickupAuthorityV1.cs` → `Assets/ShooterMover/Runtime/RunPickups/RunLocalPickupState.cs`
- `Assets/ShooterMover/Runtime/RunPickups/RunLocalPickupCollectionAuthorityV1.cs` → `Assets/ShooterMover/Runtime/RunPickups/RunLocalPickupCollectionState.cs`
- `Assets/ShooterMover/Runtime/RunPickups/RunPickupCollectionContractsV1.cs` → `Assets/ShooterMover/Runtime/RunPickups/RunPickupCollectionContracts.cs`
- `Assets/ShooterMover/Runtime/RunPickups/RunPickupContractsV1.cs` → `Assets/ShooterMover/Runtime/RunPickups/RunPickupContracts.cs`
- `Assets/ShooterMover/Runtime/RunPickups/RunPickupPortsAndIdentityV1.cs` → `Assets/ShooterMover/Runtime/RunPickups/RunPickupPortsAndIdentity.cs`
- `Assets/ShooterMover/Runtime/RunPickups/RunPickupSnapshotContractsV1.cs` → `Assets/ShooterMover/Runtime/RunPickups/RunPickupSnapshotContracts.cs`
- `Assets/ShooterMover/Runtime/RunPickups/TerminalDropRunPickupAdapterV1.cs` → `Assets/ShooterMover/Runtime/RunPickups/TerminalDropRunPickupBridge.cs`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/EnemyTerminalSourceContextAdapterV1.cs` → `Assets/ShooterMover/Runtime/TerminalDropBinding/EnemyTerminalSourceContextBridge.cs`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/PendingTerminalDropAdmissionV1.cs` → `Assets/ShooterMover/Runtime/TerminalDropBinding/PendingTerminalDropAdmission.cs`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/RunSessionTerminalDropContextResolverV1.cs` → `Assets/ShooterMover/Runtime/TerminalDropBinding/RunSessionTerminalDropContextResolver.cs`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/RunSessionTerminalRewardEnvironmentResolverV1.cs` → `Assets/ShooterMover/Runtime/TerminalDropBinding/RunSessionTerminalRewardEnvironmentResolver.cs`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/RunSessionTerminalRewardOverrideResolverV1.cs` → `Assets/ShooterMover/Runtime/TerminalDropBinding/RunSessionTerminalRewardOverrideResolver.cs`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/RunSessionTerminalRewardParticipantResolverV1.cs` → `Assets/ShooterMover/Runtime/TerminalDropBinding/RunSessionTerminalRewardParticipantResolver.cs`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropBindingCompositionV1.cs` → `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropBindingSetup.cs`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropBindingContractsV1.cs` → `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropBindingContracts.cs`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropFactAdaptersV1.cs` → `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropFactAdapters.cs`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropGenerationAuthorityV1.cs` → `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalDropGenerationState.cs`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalPersonalRewardContractsV1.cs` → `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalPersonalRewardContracts.cs`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalPersonalRewardDefaultsV1.cs` → `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalPersonalRewardDefaults.cs`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalPersonalRewardGenerationAuthorityV1.cs` → `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalPersonalRewardGenerationState.cs`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalPersonalRewardTransportAdapterV1.cs` → `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalPersonalRewardTransportBridge.cs`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalRewardPlacementFactV1.cs` → `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalRewardPlacementFact.cs`
- `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalRunMinimumGenerationAuthorityV1.cs` → `Assets/ShooterMover/Runtime/TerminalDropBinding/TerminalRunMinimumGenerationState.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelDesignFoundationValidator.Connections.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelDesignFoundationValidatorConnections.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelDesignFoundationValidator.Core.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelDesignFoundationValidatorCore.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelGridAuthoringV2CompositeValidator.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelGridAuthoringCompositeValidator.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelGridAuthoringV2Model.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelGridAuthoringModel.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelGridAuthoringV2Validator.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelGridAuthoringValidator.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelGridPlayableMetadataV2.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelGridPlayableMetadata.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelGridPlayableValidationV2.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelGridPlayableValidation.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelGridV2RoomFolderMigration.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Authoring/LevelDesign/LevelGridRoomFolderMigration.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Combat/CombatHit2DAdapter.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Combat/CombatHit2DBridge.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Combat/PlayerCombatIntentAdapter.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Combat/PlayerCombatIntentBridge.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Combat/WeaponMount2DAdapter.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Combat/WeaponMount2DBridge.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/CombatPresentation/CombatHealthPresentationV1.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/CombatPresentation/CombatHealthPresentation.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/CombatPresentation/CombatPresentationEnemyActorAuthority2D.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/CombatPresentation/CombatPresentationEnemyActorState2D.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyActor2DAdapter.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyActor2DBridge.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyAttackPatternLiveSchedulerV1.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyAttackPatternLiveScheduler.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyAttackPortV1.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyAttackPort.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyCommittedAttackPatternExecutorV1.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyCommittedAttackPatternExecutor.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyContact2DAdapter.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyContact2DBridge.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyHitV1.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyHit.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyTarget2DAdapter.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/EnemyTarget2DBridge.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/Presentation/EnemyAttackPresentationProjection2D.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/Presentation/EnemyAttackPresentationView2D.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/Presentation/EnemyPresentationAdapter2D.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/Presentation/EnemyPresentationBridge2D.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/RunSessionEnemyAttackPatternTimeV1.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Enemies/RunSessionEnemyAttackPatternTime.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Input/PlayerMovementIntentAdapter.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Input/PlayerMovementIntentBridge.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Missions/Rooms/JsonRoomRuntimeBootstrap2D.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Missions/Rooms/JsonRoomLiveBootstrap2D.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Missions/Rooms/RoomRuntimeComposition2D.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Missions/Rooms/RoomLiveSetup2D.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Missions/Rooms/RoomRuntimePresentationInstances2D.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Missions/Rooms/RoomLivePresentationInstances2D.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Physics/MovementBody2DAdapter.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Physics/MovementBody2DBridge.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Physics/MovementContact2DAdapter.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Physics/MovementContact2DBridge.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Players/CanonicalPlayerWeaponSourceV2.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Players/PlayerWeaponSource.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Players/MovementActorPlayerRuntimeAdapter.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Players/MovementActorPlayerLiveBridge.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Players/PlayerInventoryWeaponRuntimeComposition.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Players/PlayerInventoryWeaponLiveSetup.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Players/PlayerRuntimeComposition.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Players/PlayerLiveSetup.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Players/PlayerRuntimeContracts.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Players/PlayerLiveContracts.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Players/RunSessions/ExistingPlayerAndWeaponRunPortsV1.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Players/RunSessions/ExistingPlayerAndWeaponRunPorts.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Presentation/Weapons/WeaponArtSpriteRegistryV1.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Presentation/Weapons/WeaponArtSpriteRegistry.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Progression/Experience/EnemyRewards/EnemyExperienceRewardCatalogAssetV1.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Progression/Experience/EnemyRewards/EnemyExperienceRewardCatalogAsset.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Progression/Experience/EnemyRewards/EnemyExperienceRewardingAuthorityV1.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Progression/Experience/EnemyRewards/EnemyExperienceRewardingState.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/Pickups/RewardPickupApplicationAuthority2D.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/Pickups/RewardPickupApplicationState2D.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/RunPickups/PendingAdmissionPickupBridgeV1.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/RunPickups/PendingAdmissionPickupBridge.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/RunPickups/RunPickupAuthorityHost2D.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/RunPickups/RunPickupStateHost2D.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/RunPickups/RunPickupPresentationLifecycleV1.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Rewards/RunPickups/RunPickupPresentationLifecycle.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/CanonicalWeaponEquipmentProjectionLookupV2.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/WeaponEquipmentViewLookup.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/InventoryBackedWeaponExecutionAdapter.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/InventoryBackedWeaponExecutionBridge.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/InventoryWeaponMountedAimExecutionV1.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/InventoryWeaponMountedAimExecution.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/InventoryWeaponRuntimeComposition.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/InventoryWeaponLiveSetup.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/ProductionCanonicalNormalProjectile2D.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/NormalProjectile2D.cs`
- `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/ProductionCanonicalProjectileEffectSink2D.cs` → `Assets/ShooterMover/Runtime/UnityAdapters/Weapons/Live/ProjectileEffectSink2D.cs`
- `Assets/ShooterMover/Tests/EditMode/BalanceSimulator/BalanceSimulationServiceV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/BalanceSimulator/BalanceSimulationActionsTests.cs`
- `Assets/ShooterMover/Tests/EditMode/BalanceSimulator/LootboxSimulatorRuntimeV1.AuthoritativeTests.cs` → `Assets/ShooterMover/Tests/EditMode/BalanceSimulator/LootboxSimulatorLiveV1AuthoritativeTests.cs`
- `Assets/ShooterMover/Tests/EditMode/BalanceSimulator/LootboxSimulatorRuntimeV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/BalanceSimulator/LootboxSimulatorLiveTests.cs`
- `Assets/ShooterMover/Tests/EditMode/BalanceSimulator/ProductionStrongboxCatalogV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/BalanceSimulator/StrongboxCatalogTests.cs`
- `Assets/ShooterMover/Tests/EditMode/CanonicalWeaponProjectileSourceIdentityTests.cs` → `Assets/ShooterMover/Tests/EditMode/WeaponProjectileSourceIdentityTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Characters/Selection/CharacterSelectionServiceV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Characters/Selection/CharacterSelectionActionsTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Combat/WeaponRuntimeProfileTests.cs` → `Assets/ShooterMover/Tests/EditMode/Combat/WeaponLiveProfileTests.cs`
- `Assets/ShooterMover/Tests/EditMode/CombatHitPolicy/CombatHitPolicyV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/CombatHitPolicy/CombatHitPolicyTests.cs`
- `Assets/ShooterMover/Tests/EditMode/CombatPresentation/CombatDeathVfxFactoryV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/CombatPresentation/CombatDeathVfxFactoryTests.cs`
- `Assets/ShooterMover/Tests/EditMode/CombatPresentation/CombatPresentationV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/CombatPresentation/CombatPresentationTests.cs`
- `Assets/ShooterMover/Tests/EditMode/ConditionRuntime/ConditionRuntimeAuthorityV1ReplayHardeningTests.cs` → `Assets/ShooterMover/Tests/EditMode/ConditionRuntime/ConditionLiveStateReplayHardeningTests.cs`
- `Assets/ShooterMover/Tests/EditMode/ConditionRuntime/ConditionRuntimeAuthorityV1TestSupport.cs` → `Assets/ShooterMover/Tests/EditMode/ConditionRuntime/ConditionLiveStateTestSupport.cs`
- `Assets/ShooterMover/Tests/EditMode/ConditionRuntime/ConditionRuntimeAuthorityV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/ConditionRuntime/ConditionLiveStateTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Crafting/CraftingServiceV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Crafting/CraftingActionsTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Crafting/Integration/CraftingInventoryEquipServiceV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Crafting/Integration/CraftingInventoryEquipActionsTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Crafting/Presentation/FakeCraftingAuthority.cs` → `Assets/ShooterMover/Tests/EditMode/Crafting/Presentation/FakeCraftingState.cs`
- `Assets/ShooterMover/Tests/EditMode/CriticalHits/CriticalHitOrdinalV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/CriticalHits/CriticalHitOrdinalTests.cs`
- `Assets/ShooterMover/Tests/EditMode/CriticalHits/CriticalHitResolutionV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/CriticalHits/CriticalHitResolutionTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Economy/Scrap/ScrapLedgerPayloadV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Economy/Scrap/ScrapLedgerPayloadTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Economy/Scrap/ScrapWalletServiceV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Economy/Scrap/ScrapWalletActionsTests.cs`
- `Assets/ShooterMover/Tests/EditMode/EditorTooling/LevelDoorAuthorityV2Tests.cs` → `Assets/ShooterMover/Tests/EditMode/EditorTooling/LevelDoorStateTests.cs`
- `Assets/ShooterMover/Tests/EditMode/EditorTooling/LevelGridEditorRuntimeIntegrationV2Tests.cs` → `Assets/ShooterMover/Tests/EditMode/EditorTooling/LevelGridEditorLiveIntegrationTests.cs`
- `Assets/ShooterMover/Tests/EditMode/EditorTooling/LevelGridPlayableBuildPathOwnershipV2Tests.cs` → `Assets/ShooterMover/Tests/EditMode/EditorTooling/LevelGridPlayableBuildPathOwnershipTests.cs`
- `Assets/ShooterMover/Tests/EditMode/EditorTooling/LevelGridV2AssetCompilerPublicationTests.cs` → `Assets/ShooterMover/Tests/EditMode/EditorTooling/LevelGridAssetCompilerPublicationTests.cs`
- `Assets/ShooterMover/Tests/EditMode/EditorTooling/LevelSystemStabilizationV2Tests.cs` → `Assets/ShooterMover/Tests/EditMode/EditorTooling/LevelSystemStabilizationTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyAttackPatternCatalogV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyAttackPatternCatalogTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyAttackPatternEmissionFingerprintV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyAttackPatternEmissionFingerprintTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyAttackPatternLegacyCutoverV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyAttackPatternLegacyCutoverTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyAttackPatternLiveDispatchFailureV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyAttackPatternLiveDispatchFailureTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyAttackPatternLiveIntegrationPortsV1.cs` → `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyAttackPatternLiveIntegrationPorts.cs`
- `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyAttackPatternLiveIntegrationV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyAttackPatternLiveIntegrationTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyAttackPatternLiveSchedulerV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyAttackPatternLiveSchedulerTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyAttackPatternRuntimeV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyAttackPatternLiveTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyAttackPatternTestFixturesV1.cs` → `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyAttackPatternTestFixtures.cs`
- `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyAttackPatternTransactionalFailureV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyAttackPatternTransactionalFailureTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyCatalogJsonImporterV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyCatalogJsonImporterTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyPlacementRuntimeAuthorityBoundaryV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyPlacementLiveStateBoundaryTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyPlacementRuntimeFactoryV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyPlacementLiveFactoryTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyPlacementRuntimeLifecycleRoutingV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyPlacementLiveLifecycleRoutingTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyRuntimeFoundationTests.cs` → `Assets/ShooterMover/Tests/EditMode/Enemies/EnemyLiveFoundationTests.cs`
- `Assets/ShooterMover/Tests/EditMode/EnemyAttackPatternUnityIntegration/EnemyAttackPatternHitRouterV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/EnemyAttackPatternUnityIntegration/EnemyAttackPatternHitRouterTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Equipment/Upgrades/AugmentUpgradeServiceV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Equipment/Upgrades/AugmentUpgradeActionsTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Equipment/Upgrades/LegacyFirstWeaponHoldingsAdapterRetirementTests.cs` → `Assets/ShooterMover/Tests/EditMode/Equipment/Upgrades/LegacyFirstWeaponHoldingsBridgeRetirementTests.cs`
- `Assets/ShooterMover/Tests/EditMode/ExtensibilityGuardrailsV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/ExtensibilityGuardrailsTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Flow/Hub/CanonicalFirstPlayerHoldingsAuthorityV2Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Flow/Hub/FirstPlayerHoldingsStateTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Flow/Hub/ProductionExactWeaponInstanceLoadoutTests.cs` → `Assets/ShooterMover/Tests/EditMode/Flow/Hub/ExactWeaponInstanceLoadoutTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Flow/Hub/ProductionFlowSessionV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Flow/Hub/FlowSessionTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Flow/Hub/ProductionOpaqueWeaponInstanceIdentityTests.cs` → `Assets/ShooterMover/Tests/EditMode/Flow/Hub/OpaqueWeaponInstanceIdentityTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Flow/Hub/ProductionWeaponMountPolicyV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Flow/Hub/WeaponMountPolicyTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Flow/Hub/WeaponInventoryLiveV2PersistenceTests.cs` → `Assets/ShooterMover/Tests/EditMode/Flow/Hub/WeaponInventoryLivePersistenceTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Flow/PlaySelection/PlaySelectionServiceTests.cs` → `Assets/ShooterMover/Tests/EditMode/Flow/PlaySelection/PlaySelectionActionsTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Flow/ProductionPlayableLevelCatalogAvailabilityTests.cs` → `Assets/ShooterMover/Tests/EditMode/Flow/PlayableLevelCatalogAvailabilityTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Foundation/BootstrapCompositionRootTests.cs` → `Assets/ShooterMover/Tests/EditMode/Foundation/BootstrapSetupRootTests.cs`
- `Assets/ShooterMover/Tests/EditMode/GameplayEntities/PlayerActorAuthorityTests.cs` → `Assets/ShooterMover/Tests/EditMode/GameplayEntities/PlayerActorStateTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Holdings/PlayerHoldingsServiceTests.cs` → `Assets/ShooterMover/Tests/EditMode/Holdings/PlayerHoldingsActionsTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Inventory/LoadoutScreen/InventoryLoadoutScreenServiceTests.cs` → `Assets/ShooterMover/Tests/EditMode/Inventory/LoadoutScreen/InventoryLoadoutScreenActionsTests.cs`
- `Assets/ShooterMover/Tests/EditMode/LevelDesign/Foundation/LevelGridAuthoringV2AuditTests.cs` → `Assets/ShooterMover/Tests/EditMode/LevelDesign/Foundation/LevelGridAuthoringAuditTests.cs`
- `Assets/ShooterMover/Tests/EditMode/LevelDesign/Foundation/LevelGridAuthoringV2Tests.cs` → `Assets/ShooterMover/Tests/EditMode/LevelDesign/Foundation/LevelGridAuthoringTests.cs`
- `Assets/ShooterMover/Tests/EditMode/LevelDesign/Foundation/LevelGridEditorSecondAuditV2Tests.cs` → `Assets/ShooterMover/Tests/EditMode/LevelDesign/Foundation/LevelGridEditorSecondAuditTests.cs`
- `Assets/ShooterMover/Tests/EditMode/LevelDesign/Foundation/LevelGridEditorTargetedFixesV2Tests.cs` → `Assets/ShooterMover/Tests/EditMode/LevelDesign/Foundation/LevelGridEditorTargetedFixesTests.cs`
- `Assets/ShooterMover/Tests/EditMode/LevelDesign/Foundation/LevelGridEditorWindowV2Tests.cs` → `Assets/ShooterMover/Tests/EditMode/LevelDesign/Foundation/LevelGridEditorWindowTests.cs`
- `Assets/ShooterMover/Tests/EditMode/LevelDesign/Foundation/LevelGridV2CompilerTests.cs` → `Assets/ShooterMover/Tests/EditMode/LevelDesign/Foundation/LevelGridCompilerTests.cs`
- `Assets/ShooterMover/Tests/EditMode/LevelDesign/Foundation/LevelGridV2SecondAuditRegressionTests.cs` → `Assets/ShooterMover/Tests/EditMode/LevelDesign/Foundation/LevelGridSecondAuditRegressionTests.cs`
- `Assets/ShooterMover/Tests/EditMode/LevelDesign/Foundation/LevelGridV2UnityMetadataRegressionTests.cs` → `Assets/ShooterMover/Tests/EditMode/LevelDesign/Foundation/LevelGridUnityMetadataRegressionTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Missions/Results/MissionRunResultAuthorityV1Tests.Fixtures.cs` → `Assets/ShooterMover/Tests/EditMode/Missions/Results/MissionRunResultStateTestsFixtures.cs`
- `Assets/ShooterMover/Tests/EditMode/Missions/Results/MissionRunResultAuthorityV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Missions/Results/MissionRunResultStateTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/JsonRoomRuntimeBootstrapCompositionTests.cs` → `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/JsonRoomLiveBootstrapSetupTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/RoomAccessAuthorityV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/RoomAccessStateTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/RoomAccessJsonImporterV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/RoomAccessJsonImporterTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/RoomContentJsonImporterV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/RoomContentJsonImporterTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/RoomLiveRuntimeAuthorityTests.Part02.cs` → `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/RoomLiveStateTestsPart02.cs`
- `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/RoomLiveRuntimeAuthorityTests.Part03.cs` → `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/RoomLiveStateTestsPart03.cs`
- `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/RoomLiveRuntimeAuthorityTests.cs` → `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/RoomLiveStateTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/RoomRuntimeAuthorityTests.cs` → `Assets/ShooterMover/Tests/EditMode/Missions/Rooms/RoomLiveStateTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Modifiers/DerivedCharacterStatsV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Modifiers/DerivedCharacterStatsTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Modifiers/Events/ActiveEventModifierProjectionV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Modifiers/Events/ActiveEventModifierViewTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Modifiers/Events/EventStampedCommandEnvelopeV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Modifiers/Events/EventStampedCommandEnvelopeTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Modifiers/RuntimeModifierFoundationV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Modifiers/LiveModifierFoundationTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Modifiers/StatusEffects/StatusEffectAuthorityV1ApplyPolicyTests.cs` → `Assets/ShooterMover/Tests/EditMode/Modifiers/StatusEffects/StatusEffectStateApplyPolicyTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Modifiers/StatusEffects/StatusEffectAuthorityV1BridgeCatalogTests.cs` → `Assets/ShooterMover/Tests/EditMode/Modifiers/StatusEffects/StatusEffectStateBridgeCatalogTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Modifiers/StatusEffects/StatusEffectAuthorityV1ReplayLifecycleTests.cs` → `Assets/ShooterMover/Tests/EditMode/Modifiers/StatusEffects/StatusEffectStateReplayLifecycleTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Modifiers/StatusEffects/StatusEffectAuthorityV1StackingTests.cs` → `Assets/ShooterMover/Tests/EditMode/Modifiers/StatusEffects/StatusEffectStateStackingTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Persistence/Accounts/PlayerAccountSaveAuthorityV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Persistence/Accounts/PlayerAccountSaveStateTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Persistence/Components/AccountCompatibilityV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Persistence/Components/AccountCompatibilityTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Persistence/Components/AtomicSaveAndCompensationV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Persistence/Components/AtomicSaveAndCompensationTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Persistence/Components/ExplicitCodecGoldenV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Persistence/Components/ExplicitCodecGoldenTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Persistence/Components/PersistenceTestEconomyTransactionStatusV1.cs` → `Assets/ShooterMover/Tests/EditMode/Persistence/Components/PersistenceTestEconomyTransactionStatus.cs`
- `Assets/ShooterMover/Tests/EditMode/Persistence/Components/PersistenceTestHoldingProvenanceV1.cs` → `Assets/ShooterMover/Tests/EditMode/Persistence/Components/PersistenceTestHoldingProvenance.cs`
- `Assets/ShooterMover/Tests/EditMode/Persistence/Components/SaveAdaptersV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Persistence/Components/SaveAdaptersTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Persistence/Components/StrongboxSaveAdapterReplayTests.cs` → `Assets/ShooterMover/Tests/EditMode/Persistence/Components/StrongboxSaveBridgeReplayTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Persistence/Composition/CharacterCompositionCoordinatorV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Persistence/Composition/CharacterSetupFlowTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Persistence/Composition/CollectedRunRewardAtomicCoordinatorTests.cs` → `Assets/ShooterMover/Tests/EditMode/Persistence/Composition/CollectedRunRewardAtomicFlowTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Persistence/Composition/ProductionWeaponOnboardingAndMigrationTests.cs` → `Assets/ShooterMover/Tests/EditMode/Persistence/Composition/WeaponOnboardingAndMigrationTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Persistence/Composition/RequiredCharacterComponentBackfillV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Persistence/Composition/RequiredCharacterComponentBackfillTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Persistence/Composition/StrongboxPersistenceCoordinatorV1TestFixture.cs` → `Assets/ShooterMover/Tests/EditMode/Persistence/Composition/StrongboxPersistenceFlowTestFixture.cs`
- `Assets/ShooterMover/Tests/EditMode/PlayerRuntime/PlayerLiveAuthorityTests.cs` → `Assets/ShooterMover/Tests/EditMode/PlayerRuntime/PlayerLiveStateTests.cs`
- `Assets/ShooterMover/Tests/EditMode/PlayerRuntime/PlayerRuntimeCompositionTests.cs` → `Assets/ShooterMover/Tests/EditMode/PlayerRuntime/PlayerLiveSetupTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Progression/Experience/PlayerExperienceAuthorityTests.cs` → `Assets/ShooterMover/Tests/EditMode/Progression/Experience/PlayerExperienceStateTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Progression/Skills/RankedSkillFoundationV2Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Progression/Skills/RankedSkillFoundationTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Progression/Skills/SkillProgressionAuthorityTests.cs` → `Assets/ShooterMover/Tests/EditMode/Progression/Skills/SkillProgressionStateTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Props/DestructiblePropAuthorityTests.cs` → `Assets/ShooterMover/Tests/EditMode/Props/DestructiblePropStateTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Props/PropRuntimeV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Props/PropLiveTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Rewards/Application/RewardApplicationServiceV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Rewards/Application/RewardApplicationActionsTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Rewards/Drops/PersonalRewardGenerationV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Rewards/Drops/PersonalRewardGenerationTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Rewards/Drops/RunRewardRuntimeSnapshotV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Rewards/Drops/RunRewardLiveSnapshotTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Rewards/GameplayDrops/GameplayDropOperationV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Rewards/GameplayDrops/GameplayDropOperationTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Rewards/Generation/RewardGenerationServiceV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Rewards/Generation/RewardGenerationActionsTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Rewards/Strongboxes/StrongboxHybridLootV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Rewards/Strongboxes/StrongboxHybridLootTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Rewards/Strongboxes/StrongboxOpeningServiceV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Rewards/Strongboxes/StrongboxOpeningActionsTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Rewards/Strongboxes/StrongboxPowerBudgetV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Rewards/Strongboxes/StrongboxPowerBudgetTests.cs`
- `Assets/ShooterMover/Tests/EditMode/RunConditionBinding/RunConditionBindingV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/RunConditionBinding/RunConditionBindingTests.cs`
- `Assets/ShooterMover/Tests/EditMode/RunConditionBinding/RunConditionCheckpointV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/RunConditionBinding/RunConditionCheckpointTests.cs`
- `Assets/ShooterMover/Tests/EditMode/RunConditionBinding/RunConditionRestartAtomicityV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/RunConditionBinding/RunConditionRestartAtomicityTests.cs`
- `Assets/ShooterMover/Tests/EditMode/RunPickups/ExistingRunSessionPickupPortV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/RunPickups/ExistingRunSessionPickupPortTests.cs`
- `Assets/ShooterMover/Tests/EditMode/RunPickups/PendingAdmissionPickupBridgeV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/RunPickups/PendingAdmissionPickupBridgeTests.cs`
- `Assets/ShooterMover/Tests/EditMode/RunPickups/RunLocalPickupAuthorityCollectionTests.cs` → `Assets/ShooterMover/Tests/EditMode/RunPickups/RunLocalPickupStateCollectionTests.cs`
- `Assets/ShooterMover/Tests/EditMode/RunPickups/RunLocalPickupAuthorityTestSupport.cs` → `Assets/ShooterMover/Tests/EditMode/RunPickups/RunLocalPickupStateTestSupport.cs`
- `Assets/ShooterMover/Tests/EditMode/RunPickups/RunLocalPickupAuthorityV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/RunPickups/RunLocalPickupStateTests.cs`
- `Assets/ShooterMover/Tests/EditMode/RunSessions/RunSessionAuthorityV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/RunSessions/RunSessionStateTests.cs`
- `Assets/ShooterMover/Tests/EditMode/RunSessions/RunSessionDurableEndV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/RunSessions/RunSessionDurableEndTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Shops/Presentation/ShopScreenPresentationV1Tests.Fixtures.cs` → `Assets/ShooterMover/Tests/EditMode/Shops/Presentation/ShopScreenPresentationTestsFixtures.cs`
- `Assets/ShooterMover/Tests/EditMode/Shops/Presentation/ShopScreenPresentationV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Shops/Presentation/ShopScreenPresentationTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Shops/ShopRuntimeServiceV1Tests.Fixtures.cs` → `Assets/ShooterMover/Tests/EditMode/Shops/ShopLiveActionsTestsFixtures.cs`
- `Assets/ShooterMover/Tests/EditMode/Shops/ShopRuntimeServiceV1Tests.More.cs` → `Assets/ShooterMover/Tests/EditMode/Shops/ShopLiveActionsTestsMore.cs`
- `Assets/ShooterMover/Tests/EditMode/Shops/ShopRuntimeServiceV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Shops/ShopLiveActionsTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Skills/Presentation/RankedSkillsScreenSessionV2Tests.cs` → `Assets/ShooterMover/Tests/EditMode/Skills/Presentation/RankedSkillsScreenSessionTests.cs`
- `Assets/ShooterMover/Tests/EditMode/TerminalDropBinding/EnemyDeathTerminalDropFactAdapterV1.cs` → `Assets/ShooterMover/Tests/EditMode/TerminalDropBinding/EnemyDeathTerminalDropFactBridge.cs`
- `Assets/ShooterMover/Tests/EditMode/TerminalDropBinding/EnemyTerminalSourceContextAdapterV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/TerminalDropBinding/EnemyTerminalSourceContextBridgeTests.cs`
- `Assets/ShooterMover/Tests/EditMode/TerminalDropBinding/TerminalDropGenerationAuthorityV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/TerminalDropBinding/TerminalDropGenerationStateTests.cs`
- `Assets/ShooterMover/Tests/EditMode/TerminalDropBinding/TerminalDropPendingPublicationPolicyV1Tests.cs` → `Assets/ShooterMover/Tests/EditMode/TerminalDropBinding/TerminalDropPendingPublicationPolicyTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Weapons/Live/InventoryBackedWeaponExecutionAdapterFakes.cs` → `Assets/ShooterMover/Tests/EditMode/Weapons/Live/InventoryBackedWeaponExecutionBridgeFakes.cs`
- `Assets/ShooterMover/Tests/EditMode/Weapons/Live/InventoryBackedWeaponExecutionAdapterFixtures.cs` → `Assets/ShooterMover/Tests/EditMode/Weapons/Live/InventoryBackedWeaponExecutionBridgeFixtures.cs`
- `Assets/ShooterMover/Tests/EditMode/Weapons/Live/InventoryBackedWeaponExecutionAdapterReplayTests.cs` → `Assets/ShooterMover/Tests/EditMode/Weapons/Live/InventoryBackedWeaponExecutionBridgeReplayTests.cs`
- `Assets/ShooterMover/Tests/EditMode/Weapons/Live/InventoryBackedWeaponExecutionAdapterTests.cs` → `Assets/ShooterMover/Tests/EditMode/Weapons/Live/InventoryBackedWeaponExecutionBridgeTests.cs`
- `Assets/ShooterMover/Tests/PlayMode/Combat/PlayerCombatIntentAdapterTests.cs` → `Assets/ShooterMover/Tests/PlayMode/Combat/PlayerCombatIntentBridgeTests.cs`
- `Assets/ShooterMover/Tests/PlayMode/Combat/WeaponMount2DAdapterTests.cs` → `Assets/ShooterMover/Tests/PlayMode/Combat/WeaponMount2DBridgeTests.cs`
- `Assets/ShooterMover/Tests/PlayMode/Enemies/EnemyActor2DAdapterTests.cs` → `Assets/ShooterMover/Tests/PlayMode/Enemies/EnemyActor2DBridgeTests.cs`
- `Assets/ShooterMover/Tests/PlayMode/Environment/VoidHazards/VoidHazardAuthoring2DTests.Categories.cs` → `Assets/ShooterMover/Tests/PlayMode/Environment/VoidHazards/VoidHazardAuthoring2DTestsCategories.cs`
- `Assets/ShooterMover/Tests/PlayMode/Environment/VoidHazards/VoidHazardAuthoring2DTests.Player.cs` → `Assets/ShooterMover/Tests/PlayMode/Environment/VoidHazards/VoidHazardAuthoring2DTestsPlayer.cs`
- `Assets/ShooterMover/Tests/PlayMode/Flow/CharacterSelect/CharacterSelectControllerV1Tests.cs` → `Assets/ShooterMover/Tests/PlayMode/Flow/CharacterSelect/CharacterSelectControllerTests.cs`
- `Assets/ShooterMover/Tests/PlayMode/Flow/InventoryLoadout/InventoryLoadoutAuthorityConnectionTests.cs` → `Assets/ShooterMover/Tests/PlayMode/Flow/InventoryLoadout/InventoryLoadoutStateConnectionTests.cs`
- `Assets/ShooterMover/Tests/PlayMode/Flow/ProductionFlow/CanonicalUiOwnershipPlayModeTests.cs` → `Assets/ShooterMover/Tests/PlayMode/Flow/ProductionFlow/UiOwnershipPlayModeTests.cs`
- `Assets/ShooterMover/Tests/PlayMode/Flow/ProductionFlow/ProductionFlowPlayModeTests.cs` → `Assets/ShooterMover/Tests/PlayMode/Flow/ProductionFlow/FlowPlayModeTests.cs`
- `Assets/ShooterMover/Tests/PlayMode/Flow/Shop/ShopScreenControllerV1Tests.Fixtures.cs` → `Assets/ShooterMover/Tests/PlayMode/Flow/Shop/ShopScreenControllerTestsFixtures.cs`
- `Assets/ShooterMover/Tests/PlayMode/Flow/Shop/ShopScreenControllerV1Tests.cs` → `Assets/ShooterMover/Tests/PlayMode/Flow/Shop/ShopScreenControllerTests.cs`
- `Assets/ShooterMover/Tests/PlayMode/Flow/Skills/RankedSkillsSceneControllerV2Tests.cs` → `Assets/ShooterMover/Tests/PlayMode/Flow/Skills/RankedSkillsSceneControllerTests.cs`
- `Assets/ShooterMover/Tests/PlayMode/Movement/MovementBody2DAdapterTests.cs` → `Assets/ShooterMover/Tests/PlayMode/Movement/MovementBody2DBridgeTests.cs`
- `Assets/ShooterMover/Tests/PlayMode/Movement/MovementContact2DAdapterTests.cs` → `Assets/ShooterMover/Tests/PlayMode/Movement/MovementContact2DBridgeTests.cs`
- `Assets/ShooterMover/Tests/PlayMode/Movement/PlayerMovementIntentAdapterTests.cs` → `Assets/ShooterMover/Tests/PlayMode/Movement/PlayerMovementIntentBridgeTests.cs`
- `Assets/ShooterMover/Tests/PlayMode/ProductionFlow/LevelGridV2CompiledAssetPlayModeTests.cs` → `Assets/ShooterMover/Tests/PlayMode/ProductionFlow/LevelGridCompiledAssetPlayModeTests.cs`
- `Assets/ShooterMover/Tests/PlayMode/Progression/Experience/EnemyRewards/EnemyExperienceRewardingAuthorityTests.cs` → `Assets/ShooterMover/Tests/PlayMode/Progression/Experience/EnemyRewards/EnemyExperienceRewardingStateTests.cs`
- `Assets/ShooterMover/Tests/PlayMode/RunPickups/LootPickupRunProjectionPlayModeTests.cs` → `Assets/ShooterMover/Tests/PlayMode/RunPickups/LootPickupRunViewPlayModeTests.cs`
- `Assets/ShooterMover/Tests/PlayMode/Weapons/Live/CanonicalWeaponGameplayResolutionPlayModeTests.cs` → `Assets/ShooterMover/Tests/PlayMode/Weapons/Live/WeaponGameplayResolutionPlayModeTests.cs`
- `Assets/ShooterMover/Tests/PlayMode/Weapons/Live/InventoryWeaponRuntimePlayModeFakes.cs` → `Assets/ShooterMover/Tests/PlayMode/Weapons/Live/InventoryWeaponLivePlayModeFakes.cs`
- `Assets/ShooterMover/Tests/PlayMode/Weapons/Live/InventoryWeaponRuntimePlayModeFixtures.cs` → `Assets/ShooterMover/Tests/PlayMode/Weapons/Live/InventoryWeaponLivePlayModeFixtures.cs`
- `Assets/ShooterMover/Tests/PlayMode/Weapons/Live/InventoryWeaponRuntimePlayModeTests.cs` → `Assets/ShooterMover/Tests/PlayMode/Weapons/Live/InventoryWeaponLivePlayModeTests.cs`
- `Assets/ShooterMover/UI/CharacterSelect/CharacterSelectControllerV1.cs` → `Assets/ShooterMover/UI/CharacterSelect/CharacterSelectController.cs`
- `Assets/ShooterMover/UI/Crafting/CraftingScreenControllerV1.cs` → `Assets/ShooterMover/UI/Crafting/CraftingScreenController.cs`
- `Assets/ShooterMover/UI/Hub/HubFlowControllerV1.cs` → `Assets/ShooterMover/UI/Hub/HubFlowController.cs`
- `Assets/ShooterMover/UI/InventoryLoadout/InventoryLoadoutScreenControllerV1.cs` → `Assets/ShooterMover/UI/InventoryLoadout/InventoryLoadoutScreenController.cs`
- `Assets/ShooterMover/UI/InventoryLoadout/WeaponInventoryCardPresentationV1.cs` → `Assets/ShooterMover/UI/InventoryLoadout/WeaponInventoryCardPresentation.cs`
- `Assets/ShooterMover/UI/LevelSelection/LevelSelectionControllerV1.cs` → `Assets/ShooterMover/UI/LevelSelection/LevelSelectionController.cs`
- `Assets/ShooterMover/UI/LevelSelection/LevelSelectionRoutingV1.cs` → `Assets/ShooterMover/UI/LevelSelection/LevelSelectionRouting.cs`
- `Assets/ShooterMover/UI/LevelSelection/LevelSelectionViewV1.cs` → `Assets/ShooterMover/UI/LevelSelection/LevelSelectionView.cs`
- `Assets/ShooterMover/UI/PlaySelection/PlaySelectionControllerV1.cs` → `Assets/ShooterMover/UI/PlaySelection/PlaySelectionController.cs`
- `Assets/ShooterMover/UI/ProductionFlow/EnemyPlayerDamageContractsV1.cs` → `Assets/ShooterMover/UI/ProductionFlow/EnemyPlayerDamageContracts.cs`
- `Assets/ShooterMover/UI/ProductionFlow/EnemyPlayerDamageIntegrationV1.cs` → `Assets/ShooterMover/UI/ProductionFlow/EnemyPlayerDamageIntegration.cs`
- `Assets/ShooterMover/UI/ProductionFlow/PlayablePlayerVitalsV1.cs` → `Assets/ShooterMover/UI/ProductionFlow/PlayablePlayerVitals.cs`
- `Assets/ShooterMover/UI/ProductionFlow/PlayerPrefsProductionFlowProfileStoreV1.cs` → `Assets/ShooterMover/UI/ProductionFlow/PlayerPrefsFlowProfileStore.cs`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionCanonicalWeaponFireControllerV1.cs` → `Assets/ShooterMover/UI/ProductionFlow/WeaponFireController.cs`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionCanonicalWeaponGameplayBindingV2.cs` → `Assets/ShooterMover/UI/ProductionFlow/WeaponGameplayBinding.cs`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionCharacterSelectionControllerV1.cs` → `Assets/ShooterMover/UI/ProductionFlow/CharacterSelectionController.cs`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionCharacterStrongboxBridgeV1.cs` → `Assets/ShooterMover/UI/ProductionFlow/CharacterStrongboxBridge.cs`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionCollectedRunRewardRecoveryV2.cs` → `Assets/ShooterMover/UI/ProductionFlow/CollectedRunRewardRecovery.cs`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionCollectedRunRewardResultsOverlay.cs` → `Assets/ShooterMover/UI/ProductionFlow/CollectedRunRewardResultsOverlay.cs`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionCollectedRunRewardTerminalNoticeV1.cs` → `Assets/ShooterMover/UI/ProductionFlow/CollectedRunRewardTerminalNotice.cs`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionMainMenuControllerV1.cs` → `Assets/ShooterMover/UI/ProductionFlow/MainMenuController.cs`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionPlayableLevelControllerV1.cs` → `Assets/ShooterMover/UI/ProductionFlow/PlayableLevelController.cs`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionResultsControllerV1.cs` → `Assets/ShooterMover/UI/ProductionFlow/ResultsController.cs`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionRunRewardRuntimeV1.cs` → `Assets/ShooterMover/UI/ProductionFlow/RunRewardLive.cs`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionRunRewardSceneCompositionV1.cs` → `Assets/ShooterMover/UI/ProductionFlow/RunRewardSceneSetup.cs`
- `Assets/ShooterMover/UI/ProductionFlow/ProductionRunSessionPortsV1.cs` → `Assets/ShooterMover/UI/ProductionFlow/RunSessionPorts.cs`
- `Assets/ShooterMover/UI/Shop/ShopScreenControllerV1.cs` → `Assets/ShooterMover/UI/Shop/ShopScreenController.cs`
- `Assets/ShooterMover/UI/Shop/ShopScreenRuntimeHandoffV1.cs` → `Assets/ShooterMover/UI/Shop/ShopScreenLiveHandoff.cs`
- `Assets/ShooterMover/UI/Skills/SkillsScreenHubAdapterV1.cs` → `Assets/ShooterMover/UI/Skills/SkillsScreenHubBridge.cs`
- `Assets/ShooterMover/UI/StrongboxOpening/LootPickupPresentationModelsV1.cs` → `Assets/ShooterMover/UI/StrongboxOpening/LootPickupPresentationModels.cs`
- `Assets/ShooterMover/UI/StrongboxOpening/LootPickupRunProjection2D.cs` → `Assets/ShooterMover/UI/StrongboxOpening/LootPickupRunView2D.cs`
- `Assets/ShooterMover/UI/StrongboxOpening/LootPresentationDevelopmentPickupFixtureV1.cs` → `Assets/ShooterMover/UI/StrongboxOpening/LootPresentationDevelopmentPickupFixture.cs`
- `Assets/ShooterMover/UI/StrongboxOpening/LootPresentationShowcaseController.Data.cs` → `Assets/ShooterMover/UI/StrongboxOpening/LootPresentationShowcaseControllerData.cs`
- `Assets/ShooterMover/UI/StrongboxOpening/LootPresentationShowcaseController.GUI.cs` → `Assets/ShooterMover/UI/StrongboxOpening/LootPresentationShowcaseControllerGUI.cs`
- `Assets/ShooterMover/UI/StrongboxOpening/LootRunHudViewV1.cs` → `Assets/ShooterMover/UI/StrongboxOpening/LootRunHudView.cs`
- `Assets/ShooterMover/UI/StrongboxOpening/OwnedStrongboxGroupsViewV1.cs` → `Assets/ShooterMover/UI/StrongboxOpening/OwnedStrongboxGroupsView.cs`
- `Assets/ShooterMover/UI/StrongboxOpening/StrongboxGroupingPresentationV1.cs` → `Assets/ShooterMover/UI/StrongboxOpening/StrongboxGroupingPresentation.cs`
- `Assets/ShooterMover/UI/StrongboxOpening/StrongboxOpeningPresentationViewV1.cs` → `Assets/ShooterMover/UI/StrongboxOpening/StrongboxOpeningPresentationView.cs`
- `Assets/ShooterMover/UI/StrongboxOpening/StrongboxRewardCardsViewV1.cs` → `Assets/ShooterMover/UI/StrongboxOpening/StrongboxRewardCardsView.cs`
