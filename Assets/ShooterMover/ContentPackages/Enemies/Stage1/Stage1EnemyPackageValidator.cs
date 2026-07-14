using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Content;
using ShooterMover.Domain.Common;

namespace ShooterMover.ContentPackages.Enemies.Stage1
{
    public enum Stage1EnemyPackageValidationErrorCode
    {
        NullDescriptor = 1,
        MissingPackage = 2,
        DuplicatePackageId = 3,
        UnknownPackageId = 4,
        UnsupportedDescriptorVersion = 5,
        MissingContentDefinition = 6,
        WrongDefinitionKind = 7,
        UnsupportedDefinitionVersion = 8,
        MissingProvenance = 9,
        PrototypeOnlyDefinition = 10,
        InvalidClassification = 11,
        ClassificationMismatch = 12,
        InvalidCombatChannel = 13,
        CombatChannelMismatch = 14,
        InvalidWeightClass = 15,
        WeightClassMismatch = 16,
        MissingMovementReference = 17,
        MissingAttackReference = 18,
        MissingTelegraphReference = 19,
        WrongReferenceKind = 20,
        UnsupportedReferenceVersion = 21,
        OutOfBoundReference = 22,
        ReferenceNotDeclared = 23,
        OutOfBoundRegistryReference = 24,
        UnknownCapability = 25,
        MissingRequiredCapability = 26,
        OutOfBoundCapability = 27,
        ForbiddenEliteCapability = 28,
    }

    /// <summary>
    /// Immutable, deterministically ordered validation failure.
    /// </summary>
    public sealed class Stage1EnemyPackageValidationError :
        IEquatable<Stage1EnemyPackageValidationError>
    {
        internal Stage1EnemyPackageValidationError(
            Stage1EnemyPackageValidationErrorCode code,
            StableId packageId,
            string detail)
        {
            Code = code;
            PackageId = packageId;
            Detail = detail;
        }

        public Stage1EnemyPackageValidationErrorCode Code { get; }

        public StableId PackageId { get; }

        public string Detail { get; }

        public string ToCanonicalString()
        {
            return "code="
                + ToCanonicalCode(Code)
                + "\npackage_id="
                + (PackageId == null ? "null" : PackageId.ToString())
                + "\ndetail="
                + (Detail ?? "null");
        }

        public bool Equals(Stage1EnemyPackageValidationError other)
        {
            return !ReferenceEquals(other, null)
                && Code == other.Code
                && Equals(PackageId, other.PackageId)
                && string.Equals(Detail, other.Detail, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Stage1EnemyPackageValidationError);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + (int)Code;
                hash = (hash * 31) + (PackageId == null ? 0 : PackageId.GetHashCode());
                hash = (hash * 31) + (Detail == null ? 0 : OrdinalHash(Detail));
                return hash;
            }
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }

        internal static string ToCanonicalCode(Stage1EnemyPackageValidationErrorCode code)
        {
            switch (code)
            {
                case Stage1EnemyPackageValidationErrorCode.NullDescriptor:
                    return "null-descriptor";
                case Stage1EnemyPackageValidationErrorCode.MissingPackage:
                    return "missing-package";
                case Stage1EnemyPackageValidationErrorCode.DuplicatePackageId:
                    return "duplicate-package-id";
                case Stage1EnemyPackageValidationErrorCode.UnknownPackageId:
                    return "unknown-package-id";
                case Stage1EnemyPackageValidationErrorCode.UnsupportedDescriptorVersion:
                    return "unsupported-descriptor-version";
                case Stage1EnemyPackageValidationErrorCode.MissingContentDefinition:
                    return "missing-content-definition";
                case Stage1EnemyPackageValidationErrorCode.WrongDefinitionKind:
                    return "wrong-definition-kind";
                case Stage1EnemyPackageValidationErrorCode.UnsupportedDefinitionVersion:
                    return "unsupported-definition-version";
                case Stage1EnemyPackageValidationErrorCode.MissingProvenance:
                    return "missing-provenance";
                case Stage1EnemyPackageValidationErrorCode.PrototypeOnlyDefinition:
                    return "prototype-only-definition";
                case Stage1EnemyPackageValidationErrorCode.InvalidClassification:
                    return "invalid-classification";
                case Stage1EnemyPackageValidationErrorCode.ClassificationMismatch:
                    return "classification-mismatch";
                case Stage1EnemyPackageValidationErrorCode.InvalidCombatChannel:
                    return "invalid-combat-channel";
                case Stage1EnemyPackageValidationErrorCode.CombatChannelMismatch:
                    return "combat-channel-mismatch";
                case Stage1EnemyPackageValidationErrorCode.InvalidWeightClass:
                    return "invalid-weight-class";
                case Stage1EnemyPackageValidationErrorCode.WeightClassMismatch:
                    return "weight-class-mismatch";
                case Stage1EnemyPackageValidationErrorCode.MissingMovementReference:
                    return "missing-movement-reference";
                case Stage1EnemyPackageValidationErrorCode.MissingAttackReference:
                    return "missing-attack-reference";
                case Stage1EnemyPackageValidationErrorCode.MissingTelegraphReference:
                    return "missing-telegraph-reference";
                case Stage1EnemyPackageValidationErrorCode.WrongReferenceKind:
                    return "wrong-reference-kind";
                case Stage1EnemyPackageValidationErrorCode.UnsupportedReferenceVersion:
                    return "unsupported-reference-version";
                case Stage1EnemyPackageValidationErrorCode.OutOfBoundReference:
                    return "out-of-bound-reference";
                case Stage1EnemyPackageValidationErrorCode.ReferenceNotDeclared:
                    return "reference-not-declared";
                case Stage1EnemyPackageValidationErrorCode.OutOfBoundRegistryReference:
                    return "out-of-bound-registry-reference";
                case Stage1EnemyPackageValidationErrorCode.UnknownCapability:
                    return "unknown-capability";
                case Stage1EnemyPackageValidationErrorCode.MissingRequiredCapability:
                    return "missing-required-capability";
                case Stage1EnemyPackageValidationErrorCode.OutOfBoundCapability:
                    return "out-of-bound-capability";
                case Stage1EnemyPackageValidationErrorCode.ForbiddenEliteCapability:
                    return "forbidden-elite-capability";
                default:
                    throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown validation code.");
            }
        }

        private static int OrdinalHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int index = 0; index < value.Length; index++)
                {
                    hash ^= value[index];
                    hash *= 16777619u;
                }

                return (int)hash;
            }
        }
    }

    /// <summary>
    /// Immutable validation result. Input and error order cannot affect its canonical form.
    /// </summary>
    public sealed class Stage1EnemyPackageValidationResult
    {
        private readonly ReadOnlyCollection<Stage1EnemyPackageDescriptor> packages;
        private readonly ReadOnlyCollection<Stage1EnemyPackageValidationError> errors;

        internal Stage1EnemyPackageValidationResult(
            IList<Stage1EnemyPackageDescriptor> packageValues,
            IList<Stage1EnemyPackageValidationError> errorValues)
        {
            List<Stage1EnemyPackageDescriptor> orderedPackages =
                new List<Stage1EnemyPackageDescriptor>(packageValues);
            orderedPackages.Sort();
            packages = new ReadOnlyCollection<Stage1EnemyPackageDescriptor>(orderedPackages);

            List<Stage1EnemyPackageValidationError> orderedErrors =
                new List<Stage1EnemyPackageValidationError>(errorValues);
            orderedErrors.Sort(Stage1EnemyPackageValidationErrorComparer.Instance);
            errors = new ReadOnlyCollection<Stage1EnemyPackageValidationError>(orderedErrors);
        }

        public bool IsValid
        {
            get { return errors.Count == 0; }
        }

        public IReadOnlyList<Stage1EnemyPackageDescriptor> Packages
        {
            get { return packages; }
        }

        public IReadOnlyList<Stage1EnemyPackageValidationError> Errors
        {
            get { return errors; }
        }

        /// <summary>
        /// Returns the five package-owned Content Definitions v1 inputs only after
        /// the Stage 1 roster boundary validates. CS-011 remains the sole writer of
        /// generated registry documents.
        /// </summary>
        public IReadOnlyList<ContentDefinitionDescriptor> GetRegistryInputs()
        {
            if (!IsValid)
            {
                throw new InvalidOperationException(
                    "Invalid Stage 1 enemy packages cannot be projected as registry inputs.");
            }

            List<ContentDefinitionDescriptor> inputs = new List<ContentDefinitionDescriptor>();
            for (int index = 0; index < packages.Count; index++)
            {
                inputs.Add(packages[index].ContentDefinition);
            }

            return new ReadOnlyCollection<ContentDefinitionDescriptor>(inputs);
        }

        public bool TryGetPackage(
            StableId packageId,
            out Stage1EnemyPackageDescriptor descriptor)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            descriptor = null;
            for (int index = 0; index < packages.Count; index++)
            {
                Stage1EnemyPackageDescriptor candidate = packages[index];
                if (candidate.DefinitionId != null
                    && candidate.DefinitionId.Equals(packageId))
                {
                    if (descriptor != null)
                    {
                        descriptor = null;
                        return false;
                    }

                    descriptor = candidate;
                }
            }

            return descriptor != null;
        }

        public string ToCanonicalString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("valid=")
                .Append(IsValid ? "true" : "false")
                .Append("\npackage_count=")
                .Append(packages.Count.ToString(CultureInfo.InvariantCulture));

            for (int index = 0; index < packages.Count; index++)
            {
                Stage1EnemyPackageDescriptor descriptor = packages[index];
                builder.Append("\npackage_")
                    .Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append('=')
                    .Append(descriptor.DefinitionId == null
                        ? "null"
                        : descriptor.DefinitionId.ToString())
                    .Append('|')
                    .Append(((int)descriptor.Classification).ToString(CultureInfo.InvariantCulture))
                    .Append('|')
                    .Append(descriptor.GetHashCode().ToString(CultureInfo.InvariantCulture));
            }

            builder.Append("\nerror_count=")
                .Append(errors.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < errors.Count; index++)
            {
                builder.Append("\nerror_")
                    .Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append(":\n")
                    .Append(errors[index].ToCanonicalString());
            }

            return builder.ToString();
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }

        private sealed class Stage1EnemyPackageValidationErrorComparer :
            IComparer<Stage1EnemyPackageValidationError>
        {
            public static readonly Stage1EnemyPackageValidationErrorComparer Instance =
                new Stage1EnemyPackageValidationErrorComparer();

            public int Compare(
                Stage1EnemyPackageValidationError left,
                Stage1EnemyPackageValidationError right)
            {
                if (ReferenceEquals(left, right))
                {
                    return 0;
                }

                if (ReferenceEquals(left, null))
                {
                    return -1;
                }

                if (ReferenceEquals(right, null))
                {
                    return 1;
                }

                int codeComparison = left.Code.CompareTo(right.Code);
                if (codeComparison != 0)
                {
                    return codeComparison;
                }

                int idComparison = CompareIds(left.PackageId, right.PackageId);
                if (idComparison != 0)
                {
                    return idComparison;
                }

                return string.CompareOrdinal(left.Detail, right.Detail);
            }

            private static int CompareIds(StableId left, StableId right)
            {
                if (ReferenceEquals(left, right))
                {
                    return 0;
                }

                if (ReferenceEquals(left, null))
                {
                    return -1;
                }

                if (ReferenceEquals(right, null))
                {
                    return 1;
                }

                return left.CompareTo(right);
            }
        }
    }

    /// <summary>
    /// Deterministic validation for the exact amended five-enemy Stage 1 roster.
    /// It validates package authoring inputs and never mutates combat, movement,
    /// encounter, mission, or generated-registry state.
    /// </summary>
    public static class Stage1EnemyPackageValidator
    {
        private static readonly Stage1EnemyCapability KnownCapabilities =
            Stage1EnemyCapability.DirectPursuit
            | Stage1EnemyCapability.OrdinaryContactDamage
            | Stage1EnemyCapability.DisposableImpactAttack
            | Stage1EnemyCapability.MobilePositioning
            | Stage1EnemyCapability.StationaryPositioning
            | Stage1EnemyCapability.BlasterProjectile
            | Stage1EnemyCapability.FourBlasterOrigins
            | Stage1EnemyCapability.MildBoundedSpread
            | Stage1EnemyCapability.SafeRecoveryWindow
            | Stage1EnemyCapability.LineOfFireTelegraph
            | Stage1EnemyCapability.PhaseTransition
            | Stage1EnemyCapability.DenialPulse
            | Stage1EnemyCapability.MortarAttack
            | Stage1EnemyCapability.ReinforcementCall
            | Stage1EnemyCapability.Teleport
            | Stage1EnemyCapability.ComplexRepositioning
            | Stage1EnemyCapability.BulletHell;

        private static readonly Stage1EnemyCapability ForbiddenEliteCapabilities =
            Stage1EnemyCapability.PhaseTransition
            | Stage1EnemyCapability.DenialPulse
            | Stage1EnemyCapability.MortarAttack
            | Stage1EnemyCapability.ReinforcementCall
            | Stage1EnemyCapability.Teleport
            | Stage1EnemyCapability.ComplexRepositioning
            | Stage1EnemyCapability.BulletHell;

        private static readonly Stage1EnemyCapability[] OrderedCapabilities =
        {
            Stage1EnemyCapability.DirectPursuit,
            Stage1EnemyCapability.OrdinaryContactDamage,
            Stage1EnemyCapability.DisposableImpactAttack,
            Stage1EnemyCapability.MobilePositioning,
            Stage1EnemyCapability.StationaryPositioning,
            Stage1EnemyCapability.BlasterProjectile,
            Stage1EnemyCapability.FourBlasterOrigins,
            Stage1EnemyCapability.MildBoundedSpread,
            Stage1EnemyCapability.SafeRecoveryWindow,
            Stage1EnemyCapability.LineOfFireTelegraph,
            Stage1EnemyCapability.PhaseTransition,
            Stage1EnemyCapability.DenialPulse,
            Stage1EnemyCapability.MortarAttack,
            Stage1EnemyCapability.ReinforcementCall,
            Stage1EnemyCapability.Teleport,
            Stage1EnemyCapability.ComplexRepositioning,
            Stage1EnemyCapability.BulletHell,
        };

        public static Stage1EnemyPackageValidationResult Validate(
            IEnumerable<Stage1EnemyPackageDescriptor> descriptors)
        {
            if (descriptors == null)
            {
                throw new ArgumentNullException(nameof(descriptors));
            }

            List<Stage1EnemyPackageDescriptor> packages =
                new List<Stage1EnemyPackageDescriptor>();
            Dictionary<StableId, List<Stage1EnemyPackageDescriptor>> groups =
                new Dictionary<StableId, List<Stage1EnemyPackageDescriptor>>();
            List<Stage1EnemyPackageValidationError> errors =
                new List<Stage1EnemyPackageValidationError>();
            int nullCount = 0;

            foreach (Stage1EnemyPackageDescriptor descriptor in descriptors)
            {
                if (descriptor == null)
                {
                    nullCount++;
                    continue;
                }

                packages.Add(descriptor);
                if (descriptor.DefinitionId != null)
                {
                    List<Stage1EnemyPackageDescriptor> group;
                    if (!groups.TryGetValue(descriptor.DefinitionId, out group))
                    {
                        group = new List<Stage1EnemyPackageDescriptor>();
                        groups.Add(descriptor.DefinitionId, group);
                    }

                    group.Add(descriptor);
                }
            }

            if (nullCount > 0)
            {
                AddError(
                    errors,
                    Stage1EnemyPackageValidationErrorCode.NullDescriptor,
                    null,
                    "count=" + nullCount.ToString(CultureInfo.InvariantCulture));
            }

            for (int acceptedIndex = 0;
                 acceptedIndex < Stage1EnemyPackageDescriptor.AcceptedEnemyIds.Count;
                 acceptedIndex++)
            {
                StableId acceptedId =
                    Stage1EnemyPackageDescriptor.AcceptedEnemyIds[acceptedIndex];
                if (!groups.ContainsKey(acceptedId))
                {
                    AddError(
                        errors,
                        Stage1EnemyPackageValidationErrorCode.MissingPackage,
                        acceptedId,
                        null);
                }
            }

            foreach (KeyValuePair<StableId, List<Stage1EnemyPackageDescriptor>> pair in groups)
            {
                if (pair.Value.Count > 1)
                {
                    AddError(
                        errors,
                        Stage1EnemyPackageValidationErrorCode.DuplicatePackageId,
                        pair.Key,
                        "count=" + pair.Value.Count.ToString(CultureInfo.InvariantCulture));
                }
            }

            for (int packageIndex = 0; packageIndex < packages.Count; packageIndex++)
            {
                ValidateDescriptor(packages[packageIndex], errors);
            }

            return new Stage1EnemyPackageValidationResult(packages, errors);
        }

        private static void ValidateDescriptor(
            Stage1EnemyPackageDescriptor descriptor,
            IList<Stage1EnemyPackageValidationError> errors)
        {
            StableId packageId = descriptor.DefinitionId;

            if (descriptor.DescriptorVersion
                != Stage1EnemyPackageDescriptor.CurrentDescriptorVersion)
            {
                AddError(
                    errors,
                    Stage1EnemyPackageValidationErrorCode.UnsupportedDescriptorVersion,
                    packageId,
                    "expected="
                    + Stage1EnemyPackageDescriptor.CurrentDescriptorVersion.ToString(
                        CultureInfo.InvariantCulture)
                    + ";actual="
                    + descriptor.DescriptorVersion.ToString(CultureInfo.InvariantCulture));
            }

            ValidateClassificationValue(descriptor, errors);
            ValidateCombatChannelValue(descriptor, errors);
            ValidateWeightClassValue(descriptor, errors);

            if (descriptor.ContentDefinition == null)
            {
                AddError(
                    errors,
                    Stage1EnemyPackageValidationErrorCode.MissingContentDefinition,
                    null,
                    null);
                ValidateCapabilitiesWithoutRole(descriptor, errors);
                ValidateGenericReferences(descriptor, null, errors);
                return;
            }

            ValidateContentDefinition(descriptor, errors);

            ExpectedPackage expected;
            if (!TryGetExpectedPackage(packageId, out expected))
            {
                AddError(
                    errors,
                    Stage1EnemyPackageValidationErrorCode.UnknownPackageId,
                    packageId,
                    null);
                ValidateCapabilitiesWithoutRole(descriptor, errors);
                ValidateGenericReferences(descriptor, descriptor.ContentDefinition, errors);
                return;
            }

            if (IsKnownClassification(descriptor.Classification)
                && descriptor.Classification != expected.Classification)
            {
                AddError(
                    errors,
                    Stage1EnemyPackageValidationErrorCode.ClassificationMismatch,
                    packageId,
                    "expected="
                    + ToClassificationToken(expected.Classification)
                    + ";actual="
                    + ToClassificationToken(descriptor.Classification));
            }

            if (Enum.IsDefined(typeof(CombatChannel), descriptor.DamageChannel)
                && descriptor.DamageChannel != expected.DamageChannel)
            {
                AddError(
                    errors,
                    Stage1EnemyPackageValidationErrorCode.CombatChannelMismatch,
                    packageId,
                    "expected="
                    + ToChannelToken(expected.DamageChannel)
                    + ";actual="
                    + ToChannelToken(descriptor.DamageChannel));
            }

            ValidateWeightBoundary(descriptor, expected, errors);
            ValidateCapabilities(descriptor, expected, errors);
            ValidateRoleReferences(descriptor, expected, errors);
        }

        private static void ValidateContentDefinition(
            Stage1EnemyPackageDescriptor descriptor,
            IList<Stage1EnemyPackageValidationError> errors)
        {
            ContentDefinitionDescriptor content = descriptor.ContentDefinition;
            StableId packageId = content.DefinitionId;

            if (content.Kind != ContentDefinitionKind.Enemy)
            {
                AddError(
                    errors,
                    Stage1EnemyPackageValidationErrorCode.WrongDefinitionKind,
                    packageId,
                    "expected=enemy;actual=" + ToKindToken(content.Kind));
            }

            if (content.DefinitionVersion != ContentReference.SupportedDefinitionVersion)
            {
                AddError(
                    errors,
                    Stage1EnemyPackageValidationErrorCode.UnsupportedDefinitionVersion,
                    packageId,
                    "expected="
                    + ContentReference.SupportedDefinitionVersion.ToString(
                        CultureInfo.InvariantCulture)
                    + ";actual="
                    + content.DefinitionVersion.ToString(CultureInfo.InvariantCulture));
            }

            if (content.ProvenanceId == null)
            {
                AddError(
                    errors,
                    Stage1EnemyPackageValidationErrorCode.MissingProvenance,
                    packageId,
                    null);
            }

            if (content.IsPrototypeOnly)
            {
                AddError(
                    errors,
                    Stage1EnemyPackageValidationErrorCode.PrototypeOnlyDefinition,
                    packageId,
                    null);
            }
        }

        private static void ValidateClassificationValue(
            Stage1EnemyPackageDescriptor descriptor,
            IList<Stage1EnemyPackageValidationError> errors)
        {
            if (!IsKnownClassification(descriptor.Classification))
            {
                AddError(
                    errors,
                    Stage1EnemyPackageValidationErrorCode.InvalidClassification,
                    descriptor.DefinitionId,
                    "actual="
                    + ((int)descriptor.Classification).ToString(CultureInfo.InvariantCulture));
            }
        }

        private static void ValidateCombatChannelValue(
            Stage1EnemyPackageDescriptor descriptor,
            IList<Stage1EnemyPackageValidationError> errors)
        {
            if (!Enum.IsDefined(typeof(CombatChannel), descriptor.DamageChannel)
                || descriptor.DamageChannel == CombatChannel.System
                || descriptor.DamageChannel == CombatChannel.Environmental)
            {
                AddError(
                    errors,
                    Stage1EnemyPackageValidationErrorCode.InvalidCombatChannel,
                    descriptor.DefinitionId,
                    "actual="
                    + ((int)descriptor.DamageChannel).ToString(CultureInfo.InvariantCulture));
            }
        }

        private static void ValidateWeightClassValue(
            Stage1EnemyPackageDescriptor descriptor,
            IList<Stage1EnemyPackageValidationError> errors)
        {
            if (!Enum.IsDefined(typeof(CombatWeightClass), descriptor.WeightClass))
            {
                AddError(
                    errors,
                    Stage1EnemyPackageValidationErrorCode.InvalidWeightClass,
                    descriptor.DefinitionId,
                    "actual="
                    + ((int)descriptor.WeightClass).ToString(CultureInfo.InvariantCulture));
            }
        }

        private static void ValidateWeightBoundary(
            Stage1EnemyPackageDescriptor descriptor,
            ExpectedPackage expected,
            IList<Stage1EnemyPackageValidationError> errors)
        {
            if (!Enum.IsDefined(typeof(CombatWeightClass), descriptor.WeightClass))
            {
                return;
            }

            if (expected.RequiresImmovable
                && descriptor.WeightClass != CombatWeightClass.Immovable)
            {
                AddError(
                    errors,
                    Stage1EnemyPackageValidationErrorCode.WeightClassMismatch,
                    descriptor.DefinitionId,
                    "stationary-package-requires=immovable;actual="
                    + ToWeightToken(descriptor.WeightClass));
            }

            if (!expected.RequiresImmovable
                && descriptor.WeightClass == CombatWeightClass.Immovable)
            {
                AddError(
                    errors,
                    Stage1EnemyPackageValidationErrorCode.WeightClassMismatch,
                    descriptor.DefinitionId,
                    "moving-or-elite-package-cannot=immovable");
            }
        }

        private static void ValidateCapabilities(
            Stage1EnemyPackageDescriptor descriptor,
            ExpectedPackage expected,
            IList<Stage1EnemyPackageValidationError> errors)
        {
            AppendUnknownCapabilityError(descriptor, errors);

            Stage1EnemyCapability missing =
                expected.RequiredCapabilities & ~descriptor.Capabilities;
            AppendCapabilityErrors(
                descriptor.DefinitionId,
                missing,
                Stage1EnemyPackageValidationErrorCode.MissingRequiredCapability,
                errors);

            if (expected.Classification == Stage1EnemyPackageClassification.Elite)
            {
                Stage1EnemyCapability forbidden =
                    descriptor.Capabilities & ForbiddenEliteCapabilities;
                AppendCapabilityErrors(
                    descriptor.DefinitionId,
                    forbidden,
                    Stage1EnemyPackageValidationErrorCode.ForbiddenEliteCapability,
                    errors);

                Stage1EnemyCapability ordinaryExtras =
                    descriptor.Capabilities
                    & KnownCapabilities
                    & ~expected.AllowedCapabilities
                    & ~ForbiddenEliteCapabilities;
                AppendCapabilityErrors(
                    descriptor.DefinitionId,
                    ordinaryExtras,
                    Stage1EnemyPackageValidationErrorCode.OutOfBoundCapability,
                    errors);
                return;
            }

            Stage1EnemyCapability extras =
                descriptor.Capabilities
                & KnownCapabilities
                & ~expected.AllowedCapabilities;
            AppendCapabilityErrors(
                descriptor.DefinitionId,
                extras,
                Stage1EnemyPackageValidationErrorCode.OutOfBoundCapability,
                errors);
        }

        private static void ValidateCapabilitiesWithoutRole(
            Stage1EnemyPackageDescriptor descriptor,
            IList<Stage1EnemyPackageValidationError> errors)
        {
            AppendUnknownCapabilityError(descriptor, errors);
        }

        private static void AppendUnknownCapabilityError(
            Stage1EnemyPackageDescriptor descriptor,
            IList<Stage1EnemyPackageValidationError> errors)
        {
            ulong unknown = ((ulong)descriptor.Capabilities) & ~((ulong)KnownCapabilities);
            if (unknown != 0UL)
            {
                AddError(
                    errors,
                    Stage1EnemyPackageValidationErrorCode.UnknownCapability,
                    descriptor.DefinitionId,
                    "bits=0x" + unknown.ToString("x16", CultureInfo.InvariantCulture));
            }
        }

        private static void AppendCapabilityErrors(
            StableId packageId,
            Stage1EnemyCapability capabilities,
            Stage1EnemyPackageValidationErrorCode code,
            IList<Stage1EnemyPackageValidationError> errors)
        {
            for (int index = 0; index < OrderedCapabilities.Length; index++)
            {
                Stage1EnemyCapability capability = OrderedCapabilities[index];
                if ((capabilities & capability) != 0)
                {
                    AddError(errors, code, packageId, ToCapabilityToken(capability));
                }
            }
        }

        private static void ValidateRoleReferences(
            Stage1EnemyPackageDescriptor descriptor,
            ExpectedPackage expected,
            IList<Stage1EnemyPackageValidationError> errors)
        {
            ContentDefinitionDescriptor content = descriptor.ContentDefinition;
            ValidateReference(
                descriptor.DefinitionId,
                "movement",
                descriptor.MovementReference,
                Stage1EnemyPackageValidationErrorCode.MissingMovementReference,
                ContentDefinitionKind.SharedModule,
                null,
                content,
                errors);
            ValidateReference(
                descriptor.DefinitionId,
                "attack",
                descriptor.AttackReference,
                Stage1EnemyPackageValidationErrorCode.MissingAttackReference,
                expected.AttackKind,
                expected.ExactAttackId,
                content,
                errors);
            ValidateReference(
                descriptor.DefinitionId,
                "telegraph",
                descriptor.TelegraphReference,
                Stage1EnemyPackageValidationErrorCode.MissingTelegraphReference,
                ContentDefinitionKind.SharedModule,
                null,
                content,
                errors);

            ValidateRegistryReferences(descriptor, expected, errors);
        }

        private static void ValidateGenericReferences(
            Stage1EnemyPackageDescriptor descriptor,
            ContentDefinitionDescriptor content,
            IList<Stage1EnemyPackageValidationError> errors)
        {
            ValidateReference(
                descriptor.DefinitionId,
                "movement",
                descriptor.MovementReference,
                Stage1EnemyPackageValidationErrorCode.MissingMovementReference,
                ContentDefinitionKind.SharedModule,
                null,
                content,
                errors);
            ValidateReference(
                descriptor.DefinitionId,
                "attack",
                descriptor.AttackReference,
                Stage1EnemyPackageValidationErrorCode.MissingAttackReference,
                null,
                null,
                content,
                errors);
            ValidateReference(
                descriptor.DefinitionId,
                "telegraph",
                descriptor.TelegraphReference,
                Stage1EnemyPackageValidationErrorCode.MissingTelegraphReference,
                ContentDefinitionKind.SharedModule,
                null,
                content,
                errors);
        }

        private static void ValidateReference(
            StableId packageId,
            string fieldName,
            ContentReference reference,
            Stage1EnemyPackageValidationErrorCode missingCode,
            ContentDefinitionKind? expectedKind,
            StableId exactId,
            ContentDefinitionDescriptor content,
            IList<Stage1EnemyPackageValidationError> errors)
        {
            if (reference == null)
            {
                AddError(errors, missingCode, packageId, null);
                return;
            }

            if (expectedKind.HasValue && reference.ExpectedKind != expectedKind.Value)
            {
                AddError(
                    errors,
                    Stage1EnemyPackageValidationErrorCode.WrongReferenceKind,
                    packageId,
                    "field="
                    + fieldName
                    + ";expected="
                    + ToKindToken(expectedKind.Value)
                    + ";actual="
                    + ToKindToken(reference.ExpectedKind));
            }

            if (reference.ExpectedVersion != ContentReference.SupportedDefinitionVersion)
            {
                AddError(
                    errors,
                    Stage1EnemyPackageValidationErrorCode.UnsupportedReferenceVersion,
                    packageId,
                    "field="
                    + fieldName
                    + ";expected="
                    + ContentReference.SupportedDefinitionVersion.ToString(
                        CultureInfo.InvariantCulture)
                    + ";actual="
                    + reference.ExpectedVersion.ToString(CultureInfo.InvariantCulture));
            }

            if (exactId != null && !reference.DefinitionId.Equals(exactId))
            {
                AddError(
                    errors,
                    Stage1EnemyPackageValidationErrorCode.OutOfBoundReference,
                    packageId,
                    "field="
                    + fieldName
                    + ";expected="
                    + exactId
                    + ";actual="
                    + reference.DefinitionId);
            }

            if (content != null && !ContainsReference(content, reference))
            {
                AddError(
                    errors,
                    Stage1EnemyPackageValidationErrorCode.ReferenceNotDeclared,
                    packageId,
                    "field=" + fieldName + ";reference=" + ToReferenceToken(reference));
            }
        }

        private static void ValidateRegistryReferences(
            Stage1EnemyPackageDescriptor descriptor,
            ExpectedPackage expected,
            IList<Stage1EnemyPackageValidationError> errors)
        {
            IReadOnlyList<ContentReference> references =
                descriptor.ContentDefinition.References;
            for (int index = 0; index < references.Count; index++)
            {
                ContentReference reference = references[index];
                bool supportedKind = reference.ExpectedKind == ContentDefinitionKind.SharedModule
                    || reference.ExpectedKind == ContentDefinitionKind.Weapon;
                bool supportedVersion = reference.ExpectedVersion
                    == ContentReference.SupportedDefinitionVersion;
                bool supportedWeapon = reference.ExpectedKind != ContentDefinitionKind.Weapon
                    || (expected.ExactAttackId != null
                        && reference.DefinitionId.Equals(expected.ExactAttackId));

                if (!supportedKind || !supportedVersion || !supportedWeapon)
                {
                    AddError(
                        errors,
                        Stage1EnemyPackageValidationErrorCode.OutOfBoundRegistryReference,
                        descriptor.DefinitionId,
                        ToReferenceToken(reference));
                }
            }
        }

        private static bool ContainsReference(
            ContentDefinitionDescriptor descriptor,
            ContentReference reference)
        {
            for (int index = 0; index < descriptor.References.Count; index++)
            {
                if (descriptor.References[index].Equals(reference))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetExpectedPackage(
            StableId packageId,
            out ExpectedPackage expected)
        {
            expected = null;
            if (packageId == null)
            {
                return false;
            }

            if (packageId.Equals(Stage1EnemyPackageDescriptor.PursuerDroneId))
            {
                expected = new ExpectedPackage(
                    Stage1EnemyPackageClassification.Ordinary,
                    CombatChannel.Contact,
                    false,
                    ContentDefinitionKind.SharedModule,
                    null,
                    Stage1EnemyCapability.DirectPursuit
                        | Stage1EnemyCapability.OrdinaryContactDamage);
                return true;
            }

            if (packageId.Equals(Stage1EnemyPackageDescriptor.RamDroidId))
            {
                expected = new ExpectedPackage(
                    Stage1EnemyPackageClassification.Ordinary,
                    CombatChannel.Contact,
                    false,
                    ContentDefinitionKind.SharedModule,
                    null,
                    Stage1EnemyCapability.DirectPursuit
                        | Stage1EnemyCapability.DisposableImpactAttack);
                return true;
            }

            if (packageId.Equals(Stage1EnemyPackageDescriptor.MobileBlasterDroidId))
            {
                expected = new ExpectedPackage(
                    Stage1EnemyPackageClassification.Ordinary,
                    CombatChannel.Kinetic,
                    false,
                    ContentDefinitionKind.Weapon,
                    Stage1EnemyPackageDescriptor.BlasterMachineGunId,
                    Stage1EnemyCapability.MobilePositioning
                        | Stage1EnemyCapability.BlasterProjectile
                        | Stage1EnemyCapability.SafeRecoveryWindow);
                return true;
            }

            if (packageId.Equals(Stage1EnemyPackageDescriptor.BlasterTurretId))
            {
                expected = new ExpectedPackage(
                    Stage1EnemyPackageClassification.Ordinary,
                    CombatChannel.Kinetic,
                    true,
                    ContentDefinitionKind.Weapon,
                    Stage1EnemyPackageDescriptor.BlasterMachineGunId,
                    Stage1EnemyCapability.StationaryPositioning
                        | Stage1EnemyCapability.BlasterProjectile
                        | Stage1EnemyCapability.SafeRecoveryWindow
                        | Stage1EnemyCapability.LineOfFireTelegraph);
                return true;
            }

            if (packageId.Equals(Stage1EnemyPackageDescriptor.FourBlasterEliteId))
            {
                expected = new ExpectedPackage(
                    Stage1EnemyPackageClassification.Elite,
                    CombatChannel.Kinetic,
                    false,
                    ContentDefinitionKind.Weapon,
                    Stage1EnemyPackageDescriptor.BlasterMachineGunId,
                    Stage1EnemyCapability.BlasterProjectile
                        | Stage1EnemyCapability.FourBlasterOrigins
                        | Stage1EnemyCapability.MildBoundedSpread
                        | Stage1EnemyCapability.SafeRecoveryWindow);
                return true;
            }

            return false;
        }

        private static bool IsKnownClassification(
            Stage1EnemyPackageClassification classification)
        {
            return classification == Stage1EnemyPackageClassification.Ordinary
                || classification == Stage1EnemyPackageClassification.Elite;
        }

        private static void AddError(
            IList<Stage1EnemyPackageValidationError> errors,
            Stage1EnemyPackageValidationErrorCode code,
            StableId packageId,
            string detail)
        {
            errors.Add(new Stage1EnemyPackageValidationError(code, packageId, detail));
        }

        private static string ToClassificationToken(
            Stage1EnemyPackageClassification classification)
        {
            switch (classification)
            {
                case Stage1EnemyPackageClassification.Ordinary:
                    return "ordinary";
                case Stage1EnemyPackageClassification.Elite:
                    return "elite";
                default:
                    return ((int)classification).ToString(CultureInfo.InvariantCulture);
            }
        }

        private static string ToChannelToken(CombatChannel channel)
        {
            return channel.ToString().ToLowerInvariant();
        }

        private static string ToWeightToken(CombatWeightClass weightClass)
        {
            return weightClass.ToString().ToLowerInvariant();
        }

        private static string ToKindToken(ContentDefinitionKind kind)
        {
            switch (kind)
            {
                case ContentDefinitionKind.Weapon:
                    return "weapon";
                case ContentDefinitionKind.Enemy:
                    return "enemy";
                case ContentDefinitionKind.Room:
                    return "room";
                case ContentDefinitionKind.Encounter:
                    return "encounter";
                case ContentDefinitionKind.Environment:
                    return "environment";
                case ContentDefinitionKind.SharedModule:
                    return "shared-module";
                default:
                    return ((int)kind).ToString(CultureInfo.InvariantCulture);
            }
        }

        private static string ToReferenceToken(ContentReference reference)
        {
            return ToKindToken(reference.ExpectedKind)
                + "|"
                + reference.DefinitionId
                + "|"
                + reference.ExpectedVersion.ToString(CultureInfo.InvariantCulture);
        }

        private static string ToCapabilityToken(Stage1EnemyCapability capability)
        {
            switch (capability)
            {
                case Stage1EnemyCapability.DirectPursuit:
                    return "direct-pursuit";
                case Stage1EnemyCapability.OrdinaryContactDamage:
                    return "ordinary-contact-damage";
                case Stage1EnemyCapability.DisposableImpactAttack:
                    return "disposable-impact-attack";
                case Stage1EnemyCapability.MobilePositioning:
                    return "mobile-positioning";
                case Stage1EnemyCapability.StationaryPositioning:
                    return "stationary-positioning";
                case Stage1EnemyCapability.BlasterProjectile:
                    return "blaster-projectile";
                case Stage1EnemyCapability.FourBlasterOrigins:
                    return "four-blaster-origins";
                case Stage1EnemyCapability.MildBoundedSpread:
                    return "mild-bounded-spread";
                case Stage1EnemyCapability.SafeRecoveryWindow:
                    return "safe-recovery-window";
                case Stage1EnemyCapability.LineOfFireTelegraph:
                    return "line-of-fire-telegraph";
                case Stage1EnemyCapability.PhaseTransition:
                    return "phase-transition";
                case Stage1EnemyCapability.DenialPulse:
                    return "denial-pulse";
                case Stage1EnemyCapability.MortarAttack:
                    return "mortar-attack";
                case Stage1EnemyCapability.ReinforcementCall:
                    return "reinforcement-call";
                case Stage1EnemyCapability.Teleport:
                    return "teleport";
                case Stage1EnemyCapability.ComplexRepositioning:
                    return "complex-repositioning";
                case Stage1EnemyCapability.BulletHell:
                    return "bullet-hell";
                default:
                    return ((ulong)capability).ToString(CultureInfo.InvariantCulture);
            }
        }

        private sealed class ExpectedPackage
        {
            public ExpectedPackage(
                Stage1EnemyPackageClassification classification,
                CombatChannel damageChannel,
                bool requiresImmovable,
                ContentDefinitionKind attackKind,
                StableId exactAttackId,
                Stage1EnemyCapability capabilities)
            {
                Classification = classification;
                DamageChannel = damageChannel;
                RequiresImmovable = requiresImmovable;
                AttackKind = attackKind;
                ExactAttackId = exactAttackId;
                RequiredCapabilities = capabilities;
                AllowedCapabilities = capabilities;
            }

            public Stage1EnemyPackageClassification Classification { get; }

            public CombatChannel DamageChannel { get; }

            public bool RequiresImmovable { get; }

            public ContentDefinitionKind AttackKind { get; }

            public StableId ExactAttackId { get; }

            public Stage1EnemyCapability RequiredCapabilities { get; }

            public Stage1EnemyCapability AllowedCapabilities { get; }
        }
    }
}
