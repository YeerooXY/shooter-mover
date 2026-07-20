using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Persistence.Components
{
    /// <summary>
    /// Deterministic, engine-neutral object-graph codec for immutable snapshot DTOs.
    /// The expected CLR type is supplied by the typed adapter; payloads never name or
    /// activate arbitrary types. Public snapshot properties are ordered ordinally.
    /// </summary>
    public static class CanonicalSnapshotCodecV1
    {
        public static string Serialize<T>(T value)
        {
            return SerializeValue(value, typeof(T));
        }

        public static bool TryDeserialize<T>(
            string payload,
            out T value,
            out string rejectionCode)
            where T : class
        {
            value = null;
            if (payload == null)
            {
                rejectionCode = "canonical-payload-null";
                return false;
            }

            try
            {
                var parser = new Parser(payload);
                Node node = parser.ParseNode();
                if (!parser.AtEnd)
                {
                    rejectionCode = "canonical-payload-trailing-data";
                    return false;
                }

                object materialized = Materialize(node, typeof(T));
                value = materialized as T;
                if (value == null)
                {
                    rejectionCode = "canonical-payload-type-mismatch";
                    return false;
                }

                string rebuilt = Serialize(value);
                if (!string.Equals(rebuilt, payload, StringComparison.Ordinal))
                {
                    value = null;
                    rejectionCode = "canonical-payload-roundtrip-mismatch";
                    return false;
                }

                rejectionCode = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                value = null;
                rejectionCode = "canonical-payload-invalid:"
                    + exception.GetType().Name;
                return false;
            }
        }

        internal static bool TryBuildFactoryArguments(
            object source,
            ParameterInfo[] parameters,
            out object[] arguments)
        {
            arguments = new object[parameters.Length];
            Dictionary<string, PropertyInfo> properties = GetPropertyMap(
                source.GetType());
            for (int index = 0; index < parameters.Length; index++)
            {
                ParameterInfo parameter = parameters[index];
                PropertyInfo property = FindProperty(properties, parameter.Name);
                if (property == null)
                {
                    if (parameter.HasDefaultValue)
                    {
                        arguments[index] = parameter.DefaultValue;
                        continue;
                    }
                    arguments = null;
                    return false;
                }

                object propertyValue = property.GetValue(source, null);
                try
                {
                    arguments[index] = ConvertRuntimeValue(
                        propertyValue,
                        parameter.ParameterType);
                }
                catch
                {
                    arguments = null;
                    return false;
                }
            }
            return true;
        }

        private static string SerializeValue(object value, Type declaredType)
        {
            if (value == null)
            {
                return "N;";
            }

            Type runtimeType = value.GetType();
            if (IsScalar(runtimeType))
            {
                return Scalar(ToScalarText(value, runtimeType));
            }

            Type keyType;
            Type valueType;
            if (TryGetDictionaryTypes(runtimeType, out keyType, out valueType))
            {
                var entries = new List<KeyValuePair<string, string>>();
                foreach (object entry in (IEnumerable)value)
                {
                    Type entryType = entry.GetType();
                    object key = entryType.GetProperty("Key").GetValue(entry, null);
                    object item = entryType.GetProperty("Value").GetValue(entry, null);
                    string keyPayload = SerializeValue(key, keyType);
                    string valuePayload = SerializeValue(item, valueType);
                    entries.Add(new KeyValuePair<string, string>(
                        keyPayload,
                        valuePayload));
                }
                entries.Sort((left, right) =>
                    string.CompareOrdinal(left.Key, right.Key));
                var builder = new StringBuilder();
                builder.Append('M');
                builder.Append(entries.Count.ToString(CultureInfo.InvariantCulture));
                builder.Append(':');
                for (int index = 0; index < entries.Count; index++)
                {
                    builder.Append(entries[index].Key);
                    builder.Append(entries[index].Value);
                }
                return builder.ToString();
            }

            Type elementType;
            if (TryGetEnumerableElementType(runtimeType, out elementType))
            {
                var items = new List<string>();
                foreach (object item in (IEnumerable)value)
                {
                    items.Add(SerializeValue(item, elementType));
                }
                return "L"
                    + items.Count.ToString(CultureInfo.InvariantCulture)
                    + ":"
                    + string.Concat(items);
            }

            PropertyInfo[] properties = GetSerializableProperties(runtimeType);
            var objectBuilder = new StringBuilder();
            objectBuilder.Append('O');
            objectBuilder.Append(properties.Length.ToString(
                CultureInfo.InvariantCulture));
            objectBuilder.Append(':');
            for (int index = 0; index < properties.Length; index++)
            {
                PropertyInfo property = properties[index];
                objectBuilder.Append(Scalar(property.Name));
                objectBuilder.Append(SerializeValue(
                    property.GetValue(value, null),
                    property.PropertyType));
            }
            return objectBuilder.ToString();
        }

        private static object Materialize(Node node, Type expectedType)
        {
            Type nullable = Nullable.GetUnderlyingType(expectedType);
            if (nullable != null)
            {
                if (node.Kind == NodeKind.Null)
                {
                    return null;
                }
                return Materialize(node, nullable);
            }

            if (node.Kind == NodeKind.Null)
            {
                if (expectedType.IsValueType)
                {
                    throw new InvalidOperationException(
                        "Null cannot materialize a non-nullable value type.");
                }
                return null;
            }

            if (IsScalar(expectedType))
            {
                if (node.Kind != NodeKind.Value)
                {
                    throw new InvalidOperationException(
                        "Scalar node expected.");
                }
                return ParseScalar(node.Value, expectedType);
            }

            Type keyType;
            Type valueType;
            if (TryGetDictionaryTypes(expectedType, out keyType, out valueType))
            {
                if (node.Kind != NodeKind.Map)
                {
                    throw new InvalidOperationException(
                        "Dictionary node expected.");
                }
                return MaterializeDictionary(
                    node,
                    expectedType,
                    keyType,
                    valueType);
            }

            Type elementType;
            if (TryGetEnumerableElementType(expectedType, out elementType))
            {
                if (node.Kind == NodeKind.Map
                    && !IsKeyValuePair(elementType))
                {
                    return MaterializeEnumerableFromMapValues(
                        node,
                        expectedType,
                        elementType);
                }
                if (node.Kind != NodeKind.List)
                {
                    throw new InvalidOperationException(
                        "Collection node expected.");
                }
                return MaterializeEnumerable(node, expectedType, elementType);
            }

            if (node.Kind != NodeKind.Object)
            {
                throw new InvalidOperationException("Object node expected.");
            }
            if (expectedType == typeof(object)
                || expectedType.IsInterface
                || expectedType.IsAbstract)
            {
                throw new InvalidOperationException(
                    "Polymorphic snapshot nodes are not supported.");
            }

            ConstructorInfo[] constructors = expectedType.GetConstructors(
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic);
            Array.Sort(constructors, CompareConstructors);
            Exception last = null;
            for (int constructorIndex = 0;
                constructorIndex < constructors.Length;
                constructorIndex++)
            {
                ConstructorInfo constructor = constructors[constructorIndex];
                ParameterInfo[] parameters = constructor.GetParameters();
                object[] arguments = new object[parameters.Length];
                bool usable = true;
                for (int parameterIndex = 0;
                    parameterIndex < parameters.Length;
                    parameterIndex++)
                {
                    ParameterInfo parameter = parameters[parameterIndex];
                    Node parameterNode;
                    if (!TryFindNode(
                        node.Properties,
                        parameter.Name,
                        out parameterNode))
                    {
                        if (parameter.HasDefaultValue)
                        {
                            arguments[parameterIndex] = parameter.DefaultValue;
                            continue;
                        }
                        usable = false;
                        break;
                    }
                    try
                    {
                        arguments[parameterIndex] = Materialize(
                            parameterNode,
                            parameter.ParameterType);
                    }
                    catch (Exception exception)
                    {
                        last = exception;
                        usable = false;
                        break;
                    }
                }

                if (!usable)
                {
                    continue;
                }

                try
                {
                    return constructor.Invoke(arguments);
                }
                catch (TargetInvocationException exception)
                {
                    last = exception.InnerException ?? exception;
                }
                catch (Exception exception)
                {
                    last = exception;
                }
            }

            MethodInfo[] factories = expectedType.GetMethods(
                BindingFlags.Static | BindingFlags.Public);
            Array.Sort(factories, CompareFactories);
            for (int factoryIndex = 0;
                factoryIndex < factories.Length;
                factoryIndex++)
            {
                MethodInfo factory = factories[factoryIndex];
                if ((!string.Equals(factory.Name, "Create", StringComparison.Ordinal)
                    && !string.Equals(factory.Name, "CreateCanonical", StringComparison.Ordinal))
                    || !expectedType.IsAssignableFrom(factory.ReturnType))
                {
                    continue;
                }
                ParameterInfo[] parameters = factory.GetParameters();
                if (parameters.Any(parameter => parameter.ParameterType.IsByRef))
                {
                    continue;
                }
                object[] arguments = new object[parameters.Length];
                bool usable = true;
                for (int parameterIndex = 0;
                    parameterIndex < parameters.Length;
                    parameterIndex++)
                {
                    ParameterInfo parameter = parameters[parameterIndex];
                    Node parameterNode;
                    if (!TryFindNode(
                        node.Properties,
                        parameter.Name,
                        out parameterNode))
                    {
                        if (parameter.HasDefaultValue)
                        {
                            arguments[parameterIndex] = parameter.DefaultValue;
                            continue;
                        }
                        usable = false;
                        break;
                    }
                    try
                    {
                        arguments[parameterIndex] = Materialize(
                            parameterNode,
                            parameter.ParameterType);
                    }
                    catch (Exception exception)
                    {
                        last = exception;
                        usable = false;
                        break;
                    }
                }
                if (!usable)
                {
                    continue;
                }
                try
                {
                    return factory.Invoke(null, arguments);
                }
                catch (TargetInvocationException exception)
                {
                    last = exception.InnerException ?? exception;
                }
                catch (Exception exception)
                {
                    last = exception;
                }
            }

            throw new InvalidOperationException(
                "No immutable snapshot constructor or factory could be used for "
                + expectedType.FullName,
                last);
        }

        private static object MaterializeEnumerable(
            Node node,
            Type expectedType,
            Type elementType)
        {
            Type listType = typeof(List<>).MakeGenericType(elementType);
            IList list = (IList)Activator.CreateInstance(listType);
            for (int index = 0; index < node.Items.Count; index++)
            {
                list.Add(Materialize(node.Items[index], elementType));
            }
            return AdaptCollection(list, expectedType, elementType);
        }

        private static object MaterializeEnumerableFromMapValues(
            Node node,
            Type expectedType,
            Type elementType)
        {
            Type listType = typeof(List<>).MakeGenericType(elementType);
            IList list = (IList)Activator.CreateInstance(listType);
            for (int index = 0; index < node.MapEntries.Count; index++)
            {
                list.Add(Materialize(
                    node.MapEntries[index].Value,
                    elementType));
            }
            return AdaptCollection(list, expectedType, elementType);
        }

        private static object AdaptCollection(
            IList list,
            Type expectedType,
            Type elementType)
        {
            if (expectedType.IsArray)
            {
                Array array = Array.CreateInstance(elementType, list.Count);
                list.CopyTo(array, 0);
                return array;
            }
            if (expectedType.IsAssignableFrom(list.GetType()))
            {
                return list;
            }

            Type enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
            ConstructorInfo constructor = expectedType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { enumerableType },
                null);
            if (constructor != null)
            {
                return constructor.Invoke(new object[] { list });
            }

            throw new InvalidOperationException(
                "Collection type cannot be materialized: "
                + expectedType.FullName);
        }

        private static object MaterializeDictionary(
            Node node,
            Type expectedType,
            Type keyType,
            Type valueType)
        {
            Type dictionaryType = typeof(Dictionary<,>).MakeGenericType(
                keyType,
                valueType);
            IDictionary dictionary = (IDictionary)Activator.CreateInstance(
                dictionaryType);
            for (int index = 0; index < node.MapEntries.Count; index++)
            {
                object key = Materialize(node.MapEntries[index].Key, keyType);
                object value = Materialize(
                    node.MapEntries[index].Value,
                    valueType);
                dictionary.Add(key, value);
            }
            if (expectedType.IsAssignableFrom(dictionaryType))
            {
                return dictionary;
            }

            Type genericDictionary = typeof(IDictionary<,>).MakeGenericType(
                keyType,
                valueType);
            ConstructorInfo constructor = expectedType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { genericDictionary },
                null);
            if (constructor != null)
            {
                return constructor.Invoke(new object[] { dictionary });
            }

            throw new InvalidOperationException(
                "Dictionary type cannot be materialized: "
                + expectedType.FullName);
        }

        private static object ConvertRuntimeValue(object value, Type targetType)
        {
            if (value == null)
            {
                return null;
            }
            if (targetType.IsInstanceOfType(value))
            {
                return value;
            }
            if (targetType == typeof(StableId) && value is string)
            {
                return StableId.Parse((string)value);
            }

            string payload = SerializeValue(value, value.GetType());
            var parser = new Parser(payload);
            Node node = parser.ParseNode();
            return Materialize(node, targetType);
        }

        private static bool IsScalar(Type type)
        {
            Type actual = Nullable.GetUnderlyingType(type) ?? type;
            return actual.IsEnum
                || actual == typeof(string)
                || actual == typeof(char)
                || actual == typeof(bool)
                || actual == typeof(byte)
                || actual == typeof(sbyte)
                || actual == typeof(short)
                || actual == typeof(ushort)
                || actual == typeof(int)
                || actual == typeof(uint)
                || actual == typeof(long)
                || actual == typeof(ulong)
                || actual == typeof(float)
                || actual == typeof(double)
                || actual == typeof(decimal)
                || actual == typeof(Guid)
                || actual == typeof(DateTime)
                || actual == typeof(DateTimeOffset)
                || actual == typeof(TimeSpan)
                || actual == typeof(StableId);
        }

        private static string ToScalarText(object value, Type type)
        {
            if (type == typeof(string))
            {
                return (string)value;
            }
            if (type == typeof(char))
            {
                return ((char)value).ToString();
            }
            if (type == typeof(bool))
            {
                return (bool)value ? "1" : "0";
            }
            if (type == typeof(StableId))
            {
                return value.ToString();
            }
            if (type.IsEnum)
            {
                return Convert.ToInt64(value, CultureInfo.InvariantCulture)
                    .ToString(CultureInfo.InvariantCulture);
            }
            if (type == typeof(float))
            {
                return ((float)value).ToString("R", CultureInfo.InvariantCulture);
            }
            if (type == typeof(double))
            {
                return ((double)value).ToString("R", CultureInfo.InvariantCulture);
            }
            if (type == typeof(decimal))
            {
                return ((decimal)value).ToString(CultureInfo.InvariantCulture);
            }
            if (type == typeof(Guid))
            {
                return ((Guid)value).ToString("D");
            }
            if (type == typeof(DateTime))
            {
                return ((DateTime)value).ToString("O", CultureInfo.InvariantCulture);
            }
            if (type == typeof(DateTimeOffset))
            {
                return ((DateTimeOffset)value).ToString(
                    "O",
                    CultureInfo.InvariantCulture);
            }
            if (type == typeof(TimeSpan))
            {
                return ((TimeSpan)value).Ticks.ToString(
                    CultureInfo.InvariantCulture);
            }
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static object ParseScalar(string text, Type type)
        {
            if (type == typeof(string)) return text;
            if (type == typeof(char))
            {
                if (text.Length != 1) throw new FormatException("Invalid char.");
                return text[0];
            }
            if (type == typeof(bool))
            {
                if (text == "1") return true;
                if (text == "0") return false;
                throw new FormatException("Invalid bool.");
            }
            if (type == typeof(StableId)) return StableId.Parse(text);
            if (type.IsEnum)
            {
                long numeric = long.Parse(text, CultureInfo.InvariantCulture);
                return Enum.ToObject(type, numeric);
            }
            if (type == typeof(byte)) return byte.Parse(text, CultureInfo.InvariantCulture);
            if (type == typeof(sbyte)) return sbyte.Parse(text, CultureInfo.InvariantCulture);
            if (type == typeof(short)) return short.Parse(text, CultureInfo.InvariantCulture);
            if (type == typeof(ushort)) return ushort.Parse(text, CultureInfo.InvariantCulture);
            if (type == typeof(int)) return int.Parse(text, CultureInfo.InvariantCulture);
            if (type == typeof(uint)) return uint.Parse(text, CultureInfo.InvariantCulture);
            if (type == typeof(long)) return long.Parse(text, CultureInfo.InvariantCulture);
            if (type == typeof(ulong)) return ulong.Parse(text, CultureInfo.InvariantCulture);
            if (type == typeof(float)) return float.Parse(text, CultureInfo.InvariantCulture);
            if (type == typeof(double)) return double.Parse(text, CultureInfo.InvariantCulture);
            if (type == typeof(decimal)) return decimal.Parse(text, CultureInfo.InvariantCulture);
            if (type == typeof(Guid)) return Guid.ParseExact(text, "D");
            if (type == typeof(DateTime)) return DateTime.ParseExact(
                text,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);
            if (type == typeof(DateTimeOffset)) return DateTimeOffset.ParseExact(
                text,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None);
            if (type == typeof(TimeSpan)) return TimeSpan.FromTicks(
                long.Parse(text, CultureInfo.InvariantCulture));
            throw new NotSupportedException("Unsupported scalar type: " + type.FullName);
        }

        private static string Scalar(string value)
        {
            string safe = value ?? string.Empty;
            return "V"
                + safe.Length.ToString(CultureInfo.InvariantCulture)
                + ":"
                + safe;
        }

        private static PropertyInfo[] GetSerializableProperties(Type type)
        {
            return type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => property.CanRead
                    && property.GetIndexParameters().Length == 0)
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .ToArray();
        }

        private static Dictionary<string, PropertyInfo> GetPropertyMap(Type type)
        {
            var map = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);
            PropertyInfo[] properties = GetSerializableProperties(type);
            for (int index = 0; index < properties.Length; index++)
            {
                map[Normalize(properties[index].Name)] = properties[index];
            }
            return map;
        }

        private static PropertyInfo FindProperty(
            IDictionary<string, PropertyInfo> properties,
            string parameterName)
        {
            string normalized = Normalize(parameterName);
            PropertyInfo property;
            if (properties.TryGetValue(normalized, out property))
            {
                return property;
            }
            string alias = StripKnownPrefix(normalized);
            if (!string.Equals(alias, normalized, StringComparison.Ordinal)
                && properties.TryGetValue(alias, out property))
            {
                return property;
            }
            return null;
        }

        private static bool TryFindNode(
            IDictionary<string, Node> properties,
            string parameterName,
            out Node node)
        {
            string normalized = Normalize(parameterName);
            foreach (KeyValuePair<string, Node> pair in properties)
            {
                if (Normalize(pair.Key) == normalized)
                {
                    node = pair.Value;
                    return true;
                }
            }
            string alias = StripKnownPrefix(normalized);
            if (!string.Equals(alias, normalized, StringComparison.Ordinal))
            {
                foreach (KeyValuePair<string, Node> pair in properties)
                {
                    if (Normalize(pair.Key) == alias)
                    {
                        node = pair.Value;
                        return true;
                    }
                }
            }
            node = null;
            return false;
        }

        private static string StripKnownPrefix(string normalized)
        {
            string[] prefixes = { "ordered", "canonical" };
            for (int index = 0; index < prefixes.Length; index++)
            {
                if (normalized.StartsWith(prefixes[index], StringComparison.Ordinal)
                    && normalized.Length > prefixes[index].Length)
                {
                    return normalized.Substring(prefixes[index].Length);
                }
            }
            return normalized;
        }

        private static int CompareFactories(MethodInfo left, MethodInfo right)
        {
            int name = string.CompareOrdinal(left.Name, right.Name);
            return name != 0
                ? name
                : right.GetParameters().Length.CompareTo(
                    left.GetParameters().Length);
        }

        private static string Normalize(string value)
        {
            var builder = new StringBuilder();
            string source = value ?? string.Empty;
            for (int index = 0; index < source.Length; index++)
            {
                char character = source[index];
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                }
            }
            return builder.ToString();
        }

        private static int CompareConstructors(
            ConstructorInfo left,
            ConstructorInfo right)
        {
            int visibility = right.IsPublic.CompareTo(left.IsPublic);
            if (visibility != 0) return visibility;
            return right.GetParameters().Length.CompareTo(
                left.GetParameters().Length);
        }

        private static bool TryGetDictionaryTypes(
            Type type,
            out Type keyType,
            out Type valueType)
        {
            Type match = FindGenericInterface(type, typeof(IDictionary<,>))
                ?? FindGenericInterface(type, typeof(IReadOnlyDictionary<,>));
            if (match == null)
            {
                keyType = null;
                valueType = null;
                return false;
            }
            Type[] arguments = match.GetGenericArguments();
            keyType = arguments[0];
            valueType = arguments[1];
            return true;
        }

        private static bool TryGetEnumerableElementType(
            Type type,
            out Type elementType)
        {
            if (type == typeof(string))
            {
                elementType = null;
                return false;
            }
            if (type.IsArray)
            {
                elementType = type.GetElementType();
                return true;
            }
            Type match = FindGenericInterface(type, typeof(IEnumerable<>));
            if (match == null)
            {
                elementType = null;
                return false;
            }
            elementType = match.GetGenericArguments()[0];
            return true;
        }

        private static Type FindGenericInterface(Type type, Type definition)
        {
            if (type.IsGenericType
                && type.GetGenericTypeDefinition() == definition)
            {
                return type;
            }
            Type[] interfaces = type.GetInterfaces();
            for (int index = 0; index < interfaces.Length; index++)
            {
                Type candidate = interfaces[index];
                if (candidate.IsGenericType
                    && candidate.GetGenericTypeDefinition() == definition)
                {
                    return candidate;
                }
            }
            return null;
        }

        private static bool IsKeyValuePair(Type type)
        {
            return type.IsGenericType
                && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>);
        }

        private enum NodeKind
        {
            Null,
            Value,
            List,
            Map,
            Object,
        }

        private sealed class Node
        {
            public Node(NodeKind kind)
            {
                Kind = kind;
                Items = new List<Node>();
                MapEntries = new List<KeyValuePair<Node, Node>>();
                Properties = new Dictionary<string, Node>(StringComparer.Ordinal);
            }

            public NodeKind Kind { get; }

            public string Value { get; set; }

            public List<Node> Items { get; }

            public List<KeyValuePair<Node, Node>> MapEntries { get; }

            public Dictionary<string, Node> Properties { get; }
        }

        private sealed class Parser
        {
            private readonly string text;
            private int index;

            public Parser(string text)
            {
                this.text = text;
            }

            public bool AtEnd
            {
                get { return index == text.Length; }
            }

            public Node ParseNode()
            {
                char tag = ReadCharacter();
                if (tag == 'N')
                {
                    Require(';');
                    return new Node(NodeKind.Null);
                }
                if (tag == 'V')
                {
                    int length = ReadCount();
                    var value = new Node(NodeKind.Value);
                    value.Value = ReadText(length);
                    return value;
                }
                if (tag == 'L')
                {
                    int count = ReadCount();
                    var list = new Node(NodeKind.List);
                    for (int itemIndex = 0; itemIndex < count; itemIndex++)
                    {
                        list.Items.Add(ParseNode());
                    }
                    return list;
                }
                if (tag == 'M')
                {
                    int count = ReadCount();
                    var map = new Node(NodeKind.Map);
                    for (int entryIndex = 0; entryIndex < count; entryIndex++)
                    {
                        map.MapEntries.Add(new KeyValuePair<Node, Node>(
                            ParseNode(),
                            ParseNode()));
                    }
                    return map;
                }
                if (tag == 'O')
                {
                    int count = ReadCount();
                    var item = new Node(NodeKind.Object);
                    for (int propertyIndex = 0;
                        propertyIndex < count;
                        propertyIndex++)
                    {
                        Node name = ParseNode();
                        if (name.Kind != NodeKind.Value
                            || item.Properties.ContainsKey(name.Value))
                        {
                            throw new FormatException(
                                "Invalid object property name.");
                        }
                        item.Properties.Add(name.Value, ParseNode());
                    }
                    return item;
                }
                throw new FormatException("Unknown canonical node tag.");
            }

            private int ReadCount()
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
                    throw new FormatException("Invalid canonical count.");
                }
                int value = int.Parse(
                    text.Substring(start, index - start),
                    CultureInfo.InvariantCulture);
                index++;
                return value;
            }

            private string ReadText(int length)
            {
                if (length < 0 || index + length > text.Length)
                {
                    throw new FormatException("Invalid canonical text length.");
                }
                string value = text.Substring(index, length);
                index += length;
                return value;
            }

            private char ReadCharacter()
            {
                if (index >= text.Length)
                {
                    throw new FormatException("Unexpected end of canonical payload.");
                }
                return text[index++];
            }

            private void Require(char expected)
            {
                if (ReadCharacter() != expected)
                {
                    throw new FormatException("Canonical delimiter mismatch.");
                }
            }
        }
    }

    public static class CanonicalSnapshotIntegrityV1
    {
        public static SaveComponentValidationResultV1 Validate<TSnapshot>(
            TSnapshot snapshot)
            where TSnapshot : class
        {
            if (snapshot == null)
            {
                return SaveComponentValidationResultV1.Reject(
                    "snapshot-null");
            }

            Type type = snapshot.GetType();
            PropertyInfo schemaProperty = type.GetProperty(
                "SchemaVersion",
                BindingFlags.Instance | BindingFlags.Public);
            FieldInfo currentSchema = type.GetField(
                "CurrentSchemaVersion",
                BindingFlags.Static | BindingFlags.Public);
            if (schemaProperty != null
                && schemaProperty.PropertyType == typeof(int)
                && currentSchema != null
                && currentSchema.FieldType == typeof(int))
            {
                int actual = (int)schemaProperty.GetValue(snapshot, null);
                int expected = (int)currentSchema.GetValue(null);
                if (actual != expected)
                {
                    return SaveComponentValidationResultV1.Reject(
                        "snapshot-schema-unsupported");
                }
            }

            PropertyInfo fingerprintProperty = type.GetProperty(
                "Fingerprint",
                BindingFlags.Instance | BindingFlags.Public);
            if (fingerprintProperty == null
                || fingerprintProperty.PropertyType != typeof(string))
            {
                return SaveComponentValidationResultV1.Accept();
            }
            string fingerprint = (string)fingerprintProperty.GetValue(
                snapshot,
                null);
            if (string.IsNullOrWhiteSpace(fingerprint))
            {
                return SaveComponentValidationResultV1.Reject(
                    "snapshot-fingerprint-missing");
            }

            MethodInfo hasValid = type.GetMethod(
                "HasValidFingerprint",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);
            if (hasValid != null && hasValid.ReturnType == typeof(bool))
            {
                bool valid = (bool)hasValid.Invoke(snapshot, null);
                return valid
                    ? SaveComponentValidationResultV1.Accept()
                    : SaveComponentValidationResultV1.Reject(
                        "snapshot-fingerprint-mismatch");
            }

            MethodInfo[] methods = type.GetMethods(
                BindingFlags.Static | BindingFlags.Public);
            for (int index = 0; index < methods.Length; index++)
            {
                MethodInfo method = methods[index];
                if (!string.Equals(
                    method.Name,
                    "ComputeFingerprint",
                    StringComparison.Ordinal)
                    || method.ReturnType != typeof(string))
                {
                    continue;
                }

                object[] arguments;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 1
                    && parameters[0].ParameterType.IsInstanceOfType(snapshot))
                {
                    arguments = new object[] { snapshot };
                }
                else if (!CanonicalSnapshotCodecV1.TryBuildFactoryArguments(
                    snapshot,
                    parameters,
                    out arguments))
                {
                    continue;
                }

                string computed = (string)method.Invoke(null, arguments);
                return string.Equals(
                    computed,
                    fingerprint,
                    StringComparison.Ordinal)
                    ? SaveComponentValidationResultV1.Accept()
                    : SaveComponentValidationResultV1.Reject(
                        "snapshot-fingerprint-mismatch");
            }

            for (int index = 0; index < methods.Length; index++)
            {
                MethodInfo method = methods[index];
                if (!string.Equals(
                    method.Name,
                    "CreateCanonical",
                    StringComparison.Ordinal)
                    || !type.IsAssignableFrom(method.ReturnType))
                {
                    continue;
                }
                object[] arguments;
                if (!CanonicalSnapshotCodecV1.TryBuildFactoryArguments(
                    snapshot,
                    method.GetParameters(),
                    out arguments))
                {
                    continue;
                }
                object canonical = method.Invoke(null, arguments);
                string canonicalFingerprint = (string)fingerprintProperty.GetValue(
                    canonical,
                    null);
                return string.Equals(
                    canonicalFingerprint,
                    fingerprint,
                    StringComparison.Ordinal)
                    ? SaveComponentValidationResultV1.Accept()
                    : SaveComponentValidationResultV1.Reject(
                        "snapshot-fingerprint-mismatch");
            }

            // Immutable snapshot constructors that compute their own fingerprint are
            // protected by the codec's byte-identical reserialization check.
            return SaveComponentValidationResultV1.Accept();
        }
    }
}
