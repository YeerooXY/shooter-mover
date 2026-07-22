using System;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.ContentPackages.Props.DestructibleProps;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Props;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.TerminalDropBinding;

namespace ShooterMover.Content.Definitions.Rewards
{
    /// <summary>
    /// Canonical Stage 1 terminal-drop content. Production adapters consume these immutable
    /// catalogs; pickup presentation code never reconstructs enemy/prop definitions or profiles.
    /// </summary>
    public static class Stage1TerminalDropContentV1
    {
        public static readonly StableId CrateDefinitionStableId =
            StableId.Parse("prop.stage1-crate");
        public static readonly StableId ExplosiveDefinitionStableId =
            StableId.Parse("prop.stage1-explosive");
        public static readonly StableId GenericLegacyDefinitionStableId =
            StableId.Parse("prop.legacy-unclassified");
        public static readonly StableId CrateDropProfileStableId =
            StableId.Parse("drop.prop-stage1-ordinary");
        public static readonly StableId ExplosiveDropProfileStableId =
            StableId.Parse("drop.prop-stage1-explosive");
        public static readonly StableId GenericLegacyDropProfileStableId =
            StableId.Parse("drop.prop-legacy-none");

        private static readonly PropCatalogV1 CanonicalPropCatalog =
            BuildPropCatalog();
        private static readonly IRewardProfileResolverV1
            CanonicalRewardProfiles = BuildRewardProfiles();

        public static PropCatalogV1 PropCatalog
        {
            get { return CanonicalPropCatalog; }
        }

        public static IRewardProfileResolverV1 RewardProfiles
        {
            get { return CanonicalRewardProfiles; }
        }

        public static DestructiblePropTerminalProvenanceV1
            CreateCrateProvenance()
        {
            return CreatePropProvenance(
                CrateDefinitionStableId,
                CrateDropProfileStableId);
        }

        public static DestructiblePropTerminalProvenanceV1
            CreateExplosiveProvenance()
        {
            return CreatePropProvenance(
                ExplosiveDefinitionStableId,
                ExplosiveDropProfileStableId);
        }

        public static DestructiblePropTerminalProvenanceV1
            CreateGenericLegacyProvenance()
        {
            return CreatePropProvenance(
                GenericLegacyDefinitionStableId,
                GenericLegacyDropProfileStableId);
        }

        /// <summary>
        /// Bounded migration for the two pre-definition Stage 1 marker keys. Unknown reusable
        /// legacy markers become an explicit no-drop definition rather than being guessed from
        /// health, geometry, presentation, or destruction behavior.
        /// </summary>
        public static DestructiblePropTerminalProvenanceV1
            ResolveLegacyAuthoringKey(string authoringKey)
        {
            if (string.IsNullOrWhiteSpace(authoringKey))
                return CreateGenericLegacyProvenance();
            string value = authoringKey.Trim();
            if (value.StartsWith("Crate_", StringComparison.Ordinal))
                return CreateCrateProvenance();
            if (value.StartsWith("Explosive_", StringComparison.Ordinal))
                return CreateExplosiveProvenance();
            return CreateGenericLegacyProvenance();
        }

        public static bool TryReadDropProfile(
            PropDefinitionV1 definition,
            out StableId profileStableId)
        {
            profileStableId = null;
            PropCapabilityV1 capability;
            string value;
            return definition != null
                && definition.TryGet(
                    PropCapabilityIdsV1.DropOnDestroy,
                    out capability)
                && capability != null
                && capability.TryGet("profile-id", out value)
                && StableId.TryParse(value, out profileStableId)
                && profileStableId != null;
        }

        private static DestructiblePropTerminalProvenanceV1
            CreatePropProvenance(
                StableId definitionStableId,
                StableId expectedDropProfileStableId)
        {
            PropDefinitionV1 definition;
            StableId resolvedProfile;
            if (!CanonicalPropCatalog.TryGet(
                    definitionStableId,
                    out definition)
                || definition == null
                || !TryReadDropProfile(definition, out resolvedProfile)
                || resolvedProfile != expectedDropProfileStableId)
            {
                throw new InvalidOperationException(
                    "Canonical Stage 1 prop content is inconsistent: "
                    + definitionStableId);
            }
            return new DestructiblePropTerminalProvenanceV1(
                definition.DefinitionId,
                resolvedProfile,
                definition.Fingerprint);
        }

        private static PropCatalogV1 BuildPropCatalog()
        {
            return new PropCatalogV1(
                PropCapabilityRegistryV1.CreateBuiltIns(),
                new[]
                {
                    new PropDefinitionV1(
                        CrateDefinitionStableId,
                        StableId.Parse("presentation.prop.stage1-crate"),
                        new[]
                        {
                            PropCapabilitiesV1.HealthBased(
                                Stage1DestructiblePropIntegration.CrateMaximumHealth),
                            PropCapabilitiesV1.DamageBehavior(
                                PropDamageAlignmentV1.Neutral,
                                StableId.Parse("damage-policy.prop-stage1")),
                            PropCapabilitiesV1.DropOnDestroy(
                                CrateDropProfileStableId),
                        }),
                    new PropDefinitionV1(
                        ExplosiveDefinitionStableId,
                        StableId.Parse("presentation.prop.stage1-explosive"),
                        new[]
                        {
                            PropCapabilitiesV1.HealthBased(
                                Stage1DestructiblePropIntegration
                                    .ExplosiveMaximumHealth),
                            PropCapabilitiesV1.DamageBehavior(
                                PropDamageAlignmentV1.Neutral,
                                StableId.Parse("damage-policy.prop-stage1")),
                            PropCapabilitiesV1.ExplodeOnDestroy(
                                StableId.Parse(
                                    "explosion-profile.prop-stage1")),
                            PropCapabilitiesV1.DropOnDestroy(
                                ExplosiveDropProfileStableId),
                        }),
                    new PropDefinitionV1(
                        GenericLegacyDefinitionStableId,
                        StableId.Parse("presentation.prop.legacy-unclassified"),
                        new[]
                        {
                            PropCapabilitiesV1.DamageBehavior(
                                PropDamageAlignmentV1.Neutral,
                                StableId.Parse("damage-policy.prop-stage1")),
                            PropCapabilitiesV1.DropOnDestroy(
                                GenericLegacyDropProfileStableId),
                        }),
                });
        }

        private static IRewardProfileResolverV1 BuildRewardProfiles()
        {
            RewardProfileV1 enemyCommon = RewardProfileV1.Create(
                StableId.Parse("drop.enemy-common"),
                new[]
                {
                    RewardGrantSpecificationV1.CreateFixed(
                        StableId.Parse(
                            "grant.stage1-enemy-common-money"),
                        RewardGrantKindV1.Money,
                        StableId.Parse("currency.credits"),
                        5L),
                    RewardGrantSpecificationV1.CreateFixed(
                        StableId.Parse(
                            "grant.stage1-enemy-common-scrap"),
                        RewardGrantKindV1.Scrap,
                        StableId.Parse("currency.scrap"),
                        1L),
                },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());
            RewardProfileV1 enemyTurret = RewardProfileV1.Create(
                StableId.Parse("drop.enemy-turret"),
                new[]
                {
                    RewardGrantSpecificationV1.CreateFixed(
                        StableId.Parse(
                            "grant.stage1-enemy-turret-money"),
                        RewardGrantKindV1.Money,
                        StableId.Parse("currency.credits"),
                        15L),
                    RewardGrantSpecificationV1.CreateFixed(
                        StableId.Parse(
                            "grant.stage1-enemy-turret-box"),
                        RewardGrantKindV1.Strongbox,
                        StableId.Parse("strongbox.tier-common"),
                        1L),
                },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());
            RewardProfileV1 crate = RewardProfileV1.Create(
                CrateDropProfileStableId,
                new[]
                {
                    RewardGrantSpecificationV1.CreateFixed(
                        StableId.Parse(
                            "grant.stage1-prop-ordinary-scrap"),
                        RewardGrantKindV1.Scrap,
                        StableId.Parse("currency.scrap"),
                        2L),
                },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());
            RewardProfileV1 explosive = RewardProfileV1.Create(
                ExplosiveDropProfileStableId,
                new[]
                {
                    RewardGrantSpecificationV1.CreateFixed(
                        StableId.Parse(
                            "grant.stage1-prop-explosive-scrap"),
                        RewardGrantKindV1.Scrap,
                        StableId.Parse("currency.scrap"),
                        4L),
                },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());
            return new RewardProfileCatalogResolverV1(new[]
            {
                enemyCommon,
                enemyTurret,
                crate,
                explosive,
                RewardProfileV1.CreateExplicitNoDrop(
                    GenericLegacyDropProfileStableId),
                RewardProfileV1.CreateExplicitNoDrop(
                    StableId.Parse("drop.enemy-none")),
            });
        }
    }
}
