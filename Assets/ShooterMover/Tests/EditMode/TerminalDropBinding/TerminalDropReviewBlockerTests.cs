#if UNITY_EDITOR
using System;
using NUnit.Framework;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Authoring;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Props;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.TerminalDropBinding;

namespace ShooterMover.Tests.EditMode.TerminalDropBinding
{
    public sealed class TerminalDropReviewBlockerTests
    {
        private static readonly StableId RunId = Id("run", "review-blockers");
        private static readonly StableId PlayerEntityId = Id("entity", "player");
        private static readonly StableId PlayerParticipantId = Id("participant", "player");
        private static readonly StableId ProfileId = Id("drop-profile", "review-money");

        private sealed class PipelineFact
        {
            public PipelineFact(string value)
            {
                EventId = Id("terminal", value);
            }

            public StableId EventId { get; }
        }

        private sealed class PipelineAdapter : ITerminalDropFactAdapterV1
        {
            public StableId FactKindStableId
            {
                get { return Id("terminal-drop-fact", "review-pipeline"); }
            }

            public Type FactType { get { return typeof(PipelineFact); } }

            public TerminalDropAdaptationResultV1 Adapt(object terminalFact)
            {
                PipelineFact fact = terminalFact as PipelineFact;
                if (fact == null)
                {
                    return TerminalDropAdaptationResultV1.Rejected(
                        TerminalDropRejectionCodeV1.InvalidTerminalFact,
                        "review-pipeline-type-mismatch");
                }

                return TerminalDropAdaptationResultV1.Accepted(
                    new TerminalDropSourceFactV1(
                        FactKindStableId,
                        fact.EventId,
                        Id("trigger", fact.EventId.Value),
                        RunId,
                        1L,
                        Id("entity", "review-source"),
                        Id("placement", "review-source"),
                        1L,
                        Id("definition", "review-source"),
                        PlayerParticipantId,
                        PlayerEntityId,
                        Id("damage", "kinetic"),
                        ProfileId,
                        "review-source-context",
                        "review-definition-fingerprint",
                        "review-upstream-fingerprint"));
            }
        }

        private sealed class ThrowOnceAdapter : ITerminalDropFactAdapterV1
        {
            private readonly ITerminalDropFactAdapterV1 inner;
            private int calls;

            public ThrowOnceAdapter(ITerminalDropFactAdapterV1 inner)
            {
                this.inner = inner;
            }

            public StableId FactKindStableId { get { return inner.FactKindStableId; } }
            public Type FactType { get { return inner.FactType; } }

            public TerminalDropAdaptationResultV1 Adapt(object terminalFact)
            {
                calls++;
                if (calls == 1) throw new InvalidOperationException("adapter-transient");
                return inner.Adapt(terminalFact);
            }
        }

        private class FixedRunResolver : ITerminalDropRunContextResolverV1
        {
            public virtual bool TryResolve(
                StableId runStableId,
                long expectedLifecycleGeneration,
                out TerminalDropRunGenerationContextV1 context,
                out TerminalDropRejectionCodeV1 rejectionCode,
                out string diagnostic)
            {
                if (runStableId != RunId || expectedLifecycleGeneration != 1L)
                {
                    context = null;
                    rejectionCode = TerminalDropRejectionCodeV1.WrongRunLifecycle;
                    diagnostic = "review-run-context-mismatch";
                    return false;
                }

                context = new TerminalDropRunGenerationContextV1(
                    RunId,
                    1L,
                    12345UL,
                    1,
                    ProgressionContext.Create(
                        10,
                        2,
                        Id("difficulty", "normal"),
                        1),
                    "review-event-context");
                rejectionCode = TerminalDropRejectionCodeV1.None;
                diagnostic = string.Empty;
                return true;
            }
        }

        private sealed class ThrowOnceRunResolver : FixedRunResolver
        {
            private int calls;

            public override bool TryResolve(
                StableId runStableId,
                long expectedLifecycleGeneration,
                out TerminalDropRunGenerationContextV1 context,
                out TerminalDropRejectionCodeV1 rejectionCode,
                out string diagnostic)
            {
                calls++;
                if (calls == 1) throw new InvalidOperationException("run-context-transient");
                return base.TryResolve(
                    runStableId,
                    expectedLifecycleGeneration,
                    out context,
                    out rejectionCode,
                    out diagnostic);
            }
        }

        private sealed class ThrowOnceProfileResolver : IRewardProfileResolverV1
        {
            private readonly RewardProfileCatalogResolverV1 inner;
            private int calls;

            public ThrowOnceProfileResolver(RewardProfileV1 profile)
            {
                inner = new RewardProfileCatalogResolverV1(new[] { profile });
            }

            public string Fingerprint { get { return inner.Fingerprint; } }

            public bool TryResolve(StableId profileStableId, out RewardProfileV1 profile)
            {
                calls++;
                if (calls == 1) throw new InvalidOperationException("profile-transient");
                return inner.TryResolve(profileStableId, out profile);
            }
        }

        private sealed class CountingGenerator : IRewardGenerationExecutorV1
        {
            private readonly IRewardGenerationExecutorV1 inner =
                new ExistingRewardGenerationExecutorV1(new RewardGenerationServiceV1());

            public int CallCount { get; private set; }

            public RewardGenerationResultEnvelopeV1 Generate(RewardGenerationRequestV1 request)
            {
                CallCount++;
                return inner.Generate(request);
            }
        }

        private sealed class ThrowOnceGenerator : IRewardGenerationExecutorV1
        {
            private readonly IRewardGenerationExecutorV1 inner =
                new ExistingRewardGenerationExecutorV1(new RewardGenerationServiceV1());
            private int calls;

            public RewardGenerationResultEnvelopeV1 Generate(RewardGenerationRequestV1 request)
            {
                calls++;
                if (calls == 1) throw new InvalidOperationException("generation-transient");
                return inner.Generate(request);
            }
        }

        private class EnemyContextResolver : IEnemyTerminalSourceContextResolverV1
        {
            public virtual bool TryResolve(
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
                    "review-enemy-context:" + fact.Identity.EntityInstanceId);
                diagnostic = string.Empty;
                return true;
            }
        }

        private sealed class ThrowOnceEnemyContextResolver : EnemyContextResolver
        {
            private int calls;

            public override bool TryResolve(
                EnemyDeathFactV1 fact,
                out EnemyTerminalSourceContextV1 context,
                out string diagnostic)
            {
                calls++;
                if (calls == 1) throw new InvalidOperationException("enemy-context-transient");
                return base.TryResolve(fact, out context, out diagnostic);
            }
        }

        private class PropContextResolver : IPropTerminalSourceContextResolverV1
        {
            public virtual bool TryResolve(
                PropTerminalFactV1 fact,
                out PropTerminalSourceContextV1 context,
                out string diagnostic)
            {
                context = new PropTerminalSourceContextV1(
                    RunId,
                    1L,
                    fact.PropParticipantId,
                    Id("placement", "review-prop"),
                    1L,
                    "review-prop-context:" + fact.PropParticipantId);
                diagnostic = string.Empty;
                return true;
            }
        }

        private sealed class ThrowOncePropContextResolver : PropContextResolver
        {
            private int calls;

            public override bool TryResolve(
                PropTerminalFactV1 fact,
                out PropTerminalSourceContextV1 context,
                out string diagnostic)
            {
                calls++;
                if (calls == 1) throw new InvalidOperationException("prop-context-transient");
                return base.TryResolve(fact, out context, out diagnostic);
            }
        }

        private sealed class MismatchedPropContextResolver : PropContextResolver
        {
            public override bool TryResolve(
                PropTerminalFactV1 fact,
                out PropTerminalSourceContextV1 context,
                out string diagnostic)
            {
                context = new PropTerminalSourceContextV1(
                    RunId,
                    1L,
                    Id("prop-participant", "different-prop"),
                    Id("placement", "review-prop"),
                    1L,
                    "review-prop-mismatched-context");
                diagnostic = string.Empty;
                return true;
            }
        }

        private sealed class FailOncePendingAdmission :
            IGeneratedTerminalDropPendingAdmissionV1
        {
            private readonly PendingTerminalDropAdmissionAuthorityV1 inner;
            private int calls;

            public FailOncePendingAdmission(PendingTerminalDropAdmissionAuthorityV1 inner)
            {
                this.inner = inner;
            }

            public PendingTerminalDropAdmissionResultV1 Admit(
                GeneratedTerminalDropResultV1 result)
            {
                calls++;
                if (calls == 1)
                {
                    return PendingTerminalDropAdmissionResultV1.Rejected(
                        "pending-publication-transient");
                }
                return inner.Admit(result);
            }
        }

        private sealed class AlwaysAllowPropDamage : IPropDamageEligibilityPolicyV1
        {
            public bool CanDamage(PropDamageEligibilityContextV1 context)
            {
                return true;
            }
        }

        [Test]
        public void TwoConsumerDeliveries_CreateOnePendingBatch()
        {
            EnemyDefinitionV1 definition = EnemyDefinition();
            EnemyDeathFactV1 death = EnemyDeath(definition);
            CountingGenerator generator = new CountingGenerator();
            TerminalDropGenerationAuthorityV1 authority = EnemyAuthority(
                definition,
                new EnemyContextResolver(),
                generator);
            var pending = new PendingTerminalDropAdmissionAuthorityV1();
            var consumer = new EnemyTerminalDropFactConsumerV1(authority, pending);

            consumer.Consume(death);
            Assert.That(consumer.LastAdmission.Status,
                Is.EqualTo(PendingTerminalDropAdmissionStatusV1.Accepted));
            consumer.Consume(death);

            Assert.That(consumer.LastAdmission.Status,
                Is.EqualTo(PendingTerminalDropAdmissionStatusV1.ExactReplay));
            Assert.That(pending.PendingBatchCount, Is.EqualTo(1));
            Assert.That(generator.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void FailedFirstPublication_ExactReplayCanRecoverPendingBatch()
        {
            EnemyDefinitionV1 definition = EnemyDefinition();
            EnemyDeathFactV1 death = EnemyDeath(definition);
            TerminalDropGenerationAuthorityV1 authority = EnemyAuthority(
                definition,
                new EnemyContextResolver(),
                new CountingGenerator());
            var durablePending = new PendingTerminalDropAdmissionAuthorityV1();
            var failOnce = new FailOncePendingAdmission(durablePending);
            var consumer = new EnemyTerminalDropFactConsumerV1(authority, failOnce);

            consumer.Consume(death);
            Assert.That(consumer.LastAdmission.Status,
                Is.EqualTo(PendingTerminalDropAdmissionStatusV1.Rejected));
            Assert.That(durablePending.PendingBatchCount, Is.EqualTo(0));

            consumer.Consume(death);
            Assert.That(consumer.LastAdmission.Status,
                Is.EqualTo(PendingTerminalDropAdmissionStatusV1.Accepted));
            Assert.That(durablePending.PendingBatchCount, Is.EqualTo(1));
        }

        [Test]
        public void RebuiltGenerationAuthority_RedeliveryStillCannotDuplicatePendingBatch()
        {
            EnemyDefinitionV1 definition = EnemyDefinition();
            EnemyDeathFactV1 death = EnemyDeath(definition);
            var pending = new PendingTerminalDropAdmissionAuthorityV1();
            var firstConsumer = new EnemyTerminalDropFactConsumerV1(
                EnemyAuthority(definition, new EnemyContextResolver(), new CountingGenerator()),
                pending);
            var rebuiltConsumer = new EnemyTerminalDropFactConsumerV1(
                EnemyAuthority(definition, new EnemyContextResolver(), new CountingGenerator()),
                pending);

            firstConsumer.Consume(death);
            rebuiltConsumer.Consume(death);

            Assert.That(firstConsumer.LastAdmission.Status,
                Is.EqualTo(PendingTerminalDropAdmissionStatusV1.Accepted));
            Assert.That(rebuiltConsumer.LastAdmission.Status,
                Is.EqualTo(PendingTerminalDropAdmissionStatusV1.ExactReplay));
            Assert.That(pending.PendingBatchCount, Is.EqualTo(1));
        }

        [Test]
        public void ConflictingPendingOperation_RejectsWithoutSecondEntry()
        {
            GeneratedTerminalDropResultV1 accepted = PipelineAuthority().Generate(
                new PipelineFact("pending-conflict"));
            var pending = new PendingTerminalDropAdmissionAuthorityV1();
            PendingTerminalDropAdmissionResultV1 first = pending.Admit(accepted);
            var conflicting = new GeneratedTerminalDropResultV1(
                TerminalDropBindingStatusV1.Accepted,
                TerminalDropRejectionCodeV1.None,
                accepted.SourceFact,
                accepted.ResolvedDropProfileStableId,
                accepted.OperationRequest,
                accepted.GenerationSeed,
                accepted.GeneratedBatch,
                accepted.GeneratedRewards,
                "sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff",
                string.Empty);

            PendingTerminalDropAdmissionResultV1 conflict = pending.Admit(conflicting);

            Assert.That(first.Status,
                Is.EqualTo(PendingTerminalDropAdmissionStatusV1.Accepted));
            Assert.That(conflict.Status,
                Is.EqualTo(PendingTerminalDropAdmissionStatusV1.ConflictingDuplicate));
            Assert.That(pending.PendingBatchCount, Is.EqualTo(1));
        }

        [Test]
        public void ThrowingTerminalAdapter_RejectsUncachedThenRetrySucceeds()
        {
            var adapter = new ThrowOnceAdapter(new PipelineAdapter());
            TerminalDropGenerationAuthorityV1 authority = PipelineAuthority(adapter: adapter);
            var fact = new PipelineFact("adapter-retry");

            AssertRetryableStage(authority, fact, "adaptation");
        }

        [Test]
        public void ThrowingEnemySourceResolver_RejectsUncachedThenRetrySucceeds()
        {
            EnemyDefinitionV1 definition = EnemyDefinition();
            TerminalDropGenerationAuthorityV1 authority = EnemyAuthority(
                definition,
                new ThrowOnceEnemyContextResolver(),
                new CountingGenerator());

            GeneratedTerminalDropResultV1 first = authority.Generate(EnemyDeath(definition));
            Assert.That(first.Status, Is.EqualTo(TerminalDropBindingStatusV1.Rejected));
            Assert.That(first.Diagnostic, Does.Contain("enemy-source-context-exception"));
            Assert.That(authority.AcceptedBatchCount, Is.EqualTo(0));

            GeneratedTerminalDropResultV1 retry = authority.Generate(EnemyDeath(definition));
            Assert.That(retry.Status, Is.EqualTo(TerminalDropBindingStatusV1.Accepted));
            Assert.That(authority.AcceptedBatchCount, Is.EqualTo(1));
        }

        [Test]
        public void ThrowingPropSourceResolver_RejectsUncachedThenRetrySucceeds()
        {
            PropDefinitionV1 definition = PropDefinition();
            PropCatalogV1 catalog = PropCatalog(definition);
            PropFactBatchV1 fact = DestroyedPropFacts(catalog, definition);
            TerminalDropGenerationAuthorityV1 authority = new TerminalDropGenerationAuthorityV1(
                new TerminalDropFactAdapterRegistryV1(new ITerminalDropFactAdapterV1[]
                {
                    new PropDestructionTerminalDropFactAdapterV1(
                        catalog,
                        new ThrowOncePropContextResolver())
                }),
                new FixedRunResolver(),
                Profiles(),
                new CountingGenerator());

            GeneratedTerminalDropResultV1 first = authority.Generate(fact);
            Assert.That(first.Status, Is.EqualTo(TerminalDropBindingStatusV1.Rejected));
            Assert.That(first.Diagnostic, Does.Contain("prop-source-context-exception"));
            Assert.That(authority.AcceptedBatchCount, Is.EqualTo(0));

            GeneratedTerminalDropResultV1 retry = authority.Generate(fact);
            Assert.That(retry.Status, Is.EqualTo(TerminalDropBindingStatusV1.Accepted));
            Assert.That(authority.AcceptedBatchCount, Is.EqualTo(1));
        }

        [Test]
        public void ThrowingRunResolver_RejectsUncachedThenRetrySucceeds()
        {
            TerminalDropGenerationAuthorityV1 authority = PipelineAuthority(
                runResolver: new ThrowOnceRunResolver());
            AssertRetryableStage(
                authority,
                new PipelineFact("run-retry"),
                "run-context-resolution");
        }

        [Test]
        public void ThrowingProfileResolver_RejectsUncachedThenRetrySucceeds()
        {
            TerminalDropGenerationAuthorityV1 authority = PipelineAuthority(
                profiles: new ThrowOnceProfileResolver(Profile()));
            AssertRetryableStage(
                authority,
                new PipelineFact("profile-retry"),
                "profile-resolution");
        }

        [Test]
        public void ThrowingGenExecutor_RejectsUncachedThenRetrySucceeds()
        {
            TerminalDropGenerationAuthorityV1 authority = PipelineAuthority(
                generator: new ThrowOnceGenerator());
            AssertRetryableStage(
                authority,
                new PipelineFact("generation-retry"),
                "generation-execution");
        }

        [Test]
        public void PropSourceContextForDifferentProp_RejectsBeforeGen()
        {
            PropDefinitionV1 definition = PropDefinition();
            PropCatalogV1 catalog = PropCatalog(definition);
            PropFactBatchV1 fact = DestroyedPropFacts(catalog, definition);
            var generator = new CountingGenerator();
            TerminalDropGenerationAuthorityV1 authority = new TerminalDropGenerationAuthorityV1(
                new TerminalDropFactAdapterRegistryV1(new ITerminalDropFactAdapterV1[]
                {
                    new PropDestructionTerminalDropFactAdapterV1(
                        catalog,
                        new MismatchedPropContextResolver())
                }),
                new FixedRunResolver(),
                Profiles(),
                generator);

            GeneratedTerminalDropResultV1 result = authority.Generate(fact);

            Assert.That(result.Status, Is.EqualTo(TerminalDropBindingStatusV1.Rejected));
            Assert.That(result.RejectionCode,
                Is.EqualTo(TerminalDropRejectionCodeV1.MissingSourceContext));
            Assert.That(result.Diagnostic, Does.Contain("prop-source-context-entity-mismatch"));
            Assert.That(generator.CallCount, Is.EqualTo(0));
            Assert.That(authority.AcceptedBatchCount, Is.EqualTo(0));
        }

        private static void AssertRetryableStage(
            TerminalDropGenerationAuthorityV1 authority,
            object fact,
            string stage)
        {
            GeneratedTerminalDropResultV1 first = authority.Generate(fact);
            Assert.That(first.Status, Is.EqualTo(TerminalDropBindingStatusV1.Rejected));
            Assert.That(first.Diagnostic, Does.Contain(stage));
            Assert.That(authority.AcceptedBatchCount, Is.EqualTo(0));

            GeneratedTerminalDropResultV1 retry = authority.Generate(fact);
            Assert.That(retry.Status, Is.EqualTo(TerminalDropBindingStatusV1.Accepted));
            Assert.That(authority.AcceptedBatchCount, Is.EqualTo(1));
        }

        private static TerminalDropGenerationAuthorityV1 PipelineAuthority(
            ITerminalDropFactAdapterV1 adapter = null,
            ITerminalDropRunContextResolverV1 runResolver = null,
            IRewardProfileResolverV1 profiles = null,
            IRewardGenerationExecutorV1 generator = null)
        {
            return new TerminalDropGenerationAuthorityV1(
                new TerminalDropFactAdapterRegistryV1(new ITerminalDropFactAdapterV1[]
                {
                    adapter ?? new PipelineAdapter()
                }),
                runResolver ?? new FixedRunResolver(),
                profiles ?? Profiles(),
                generator ?? new CountingGenerator());
        }

        private static TerminalDropGenerationAuthorityV1 EnemyAuthority(
            EnemyDefinitionV1 definition,
            IEnemyTerminalSourceContextResolverV1 contexts,
            IRewardGenerationExecutorV1 generator)
        {
            EnemyCatalogV1 catalog = new EnemyCatalogV1(
                EnemyCatalogV1.SupportedSchemaVersion,
                Id("enemy-content", "review-blockers"),
                new[] { definition });
            return new TerminalDropGenerationAuthorityV1(
                new TerminalDropFactAdapterRegistryV1(new ITerminalDropFactAdapterV1[]
                {
                    new ContextResolvedEnemyDeathTerminalDropFactAdapterV1(
                        catalog,
                        contexts)
                }),
                new FixedRunResolver(),
                Profiles(),
                generator);
        }

        private static RewardProfileCatalogResolverV1 Profiles()
        {
            return new RewardProfileCatalogResolverV1(new[] { Profile() });
        }

        private static RewardProfileV1 Profile()
        {
            return RewardProfileV1.Create(
                ProfileId,
                new[]
                {
                    RewardGrantSpecificationV1.CreateFixed(
                        Id("grant", "review-money"),
                        RewardGrantKindV1.Money,
                        Id("currency", "credits"),
                        5L)
                },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());
        }

        private static EnemyDefinitionV1 EnemyDefinition()
        {
            return new EnemyDefinitionV1(
                Id("enemy", "review-blocker"),
                Id("presentation", "review-blocker"),
                10d,
                new EnemyLevelScalingProfileV1(1, 100, 0d, 1d),
                Id("faction", "enemy"),
                10d,
                360d,
                Id("movement", "fixture"),
                Id("decision", "fixture"),
                Array.Empty<EnemyAttackCapabilityDescriptorV1>(),
                Id("experience-profile", "fixture"),
                ProfileId,
                EnemyCatalogRoomClearRoleV1.RequiredEnemy,
                Array.Empty<StableId>());
        }

        private static EnemyDeathFactV1 EnemyDeath(EnemyDefinitionV1 definition)
        {
            var identity = new EnemyRuntimeIdentityV1(
                Id("enemy-entity", "review-blocker"),
                Id("run-participant", "review-blocker"),
                RunId,
                Id("room-runtime", "review-blocker"),
                Id("room", "review-blocker"),
                Id("placement", "review-blocker"));
            return new EnemyDeathFactV1(
                Id("death", "review-blocker"),
                Id("trigger", "review-blocker"),
                identity,
                definition.DefinitionId,
                1,
                2L,
                PlayerEntityId,
                PlayerParticipantId,
                definition.ExperienceProfileId,
                definition.DropProfileId,
                EnemyActorDeathCause.IncomingDamage);
        }

        private static PropDefinitionV1 PropDefinition()
        {
            return new PropDefinitionV1(
                Id("prop", "review-blocker"),
                Id("presentation", "review-blocker-prop"),
                new[]
                {
                    PropCapabilitiesV1.Collision(true),
                    PropCapabilitiesV1.HealthBased(10d),
                    PropCapabilitiesV1.DamageBehavior(
                        PropDamageAlignmentV1.Hostile,
                        Id("damage-policy", "player-normal")),
                    PropCapabilitiesV1.DropOnDestroy(ProfileId)
                });
        }

        private static PropCatalogV1 PropCatalog(PropDefinitionV1 definition)
        {
            return new PropCatalogV1(
                PropCapabilityRegistryV1.CreateBuiltIns(),
                new[] { definition });
        }

        private static PropFactBatchV1 DestroyedPropFacts(
            PropCatalogV1 catalog,
            PropDefinitionV1 definition)
        {
            PropRuntimeCreationResultV1 created = new PropRuntimeFactoryV1().Create(
                catalog,
                new PropPlacementV1(
                    PlacedObjectIdentity.CreateAuthored(Id("placement", "review-prop")),
                    definition.DefinitionId),
                new AlwaysAllowPropDamage());
            Assert.That(created.IsCreated, Is.True);
            PropDamageResultV1 destroyed = created.Runtime.ApplyDamage(
                new PropDamageCommandV1(
                    Id("operation", "destroy-review-prop"),
                    PlayerParticipantId,
                    Id("faction", "player"),
                    Id("damage", "kinetic"),
                    10d));
            Assert.That(destroyed.Status, Is.EqualTo(PropDamageStatusV1.Destroyed));
            return destroyed.Facts;
        }

        private static StableId Id(string scope, string value)
        {
            return StableId.Create(scope, value);
        }
    }
}
#endif
