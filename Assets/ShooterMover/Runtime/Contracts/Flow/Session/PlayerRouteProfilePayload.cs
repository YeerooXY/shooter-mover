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
        MissingGunSlots = 11,
        GunSlotCountMismatch = 12,
        NullGunSlot = 13,
        MissingGunSlotIdentity = 14,
        MalformedGunSlotIdentity = 15,
        DuplicateGunSlotIdentity = 16,
        UnexpectedGunSlotIdentity = 17,
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
    public sealed class PlayerRouteGunSlotEnvelope
    {
        public PlayerRouteGunSlotEnvelope(
            string gunSlotStableId,
            string equipmentInstanceStableId)
        {
            GunSlotStableId = gunSlotStableId;
            EquipmentInstanceStableId = equipmentInstanceStableId;
        }

        public string GunSlotStableId { get; }
        public string EquipmentInstanceStableId { get; }
    }

    public sealed class PlayerRouteProfileEnvelope
    {
        private readonly ReadOnlyCollection<PlayerRouteGunSlotEnvelope>
            gunSlots;

        public PlayerRouteProfileEnvelope(
            int schemaVersion,
            string contractStableId,
            string selectedCharacterStableId,
            string loadoutProfileStableId,
            IEnumerable<PlayerRouteGunSlotEnvelope> gunSlots,
            string fingerprint)
        {
            SchemaVersion = schemaVersion;
            ContractStableId = contractStableId;
            SelectedCharacterStableId = selectedCharacterStableId;
            LoadoutProfileStableId = loadoutProfileStableId;
            this.gunSlots = gunSlots == null
                ? null
                : new ReadOnlyCollection<PlayerRouteGunSlotEnvelope>(
                    new List<PlayerRouteGunSlotEnvelope>(gunSlots));
            Fingerprint = fingerprint;
        }

        public int SchemaVersion { get; }
        public string ContractStableId { get; }
        public string SelectedCharacterStableId { get; }
        public string LoadoutProfileStableId { get; }
        public IReadOnlyList<PlayerRouteGunSlotEnvelope> GunSlots
        {
            get { return gunSlots; }
        }
        public string Fingerprint { get; }
    }

    public sealed class PlayerRouteGunSlot :
        IEquatable<PlayerRouteGunSlot>
    {
        internal PlayerRouteGunSlot(
            StableId gunSlotStableId,
            StableId equipmentInstanceStableId)
        {
            GunSlotStableId = gunSlotStableId
                ?? throw new ArgumentNullException(nameof(gunSlotStableId));
            EquipmentInstanceStableId = equipmentInstanceStableId;
        }

        public StableId GunSlotStableId { get; }
        public StableId EquipmentInstanceStableId { get; }
        public bool IsBound
        {
            get { return EquipmentInstanceStableId != null; }
        }

        public bool Equals(PlayerRouteGunSlot other)
        {
            return !ReferenceEquals(other, null)
                && GunSlotStableId == other.GunSlotStableId
                && EquipmentInstanceStableId
                    == other.EquipmentInstanceStableId;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PlayerRouteGunSlot);
        }

        public override int GetHashCode()
        {
            return PlayerRouteProfilePayload.OrdinalHash(
                ToCanonicalString());
        }

        public string ToCanonicalString()
        {
            return GunSlotStableId
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
        public const int GunSlotCount = 4;
        public const string CurrentContractStableIdText =
            "route-profile.player-v1";

        private static readonly ReadOnlyCollection<StableId>
            expectedGunSlotIds =
                new ReadOnlyCollection<StableId>(new List<StableId>
                {
                    StableId.Parse("gun-slot.slot-1"),
                    StableId.Parse("gun-slot.slot-2"),
                    StableId.Parse("gun-slot.slot-3"),
                    StableId.Parse("gun-slot.slot-4"),
                });

        private readonly ReadOnlyCollection<PlayerRouteGunSlot>
            gunSlots;
        private readonly string canonicalText;

        private PlayerRouteProfilePayload(
            StableId selectedCharacterStableId,
            StableId loadoutProfileStableId,
            IEnumerable<PlayerRouteGunSlot> gunSlots)
        {
            SchemaVersion = CurrentSchemaVersion;
            ContractStableId = StableId.Parse(CurrentContractStableIdText);
            SelectedCharacterStableId = selectedCharacterStableId
                ?? throw new ArgumentNullException(
                    nameof(selectedCharacterStableId));
            LoadoutProfileStableId = loadoutProfileStableId
                ?? throw new ArgumentNullException(
                    nameof(loadoutProfileStableId));
            this.gunSlots =
                new ReadOnlyCollection<PlayerRouteGunSlot>(
                    new List<PlayerRouteGunSlot>(
                        gunSlots
                        ?? throw new ArgumentNullException(
                            nameof(gunSlots))));
            canonicalText = BuildCanonicalText(
                SchemaVersion,
                ContractStableId,
                SelectedCharacterStableId,
                LoadoutProfileStableId,
                this.gunSlots);
            Fingerprint = ComputeFingerprint(canonicalText);
        }

        public int SchemaVersion { get; }
        public StableId ContractStableId { get; }
        public StableId SelectedCharacterStableId { get; }
        public StableId LoadoutProfileStableId { get; }
        public IReadOnlyList<PlayerRouteGunSlot> GunSlots
        {
            get { return gunSlots; }
        }
        public string Fingerprint { get; }
        public static IReadOnlyList<StableId> ExpectedGunSlotIds
        {
            get { return expectedGunSlotIds; }
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
            if (instances.Count != GunSlotCount)
            {
                throw new ArgumentException(
                    "Exactly four ordered gun-position bindings are required.",
                    nameof(orderedEquipmentInstanceStableIds));
            }

            var seenInstances = new HashSet<StableId>();
            var slots = new List<PlayerRouteGunSlot>(GunSlotCount);
            for (int index = 0; index < GunSlotCount; index++)
            {
                StableId instanceStableId = instances[index];
                if (instanceStableId != null
                    && !seenInstances.Add(instanceStableId))
                {
                    throw new ArgumentException(
                        "Bound equipment-instance identities must be unique.",
                        nameof(orderedEquipmentInstanceStableIds));
                }
                slots.Add(new PlayerRouteGunSlot(
                    expectedGunSlotIds[index],
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

            if (envelope.GunSlots == null)
            {
                return Reject(
                    PlayerRouteProfileValidationStatus.MissingGunSlots,
                    "route-profile-slots-missing");
            }
            if (envelope.GunSlots.Count != GunSlotCount)
            {
                return Reject(
                    PlayerRouteProfileValidationStatus
                        .GunSlotCountMismatch,
                    "route-profile-slot-count-mismatch");
            }

            var parsedSlots = new List<PlayerRouteGunSlot>(
                GunSlotCount);
            var seenSlotIds = new HashSet<StableId>();
            var seenInstanceIds = new HashSet<StableId>();
            for (int index = 0;
                 index < envelope.GunSlots.Count;
                 index++)
            {
                PlayerRouteGunSlotEnvelope slot =
                    envelope.GunSlots[index];
                if (slot == null)
                {
                    return Reject(
                        PlayerRouteProfileValidationStatus.NullGunSlot,
                        "route-profile-slot-null");
                }
                if (string.IsNullOrWhiteSpace(slot.GunSlotStableId))
                {
                    return Reject(
                        PlayerRouteProfileValidationStatus
                            .MissingGunSlotIdentity,
                        "route-profile-slot-id-missing");
                }

                StableId slotStableId;
                if (!StableId.TryParse(
                        slot.GunSlotStableId,
                        out slotStableId))
                {
                    return Reject(
                        PlayerRouteProfileValidationStatus
                            .MalformedGunSlotIdentity,
                        "route-profile-slot-id-malformed");
                }
                if (!seenSlotIds.Add(slotStableId))
                {
                    return Reject(
                        PlayerRouteProfileValidationStatus
                            .DuplicateGunSlotIdentity,
                        "route-profile-slot-id-duplicate");
                }
                if (slotStableId != expectedGunSlotIds[index])
                {
                    return Reject(
                        PlayerRouteProfileValidationStatus
                            .UnexpectedGunSlotIdentity,
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

                parsedSlots.Add(new PlayerRouteGunSlot(
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
            var slots = new List<PlayerRouteGunSlotEnvelope>(
                gunSlots.Count);
            for (int index = 0; index < gunSlots.Count; index++)
            {
                slots.Add(new PlayerRouteGunSlotEnvelope(
                    gunSlots[index].GunSlotStableId.ToString(),
                    gunSlots[index].EquipmentInstanceStableId == null
                        ? null
                        : gunSlots[index]
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
            var instances = new List<StableId>(gunSlots.Count);
            for (int index = 0; index < gunSlots.Count; index++)
            {
                StableId source =
                    gunSlots[index].EquipmentInstanceStableId;
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
            IReadOnlyList<PlayerRouteGunSlot> slots)
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
