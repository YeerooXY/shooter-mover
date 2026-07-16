using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Authoring
{
    public enum PlacedObjectIdentityKind
    {
        Authored = 0,
        RuntimeSpawned = 1
    }

    /// <summary>
    /// Stable identity for one authored placement or one explicitly identified runtime spawn.
    /// </summary>
    public sealed class PlacedObjectIdentity : IEquatable<PlacedObjectIdentity>
    {
        private PlacedObjectIdentity(
            StableId value,
            PlacedObjectIdentityKind kind,
            StableId spawnOperationId)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
            Kind = kind;
            SpawnOperationId = spawnOperationId;
        }

        public StableId Value { get; }

        public PlacedObjectIdentityKind Kind { get; }

        public StableId SpawnOperationId { get; }

        public static PlacedObjectIdentity CreateAuthored(StableId value)
        {
            return new PlacedObjectIdentity(value, PlacedObjectIdentityKind.Authored, null);
        }

        public static PlacedObjectIdentity CreateRuntimeSpawned(
            StableId value,
            StableId spawnOperationId)
        {
            if (spawnOperationId == null)
            {
                throw new ArgumentNullException(nameof(spawnOperationId));
            }

            return new PlacedObjectIdentity(
                value,
                PlacedObjectIdentityKind.RuntimeSpawned,
                spawnOperationId);
        }

        public bool Equals(PlacedObjectIdentity other)
        {
            return !ReferenceEquals(other, null)
                && Value.Equals(other.Value)
                && Kind == other.Kind
                && object.Equals(SpawnOperationId, other.SpawnOperationId);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PlacedObjectIdentity);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Value.GetHashCode();
                hash = (hash * 397) ^ (int)Kind;
                hash = (hash * 397) ^ (SpawnOperationId == null ? 0 : SpawnOperationId.GetHashCode());
                return hash;
            }
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    public sealed class CapabilityReference : IEquatable<CapabilityReference>
    {
        public CapabilityReference(StableId capabilityId)
        {
            CapabilityId = capabilityId ?? throw new ArgumentNullException(nameof(capabilityId));
        }

        public StableId CapabilityId { get; }

        public bool Equals(CapabilityReference other)
        {
            return !ReferenceEquals(other, null) && CapabilityId.Equals(other.CapabilityId);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CapabilityReference);
        }

        public override int GetHashCode()
        {
            return CapabilityId.GetHashCode();
        }

        public override string ToString()
        {
            return CapabilityId.ToString();
        }
    }

    public sealed class ObjectDefinitionReference : IEquatable<ObjectDefinitionReference>
    {
        public ObjectDefinitionReference(StableId familyId, StableId variantId)
        {
            FamilyId = familyId ?? throw new ArgumentNullException(nameof(familyId));
            VariantId = variantId ?? throw new ArgumentNullException(nameof(variantId));
        }

        public StableId FamilyId { get; }

        public StableId VariantId { get; }

        public bool Equals(ObjectDefinitionReference other)
        {
            return !ReferenceEquals(other, null)
                && FamilyId.Equals(other.FamilyId)
                && VariantId.Equals(other.VariantId);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ObjectDefinitionReference);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (FamilyId.GetHashCode() * 397) ^ VariantId.GetHashCode();
            }
        }

        public override string ToString()
        {
            return FamilyId + "/" + VariantId;
        }
    }

    public enum CapabilityValueKind
    {
        Boolean = 0,
        Integer = 1,
        Decimal = 2,
        Text = 3,
        StableId = 4
    }

    /// <summary>
    /// Small immutable typed value used inside one capability-specific definition.
    /// </summary>
    public sealed class CapabilityFieldValue : IEquatable<CapabilityFieldValue>
    {
        private readonly bool _booleanValue;
        private readonly long _integerValue;
        private readonly double _decimalValue;
        private readonly string _textValue;
        private readonly StableId _stableIdValue;

        private CapabilityFieldValue(
            CapabilityValueKind kind,
            bool booleanValue,
            long integerValue,
            double decimalValue,
            string textValue,
            StableId stableIdValue)
        {
            Kind = kind;
            _booleanValue = booleanValue;
            _integerValue = integerValue;
            _decimalValue = decimalValue;
            _textValue = textValue;
            _stableIdValue = stableIdValue;
        }

        public CapabilityValueKind Kind { get; }

        public bool BooleanValue
        {
            get
            {
                RequireKind(CapabilityValueKind.Boolean);
                return _booleanValue;
            }
        }

        public long IntegerValue
        {
            get
            {
                RequireKind(CapabilityValueKind.Integer);
                return _integerValue;
            }
        }

        public double DecimalValue
        {
            get
            {
                RequireKind(CapabilityValueKind.Decimal);
                return _decimalValue;
            }
        }

        public string TextValue
        {
            get
            {
                RequireKind(CapabilityValueKind.Text);
                return _textValue;
            }
        }

        public StableId StableIdValue
        {
            get
            {
                RequireKind(CapabilityValueKind.StableId);
                return _stableIdValue;
            }
        }

        public static CapabilityFieldValue FromBoolean(bool value)
        {
            return new CapabilityFieldValue(
                CapabilityValueKind.Boolean,
                value,
                0L,
                0d,
                null,
                null);
        }

        public static CapabilityFieldValue FromInteger(long value)
        {
            return new CapabilityFieldValue(
                CapabilityValueKind.Integer,
                false,
                value,
                0d,
                null,
                null);
        }

        public static CapabilityFieldValue FromDecimal(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Capability decimal values must be finite.");
            }

            return new CapabilityFieldValue(
                CapabilityValueKind.Decimal,
                false,
                0L,
                value,
                null,
                null);
        }

        public static CapabilityFieldValue FromText(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return new CapabilityFieldValue(
                CapabilityValueKind.Text,
                false,
                0L,
                0d,
                value,
                null);
        }

        public static CapabilityFieldValue FromStableId(StableId value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return new CapabilityFieldValue(
                CapabilityValueKind.StableId,
                false,
                0L,
                0d,
                null,
                value);
        }

        public string ToCanonicalString()
        {
            switch (Kind)
            {
                case CapabilityValueKind.Boolean:
                    return _booleanValue ? "b:1" : "b:0";
                case CapabilityValueKind.Integer:
                    return "i:" + _integerValue.ToString(CultureInfo.InvariantCulture);
                case CapabilityValueKind.Decimal:
                    return "d:" + _decimalValue.ToString("R", CultureInfo.InvariantCulture);
                case CapabilityValueKind.Text:
                    return "t:" + _textValue.Length.ToString(CultureInfo.InvariantCulture)
                        + ":" + _textValue;
                case CapabilityValueKind.StableId:
                    return "s:" + _stableIdValue;
                default:
                    throw new InvalidOperationException("Unknown capability value kind.");
            }
        }

        public bool Equals(CapabilityFieldValue other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    ToCanonicalString(),
                    other.ToCanonicalString(),
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CapabilityFieldValue);
        }

        public override int GetHashCode()
        {
            return AuthoringFingerprint.Compute32(ToCanonicalString());
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }

        private void RequireKind(CapabilityValueKind expected)
        {
            if (Kind != expected)
            {
                throw new InvalidOperationException(
                    $"Capability value is {Kind}, not {expected}.");
            }
        }
    }

    public sealed class CapabilityField
    {
        public CapabilityField(StableId fieldId, CapabilityFieldValue value)
        {
            FieldId = fieldId ?? throw new ArgumentNullException(nameof(fieldId));
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public StableId FieldId { get; }

        public CapabilityFieldValue Value { get; }

        internal string ToCanonicalString()
        {
            return FieldId + "=" + Value.ToCanonicalString();
        }
    }

    /// <summary>
    /// One capability-specific immutable definition. It contains only fields owned by that capability.
    /// </summary>
    public sealed class CapabilityDefinition : IEquatable<CapabilityDefinition>
    {
        private readonly ReadOnlyCollection<CapabilityField> _fields;
        private readonly string _canonicalText;

        public CapabilityDefinition(
            StableId capabilityId,
            IEnumerable<CapabilityField> fields)
        {
            CapabilityId = capabilityId ?? throw new ArgumentNullException(nameof(capabilityId));
            if (fields == null)
            {
                throw new ArgumentNullException(nameof(fields));
            }

            List<CapabilityField> ordered = new List<CapabilityField>(fields);
            ordered.Sort((left, right) => left.FieldId.CompareTo(right.FieldId));

            for (int index = 0; index < ordered.Count; index++)
            {
                CapabilityField current = ordered[index]
                    ?? throw new ArgumentException("Capability fields cannot contain null.", nameof(fields));
                if (index > 0 && ordered[index - 1].FieldId.Equals(current.FieldId))
                {
                    throw new ArgumentException(
                        $"Duplicate capability field '{current.FieldId}'.",
                        nameof(fields));
                }
            }

            _fields = ordered.AsReadOnly();
            _canonicalText = BuildCanonicalText(CapabilityId, ordered);
            Fingerprint = AuthoringFingerprint.Compute64Hex(_canonicalText);
        }

        public StableId CapabilityId { get; }

        public IReadOnlyList<CapabilityField> Fields
        {
            get { return _fields; }
        }

        public string Fingerprint { get; }

        public bool TryGetField(StableId fieldId, out CapabilityField field)
        {
            if (fieldId == null)
            {
                throw new ArgumentNullException(nameof(fieldId));
            }

            for (int index = 0; index < _fields.Count; index++)
            {
                if (_fields[index].FieldId.Equals(fieldId))
                {
                    field = _fields[index];
                    return true;
                }
            }

            field = null;
            return false;
        }

        public bool Equals(CapabilityDefinition other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(_canonicalText, other._canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CapabilityDefinition);
        }

        public override int GetHashCode()
        {
            return AuthoringFingerprint.Compute32(_canonicalText);
        }

        public override string ToString()
        {
            return _canonicalText;
        }

        internal string CanonicalText
        {
            get { return _canonicalText; }
        }

        private static string BuildCanonicalText(
            StableId capabilityId,
            IReadOnlyList<CapabilityField> fields)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(capabilityId);
            builder.Append('{');
            for (int index = 0; index < fields.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(';');
                }

                builder.Append(fields[index].ToCanonicalString());
            }

            builder.Append('}');
            return builder.ToString();
        }
    }

    public enum CapabilityOverrideMode
    {
        Inherit = 0,
        Override = 1
    }

    public sealed class CapabilitySelection
    {
        private CapabilitySelection(
            StableId capabilityId,
            CapabilityOverrideMode mode,
            CapabilityDefinition overrideDefinition)
        {
            CapabilityId = capabilityId ?? throw new ArgumentNullException(nameof(capabilityId));
            Mode = mode;
            OverrideDefinition = overrideDefinition;

            if (mode == CapabilityOverrideMode.Override)
            {
                if (overrideDefinition == null)
                {
                    throw new ArgumentNullException(nameof(overrideDefinition));
                }

                if (!capabilityId.Equals(overrideDefinition.CapabilityId))
                {
                    throw new ArgumentException(
                        "Capability selection ID must match its override definition.",
                        nameof(overrideDefinition));
                }
            }
            else if (overrideDefinition != null)
            {
                throw new ArgumentException(
                    "Inherited capability selections cannot carry an override definition.",
                    nameof(overrideDefinition));
            }
        }

        public StableId CapabilityId { get; }

        public CapabilityOverrideMode Mode { get; }

        public CapabilityDefinition OverrideDefinition { get; }

        public static CapabilitySelection Inherit(StableId capabilityId)
        {
            return new CapabilitySelection(
                capabilityId,
                CapabilityOverrideMode.Inherit,
                null);
        }

        public static CapabilitySelection Override(CapabilityDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            return new CapabilitySelection(
                definition.CapabilityId,
                CapabilityOverrideMode.Override,
                definition);
        }
    }

    public sealed class ObjectVariantDefinition
    {
        private readonly ReadOnlyCollection<CapabilitySelection> _selections;

        public ObjectVariantDefinition(
            StableId variantId,
            int? objectLevel,
            IEnumerable<CapabilitySelection> selections)
        {
            VariantId = variantId ?? throw new ArgumentNullException(nameof(variantId));
            ObjectLevel = objectLevel;
            if (selections == null)
            {
                throw new ArgumentNullException(nameof(selections));
            }

            List<CapabilitySelection> ordered = new List<CapabilitySelection>(selections);
            ordered.Sort((left, right) => left.CapabilityId.CompareTo(right.CapabilityId));

            for (int index = 0; index < ordered.Count; index++)
            {
                CapabilitySelection current = ordered[index]
                    ?? throw new ArgumentException(
                        "Variant capability selections cannot contain null.",
                        nameof(selections));
                if (index > 0 && ordered[index - 1].CapabilityId.Equals(current.CapabilityId))
                {
                    throw new ArgumentException(
                        $"Duplicate capability selection '{current.CapabilityId}'.",
                        nameof(selections));
                }
            }

            _selections = ordered.AsReadOnly();
        }

        public StableId VariantId { get; }

        public int? ObjectLevel { get; }

        public IReadOnlyList<CapabilitySelection> CapabilitySelections
        {
            get { return _selections; }
        }
    }

    public sealed class ObjectFamilyDefinition
    {
        private readonly ReadOnlyCollection<CapabilityDefinition> _familyDefaults;
        private readonly ReadOnlyCollection<ObjectVariantDefinition> _variants;

        public ObjectFamilyDefinition(
            StableId familyId,
            string displayName,
            StableId defaultVariantId,
            IEnumerable<CapabilityDefinition> familyDefaults,
            IEnumerable<ObjectVariantDefinition> variants)
        {
            FamilyId = familyId ?? throw new ArgumentNullException(nameof(familyId));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            DefaultVariantId = defaultVariantId
                ?? throw new ArgumentNullException(nameof(defaultVariantId));
            if (familyDefaults == null)
            {
                throw new ArgumentNullException(nameof(familyDefaults));
            }

            if (variants == null)
            {
                throw new ArgumentNullException(nameof(variants));
            }

            List<CapabilityDefinition> defaults =
                new List<CapabilityDefinition>(familyDefaults);
            defaults.Sort((left, right) => left.CapabilityId.CompareTo(right.CapabilityId));
            for (int index = 0; index < defaults.Count; index++)
            {
                CapabilityDefinition current = defaults[index]
                    ?? throw new ArgumentException(
                        "Family defaults cannot contain null.",
                        nameof(familyDefaults));
                if (index > 0 && defaults[index - 1].CapabilityId.Equals(current.CapabilityId))
                {
                    throw new ArgumentException(
                        $"Duplicate family capability default '{current.CapabilityId}'.",
                        nameof(familyDefaults));
                }
            }

            List<ObjectVariantDefinition> variantList =
                new List<ObjectVariantDefinition>(variants);
            if (variantList.Count == 0)
            {
                throw new ArgumentException(
                    "An object family must contain at least one variant.",
                    nameof(variants));
            }

            bool defaultFound = false;
            HashSet<string> variantIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < variantList.Count; index++)
            {
                ObjectVariantDefinition current = variantList[index]
                    ?? throw new ArgumentException(
                        "Family variants cannot contain null.",
                        nameof(variants));
                if (!variantIds.Add(current.VariantId.ToString()))
                {
                    throw new ArgumentException(
                        $"Duplicate object variant '{current.VariantId}'.",
                        nameof(variants));
                }

                if (current.VariantId.Equals(DefaultVariantId))
                {
                    defaultFound = true;
                }
            }

            if (!defaultFound)
            {
                throw new ArgumentException(
                    $"Default variant '{DefaultVariantId}' is not present in the family.",
                    nameof(defaultVariantId));
            }

            _familyDefaults = defaults.AsReadOnly();
            _variants = variantList.AsReadOnly();
        }

        public StableId FamilyId { get; }

        public string DisplayName { get; }

        public StableId DefaultVariantId { get; }

        public IReadOnlyList<CapabilityDefinition> FamilyDefaults
        {
            get { return _familyDefaults; }
        }

        /// <summary>
        /// Variants preserve authored order and have no fixed count or enum cap.
        /// </summary>
        public IReadOnlyList<ObjectVariantDefinition> Variants
        {
            get { return _variants; }
        }

        public bool TryGetFamilyDefault(
            StableId capabilityId,
            out CapabilityDefinition definition)
        {
            for (int index = 0; index < _familyDefaults.Count; index++)
            {
                if (_familyDefaults[index].CapabilityId.Equals(capabilityId))
                {
                    definition = _familyDefaults[index];
                    return true;
                }
            }

            definition = null;
            return false;
        }

        public bool TryGetVariant(
            StableId variantId,
            out ObjectVariantDefinition variant)
        {
            for (int index = 0; index < _variants.Count; index++)
            {
                if (_variants[index].VariantId.Equals(variantId))
                {
                    variant = _variants[index];
                    return true;
                }
            }

            variant = null;
            return false;
        }
    }

    public sealed class CapabilityOverride
    {
        private CapabilityOverride(
            StableId capabilityId,
            CapabilityOverrideMode mode,
            CapabilityDefinition overrideDefinition)
        {
            CapabilityId = capabilityId ?? throw new ArgumentNullException(nameof(capabilityId));
            Mode = mode;
            OverrideDefinition = overrideDefinition;

            if (mode == CapabilityOverrideMode.Override)
            {
                if (overrideDefinition == null)
                {
                    throw new ArgumentNullException(nameof(overrideDefinition));
                }

                if (!capabilityId.Equals(overrideDefinition.CapabilityId))
                {
                    throw new ArgumentException(
                        "Capability override ID must match its definition.",
                        nameof(overrideDefinition));
                }
            }
            else if (overrideDefinition != null)
            {
                throw new ArgumentException(
                    "Inherited capability overrides cannot carry a definition.",
                    nameof(overrideDefinition));
            }
        }

        public StableId CapabilityId { get; }

        public CapabilityOverrideMode Mode { get; }

        public CapabilityDefinition OverrideDefinition { get; }

        public static CapabilityOverride Inherit(StableId capabilityId)
        {
            return new CapabilityOverride(
                capabilityId,
                CapabilityOverrideMode.Inherit,
                null);
        }

        public static CapabilityOverride Override(CapabilityDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            return new CapabilityOverride(
                definition.CapabilityId,
                CapabilityOverrideMode.Override,
                definition);
        }
    }

    public sealed class CapabilityResolution
    {
        public CapabilityResolution(
            StableId capabilityId,
            CapabilityDefinition familyDefault,
            CapabilityOverrideMode variantMode,
            CapabilityDefinition variantDefinition,
            CapabilityOverrideMode instanceMode,
            CapabilityDefinition instanceDefinition,
            CapabilityDefinition resolvedDefinition)
        {
            CapabilityId = capabilityId ?? throw new ArgumentNullException(nameof(capabilityId));
            FamilyDefault = familyDefault;
            VariantMode = variantMode;
            VariantDefinition = variantDefinition
                ?? throw new ArgumentNullException(nameof(variantDefinition));
            InstanceMode = instanceMode;
            InstanceDefinition = instanceDefinition;
            ResolvedDefinition = resolvedDefinition
                ?? throw new ArgumentNullException(nameof(resolvedDefinition));
        }

        public StableId CapabilityId { get; }

        public CapabilityDefinition FamilyDefault { get; }

        public CapabilityOverrideMode VariantMode { get; }

        public CapabilityDefinition VariantDefinition { get; }

        public CapabilityOverrideMode InstanceMode { get; }

        public CapabilityDefinition InstanceDefinition { get; }

        public CapabilityDefinition ResolvedDefinition { get; }

        public bool IsInstanceOverridden
        {
            get { return InstanceMode == CapabilityOverrideMode.Override; }
        }
    }

    public sealed class ResolvedCapabilitySet : IEquatable<ResolvedCapabilitySet>
    {
        private readonly ReadOnlyCollection<CapabilityDefinition> _capabilities;
        private readonly string _canonicalText;

        public ResolvedCapabilitySet(IEnumerable<CapabilityDefinition> capabilities)
        {
            if (capabilities == null)
            {
                throw new ArgumentNullException(nameof(capabilities));
            }

            List<CapabilityDefinition> ordered = new List<CapabilityDefinition>(capabilities);
            ordered.Sort((left, right) => left.CapabilityId.CompareTo(right.CapabilityId));
            for (int index = 0; index < ordered.Count; index++)
            {
                CapabilityDefinition current = ordered[index]
                    ?? throw new ArgumentException(
                        "Resolved capabilities cannot contain null.",
                        nameof(capabilities));
                if (index > 0 && ordered[index - 1].CapabilityId.Equals(current.CapabilityId))
                {
                    throw new ArgumentException(
                        $"Duplicate resolved capability '{current.CapabilityId}'.",
                        nameof(capabilities));
                }
            }

            _capabilities = ordered.AsReadOnly();
            StringBuilder builder = new StringBuilder();
            for (int index = 0; index < ordered.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append('|');
                }

                builder.Append(ordered[index].CanonicalText);
            }

            _canonicalText = builder.ToString();
            Fingerprint = AuthoringFingerprint.Compute64Hex(_canonicalText);
        }

        public IReadOnlyList<CapabilityDefinition> Capabilities
        {
            get { return _capabilities; }
        }

        public string Fingerprint { get; }

        public bool TryGet(
            StableId capabilityId,
            out CapabilityDefinition definition)
        {
            for (int index = 0; index < _capabilities.Count; index++)
            {
                if (_capabilities[index].CapabilityId.Equals(capabilityId))
                {
                    definition = _capabilities[index];
                    return true;
                }
            }

            definition = null;
            return false;
        }

        public bool Equals(ResolvedCapabilitySet other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(_canonicalText, other._canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ResolvedCapabilitySet);
        }

        public override int GetHashCode()
        {
            return AuthoringFingerprint.Compute32(_canonicalText);
        }
    }

    public enum ObjectDefinitionResolutionStatus
    {
        Resolved = 0,
        MissingVariant = 1,
        MissingInheritedCapability = 2,
        DuplicateInstanceOverride = 3,
        OverrideTargetsUnselectedCapability = 4
    }

    public sealed class ObjectDefinitionResolutionResult
    {
        private ObjectDefinitionResolutionResult(
            ObjectDefinitionResolutionStatus status,
            string message,
            ObjectDefinitionReference definitionReference,
            ResolvedCapabilitySet resolvedCapabilities,
            IReadOnlyList<CapabilityResolution> capabilityResolutions)
        {
            Status = status;
            Message = message;
            DefinitionReference = definitionReference;
            ResolvedCapabilities = resolvedCapabilities;
            CapabilityResolutions = capabilityResolutions
                ?? new ReadOnlyCollection<CapabilityResolution>(
                    new List<CapabilityResolution>());
        }

        public ObjectDefinitionResolutionStatus Status { get; }

        public string Message { get; }

        public ObjectDefinitionReference DefinitionReference { get; }

        public ResolvedCapabilitySet ResolvedCapabilities { get; }

        public IReadOnlyList<CapabilityResolution> CapabilityResolutions { get; }

        public bool IsResolved
        {
            get { return Status == ObjectDefinitionResolutionStatus.Resolved; }
        }

        internal static ObjectDefinitionResolutionResult Success(
            ObjectDefinitionReference definitionReference,
            ResolvedCapabilitySet resolvedCapabilities,
            IReadOnlyList<CapabilityResolution> capabilityResolutions)
        {
            return new ObjectDefinitionResolutionResult(
                ObjectDefinitionResolutionStatus.Resolved,
                string.Empty,
                definitionReference,
                resolvedCapabilities,
                capabilityResolutions);
        }

        internal static ObjectDefinitionResolutionResult Failure(
            ObjectDefinitionResolutionStatus status,
            string message,
            ObjectDefinitionReference definitionReference)
        {
            return new ObjectDefinitionResolutionResult(
                status,
                message,
                definitionReference,
                null,
                null);
        }
    }

    public static class ObjectDefinitionResolver
    {
        public static ObjectDefinitionResolutionResult Resolve(
            ObjectFamilyDefinition family,
            StableId selectedVariantId,
            IEnumerable<CapabilityOverride> instanceOverrides)
        {
            if (family == null)
            {
                throw new ArgumentNullException(nameof(family));
            }

            StableId variantId = selectedVariantId ?? family.DefaultVariantId;
            ObjectDefinitionReference definitionReference =
                new ObjectDefinitionReference(family.FamilyId, variantId);

            ObjectVariantDefinition variant;
            if (!family.TryGetVariant(variantId, out variant))
            {
                return ObjectDefinitionResolutionResult.Failure(
                    ObjectDefinitionResolutionStatus.MissingVariant,
                    $"Variant '{variantId}' does not exist in family '{family.FamilyId}'.",
                    definitionReference);
            }

            Dictionary<string, ResolutionBuilder> builders =
                new Dictionary<string, ResolutionBuilder>(StringComparer.Ordinal);
            for (int index = 0; index < variant.CapabilitySelections.Count; index++)
            {
                CapabilitySelection selection = variant.CapabilitySelections[index];
                CapabilityDefinition familyDefault;
                family.TryGetFamilyDefault(selection.CapabilityId, out familyDefault);

                CapabilityDefinition variantDefinition;
                if (selection.Mode == CapabilityOverrideMode.Inherit)
                {
                    if (familyDefault == null)
                    {
                        return ObjectDefinitionResolutionResult.Failure(
                            ObjectDefinitionResolutionStatus.MissingInheritedCapability,
                            $"Variant '{variantId}' inherits missing capability "
                                + $"'{selection.CapabilityId}'.",
                            definitionReference);
                    }

                    variantDefinition = familyDefault;
                }
                else
                {
                    variantDefinition = selection.OverrideDefinition;
                }

                builders.Add(
                    selection.CapabilityId.ToString(),
                    new ResolutionBuilder(
                        selection.CapabilityId,
                        familyDefault,
                        selection.Mode,
                        variantDefinition));
            }

            if (instanceOverrides != null)
            {
                HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (CapabilityOverride instanceOverride in instanceOverrides)
                {
                    if (instanceOverride == null)
                    {
                        throw new ArgumentException(
                            "Instance overrides cannot contain null.",
                            nameof(instanceOverrides));
                    }

                    string key = instanceOverride.CapabilityId.ToString();
                    if (!seen.Add(key))
                    {
                        return ObjectDefinitionResolutionResult.Failure(
                            ObjectDefinitionResolutionStatus.DuplicateInstanceOverride,
                            $"Capability '{instanceOverride.CapabilityId}' has more than one "
                                + "instance override.",
                            definitionReference);
                    }

                    ResolutionBuilder builder;
                    if (!builders.TryGetValue(key, out builder))
                    {
                        return ObjectDefinitionResolutionResult.Failure(
                            ObjectDefinitionResolutionStatus.OverrideTargetsUnselectedCapability,
                            $"Capability '{instanceOverride.CapabilityId}' is not selected by "
                                + $"variant '{variantId}'.",
                            definitionReference);
                    }

                    builder.ApplyInstanceOverride(instanceOverride);
                }
            }

            List<ResolutionBuilder> orderedBuilders =
                new List<ResolutionBuilder>(builders.Values);
            orderedBuilders.Sort(
                (left, right) => left.CapabilityId.CompareTo(right.CapabilityId));

            List<CapabilityDefinition> resolved =
                new List<CapabilityDefinition>(orderedBuilders.Count);
            List<CapabilityResolution> resolutions =
                new List<CapabilityResolution>(orderedBuilders.Count);
            for (int index = 0; index < orderedBuilders.Count; index++)
            {
                CapabilityResolution resolution = orderedBuilders[index].Build();
                resolutions.Add(resolution);
                resolved.Add(resolution.ResolvedDefinition);
            }

            return ObjectDefinitionResolutionResult.Success(
                definitionReference,
                new ResolvedCapabilitySet(resolved),
                resolutions.AsReadOnly());
        }

        private sealed class ResolutionBuilder
        {
            private CapabilityOverrideMode _instanceMode;
            private CapabilityDefinition _instanceDefinition;

            public ResolutionBuilder(
                StableId capabilityId,
                CapabilityDefinition familyDefault,
                CapabilityOverrideMode variantMode,
                CapabilityDefinition variantDefinition)
            {
                CapabilityId = capabilityId;
                FamilyDefault = familyDefault;
                VariantMode = variantMode;
                VariantDefinition = variantDefinition;
                _instanceMode = CapabilityOverrideMode.Inherit;
            }

            public StableId CapabilityId { get; }

            public CapabilityDefinition FamilyDefault { get; }

            public CapabilityOverrideMode VariantMode { get; }

            public CapabilityDefinition VariantDefinition { get; }

            public void ApplyInstanceOverride(CapabilityOverride instanceOverride)
            {
                _instanceMode = instanceOverride.Mode;
                _instanceDefinition = instanceOverride.OverrideDefinition;
            }

            public CapabilityResolution Build()
            {
                CapabilityDefinition resolved = _instanceMode == CapabilityOverrideMode.Override
                    ? _instanceDefinition
                    : VariantDefinition;
                return new CapabilityResolution(
                    CapabilityId,
                    FamilyDefault,
                    VariantMode,
                    VariantDefinition,
                    _instanceMode,
                    _instanceDefinition,
                    resolved);
            }
        }
    }

    internal static class AuthoringFingerprint
    {
        private const ulong Fnv64OffsetBasis = 14695981039346656037UL;
        private const ulong Fnv64Prime = 1099511628211UL;
        private const uint Fnv32OffsetBasis = 2166136261U;
        private const uint Fnv32Prime = 16777619U;

        public static string Compute64Hex(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            unchecked
            {
                ulong hash = Fnv64OffsetBasis;
                for (int index = 0; index < text.Length; index++)
                {
                    char value = text[index];
                    hash ^= (byte)(value & 0xff);
                    hash *= Fnv64Prime;
                    hash ^= (byte)(value >> 8);
                    hash *= Fnv64Prime;
                }

                return hash.ToString("x16", CultureInfo.InvariantCulture);
            }
        }

        public static int Compute32(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            unchecked
            {
                uint hash = Fnv32OffsetBasis;
                for (int index = 0; index < text.Length; index++)
                {
                    char value = text[index];
                    hash ^= (byte)(value & 0xff);
                    hash *= Fnv32Prime;
                    hash ^= (byte)(value >> 8);
                    hash *= Fnv32Prime;
                }

                return (int)hash;
            }
        }
    }
}
