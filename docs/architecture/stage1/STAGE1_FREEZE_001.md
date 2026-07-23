# STAGE1-FREEZE-001 — retained Stage 1 migration freeze

## Decision

`Stage1VisibleSliceController` and `Stage1PlayableLoopCompositionV1` are separate retirement targets. Moving gameplay behavior from the controller into the composition is explicitly prohibited. The retained types are migration-only and may lose responsibilities, but may not accumulate new ones.

## Launch and dependency proof

- Launch `main`: `de0ff8029d505bffe832e1a2b22d11d34d76ece2`.
- PR #280: merged before launch.
- DROP-STRONGBOX-LIVE-001 / PR #284: merged before launch.
- Open PR inventory at launch: zero; no active PR owned the Stage 1 production composition files.
- `CURRENT_TASKS.md` was read in full before the inventory was produced.
- No merged PR implemented STAGE1-FREEZE-001 before this work.

## Frozen source inventory

The manifest freezes **30 source files**, **24 frozen type entries**, and approximately **10,705 lines** of retained Stage 1 migration code. Every target records its own approximate source-line total in addition to its source-file references.

| Source file | Current lines | Frozen baseline blob |
|---|---:|---|
| `Assets/ShooterMover/ContentPackages/Props/DestructibleProps/Stage1DestructiblePropIntegration.cs` | 455 | `0d63bc289774208b09817b95734bf3e36f211fd5` |
| `Assets/ShooterMover/Production/Content/Stage1TerminalDropContentV1.cs` | 158 | `d65a36cf2c8c73e619f66d72d4788b9f3a039dc8` |
| `Assets/ShooterMover/Production/Stage1/RetainedTerminalDropEquipmentPayloads.cs` | 330 | `cfa1bba8e48be975a6dabbf5236851820747d4cd` |
| `Assets/ShooterMover/Production/Stage1/Stage1CanonicalPropDestructionFactV1.cs` | 89 | `84a4884e52fd58ba03dd101320feb139e7d57a7b` |
| `Assets/ShooterMover/Production/Stage1/Stage1CanonicalPropTerminalDropFactAdapterV1.cs` | 82 | `4a00bdd4c94be708ab2ea10014e2b39217a88338` |
| `Assets/ShooterMover/Production/Stage1/Stage1PersonalRewardBatchDeliveryV1.cs` | 253 | `704a62626083c78466160739794e274318657580` |
| `Assets/ShooterMover/Production/Stage1/Stage1PlayableLoopCompositionV1.Catalogs.cs` | 282 | `d2f397a16258cf452dd05c45e9bb9462b4621f0d` |
| `Assets/ShooterMover/Production/Stage1/Stage1PlayableLoopCompositionV1.CollectedRunTransfer.cs` | 797 | `50b8dc883712aea8bd171598a0cc48a5bab093f3` |
| `Assets/ShooterMover/Production/Stage1/Stage1PlayableLoopCompositionV1.Combat.cs` | 490 | `037857b9b9c5bd51483f752416a0e990e60e4720` |
| `Assets/ShooterMover/Production/Stage1/Stage1PlayableLoopCompositionV1.CombatPresentation.cs` | 237 | `4980ea5fc05530be92bf2bb7a630e0c77aa7f1bd` |
| `Assets/ShooterMover/Production/Stage1/Stage1PlayableLoopCompositionV1.EnemyAttackPatternContent.cs` | 329 | `941e65b2a142629747237b7fc98f5cce5b98c327` |
| `Assets/ShooterMover/Production/Stage1/Stage1PlayableLoopCompositionV1.EnemyAttackPatterns.cs` | 221 | `e4fafbd71e9273f470d1be7ee2e5ad71de1c566d` |
| `Assets/ShooterMover/Production/Stage1/Stage1PlayableLoopCompositionV1.Flow.cs` | 425 | `7f366e067162fc77e03839d72d4d1bbde40e6fd1` |
| `Assets/ShooterMover/Production/Stage1/Stage1PlayableLoopCompositionV1.HubLoadout.cs` | 317 | `648b217261719294f44c532b972b0406ed568be1` |
| `Assets/ShooterMover/Production/Stage1/Stage1PlayableLoopCompositionV1.RunPickupAccess.cs` | 39 | `9e05a1792b8f2bdc7c9c394f372e202743e4953e` |
| `Assets/ShooterMover/Production/Stage1/Stage1PlayableLoopCompositionV1.RunPickupRequirements.cs` | 16 | `97a09f631435c0e7c3745c681e16efbcf7d9268c` |
| `Assets/ShooterMover/Production/Stage1/Stage1PlayableLoopCompositionV1.RunRewardCompletion.cs` | 24 | `022fee9e400e7748933e676aa8b1920c50ced4f4` |
| `Assets/ShooterMover/Production/Stage1/Stage1PlayableLoopCompositionV1.RunSession.cs` | 384 | `1134d5a5c5c46f7745611778befded6125a68be5` |
| `Assets/ShooterMover/Production/Stage1/Stage1PlayableLoopCompositionV1.TerminalDropProvenance.cs` | 356 | `8e2647c412415fdaa5a2bc1327065b0ec439b65f` |
| `Assets/ShooterMover/Production/Stage1/Stage1PlayableLoopCompositionV1.cs` | 399 | `6093d8e61a3e75dc89028f0e61417eb26e739518` |
| `Assets/ShooterMover/Production/Stage1/Stage1RunPickupBootstrap2D.DurableEquipmentPayloads.cs` | 50 | `09978452f2295c87b9b69ed830345147f35e28ea` |
| `Assets/ShooterMover/Production/Stage1/Stage1RunPickupBootstrap2D.Rewards.cs` | 307 | `0a678880d30b985775aa164df2d47028b07c8823` |
| `Assets/ShooterMover/Production/Stage1/Stage1RunPickupBootstrap2D.cs` | 487 | `339bfb86702970f02a747d2cbf17db1c7ca63ede` |
| `Assets/ShooterMover/Production/Stage1/Stage1RunPickupBootstrapSupportV1.cs` | 187 | `955470e3eec80adfac05987715b4565b0548d136` |
| `Assets/ShooterMover/Production/Stage1/Stage1RunPickupPropBootstrap2D.cs` | 393 | `906baa6fa73b81eb91bc3f017ece239f3d02cfae` |
| `Assets/ShooterMover/Production/Stage1/Stage1SharedRunCompositionPortsV1.cs` | 238 | `ded714b4c53160b3184194a2e197a957fcf2c51c` |
| `Assets/ShooterMover/Production/Stage1/Stage1SharedRunPlayerWeaponPortsV1.cs` | 265 | `2bbf027a79eb1a48cca83d1550ba9ec38a1064f7` |
| `Assets/ShooterMover/Production/Stage1/Stage1MigrationOnlySurfaceDocumentationV1.cs` | 52 | `0fd32f9db33ac873be85f8f2d206f55ccbc5b76c` |
| `Assets/ShooterMover/Production/Stage1/Stage1WeaponPresentationRepairV1.cs` | 649 | `80ba18331a9050dbe2398d90ed6bcd14e9118750` |
| `Assets/ShooterMover/TestSupport/VisibleSlice/Stage1VisibleSliceController.cs` | 2,394 | `e8eb59056f91ca59371d652d4b3a592800724fe1` |

Notable aggregates:

- `Stage1VisibleSliceController`: **2,394 lines**.
- `Stage1PlayableLoopCompositionV1` across all current partials: **4,316 lines**.
- `Stage1RunPickupBootstrap2D` across all current partials: **844 lines**.
- The machine-readable manifest discovers these partial sets dynamically and fails when the tree and manifest diverge.

## Interface inventory

| Type | Current interfaces | Freeze outcome |
|---|---|---|
| `Stage1VisibleSliceController` | `IGeneralCombatHudStateSource`, `IVisibleSliceBlasterTurretPresentationSource`, `IVisibleSliceReducedEffectsSource`, `IDoorTargetConditionReader`, `IVoidHazardCombatPort` | No additional gameplay-authority interface may be added. |
| `Stage1PlayableLoopCompositionV1` | none beyond `MonoBehaviour` | No gameplay-authority or persistence implementation may be added. |
| `Stage1EnemyTerminalSourceContextResolverV1` | `IEnemyTerminalSourceContextResolverV1` | Migrate to `Stage1EnemyTerminalPickupConsumerV1`. |
| `Stage1PickupTerminalDropRunContextResolverV1` | `ITerminalDropRunContextResolverV1` | Migrate to `RunPickupLifecycleProjection2D`. |
| `Stage1MissingPropTerminalSourceContextResolverV1` | `IPropTerminalSourceContextResolverV1` | Delete with legacy prop path. |
| `Stage1CanonicalPropDestructionFactV1` | `ITerminalRewardPlacementFactV1` | Replace with generic authored prop terminal fact. |
| `Stage1CanonicalPropTerminalDropFactAdapterV1` | `ITerminalDropFactAdapterV1` | Delete when generic prop runtime emits the source fact directly. |
| `RetainedTerminalDropEquipmentPayloadAuthority` | `ICollectedRunEquipmentPayloadSource` | Move behind generic pickup/reward composition. |
| `RetainingTerminalDropRewardGenerationExecutor` | `IRewardGenerationExecutorV1` | Move behind generic pickup/reward composition. |
| `Stage1ProductionConditionDefinitionProviderV1` | `IRunConditionDefinitionProviderV1` | Replace with level/run content definition. |
| `Stage1RoomRunPortV1` | `IRunRoomRuntimePortV1` | Replace with generic room runtime port. |
| `Stage1SharedRunSessionNonConditionRuntimePortFactoryV1` | `IRunSessionNonConditionRuntimePortFactoryV1` | Replace with generic level run composition. |
| `Stage1ProductionRunStatInputResolverV1` | `IProductionRunStatInputResolverV1` | Replace with generic character-to-run composition. |
| `Stage1PlayerRunPortV1` | `IRunPlayerRuntimePortV1` | Replace with generic player runtime port. |
| `Stage1WeaponRunPortV1` | `IRunWeaponRuntimePortV1` | Replace with generic inventory weapon runtime port. |

## Unity lifecycle inventory

| Type / partial | Lifecycle methods |
|---|---|
| `Stage1VisibleSliceController` | `Awake`, `Update`, `FixedUpdate`, `OnDestroy` |
| `Stage1PlayableLoopCompositionV1.cs` | `Start` plus subsystem-registration and before-scene-load runtime initializers |
| `.Combat.cs` | `Update` |
| `.Flow.cs` | `OnGUI`, `OnDestroy` |
| `.CombatPresentation.cs` | `LateUpdate` |
| `.RunSession.cs` | `FixedUpdate`, `OnDisable` |
| `.CollectedRunTransfer.cs` | `LateUpdate` |
| `Stage1RunPickupBootstrap2D` | `Start`, `LateUpdate`, `OnDestroy` |
| `Stage1RunPickupPropBootstrap2D` | `Start`, `LateUpdate`, `OnDestroy` |
| `Stage1WeaponPresentationRepairV1` | `Update`, `LateUpdate`, `OnDestroy` |
| `DestructiblePropSet2D` | `LateUpdate`, `OnDestroy` |

## Scene installation inventory

There is exactly one allowlisted production Stage 1 subscription:

| Subscriber | Source | Callback | Installation path |
|---|---|---|---|
| `Stage1PlayableLoopCompositionV1` | `Stage1PlayableLoopCompositionV1.cs` | `HandleSceneLoaded` | `ResetStatics` / `InstallSceneHook` |

The subscriber finds the retained controller from scene roots, may fall back to global discovery for the production-flow coordinator, and attaches retained components to the controller object. The audit rejects any additional `SceneManager.sceneLoaded +=` occurrence and any newly added global object discovery in retained Stage 1 code.

## Reflection inventory

There is exactly one allowlisted production private-reflection access involving the retirement targets:

| Caller | Target | Member | Flags | Source |
|---|---|---|---|---|
| `Stage1PlayableLoopCompositionV1` | `Stage1VisibleSliceController` | private field `roomMissionLayout` | `Instance`, `NonPublic` | `Stage1PlayableLoopCompositionV1.cs` |

Any additional private field, method, or property reflection against either retirement target fails the audit.

## Responsibility-to-replacement map

`Stage1RunLoopDriver2D` is deliberately **not** the replacement owner for the systems it coordinates. Its only allowed role is:

> observe Run Session lifecycle → forward typed commands → coordinate lifecycle projections → request restart/end through ports

It may connect the owners below, but it must not inherit their mutable state, concrete implementations, authority interfaces, persistence, or gameplay decisions.

| Current responsibility | Current retained owner | Canonical destination | Driver role |
|---|---|---|---|
| Health/player authority projection | `Stage1VisibleSliceController`, `Stage1PlayerRunPortV1` | existing player authority and `Level1PlayerRuntimeSceneAdapterV1` | observe lifecycle snapshots only |
| Player input and movement | `Stage1VisibleSliceController` | canonical player scene/input adapter (`Level1PlayerRuntimeSceneAdapterV1`) | none beyond lifecycle enable/disable command forwarding |
| Weapon selection and execution | `Stage1PlayableLoopCompositionV1` | `InventoryWeaponRuntimeComposition` and `InventoryBackedWeaponExecutionAdapter` | forward typed fire/select commands only |
| Projectile/effect realization and damage routing | composition combat/presentation partials, `Stage1WeaponPresentationRepairV1` | `InventoryWeaponEffectDamageRouter2D`; presentation in `Stage1LegacyScenePresentation2D` | no damage/effect implementation |
| Enemy construction/runtime and attack scheduling | composition enemy partials | generic enemy factory/runtime and `EnemyAttackPatternLiveSchedulerV1` | lifecycle coordination only |
| Prop runtime compatibility | controller plus `Stage1DestructiblePropIntegration` | generic prop runtime; explicit binding through `Stage1SceneInstaller2D` | none |
| Room authority | composition room runtime and `Stage1RoomRunPortV1` | generic room authority / `RoomRuntimeComposition2D` | observe room lifecycle only |
| Traversal/access presentation | composition flow partial | `Stage1RoomFlowController2D`, consuming authored room/link/spawn identities | no room truth |
| Pickups and lifecycle projection | `Stage1RunPickupBootstrap2D` | `RunPickupLifecycleProjection2D` | restart ordering only |
| Enemy terminal pickup consumption | composition terminal provenance plus pickup bootstrap | `Stage1EnemyTerminalPickupConsumerV1` using existing reward/pickup authorities | none |
| Prop terminal pickup consumption | `Stage1RunPickupPropBootstrap2D` | `Stage1PropTerminalPickupConsumerV1` using existing reward/pickup authorities | none |
| Reward generation and run minimum | pickup reward partial | existing terminal reward generation/admission authorities | none |
| Durable transfer | composition collected-run-transfer partial | `CollectedRunRewardTransferPreparationFactoryV2`, receipt authorities, and `ProductionCollectedRunRewardPersistenceV2` | request accepted End through a typed port only |
| Results publication/navigation | composition flow and durable-transfer partials | `MissionRunResultAuthorityV1`, `ProductionCollectedRunRewardResultsBridge`, and production flow | no navigation/presentation state |
| HUD/presentation | controller, flow partial, presentation repair | `Stage1LegacyScenePresentation2D` / `Stage1SceneView2D` | none |
| Restart | controller, composition shared-run partial, run ports | `RunSessionAggregateV1` plus focused player/weapon/room/pickup ports | issue one typed restart request and coordinate accepted generation projection |
| Scene installation | global composition hook | `Stage1SceneInstaller2D`, invoked explicitly by the production route | none |

## Existing migration debt baseline

The manifest contains **15 uniquely identified debt entries**. Each entry names a source anchor, replacement owner, and retirement task. The audit rejects duplicate debt IDs, missing anchors, and entries without a replacement plan.

Baseline debt includes:

- the sole global scene hook;
- the private `roomMissionLayout` reflection;
- runtime scene construction in the controller;
- controller/composition global discovery;
- shared run authority ownership;
- room flow, weapon damage routing and enemy scheduling in the composition;
- reward composition and run-minimum realization in the pickup bootstrap;
- prop terminal reward consumption;
- durable collected-run transfer in the composition;
- legacy prop signature classification;
- definition-specific compatibility weapon presentation;
- pickup self-attachment through `RequireComponent`.

Removing a frozen responsibility is allowed. The removal must be accompanied by an intentional manifest update that removes or relocates its debt/responsibility entry and refreshes the exact source baseline. Adding a new responsibility without a migration-plan entry fails.

## Guardrails

`tools/architecture/verify_stage1_freeze.py` enforces:

1. Every inventoried source exists and matches its current line count and Git blob SHA.
2. Every retained source is represented in the manifest; every newly added Stage 1 production source must declare an approved replacement type or be explicitly inventoried as retained debt.
3. Known debt and responsibility IDs are unique.
4. Both controller and composition remain separate retirement targets.
5. All required replacement boundaries, narrow-driver ownership policy, canonical-owner map, and the six-step integration sequence remain declared.
6. The sole scene hook and sole private reflection access match the exact inventory.
7. Controller and composition interface baselines are aggregated across every partial declaration and do not grow.
8. Retained classes do not directly construct `RunSessionAggregateV1`.
9. Complete added C# hunks are scanned for new authority/persistence ownership, reward selection/probability logic, multiline weapon-name/definition switches, global discovery, name/hierarchy/room-number decisions, and content registration in any partial of the two main retained controllers. Alias and semantic authority-name detection are included.
10. Ordinary content under definition/resource roots remains outside the retained controller boundary and is exercised through the end-to-end audit path.

## Test proof

Command:

```text
python -m py_compile tools/architecture/verify_stage1_freeze.py tools/architecture/test_verify_stage1_freeze.py
python tools/architecture/test_verify_stage1_freeze.py
```

Result:

```text
Ran 19 tests
OK
```

The 19 fixtures include end-to-end temporary Git repositories. They prove rejection of an unlisted `Stage1SomethingCoordinator.cs`, a Stage 1 class outside the legacy folders, an interface added through another partial declaration, multiline weapon-definition switching, aliased and previously unknown authority construction, a concrete owner field in `Stage1RunLoopDriver2D`, a new scene-loaded installer, private reflection, and name-based gameplay decisions. They also prove approved replacement source admission, ordinary content through the real `run_audit()` path, a clean passing audit, and genuine source deletion accompanied by matching source/debt/plan updates.

## Split sequence

1. `STAGE1-FREEZE-001`
2. `ROOM-JSON-LIVE-001`
3. `STAGE1-RUNTIME-DECOMPOSE-A-001`
4. `STAGE1-RUNTIME-DECOMPOSE-B-001`
5. `ABILITY-RUNTIME-001`
6. `LEVEL1-CONTROLLER-RETIRE-001`

## Runtime-behavior statement

This task intentionally changes no existing gameplay, scene, prefab, reward, weapon, room, Results, save, balancing, or content behavior. The only compiled addition is a constant-only code documentation marker with no methods, initialization, component, or runtime registration. All other additions are documentation, machine-readable inventory, and engine-independent architecture audits.

## Known verification limitation

Unity was not available in the execution environment, so no Unity EditMode/PlayMode suite was run here. The Python architecture fixture suite and syntax compilation passed; the PR should still receive the repository's normal Unity compilation/test checks.
