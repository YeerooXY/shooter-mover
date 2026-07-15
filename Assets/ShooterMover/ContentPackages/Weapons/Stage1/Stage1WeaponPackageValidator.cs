using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using ShooterMover.Contracts.Content;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;

namespace ShooterMover.ContentPackages.Weapons.Stage1
{
    public enum Stage1WeaponPackageValidationErrorCode
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
        MissingDefaultStartingWeapon = 11,
        DuplicateDefaultStartingWeapon = 12,
        DefaultStartingWeaponMismatch = 13,
        MissingNormalFireProfile = 14,
        MissingEmpoweredFireProfile = 15,
        MissingRuntimeProfile = 16,
        UnsupportedRuntimeProfileVersion = 17,
        MissingIndependentPowerBank = 18,
        ConsumableNormalAmmunition = 19,
        ConsumableEmpoweredAmmunition = 20,
        MissingBehaviorTopology = 21,
        InvalidBehaviorKind = 22,
        BehaviorKindMismatch = 23,
        MalformedBehaviorTopology = 24,
        BehaviorTopologyOutOfBounds = 25,
        ArcAdditionalTargetLimitExceeded = 26,
        RicochetWallBounceLimitExceeded = 27,
        RocketFragmentationNotSupported = 28,
        RocketDetonationCountMismatch = 29,
        RocketSecondDetonationNotSupported = 30,
        EmpoweredBehaviorTopologyChanged = 31,
        EmpoweredCycleModeChanged = 32,
        EmpoweredBehaviorModulesChanged = 33,
        MissingNumericCoefficientCollection = 34,
        NullNumericCoefficient = 35,
        UnknownNumericCoefficient = 36,
        DuplicateNumericCoefficient = 37,
        NonFiniteNumericCoefficient = 38,
        EmpoweredCoefficientSetChanged = 39,
        WrongReferenceKind = 40,
        UnsupportedReferenceVersion = 41,
        BehaviorModuleNotDeclared = 42,
        OutOfBoundRegistryReference = 43,
    }

    /// <summary>
    /// Immutable deterministically ordered Stage 1 weapon-package validation failure.
    /// </summary>
    public sealed class Stage1WeaponPackageValidationError :
        IEquatable<Stage1WeaponPackageValidationError>
    {
        internal Stage1WeaponPackageValidationError(
            Stage1WeaponPackageValidationErrorCode code,
            StableId packageId,
            string detail)
        {
            Code = code;
            PackageId = packageId;
            Detail = detail;
        }

        public Stage1WeaponPackageValidationErrorCode Code { get; }

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

        public bool Equals(Stage1WeaponPackageValidationError other)
        {
            return !ReferenceEquals(other, null)
                && Code == other.Code
                && Equals(PackageId, other.PackageId)
                && string.Equals(Detail, other.Detail, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Stage1WeaponPackageValidationError);
        }

        public override int GetHashCode()
        {
            return Stage1WeaponPackageDescriptor.OrdinalHash(ToCanonicalString());
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }

        internal static string ToCanonicalCode(
            Stage1WeaponPackageValidationErrorCode code)
        {
            switch (code)
            {
                case Stage1WeaponPackageValidationErrorCode.NullDescriptor:
                    return "null-descriptor";
                case Stage1WeaponPackageValidationErrorCode.MissingPackage:
                    return "missing-package";
                case Stage1WeaponPackageValidationErrorCode.DuplicatePackageId:
                    return "duplicate-package-id";
                case Stage1WeaponPackageValidationErrorCode.UnknownPackageId:
                    return "unknown-package-id";
                case Stage1WeaponPackageValidationErrorCode.UnsupportedDescriptorVersion:
                    return "unsupported-descriptor-version";
                case Stage1WeaponPackageValidationErrorCode.MissingContentDefinition:
                    return "missing-content-definition";
                case Stage1WeaponPackageValidationErrorCode.WrongDefinitionKind:
                    return "wrong-definition-kind";
                case Stage1WeaponPackageValidationErrorCode.UnsupportedDefinitionVersion:
                    return "unsupported-definition-version";
                case Stage1WeaponPackageValidationErrorCode.MissingProvenance:
                    return "missing-provenance";
                case Stage1WeaponPackageValidationErrorCode.PrototypeOnlyDefinition:
                    return "prototype-only-definition";
                case Stage1WeaponPackageValidationErrorCode.MissingDefaultStartingWeapon:
                    return "missing-default-starting-weapon";
                case Stage1WeaponPackageValidationErrorCode.DuplicateDefaultStartingWeapon:
                    return "duplicate-default-starting-weapon";
                case Stage1WeaponPackageValidationErrorCode.DefaultStartingWeaponMismatch:
                    return "default-starting-weapon-mismatch";
                case Stage1WeaponPackageValidationErrorCode.MissingNormalFireProfile:
                    return "missing-normal-fire-profile";
                case Stage1WeaponPackageValidationErrorCode.MissingEmpoweredFireProfile:
                    return "missing-empowered-fire-profile";
                case Stage1WeaponPackageValidationErrorCode.MissingRuntimeProfile:
                    return "missing-runtime-profile";
                case Stage1WeaponPackageValidationErrorCode.UnsupportedRuntimeProfileVersion:
                    return "unsupported-runtime-profile-version";
                case Stage1WeaponPackageValidationErrorCode.MissingIndependentPowerBank:
                    return "missing-independent-power-bank";
                case Stage1WeaponPackageValidationErrorCode.ConsumableNormalAmmunition:
                    return "consumable-normal-ammunition";
                case Stage1WeaponPackageValidationErrorCode.ConsumableEmpoweredAmmunition:
                    return "consumable-empowered-ammunition";
                case Stage1WeaponPackageValidationErrorCode.MissingBehaviorTopology:
                    return "missing-behavior-topology";
                case Stage1WeaponPackageValidationErrorCode.InvalidBehaviorKind:
                    return "invalid-behavior-kind";
                case Stage1WeaponPackageValidationErrorCode.BehaviorKindMismatch:
                    return "behavior-kind-mismatch";
                case Stage1WeaponPackageValidationErrorCode.MalformedBehaviorTopology:
                    return "malformed-behavior-topology";
                case Stage1WeaponPackageValidationErrorCode.BehaviorTopologyOutOfBounds:
                    return "behavior-topology-out-of-bounds";
                case Stage1WeaponPackageValidationErrorCode.ArcAdditionalTargetLimitExceeded:
                    return "arc-additional-target-limit-exceeded";
                case Stage1WeaponPackageValidationErrorCode.RicochetWallBounceLimitExceeded:
                    return "ricochet-wall-bounce-limit-exceeded";
                case Stage1WeaponPackageValidationErrorCode.RocketFragmentationNotSupported:
                    return "rocket-fragmentation-not-supported";
                case Stage1WeaponPackageValidationErrorCode.RocketDetonationCountMismatch:
                    return "rocket-detonation-count-mismatch";
                case Stage1WeaponPackageValidationErrorCode.RocketSecondDetonationNotSupported:
                    return "rocket-second-detonation-not-supported";
                case Stage1WeaponPackageValidationErrorCode.EmpoweredBehaviorTopologyChanged:
                    return "empowered-behavior-topology-changed";
                case Stage1WeaponPackageValidationErrorCode.EmpoweredCycleModeChanged:
                    return "empowered-cycle-mode-changed";
                case Stage1WeaponPackageValidationErrorCode.EmpoweredBehaviorModulesChanged:
                    return "empowered-behavior-modules-changed";
                case Stage1WeaponPackageValidationErrorCode.MissingNumericCoefficientCollection:
                    return "missing-numeric-coefficient-collection";
                case Stage1WeaponPackageValidationErrorCode.NullNumericCoefficient:
                    return "null-numeric-coefficient";
                case Stage1WeaponPackageValidationErrorCode.UnknownNumericCoefficient:
                    return "unknown-numeric-coefficient";
                case Stage1WeaponPackageValidationErrorCode.DuplicateNumericCoefficient:
                    return "duplicate-numeric-coefficient";
                case Stage1WeaponPackageValidationErrorCode.NonFiniteNumericCoefficient:
                    return "non-finite-numeric-coefficient";
                case Stage1WeaponPackageValidationErrorCode.EmpoweredCoefficientSetChanged:
                    return "empowered-coefficient-set-changed";
                case Stage1WeaponPackageValidationErrorCode.WrongReferenceKind:
                    return "wrong-reference-kind";
                case Stage1WeaponPackageValidationErrorCode.UnsupportedReferenceVersion:
                    return "unsupported-reference-version";
                case Stage1WeaponPackageValidationErrorCode.BehaviorModuleNotDeclared:
                    return "behavior-module-not-declared";
                case Stage1WeaponPackageValidationErrorCode.OutOfBoundRegistryReference:
                    return "out-of-bound-registry-reference";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(code),
                        code,
                        "Unknown Stage 1 weapon-package validation code.");
            }
        }
    }

    /// <summary>
    /// Immutable validation result whose canonical form is independent of input order.
    /// </summary>
    public sealed class Stage1WeaponPackageValidationResult
    {
        private readonly ReadOnlyCollection<Stage1WeaponPackageDescriptor> packages;
        private readonly ReadOnlyCollection<Stage1WeaponPackageValidationError> errors;

        internal Stage1WeaponPackageValidationResult(
            IList<Stage1WeaponPackageDescriptor> packageValues,
            IList<Stage1WeaponPackageValidationError> errorValues)
        {
            List<Stage1WeaponPackageDescriptor> orderedPackages =
                new List<Stage1WeaponPackageDescriptor>(packageValues);
            orderedPackages.Sort();
            packages =
                new ReadOnlyCollection<Stage1WeaponPackageDescriptor>(orderedPackages);

            List<Stage1WeaponPackageValidationError> orderedErrors =
                new List<Stage1WeaponPackageValidationError>(errorValues);
            orderedErrors.Sort(Stage1WeaponPackageValidationErrorComparer.Instance);
            errors =
                new ReadOnlyCollection<Stage1WeaponPackageValidationError>(orderedErrors);
        }

        public bool IsValid
        {
            get { return errors.Count == 0; }
        }

        public IReadOnlyList<Stage1WeaponPackageDescriptor> Packages
        {
            get { return packages; }
        }

        public IReadOnlyList<Stage1WeaponPackageValidationError> Errors
        {
            get { return errors; }
        }

        /// <summary>
        /// Returns package-owned Content Definitions v1 inputs only after the exact
        /// Stage 1 roster validates. CS-011 remains the sole generated-output writer.
        /// </summary>
        public IReadOnlyList<ContentDefinitionDescriptor> GetRegistryInputs()
        {
            if (!IsValid)
            {
                throw new InvalidOperationException(
                    "Invalid Stage 1 weapon packages cannot be projected as registry inputs.");
            }

            List<ContentDefinitionDescriptor> inputs =
                new List<ContentDefinitionDescriptor>();
            for (int index = 0; index < packages.Count; index++)
            {
                inputs.Add(packages[index].ContentDefinition);
            }

            return new ReadOnlyCollection<ContentDefinitionDescriptor>(inputs);
        }

        public Stage1WeaponPackageDescriptor GetDefaultStartingWeapon()
        {
            if (!IsValid)
            {
                throw new InvalidOperationException(
                    "An invalid Stage 1 weapon roster has no accepted default weapon.");
            }

            for (int index = 0; index < packages.Count; index++)
            {
                if (packages[index].IsDefaultStartingWeapon)
                {
                    return packages[index];
                }
            }

            throw new InvalidOperationException(
                "A valid Stage 1 weapon roster unexpectedly has no default weapon.");
        }

        public bool TryGetPackage(
            StableId packageId,
            out Stage1WeaponPackageDescriptor descriptor)
        {
            if (packageId == null)
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            descriptor = null;
            for (int index = 0; index < packages.Count; index++)
            {
                Stage1WeaponPackageDescriptor candidate = packages[index];
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
                Stage1WeaponPackageDescriptor descriptor = packages[index];
                builder.Append("\npackage_")
                    .Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append('=')
                    .Append(descriptor.DefinitionId == null
                        ? "null"
                        : descriptor.DefinitionId.ToString())
                    .Append('|')
                    .Append(descriptor.IsDefaultStartingWeapon ? "default" : "non-default")
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

        private sealed class Stage1WeaponPackageValidationErrorComparer :
            IComparer<Stage1WeaponPackageValidationError>
        {
            public static readonly Stage1WeaponPackageValidationErrorComparer Instance =
                new Stage1WeaponPackageValidationErrorComparer();

            public int Compare(
                Stage1WeaponPackageValidationError left,
                Stage1WeaponPackageValidationError right)
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
    /// Deterministic validation for exactly the amended five Stage 1 weapons.
    /// This is an authoring boundary only: it does not change combat contracts,
    /// dispatch behavior by weapon ID, or write generated registry outputs.
    /// </summary>
    public static class Stage1WeaponPackageValidator
    {
        public const int MaximumArcAdditionalTargetCount = 3;
        public const int MaximumRicochetWallBounceCount = 2;

        public static Stage1WeaponPackageValidationResult Validate(
            IEnumerable<Stage1WeaponPackageDescriptor> descriptors)
        {
            if (descriptors == null)
            {
                throw new ArgumentNullException(nameof(descriptors));
            }

            List<Stage1WeaponPackageDescriptor> packages =
                new List<Stage1WeaponPackageDescriptor>();
            Dictionary<StableId, List<Stage1WeaponPackageDescriptor>> groups =
                new Dictionary<StableId, List<Stage1WeaponPackageDescriptor>>();
            List<Stage1WeaponPackageValidationError> errors =
                new List<Stage1WeaponPackageValidationError>();
            int nullCount = 0;

            foreach (Stage1WeaponPackageDescriptor descriptor in descriptors)
            {
                if (descriptor == null)
                {
                    nullCount++;
                    continue;
                }

                packages.Add(descriptor);
                if (descriptor.DefinitionId == null)
                {
                    continue;
                }

                List<Stage1WeaponPackageDescriptor> group;
                if (!groups.TryGetValue(descriptor.DefinitionId, out group))
                {
                    group = new List<Stage1WeaponPackageDescriptor>();
                    groups.Add(descriptor.DefinitionId, group);
                }

                group.Add(descriptor);
            }

            if (nullCount > 0)
            {
                AddError(
                    errors,
                    Stage1WeaponPackageValidationErrorCode.NullDescriptor,
                    null,
                    "count=" + nullCount.ToString(CultureInfo.InvariantCulture));
            }

            for (int index = 0;
                 index < Stage1WeaponPackageDescriptor.AcceptedWeaponIds.Count;
                 index++)
            {
                StableId acceptedId =
                    Stage1WeaponPackageDescriptor.AcceptedWeaponIds[index];
                if (!groups.ContainsKey(acceptedId))
                {
                    AddError(
                        errors,
                        Stage1WeaponPackageValidationErrorCode.MissingPackage,
                        acceptedId,
                        null);
                }
            }

            foreach (KeyValuePair<StableId, List<Stage1WeaponPackageDescriptor>> pair
                     in groups)
            {
                if (pair.Value.Count > 1)
                {
                    AddError(
                        errors,
                        Stage1WeaponPackageValidationErrorCode.DuplicatePackageId,
                        pair.Key,
                        "count=" + pair.Value.Count.ToString(CultureInfo.InvariantCulture));
                }
            }

            int defaultCount = 0;
            for (int index = 0; index < packages.Count; index++)
            {
                if (packages[index].IsDefaultStartingWeapon)
                {
                    defaultCount++;
                }

                ValidateDescriptor(packages[index], errors);
            }

            if (defaultCount == 0)
            {
                AddError(
                    errors,
                    Stage1WeaponPackageValidationErrorCode.MissingDefaultStartingWeapon,
                    Stage1WeaponPackageDescriptor.BlasterMachineGunId,
                    null);
            }
            else if (defaultCount > 1)
            {
                AddError(
                    errors,
                    Stage1WeaponPackageValidationErrorCode.DuplicateDefaultStartingWeapon,
                    Stage1WeaponPackageDescriptor.BlasterMachineGunId,
                    "count=" + defaultCount.ToString(CultureInfo.InvariantCulture));
            }

            return new Stage1WeaponPackageValidationResult(packages, errors);
        }

        private static void ValidateDescriptor(
            Stage1WeaponPackageDescriptor descriptor,
            IList<Stage1WeaponPackageValidationError> errors)
        {
            StableId packageId = descriptor.DefinitionId;

            if (descriptor.DescriptorVersion
                != Stage1WeaponPackageDescriptor.CurrentDescriptorVersion)
            {
                AddError(
                    errors,
                    Stage1WeaponPackageValidationErrorCode.UnsupportedDescriptorVersion,
                    packageId,
                    "expected="
                    + Stage1WeaponPackageDescriptor.CurrentDescriptorVersion.ToString(
                        CultureInfo.InvariantCulture)
                    + ";actual="
                    + descriptor.DescriptorVersion.ToString(CultureInfo.InvariantCulture));
            }

            if (descriptor.ContentDefinition == null)
            {
                AddError(
                    errors,
                    Stage1WeaponPackageValidationErrorCode.MissingContentDefinition,
                    null,
                    null);
            }
            else
            {
                ValidateContentDefinition(descriptor.ContentDefinition, errors);
            }

            Stage1WeaponBehaviorKind expectedKind;
            bool hasExpectedKind = TryGetExpectedBehaviorKind(packageId, out expectedKind);
            if (!hasExpectedKind && packageId != null)
            {
                AddError(
                    errors,
                    Stage1WeaponPackageValidationErrorCode.UnknownPackageId,
                    packageId,
                    null);
            }

            ValidateDefaultFlag(descriptor, errors);
            ValidateFireProfile(
                descriptor.NormalFire,
                true,
                packageId,
                hasExpectedKind ? (Stage1WeaponBehaviorKind?)expectedKind : null,
                errors);
            ValidateFireProfile(
                descriptor.EmpoweredFire,
                false,
                packageId,
                hasExpectedKind ? (Stage1WeaponBehaviorKind?)expectedKind : null,
                errors);
            ValidateEmpoweredBoundary(descriptor, errors);
            ValidateRegistryReferences(descriptor, errors);
        }

        private static void ValidateContentDefinition(
            ContentDefinitionDescriptor content,
            IList<Stage1WeaponPackageValidationError> errors)
        {
            StableId packageId = content.DefinitionId;

            if (content.Kind != ContentDefinitionKind.Weapon)
            {
                AddError(
                    errors,
                    Stage1WeaponPackageValidationErrorCode.WrongDefinitionKind,
                    packageId,
                    "expected=weapon;actual=" + ToKindToken(content.Kind));
            }

            if (content.DefinitionVersion != ContentReference.SupportedDefinitionVersion)
            {
                AddError(
                    errors,
                    Stage1WeaponPackageValidationErrorCode.UnsupportedDefinitionVersion,
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
                    Stage1WeaponPackageValidationErrorCode.MissingProvenance,
                    packageId,
                    null);
            }

            if (content.IsPrototypeOnly)
            {
                AddError(
                    errors,
                    Stage1WeaponPackageValidationErrorCode.PrototypeOnlyDefinition,
                    packageId,
                    null);
            }
        }

        private static void ValidateDefaultFlag(
            Stage1WeaponPackageDescriptor descriptor,
            IList<Stage1WeaponPackageValidationError> errors)
        {
            StableId packageId = descriptor.DefinitionId;
            bool shouldBeDefault = packageId != null
                && packageId.Equals(Stage1WeaponPackageDescriptor.BlasterMachineGunId);
            if (descriptor.IsDefaultStartingWeapon != shouldBeDefault)
            {
                AddError(
                    errors,
                    Stage1WeaponPackageValidationErrorCode.DefaultStartingWeaponMismatch,
                    packageId,
                    "expected="
                    + (shouldBeDefault ? "true" : "false")
                    + ";actual="
                    + (descriptor.IsDefaultStartingWeapon ? "true" : "false"));
            }
        }

        private static void ValidateFireProfile(
            Stage1WeaponFireProfile profile,
            bool isNormal,
            StableId packageId,
            Stage1WeaponBehaviorKind? expectedKind,
            IList<Stage1WeaponPackageValidationError> errors)
        {
            string profileName = isNormal ? "normal" : "empowered";
            if (profile == null)
            {
                AddError(
                    errors,
                    isNormal
                        ? Stage1WeaponPackageValidationErrorCode.MissingNormalFireProfile
                        : Stage1WeaponPackageValidationErrorCode.MissingEmpoweredFireProfile,
                    packageId,
                    null);
                return;
            }

            if (profile.RuntimeProfile == null)
            {
                AddError(
                    errors,
                    Stage1WeaponPackageValidationErrorCode.MissingRuntimeProfile,
                    packageId,
                    "profile=" + profileName);
            }
            else
            {
                if (profile.RuntimeProfile.ProfileVersion
                    != WeaponRuntimeProfile.CurrentProfileVersion)
                {
                    AddError(
                        errors,
                        Stage1WeaponPackageValidationErrorCode.UnsupportedRuntimeProfileVersion,
                        packageId,
                        "profile="
                        + profileName
                        + ";expected="
                        + WeaponRuntimeProfile.CurrentProfileVersion.ToString(
                            CultureInfo.InvariantCulture)
                        + ";actual="
                        + profile.RuntimeProfile.ProfileVersion.ToString(
                            CultureInfo.InvariantCulture));
                }

                if (!profile.RuntimeProfile.HasIndependentPowerBank)
                {
                    AddError(
                        errors,
                        Stage1WeaponPackageValidationErrorCode.MissingIndependentPowerBank,
                        packageId,
                        "profile=" + profileName);
                }
            }

            if (isNormal && profile.ConsumesConsumableAmmunition)
            {
                AddError(
                    errors,
                    Stage1WeaponPackageValidationErrorCode.ConsumableNormalAmmunition,
                    packageId,
                    null);
            }

            if (!isNormal && profile.ConsumesConsumableAmmunition)
            {
                AddError(
                    errors,
                    Stage1WeaponPackageValidationErrorCode.ConsumableEmpoweredAmmunition,
                    packageId,
                    null);
            }

            ValidateTopology(
                profile.Topology,
                profileName,
                packageId,
                expectedKind,
                errors);
            ValidateCoefficients(
                profile.NumericCoefficients,
                profileName,
                packageId,
                errors);
        }

        private static void ValidateTopology(
            Stage1WeaponBehaviorTopology topology,
            string profileName,
            StableId packageId,
            Stage1WeaponBehaviorKind? expectedKind,
            IList<Stage1WeaponPackageValidationError> errors)
        {
            if (topology == null)
            {
                AddError(
                    errors,
                    Stage1WeaponPackageValidationErrorCode.MissingBehaviorTopology,
                    packageId,
                    "profile=" + profileName);
                return;
            }

            if (!Enum.IsDefined(typeof(Stage1WeaponBehaviorKind), topology.Kind))
            {
                AddError(
                    errors,
                    Stage1WeaponPackageValidationErrorCode.InvalidBehaviorKind,
                    packageId,
                    "profile="
                    + profileName
                    + ";actual="
                    + ((int)topology.Kind).ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (expectedKind.HasValue && topology.Kind != expectedKind.Value)
            {
                AddError(
                    errors,
                    Stage1WeaponPackageValidationErrorCode.BehaviorKindMismatch,
                    packageId,
                    "profile="
                    + profileName
                    + ";expected="
                    + ToBehaviorToken(expectedKind.Value)
                    + ";actual="
                    + ToBehaviorToken(topology.Kind));
            }

            if (topology.AdditionalTargetCount < 0
                || topology.WallBounceCount < 0
                || topology.DetonationCount < 0)
            {
                AddError(
                    errors,
                    Stage1WeaponPackageValidationErrorCode.MalformedBehaviorTopology,
                    packageId,
                    "profile=" + profileName + ";negative-count");
            }

            Stage1WeaponBehaviorKind validationKind =
                expectedKind.HasValue ? expectedKind.Value : topology.Kind;
            switch (validationKind)
            {
                case Stage1WeaponBehaviorKind.AutomaticProjectile:
                case Stage1WeaponBehaviorKind.SpreadProjectile:
                    ValidateZeroTopology(
                        topology,
                        profileName,
                        packageId,
                        errors);
                    break;

                case Stage1WeaponBehaviorKind.RocketAreaDetonation:
                    if (topology.AdditionalTargetCount != 0
                        || topology.WallBounceCount != 0)
                    {
                        AddTopologyBoundsError(profileName, packageId, topology, errors);
                    }

                    if (topology.HasFragmentation)
                    {
                        AddError(
                            errors,
                            Stage1WeaponPackageValidationErrorCode.RocketFragmentationNotSupported,
                            packageId,
                            "profile=" + profileName);
                    }

                    if (topology.DetonationCount != 1)
                    {
                        AddError(
                            errors,
                            Stage1WeaponPackageValidationErrorCode.RocketDetonationCountMismatch,
                            packageId,
                            "profile="
                            + profileName
                            + ";actual="
                            + topology.DetonationCount.ToString(CultureInfo.InvariantCulture));
                    }

                    if (topology.DetonationCount > 1)
                    {
                        AddError(
                            errors,
                            Stage1WeaponPackageValidationErrorCode.RocketSecondDetonationNotSupported,
                            packageId,
                            "profile="
                            + profileName
                            + ";actual="
                            + topology.DetonationCount.ToString(CultureInfo.InvariantCulture));
                    }

                    break;

                case Stage1WeaponBehaviorKind.ArcChain:
                    if (topology.AdditionalTargetCount
                        > MaximumArcAdditionalTargetCount)
                    {
                        AddError(
                            errors,
                            Stage1WeaponPackageValidationErrorCode
                                .ArcAdditionalTargetLimitExceeded,
                            packageId,
                            "profile="
                            + profileName
                            + ";maximum="
                            + MaximumArcAdditionalTargetCount.ToString(
                                CultureInfo.InvariantCulture)
                            + ";actual="
                            + topology.AdditionalTargetCount.ToString(
                                CultureInfo.InvariantCulture));
                    }

                    if (topology.WallBounceCount != 0
                        || topology.DetonationCount != 0
                        || topology.HasFragmentation)
                    {
                        AddTopologyBoundsError(profileName, packageId, topology, errors);
                    }

                    break;

                case Stage1WeaponBehaviorKind.RicochetProjectile:
                    if (topology.WallBounceCount
                        > MaximumRicochetWallBounceCount)
                    {
                        AddError(
                            errors,
                            Stage1WeaponPackageValidationErrorCode
                                .RicochetWallBounceLimitExceeded,
                            packageId,
                            "profile="
                            + profileName
                            + ";maximum="
                            + MaximumRicochetWallBounceCount.ToString(
                                CultureInfo.InvariantCulture)
                            + ";actual="
                            + topology.WallBounceCount.ToString(
                                CultureInfo.InvariantCulture));
                    }

                    if (topology.AdditionalTargetCount != 0
                        || topology.DetonationCount != 0
                        || topology.HasFragmentation)
                    {
                        AddTopologyBoundsError(profileName, packageId, topology, errors);
                    }

                    break;

                default:
                    throw new InvalidOperationException(
                        "Unreachable Stage 1 weapon behavior kind.");
            }
        }

        private static void ValidateZeroTopology(
            Stage1WeaponBehaviorTopology topology,
            string profileName,
            StableId packageId,
            IList<Stage1WeaponPackageValidationError> errors)
        {
            if (topology.AdditionalTargetCount != 0
                || topology.WallBounceCount != 0
                || topology.DetonationCount != 0
                || topology.HasFragmentation)
            {
                AddTopologyBoundsError(profileName, packageId, topology, errors);
            }
        }

        private static void AddTopologyBoundsError(
            string profileName,
            StableId packageId,
            Stage1WeaponBehaviorTopology topology,
            IList<Stage1WeaponPackageValidationError> errors)
        {
            AddError(
                errors,
                Stage1WeaponPackageValidationErrorCode.BehaviorTopologyOutOfBounds,
                packageId,
                "profile="
                + profileName
                + ";additional_targets="
                + topology.AdditionalTargetCount.ToString(CultureInfo.InvariantCulture)
                + ";wall_bounces="
                + topology.WallBounceCount.ToString(CultureInfo.InvariantCulture)
                + ";detonations="
                + topology.DetonationCount.ToString(CultureInfo.InvariantCulture)
                + ";fragmentation="
                + (topology.HasFragmentation ? "true" : "false"));
        }

        private static void ValidateCoefficients(
            IReadOnlyList<Stage1WeaponNumericCoefficient> coefficients,
            string profileName,
            StableId packageId,
            IList<Stage1WeaponPackageValidationError> errors)
        {
            if (coefficients == null)
            {
                AddError(
                    errors,
                    Stage1WeaponPackageValidationErrorCode
                        .MissingNumericCoefficientCollection,
                    packageId,
                    "profile=" + profileName);
                return;
            }

            Dictionary<Stage1WeaponNumericCoefficientKind, int> counts =
                new Dictionary<Stage1WeaponNumericCoefficientKind, int>();
            int nullCount = 0;
            for (int index = 0; index < coefficients.Count; index++)
            {
                Stage1WeaponNumericCoefficient coefficient = coefficients[index];
                if (coefficient == null)
                {
                    nullCount++;
                    continue;
                }

                if (!Enum.IsDefined(
                        typeof(Stage1WeaponNumericCoefficientKind),
                        coefficient.Kind))
                {
                    AddError(
                        errors,
                        Stage1WeaponPackageValidationErrorCode.UnknownNumericCoefficient,
                        packageId,
                        "profile="
                        + profileName
                        + ";actual="
                        + ((int)coefficient.Kind).ToString(CultureInfo.InvariantCulture));
                }

                if (double.IsNaN(coefficient.Value)
                    || double.IsInfinity(coefficient.Value))
                {
                    AddError(
                        errors,
                        Stage1WeaponPackageValidationErrorCode.NonFiniteNumericCoefficient,
                        packageId,
                        "profile="
                        + profileName
                        + ";kind="
                        + ((int)coefficient.Kind).ToString(CultureInfo.InvariantCulture));
                }

                int count;
                counts.TryGetValue(coefficient.Kind, out count);
                counts[coefficient.Kind] = count + 1;
            }

            if (nullCount > 0)
            {
                AddError(
                    errors,
                    Stage1WeaponPackageValidationErrorCode.NullNumericCoefficient,
                    packageId,
                    "profile="
                    + profileName
                    + ";count="
                    + nullCount.ToString(CultureInfo.InvariantCulture));
            }

            foreach (KeyValuePair<Stage1WeaponNumericCoefficientKind, int> pair
                     in counts.OrderBy(value => (int)value.Key))
            {
                if (pair.Value > 1)
                {
                    AddError(
                        errors,
                        Stage1WeaponPackageValidationErrorCode.DuplicateNumericCoefficient,
                        packageId,
                        "profile="
                        + profileName
                        + ";kind="
                        + ((int)pair.Key).ToString(CultureInfo.InvariantCulture)
                        + ";count="
                        + pair.Value.ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        private static void ValidateEmpoweredBoundary(
            Stage1WeaponPackageDescriptor descriptor,
            IList<Stage1WeaponPackageValidationError> errors)
        {
            Stage1WeaponFireProfile normal = descriptor.NormalFire;
            Stage1WeaponFireProfile empowered = descriptor.EmpoweredFire;
            if (normal == null || empowered == null)
            {
                return;
            }

            if (normal.Topology != null
                && empowered.Topology != null
                && !normal.Topology.Equals(empowered.Topology))
            {
                AddError(
                    errors,
                    Stage1WeaponPackageValidationErrorCode
                        .EmpoweredBehaviorTopologyChanged,
                    descriptor.DefinitionId,
                    null);
            }

            if (normal.RuntimeProfile != null && empowered.RuntimeProfile != null)
            {
                if (normal.RuntimeProfile.CycleMode
                    != empowered.RuntimeProfile.CycleMode)
                {
                    AddError(
                        errors,
                        Stage1WeaponPackageValidationErrorCode
                            .EmpoweredCycleModeChanged,
                        descriptor.DefinitionId,
                        "normal="
                        + normal.RuntimeProfile.CycleMode
                        + ";empowered="
                        + empowered.RuntimeProfile.CycleMode);
                }

                if (!HaveSameBehaviorModules(
                        normal.RuntimeProfile,
                        empowered.RuntimeProfile))
                {
                    AddError(
                        errors,
                        Stage1WeaponPackageValidationErrorCode
                            .EmpoweredBehaviorModulesChanged,
                        descriptor.DefinitionId,
                        null);
                }
            }

            if (normal.NumericCoefficients != null
                && empowered.NumericCoefficients != null
                && !HaveSameCoefficientKinds(
                    normal.NumericCoefficients,
                    empowered.NumericCoefficients))
            {
                AddError(
                    errors,
                    Stage1WeaponPackageValidationErrorCode
                        .EmpoweredCoefficientSetChanged,
                    descriptor.DefinitionId,
                    null);
            }
        }

        private static void ValidateRegistryReferences(
            Stage1WeaponPackageDescriptor descriptor,
            IList<Stage1WeaponPackageValidationError> errors)
        {
            if (descriptor.ContentDefinition == null)
            {
                return;
            }

            WeaponRuntimeProfile normalRuntime = descriptor.NormalFire == null
                ? null
                : descriptor.NormalFire.RuntimeProfile;
            WeaponRuntimeProfile empoweredRuntime = descriptor.EmpoweredFire == null
                ? null
                : descriptor.EmpoweredFire.RuntimeProfile;

            ValidateModuleDeclarations(
                descriptor.DefinitionId,
                "normal",
                normalRuntime,
                descriptor.ContentDefinition,
                errors);
            ValidateModuleDeclarations(
                descriptor.DefinitionId,
                "empowered",
                empoweredRuntime,
                descriptor.ContentDefinition,
                errors);

            IReadOnlyList<ContentReference> references =
                descriptor.ContentDefinition.References;
            for (int index = 0; index < references.Count; index++)
            {
                ContentReference reference = references[index];
                if (reference.ExpectedKind != ContentDefinitionKind.SharedModule)
                {
                    AddError(
                        errors,
                        Stage1WeaponPackageValidationErrorCode.WrongReferenceKind,
                        descriptor.DefinitionId,
                        "expected=shared-module;actual="
                        + ToKindToken(reference.ExpectedKind)
                        + ";reference="
                        + reference.DefinitionId);
                }

                if (reference.ExpectedVersion
                    != ContentReference.SupportedDefinitionVersion)
                {
                    AddError(
                        errors,
                        Stage1WeaponPackageValidationErrorCode.UnsupportedReferenceVersion,
                        descriptor.DefinitionId,
                        "reference="
                        + reference.DefinitionId
                        + ";expected="
                        + ContentReference.SupportedDefinitionVersion.ToString(
                            CultureInfo.InvariantCulture)
                        + ";actual="
                        + reference.ExpectedVersion.ToString(
                            CultureInfo.InvariantCulture));
                }

                if (!RuntimeContainsModule(normalRuntime, reference.DefinitionId)
                    || !RuntimeContainsModule(empoweredRuntime, reference.DefinitionId))
                {
                    AddError(
                        errors,
                        Stage1WeaponPackageValidationErrorCode
                            .OutOfBoundRegistryReference,
                        descriptor.DefinitionId,
                        reference.DefinitionId.ToString());
                }
            }
        }

        private static void ValidateModuleDeclarations(
            StableId packageId,
            string profileName,
            WeaponRuntimeProfile runtimeProfile,
            ContentDefinitionDescriptor content,
            IList<Stage1WeaponPackageValidationError> errors)
        {
            if (runtimeProfile == null)
            {
                return;
            }

            for (int index = 0; index < runtimeProfile.BehaviorModuleCount; index++)
            {
                StableId moduleId = runtimeProfile.GetBehaviorModuleId(index);
                if (!ContainsSharedModuleReference(content, moduleId))
                {
                    AddError(
                        errors,
                        Stage1WeaponPackageValidationErrorCode
                            .BehaviorModuleNotDeclared,
                        packageId,
                        "profile=" + profileName + ";module=" + moduleId);
                }
            }
        }

        private static bool ContainsSharedModuleReference(
            ContentDefinitionDescriptor content,
            StableId moduleId)
        {
            for (int index = 0; index < content.References.Count; index++)
            {
                ContentReference reference = content.References[index];
                if (reference.DefinitionId.Equals(moduleId)
                    && reference.ExpectedKind == ContentDefinitionKind.SharedModule
                    && reference.ExpectedVersion
                        == ContentReference.SupportedDefinitionVersion)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool RuntimeContainsModule(
            WeaponRuntimeProfile runtimeProfile,
            StableId moduleId)
        {
            if (runtimeProfile == null)
            {
                return false;
            }

            for (int index = 0; index < runtimeProfile.BehaviorModuleCount; index++)
            {
                if (runtimeProfile.GetBehaviorModuleId(index).Equals(moduleId))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HaveSameBehaviorModules(
            WeaponRuntimeProfile left,
            WeaponRuntimeProfile right)
        {
            if (left.BehaviorModuleCount != right.BehaviorModuleCount)
            {
                return false;
            }

            for (int index = 0; index < left.BehaviorModuleCount; index++)
            {
                if (!left.GetBehaviorModuleId(index).Equals(
                        right.GetBehaviorModuleId(index)))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HaveSameCoefficientKinds(
            IReadOnlyList<Stage1WeaponNumericCoefficient> left,
            IReadOnlyList<Stage1WeaponNumericCoefficient> right)
        {
            List<int> leftKinds = CopyCoefficientKinds(left);
            List<int> rightKinds = CopyCoefficientKinds(right);
            if (leftKinds.Count != rightKinds.Count)
            {
                return false;
            }

            for (int index = 0; index < leftKinds.Count; index++)
            {
                if (leftKinds[index] != rightKinds[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static List<int> CopyCoefficientKinds(
            IReadOnlyList<Stage1WeaponNumericCoefficient> coefficients)
        {
            List<int> kinds = new List<int>();
            for (int index = 0; index < coefficients.Count; index++)
            {
                if (coefficients[index] != null)
                {
                    kinds.Add((int)coefficients[index].Kind);
                }
            }

            kinds.Sort();
            return kinds;
        }

        private static bool TryGetExpectedBehaviorKind(
            StableId packageId,
            out Stage1WeaponBehaviorKind kind)
        {
            kind = default(Stage1WeaponBehaviorKind);
            if (packageId == null)
            {
                return false;
            }

            if (packageId.Equals(Stage1WeaponPackageDescriptor.BlasterMachineGunId))
            {
                kind = Stage1WeaponBehaviorKind.AutomaticProjectile;
                return true;
            }

            if (packageId.Equals(Stage1WeaponPackageDescriptor.ShotgunId))
            {
                kind = Stage1WeaponBehaviorKind.SpreadProjectile;
                return true;
            }

            if (packageId.Equals(Stage1WeaponPackageDescriptor.RocketLauncherId))
            {
                kind = Stage1WeaponBehaviorKind.RocketAreaDetonation;
                return true;
            }

            if (packageId.Equals(Stage1WeaponPackageDescriptor.ArcGunId))
            {
                kind = Stage1WeaponBehaviorKind.ArcChain;
                return true;
            }

            if (packageId.Equals(Stage1WeaponPackageDescriptor.RicochetGunId))
            {
                kind = Stage1WeaponBehaviorKind.RicochetProjectile;
                return true;
            }

            return false;
        }

        private static void AddError(
            IList<Stage1WeaponPackageValidationError> errors,
            Stage1WeaponPackageValidationErrorCode code,
            StableId packageId,
            string detail)
        {
            errors.Add(
                new Stage1WeaponPackageValidationError(code, packageId, detail));
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

        private static string ToBehaviorToken(Stage1WeaponBehaviorKind kind)
        {
            switch (kind)
            {
                case Stage1WeaponBehaviorKind.AutomaticProjectile:
                    return "automatic-projectile";
                case Stage1WeaponBehaviorKind.SpreadProjectile:
                    return "spread-projectile";
                case Stage1WeaponBehaviorKind.RocketAreaDetonation:
                    return "rocket-area-detonation";
                case Stage1WeaponBehaviorKind.ArcChain:
                    return "arc-chain";
                case Stage1WeaponBehaviorKind.RicochetProjectile:
                    return "ricochet-projectile";
                default:
                    return ((int)kind).ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}
