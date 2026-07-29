using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Progression.Experience;
using ShooterMover.Application.Progression.Experience.EnemyRewards;
using ShooterMover.Contracts.Progression.Experience;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Progression.Experience;
using ShooterMover.UnityAdapters.Progression.Experience.EnemyRewards;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.Progression.Experience.EnemyRewards
{
    public sealed class EnemyExperienceRewardTests
    {
        [Test]
        public void DefaultAuthoring_ResolvesAllKnownEnemiesByLevel()
        {
            EnemyExperienceRewardCatalogAsset asset =
                EnemyExperienceRewardCatalogAsset.CreateStage1DefaultsRuntime();
            try
            {
                EnemyExperienceRewardCatalog catalog = asset.BuildCatalogOrThrow();

                Assert.That(catalog.DefinitionCount, Is.EqualTo(4));
                for (int index = 0;
                    index < EnemyExperienceRewardIds.KnownEnemies.Count;
                    index++)
                {
                    StableId enemyId = EnemyExperienceRewardIds.KnownEnemies[index];
                    long levelOne;
                    long levelFifty;
                    long levelOneHundred;
                    Assert.That(catalog.TryResolve(enemyId, 1, out levelOne), Is.True);
                    Assert.That(catalog.TryResolve(enemyId, 50, out levelFifty), Is.True);
                    Assert.That(catalog.TryResolve(enemyId, 100, out levelOneHundred), Is.True);
                    Assert.That(levelOne, Is.GreaterThan(0L));
                    Assert.That(levelFifty, Is.GreaterThanOrEqualTo(levelOne));
                    Assert.That(levelOneHundred, Is.GreaterThanOrEqualTo(levelFifty));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void Validation_RejectsNegativeAndPermitsZeroReward()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new EnemyExperienceRewardBand(1, 100, -1L));

            PlayerExperience authority = CreateAuthority();
            var catalog = new EnemyExperienceRewardCatalog(
                new[]
                {
                    CreateDefinition(EnemyExperienceRewardIds.PursuerDrone, 0L),
                });
            var service = new EnemyExperienceRewardActions(authority, catalog);
            EnemyDestroyedNotification destruction = CreateDestruction(
                StableId.Parse("enemy-instance.zero-reward"),
                EnemyExperienceRewardIds.PursuerDrone,
                StableId.Parse("enemy-death.zero-reward"));

            EnemyExperienceRewardFact result = service.ProcessDestruction(
                StableId.Parse("run.zero-reward"),
                EnemyExperienceRewardIds.PursuerDrone,
                1,
                destruction);

            Assert.That(
                result.Status,
                Is.EqualTo(EnemyExperienceRewardStatus.ZeroRewardNoChange));
            Assert.That(result.SourceOperationStableId, Is.Not.Null);
            Assert.That(authority.CurrentState.CumulativeExperience, Is.Zero);
            Assert.That(authority.CurrentSnapshot.Sequence, Is.Zero);
        }

        [Test]
        public void DuplicateAndConflictingDeath_AwardExactlyOnce()
        {
            PlayerExperience authority = CreateAuthority();
            var firstService = new EnemyExperienceRewardActions(
                authority,
                CreateCatalog(EnemyExperienceRewardIds.BlasterTurret, 100L));
            EnemyDestroyedNotification destruction = CreateDestruction(
                StableId.Parse("enemy-instance.turret-one"),
                EnemyExperienceRewardIds.BlasterTurret,
                StableId.Parse("enemy-death.turret-one"));
            StableId runId = StableId.Parse("run.duplicate-death");

            EnemyExperienceRewardFact applied = firstService.ProcessDestruction(
                runId,
                EnemyExperienceRewardIds.BlasterTurret,
                1,
                destruction);
            EnemyExperienceRewardFact duplicate = firstService.ProcessDestruction(
                runId,
                EnemyExperienceRewardIds.BlasterTurret,
                1,
                destruction);
            var changedService = new EnemyExperienceRewardActions(
                authority,
                CreateCatalog(EnemyExperienceRewardIds.BlasterTurret, 101L));
            EnemyExperienceRewardFact conflict = changedService.ProcessDestruction(
                runId,
                EnemyExperienceRewardIds.BlasterTurret,
                1,
                destruction);

            Assert.That(applied.Status, Is.EqualTo(EnemyExperienceRewardStatus.Applied));
            Assert.That(
                duplicate.Status,
                Is.EqualTo(EnemyExperienceRewardStatus.DuplicateNoChange));
            Assert.That(
                conflict.Status,
                Is.EqualTo(EnemyExperienceRewardStatus.ConflictingDuplicate));
            Assert.That(applied.SourceOperationStableId, Is.EqualTo(
                duplicate.SourceOperationStableId));
            Assert.That(applied.SourceOperationStableId, Is.EqualTo(
                conflict.SourceOperationStableId));
            Assert.That(authority.CurrentState.CumulativeExperience, Is.EqualTo(100L));
            Assert.That(authority.CurrentSnapshot.Sequence, Is.EqualTo(1L));
        }

        [Test]
        public void DistinctEnemyInstances_GrantIndependentlyForSameDefinition()
        {
            PlayerExperience authority = CreateAuthority();
            var service = new EnemyExperienceRewardActions(
                authority,
                CreateCatalog(EnemyExperienceRewardIds.MobileBlasterDroid, 40L));
            StableId runId = StableId.Parse("run.distinct-enemies");
            StableId sharedDeathId = StableId.Parse("enemy-death.shared-template-operation");

            EnemyExperienceRewardFact first = service.ProcessDestruction(
                runId,
                EnemyExperienceRewardIds.MobileBlasterDroid,
                10,
                CreateDestruction(
                    StableId.Parse("enemy-instance.mobile-one"),
                    EnemyExperienceRewardIds.MobileBlasterDroid,
                    sharedDeathId));
            EnemyExperienceRewardFact second = service.ProcessDestruction(
                runId,
                EnemyExperienceRewardIds.MobileBlasterDroid,
                10,
                CreateDestruction(
                    StableId.Parse("enemy-instance.mobile-two"),
                    EnemyExperienceRewardIds.MobileBlasterDroid,
                    sharedDeathId));

            Assert.That(first.Status, Is.EqualTo(EnemyExperienceRewardStatus.Applied));
            Assert.That(second.Status, Is.EqualTo(EnemyExperienceRewardStatus.Applied));
            Assert.That(first.SourceOperationStableId, Is.Not.EqualTo(
                second.SourceOperationStableId));
            Assert.That(authority.CurrentState.CumulativeExperience, Is.EqualTo(80L));
            Assert.That(authority.CurrentSnapshot.Sequence, Is.EqualTo(2L));
        }

        [Test]
        public void AppliedReward_PreservesXpLevelUpFacts()
        {
            PlayerExperience authority = CreateAuthority();
            var service = new EnemyExperienceRewardActions(
                authority,
                CreateCatalog(EnemyExperienceRewardIds.RamDroid, 100L));

            EnemyExperienceRewardFact result = service.ProcessDestruction(
                StableId.Parse("run.level-up"),
                EnemyExperienceRewardIds.RamDroid,
                1,
                CreateDestruction(
                    StableId.Parse("enemy-instance.ram-level-up"),
                    EnemyExperienceRewardIds.RamDroid,
                    StableId.Parse("enemy-death.ram-level-up")));

            Assert.That(result.Status, Is.EqualTo(EnemyExperienceRewardStatus.Applied));
            Assert.That(result.GrantFact, Is.Not.Null);
            Assert.That(result.LevelUpFacts.Count, Is.EqualTo(1));
            Assert.That(result.LevelUpFacts[0].PreviousLevel, Is.EqualTo(1));
            Assert.That(result.LevelUpFacts[0].CurrentLevel, Is.EqualTo(2));
            Assert.That(result.LevelUpFacts[0].SkillPointsGranted, Is.EqualTo(1));
            Assert.That(authority.CurrentState.Level, Is.EqualTo(2));
        }

        [Test]
        public void SnapshotImport_ReplayedDeathProducesNoAdditionalXp()
        {
            PlayerExperienceCurve curve = CreateConstantCurve();
            PlayerExperience original = CreateAuthority(curve);
            EnemyExperienceRewardCatalog catalog = CreateCatalog(
                EnemyExperienceRewardIds.PursuerDrone,
                45L);
            var originalService = new EnemyExperienceRewardActions(original, catalog);
            StableId runId = StableId.Parse("run.import-replay");
            EnemyDestroyedNotification destruction = CreateDestruction(
                StableId.Parse("enemy-instance.import-replay"),
                EnemyExperienceRewardIds.PursuerDrone,
                StableId.Parse("enemy-death.import-replay"));

            originalService.ProcessDestruction(
                runId,
                EnemyExperienceRewardIds.PursuerDrone,
                55,
                destruction);
            PlayerExperienceSnapshot snapshot = original.ExportSnapshot();

            PlayerExperience restored = CreateAuthority(curve);
            Assert.That(
                restored.TryImport(snapshot).Status,
                Is.EqualTo(PlayerExperienceImportStatus.Imported));
            var restoredService = new EnemyExperienceRewardActions(restored, catalog);
            EnemyExperienceRewardFact replay = restoredService.ProcessDestruction(
                runId,
                EnemyExperienceRewardIds.PursuerDrone,
                55,
                destruction);

            Assert.That(
                replay.Status,
                Is.EqualTo(EnemyExperienceRewardStatus.DuplicateNoChange));
            Assert.That(restored.CurrentState.CumulativeExperience, Is.EqualTo(45L));
            Assert.That(restored.CurrentSnapshot.Sequence, Is.EqualTo(1L));
        }

        [Test]
        public void RetryWithDifferentDeathEvent_ForSameRunAndActorIsDuplicate()
        {
            PlayerExperience authority = CreateAuthority();
            var service = new EnemyExperienceRewardActions(
                authority,
                CreateCatalog(EnemyExperienceRewardIds.PursuerDrone, 30L));
            StableId runId = StableId.Parse("run.retry-event-change");
            StableId actorId = StableId.Parse("enemy-instance.retry-event-change");

            EnemyExperienceRewardFact first = service.ProcessDestruction(
                runId,
                EnemyExperienceRewardIds.PursuerDrone,
                30,
                CreateDestruction(
                    actorId,
                    EnemyExperienceRewardIds.PursuerDrone,
                    StableId.Parse("enemy-death.retry-first")));
            EnemyExperienceRewardFact retry = service.ProcessDestruction(
                runId,
                EnemyExperienceRewardIds.PursuerDrone,
                30,
                CreateDestruction(
                    actorId,
                    EnemyExperienceRewardIds.PursuerDrone,
                    StableId.Parse("enemy-death.retry-second")));

            Assert.That(first.Status, Is.EqualTo(EnemyExperienceRewardStatus.Applied));
            Assert.That(
                retry.Status,
                Is.EqualTo(EnemyExperienceRewardStatus.DuplicateNoChange));
            Assert.That(first.SourceOperationStableId, Is.EqualTo(
                retry.SourceOperationStableId));
            Assert.That(authority.CurrentState.CumulativeExperience, Is.EqualTo(30L));
            Assert.That(authority.CurrentSnapshot.Sequence, Is.EqualTo(1L));
        }

        [Test]
        public void OperationIdentity_IsDeterministicAndScopedByRunAndActor()
        {
            StableId run = StableId.Parse("run.identity-one");
            StableId actor = StableId.Parse("enemy-instance.identity-one");
            EnemyExperienceRewardOperationIdentity first =
                EnemyExperienceRewardOperationIdentity.Create(run, actor);
            EnemyExperienceRewardOperationIdentity repeat =
                EnemyExperienceRewardOperationIdentity.Create(run, actor);
            EnemyExperienceRewardOperationIdentity otherRun =
                EnemyExperienceRewardOperationIdentity.Create(
                    StableId.Parse("run.identity-two"),
                    actor);
            EnemyExperienceRewardOperationIdentity otherActor =
                EnemyExperienceRewardOperationIdentity.Create(
                    run,
                    StableId.Parse("enemy-instance.identity-two"));

            Assert.That(first.SourceOperationStableId, Is.EqualTo(
                repeat.SourceOperationStableId));
            Assert.That(first.Fingerprint, Is.EqualTo(repeat.Fingerprint));
            Assert.That(first.SourceOperationStableId, Is.Not.EqualTo(
                otherRun.SourceOperationStableId));
            Assert.That(first.SourceOperationStableId, Is.Not.EqualTo(
                otherActor.SourceOperationStableId));
        }

        [Test]
        public void LevelBandValidation_RejectsGapsAndOverlaps()
        {
            StableId enemyId = StableId.Parse("enemy.future-test");

            Assert.Throws<ArgumentException>(() =>
                new EnemyExperienceRewardDefinition(
                    enemyId,
                    new[]
                    {
                        new EnemyExperienceRewardBand(1, 49, 10L),
                        new EnemyExperienceRewardBand(51, 100, 20L),
                    }));
            Assert.Throws<ArgumentException>(() =>
                new EnemyExperienceRewardDefinition(
                    enemyId,
                    new[]
                    {
                        new EnemyExperienceRewardBand(1, 50, 10L),
                        new EnemyExperienceRewardBand(50, 100, 20L),
                    }));
        }

        private static EnemyExperienceRewardCatalog CreateCatalog(
            StableId enemyDefinitionStableId,
            long amount)
        {
            return new EnemyExperienceRewardCatalog(
                new[] { CreateDefinition(enemyDefinitionStableId, amount) });
        }

        private static EnemyExperienceRewardDefinition CreateDefinition(
            StableId enemyDefinitionStableId,
            long amount)
        {
            return new EnemyExperienceRewardDefinition(
                enemyDefinitionStableId,
                new[] { new EnemyExperienceRewardBand(1, 100, amount) });
        }

        private static EnemyDestroyedNotification CreateDestruction(
            StableId actorId,
            StableId roleId,
            StableId eventId)
        {
            EnemyActorState state = EnemyActorState.Create(
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
            EnemyActorStepResult result = EnemyActorStepper.Step(
                state,
                new[]
                {
                    EnemyActorCommand.Damage(
                        0L,
                        eventId,
                        StableId.Parse("actor.player"),
                        EnemyContactPolicy.KineticChannelValue,
                        1d),
                });

            for (int index = 0; index < result.Notifications.Count; index++)
            {
                EnemyDestroyedNotification destruction =
                    result.Notifications[index] as EnemyDestroyedNotification;
                if (destruction != null)
                {
                    return destruction;
                }
            }

            throw new InvalidOperationException("Expected one enemy destruction fact.");
        }

        private static PlayerExperience CreateAuthority()
        {
            return CreateAuthority(CreateConstantCurve());
        }

        private static PlayerExperience CreateAuthority(
            PlayerExperienceCurve curve)
        {
            return new PlayerExperience(
                curve,
                ProgressionContext.Create(
                    1,
                    1,
                    StableId.Parse("difficulty.normal"),
                    0,
                    new[] { StableId.Parse("progression-tag.campaign") }));
        }

        private static PlayerExperienceCurve CreateConstantCurve()
        {
            return new PlayerExperienceCurve(
                100L,
                100L,
                50,
                new SoftActivationCurveParameters(0.1, 10L, 10L));
        }
    }
}
