using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Contracts.Progression.Experience;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;

namespace ShooterMover.Application.Progression.Experience.EnemyRewards
{
    /// <summary>
    /// Converts accepted EN-002 destruction facts into XP-001 grant requests. This
    /// service owns no enemy state and XP-001 remains the only mutable XP authority.
    /// </summary>
    public sealed class EnemyExperienceRewardActions
    {
        private readonly IPlayerExperience experienceAuthority;
        private readonly EnemyExperienceRewardCatalog catalog;

        public EnemyExperienceRewardActions(
            IPlayerExperience experienceAuthority,
            EnemyExperienceRewardCatalog catalog)
        {
            this.experienceAuthority = experienceAuthority
                ?? throw new ArgumentNullException(nameof(experienceAuthority));
            this.catalog = catalog
                ?? throw new ArgumentNullException(nameof(catalog));
        }

        public EnemyExperienceRewardFact ProcessDestruction(
            StableId runStableId,
            StableId enemyDefinitionStableId,
            int enemyLevel,
            EnemyDestroyedNotification destruction)
        {
            if (runStableId == null
                || enemyDefinitionStableId == null
                || destruction == null
                || destruction.TargetId == null
                || destruction.EventId == null)
            {
                return CreateNoChange(
                    EnemyExperienceRewardStatus.InvalidRequest,
                    "enemy-xp-request-invalid",
                    runStableId,
                    enemyDefinitionStableId,
                    enemyLevel,
                    destruction,
                    null,
                    0L);
            }

            if (enemyLevel < EnemyExperienceRewardIds.MinimumEnemyLevel
                || enemyLevel > EnemyExperienceRewardIds.MaximumEnemyLevel)
            {
                return CreateNoChange(
                    EnemyExperienceRewardStatus.InvalidEnemyLevel,
                    "enemy-xp-level-out-of-range",
                    runStableId,
                    enemyDefinitionStableId,
                    enemyLevel,
                    destruction,
                    null,
                    0L);
            }

            long amount;
            if (!catalog.TryResolve(enemyDefinitionStableId, enemyLevel, out amount))
            {
                return CreateNoChange(
                    EnemyExperienceRewardStatus.MissingDefinition,
                    "enemy-xp-definition-missing",
                    runStableId,
                    enemyDefinitionStableId,
                    enemyLevel,
                    destruction,
                    null,
                    0L);
            }

            EnemyExperienceRewardOperationIdentity identity =
                EnemyExperienceRewardOperationIdentity.Create(
                    runStableId,
                    destruction.TargetId);
            if (amount == 0L)
            {
                return CreateNoChange(
                    EnemyExperienceRewardStatus.ZeroRewardNoChange,
                    string.Empty,
                    runStableId,
                    enemyDefinitionStableId,
                    enemyLevel,
                    destruction,
                    identity,
                    amount);
            }

            PlayerExperienceGrantFact grant = experienceAuthority.Grant(
                new PlayerExperienceGrantRequest(
                    identity.SourceOperationStableId,
                    amount));
            return new EnemyExperienceRewardFact(
                MapStatus(grant.Status),
                grant.RejectionCode,
                runStableId,
                enemyDefinitionStableId,
                enemyLevel,
                destruction.TargetId,
                destruction.EventId,
                identity.SourceOperationStableId,
                identity.Fingerprint,
                amount,
                grant);
        }

        public IReadOnlyList<EnemyExperienceRewardFact> ProcessStepResult(
            StableId runStableId,
            StableId enemyDefinitionStableId,
            int enemyLevel,
            EnemyActorStepResult stepResult)
        {
            if (stepResult == null)
            {
                throw new ArgumentNullException(nameof(stepResult));
            }

            var results = new List<EnemyExperienceRewardFact>();
            for (int index = 0; index < stepResult.Notifications.Count; index++)
            {
                EnemyDestroyedNotification destruction =
                    stepResult.Notifications[index] as EnemyDestroyedNotification;
                if (destruction != null)
                {
                    results.Add(ProcessDestruction(
                        runStableId,
                        enemyDefinitionStableId,
                        enemyLevel,
                        destruction));
                }
            }

            return new ReadOnlyCollection<EnemyExperienceRewardFact>(results);
        }

        private static EnemyExperienceRewardStatus MapStatus(
            PlayerExperienceGrantStatus status)
        {
            switch (status)
            {
                case PlayerExperienceGrantStatus.Applied:
                    return EnemyExperienceRewardStatus.Applied;
                case PlayerExperienceGrantStatus.DuplicateNoChange:
                    return EnemyExperienceRewardStatus.DuplicateNoChange;
                case PlayerExperienceGrantStatus.ConflictingDuplicate:
                    return EnemyExperienceRewardStatus.ConflictingDuplicate;
                default:
                    return EnemyExperienceRewardStatus.AuthorityRejected;
            }
        }

        private static EnemyExperienceRewardFact CreateNoChange(
            EnemyExperienceRewardStatus status,
            string rejectionCode,
            StableId runStableId,
            StableId enemyDefinitionStableId,
            int enemyLevel,
            EnemyDestroyedNotification destruction,
            EnemyExperienceRewardOperationIdentity identity,
            long amount)
        {
            return new EnemyExperienceRewardFact(
                status,
                rejectionCode,
                runStableId,
                enemyDefinitionStableId,
                enemyLevel,
                destruction == null ? null : destruction.TargetId,
                destruction == null ? null : destruction.EventId,
                identity == null ? null : identity.SourceOperationStableId,
                identity == null ? string.Empty : identity.Fingerprint,
                amount,
                null);
        }
    }
}
