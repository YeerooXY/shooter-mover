using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Missions.Results;
using ShooterMover.Application.Progression.Experience;
using ShooterMover.Application.Progression.Experience.EnemyRewards;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.ContentPackages.Enemies.BlasterTurret;
using ShooterMover.ContentPackages.Enemies.MobileBlasterDroid;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;
using ShooterMover.TestSupport.VisibleSlice;
using ShooterMover.UI.ProductionFlow;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using ShooterMover.UnityAdapters.Enemies;
using ShooterMover.UnityAdapters.Missions.Rooms;
using ShooterMover.UnityAdapters.Weapons.Live;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    public sealed partial class Stage1PlayableLoopCompositionV1
    {
        private RoomPresentationCatalog2D BuildRoomPresentationCatalog()
        {
            RoomPresentationCatalog2D catalog =
                ScriptableObject.CreateInstance<RoomPresentationCatalog2D>();
            runtimeAssets.Add(catalog);

            GameObject droid = MarkerPrefab(
                "Room Runtime Moving Droid Fact",
                true,
                false);
            GameObject turret = MarkerPrefab(
                "Room Runtime Turret Fact",
                true,
                false);
            GameObject cover = MarkerPrefab(
                "Room Runtime Cover Projection",
                false,
                false);
            GameObject door = MarkerPrefab(
                "Room Runtime Door Projection",
                false,
                true);
            runtimeAssets.Add(droid);
            runtimeAssets.Add(turret);
            runtimeAssets.Add(cover);
            runtimeAssets.Add(door);

            catalog.ConfigureForTests(
                Entry(
                    Level1AuthorableRoomDefinitionV1.MovingDroidPresentationStableId,
                    droid),
                Entry(
                    Level1AuthorableRoomDefinitionV1.TurretPresentationStableId,
                    turret),
                Entry(
                    Level1AuthorableRoomDefinitionV1.CoverPresentationStableId,
                    cover),
                Entry(
                    Level1AuthorableRoomDefinitionV1.DoorPresentationStableId,
                    door));
            return catalog;
        }

        private static RoomPresentationCatalogEntry2D Entry(
            StableId id,
            GameObject prefab)
        {
            var entry = new RoomPresentationCatalogEntry2D();
            entry.ConfigureForTests(id.ToString(), prefab);
            return entry;
        }

        private static GameObject MarkerPrefab(
            string name,
            bool enemy,
            bool door)
        {
            GameObject marker = new GameObject(name);
            marker.SetActive(false);
            if (enemy)
            {
                marker.AddComponent<Stage1RoomEnemyAuthorityProjection2D>();
            }
            if (door)
            {
                BoxCollider2D collider = marker.AddComponent<BoxCollider2D>();
                collider.enabled = false;
            }
            return marker;
        }

        private static EquipmentCatalog BuildEquipmentCatalog()
        {
            EquipmentQualityTier common = EquipmentQualityTier.Create(
                StableId.Parse("equipment-quality.common"),
                "Common",
                1);
            EquipmentCatalogBuildResult result = EquipmentCatalog.Build(
                new[]
                {
                    WeaponEquipment(
                        "equipment.demo-cutover-blaster",
                        "family.blaster",
                        "Blaster",
                        "weapon.blaster-machine-gun",
                        common),
                    WeaponEquipment(
                        "equipment.demo-cutover-shotgun",
                        "family.shotgun",
                        "Shotgun",
                        "weapon.shotgun",
                        common),
                    WeaponEquipment(
                        "equipment.demo-cutover-rocket-launcher",
                        "family.rocket-launcher",
                        "Rocket Launcher",
                        "weapon.rocket-launcher",
                        common),
                    WeaponEquipment(
                        "equipment.demo-cutover-flamethrower",
                        "family.flamethrower",
                        "Flamethrower",
                        "weapon.flamethrower",
                        common),
                },
                Array.Empty<AugmentDefinition>());
            if (!result.IsValid || result.Catalog == null)
            {
                throw new InvalidOperationException(
                    "The DEMO-CUTOVER-001 equipment catalog is invalid.");
            }
            return result.Catalog;
        }

        private static EquipmentDefinition WeaponEquipment(
            string definition,
            string family,
            string displayName,
            string runtime,
            EquipmentQualityTier quality)
        {
            return EquipmentDefinition.Create(
                StableId.Parse(definition),
                EquipmentCategoryIds.Weapon,
                StableId.Parse(family),
                displayName,
                StableId.Parse(runtime),
                InclusiveIntRange.Create(1, 100),
                0,
                new[] { quality },
                Array.Empty<StableId>());
        }

        private void AddEquipment(EquipmentInstance equipment, int index)
        {
            string token = "demo-cutover-slot-" + (index + 1);
            PlayerHoldingsMutationResultV1 result = holdings.Apply(
                PlayerHoldingsCommandV1.AddEquipment(
                    StableId.Parse("transaction." + token),
                    StableId.Parse("operation." + token),
                    holdings.AuthorityStableId,
                    equipment,
                    HoldingProvenanceV1.Create(
                        StableId.Parse("grant." + token),
                        StableId.Parse("source.demo-cutover-starter-loadout")),
                    holdings.Sequence));
            if (result.Status != PlayerHoldingsMutationStatusV1.Applied
                && result.Status
                    != PlayerHoldingsMutationStatusV1.ExactDuplicateNoChange)
            {
                throw new InvalidOperationException(
                    "Unable to bind concrete loadout equipment instance: "
                    + result.RejectionCode);
            }
        }

        private static WeaponCatalog BuildWeaponCatalog()
        {
            var rules = new WeaponCatalogRules(
                true,
                false,
                "20-25",
                new[] { 75, 105, 135 },
                new[] { "Kinetic", "Thermal" },
                10,
                true,
                true,
                true);
            var inputs = new WeaponCatalogInputs(
                12d,
                0.05d,
                0.055d,
                0.06d,
                new Dictionary<string, WeaponRarityInput>(StringComparer.Ordinal)
                {
                    { "Common", new WeaponRarityInput("Common", 1000d, 0, 4d, 13d) },
                });
            var archetype = new WeaponArchetypeDefinition(
                "DemoCutover",
                "Demo Cutover",
                1d,
                1d,
                1,
                1,
                0d,
                10d,
                10d,
                1d,
                0d,
                0d,
                0d,
                0d,
                0d,
                0d,
                0,
                0,
                0d,
                0d,
                1d);
            var family = new WeaponFamilyDefinition(
                "demo-cutover-family",
                "Demo Cutover Family",
                "DemoCutover",
                "Kinetic",
                "Universal",
                1,
                20,
                20,
                3,
                "Common",
                "Common",
                "Common",
                1d,
                "Standard",
                "Production vertical slice",
                "Production vertical slice",
                WeaponCatalogAvailability.Live,
                Array.Empty<string>());
            WeaponCatalog authored = new WeaponCatalog(
                "1.0",
                "demo-cutover-live",
                rules,
                inputs,
                new Dictionary<string, WeaponArchetypeDefinition>(StringComparer.Ordinal)
                {
                    { "DemoCutover", archetype },
                },
                new[] { family },
                new[]
                {
                    WeaponDefinition(
                        "weapon.blaster-machine-gun",
                        "Kinetic",
                        10d,
                        1,
                        0d,
                        40d,
                        30d,
                        5d,
                        1),
                    WeaponDefinition(
                        "weapon.shotgun",
                        "Kinetic",
                        2d,
                        7,
                        24d,
                        30d,
                        15d,
                        3d,
                        0),
                    WeaponDefinition(
                        "weapon.rocket-launcher",
                        "Kinetic",
                        1d,
                        1,
                        0d,
                        12d,
                        35d,
                        4d,
                        0,
                        20d,
                        3d),
                    WeaponDefinition(
                        "weapon.flamethrower",
                        "Thermal",
                        5d,
                        4,
                        12d,
                        10d,
                        8d,
                        1d,
                        0,
                        0d,
                        0d,
                        4d,
                        2d,
                        2d,
                        3d),
                });
            // WPN-LIVE-001 resolves these catalog entries exclusively through the
            // concrete equipment instance's runtime weapon reference. No gameplay
            // branch switches on a weapon name or supplies a fallback definition.
            return authored;
        }

        private static WeaponDefinitionData WeaponDefinition(
            string id,
            string damageType,
            double fireRate,
            int projectiles,
            double spread,
            double speed,
            double range,
            double damage,
            int pierce,
            double areaDamage = 0d,
            double explosionRadius = 0d,
            double dotDps = 0d,
            double dotDuration = 0d,
            double poolRadius = 0d,
            double poolDuration = 0d)
        {
            bool explosive = areaDamage > 0d;
            bool dot = dotDps > 0d;
            return new WeaponDefinitionData(
                id,
                id,
                "demo-cutover-family",
                1,
                damageType,
                "DemoCutover",
                "Universal",
                1,
                1,
                1,
                "Common",
                1000d,
                1d,
                1000d,
                4d,
                13d,
                "Standard",
                false,
                "Standard",
                1d,
                100d,
                10d,
                explosive ? 0.2d : dot ? 0.2d : 1d,
                explosive ? 0.8d : 0d,
                dot ? 0.8d : 0d,
                fireRate,
                projectiles,
                1,
                damage,
                spread,
                speed,
                range,
                pierce,
                explosionRadius,
                areaDamage,
                dotDps,
                dotDuration,
                poolRadius,
                poolDuration,
                0,
                0d,
                0.5d,
                1d,
                0d,
                "Production vertical slice",
                "Production vertical slice",
                WeaponCatalogAvailability.Live,
                Array.Empty<string>());
        }

        private static EnemyExperienceRewardDefinitionV1 Reward(
            StableId definitionStableId,
            long experienceAmount)
        {
            return new EnemyExperienceRewardDefinitionV1(
                definitionStableId,
                new[]
                {
                    new EnemyExperienceRewardBandV1(
                        1,
                        100,
                        experienceAmount),
                });
        }

        private sealed class ProjectilePresentation
        {
            public ProjectilePresentation(Color color, Vector3 scale)
            {
                Color = color;
                Scale = scale;
            }

            public Color Color { get; }
            public Vector3 Scale { get; }
        }

        private sealed class EnemyBinding
        {
            public EnemyBinding(
                IEnemyActor2DAuthority authority,
                StableId definitionStableId,
                StableId roomInstanceStableId)
            {
                Authority = authority;
                DefinitionStableId = definitionStableId;
                RoomInstanceStableId = roomInstanceStableId;
            }

            public IEnemyActor2DAuthority Authority { get; }
            public StableId DefinitionStableId { get; }
            public StableId RoomInstanceStableId { get; }
        }

        private sealed class PendingEnemyReward
        {
            public PendingEnemyReward(
                StableId participantStableId,
                StableId enemyDefinitionStableId,
                EnemyDestroyedNotification destruction)
            {
                ParticipantStableId = participantStableId;
                EnemyDefinitionStableId = enemyDefinitionStableId;
                Destruction = destruction;
            }

            public StableId ParticipantStableId { get; }
            public StableId EnemyDefinitionStableId { get; }
            public EnemyDestroyedNotification Destruction { get; }
        }

        private sealed class ParticipantRunStats
        {
            public ParticipantRunStats(StableId participantStableId)
            {
                ParticipantStableId = participantStableId;
            }

            public StableId ParticipantStableId { get; }
            public int Kills { get; set; }
            public long Experience { get; set; }
        }

        private sealed class CatalogEquipmentValidatorV1 :
            IEquipmentInstanceValidator
        {
            private readonly EquipmentCatalog catalog;

            public CatalogEquipmentValidatorV1(EquipmentCatalog catalog)
            {
                this.catalog = catalog;
            }

            public EquipmentInstanceValidationResponse Validate(
                EquipmentInstanceValidationRequest request)
            {
                return EquipmentInstanceValidationResponse.From(
                    catalog,
                    request == null ? null : request.Instance,
                    catalog.ValidateInstance(
                        request == null ? null : request.Instance));
            }
        }

        private sealed class PlayerWeaponActorStateSourceV1 :
            IInventoryWeaponActorStateSource
        {
            private readonly Stage1VisibleSliceController owner;

            public PlayerWeaponActorStateSourceV1(
                Stage1VisibleSliceController owner)
            {
                this.owner = owner;
            }

            public bool TryResolveActorState(
                out WeaponActorInstanceId actorId,
                out LifecycleGeneration lifecycleGeneration)
            {
                actorId = new WeaponActorInstanceId(
                    StableId.Parse(PlayerActorStableIdText));
                lifecycleGeneration = new LifecycleGeneration(
                    owner.RestartGeneration);
                return owner.PlayerLiveAuthority != null
                    && owner.PlayerLiveAuthority.IsInitialized
                    && !owner.IsPlayerDead;
            }
        }

        private sealed class PlayerWeaponOwnershipResolverV1 :
            IWeaponActorOwnershipResolver
        {
            private readonly Stage1VisibleSliceController owner;

            public PlayerWeaponOwnershipResolverV1(
                Stage1VisibleSliceController owner)
            {
                this.owner = owner;
            }

            public bool TryResolveParticipant(
                WeaponActorInstanceId actorId,
                LifecycleGeneration lifecycleGeneration,
                out RunParticipantId participantId)
            {
                bool valid = actorId != null
                    && lifecycleGeneration != null
                    && actorId.Value == StableId.Parse(PlayerActorStableIdText)
                    && lifecycleGeneration.Value == owner.RestartGeneration;
                participantId = valid
                    ? new RunParticipantId(owner.PlayerRunParticipantId)
                    : null;
                return valid;
            }
        }

        private sealed class EmptyStrongboxMissionPortV1 :
            IMissionRunExistingAuthorityPortV1
        {
            private readonly PlayerHoldingsService holdings;

            public EmptyStrongboxMissionPortV1(PlayerHoldingsService holdings)
            {
                this.holdings = holdings;
                OpeningFingerprint = MissionRunCanonicalV1.Fingerprint(
                    "production-strongbox-openings-empty-v1");
            }

            public long OpeningSequence { get { return 0L; } }
            public string OpeningFingerprint { get; }

            public MissionRunCollectionVerificationV1 VerifyCollectedStrongbox(
                MissionRunCollectStrongboxCommandV1 command)
            {
                return MissionRunCollectionVerificationV1.Reject(
                    "production-physical-strongbox-port-not-connected");
            }

            public MissionRunStrongboxProjectionV1 ProjectStrongboxStates(
                EndMissionRunCommandV1 command,
                IReadOnlyList<MissionRunStrongboxCollectionV1> collected)
            {
                if (command == null || collected == null || collected.Count != 0)
                {
                    return MissionRunStrongboxProjectionV1.Reject(
                        "production-strongbox-projection-invalid");
                }

                PlayerHoldingsSnapshotV1 snapshot = holdings.ExportSnapshot();
                return MissionRunStrongboxProjectionV1.Accept(
                    Array.Empty<MissionRunStrongboxResultV1>(),
                    holdings.Sequence,
                    snapshot.Fingerprint,
                    OpeningSequence,
                    OpeningFingerprint);
            }
        }
    }
}
