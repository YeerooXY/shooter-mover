using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Application.Progression.Experience.EnemyRewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.UnityAdapters.Enemies;

namespace ShooterMover.UnityAdapters.Progression.Experience.EnemyRewards
{
    /// <summary>
    /// Decorates an existing EN-003 authority port. Enemy combat and lifecycle remain
    /// unchanged; accepted EN-002 destruction notifications are forwarded to XP-002.
    /// </summary>
    public sealed class EnemyExperienceRewardingAuthorityV1 : IEnemyActor2DAuthority
    {
        private static readonly IReadOnlyList<EnemyExperienceRewardFactV1> EmptyFacts =
            Array.AsReadOnly(new EnemyExperienceRewardFactV1[0]);

        private readonly IEnemyActor2DAuthority innerAuthority;
        private readonly EnemyExperienceRewardServiceV1 rewardService;
        private readonly StableId runStableId;
        private readonly StableId enemyDefinitionStableId;
        private readonly int enemyLevel;

        private IReadOnlyList<EnemyExperienceRewardFactV1> lastRewardFacts = EmptyFacts;

        public EnemyExperienceRewardingAuthorityV1(
            IEnemyActor2DAuthority innerAuthority,
            EnemyExperienceRewardServiceV1 rewardService,
            StableId runStableId,
            StableId enemyDefinitionStableId,
            int enemyLevel)
        {
            this.innerAuthority = innerAuthority
                ?? throw new ArgumentNullException(nameof(innerAuthority));
            this.rewardService = rewardService
                ?? throw new ArgumentNullException(nameof(rewardService));
            this.runStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
            this.enemyDefinitionStableId = enemyDefinitionStableId
                ?? throw new ArgumentNullException(nameof(enemyDefinitionStableId));
            EnemyExperienceRewardDefinitionV1.RequireEnemyLevel(enemyLevel);
            this.enemyLevel = enemyLevel;
        }

        public IReadOnlyList<EnemyExperienceRewardFactV1> LastRewardFacts
        {
            get { return lastRewardFacts; }
        }

        public bool TryReadState(out EnemyActorState state)
        {
            return innerAuthority.TryReadState(out state);
        }

        public EnemyActorStepResult Apply(EnemyActorCommand command)
        {
            EnemyActorStepResult result = innerAuthority.Apply(command);
            IReadOnlyList<EnemyExperienceRewardFactV1> processed =
                rewardService.ProcessStepResult(
                    runStableId,
                    enemyDefinitionStableId,
                    enemyLevel,
                    result);
            lastRewardFacts = new ReadOnlyCollection<EnemyExperienceRewardFactV1>(
                new List<EnemyExperienceRewardFactV1>(processed));
            return result;
        }

        /// <summary>
        /// Resets only enemy transport/lifecycle state. XP replay history deliberately
        /// remains in XP-001, so a repeated death operation in the same run is a no-op.
        /// </summary>
        public bool Reset()
        {
            lastRewardFacts = EmptyFacts;
            return innerAuthority.Reset();
        }
    }
}
