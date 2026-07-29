using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Characters.Selection
{
    public enum CharacterClassKind
    {
        Aggressive = 1,
        Defensive = 2,
        Healer = 3,
    }

    /// <summary>
    /// Stable presentation metadata only. These identities are extension points for future
    /// body, armor, and visual composition; they do not own stats, equipment, or inventory.
    /// </summary>
    public sealed class CharacterVisualMetadata : IEquatable<CharacterVisualMetadata>
    {
        public CharacterVisualMetadata(
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

        public bool Equals(CharacterVisualMetadata other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    ToCanonicalString(),
                    other.ToCanonicalString(),
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CharacterVisualMetadata);
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

    public sealed class CharacterSelectionDefinition :
        IEquatable<CharacterSelectionDefinition>
    {
        private readonly string canonicalText;

        public CharacterSelectionDefinition(
            StableId characterStableId,
            string displayName,
            string description,
            StableId defaultLoadoutProfileStableId,
            CharacterVisualMetadata visualMetadata)
        {
            CharacterStableId = characterStableId
                ?? throw new ArgumentNullException(nameof(characterStableId));
            DisplayName = CharacterVisualMetadata.RequireText(
                displayName,
                nameof(displayName));
            Description = CharacterVisualMetadata.RequireText(
                description,
                nameof(description));
            DefaultLoadoutProfileStableId = defaultLoadoutProfileStableId
                ?? throw new ArgumentNullException(nameof(defaultLoadoutProfileStableId));
            VisualMetadata = visualMetadata
                ?? throw new ArgumentNullException(nameof(visualMetadata));

            var builder = new StringBuilder();
            CharacterVisualMetadata.Append(
                builder,
                "character",
                CharacterStableId.ToString());
            CharacterVisualMetadata.Append(builder, "name", DisplayName);
            CharacterVisualMetadata.Append(
                builder,
                "description",
                Description);
            CharacterVisualMetadata.Append(
                builder,
                "default-profile",
                DefaultLoadoutProfileStableId.ToString());
            CharacterVisualMetadata.Append(
                builder,
                "visual-metadata",
                VisualMetadata.ToCanonicalString());
            canonicalText = builder.ToString();
            Fingerprint = CharacterVisualMetadata.Fingerprint(canonicalText);
        }

        public StableId CharacterStableId { get; }

        public string DisplayName { get; }

        public string Description { get; }

        public StableId DefaultLoadoutProfileStableId { get; }

        public CharacterVisualMetadata VisualMetadata { get; }

        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(CharacterSelectionDefinition other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    canonicalText,
                    other.canonicalText,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CharacterSelectionDefinition);
        }

        public override int GetHashCode()
        {
            return CharacterVisualMetadata.DeterministicHash(Fingerprint);
        }
    }

    public sealed class CharacterClassProfileDefinition :
        IEquatable<CharacterClassProfileDefinition>
    {
        private readonly string canonicalText;

        public CharacterClassProfileDefinition(
            StableId loadoutProfileStableId,
            StableId characterStableId,
            CharacterClassKind classKind,
            string displayName,
            string description,
            CharacterVisualMetadata visualMetadata)
        {
            LoadoutProfileStableId = loadoutProfileStableId
                ?? throw new ArgumentNullException(nameof(loadoutProfileStableId));
            CharacterStableId = characterStableId
                ?? throw new ArgumentNullException(nameof(characterStableId));
            if (!Enum.IsDefined(typeof(CharacterClassKind), classKind))
            {
                throw new ArgumentOutOfRangeException(nameof(classKind));
            }

            ClassKind = classKind;
            DisplayName = CharacterVisualMetadata.RequireText(
                displayName,
                nameof(displayName));
            Description = CharacterVisualMetadata.RequireText(
                description,
                nameof(description));
            VisualMetadata = visualMetadata
                ?? throw new ArgumentNullException(nameof(visualMetadata));

            var builder = new StringBuilder();
            CharacterVisualMetadata.Append(
                builder,
                "profile",
                LoadoutProfileStableId.ToString());
            CharacterVisualMetadata.Append(
                builder,
                "character",
                CharacterStableId.ToString());
            CharacterVisualMetadata.Append(
                builder,
                "class",
                ((int)ClassKind).ToString(CultureInfo.InvariantCulture));
            CharacterVisualMetadata.Append(builder, "name", DisplayName);
            CharacterVisualMetadata.Append(
                builder,
                "description",
                Description);
            CharacterVisualMetadata.Append(
                builder,
                "visual-metadata",
                VisualMetadata.ToCanonicalString());
            canonicalText = builder.ToString();
            Fingerprint = CharacterVisualMetadata.Fingerprint(canonicalText);
        }

        public StableId LoadoutProfileStableId { get; }

        public StableId CharacterStableId { get; }

        public CharacterClassKind ClassKind { get; }

        public string DisplayName { get; }

        public string Description { get; }

        public CharacterVisualMetadata VisualMetadata { get; }

        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(CharacterClassProfileDefinition other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    canonicalText,
                    other.canonicalText,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CharacterClassProfileDefinition);
        }

        public override int GetHashCode()
        {
            return CharacterVisualMetadata.DeterministicHash(Fingerprint);
        }
    }
}
