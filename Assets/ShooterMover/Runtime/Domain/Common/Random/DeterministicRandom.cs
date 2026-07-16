using System;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Common.Random
{
    /// <summary>
    /// Immutable SplitMix64 version 1 stream with explicit state and deterministic named forks.
    /// </summary>
    public readonly struct DeterministicRandom : IEquatable<DeterministicRandom>
    {
        public const int AlgorithmVersion1 = 1;
        public const int CurrentAlgorithmVersion = AlgorithmVersion1;

        private const ulong Gamma = 0x9E3779B97F4A7C15UL;
        private const ulong MixMultiplier1 = 0xBF58476D1CE4E5B9UL;
        private const ulong MixMultiplier2 = 0x94D049BB133111EBUL;
        private const ulong FnvOffsetBasis64 = 14695981039346656037UL;
        private const ulong FnvPrime64 = 1099511628211UL;
        private const double Unit53Scale = 1.0 / 9007199254740992.0;

        private readonly ulong _state;

        private DeterministicRandom(
            ulong rootSeed,
            ulong streamSeed,
            ulong state,
            ulong samplesConsumed,
            int algorithmVersion)
        {
            RootSeed = rootSeed;
            StreamSeed = streamSeed;
            _state = state;
            SamplesConsumed = samplesConsumed;
            AlgorithmVersion = algorithmVersion;
        }

        public int AlgorithmVersion { get; }

        public ulong RootSeed { get; }

        public ulong StreamSeed { get; }

        public ulong State => _state;

        public ulong SamplesConsumed { get; }

        public static DeterministicRandom Create(
            ulong rootSeed,
            int algorithmVersion = CurrentAlgorithmVersion)
        {
            EnsureSupportedVersion(algorithmVersion);
            return new DeterministicRandom(rootSeed, rootSeed, rootSeed, 0UL, algorithmVersion);
        }

        public static DeterministicRandom CreateSubstream(
            ulong rootSeed,
            int algorithmVersion,
            StableId purposeId,
            ulong ordinal)
        {
            EnsureSupportedVersion(algorithmVersion);
            if (purposeId == null)
            {
                throw new ArgumentNullException(nameof(purposeId));
            }

            ulong streamSeed = DeriveSubstreamSeed(rootSeed, algorithmVersion, purposeId, ordinal);
            return new DeterministicRandom(rootSeed, streamSeed, streamSeed, 0UL, algorithmVersion);
        }

        /// <summary>
        /// Derives an isolated stream from the original root seed without consuming this stream.
        /// </summary>
        public DeterministicRandom Fork(StableId purposeId, ulong ordinal = 0UL)
        {
            EnsureUsable();
            return CreateSubstream(RootSeed, AlgorithmVersion, purposeId, ordinal);
        }

        /// <summary>
        /// Returns the next immutable stream state and writes one SplitMix64 sample.
        /// </summary>
        public DeterministicRandom NextUInt64(out ulong sample)
        {
            EnsureUsable();
            if (SamplesConsumed == ulong.MaxValue)
            {
                throw new InvalidOperationException("The deterministic random sample counter is exhausted.");
            }

            ulong nextState;
            unchecked
            {
                nextState = _state + Gamma;
            }

            sample = Mix64(nextState);
            return new DeterministicRandom(
                RootSeed,
                StreamSeed,
                nextState,
                SamplesConsumed + 1UL,
                AlgorithmVersion);
        }

        /// <summary>
        /// Uses the high 32 bits of one version 1 sample.
        /// </summary>
        public DeterministicRandom NextUInt32(out uint sample)
        {
            DeterministicRandom next = NextUInt64(out ulong wideSample);
            sample = (uint)(wideSample >> 32);
            return next;
        }

        /// <summary>
        /// Samples uniformly from [0, exclusiveUpperBound) with rejection sampling.
        /// </summary>
        public DeterministicRandom NextBoundedUInt64(
            ulong exclusiveUpperBound,
            out ulong sample)
        {
            EnsureUsable();
            if (exclusiveUpperBound == 0UL)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(exclusiveUpperBound),
                    "The exclusive upper bound must be positive.");
            }

            ulong threshold;
            unchecked
            {
                threshold = (0UL - exclusiveUpperBound) % exclusiveUpperBound;
            }

            DeterministicRandom cursor = this;
            while (true)
            {
                cursor = cursor.NextUInt64(out ulong candidate);
                if (candidate >= threshold)
                {
                    sample = candidate % exclusiveUpperBound;
                    return cursor;
                }
            }
        }

        public DeterministicRandom NextInt32(
            int exclusiveUpperBound,
            out int sample)
        {
            return NextInt32(0, exclusiveUpperBound, out sample);
        }

        public DeterministicRandom NextInt32(
            int inclusiveLowerBound,
            int exclusiveUpperBound,
            out int sample)
        {
            EnsureUsable();
            if (exclusiveUpperBound <= inclusiveLowerBound)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(exclusiveUpperBound),
                    "The exclusive upper bound must be greater than the inclusive lower bound.");
            }

            ulong range = (ulong)((long)exclusiveUpperBound - inclusiveLowerBound);
            DeterministicRandom next = NextBoundedUInt64(range, out ulong offset);
            sample = (int)((long)inclusiveLowerBound + (long)offset);
            return next;
        }

        /// <summary>
        /// Samples an IEEE-754 double from [0, 1) using the high 53 sample bits.
        /// </summary>
        public DeterministicRandom NextUnitInterval(out double sample)
        {
            DeterministicRandom next = NextUInt64(out ulong wideSample);
            sample = (wideSample >> 11) * Unit53Scale;
            return next;
        }

        /// <summary>
        /// Performs an exact rational probability roll: success iff an unbiased sample from
        /// [0, denominator) is less than numerator. Valid zero and one probabilities consume
        /// the same bounded-sampling path as every other probability.
        /// </summary>
        public DeterministicRandom NextChance(
            ulong numerator,
            ulong denominator,
            out bool success)
        {
            EnsureUsable();
            if (denominator == 0UL)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(denominator),
                    "The probability denominator must be positive.");
            }

            if (numerator > denominator)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(numerator),
                    "The probability numerator must not exceed the denominator.");
            }

            DeterministicRandom next = NextBoundedUInt64(denominator, out ulong roll);
            success = roll < numerator;
            return next;
        }

        public DeterministicRandomTrace GetTrace()
        {
            EnsureUsable();
            return new DeterministicRandomTrace(
                AlgorithmVersion,
                RootSeed,
                StreamSeed,
                _state,
                SamplesConsumed);
        }

        public bool Equals(DeterministicRandom other)
        {
            return AlgorithmVersion == other.AlgorithmVersion
                && RootSeed == other.RootSeed
                && StreamSeed == other.StreamSeed
                && _state == other._state
                && SamplesConsumed == other.SamplesConsumed;
        }

        public override bool Equals(object obj)
        {
            return obj is DeterministicRandom other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = AlgorithmVersion;
                hash = (hash * 397) ^ RootSeed.GetHashCode();
                hash = (hash * 397) ^ StreamSeed.GetHashCode();
                hash = (hash * 397) ^ _state.GetHashCode();
                hash = (hash * 397) ^ SamplesConsumed.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(DeterministicRandom left, DeterministicRandom right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DeterministicRandom left, DeterministicRandom right)
        {
            return !left.Equals(right);
        }

        internal static void EnsureSupportedVersion(int algorithmVersion)
        {
            if (algorithmVersion != AlgorithmVersion1)
            {
                throw new NotSupportedException(
                    $"Deterministic random algorithm version {algorithmVersion} is not supported.");
            }
        }

        internal static ulong Mix64(ulong value)
        {
            unchecked
            {
                value = (value ^ (value >> 30)) * MixMultiplier1;
                value = (value ^ (value >> 27)) * MixMultiplier2;
                return value ^ (value >> 31);
            }
        }

        internal static ulong AppendFnvByte(ulong hash, byte value)
        {
            unchecked
            {
                return (hash ^ value) * FnvPrime64;
            }
        }

        internal static ulong AppendFnvUInt32LittleEndian(ulong hash, uint value)
        {
            for (int shift = 0; shift < 32; shift += 8)
            {
                hash = AppendFnvByte(hash, (byte)(value >> shift));
            }

            return hash;
        }

        internal static ulong AppendFnvUInt64LittleEndian(ulong hash, ulong value)
        {
            for (int shift = 0; shift < 64; shift += 8)
            {
                hash = AppendFnvByte(hash, (byte)(value >> shift));
            }

            return hash;
        }

        internal static ulong AppendFnvAscii(ulong hash, string text)
        {
            for (int index = 0; index < text.Length; index++)
            {
                char character = text[index];
                if (character > 0x7F)
                {
                    throw new InvalidOperationException("Canonical random trace input must be ASCII.");
                }

                hash = AppendFnvByte(hash, (byte)character);
            }

            return hash;
        }

        internal static ulong FnvOffsetBasis => FnvOffsetBasis64;

        private static ulong DeriveSubstreamSeed(
            ulong rootSeed,
            int algorithmVersion,
            StableId purposeId,
            ulong ordinal)
        {
            ulong hash = FnvOffsetBasis64;
            hash = AppendFnvAscii(hash, "sm-rng-substream-v1");
            hash = AppendFnvUInt64LittleEndian(hash, rootSeed);
            hash = AppendFnvUInt32LittleEndian(hash, (uint)algorithmVersion);
            hash = AppendFnvAscii(hash, purposeId.ToString());
            hash = AppendFnvByte(hash, 0xFF);
            hash = AppendFnvUInt64LittleEndian(hash, ordinal);
            return Mix64(hash);
        }

        private void EnsureUsable()
        {
            EnsureSupportedVersion(AlgorithmVersion);
        }
    }
}
