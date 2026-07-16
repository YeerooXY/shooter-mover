using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.ContentPackages.Weapons.ArcGun;
using ShooterMover.ContentPackages.Weapons.BlasterMachineGun;
using ShooterMover.ContentPackages.Weapons.RicochetGun.Runtime;
using ShooterMover.ContentPackages.Weapons.RocketLauncher.Runtime;
using ShooterMover.ContentPackages.Weapons.Shotgun;
using ShooterMover.ContentPackages.Weapons.Stage1;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Identity;
using ShooterMover.Domain.Common;

namespace ShooterMover.ContentPackages.Weapons.Stage1Loadouts
{
    public sealed class Stage1WeaponLoadoutSlot : IEquatable<Stage1WeaponLoadoutSlot>
    {
        private Stage1WeaponLoadoutSlot(WeaponMountSlot slot, StableId weaponId)
        {
            if (!Enum.IsDefined(typeof(WeaponMountSlot), slot))
            {
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unknown four-mount slot.");
            }

            Slot = slot;
            WeaponId = weaponId ?? throw new ArgumentNullException(nameof(weaponId));
        }

        public WeaponMountSlot Slot { get; }

        public StableId WeaponId { get; }

        public static Stage1WeaponLoadoutSlot Create(WeaponMountSlot slot, StableId weaponId)
        {
            return new Stage1WeaponLoadoutSlot(slot, weaponId);
        }

        public string ToCanonicalString()
        {
            return "slot="
                + ((int)Slot).ToString(CultureInfo.InvariantCulture)
                + "\nweapon_id="
                + WeaponId;
        }

        public bool Equals(Stage1WeaponLoadoutSlot other)
        {
            return !ReferenceEquals(other, null)
                && Slot == other.Slot
                && WeaponId.Equals(other.WeaponId);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Stage1WeaponLoadoutSlot);
        }

        public override int GetHashCode()
        {
            return DeterministicText.OrdinalHash(ToCanonicalString());
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }
    }

    /// <summary>
    /// Immutable evidence-only four-slot comparison. It contains identities only:
    /// package tuning and behavior descriptors remain the package-owned source of truth.
    /// </summary>
    public sealed class Stage1WeaponLoadoutFixture : IEquatable<Stage1WeaponLoadoutFixture>
    {
        public const int CurrentFixtureVersion = 1;

        private readonly ReadOnlyCollection<Stage1WeaponLoadoutSlot> slots;
        private readonly string canonicalText;

        private Stage1WeaponLoadoutFixture(
            StableId fixtureId,
            IEnumerable<Stage1WeaponLoadoutSlot> sourceSlots)
        {
            FixtureId = fixtureId ?? throw new ArgumentNullException(nameof(fixtureId));
            slots = CopyAndValidateSlots(sourceSlots);
            canonicalText = BuildCanonicalText();
            Checksum = DeterministicText.ComputeSha256(canonicalText);
        }

        public StableId FixtureId { get; }

        public int FixtureVersion
        {
            get { return CurrentFixtureVersion; }
        }

        public int Count
        {
            get { return WeaponMountContractRules.MountCount; }
        }

        public IReadOnlyList<Stage1WeaponLoadoutSlot> Slots
        {
            get { return slots; }
        }

        public string Checksum { get; }

        public static Stage1WeaponLoadoutFixture Create(
            StableId fixtureId,
            IEnumerable<Stage1WeaponLoadoutSlot> slots)
        {
            return new Stage1WeaponLoadoutFixture(fixtureId, slots);
        }

        public Stage1WeaponLoadoutSlot GetByHudIndex(int hudIndex)
        {
            if (hudIndex < 0 || hudIndex >= slots.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(hudIndex));
            }

            return slots[hudIndex];
        }

        public Stage1WeaponLoadoutSlot GetBySlot(WeaponMountSlot slot)
        {
            return slots[WeaponMountContractRules.GetHudIndex(slot)];
        }

        public bool ContainsWeapon(StableId weaponId)
        {
            if (weaponId == null)
            {
                throw new ArgumentNullException(nameof(weaponId));
            }

            for (int index = 0; index < slots.Count; index++)
            {
                if (slots[index].WeaponId.Equals(weaponId))
                {
                    return true;
                }
            }

            return false;
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(Stage1WeaponLoadoutFixture other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Stage1WeaponLoadoutFixture);
        }

        public override int GetHashCode()
        {
            return DeterministicText.OrdinalHash(canonicalText);
        }

        public override string ToString()
        {
            return canonicalText;
        }

        private static ReadOnlyCollection<Stage1WeaponLoadoutSlot> CopyAndValidateSlots(
            IEnumerable<Stage1WeaponLoadoutSlot> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            Stage1WeaponLoadoutSlot[] canonical =
                new Stage1WeaponLoadoutSlot[WeaponMountContractRules.MountCount];

            int suppliedCount = 0;
            foreach (Stage1WeaponLoadoutSlot slot in source)
            {
                suppliedCount++;
                if (slot == null)
                {
                    throw new ArgumentException("Loadout slots cannot contain null.", nameof(source));
                }

                if (!IsApprovedWeaponId(slot.WeaponId))
                {
                    throw new ArgumentException(
                        "Loadout slots may resolve only approved Stage 1 weapon IDs.",
                        nameof(source));
                }

                int hudIndex = WeaponMountContractRules.GetHudIndex(slot.Slot);
                if (canonical[hudIndex] != null)
                {
                    throw new ArgumentException(
                        "Each stable mount slot must appear exactly once.",
                        nameof(source));
                }

                canonical[hudIndex] = slot;
            }

            if (suppliedCount != WeaponMountContractRules.MountCount)
            {
                throw new ArgumentException(
                    "Exactly four loadout slots are required.",
                    nameof(source));
            }

            for (int index = 0; index < canonical.Length; index++)
            {
                if (canonical[index] == null)
                {
                    throw new ArgumentException(
                        "Each stable mount slot must appear exactly once.",
                        nameof(source));
                }
            }

            return new ReadOnlyCollection<Stage1WeaponLoadoutSlot>(canonical);
        }

        private static bool IsApprovedWeaponId(StableId weaponId)
        {
            if (weaponId == null)
            {
                return false;
            }

            IReadOnlyList<StableId> approved =
                Stage1WeaponPackageDescriptor.AcceptedWeaponIds;
            for (int index = 0; index < approved.Count; index++)
            {
                if (approved[index].Equals(weaponId))
                {
                    return true;
                }
            }

            return false;
        }

        private string BuildCanonicalText()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("schema=shooter-mover.stage1-weapon-loadout")
                .Append("\nversion=")
                .Append(CurrentFixtureVersion.ToString(CultureInfo.InvariantCulture))
                .Append("\nfixture_id=")
                .Append(FixtureId)
                .Append("\nslot_count=")
                .Append(WeaponMountContractRules.MountCount.ToString(CultureInfo.InvariantCulture));

            for (int index = 0; index < slots.Count; index++)
            {
                Stage1WeaponLoadoutSlot slot = slots[index];
                builder.Append("\nslot_")
                    .Append((index + 1).ToString("D2", CultureInfo.InvariantCulture))
                    .Append('=')
                    .Append(slot.WeaponId);
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// The accepted five-descriptor roster plus documented fixed comparisons.
    /// It is immutable and has no inventory, reward, economy, scene, or save authority.
    /// </summary>
    public sealed class Stage1WeaponLoadoutCatalog
    {
        public const string DefaultFixtureIdText = "loadout.stage1-default-comparison";
        public const string RicochetFixtureIdText = "loadout.stage1-ricochet-comparison";

        private static readonly Stage1WeaponLoadoutCatalog ApprovedValue = CreateApproved();

        private readonly Stage1WeaponPackageValidationResult packageValidation;
        private readonly ReadOnlyCollection<Stage1WeaponLoadoutFixture> fixedFixtures;

        private Stage1WeaponLoadoutCatalog(
            Stage1WeaponPackageValidationResult packageValidation,
            IEnumerable<Stage1WeaponLoadoutFixture> fixtures)
        {
            this.packageValidation =
                packageValidation ?? throw new ArgumentNullException(nameof(packageValidation));
            if (!packageValidation.IsValid)
            {
                throw new InvalidOperationException(
                    "Stage 1 loadouts require the exact validated five-package roster.");
            }

            fixedFixtures = CopyAndValidateFixtures(fixtures);
            ValidateMatrix();
        }

        public static Stage1WeaponLoadoutCatalog Approved
        {
            get { return ApprovedValue; }
        }

        public IReadOnlyList<Stage1WeaponPackageDescriptor> PackageDescriptors
        {
            get { return packageValidation.Packages; }
        }

        public IReadOnlyList<Stage1WeaponLoadoutFixture> FixedFixtures
        {
            get { return fixedFixtures; }
        }

        public Stage1WeaponLoadoutFixture DefaultFixture
        {
            get { return GetFixedFixture(StableId.Parse(DefaultFixtureIdText)); }
        }

        public Stage1WeaponPackageDescriptor ResolveDescriptor(StableId weaponId)
        {
            if (weaponId == null)
            {
                throw new ArgumentNullException(nameof(weaponId));
            }

            Stage1WeaponPackageDescriptor descriptor;
            if (!packageValidation.TryGetPackage(weaponId, out descriptor))
            {
                throw new KeyNotFoundException(
                    "Unknown Stage 1 weapon package ID: " + weaponId);
            }

            return descriptor;
        }

        public Stage1WeaponLoadoutFixture GetFixedFixture(StableId fixtureId)
        {
            if (fixtureId == null)
            {
                throw new ArgumentNullException(nameof(fixtureId));
            }

            for (int index = 0; index < fixedFixtures.Count; index++)
            {
                if (fixedFixtures[index].FixtureId.Equals(fixtureId))
                {
                    return fixedFixtures[index];
                }
            }

            throw new KeyNotFoundException("Unknown Stage 1 loadout fixture ID: " + fixtureId);
        }

        internal Stage1WeaponLoadoutFixture CreateSeededFixture(
            int runSeed,
            BuildIdentity buildIdentity,
            ContentVersion contentVersion)
        {
            if (runSeed <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(runSeed),
                    runSeed,
                    "Evidence run seeds must be positive.");
            }

            if (buildIdentity == null)
            {
                throw new ArgumentNullException(nameof(buildIdentity));
            }

            if (contentVersion == null)
            {
                throw new ArgumentNullException(nameof(contentVersion));
            }

            if (!string.Equals(
                    buildIdentity.ContentFingerprint,
                    contentVersion.DefinitionFingerprint,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Build identity and content version must identify the same content fingerprint.");
            }

            List<StableId> candidates = new List<StableId>
            {
                Stage1WeaponPackageDescriptor.ShotgunId,
                Stage1WeaponPackageDescriptor.RocketLauncherId,
                Stage1WeaponPackageDescriptor.ArcGunId,
                Stage1WeaponPackageDescriptor.RicochetGunId,
            };

            string seedMaterial = "schema=shooter-mover.stage1-loadout-seed"
                + "\nversion=1"
                + "\nrun_seed="
                + runSeed.ToString(CultureInfo.InvariantCulture)
                + "\nbuild_identity:\n"
                + buildIdentity.ToCanonicalString()
                + "\ncontent_version:\n"
                + contentVersion.ToCanonicalString();
            DeterministicIndexStream stream = new DeterministicIndexStream(seedMaterial);
            for (int index = candidates.Count - 1; index > 0; index--)
            {
                int selected = stream.NextIndex(index + 1);
                StableId swap = candidates[index];
                candidates[index] = candidates[selected];
                candidates[selected] = swap;
            }

            string seedHash = DeterministicText.ComputeSha256Hex(seedMaterial);
            StableId fixtureId = StableId.Create(
                "loadout",
                "stage1-seed-"
                + runSeed.ToString(CultureInfo.InvariantCulture)
                + "-"
                + seedHash.Substring(0, 12));

            return Stage1WeaponLoadoutFixture.Create(
                fixtureId,
                new[]
                {
                    Stage1WeaponLoadoutSlot.Create(
                        WeaponMountSlot.MountOne,
                        Stage1WeaponPackageDescriptor.BlasterMachineGunId),
                    Stage1WeaponLoadoutSlot.Create(
                        WeaponMountSlot.MountTwo,
                        candidates[0]),
                    Stage1WeaponLoadoutSlot.Create(
                        WeaponMountSlot.MountThree,
                        candidates[1]),
                    Stage1WeaponLoadoutSlot.Create(
                        WeaponMountSlot.MountFour,
                        candidates[2]),
                });
        }

        private static Stage1WeaponLoadoutCatalog CreateApproved()
        {
            Stage1WeaponPackageDescriptor[] descriptors =
            {
                BlasterMachineGunPackage.CreateDescriptor(),
                ShotgunPackageDefinition.CreateDefaultDescriptor(),
                RocketLauncherPackage.Descriptor,
                ArcGunPackage.CreateDescriptor(),
                RicochetGunPackage.CreateDescriptor(),
            };
            Stage1WeaponPackageValidationResult validation =
                Stage1WeaponPackageValidator.Validate(descriptors);

            Stage1WeaponLoadoutFixture[] fixtures =
            {
                CreateFixedFixture(
                    DefaultFixtureIdText,
                    Stage1WeaponPackageDescriptor.BlasterMachineGunId,
                    Stage1WeaponPackageDescriptor.ShotgunId,
                    Stage1WeaponPackageDescriptor.RocketLauncherId,
                    Stage1WeaponPackageDescriptor.ArcGunId),
                CreateFixedFixture(
                    RicochetFixtureIdText,
                    Stage1WeaponPackageDescriptor.BlasterMachineGunId,
                    Stage1WeaponPackageDescriptor.RicochetGunId,
                    Stage1WeaponPackageDescriptor.ShotgunId,
                    Stage1WeaponPackageDescriptor.RocketLauncherId),
            };

            return new Stage1WeaponLoadoutCatalog(validation, fixtures);
        }

        private static Stage1WeaponLoadoutFixture CreateFixedFixture(
            string fixtureId,
            StableId mountOne,
            StableId mountTwo,
            StableId mountThree,
            StableId mountFour)
        {
            return Stage1WeaponLoadoutFixture.Create(
                StableId.Parse(fixtureId),
                new[]
                {
                    Stage1WeaponLoadoutSlot.Create(WeaponMountSlot.MountOne, mountOne),
                    Stage1WeaponLoadoutSlot.Create(WeaponMountSlot.MountTwo, mountTwo),
                    Stage1WeaponLoadoutSlot.Create(WeaponMountSlot.MountThree, mountThree),
                    Stage1WeaponLoadoutSlot.Create(WeaponMountSlot.MountFour, mountFour),
                });
        }

        private static ReadOnlyCollection<Stage1WeaponLoadoutFixture> CopyAndValidateFixtures(
            IEnumerable<Stage1WeaponLoadoutFixture> fixtures)
        {
            if (fixtures == null)
            {
                throw new ArgumentNullException(nameof(fixtures));
            }

            List<Stage1WeaponLoadoutFixture> copy = new List<Stage1WeaponLoadoutFixture>();
            HashSet<StableId> fixtureIds = new HashSet<StableId>();
            foreach (Stage1WeaponLoadoutFixture fixture in fixtures)
            {
                if (fixture == null)
                {
                    throw new ArgumentException("Fixed fixtures cannot contain null.", nameof(fixtures));
                }

                if (!fixtureIds.Add(fixture.FixtureId))
                {
                    throw new ArgumentException(
                        "Fixed fixture IDs must be unique.",
                        nameof(fixtures));
                }

                copy.Add(fixture);
            }

            if (copy.Count == 0)
            {
                throw new ArgumentException(
                    "At least one fixed Stage 1 comparison is required.",
                    nameof(fixtures));
            }

            return new ReadOnlyCollection<Stage1WeaponLoadoutFixture>(copy);
        }

        private void ValidateMatrix()
        {
            Stage1WeaponLoadoutFixture defaultFixture = GetFixedFixture(
                StableId.Parse(DefaultFixtureIdText));
            if (!defaultFixture.ContainsWeapon(
                    Stage1WeaponPackageDescriptor.BlasterMachineGunId))
            {
                throw new InvalidOperationException(
                    "The default comparison must include the Blaster Machine Gun.");
            }

            HashSet<StableId> covered = new HashSet<StableId>();
            for (int fixtureIndex = 0; fixtureIndex < fixedFixtures.Count; fixtureIndex++)
            {
                Stage1WeaponLoadoutFixture fixture = fixedFixtures[fixtureIndex];
                for (int slotIndex = 0; slotIndex < fixture.Count; slotIndex++)
                {
                    StableId weaponId = fixture.GetByHudIndex(slotIndex).WeaponId;
                    ResolveDescriptor(weaponId);
                    covered.Add(weaponId);
                }
            }

            IReadOnlyList<StableId> approved =
                Stage1WeaponPackageDescriptor.AcceptedWeaponIds;
            for (int index = 0; index < approved.Count; index++)
            {
                if (!covered.Contains(approved[index]))
                {
                    throw new InvalidOperationException(
                        "The fixed comparison matrix must expose every approved weapon identity.");
                }
            }
        }
    }

    /// <summary>
    /// One non-persistent evidence-session wrapper. It binds selection to accepted
    /// build/content identity and a supplied EH-002 run seed, then exposes immutable IDs.
    /// </summary>
    public sealed class Stage1WeaponLoadoutEvidenceSession :
        IEquatable<Stage1WeaponLoadoutEvidenceSession>
    {
        public const int CurrentSessionVersion = 1;

        private readonly string canonicalText;

        private Stage1WeaponLoadoutEvidenceSession(
            int runSeed,
            BuildIdentity buildIdentity,
            ContentVersion contentVersion)
        {
            RunSeed = runSeed;
            BuildIdentity = buildIdentity ?? throw new ArgumentNullException(nameof(buildIdentity));
            ContentVersion = contentVersion ?? throw new ArgumentNullException(nameof(contentVersion));
            Loadout = Stage1WeaponLoadoutCatalog.Approved.CreateSeededFixture(
                runSeed,
                buildIdentity,
                contentVersion);
            canonicalText = BuildCanonicalText();
            Checksum = DeterministicText.ComputeSha256(canonicalText);
        }

        public int SessionVersion
        {
            get { return CurrentSessionVersion; }
        }

        public int RunSeed { get; }

        public BuildIdentity BuildIdentity { get; }

        public ContentVersion ContentVersion { get; }

        public Stage1WeaponLoadoutFixture Loadout { get; }

        public string Checksum { get; }

        public bool HasPersistentInventory
        {
            get { return false; }
        }

        public bool CanPersistRewards
        {
            get { return false; }
        }

        public bool CreatesRandomizedModifiers
        {
            get { return false; }
        }

        public static Stage1WeaponLoadoutEvidenceSession Create(
            int runSeed,
            BuildIdentity buildIdentity,
            ContentVersion contentVersion)
        {
            return new Stage1WeaponLoadoutEvidenceSession(
                runSeed,
                buildIdentity,
                contentVersion);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(Stage1WeaponLoadoutEvidenceSession other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Stage1WeaponLoadoutEvidenceSession);
        }

        public override int GetHashCode()
        {
            return DeterministicText.OrdinalHash(canonicalText);
        }

        public override string ToString()
        {
            return canonicalText;
        }

        private string BuildCanonicalText()
        {
            return "schema=shooter-mover.stage1-loadout-evidence-session"
                + "\nversion="
                + CurrentSessionVersion.ToString(CultureInfo.InvariantCulture)
                + "\nrun_seed="
                + RunSeed.ToString(CultureInfo.InvariantCulture)
                + "\npersistent_inventory=false"
                + "\nreward_persistence=false"
                + "\nrandomized_modifiers=false"
                + "\nbuild_identity:\n"
                + BuildIdentity.ToCanonicalString()
                + "\ncontent_version:\n"
                + ContentVersion.ToCanonicalString()
                + "\nloadout:\n"
                + Loadout.ToCanonicalString();
        }
    }

    internal sealed class DeterministicIndexStream
    {
        private readonly string seedMaterial;
        private int counter;

        public DeterministicIndexStream(string seedMaterial)
        {
            this.seedMaterial =
                seedMaterial ?? throw new ArgumentNullException(nameof(seedMaterial));
        }

        public int NextIndex(int exclusiveUpperBound)
        {
            if (exclusiveUpperBound <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(exclusiveUpperBound));
            }

            string input = seedMaterial
                + "\ncounter="
                + counter.ToString(CultureInfo.InvariantCulture);
            counter++;
            byte[] digest = DeterministicText.ComputeSha256Bytes(input);
            uint value = (uint)digest[0]
                | ((uint)digest[1] << 8)
                | ((uint)digest[2] << 16)
                | ((uint)digest[3] << 24);
            return (int)(value % (uint)exclusiveUpperBound);
        }
    }

    internal static class DeterministicText
    {
        public static string ComputeSha256(string value)
        {
            return "sha256:" + ComputeSha256Hex(value);
        }

        public static string ComputeSha256Hex(string value)
        {
            byte[] digest = ComputeSha256Bytes(value);
            StringBuilder builder = new StringBuilder(digest.Length * 2);
            for (int index = 0; index < digest.Length; index++)
            {
                builder.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        public static byte[] ComputeSha256Bytes(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            using (SHA256 sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
            }
        }

        public static int OrdinalHash(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            unchecked
            {
                uint hash = 2166136261u;
                for (int index = 0; index < value.Length; index++)
                {
                    hash ^= value[index];
                    hash *= 16777619u;
                }

                return (int)hash;
            }
        }
    }
}
