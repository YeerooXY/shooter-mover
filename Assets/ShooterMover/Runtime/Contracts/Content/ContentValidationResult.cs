using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Contracts.Content
{
    public enum ContentValidationMode
    {
        Release = 1,
        Prototype = 2
    }

    public enum ContentValidationErrorCode
    {
        DuplicateDefinition = 1,
        MissingDefinition = 2,
        WrongDefinitionKind = 3,
        UnsupportedDefinitionVersion = 4,
        CyclicDependency = 5,
        MissingProvenance = 6,
        TombstonedId = 7,
        PrototypeOnlyDefinition = 8
    }

    /// <summary>
    /// Immutable structured content-validation failure.
    /// </summary>
    public sealed class ContentValidationError : IEquatable<ContentValidationError>
    {
        private readonly ReadOnlyCollection<StableId> _cycle;

        private ContentValidationError(
            ContentValidationErrorCode code,
            StableId definitionId,
            StableId referencedId,
            ContentDefinitionKind? expectedKind,
            ContentDefinitionKind? actualKind,
            int? expectedVersion,
            int? actualVersion,
            string detail,
            IEnumerable<StableId> cycle)
        {
            Code = code;
            DefinitionId = definitionId;
            ReferencedId = referencedId;
            ExpectedKind = expectedKind;
            ActualKind = actualKind;
            ExpectedVersion = expectedVersion;
            ActualVersion = actualVersion;
            Detail = detail;
            _cycle = CopyCycle(cycle);
        }

        public ContentValidationErrorCode Code { get; }

        public StableId DefinitionId { get; }

        public StableId ReferencedId { get; }

        public ContentDefinitionKind? ExpectedKind { get; }

        public ContentDefinitionKind? ActualKind { get; }

        public int? ExpectedVersion { get; }

        public int? ActualVersion { get; }

        public string Detail { get; }

        public IReadOnlyList<StableId> Cycle => _cycle;

        internal static ContentValidationError Duplicate(StableId definitionId, int count)
        {
            return Create(
                ContentValidationErrorCode.DuplicateDefinition,
                definitionId,
                null,
                null,
                null,
                null,
                null,
                "count=" + count.ToString(CultureInfo.InvariantCulture));
        }

        internal static ContentValidationError Missing(
            StableId sourceId,
            ContentReference reference)
        {
            return Create(
                ContentValidationErrorCode.MissingDefinition,
                sourceId,
                reference.DefinitionId,
                reference.ExpectedKind,
                null,
                reference.ExpectedVersion,
                null,
                null);
        }

        internal static ContentValidationError WrongKind(
            StableId sourceId,
            ContentReference reference,
            ContentDefinitionDescriptor actual)
        {
            return Create(
                ContentValidationErrorCode.WrongDefinitionKind,
                sourceId,
                reference.DefinitionId,
                reference.ExpectedKind,
                actual.Kind,
                reference.ExpectedVersion,
                actual.DefinitionVersion,
                null);
        }

        internal static ContentValidationError UnsupportedVersion(
            StableId sourceId,
            StableId referencedId,
            ContentDefinitionKind? expectedKind,
            ContentDefinitionKind? actualKind,
            int? expectedVersion,
            int? actualVersion)
        {
            return Create(
                ContentValidationErrorCode.UnsupportedDefinitionVersion,
                sourceId,
                referencedId,
                expectedKind,
                actualKind,
                expectedVersion,
                actualVersion,
                null);
        }

        internal static ContentValidationError MissingProvenance(StableId definitionId)
        {
            return Create(
                ContentValidationErrorCode.MissingProvenance,
                definitionId,
                null,
                null,
                null,
                null,
                null,
                null);
        }

        internal static ContentValidationError Tombstoned(
            StableId sourceId,
            StableId referencedId)
        {
            return Create(
                ContentValidationErrorCode.TombstonedId,
                sourceId,
                referencedId,
                null,
                null,
                null,
                null,
                null);
        }

        internal static ContentValidationError PrototypeOnly(StableId definitionId)
        {
            return Create(
                ContentValidationErrorCode.PrototypeOnlyDefinition,
                definitionId,
                null,
                null,
                null,
                null,
                null,
                null);
        }

        internal static ContentValidationError Cyclic(IList<StableId> component)
        {
            if (component == null || component.Count == 0)
            {
                throw new ArgumentException(
                    "A cyclic component must contain at least one definition ID.",
                    nameof(component));
            }

            return new ContentValidationError(
                ContentValidationErrorCode.CyclicDependency,
                component[0],
                null,
                null,
                null,
                null,
                null,
                "component_size=" + component.Count.ToString(CultureInfo.InvariantCulture),
                component);
        }

        public string ToCanonicalString()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("code=")
                .Append(ContentValidationErrorCodeFormat.ToCanonicalName(Code))
                .Append("\ndefinition_id=")
                .Append(DefinitionId == null ? "null" : DefinitionId.ToString())
                .Append("\nreferenced_id=")
                .Append(ReferencedId == null ? "null" : ReferencedId.ToString())
                .Append("\nexpected_kind=")
                .Append(ExpectedKind.HasValue
                    ? ContentDefinitionKindFormat.ToCanonicalName(ExpectedKind.Value)
                    : "null")
                .Append("\nactual_kind=")
                .Append(ActualKind.HasValue
                    ? ContentDefinitionKindFormat.ToCanonicalName(ActualKind.Value)
                    : "null")
                .Append("\nexpected_version=")
                .Append(ExpectedVersion.HasValue
                    ? ExpectedVersion.Value.ToString(CultureInfo.InvariantCulture)
                    : "null")
                .Append("\nactual_version=")
                .Append(ActualVersion.HasValue
                    ? ActualVersion.Value.ToString(CultureInfo.InvariantCulture)
                    : "null")
                .Append("\ndetail=")
                .Append(Detail ?? "null")
                .Append("\ncycle=");

            if (_cycle.Count == 0)
            {
                builder.Append("null");
            }
            else
            {
                for (int index = 0; index < _cycle.Count; index++)
                {
                    if (index > 0)
                    {
                        builder.Append(',');
                    }

                    builder.Append(_cycle[index]);
                }
            }

            return builder.ToString();
        }

        public bool Equals(ContentValidationError other)
        {
            if (ReferenceEquals(other, null)
                || Code != other.Code
                || !Equals(DefinitionId, other.DefinitionId)
                || !Equals(ReferencedId, other.ReferencedId)
                || ExpectedKind != other.ExpectedKind
                || ActualKind != other.ActualKind
                || ExpectedVersion != other.ExpectedVersion
                || ActualVersion != other.ActualVersion
                || !string.Equals(Detail, other.Detail, StringComparison.Ordinal)
                || _cycle.Count != other._cycle.Count)
            {
                return false;
            }

            for (int index = 0; index < _cycle.Count; index++)
            {
                if (!_cycle[index].Equals(other._cycle[index]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ContentValidationError);
        }

        public override int GetHashCode()
        {
            return ContentReference.DeterministicHash(ToCanonicalString());
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }

        private static ContentValidationError Create(
            ContentValidationErrorCode code,
            StableId definitionId,
            StableId referencedId,
            ContentDefinitionKind? expectedKind,
            ContentDefinitionKind? actualKind,
            int? expectedVersion,
            int? actualVersion,
            string detail)
        {
            return new ContentValidationError(
                code,
                definitionId,
                referencedId,
                expectedKind,
                actualKind,
                expectedVersion,
                actualVersion,
                detail,
                Array.Empty<StableId>());
        }

        private static ReadOnlyCollection<StableId> CopyCycle(IEnumerable<StableId> cycle)
        {
            if (cycle == null)
            {
                throw new ArgumentNullException(nameof(cycle));
            }

            List<StableId> copy = new List<StableId>();
            foreach (StableId definitionId in cycle)
            {
                if (definitionId == null)
                {
                    throw new ArgumentException(
                        "Cycle IDs cannot contain null values.",
                        nameof(cycle));
                }

                copy.Add(definitionId);
            }

            return new ReadOnlyCollection<StableId>(copy);
        }
    }

    /// <summary>
    /// Immutable deterministic validation result and exact typed-reference resolver.
    /// </summary>
    public sealed class ContentValidationResult
    {
        private readonly ReadOnlyCollection<ContentDefinitionDescriptor> _descriptors;
        private readonly ReadOnlyCollection<ContentValidationError> _errors;
        private readonly HashSet<StableId> _tombstonedIds;

        private ContentValidationResult(
            ContentValidationMode mode,
            IList<ContentDefinitionDescriptor> descriptors,
            IEnumerable<StableId> tombstonedIds,
            IList<ContentValidationError> errors)
        {
            Mode = mode;
            _descriptors = new ReadOnlyCollection<ContentDefinitionDescriptor>(
                new List<ContentDefinitionDescriptor>(descriptors));
            _tombstonedIds = new HashSet<StableId>(tombstonedIds);

            List<ContentValidationError> orderedErrors =
                new List<ContentValidationError>(errors);
            orderedErrors.Sort(ContentValidationErrorComparer.Instance);
            _errors = new ReadOnlyCollection<ContentValidationError>(orderedErrors);
        }

        public ContentValidationMode Mode { get; }

        public bool IsValid => _errors.Count == 0;

        public IReadOnlyList<ContentValidationError> Errors => _errors;

        public static ContentValidationResult Validate(
            IEnumerable<ContentDefinitionDescriptor> descriptors,
            ContentValidationMode mode)
        {
            return Validate(descriptors, Array.Empty<StableId>(), mode);
        }

        public static ContentValidationResult Validate(
            IEnumerable<ContentDefinitionDescriptor> descriptors,
            IEnumerable<StableId> tombstonedIds,
            ContentValidationMode mode)
        {
            RequireKnownMode(mode);
            List<ContentDefinitionDescriptor> descriptorList = CopyDescriptors(descriptors);
            HashSet<StableId> tombstoneSet = CopyTombstones(tombstonedIds);
            Dictionary<StableId, List<ContentDefinitionDescriptor>> groups =
                GroupByDefinitionId(descriptorList);
            List<ContentValidationError> errors = new List<ContentValidationError>();

            foreach (KeyValuePair<StableId, List<ContentDefinitionDescriptor>> pair in groups)
            {
                if (pair.Value.Count > 1)
                {
                    errors.Add(ContentValidationError.Duplicate(pair.Key, pair.Value.Count));
                }
            }

            for (int descriptorIndex = 0; descriptorIndex < descriptorList.Count; descriptorIndex++)
            {
                ContentDefinitionDescriptor descriptor = descriptorList[descriptorIndex];

                if (descriptor.DefinitionVersion != ContentReference.SupportedDefinitionVersion)
                {
                    errors.Add(
                        ContentValidationError.UnsupportedVersion(
                            descriptor.DefinitionId,
                            null,
                            descriptor.Kind,
                            descriptor.Kind,
                            ContentReference.SupportedDefinitionVersion,
                            descriptor.DefinitionVersion));
                }

                if (descriptor.ProvenanceId == null)
                {
                    errors.Add(
                        ContentValidationError.MissingProvenance(descriptor.DefinitionId));
                }

                if (tombstoneSet.Contains(descriptor.DefinitionId))
                {
                    errors.Add(
                        ContentValidationError.Tombstoned(
                            descriptor.DefinitionId,
                            descriptor.DefinitionId));
                }

                if (mode == ContentValidationMode.Release && descriptor.IsPrototypeOnly)
                {
                    errors.Add(
                        ContentValidationError.PrototypeOnly(descriptor.DefinitionId));
                }

                ValidateReferences(descriptor, groups, tombstoneSet, errors);
            }

            ContentCycleFinder.AppendCycleErrors(
                descriptorList,
                groups,
                tombstoneSet,
                errors);

            return new ContentValidationResult(
                mode,
                descriptorList,
                tombstoneSet,
                errors);
        }

        /// <summary>
        /// Resolves only an exact, unique, supported and mode-eligible typed reference.
        /// </summary>
        public bool TryResolve(
            ContentReference reference,
            out ContentDefinitionDescriptor descriptor)
        {
            if (reference == null)
            {
                throw new ArgumentNullException(nameof(reference));
            }

            descriptor = null;
            if (reference.ExpectedVersion != ContentReference.SupportedDefinitionVersion
                || _tombstonedIds.Contains(reference.DefinitionId))
            {
                return false;
            }

            ContentDefinitionDescriptor match = null;
            for (int index = 0; index < _descriptors.Count; index++)
            {
                ContentDefinitionDescriptor candidate = _descriptors[index];
                if (!candidate.DefinitionId.Equals(reference.DefinitionId))
                {
                    continue;
                }

                if (match != null)
                {
                    return false;
                }

                match = candidate;
            }

            if (match == null
                || match.Kind != reference.ExpectedKind
                || match.DefinitionVersion != reference.ExpectedVersion
                || match.DefinitionVersion != ContentReference.SupportedDefinitionVersion
                || match.ProvenanceId == null
                || (Mode == ContentValidationMode.Release && match.IsPrototypeOnly))
            {
                return false;
            }

            descriptor = match;
            return true;
        }

        private static void ValidateReferences(
            ContentDefinitionDescriptor source,
            IDictionary<StableId, List<ContentDefinitionDescriptor>> groups,
            ISet<StableId> tombstonedIds,
            ICollection<ContentValidationError> errors)
        {
            for (int referenceIndex = 0;
                referenceIndex < source.References.Count;
                referenceIndex++)
            {
                ContentReference reference = source.References[referenceIndex];

                if (tombstonedIds.Contains(reference.DefinitionId))
                {
                    errors.Add(
                        ContentValidationError.Tombstoned(
                            source.DefinitionId,
                            reference.DefinitionId));
                    continue;
                }

                List<ContentDefinitionDescriptor> matches;
                if (!groups.TryGetValue(reference.DefinitionId, out matches))
                {
                    errors.Add(ContentValidationError.Missing(source.DefinitionId, reference));
                    continue;
                }

                if (matches.Count != 1)
                {
                    continue;
                }

                ContentDefinitionDescriptor actual = matches[0];
                if (actual.Kind != reference.ExpectedKind)
                {
                    errors.Add(
                        ContentValidationError.WrongKind(
                            source.DefinitionId,
                            reference,
                            actual));
                    continue;
                }

                if (reference.ExpectedVersion != ContentReference.SupportedDefinitionVersion
                    || actual.DefinitionVersion != reference.ExpectedVersion)
                {
                    errors.Add(
                        ContentValidationError.UnsupportedVersion(
                            source.DefinitionId,
                            reference.DefinitionId,
                            reference.ExpectedKind,
                            actual.Kind,
                            reference.ExpectedVersion,
                            actual.DefinitionVersion));
                }
            }
        }

        private static List<ContentDefinitionDescriptor> CopyDescriptors(
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
                        "Content descriptor collections cannot contain null values.",
                        nameof(descriptors));
                }

                copy.Add(descriptor);
            }

            copy.Sort();
            return copy;
        }

        private static HashSet<StableId> CopyTombstones(IEnumerable<StableId> tombstonedIds)
        {
            if (tombstonedIds == null)
            {
                throw new ArgumentNullException(nameof(tombstonedIds));
            }

            HashSet<StableId> copy = new HashSet<StableId>();
            foreach (StableId definitionId in tombstonedIds)
            {
                if (definitionId == null)
                {
                    throw new ArgumentException(
                        "Tombstone collections cannot contain null values.",
                        nameof(tombstonedIds));
                }

                copy.Add(definitionId);
            }

            return copy;
        }

        private static Dictionary<StableId, List<ContentDefinitionDescriptor>> GroupByDefinitionId(
            IEnumerable<ContentDefinitionDescriptor> descriptors)
        {
            Dictionary<StableId, List<ContentDefinitionDescriptor>> groups =
                new Dictionary<StableId, List<ContentDefinitionDescriptor>>();

            foreach (ContentDefinitionDescriptor descriptor in descriptors)
            {
                List<ContentDefinitionDescriptor> matches;
                if (!groups.TryGetValue(descriptor.DefinitionId, out matches))
                {
                    matches = new List<ContentDefinitionDescriptor>();
                    groups.Add(descriptor.DefinitionId, matches);
                }

                matches.Add(descriptor);
            }

            return groups;
        }

        private static void RequireKnownMode(ContentValidationMode mode)
        {
            if (mode != ContentValidationMode.Release
                && mode != ContentValidationMode.Prototype)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(mode),
                    mode,
                    "Unknown content validation mode.");
            }
        }
    }

    internal sealed class ContentValidationErrorComparer : IComparer<ContentValidationError>
    {
        public static readonly ContentValidationErrorComparer Instance =
            new ContentValidationErrorComparer();

        private ContentValidationErrorComparer()
        {
        }

        public int Compare(ContentValidationError left, ContentValidationError right)
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

            int comparison = left.Code.CompareTo(right.Code);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = CompareStableIds(left.DefinitionId, right.DefinitionId);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = CompareStableIds(left.ReferencedId, right.ReferencedId);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = CompareNullableKinds(left.ExpectedKind, right.ExpectedKind);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = CompareNullableKinds(left.ActualKind, right.ActualKind);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = Nullable.Compare(left.ExpectedVersion, right.ExpectedVersion);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = Nullable.Compare(left.ActualVersion, right.ActualVersion);
            if (comparison != 0)
            {
                return comparison;
            }

            comparison = CompareCycles(left.Cycle, right.Cycle);
            if (comparison != 0)
            {
                return comparison;
            }

            return string.CompareOrdinal(left.Detail, right.Detail);
        }

        private static int CompareStableIds(StableId left, StableId right)
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

        private static int CompareNullableKinds(
            ContentDefinitionKind? left,
            ContentDefinitionKind? right)
        {
            if (!left.HasValue)
            {
                return right.HasValue ? -1 : 0;
            }

            if (!right.HasValue)
            {
                return 1;
            }

            return string.CompareOrdinal(
                ContentDefinitionKindFormat.ToCanonicalName(left.Value),
                ContentDefinitionKindFormat.ToCanonicalName(right.Value));
        }

        private static int CompareCycles(
            IReadOnlyList<StableId> left,
            IReadOnlyList<StableId> right)
        {
            int count = Math.Min(left.Count, right.Count);
            for (int index = 0; index < count; index++)
            {
                int comparison = left[index].CompareTo(right[index]);
                if (comparison != 0)
                {
                    return comparison;
                }
            }

            return left.Count.CompareTo(right.Count);
        }
    }

    internal static class ContentValidationErrorCodeFormat
    {
        public static string ToCanonicalName(ContentValidationErrorCode code)
        {
            switch (code)
            {
                case ContentValidationErrorCode.DuplicateDefinition:
                    return "duplicate-definition";
                case ContentValidationErrorCode.MissingDefinition:
                    return "missing-definition";
                case ContentValidationErrorCode.WrongDefinitionKind:
                    return "wrong-definition-kind";
                case ContentValidationErrorCode.UnsupportedDefinitionVersion:
                    return "unsupported-definition-version";
                case ContentValidationErrorCode.CyclicDependency:
                    return "cyclic-dependency";
                case ContentValidationErrorCode.MissingProvenance:
                    return "missing-provenance";
                case ContentValidationErrorCode.TombstonedId:
                    return "tombstoned-id";
                case ContentValidationErrorCode.PrototypeOnlyDefinition:
                    return "prototype-only-definition";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(code),
                        code,
                        "Unknown content validation error code.");
            }
        }
    }

    internal sealed class ContentCycleFinder
    {
        private readonly Dictionary<StableId, ContentDefinitionDescriptor> _nodes;
        private readonly Dictionary<StableId, List<StableId>> _edges;
        private readonly Dictionary<StableId, int> _indices;
        private readonly Dictionary<StableId, int> _lowLinks;
        private readonly HashSet<StableId> _onStack;
        private readonly Stack<StableId> _stack;
        private readonly ICollection<ContentValidationError> _errors;
        private int _nextIndex;

        private ContentCycleFinder(
            Dictionary<StableId, ContentDefinitionDescriptor> nodes,
            Dictionary<StableId, List<StableId>> edges,
            ICollection<ContentValidationError> errors)
        {
            _nodes = nodes;
            _edges = edges;
            _errors = errors;
            _indices = new Dictionary<StableId, int>();
            _lowLinks = new Dictionary<StableId, int>();
            _onStack = new HashSet<StableId>();
            _stack = new Stack<StableId>();
        }

        public static void AppendCycleErrors(
            IEnumerable<ContentDefinitionDescriptor> descriptors,
            IDictionary<StableId, List<ContentDefinitionDescriptor>> groups,
            ISet<StableId> tombstonedIds,
            ICollection<ContentValidationError> errors)
        {
            Dictionary<StableId, ContentDefinitionDescriptor> nodes =
                new Dictionary<StableId, ContentDefinitionDescriptor>();

            foreach (ContentDefinitionDescriptor descriptor in descriptors)
            {
                List<ContentDefinitionDescriptor> matches = groups[descriptor.DefinitionId];
                if (matches.Count == 1
                    && descriptor.DefinitionVersion == ContentReference.SupportedDefinitionVersion
                    && !tombstonedIds.Contains(descriptor.DefinitionId))
                {
                    nodes.Add(descriptor.DefinitionId, descriptor);
                }
            }

            Dictionary<StableId, List<StableId>> edges =
                new Dictionary<StableId, List<StableId>>();
            foreach (KeyValuePair<StableId, ContentDefinitionDescriptor> pair in nodes)
            {
                List<StableId> targets = new List<StableId>();
                for (int referenceIndex = 0;
                    referenceIndex < pair.Value.References.Count;
                    referenceIndex++)
                {
                    ContentReference reference = pair.Value.References[referenceIndex];
                    ContentDefinitionDescriptor target;
                    if (!nodes.TryGetValue(reference.DefinitionId, out target)
                        || reference.ExpectedKind != target.Kind
                        || reference.ExpectedVersion != target.DefinitionVersion
                        || reference.ExpectedVersion != ContentReference.SupportedDefinitionVersion)
                    {
                        continue;
                    }

                    targets.Add(target.DefinitionId);
                }

                targets.Sort();
                edges.Add(pair.Key, targets);
            }

            ContentCycleFinder finder = new ContentCycleFinder(nodes, edges, errors);
            finder.Run();
        }

        private void Run()
        {
            List<StableId> orderedIds = new List<StableId>(_nodes.Keys);
            orderedIds.Sort();
            for (int index = 0; index < orderedIds.Count; index++)
            {
                if (!_indices.ContainsKey(orderedIds[index]))
                {
                    Visit(orderedIds[index]);
                }
            }
        }

        private void Visit(StableId definitionId)
        {
            _indices.Add(definitionId, _nextIndex);
            _lowLinks.Add(definitionId, _nextIndex);
            _nextIndex++;
            _stack.Push(definitionId);
            _onStack.Add(definitionId);

            List<StableId> targets = _edges[definitionId];
            for (int index = 0; index < targets.Count; index++)
            {
                StableId target = targets[index];
                if (!_indices.ContainsKey(target))
                {
                    Visit(target);
                    _lowLinks[definitionId] = Math.Min(
                        _lowLinks[definitionId],
                        _lowLinks[target]);
                }
                else if (_onStack.Contains(target))
                {
                    _lowLinks[definitionId] = Math.Min(
                        _lowLinks[definitionId],
                        _indices[target]);
                }
            }

            if (_lowLinks[definitionId] != _indices[definitionId])
            {
                return;
            }

            List<StableId> component = new List<StableId>();
            StableId member;
            do
            {
                member = _stack.Pop();
                _onStack.Remove(member);
                component.Add(member);
            }
            while (!member.Equals(definitionId));

            component.Sort();
            bool selfLoop = component.Count == 1
                && _edges[component[0]].Contains(component[0]);
            if (component.Count > 1 || selfLoop)
            {
                _errors.Add(ContentValidationError.Cyclic(component));
            }
        }
    }
}
