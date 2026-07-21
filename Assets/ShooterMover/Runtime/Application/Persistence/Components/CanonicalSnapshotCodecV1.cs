using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace ShooterMover.Application.Persistence.Components
{
    public static class SavePersistenceLimitsV1
    {
        public const int MaximumAccountFileBytes = 16 * 1024 * 1024;
        public const int MaximumAccountPayloadBytes = 12 * 1024 * 1024;
        public const int MaximumComponentPayloadBytes = 2 * 1024 * 1024;
        public const int MaximumNodeDepth = 48;
        public const int MaximumCollectionCount = 8192;
        public const int MaximumPropertyCount = 128;
        public const int MaximumScalarLength = 1024 * 1024;
    }

    public sealed class CanonicalPayloadExceptionV1 : Exception
    {
        public CanonicalPayloadExceptionV1(string rejectionCode)
            : base(rejectionCode)
        {
            RejectionCode = rejectionCode ?? "canonical-payload-invalid";
        }

        public string RejectionCode { get; }
    }

    public enum CanonicalNodeKindV1
    {
        Null = 1,
        Scalar = 2,
        List = 3,
        Object = 4,
    }

    public sealed class CanonicalFieldV1
    {
        public CanonicalFieldV1(string name, CanonicalNodeV1 value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A persisted field name is required.", nameof(name));
            }
            Name = name.Trim();
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public string Name { get; }

        public CanonicalNodeV1 Value { get; }
    }

    public sealed class CanonicalNodeV1
    {
        private readonly ReadOnlyCollection<CanonicalNodeV1> items;
        private readonly ReadOnlyCollection<CanonicalFieldV1> fields;

        private CanonicalNodeV1(
            CanonicalNodeKindV1 kind,
            string scalar,
            IEnumerable<CanonicalNodeV1> items,
            IEnumerable<CanonicalFieldV1> fields)
        {
            Kind = kind;
            Scalar = scalar;
            this.items = new ReadOnlyCollection<CanonicalNodeV1>(
                new List<CanonicalNodeV1>(items ?? Array.Empty<CanonicalNodeV1>()));
            this.fields = new ReadOnlyCollection<CanonicalFieldV1>(
                new List<CanonicalFieldV1>(fields ?? Array.Empty<CanonicalFieldV1>()));
        }

        public CanonicalNodeKindV1 Kind { get; }

        public string Scalar { get; }

        public IReadOnlyList<CanonicalNodeV1> Items { get { return items; } }

        public IReadOnlyList<CanonicalFieldV1> Fields { get { return fields; } }

        public static CanonicalNodeV1 Null()
        {
            return new CanonicalNodeV1(
                CanonicalNodeKindV1.Null,
                null,
                null,
                null);
        }

        public static CanonicalNodeV1 ScalarValue(string value)
        {
            string safe = value ?? string.Empty;
            if (safe.Length > SavePersistenceLimitsV1.MaximumScalarLength)
            {
                throw new CanonicalPayloadExceptionV1(
                    "canonical-scalar-length-exceeded");
            }
            return new CanonicalNodeV1(
                CanonicalNodeKindV1.Scalar,
                safe,
                null,
                null);
        }

        public static CanonicalNodeV1 List(IEnumerable<CanonicalNodeV1> values)
        {
            var copy = new List<CanonicalNodeV1>(
                values ?? throw new ArgumentNullException(nameof(values)));
            if (copy.Count > SavePersistenceLimitsV1.MaximumCollectionCount)
            {
                throw new CanonicalPayloadExceptionV1(
                    "canonical-collection-count-exceeded");
            }
            for (int index = 0; index < copy.Count; index++)
            {
                if (copy[index] == null)
                {
                    throw new ArgumentException(
                        "Canonical lists must not contain null node references.",
                        nameof(values));
                }
            }
            return new CanonicalNodeV1(
                CanonicalNodeKindV1.List,
                null,
                copy,
                null);
        }

        public static CanonicalNodeV1 Object(params CanonicalFieldV1[] values)
        {
            var copy = new List<CanonicalFieldV1>(
                values ?? throw new ArgumentNullException(nameof(values)));
            if (copy.Count > SavePersistenceLimitsV1.MaximumPropertyCount)
            {
                throw new CanonicalPayloadExceptionV1(
                    "canonical-property-count-exceeded");
            }
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < copy.Count; index++)
            {
                CanonicalFieldV1 field = copy[index];
                if (field == null || !names.Add(field.Name))
                {
                    throw new ArgumentException(
                        "Canonical object fields must be non-null and unique.",
                        nameof(values));
                }
            }
            return new CanonicalNodeV1(
                CanonicalNodeKindV1.Object,
                null,
                null,
                copy);
        }
    }

    public static class CanonicalNodeCodecV1
    {
        public static string Encode(CanonicalNodeV1 node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }
            var builder = new StringBuilder();
            Append(builder, node, 0);
            return builder.ToString();
        }

        public static bool TryDecode(
            string payload,
            int maximumPayloadBytes,
            out CanonicalNodeV1 node,
            out string rejectionCode)
        {
            node = null;
            if (payload == null)
            {
                rejectionCode = "canonical-payload-null";
                return false;
            }
            if (Encoding.UTF8.GetByteCount(payload) > maximumPayloadBytes)
            {
                rejectionCode = maximumPayloadBytes
                    == SavePersistenceLimitsV1.MaximumComponentPayloadBytes
                        ? "component-payload-too-large"
                        : "account-payload-too-large";
                return false;
            }

            try
            {
                var parser = new Parser(payload);
                node = parser.ParseNode(0);
                if (!parser.AtEnd)
                {
                    node = null;
                    rejectionCode = "canonical-payload-trailing-data";
                    return false;
                }
                rejectionCode = string.Empty;
                return true;
            }
            catch (CanonicalPayloadExceptionV1 exception)
            {
                node = null;
                rejectionCode = exception.RejectionCode;
                return false;
            }
            catch (FormatException)
            {
                node = null;
                rejectionCode = "canonical-payload-format-invalid";
                return false;
            }
            catch (OverflowException)
            {
                node = null;
                rejectionCode = "canonical-payload-number-overflow";
                return false;
            }
        }

        private static void Append(
            StringBuilder builder,
            CanonicalNodeV1 node,
            int depth)
        {
            if (depth > SavePersistenceLimitsV1.MaximumNodeDepth)
            {
                throw new CanonicalPayloadExceptionV1(
                    "canonical-node-depth-exceeded");
            }

            switch (node.Kind)
            {
                case CanonicalNodeKindV1.Null:
                    builder.Append("N;");
                    return;
                case CanonicalNodeKindV1.Scalar:
                    AppendScalar(builder, node.Scalar);
                    return;
                case CanonicalNodeKindV1.List:
                    if (node.Items.Count
                        > SavePersistenceLimitsV1.MaximumCollectionCount)
                    {
                        throw new CanonicalPayloadExceptionV1(
                            "canonical-collection-count-exceeded");
                    }
                    builder.Append('L')
                        .Append(node.Items.Count.ToString(
                            CultureInfo.InvariantCulture))
                        .Append(':');
                    for (int index = 0; index < node.Items.Count; index++)
                    {
                        Append(builder, node.Items[index], depth + 1);
                    }
                    return;
                case CanonicalNodeKindV1.Object:
                    if (node.Fields.Count
                        > SavePersistenceLimitsV1.MaximumPropertyCount)
                    {
                        throw new CanonicalPayloadExceptionV1(
                            "canonical-property-count-exceeded");
                    }
                    builder.Append('O')
                        .Append(node.Fields.Count.ToString(
                            CultureInfo.InvariantCulture))
                        .Append(':');
                    for (int index = 0; index < node.Fields.Count; index++)
                    {
                        AppendScalar(builder, node.Fields[index].Name);
                        Append(builder, node.Fields[index].Value, depth + 1);
                    }
                    return;
                default:
                    throw new CanonicalPayloadExceptionV1(
                        "canonical-node-kind-invalid");
            }
        }

        private static void AppendScalar(StringBuilder builder, string value)
        {
            string safe = value ?? string.Empty;
            if (safe.Length > SavePersistenceLimitsV1.MaximumScalarLength)
            {
                throw new CanonicalPayloadExceptionV1(
                    "canonical-scalar-length-exceeded");
            }
            builder.Append('V')
                .Append(safe.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(safe);
        }

        private sealed class Parser
        {
            private readonly string text;
            private int index;

            public Parser(string text)
            {
                this.text = text;
            }

            public bool AtEnd { get { return index == text.Length; } }

            public CanonicalNodeV1 ParseNode(int depth)
            {
                if (depth > SavePersistenceLimitsV1.MaximumNodeDepth)
                {
                    throw new CanonicalPayloadExceptionV1(
                        "canonical-node-depth-exceeded");
                }
                char tag = ReadCharacter();
                switch (tag)
                {
                    case 'N':
                        Require(';');
                        return CanonicalNodeV1.Null();
                    case 'V':
                        return CanonicalNodeV1.ScalarValue(
                            ReadText(ReadBoundedCount(
                                SavePersistenceLimitsV1.MaximumScalarLength,
                                "canonical-scalar-length-exceeded")));
                    case 'L':
                    {
                        int count = ReadBoundedCount(
                            SavePersistenceLimitsV1.MaximumCollectionCount,
                            "canonical-collection-count-exceeded");
                        var values = new List<CanonicalNodeV1>(count);
                        for (int itemIndex = 0;
                            itemIndex < count;
                            itemIndex++)
                        {
                            values.Add(ParseNode(depth + 1));
                        }
                        return CanonicalNodeV1.List(values);
                    }
                    case 'O':
                    {
                        int count = ReadBoundedCount(
                            SavePersistenceLimitsV1.MaximumPropertyCount,
                            "canonical-property-count-exceeded");
                        var fields = new CanonicalFieldV1[count];
                        var names = new HashSet<string>(StringComparer.Ordinal);
                        for (int fieldIndex = 0;
                            fieldIndex < count;
                            fieldIndex++)
                        {
                            if (ReadCharacter() != 'V')
                            {
                                throw new FormatException();
                            }
                            string name = ReadText(ReadBoundedCount(
                                SavePersistenceLimitsV1.MaximumScalarLength,
                                "canonical-scalar-length-exceeded"));
                            if (string.IsNullOrWhiteSpace(name)
                                || !names.Add(name))
                            {
                                throw new CanonicalPayloadExceptionV1(
                                    "canonical-object-field-invalid");
                            }
                            fields[fieldIndex] = new CanonicalFieldV1(
                                name,
                                ParseNode(depth + 1));
                        }
                        return CanonicalNodeV1.Object(fields);
                    }
                    default:
                        throw new FormatException();
                }
            }

            private int ReadBoundedCount(int maximum, string rejectionCode)
            {
                int start = index;
                while (index < text.Length
                    && text[index] >= '0'
                    && text[index] <= '9')
                {
                    index++;
                }
                if (start == index || index >= text.Length || text[index] != ':')
                {
                    throw new FormatException();
                }
                int count = int.Parse(
                    text.Substring(start, index - start),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture);
                index++;
                if (count < 0 || count > maximum)
                {
                    throw new CanonicalPayloadExceptionV1(rejectionCode);
                }
                return count;
            }

            private string ReadText(int length)
            {
                if (length < 0 || length > text.Length - index)
                {
                    throw new FormatException();
                }
                string output = text.Substring(index, length);
                index += length;
                return output;
            }

            private char ReadCharacter()
            {
                if (index >= text.Length)
                {
                    throw new FormatException();
                }
                return text[index++];
            }

            private void Require(char expected)
            {
                if (ReadCharacter() != expected)
                {
                    throw new FormatException();
                }
            }
        }
    }

    public sealed class CanonicalObjectReaderV1
    {
        private readonly CanonicalNodeV1 node;
        private int index;

        public CanonicalObjectReaderV1(
            CanonicalNodeV1 node,
            params string[] exactFieldOrder)
        {
            this.node = node ?? throw new ArgumentNullException(nameof(node));
            if (node.Kind != CanonicalNodeKindV1.Object)
            {
                throw new CanonicalPayloadExceptionV1(
                    "canonical-object-expected");
            }
            if (exactFieldOrder == null
                || node.Fields.Count != exactFieldOrder.Length)
            {
                throw new CanonicalPayloadExceptionV1(
                    "canonical-object-shape-mismatch");
            }
            for (int fieldIndex = 0;
                fieldIndex < exactFieldOrder.Length;
                fieldIndex++)
            {
                if (!string.Equals(
                    node.Fields[fieldIndex].Name,
                    exactFieldOrder[fieldIndex],
                    StringComparison.Ordinal))
                {
                    throw new CanonicalPayloadExceptionV1(
                        "canonical-object-field-order-mismatch");
                }
            }
        }

        public CanonicalNodeV1 Next(string expectedName)
        {
            if (index >= node.Fields.Count
                || !string.Equals(
                    node.Fields[index].Name,
                    expectedName,
                    StringComparison.Ordinal))
            {
                throw new CanonicalPayloadExceptionV1(
                    "canonical-object-field-order-mismatch");
            }
            return node.Fields[index++].Value;
        }
    }

    public static class CanonicalValueV1
    {
        public static CanonicalFieldV1 Field(
            string name,
            CanonicalNodeV1 value)
        {
            return new CanonicalFieldV1(name, value);
        }

        public static CanonicalNodeV1 String(string value)
        {
            return value == null
                ? CanonicalNodeV1.Null()
                : CanonicalNodeV1.ScalarValue(value);
        }

        public static CanonicalNodeV1 RequiredString(string value)
        {
            return CanonicalNodeV1.ScalarValue(
                value ?? throw new ArgumentNullException(nameof(value)));
        }

        public static CanonicalNodeV1 Int32(int value)
        {
            return CanonicalNodeV1.ScalarValue(
                value.ToString(CultureInfo.InvariantCulture));
        }

        public static CanonicalNodeV1 Int64(long value)
        {
            return CanonicalNodeV1.ScalarValue(
                value.ToString(CultureInfo.InvariantCulture));
        }

        public static CanonicalNodeV1 UInt64(ulong value)
        {
            return CanonicalNodeV1.ScalarValue(
                value.ToString(CultureInfo.InvariantCulture));
        }

        public static CanonicalNodeV1 Boolean(bool value)
        {
            return CanonicalNodeV1.ScalarValue(value ? "1" : "0");
        }

        public static CanonicalNodeV1 OptionalInt64(long? value)
        {
            return value.HasValue ? Int64(value.Value) : CanonicalNodeV1.Null();
        }

        public static string ReadRequiredString(CanonicalNodeV1 node)
        {
            if (node == null || node.Kind != CanonicalNodeKindV1.Scalar)
            {
                throw new CanonicalPayloadExceptionV1(
                    "canonical-scalar-expected");
            }
            return node.Scalar;
        }

        public static string ReadOptionalString(CanonicalNodeV1 node)
        {
            if (node == null)
            {
                throw new CanonicalPayloadExceptionV1(
                    "canonical-node-null-reference");
            }
            if (node.Kind == CanonicalNodeKindV1.Null)
            {
                return null;
            }
            return ReadRequiredString(node);
        }

        public static int ReadInt32(CanonicalNodeV1 node)
        {
            int value;
            if (!int.TryParse(
                ReadRequiredString(node),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value))
            {
                throw new CanonicalPayloadExceptionV1(
                    "canonical-int32-invalid");
            }
            return value;
        }

        public static long ReadInt64(CanonicalNodeV1 node)
        {
            long value;
            if (!long.TryParse(
                ReadRequiredString(node),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value))
            {
                throw new CanonicalPayloadExceptionV1(
                    "canonical-int64-invalid");
            }
            return value;
        }

        public static ulong ReadUInt64(CanonicalNodeV1 node)
        {
            ulong value;
            if (!ulong.TryParse(
                ReadRequiredString(node),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out value))
            {
                throw new CanonicalPayloadExceptionV1(
                    "canonical-uint64-invalid");
            }
            return value;
        }

        public static long? ReadOptionalInt64(CanonicalNodeV1 node)
        {
            return node.Kind == CanonicalNodeKindV1.Null
                ? (long?)null
                : ReadInt64(node);
        }

        public static bool ReadBoolean(CanonicalNodeV1 node)
        {
            string value = ReadRequiredString(node);
            if (value == "1") return true;
            if (value == "0") return false;
            throw new CanonicalPayloadExceptionV1(
                "canonical-boolean-invalid");
        }

        public static IReadOnlyList<CanonicalNodeV1> ReadList(
            CanonicalNodeV1 node)
        {
            if (node == null || node.Kind != CanonicalNodeKindV1.List)
            {
                throw new CanonicalPayloadExceptionV1(
                    "canonical-list-expected");
            }
            return node.Items;
        }
    }
}
