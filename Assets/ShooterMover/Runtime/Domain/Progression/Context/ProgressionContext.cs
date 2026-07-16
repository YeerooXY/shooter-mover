using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Progression.Context
{
    /// <summary>
    /// Stable validation codes for progression-context construction and replacement.
    /// </summary>
    public enum ProgressionContextValidationCode
    {
        None = 0,
        ContextMissing = 1,
        CharacterLevelNegative = 2,
        RegionLevelNegative = 3,
        DifficultyIdentityMissing = 4,
        DifficultyValueNegative = 5,
        ProgressionTagMissing = 6,
    }

    /// <summary>
    /// Immutable, engine-independent validation outcome.
    /// </summary>
    public sealed class ProgressionContextValidationResult
        : IEquatable<ProgressionContextValidationResult>
    {
        private static readonly ProgressionContextValidationResult ValidResult =
            new ProgressionContextValidationResult(
                ProgressionContextValidationCode.None,
                string.Empty,
                string.Empty);

        private ProgressionContextValidationResult(
            ProgressionContextValidationCode code,
            string fieldName,
            string message)
        {
            Code = code;
            FieldName = fieldName;
            Message = message;
        }

        public ProgressionContextValidationCode Code { get; }

        public string FieldName { get; }

        public string Message { get; }

        public bool IsValid => Code == ProgressionContextValidationCode.None;

        public static ProgressionContextValidationResult Valid => ValidResult;

        public static ProgressionContextValidationResult Failure(
            ProgressionContextValidationCode code,
            string fieldName,
            string message)
        {
            if (code == ProgressionContextValidationCode.None)
            {
                throw new ArgumentException(
                    "A validation failure must use a non-success code.",
                    nameof(code));
            }

            if (fieldName == null)
            {
                throw new ArgumentNullException(nameof(fieldName));
            }

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return new ProgressionContextValidationResult(code, fieldName, message);
        }

        public bool Equals(ProgressionContextValidationResult other)
        {
            return !ReferenceEquals(other, null)
                && Code == other.Code
                && string.Equals(FieldName, other.FieldName, StringComparison.Ordinal)
                && string.Equals(Message, other.Message, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ProgressionContextValidationResult);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = AddToHash(hash, ((int)Code).ToString(CultureInfo.InvariantCulture));
                hash = AddToHash(hash, FieldName);
                hash = AddToHash(hash, Message);
                return (int)hash;
            }
        }

        private static uint AddToHash(uint hash, string value)
        {
            unchecked
            {
                for (int index = 0; index < value.Length; index++)
                {
                    hash ^= value[index];
                    hash *= 16777619u;
                }

                hash ^= '\n';
                hash *= 16777619u;
                return hash;
            }
        }
    }

    /// <summary>
    /// Immutable character, region, difficulty, and canonical-tag progression snapshot.
    /// </summary>
    public sealed class ProgressionContext : IEquatable<ProgressionContext>
    {
        private const string SchemaId = "progression-context-v1";
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        private readonly ReadOnlyCollection<StableId> _progressionTags;
        private readonly string _canonicalString;

        private ProgressionContext(
            int characterLevel,
            int regionLevel,
            StableId difficultyId,
            int difficultyValue,
            StableId[] canonicalTags)
        {
            CharacterLevel = characterLevel;
            RegionLevel = regionLevel;
            DifficultyId = difficultyId;
            DifficultyValue = difficultyValue;
            _progressionTags = Array.AsReadOnly(canonicalTags);
            _canonicalString = BuildCanonicalString();
            Fingerprint = ComputeSha256Fingerprint(_canonicalString);
        }

        public int CharacterLevel { get; }

        public int RegionLevel { get; }

        public StableId DifficultyId { get; }

        public int DifficultyValue { get; }

        public IReadOnlyList<StableId> ProgressionTags => _progressionTags;

        /// <summary>
        /// Gets the canonical sha256 fingerprint of the complete immutable context.
        /// </summary>
        public string Fingerprint { get; }

        public static ProgressionContext Create(
            int characterLevel,
            int regionLevel,
            StableId difficultyId,
            int difficultyValue,
            IEnumerable<StableId> progressionTags = null)
        {
            ProgressionContext context;
            ProgressionContextValidationResult validation;
            if (!TryCreate(
                characterLevel,
                regionLevel,
                difficultyId,
                difficultyValue,
                progressionTags,
                out context,
                out validation))
            {
                throw new ArgumentException(validation.Message, validation.FieldName);
            }

            return context;
        }

        public static bool TryCreate(
            int characterLevel,
            int regionLevel,
            StableId difficultyId,
            int difficultyValue,
            IEnumerable<StableId> progressionTags,
            out ProgressionContext context,
            out ProgressionContextValidationResult validation)
        {
            context = null;

            if (characterLevel < 0)
            {
                validation = ProgressionContextValidationResult.Failure(
                    ProgressionContextValidationCode.CharacterLevelNegative,
                    nameof(characterLevel),
                    "Character level must be non-negative.");
                return false;
            }

            if (regionLevel < 0)
            {
                validation = ProgressionContextValidationResult.Failure(
                    ProgressionContextValidationCode.RegionLevelNegative,
                    nameof(regionLevel),
                    "Region level must be non-negative.");
                return false;
            }

            if (difficultyId == null)
            {
                validation = ProgressionContextValidationResult.Failure(
                    ProgressionContextValidationCode.DifficultyIdentityMissing,
                    nameof(difficultyId),
                    "Difficulty identity is required.");
                return false;
            }

            if (difficultyValue < 0)
            {
                validation = ProgressionContextValidationResult.Failure(
                    ProgressionContextValidationCode.DifficultyValueNegative,
                    nameof(difficultyValue),
                    "Difficulty value must be non-negative.");
                return false;
            }

            var canonicalTags = new SortedSet<StableId>();
            if (progressionTags != null)
            {
                foreach (StableId progressionTag in progressionTags)
                {
                    if (progressionTag == null)
                    {
                        validation = ProgressionContextValidationResult.Failure(
                            ProgressionContextValidationCode.ProgressionTagMissing,
                            nameof(progressionTags),
                            "Progression tags must not contain null entries.");
                        return false;
                    }

                    canonicalTags.Add(progressionTag);
                }
            }

            var tagArray = new StableId[canonicalTags.Count];
            canonicalTags.CopyTo(tagArray);
            context = new ProgressionContext(
                characterLevel,
                regionLevel,
                difficultyId,
                difficultyValue,
                tagArray);
            validation = ProgressionContextValidationResult.Valid;
            return true;
        }

        public string ToCanonicalString()
        {
            return _canonicalString;
        }

        public bool Equals(ProgressionContext other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    _canonicalString,
                    other._canonicalString,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ProgressionContext);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                uint hash = FnvOffsetBasis;
                for (int index = 0; index < _canonicalString.Length; index++)
                {
                    hash ^= _canonicalString[index];
                    hash *= FnvPrime;
                }

                return (int)hash;
            }
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }

        public static bool operator ==(ProgressionContext left, ProgressionContext right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
            {
                return false;
            }

            return left.Equals(right);
        }

        public static bool operator !=(ProgressionContext left, ProgressionContext right)
        {
            return !(left == right);
        }

        private string BuildCanonicalString()
        {
            var builder = new StringBuilder();
            builder.Append("schema=").Append(SchemaId);
            builder.Append("\ncharacter_level=")
                .Append(CharacterLevel.ToString(CultureInfo.InvariantCulture));
            builder.Append("\nregion_level=")
                .Append(RegionLevel.ToString(CultureInfo.InvariantCulture));
            builder.Append("\ndifficulty_id=").Append(DifficultyId.ToString());
            builder.Append("\ndifficulty_value=")
                .Append(DifficultyValue.ToString(CultureInfo.InvariantCulture));
            builder.Append("\ntag_count=")
                .Append(_progressionTags.Count.ToString(CultureInfo.InvariantCulture));

            for (int index = 0; index < _progressionTags.Count; index++)
            {
                builder.Append("\ntag=").Append(_progressionTags[index].ToString());
            }

            return builder.ToString();
        }

        private static string ComputeSha256Fingerprint(string canonicalText)
        {
            byte[] input = Encoding.UTF8.GetBytes(canonicalText);
            byte[] digest;
            using (SHA256 algorithm = SHA256.Create())
            {
                digest = algorithm.ComputeHash(input);
            }

            var builder = new StringBuilder("sha256:", 71);
            for (int index = 0; index < digest.Length; index++)
            {
                builder.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }
}
