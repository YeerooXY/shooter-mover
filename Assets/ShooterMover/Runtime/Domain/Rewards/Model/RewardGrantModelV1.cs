using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Rewards.Model
{
    /// <summary>
    /// Closed grant vocabulary shared by every reward-producing product surface.
    /// The content identifier supplies the concrete currency, box, item, or equipment definition.
    /// </summary>
    public enum RewardGrantKindV1
    {
        Money = 1,
        Scrap = 2,
        Strongbox = 3,
        EquipmentReference = 4,
        PremiumAmmo = 5,
        Miscellaneous = 6,
    }

    /// <summary>
    /// Identifies an explicit progression/source input which a later generator may consume.
    /// This descriptor carries no curve and performs no scaling itself.
    /// </summary>
    public enum RewardScalingInputKindV1
    {
        CharacterLevel = 1,
        RegionLevel = 2,
        Difficulty = 3,
        SourceTier = 4,
        Custom = 5,
    }

    /// <summary>
    /// Immutable inclusive positive quantity range.
    /// </summary>
    public sealed class RewardQuantityRangeV1 : IEquatable<RewardQuantityRangeV1>
    {
        private readonly string canonicalText;

        private RewardQuantityRangeV1(long minimum, long maximum)
        {
            if (minimum < 1L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimum),
                    minimum,
                    "Reward quantities must be positive.");
            }

            if (maximum < minimum)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximum),
                    maximum,
                    "Maximum reward quantity must be greater than or equal to minimum quantity.");
            }

            this.Minimum = minimum;
            this.Maximum = maximum;
            this.canonicalText = "minimum="
                + minimum.ToString(CultureInfo.InvariantCulture)
                + "\nmaximum="
                + maximum.ToString(CultureInfo.InvariantCulture);
        }

        public long Minimum { get; }

        public long Maximum { get; }

        public bool IsFixed
        {
            get { return this.Minimum == this.Maximum; }
        }

        public static RewardQuantityRangeV1 Create(long minimum, long maximum)
        {
            return new RewardQuantityRangeV1(minimum, maximum);
        }

        public static RewardQuantityRangeV1 Fixed(long quantity)
        {
            return new RewardQuantityRangeV1(quantity, quantity);
        }

        public string ToCanonicalString()
        {
            return this.canonicalText;
        }

        public bool Equals(RewardQuantityRangeV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(this.canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as RewardQuantityRangeV1);
        }

        public override int GetHashCode()
        {
            return RewardModelFormatV1.DeterministicHash(this.canonicalText);
        }

        public override string ToString()
        {
            return this.canonicalText;
        }
    }

    /// <summary>
    /// Immutable reference to one named scaling input. The later progression/generator
    /// authorities define how the input affects an authored range.
    /// </summary>
    public sealed class RewardScalingInputDescriptorV1 :
        IEquatable<RewardScalingInputDescriptorV1>,
        IComparable<RewardScalingInputDescriptorV1>,
        IComparable
    {
        private readonly string canonicalText;

        private RewardScalingInputDescriptorV1(
            StableId inputStableId,
            RewardScalingInputKindV1 kind)
        {
            this.InputStableId = RewardModelFormatV1.RequireStableId(
                inputStableId,
                nameof(inputStableId));
            RewardModelFormatV1.RequireDefinedEnum(kind, nameof(kind));
            this.Kind = kind;
            this.canonicalText = "input_stable_id="
                + this.InputStableId
                + "\nkind="
                + ((int)this.Kind).ToString(CultureInfo.InvariantCulture);
        }

        public StableId InputStableId { get; }

        public RewardScalingInputKindV1 Kind { get; }

        public static RewardScalingInputDescriptorV1 Create(
            StableId inputStableId,
            RewardScalingInputKindV1 kind)
        {
            return new RewardScalingInputDescriptorV1(inputStableId, kind);
        }

        public string ToCanonicalString()
        {
            return this.canonicalText;
        }

        public bool Equals(RewardScalingInputDescriptorV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(this.canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as RewardScalingInputDescriptorV1);
        }

        public override int GetHashCode()
        {
            return RewardModelFormatV1.DeterministicHash(this.canonicalText);
        }

        public int CompareTo(RewardScalingInputDescriptorV1 other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }

            int identityComparison = this.InputStableId.CompareTo(other.InputStableId);
            if (identityComparison != 0)
            {
                return identityComparison;
            }

            return this.Kind.CompareTo(other.Kind);
        }

        int IComparable.CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }

            RewardScalingInputDescriptorV1 other = obj as RewardScalingInputDescriptorV1;
            if (other == null)
            {
                throw new ArgumentException(
                    "Object must be a RewardScalingInputDescriptorV1.",
                    nameof(obj));
            }

            return this.CompareTo(other);
        }

        public override string ToString()
        {
            return this.canonicalText;
        }
    }

    /// <summary>
    /// Immutable authored grant specification. Equipment remains a StableId reference;
    /// no EQP-001 concrete type is required.
    /// </summary>
    public sealed class RewardGrantSpecificationV1 :
        IEquatable<RewardGrantSpecificationV1>,
        IComparable<RewardGrantSpecificationV1>,
        IComparable
    {
        private readonly ReadOnlyCollection<RewardScalingInputDescriptorV1> scalingInputs;
        private readonly string canonicalText;
        private readonly string fingerprint;

        private RewardGrantSpecificationV1(
            StableId grantStableId,
            RewardGrantKindV1 kind,
            StableId contentStableId,
            RewardQuantityRangeV1 quantity,
            IEnumerable<RewardScalingInputDescriptorV1> scalingInputs)
        {
            this.GrantStableId = RewardModelFormatV1.RequireStableId(
                grantStableId,
                nameof(grantStableId));
            RewardModelFormatV1.RequireDefinedEnum(kind, nameof(kind));
            this.Kind = kind;
            this.ContentStableId = RewardModelFormatV1.RequireStableId(
                contentStableId,
                nameof(contentStableId));
            this.Quantity = quantity ?? throw new ArgumentNullException(nameof(quantity));
            this.scalingInputs = RewardModelFormatV1.CopyAndSortUnique(
                scalingInputs,
                nameof(scalingInputs),
                delegate(RewardScalingInputDescriptorV1 item) { return item.InputStableId; });
            this.canonicalText = this.BuildCanonicalText();
            this.fingerprint = RewardModelFormatV1.Fingerprint(this.canonicalText);
        }

        public StableId GrantStableId { get; }

        public RewardGrantKindV1 Kind { get; }

        /// <summary>
        /// Currency, strongbox, item, premium-ammo, miscellaneous-item, or equipment-definition ID.
        /// </summary>
        public StableId ContentStableId { get; }

        public RewardQuantityRangeV1 Quantity { get; }

        public IReadOnlyList<RewardScalingInputDescriptorV1> ScalingInputs
        {
            get { return this.scalingInputs; }
        }

        public string Fingerprint
        {
            get { return this.fingerprint; }
        }

        public static RewardGrantSpecificationV1 Create(
            StableId grantStableId,
            RewardGrantKindV1 kind,
            StableId contentStableId,
            RewardQuantityRangeV1 quantity,
            IEnumerable<RewardScalingInputDescriptorV1> scalingInputs)
        {
            return new RewardGrantSpecificationV1(
                grantStableId,
                kind,
                contentStableId,
                quantity,
                scalingInputs);
        }

        public static RewardGrantSpecificationV1 CreateFixed(
            StableId grantStableId,
            RewardGrantKindV1 kind,
            StableId contentStableId,
            long quantity)
        {
            return new RewardGrantSpecificationV1(
                grantStableId,
                kind,
                contentStableId,
                RewardQuantityRangeV1.Fixed(quantity),
                Array.Empty<RewardScalingInputDescriptorV1>());
        }

        public string ToCanonicalString()
        {
            return this.canonicalText;
        }

        public bool Equals(RewardGrantSpecificationV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(this.canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as RewardGrantSpecificationV1);
        }

        public override int GetHashCode()
        {
            return RewardModelFormatV1.DeterministicHash(this.canonicalText);
        }

        public int CompareTo(RewardGrantSpecificationV1 other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }

            return this.GrantStableId.CompareTo(other.GrantStableId);
        }

        int IComparable.CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }

            RewardGrantSpecificationV1 other = obj as RewardGrantSpecificationV1;
            if (other == null)
            {
                throw new ArgumentException(
                    "Object must be a RewardGrantSpecificationV1.",
                    nameof(obj));
            }

            return this.CompareTo(other);
        }

        public override string ToString()
        {
            return this.canonicalText;
        }

        private string BuildCanonicalText()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("grant_stable_id=")
                .Append(this.GrantStableId)
                .Append("\nkind=")
                .Append(((int)this.Kind).ToString(CultureInfo.InvariantCulture))
                .Append("\ncontent_stable_id=")
                .Append(this.ContentStableId)
                .Append("\nquantity:\n")
                .Append(this.Quantity.ToCanonicalString())
                .Append("\nscaling_input_count=")
                .Append(this.scalingInputs.Count.ToString(CultureInfo.InvariantCulture));

            for (int index = 0; index < this.scalingInputs.Count; index++)
            {
                builder.Append("\nscaling_input_")
                    .Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append(":\n")
                    .Append(this.scalingInputs[index].ToCanonicalString());
            }

            return builder.ToString();
        }
    }

    internal static class RewardModelFormatV1
    {
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public static StableId RequireStableId(StableId value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            return value;
        }

        public static void RequireDefinedEnum<TEnum>(TEnum value, string parameterName)
            where TEnum : struct
        {
            if (!Enum.IsDefined(typeof(TEnum), value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Value must be a defined contract enum member.");
            }
        }

        public static ReadOnlyCollection<T> CopyAndSortUnique<T>(
            IEnumerable<T> source,
            string parameterName,
            Func<T, StableId> identitySelector)
            where T : IComparable<T>
        {
            if (source == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            List<T> copy = new List<T>();
            HashSet<StableId> identities = new HashSet<StableId>();
            foreach (T item in source)
            {
                if (ReferenceEquals(item, null))
                {
                    throw new ArgumentException(
                        parameterName + " must not contain null entries.",
                        parameterName);
                }

                StableId identity = identitySelector(item);
                if (!identities.Add(identity))
                {
                    throw new ArgumentException(
                        parameterName + " contains duplicate identity " + identity + ".",
                        parameterName);
                }

                copy.Add(item);
            }

            copy.Sort();
            return new ReadOnlyCollection<T>(copy);
        }

        public static string Fingerprint(string canonicalText)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(canonicalText);
            byte[] digest;
            using (SHA256 sha256 = SHA256.Create())
            {
                digest = sha256.ComputeHash(bytes);
            }

            StringBuilder builder = new StringBuilder("sha256:", 71);
            for (int index = 0; index < digest.Length; index++)
            {
                builder.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        public static int DeterministicHash(string canonicalText)
        {
            unchecked
            {
                uint hash = FnvOffsetBasis;
                for (int index = 0; index < canonicalText.Length; index++)
                {
                    hash ^= canonicalText[index];
                    hash *= FnvPrime;
                }

                return (int)hash;
            }
        }
    }
}
