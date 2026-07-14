using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Contracts.Content
{
    /// <summary>
    /// Immutable registry-input description of one content definition.
    /// </summary>
    public sealed class ContentDefinitionDescriptor :
        IEquatable<ContentDefinitionDescriptor>,
        IComparable<ContentDefinitionDescriptor>,
        IComparable
    {
        private readonly ReadOnlyCollection<ContentReference> _references;

        private ContentDefinitionDescriptor(
            StableId definitionId,
            ContentDefinitionKind kind,
            int definitionVersion,
            StableId provenanceId,
            bool isPrototypeOnly,
            IEnumerable<ContentReference> references)
        {
            DefinitionId = definitionId ?? throw new ArgumentNullException(nameof(definitionId));
            Kind = ContentDefinitionKindFormat.RequireKnown(kind, nameof(kind));
            DefinitionVersion = ContentReference.RequirePositiveVersion(
                definitionVersion,
                nameof(definitionVersion));
            ProvenanceId = provenanceId;
            IsPrototypeOnly = isPrototypeOnly;
            _references = CopyAndOrderReferences(references);
        }

        public StableId DefinitionId { get; }

        public ContentDefinitionKind Kind { get; }

        public int DefinitionVersion { get; }

        /// <summary>
        /// Stable identity of the source/provenance record. Null is retained so validation can report it.
        /// </summary>
        public StableId ProvenanceId { get; }

        public bool IsPrototypeOnly { get; }

        public IReadOnlyList<ContentReference> References => _references;

        public static ContentDefinitionDescriptor Create(
            StableId definitionId,
            ContentDefinitionKind kind,
            int definitionVersion,
            StableId provenanceId,
            bool isPrototypeOnly,
            IEnumerable<ContentReference> references)
        {
            return new ContentDefinitionDescriptor(
                definitionId,
                kind,
                definitionVersion,
                provenanceId,
                isPrototypeOnly,
                references);
        }

        public static ContentDefinitionDescriptor Create(
            StableId definitionId,
            ContentDefinitionKind kind,
            int definitionVersion,
            StableId provenanceId,
            bool isPrototypeOnly,
            params ContentReference[] references)
        {
            return Create(
                definitionId,
                kind,
                definitionVersion,
                provenanceId,
                isPrototypeOnly,
                (IEnumerable<ContentReference>)references);
        }

        /// <summary>
        /// Emits deterministic descriptor text suitable as a future registry fingerprint input.
        /// It does not calculate ContentVersion or write a registry.
        /// </summary>
        public string ToCanonicalString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("definition_kind=")
                .Append(ContentDefinitionKindFormat.ToCanonicalName(Kind))
                .Append("\ndefinition_id=")
                .Append(DefinitionId)
                .Append("\ndefinition_version=")
                .Append(DefinitionVersion.ToString(CultureInfo.InvariantCulture))
                .Append("\nprovenance_id=")
                .Append(ProvenanceId == null ? "null" : ProvenanceId.ToString())
                .Append("\nprototype_only=")
                .Append(IsPrototypeOnly ? "true" : "false")
                .Append("\nreference_count=")
                .Append(_references.Count.ToString(CultureInfo.InvariantCulture));

            for (int index = 0; index < _references.Count; index++)
            {
                builder.Append("\nreference_")
                    .Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append('=')
                    .Append(_references[index].ToCanonicalToken());
            }

            return builder.ToString();
        }

        public bool Equals(ContentDefinitionDescriptor other)
        {
            if (ReferenceEquals(other, null)
                || !DefinitionId.Equals(other.DefinitionId)
                || Kind != other.Kind
                || DefinitionVersion != other.DefinitionVersion
                || !Equals(ProvenanceId, other.ProvenanceId)
                || IsPrototypeOnly != other.IsPrototypeOnly
                || _references.Count != other._references.Count)
            {
                return false;
            }

            for (int index = 0; index < _references.Count; index++)
            {
                if (!_references[index].Equals(other._references[index]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ContentDefinitionDescriptor);
        }

        public override int GetHashCode()
        {
            return ContentReference.DeterministicHash(ToCanonicalString());
        }

        public int CompareTo(ContentDefinitionDescriptor other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }

            int kindComparison = string.CompareOrdinal(
                ContentDefinitionKindFormat.ToCanonicalName(Kind),
                ContentDefinitionKindFormat.ToCanonicalName(other.Kind));
            if (kindComparison != 0)
            {
                return kindComparison;
            }

            int idComparison = DefinitionId.CompareTo(other.DefinitionId);
            if (idComparison != 0)
            {
                return idComparison;
            }

            return DefinitionVersion.CompareTo(other.DefinitionVersion);
        }

        int IComparable.CompareTo(object obj)
        {
            if (obj == null)
            {
                return 1;
            }

            ContentDefinitionDescriptor other = obj as ContentDefinitionDescriptor;
            if (other == null)
            {
                throw new ArgumentException(
                    $"Object must be of type {nameof(ContentDefinitionDescriptor)}.",
                    nameof(obj));
            }

            return CompareTo(other);
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }

        public static bool operator ==(
            ContentDefinitionDescriptor left,
            ContentDefinitionDescriptor right)
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

        public static bool operator !=(
            ContentDefinitionDescriptor left,
            ContentDefinitionDescriptor right)
        {
            return !(left == right);
        }

        private static ReadOnlyCollection<ContentReference> CopyAndOrderReferences(
            IEnumerable<ContentReference> references)
        {
            if (references == null)
            {
                throw new ArgumentNullException(nameof(references));
            }

            List<ContentReference> copy = new List<ContentReference>();
            HashSet<ContentReference> seen = new HashSet<ContentReference>();
            foreach (ContentReference reference in references)
            {
                if (reference == null)
                {
                    throw new ArgumentException(
                        "Content descriptor references cannot contain null values.",
                        nameof(references));
                }

                if (!seen.Add(reference))
                {
                    throw new ArgumentException(
                        "Content descriptor references cannot contain duplicate typed references.",
                        nameof(references));
                }

                copy.Add(reference);
            }

            copy.Sort();
            return new ReadOnlyCollection<ContentReference>(copy);
        }
    }
}
