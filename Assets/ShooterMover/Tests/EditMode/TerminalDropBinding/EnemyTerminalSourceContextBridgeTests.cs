#if UNITY_EDITOR
using System;
using NUnit.Framework;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.TerminalDropBinding;

namespace ShooterMover.Tests.EditMode.TerminalDropBinding
{
    public sealed class EnemyTerminalSourceContextBridgeTests
    {
        private sealed class EnemyContextResolver : IEnemyTerminalSourceContextResolver
        {
            public bool TryResolve(
                EnemyDeathFact fact,
                out EnemyTerminalSourceContext context,
                out string diagnostic)
            {
                context = new EnemyTerminalSourceContext(
                    fact.Identity.RunStableId,
                    1L,
                    fact.Identity.EntityInstanceId,
                    fact.Identity.PlacementStableId,
                    fact.LifecycleGeneration,
                    "enemy-context-run-one-source-" + fact.LifecycleGeneration);
                diagnostic = string.Empty;
                return true;
            }
        }

        private sealed class RunContextResolver : ITerminalDropRunContextResolver
        {
            public bool TryResolve(
                StableId runStableId,
                long expectedLifecycleGeneration,
                out TerminalDropRunGenerationContext context,
                out TerminalDropRejectionCode rejectionCode,
                out string diagnostic)
            {
                if (expectedLifecycleGeneration != 1L)
                {
                    context = null;
                    rejectionCode = TerminalDropRejectionCode.WrongRunLifecycle;
                    diagnostic = "expected-run-generation-one";
                    return false;
                }

                context = new TerminalDropRunGenerationContext(
                    runStableId,
                    1L,
                    42UL,
                    1,
                    ProgressionContext.Create(5, 2, Id("difficulty", "normal"), 1),
                    "enemy-context-event-fixture");
                rejectionCode = TerminalDropRejectionCode.None;
                diagnostic = string.Empty;
                return true;
            }
        }

        [Test]
        public void EnemyLifecycleTwo_InRunLifecycleOne_GeneratesSuccessfully()
        {
            StableId profileId = Id("drop-profile", "enemy-context");
            EnemyDefinition definition = new EnemyDefinition(
                Id("enemy", "context-fixture"),
                Id("presentation", "context-fixture"),
                10d,
                new EnemyLevelScalingProfile(1, 100, 0d, 1d),
                Id("faction", "enemy"),
                10d,
                360d,
                Id("movement", "fixture"),
                Id("decision", "fixture"),
                Array.Empty<EnemyAttackCapabilityDescriptor>(),
                Id("experience-profile", "fixture"),
                profileId,
                EnemyCatalogRoomClearRole.RequiredEnemy,
                Array.Empty<StableId>());
            EnemyCatalog catalog = new EnemyCatalog(
                EnemyCatalog.SupportedSchemaVersion,
                Id("enemy-content", "context-fixture"),
                new[] { definition });
            EnemyLiveIdentity identity = new EnemyLiveIdentity(
                Id("enemy-entity", "context-fixture"),
                Id("run-participant", "context-fixture"),
                Id("run", "context-fixture"),
                Id("room-runtime", "context-fixture"),
                Id("room", "context-fixture"),
                Id("placement", "context-fixture"));
            EnemyDeathFact death = new EnemyDeathFact(
                Id("death", "context-fixture"),
                Id("trigger", "context-fixture"),
                identity,
                definition.DefinitionId,
                5,
                2L,
                Id("entity", "player"),
                Id("participant", "player"),
                definition.ExperienceProfileId,
                definition.DropProfileId,
                (EnemyActorDeathCause)1);
            RewardProfile profile = RewardProfile.Create(
                profileId,
                new[]
                {
                    RewardGrantSpecification.CreateFixed(
                        Id("grant", "enemy-context-money"),
                        RewardGrantKind.Money,
                        Id("currency", "credits"),
                        1L)
                },
                Array.Empty<IndependentRewardRoll>(),
                Array.Empty<ExclusiveRewardGroup>());
            ITerminalDropFactBridge adapter =
                new ContextResolvedEnemyDeathTerminalDropFactBridge(
                    catalog,
                    new EnemyContextResolver());
            TerminalDropGenerationState authority =
                new TerminalDropGenerationState(
                    new TerminalDropFactBridgeRegistry(new[] { adapter }),
                    new RunContextResolver(),
                    new RewardProfileCatalogResolver(new[] { profile }),
                    new ExistingRewardGenerationExecutor(
                        new RewardGenerationActions()));

            GeneratedTerminalDropResult result = authority.Generate(death);

            Assert.That(result.Status, Is.EqualTo(TerminalDropBindingStatus.Accepted));
            Assert.That(result.SourceFact.RunLifecycleGeneration, Is.EqualTo(1L));
            Assert.That(result.SourceFact.SourceLifecycleGeneration, Is.EqualTo(2L));
            Assert.That(result.GeneratedRewards.Count, Is.EqualTo(1));
        }

        [Test]
        public void CatalogOnlyEnemyProjector_IsInternalAndCannotActAsCompleteAdapter()
        {
            Type assemblyProjector = typeof(ContextResolvedEnemyDeathTerminalDropFactBridge)
                .Assembly.GetType(
                    "ShooterMover.TerminalDropBinding.EnemyDeathTerminalDropDefinitionProjector",
                    true);
            Type unsafeRawAdapter = typeof(ContextResolvedEnemyDeathTerminalDropFactBridge)
                .Assembly.GetType(
                    "ShooterMover.TerminalDropBinding.EnemyDeathTerminalDropFactBridge",
                    false);

            Assert.That(assemblyProjector.IsPublic, Is.False);
            Assert.That(typeof(ITerminalDropFactBridge).IsAssignableFrom(assemblyProjector), Is.False);
            Assert.That(unsafeRawAdapter, Is.Null);
        }

        private static StableId Id(string scope, string value)
        {
            return StableId.Create(scope, value);
        }
    }
}
#endif
