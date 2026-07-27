using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;

namespace ShooterMover.Application.Flow.Production
{
    public sealed class WeaponMountBindingV2 : IEquatable<WeaponMountBindingV2>
    {
        public WeaponMountBindingV2(StableId mountId, StableId instanceId)
        {
            MountId = mountId ?? throw new ArgumentNullException(nameof(mountId));
            InstanceId = instanceId;
        }

        public StableId MountId { get; }
        public StableId InstanceId { get; }

        public bool Equals(WeaponMountBindingV2 other)
        {
            return other != null
                && MountId == other.MountId
                && InstanceId == other.InstanceId;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as WeaponMountBindingV2);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (MountId.GetHashCode() * 397)
                    ^ (InstanceId == null ? 0 : InstanceId.GetHashCode());
            }
        }
    }

    /// <summary>
    /// Canonical weapon loadout persistence. It contains exactly one record per physical class
    /// mount and never stores legacy generic weapon-slot placeholders.
    /// </summary>
    public sealed class WeaponMountLoadoutSnapshotV2
    {
        public const int CurrentSchemaVersion = 2;
        private readonly ReadOnlyCollection<WeaponMountBindingV2> bindings;

        private WeaponMountLoadoutSnapshotV2(
            long sequence,
            IEnumerable<WeaponMountBindingV2> values,
            string fingerprint)
        {
            if (sequence < 0L) throw new ArgumentOutOfRangeException(nameof(sequence));
            Sequence = sequence;
            bindings = Canonicalize(values);
            Fingerprint = fingerprint ?? string.Empty;
        }

        public int SchemaVersion { get { return CurrentSchemaVersion; } }
        public long Sequence { get; }
        public IReadOnlyList<WeaponMountBindingV2> Bindings { get { return bindings; } }
        public string Fingerprint { get; }

        public static WeaponMountLoadoutSnapshotV2 CreateCanonical(
            long sequence,
            IEnumerable<WeaponMountBindingV2> values)
        {
            var preliminary = new WeaponMountLoadoutSnapshotV2(
                sequence,
                values,
                string.Empty);
            return new WeaponMountLoadoutSnapshotV2(
                sequence,
                preliminary.Bindings,
                ComputeFingerprint(sequence, preliminary.Bindings));
        }

        public bool HasValidFingerprint()
        {
            return string.Equals(
                Fingerprint,
                ComputeFingerprint(Sequence, bindings),
                StringComparison.Ordinal);
        }

        public WeaponMountBindingV2 Find(StableId mountId)
        {
            if (mountId == null) return null;
            for (int index = 0; index < bindings.Count; index++)
            {
                if (bindings[index].MountId == mountId) return bindings[index];
            }
            return null;
        }

        private static ReadOnlyCollection<WeaponMountBindingV2> Canonicalize(
            IEnumerable<WeaponMountBindingV2> source)
        {
            var copy = new List<WeaponMountBindingV2>(
                source ?? throw new ArgumentNullException(nameof(source)));
            copy.Sort(delegate(WeaponMountBindingV2 left, WeaponMountBindingV2 right)
            {
                if (ReferenceEquals(left, null)) return -1;
                if (ReferenceEquals(right, null)) return 1;
                return left.MountId.CompareTo(right.MountId);
            });

            var mountIds = new HashSet<StableId>();
            var instanceIds = new HashSet<StableId>();
            for (int index = 0; index < copy.Count; index++)
            {
                WeaponMountBindingV2 binding = copy[index];
                if (binding == null || !mountIds.Add(binding.MountId))
                {
                    throw new ArgumentException(
                        "Weapon mount loadouts require unique non-null mount identities.",
                        nameof(source));
                }
                if (binding.InstanceId != null && !instanceIds.Add(binding.InstanceId))
                {
                    throw new ArgumentException(
                        "An exact weapon instance cannot occupy two physical mounts.",
                        nameof(source));
                }
            }
            return new ReadOnlyCollection<WeaponMountBindingV2>(copy);
        }

        private static string ComputeFingerprint(
            long sequence,
            IReadOnlyList<WeaponMountBindingV2> values)
        {
            var builder = new StringBuilder();
            Append(builder, "schema", CurrentSchemaVersion.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
            Append(builder, "sequence", sequence.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
            Append(builder, "count", values.Count.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
            for (int index = 0; index < values.Count; index++)
            {
                Append(builder, "mount", values[index].MountId.ToString());
                Append(
                    builder,
                    "instance",
                    values[index].InstanceId == null
                        ? string.Empty
                        : values[index].InstanceId.ToString());
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

        private static void Append(StringBuilder builder, string name, string value)
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

    public sealed class WeaponMountLoadoutImportResultV2
    {
        public WeaponMountLoadoutImportResultV2(
            bool succeeded,
            string rejectionCode,
            WeaponMountLoadoutSnapshotV2 snapshot)
        {
            Succeeded = succeeded;
            RejectionCode = rejectionCode ?? string.Empty;
            Snapshot = snapshot;
        }

        public bool Succeeded { get; }
        public string RejectionCode { get; }
        public WeaponMountLoadoutSnapshotV2 Snapshot { get; }
    }

    public sealed class ProductionWeaponMountLoadoutAuthorityV2
    {
        private readonly ProductionWeaponMountLayoutV1 layout;
        private readonly ProductionWeaponHoldingsAuthorityV2 holdings;
        private WeaponMountLoadoutSnapshotV2 snapshot;

        public ProductionWeaponMountLoadoutAuthorityV2(
            ProductionWeaponMountLayoutV1 mountLayout,
            ProductionWeaponHoldingsAuthorityV2 weaponHoldings,
            WeaponMountLoadoutSnapshotV2 initial)
        {
            layout = mountLayout ?? throw new ArgumentNullException(nameof(mountLayout));
            holdings = weaponHoldings
                ?? throw new ArgumentNullException(nameof(weaponHoldings));
            snapshot = WeaponMountLoadoutSnapshotV2.CreateCanonical(
                0L,
                EmptyPhysicalBindings(layout));
            WeaponMountLoadoutImportResultV2 result = ImportSnapshot(
                initial ?? throw new ArgumentNullException(nameof(initial)));
            if (!result.Succeeded)
            {
                throw new ArgumentException(result.RejectionCode, nameof(initial));
            }
        }

        public long Sequence { get { return snapshot.Sequence; } }
        public WeaponMountLoadoutSnapshotV2 ExportSnapshot() { return snapshot; }

        public WeaponMountLoadoutImportResultV2 ImportSnapshot(
            WeaponMountLoadoutSnapshotV2 imported)
        {
            string rejectionCode;
            if (!Validate(imported, out rejectionCode))
            {
                return new WeaponMountLoadoutImportResultV2(
                    false,
                    rejectionCode,
                    snapshot);
            }
            snapshot = WeaponMountLoadoutSnapshotV2.CreateCanonical(
                imported.Sequence,
                imported.Bindings);
            return new WeaponMountLoadoutImportResultV2(true, string.Empty, snapshot);
        }

        public WeaponMountLoadoutImportResultV2 Apply(
            long expectedSequence,
            IEnumerable<WeaponMountBindingV2> bindings)
        {
            if (expectedSequence != snapshot.Sequence)
            {
                return new WeaponMountLoadoutImportResultV2(
                    false,
                    "weapon-mount-loadout-v2-sequence-stale",
                    snapshot);
            }

            WeaponMountLoadoutSnapshotV2 candidate;
            try
            {
                candidate = WeaponMountLoadoutSnapshotV2.CreateCanonical(
                    checked(snapshot.Sequence + 1L),
                    bindings);
            }
            catch (Exception exception)
                when (exception is ArgumentException || exception is OverflowException)
            {
                return new WeaponMountLoadoutImportResultV2(
                    false,
                    "weapon-mount-loadout-v2-command-invalid",
                    snapshot);
            }

            string rejectionCode;
            if (!Validate(candidate, out rejectionCode))
            {
                return new WeaponMountLoadoutImportResultV2(
                    false,
                    rejectionCode,
                    snapshot);
            }
            if (BindingsEqual(snapshot.Bindings, candidate.Bindings))
            {
                return new WeaponMountLoadoutImportResultV2(
                    true,
                    string.Empty,
                    snapshot);
            }
            snapshot = candidate;
            return new WeaponMountLoadoutImportResultV2(true, string.Empty, snapshot);
        }

        private bool Validate(
            WeaponMountLoadoutSnapshotV2 candidate,
            out string rejectionCode)
        {
            rejectionCode = string.Empty;
            if (candidate == null
                || candidate.SchemaVersion
                    != WeaponMountLoadoutSnapshotV2.CurrentSchemaVersion
                || !candidate.HasValidFingerprint())
            {
                rejectionCode = "weapon-mount-loadout-v2-snapshot-invalid";
                return false;
            }
            if (candidate.Bindings.Count != layout.PhysicalPositions.Count)
            {
                rejectionCode = "weapon-mount-loadout-v2-physical-count-invalid";
                return false;
            }

            var expectedMountIds = new HashSet<StableId>();
            for (int index = 0; index < layout.PhysicalPositions.Count; index++)
            {
                expectedMountIds.Add(layout.PhysicalPositions[index].MountStableId);
            }
            for (int index = 0; index < candidate.Bindings.Count; index++)
            {
                WeaponMountBindingV2 binding = candidate.Bindings[index];
                if (!expectedMountIds.Remove(binding.MountId))
                {
                    rejectionCode = "weapon-mount-loadout-v2-mount-not-owned-by-class";
                    return false;
                }
                ProductionWeaponMountPositionV1 position =
                    ProductionWeaponMountPolicyV1.FindPosition(layout, binding.MountId);
                if (position == null)
                {
                    rejectionCode = "weapon-mount-loadout-v2-mount-unresolved";
                    return false;
                }
                if (position.IsLockedBySkill && binding.InstanceId != null)
                {
                    rejectionCode = "weapon-mount-loadout-v2-mount-locked-by-skill";
                    return false;
                }
                if (binding.InstanceId != null
                    && holdings.Find(binding.InstanceId) == null)
                {
                    rejectionCode = "weapon-mount-loadout-v2-instance-not-owned";
                    return false;
                }
            }
            if (expectedMountIds.Count != 0)
            {
                rejectionCode = "weapon-mount-loadout-v2-physical-mount-missing";
                return false;
            }
            return true;
        }

        private static bool BindingsEqual(
            IReadOnlyList<WeaponMountBindingV2> left,
            IReadOnlyList<WeaponMountBindingV2> right)
        {
            if (left == null || right == null || left.Count != right.Count) return false;
            for (int index = 0; index < left.Count; index++)
            {
                if (!left[index].Equals(right[index])) return false;
            }
            return true;
        }

        private static IEnumerable<WeaponMountBindingV2> EmptyPhysicalBindings(
            ProductionWeaponMountLayoutV1 source)
        {
            var values = new List<WeaponMountBindingV2>(source.PhysicalPositions.Count);
            for (int index = 0; index < source.PhysicalPositions.Count; index++)
            {
                values.Add(new WeaponMountBindingV2(
                    source.PhysicalPositions[index].MountStableId,
                    null));
            }
            return values;
        }
    }

    public static class ProductionWeaponMountLoadoutProjectionV2
    {
        public static WeaponMountLoadoutSnapshotV2 MigrateLegacy(
            ProductionWeaponMountLayoutV1 layout,
            ProductionWeaponHoldingsAuthorityV2 holdings,
            InventoryLoadoutAuthoritySnapshotV1 legacy)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (holdings == null) throw new ArgumentNullException(nameof(holdings));
            if (legacy == null || !legacy.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "A valid legacy loadout snapshot is required.",
                    nameof(legacy));
            }

            var selected = new HashSet<StableId>();
            var bindings = new List<WeaponMountBindingV2>(
                layout.PhysicalPositions.Count);
            for (int index = 0; index < layout.PhysicalPositions.Count; index++)
            {
                ProductionWeaponMountPositionV1 position =
                    layout.PhysicalPositions[index];
                StableId instanceId = legacy.GetBinding(
                    position.LoadoutSlotStableId).EquipmentInstanceStableId;
                if (!position.IsActive
                    || instanceId == null
                    || holdings.Find(instanceId) == null
                    || !selected.Add(instanceId))
                {
                    instanceId = null;
                }
                bindings.Add(new WeaponMountBindingV2(
                    position.MountStableId,
                    instanceId));
            }
            return WeaponMountLoadoutSnapshotV2.CreateCanonical(
                legacy.Sequence,
                bindings);
        }

        public static InventoryLoadoutAuthoritySnapshotV1 ToLegacyProjection(
            ProductionWeaponMountLayoutV1 layout,
            WeaponMountLoadoutSnapshotV2 mounts,
            InventoryLoadoutAuthoritySnapshotV1 armorTemplate)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (mounts == null || !mounts.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "A valid canonical mount loadout is required.",
                    nameof(mounts));
            }
            if (armorTemplate == null || !armorTemplate.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "A valid legacy armor projection is required.",
                    nameof(armorTemplate));
            }

            var bindings = new List<InventoryLoadoutSlotBindingV1>(
                InventoryLoadoutSlotsV1.All.Count);
            for (int index = 0; index < InventoryLoadoutSlotsV1.All.Count; index++)
            {
                InventoryLoadoutSlotDescriptorV1 slot = InventoryLoadoutSlotsV1.All[index];
                StableId instanceId = slot.Kind == InventoryLoadoutSlotKindV1.Weapon
                    ? null
                    : armorTemplate.GetBinding(slot.SlotStableId)
                        .EquipmentInstanceStableId;
                bindings.Add(new InventoryLoadoutSlotBindingV1(
                    slot.SlotStableId,
                    instanceId));
            }

            for (int index = 0; index < layout.PhysicalPositions.Count; index++)
            {
                ProductionWeaponMountPositionV1 position = layout.PhysicalPositions[index];
                WeaponMountBindingV2 binding = mounts.Find(position.MountStableId);
                if (binding == null)
                {
                    throw new ArgumentException(
                        "The canonical loadout is missing a physical mount.",
                        nameof(mounts));
                }
                int slotIndex = FindLegacySlotIndex(position.LoadoutSlotStableId);
                bindings[slotIndex] = new InventoryLoadoutSlotBindingV1(
                    position.LoadoutSlotStableId,
                    position.IsActive ? binding.InstanceId : null);
            }
            return InventoryLoadoutAuthoritySnapshotV1.CreateCanonical(
                mounts.Sequence,
                bindings);
        }

        public static InventoryLoadoutAuthoritySnapshotV1 ArmorOnly(
            InventoryLoadoutAuthoritySnapshotV1 source)
        {
            if (source == null || !source.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "A valid loadout snapshot is required.",
                    nameof(source));
            }
            var bindings = new List<InventoryLoadoutSlotBindingV1>(
                InventoryLoadoutSlotsV1.All.Count);
            for (int index = 0; index < InventoryLoadoutSlotsV1.All.Count; index++)
            {
                InventoryLoadoutSlotDescriptorV1 slot = InventoryLoadoutSlotsV1.All[index];
                bindings.Add(new InventoryLoadoutSlotBindingV1(
                    slot.SlotStableId,
                    slot.Kind == InventoryLoadoutSlotKindV1.Weapon
                        ? null
                        : source.GetBinding(slot.SlotStableId)
                            .EquipmentInstanceStableId));
            }
            return InventoryLoadoutAuthoritySnapshotV1.CreateCanonical(
                source.Sequence,
                bindings);
        }

        public static PlayerRouteProfilePayloadV1 Route(
            StableId characterId,
            StableId loadoutProfileId,
            ProductionWeaponMountLayoutV1 layout,
            WeaponMountLoadoutSnapshotV2 mounts)
        {
            InventoryLoadoutAuthoritySnapshotV1 emptyArmor =
                InventoryLoadoutAuthoritySnapshotV1.CreateCanonical(
                    mounts.Sequence,
                    EmptyLegacyBindings());
            InventoryLoadoutAuthoritySnapshotV1 projection = ToLegacyProjection(
                layout,
                mounts,
                emptyArmor);
            return ProductionWeaponOnboardingV1.RouteFromLoadout(
                characterId,
                loadoutProfileId,
                projection);
        }

        private static int FindLegacySlotIndex(StableId slotId)
        {
            for (int index = 0; index < InventoryLoadoutSlotsV1.All.Count; index++)
            {
                if (InventoryLoadoutSlotsV1.All[index].SlotStableId == slotId) return index;
            }
            throw new InvalidOperationException(
                "A physical mount has no legacy route projection slot.");
        }

        private static IEnumerable<InventoryLoadoutSlotBindingV1> EmptyLegacyBindings()
        {
            var values = new List<InventoryLoadoutSlotBindingV1>(
                InventoryLoadoutSlotsV1.All.Count);
            for (int index = 0; index < InventoryLoadoutSlotsV1.All.Count; index++)
            {
                values.Add(new InventoryLoadoutSlotBindingV1(
                    InventoryLoadoutSlotsV1.All[index].SlotStableId,
                    null));
            }
            return values;
        }
    }

    public sealed class WeaponMountLoadoutComponentCodecV2 :
        ISaveComponentPayloadCodecV1<WeaponMountLoadoutSnapshotV2>
    {
        private const string Prefix = "weapon-mount-loadout-v2:";
        private const int MaximumMounts = 4;
        public string ContractId { get { return "weapon-mount-loadout-explicit-v2"; } }

        public string Encode(WeaponMountLoadoutSnapshotV2 snapshot)
        {
            SaveComponentValidationResultV1 validation = Validate(snapshot);
            if (!validation.Succeeded)
            {
                throw new ArgumentException(validation.RejectionCode, nameof(snapshot));
            }
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(WeaponMountLoadoutSnapshotV2.CurrentSchemaVersion);
                writer.Write(snapshot.Sequence);
                writer.Write(snapshot.Bindings.Count);
                for (int index = 0; index < snapshot.Bindings.Count; index++)
                {
                    writer.Write(snapshot.Bindings[index].MountId.ToString());
                    writer.Write(snapshot.Bindings[index].InstanceId != null);
                    if (snapshot.Bindings[index].InstanceId != null)
                    {
                        writer.Write(snapshot.Bindings[index].InstanceId.ToString());
                    }
                }
                writer.Flush();
                return Prefix + Convert.ToBase64String(stream.ToArray());
            }
        }

        public bool TryDecode(
            string canonicalPayload,
            out WeaponMountLoadoutSnapshotV2 snapshot,
            out string rejectionCode)
        {
            snapshot = null;
            rejectionCode = string.Empty;
            if (string.IsNullOrWhiteSpace(canonicalPayload)
                || !canonicalPayload.StartsWith(Prefix, StringComparison.Ordinal))
            {
                rejectionCode = "weapon-mount-loadout-v2-payload-prefix-invalid";
                return false;
            }
            try
            {
                byte[] bytes = Convert.FromBase64String(
                    canonicalPayload.Substring(Prefix.Length));
                using (var stream = new MemoryStream(bytes, false))
                using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
                {
                    int schema = reader.ReadInt32();
                    long sequence = reader.ReadInt64();
                    int count = reader.ReadInt32();
                    if (schema != WeaponMountLoadoutSnapshotV2.CurrentSchemaVersion
                        || sequence < 0L
                        || count < 0
                        || count > MaximumMounts)
                    {
                        rejectionCode = "weapon-mount-loadout-v2-header-invalid";
                        return false;
                    }
                    var values = new List<WeaponMountBindingV2>(count);
                    for (int index = 0; index < count; index++)
                    {
                        StableId mountId = StableId.Parse(reader.ReadString());
                        StableId instanceId = reader.ReadBoolean()
                            ? StableId.Parse(reader.ReadString())
                            : null;
                        values.Add(new WeaponMountBindingV2(mountId, instanceId));
                    }
                    if (stream.Position != stream.Length)
                    {
                        rejectionCode = "weapon-mount-loadout-v2-payload-trailing-data";
                        return false;
                    }
                    snapshot = WeaponMountLoadoutSnapshotV2.CreateCanonical(
                        sequence,
                        values);
                }
                SaveComponentValidationResultV1 validation = Validate(snapshot);
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
                rejectionCode = "weapon-mount-loadout-v2-payload-invalid";
                return false;
            }
        }

        public SaveComponentValidationResultV1 Validate(
            WeaponMountLoadoutSnapshotV2 snapshot)
        {
            return snapshot != null
                && snapshot.SchemaVersion
                    == WeaponMountLoadoutSnapshotV2.CurrentSchemaVersion
                && snapshot.HasValidFingerprint()
                && snapshot.Bindings.Count >= 2
                && snapshot.Bindings.Count <= MaximumMounts
                ? SaveComponentValidationResultV1.Accept()
                : SaveComponentValidationResultV1.Reject(
                    "weapon-mount-loadout-v2-snapshot-invalid");
        }
    }

    public static class WeaponMountLoadoutSaveComponentV2
    {
        private static readonly StableId ComponentId =
            StableId.Parse("save-component.weapon-mount-loadout");
        private static readonly WeaponMountLoadoutComponentCodecV2 codec =
            new WeaponMountLoadoutComponentCodecV2();

        public static WeaponMountLoadoutComponentCodecV2 Codec { get { return codec; } }

        public static SaveComponentDefinitionV1 Definition()
        {
            return new SaveComponentDefinitionV1(
                ComponentId,
                2,
                codec.ContractId,
                false,
                26);
        }

        public static ISaveComponentAdapterV1 CreateAdapter(
            ProductionWeaponMountLoadoutAuthorityV2 authority)
        {
            if (authority == null) throw new ArgumentNullException(nameof(authority));
            return new AuthoritySnapshotSaveComponentAdapterV1<
                WeaponMountLoadoutSnapshotV2>(
                Definition(),
                codec,
                authority.ExportSnapshot,
                codec.Validate,
                snapshot =>
                {
                    WeaponMountLoadoutImportResultV2 result =
                        authority.ImportSnapshot(snapshot);
                    return result.Succeeded
                        ? SaveComponentApplyResultV1.Applied()
                        : SaveComponentApplyResultV1.Rejected(result.RejectionCode);
                });
        }

        public static bool TryRead(
            CharacterInstanceSnapshotV1 character,
            out WeaponMountLoadoutSnapshotV2 snapshot,
            out string rejectionCode)
        {
            snapshot = null;
            rejectionCode = string.Empty;
            if (character == null)
            {
                rejectionCode = "weapon-mount-loadout-v2-character-null";
                return false;
            }
            SaveComponentSnapshotV1 component;
            if (!character.TryGetComponent(ComponentId, out component)) return false;
            if (component.SchemaVersion != 2
                || !string.Equals(
                    component.ContentVersion,
                    codec.ContractId,
                    StringComparison.Ordinal))
            {
                rejectionCode =
                    "weapon-mount-loadout-v2-component-version-unsupported";
                return false;
            }
            return codec.TryDecode(
                component.CanonicalPayload,
                out snapshot,
                out rejectionCode);
        }
    }
}
