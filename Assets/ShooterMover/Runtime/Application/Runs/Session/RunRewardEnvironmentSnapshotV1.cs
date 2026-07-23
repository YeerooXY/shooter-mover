using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Drops;

namespace ShooterMover.Application.Runs.Session
{
    /// <summary>
    /// Immutable run-local reward environment. The mission/game-mode composition owns
    /// these authored inputs; terminal adapters only consume the frozen snapshot.
    /// </summary>
    public sealed class RunRewardEnvironmentSnapshotV1
    {
        private readonly ReadOnlyCollection<StableId> eventModifierIds;
        private readonly string canonicalText;

        public RunRewardEnvironmentSnapshotV1(
            StableId gameModeStableId,
            IEnumerable<StableId> eventModifierIds,
            int moneyQuantityMultiplierPermille,
            int scrapQuantityMultiplierPermille,
            RunDropPacingPolicyV1 pacingPolicy)
        {
            GameModeStableId = gameModeStableId
                ?? throw new ArgumentNullException(nameof(gameModeStableId));
            if (moneyQuantityMultiplierPermille < 0
                || scrapQuantityMultiplierPermille < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(moneyQuantityMultiplierPermille));
            }
            MoneyQuantityMultiplierPermille = moneyQuantityMultiplierPermille;
            ScrapQuantityMultiplierPermille = scrapQuantityMultiplierPermille;
            PacingPolicy = pacingPolicy
                ?? throw new ArgumentNullException(nameof(pacingPolicy));

            var ordered = new SortedSet<StableId>();
            if (eventModifierIds != null)
            {
                foreach (StableId value in eventModifierIds)
                {
                    if (value == null)
                    {
                        throw new ArgumentException(
                            "Event modifier identities must not contain null entries.",
                            nameof(eventModifierIds));
                    }
                    ordered.Add(value);
                }
            }
            this.eventModifierIds = new ReadOnlyCollection<StableId>(
                new List<StableId>(ordered));

            var builder = new StringBuilder(
                "schema=run-reward-environment-snapshot-v1");
            builder.Append("\ngame_mode_id=").Append(GameModeStableId)
                .Append("\nmoney_multiplier_permille=")
                .Append(MoneyQuantityMultiplierPermille.ToString(
                    CultureInfo.InvariantCulture))
                .Append("\nscrap_multiplier_permille=")
                .Append(ScrapQuantityMultiplierPermille.ToString(
                    CultureInfo.InvariantCulture))
                .Append("\npacing_policy=").Append(PacingPolicy.Fingerprint)
                .Append("\nevent_modifier_count=")
                .Append(this.eventModifierIds.Count.ToString(
                    CultureInfo.InvariantCulture));
            for (int index = 0; index < this.eventModifierIds.Count; index++)
            {
                builder.Append("\nevent_modifier_")
                    .Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append("=")
                    .Append(this.eventModifierIds[index]);
            }
            canonicalText = builder.ToString();
            Fingerprint = RunSessionFingerprintV1.Hash(canonicalText);
        }

        public StableId GameModeStableId { get; }
        public IReadOnlyList<StableId> EventModifierIds
        {
            get { return eventModifierIds; }
        }
        public int MoneyQuantityMultiplierPermille { get; }
        public int ScrapQuantityMultiplierPermille { get; }
        public RunDropPacingPolicyV1 PacingPolicy { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return canonicalText;
        }
    }
}
