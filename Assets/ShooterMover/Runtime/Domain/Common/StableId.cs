using System;

namespace ShooterMover.Domain.Common
{
    /// <summary>
    /// Immutable, engine-independent identifier with a canonical lowercase representation.
    /// </summary>
    public sealed class StableId : IEquatable<StableId>, IComparable<StableId>, IComparable
    {
        public const int MaxNamespaceLength = 32;
        public const int MaxValueLength = 96;
        public const int MaxCanonicalLength = 128;

        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        private readonly string _canonical;

        private StableId(string namespaceName, string value)
        {
            Namespace = namespaceName;
            Value = value;
            _canonical = namespaceName + "." + value;
        }

        /// <summary>
        /// Gets the category portion before the canonical dot separator.
        /// </summary>
        public string Namespace { get; }

        /// <summary>
        /// Gets the namespace-local value after the canonical dot separator.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Creates a StableId from already-separated namespace and value components.
        /// </summary>
        /// <exception cref="ArgumentNullException">A component is null.</exception>
        /// <exception cref="FormatException">A component or the combined value is not canonical.</exception>
        public static StableId Create(string namespaceName, string value)
        {
            if (namespaceName == null)
            {
                throw new ArgumentNullException(nameof(namespaceName));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            string error;
            if (!IsValidComponent(namespaceName, MaxNamespaceLength, "namespace", out error))
            {
                throw new FormatException(error);
            }

            if (!IsValidComponent(value, MaxValueLength, "value", out error))
            {
                throw new FormatException(error);
            }

            if (namespaceName.Length + 1 + value.Length > MaxCanonicalLength)
            {
                throw new FormatException(
                    $"StableId canonical length must not exceed {MaxCanonicalLength} characters.");
            }

            return new StableId(namespaceName, value);
        }

        /// <summary>
        /// Parses one canonical StableId string.
        /// </summary>
        /// <exception cref="ArgumentNullException">The input is null.</exception>
        /// <exception cref="FormatException">The input is not a canonical StableId.</exception>
        public static StableId Parse(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            StableId result;
            string error;
            if (!TryParseCore(text, out result, out error))
            {
                throw new FormatException(error);
            }

            return result;
        }

        /// <summary>
        /// Attempts to parse one canonical StableId string without normalization.
        /// </summary>
        public static bool TryParse(string text, out StableId result)
        {
            string ignored;
            return TryParseCore(text, out result, out ignored);
        }

        public bool Equals(StableId other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(_canonical, other._canonical, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as StableId);
        }

        /// <summary>
        /// Returns the deterministic 32-bit FNV-1a hash of the canonical ASCII form.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                uint hash = FnvOffsetBasis;
                for (int index = 0; index < _canonical.Length; index++)
                {
                    hash ^= _canonical[index];
                    hash *= FnvPrime;
                }

                return (int)hash;
            }
        }

        /// <summary>
        /// Orders IDs by ordinal comparison of their complete canonical forms.
        /// </summary>
        public int CompareTo(StableId other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }

            return string.CompareOrdinal(_canonical, other._canonical);
        }

        int IComparable.CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }

            StableId other = obj as StableId;
            if (other == null)
            {
                throw new ArgumentException($"Object must be of type {nameof(StableId)}.", nameof(obj));
            }

            return CompareTo(other);
        }

        public override string ToString()
        {
            return _canonical;
        }

        public static bool operator ==(StableId left, StableId right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
            {
                return false;
            }

            return left.Equals(right);
        }

        public static bool operator !=(StableId left, StableId right)
        {
            return !(left == right);
        }

        public static bool operator <(StableId left, StableId right)
        {
            if (ReferenceEquals(left, null))
            {
                return !ReferenceEquals(right, null);
            }

            return left.CompareTo(right) < 0;
        }

        public static bool operator >(StableId left, StableId right)
        {
            if (ReferenceEquals(left, null))
            {
                return false;
            }

            return left.CompareTo(right) > 0;
        }

        public static bool operator <=(StableId left, StableId right)
        {
            return !(left > right);
        }

        public static bool operator >=(StableId left, StableId right)
        {
            return !(left < right);
        }

        private static bool TryParseCore(string text, out StableId result, out string error)
        {
            result = null;

            if (text == null)
            {
                error = "StableId cannot be null.";
                return false;
            }

            if (text.Length == 0)
            {
                error = "StableId cannot be empty.";
                return false;
            }

            if (text.Length > MaxCanonicalLength)
            {
                error = $"StableId canonical length must not exceed {MaxCanonicalLength} characters.";
                return false;
            }

            int separatorIndex = text.IndexOf('.');
            if (separatorIndex <= 0
                || separatorIndex != text.LastIndexOf('.')
                || separatorIndex == text.Length - 1)
            {
                error = "StableId must contain exactly one dot between non-empty namespace and value components.";
                return false;
            }

            string namespaceName = text.Substring(0, separatorIndex);
            string value = text.Substring(separatorIndex + 1);

            if (!IsValidComponent(namespaceName, MaxNamespaceLength, "namespace", out error))
            {
                return false;
            }

            if (!IsValidComponent(value, MaxValueLength, "value", out error))
            {
                return false;
            }

            result = new StableId(namespaceName, value);
            error = null;
            return true;
        }

        private static bool IsValidComponent(
            string component,
            int maxLength,
            string componentName,
            out string error)
        {
            if (component.Length == 0)
            {
                error = $"StableId {componentName} cannot be empty.";
                return false;
            }

            if (component.Length > maxLength)
            {
                error = $"StableId {componentName} length must not exceed {maxLength} characters.";
                return false;
            }

            bool previousWasHyphen = false;
            for (int index = 0; index < component.Length; index++)
            {
                char current = component[index];
                bool isAsciiLower = current >= 'a' && current <= 'z';
                bool isAsciiDigit = current >= '0' && current <= '9';

                if (isAsciiLower || isAsciiDigit)
                {
                    previousWasHyphen = false;
                    continue;
                }

                if (current == '-'
                    && index > 0
                    && index < component.Length - 1
                    && !previousWasHyphen)
                {
                    previousWasHyphen = true;
                    continue;
                }

                error =
                    $"StableId {componentName} must use lowercase ASCII letters or digits "
                    + "separated by single hyphens.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
