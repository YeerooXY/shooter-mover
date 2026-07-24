using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using ShooterMover.Application.Rewards.Strongboxes.Simulation;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Editor.BalanceSimulator
{
    /// <summary>
    /// Production-backed observation gateway for STRONGBOX-SIM-001.
    ///
    /// Every observation is resolved through the existing authoritative editor runtime,
    /// which composes StrongboxOpeningServiceV1, the production reward generator,
    /// StrongboxHybridEquipmentGenerationResolverV1, RAP, isolated holdings, and an
    /// isolated generated-augment-signature authority. No player-owned authority is
    /// supplied to this gateway and no production probability formula is copied here.
    /// </summary>
    public sealed class AuthoritativeStrongboxSimulationProductionGatewayV1 :
        IStrongboxSimulationProductionGateway
    {
        private readonly string weaponCatalogJson;
        private readonly ReadOnlyCollection<StrongboxEquipmentMetadata> definitions;
        private readonly Dictionary<StableId, StrongboxEquipmentMetadata> metadataById;

        public AuthoritativeStrongboxSimulationProductionGatewayV1(
            string weaponCatalogJson,
            StrongboxProductionFingerprints fingerprints,
            IEnumerable<StrongboxEquipmentMetadata> equipmentDefinitions)
        {
            if (string.IsNullOrWhiteSpace(weaponCatalogJson))
            {
                throw new ArgumentException(
                    "Production weapon catalog JSON is required.",
                    nameof(weaponCatalogJson));
            }
            Fingerprints = fingerprints
                ?? throw new ArgumentNullException(nameof(fingerprints));

            this.weaponCatalogJson = weaponCatalogJson;
            var copied = new List<StrongboxEquipmentMetadata>();
            metadataById = new Dictionary<StableId, StrongboxEquipmentMetadata>();
            foreach (StrongboxEquipmentMetadata metadata in
                equipmentDefinitions ?? Array.Empty<StrongboxEquipmentMetadata>())
            {
                if (metadata == null)
                {
                    throw new ArgumentException(
                        "Equipment metadata cannot contain null entries.",
                        nameof(equipmentDefinitions));
                }
                if (metadataById.ContainsKey(metadata.DefinitionId))
                {
                    throw new ArgumentException(
                        "Duplicate equipment metadata identity: "
                        + metadata.DefinitionId,
                        nameof(equipmentDefinitions));
                }
                metadataById.Add(metadata.DefinitionId, metadata);
                copied.Add(metadata);
            }
            copied.Sort(delegate(
                StrongboxEquipmentMetadata left,
                StrongboxEquipmentMetadata right)
            {
                return left.DefinitionId.CompareTo(right.DefinitionId);
            });
            definitions = new ReadOnlyCollection<StrongboxEquipmentMetadata>(copied);
        }

        public StrongboxProductionFingerprints Fingerprints { get; }

        public IReadOnlyList<StrongboxEquipmentMetadata> EquipmentDefinitions
        {
            get { return definitions; }
        }

        public bool TryGenerate(
            StrongboxSimulationScenario scenario,
            long ordinal,
            out StrongboxGeneratedEquipmentObservation observation,
            out string diagnostic)
        {
            observation = null;
            diagnostic = string.Empty;
            if (scenario == null)
            {
                diagnostic = "strongbox-simulation-scenario-null";
                return false;
            }
            if (ordinal < 0L || ordinal >= scenario.SampleCount)
            {
                diagnostic = "strongbox-simulation-ordinal-out-of-range";
                return false;
            }
            if (scenario.EquipmentDefinitionId != null)
            {
                diagnostic =
                    "strongbox-simulation-definition-conditioning-not-supported-by-production-resolver";
                return false;
            }

            int tierNumber;
            if (!TryResolveTierNumber(scenario.StrongboxTierId, out tierNumber))
            {
                diagnostic = "strongbox-simulation-tier-unknown";
                return false;
            }

            AuthoritativeStrongboxSimulatorRuntimeV1 runtime;
            if (!AuthoritativeStrongboxSimulatorRuntimeV1.TryCreate(
                    weaponCatalogJson,
                    out runtime,
                    out diagnostic)
                || runtime == null)
            {
                diagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "strongbox-simulation-runtime-create-rejected"
                    : diagnostic;
                return false;
            }

            ulong openingSeed = DeriveOpeningSeed(scenario.RootSeed, ordinal);
            IReadOnlyList<AuthoritativeStrongboxPreparedOpenV1> prepared;
            try
            {
                prepared = runtime.PrepareBatch(
                    new[] { tierNumber },
                    scenario.PlayerLevel,
                    openingSeed);
            }
            catch (Exception exception)
            {
                diagnostic = "strongbox-simulation-prepare-exception-"
                    + exception.GetType().Name.ToLowerInvariant();
                return false;
            }
            if (prepared == null || prepared.Count != 1 || prepared[0] == null)
            {
                diagnostic = "strongbox-simulation-prepared-opening-invalid";
                return false;
            }

            AuthoritativeStrongboxPreparedOpenV1 opening = prepared[0];
            StrongboxOpeningResultRuntimeV1 result;
            try
            {
                result = runtime.OpenOrRetry(opening);
            }
            catch (Exception exception)
            {
                diagnostic = "strongbox-simulation-open-exception-"
                    + exception.GetType().Name.ToLowerInvariant();
                return false;
            }

            IReadOnlyList<EquipmentInstance> equipment = runtime.EquipmentFrom(result);
            if (equipment == null || equipment.Count != 1 || equipment[0] == null)
            {
                diagnostic = result == null || string.IsNullOrWhiteSpace(result.RejectionCode)
                    ? "strongbox-simulation-equipment-count-invalid"
                    : result.RejectionCode;
                return false;
            }

            EquipmentInstance generated = equipment[0];
            StrongboxEquipmentMetadata metadata;
            if (!metadataById.TryGetValue(generated.DefinitionId, out metadata))
            {
                diagnostic = "strongbox-simulation-equipment-metadata-missing-"
                    + generated.DefinitionId;
                return false;
            }

            GeneratedEquipmentAugmentSignatureV1 signature;
            if (!runtime.TryGetAugmentSignature(generated.InstanceId, out signature)
                || signature == null)
            {
                diagnostic = "strongbox-simulation-augment-signature-missing";
                return false;
            }

            StrongboxHybridLootPolicyV1 policy =
                ProductionStrongboxHybridLootCatalogV1.GetByTierNumber(tierNumber);
            StrongboxTargetLevelRollV1 target = policy.RollTargetLevel(
                scenario.PlayerLevel,
                opening.Context.RootSeed,
                opening.Context.AlgorithmVersion,
                0UL);

            bool exceptionalSlots =
                signature.SlotCount > metadata.OrdinaryMaximumSlots;
            bool exceptionalLevel = signature.SlotCount > 0
                && signature.SharedLevel > metadata.OrdinaryMaximumAugmentLevel;
            observation = new StrongboxGeneratedEquipmentObservation(
                ordinal,
                metadata,
                target.TargetLevel,
                generated.ItemLevel,
                generated.QualityId,
                signature.SlotCount,
                signature.SharedLevel,
                ResolveAugmentBias(signature),
                exceptionalSlots,
                exceptionalLevel,
                string.Empty,
                BuildObservationFingerprint(
                    scenario,
                    ordinal,
                    opening,
                    generated,
                    signature,
                    target));
            diagnostic = string.Empty;
            return true;
        }

        private static bool TryResolveTierNumber(
            StableId tierId,
            out int tierNumber)
        {
            tierNumber = 0;
            if (tierId == null) return false;
            IReadOnlyList<ProductionStrongboxTierV1> tiers =
                ProductionStrongboxCatalogV1.Tiers;
            for (int index = 0; index < tiers.Count; index++)
            {
                ProductionStrongboxTierV1 tier = tiers[index];
                if (tier != null && tier.TierStableId == tierId)
                {
                    tierNumber = tier.TierNumber;
                    return true;
                }
            }
            return false;
        }

        private static ulong DeriveOpeningSeed(ulong rootSeed, long ordinal)
        {
            string fingerprint = StrongboxCanonicalV1.Fingerprint(
                "strongbox-simulation-opening-v1|"
                + rootSeed.ToString("x16", CultureInfo.InvariantCulture)
                + "|"
                + ordinal.ToString(CultureInfo.InvariantCulture));
            return ulong.Parse(
                fingerprint.Substring(fingerprint.Length - 16),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture);
        }

        private static double ResolveAugmentBias(
            GeneratedEquipmentAugmentSignatureV1 signature)
        {
            return signature.EffectiveBiasLevels;
        }

        private static string BuildObservationFingerprint(
            StrongboxSimulationScenario scenario,
            long ordinal,
            AuthoritativeStrongboxPreparedOpenV1 opening,
            EquipmentInstance equipment,
            GeneratedEquipmentAugmentSignatureV1 signature,
            StrongboxTargetLevelRollV1 target)
        {
            return StrongboxCanonicalV1.Fingerprint(
                "strongbox-simulation-observation-v1|"
                + scenario.StrongboxTierId + "|"
                + scenario.PlayerLevel.ToString(CultureInfo.InvariantCulture) + "|"
                + ordinal.ToString(CultureInfo.InvariantCulture) + "|"
                + opening.Fingerprint + "|"
                + equipment.InstanceId + "|"
                + equipment.DefinitionId + "|"
                + equipment.ItemLevel.ToString(CultureInfo.InvariantCulture) + "|"
                + signature.Fingerprint + "|"
                + target.Fingerprint);
        }
    }
}
