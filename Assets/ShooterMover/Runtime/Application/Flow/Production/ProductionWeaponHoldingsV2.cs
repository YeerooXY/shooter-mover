using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Contracts.Economy;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Application.Flow.Production
{
    /// <summary>
    /// Schema-V2 canonical weapon holdings. This component owns only exact weapon-instance
    /// state. Generic holdings remain the immutable reward/strongbox ledger and are not rewritten.
    /// </summary>
    public sealed class WeaponHoldingsSnapshotV2
    {
        public const int CurrentSchemaVersion = 2;

        private readonly ReadOnlyCollection<WeaponEquipmentInstance> instances;

        private WeaponHoldingsSnapshotV2(
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

        public static WeaponHoldingsSnapshotV2 Empty()
        {
            return CreateCanonical(0L, Array.Empty<WeaponEquipmentInstance>());
        }

        public static WeaponHoldingsSnapshotV2 CreateCanonical(
            long sequence,
            IEnumerable<WeaponEquipmentInstance> values)
        {
            var preliminary = new WeaponHoldingsSnapshotV2(
                sequence,
                values,
                string.Empty);
            return new WeaponHoldingsSnapshotV2(
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

    public sealed class WeaponHoldingsImportResultV2
    {
        public WeaponHoldingsImportResultV2(
            bool succeeded,
            string rejectionCode,
            WeaponHoldingsSnapshotV2 snapshot)
        {
            Succeeded = succeeded;
            RejectionCode = rejectionCode ?? string.Empty;
            Snapshot = snapshot;
        }

        public bool Succeeded { get; }
        public string RejectionCode { get; }
        public WeaponHoldingsSnapshotV2 Snapshot { get; }
    }

    /// <summary>
    /// Character-local canonical authority for exact owned weapons.
    /// Equip and unequip never mutate this authority.
    /// </summary>
    public sealed class ProductionWeaponHoldingsAuthorityV2
    {
        private WeaponHoldingsSnapshotV2 snapshot;

        public ProductionWeaponHoldingsAuthorityV2(
            WeaponHoldingsSnapshotV2 initial = null)
        {
            snapshot = WeaponHoldingsSnapshotV2.Empty();
            if (initial != null)
            {
                WeaponHoldingsImportResultV2 imported = ImportSnapshot(initial);
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

        public WeaponHoldingsSnapshotV2 ExportSnapshot()
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

        public WeaponHoldingsImportResultV2 ImportSnapshot(
            WeaponHoldingsSnapshotV2 imported)
        {
            if (imported == null)
            {
                return Reject("weapon-holdings-v2-import-null");
            }
            if (imported.SchemaVersion
                    != WeaponHoldingsSnapshotV2.CurrentSchemaVersion
                || !imported.HasValidFingerprint())
            {
                return Reject("weapon-holdings-v2-import-invalid");
            }

            snapshot = WeaponHoldingsSnapshotV2.CreateCanonical(
                imported.Sequence,
                imported.Instances);
            return new WeaponHoldingsImportResultV2(
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
            snapshot = WeaponHoldingsSnapshotV2.CreateCanonical(
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
            if (snapshot.Find(instanceId) == null)
            {
                return true;
            }

            var next = new List<WeaponEquipmentInstance>();
            for (int index = 0; index < snapshot.Instances.Count; index++)
            {
                if (snapshot.Instances[index].InstanceId != instanceId)
                {
                    next.Add(snapshot.Instances[index]);
                }
            }
            snapshot = WeaponHoldingsSnapshotV2.CreateCanonical(
                checked(snapshot.Sequence + 1L),
                next);
            return true;
        }

        private WeaponHoldingsImportResultV2 Reject(string code)
        {
            return new WeaponHoldingsImportResultV2(false, code, snapshot);
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
    public static class ProductionWeaponHoldingsMigrationV2
    {
        public static WeaponHoldingsSnapshotV2 ConvertLegacy(
            PlayerHoldingsSnapshotV1 legacy)
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
                UniqueHoldingSnapshotV1 holding = legacy.UniqueHoldings[index];
                WeaponEquipmentInstance converted;
                if (TryConvertHolding(holding, out converted))
                {
                    migrated.Add(converted);
                }
            }
            return WeaponHoldingsSnapshotV2.CreateCanonical(0L, migrated);
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
                ProductionWeaponCatalogProvider.EquipmentCatalog
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
                new WeaponDefinitionId(
                    definition.RuntimeWeaponReferenceId.ToString()),
                augmentAssignments,
                Array.Empty<StableId>());
            return true;
        }

        private static bool TryConvertHolding(
            UniqueHoldingSnapshotV1 holding,
            out WeaponEquipmentInstance converted)
        {
            converted = null;
            return holding != null
                && holding.RewardKind == RewardGrantKindV1.EquipmentReference
                && holding.InstanceStableId != null
                && holding.EquipmentInstance != null
                && TryConvertEquipment(holding.EquipmentInstance, out converted)
                && converted.InstanceId == holding.InstanceStableId;
        }
    }

    /// <summary>
    /// Keeps immutable generic reward receipts authoritative while projecting accepted weapon
    /// additions/removals into canonical V2 holdings in the same runtime operation.
    /// </summary>
    public sealed class CanonicalizingPlayerHoldingsAuthorityV2 :
        IPlayerHoldingsAuthorityV1
    {
        private readonly IPlayerHoldingsAuthorityV1 legacy;
        private readonly ProductionWeaponHoldingsAuthorityV2 weapons;

        public CanonicalizingPlayerHoldingsAuthorityV2(
            IPlayerHoldingsAuthorityV1 legacyAuthority,
            ProductionWeaponHoldingsAuthorityV2 weaponAuthority)
        {
            legacy = legacyAuthority
                ?? throw new ArgumentNullException(nameof(legacyAuthority));
            weapons = weaponAuthority
                ?? throw new ArgumentNullException(nameof(weaponAuthority));
        }

        public StableId AuthorityStableId { get { return legacy.AuthorityStableId; } }
        public long Sequence { get { return legacy.Sequence; } }

        public PlayerHoldingsSnapshotV1 ExportSnapshot()
        {
            return legacy.ExportSnapshot();
        }

        public PlayerHoldingsImportResultV1 ImportSnapshot(
            PlayerHoldingsSnapshotV1 snapshot)
        {
            PlayerHoldingsImportResultV1 result = legacy.ImportSnapshot(snapshot);
            if (result != null && result.Succeeded)
            {
                SynchronizeLegacyWeapons();
            }
            return result;
        }

        public PlayerHoldingsMutationResultV1 Apply(
            PlayerHoldingsCommandV1 command)
        {
            PlayerHoldingsMutationResultV1 result = legacy.Apply(command);
            if (result == null
                || (result.Status != PlayerHoldingsMutationStatusV1.Applied
                    && result.Status
                        != PlayerHoldingsMutationStatusV1.ExactDuplicateNoChange)
                || command == null
                || command.RewardKind != RewardGrantKindV1.EquipmentReference)
            {
                return result;
            }

            string rejectionCode;
            if (command.Transaction.Operation
                    == EconomyTransactionOperationV1.AddUnique
                && command.EquipmentInstance != null)
            {
                WeaponEquipmentInstance converted;
                if (ProductionWeaponHoldingsMigrationV2.TryConvertEquipment(
                        command.EquipmentInstance,
                        out converted)
                    && !weapons.TryAdd(converted, out rejectionCode))
                {
                    throw new InvalidOperationException(
                        "Canonical weapon grant projection failed: "
                        + rejectionCode);
                }
            }
            else if (command.Transaction.Operation
                    == EconomyTransactionOperationV1.RemoveUnique
                && command.Transaction.InstanceStableId != null
                && !weapons.TryRemove(
                    command.Transaction.InstanceStableId,
                    out rejectionCode))
            {
                throw new InvalidOperationException(
                    "Canonical weapon removal projection failed: "
                    + rejectionCode);
            }
            return result;
        }

        private void SynchronizeLegacyWeapons()
        {
            WeaponHoldingsSnapshotV2 migrated =
                ProductionWeaponHoldingsMigrationV2.ConvertLegacy(
                    legacy.ExportSnapshot());
            for (int index = 0; index < migrated.Instances.Count; index++)
            {
                string ignored;
                weapons.TryAdd(migrated.Instances[index], out ignored);
            }
        }
    }

    public sealed class WeaponHoldingsComponentCodecV2 :
        ISaveComponentPayloadCodecV1<WeaponHoldingsSnapshotV2>
    {
        private const string Prefix = "weapon-holdings-v2:";
        private const int MaximumInstances = 4096;
        private const int MaximumAssignmentsPerInstance = 256;

        public string ContractId { get { return "weapon-holdings-explicit-v2"; } }

        public string Encode(WeaponHoldingsSnapshotV2 snapshot)
        {
            SaveComponentValidationResultV1 validation = Validate(snapshot);
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
                writer.Write(WeaponHoldingsSnapshotV2.CurrentSchemaVersion);
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
            out WeaponHoldingsSnapshotV2 snapshot,
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
                    if (schema != WeaponHoldingsSnapshotV2.CurrentSchemaVersion)
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

                    snapshot = WeaponHoldingsSnapshotV2.CreateCanonical(
                        sequence,
                        instances);
                }

                SaveComponentValidationResultV1 validation =
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

        public SaveComponentValidationResultV1 Validate(
            WeaponHoldingsSnapshotV2 snapshot)
        {
            if (snapshot == null)
            {
                return SaveComponentValidationResultV1.Reject(
                    "weapon-holdings-v2-snapshot-null");
            }
            if (snapshot.SchemaVersion
                    != WeaponHoldingsSnapshotV2.CurrentSchemaVersion
                || !snapshot.HasValidFingerprint()
                || snapshot.Instances.Count > MaximumInstances)
            {
                return SaveComponentValidationResultV1.Reject(
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
                    return SaveComponentValidationResultV1.Reject(
                        "weapon-holdings-v2-assignment-count-invalid");
                }

                WeaponDefinitionData ignored;
                if (!ProductionWeaponCatalogProvider.WeaponCatalog
                    .TryGetDefinition(
                        instance.WeaponDefinitionId.Value,
                        out ignored))
                {
                    return SaveComponentValidationResultV1.Reject(
                        "weapon-holdings-v2-definition-unknown:"
                        + instance.WeaponDefinitionId.Value);
                }
            }
            return SaveComponentValidationResultV1.Accept();
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

    public static class WeaponHoldingsSaveComponentV2
    {
        private static readonly StableId ComponentId =
            StableId.Parse("save-component.weapon-holdings");
        private static readonly WeaponHoldingsComponentCodecV2 codec =
            new WeaponHoldingsComponentCodecV2();

        public static WeaponHoldingsComponentCodecV2 Codec
        {
            get { return codec; }
        }

        public static SaveComponentDefinitionV1 Definition()
        {
            return new SaveComponentDefinitionV1(
                ComponentId,
                2,
                codec.ContractId,
                false,
                25);
        }

        public static ISaveComponentAdapterV1 CreateAdapter(
            ProductionWeaponHoldingsAuthorityV2 authority)
        {
            if (authority == null)
            {
                throw new ArgumentNullException(nameof(authority));
            }
            return new AuthoritySnapshotSaveComponentAdapterV1<
                WeaponHoldingsSnapshotV2>(
                Definition(),
                codec,
                authority.ExportSnapshot,
                codec.Validate,
                snapshot =>
                {
                    WeaponHoldingsImportResultV2 result =
                        authority.ImportSnapshot(snapshot);
                    return result.Succeeded
                        ? SaveComponentApplyResultV1.Applied()
                        : SaveComponentApplyResultV1.Rejected(
                            result.RejectionCode);
                });
        }

        public static bool TryRead(
            CharacterInstanceSnapshotV1 character,
            out WeaponHoldingsSnapshotV2 snapshot,
            out string rejectionCode)
        {
            snapshot = null;
            rejectionCode = string.Empty;
            if (character == null)
            {
                rejectionCode = "weapon-holdings-v2-character-null";
                return false;
            }

            SaveComponentSnapshotV1 component;
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
    public sealed class CanonicalWeaponInstanceLookupV2
    {
        private readonly ProductionWeaponHoldingsAuthorityV2 holdings;

        public CanonicalWeaponInstanceLookupV2(
            ProductionWeaponHoldingsAuthorityV2 authority)
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
