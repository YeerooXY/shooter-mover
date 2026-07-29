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
    public sealed class EnemyExperienceRewardingState : IEnemyActor2DState
    {
        private static readonly IReadOnlyList<EnemyExperienceRewardFact> EmptyFacts =
            Array.AsReadOnly(new EnemyExperienceRewardFact[0]);

        private readonly IEnemyActor2DState innerAuthority;
        private readonly EnemyExperienceRewardActions rewardService;
        private readonly StableId runStableId;
        private readonly StableId enemyDefinitionStableId;
        private readonly int enemyLevel;

        private IReadOnlyList<EnemyExperienceRewardFact> lastRewardFacts = EmptyFacts;

        public EnemyExperienceRewardingState(
            IEnemyActor2DState innerAuthority,
            EnemyExperienceRewardActions rewardService,
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
            EnemyExperienceRewardDefinition.RequireEnemyLevel(enemyLevel);
            this.enemyLevel = enemyLevel;
        }

        public IReadOnlyList<EnemyExperienceRewardFact> LastRewardFacts
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
            IReadOnlyList<EnemyExperienceRewardFact> processed =
                rewardService.ProcessStepResult(
                    runStableId,
                    enemyDefinitionStableId,
                    enemyLevel,
                    result);
            lastRewardFacts = new ReadOnlyCollection<EnemyExperienceRewardFact>(
                new List<EnemyExperienceRewardFact>(processed));
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
