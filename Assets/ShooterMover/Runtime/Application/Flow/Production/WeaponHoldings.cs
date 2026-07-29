using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Execution;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Application.Flow.Production
{
    /// <summary>
    /// Schema-V2 canonical weapon holdings. This component owns only exact weapon-instance
    /// state. Generic holdings remain the immutable reward/strongbox ledger and are not rewritten.
    /// </summary>
    public sealed class WeaponHoldingsSnapshot
    {
        public const int CurrentSchemaVersion = 2;

        private readonly ReadOnlyCollection<WeaponEquipmentInstance> instances;

        private WeaponHoldingsSnapshot(
            long sequence,
            IEnumerable<WeaponEquipmentInstance> values,
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
        public IReadOnlyList<WeaponEquipmentInstance> Instances
        {
            get { return instances; }
        }
        public string Fingerprint { get; }

        public static WeaponHoldingsSnapshot Empty()
        {
            return CreateCanonical(0L, Array.Empty<WeaponEquipmentInstance>());
        }

        public static WeaponHoldingsSnapshot CreateCanonical(
            long sequence,
            IEnumerable<WeaponEquipmentInstance> values)
        {
            var preliminary = new WeaponHoldingsSnapshot(
                sequence,
                values,
                string.Empty);
            return new WeaponHoldingsSnapshot(
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

        public WeaponEquipmentInstance Find(StableId instanceId)
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

        private static ReadOnlyCollection<WeaponEquipmentInstance> Canonicalize(
            IEnumerable<WeaponEquipmentInstance> source)
        {
            var copy = new List<WeaponEquipmentInstance>(
                source ?? throw new ArgumentNullException(nameof(source)));
            copy.Sort(delegate(
                WeaponEquipmentInstance left,
                WeaponEquipmentInstance right)
            {
                if (ReferenceEquals(left, null)) return -1;
                if (ReferenceEquals(right, null)) return 1;
                return left.InstanceId.CompareTo(right.InstanceId);
            });

            var seen = new HashSet<StableId>();
            for (int index = 0; index < copy.Count; index++)
            {
                WeaponEquipmentInstance instance = copy[index];
                if (instance == null)
                {
                    throw new ArgumentException(
                        "Weapon holdings cannot contain null instances.",
                        nameof(source));
                }
                if (!seen.Add(instance.InstanceId))
                {
                    throw new ArgumentException(
                        "Weapon holdings cannot contain duplicate instance IDs.",
                        nameof(source));
                }
            }
            return new ReadOnlyCollection<WeaponEquipmentInstance>(copy);
        }

        private static string ComputeFingerprint(
            long sequence,
            IReadOnlyList<WeaponEquipmentInstance> values)
        {
            var builder = new StringBuilder();
            Append(builder, "schema", CurrentSchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Append(builder, "sequence", sequence.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Append(builder, "count", values.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            for (int index = 0; index < values.Count; index++)
            {
                WeaponEquipmentInstance instance = values[index];
                Append(builder, "instance", instance.InstanceId.ToString());
                Append(
                    builder,
                    "weapon-definition",
                    instance.WeaponDefinitionId.Value);
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

    public sealed class WeaponHoldingsImportResult
    {
        public WeaponHoldingsImportResult(
            bool succeeded,
            string rejectionCode,
            WeaponHoldingsSnapshot snapshot)
        {
            Succeeded = succeeded;
            RejectionCode = rejectionCode ?? string.Empty;
            Snapshot = snapshot;
        }

        public bool Succeeded { get; }
        public string RejectionCode { get; }
        public WeaponHoldingsSnapshot Snapshot { get; }
    }

    /// <summary>
    /// Character-local canonical authority for exact owned weapons.
    /// Equip and unequip never mutate this authority. Runtime additions and destructive removals
    /// validate the canonical definition and current unsupported-state policy before committing.
    /// </summary>
    public sealed class WeaponHoldingsState
    {
        private WeaponHoldingsSnapshot snapshot;

        public WeaponHoldingsState(
            WeaponHoldingsSnapshot initial = null)
        {
            snapshot = WeaponHoldingsSnapshot.Empty();
            if (initial != null)
            {
                WeaponHoldingsImportResult imported = ImportSnapshot(initial);
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

        public WeaponHoldingsSnapshot ExportSnapshot()
        {
            return snapshot;
        }

        public WeaponEquipmentInstance Find(StableId instanceId)
        {
            return snapshot.Find(instanceId);
        }

        public bool Contains(StableId instanceId)
        {
            return Find(instanceId) != null;
        }

        public WeaponHoldingsImportResult ImportSnapshot(
            WeaponHoldingsSnapshot imported)
        {
            if (imported == null)
            {
                return Reject("weapon-holdings-v2-import-null");
            }
            if (imported.SchemaVersion
                    != WeaponHoldingsSnapshot.CurrentSchemaVersion
                || !imported.HasValidFingerprint())
            {
                return Reject("weapon-holdings-v2-import-invalid");
            }

            snapshot = WeaponHoldingsSnapshot.CreateCanonical(
                imported.Sequence,
                imported.Instances);
            return new WeaponHoldingsImportResult(
                true,
                string.Empty,
                snapshot);
        }

        public bool TryAdd(
            WeaponEquipmentInstance instance,
            out string rejectionCode)
        {
            rejectionCode = string.Empty;
            if (instance == null)
            {
                rejectionCode = "weapon-holdings-v2-instance-null";
                return false;
            }

            WeaponMark mark;
            bool definitionResolved = WeaponCatalogProvider.Current
                .TryGetMark(instance.WeaponDefinitionId.Value, out mark)
                && mark != null;
            WeaponOperationAvailability availability =
                WeaponSafetyPolicy.EvaluateRewardAcceptance(
                    instance,
                    definitionResolved);
            if (!availability.IsAvailable)
            {
                rejectionCode = availability.RejectionCode;
                return false;
            }

            WeaponEquipmentInstance existing = snapshot.Find(instance.InstanceId);
            if (existing != null)
            {
                if (SameInstance(existing, instance))
                {
                    return true;
                }
                rejectionCode = "weapon-holdings-v2-instance-conflict";
                return false;
            }

            var next = new List<WeaponEquipmentInstance>(snapshot.Instances)
            {
                instance
            };
            snapshot = WeaponHoldingsSnapshot.CreateCanonical(
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
                rejectionCode = "weapon-holdings-v2-instance-id-null";
                return false;
            }

            WeaponEquipmentInstance existing = snapshot.Find(instanceId);
            if (existing == null)
            {
                return true;
            }

            WeaponMark mark;
            if (!WeaponCatalogProvider.Current.TryGetMark(
                    existing.WeaponDefinitionId.Value,
                    out mark)
                || mark == null)
            {
                rejectionCode = "canonical-weapon-definition-unresolved";
                return false;
            }

            var next = new List<WeaponEquipmentInstance>();
            for (int index = 0; index < snapshot.Instances.Count; index++)
            {
                if (snapshot.Instances[index].InstanceId != instanceId)
                {
                    next.Add(snapshot.Instances[index]);
                }
            }
            snapshot = WeaponHoldingsSnapshot.CreateCanonical(
                checked(snapshot.Sequence + 1L),
                next);
            return true;
        }

        private WeaponHoldingsImportResult Reject(string code)
        {
            return new WeaponHoldingsImportResult(false, code, snapshot);
        }

        private static bool SameInstance(
            WeaponEquipmentInstance left,
            WeaponEquipmentInstance right)
        {
            if (left.InstanceId != right.InstanceId
                || !left.WeaponDefinitionId.Equals(right.WeaponDefinitionId)
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
    public static class WeaponHoldingsMigration
    {
        public static WeaponHoldingsSnapshot ConvertLegacy(
            PlayerHoldingsSnapshot legacy)
        {
            if (legacy == null)
            {
                throw new ArgumentNullException(nameof(legacy));
            }

            var migrated = new List<WeaponEquipmentInstance>();
            for (int index = 0;
                 index < legacy.UniqueHoldings.Count;
                 index++)
            {
                UniqueHoldingSnapshot holding = legacy.UniqueHoldings[index];
                WeaponEquipmentInstance converted;
                if (TryConvertHolding(holding, out converted))
                {
                    migrated.Add(converted);
                }
            }
            return WeaponHoldingsSnapshot.CreateCanonical(0L, migrated);
        }

        public static bool TryConvertEquipment(
            EquipmentInstance legacy,
            out WeaponEquipmentInstance converted)
        {
            converted = null;
            if (legacy == null)
            {
                return false;
            }

            EquipmentDefinition definition =
                WeaponCatalogProvider.EquipmentCatalog
                    .FindEquipmentDefinition(legacy.DefinitionId);
            if (definition == null
                || definition.CategoryId != EquipmentCategoryIds.Weapon
                || definition.RuntimeWeaponReferenceId == null)
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

            converted = WeaponEquipmentInstance.Create(
                legacy.InstanceId,
                WeaponDefinitionId.FromRuntimeReference(
                    definition.RuntimeWeaponReferenceId),
                augmentAssignments,
                Array.Empty<StableId>());
            return true;
        }

        private static bool TryConvertHolding(
            UniqueHoldingSnapshot holding,
            out WeaponEquipmentInstance converted)
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

    public sealed class WeaponHoldingsComponentCodec :
        ISaveComponentPayloadCodec<WeaponHoldingsSnapshot>
    {
        private const string Prefix = "weapon-holdings-v2:";
        private const int MaximumInstances = 4096;
        private const int MaximumAssignmentsPerInstance = 256;

        public string ContractId { get { return "weapon-holdings-explicit-v2"; } }

        public string Encode(WeaponHoldingsSnapshot snapshot)
        {
            SaveComponentValidationResult validation = Validate(snapshot);
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
                writer.Write(WeaponHoldingsSnapshot.CurrentSchemaVersion);
                writer.Write(snapshot.Sequence);
                writer.Write(snapshot.Instances.Count);
                for (int index = 0;
                     index < snapshot.Instances.Count;
                     index++)
                {
                    WeaponEquipmentInstance instance =
                        snapshot.Instances[index];
                    writer.Write(instance.InstanceId.ToString());
                    writer.Write(instance.WeaponDefinitionId.Value);
                    WriteAssignments(writer, instance.AugmentAssignments);
                    WriteAssignments(writer, instance.OverclockAssignments);
                }
                writer.Flush();
                return Prefix + Convert.ToBase64String(stream.ToArray());
            }
        }

        public bool TryDecode(
            string canonicalPayload,
            out WeaponHoldingsSnapshot snapshot,
            out string rejectionCode)
        {
            snapshot = null;
            rejectionCode = string.Empty;
            if (string.IsNullOrWhiteSpace(canonicalPayload)
                || !canonicalPayload.StartsWith(
                    Prefix,
                    StringComparison.Ordinal))
            {
                rejectionCode = "weapon-holdings-v2-payload-prefix-invalid";
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
                    if (schema != WeaponHoldingsSnapshot.CurrentSchemaVersion)
                    {
                        rejectionCode =
                            "weapon-holdings-v2-schema-unsupported";
                        return false;
                    }

                    long sequence = reader.ReadInt64();
                    int count = reader.ReadInt32();
                    if (sequence < 0L
                        || count < 0
                        || count > MaximumInstances)
                    {
                        rejectionCode =
                            "weapon-holdings-v2-header-invalid";
                        return false;
                    }

                    var instances = new List<WeaponEquipmentInstance>(count);
                    for (int index = 0; index < count; index++)
                    {
                        StableId instanceId = StableId.Parse(reader.ReadString());
                        var definitionId =
                            new WeaponDefinitionId(reader.ReadString());
                        IReadOnlyList<StableId> augments =
                            ReadAssignments(reader);
                        IReadOnlyList<StableId> overclocks =
                            ReadAssignments(reader);
                        instances.Add(WeaponEquipmentInstance.Create(
                            instanceId,
                            definitionId,
                            augments,
                            overclocks));
                    }
                    if (stream.Position != stream.Length)
                    {
                        rejectionCode =
                            "weapon-holdings-v2-payload-trailing-data";
                        return false;
                    }

                    snapshot = WeaponHoldingsSnapshot.CreateCanonical(
                        sequence,
                        instances);
                }

                SaveComponentValidationResult validation =
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
                rejectionCode = "weapon-holdings-v2-payload-invalid";
                return false;
            }
        }

        public SaveComponentValidationResult Validate(
            WeaponHoldingsSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return SaveComponentValidationResult.Reject(
                    "weapon-holdings-v2-snapshot-null");
            }
            if (snapshot.SchemaVersion
                    != WeaponHoldingsSnapshot.CurrentSchemaVersion
                || !snapshot.HasValidFingerprint()
                || snapshot.Instances.Count > MaximumInstances)
            {
                return SaveComponentValidationResult.Reject(
                    "weapon-holdings-v2-snapshot-invalid");
            }

            for (int index = 0; index < snapshot.Instances.Count; index++)
            {
                WeaponEquipmentInstance instance = snapshot.Instances[index];
                if (instance.AugmentAssignments.Count
                        > MaximumAssignmentsPerInstance
                    || instance.OverclockAssignments.Count
                        > MaximumAssignmentsPerInstance)
                {
                    return SaveComponentValidationResult.Reject(
                        "weapon-holdings-v2-assignment-count-invalid");
                }

                WeaponDefinitionData ignored;
                if (!WeaponCatalogProvider.WeaponCatalog
                    .TryGetDefinition(
                        instance.WeaponDefinitionId.Value,
                        out ignored))
                {
                    return SaveComponentValidationResult.Reject(
                        "weapon-holdings-v2-definition-unknown:"
                        + instance.WeaponDefinitionId.Value);
                }
            }
            return SaveComponentValidationResult.Accept();
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
                    "Weapon assignment count is outside the supported bound.");
            }

            var values = new List<StableId>(count);
            for (int index = 0; index < count; index++)
            {
                values.Add(StableId.Parse(reader.ReadString()));
            }
            return values;
        }
    }

    public static class WeaponHoldingsSaveComponent
    {
        private static readonly StableId ComponentId =
            StableId.Parse("save-component.weapon-holdings");
        private static readonly WeaponHoldingsComponentCodec codec =
            new WeaponHoldingsComponentCodec();

        public static WeaponHoldingsComponentCodec Codec
        {
            get { return codec; }
        }

        public static SaveComponentDefinition Definition()
        {
            return new SaveComponentDefinition(
                ComponentId,
                2,
                codec.ContractId,
                false,
                25);
        }

        public static ISaveComponentBridge CreateAdapter(
            WeaponHoldingsState authority)
        {
            if (authority == null)
            {
                throw new ArgumentNullException(nameof(authority));
            }
            return new StateSnapshotSaveComponentBridge<
                WeaponHoldingsSnapshot>(
                Definition(),
                codec,
                authority.ExportSnapshot,
                codec.Validate,
                snapshot =>
                {
                    WeaponHoldingsImportResult result =
                        authority.ImportSnapshot(snapshot);
                    return result.Succeeded
                        ? SaveComponentApplyResult.Applied()
                        : SaveComponentApplyResult.Rejected(
                            result.RejectionCode);
                });
        }

        public static bool TryRead(
            CharacterInstanceSnapshot character,
            out WeaponHoldingsSnapshot snapshot,
            out string rejectionCode)
        {
            snapshot = null;
            rejectionCode = string.Empty;
            if (character == null)
            {
                rejectionCode = "weapon-holdings-v2-character-null";
                return false;
            }

            SaveComponentSnapshot component;
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
                    "weapon-holdings-v2-component-version-unsupported";
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
    public sealed class WeaponInstanceLookup
    {
        private readonly WeaponHoldingsState holdings;

        public WeaponInstanceLookup(
            WeaponHoldingsState authority)
        {
            holdings = authority
                ?? throw new ArgumentNullException(nameof(authority));
        }

        public bool TryResolve(
            StableId instanceId,
            out WeaponEquipmentInstance instance)
        {
            instance = holdings.Find(instanceId);
            return instance != null;
        }
    }
}
