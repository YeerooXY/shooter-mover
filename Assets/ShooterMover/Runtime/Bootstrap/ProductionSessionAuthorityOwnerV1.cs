using System;
using System.Collections.Generic;
using ShooterMover.Application.Flow.Hub;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Missions.Results;
using ShooterMover.Application.Missions.Run;
using ShooterMover.Application.Progression.Experience;
using ShooterMover.Application.Progression.Experience.EnemyRewards;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Contracts.Missions.Run;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;

namespace ShooterMover.Bootstrap
{
    /// <summary>
    /// Bootstrap-owned lifetime boundary for the production player holdings, XP and run
    /// authorities. It composes existing authorities directly and publishes only bounded
    /// immutable route/dependency projections to scene flow.
    /// </summary>
    public sealed class ProductionSessionAuthorityOwnerV1 : IDisposable
    {
        private static readonly StableId CommonQualityStableId =
            StableId.Parse("equipment-quality.common");

        private readonly object ownerToken = new object();
        private readonly EquipmentCatalog equipmentCatalog;
        private readonly PlayerHoldingsService holdings;
        private readonly PlayerExperienceAuthorityV1 experience;
        private readonly EnemyExperienceRewardServiceV1 enemyRewards;
        private readonly ProductionMissionRunAuthorityPortV1 missionPort;
        private readonly MissionRunResultAuthorityV1 missionResults;
        private readonly Stage1ProductionAuthorityBundleV1 stage1Bundle;
        private readonly PlayerRouteProfilePayloadV1 starterPayload;
        private bool started;
        private bool disposed;

        public ProductionSessionAuthorityOwnerV1()
        {
            equipmentCatalog = BuildStarterCatalog();
            holdings = new PlayerHoldingsService(
                StableId.Parse("authority.production-player-holdings"),
                0L,
                new CatalogEquipmentValidatorV1(equipmentCatalog));

            StarterRouteProfileResultV1 starter =
                new StarterRouteProfileFactoryV1().CreateOrRestore(
                    holdings,
                    equipmentCatalog,
                    new StarterRouteProfileRequestV1(
                        StableId.Parse("character.aggressive"),
                        StableId.Parse("loadout.production-starter"),
                        new[]
                        {
                            StableId.Parse("equipment.stage1-01-blaster"),
                            StableId.Parse("equipment.stage1-02-shotgun"),
                            StableId.Parse("equipment.stage1-03-rocket"),
                            StableId.Parse("equipment.stage1-04-arc"),
                        },
                        CommonQualityStableId,
                        1));
            if (!starter.Succeeded || starter.RoutePayload == null)
            {
                throw new InvalidOperationException(
                    "Production starter holdings could not be composed: "
                    + starter.RejectionCode);
            }
            starterPayload = starter.RoutePayload;

            experience = new PlayerExperienceAuthorityV1(
                new PlayerExperienceCurveV1(
                    100L,
                    100L,
                    50,
                    new SoftActivationCurveParameters(0.1, 10L, 10L)),
                ProgressionContext.Create(
                    1,
                    1,
                    StableId.Parse("difficulty.normal"),
                    0,
                    new[] { StableId.Parse("progression-tag.campaign") }));
            enemyRewards = new EnemyExperienceRewardServiceV1(
                experience,
                new EnemyExperienceRewardCatalogV1(
                    new[]
                    {
                        Reward(
                            EnemyExperienceRewardIdsV1.MobileBlasterDroid,
                            40L),
                        Reward(
                            EnemyExperienceRewardIdsV1.BlasterTurret,
                            60L),
                    }));

            missionPort = new ProductionMissionRunAuthorityPortV1(holdings);
            missionResults = new MissionRunResultAuthorityV1(missionPort);
            PlayerHoldingsSnapshotV1 holdingsSnapshot = holdings.ExportSnapshot();
            stage1Bundle = new Stage1ProductionAuthorityBundleV1(
                new UniqueLevelRunStableIdFactoryV1(),
                new HoldingsLevelRunLoadoutResolverV1(
                    holdings,
                    equipmentCatalog),
                enemyRewards,
                missionResults,
                new MissionRunAuthorityCheckpointV1(
                    holdings.Sequence,
                    holdingsSnapshot.Fingerprint,
                    missionPort.OpeningSequence,
                    missionPort.OpeningFingerprint));
        }

        public PlayerRouteProfilePayloadV1 StarterPayload
        {
            get { return starterPayload; }
        }

        public void Start()
        {
            ThrowIfDisposed();
            if (started) return;

            ProductionSessionAuthorityContextV1.CaptureOwner(
                ownerToken,
                starterPayload,
                stage1Bundle);
            started = true;
        }

        public void Stop()
        {
            if (!started) return;
            ProductionSessionAuthorityContextV1.ReleaseOwner(ownerToken);
            started = false;
        }

        public void Dispose()
        {
            if (disposed) return;
            Stop();
            disposed = true;
        }

        private static EquipmentCatalog BuildStarterCatalog()
        {
            EquipmentQualityTier common = EquipmentQualityTier.Create(
                CommonQualityStableId,
                "Common",
                1);
            EquipmentCatalogBuildResult result = EquipmentCatalog.Build(
                new[]
                {
                    Weapon(
                        "equipment.stage1-01-blaster",
                        "family.blaster",
                        "Blaster",
                        "weapon.blaster-machine-gun",
                        common),
                    Weapon(
                        "equipment.stage1-02-shotgun",
                        "family.shotgun",
                        "Shotgun",
                        "weapon.shotgun",
                        common),
                    Weapon(
                        "equipment.stage1-03-rocket",
                        "family.rocket",
                        "Rocket Launcher",
                        "weapon.rocket-launcher",
                        common),
                    Weapon(
                        "equipment.stage1-04-arc",
                        "family.arc",
                        "Arc Gun",
                        "weapon.arc-gun",
                        common),
                },
                Array.Empty<AugmentDefinition>());
            if (!result.IsValid || result.Catalog == null)
            {
                throw new InvalidOperationException(
                    "The production starter equipment catalog is invalid.");
            }
            return result.Catalog;
        }

        private static EquipmentDefinition Weapon(
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

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(
                    nameof(ProductionSessionAuthorityOwnerV1));
            }
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

        /// <summary>
        /// Read-only bridge used until the physical strongbox pickup authority is connected.
        /// It rejects collection claims and projects an empty exact collection at extraction.
        /// </summary>
        private sealed class ProductionMissionRunAuthorityPortV1 :
            IMissionRunExistingAuthorityPortV1
        {
            private readonly PlayerHoldingsService holdings;

            public ProductionMissionRunAuthorityPortV1(
                PlayerHoldingsService holdings)
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
