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
    public sealed class EnemyExperienceRewardingAuthorityTests
    {
        [Test]
        public void Decorator_ForwardsAcceptedDeathAndPreservesInnerResult()
        {
            StableId actorId = StableId.Parse("enemy-instance.playmode-forward");
            StableId deathId = StableId.Parse("enemy-death.playmode-forward");
            var inner = new TestEnemyAuthority(
                actorId,
                EnemyExperienceRewardIdsV1.BlasterTurret,
                deathId);
            PlayerExperienceAuthorityV1 xpAuthority = CreateExperienceAuthority();
            var rewardService = new EnemyExperienceRewardServiceV1(
                xpAuthority,
                CreateCatalog(EnemyExperienceRewardIdsV1.BlasterTurret, 35L));
            var decorator = new EnemyExperienceRewardingAuthorityV1(
                inner,
                rewardService,
                StableId.Parse("run.playmode-forward"),
                EnemyExperienceRewardIdsV1.BlasterTurret,
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
                Is.EqualTo(EnemyExperienceRewardStatusV1.Applied));
            Assert.That(xpAuthority.CurrentState.CumulativeExperience, Is.EqualTo(35L));
        }

        [Test]
        public void QuickRestartAndRepeatedDeath_ProduceNoAdditionalExperience()
        {
            StableId actorId = StableId.Parse("enemy-instance.playmode-restart");
            StableId deathId = StableId.Parse("enemy-death.playmode-restart");
            var inner = new TestEnemyAuthority(
                actorId,
                EnemyExperienceRewardIdsV1.RamDroid,
                deathId);
            PlayerExperienceAuthorityV1 xpAuthority = CreateExperienceAuthority();
            var rewardService = new EnemyExperienceRewardServiceV1(
                xpAuthority,
                CreateCatalog(EnemyExperienceRewardIdsV1.RamDroid, 90L));
            var decorator = new EnemyExperienceRewardingAuthorityV1(
                inner,
                rewardService,
                StableId.Parse("run.playmode-restart"),
                EnemyExperienceRewardIdsV1.RamDroid,
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
                Is.EqualTo(EnemyExperienceRewardStatusV1.DuplicateNoChange));
            Assert.That(xpAuthority.CurrentState.CumulativeExperience, Is.EqualTo(90L));
            Assert.That(xpAuthority.CurrentSnapshot.Sequence, Is.EqualTo(1L));
        }

        private static EnemyExperienceRewardCatalogV1 CreateCatalog(
            StableId definitionId,
            long amount)
        {
            return new EnemyExperienceRewardCatalogV1(
                new IEnemyExperienceRewardDefinitionV1[]
                {
                    new EnemyExperienceRewardDefinitionV1(
                        definitionId,
                        new[]
                        {
                            new EnemyExperienceRewardBandV1(1, 100, amount),
                        }),
                });
        }

        private static PlayerExperienceAuthorityV1 CreateExperienceAuthority()
        {
            return new PlayerExperienceAuthorityV1(
                new PlayerExperienceCurveV1(
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

        private sealed class TestEnemyAuthority : IEnemyActor2DAuthority
        {
            private readonly EnemyActorState initialState;
            private EnemyActorState state;

            public TestEnemyAuthority(
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
