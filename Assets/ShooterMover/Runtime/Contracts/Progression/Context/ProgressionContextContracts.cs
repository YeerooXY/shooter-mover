using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Progression.Context;

namespace ShooterMover.Contracts.Progression.Context
{
    /// <summary>
    /// Read-only provider port for the current immutable progression context.
    /// </summary>
    public interface IProgressionContextProvider
    {
        ProgressionContext CurrentContext { get; }
    }

    /// <summary>
    /// Immutable session snapshot pairing one context with its replacement sequence.
    /// </summary>
    public sealed class ProgressionContextSnapshot : IEquatable<ProgressionContextSnapshot>
    {
        private const string SchemaId = "progression-context-snapshot-v1";

        private readonly string _canonicalString;

        private ProgressionContextSnapshot(long sequence, ProgressionContext context)
        {
            if (sequence < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sequence),
                    sequence,
                    "Progression-context sequence must be non-negative.");
            }

            Context = context ?? throw new ArgumentNullException(nameof(context));
            Sequence = sequence;
            _canonicalString =
                "schema="
                + SchemaId
                + "\nsequence="
                + sequence.ToString(CultureInfo.InvariantCulture)
                + "\ncontext_fingerprint="
                + context.Fingerprint;
            Fingerprint = ProgressionContextContractFormat.ComputeSha256(_canonicalString);
        }

        public long Sequence { get; }

        public ProgressionContext Context { get; }

        public string Fingerprint { get; }

        public static ProgressionContextSnapshot Create(long sequence, ProgressionContext context)
        {
            return new ProgressionContextSnapshot(sequence, context);
        }

        public string ToCanonicalString()
        {
            return _canonicalString;
        }

        public bool Equals(ProgressionContextSnapshot other)
        {
            return !ReferenceEquals(other, null)
                && Sequence == other.Sequence
                && Context.Equals(other.Context);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ProgressionContextSnapshot);
        }

        public override int GetHashCode()
        {
            return ProgressionContextContractFormat.DeterministicHash(_canonicalString);
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }
    }

    public enum ProgressionContextReplacementStatus
    {
        Applied = 0,
        DuplicateNoChange = 1,
        Rejected = 2,
    }

    /// <summary>
    /// Immutable fact describing one explicit session-context replacement attempt.
    /// </summary>
    public sealed class ProgressionContextChangeFact
    {
        private ProgressionContextChangeFact(
            ProgressionContextReplacementStatus status,
            ProgressionContextSnapshot previousSnapshot,
            ProgressionContextSnapshot currentSnapshot,
            ProgressionContextValidationResult validation)
        {
            PreviousSnapshot = previousSnapshot
                ?? throw new ArgumentNullException(nameof(previousSnapshot));
            CurrentSnapshot = currentSnapshot
                ?? throw new ArgumentNullException(nameof(currentSnapshot));
            Validation = validation
                ?? throw new ArgumentNullException(nameof(validation));
            Status = status;
        }

        public ProgressionContextReplacementStatus Status { get; }

        public ProgressionContextSnapshot PreviousSnapshot { get; }

        public ProgressionContextSnapshot CurrentSnapshot { get; }

        public ProgressionContextValidationResult Validation { get; }

        public bool Changed => Status == ProgressionContextReplacementStatus.Applied;

        public static ProgressionContextChangeFact Applied(
            ProgressionContextSnapshot previousSnapshot,
            ProgressionContextSnapshot currentSnapshot)
        {
            if (previousSnapshot == null)
            {
                throw new ArgumentNullException(nameof(previousSnapshot));
            }

            if (currentSnapshot == null)
            {
                throw new ArgumentNullException(nameof(currentSnapshot));
            }

            if (currentSnapshot.Sequence != previousSnapshot.Sequence + 1)
            {
                throw new ArgumentException(
                    "An applied context change must increment sequence exactly once.",
                    nameof(currentSnapshot));
            }

            if (currentSnapshot.Context.Equals(previousSnapshot.Context))
            {
                throw new ArgumentException(
                    "An applied context change must contain a different context.",
                    nameof(currentSnapshot));
            }

            return new ProgressionContextChangeFact(
                ProgressionContextReplacementStatus.Applied,
                previousSnapshot,
                currentSnapshot,
                ProgressionContextValidationResult.Valid);
        }

        public static ProgressionContextChangeFact DuplicateNoChange(
            ProgressionContextSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return new ProgressionContextChangeFact(
                ProgressionContextReplacementStatus.DuplicateNoChange,
                snapshot,
                snapshot,
                ProgressionContextValidationResult.Valid);
        }

        public static ProgressionContextChangeFact Rejected(
            ProgressionContextSnapshot snapshot,
            ProgressionContextValidationResult validation)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (validation == null)
            {
                throw new ArgumentNullException(nameof(validation));
            }

            if (validation.IsValid)
            {
                throw new ArgumentException(
                    "A rejected context replacement requires a validation failure.",
                    nameof(validation));
            }

            return new ProgressionContextChangeFact(
                ProgressionContextReplacementStatus.Rejected,
                snapshot,
                snapshot,
                validation);
        }
    }

    internal static class ProgressionContextContractFormat
    {
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public static string ComputeSha256(string canonicalText)
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

        public static int DeterministicHash(string canonicalText)
        {
            unchecked
            {
                uint hash = FnvOffsetBasis;
                for (int index = 0; index < canonicalText.Length; index++)
                {
                    hash ^= canonicalText[index];
                    hash *= FnvPrime;
                }

                return (int)hash;
            }
        }
    }
}
