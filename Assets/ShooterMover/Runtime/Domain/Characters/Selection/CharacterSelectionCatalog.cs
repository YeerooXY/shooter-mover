using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Characters.Selection
{
    public enum CharacterSelectionCatalogStatus
    {
        Valid = 1,
        MissingDefaultCharacterIdentity = 2,
        MissingCharacters = 3,
        MissingProfiles = 4,
        EmptyCharacters = 5,
        EmptyProfiles = 6,
        NullCharacter = 7,
        NullProfile = 8,
        DuplicateCharacterIdentity = 9,
        DuplicateProfileIdentity = 10,
        DefaultCharacterMissing = 11,
        ProfileCharacterMissing = 12,
        CharacterDefaultProfileMissing = 13,
        CharacterDefaultProfileOwnerMismatch = 14,
        DuplicateClassForCharacter = 15,
    }

    public sealed class CharacterSelectionCatalogResult
    {
        private CharacterSelectionCatalogResult(
            CharacterSelectionCatalogStatus status,
            string rejectionCode,
            CharacterSelectionCatalog catalog)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            Catalog = catalog;
        }

        public CharacterSelectionCatalogStatus Status { get; }

        public string RejectionCode { get; }

        public CharacterSelectionCatalog Catalog { get; }

        public bool IsValid
        {
            get { return Status == CharacterSelectionCatalogStatus.Valid; }
        }

        internal static CharacterSelectionCatalogResult Accept(
            CharacterSelectionCatalog catalog)
        {
            return new CharacterSelectionCatalogResult(
                CharacterSelectionCatalogStatus.Valid,
                string.Empty,
                catalog ?? throw new ArgumentNullException(nameof(catalog)));
        }

        internal static CharacterSelectionCatalogResult Reject(
            CharacterSelectionCatalogStatus status,
            string rejectionCode)
        {
            return new CharacterSelectionCatalogResult(
                status,
                rejectionCode,
                null);
        }
    }

    /// <summary>
    /// Immutable, deterministic catalog of selectable character identities and their
    /// class/loadout-profile identities. It contains content metadata only.
    /// </summary>
    public sealed class CharacterSelectionCatalog
    {
        private readonly ReadOnlyCollection<CharacterSelectionDefinition> characters;
        private readonly ReadOnlyCollection<CharacterClassProfileDefinition> profiles;
        private readonly Dictionary<StableId, CharacterSelectionDefinition>
            characterByIdentity;
        private readonly Dictionary<StableId, CharacterClassProfileDefinition>
            profileByIdentity;
        private readonly Dictionary<StableId, ReadOnlyCollection<CharacterClassProfileDefinition>>
            profilesByCharacter;

        private CharacterSelectionCatalog(
            StableId defaultCharacterStableId,
            IList<CharacterSelectionDefinition> orderedCharacters,
            IList<CharacterClassProfileDefinition> orderedProfiles)
        {
            DefaultCharacterStableId = defaultCharacterStableId;
            characters = new ReadOnlyCollection<CharacterSelectionDefinition>(
                new List<CharacterSelectionDefinition>(orderedCharacters));
            profiles = new ReadOnlyCollection<CharacterClassProfileDefinition>(
                new List<CharacterClassProfileDefinition>(orderedProfiles));
            characterByIdentity =
                new Dictionary<StableId, CharacterSelectionDefinition>();
            profileByIdentity =
                new Dictionary<StableId, CharacterClassProfileDefinition>();
            profilesByCharacter =
                new Dictionary<StableId, ReadOnlyCollection<CharacterClassProfileDefinition>>();

            for (int index = 0; index < characters.Count; index++)
            {
                characterByIdentity.Add(
                    characters[index].CharacterStableId,
                    characters[index]);
            }

            var mutableProfiles =
                new Dictionary<StableId, List<CharacterClassProfileDefinition>>();
            for (int index = 0; index < profiles.Count; index++)
            {
                CharacterClassProfileDefinition profile = profiles[index];
                profileByIdentity.Add(profile.LoadoutProfileStableId, profile);
                List<CharacterClassProfileDefinition> values;
                if (!mutableProfiles.TryGetValue(profile.CharacterStableId, out values))
                {
                    values = new List<CharacterClassProfileDefinition>();
                    mutableProfiles.Add(profile.CharacterStableId, values);
                }

                values.Add(profile);
            }

            foreach (KeyValuePair<StableId, List<CharacterClassProfileDefinition>> pair
                in mutableProfiles)
            {
                profilesByCharacter.Add(
                    pair.Key,
                    new ReadOnlyCollection<CharacterClassProfileDefinition>(
                        pair.Value));
            }

            DefaultCharacter = characterByIdentity[DefaultCharacterStableId];
            Fingerprint = BuildFingerprint();
        }

        public StableId DefaultCharacterStableId { get; }

        public CharacterSelectionDefinition DefaultCharacter { get; }

        public IReadOnlyList<CharacterSelectionDefinition> Characters
        {
            get { return characters; }
        }

        public IReadOnlyList<CharacterClassProfileDefinition> Profiles
        {
            get { return profiles; }
        }

        public string Fingerprint { get; }

        public static CharacterSelectionCatalogResult TryCreate(
            StableId defaultCharacterStableId,
            IEnumerable<CharacterSelectionDefinition> characterDefinitions,
            IEnumerable<CharacterClassProfileDefinition> profileDefinitions)
        {
            if (defaultCharacterStableId == null)
            {
                return Reject(
                    CharacterSelectionCatalogStatus.MissingDefaultCharacterIdentity,
                    "character-selection-default-character-missing");
            }

            if (characterDefinitions == null)
            {
                return Reject(
                    CharacterSelectionCatalogStatus.MissingCharacters,
                    "character-selection-characters-missing");
            }

            if (profileDefinitions == null)
            {
                return Reject(
                    CharacterSelectionCatalogStatus.MissingProfiles,
                    "character-selection-profiles-missing");
            }

            var characters = new List<CharacterSelectionDefinition>(
                characterDefinitions);
            var profiles = new List<CharacterClassProfileDefinition>(
                profileDefinitions);
            if (characters.Count == 0)
            {
                return Reject(
                    CharacterSelectionCatalogStatus.EmptyCharacters,
                    "character-selection-characters-empty");
            }

            if (profiles.Count == 0)
            {
                return Reject(
                    CharacterSelectionCatalogStatus.EmptyProfiles,
                    "character-selection-profiles-empty");
            }

            var characterIds = new HashSet<StableId>();
            for (int index = 0; index < characters.Count; index++)
            {
                CharacterSelectionDefinition character = characters[index];
                if (character == null)
                {
                    return Reject(
                        CharacterSelectionCatalogStatus.NullCharacter,
                        "character-selection-character-null");
                }

                if (!characterIds.Add(character.CharacterStableId))
                {
                    return Reject(
                        CharacterSelectionCatalogStatus.DuplicateCharacterIdentity,
                        "character-selection-character-duplicate");
                }
            }

            if (!characterIds.Contains(defaultCharacterStableId))
            {
                return Reject(
                    CharacterSelectionCatalogStatus.DefaultCharacterMissing,
                    "character-selection-default-character-unknown");
            }

            var profileIds = new HashSet<StableId>();
            var classKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < profiles.Count; index++)
            {
                CharacterClassProfileDefinition profile = profiles[index];
                if (profile == null)
                {
                    return Reject(
                        CharacterSelectionCatalogStatus.NullProfile,
                        "character-selection-profile-null");
                }

                if (!profileIds.Add(profile.LoadoutProfileStableId))
                {
                    return Reject(
                        CharacterSelectionCatalogStatus.DuplicateProfileIdentity,
                        "character-selection-profile-duplicate");
                }

                if (!characterIds.Contains(profile.CharacterStableId))
                {
                    return Reject(
                        CharacterSelectionCatalogStatus.ProfileCharacterMissing,
                        "character-selection-profile-character-unknown");
                }

                string classKey = profile.CharacterStableId
                    + "|"
                    + ((int)profile.ClassKind).ToString(CultureInfo.InvariantCulture);
                if (!classKeys.Add(classKey))
                {
                    return Reject(
                        CharacterSelectionCatalogStatus.DuplicateClassForCharacter,
                        "character-selection-class-duplicate-for-character");
                }
            }

            var profileById =
                new Dictionary<StableId, CharacterClassProfileDefinition>();
            for (int index = 0; index < profiles.Count; index++)
            {
                profileById.Add(profiles[index].LoadoutProfileStableId, profiles[index]);
            }

            for (int index = 0; index < characters.Count; index++)
            {
                CharacterSelectionDefinition character = characters[index];
                CharacterClassProfileDefinition defaultProfile;
                if (!profileById.TryGetValue(
                    character.DefaultLoadoutProfileStableId,
                    out defaultProfile))
                {
                    return Reject(
                        CharacterSelectionCatalogStatus.CharacterDefaultProfileMissing,
                        "character-selection-default-profile-unknown");
                }

                if (defaultProfile.CharacterStableId != character.CharacterStableId)
                {
                    return Reject(
                        CharacterSelectionCatalogStatus.CharacterDefaultProfileOwnerMismatch,
                        "character-selection-default-profile-owner-mismatch");
                }
            }

            characters.Sort(delegate(
                CharacterSelectionDefinition left,
                CharacterSelectionDefinition right)
            {
                return string.CompareOrdinal(
                    left.CharacterStableId.ToString(),
                    right.CharacterStableId.ToString());
            });
            profiles.Sort(delegate(
                CharacterClassProfileDefinition left,
                CharacterClassProfileDefinition right)
            {
                return string.CompareOrdinal(
                    left.LoadoutProfileStableId.ToString(),
                    right.LoadoutProfileStableId.ToString());
            });

            return CharacterSelectionCatalogResult.Accept(
                new CharacterSelectionCatalog(
                    defaultCharacterStableId,
                    characters,
                    profiles));
        }

        public bool TryGetCharacter(
            StableId characterStableId,
            out CharacterSelectionDefinition character)
        {
            if (characterStableId == null)
            {
                character = null;
                return false;
            }

            return characterByIdentity.TryGetValue(characterStableId, out character);
        }

        public bool TryGetProfile(
            StableId loadoutProfileStableId,
            out CharacterClassProfileDefinition profile)
        {
            if (loadoutProfileStableId == null)
            {
                profile = null;
                return false;
            }

            return profileByIdentity.TryGetValue(loadoutProfileStableId, out profile);
        }

        public IReadOnlyList<CharacterClassProfileDefinition> GetProfiles(
            StableId characterStableId)
        {
            if (characterStableId == null)
            {
                return new ReadOnlyCollection<CharacterClassProfileDefinition>(
                    new List<CharacterClassProfileDefinition>());
            }

            ReadOnlyCollection<CharacterClassProfileDefinition> values;
            if (!profilesByCharacter.TryGetValue(characterStableId, out values))
            {
                return new ReadOnlyCollection<CharacterClassProfileDefinition>(
                    new List<CharacterClassProfileDefinition>());
            }

            return values;
        }

        private string BuildFingerprint()
        {
            var builder = new StringBuilder();
            CharacterVisualMetadata.Append(
                builder,
                "default-character",
                DefaultCharacterStableId.ToString());
            builder.Append("character-count=")
                .Append(characters.Count.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
            for (int index = 0; index < characters.Count; index++)
            {
                CharacterVisualMetadata.Append(
                    builder,
                    "character-" + index.ToString("D2", CultureInfo.InvariantCulture),
                    characters[index].ToCanonicalString());
            }

            builder.Append("profile-count=")
                .Append(profiles.Count.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
            for (int index = 0; index < profiles.Count; index++)
            {
                CharacterVisualMetadata.Append(
                    builder,
                    "profile-" + index.ToString("D2", CultureInfo.InvariantCulture),
                    profiles[index].ToCanonicalString());
            }

            return CharacterVisualMetadata.Fingerprint(builder.ToString());
        }

        private static CharacterSelectionCatalogResult Reject(
            CharacterSelectionCatalogStatus status,
            string rejectionCode)
        {
            return CharacterSelectionCatalogResult.Reject(status, rejectionCode);
        }
    }
}
