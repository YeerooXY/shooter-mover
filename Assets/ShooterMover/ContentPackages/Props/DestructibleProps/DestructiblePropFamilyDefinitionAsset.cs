using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Contracts.Authoring;
using ShooterMover.Domain.Authoring;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.ContentPackages.Props.DestructibleProps
{
    [CreateAssetMenu(
        fileName = "DestructiblePropFamilyDefinition",
        menuName = "Shooter Mover/Props/Destructible Prop Family")]
    public sealed class DestructiblePropFamilyDefinitionAsset :
        ScriptableObject,
        IObjectFamilyDefinitionSource
    {
        public const string CapabilityIdText = "capability.destructible-prop";

        [SerializeField] private string familyId = "family.destructible-prop-unassigned";
        [SerializeField] private string displayName = "Unassigned destructible prop family";
        [SerializeField] private string defaultVariantId = "variant.unassigned";
        [SerializeField] private DestructiblePropDefinitionValuesAuthoring familyDefaults =
            new DestructiblePropDefinitionValuesAuthoring();
        [SerializeField] private DestructiblePropVariantDefinitionAuthoring[] variants =
            Array.Empty<DestructiblePropVariantDefinitionAuthoring>();

        public StableId FamilyId => StableId.Parse(familyId);

        public ObjectFamilyDefinition BuildDefinition()
        {
            DestructiblePropResolvedValues defaults = BuildFamilyDefaults();
            var builtVariants = new List<ObjectVariantDefinition>(
                variants == null ? 0 : variants.Length);
            if (variants != null)
            {
                for (int index = 0; index < variants.Length; index++)
                {
                    DestructiblePropVariantDefinitionAuthoring variant = variants[index];
                    if (variant == null)
                    {
                        throw new InvalidOperationException(
                            $"Destructible prop family '{familyId}' contains a null variant.");
                    }

                    DestructiblePropResolvedValues resolved = variant.Resolve(defaults);
                    builtVariants.Add(
                        new ObjectVariantDefinition(
                            variant.VariantId,
                            variant.ObjectLevel,
                            new[]
                            {
                                CapabilitySelection.Override(BuildCapabilityDefinition(resolved)),
                            }));
                }
            }

            return new ObjectFamilyDefinition(
                FamilyId,
                displayName ?? string.Empty,
                StableId.Parse(defaultVariantId),
                new[] { BuildCapabilityDefinition(defaults) },
                builtVariants);
        }

        public DestructiblePropDefinitionResolution Resolve(
            StableId selectedVariantId,
            DestructiblePropInstanceOverrideAuthoring instanceOverrides)
        {
            ObjectFamilyDefinition generic = BuildDefinition();
            StableId variantId = selectedVariantId ?? generic.DefaultVariantId;
            DestructiblePropVariantDefinitionAuthoring authoredVariant = FindVariant(variantId);
            DestructiblePropResolvedValues defaults = BuildFamilyDefaults();
            DestructiblePropResolvedValues variantValues = authoredVariant.Resolve(defaults);
            DestructiblePropResolvedValues finalValues = instanceOverrides == null
                ? variantValues
                : instanceOverrides.Apply(variantValues);
            string familyFingerprint = BuildFamilyFingerprint(defaults);
            string variantFingerprint = DestructiblePropResolvedValues.Sha256(
                "family=" + FamilyId
                + "|variant=" + variantId
                + "|level=" + (authoredVariant.ObjectLevel.HasValue
                    ? authoredVariant.ObjectLevel.Value.ToString(CultureInfo.InvariantCulture)
                    : "none")
                + "|values=" + variantValues.Fingerprint);
            return new DestructiblePropDefinitionResolution(
                FamilyId,
                variantId,
                authoredVariant.ObjectLevel,
                finalValues,
                familyFingerprint,
                variantFingerprint);
        }

        public static DestructiblePropFamilyDefinitionAsset CreateRuntime(
            string familyId,
            string displayName,
            string defaultVariantId,
            DestructiblePropDefinitionValuesAuthoring familyDefaults,
            params DestructiblePropVariantDefinitionAuthoring[] variants)
        {
            DestructiblePropFamilyDefinitionAsset asset =
                CreateInstance<DestructiblePropFamilyDefinitionAsset>();
            asset.familyId = familyId ?? throw new ArgumentNullException(nameof(familyId));
            asset.displayName = displayName ?? string.Empty;
            asset.defaultVariantId = defaultVariantId
                ?? throw new ArgumentNullException(nameof(defaultVariantId));
            asset.familyDefaults = familyDefaults
                ?? throw new ArgumentNullException(nameof(familyDefaults));
            asset.variants = variants ?? Array.Empty<DestructiblePropVariantDefinitionAuthoring>();
            asset.hideFlags = HideFlags.HideAndDontSave;
            asset.BuildDefinition();
            return asset;
        }

        private DestructiblePropResolvedValues BuildFamilyDefaults()
        {
            if (familyDefaults == null)
            {
                throw new InvalidOperationException(
                    $"Destructible prop family '{familyId}' has no family defaults.");
            }

            return familyDefaults.Build();
        }

        private DestructiblePropVariantDefinitionAuthoring FindVariant(StableId variantId)
        {
            if (variants != null)
            {
                for (int index = 0; index < variants.Length; index++)
                {
                    DestructiblePropVariantDefinitionAuthoring variant = variants[index];
                    if (variant != null && variant.VariantId.Equals(variantId))
                    {
                        return variant;
                    }
                }
            }

            throw new InvalidOperationException(
                $"Variant '{variantId}' does not exist in destructible prop family '{FamilyId}'.");
        }

        private string BuildFamilyFingerprint(DestructiblePropResolvedValues defaults)
        {
            var ordered = new List<string>();
            if (variants != null)
            {
                for (int index = 0; index < variants.Length; index++)
                {
                    DestructiblePropVariantDefinitionAuthoring variant = variants[index];
                    if (variant == null)
                    {
                        continue;
                    }

                    DestructiblePropResolvedValues resolved = variant.Resolve(defaults);
                    ordered.Add(
                        variant.VariantId
                        + "@level="
                        + (variant.ObjectLevel.HasValue
                            ? variant.ObjectLevel.Value.ToString(CultureInfo.InvariantCulture)
                            : "none")
                        + "="
                        + resolved.Fingerprint);
                }
            }

            ordered.Sort(StringComparer.Ordinal);
            return DestructiblePropResolvedValues.Sha256(
                "family=" + FamilyId
                + "|default=" + StableId.Parse(defaultVariantId)
                + "|defaults=" + defaults.Fingerprint
                + "|variants=" + string.Join(";", ordered.ToArray()));
        }

        private static CapabilityDefinition BuildCapabilityDefinition(
            DestructiblePropResolvedValues values)
        {
            return new CapabilityDefinition(
                StableId.Parse(CapabilityIdText),
                new[]
                {
                    DecimalField("prop-field.maximum-health", values.MaximumHealth),
                    IntegerField("prop-field.collider-shape", (int)values.ColliderShape),
                    DecimalField("prop-field.collider-size-x", values.ColliderSize.x),
                    DecimalField("prop-field.collider-size-y", values.ColliderSize.y),
                    DecimalField("prop-field.collider-offset-x", values.ColliderOffset.x),
                    DecimalField("prop-field.collider-offset-y", values.ColliderOffset.y),
                    StableIdField("prop-field.intact-presentation", values.IntactPresentationId),
                    StableIdField("prop-field.destruction-animation", values.DestructionAnimationId),
                    IntegerField(
                        "prop-field.destroyed-collider-policy",
                        (int)values.DestroyedColliderPolicy),
                    StableIdField("prop-field.reward-profile", values.RewardProfileId),
                });
        }

        private static CapabilityField DecimalField(string id, double value)
        {
            return new CapabilityField(
                StableId.Parse(id),
                CapabilityFieldValue.FromDecimal(value));
        }

        private static CapabilityField IntegerField(string id, long value)
        {
            return new CapabilityField(
                StableId.Parse(id),
                CapabilityFieldValue.FromInteger(value));
        }

        private static CapabilityField StableIdField(string id, StableId value)
        {
            return new CapabilityField(
                StableId.Parse(id),
                CapabilityFieldValue.FromStableId(value));
        }

        private void OnValidate()
        {
            if (variants == null)
            {
                variants = Array.Empty<DestructiblePropVariantDefinitionAuthoring>();
            }
        }
    }
}
