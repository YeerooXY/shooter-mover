using System;
using System.Globalization;

namespace ShooterMover.Domain.Common.Random
{
    /// <summary>
    /// Immutable observation of deterministic random state. Creating or reading a trace never
    /// advances the observed stream.
    /// </summary>
    public sealed class DeterministicRandomTrace : IEquatable<DeterministicRandomTrace>
    {
        internal DeterministicRandomTrace(
            int algorithmVersion,
            ulong rootSeed,
            ulong streamSeed,
            ulong state,
            ulong samplesConsumed)
        {
            DeterministicRandom.EnsureSupportedVersion(algorithmVersion);
            AlgorithmVersion = algorithmVersion;
            RootSeed = rootSeed;
            StreamSeed = streamSeed;
            State = state;
            SamplesConsumed = samplesConsumed;
            Fingerprint = ComputeFingerprint(
                algorithmVersion,
                rootSeed,
                streamSeed,
                state,
                samplesConsumed);
        }

        public int AlgorithmVersion { get; }

        public ulong RootSeed { get; }

        public ulong StreamSeed { get; }

        public ulong State { get; }

        public ulong SamplesConsumed { get; }

        public string Fingerprint { get; }

        public bool Equals(DeterministicRandomTrace other)
        {
            return !ReferenceEquals(other, null)
                && AlgorithmVersion == other.AlgorithmVersion
                && RootSeed == other.RootSeed
                && StreamSeed == other.StreamSeed
                && State == other.State
                && SamplesConsumed == other.SamplesConsumed
                && string.Equals(Fingerprint, other.Fingerprint, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as DeterministicRandomTrace);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = AlgorithmVersion;
                hash = (hash * 397) ^ RootSeed.GetHashCode();
                hash = (hash * 397) ^ StreamSeed.GetHashCode();
                hash = (hash * 397) ^ State.GetHashCode();
                hash = (hash * 397) ^ SamplesConsumed.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "v{0}:root={1:x16};stream={2:x16};state={3:x16};samples={4};fp={5}",
                AlgorithmVersion,
                RootSeed,
                StreamSeed,
                State,
                SamplesConsumed,
                Fingerprint);
        }

        private static string ComputeFingerprint(
            int algorithmVersion,
            ulong rootSeed,
            ulong streamSeed,
            ulong state,
            ulong samplesConsumed)
        {
            ulong hash = DeterministicRandom.FnvOffsetBasis;
            hash = DeterministicRandom.AppendFnvAscii(hash, "sm-rng-trace-v1");
            hash = DeterministicRandom.AppendFnvUInt32LittleEndian(hash, (uint)algorithmVersion);
            hash = DeterministicRandom.AppendFnvUInt64LittleEndian(hash, rootSeed);
            hash = DeterministicRandom.AppendFnvUInt64LittleEndian(hash, streamSeed);
            hash = DeterministicRandom.AppendFnvUInt64LittleEndian(hash, state);
            hash = DeterministicRandom.AppendFnvUInt64LittleEndian(hash, samplesConsumed);
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }
    }
}
