using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Characters.Selection
{
    public enum CharacterClassKindV1
    {
        Aggressive = 1,
        Defensive = 2,
        Healer = 3,
    }

    /// <summary>
    /// Stable presentation metadata only. These identities are extension points for future
    /// body, armor, and visual composition; they do not own stats, equipment, or inventory.
    /// </summary>
    public sealed class CharacterVisualMetadataV1 : IEquatable<CharacterVisualMetadataV1>
    {
        public CharacterVisualMetadataV1(
            string portraitResourceKey,
            string previewResourceKey,
            StableId visualVariantStableId,
            StableId bodyVariantStableId,
            StableId armorVariantStableId)
        {
            PortraitResourceKey = RequireText(
                portraitResourceKey,
                nameof(portraitResourceKey));
            PreviewResourceKey = RequireText(
                previewResourceKey,
                nameof(previewResourceKey));
            VisualVariantStableId = visualVariantStableId;
            BodyVariantStableId = bodyVariantStableId;
            ArmorVariantStableId = armorVariantStableId;
        }

        public string PortraitResourceKey { get; }

        public string PreviewResourceKey { get; }

        public StableId VisualVariantStableId { get; }

        public StableId BodyVariantStableId { get; }

        public StableId ArmorVariantStableId { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            Append(builder, "portrait", PortraitResourceKey);
            Append(builder, "preview", PreviewResourceKey);
            Append(builder, "visual", Text(VisualVariantStableId));
            Append(builder, "body", Text(BodyVariantStableId));
            Append(builder, "armor", Text(ArmorVariantStableId));
            return builder.ToString();
        }

        public bool Equals(CharacterVisualMetadataV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    ToCanonicalString(),
                    other.ToCanonicalString(),
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CharacterVisualMetadataV1);
        }

        public override int GetHashCode()
        {
            return DeterministicHash(ToCanonicalString());
        }

        internal static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A non-empty value is required.",
                    parameterName);
            }

            return value.Trim();
        }

        internal static string Text(StableId stableId)
        {
            return stableId == null ? string.Empty : stableId.ToString();
        }

        internal static void Append(
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

        internal static string Fingerprint(string canonicalText)
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
                builder.Append(
                    digest[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        internal static int DeterministicHash(string value)
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
    }

    public sealed class CharacterSelectionDefinitionV1 :
        IEquatable<CharacterSelectionDefinitionV1>
    {
        private readonly string canonicalText;

        public CharacterSelectionDefinitionV1(
            StableId characterStableId,
            string displayName,
            string description,
            StableId defaultLoadoutProfileStableId,
            CharacterVisualMetadataV1 visualMetadata)
        {
            CharacterStableId = characterStableId
                ?? throw new ArgumentNullException(nameof(characterStableId));
            DisplayName = CharacterVisualMetadataV1.RequireText(
                displayName,
                nameof(displayName));
            Description = CharacterVisualMetadataV1.RequireText(
                description,
                nameof(description));
            DefaultLoadoutProfileStableId = defaultLoadoutProfileStableId
                ?? throw new ArgumentNullException(nameof(defaultLoadoutProfileStableId));
            VisualMetadata = visualMetadata
                ?? throw new ArgumentNullException(nameof(visualMetadata));

            var builder = new StringBuilder();
            CharacterVisualMetadataV1.Append(
                builder,
                "character",
                CharacterStableId.ToString());
            CharacterVisualMetadataV1.Append(builder, "name", DisplayName);
            CharacterVisualMetadataV1.Append(
                builder,
                "description",
                Description);
            CharacterVisualMetadataV1.Append(
                builder,
                "default-profile",
                DefaultLoadoutProfileStableId.ToString());
            CharacterVisualMetadataV1.Append(
                builder,
                "visual-metadata",
                VisualMetadata.ToCanonicalString());
            canonicalText = builder.ToString();
            Fingerprint = CharacterVisualMetadataV1.Fingerprint(canonicalText);
        }

        public StableId CharacterStableId { get; }

        public string DisplayName { get; }

        public string Description { get; }

        public StableId DefaultLoadoutProfileStableId { get; }

        public CharacterVisualMetadataV1 VisualMetadata { get; }

        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(CharacterSelectionDefinitionV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    canonicalText,
                    other.canonicalText,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CharacterSelectionDefinitionV1);
        }

        public override int GetHashCode()
        {
            return CharacterVisualMetadataV1.DeterministicHash(Fingerprint);
        }
    }

    public sealed class CharacterClassProfileDefinitionV1 :
        IEquatable<CharacterClassProfileDefinitionV1>
    {
        private readonly string canonicalText;

        public CharacterClassProfileDefinitionV1(
            StableId loadoutProfileStableId,
            StableId characterStableId,
            CharacterClassKindV1 classKind,
            string displayName,
            string description,
            CharacterVisualMetadataV1 visualMetadata)
        {
            LoadoutProfileStableId = loadoutProfileStableId
                ?? throw new ArgumentNullException(nameof(loadoutProfileStableId));
            CharacterStableId = characterStableId
                ?? throw new ArgumentNullException(nameof(characterStableId));
            if (!Enum.IsDefined(typeof(CharacterClassKindV1), classKind))
            {
                throw new ArgumentOutOfRangeException(nameof(classKind));
            }

            ClassKind = classKind;
            DisplayName = CharacterVisualMetadataV1.RequireText(
                displayName,
                nameof(displayName));
            Description = CharacterVisualMetadataV1.RequireText(
                description,
                nameof(description));
            VisualMetadata = visualMetadata
                ?? throw new ArgumentNullException(nameof(visualMetadata));

            var builder = new StringBuilder();
            CharacterVisualMetadataV1.Append(
                builder,
                "profile",
                LoadoutProfileStableId.ToString());
            CharacterVisualMetadataV1.Append(
                builder,
                "character",
                CharacterStableId.ToString());
            CharacterVisualMetadataV1.Append(
                builder,
                "class",
                ((int)ClassKind).ToString(CultureInfo.InvariantCulture));
            CharacterVisualMetadataV1.Append(builder, "name", DisplayName);
            CharacterVisualMetadataV1.Append(
                builder,
                "description",
                Description);
            CharacterVisualMetadataV1.Append(
                builder,
                "visual-metadata",
                VisualMetadata.ToCanonicalString());
            canonicalText = builder.ToString();
            Fingerprint = CharacterVisualMetadataV1.Fingerprint(canonicalText);
        }

        public StableId LoadoutProfileStableId { get; }

        public StableId CharacterStableId { get; }

        public CharacterClassKindV1 ClassKind { get; }

        public string DisplayName { get; }

        public string Description { get; }

        public CharacterVisualMetadataV1 VisualMetadata { get; }

        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(CharacterClassProfileDefinitionV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    canonicalText,
                    other.canonicalText,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CharacterClassProfileDefinitionV1);
        }

        public override int GetHashCode()
        {
            return CharacterVisualMetadataV1.DeterministicHash(Fingerprint);
        }
    }
}
