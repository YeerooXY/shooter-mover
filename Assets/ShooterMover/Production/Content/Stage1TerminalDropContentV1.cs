using System;
using ShooterMover.Application.Rewards.Drops;
using ShooterMover.ContentPackages.Props.DestructibleProps;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Props;

namespace ShooterMover.Content.Definitions.Rewards
{
    /// <summary>
    /// Canonical Stage 1 terminal-source content. This class owns only explicit
    /// content-to-profile references; reusable reward probabilities and quantities live
    /// in ProductionRewardSourceCatalogV1. No object-name resolver exists here.
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
            ProductionRewardSourceCatalogV1.NormalPropId;
        public static readonly StableId ExplosiveDropProfileStableId =
            ProductionRewardSourceCatalogV1.RarePropId;
        public static readonly StableId GenericLegacyDropProfileStableId =
            ProductionRewardSourceCatalogV1.ExplicitNoDropId;

        private static readonly PropCatalogV1 CanonicalPropCatalog =
            BuildPropCatalog();

        public static PropCatalogV1 PropCatalog
        {
            get { return CanonicalPropCatalog; }
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
                || resolvedProfile != expectedDropProfileStableId
                || !ProductionRewardSourceCatalogV1.Profiles.ContainsKey(
                    resolvedProfile))
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
                        StableId.Parse("presentation.prop-stage1-crate"),
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
                        StableId.Parse("presentation.prop-stage1-explosive"),
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
                        StableId.Parse("presentation.prop-legacy-unclassified"),
                        new[]
                        {
                            PropCapabilitiesV1.HealthBased(
                                Stage1DestructiblePropIntegration
                                    .CrateMaximumHealth),
                            PropCapabilitiesV1.DamageBehavior(
                                PropDamageAlignmentV1.Neutral,
                                StableId.Parse("damage-policy.prop-stage1")),
                            PropCapabilitiesV1.DropOnDestroy(
                                GenericLegacyDropProfileStableId),
                        }),
                });
        }
    }
}
