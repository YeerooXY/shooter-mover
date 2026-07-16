using System;
using System.Collections.Generic;
using ShooterMover.Contracts.Authoring;
using ShooterMover.Domain.Authoring;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.Content.Definitions.Objects
{
    [Serializable]
    public sealed class ObjectCapabilitySelectionAuthoring
    {
        [SerializeField] private CapabilityOverrideMode mode =
            CapabilityOverrideMode.Inherit;
        [SerializeField] private string inheritedCapabilityId =
            "capability.unassigned";
        [SerializeField] private ObjectCapabilityDefinitionAsset overrideDefinition;

        public ObjectCapabilitySelectionAuthoring()
        {
        }

        private ObjectCapabilitySelectionAuthoring(
            CapabilityOverrideMode mode,
            string inheritedCapabilityId,
            ObjectCapabilityDefinitionAsset overrideDefinition)
        {
            this.mode = mode;
            this.inheritedCapabilityId = inheritedCapabilityId;
            this.overrideDefinition = overrideDefinition;
        }

        public CapabilitySelection BuildSelection()
        {
            if (mode == CapabilityOverrideMode.Inherit)
            {
                return CapabilitySelection.Inherit(
                    StableId.Parse(inheritedCapabilityId));
            }

            if (overrideDefinition == null)
            {
                throw new InvalidOperationException(
                    "An overridden variant capability requires a definition asset.");
            }

            return CapabilitySelection.Override(
                overrideDefinition.BuildDefinition());
        }

        public static ObjectCapabilitySelectionAuthoring Inherit(
            string capabilityId)
        {
            return new ObjectCapabilitySelectionAuthoring(
                CapabilityOverrideMode.Inherit,
                capabilityId ?? throw new ArgumentNullException(nameof(capabilityId)),
                null);
        }

        public static ObjectCapabilitySelectionAuthoring Override(
            ObjectCapabilityDefinitionAsset definition)
        {
            return new ObjectCapabilitySelectionAuthoring(
                CapabilityOverrideMode.Override,
                definition == null ? null : definition.CapabilityId.ToString(),
                definition ?? throw new ArgumentNullException(nameof(definition)));
        }
    }

    [Serializable]
    public sealed class ObjectVariantAuthoring
    {
        [SerializeField] private string variantId = "variant.unassigned";
        [SerializeField] private bool hasObjectLevel;
        [SerializeField] private int objectLevel;
        [SerializeField] private ObjectCapabilitySelectionAuthoring[] capabilitySelections =
            Array.Empty<ObjectCapabilitySelectionAuthoring>();

        public ObjectVariantAuthoring()
        {
        }

        public ObjectVariantAuthoring(
            string variantId,
            int? objectLevel,
            params ObjectCapabilitySelectionAuthoring[] capabilitySelections)
        {
            this.variantId = variantId ?? throw new ArgumentNullException(nameof(variantId));
            hasObjectLevel = objectLevel.HasValue;
            this.objectLevel = objectLevel.GetValueOrDefault();
            this.capabilitySelections = capabilitySelections
                ?? Array.Empty<ObjectCapabilitySelectionAuthoring>();
        }

        public ObjectVariantDefinition BuildDefinition()
        {
            List<CapabilitySelection> selections =
                new List<CapabilitySelection>(
                    capabilitySelections == null ? 0 : capabilitySelections.Length);

            if (capabilitySelections != null)
            {
                for (int index = 0; index < capabilitySelections.Length; index++)
                {
                    ObjectCapabilitySelectionAuthoring selection =
                        capabilitySelections[index];
                    if (selection == null)
                    {
                        throw new InvalidOperationException(
                            $"Variant '{variantId}' contains a null capability selection.");
                    }

                    selections.Add(selection.BuildSelection());
                }
            }

            return new ObjectVariantDefinition(
                StableId.Parse(variantId),
                hasObjectLevel ? (int?)objectLevel : null,
                selections);
        }
    }

    /// <summary>
    /// Authored family organization and arbitrary ordered variants. Runtime state
    /// never lives in this ScriptableObject.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ObjectFamilyDefinition",
        menuName = "Shooter Mover/Objects/Family Definition")]
    public sealed class ObjectFamilyDefinitionAsset :
        ScriptableObject,
        IObjectFamilyDefinitionSource
    {
        [SerializeField] private string familyId = "family.unassigned";
        [SerializeField] private string displayName = "Unassigned object family";
        [SerializeField] private string defaultVariantId = "variant.unassigned";
        [SerializeField] private ObjectCapabilityDefinitionAsset[] familyDefaults =
            Array.Empty<ObjectCapabilityDefinitionAsset>();
        [SerializeField] private ObjectVariantAuthoring[] variants =
            Array.Empty<ObjectVariantAuthoring>();

        public StableId FamilyId
        {
            get { return StableId.Parse(familyId); }
        }

        public ObjectFamilyDefinition BuildDefinition()
        {
            List<CapabilityDefinition> defaults =
                new List<CapabilityDefinition>(
                    familyDefaults == null ? 0 : familyDefaults.Length);
            if (familyDefaults != null)
            {
                for (int index = 0; index < familyDefaults.Length; index++)
                {
                    ObjectCapabilityDefinitionAsset definition = familyDefaults[index];
                    if (definition == null)
                    {
                        throw new InvalidOperationException(
                            $"Object family '{familyId}' contains a null family default.");
                    }

                    defaults.Add(definition.BuildDefinition());
                }
            }

            List<ObjectVariantDefinition> builtVariants =
                new List<ObjectVariantDefinition>(
                    variants == null ? 0 : variants.Length);
            if (variants != null)
            {
                for (int index = 0; index < variants.Length; index++)
                {
                    ObjectVariantAuthoring variant = variants[index];
                    if (variant == null)
                    {
                        throw new InvalidOperationException(
                            $"Object family '{familyId}' contains a null variant.");
                    }

                    builtVariants.Add(variant.BuildDefinition());
                }
            }

            return new ObjectFamilyDefinition(
                FamilyId,
                displayName ?? string.Empty,
                StableId.Parse(defaultVariantId),
                defaults,
                builtVariants);
        }

        public void ValidateOrThrow()
        {
            BuildDefinition();
        }

        public static ObjectFamilyDefinitionAsset CreateRuntime(
            string familyId,
            string displayName,
            string defaultVariantId,
            ObjectCapabilityDefinitionAsset[] familyDefaults,
            params ObjectVariantAuthoring[] variants)
        {
            ObjectFamilyDefinitionAsset asset =
                CreateInstance<ObjectFamilyDefinitionAsset>();
            asset.familyId = familyId ?? throw new ArgumentNullException(nameof(familyId));
            asset.displayName = displayName ?? string.Empty;
            asset.defaultVariantId = defaultVariantId
                ?? throw new ArgumentNullException(nameof(defaultVariantId));
            asset.familyDefaults = familyDefaults
                ?? Array.Empty<ObjectCapabilityDefinitionAsset>();
            asset.variants = variants ?? Array.Empty<ObjectVariantAuthoring>();
            asset.hideFlags = HideFlags.HideAndDontSave;
            asset.ValidateOrThrow();
            return asset;
        }

        private void OnValidate()
        {
            if (familyDefaults == null)
            {
                familyDefaults = Array.Empty<ObjectCapabilityDefinitionAsset>();
            }

            if (variants == null)
            {
                variants = Array.Empty<ObjectVariantAuthoring>();
            }
        }
    }
}
