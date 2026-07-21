using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ShooterMover.Domain.Common;

namespace ShooterMover.ConditionRuntime
{
    /// <summary>
    /// Required companion contract for accepted gameplay-fact adapters. The condition
    /// runtime requests this canonical fingerprint before adaptation, so unsupported or
    /// adapter-rejected facts still participate in deterministic replay classification.
    /// </summary>
    public interface IAcceptedGameplayFactSourceFingerprintV1
    {
        string ComputeSourceFactFingerprint(object sourceFact);
    }

    /// <summary>
    /// Generic deterministic fallback used for unsupported source types and reusable by
    /// adapters whose immutable fact DTOs are represented by public read-only properties.
    /// </summary>
    public static class ConditionSourceFactFingerprintV1
    {
        public static string Compute(object sourceFact)
        {
            if (sourceFact == null)
                throw new ArgumentNullException(nameof(sourceFact));
            var visiting = new HashSet<object>(ReferenceComparer.Instance);
            return ConditionRuntimeHashV1.Hash(
                Canonicalize(sourceFact, visiting, 0));
        }

        private static string Canonicalize(
            object value,
            HashSet<object> visiting,
            int depth)
        {
            if (value == null) return Token("null", string.Empty);
            if (depth > 32)
                throw new ArgumentException(
                    "Source-fact graphs must not exceed 32 canonicalization levels.");

            Type type = value.GetType();
            string typeName = type.FullName ?? type.Name;
            if (value is string)
                return Token(typeName, (string)value);
            if (value is StableId)
                return Token(typeName, value.ToString());
            if (value is bool)
                return Token(typeName, (bool)value ? "1" : "0");
            if (value is char)
                return Token(typeName, ((char)value).ToString());
            if (value is Enum)
                return Token(
                    typeName,
                    Convert.ToInt64(value, CultureInfo.InvariantCulture)
                        .ToString(CultureInfo.InvariantCulture));
            if (value is double)
                return Token(
                    typeName,
                    ((double)value).ToString("R", CultureInfo.InvariantCulture));
            if (value is float)
                return Token(
                    typeName,
                    ((float)value).ToString("R", CultureInfo.InvariantCulture));
            if (value is decimal)
                return Token(
                    typeName,
                    ((decimal)value).ToString("G29", CultureInfo.InvariantCulture));
            if (value is DateTime)
                return Token(
                    typeName,
                    ((DateTime)value).ToString("O", CultureInfo.InvariantCulture));
            if (value is DateTimeOffset)
                return Token(
                    typeName,
                    ((DateTimeOffset)value).ToString(
                        "O",
                        CultureInfo.InvariantCulture));
            if (value is TimeSpan)
                return Token(
                    typeName,
                    ((TimeSpan)value).ToString("c", CultureInfo.InvariantCulture));
            if (value is Guid)
                return Token(typeName, ((Guid)value).ToString("D"));
            if (value is IFormattable && type.IsPrimitive)
            {
                return Token(
                    typeName,
                    ((IFormattable)value).ToString(
                        null,
                        CultureInfo.InvariantCulture));
            }

            bool trackReference = !type.IsValueType;
            if (trackReference && !visiting.Add(value))
                throw new ArgumentException(
                    "Source-fact graphs must be acyclic.");
            try
            {
                IDictionary dictionary = value as IDictionary;
                if (dictionary != null)
                {
                    var entries = new List<string>();
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        entries.Add(
                            Canonicalize(entry.Key, visiting, depth + 1)
                            + "=>"
                            + Canonicalize(entry.Value, visiting, depth + 1));
                    }
                    entries.Sort(StringComparer.Ordinal);
                    return Token(typeName, string.Join(";", entries));
                }

                IEnumerable sequence = value as IEnumerable;
                if (sequence != null)
                {
                    var items = new List<string>();
                    foreach (object item in sequence)
                        items.Add(Canonicalize(item, visiting, depth + 1));
                    return Token(typeName, string.Join(";", items));
                }

                PropertyInfo[] properties = type.GetProperties(
                    BindingFlags.Instance | BindingFlags.Public);
                var parts = new List<string>();
                foreach (PropertyInfo property in properties
                    .Where(item => item.CanRead
                        && item.GetIndexParameters().Length == 0)
                    .OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    object propertyValue;
                    try
                    {
                        propertyValue = property.GetValue(value, null);
                    }
                    catch (TargetInvocationException exception)
                    {
                        throw new ArgumentException(
                            "A source-fact property getter failed during canonicalization.",
                            exception);
                    }
                    parts.Add(Token(
                        property.Name,
                        Canonicalize(propertyValue, visiting, depth + 1)));
                }
                return Token(typeName, string.Join(";", parts));
            }
            finally
            {
                if (trackReference) visiting.Remove(value);
            }
        }

        private static string Token(string key, string value)
        {
            string safeKey = key ?? string.Empty;
            string safeValue = value ?? string.Empty;
            return safeKey.Length.ToString(CultureInfo.InvariantCulture)
                + ":" + safeKey + "="
                + safeValue.Length.ToString(CultureInfo.InvariantCulture)
                + ":" + safeValue;
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceComparer Instance =
                new ReferenceComparer();

            public new bool Equals(object left, object right)
            {
                return ReferenceEquals(left, right);
            }

            public int GetHashCode(object value)
            {
                return RuntimeHelpers.GetHashCode(value);
            }
        }
    }
}
