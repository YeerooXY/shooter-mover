using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Contracts.Flow.Session
{
    public enum HubRoute
    {
        MainMenu = 1,
        CharacterSelect = 2,
        InventoryLoadoutHub = 3,
        Inventory = 4,
        Skills = 5,
        Shop = 6,
        Crafting = 7,
        Play = 8,
    }

    public enum PlayerRouteProfileValidationStatus
    {
        Valid = 1,
        NullEnvelope = 2,
        UnsupportedSchemaVersion = 3,
        MissingContractIdentity = 4,
        MalformedContractIdentity = 5,
        ContractIdentityMismatch = 6,
        MissingCharacterIdentity = 7,
        MalformedCharacterIdentity = 8,
        MissingLoadoutProfileIdentity = 9,
        MalformedLoadoutProfileIdentity = 10,
        MissingWeaponSlots = 11,
        WeaponSlotCountMismatch = 12,
        NullWeaponSlot = 13,
        MissingWeaponSlotIdentity = 14,
        MalformedWeaponSlotIdentity = 15,
        DuplicateWeaponSlotIdentity = 16,
        UnexpectedWeaponSlotIdentity = 17,
        MissingEquipmentInstanceIdentity = 18,
        MalformedEquipmentInstanceIdentity = 19,
        DuplicateEquipmentInstanceIdentity = 20,
        MissingFingerprint = 21,
        FingerprintMismatch = 22,
    }

    /// <summary>
    /// Raw persistence/navigation envelope. A null equipment identity means the physical
    /// position is intentionally unbound. Route payloads describe navigation and bindings;
    /// they never imply inventory ownership.
    /// </summary>
    public sealed class PlayerRouteWeaponSlotEnvelope
    {
        public PlayerRouteWeaponSlotEnvelope(
            string weaponSlotStableId,
            string equipmentInstanceStableId)
        {
            WeaponSlotStableId = weaponSlotStableId;
            EquipmentInstanceStableId = equipmentInstanceStableId;
        }

        public string WeaponSlotStableId { get; }
        public string EquipmentInstanceStableId { get; }
    }

    public sealed class PlayerRouteProfileEnvelope
    {
        private readonly ReadOnlyCollection<PlayerRouteWeaponSlotEnvelope>
            weaponSlots;

        public PlayerRouteProfileEnvelope(
            int schemaVersion,
            string contractStableId,
            string selectedCharacterStableId,
            string loadoutProfileStableId,
            IEnumerable<PlayerRouteWeaponSlotEnvelope> weaponSlots,
            string fingerprint)
        {
            SchemaVersion = schemaVersion;
            ContractStableId = contractStableId;
            SelectedCharacterStableId = selectedCharacterStableId;
            LoadoutProfileStableId = loadoutProfileStableId;
            this.weaponSlots = weaponSlots == null
                ? null
                : new ReadOnlyCollection<PlayerRouteWeaponSlotEnvelope>(
                    new List<PlayerRouteWeaponSlotEnvelope>(weaponSlots));
            Fingerprint = fingerprint;
        }

        public int SchemaVersion { get; }
        public string ContractStableId { get; }
        public string SelectedCharacterStableId { get; }
        public string LoadoutProfileStableId { get; }
        public IReadOnlyList<PlayerRouteWeaponSlotEnvelope> WeaponSlots
        {
            get { return weaponSlots; }
        }
        public string Fingerprint { get; }
    }

    public sealed class PlayerRouteWeaponSlot :
        IEquatable<PlayerRouteWeaponSlot>
    {
        internal PlayerRouteWeaponSlot(
            StableId weaponSlotStableId,
            StableId equipmentInstanceStableId)
        {
            WeaponSlotStableId = weaponSlotStableId
                ?? throw new ArgumentNullException(nameof(weaponSlotStableId));
            EquipmentInstanceStableId = equipmentInstanceStableId;
        }

        public StableId WeaponSlotStableId { get; }
        public StableId EquipmentInstanceStableId { get; }
        public bool IsBound
        {
            get { return EquipmentInstanceStableId != null; }
        }

        public bool Equals(PlayerRouteWeaponSlot other)
        {
            return !ReferenceEquals(other, null)
                && WeaponSlotStableId == other.WeaponSlotStableId
                && EquipmentInstanceStableId
                    == other.EquipmentInstanceStableId;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PlayerRouteWeaponSlot);
        }

        public override int GetHashCode()
        {
            return PlayerRouteProfilePayload.OrdinalHash(
                ToCanonicalString());
        }

        public string ToCanonicalString()
        {
            return WeaponSlotStableId
                + "|"
                + (EquipmentInstanceStableId == null
                    ? "unbound"
                    : EquipmentInstanceStableId.ToString());
        }
    }

    public sealed class PlayerRouteProfileValidationResult
    {
        private PlayerRouteProfileValidationResult(
            PlayerRouteProfileValidationStatus status,
            string rejectionCode,
            PlayerRouteProfilePayload payload)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            Payload = payload;
        }

        public PlayerRouteProfileValidationStatus Status { get; }
        public string RejectionCode { get; }
        public PlayerRouteProfilePayload Payload { get; }
        public bool IsValid
        {
            get { return Status == PlayerRouteProfileValidationStatus.Valid; }
        }

        internal static PlayerRouteProfileValidationResult Accept(
            PlayerRouteProfilePayload payload)
        {
            return new PlayerRouteProfileValidationResult(
                PlayerRouteProfileValidationStatus.Valid,
                string.Empty,
                payload ?? throw new ArgumentNullException(nameof(payload)));
        }

        internal static PlayerRouteProfileValidationResult Reject(
            PlayerRouteProfileValidationStatus status,
            string rejectionCode)
        {
            return new PlayerRouteProfileValidationResult(
                status,
                rejectionCode ?? string.Empty,
                null);
        }
    }

    /// <summary>
    /// Immutable route payload shared by Hub destinations. It retains four stable physical
    /// positions while character policy decides which positions are available. All four may
    /// be unbound during character selection; onboarding creates ownership separately.
    /// </summary>
    public sealed class PlayerRouteProfilePayload :
        IEquatable<PlayerRouteProfilePayload>
    {
        public const int CurrentSchemaVersion = 1;
        public const int WeaponSlotCount = 4;
        public const string CurrentContractStableIdText =
            "route-profile.player-v1";

        private static readonly ReadOnlyCollection<StableId>
            expectedWeaponSlotIds =
                new ReadOnlyCollection<StableId>(new List<StableId>
                {
                    StableId.Parse("weapon-slot.slot-1"),
                    StableId.Parse("weapon-slot.slot-2"),
                    StableId.Parse("weapon-slot.slot-3"),
                    StableId.Parse("weapon-slot.slot-4"),
                });

        private readonly ReadOnlyCollection<PlayerRouteWeaponSlot>
            weaponSlots;
        private readonly string canonicalText;

        private PlayerRouteProfilePayload(
            StableId selectedCharacterStableId,
            StableId loadoutProfileStableId,
            IEnumerable<PlayerRouteWeaponSlot> weaponSlots)
        {
            SchemaVersion = CurrentSchemaVersion;
            ContractStableId = StableId.Parse(CurrentContractStableIdText);
            SelectedCharacterStableId = selectedCharacterStableId
                ?? throw new ArgumentNullException(
                    nameof(selectedCharacterStableId));
            LoadoutProfileStableId = loadoutProfileStableId
                ?? throw new ArgumentNullException(
                    nameof(loadoutProfileStableId));
            this.weaponSlots =
                new ReadOnlyCollection<PlayerRouteWeaponSlot>(
                    new List<PlayerRouteWeaponSlot>(
                        weaponSlots
                        ?? throw new ArgumentNullException(
                            nameof(weaponSlots))));
            canonicalText = BuildCanonicalText(
                SchemaVersion,
                ContractStableId,
                SelectedCharacterStableId,
                LoadoutProfileStableId,
                this.weaponSlots);
            Fingerprint = ComputeFingerprint(canonicalText);
        }

        public int SchemaVersion { get; }
        public StableId ContractStableId { get; }
        public StableId SelectedCharacterStableId { get; }
        public StableId LoadoutProfileStableId { get; }
        public IReadOnlyList<PlayerRouteWeaponSlot> WeaponSlots
        {
            get { return weaponSlots; }
        }
        public string Fingerprint { get; }
        public static IReadOnlyList<StableId> ExpectedWeaponSlotIds
        {
            get { return expectedWeaponSlotIds; }
        }

        public static PlayerRouteProfilePayload Create(
            StableId selectedCharacterStableId,
            StableId loadoutProfileStableId,
            IEnumerable<StableId> orderedEquipmentInstanceStableIds)
        {
            if (selectedCharacterStableId == null)
            {
                throw new ArgumentNullException(
                    nameof(selectedCharacterStableId));
            }
            if (loadoutProfileStableId == null)
            {
                throw new ArgumentNullException(
                    nameof(loadoutProfileStableId));
            }
            if (orderedEquipmentInstanceStableIds == null)
            {
                throw new ArgumentNullException(
                    nameof(orderedEquipmentInstanceStableIds));
            }

            var instances = new List<StableId>(
                orderedEquipmentInstanceStableIds);
            if (instances.Count != WeaponSlotCount)
            {
                throw new ArgumentException(
                    "Exactly four ordered weapon-position bindings are required.",
                    nameof(orderedEquipmentInstanceStableIds));
            }

            var seenInstances = new HashSet<StableId>();
            var slots = new List<PlayerRouteWeaponSlot>(WeaponSlotCount);
            for (int index = 0; index < WeaponSlotCount; index++)
            {
                StableId instanceStableId = instances[index];
                if (instanceStableId != null
                    && !seenInstances.Add(instanceStableId))
                {
                    throw new ArgumentException(
                        "Bound equipment-instance identities must be unique.",
                        nameof(orderedEquipmentInstanceStableIds));
                }
                slots.Add(new PlayerRouteWeaponSlot(
                    expectedWeaponSlotIds[index],
                    instanceStableId));
            }

            return new PlayerRouteProfilePayload(
                selectedCharacterStableId,
                loadoutProfileStableId,
                slots);
        }

        public static PlayerRouteProfileValidationResult TryImport(
            PlayerRouteProfileEnvelope envelope)
        {
            if (envelope == null)
            {
                return Reject(
                    PlayerRouteProfileValidationStatus.NullEnvelope,
                    "route-profile-envelope-null");
            }
            if (envelope.SchemaVersion != CurrentSchemaVersion)
            {
                return Reject(
                    PlayerRouteProfileValidationStatus
                        .UnsupportedSchemaVersion,
                    "route-profile-schema-unsupported");
            }
            if (string.IsNullOrWhiteSpace(envelope.ContractStableId))
            {
                return Reject(
                    PlayerRouteProfileValidationStatus
                        .MissingContractIdentity,
                    "route-profile-contract-missing");
            }

            StableId contractStableId;
            if (!StableId.TryParse(
                    envelope.ContractStableId,
                    out contractStableId))
            {
                return Reject(
                    PlayerRouteProfileValidationStatus
                        .MalformedContractIdentity,
                    "route-profile-contract-malformed");
            }
            if (contractStableId
                != StableId.Parse(CurrentContractStableIdText))
            {
                return Reject(
                    PlayerRouteProfileValidationStatus
                        .ContractIdentityMismatch,
                    "route-profile-contract-mismatch");
            }

            StableId selectedCharacterStableId;
            PlayerRouteProfileValidationResult identityFailure =
                TryParseRequiredIdentity(
                    envelope.SelectedCharacterStableId,
                    PlayerRouteProfileValidationStatus
                        .MissingCharacterIdentity,
                    PlayerRouteProfileValidationStatus
                        .MalformedCharacterIdentity,
                    "route-profile-character-missing",
                    "route-profile-character-malformed",
                    out selectedCharacterStableId);
            if (identityFailure != null)
            {
                return identityFailure;
            }

            StableId loadoutProfileStableId;
            identityFailure = TryParseRequiredIdentity(
                envelope.LoadoutProfileStableId,
                PlayerRouteProfileValidationStatus
                    .MissingLoadoutProfileIdentity,
                PlayerRouteProfileValidationStatus
                    .MalformedLoadoutProfileIdentity,
                "route-profile-loadout-missing",
                "route-profile-loadout-malformed",
                out loadoutProfileStableId);
            if (identityFailure != null)
            {
                return identityFailure;
            }

            if (envelope.WeaponSlots == null)
            {
                return Reject(
                    PlayerRouteProfileValidationStatus.MissingWeaponSlots,
                    "route-profile-slots-missing");
            }
            if (envelope.WeaponSlots.Count != WeaponSlotCount)
            {
                return Reject(
                    PlayerRouteProfileValidationStatus
                        .WeaponSlotCountMismatch,
                    "route-profile-slot-count-mismatch");
            }

            var parsedSlots = new List<PlayerRouteWeaponSlot>(
                WeaponSlotCount);
            var seenSlotIds = new HashSet<StableId>();
            var seenInstanceIds = new HashSet<StableId>();
            for (int index = 0;
                 index < envelope.WeaponSlots.Count;
                 index++)
            {
                PlayerRouteWeaponSlotEnvelope slot =
                    envelope.WeaponSlots[index];
                if (slot == null)
                {
                    return Reject(
                        PlayerRouteProfileValidationStatus.NullWeaponSlot,
                        "route-profile-slot-null");
                }
                if (string.IsNullOrWhiteSpace(slot.WeaponSlotStableId))
                {
                    return Reject(
                        PlayerRouteProfileValidationStatus
                            .MissingWeaponSlotIdentity,
                        "route-profile-slot-id-missing");
                }

                StableId slotStableId;
                if (!StableId.TryParse(
                        slot.WeaponSlotStableId,
                        out slotStableId))
                {
                    return Reject(
                        PlayerRouteProfileValidationStatus
                            .MalformedWeaponSlotIdentity,
                        "route-profile-slot-id-malformed");
                }
                if (!seenSlotIds.Add(slotStableId))
                {
                    return Reject(
                        PlayerRouteProfileValidationStatus
                            .DuplicateWeaponSlotIdentity,
                        "route-profile-slot-id-duplicate");
                }
                if (slotStableId != expectedWeaponSlotIds[index])
                {
                    return Reject(
                        PlayerRouteProfileValidationStatus
                            .UnexpectedWeaponSlotIdentity,
                        "route-profile-slot-order-or-id-mismatch");
                }

                StableId equipmentInstanceStableId = null;
                if (slot.EquipmentInstanceStableId != null)
                {
                    if (string.IsNullOrWhiteSpace(
                            slot.EquipmentInstanceStableId))
                    {
                        return Reject(
                            PlayerRouteProfileValidationStatus
                                .MissingEquipmentInstanceIdentity,
                            "route-profile-equipment-instance-missing");
                    }
                    if (!StableId.TryParse(
                            slot.EquipmentInstanceStableId,
                            out equipmentInstanceStableId))
                    {
                        return Reject(
                            PlayerRouteProfileValidationStatus
                                .MalformedEquipmentInstanceIdentity,
                            "route-profile-equipment-instance-malformed");
                    }
                    if (!seenInstanceIds.Add(equipmentInstanceStableId))
                    {
                        return Reject(
                            PlayerRouteProfileValidationStatus
                                .DuplicateEquipmentInstanceIdentity,
                            "route-profile-equipment-instance-duplicate");
                    }
                }

                parsedSlots.Add(new PlayerRouteWeaponSlot(
                    slotStableId,
                    equipmentInstanceStableId));
            }

            var candidate = new PlayerRouteProfilePayload(
                selectedCharacterStableId,
                loadoutProfileStableId,
                parsedSlots);
            if (string.IsNullOrWhiteSpace(envelope.Fingerprint))
            {
                return Reject(
                    PlayerRouteProfileValidationStatus.MissingFingerprint,
                    "route-profile-fingerprint-missing");
            }
            if (!string.Equals(
                    candidate.Fingerprint,
                    envelope.Fingerprint,
                    StringComparison.Ordinal))
            {
                return Reject(
                    PlayerRouteProfileValidationStatus.FingerprintMismatch,
                    "route-profile-fingerprint-mismatch");
            }

            return PlayerRouteProfileValidationResult.Accept(candidate);
        }

        public PlayerRouteProfileEnvelope ToEnvelope()
        {
            var slots = new List<PlayerRouteWeaponSlotEnvelope>(
                weaponSlots.Count);
            for (int index = 0; index < weaponSlots.Count; index++)
            {
                slots.Add(new PlayerRouteWeaponSlotEnvelope(
                    weaponSlots[index].WeaponSlotStableId.ToString(),
                    weaponSlots[index].EquipmentInstanceStableId == null
                        ? null
                        : weaponSlots[index]
                            .EquipmentInstanceStableId
                            .ToString()));
            }

            return new PlayerRouteProfileEnvelope(
                SchemaVersion,
                ContractStableId.ToString(),
                SelectedCharacterStableId.ToString(),
                LoadoutProfileStableId.ToString(),
                slots,
                Fingerprint);
        }

        public PlayerRouteProfilePayload Copy()
        {
            var instances = new List<StableId>(weaponSlots.Count);
            for (int index = 0; index < weaponSlots.Count; index++)
            {
                StableId source =
                    weaponSlots[index].EquipmentInstanceStableId;
                instances.Add(source == null
                    ? null
                    : StableId.Parse(source.ToString()));
            }
            return Create(
                StableId.Parse(SelectedCharacterStableId.ToString()),
                StableId.Parse(LoadoutProfileStableId.ToString()),
                instances);
        }

        public bool HasValidFingerprint()
        {
            return string.Equals(
                Fingerprint,
                ComputeFingerprint(canonicalText),
                StringComparison.Ordinal);
        }

        public bool Equals(PlayerRouteProfilePayload other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    canonicalText,
                    other.canonicalText,
                    StringComparison.Ordinal)
                && string.Equals(
                    Fingerprint,
                    other.Fingerprint,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PlayerRouteProfilePayload);
        }

        public override int GetHashCode()
        {
            return OrdinalHash(Fingerprint);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        internal static int OrdinalHash(string value)
        {
            unchecked
            {
                const uint offset = 2166136261u;
                const uint prime = 16777619u;
                uint hash = offset;
                string source = value ?? string.Empty;
                for (int index = 0; index < source.Length; index++)
                {
                    hash ^= source[index];
                    hash *= prime;
                }
                return (int)hash;
            }
        }

        private static PlayerRouteProfileValidationResult
            TryParseRequiredIdentity(
                string text,
                PlayerRouteProfileValidationStatus missingStatus,
                PlayerRouteProfileValidationStatus malformedStatus,
                string missingCode,
                string malformedCode,
                out StableId stableId)
        {
            stableId = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return Reject(missingStatus, missingCode);
            }
            if (!StableId.TryParse(text, out stableId))
            {
                return Reject(malformedStatus, malformedCode);
            }
            return null;
        }

        private static PlayerRouteProfileValidationResult Reject(
            PlayerRouteProfileValidationStatus status,
            string rejectionCode)
        {
            return PlayerRouteProfileValidationResult.Reject(
                status,
                rejectionCode);
        }

        private static string BuildCanonicalText(
            int schemaVersion,
            StableId contractStableId,
            StableId selectedCharacterStableId,
            StableId loadoutProfileStableId,
            IReadOnlyList<PlayerRouteWeaponSlot> slots)
        {
            var builder = new StringBuilder();
            builder.Append("schema=")
                .Append(schemaVersion.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
            Append(builder, "contract", contractStableId.ToString());
            Append(builder, "character", selectedCharacterStableId.ToString());
            Append(builder, "loadout", loadoutProfileStableId.ToString());
            builder.Append("slot-count=")
                .Append(slots.Count.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
            for (int index = 0; index < slots.Count; index++)
            {
                Append(
                    builder,
                    "slot-" + index.ToString(
                        "D2",
                        CultureInfo.InvariantCulture),
                    slots[index].ToCanonicalString());
            }
            return builder.ToString();
        }

        private static void Append(
            StringBuilder builder,
            string name,
            string value)
        {
            string safe = value ?? string.Empty;
            builder.Append(name)
                .Append('=')
                .Append(safe.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(safe)
                .Append('\n');
        }

        private static string ComputeFingerprint(string canonicalText)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(
                canonicalText ?? string.Empty);
            byte[] digest;
            using (SHA256 sha256 = SHA256.Create())
            {
                digest = sha256.ComputeHash(bytes);
            }

            var builder = new StringBuilder(digest.Length * 2);
            for (int index = 0; index < digest.Length; index++)
            {
                builder.Append(digest[index].ToString(
                    "x2",
                    CultureInfo.InvariantCulture));
            }
            return builder.ToString();
        }
    }
}
