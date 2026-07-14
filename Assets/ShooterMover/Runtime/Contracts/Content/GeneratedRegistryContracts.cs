using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Contracts.Identity;

namespace ShooterMover.Contracts.Content
{
    /// <summary>
    /// Immutable count for one accepted Content Definitions v1 kind.
    /// </summary>
    public sealed class GeneratedRegistryKindCount : IEquatable<GeneratedRegistryKindCount>
    {
        internal GeneratedRegistryKindCount(ContentDefinitionKind kind, int count)
        {
            Kind = ContentDefinitionKindFormat.RequireKnown(kind, nameof(kind));
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "Registry counts cannot be negative.");
            }

            Count = count;
        }

        public ContentDefinitionKind Kind { get; }

        public int Count { get; }

        public bool Equals(GeneratedRegistryKindCount other)
        {
            return !ReferenceEquals(other, null) && Kind == other.Kind && Count == other.Count;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as GeneratedRegistryKindCount);
        }

        public override int GetHashCode()
        {
            return ContentReference.DeterministicHash(
                ContentDefinitionKindFormat.ToCanonicalName(Kind)
                + "="
                + Count.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Canonical, engine-independent machine registry document.
    /// This contract materializes already-supplied descriptors; it does not scan or generate content.
    /// </summary>
    public sealed class GeneratedMachineRegistry : IEquatable<GeneratedMachineRegistry>
    {
        public const int SupportedSchemaVersion = 1;
        public const string SchemaId = "urn:shooter-mover:schema:generated-registry:1";

        private readonly ReadOnlyCollection<ContentDefinitionDescriptor> _entries;

        private GeneratedMachineRegistry(
            ContentValidationMode validationMode,
            ContentVersion contentVersion,
            string registryFingerprint,
            IList<ContentDefinitionDescriptor> entries)
        {
            ValidationMode = GeneratedRegistryContractFormat.RequireKnownMode(validationMode);
            ContentVersion = contentVersion ?? throw new ArgumentNullException(nameof(contentVersion));
            RegistryFingerprint = GeneratedRegistryContractFormat.RequireFingerprint(
                registryFingerprint,
                nameof(registryFingerprint));
            _entries = new ReadOnlyCollection<ContentDefinitionDescriptor>(
                new List<ContentDefinitionDescriptor>(entries));
        }

        public int SchemaVersion => SupportedSchemaVersion;

        public ContentValidationMode ValidationMode { get; }

        public ContentVersion ContentVersion { get; }

        public string RegistryFingerprint { get; }

        public IReadOnlyList<ContentDefinitionDescriptor> Entries => _entries;

        public static GeneratedMachineRegistry Create(
            int catalogVersion,
            IEnumerable<ContentDefinitionDescriptor> descriptors,
            ContentValidationMode validationMode)
        {
            ContentValidationMode knownMode = GeneratedRegistryContractFormat.RequireKnownMode(validationMode);
            List<ContentDefinitionDescriptor> entries =
                GeneratedRegistryContractFormat.CopyAndOrderDescriptors(descriptors);
            ContentValidationResult validation = ContentValidationResult.Validate(entries, knownMode);
            if (!validation.IsValid)
            {
                throw new ArgumentException(
                    "A machine registry can be created only from a valid Content Definitions v1 catalog. "
                    + "First error: "
                    + validation.Errors[0].ToCanonicalString(),
                    nameof(descriptors));
            }

            string definitionFingerprint = GeneratedRegistryContractFormat.CalculateSha256(
                GeneratedRegistryContractFormat.BuildDefinitionFingerprintInput(entries));
            ContentVersion contentVersion = ContentVersion.Create(
                catalogVersion,
                definitionFingerprint);
            string registryFingerprint = GeneratedRegistryContractFormat.CalculateSha256(
                GeneratedRegistryContractFormat.BuildRegistryFingerprintInput(
                    knownMode,
                    contentVersion,
                    entries));

            return new GeneratedMachineRegistry(
                knownMode,
                contentVersion,
                registryFingerprint,
                entries);
        }

        public string ToCanonicalJson()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("{\n")
                .Append("  \"$schema\": ")
                .Append(GeneratedRegistryContractFormat.QuoteJson(SchemaId))
                .Append(",\n  \"schema_version\": ")
                .Append(SupportedSchemaVersion.ToString(CultureInfo.InvariantCulture))
                .Append(",\n  \"validation_mode\": ")
                .Append(GeneratedRegistryContractFormat.QuoteJson(
                    GeneratedRegistryContractFormat.ToCanonicalMode(ValidationMode)))
                .Append(",\n  \"catalog_version\": ")
                .Append(ContentVersion.CatalogVersion.ToString(CultureInfo.InvariantCulture))
                .Append(",\n  \"definition_fingerprint\": ")
                .Append(GeneratedRegistryContractFormat.QuoteJson(
                    ContentVersion.DefinitionFingerprint))
                .Append(",\n  \"registry_fingerprint\": ")
                .Append(GeneratedRegistryContractFormat.QuoteJson(RegistryFingerprint))
                .Append(",\n  \"entry_count\": ")
                .Append(_entries.Count.ToString(CultureInfo.InvariantCulture))
                .Append(",\n  \"entries\": [");

            if (_entries.Count > 0)
            {
                builder.Append('\n');
            }

            for (int index = 0; index < _entries.Count; index++)
            {
                AppendMachineEntry(builder, _entries[index], "    ");
                if (index + 1 < _entries.Count)
                {
                    builder.Append(',');
                }

                builder.Append('\n');
            }

            builder.Append("  ]\n}\n");
            return builder.ToString();
        }

        public byte[] GetCanonicalUtf8Bytes()
        {
            return GeneratedRegistryContractFormat.Utf8.GetBytes(ToCanonicalJson());
        }

        public bool Equals(GeneratedMachineRegistry other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(ToCanonicalJson(), other.ToCanonicalJson(), StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as GeneratedMachineRegistry);
        }

        public override int GetHashCode()
        {
            return ContentReference.DeterministicHash(ToCanonicalJson());
        }

        public override string ToString()
        {
            return ToCanonicalJson();
        }

        private static void AppendMachineEntry(
            StringBuilder builder,
            ContentDefinitionDescriptor descriptor,
            string indent)
        {
            builder.Append(indent)
                .Append("{\n")
                .Append(indent)
                .Append("  \"definition_kind\": ")
                .Append(GeneratedRegistryContractFormat.QuoteJson(
                    ContentDefinitionKindFormat.ToCanonicalName(descriptor.Kind)))
                .Append(",\n")
                .Append(indent)
                .Append("  \"definition_id\": ")
                .Append(GeneratedRegistryContractFormat.QuoteJson(descriptor.DefinitionId.ToString()))
                .Append(",\n")
                .Append(indent)
                .Append("  \"definition_version\": ")
                .Append(descriptor.DefinitionVersion.ToString(CultureInfo.InvariantCulture))
                .Append(",\n")
                .Append(indent)
                .Append("  \"provenance_id\": ")
                .Append(GeneratedRegistryContractFormat.QuoteJson(descriptor.ProvenanceId.ToString()))
                .Append(",\n")
                .Append(indent)
                .Append("  \"prototype_only\": ")
                .Append(descriptor.IsPrototypeOnly ? "true" : "false")
                .Append(",\n")
                .Append(indent)
                .Append("  \"reference_count\": ")
                .Append(descriptor.References.Count.ToString(CultureInfo.InvariantCulture))
                .Append(",\n")
                .Append(indent)
                .Append("  \"references\": [");

            if (descriptor.References.Count > 0)
            {
                builder.Append('\n');
            }

            for (int referenceIndex = 0;
                 referenceIndex < descriptor.References.Count;
                 referenceIndex++)
            {
                ContentReference reference = descriptor.References[referenceIndex];
                builder.Append(indent)
                    .Append("    {\n")
                    .Append(indent)
                    .Append("      \"definition_kind\": ")
                    .Append(GeneratedRegistryContractFormat.QuoteJson(
                        ContentDefinitionKindFormat.ToCanonicalName(reference.ExpectedKind)))
                    .Append(",\n")
                    .Append(indent)
                    .Append("      \"definition_id\": ")
                    .Append(GeneratedRegistryContractFormat.QuoteJson(
                        reference.DefinitionId.ToString()))
                    .Append(",\n")
                    .Append(indent)
                    .Append("      \"definition_version\": ")
                    .Append(reference.ExpectedVersion.ToString(CultureInfo.InvariantCulture))
                    .Append('\n')
                    .Append(indent)
                    .Append("    }");

                if (referenceIndex + 1 < descriptor.References.Count)
                {
                    builder.Append(',');
                }

                builder.Append('\n');
            }

            builder.Append(indent)
                .Append("  ]\n")
                .Append(indent)
                .Append('}');
        }
    }

    /// <summary>
    /// Deterministic, diff-friendly human review snapshot of one validated machine registry.
    /// </summary>
    public sealed class GeneratedRegistryReviewSnapshot : IEquatable<GeneratedRegistryReviewSnapshot>
    {
        public const int SupportedSchemaVersion = 1;
        public const string SchemaId = "urn:shooter-mover:schema:generated-registry-review:1";

        private readonly ReadOnlyCollection<GeneratedRegistryKindCount> _kindCounts;

        private GeneratedRegistryReviewSnapshot(
            GeneratedMachineRegistry registry,
            int prototypeOnlyCount,
            int referenceCount,
            IList<GeneratedRegistryKindCount> kindCounts,
            string snapshotFingerprint)
        {
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            PrototypeOnlyCount = prototypeOnlyCount;
            ReferenceCount = referenceCount;
            _kindCounts = new ReadOnlyCollection<GeneratedRegistryKindCount>(
                new List<GeneratedRegistryKindCount>(kindCounts));
            SnapshotFingerprint = GeneratedRegistryContractFormat.RequireFingerprint(
                snapshotFingerprint,
                nameof(snapshotFingerprint));
        }

        public int SchemaVersion => SupportedSchemaVersion;

        public GeneratedMachineRegistry Registry { get; }

        public int PrototypeOnlyCount { get; }

        public int ReferenceCount { get; }

        public IReadOnlyList<GeneratedRegistryKindCount> KindCounts => _kindCounts;

        public string SnapshotFingerprint { get; }

        public static GeneratedRegistryReviewSnapshot Create(GeneratedMachineRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            List<GeneratedRegistryKindCount> kindCounts = new List<GeneratedRegistryKindCount>();
            int prototypeOnlyCount = 0;
            int referenceCount = 0;
            ContentDefinitionKind[] kinds = GeneratedRegistryContractFormat.OrderedKinds;
            for (int kindIndex = 0; kindIndex < kinds.Length; kindIndex++)
            {
                int count = 0;
                for (int entryIndex = 0; entryIndex < registry.Entries.Count; entryIndex++)
                {
                    ContentDefinitionDescriptor descriptor = registry.Entries[entryIndex];
                    if (descriptor.Kind == kinds[kindIndex])
                    {
                        count++;
                    }

                    if (kindIndex == 0)
                    {
                        if (descriptor.IsPrototypeOnly)
                        {
                            prototypeOnlyCount++;
                        }

                        referenceCount += descriptor.References.Count;
                    }
                }

                kindCounts.Add(new GeneratedRegistryKindCount(kinds[kindIndex], count));
            }

            string snapshotFingerprint = GeneratedRegistryContractFormat.CalculateSha256(
                GeneratedRegistryContractFormat.BuildReviewFingerprintInput(
                    registry,
                    prototypeOnlyCount,
                    referenceCount,
                    kindCounts));

            return new GeneratedRegistryReviewSnapshot(
                registry,
                prototypeOnlyCount,
                referenceCount,
                kindCounts,
                snapshotFingerprint);
        }

        public string ToCanonicalJson()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("{\n")
                .Append("  \"$schema\": ")
                .Append(GeneratedRegistryContractFormat.QuoteJson(SchemaId))
                .Append(",\n  \"schema_version\": ")
                .Append(SupportedSchemaVersion.ToString(CultureInfo.InvariantCulture))
                .Append(",\n  \"machine_registry_schema_version\": ")
                .Append(Registry.SchemaVersion.ToString(CultureInfo.InvariantCulture))
                .Append(",\n  \"validation\": {\n")
                .Append("    \"mode\": ")
                .Append(GeneratedRegistryContractFormat.QuoteJson(
                    GeneratedRegistryContractFormat.ToCanonicalMode(Registry.ValidationMode)))
                .Append(",\n    \"is_valid\": true,\n")
                .Append("    \"error_count\": 0\n")
                .Append("  },\n")
                .Append("  \"catalog_version\": ")
                .Append(Registry.ContentVersion.CatalogVersion.ToString(CultureInfo.InvariantCulture))
                .Append(",\n  \"definition_fingerprint\": ")
                .Append(GeneratedRegistryContractFormat.QuoteJson(
                    Registry.ContentVersion.DefinitionFingerprint))
                .Append(",\n  \"registry_fingerprint\": ")
                .Append(GeneratedRegistryContractFormat.QuoteJson(Registry.RegistryFingerprint))
                .Append(",\n  \"snapshot_fingerprint\": ")
                .Append(GeneratedRegistryContractFormat.QuoteJson(SnapshotFingerprint))
                .Append(",\n  \"summary\": {\n")
                .Append("    \"entry_count\": ")
                .Append(Registry.Entries.Count.ToString(CultureInfo.InvariantCulture))
                .Append(",\n    \"prototype_only_count\": ")
                .Append(PrototypeOnlyCount.ToString(CultureInfo.InvariantCulture))
                .Append(",\n    \"reference_count\": ")
                .Append(ReferenceCount.ToString(CultureInfo.InvariantCulture))
                .Append(",\n    \"kind_counts\": [\n");

            for (int index = 0; index < _kindCounts.Count; index++)
            {
                GeneratedRegistryKindCount count = _kindCounts[index];
                builder.Append("      { \"definition_kind\": ")
                    .Append(GeneratedRegistryContractFormat.QuoteJson(
                        ContentDefinitionKindFormat.ToCanonicalName(count.Kind)))
                    .Append(", \"count\": ")
                    .Append(count.Count.ToString(CultureInfo.InvariantCulture))
                    .Append(" }");
                if (index + 1 < _kindCounts.Count)
                {
                    builder.Append(',');
                }

                builder.Append('\n');
            }

            builder.Append("    ]\n")
                .Append("  },\n")
                .Append("  \"entries\": [");

            if (Registry.Entries.Count > 0)
            {
                builder.Append('\n');
            }

            for (int index = 0; index < Registry.Entries.Count; index++)
            {
                ContentDefinitionDescriptor descriptor = Registry.Entries[index];
                builder.Append("    { \"definition_kind\": ")
                    .Append(GeneratedRegistryContractFormat.QuoteJson(
                        ContentDefinitionKindFormat.ToCanonicalName(descriptor.Kind)))
                    .Append(", \"definition_id\": ")
                    .Append(GeneratedRegistryContractFormat.QuoteJson(
                        descriptor.DefinitionId.ToString()))
                    .Append(", \"definition_version\": ")
                    .Append(descriptor.DefinitionVersion.ToString(CultureInfo.InvariantCulture))
                    .Append(", \"reference_count\": ")
                    .Append(descriptor.References.Count.ToString(CultureInfo.InvariantCulture))
                    .Append(" }");
                if (index + 1 < Registry.Entries.Count)
                {
                    builder.Append(',');
                }

                builder.Append('\n');
            }

            builder.Append("  ]\n}\n");
            return builder.ToString();
        }

        public byte[] GetCanonicalUtf8Bytes()
        {
            return GeneratedRegistryContractFormat.Utf8.GetBytes(ToCanonicalJson());
        }

        public bool Equals(GeneratedRegistryReviewSnapshot other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(ToCanonicalJson(), other.ToCanonicalJson(), StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as GeneratedRegistryReviewSnapshot);
        }

        public override int GetHashCode()
        {
            return ContentReference.DeterministicHash(ToCanonicalJson());
        }

        public override string ToString()
        {
            return ToCanonicalJson();
        }
    }

    internal static class GeneratedRegistryContractFormat
    {
        public static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        public static readonly ContentDefinitionKind[] OrderedKinds =
        {
            ContentDefinitionKind.Enemy,
            ContentDefinitionKind.Encounter,
            ContentDefinitionKind.Environment,
            ContentDefinitionKind.Room,
            ContentDefinitionKind.SharedModule,
            ContentDefinitionKind.Weapon
        };

        public static ContentValidationMode RequireKnownMode(ContentValidationMode mode)
        {
            if (mode != ContentValidationMode.Release
                && mode != ContentValidationMode.Prototype)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(mode),
                    mode,
                    "Generated Registry v1 requires a known Content Definitions v1 validation mode.");
            }

            return mode;
        }

        public static string ToCanonicalMode(ContentValidationMode mode)
        {
            switch (RequireKnownMode(mode))
            {
                case ContentValidationMode.Release:
                    return "release";
                case ContentValidationMode.Prototype:
                    return "prototype";
                default:
                    throw new InvalidOperationException("Unreachable validation mode.");
            }
        }

        public static List<ContentDefinitionDescriptor> CopyAndOrderDescriptors(
            IEnumerable<ContentDefinitionDescriptor> descriptors)
        {
            if (descriptors == null)
            {
                throw new ArgumentNullException(nameof(descriptors));
            }

            List<ContentDefinitionDescriptor> copy = new List<ContentDefinitionDescriptor>();
            foreach (ContentDefinitionDescriptor descriptor in descriptors)
            {
                if (descriptor == null)
                {
                    throw new ArgumentException(
                        "Generated registry inputs cannot contain null descriptors.",
                        nameof(descriptors));
                }

                copy.Add(descriptor);
            }

            copy.Sort();
            return copy;
        }

        public static string BuildDefinitionFingerprintInput(
            IList<ContentDefinitionDescriptor> entries)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("format=generated-registry-definition-set-v1\nentry_count=")
                .Append(entries.Count.ToString(CultureInfo.InvariantCulture));

            for (int index = 0; index < entries.Count; index++)
            {
                string descriptor = entries[index].ToCanonicalString();
                builder.Append("\nentry_")
                    .Append(index.ToString("D6", CultureInfo.InvariantCulture))
                    .Append("_utf8_length=")
                    .Append(Utf8.GetByteCount(descriptor).ToString(CultureInfo.InvariantCulture))
                    .Append("\nentry_")
                    .Append(index.ToString("D6", CultureInfo.InvariantCulture))
                    .Append('=')
                    .Append(descriptor);
            }

            return builder.ToString();
        }

        public static string BuildRegistryFingerprintInput(
            ContentValidationMode validationMode,
            ContentVersion contentVersion,
            IList<ContentDefinitionDescriptor> entries)
        {
            string definitionSet = BuildDefinitionFingerprintInput(entries);
            string contentVersionText = contentVersion.ToCanonicalString();
            return "format=generated-machine-registry-v1"
                + "\nschema_version="
                + GeneratedMachineRegistry.SupportedSchemaVersion.ToString(CultureInfo.InvariantCulture)
                + "\nvalidation_mode="
                + ToCanonicalMode(validationMode)
                + "\ncontent_version_utf8_length="
                + Utf8.GetByteCount(contentVersionText).ToString(CultureInfo.InvariantCulture)
                + "\ncontent_version="
                + contentVersionText
                + "\ndefinition_set_utf8_length="
                + Utf8.GetByteCount(definitionSet).ToString(CultureInfo.InvariantCulture)
                + "\ndefinition_set="
                + definitionSet;
        }

        public static string BuildReviewFingerprintInput(
            GeneratedMachineRegistry registry,
            int prototypeOnlyCount,
            int referenceCount,
            IList<GeneratedRegistryKindCount> kindCounts)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("format=generated-registry-review-v1")
                .Append("\nschema_version=")
                .Append(GeneratedRegistryReviewSnapshot.SupportedSchemaVersion.ToString(
                    CultureInfo.InvariantCulture))
                .Append("\nregistry_fingerprint=")
                .Append(registry.RegistryFingerprint)
                .Append("\nentry_count=")
                .Append(registry.Entries.Count.ToString(CultureInfo.InvariantCulture))
                .Append("\nprototype_only_count=")
                .Append(prototypeOnlyCount.ToString(CultureInfo.InvariantCulture))
                .Append("\nreference_count=")
                .Append(referenceCount.ToString(CultureInfo.InvariantCulture))
                .Append("\nvalidation_mode=")
                .Append(ToCanonicalMode(registry.ValidationMode))
                .Append("\nvalidation_is_valid=true\nvalidation_error_count=0");

            for (int index = 0; index < kindCounts.Count; index++)
            {
                builder.Append("\nkind_")
                    .Append(index.ToString("D2", CultureInfo.InvariantCulture))
                    .Append('=')
                    .Append(ContentDefinitionKindFormat.ToCanonicalName(kindCounts[index].Kind))
                    .Append('|')
                    .Append(kindCounts[index].Count.ToString(CultureInfo.InvariantCulture));
            }

            for (int index = 0; index < registry.Entries.Count; index++)
            {
                ContentDefinitionDescriptor descriptor = registry.Entries[index];
                builder.Append("\nentry_")
                    .Append(index.ToString("D6", CultureInfo.InvariantCulture))
                    .Append('=')
                    .Append(ContentDefinitionKindFormat.ToCanonicalName(descriptor.Kind))
                    .Append('|')
                    .Append(descriptor.DefinitionId)
                    .Append('|')
                    .Append(descriptor.DefinitionVersion.ToString(CultureInfo.InvariantCulture))
                    .Append('|')
                    .Append(descriptor.References.Count.ToString(CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        public static string CalculateSha256(string canonicalText)
        {
            byte[] bytes = Utf8.GetBytes(canonicalText);
            byte[] hash;
            using (SHA256 sha256 = SHA256.Create())
            {
                hash = sha256.ComputeHash(bytes);
            }

            StringBuilder builder = new StringBuilder("sha256:");
            for (int index = 0; index < hash.Length; index++)
            {
                builder.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        public static string RequireFingerprint(string value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (value.Length != 71 || !value.StartsWith("sha256:", StringComparison.Ordinal))
            {
                throw new FormatException(
                    parameterName + " must use canonical sha256:<64 lowercase hex characters> form.");
            }

            for (int index = 7; index < value.Length; index++)
            {
                char current = value[index];
                if (!((current >= '0' && current <= '9')
                    || (current >= 'a' && current <= 'f')))
                {
                    throw new FormatException(parameterName + " must contain lowercase hexadecimal text only.");
                }
            }

            return value;
        }

        public static string QuoteJson(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            StringBuilder builder = new StringBuilder(value.Length + 2);
            builder.Append('"');
            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                switch (current)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (current < 0x20)
                        {
                            builder.Append("\\u")
                                .Append(((int)current).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(current);
                        }

                        break;
                }
            }

            builder.Append('"');
            return builder.ToString();
        }
    }
}
