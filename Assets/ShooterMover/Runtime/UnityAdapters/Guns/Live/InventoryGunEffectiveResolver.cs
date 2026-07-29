using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Application.Guns.Catalog;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Catalog;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.UnityAdapters.Guns.Live
{
    public static class GunLiveExceptionPolicy
    {
        public static bool IsFatal(Exception exception)
        {
            return exception is OutOfMemoryException
                || exception is StackOverflowException
                || exception is AccessViolationException;
        }
    }

    public interface IGunMappingPolicyResolver
    {
        bool TryResolve(
            GunDefinitionId definitionId,
            out GunCatalogBlueprintMappingIntent mappingIntent);
    }

    /// <summary>
    /// Exact authored-blueprint seam for compositions that already own canonical gun content.
    /// When supplied, live combat does not rebuild mechanics from the flat compatibility catalogue.
    /// </summary>
    public interface IGunResolver
    {
        bool TryResolveCanonical(
            GunDefinitionId definitionId,
            out Gun blueprint);
    }

    /// <summary>
    /// One explicit composition-level mapping authority keyed by canonical gun definition ID.
    /// It stores semantic decisions that cannot be inferred losslessly from GunDefinitionData.
    /// </summary>
    public sealed class GunMappingPolicyRegistry :
        IGunMappingPolicyResolver
    {
        private readonly Dictionary<string, GunCatalogBlueprintMappingIntent>
            intents = new Dictionary<string, GunCatalogBlueprintMappingIntent>(
                StringComparer.Ordinal);

        public GunMappingPolicyRegistry(
            IEnumerable<GunCatalogBlueprintMappingIntent> mappingIntents)
        {
            if (mappingIntents == null)
            {
                throw new ArgumentNullException(nameof(mappingIntents));
            }

            foreach (GunCatalogBlueprintMappingIntent intent in mappingIntents)
            {
                if (intent == null || intent.ExpectedDefinitionId == null)
                {
                    throw new ArgumentException(
                        "Every mapping policy requires a canonical definition identity.",
                        nameof(mappingIntents));
                }
                if (intents.ContainsKey(intent.ExpectedDefinitionId.Value))
                {
                    throw new ArgumentException(
                        "Only one mapping policy may own a gun definition.",
                        nameof(mappingIntents));
                }

                intents.Add(intent.ExpectedDefinitionId.Value, intent);
            }
        }

        public int Count { get { return intents.Count; } }

        public bool TryResolve(
            GunDefinitionId definitionId,
            out GunCatalogBlueprintMappingIntent mappingIntent)
        {
            if (definitionId == null)
            {
                mappingIntent = null;
                return false;
            }

            return intents.TryGetValue(definitionId.Value, out mappingIntent);
        }
    }

    public interface IGunAugmentModifierSetResolver
    {
        bool TryResolve(
            EquipmentInstance equipmentInstance,
            EquipmentCatalog equipmentCatalog,
            out IReadOnlyList<GunAugmentModifierSet> modifierSets,
            out string rejectionCode);
    }

    /// <summary>
    /// Default prototype composition for unaugmented equipment. Installed augments are rejected
    /// until the caller supplies the canonical application policy that maps each exact augment
    /// instance to one GunAugmentModifierSet.
    /// </summary>
    public sealed class UnaugmentedGunModifierSetResolver :
        IGunAugmentModifierSetResolver
    {
        private static readonly ReadOnlyCollection<GunAugmentModifierSet> Empty =
            new ReadOnlyCollection<GunAugmentModifierSet>(
                new List<GunAugmentModifierSet>());

        public bool TryResolve(
            EquipmentInstance equipmentInstance,
            EquipmentCatalog equipmentCatalog,
            out IReadOnlyList<GunAugmentModifierSet> modifierSets,
            out string rejectionCode)
        {
            modifierSets = null;
            if (equipmentInstance == null || equipmentCatalog == null)
            {
                rejectionCode = "gun-live-augment-resolution-input-invalid";
                return false;
            }
            if (equipmentInstance.Augments.Count != 0)
            {
                rejectionCode = "gun-live-augment-policy-missing";
                return false;
            }

            modifierSets = Empty;
            rejectionCode = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// Resolves one exact immutable EffectiveGun. Production canonical composition consumes the
    /// exact authored Gun directly; retained compatibility callers may still provide an
    /// explicit flat-catalogue mapping policy. Neither path substitutes equipment or guesses missing
    /// semantics.
    /// </summary>
    public sealed class InventoryGunEffectiveResolver
    {
        private readonly EquipmentCatalog equipmentCatalog;
        private readonly GunCatalog gunCatalog;
        private readonly IGunMappingPolicyResolver mappingPolicies;
        private readonly IGunAugmentModifierSetResolver augmentModifiers;

        public InventoryGunEffectiveResolver(
            EquipmentCatalog equipmentDefinitions,
            GunCatalog gunDefinitions,
            IGunMappingPolicyResolver mappingPolicyResolver,
            IGunAugmentModifierSetResolver augmentModifierResolver)
        {
            equipmentCatalog = equipmentDefinitions
                ?? throw new ArgumentNullException(nameof(equipmentDefinitions));
            gunCatalog = gunDefinitions
                ?? throw new ArgumentNullException(nameof(gunDefinitions));
            mappingPolicies = mappingPolicyResolver
                ?? throw new ArgumentNullException(nameof(mappingPolicyResolver));
            augmentModifiers = augmentModifierResolver
                ?? throw new ArgumentNullException(nameof(augmentModifierResolver));
        }

        public bool TryResolve(
            EquipmentInstance equipmentInstance,
            out EffectiveGun effectiveGun,
            out string rejectionCode)
        {
            effectiveGun = null;
            if (equipmentInstance == null)
            {
                rejectionCode = "gun-live-equipment-unresolved";
                return false;
            }

            EquipmentValidationResult validation =
                equipmentCatalog.ValidateInstance(equipmentInstance);
            if (validation == null || !validation.IsValid)
            {
                rejectionCode = "gun-live-equipment-invalid";
                return false;
            }

            EquipmentDefinition equipmentDefinition =
                equipmentCatalog.FindEquipmentDefinition(
                    equipmentInstance.DefinitionId);
            if (equipmentDefinition == null
                || !EquipmentCategoryIds.Gun.Equals(
                    equipmentDefinition.CategoryId)
                || equipmentDefinition.RuntimeGunReferenceId == null)
            {
                rejectionCode = "gun-live-equipment-definition-invalid";
                return false;
            }

            string definitionValue =
                GunDefinitionId.FromRuntimeReference(
                    equipmentDefinition.RuntimeGunReferenceId).Value;
            var definitionId = new GunDefinitionId(definitionValue);
            Gun blueprint;
            IGunResolver canonicalResolver =
                mappingPolicies as IGunResolver;
            if (canonicalResolver != null)
            {
                try
                {
                    if (!canonicalResolver.TryResolveCanonical(
                            definitionId,
                            out blueprint)
                        || blueprint == null)
                    {
                        rejectionCode =
                            "gun-live-canonical-blueprint-missing:"
                            + definitionValue;
                        return false;
                    }
                }
                catch (Exception exception)
                {
                    if (GunLiveExceptionPolicy.IsFatal(exception)) throw;
                    rejectionCode =
                        "gun-live-canonical-blueprint-resolution-exception";
                    return false;
                }

                if (blueprint.IsTransitionalCatalogProjection
                    || !blueprint.DefinitionId.Equals(definitionId))
                {
                    rejectionCode =
                        "gun-live-canonical-blueprint-identity-mismatch";
                    return false;
                }
            }
            else
            {
                GunDefinitionData catalogDefinition;
                if (!gunCatalog.TryGetDefinition(
                        definitionValue,
                        out catalogDefinition)
                    || catalogDefinition == null)
                {
                    rejectionCode =
                        "gun-live-definition-unresolved:" + definitionValue;
                    return false;
                }
                if (catalogDefinition.Availability
                    != GunCatalogAvailability.Live)
                {
                    rejectionCode =
                        "gun-live-definition-not-live:" + definitionValue;
                    return false;
                }

                GunCatalogBlueprintMappingIntent intent;
                try
                {
                    if (!mappingPolicies.TryResolve(definitionId, out intent)
                        || intent == null)
                    {
                        rejectionCode =
                            "gun-live-blueprint-policy-missing:"
                            + definitionValue;
                        return false;
                    }
                }
                catch (Exception exception)
                {
                    if (GunLiveExceptionPolicy.IsFatal(exception)) throw;
                    rejectionCode = "gun-live-blueprint-policy-exception";
                    return false;
                }

                GunMappingResult mapping;
                try
                {
                    mapping = GunCatalogBlueprintMapper.Map(
                        gunCatalog,
                        definitionValue,
                        intent);
                }
                catch (OverflowException)
                {
                    rejectionCode =
                        "gun-live-blueprint-mapping-numerical-failure";
                    return false;
                }
                catch (Exception exception)
                {
                    if (GunLiveExceptionPolicy.IsFatal(exception)) throw;
                    rejectionCode = "gun-live-blueprint-mapping-exception";
                    return false;
                }

                if (mapping == null
                    || !mapping.Succeeded
                    || mapping.Blueprint == null)
                {
                    string issue = mapping == null || mapping.Issues.Count == 0
                        ? "unknown"
                        : mapping.Issues[0].Code.ToString();
                    rejectionCode =
                        "gun-live-blueprint-mapping-failed:" + issue;
                    return false;
                }
                blueprint = mapping.Blueprint;
            }

            IReadOnlyList<GunAugmentModifierSet> modifierSets;
            try
            {
                if (!augmentModifiers.TryResolve(
                        equipmentInstance,
                        equipmentCatalog,
                        out modifierSets,
                        out rejectionCode)
                    || modifierSets == null)
                {
                    if (string.IsNullOrWhiteSpace(rejectionCode))
                    {
                        rejectionCode = "gun-live-augment-resolution-failed";
                    }
                    return false;
                }
            }
            catch (Exception exception)
            {
                if (GunLiveExceptionPolicy.IsFatal(exception)) throw;
                rejectionCode = "gun-live-augment-resolution-exception";
                return false;
            }

            try
            {
                effectiveGun = EffectiveGunFactory.Create(
                    blueprint,
                    equipmentCatalog,
                    equipmentInstance,
                    modifierSets);
            }
            catch (ArgumentException)
            {
                rejectionCode = "gun-live-effective-gun-invalid";
                return false;
            }
            catch (InvalidOperationException)
            {
                rejectionCode = "gun-live-effective-gun-invalid";
                return false;
            }
            catch (OverflowException)
            {
                rejectionCode = "gun-live-effective-gun-numerical-failure";
                return false;
            }

            if (effectiveGun == null
                || !effectiveGun.EquipmentInstanceId.Value.Equals(
                    equipmentInstance.InstanceId)
                || !effectiveGun.DefinitionId.Equals(definitionId))
            {
                effectiveGun = null;
                rejectionCode = "gun-live-effective-gun-identity-mismatch";
                return false;
            }

            rejectionCode = string.Empty;
            return true;
        }
    }
}
