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
        public void Stage1Authoring_ResolvesAllNamedEnemiesByLevel()
        {
            EnemyExperienceRewardCatalogAssetV1 asset =
                EnemyExperienceRewardCatalogAssetV1.CreateStage1DefaultsRuntime();
            try
            {
                EnemyExperienceRewardCatalogV1 catalog = asset.BuildCatalogOrThrow();

                Assert.That(catalog.DefinitionCount, Is.EqualTo(4));
                for (int index = 0;
                    index < EnemyExperienceRewardIdsV1.Stage1Enemies.Count;
                    index++)
                {
                    StableId enemyId = EnemyExperienceRewardIdsV1.Stage1Enemies[index];
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
                new EnemyExperienceRewardBandV1(1, 100, -1L));

            PlayerExperienceAuthorityV1 authority = CreateAuthority();
            var catalog = new EnemyExperienceRewardCatalogV1(
                new[]
                {
                    CreateDefinition(EnemyExperienceRewardIdsV1.PursuerDrone, 0L),
                });
            var service = new EnemyExperienceRewardServiceV1(authority, catalog);
            EnemyDestroyedNotification destruction = CreateDestruction(
                StableId.Parse("enemy-instance.zero-reward"),
                EnemyExperienceRewardIdsV1.PursuerDrone,
                StableId.Parse("enemy-death.zero-reward"));

            EnemyExperienceRewardFactV1 result = service.ProcessDestruction(
                StableId.Parse("run.zero-reward"),
                EnemyExperienceRewardIdsV1.PursuerDrone,
                1,
                destruction);

            Assert.That(
                result.Status,
                Is.EqualTo(EnemyExperienceRewardStatusV1.ZeroRewardNoChange));
            Assert.That(result.SourceOperationStableId, Is.Not.Null);
            Assert.That(authority.CurrentState.CumulativeExperience, Is.Zero);
            Assert.That(authority.CurrentSnapshot.Sequence, Is.Zero);
        }

        [Test]
        public void DuplicateAndConflictingDeath_AwardExactlyOnce()
        {
            PlayerExperienceAuthorityV1 authority = CreateAuthority();
            var firstService = new EnemyExperienceRewardServiceV1(
                authority,
                CreateCatalog(EnemyExperienceRewardIdsV1.BlasterTurret, 100L));
            EnemyDestroyedNotification destruction = CreateDestruction(
                StableId.Parse("enemy-instance.turret-one"),
                EnemyExperienceRewardIdsV1.BlasterTurret,
                StableId.Parse("enemy-death.turret-one"));
            StableId runId = StableId.Parse("run.duplicate-death");

            EnemyExperienceRewardFactV1 applied = firstService.ProcessDestruction(
                runId,
                EnemyExperienceRewardIdsV1.BlasterTurret,
                1,
                destruction);
            EnemyExperienceRewardFactV1 duplicate = firstService.ProcessDestruction(
                runId,
                EnemyExperienceRewardIdsV1.BlasterTurret,
                1,
                destruction);
            var changedService = new EnemyExperienceRewardServiceV1(
                authority,
                CreateCatalog(EnemyExperienceRewardIdsV1.BlasterTurret, 101L));
            EnemyExperienceRewardFactV1 conflict = changedService.ProcessDestruction(
                runId,
                EnemyExperienceRewardIdsV1.BlasterTurret,
                1,
                destruction);

            Assert.That(applied.Status, Is.EqualTo(EnemyExperienceRewardStatusV1.Applied));
            Assert.That(
                duplicate.Status,
                Is.EqualTo(EnemyExperienceRewardStatusV1.DuplicateNoChange));
            Assert.That(
                conflict.Status,
                Is.EqualTo(EnemyExperienceRewardStatusV1.ConflictingDuplicate));
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
            PlayerExperienceAuthorityV1 authority = CreateAuthority();
            var service = new EnemyExperienceRewardServiceV1(
                authority,
                CreateCatalog(EnemyExperienceRewardIdsV1.MobileBlasterDroid, 40L));
            StableId runId = StableId.Parse("run.distinct-enemies");
            StableId sharedDeathId = StableId.Parse("enemy-death.shared-template-operation");

            EnemyExperienceRewardFactV1 first = service.ProcessDestruction(
                runId,
                EnemyExperienceRewardIdsV1.MobileBlasterDroid,
                10,
                CreateDestruction(
                    StableId.Parse("enemy-instance.mobile-one"),
                    EnemyExperienceRewardIdsV1.MobileBlasterDroid,
                    sharedDeathId));
            EnemyExperienceRewardFactV1 second = service.ProcessDestruction(
                runId,
                EnemyExperienceRewardIdsV1.MobileBlasterDroid,
                10,
                CreateDestruction(
                    StableId.Parse("enemy-instance.mobile-two"),
                    EnemyExperienceRewardIdsV1.MobileBlasterDroid,
                    sharedDeathId));

            Assert.That(first.Status, Is.EqualTo(EnemyExperienceRewardStatusV1.Applied));
            Assert.That(second.Status, Is.EqualTo(EnemyExperienceRewardStatusV1.Applied));
            Assert.That(first.SourceOperationStableId, Is.Not.EqualTo(
                second.SourceOperationStableId));
            Assert.That(authority.CurrentState.CumulativeExperience, Is.EqualTo(80L));
            Assert.That(authority.CurrentSnapshot.Sequence, Is.EqualTo(2L));
        }

        [Test]
        public void AppliedReward_PreservesXpLevelUpFacts()
        {
            PlayerExperienceAuthorityV1 authority = CreateAuthority();
            var service = new EnemyExperienceRewardServiceV1(
                authority,
                CreateCatalog(EnemyExperienceRewardIdsV1.RamDroid, 100L));

            EnemyExperienceRewardFactV1 result = service.ProcessDestruction(
                StableId.Parse("run.level-up"),
                EnemyExperienceRewardIdsV1.RamDroid,
                1,
                CreateDestruction(
                    StableId.Parse("enemy-instance.ram-level-up"),
                    EnemyExperienceRewardIdsV1.RamDroid,
                    StableId.Parse("enemy-death.ram-level-up")));

            Assert.That(result.Status, Is.EqualTo(EnemyExperienceRewardStatusV1.Applied));
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
            PlayerExperienceCurveV1 curve = CreateConstantCurve();
            PlayerExperienceAuthorityV1 original = CreateAuthority(curve);
            EnemyExperienceRewardCatalogV1 catalog = CreateCatalog(
                EnemyExperienceRewardIdsV1.PursuerDrone,
                45L);
            var originalService = new EnemyExperienceRewardServiceV1(original, catalog);
            StableId runId = StableId.Parse("run.import-replay");
            EnemyDestroyedNotification destruction = CreateDestruction(
                StableId.Parse("enemy-instance.import-replay"),
                EnemyExperienceRewardIdsV1.PursuerDrone,
                StableId.Parse("enemy-death.import-replay"));

            originalService.ProcessDestruction(
                runId,
                EnemyExperienceRewardIdsV1.PursuerDrone,
                55,
                destruction);
            PlayerExperienceSnapshotV1 snapshot = original.ExportSnapshot();

            PlayerExperienceAuthorityV1 restored = CreateAuthority(curve);
            Assert.That(
                restored.TryImport(snapshot).Status,
                Is.EqualTo(PlayerExperienceImportStatusV1.Imported));
            var restoredService = new EnemyExperienceRewardServiceV1(restored, catalog);
            EnemyExperienceRewardFactV1 replay = restoredService.ProcessDestruction(
                runId,
                EnemyExperienceRewardIdsV1.PursuerDrone,
                55,
                destruction);

            Assert.That(
                replay.Status,
                Is.EqualTo(EnemyExperienceRewardStatusV1.DuplicateNoChange));
            Assert.That(restored.CurrentState.CumulativeExperience, Is.EqualTo(45L));
            Assert.That(restored.CurrentSnapshot.Sequence, Is.EqualTo(1L));
        }

        [Test]
        public void RetryWithDifferentDeathEvent_ForSameRunAndActorIsDuplicate()
        {
            PlayerExperienceAuthorityV1 authority = CreateAuthority();
            var service = new EnemyExperienceRewardServiceV1(
                authority,
                CreateCatalog(EnemyExperienceRewardIdsV1.PursuerDrone, 30L));
            StableId runId = StableId.Parse("run.retry-event-change");
            StableId actorId = StableId.Parse("enemy-instance.retry-event-change");

            EnemyExperienceRewardFactV1 first = service.ProcessDestruction(
                runId,
                EnemyExperienceRewardIdsV1.PursuerDrone,
                30,
                CreateDestruction(
                    actorId,
                    EnemyExperienceRewardIdsV1.PursuerDrone,
                    StableId.Parse("enemy-death.retry-first")));
            EnemyExperienceRewardFactV1 retry = service.ProcessDestruction(
                runId,
                EnemyExperienceRewardIdsV1.PursuerDrone,
                30,
                CreateDestruction(
                    actorId,
                    EnemyExperienceRewardIdsV1.PursuerDrone,
                    StableId.Parse("enemy-death.retry-second")));

            Assert.That(first.Status, Is.EqualTo(EnemyExperienceRewardStatusV1.Applied));
            Assert.That(
                retry.Status,
                Is.EqualTo(EnemyExperienceRewardStatusV1.DuplicateNoChange));
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
            EnemyExperienceRewardOperationIdentityV1 first =
                EnemyExperienceRewardOperationIdentityV1.Create(run, actor);
            EnemyExperienceRewardOperationIdentityV1 repeat =
                EnemyExperienceRewardOperationIdentityV1.Create(run, actor);
            EnemyExperienceRewardOperationIdentityV1 otherRun =
                EnemyExperienceRewardOperationIdentityV1.Create(
                    StableId.Parse("run.identity-two"),
                    actor);
            EnemyExperienceRewardOperationIdentityV1 otherActor =
                EnemyExperienceRewardOperationIdentityV1.Create(
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
                new EnemyExperienceRewardDefinitionV1(
                    enemyId,
                    new[]
                    {
                        new EnemyExperienceRewardBandV1(1, 49, 10L),
                        new EnemyExperienceRewardBandV1(51, 100, 20L),
                    }));
            Assert.Throws<ArgumentException>(() =>
                new EnemyExperienceRewardDefinitionV1(
                    enemyId,
                    new[]
                    {
                        new EnemyExperienceRewardBandV1(1, 50, 10L),
                        new EnemyExperienceRewardBandV1(50, 100, 20L),
                    }));
        }

        private static EnemyExperienceRewardCatalogV1 CreateCatalog(
            StableId enemyDefinitionStableId,
            long amount)
        {
            return new EnemyExperienceRewardCatalogV1(
                new[] { CreateDefinition(enemyDefinitionStableId, amount) });
        }

        private static EnemyExperienceRewardDefinitionV1 CreateDefinition(
            StableId enemyDefinitionStableId,
            long amount)
        {
            return new EnemyExperienceRewardDefinitionV1(
                enemyDefinitionStableId,
                new[] { new EnemyExperienceRewardBandV1(1, 100, amount) });
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

        private static PlayerExperienceAuthorityV1 CreateAuthority()
        {
            return CreateAuthority(CreateConstantCurve());
        }

        private static PlayerExperienceAuthorityV1 CreateAuthority(
            PlayerExperienceCurveV1 curve)
        {
            return new PlayerExperienceAuthorityV1(
                curve,
                ProgressionContext.Create(
                    1,
                    1,
                    StableId.Parse("difficulty.normal"),
                    0,
                    new[] { StableId.Parse("progression-tag.campaign") }));
        }

        private static PlayerExperienceCurveV1 CreateConstantCurve()
        {
            return new PlayerExperienceCurveV1(
                100L,
                100L,
                50,
                new SoftActivationCurveParameters(0.1, 10L, 10L));
        }
    }
}
