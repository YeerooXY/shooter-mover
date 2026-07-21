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
    public sealed class EnemyTerminalSourceContextAdapterV1Tests
    {
        private sealed class EnemyContextResolver : IEnemyTerminalSourceContextResolverV1
        {
            public bool TryResolve(
                EnemyDeathFactV1 fact,
                out EnemyTerminalSourceContextV1 context,
                out string diagnostic)
            {
                context = new EnemyTerminalSourceContextV1(
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

        private sealed class RunContextResolver : ITerminalDropRunContextResolverV1
        {
            public bool TryResolve(
                StableId runStableId,
                long expectedLifecycleGeneration,
                out TerminalDropRunGenerationContextV1 context,
                out TerminalDropRejectionCodeV1 rejectionCode,
                out string diagnostic)
            {
                if (expectedLifecycleGeneration != 1L)
                {
                    context = null;
                    rejectionCode = TerminalDropRejectionCodeV1.WrongRunLifecycle;
                    diagnostic = "expected-run-generation-one";
                    return false;
                }

                context = new TerminalDropRunGenerationContextV1(
                    runStableId,
                    1L,
                    42UL,
                    1,
                    ProgressionContext.Create(5, 2, Id("difficulty", "normal"), 1),
                    "enemy-context-event-fixture");
                rejectionCode = TerminalDropRejectionCodeV1.None;
                diagnostic = string.Empty;
                return true;
            }
        }

        [Test]
        public void EnemyLifecycleTwo_InRunLifecycleOne_GeneratesSuccessfully()
        {
            StableId profileId = Id("drop-profile", "enemy-context");
            EnemyDefinitionV1 definition = new EnemyDefinitionV1(
                Id("enemy", "context-fixture"),
                Id("presentation", "context-fixture"),
                10d,
                new EnemyLevelScalingProfileV1(1, 100, 0d, 1d),
                Id("faction", "enemy"),
                10d,
                360d,
                Id("movement", "fixture"),
                Id("decision", "fixture"),
                Array.Empty<EnemyAttackCapabilityDescriptorV1>(),
                Id("experience-profile", "fixture"),
                profileId,
                EnemyCatalogRoomClearRoleV1.RequiredEnemy,
                Array.Empty<StableId>());
            EnemyCatalogV1 catalog = new EnemyCatalogV1(
                EnemyCatalogV1.SupportedSchemaVersion,
                Id("enemy-content", "context-fixture"),
                new[] { definition });
            EnemyRuntimeIdentityV1 identity = new EnemyRuntimeIdentityV1(
                Id("enemy-entity", "context-fixture"),
                Id("run-participant", "context-fixture"),
                Id("run", "context-fixture"),
                Id("room-runtime", "context-fixture"),
                Id("room", "context-fixture"),
                Id("placement", "context-fixture"));
            EnemyDeathFactV1 death = new EnemyDeathFactV1(
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
            RewardProfileV1 profile = RewardProfileV1.Create(
                profileId,
                new[]
                {
                    RewardGrantSpecificationV1.CreateFixed(
                        Id("grant", "enemy-context-money"),
                        RewardGrantKindV1.Money,
                        Id("currency", "credits"),
                        1L)
                },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());
            ITerminalDropFactAdapterV1 adapter =
                new ContextResolvedEnemyDeathTerminalDropFactAdapterV1(
                    new EnemyDeathTerminalDropFactAdapterV1(catalog),
                    new EnemyContextResolver());
            TerminalDropGenerationAuthorityV1 authority =
                new TerminalDropGenerationAuthorityV1(
                    new TerminalDropFactAdapterRegistryV1(new[] { adapter }),
                    new RunContextResolver(),
                    new RewardProfileCatalogResolverV1(new[] { profile }),
                    new ExistingRewardGenerationExecutorV1(
                        new RewardGenerationServiceV1()));

            GeneratedTerminalDropResultV1 result = authority.Generate(death);

            Assert.That(result.Status, Is.EqualTo(TerminalDropBindingStatusV1.Accepted));
            Assert.That(result.SourceFact.RunLifecycleGeneration, Is.EqualTo(1L));
            Assert.That(result.SourceFact.SourceLifecycleGeneration, Is.EqualTo(2L));
            Assert.That(result.GeneratedRewards.Count, Is.EqualTo(1));
        }

        private static StableId Id(string scope, string value)
        {
            return StableId.Create(scope, value);
        }
    }
}
#endif
