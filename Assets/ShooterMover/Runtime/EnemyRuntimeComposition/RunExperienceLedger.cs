using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using ShooterMover.Application.Progression.Experience;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Progression.Experience;

namespace ShooterMover.EnemyRuntimeComposition
{
    public sealed class RunExperienceKillRecord
    {
        internal RunExperienceKillRecord(
            EnemyDeathFact fact,
            long experience,
            string fingerprint)
        {
            DeathEventStableId = fact.DeathEventStableId;
            EnemyActorStableId = fact.Identity.EntityInstanceId;
            EnemyDefinitionStableId = fact.DefinitionStableId;
            ExperienceProfileStableId = fact.ExperienceProfileStableId;
            Tier = fact.Level;
            RoomStableId = fact.Identity.RoomStableId;
            KillerParticipantStableId = fact.KillerRunParticipantStableId;
            Experience = experience;
            Fingerprint = fingerprint;
        }

        public StableId DeathEventStableId { get; }
        public StableId EnemyActorStableId { get; }
        public StableId EnemyDefinitionStableId { get; }
        public StableId ExperienceProfileStableId { get; }
        public int Tier { get; }
        public StableId RoomStableId { get; }
        public StableId KillerParticipantStableId { get; }
        public long Experience { get; }
        public string Fingerprint { get; }
    }

    public sealed class RunExperienceLedgerSnapshot
    {
        internal RunExperienceLedgerSnapshot(
            StableId runStableId,
            StableId participantStableId,
            IEnumerable<RunExperienceKillRecord> kills,
            long enemyExperience)
        {
            RunStableId = runStableId;
            ParticipantStableId = participantStableId;
            Kills = new ReadOnlyCollection<RunExperienceKillRecord>(
                new List<RunExperienceKillRecord>(kills));
            EnemyExperience = enemyExperience;
        }

        public StableId RunStableId { get; }
        public StableId ParticipantStableId { get; }
        public IReadOnlyList<RunExperienceKillRecord> Kills { get; }
        public int EnemiesKilled => Kills.Count;
        public long EnemyExperience { get; }
    }

    /// <summary>
    /// Run-scoped, non-persistent XP journal. Accepted deaths are immutable and
    /// duplicate-safe; no character progression authority is mutated until terminal
    /// mission completion accepts the whole ledger.
    /// </summary>
    public sealed class RunExperienceLedger : IEnemyExperienceFactConsumer
    {
        private readonly object gate = new object();
        private readonly StableId runStableId;
        private readonly StableId participantStableId;
        private readonly MissionExperienceRewardPolicy policy;
        private readonly Dictionary<StableId, RunExperienceKillRecord> byDeath =
            new Dictionary<StableId, RunExperienceKillRecord>();
        private readonly List<RunExperienceKillRecord> ordered =
            new List<RunExperienceKillRecord>();
        private long enemyExperience;

        public RunExperienceLedger(
            StableId runStableId,
            StableId participantStableId,
            MissionExperienceRewardPolicy policy)
        {
            this.runStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
            this.participantStableId = participantStableId
                ?? throw new ArgumentNullException(nameof(participantStableId));
            this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        public void Consume(EnemyDeathFact fact)
        {
            if (fact == null) throw new ArgumentNullException(nameof(fact));
            if (fact.Identity == null
                || fact.Identity.RunStableId != runStableId
                || fact.DeathEventStableId == null
                || fact.Identity.EntityInstanceId == null
                || fact.Identity.RoomStableId == null
                || fact.ExperienceProfileStableId == null)
            {
                throw new InvalidOperationException(
                    "Enemy death does not match the run XP ledger.");
            }
            if (fact.KillerRunParticipantStableId != participantStableId)
            {
                return;
            }

            long experience = policy.CalculateEnemyExperience(
                fact.ExperienceProfileStableId,
                fact.Level);
            string fingerprint = BuildFingerprint(fact, experience);
            lock (gate)
            {
                RunExperienceKillRecord existing;
                if (byDeath.TryGetValue(fact.DeathEventStableId, out existing))
                {
                    if (!string.Equals(
                        existing.Fingerprint,
                        fingerprint,
                        StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "Conflicting enemy death replay: "
                            + fact.DeathEventStableId);
                    }
                    return;
                }

                var record = new RunExperienceKillRecord(
                    fact,
                    experience,
                    fingerprint);
                enemyExperience = checked(enemyExperience + experience);
                byDeath.Add(record.DeathEventStableId, record);
                ordered.Add(record);
            }
        }

        public RunExperienceLedgerSnapshot ExportSnapshot()
        {
            lock (gate)
            {
                return new RunExperienceLedgerSnapshot(
                    runStableId,
                    participantStableId,
                    ordered,
                    enemyExperience);
            }
        }

        private static string BuildFingerprint(
            EnemyDeathFact fact,
            long experience)
        {
            return PlayerExperienceFormat.ComputeSha256(
                "run-xp-kill-v1|"
                + fact.DeathEventStableId + "|"
                + fact.Identity.EntityInstanceId + "|"
                + fact.DefinitionStableId + "|"
                + fact.ExperienceProfileStableId + "|"
                + fact.Level.ToString(CultureInfo.InvariantCulture) + "|"
                + fact.Identity.RoomStableId + "|"
                + fact.KillerRunParticipantStableId + "|"
                + experience.ToString(CultureInfo.InvariantCulture));
        }
    }
}
