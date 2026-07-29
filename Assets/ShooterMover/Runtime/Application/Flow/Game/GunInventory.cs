using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Persistence.SaveParts;
using ShooterMover.Application.Guns.Catalog;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Execution;
using ShooterMover.Domain.Guns.Catalog;

namespace ShooterMover.Application.Flow.Game
{
    /// <summary>
    /// Schema-V2 canonical gun holdings. This component owns only exact gun-instance
    /// state. Generic holdings remain the immutable reward/strongbox ledger and are not rewritten.
    /// </summary>
    public sealed class GunInventorySnapshot
    {
        public const int CurrentSchemaVersion = 2;

        private readonly ReadOnlyCollection<GunItem> instances;

        private GunInventorySnapshot(
            long sequence,
            IEnumerable<GunItem> values,
            string fingerprint)
        {
            if (sequence < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            Sequence = sequence;
            instances = Canonicalize(values);
            Fingerprint = fingerprint ?? string.Empty;
        }

        public int SchemaVersion { get { return CurrentSchemaVersion; } }
        public long Sequence { get; }
        public IReadOnlyList<GunItem> Instances
        {
            get { return instances; }
        }
        public string Fingerprint { get; }

        public static GunInventorySnapshot Empty()
        {
            return CreateCanonical(0L, Array.Empty<GunItem>());
        }

        public static GunInventorySnapshot CreateCanonical(
            long sequence,
            IEnumerable<GunItem> values)
        {
            var preliminary = new GunInventorySnapshot(
                sequence,
                values,
                string.Empty);
            return new GunInventorySnapshot(
                sequence,
                preliminary.Instances,
                ComputeFingerprint(sequence, preliminary.Instances));
        }

        public bool HasValidFingerprint()
        {
            return string.Equals(
                Fingerprint,
                ComputeFingerprint(Sequence, instances),
                StringComparison.Ordinal);
        }

        public GunItem Find(StableId instanceId)
        {
            if (instanceId == null)
            {
                return null;
            }

            for (int index = 0; index < instances.Count; index++)
            {
                if (instances[index].InstanceId == instanceId)
                {
                    return instances[index];
                }
            }
            return null;
        }

        private static ReadOnlyCollection<GunItem> Canonicalize(
            IEnumerable<GunItem> source)
        {
            var copy = new List<GunItem>(
                source ?? throw new ArgumentNullException(nameof(source)));
            copy.Sort(delegate(
                GunItem left,
                GunItem right)
            {
                if (ReferenceEquals(left, null)) return -1;
                if (ReferenceEquals(right, null)) return 1;
                return left.InstanceId.CompareTo(right.InstanceId);
            });

            var seen = new HashSet<StableId>();
            for (int index = 0; index < copy.Count; index++)
            {
                GunItem instance = copy[index];
                if (instance == null)
                {
                    throw new ArgumentException(
                        "Gun holdings cannot contain null instances.",
                        nameof(source));
                }
                if (!seen.Add(instance.InstanceId))
                {
                    throw new ArgumentException(
                        "Gun holdings cannot contain duplicate instance IDs.",
                        nameof(source));
                }
            }
            return new ReadOnlyCollection<GunItem>(copy);
        }

        private static string ComputeFingerprint(
            long sequence,
            IReadOnlyList<GunItem> values)
        {
            var builder = new StringBuilder();
            Append(builder, "schema", CurrentSchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Append(builder, "sequence", sequence.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Append(builder, "count", values.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            for (int index = 0; index < values.Count; index++)
            {
                GunItem instance = values[index];
                Append(builder, "instance", instance.InstanceId.ToString());
                Append(
                    builder,
                    "gun-definition",
                    instance.GunDefinitionId.Value);
                Append(
                    builder,
                    "augment-count",
                    instance.AugmentAssignments.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
                for (int assignmentIndex = 0;
                     assignmentIndex < instance.AugmentAssignments.Count;
                     assignmentIndex++)
                {
                    Append(
                        builder,
                        "augment",
                        instance.AugmentAssignments[assignmentIndex].ToString());
                }
                Append(
                    builder,
                    "overclock-count",
                    instance.OverclockAssignments.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
                for (int assignmentIndex = 0;
                     assignmentIndex < instance.OverclockAssignments.Count;
                     assignmentIndex++)
                {
                    Append(
                        builder,
                        "overclock",
                        instance.OverclockAssignments[assignmentIndex].ToString());
                }
            }

            byte[] digest;
            using (SHA256 sha = SHA256.Create())
            {
                digest = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
            }
            var output = new StringBuilder("sha256:");
            for (int index = 0; index < digest.Length; index++)
            {
                output.Append(digest[index].ToString("x2"));
            }
            return output.ToString();
        }

        private static void Append(
            StringBuilder builder,
            string name,
            string value)
        {
            string safe = value ?? string.Empty;
            builder.Append(name)
                .Append(':')
                .Append(safe.Length)
                .Append(':')
                .Append(safe)
                .Append('\n');
        }
    }

    public sealed class GunInventoryImportResult
    {
        public GunInventoryImportResult(
            bool succeeded,
            string rejectionCode,
            GunInventorySnapshot snapshot)
        {
            Succeeded = succeeded;
            RejectionCode = rejectionCode ?? string.Empty;
            Snapshot = snapshot;
        }

        public bool Succeeded { get; }
        public string RejectionCode { get; }
        public GunInventorySnapshot Snapshot { get; }
    }

    /// <summary>
    /// Character-local canonical authority for exact owned guns.
    /// Equip and unequip never mutate this authority. Runtime additions and destructive removals
    /// validate the canonical definition and current unsupported-state policy before committing.
    /// </summary>
    public sealed class GunInventoryState
    {
        private GunInventorySnapshot snapshot;

        public GunInventoryState(
            GunInventorySnapshot initial = null)
        {
            snapshot = GunInventorySnapshot.Empty();
            if (initial != null)
            {
                GunInventoryImportResult imported = ImportSnapshot(initial);
                if (!imported.Succeeded)
                {
                    throw new ArgumentException(
                        imported.RejectionCode,
                        nameof(initial));
                }
            }
        }

        public long Sequence { get { return snapshot.Sequence; } }
        public int Count { get { return snapshot.Instances.Count; } }

        public GunInventorySnapshot ExportSnapshot()
        {
            return snapshot;
        }

        public GunItem Find(StableId instanceId)
        {
            return snapshot.Find(instanceId);
        }

        public bool Contains(StableId instanceId)
        {
            return Find(instanceId) != null;
        }

        public GunInventoryImportResult ImportSnapshot(
            GunInventorySnapshot imported)
        {
            if (imported == null)
            {
                return Reject("gun-holdings-v2-import-null");
            }
            if (imported.SchemaVersion
                    != GunInventorySnapshot.CurrentSchemaVersion
                || !imported.HasValidFingerprint())
            {
                return Reject("gun-holdings-v2-import-invalid");
            }

            snapshot = GunInventorySnapshot.CreateCanonical(
                imported.Sequence,
                imported.Instances);
            return new GunInventoryImportResult(
                true,
                string.Empty,
                snapshot);
        }

        public bool TryAdd(
            GunItem instance,
            out string rejectionCode)
        {
            rejectionCode = string.Empty;
            if (instance == null)
            {
                rejectionCode = "gun-holdings-v2-instance-null";
                return false;
            }

            GunMark mark;
            bool definitionResolved = GunCatalogProvider.Current
                .TryGetMark(instance.GunDefinitionId.Value, out mark)
                && mark != null;
            GunOperationAvailability availability =
                GunSafetyPolicy.EvaluateRewardAcceptance(
                    instance,
                    definitionResolved);
            if (!availability.IsAvailable)
            {
                rejectionCode = availability.RejectionCode;
                return false;
            }

            GunItem existing = snapshot.Find(instance.InstanceId);
            if (existing != null)
            {
                if (SameInstance(existing, instance))
                {
                    return true;
                }
                rejectionCode = "gun-holdings-v2-instance-conflict";
                return false;
            }

            var next = new List<GunItem>(snapshot.Instances)
            {
                instance
            };
            snapshot = GunInventorySnapshot.CreateCanonical(
                checked(snapshot.Sequence + 1L),
                next);
            return true;
        }

        public bool TryRemove(
            StableId instanceId,
            out string rejectionCode)
        {
            rejectionCode = string.Empty;
            if (instanceId == null)
            {
                rejectionCode = "gun-holdings-v2-instance-id-null";
                return false;
            }

            GunItem existing = snapshot.Find(instanceId);
            if (existing == null)
            {
                return true;
            }

            GunMark mark;
            if (!GunCatalogProvider.Current.TryGetMark(
                    existing.GunDefinitionId.Value,
                    out mark)
                || mark == null)
            {
                rejectionCode = "canonical-gun-definition-unresolved";
                return false;
            }

            var next = new List<GunItem>();
            for (int index = 0; index < snapshot.Instances.Count; index++)
            {
                if (snapshot.Instances[index].InstanceId != instanceId)
                {
                    next.Add(snapshot.Instances[index]);
                }
            }
            snapshot = GunInventorySnapshot.CreateCanonical(
                checked(snapshot.Sequence + 1L),
                next);
            return true;
        }

        private GunInventoryImportResult Reject(string code)
        {
            return new GunInventoryImportResult(false, code, snapshot);
        }

        private static bool SameInstance(
            GunItem left,
            GunItem right)
        {
            if (left.InstanceId != right.InstanceId
                || !left.GunDefinitionId.Equals(right.GunDefinitionId)
                || left.AugmentAssignments.Count
                    != right.AugmentAssignments.Count
                || left.OverclockAssignments.Count
                    != right.OverclockAssignments.Count)
            {
                return false;
            }

            for (int index = 0;
                 index < left.AugmentAssignments.Count;
                 index++)
            {
                if (left.AugmentAssignments[index]
                    != right.AugmentAssignments[index])
                {
                    return false;
                }
            }
            for (int index = 0;
                 index < left.OverclockAssignments.Count;
                 index++)
            {
                if (left.OverclockAssignments[index]
                    != right.OverclockAssignments[index])
                {
                    return false;
                }
            }
            return true;
        }
    }

    /// <summary>
    /// Deterministic schema-V1 dual-read conversion. It preserves every existing opaque
    /// equipment instance ID and never rewrites generic holdings transactions or provenance.
    /// </summary>
    public static class GunInventoryMigration
    {
        public static GunInventorySnapshot ConvertLegacy(
            PlayerHoldingsSnapshot legacy)
        {
            if (legacy == null)
            {
                throw new ArgumentNullException(nameof(legacy));
            }

            var migrated = new List<GunItem>();
            for (int index = 0;
                 index < legacy.UniqueHoldings.Count;
                 index++)
            {
                UniqueHoldingSnapshot holding = legacy.UniqueHoldings[index];
                GunItem converted;
                if (TryConvertHolding(holding, out converted))
                {
                    migrated.Add(converted);
                }
            }
            return GunInventorySnapshot.CreateCanonical(0L, migrated);
        }

        public static bool TryConvertEquipment(
            EquipmentInstance legacy,
            out GunItem converted)
        {
            converted = null;
            if (legacy == null)
            {
                return false;
            }

            EquipmentDefinition definition =
                GunCatalogProvider.EquipmentCatalog
                    .FindEquipmentDefinition(legacy.DefinitionId);
            if (definition == null
                || definition.CategoryId != EquipmentCategoryIds.Gun
                || definition.RuntimeGunReferenceId == null)
            {
                return false;
            }

            var augmentAssignments = new List<StableId>();
            for (int index = 0; index < legacy.Augments.Count; index++)
            {
                AugmentInstance augment = legacy.Augments[index];
                if (augment != null)
                {
                    augmentAssignments.Add(augment.InstanceId);
                }
            }

            converted = GunItem.Create(
                legacy.InstanceId,
                GunDefinitionId.FromRuntimeReference(
                    definition.RuntimeGunReferenceId),
                augmentAssignments,
                Array.Empty<StableId>());
            return true;
        }

        private static bool TryConvertHolding(
            UniqueHoldingSnapshot holding,
            out GunItem converted)
        {
            converted = null;
            return holding != null
                && holding.RewardKind == RewardGrantKind.EquipmentReference
                && holding.InstanceStableId != null
                && holding.EquipmentInstance != null
                && TryConvertEquipment(holding.EquipmentInstance, out converted)
                && converted.InstanceId == holding.InstanceStableId;
        }
    }

    public sealed class GunInventoryCodec :
        ISavePartFormat<GunInventorySnapshot>
    {
        private const string Prefix = "gun-holdings-v2:";
        private const int MaximumInstances = 4096;
        private const int MaximumAssignmentsPerInstance = 256;

        public string ContractId { get { return "gun-holdings-explicit-v2"; } }

        public string Encode(GunInventorySnapshot snapshot)
        {
            SavePartValidationResult validation = Validate(snapshot);
            if (!validation.Succeeded)
            {
                throw new ArgumentException(
                    validation.RejectionCode,
                    nameof(snapshot));
            }

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(
                stream,
                Encoding.UTF8,
                true))
            {
                writer.Write(GunInventorySnapshot.CurrentSchemaVersion);
                writer.Write(snapshot.Sequence);
                writer.Write(snapshot.Instances.Count);
                for (int index = 0;
                     index < snapshot.Instances.Count;
                     index++)
                {
                    GunItem instance =
                        snapshot.Instances[index];
                    writer.Write(instance.InstanceId.ToString());
                    writer.Write(instance.GunDefinitionId.Value);
                    WriteAssignments(writer, instance.AugmentAssignments);
                    WriteAssignments(writer, instance.OverclockAssignments);
                }
                writer.Flush();
                return Prefix + Convert.ToBase64String(stream.ToArray());
            }
        }

        public bool TryDecode(
            string canonicalPayload,
            out GunInventorySnapshot snapshot,
            out string rejectionCode)
        {
            snapshot = null;
            rejectionCode = string.Empty;
            if (string.IsNullOrWhiteSpace(canonicalPayload)
                || !canonicalPayload.StartsWith(
                    Prefix,
                    StringComparison.Ordinal))
            {
                rejectionCode = "gun-holdings-v2-payload-prefix-invalid";
                return false;
            }

            try
            {
                byte[] bytes = Convert.FromBase64String(
                    canonicalPayload.Substring(Prefix.Length));
                using (var stream = new MemoryStream(bytes, false))
                using (var reader = new BinaryReader(
                    stream,
                    Encoding.UTF8,
                    true))
                {
                    int schema = reader.ReadInt32();
                    if (schema != GunInventorySnapshot.CurrentSchemaVersion)
                    {
                        rejectionCode =
                            "gun-holdings-v2-schema-unsupported";
                        return false;
                    }

                    long sequence = reader.ReadInt64();
                    int count = reader.ReadInt32();
                    if (sequence < 0L
                        || count < 0
                        || count > MaximumInstances)
                    {
                        rejectionCode =
                            "gun-holdings-v2-header-invalid";
                        return false;
                    }

                    var instances = new List<GunItem>(count);
                    for (int index = 0; index < count; index++)
                    {
                        StableId instanceId = StableId.Parse(reader.ReadString());
                        var definitionId =
                            new GunDefinitionId(reader.ReadString());
                        IReadOnlyList<StableId> augments =
                            ReadAssignments(reader);
                        IReadOnlyList<StableId> overclocks =
                            ReadAssignments(reader);
                        instances.Add(GunItem.Create(
                            instanceId,
                            definitionId,
                            augments,
                            overclocks));
                    }
                    if (stream.Position != stream.Length)
                    {
                        rejectionCode =
                            "gun-holdings-v2-payload-trailing-data";
                        return false;
                    }

                    snapshot = GunInventorySnapshot.CreateCanonical(
                        sequence,
                        instances);
                }

                SavePartValidationResult validation =
                    Validate(snapshot);
                if (!validation.Succeeded)
                {
                    snapshot = null;
                    rejectionCode = validation.RejectionCode;
                    return false;
                }
                return true;
            }
            catch
            {
                snapshot = null;
                rejectionCode = "gun-holdings-v2-payload-invalid";
                return false;
            }
        }

        public SavePartValidationResult Validate(
            GunInventorySnapshot snapshot)
        {
            if (snapshot == null)
            {
                return SavePartValidationResult.Reject(
                    "gun-holdings-v2-snapshot-null");
            }
            if (snapshot.SchemaVersion
                    != GunInventorySnapshot.CurrentSchemaVersion
                || !snapshot.HasValidFingerprint()
                || snapshot.Instances.Count > MaximumInstances)
            {
                return SavePartValidationResult.Reject(
                    "gun-holdings-v2-snapshot-invalid");
            }

            for (int index = 0; index < snapshot.Instances.Count; index++)
            {
                GunItem instance = snapshot.Instances[index];
                if (instance.AugmentAssignments.Count
                        > MaximumAssignmentsPerInstance
                    || instance.OverclockAssignments.Count
                        > MaximumAssignmentsPerInstance)
                {
                    return SavePartValidationResult.Reject(
                        "gun-holdings-v2-assignment-count-invalid");
                }

                GunDefinitionData ignored;
                if (!GunCatalogProvider.GunCatalog
                    .TryGetDefinition(
                        instance.GunDefinitionId.Value,
                        out ignored))
                {
                    return SavePartValidationResult.Reject(
                        "gun-holdings-v2-definition-unknown:"
                        + instance.GunDefinitionId.Value);
                }
            }
            return SavePartValidationResult.Accept();
        }

        private static void WriteAssignments(
            BinaryWriter writer,
            IReadOnlyList<StableId> assignments)
        {
            writer.Write(assignments.Count);
            for (int index = 0; index < assignments.Count; index++)
            {
                writer.Write(assignments[index].ToString());
            }
        }

        private static IReadOnlyList<StableId> ReadAssignments(
            BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > MaximumAssignmentsPerInstance)
            {
                throw new InvalidDataException(
                    "Gun assignment count is outside the supported bound.");
            }

            var values = new List<StableId>(count);
            for (int index = 0; index < count; index++)
            {
                values.Add(StableId.Parse(reader.ReadString()));
            }
            return values;
        }
    }

    public static class GunInventorySavePart
    {
        private static readonly StableId ComponentId =
            StableId.Parse("save-part.gun-holdings");
        private static readonly GunInventoryCodec codec =
            new GunInventoryCodec();

        public static GunInventoryCodec Codec
        {
            get { return codec; }
        }

        public static SavePartDefinition Definition()
        {
            return new SavePartDefinition(
                ComponentId,
                2,
                codec.ContractId,
                false,
                25);
        }

        public static ISavePart CreateAdapter(
            GunInventoryState authority)
        {
            if (authority == null)
            {
                throw new ArgumentNullException(nameof(authority));
            }
            return new SnapshotSavePart<
                GunInventorySnapshot>(
                Definition(),
                codec,
                authority.ExportSnapshot,
                codec.Validate,
                snapshot =>
                {
                    GunInventoryImportResult result =
                        authority.ImportSnapshot(snapshot);
                    return result.Succeeded
                        ? SavePartApplyResult.Applied()
                        : SavePartApplyResult.Rejected(
                            result.RejectionCode);
                });
        }

        public static bool TryRead(
            CharacterInstanceSnapshot character,
            out GunInventorySnapshot snapshot,
            out string rejectionCode)
        {
            snapshot = null;
            rejectionCode = string.Empty;
            if (character == null)
            {
                rejectionCode = "gun-holdings-v2-character-null";
                return false;
            }

            SavePartSnapshot component;
            if (!character.TryGetComponent(ComponentId, out component))
            {
                return false;
            }
            if (component.SchemaVersion != 2
                || !string.Equals(
                    component.ContentVersion,
                    codec.ContractId,
                    StringComparison.Ordinal))
            {
                rejectionCode =
                    "gun-holdings-v2-component-version-unsupported";
                return false;
            }
            return codec.TryDecode(
                component.CanonicalPayload,
                out snapshot,
                out rejectionCode);
        }
    }

    /// <summary>
    /// Read-only exact-instance gameplay lookup. It never fabricates a scene-local fallback.
    /// </summary>
    public sealed class GunInstanceLookup
    {
        private readonly GunInventoryState holdings;

        public GunInstanceLookup(
            GunInventoryState authority)
        {
            holdings = authority
                ?? throw new ArgumentNullException(nameof(authority));
        }

        public bool TryResolve(
            StableId instanceId,
            out GunItem instance)
        {
            instance = holdings.Find(instanceId);
            return instance != null;
        }
    }
}
