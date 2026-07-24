using System;
using System.Collections.Generic;
using System.Globalization;

namespace ShooterMover.Application.Rewards.Strongboxes.Simulation
{
    /// <summary>
    /// Canonical augment-bias decoding and averaging. Inputs must already be in
    /// deterministic key order; report distributions satisfy that contract.
    /// </summary>
    public static class StrongboxSimulationBiasMath
    {
        public static double Average(IReadOnlyList<StrongboxDistributionEntry> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            long count = 0L;
            double total = 0d;
            for (int index = 0; index < values.Count; index++)
            {
                StrongboxDistributionEntry entry = values[index]
                    ?? throw new StrongboxSimulationIntegrityException(
                        "strongbox-simulation-bias-entry-null");
                double value;
                if (!TryParseKey(entry.Key, out value))
                    throw new StrongboxSimulationIntegrityException(
                        "strongbox-simulation-bias-key-invalid");
                if (entry.Count < 0L)
                    throw new StrongboxSimulationIntegrityException(
                        "strongbox-simulation-bias-count-negative");
                count = checked(count + entry.Count);
                total += value * entry.Count;
            }
            return count == 0L ? 0d : total / count;
        }

        public static double Average(SortedDictionary<string, long> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            long count = 0L;
            double total = 0d;
            foreach (KeyValuePair<string, long> pair in values)
            {
                double value;
                if (!TryParseKey(pair.Key, out value))
                    throw new StrongboxSimulationIntegrityException(
                        "strongbox-simulation-bias-key-invalid");
                if (pair.Value < 0L)
                    throw new StrongboxSimulationIntegrityException(
                        "strongbox-simulation-bias-count-negative");
                count = checked(count + pair.Value);
                total += value * pair.Value;
            }
            return count == 0L ? 0d : total / count;
        }

        public static bool TryParseKey(string key, out double value)
        {
            value = 0d;
            if (string.IsNullOrEmpty(key)
                || !key.StartsWith("bits:", StringComparison.Ordinal))
                return false;
            long bits;
            if (!long.TryParse(
                    key.Substring(5),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out bits))
                return false;
            value = BitConverter.Int64BitsToDouble(bits);
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
