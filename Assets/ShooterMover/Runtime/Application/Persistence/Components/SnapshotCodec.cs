using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace ShooterMover.Application.Persistence.Components
{
    public static class SavePersistenceLimits
    {
        public const int MaximumAccountFileBytes = 16 * 1024 * 1024;
        public const int MaximumAccountPayloadBytes = 12 * 1024 * 1024;
        public const int MaximumComponentPayloadBytes = 2 * 1024 * 1024;
        public const int MaximumNodeDepth = 48;
        public const int MaximumCollectionCount = 8192;
        public const int MaximumPropertyCount = 128;
        public const int MaximumScalarLength = 1024 * 1024;
    }

    public sealed class PayloadException : Exception
    {
        public PayloadException(string rejectionCode)
            : base(rejectionCode)
        {
            RejectionCode = rejectionCode ?? "canonical-payload-invalid";
        }

        public string RejectionCode { get; }
    }

    public enum NodeKind
    {
        Null = 1,
        Scalar = 2,
        List = 3,
        Object = 4,
    }

    public sealed class Field
    {
        public Field(string name, Node value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A persisted field name is required.", nameof(name));
            }
            Name = name.Trim();
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public string Name { get; }

        public Node Value { get; }
    }

    public sealed class Node
    {
        private readonly ReadOnlyCollection<Node> items;
        private readonly ReadOnlyCollection<Field> fields;

        private Node(
            NodeKind kind,
            string scalar,
            IEnumerable<Node> items,
            IEnumerable<Field> fields)
        {
            Kind = kind;
            Scalar = scalar;
            this.items = new ReadOnlyCollection<Node>(
                new List<Node>(items ?? Array.Empty<Node>()));
            this.fields = new ReadOnlyCollection<Field>(
                new List<Field>(fields ?? Array.Empty<Field>()));
        }

        public NodeKind Kind { get; }

        public string Scalar { get; }

        public IReadOnlyList<Node> Items { get { return items; } }

        public IReadOnlyList<Field> Fields { get { return fields; } }

        public static Node Null()
        {
            return new Node(
                NodeKind.Null,
                null,
                null,
                null);
        }

        public static Node ScalarValue(string value)
        {
            string safe = value ?? string.Empty;
            if (safe.Length > SavePersistenceLimits.MaximumScalarLength)
            {
                throw new PayloadException(
                    "canonical-scalar-length-exceeded");
            }
            return new Node(
                NodeKind.Scalar,
                safe,
                null,
                null);
        }

        public static Node List(IEnumerable<Node> values)
        {
            var copy = new List<Node>(
                values ?? throw new ArgumentNullException(nameof(values)));
            if (copy.Count > SavePersistenceLimits.MaximumCollectionCount)
            {
                throw new PayloadException(
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
            return new Node(
                NodeKind.List,
                null,
                copy,
                null);
        }

        public static Node Object(params Field[] values)
        {
            var copy = new List<Field>(
                values ?? throw new ArgumentNullException(nameof(values)));
            if (copy.Count > SavePersistenceLimits.MaximumPropertyCount)
            {
                throw new PayloadException(
                    "canonical-property-count-exceeded");
            }
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < copy.Count; index++)
            {
                Field field = copy[index];
                if (field == null || !names.Add(field.Name))
                {
                    throw new ArgumentException(
                        "Canonical object fields must be non-null and unique.",
                        nameof(values));
                }
            }
            return new Node(
                NodeKind.Object,
                null,
                null,
                copy);
        }
    }

    public static class NodeCodec
    {
        public static string Encode(Node node)
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
            out Node node,
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
                    == SavePersistenceLimits.MaximumComponentPayloadBytes
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
            catch (PayloadException exception)
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
            Node node,
            int depth)
        {
            if (depth > SavePersistenceLimits.MaximumNodeDepth)
            {
                throw new PayloadException(
                    "canonical-node-depth-exceeded");
            }

            switch (node.Kind)
            {
                case NodeKind.Null:
                    builder.Append("N;");
                    return;
                case NodeKind.Scalar:
                    AppendScalar(builder, node.Scalar);
                    return;
                case NodeKind.List:
                    if (node.Items.Count
                        > SavePersistenceLimits.MaximumCollectionCount)
                    {
                        throw new PayloadException(
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
                case NodeKind.Object:
                    if (node.Fields.Count
                        > SavePersistenceLimits.MaximumPropertyCount)
                    {
                        throw new PayloadException(
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
                    throw new PayloadException(
                        "canonical-node-kind-invalid");
            }
        }

        private static void AppendScalar(StringBuilder builder, string value)
        {
            string safe = value ?? string.Empty;
            if (safe.Length > SavePersistenceLimits.MaximumScalarLength)
            {
                throw new PayloadException(
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

            public Node ParseNode(int depth)
            {
                if (depth > SavePersistenceLimits.MaximumNodeDepth)
                {
                    throw new PayloadException(
                        "canonical-node-depth-exceeded");
                }
                char tag = ReadCharacter();
                switch (tag)
                {
                    case 'N':
                        Require(';');
                        return Node.Null();
                    case 'V':
                        return Node.ScalarValue(
                            ReadText(ReadBoundedCount(
                                SavePersistenceLimits.MaximumScalarLength,
                                "canonical-scalar-length-exceeded")));
                    case 'L':
                    {
                        int count = ReadBoundedCount(
                            SavePersistenceLimits.MaximumCollectionCount,
                            "canonical-collection-count-exceeded");
                        var values = new List<Node>(count);
                        for (int itemIndex = 0;
                            itemIndex < count;
                            itemIndex++)
                        {
                            values.Add(ParseNode(depth + 1));
                        }
                        return Node.List(values);
                    }
                    case 'O':
                    {
                        int count = ReadBoundedCount(
                            SavePersistenceLimits.MaximumPropertyCount,
                            "canonical-property-count-exceeded");
                        var fields = new Field[count];
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
                                SavePersistenceLimits.MaximumScalarLength,
                                "canonical-scalar-length-exceeded"));
                            if (string.IsNullOrWhiteSpace(name)
                                || !names.Add(name))
                            {
                                throw new PayloadException(
                                    "canonical-object-field-invalid");
                            }
                            fields[fieldIndex] = new Field(
                                name,
                                ParseNode(depth + 1));
                        }
                        return Node.Object(fields);
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
                    throw new PayloadException(rejectionCode);
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

    public sealed class ObjectReader
    {
        private readonly Node node;
        private int index;

        public ObjectReader(
            Node node,
            params string[] exactFieldOrder)
        {
            this.node = node ?? throw new ArgumentNullException(nameof(node));
            if (node.Kind != NodeKind.Object)
            {
                throw new PayloadException(
                    "canonical-object-expected");
            }
            if (exactFieldOrder == null
                || node.Fields.Count != exactFieldOrder.Length)
            {
                throw new PayloadException(
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
                    throw new PayloadException(
                        "canonical-object-field-order-mismatch");
                }
            }
        }

        public Node Next(string expectedName)
        {
            if (index >= node.Fields.Count
                || !string.Equals(
                    node.Fields[index].Name,
                    expectedName,
                    StringComparison.Ordinal))
            {
                throw new PayloadException(
                    "canonical-object-field-order-mismatch");
            }
            return node.Fields[index++].Value;
        }
    }

    public static class Value
    {
        public static Field Field(
            string name,
            Node value)
        {
            return new Field(name, value);
        }

        public static Node String(string value)
        {
            return value == null
                ? Node.Null()
                : Node.ScalarValue(value);
        }

        public static Node RequiredString(string value)
        {
            return Node.ScalarValue(
                value ?? throw new ArgumentNullException(nameof(value)));
        }

        public static Node Int32(int value)
        {
            return Node.ScalarValue(
                value.ToString(CultureInfo.InvariantCulture));
        }

        public static Node Int64(long value)
        {
            return Node.ScalarValue(
                value.ToString(CultureInfo.InvariantCulture));
        }

        public static Node UInt64(ulong value)
        {
            return Node.ScalarValue(
                value.ToString(CultureInfo.InvariantCulture));
        }

        public static Node Boolean(bool value)
        {
            return Node.ScalarValue(value ? "1" : "0");
        }

        public static Node OptionalInt64(long? value)
        {
            return value.HasValue ? Int64(value.Value) : Node.Null();
        }

        public static string ReadRequiredString(Node node)
        {
            if (node == null || node.Kind != NodeKind.Scalar)
            {
                throw new PayloadException(
                    "canonical-scalar-expected");
            }
            return node.Scalar;
        }

        public static string ReadOptionalString(Node node)
        {
            if (node == null)
            {
                throw new PayloadException(
                    "canonical-node-null-reference");
            }
            if (node.Kind == NodeKind.Null)
            {
                return null;
            }
            return ReadRequiredString(node);
        }

        public static int ReadInt32(Node node)
        {
            int value;
            if (!int.TryParse(
                ReadRequiredString(node),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value))
            {
                throw new PayloadException(
                    "canonical-int32-invalid");
            }
            return value;
        }

        public static long ReadInt64(Node node)
        {
            long value;
            if (!long.TryParse(
                ReadRequiredString(node),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value))
            {
                throw new PayloadException(
                    "canonical-int64-invalid");
            }
            return value;
        }

        public static ulong ReadUInt64(Node node)
        {
            ulong value;
            if (!ulong.TryParse(
                ReadRequiredString(node),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out value))
            {
                throw new PayloadException(
                    "canonical-uint64-invalid");
            }
            return value;
        }

        public static long? ReadOptionalInt64(Node node)
        {
            return node.Kind == NodeKind.Null
                ? (long?)null
                : ReadInt64(node);
        }

        public static bool ReadBoolean(Node node)
        {
            string value = ReadRequiredString(node);
            if (value == "1") return true;
            if (value == "0") return false;
            throw new PayloadException(
                "canonical-boolean-invalid");
        }

        public static IReadOnlyList<Node> ReadList(
            Node node)
        {
            if (node == null || node.Kind != NodeKind.List)
            {
                throw new PayloadException(
                    "canonical-list-expected");
            }
            return node.Items;
        }
    }
}
