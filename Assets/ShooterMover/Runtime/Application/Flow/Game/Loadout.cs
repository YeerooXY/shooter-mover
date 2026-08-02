using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Application.Persistence.SaveParts;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;

namespace ShooterMover.Application.Flow.Game
{
    public sealed class EquippedGun : IEquatable<EquippedGun>
    {
        public EquippedGun(StableId mountId, StableId instanceId)
        {
            MountId = mountId ?? throw new ArgumentNullException(nameof(mountId));
            InstanceId = instanceId;
        }

        public StableId MountId { get; }
        public StableId InstanceId { get; }

        public bool Equals(EquippedGun other)
        {
            return other != null
                && MountId == other.MountId
                && InstanceId == other.InstanceId;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as EquippedGun);
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
    /// Canonical gun loadout persistence. It contains exactly one record per physical class
    /// mount and never stores legacy generic gun-slot placeholders.
    /// </summary>
    public sealed class LoadoutSnapshot
    {
        public const int CurrentSchemaVersion = 2;
        private readonly ReadOnlyCollection<EquippedGun> bindings;

        private LoadoutSnapshot(
            long sequence,
            IEnumerable<EquippedGun> values,
            string fingerprint)
        {
            if (sequence < 0L) throw new ArgumentOutOfRangeException(nameof(sequence));
            Sequence = sequence;
            bindings = Canonicalize(values);
            Fingerprint = fingerprint ?? string.Empty;
        }

        public int SchemaVersion { get { return CurrentSchemaVersion; } }
        public long Sequence { get; }
        public IReadOnlyList<EquippedGun> Bindings { get { return bindings; } }
        public string Fingerprint { get; }

        public static LoadoutSnapshot CreateCanonical(
            long sequence,
            IEnumerable<EquippedGun> values)
        {
            var preliminary = new LoadoutSnapshot(
                sequence,
                values,
                string.Empty);
            return new LoadoutSnapshot(
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

        public EquippedGun Find(StableId mountId)
        {
            if (mountId == null) return null;
            for (int index = 0; index < bindings.Count; index++)
            {
                if (bindings[index].MountId == mountId) return bindings[index];
            }
            return null;
        }

        private static ReadOnlyCollection<EquippedGun> Canonicalize(
            IEnumerable<EquippedGun> source)
        {
            var copy = new List<EquippedGun>(
                source ?? throw new ArgumentNullException(nameof(source)));
            copy.Sort(delegate(EquippedGun left, EquippedGun right)
            {
                if (ReferenceEquals(left, null)) return -1;
                if (ReferenceEquals(right, null)) return 1;
                return left.MountId.CompareTo(right.MountId);
            });

            var mountIds = new HashSet<StableId>();
            var instanceIds = new HashSet<StableId>();
            for (int index = 0; index < copy.Count; index++)
            {
                EquippedGun binding = copy[index];
                if (binding == null || !mountIds.Add(binding.MountId))
                {
                    throw new ArgumentException(
                        "Gun mount loadouts require unique non-null mount identities.",
                        nameof(source));
                }
                if (binding.InstanceId != null && !instanceIds.Add(binding.InstanceId))
                {
                    throw new ArgumentException(
                        "An exact gun instance cannot occupy two physical mounts.",
                        nameof(source));
                }
            }
            return new ReadOnlyCollection<EquippedGun>(copy);
        }

        private static string ComputeFingerprint(
            long sequence,
            IReadOnlyList<EquippedGun> values)
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

    public sealed class LoadoutImportResult
    {
        public LoadoutImportResult(
            bool succeeded,
            string rejectionCode,
            LoadoutSnapshot snapshot)
        {
            Succeeded = succeeded;
            RejectionCode = rejectionCode ?? string.Empty;
            Snapshot = snapshot;
        }

        public bool Succeeded { get; }
        public string RejectionCode { get; }
        public LoadoutSnapshot Snapshot { get; }
    }

    public sealed class LoadoutState
    {
        private readonly GunSlots layout;
        private readonly GunInventoryState holdings;
        private LoadoutSnapshot snapshot;

        public LoadoutState(
            GunSlots mountLayout,
            GunInventoryState gunHoldings,
            LoadoutSnapshot initial)
        {
            layout = mountLayout ?? throw new ArgumentNullException(nameof(mountLayout));
            holdings = gunHoldings
                ?? throw new ArgumentNullException(nameof(gunHoldings));
            snapshot = LoadoutSnapshot.CreateCanonical(
                0L,
                EmptyPhysicalBindings(layout));
            LoadoutImportResult result = ImportSnapshot(
                initial ?? throw new ArgumentNullException(nameof(initial)));
            if (!result.Succeeded)
            {
                throw new ArgumentException(result.RejectionCode, nameof(initial));
            }
        }

        public long Sequence { get { return snapshot.Sequence; } }
        public LoadoutSnapshot ExportSnapshot() { return snapshot; }

        public LoadoutImportResult ImportSnapshot(
            LoadoutSnapshot imported)
        {
            string rejectionCode;
            if (!Validate(imported, out rejectionCode))
            {
                return new LoadoutImportResult(
                    false,
                    rejectionCode,
                    snapshot);
            }
            snapshot = LoadoutSnapshot.CreateCanonical(
                imported.Sequence,
                imported.Bindings);
            return new LoadoutImportResult(true, string.Empty, snapshot);
        }

        public LoadoutImportResult Apply(
            long expectedSequence,
            IEnumerable<EquippedGun> bindings)
        {
            if (expectedSequence != snapshot.Sequence)
            {
                return new LoadoutImportResult(
                    false,
                    "gun-mount-loadout-v2-sequence-stale",
                    snapshot);
            }

            LoadoutSnapshot candidate;
            try
            {
                candidate = LoadoutSnapshot.CreateCanonical(
                    checked(snapshot.Sequence + 1L),
                    bindings);
            }
            catch (Exception exception)
                when (exception is ArgumentException || exception is OverflowException)
            {
                return new LoadoutImportResult(
                    false,
                    "gun-mount-loadout-v2-command-invalid",
                    snapshot);
            }

            string rejectionCode;
            if (!Validate(candidate, out rejectionCode))
            {
                return new LoadoutImportResult(
                    false,
                    rejectionCode,
                    snapshot);
            }
            if (BindingsEqual(snapshot.Bindings, candidate.Bindings))
            {
                return new LoadoutImportResult(
                    true,
                    string.Empty,
                    snapshot);
            }
            snapshot = candidate;
            return new LoadoutImportResult(true, string.Empty, snapshot);
        }

        private bool Validate(
            LoadoutSnapshot candidate,
            out string rejectionCode)
        {
            rejectionCode = string.Empty;
            if (candidate == null
                || candidate.SchemaVersion
                    != LoadoutSnapshot.CurrentSchemaVersion
                || !candidate.HasValidFingerprint())
            {
                rejectionCode = "gun-mount-loadout-v2-snapshot-invalid";
                return false;
            }
            if (candidate.Bindings.Count != layout.PhysicalPositions.Count)
            {
                rejectionCode = "gun-mount-loadout-v2-physical-count-invalid";
                return false;
            }

            var expectedMountIds = new HashSet<StableId>();
            for (int index = 0; index < layout.PhysicalPositions.Count; index++)
            {
                expectedMountIds.Add(layout.PhysicalPositions[index].MountStableId);
            }
            for (int index = 0; index < candidate.Bindings.Count; index++)
            {
                EquippedGun binding = candidate.Bindings[index];
                if (!expectedMountIds.Remove(binding.MountId))
                {
                    rejectionCode = "gun-mount-loadout-v2-mount-not-owned-by-class";
                    return false;
                }
                GunSlot position =
                    GunMountPolicy.FindPosition(layout, binding.MountId);
                if (position == null)
                {
                    rejectionCode = "gun-mount-loadout-v2-mount-unresolved";
                    return false;
                }
                if (position.IsLockedBySkill && binding.InstanceId != null)
                {
                    rejectionCode = "gun-mount-loadout-v2-mount-locked-by-skill";
                    return false;
                }
                if (binding.InstanceId != null
                    && holdings.Find(binding.InstanceId) == null)
                {
                    rejectionCode = "gun-mount-loadout-v2-instance-not-owned";
                    return false;
                }
            }
            if (expectedMountIds.Count != 0)
            {
                rejectionCode = "gun-mount-loadout-v2-physical-mount-missing";
                return false;
            }
            return true;
        }

        private static bool BindingsEqual(
            IReadOnlyList<EquippedGun> left,
            IReadOnlyList<EquippedGun> right)
        {
            if (left == null || right == null || left.Count != right.Count) return false;
            for (int index = 0; index < left.Count; index++)
            {
                if (!left[index].Equals(right[index])) return false;
            }
            return true;
        }

        private static IEnumerable<EquippedGun> EmptyPhysicalBindings(
            GunSlots source)
        {
            var values = new List<EquippedGun>(source.PhysicalPositions.Count);
            for (int index = 0; index < source.PhysicalPositions.Count; index++)
            {
                values.Add(new EquippedGun(
                    source.PhysicalPositions[index].MountStableId,
                    null));
            }
            return values;
        }
    }

    public static class LoadoutView
    {
        public static PlayerRouteProfilePayload Route(
            StableId characterId,
            StableId loadoutProfileId,
            GunSlots layout,
            LoadoutSnapshot mounts)
        {
            if (characterId == null)
            {
                throw new ArgumentNullException(nameof(characterId));
            }
            if (loadoutProfileId == null)
            {
                throw new ArgumentNullException(nameof(loadoutProfileId));
            }
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }
            if (mounts == null || !mounts.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "A valid canonical gun mount snapshot is required.",
                    nameof(mounts));
            }
            if (mounts.Bindings.Count != layout.PhysicalPositions.Count)
            {
                throw new ArgumentException(
                    "The canonical gun mount count does not match the class layout.",
                    nameof(mounts));
            }

            var routeInstances = new StableId[
                PlayerRouteProfilePayload.GunSlotCount];
            for (int index = 0; index < layout.PhysicalPositions.Count; index++)
            {
                GunSlot position = layout.PhysicalPositions[index];
                EquippedGun binding = mounts.Find(position.MountStableId);
                if (binding == null)
                {
                    throw new ArgumentException(
                        "The canonical loadout is missing a physical mount.",
                        nameof(mounts));
                }
                int routeIndex = GunLoadoutSlotIds.IndexOf(
                    position.LoadoutSlotStableId);
                if (routeIndex < 0
                    || routeIndex >= routeInstances.Length)
                {
                    throw new InvalidOperationException(
                        "A physical gun mount has no route slot identity.");
                }
                routeInstances[routeIndex] = position.IsActive
                    ? binding.InstanceId
                    : null;
            }

            return PlayerRouteProfilePayload.Create(
                characterId,
                loadoutProfileId,
                routeInstances);
        }
    }

    public sealed class EquippedGunsCodec :
        ISavePartFormat<LoadoutSnapshot>
    {
        private const string Prefix = "gun-mount-loadout-v2:";
        private const int MaximumMounts = 4;
        public string ContractId { get { return "gun-mount-loadout-explicit-v2"; } }

        public string Encode(LoadoutSnapshot snapshot)
        {
            SavePartValidationResult validation = Validate(snapshot);
            if (!validation.Succeeded)
            {
                throw new ArgumentException(validation.RejectionCode, nameof(snapshot));
            }
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(LoadoutSnapshot.CurrentSchemaVersion);
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
            out LoadoutSnapshot snapshot,
            out string rejectionCode)
        {
            snapshot = null;
            rejectionCode = string.Empty;
            if (string.IsNullOrWhiteSpace(canonicalPayload)
                || !canonicalPayload.StartsWith(Prefix, StringComparison.Ordinal))
            {
                rejectionCode = "gun-mount-loadout-v2-payload-prefix-invalid";
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
                    if (schema != LoadoutSnapshot.CurrentSchemaVersion
                        || sequence < 0L
                        || count < 0
                        || count > MaximumMounts)
                    {
                        rejectionCode = "gun-mount-loadout-v2-header-invalid";
                        return false;
                    }
                    var values = new List<EquippedGun>(count);
                    for (int index = 0; index < count; index++)
                    {
                        StableId mountId = StableId.Parse(reader.ReadString());
                        StableId instanceId = reader.ReadBoolean()
                            ? StableId.Parse(reader.ReadString())
                            : null;
                        values.Add(new EquippedGun(mountId, instanceId));
                    }
                    if (stream.Position != stream.Length)
                    {
                        rejectionCode = "gun-mount-loadout-v2-payload-trailing-data";
                        return false;
                    }
                    snapshot = LoadoutSnapshot.CreateCanonical(
                        sequence,
                        values);
                }
                SavePartValidationResult validation = Validate(snapshot);
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
                rejectionCode = "gun-mount-loadout-v2-payload-invalid";
                return false;
            }
        }

        public SavePartValidationResult Validate(
            LoadoutSnapshot snapshot)
        {
            return snapshot != null
                && snapshot.SchemaVersion
                    == LoadoutSnapshot.CurrentSchemaVersion
                && snapshot.HasValidFingerprint()
                && snapshot.Bindings.Count >= 2
                && snapshot.Bindings.Count <= MaximumMounts
                ? SavePartValidationResult.Accept()
                : SavePartValidationResult.Reject(
                    "gun-mount-loadout-v2-snapshot-invalid");
        }
    }

    public static class LoadoutSavePart
    {
        private static readonly StableId ComponentId =
            StableId.Parse("save-part.gun-mount-loadout");
        private static readonly EquippedGunsCodec codec =
            new EquippedGunsCodec();

        public static EquippedGunsCodec Codec { get { return codec; } }

        public static SavePartDefinition Definition()
        {
            return new SavePartDefinition(
                ComponentId,
                2,
                codec.ContractId,
                false,
                26);
        }

        public static ISavePart CreateAdapter(
            LoadoutState authority)
        {
            if (authority == null) throw new ArgumentNullException(nameof(authority));
            return new SnapshotSavePart<
                LoadoutSnapshot>(
                Definition(),
                codec,
                authority.ExportSnapshot,
                codec.Validate,
                snapshot =>
                {
                    LoadoutImportResult result =
                        authority.ImportSnapshot(snapshot);
                    return result.Succeeded
                        ? SavePartApplyResult.Applied()
                        : SavePartApplyResult.Rejected(result.RejectionCode);
                });
        }

        public static bool TryRead(
            CharacterInstanceSnapshot character,
            out LoadoutSnapshot snapshot,
            out string rejectionCode)
        {
            snapshot = null;
            rejectionCode = string.Empty;
            if (character == null)
            {
                rejectionCode = "gun-mount-loadout-v2-character-null";
                return false;
            }
            SavePartSnapshot component;
            if (!character.TryGetComponent(ComponentId, out component)) return false;
            if (component.SchemaVersion != 2
                || !string.Equals(
                    component.ContentVersion,
                    codec.ContractId,
                    StringComparison.Ordinal))
            {
                rejectionCode =
                    "gun-mount-loadout-v2-component-version-unsupported";
                return false;
            }
            return codec.TryDecode(
                component.CanonicalPayload,
                out snapshot,
                out rejectionCode);
        }
    }
}
