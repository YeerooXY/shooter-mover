using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Contracts.Progression.Experience;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Progression.Experience.EnemyRewards
{
    public sealed class EnemyExperienceRewardOperationIdentity
    {
        private const string SchemaId = "enemy-xp-operation-v1";

        private EnemyExperienceRewardOperationIdentity(
            StableId sourceOperationStableId,
            string fingerprint)
        {
            SourceOperationStableId = sourceOperationStableId;
            Fingerprint = fingerprint;
        }

        public StableId SourceOperationStableId { get; }

        public string Fingerprint { get; }

        public static EnemyExperienceRewardOperationIdentity Create(
            StableId runStableId,
            StableId enemyActorStableId)
        {
            if (runStableId == null)
            {
                throw new ArgumentNullException(nameof(runStableId));
            }

            if (enemyActorStableId == null)
            {
                throw new ArgumentNullException(nameof(enemyActorStableId));
            }

            var builder = new StringBuilder();
            AppendToken(builder, "schema", SchemaId);
            AppendToken(builder, "run_stable_id", runStableId.ToString());
            AppendToken(builder, "enemy_actor_stable_id", enemyActorStableId.ToString());
            AppendToken(builder, "operation_kind", "enemy-destroyed");
            string fingerprint = ComputeSha256(builder.ToString());
            return new EnemyExperienceRewardOperationIdentity(
                StableId.Create("xp-operation", fingerprint),
                fingerprint);
        }

        private static void AppendToken(
            StringBuilder builder,
            string name,
            string value)
        {
            string safe = value ?? string.Empty;
            builder.Append(name);
            builder.Append('=');
            builder.Append(safe.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(safe);
            builder.Append(';');
        }

        private static string ComputeSha256(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(value);
                byte[] hash = sha.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                for (int index = 0; index < hash.Length; index++)
                {
                    builder.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }
    }

    public enum EnemyExperienceRewardStatus
    {
        Applied = 1,
        DuplicateNoChange = 2,
        ConflictingDuplicate = 3,
        ZeroRewardNoChange = 4,
        MissingDefinition = 5,
        InvalidRequest = 6,
        InvalidEnemyLevel = 7,
        AuthorityRejected = 8,
    }

    public sealed class EnemyExperienceRewardFact
    {
        internal EnemyExperienceRewardFact(
            EnemyExperienceRewardStatus status,
            string rejectionCode,
            StableId runStableId,
            StableId enemyDefinitionStableId,
            int enemyLevel,
            StableId enemyActorStableId,
            StableId destructionEventStableId,
            StableId sourceOperationStableId,
            string operationFingerprint,
            long experienceAmount,
            PlayerExperienceGrantFact grantFact)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            RunStableId = runStableId;
            EnemyDefinitionStableId = enemyDefinitionStableId;
            EnemyLevel = enemyLevel;
            EnemyActorStableId = enemyActorStableId;
            DestructionEventStableId = destructionEventStableId;
            SourceOperationStableId = sourceOperationStableId;
            OperationFingerprint = operationFingerprint ?? string.Empty;
            ExperienceAmount = experienceAmount;
            GrantFact = grantFact;
        }

        public EnemyExperienceRewardStatus Status { get; }

        public string RejectionCode { get; }

        public StableId RunStableId { get; }

        public StableId EnemyDefinitionStableId { get; }

        public int EnemyLevel { get; }

        public StableId EnemyActorStableId { get; }

        public StableId DestructionEventStableId { get; }

        public StableId SourceOperationStableId { get; }

        public string OperationFingerprint { get; }

        public long ExperienceAmount { get; }

        public PlayerExperienceGrantFact GrantFact { get; }

        public IReadOnlyList<PlayerLevelUpFact> LevelUpFacts
        {
            get
            {
                return GrantFact == null
                    ? Array.Empty<PlayerLevelUpFact>()
                    : GrantFact.LevelUpFacts;
            }
        }

        public bool Changed
        {
            get { return Status == EnemyExperienceRewardStatus.Applied; }
        }
    }

}
