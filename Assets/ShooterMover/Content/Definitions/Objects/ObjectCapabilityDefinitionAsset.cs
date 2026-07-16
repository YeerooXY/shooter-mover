using System;
using System.Collections.Generic;
using ShooterMover.Contracts.Authoring;
using ShooterMover.Domain.Authoring;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.Content.Definitions.Objects
{
    [Serializable]
    public sealed class CapabilityFieldAuthoring
    {
        [SerializeField] private string fieldId = "field.value";
        [SerializeField] private CapabilityValueKind valueKind = CapabilityValueKind.Text;
        [SerializeField] private bool booleanValue;
        [SerializeField] private long integerValue;
        [SerializeField] private double decimalValue;
        [SerializeField] private string textValue = string.Empty;
        [SerializeField] private string stableIdValue = "value.unassigned";

        public CapabilityFieldAuthoring()
        {
        }

        public CapabilityFieldAuthoring(
            string fieldId,
            CapabilityFieldValue value)
        {
            this.fieldId = fieldId ?? throw new ArgumentNullException(nameof(fieldId));
            SetValue(value ?? throw new ArgumentNullException(nameof(value)));
        }

        public string FieldIdText
        {
            get { return fieldId; }
        }

        public CapabilityField BuildField()
        {
            StableId parsedFieldId = StableId.Parse(fieldId);
            CapabilityFieldValue value;
            switch (valueKind)
            {
                case CapabilityValueKind.Boolean:
                    value = CapabilityFieldValue.FromBoolean(booleanValue);
                    break;
                case CapabilityValueKind.Integer:
                    value = CapabilityFieldValue.FromInteger(integerValue);
                    break;
                case CapabilityValueKind.Decimal:
                    value = CapabilityFieldValue.FromDecimal(decimalValue);
                    break;
                case CapabilityValueKind.Text:
                    value = CapabilityFieldValue.FromText(textValue ?? string.Empty);
                    break;
                case CapabilityValueKind.StableId:
                    value = CapabilityFieldValue.FromStableId(
                        StableId.Parse(stableIdValue));
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported capability field value kind '{valueKind}'.");
            }

            return new CapabilityField(parsedFieldId, value);
        }

        private void SetValue(CapabilityFieldValue value)
        {
            valueKind = value.Kind;
            switch (value.Kind)
            {
                case CapabilityValueKind.Boolean:
                    booleanValue = value.BooleanValue;
                    break;
                case CapabilityValueKind.Integer:
                    integerValue = value.IntegerValue;
                    break;
                case CapabilityValueKind.Decimal:
                    decimalValue = value.DecimalValue;
                    break;
                case CapabilityValueKind.Text:
                    textValue = value.TextValue;
                    break;
                case CapabilityValueKind.StableId:
                    stableIdValue = value.StableIdValue.ToString();
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported capability field value kind '{value.Kind}'.");
            }
        }
    }

    /// <summary>
    /// Authored immutable data for exactly one capability module.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ObjectCapabilityDefinition",
        menuName = "Shooter Mover/Objects/Capability Definition")]
    public sealed class ObjectCapabilityDefinitionAsset :
        ScriptableObject,
        IObjectCapabilityDefinitionSource
    {
        [SerializeField] private string capabilityId = "capability.unassigned";
        [SerializeField] private CapabilityFieldAuthoring[] fields =
            Array.Empty<CapabilityFieldAuthoring>();

        public StableId CapabilityId
        {
            get { return StableId.Parse(capabilityId); }
        }

        public CapabilityDefinition BuildDefinition()
        {
            List<CapabilityField> domainFields =
                new List<CapabilityField>(fields == null ? 0 : fields.Length);

            if (fields != null)
            {
                for (int index = 0; index < fields.Length; index++)
                {
                    CapabilityFieldAuthoring field = fields[index];
                    if (field == null)
                    {
                        throw new InvalidOperationException(
                            $"Capability '{capabilityId}' contains a null field entry.");
                    }

                    domainFields.Add(field.BuildField());
                }
            }

            return new CapabilityDefinition(CapabilityId, domainFields);
        }

        public void ValidateOrThrow()
        {
            BuildDefinition();
        }

        public static ObjectCapabilityDefinitionAsset CreateRuntime(
            string capabilityId,
            params CapabilityFieldAuthoring[] fields)
        {
            ObjectCapabilityDefinitionAsset asset =
                CreateInstance<ObjectCapabilityDefinitionAsset>();
            asset.capabilityId = capabilityId
                ?? throw new ArgumentNullException(nameof(capabilityId));
            asset.fields = fields ?? Array.Empty<CapabilityFieldAuthoring>();
            asset.hideFlags = HideFlags.HideAndDontSave;
            asset.ValidateOrThrow();
            return asset;
        }

        private void OnValidate()
        {
            if (fields == null)
            {
                fields = Array.Empty<CapabilityFieldAuthoring>();
            }
        }
    }
}
