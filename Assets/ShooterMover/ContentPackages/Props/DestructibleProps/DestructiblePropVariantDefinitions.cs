using System;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.ContentPackages.Props.DestructibleProps
{
    [Serializable]
    public sealed class DestructiblePropVariantDefinitionAuthoring
    {
        [SerializeField] private string variantId = "variant.unassigned";
        [SerializeField] private bool hasObjectLevel;
        [SerializeField] private int objectLevel;
        [SerializeField] private DestructiblePropValueOverrideMask overrideMask =
            DestructiblePropValueOverrideMask.None;
        [SerializeField] private DestructiblePropDefinitionValuesAuthoring values =
            new DestructiblePropDefinitionValuesAuthoring();

        public StableId VariantId => StableId.Parse(variantId);
        public int? ObjectLevel => hasObjectLevel ? (int?)objectLevel : null;
        public DestructiblePropValueOverrideMask OverrideMask => overrideMask;

        public DestructiblePropResolvedValues Resolve(DestructiblePropResolvedValues defaults)
        {
            if (defaults == null)
            {
                throw new ArgumentNullException(nameof(defaults));
            }

            return defaults.Apply(overrideMask, values);
        }

        public static DestructiblePropVariantDefinitionAuthoring CreateRuntime(
            string variantId,
            int? objectLevel,
            DestructiblePropValueOverrideMask overrideMask,
            DestructiblePropDefinitionValuesAuthoring values)
        {
            return new DestructiblePropVariantDefinitionAuthoring
            {
                variantId = variantId,
                hasObjectLevel = objectLevel.HasValue,
                objectLevel = objectLevel.GetValueOrDefault(),
                overrideMask = overrideMask,
                values = values ?? throw new ArgumentNullException(nameof(values)),
            };
        }
    }

    [Serializable]
    public sealed class DestructiblePropInstanceOverrideAuthoring
    {
        [SerializeField] private DestructiblePropValueOverrideMask overrideMask =
            DestructiblePropValueOverrideMask.None;
        [SerializeField] private DestructiblePropDefinitionValuesAuthoring values =
            new DestructiblePropDefinitionValuesAuthoring();

        public DestructiblePropValueOverrideMask OverrideMask => overrideMask;

        public DestructiblePropResolvedValues Apply(DestructiblePropResolvedValues variantValues)
        {
            if (variantValues == null)
            {
                throw new ArgumentNullException(nameof(variantValues));
            }

            return variantValues.Apply(overrideMask, values);
        }

        public static DestructiblePropInstanceOverrideAuthoring CreateRuntime(
            DestructiblePropValueOverrideMask overrideMask,
            DestructiblePropDefinitionValuesAuthoring values)
        {
            return new DestructiblePropInstanceOverrideAuthoring
            {
                overrideMask = overrideMask,
                values = values ?? throw new ArgumentNullException(nameof(values)),
            };
        }
    }

    public sealed class DestructiblePropDefinitionResolution
    {
        public DestructiblePropDefinitionResolution(
            StableId familyId,
            StableId variantId,
            int? objectLevel,
            DestructiblePropResolvedValues values,
            string familyFingerprint,
            string variantFingerprint)
        {
            FamilyId = familyId ?? throw new ArgumentNullException(nameof(familyId));
            VariantId = variantId ?? throw new ArgumentNullException(nameof(variantId));
            ObjectLevel = objectLevel;
            Values = values ?? throw new ArgumentNullException(nameof(values));
            FamilyFingerprint = familyFingerprint
                ?? throw new ArgumentNullException(nameof(familyFingerprint));
            VariantFingerprint = variantFingerprint
                ?? throw new ArgumentNullException(nameof(variantFingerprint));
        }

        public StableId FamilyId { get; }
        public StableId VariantId { get; }
        public int? ObjectLevel { get; }
        public DestructiblePropResolvedValues Values { get; }
        public string FamilyFingerprint { get; }
        public string VariantFingerprint { get; }
    }
}
