using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Contracts.Flow.Session
{
    public enum HubRouteV1
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

    public enum PlayerRouteProfileValidationStatusV1
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
    /// Raw persistence/navigation envelope. It deliberately stores strings so invalid
    /// external data can be rejected before any StableId or live route state is created.
    /// </summary>
    public sealed class PlayerRouteWeaponSlotEnvelopeV1
    {
        public PlayerRouteWeaponSlotEnvelopeV1(
            string weaponSlotStableId,
            string equipmentInstanceStableId)
        {
            WeaponSlotStableId = weaponSlotStableId;
            EquipmentInstanceStableId = equipmentInstanceStableId;
        }

        public string WeaponSlotStableId { get; }

        public string EquipmentInstanceStableId { get; }
    }

    public sealed class PlayerRouteProfileEnvelopeV1
    {
        private readonly ReadOnlyCollection<PlayerRouteWeaponSlotEnvelopeV1> weaponSlots;

        public PlayerRouteProfileEnvelopeV1(
            int schemaVersion,
            string contractStableId,
            string selectedCharacterStableId,
            string loadoutProfileStableId,
            IEnumerable<PlayerRouteWeaponSlotEnvelopeV1> weaponSlots,
            string fingerprint)
        {
            SchemaVersion = schemaVersion;
            ContractStableId = contractStableId;
            SelectedCharacterStableId = selectedCharacterStableId;
            LoadoutProfileStableId = loadoutProfileStableId;
            this.weaponSlots = weaponSlots == null
                ? null
                : new ReadOnlyCollection<PlayerRouteWeaponSlotEnvelopeV1>(
                    new List<PlayerRouteWeaponSlotEnvelopeV1>(weaponSlots));
            Fingerprint = fingerprint;
        }

        public int SchemaVersion { get; }

        public string ContractStableId { get; }

        public string SelectedCharacterStableId { get; }

        public string LoadoutProfileStableId { get; }

        public IReadOnlyList<PlayerRouteWeaponSlotEnvelopeV1> WeaponSlots
        {
            get { return weaponSlots; }
        }

        public string Fingerprint { get; }
    }

    public sealed class PlayerRouteWeaponSlotV1 : IEquatable<PlayerRouteWeaponSlotV1>
    {
        internal PlayerRouteWeaponSlotV1(
            StableId weaponSlotStableId,
            StableId equipmentInstanceStableId)
        {
            WeaponSlotStableId = weaponSlotStableId
                ?? throw new ArgumentNullException(nameof(weaponSlotStableId));
            EquipmentInstanceStableId = equipmentInstanceStableId
                ?? throw new ArgumentNullException(nameof(equipmentInstanceStableId));
        }

        public StableId WeaponSlotStableId { get; }

        public StableId EquipmentInstanceStableId { get; }

        public bool Equals(PlayerRouteWeaponSlotV1 other)
        {
            return !ReferenceEquals(other, null)
                && WeaponSlotStableId == other.WeaponSlotStableId
                && EquipmentInstanceStableId == other.EquipmentInstanceStableId;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PlayerRouteWeaponSlotV1);
        }

        public override int GetHashCode()
        {
            return PlayerRouteProfilePayloadV1.OrdinalHash(ToCanonicalString());
        }

        public string ToCanonicalString()
        {
            return WeaponSlotStableId + "|" + EquipmentInstanceStableId;
        }
    }

    public sealed class PlayerRouteProfileValidationResultV1
    {
        private PlayerRouteProfileValidationResultV1(
            PlayerRouteProfileValidationStatusV1 status,
            string rejectionCode,
            PlayerRouteProfilePayloadV1 payload)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            Payload = payload;
        }

        public PlayerRouteProfileValidationStatusV1 Status { get; }

        public string RejectionCode { get; }

        public PlayerRouteProfilePayloadV1 Payload { get; }

        public bool IsValid
        {
            get { return Status == PlayerRouteProfileValidationStatusV1.Valid; }
        }

        internal static PlayerRouteProfileValidationResultV1 Accept(
            PlayerRouteProfilePayloadV1 payload)
        {
            return new PlayerRouteProfileValidationResultV1(
                PlayerRouteProfileValidationStatusV1.Valid,
                string.Empty,
                payload ?? throw new ArgumentNullException(nameof(payload)));
        }

        internal static PlayerRouteProfileValidationResultV1 Reject(
            PlayerRouteProfileValidationStatusV1 status,
            string rejectionCode)
        {
            return new PlayerRouteProfileValidationResultV1(
                status,
                rejectionCode,
                null);
        }
    }

    /// <summary>
    /// Immutable V1 route payload shared by every menu/hub destination. Ordered slot
    /// bindings preserve concrete equipment-instance identities, not definitions.
    /// </summary>
    public sealed class PlayerRouteProfilePayloadV1 :
        IEquatable<PlayerRouteProfilePayloadV1>
    {
        public const int CurrentSchemaVersion = 1;
        public const int WeaponSlotCount = 4;
        public const string CurrentContractStableIdText = "route-profile.player-v1";

        private static readonly ReadOnlyCollection<StableId> expectedWeaponSlotIds =
            new ReadOnlyCollection<StableId>(new List<StableId>
            {
                StableId.Parse("weapon-slot.slot-1"),
                StableId.Parse("weapon-slot.slot-2"),
                StableId.Parse("weapon-slot.slot-3"),
                StableId.Parse("weapon-slot.slot-4"),
            });

        private readonly ReadOnlyCollection<PlayerRouteWeaponSlotV1> weaponSlots;
        private readonly string canonicalText;

        private PlayerRouteProfilePayloadV1(
            StableId selectedCharacterStableId,
            StableId loadoutProfileStableId,
            IEnumerable<PlayerRouteWeaponSlotV1> weaponSlots)
        {
            SchemaVersion = CurrentSchemaVersion;
            ContractStableId = StableId.Parse(CurrentContractStableIdText);
            SelectedCharacterStableId = selectedCharacterStableId
                ?? throw new ArgumentNullException(nameof(selectedCharacterStableId));
            LoadoutProfileStableId = loadoutProfileStableId
                ?? throw new ArgumentNullException(nameof(loadoutProfileStableId));
            this.weaponSlots = new ReadOnlyCollection<PlayerRouteWeaponSlotV1>(
                new List<PlayerRouteWeaponSlotV1>(
                    weaponSlots ?? throw new ArgumentNullException(nameof(weaponSlots))));
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

        public IReadOnlyList<PlayerRouteWeaponSlotV1> WeaponSlots
        {
            get { return weaponSlots; }
        }

        public string Fingerprint { get; }

        public static IReadOnlyList<StableId> ExpectedWeaponSlotIds
        {
            get { return expectedWeaponSlotIds; }
        }

        public static PlayerRouteProfilePayloadV1 Create(
            StableId selectedCharacterStableId,
            StableId loadoutProfileStableId,
            IEnumerable<StableId> orderedEquipmentInstanceStableIds)
        {
            if (selectedCharacterStableId == null)
            {
                throw new ArgumentNullException(nameof(selectedCharacterStableId));
            }

            if (loadoutProfileStableId == null)
            {
                throw new ArgumentNullException(nameof(loadoutProfileStableId));
            }

            if (orderedEquipmentInstanceStableIds == null)
            {
                throw new ArgumentNullException(nameof(orderedEquipmentInstanceStableIds));
            }

            var instances = new List<StableId>(orderedEquipmentInstanceStableIds);
            if (instances.Count != WeaponSlotCount)
            {
                throw new ArgumentException(
                    "Exactly four ordered weapon equipment-instance identities are required.",
                    nameof(orderedEquipmentInstanceStableIds));
            }

            var seenInstances = new HashSet<StableId>();
            var slots = new List<PlayerRouteWeaponSlotV1>(WeaponSlotCount);
            for (int index = 0; index < WeaponSlotCount; index++)
            {
                StableId instanceStableId = instances[index];
                if (instanceStableId == null)
                {
                    throw new ArgumentException(
                        "Equipment-instance identities cannot contain null.",
                        nameof(orderedEquipmentInstanceStableIds));
                }

                if (!seenInstances.Add(instanceStableId))
                {
                    throw new ArgumentException(
                        "Equipment-instance identities must be unique across weapon slots.",
                        nameof(orderedEquipmentInstanceStableIds));
                }

                slots.Add(new PlayerRouteWeaponSlotV1(
                    expectedWeaponSlotIds[index],
                    instanceStableId));
            }

            return new PlayerRouteProfilePayloadV1(
                selectedCharacterStableId,
                loadoutProfileStableId,
                slots);
        }

        public static PlayerRouteProfileValidationResultV1 TryImport(
            PlayerRouteProfileEnvelopeV1 envelope)
        {
            if (envelope == null)
            {
                return Reject(
                    PlayerRouteProfileValidationStatusV1.NullEnvelope,
                    "route-profile-envelope-null");
            }

            if (envelope.SchemaVersion != CurrentSchemaVersion)
            {
                return Reject(
                    PlayerRouteProfileValidationStatusV1.UnsupportedSchemaVersion,
                    "route-profile-schema-unsupported");
            }

            if (string.IsNullOrWhiteSpace(envelope.ContractStableId))
            {
                return Reject(
                    PlayerRouteProfileValidationStatusV1.MissingContractIdentity,
                    "route-profile-contract-missing");
            }

            StableId contractStableId;
            if (!StableId.TryParse(envelope.ContractStableId, out contractStableId))
            {
                return Reject(
                    PlayerRouteProfileValidationStatusV1.MalformedContractIdentity,
                    "route-profile-contract-malformed");
            }

            if (contractStableId != StableId.Parse(CurrentContractStableIdText))
            {
                return Reject(
                    PlayerRouteProfileValidationStatusV1.ContractIdentityMismatch,
                    "route-profile-contract-mismatch");
            }

            StableId selectedCharacterStableId;
            PlayerRouteProfileValidationResultV1 identityFailure = TryParseRequiredIdentity(
                envelope.SelectedCharacterStableId,
                PlayerRouteProfileValidationStatusV1.MissingCharacterIdentity,
                PlayerRouteProfileValidationStatusV1.MalformedCharacterIdentity,
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
                PlayerRouteProfileValidationStatusV1.MissingLoadoutProfileIdentity,
                PlayerRouteProfileValidationStatusV1.MalformedLoadoutProfileIdentity,
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
                    PlayerRouteProfileValidationStatusV1.MissingWeaponSlots,
                    "route-profile-slots-missing");
            }

            if (envelope.WeaponSlots.Count != WeaponSlotCount)
            {
                return Reject(
                    PlayerRouteProfileValidationStatusV1.WeaponSlotCountMismatch,
                    "route-profile-slot-count-mismatch");
            }

            var parsedSlots = new List<PlayerRouteWeaponSlotV1>(WeaponSlotCount);
            var seenSlotIds = new HashSet<StableId>();
            var seenInstanceIds = new HashSet<StableId>();
            for (int index = 0; index < envelope.WeaponSlots.Count; index++)
            {
                PlayerRouteWeaponSlotEnvelopeV1 slot = envelope.WeaponSlots[index];
                if (slot == null)
                {
                    return Reject(
                        PlayerRouteProfileValidationStatusV1.NullWeaponSlot,
                        "route-profile-slot-null");
                }

                if (string.IsNullOrWhiteSpace(slot.WeaponSlotStableId))
                {
                    return Reject(
                        PlayerRouteProfileValidationStatusV1.MissingWeaponSlotIdentity,
                        "route-profile-slot-id-missing");
                }

                StableId slotStableId;
                if (!StableId.TryParse(slot.WeaponSlotStableId, out slotStableId))
                {
                    return Reject(
                        PlayerRouteProfileValidationStatusV1.MalformedWeaponSlotIdentity,
                        "route-profile-slot-id-malformed");
                }

                if (!seenSlotIds.Add(slotStableId))
                {
                    return Reject(
                        PlayerRouteProfileValidationStatusV1.DuplicateWeaponSlotIdentity,
                        "route-profile-slot-id-duplicate");
                }

                if (slotStableId != expectedWeaponSlotIds[index])
                {
                    return Reject(
                        PlayerRouteProfileValidationStatusV1.UnexpectedWeaponSlotIdentity,
                        "route-profile-slot-order-or-id-mismatch");
                }

                if (string.IsNullOrWhiteSpace(slot.EquipmentInstanceStableId))
                {
                    return Reject(
                        PlayerRouteProfileValidationStatusV1.MissingEquipmentInstanceIdentity,
                        "route-profile-equipment-instance-missing");
                }

                StableId equipmentInstanceStableId;
                if (!StableId.TryParse(
                    slot.EquipmentInstanceStableId,
                    out equipmentInstanceStableId))
                {
                    return Reject(
                        PlayerRouteProfileValidationStatusV1.MalformedEquipmentInstanceIdentity,
                        "route-profile-equipment-instance-malformed");
                }

                if (!seenInstanceIds.Add(equipmentInstanceStableId))
                {
                    return Reject(
                        PlayerRouteProfileValidationStatusV1.DuplicateEquipmentInstanceIdentity,
                        "route-profile-equipment-instance-duplicate");
                }

                parsedSlots.Add(new PlayerRouteWeaponSlotV1(
                    slotStableId,
                    equipmentInstanceStableId));
            }

            var candidate = new PlayerRouteProfilePayloadV1(
                selectedCharacterStableId,
                loadoutProfileStableId,
                parsedSlots);
            if (string.IsNullOrWhiteSpace(envelope.Fingerprint))
            {
                return Reject(
                    PlayerRouteProfileValidationStatusV1.MissingFingerprint,
                    "route-profile-fingerprint-missing");
            }

            if (!string.Equals(
                candidate.Fingerprint,
                envelope.Fingerprint,
                StringComparison.Ordinal))
            {
                return Reject(
                    PlayerRouteProfileValidationStatusV1.FingerprintMismatch,
                    "route-profile-fingerprint-mismatch");
            }

            return PlayerRouteProfileValidationResultV1.Accept(candidate);
        }

        public PlayerRouteProfileEnvelopeV1 ToEnvelope()
        {
            var slots = new List<PlayerRouteWeaponSlotEnvelopeV1>(weaponSlots.Count);
            for (int index = 0; index < weaponSlots.Count; index++)
            {
                slots.Add(new PlayerRouteWeaponSlotEnvelopeV1(
                    weaponSlots[index].WeaponSlotStableId.ToString(),
                    weaponSlots[index].EquipmentInstanceStableId.ToString()));
            }

            return new PlayerRouteProfileEnvelopeV1(
                SchemaVersion,
                ContractStableId.ToString(),
                SelectedCharacterStableId.ToString(),
                LoadoutProfileStableId.ToString(),
                slots,
                Fingerprint);
        }

        public PlayerRouteProfilePayloadV1 Copy()
        {
            var instances = new List<StableId>(weaponSlots.Count);
            for (int index = 0; index < weaponSlots.Count; index++)
            {
                instances.Add(StableId.Parse(
                    weaponSlots[index].EquipmentInstanceStableId.ToString()));
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

        public bool Equals(PlayerRouteProfilePayloadV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal)
                && string.Equals(Fingerprint, other.Fingerprint, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PlayerRouteProfilePayloadV1);
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

        private static PlayerRouteProfileValidationResultV1 TryParseRequiredIdentity(
            string text,
            PlayerRouteProfileValidationStatusV1 missingStatus,
            PlayerRouteProfileValidationStatusV1 malformedStatus,
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

        private static PlayerRouteProfileValidationResultV1 Reject(
            PlayerRouteProfileValidationStatusV1 status,
            string rejectionCode)
        {
            return PlayerRouteProfileValidationResultV1.Reject(status, rejectionCode);
        }

        private static string BuildCanonicalText(
            int schemaVersion,
            StableId contractStableId,
            StableId selectedCharacterStableId,
            StableId loadoutProfileStableId,
            IReadOnlyList<PlayerRouteWeaponSlotV1> slots)
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
                    "slot-" + index.ToString("D2", CultureInfo.InvariantCulture),
                    slots[index].ToCanonicalString());
            }

            return builder.ToString();
        }

        private static void Append(StringBuilder builder, string name, string value)
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
            byte[] bytes = Encoding.UTF8.GetBytes(canonicalText ?? string.Empty);
            byte[] digest;
            using (SHA256 sha256 = SHA256.Create())
            {
                digest = sha256.ComputeHash(bytes);
            }

            var builder = new StringBuilder(digest.Length * 2);
            for (int index = 0; index < digest.Length; index++)
            {
                builder.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }
}
