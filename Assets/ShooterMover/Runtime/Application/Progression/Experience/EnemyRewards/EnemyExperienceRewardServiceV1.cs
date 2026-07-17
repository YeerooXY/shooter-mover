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
    public sealed class EnemyExperienceRewardServiceV1
    {
        private readonly IPlayerExperienceAuthorityV1 experienceAuthority;
        private readonly EnemyExperienceRewardCatalogV1 catalog;

        public EnemyExperienceRewardServiceV1(
            IPlayerExperienceAuthorityV1 experienceAuthority,
            EnemyExperienceRewardCatalogV1 catalog)
        {
            this.experienceAuthority = experienceAuthority
                ?? throw new ArgumentNullException(nameof(experienceAuthority));
            this.catalog = catalog
                ?? throw new ArgumentNullException(nameof(catalog));
        }

        public EnemyExperienceRewardFactV1 ProcessDestruction(
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
                    EnemyExperienceRewardStatusV1.InvalidRequest,
                    "enemy-xp-request-invalid",
                    runStableId,
                    enemyDefinitionStableId,
                    enemyLevel,
                    destruction,
                    null,
                    0L);
            }

            if (enemyLevel < EnemyExperienceRewardIdsV1.MinimumEnemyLevel
                || enemyLevel > EnemyExperienceRewardIdsV1.MaximumEnemyLevel)
            {
                return CreateNoChange(
                    EnemyExperienceRewardStatusV1.InvalidEnemyLevel,
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
                    EnemyExperienceRewardStatusV1.MissingDefinition,
                    "enemy-xp-definition-missing",
                    runStableId,
                    enemyDefinitionStableId,
                    enemyLevel,
                    destruction,
                    null,
                    0L);
            }

            EnemyExperienceRewardOperationIdentityV1 identity =
                EnemyExperienceRewardOperationIdentityV1.Create(
                    runStableId,
                    destruction.TargetId);
            if (amount == 0L)
            {
                return CreateNoChange(
                    EnemyExperienceRewardStatusV1.ZeroRewardNoChange,
                    string.Empty,
                    runStableId,
                    enemyDefinitionStableId,
                    enemyLevel,
                    destruction,
                    identity,
                    amount);
            }

            PlayerExperienceGrantFactV1 grant = experienceAuthority.Grant(
                new PlayerExperienceGrantRequestV1(
                    identity.SourceOperationStableId,
                    amount));
            return new EnemyExperienceRewardFactV1(
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

        public IReadOnlyList<EnemyExperienceRewardFactV1> ProcessStepResult(
            StableId runStableId,
            StableId enemyDefinitionStableId,
            int enemyLevel,
            EnemyActorStepResult stepResult)
        {
            if (stepResult == null)
            {
                throw new ArgumentNullException(nameof(stepResult));
            }

            var results = new List<EnemyExperienceRewardFactV1>();
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

            return new ReadOnlyCollection<EnemyExperienceRewardFactV1>(results);
        }

        private static EnemyExperienceRewardStatusV1 MapStatus(
            PlayerExperienceGrantStatusV1 status)
        {
            switch (status)
            {
                case PlayerExperienceGrantStatusV1.Applied:
                    return EnemyExperienceRewardStatusV1.Applied;
                case PlayerExperienceGrantStatusV1.DuplicateNoChange:
                    return EnemyExperienceRewardStatusV1.DuplicateNoChange;
                case PlayerExperienceGrantStatusV1.ConflictingDuplicate:
                    return EnemyExperienceRewardStatusV1.ConflictingDuplicate;
                default:
                    return EnemyExperienceRewardStatusV1.AuthorityRejected;
            }
        }

        private static EnemyExperienceRewardFactV1 CreateNoChange(
            EnemyExperienceRewardStatusV1 status,
            string rejectionCode,
            StableId runStableId,
            StableId enemyDefinitionStableId,
            int enemyLevel,
            EnemyDestroyedNotification destruction,
            EnemyExperienceRewardOperationIdentityV1 identity,
            long amount)
        {
            return new EnemyExperienceRewardFactV1(
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
