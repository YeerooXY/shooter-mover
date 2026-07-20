using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Persistence.Accounts
{
    /// <summary>
    /// Immutable, subsystem-owned payload embedded in an account or character save.
    /// The account model does not interpret XP, holdings, wallet, skill, loadout,
    /// achievement, event, or future multiplayer payloads. Their owning adapters
    /// serialize and validate those payloads through stable component identities.
    /// </summary>
    public sealed class SaveComponentSnapshotV1
    {
        public SaveComponentSnapshotV1(
            StableId componentStableId,
            int schemaVersion,
            string contentVersion,
            string canonicalPayload)
        {
            ComponentStableId = componentStableId
                ?? throw new ArgumentNullException(nameof(componentStableId));
            if (schemaVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            }
            if (string.IsNullOrWhiteSpace(contentVersion))
            {
                throw new ArgumentException(
                    "A component content version is required.",
                    nameof(contentVersion));
            }
            if (canonicalPayload == null)
            {
                throw new ArgumentNullException(nameof(canonicalPayload));
            }

            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion.Trim();
            CanonicalPayload = canonicalPayload;
            Fingerprint = PlayerAccountSnapshotFingerprintV1.Hash(
                ToCanonicalString());
        }

        public StableId ComponentStableId { get; }

        public int SchemaVersion { get; }

        public string ContentVersion { get; }

        public string CanonicalPayload { get; }

        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return ComponentStableId
                + "|"
                + SchemaVersion.ToString(CultureInfo.InvariantCulture)
                + "|"
                + ContentVersion
                + "|"
                + CanonicalPayload;
        }
    }

    /// <summary>
    /// Durable truth for one of the account's six character instances. Class is a
    /// definition identity rather than a CLR subtype, so future classes do not require
    /// another CharacterInstance inheritance branch.
    /// </summary>
    public sealed class CharacterInstanceSnapshotV1
    {
        public CharacterInstanceSnapshotV1(
            StableId characterInstanceStableId,
            StableId classDefinitionStableId,
            int slotIndex,
            string displayName,
            long revision,
            IEnumerable<SaveComponentSnapshotV1> components)
        {
            CharacterInstanceStableId = characterInstanceStableId
                ?? throw new ArgumentNullException(
                    nameof(characterInstanceStableId));
            ClassDefinitionStableId = classDefinitionStableId
                ?? throw new ArgumentNullException(
                    nameof(classDefinitionStableId));
            PlayerAccountSnapshotV1.ValidateSlotIndex(slotIndex);
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException(
                    "A character display name is required.",
                    nameof(displayName));
            }
            if (revision < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(revision));
            }

            SlotIndex = slotIndex;
            DisplayName = displayName.Trim();
            Revision = revision;
            Components = FreezeComponents(components);
            Fingerprint = PlayerAccountSnapshotFingerprintV1.Hash(
                ToCanonicalString());
        }

        public StableId CharacterInstanceStableId { get; }

        public StableId ClassDefinitionStableId { get; }

        public int SlotIndex { get; }

        public string DisplayName { get; }

        public long Revision { get; }

        public IReadOnlyDictionary<StableId, SaveComponentSnapshotV1>
            Components { get; }

        public string Fingerprint { get; }

        public bool TryGetComponent(
            StableId componentStableId,
            out SaveComponentSnapshotV1 component)
        {
            component = null;
            return componentStableId != null
                && Components.TryGetValue(componentStableId, out component);
        }

        public CharacterInstanceSnapshotV1 WithComponent(
            SaveComponentSnapshotV1 component)
        {
            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            var next = Components.Values.ToDictionary(
                item => item.ComponentStableId,
                item => item);
            next[component.ComponentStableId] = component;
            return new CharacterInstanceSnapshotV1(
                CharacterInstanceStableId,
                ClassDefinitionStableId,
                SlotIndex,
                DisplayName,
                checked(Revision + 1L),
                next.Values);
        }

        public string ToCanonicalString()
        {
            return CharacterInstanceStableId
                + "|"
                + ClassDefinitionStableId
                + "|"
                + SlotIndex.ToString(CultureInfo.InvariantCulture)
                + "|"
                + DisplayName
                + "|"
                + Revision.ToString(CultureInfo.InvariantCulture)
                + "|"
                + string.Join(
                    ";",
                    Components.Values
                        .OrderBy(
                            item => item.ComponentStableId.ToString(),
                            StringComparer.Ordinal)
                        .Select(
                            item => item.ComponentStableId
                                + "="
                                + item.Fingerprint));
        }

        private static IReadOnlyDictionary<StableId, SaveComponentSnapshotV1>
            FreezeComponents(IEnumerable<SaveComponentSnapshotV1> components)
        {
            var output = new SortedDictionary<
                string,
                SaveComponentSnapshotV1>(StringComparer.Ordinal);
            foreach (SaveComponentSnapshotV1 component in
                components ?? Array.Empty<SaveComponentSnapshotV1>())
            {
                if (component == null)
                {
                    throw new ArgumentException(
                        "Character save components must be non-null.",
                        nameof(components));
                }
                string key = component.ComponentStableId.ToString();
                if (output.ContainsKey(key))
                {
                    throw new ArgumentException(
                        "Character save component identities must be unique.",
                        nameof(components));
                }
                output.Add(key, component);
            }

            return new ReadOnlyDictionary<StableId, SaveComponentSnapshotV1>(
                output.Values.ToDictionary(
                    item => item.ComponentStableId,
                    item => item));
        }
    }

    /// <summary>
    /// Versioned account aggregate with exactly six nullable character positions and
    /// extensible account-level components for achievements, collections, entitlements,
    /// daily challenges, seasonal state, or later multiplayer/account services.
    /// </summary>
    public sealed class PlayerAccountSnapshotV1
    {
        public const int CharacterSlotCount = 6;
        public const int CurrentSchemaVersion = 1;

        public PlayerAccountSnapshotV1(
            StableId accountStableId,
            long revision,
            IEnumerable<CharacterInstanceSnapshotV1> orderedCharacterSlots,
            IEnumerable<SaveComponentSnapshotV1> accountComponents)
        {
            AccountStableId = accountStableId
                ?? throw new ArgumentNullException(nameof(accountStableId));
            if (revision < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(revision));
            }

            Revision = revision;
            CharacterSlots = FreezeCharacterSlots(orderedCharacterSlots);
            AccountComponents = FreezeAccountComponents(accountComponents);
            Fingerprint = PlayerAccountSnapshotFingerprintV1.Hash(
                ToCanonicalString());
        }

        public int SchemaVersion
        {
            get { return CurrentSchemaVersion; }
        }

        public StableId AccountStableId { get; }

        public long Revision { get; }

        public IReadOnlyList<CharacterInstanceSnapshotV1> CharacterSlots
        {
            get;
        }

        public IReadOnlyDictionary<StableId, SaveComponentSnapshotV1>
            AccountComponents { get; }

        public string Fingerprint { get; }

        public CharacterInstanceSnapshotV1 CharacterAt(int slotIndex)
        {
            ValidateSlotIndex(slotIndex);
            return CharacterSlots[slotIndex];
        }

        public bool TryGetAccountComponent(
            StableId componentStableId,
            out SaveComponentSnapshotV1 component)
        {
            component = null;
            return componentStableId != null
                && AccountComponents.TryGetValue(componentStableId, out component);
        }

        public PlayerAccountSnapshotV1 WithCharacter(
            int slotIndex,
            CharacterInstanceSnapshotV1 character)
        {
            ValidateSlotIndex(slotIndex);
            if (character == null)
            {
                throw new ArgumentNullException(nameof(character));
            }
            if (character.SlotIndex != slotIndex)
            {
                throw new ArgumentException(
                    "The character snapshot slot does not match its account position.",
                    nameof(character));
            }

            var slots = CharacterSlots.ToArray();
            slots[slotIndex] = character;
            return new PlayerAccountSnapshotV1(
                AccountStableId,
                checked(Revision + 1L),
                slots,
                AccountComponents.Values);
        }

        public PlayerAccountSnapshotV1 WithoutCharacter(int slotIndex)
        {
            ValidateSlotIndex(slotIndex);
            var slots = CharacterSlots.ToArray();
            slots[slotIndex] = null;
            return new PlayerAccountSnapshotV1(
                AccountStableId,
                checked(Revision + 1L),
                slots,
                AccountComponents.Values);
        }

        public PlayerAccountSnapshotV1 WithAccountComponent(
            SaveComponentSnapshotV1 component)
        {
            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            var components = AccountComponents.Values.ToDictionary(
                item => item.ComponentStableId,
                item => item);
            components[component.ComponentStableId] = component;
            return new PlayerAccountSnapshotV1(
                AccountStableId,
                checked(Revision + 1L),
                CharacterSlots,
                components.Values);
        }

        public string ToCanonicalString()
        {
            return SchemaVersion.ToString(CultureInfo.InvariantCulture)
                + "|"
                + AccountStableId
                + "|"
                + Revision.ToString(CultureInfo.InvariantCulture)
                + "|"
                + string.Join(
                    ";",
                    CharacterSlots.Select(
                        item => item == null ? "empty" : item.Fingerprint))
                + "|"
                + string.Join(
                    ";",
                    AccountComponents.Values
                        .OrderBy(
                            item => item.ComponentStableId.ToString(),
                            StringComparer.Ordinal)
                        .Select(
                            item => item.ComponentStableId
                                + "="
                                + item.Fingerprint));
        }

        public static PlayerAccountSnapshotV1 Empty(StableId accountStableId)
        {
            return new PlayerAccountSnapshotV1(
                accountStableId,
                0L,
                new CharacterInstanceSnapshotV1[CharacterSlotCount],
                null);
        }

        public static void ValidateSlotIndex(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= CharacterSlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(slotIndex));
            }
        }

        private static IReadOnlyList<CharacterInstanceSnapshotV1>
            FreezeCharacterSlots(
                IEnumerable<CharacterInstanceSnapshotV1> orderedCharacterSlots)
        {
            if (orderedCharacterSlots == null)
            {
                throw new ArgumentNullException(nameof(orderedCharacterSlots));
            }

            var slots = orderedCharacterSlots.ToList();
            if (slots.Count != CharacterSlotCount)
            {
                throw new ArgumentException(
                    "Exactly six ordered character positions are required.",
                    nameof(orderedCharacterSlots));
            }

            var identities = new HashSet<StableId>();
            for (int index = 0; index < slots.Count; index++)
            {
                CharacterInstanceSnapshotV1 character = slots[index];
                if (character == null)
                {
                    continue;
                }
                if (character.SlotIndex != index)
                {
                    throw new ArgumentException(
                        "Each character snapshot must identify its exact slot.",
                        nameof(orderedCharacterSlots));
                }
                if (!identities.Add(character.CharacterInstanceStableId))
                {
                    throw new ArgumentException(
                        "Character instance identities must be unique per account.",
                        nameof(orderedCharacterSlots));
                }
            }

            return new ReadOnlyCollection<CharacterInstanceSnapshotV1>(slots);
        }

        private static IReadOnlyDictionary<StableId, SaveComponentSnapshotV1>
            FreezeAccountComponents(
                IEnumerable<SaveComponentSnapshotV1> accountComponents)
        {
            var output = new SortedDictionary<
                string,
                SaveComponentSnapshotV1>(StringComparer.Ordinal);
            foreach (SaveComponentSnapshotV1 component in
                accountComponents ?? Array.Empty<SaveComponentSnapshotV1>())
            {
                if (component == null)
                {
                    throw new ArgumentException(
                        "Account save components must be non-null.",
                        nameof(accountComponents));
                }
                string key = component.ComponentStableId.ToString();
                if (output.ContainsKey(key))
                {
                    throw new ArgumentException(
                        "Account save component identities must be unique.",
                        nameof(accountComponents));
                }
                output.Add(key, component);
            }

            return new ReadOnlyDictionary<StableId, SaveComponentSnapshotV1>(
                output.Values.ToDictionary(
                    item => item.ComponentStableId,
                    item => item));
        }
    }

    internal static class PlayerAccountSnapshotFingerprintV1
    {
        internal static string Hash(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(
                    Encoding.UTF8.GetBytes(value ?? string.Empty));
                return BitConverter.ToString(bytes)
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }
    }
}
