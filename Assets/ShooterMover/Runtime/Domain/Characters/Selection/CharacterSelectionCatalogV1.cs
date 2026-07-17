using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Characters.Selection
{
    public enum CharacterSelectionCatalogStatusV1
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

    public sealed class CharacterSelectionCatalogResultV1
    {
        private CharacterSelectionCatalogResultV1(
            CharacterSelectionCatalogStatusV1 status,
            string rejectionCode,
            CharacterSelectionCatalogV1 catalog)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            Catalog = catalog;
        }

        public CharacterSelectionCatalogStatusV1 Status { get; }

        public string RejectionCode { get; }

        public CharacterSelectionCatalogV1 Catalog { get; }

        public bool IsValid
        {
            get { return Status == CharacterSelectionCatalogStatusV1.Valid; }
        }

        internal static CharacterSelectionCatalogResultV1 Accept(
            CharacterSelectionCatalogV1 catalog)
        {
            return new CharacterSelectionCatalogResultV1(
                CharacterSelectionCatalogStatusV1.Valid,
                string.Empty,
                catalog ?? throw new ArgumentNullException(nameof(catalog)));
        }

        internal static CharacterSelectionCatalogResultV1 Reject(
            CharacterSelectionCatalogStatusV1 status,
            string rejectionCode)
        {
            return new CharacterSelectionCatalogResultV1(
                status,
                rejectionCode,
                null);
        }
    }

    /// <summary>
    /// Immutable, deterministic catalog of selectable character identities and their
    /// class/loadout-profile identities. It contains content metadata only.
    /// </summary>
    public sealed class CharacterSelectionCatalogV1
    {
        private readonly ReadOnlyCollection<CharacterSelectionDefinitionV1> characters;
        private readonly ReadOnlyCollection<CharacterClassProfileDefinitionV1> profiles;
        private readonly Dictionary<StableId, CharacterSelectionDefinitionV1>
            characterByIdentity;
        private readonly Dictionary<StableId, CharacterClassProfileDefinitionV1>
            profileByIdentity;
        private readonly Dictionary<StableId, ReadOnlyCollection<CharacterClassProfileDefinitionV1>>
            profilesByCharacter;

        private CharacterSelectionCatalogV1(
            StableId defaultCharacterStableId,
            IList<CharacterSelectionDefinitionV1> orderedCharacters,
            IList<CharacterClassProfileDefinitionV1> orderedProfiles)
        {
            DefaultCharacterStableId = defaultCharacterStableId;
            characters = new ReadOnlyCollection<CharacterSelectionDefinitionV1>(
                new List<CharacterSelectionDefinitionV1>(orderedCharacters));
            profiles = new ReadOnlyCollection<CharacterClassProfileDefinitionV1>(
                new List<CharacterClassProfileDefinitionV1>(orderedProfiles));
            characterByIdentity =
                new Dictionary<StableId, CharacterSelectionDefinitionV1>();
            profileByIdentity =
                new Dictionary<StableId, CharacterClassProfileDefinitionV1>();
            profilesByCharacter =
                new Dictionary<StableId, ReadOnlyCollection<CharacterClassProfileDefinitionV1>>();

            for (int index = 0; index < characters.Count; index++)
            {
                characterByIdentity.Add(
                    characters[index].CharacterStableId,
                    characters[index]);
            }

            var mutableProfiles =
                new Dictionary<StableId, List<CharacterClassProfileDefinitionV1>>();
            for (int index = 0; index < profiles.Count; index++)
            {
                CharacterClassProfileDefinitionV1 profile = profiles[index];
                profileByIdentity.Add(profile.LoadoutProfileStableId, profile);
                List<CharacterClassProfileDefinitionV1> values;
                if (!mutableProfiles.TryGetValue(profile.CharacterStableId, out values))
                {
                    values = new List<CharacterClassProfileDefinitionV1>();
                    mutableProfiles.Add(profile.CharacterStableId, values);
                }

                values.Add(profile);
            }

            foreach (KeyValuePair<StableId, List<CharacterClassProfileDefinitionV1>> pair
                in mutableProfiles)
            {
                profilesByCharacter.Add(
                    pair.Key,
                    new ReadOnlyCollection<CharacterClassProfileDefinitionV1>(
                        pair.Value));
            }

            DefaultCharacter = characterByIdentity[DefaultCharacterStableId];
            Fingerprint = BuildFingerprint();
        }

        public StableId DefaultCharacterStableId { get; }

        public CharacterSelectionDefinitionV1 DefaultCharacter { get; }

        public IReadOnlyList<CharacterSelectionDefinitionV1> Characters
        {
            get { return characters; }
        }

        public IReadOnlyList<CharacterClassProfileDefinitionV1> Profiles
        {
            get { return profiles; }
        }

        public string Fingerprint { get; }

        public static CharacterSelectionCatalogResultV1 TryCreate(
            StableId defaultCharacterStableId,
            IEnumerable<CharacterSelectionDefinitionV1> characterDefinitions,
            IEnumerable<CharacterClassProfileDefinitionV1> profileDefinitions)
        {
            if (defaultCharacterStableId == null)
            {
                return Reject(
                    CharacterSelectionCatalogStatusV1.MissingDefaultCharacterIdentity,
                    "character-selection-default-character-missing");
            }

            if (characterDefinitions == null)
            {
                return Reject(
                    CharacterSelectionCatalogStatusV1.MissingCharacters,
                    "character-selection-characters-missing");
            }

            if (profileDefinitions == null)
            {
                return Reject(
                    CharacterSelectionCatalogStatusV1.MissingProfiles,
                    "character-selection-profiles-missing");
            }

            var characters = new List<CharacterSelectionDefinitionV1>(
                characterDefinitions);
            var profiles = new List<CharacterClassProfileDefinitionV1>(
                profileDefinitions);
            if (characters.Count == 0)
            {
                return Reject(
                    CharacterSelectionCatalogStatusV1.EmptyCharacters,
                    "character-selection-characters-empty");
            }

            if (profiles.Count == 0)
            {
                return Reject(
                    CharacterSelectionCatalogStatusV1.EmptyProfiles,
                    "character-selection-profiles-empty");
            }

            var characterIds = new HashSet<StableId>();
            for (int index = 0; index < characters.Count; index++)
            {
                CharacterSelectionDefinitionV1 character = characters[index];
                if (character == null)
                {
                    return Reject(
                        CharacterSelectionCatalogStatusV1.NullCharacter,
                        "character-selection-character-null");
                }

                if (!characterIds.Add(character.CharacterStableId))
                {
                    return Reject(
                        CharacterSelectionCatalogStatusV1.DuplicateCharacterIdentity,
                        "character-selection-character-duplicate");
                }
            }

            if (!characterIds.Contains(defaultCharacterStableId))
            {
                return Reject(
                    CharacterSelectionCatalogStatusV1.DefaultCharacterMissing,
                    "character-selection-default-character-unknown");
            }

            var profileIds = new HashSet<StableId>();
            var classKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < profiles.Count; index++)
            {
                CharacterClassProfileDefinitionV1 profile = profiles[index];
                if (profile == null)
                {
                    return Reject(
                        CharacterSelectionCatalogStatusV1.NullProfile,
                        "character-selection-profile-null");
                }

                if (!profileIds.Add(profile.LoadoutProfileStableId))
                {
                    return Reject(
                        CharacterSelectionCatalogStatusV1.DuplicateProfileIdentity,
                        "character-selection-profile-duplicate");
                }

                if (!characterIds.Contains(profile.CharacterStableId))
                {
                    return Reject(
                        CharacterSelectionCatalogStatusV1.ProfileCharacterMissing,
                        "character-selection-profile-character-unknown");
                }

                string classKey = profile.CharacterStableId
                    + "|"
                    + ((int)profile.ClassKind).ToString(CultureInfo.InvariantCulture);
                if (!classKeys.Add(classKey))
                {
                    return Reject(
                        CharacterSelectionCatalogStatusV1.DuplicateClassForCharacter,
                        "character-selection-class-duplicate-for-character");
                }
            }

            var profileById =
                new Dictionary<StableId, CharacterClassProfileDefinitionV1>();
            for (int index = 0; index < profiles.Count; index++)
            {
                profileById.Add(profiles[index].LoadoutProfileStableId, profiles[index]);
            }

            for (int index = 0; index < characters.Count; index++)
            {
                CharacterSelectionDefinitionV1 character = characters[index];
                CharacterClassProfileDefinitionV1 defaultProfile;
                if (!profileById.TryGetValue(
                    character.DefaultLoadoutProfileStableId,
                    out defaultProfile))
                {
                    return Reject(
                        CharacterSelectionCatalogStatusV1.CharacterDefaultProfileMissing,
                        "character-selection-default-profile-unknown");
                }

                if (defaultProfile.CharacterStableId != character.CharacterStableId)
                {
                    return Reject(
                        CharacterSelectionCatalogStatusV1.CharacterDefaultProfileOwnerMismatch,
                        "character-selection-default-profile-owner-mismatch");
                }
            }

            characters.Sort(delegate(
                CharacterSelectionDefinitionV1 left,
                CharacterSelectionDefinitionV1 right)
            {
                return string.CompareOrdinal(
                    left.CharacterStableId.ToString(),
                    right.CharacterStableId.ToString());
            });
            profiles.Sort(delegate(
                CharacterClassProfileDefinitionV1 left,
                CharacterClassProfileDefinitionV1 right)
            {
                return string.CompareOrdinal(
                    left.LoadoutProfileStableId.ToString(),
                    right.LoadoutProfileStableId.ToString());
            });

            return CharacterSelectionCatalogResultV1.Accept(
                new CharacterSelectionCatalogV1(
                    defaultCharacterStableId,
                    characters,
                    profiles));
        }

        public bool TryGetCharacter(
            StableId characterStableId,
            out CharacterSelectionDefinitionV1 character)
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
            out CharacterClassProfileDefinitionV1 profile)
        {
            if (loadoutProfileStableId == null)
            {
                profile = null;
                return false;
            }

            return profileByIdentity.TryGetValue(loadoutProfileStableId, out profile);
        }

        public IReadOnlyList<CharacterClassProfileDefinitionV1> GetProfiles(
            StableId characterStableId)
        {
            if (characterStableId == null)
            {
                return new ReadOnlyCollection<CharacterClassProfileDefinitionV1>(
                    new List<CharacterClassProfileDefinitionV1>());
            }

            ReadOnlyCollection<CharacterClassProfileDefinitionV1> values;
            if (!profilesByCharacter.TryGetValue(characterStableId, out values))
            {
                return new ReadOnlyCollection<CharacterClassProfileDefinitionV1>(
                    new List<CharacterClassProfileDefinitionV1>());
            }

            return values;
        }

        private string BuildFingerprint()
        {
            var builder = new StringBuilder();
            CharacterVisualMetadataV1.Append(
                builder,
                "default-character",
                DefaultCharacterStableId.ToString());
            builder.Append("character-count=")
                .Append(characters.Count.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
            for (int index = 0; index < characters.Count; index++)
            {
                CharacterVisualMetadataV1.Append(
                    builder,
                    "character-" + index.ToString("D2", CultureInfo.InvariantCulture),
                    characters[index].ToCanonicalString());
            }

            builder.Append("profile-count=")
                .Append(profiles.Count.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
            for (int index = 0; index < profiles.Count; index++)
            {
                CharacterVisualMetadataV1.Append(
                    builder,
                    "profile-" + index.ToString("D2", CultureInfo.InvariantCulture),
                    profiles[index].ToCanonicalString());
            }

            return CharacterVisualMetadataV1.Fingerprint(builder.ToString());
        }

        private static CharacterSelectionCatalogResultV1 Reject(
            CharacterSelectionCatalogStatusV1 status,
            string rejectionCode)
        {
            return CharacterSelectionCatalogResultV1.Reject(status, rejectionCode);
        }
    }
}
