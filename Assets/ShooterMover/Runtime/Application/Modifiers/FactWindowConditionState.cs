using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Modifiers;
using ShooterMover.Domain.Progression.Skills;

namespace ShooterMover.Application.Modifiers
{
    public enum LiveObservedFactStatus
    {
        Applied = 1,
        ExactDuplicateNoChange = 2,
        ConflictingDuplicate = 3,
        StaleSimulationTick = 4,
        Rejected = 5,
    }

    public sealed class LiveObservedFact
    {
        public LiveObservedFact(
            string factId,
            string factTypeId,
            string subjectId,
            long simulationTick)
        {
            if (string.IsNullOrWhiteSpace(factId))
            {
                throw new ArgumentException(
                    "A fact identity is required.",
                    nameof(factId));
            }
            if (string.IsNullOrWhiteSpace(factTypeId))
            {
                throw new ArgumentException(
                    "A fact type identity is required.",
                    nameof(factTypeId));
            }
            if (string.IsNullOrWhiteSpace(subjectId))
            {
                throw new ArgumentException(
                    "A fact subject identity is required.",
                    nameof(subjectId));
            }
            if (simulationTick < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(simulationTick));
            }

            FactId = factId.Trim();
            FactTypeId = factTypeId.Trim();
            SubjectId = subjectId.Trim();
            SimulationTick = simulationTick;
            Fingerprint = ModifierApplicationFingerprint.Hash(
                FactId
                    + "|"
                    + FactTypeId
                    + "|"
                    + SubjectId
                    + "|"
                    + SimulationTick.ToString(
                        CultureInfo.InvariantCulture));
        }

        public string FactId { get; }

        public string FactTypeId { get; }

        public string SubjectId { get; }

        public long SimulationTick { get; }

        public string Fingerprint { get; }
    }

    public sealed class LiveConditionActivationFact
    {
        public LiveConditionActivationFact(
            string conditionId,
            string subjectId,
            long activationTick,
            long expiresAtTickExclusive)
        {
            ConditionId = conditionId
                ?? throw new ArgumentNullException(nameof(conditionId));
            SubjectId = subjectId
                ?? throw new ArgumentNullException(nameof(subjectId));
            ActivationTick = activationTick;
            ExpiresAtTickExclusive = expiresAtTickExclusive;
            Fingerprint = ModifierApplicationFingerprint.Hash(
                ConditionId
                    + "|"
                    + SubjectId
                    + "|"
                    + ActivationTick.ToString(
                        CultureInfo.InvariantCulture)
                    + "|"
                    + ExpiresAtTickExclusive.ToString(
                        CultureInfo.InvariantCulture));
        }

        public string ConditionId { get; }

        public string SubjectId { get; }

        public long ActivationTick { get; }

        public long ExpiresAtTickExclusive { get; }

        public string Fingerprint { get; }
    }

    public sealed class LiveObservedFactResult
    {
        public LiveObservedFactResult(
            LiveObservedFactStatus status,
            string rejectionCode,
            long latestAcceptedTick,
            IEnumerable<LiveConditionActivationFact> activations)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            LatestAcceptedTick = latestAcceptedTick;
            Activations = new ReadOnlyCollection<
                LiveConditionActivationFact>(
                    (activations
                        ?? Array.Empty<LiveConditionActivationFact>())
                    .ToList());
        }

        public LiveObservedFactStatus Status { get; }

        public string RejectionCode { get; }

        public long LatestAcceptedTick { get; }

        public IReadOnlyList<LiveConditionActivationFact> Activations
        {
            get;
        }
    }

    /// <summary>
    /// Generic deterministic condition runtime for facts such as enemy-killed,
    /// teammate-healed, prop-destroyed, or objective-completed. A killing-spree skill is
    /// just a fact-window definition plus a conditional modifier; it does not need a
    /// skill-specific MonoBehaviour or controller branch.
    /// </summary>
    public sealed class FactWindowConditionState
    {
        private sealed class ReplayRecord
        {
            public ReplayRecord(
                string factFingerprint,
                LiveObservedFactResult result)
            {
                FactFingerprint = factFingerprint;
                Result = result;
            }

            public string FactFingerprint { get; }

            public LiveObservedFactResult Result { get; }
        }

        private readonly string subjectId;
        private readonly IReadOnlyList<FactWindowConditionDefinition>
            definitions;
        private readonly Dictionary<string, List<long>> ticksByCondition =
            new Dictionary<string, List<long>>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> activeUntilExclusive =
            new Dictionary<string, long>(StringComparer.Ordinal);
        private readonly Dictionary<string, ReplayRecord> replay =
            new Dictionary<string, ReplayRecord>(StringComparer.Ordinal);
        private long latestAcceptedTick;

        public FactWindowConditionState(
            string subjectId,
            IEnumerable<FactWindowConditionDefinition> definitions)
        {
            if (string.IsNullOrWhiteSpace(subjectId))
            {
                throw new ArgumentException(
                    "A condition subject identity is required.",
                    nameof(subjectId));
            }

            var items = (definitions
                ?? throw new ArgumentNullException(nameof(definitions)))
                .ToList();
            if (items.Count == 0 || items.Any(item => item == null))
            {
                throw new ArgumentException(
                    "At least one non-null condition definition is required.",
                    nameof(definitions));
            }
            if (items.Select(item => item.ConditionId)
                .Distinct(StringComparer.Ordinal)
                .Count() != items.Count)
            {
                throw new ArgumentException(
                    "Condition identities must be unique.",
                    nameof(definitions));
            }

            this.subjectId = subjectId.Trim();
            this.definitions = new ReadOnlyCollection<
                FactWindowConditionDefinition>(
                    items.OrderBy(
                            item => item.ConditionId,
                            StringComparer.Ordinal)
                        .ToList());
            foreach (FactWindowConditionDefinition definition in
                this.definitions)
            {
                ticksByCondition.Add(
                    definition.ConditionId,
                    new List<long>());
                activeUntilExclusive.Add(definition.ConditionId, 0L);
            }
        }

        public long LatestAcceptedTick
        {
            get { return latestAcceptedTick; }
        }

        public LiveObservedFactResult Apply(LiveObservedFact fact)
        {
            if (fact == null)
            {
                return Reject(
                    LiveObservedFactStatus.Rejected,
                    "modifier-fact-null");
            }
            if (!string.Equals(
                fact.SubjectId,
                subjectId,
                StringComparison.Ordinal))
            {
                return Reject(
                    LiveObservedFactStatus.Rejected,
                    "modifier-fact-subject-mismatch");
            }

            ReplayRecord prior;
            if (replay.TryGetValue(fact.FactId, out prior))
            {
                if (!string.Equals(
                    prior.FactFingerprint,
                    fact.Fingerprint,
                    StringComparison.Ordinal))
                {
                    return Reject(
                        LiveObservedFactStatus.ConflictingDuplicate,
                        "modifier-fact-conflicting-duplicate");
                }
                return new LiveObservedFactResult(
                    LiveObservedFactStatus.ExactDuplicateNoChange,
                    prior.Result.RejectionCode,
                    prior.Result.LatestAcceptedTick,
                    prior.Result.Activations);
            }

            LiveObservedFactResult result;
            if (fact.SimulationTick < latestAcceptedTick)
            {
                result = Reject(
                    LiveObservedFactStatus.StaleSimulationTick,
                    "modifier-fact-tick-stale");
            }
            else
            {
                result = ApplyAccepted(fact);
            }

            replay.Add(
                fact.FactId,
                new ReplayRecord(fact.Fingerprint, result));
            return result;
        }

        public IReadOnlyList<string> ActiveConditionIdsAt(
            long simulationTick)
        {
            if (simulationTick < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(simulationTick));
            }

            return new ReadOnlyCollection<string>(
                activeUntilExclusive
                    .Where(pair => pair.Value > simulationTick)
                    .Select(pair => pair.Key)
                    .OrderBy(item => item, StringComparer.Ordinal)
                    .ToList());
        }

        public bool IsConditionActive(
            string conditionId,
            long simulationTick)
        {
            long until;
            return conditionId != null
                && activeUntilExclusive.TryGetValue(conditionId, out until)
                && until > simulationTick;
        }

        private LiveObservedFactResult ApplyAccepted(
            LiveObservedFact fact)
        {
            latestAcceptedTick = fact.SimulationTick;
            var activations = new List<LiveConditionActivationFact>();

            foreach (FactWindowConditionDefinition definition in definitions)
            {
                if (!string.Equals(
                    definition.ObservedFactTypeId,
                    fact.FactTypeId,
                    StringComparison.Ordinal))
                {
                    continue;
                }

                List<long> ticks = ticksByCondition[definition.ConditionId];
                long earliestIncludedTick = Math.Max(
                    0L,
                    fact.SimulationTick - definition.WindowTicks + 1L);
                ticks.RemoveAll(item => item < earliestIncludedTick);
                ticks.Add(fact.SimulationTick);

                if (ticks.Count < definition.RequiredFactCount)
                {
                    continue;
                }

                long expiresAtExclusive = checked(
                    fact.SimulationTick + definition.ActiveDurationTicks);
                activeUntilExclusive[definition.ConditionId] = Math.Max(
                    activeUntilExclusive[definition.ConditionId],
                    expiresAtExclusive);
                activations.Add(
                    new LiveConditionActivationFact(
                        definition.ConditionId,
                        subjectId,
                        fact.SimulationTick,
                        expiresAtExclusive));
                if (definition.ConsumeWindowOnActivation)
                {
                    ticks.Clear();
                }
            }

            return new LiveObservedFactResult(
                LiveObservedFactStatus.Applied,
                string.Empty,
                latestAcceptedTick,
                activations);
        }

        private LiveObservedFactResult Reject(
            LiveObservedFactStatus status,
            string rejectionCode)
        {
            return new LiveObservedFactResult(
                status,
                rejectionCode,
                latestAcceptedTick,
                null);
        }
    }

    /// <summary>
    /// Narrow adapter from the existing ranked-skill effect projection into the shared
    /// runtime modifier language. Existing and future stat-only skills remain data-only.
    /// </summary>
    public static class SkillEffectModifierBridge
    {
        public static LiveModifierSnapshot Adapt(
            SkillEffectSnapshot skillEffects)
        {
            if (skillEffects == null)
            {
                throw new ArgumentNullException(nameof(skillEffects));
            }

            return new LiveModifierSnapshot(
                skillEffects.Contributions.Select(
                    contribution => new LiveModifierDefinition(
                        contribution.SourceId,
                        contribution.Effect.StatId,
                        Map(contribution.Effect.Kind),
                        contribution.Effect.Value,
                        contribution.Effect.ConditionId)));
        }

        private static LiveModifierOperation Map(
            SkillModifierKind kind)
        {
            switch (kind)
            {
                case SkillModifierKind.Flat:
                case SkillModifierKind.IntegerCapacity:
                    return LiveModifierOperation.Flat;
                case SkillModifierKind.Percentage:
                    return LiveModifierOperation.Percentage;
                case SkillModifierKind.Multiplicative:
                    return LiveModifierOperation.Multiplicative;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }

    internal static class ModifierApplicationFingerprint
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
