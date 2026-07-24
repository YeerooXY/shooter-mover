using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Rewards.Strongboxes.Simulation
{
    public enum StrongboxSimulationMode
    {
        FullOpening = 1,
        DefinitionConditioned = 2,
        Comparison = 3,
        PlayerLevelSweep = 4,
        StrongboxTierSweep = 5,
    }

    public sealed class StrongboxSimulationScenario
    {
        public StrongboxSimulationScenario(
            int playerLevel,
            StableId strongboxTierId,
            int sampleCount,
            ulong rootSeed,
            StableId equipmentDefinitionId = null,
            bool diagnosticEligibilityOverride = false)
        {
            if (playerLevel < 0) throw new ArgumentOutOfRangeException(nameof(playerLevel));
            if (sampleCount < 1) throw new ArgumentOutOfRangeException(nameof(sampleCount));
            PlayerLevel = playerLevel;
            StrongboxTierId = strongboxTierId ?? throw new ArgumentNullException(nameof(strongboxTierId));
            SampleCount = sampleCount;
            RootSeed = rootSeed;
            EquipmentDefinitionId = equipmentDefinitionId;
            DiagnosticEligibilityOverride = diagnosticEligibilityOverride;
        }

        public int PlayerLevel { get; }
        public StableId StrongboxTierId { get; }
        public int SampleCount { get; }
        public ulong RootSeed { get; }
        public StableId EquipmentDefinitionId { get; }
        public bool DiagnosticEligibilityOverride { get; }
    }

    public sealed class StrongboxSimulationRequest
    {
        public StrongboxSimulationRequest(
            StrongboxSimulationMode mode,
            StrongboxSimulationScenario primary,
            StrongboxSimulationScenario comparison = null)
        {
            if (!Enum.IsDefined(typeof(StrongboxSimulationMode), mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (mode == StrongboxSimulationMode.Comparison && comparison == null)
                throw new ArgumentNullException(nameof(comparison));
            Mode = mode;
            Primary = primary ?? throw new ArgumentNullException(nameof(primary));
            Comparison = comparison;
        }

        public StrongboxSimulationMode Mode { get; }
        public StrongboxSimulationScenario Primary { get; }
        public StrongboxSimulationScenario Comparison { get; }
    }

    public sealed class StrongboxProductionFingerprints
    {
        public StrongboxProductionFingerprints(
            string equipmentCatalog,
            string equipmentProjection,
            string strongboxPolicy,
            string rarityPolicy,
            string itemLevelPolicy,
            string augmentSlotPolicy,
            string augmentLevelPolicy)
        {
            EquipmentCatalog = Required(equipmentCatalog, nameof(equipmentCatalog));
            EquipmentProjection = Required(equipmentProjection, nameof(equipmentProjection));
            StrongboxPolicy = Required(strongboxPolicy, nameof(strongboxPolicy));
            RarityPolicy = rarityPolicy ?? string.Empty;
            ItemLevelPolicy = itemLevelPolicy ?? string.Empty;
            AugmentSlotPolicy = augmentSlotPolicy ?? string.Empty;
            AugmentLevelPolicy = augmentLevelPolicy ?? string.Empty;
        }

        public string EquipmentCatalog { get; }
        public string EquipmentProjection { get; }
        public string StrongboxPolicy { get; }
        public string RarityPolicy { get; }
        public string ItemLevelPolicy { get; }
        public string AugmentSlotPolicy { get; }
        public string AugmentLevelPolicy { get; }

        private static string Required(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", name);
            return value;
        }
    }

    public sealed class StrongboxEquipmentMetadata
    {
        public StrongboxEquipmentMetadata(
            StableId definitionId,
            string displayName,
            StableId categoryId,
            StableId familyId,
            StableId slotId,
            IReadOnlyList<StableId> canonicalTags,
            StableId rarityId,
            int firstAppearanceLevel,
            int anchorLevel,
            double authoredBaseWeight,
            bool available,
            bool topBoxOnly,
            int ordinaryMaximumSlots,
            int absoluteMaximumSlots,
            int ordinaryMaximumAugmentLevel,
            int absoluteMaximumAugmentLevel)
        {
            DefinitionId = definitionId ?? throw new ArgumentNullException(nameof(definitionId));
            DisplayName = displayName ?? string.Empty;
            CategoryId = categoryId ?? throw new ArgumentNullException(nameof(categoryId));
            FamilyId = familyId;
            SlotId = slotId;
            CanonicalTags = CanonicalizeTags(canonicalTags);
            RarityId = rarityId;
            FirstAppearanceLevel = firstAppearanceLevel;
            AnchorLevel = anchorLevel;
            AuthoredBaseWeight = authoredBaseWeight;
            Available = available;
            TopBoxOnly = topBoxOnly;
            OrdinaryMaximumSlots = ordinaryMaximumSlots;
            AbsoluteMaximumSlots = absoluteMaximumSlots;
            OrdinaryMaximumAugmentLevel = ordinaryMaximumAugmentLevel;
            AbsoluteMaximumAugmentLevel = absoluteMaximumAugmentLevel;
        }

        public StableId DefinitionId { get; }
        public string DisplayName { get; }
        public StableId CategoryId { get; }
        public StableId FamilyId { get; }
        public StableId SlotId { get; }
        public IReadOnlyList<StableId> CanonicalTags { get; }
        public StableId RarityId { get; }
        public int FirstAppearanceLevel { get; }
        public int AnchorLevel { get; }
        public double AuthoredBaseWeight { get; }
        public bool Available { get; }
        public bool TopBoxOnly { get; }
        public int OrdinaryMaximumSlots { get; }
        public int AbsoluteMaximumSlots { get; }
        public int OrdinaryMaximumAugmentLevel { get; }
        public int AbsoluteMaximumAugmentLevel { get; }

        private static IReadOnlyList<StableId> CanonicalizeTags(IReadOnlyList<StableId> tags)
        {
            var values = new List<StableId>(tags ?? Array.Empty<StableId>());
            for (int index = 0; index < values.Count; index++)
                if (values[index] == null)
                    throw new ArgumentException("Canonical tags cannot contain null.", nameof(tags));
            values.Sort(delegate(StableId left, StableId right) { return left.CompareTo(right); });
            for (int index = 1; index < values.Count; index++)
                if (values[index - 1] == values[index])
                    throw new ArgumentException("Canonical tags cannot contain duplicates.", nameof(tags));
            return new ReadOnlyCollection<StableId>(values);
        }
    }

    public sealed class StrongboxGeneratedEquipmentObservation
    {
        public StrongboxGeneratedEquipmentObservation(
            long ordinal,
            StrongboxEquipmentMetadata equipment,
            int targetLevel,
            int itemLevel,
            StableId qualityId,
            int augmentSlotCount,
            int sharedAugmentLevel,
            double augmentBias,
            bool exceptionalSlotOutcome,
            bool exceptionalAugmentLevelOutcome,
            string exceptionalDiagnostic,
            string generationFingerprint)
        {
            if (ordinal < 0) throw new ArgumentOutOfRangeException(nameof(ordinal));
            Ordinal = ordinal;
            Equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
            TargetLevel = targetLevel;
            ItemLevel = itemLevel;
            QualityId = qualityId;
            AugmentSlotCount = augmentSlotCount;
            SharedAugmentLevel = sharedAugmentLevel;
            AugmentBias = augmentBias;
            ExceptionalSlotOutcome = exceptionalSlotOutcome;
            ExceptionalAugmentLevelOutcome = exceptionalAugmentLevelOutcome;
            ExceptionalDiagnostic = exceptionalDiagnostic ?? string.Empty;
            GenerationFingerprint = generationFingerprint ?? string.Empty;
        }

        public long Ordinal { get; }
        public StrongboxEquipmentMetadata Equipment { get; }
        public int TargetLevel { get; }
        public int ItemLevel { get; }
        public StableId QualityId { get; }
        public int AugmentSlotCount { get; }
        public int SharedAugmentLevel { get; }
        public double AugmentBias { get; }
        public bool ExceptionalSlotOutcome { get; }
        public bool ExceptionalAugmentLevelOutcome { get; }
        public string ExceptionalDiagnostic { get; }
        public string GenerationFingerprint { get; }
    }

    public interface IStrongboxSimulationProductionGateway
    {
        StrongboxProductionFingerprints Fingerprints { get; }
        IReadOnlyList<StrongboxEquipmentMetadata> EquipmentDefinitions { get; }
        bool TryGenerate(
            StrongboxSimulationScenario scenario,
            long ordinal,
            out StrongboxGeneratedEquipmentObservation observation,
            out string diagnostic);
    }
}
