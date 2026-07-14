using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ShooterMover.Contracts.Identity;
using ShooterMover.Domain.Common;

namespace ShooterMover.Contracts.Diagnostics
{
    public enum SupportBundleItemKind
    {
        BuildIdentity = 1,
        DiagnosticEvents = 2,
        RunValidity = 3,
        EvidenceConfiguration = 4,
        PerformanceCapture = 5,
        CrashMarker = 6,
    }

    public enum SupportBundleItemDisposition
    {
        Included = 1,
        Redacted = 2,
        Omitted = 3,
    }

    /// <summary>
    /// One logical support-bundle item. It carries no filesystem path, endpoint,
    /// user identity, raw free text, or upload destination.
    /// </summary>
    public sealed class SupportBundleItem : IEquatable<SupportBundleItem>
    {
        private SupportBundleItem(
            StableId logicalItemId,
            SupportBundleItemKind itemKind,
            SupportBundleItemDisposition disposition,
            long byteLength,
            string contentFingerprint,
            DiagnosticRedactionReason? redactionReason)
        {
            LogicalItemId = DiagnosticsContractFormat.RequireNotNull(
                logicalItemId,
                nameof(logicalItemId));
            SupportBundleContractFormat.RequireKnownItemKind(itemKind);
            SupportBundleContractFormat.RequireKnownDisposition(disposition);

            if (disposition == SupportBundleItemDisposition.Included)
            {
                if (byteLength < 1L)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(byteLength),
                        byteLength,
                        "Included support-bundle items require a positive byte length.");
                }

                ContentFingerprint = DiagnosticsContractFormat.RequireSha256(
                    contentFingerprint,
                    nameof(contentFingerprint));

                if (redactionReason.HasValue)
                {
                    throw new ArgumentException(
                        "Included support-bundle items cannot carry a redaction reason.",
                        nameof(redactionReason));
                }
            }
            else
            {
                if (byteLength != 0L || contentFingerprint != null || !redactionReason.HasValue)
                {
                    throw new ArgumentException(
                        "Redacted or omitted items carry no content facts and require one reason.");
                }

                DiagnosticsContractFormat.RequireKnownRedactionReason(redactionReason.Value);
            }

            ItemKind = itemKind;
            Disposition = disposition;
            ByteLength = byteLength;
            RedactionReason = redactionReason;
        }

        public StableId LogicalItemId { get; }

        public SupportBundleItemKind ItemKind { get; }

        public SupportBundleItemDisposition Disposition { get; }

        public long ByteLength { get; }

        public string ContentFingerprint { get; }

        public DiagnosticRedactionReason? RedactionReason { get; }

        public static SupportBundleItem Included(
            StableId logicalItemId,
            SupportBundleItemKind itemKind,
            long byteLength,
            string contentFingerprint)
        {
            return new SupportBundleItem(
                logicalItemId,
                itemKind,
                SupportBundleItemDisposition.Included,
                byteLength,
                contentFingerprint,
                null);
        }

        public static SupportBundleItem Redacted(
            StableId logicalItemId,
            SupportBundleItemKind itemKind,
            DiagnosticRedactionReason reason)
        {
            return new SupportBundleItem(
                logicalItemId,
                itemKind,
                SupportBundleItemDisposition.Redacted,
                0L,
                null,
                reason);
        }

        public static SupportBundleItem Omitted(
            StableId logicalItemId,
            SupportBundleItemKind itemKind,
            DiagnosticRedactionReason reason)
        {
            return new SupportBundleItem(
                logicalItemId,
                itemKind,
                SupportBundleItemDisposition.Omitted,
                0L,
                null,
                reason);
        }

        public string ToCanonicalString()
        {
            return "logical_item_id="
                + LogicalItemId
                + "\nitem_kind="
                + SupportBundleContractFormat.ItemKindToken(ItemKind)
                + "\ndisposition="
                + SupportBundleContractFormat.DispositionToken(Disposition)
                + "\nbyte_length="
                + ByteLength.ToString(CultureInfo.InvariantCulture)
                + "\ncontent_fingerprint="
                + (ContentFingerprint ?? "null")
                + "\nredaction_reason="
                + (RedactionReason.HasValue
                    ? DiagnosticsContractFormat.RedactionReasonToken(RedactionReason.Value)
                    : "null");
        }

        public bool Equals(SupportBundleItem other)
        {
            return !ReferenceEquals(other, null)
                && LogicalItemId.Equals(other.LogicalItemId)
                && ItemKind == other.ItemKind
                && Disposition == other.Disposition
                && ByteLength == other.ByteLength
                && string.Equals(
                    ContentFingerprint,
                    other.ContentFingerprint,
                    StringComparison.Ordinal)
                && RedactionReason == other.RedactionReason;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as SupportBundleItem);
        }

        public override int GetHashCode()
        {
            return DiagnosticsContractFormat.DeterministicHash(ToCanonicalString());
        }
    }

    /// <summary>
    /// Immutable review/export manifest for a bounded local support bundle. This
    /// contract describes explicit private export contents but performs no I/O.
    /// </summary>
    public sealed class SupportBundleManifest : IEquatable<SupportBundleManifest>
    {
        public const int ManifestVersion = 1;

        private readonly SupportBundleItem[] _items;

        public SupportBundleManifest(
            DiagnosticsSchemaVersion diagnosticsSchemaVersion,
            BuildIdentity buildIdentity,
            StableId runId,
            RunTechnicalValidity technicalValidity,
            DiagnosticBounds bounds,
            IEnumerable<SupportBundleItem> items)
        {
            DiagnosticsSchemaVersion = DiagnosticsContractFormat.RequireNotNull(
                diagnosticsSchemaVersion,
                nameof(diagnosticsSchemaVersion));
            BuildIdentity = DiagnosticsContractFormat.RequireNotNull(
                buildIdentity,
                nameof(buildIdentity));
            RunId = DiagnosticsContractFormat.RequireNotNull(runId, nameof(runId));
            TechnicalValidity = DiagnosticsContractFormat.RequireNotNull(
                technicalValidity,
                nameof(technicalValidity));
            Bounds = DiagnosticsContractFormat.RequireNotNull(bounds, nameof(bounds));

            if (!runId.Equals(technicalValidity.RunId))
            {
                throw new ArgumentException(
                    "Support-bundle run ID must match technical validity.",
                    nameof(technicalValidity));
            }

            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            _items = items.ToArray();
            if (_items.Length == 0)
            {
                throw new ArgumentException(
                    "A support-bundle manifest must contain at least one logical item.",
                    nameof(items));
            }

            if (_items.Length > bounds.MaxSupportBundleItems)
            {
                throw new ArgumentException(
                    "Support-bundle item count exceeds its configured bound.",
                    nameof(items));
            }

            Array.Sort(
                _items,
                delegate(SupportBundleItem left, SupportBundleItem right)
                {
                    if (left == null || right == null)
                    {
                        return ReferenceEquals(left, right) ? 0 : (left == null ? -1 : 1);
                    }

                    int kindComparison = left.ItemKind.CompareTo(right.ItemKind);
                    return kindComparison != 0
                        ? kindComparison
                        : left.LogicalItemId.CompareTo(right.LogicalItemId);
                });

            long totalIncludedBytes = 0L;
            HashSet<StableId> logicalItemIds = new HashSet<StableId>();
            for (int index = 0; index < _items.Length; index++)
            {
                SupportBundleItem current = _items[index];
                if (current == null)
                {
                    throw new ArgumentException(
                        "Support-bundle manifests cannot contain null items.",
                        nameof(items));
                }

                if (!logicalItemIds.Add(current.LogicalItemId))
                {
                    throw new ArgumentException(
                        "Support-bundle logical item IDs must be unique.",
                        nameof(items));
                }

                if (current.Disposition == SupportBundleItemDisposition.Included)
                {
                    if (current.ByteLength > bounds.MaxSupportBundleBytes - totalIncludedBytes)
                    {
                        throw new ArgumentException(
                            "Support-bundle included bytes exceed the configured bound.",
                            nameof(items));
                    }

                    totalIncludedBytes += current.ByteLength;
                }
            }

            TotalIncludedBytes = totalIncludedBytes;
        }

        public DiagnosticsSchemaVersion DiagnosticsSchemaVersion { get; }

        public BuildIdentity BuildIdentity { get; }

        public StableId RunId { get; }

        public RunTechnicalValidity TechnicalValidity { get; }

        public DiagnosticBounds Bounds { get; }

        public long TotalIncludedBytes { get; }

        public IReadOnlyList<SupportBundleItem> Items
        {
            get { return Array.AsReadOnly(_items); }
        }

        public string ToCanonicalString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("support_bundle_manifest_version=");
            builder.Append(ManifestVersion.ToString(CultureInfo.InvariantCulture));
            builder.Append("\n");
            builder.Append(DiagnosticsSchemaVersion.ToCanonicalString());
            builder.Append("\nexport_scope=local-explicit-private");
            builder.Append("\ncontains_raw_personal_data=false");
            builder.Append("\nrun_id=");
            builder.Append(RunId);
            builder.Append("\nbuild_identity:\n");
            builder.Append(BuildIdentity.ToCanonicalString());
            builder.Append("\ntechnical_validity:\n");
            builder.Append(TechnicalValidity.ToCanonicalString());
            builder.Append("\nbounds:\n");
            builder.Append(Bounds.ToCanonicalString());
            builder.Append("\ntotal_included_bytes=");
            builder.Append(TotalIncludedBytes.ToString(CultureInfo.InvariantCulture));
            builder.Append("\nitem_count=");
            builder.Append(_items.Length.ToString(CultureInfo.InvariantCulture));

            for (int index = 0; index < _items.Length; index++)
            {
                builder.Append("\nitem[");
                builder.Append(index.ToString(CultureInfo.InvariantCulture));
                builder.Append("]:\n");
                builder.Append(_items[index].ToCanonicalString());
            }

            return builder.ToString();
        }

        public bool Equals(SupportBundleManifest other)
        {
            if (ReferenceEquals(other, null)
                || !DiagnosticsSchemaVersion.Equals(other.DiagnosticsSchemaVersion)
                || !BuildIdentity.Equals(other.BuildIdentity)
                || !RunId.Equals(other.RunId)
                || !TechnicalValidity.Equals(other.TechnicalValidity)
                || !Bounds.Equals(other.Bounds)
                || TotalIncludedBytes != other.TotalIncludedBytes
                || _items.Length != other._items.Length)
            {
                return false;
            }

            for (int index = 0; index < _items.Length; index++)
            {
                if (!_items[index].Equals(other._items[index]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as SupportBundleManifest);
        }

        public override int GetHashCode()
        {
            return DiagnosticsContractFormat.DeterministicHash(ToCanonicalString());
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }
    }

    internal static class SupportBundleContractFormat
    {
        public static void RequireKnownItemKind(SupportBundleItemKind value)
        {
            if (value < SupportBundleItemKind.BuildIdentity
                || value > SupportBundleItemKind.CrashMarker)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Unknown support-bundle item kind.");
            }
        }

        public static void RequireKnownDisposition(SupportBundleItemDisposition value)
        {
            if (value < SupportBundleItemDisposition.Included
                || value > SupportBundleItemDisposition.Omitted)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Unknown support-bundle item disposition.");
            }
        }

        public static string ItemKindToken(SupportBundleItemKind value)
        {
            switch (value)
            {
                case SupportBundleItemKind.BuildIdentity: return "build-identity";
                case SupportBundleItemKind.DiagnosticEvents: return "diagnostic-events";
                case SupportBundleItemKind.RunValidity: return "run-validity";
                case SupportBundleItemKind.EvidenceConfiguration: return "evidence-configuration";
                case SupportBundleItemKind.PerformanceCapture: return "performance-capture";
                case SupportBundleItemKind.CrashMarker: return "crash-marker";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        value,
                        "Unknown support-bundle item kind.");
            }
        }

        public static string DispositionToken(SupportBundleItemDisposition value)
        {
            switch (value)
            {
                case SupportBundleItemDisposition.Included: return "included";
                case SupportBundleItemDisposition.Redacted: return "redacted";
                case SupportBundleItemDisposition.Omitted: return "omitted";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        value,
                        "Unknown support-bundle item disposition.");
            }
        }
    }
}
