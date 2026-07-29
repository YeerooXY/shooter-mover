using System;
using NUnit.Framework;
using ShooterMover.Application.Progression.Experience;
using ShooterMover.Application.Progression.Experience.EnemyRewards;
using ShooterMover.Contracts.Progression.Experience;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Progression.Experience;
using ShooterMover.UnityAdapters.Enemies;
using ShooterMover.UnityAdapters.Progression.Experience.EnemyRewards;

namespace ShooterMover.Tests.PlayMode.Progression.Experience.EnemyRewards
{
    public sealed class EnemyExperienceRewardingStateTests
    {
        [Test]
        public void Decorator_ForwardsAcceptedDeathAndPreservesInnerResult()
        {
            StableId actorId = StableId.Parse("enemy-instance.playmode-forward");
            StableId deathId = StableId.Parse("enemy-death.playmode-forward");
            var inner = new TestEnemyState(
                actorId,
                EnemyExperienceRewardIds.BlasterTurret,
                deathId);
            PlayerExperienceState xpAuthority = CreateExperienceAuthority();
            var rewardService = new EnemyExperienceRewardActions(
                xpAuthority,
                CreateCatalog(EnemyExperienceRewardIds.BlasterTurret, 35L));
            var decorator = new EnemyExperienceRewardingState(
                inner,
                rewardService,
                StableId.Parse("run.playmode-forward"),
                EnemyExperienceRewardIds.BlasterTurret,
                1);

            EnemyActorStepResult innerResult = decorator.Apply(
                EnemyActorCommand.Damage(
                    0L,
                    deathId,
                    StableId.Parse("actor.player"),
                    EnemyContactPolicy.KineticChannelValue,
                    1d));

            Assert.That(innerResult, Is.SameAs(inner.LastResult));
            Assert.That(decorator.LastRewardFacts.Count, Is.EqualTo(1));
            Assert.That(
                decorator.LastRewardFacts[0].Status,
                Is.EqualTo(EnemyExperienceRewardStatus.Applied));
            Assert.That(xpAuthority.CurrentState.CumulativeExperience, Is.EqualTo(35L));
        }

        [Test]
        public void QuickRestartAndRepeatedDeath_ProduceNoAdditionalExperience()
        {
            StableId actorId = StableId.Parse("enemy-instance.playmode-restart");
            StableId deathId = StableId.Parse("enemy-death.playmode-restart");
            var inner = new TestEnemyState(
                actorId,
                EnemyExperienceRewardIds.RamDroid,
                deathId);
            PlayerExperienceState xpAuthority = CreateExperienceAuthority();
            var rewardService = new EnemyExperienceRewardActions(
                xpAuthority,
                CreateCatalog(EnemyExperienceRewardIds.RamDroid, 90L));
            var decorator = new EnemyExperienceRewardingState(
                inner,
                rewardService,
                StableId.Parse("run.playmode-restart"),
                EnemyExperienceRewardIds.RamDroid,
                100);
            EnemyActorCommand lethal = EnemyActorCommand.Damage(
                0L,
                deathId,
                StableId.Parse("actor.player"),
                EnemyContactPolicy.KineticChannelValue,
                1d);

            decorator.Apply(lethal);
            Assert.That(decorator.Reset(), Is.True);
            decorator.Apply(lethal);

            Assert.That(decorator.LastRewardFacts.Count, Is.EqualTo(1));
            Assert.That(
                decorator.LastRewardFacts[0].Status,
                Is.EqualTo(EnemyExperienceRewardStatus.DuplicateNoChange));
            Assert.That(xpAuthority.CurrentState.CumulativeExperience, Is.EqualTo(90L));
            Assert.That(xpAuthority.CurrentSnapshot.Sequence, Is.EqualTo(1L));
        }

        private static EnemyExperienceRewardCatalog CreateCatalog(
            StableId definitionId,
            long amount)
        {
            return new EnemyExperienceRewardCatalog(
                new IEnemyExperienceRewardDefinition[]
                {
                    new EnemyExperienceRewardDefinition(
                        definitionId,
                        new[]
                        {
                            new EnemyExperienceRewardBand(1, 100, amount),
                        }),
                });
        }

        private static PlayerExperienceState CreateExperienceAuthority()
        {
            return new PlayerExperienceState(
                new PlayerExperienceCurve(
                    100L,
                    100L,
                    50,
                    new SoftActivationCurveParameters(0.1, 10L, 10L)),
                ProgressionContext.Create(
                    1,
                    1,
                    StableId.Parse("difficulty.normal"),
                    0,
                    new[] { StableId.Parse("progression-tag.campaign") }));
        }

        private sealed class TestEnemyState : IEnemyActor2DState
        {
            private readonly EnemyActorState initialState;
            private EnemyActorState state;

            public TestEnemyState(
                StableId actorId,
                StableId roleId,
                StableId deathId)
            {
                DeathId = deathId ?? throw new ArgumentNullException(nameof(deathId));
                initialState = EnemyActorState.Create(
                    actorId,
                    roleId,
                    1d,
                    2,
                    EnemyContactPolicy.Create(
                        EnemyContactMode.None,
                        0d,
                        0.5d,
                        0.02d,
                        4));
                state = initialState;
            }

            public StableId DeathId { get; }

            public EnemyActorStepResult LastResult { get; private set; }

            public bool TryReadState(out EnemyActorState current)
            {
                current = state;
                return true;
            }

            public EnemyActorStepResult Apply(EnemyActorCommand command)
            {
                LastResult = EnemyActorStepper.Step(state, new[] { command });
                state = LastResult.State;
                return LastResult;
            }

            public bool Reset()
            {
                state = initialState;
                LastResult = null;
                return true;
            }
        }
    }
}
