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
    public sealed class WeaponMountBinding : IEquatable<WeaponMountBinding>
    {
        public WeaponMountBinding(StableId mountId, StableId instanceId)
        {
            MountId = mountId ?? throw new ArgumentNullException(nameof(mountId));
            InstanceId = instanceId;
        }

        public StableId MountId { get; }
        public StableId InstanceId { get; }

        public bool Equals(WeaponMountBinding other)
        {
            return other != null
                && MountId == other.MountId
                && InstanceId == other.InstanceId;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as WeaponMountBinding);
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
    public sealed class WeaponMountLoadoutSnapshot
    {
        public const int CurrentSchemaVersion = 2;
        private readonly ReadOnlyCollection<WeaponMountBinding> bindings;

        private WeaponMountLoadoutSnapshot(
            long sequence,
            IEnumerable<WeaponMountBinding> values,
            string fingerprint)
        {
            if (sequence < 0L) throw new ArgumentOutOfRangeException(nameof(sequence));
            Sequence = sequence;
            bindings = Canonicalize(values);
            Fingerprint = fingerprint ?? string.Empty;
        }

        public int SchemaVersion { get { return CurrentSchemaVersion; } }
        public long Sequence { get; }
        public IReadOnlyList<WeaponMountBinding> Bindings { get { return bindings; } }
        public string Fingerprint { get; }

        public static WeaponMountLoadoutSnapshot CreateCanonical(
            long sequence,
            IEnumerable<WeaponMountBinding> values)
        {
            var preliminary = new WeaponMountLoadoutSnapshot(
                sequence,
                values,
                string.Empty);
            return new WeaponMountLoadoutSnapshot(
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

        public WeaponMountBinding Find(StableId mountId)
        {
            if (mountId == null) return null;
            for (int index = 0; index < bindings.Count; index++)
            {
                if (bindings[index].MountId == mountId) return bindings[index];
            }
            return null;
        }

        private static ReadOnlyCollection<WeaponMountBinding> Canonicalize(
            IEnumerable<WeaponMountBinding> source)
        {
            var copy = new List<WeaponMountBinding>(
                source ?? throw new ArgumentNullException(nameof(source)));
            copy.Sort(delegate(WeaponMountBinding left, WeaponMountBinding right)
            {
                if (ReferenceEquals(left, null)) return -1;
                if (ReferenceEquals(right, null)) return 1;
                return left.MountId.CompareTo(right.MountId);
            });

            var mountIds = new HashSet<StableId>();
            var instanceIds = new HashSet<StableId>();
            for (int index = 0; index < copy.Count; index++)
            {
                WeaponMountBinding binding = copy[index];
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
            return new ReadOnlyCollection<WeaponMountBinding>(copy);
        }

        private static string ComputeFingerprint(
            long sequence,
            IReadOnlyList<WeaponMountBinding> values)
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

    public sealed class WeaponMountLoadoutImportResult
    {
        public WeaponMountLoadoutImportResult(
            bool succeeded,
            string rejectionCode,
            WeaponMountLoadoutSnapshot snapshot)
        {
            Succeeded = succeeded;
            RejectionCode = rejectionCode ?? string.Empty;
            Snapshot = snapshot;
        }

        public bool Succeeded { get; }
        public string RejectionCode { get; }
        public WeaponMountLoadoutSnapshot Snapshot { get; }
    }

    public sealed class WeaponMountLoadoutState
    {
        private readonly WeaponMountLayout layout;
        private readonly WeaponHoldingsState holdings;
        private WeaponMountLoadoutSnapshot snapshot;

        public WeaponMountLoadoutState(
            WeaponMountLayout mountLayout,
            WeaponHoldingsState weaponHoldings,
            WeaponMountLoadoutSnapshot initial)
        {
            layout = mountLayout ?? throw new ArgumentNullException(nameof(mountLayout));
            holdings = weaponHoldings
                ?? throw new ArgumentNullException(nameof(weaponHoldings));
            snapshot = WeaponMountLoadoutSnapshot.CreateCanonical(
                0L,
                EmptyPhysicalBindings(layout));
            WeaponMountLoadoutImportResult result = ImportSnapshot(
                initial ?? throw new ArgumentNullException(nameof(initial)));
            if (!result.Succeeded)
            {
                throw new ArgumentException(result.RejectionCode, nameof(initial));
            }
        }

        public long Sequence { get { return snapshot.Sequence; } }
        public WeaponMountLoadoutSnapshot ExportSnapshot() { return snapshot; }

        public WeaponMountLoadoutImportResult ImportSnapshot(
            WeaponMountLoadoutSnapshot imported)
        {
            string rejectionCode;
            if (!Validate(imported, out rejectionCode))
            {
                return new WeaponMountLoadoutImportResult(
                    false,
                    rejectionCode,
                    snapshot);
            }
            snapshot = WeaponMountLoadoutSnapshot.CreateCanonical(
                imported.Sequence,
                imported.Bindings);
            return new WeaponMountLoadoutImportResult(true, string.Empty, snapshot);
        }

        public WeaponMountLoadoutImportResult Apply(
            long expectedSequence,
            IEnumerable<WeaponMountBinding> bindings)
        {
            if (expectedSequence != snapshot.Sequence)
            {
                return new WeaponMountLoadoutImportResult(
                    false,
                    "weapon-mount-loadout-v2-sequence-stale",
                    snapshot);
            }

            WeaponMountLoadoutSnapshot candidate;
            try
            {
                candidate = WeaponMountLoadoutSnapshot.CreateCanonical(
                    checked(snapshot.Sequence + 1L),
                    bindings);
            }
            catch (Exception exception)
                when (exception is ArgumentException || exception is OverflowException)
            {
                return new WeaponMountLoadoutImportResult(
                    false,
                    "weapon-mount-loadout-v2-command-invalid",
                    snapshot);
            }

            string rejectionCode;
            if (!Validate(candidate, out rejectionCode))
            {
                return new WeaponMountLoadoutImportResult(
                    false,
                    rejectionCode,
                    snapshot);
            }
            if (BindingsEqual(snapshot.Bindings, candidate.Bindings))
            {
                return new WeaponMountLoadoutImportResult(
                    true,
                    string.Empty,
                    snapshot);
            }
            snapshot = candidate;
            return new WeaponMountLoadoutImportResult(true, string.Empty, snapshot);
        }

        private bool Validate(
            WeaponMountLoadoutSnapshot candidate,
            out string rejectionCode)
        {
            rejectionCode = string.Empty;
            if (candidate == null
                || candidate.SchemaVersion
                    != WeaponMountLoadoutSnapshot.CurrentSchemaVersion
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
                WeaponMountBinding binding = candidate.Bindings[index];
                if (!expectedMountIds.Remove(binding.MountId))
                {
                    rejectionCode = "weapon-mount-loadout-v2-mount-not-owned-by-class";
                    return false;
                }
                WeaponMountPosition position =
                    WeaponMountPolicy.FindPosition(layout, binding.MountId);
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
            IReadOnlyList<WeaponMountBinding> left,
            IReadOnlyList<WeaponMountBinding> right)
        {
            if (left == null || right == null || left.Count != right.Count) return false;
            for (int index = 0; index < left.Count; index++)
            {
                if (!left[index].Equals(right[index])) return false;
            }
            return true;
        }

        private static IEnumerable<WeaponMountBinding> EmptyPhysicalBindings(
            WeaponMountLayout source)
        {
            var values = new List<WeaponMountBinding>(source.PhysicalPositions.Count);
            for (int index = 0; index < source.PhysicalPositions.Count; index++)
            {
                values.Add(new WeaponMountBinding(
                    source.PhysicalPositions[index].MountStableId,
                    null));
            }
            return values;
        }
    }

    public static class WeaponMountLoadoutView
    {
        public static WeaponMountLoadoutSnapshot MigrateLegacy(
            WeaponMountLayout layout,
            WeaponHoldingsState holdings,
            InventoryLoadoutStateSnapshot legacy)
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
            var bindings = new List<WeaponMountBinding>(
                layout.PhysicalPositions.Count);
            for (int index = 0; index < layout.PhysicalPositions.Count; index++)
            {
                WeaponMountPosition position =
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
                bindings.Add(new WeaponMountBinding(
                    position.MountStableId,
                    instanceId));
            }
            return WeaponMountLoadoutSnapshot.CreateCanonical(
                legacy.Sequence,
                bindings);
        }

        public static InventoryLoadoutStateSnapshot ToLegacyProjection(
            WeaponMountLayout layout,
            WeaponMountLoadoutSnapshot mounts,
            InventoryLoadoutStateSnapshot armorTemplate)
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

            var bindings = new List<InventoryLoadoutSlotBinding>(
                InventoryLoadoutSlots.All.Count);
            for (int index = 0; index < InventoryLoadoutSlots.All.Count; index++)
            {
                InventoryLoadoutSlotDescriptor slot = InventoryLoadoutSlots.All[index];
                StableId instanceId = slot.Kind == InventoryLoadoutSlotKind.Weapon
                    ? null
                    : armorTemplate.GetBinding(slot.SlotStableId)
                        .EquipmentInstanceStableId;
                bindings.Add(new InventoryLoadoutSlotBinding(
                    slot.SlotStableId,
                    instanceId));
            }

            for (int index = 0; index < layout.PhysicalPositions.Count; index++)
            {
                WeaponMountPosition position = layout.PhysicalPositions[index];
                WeaponMountBinding binding = mounts.Find(position.MountStableId);
                if (binding == null)
                {
                    throw new ArgumentException(
                        "The canonical loadout is missing a physical mount.",
                        nameof(mounts));
                }
                int slotIndex = FindLegacySlotIndex(position.LoadoutSlotStableId);
                bindings[slotIndex] = new InventoryLoadoutSlotBinding(
                    position.LoadoutSlotStableId,
                    position.IsActive ? binding.InstanceId : null);
            }
            return InventoryLoadoutStateSnapshot.CreateCanonical(
                mounts.Sequence,
                bindings);
        }

        public static InventoryLoadoutStateSnapshot ArmorOnly(
            InventoryLoadoutStateSnapshot source)
        {
            if (source == null || !source.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "A valid loadout snapshot is required.",
                    nameof(source));
            }
            var bindings = new List<InventoryLoadoutSlotBinding>(
                InventoryLoadoutSlots.All.Count);
            for (int index = 0; index < InventoryLoadoutSlots.All.Count; index++)
            {
                InventoryLoadoutSlotDescriptor slot = InventoryLoadoutSlots.All[index];
                bindings.Add(new InventoryLoadoutSlotBinding(
                    slot.SlotStableId,
                    slot.Kind == InventoryLoadoutSlotKind.Weapon
                        ? null
                        : source.GetBinding(slot.SlotStableId)
                            .EquipmentInstanceStableId));
            }
            return InventoryLoadoutStateSnapshot.CreateCanonical(
                source.Sequence,
                bindings);
        }

        public static PlayerRouteProfilePayload Route(
            StableId characterId,
            StableId loadoutProfileId,
            WeaponMountLayout layout,
            WeaponMountLoadoutSnapshot mounts)
        {
            InventoryLoadoutStateSnapshot emptyArmor =
                InventoryLoadoutStateSnapshot.CreateCanonical(
                    mounts.Sequence,
                    EmptyLegacyBindings());
            InventoryLoadoutStateSnapshot projection = ToLegacyProjection(
                layout,
                mounts,
                emptyArmor);
            return LegacyWeaponSetup.RouteFromLoadout(
                characterId,
                loadoutProfileId,
                projection);
        }

        private static int FindLegacySlotIndex(StableId slotId)
        {
            for (int index = 0; index < InventoryLoadoutSlots.All.Count; index++)
            {
                if (InventoryLoadoutSlots.All[index].SlotStableId == slotId) return index;
            }
            throw new InvalidOperationException(
                "A physical mount has no legacy route projection slot.");
        }

        private static IEnumerable<InventoryLoadoutSlotBinding> EmptyLegacyBindings()
        {
            var values = new List<InventoryLoadoutSlotBinding>(
                InventoryLoadoutSlots.All.Count);
            for (int index = 0; index < InventoryLoadoutSlots.All.Count; index++)
            {
                values.Add(new InventoryLoadoutSlotBinding(
                    InventoryLoadoutSlots.All[index].SlotStableId,
                    null));
            }
            return values;
        }
    }

    public sealed class WeaponMountLoadoutComponentCodec :
        ISaveComponentPayloadCodec<WeaponMountLoadoutSnapshot>
    {
        private const string Prefix = "weapon-mount-loadout-v2:";
        private const int MaximumMounts = 4;
        public string ContractId { get { return "weapon-mount-loadout-explicit-v2"; } }

        public string Encode(WeaponMountLoadoutSnapshot snapshot)
        {
            SaveComponentValidationResult validation = Validate(snapshot);
            if (!validation.Succeeded)
            {
                throw new ArgumentException(validation.RejectionCode, nameof(snapshot));
            }
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(WeaponMountLoadoutSnapshot.CurrentSchemaVersion);
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
            out WeaponMountLoadoutSnapshot snapshot,
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
                    if (schema != WeaponMountLoadoutSnapshot.CurrentSchemaVersion
                        || sequence < 0L
                        || count < 0
                        || count > MaximumMounts)
                    {
                        rejectionCode = "weapon-mount-loadout-v2-header-invalid";
                        return false;
                    }
                    var values = new List<WeaponMountBinding>(count);
                    for (int index = 0; index < count; index++)
                    {
                        StableId mountId = StableId.Parse(reader.ReadString());
                        StableId instanceId = reader.ReadBoolean()
                            ? StableId.Parse(reader.ReadString())
                            : null;
                        values.Add(new WeaponMountBinding(mountId, instanceId));
                    }
                    if (stream.Position != stream.Length)
                    {
                        rejectionCode = "weapon-mount-loadout-v2-payload-trailing-data";
                        return false;
                    }
                    snapshot = WeaponMountLoadoutSnapshot.CreateCanonical(
                        sequence,
                        values);
                }
                SaveComponentValidationResult validation = Validate(snapshot);
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

        public SaveComponentValidationResult Validate(
            WeaponMountLoadoutSnapshot snapshot)
        {
            return snapshot != null
                && snapshot.SchemaVersion
                    == WeaponMountLoadoutSnapshot.CurrentSchemaVersion
                && snapshot.HasValidFingerprint()
                && snapshot.Bindings.Count >= 2
                && snapshot.Bindings.Count <= MaximumMounts
                ? SaveComponentValidationResult.Accept()
                : SaveComponentValidationResult.Reject(
                    "weapon-mount-loadout-v2-snapshot-invalid");
        }
    }

    public static class WeaponMountLoadoutSaveComponent
    {
        private static readonly StableId ComponentId =
            StableId.Parse("save-component.weapon-mount-loadout");
        private static readonly WeaponMountLoadoutComponentCodec codec =
            new WeaponMountLoadoutComponentCodec();

        public static WeaponMountLoadoutComponentCodec Codec { get { return codec; } }

        public static SaveComponentDefinition Definition()
        {
            return new SaveComponentDefinition(
                ComponentId,
                2,
                codec.ContractId,
                false,
                26);
        }

        public static ISaveComponentBridge CreateAdapter(
            WeaponMountLoadoutState authority)
        {
            if (authority == null) throw new ArgumentNullException(nameof(authority));
            return new StateSnapshotSaveComponentBridge<
                WeaponMountLoadoutSnapshot>(
                Definition(),
                codec,
                authority.ExportSnapshot,
                codec.Validate,
                snapshot =>
                {
                    WeaponMountLoadoutImportResult result =
                        authority.ImportSnapshot(snapshot);
                    return result.Succeeded
                        ? SaveComponentApplyResult.Applied()
                        : SaveComponentApplyResult.Rejected(result.RejectionCode);
                });
        }

        public static bool TryRead(
            CharacterInstanceSnapshot character,
            out WeaponMountLoadoutSnapshot snapshot,
            out string rejectionCode)
        {
            snapshot = null;
            rejectionCode = string.Empty;
            if (character == null)
            {
                rejectionCode = "weapon-mount-loadout-v2-character-null";
                return false;
            }
            SaveComponentSnapshot component;
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
