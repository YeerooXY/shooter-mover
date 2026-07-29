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

        private sealed class PipelineBridge : ITerminalDropFactBridge
        {
            public StableId FactKindStableId
            {
                get { return Id("terminal-drop-fact", "review-pipeline"); }
            }

            public Type FactType { get { return typeof(PipelineFact); } }

            public TerminalDropAdaptationResult Adapt(object terminalFact)
            {
                PipelineFact fact = terminalFact as PipelineFact;
                if (fact == null)
                {
                    return TerminalDropAdaptationResult.Rejected(
                        TerminalDropRejectionCode.InvalidTerminalFact,
                        "review-pipeline-type-mismatch");
                }

                return TerminalDropAdaptationResult.Accepted(
                    new TerminalDropSourceFact(
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

        private sealed class ThrowOnceBridge : ITerminalDropFactBridge
        {
            private readonly ITerminalDropFactBridge inner;
            private int calls;

            public ThrowOnceBridge(ITerminalDropFactBridge inner)
            {
                this.inner = inner;
            }

            public StableId FactKindStableId { get { return inner.FactKindStableId; } }
            public Type FactType { get { return inner.FactType; } }

            public TerminalDropAdaptationResult Adapt(object terminalFact)
            {
                calls++;
                if (calls == 1) throw new InvalidOperationException("adapter-transient");
                return inner.Adapt(terminalFact);
            }
        }

        private class FixedRunResolver : ITerminalDropRunContextResolver
        {
            public virtual bool TryResolve(
                StableId runStableId,
                long expectedLifecycleGeneration,
                out TerminalDropRunGenerationContext context,
                out TerminalDropRejectionCode rejectionCode,
                out string diagnostic)
            {
                if (runStableId != RunId || expectedLifecycleGeneration != 1L)
                {
                    context = null;
                    rejectionCode = TerminalDropRejectionCode.WrongRunLifecycle;
                    diagnostic = "review-run-context-mismatch";
                    return false;
                }

                context = new TerminalDropRunGenerationContext(
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
                rejectionCode = TerminalDropRejectionCode.None;
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
                out TerminalDropRunGenerationContext context,
                out TerminalDropRejectionCode rejectionCode,
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

        private sealed class ThrowOnceProfileResolver : IRewardProfileResolver
        {
            private readonly RewardProfileCatalogResolver inner;
            private int calls;

            public ThrowOnceProfileResolver(RewardProfile profile)
            {
                inner = new RewardProfileCatalogResolver(new[] { profile });
            }

            public string Fingerprint { get { return inner.Fingerprint; } }

            public bool TryResolve(StableId profileStableId, out RewardProfile profile)
            {
                calls++;
                if (calls == 1) throw new InvalidOperationException("profile-transient");
                return inner.TryResolve(profileStableId, out profile);
            }
        }

        private sealed class CountingGenerator : IRewardGenerationExecutor
        {
            private readonly IRewardGenerationExecutor inner =
                new ExistingRewardGenerationExecutor(new RewardGenerationActions());

            public int CallCount { get; private set; }

            public RewardGenerationResultEnvelope Generate(RewardGenerationRequest request)
            {
                CallCount++;
                return inner.Generate(request);
            }
        }

        private sealed class ThrowOnceGenerator : IRewardGenerationExecutor
        {
            private readonly IRewardGenerationExecutor inner =
                new ExistingRewardGenerationExecutor(new RewardGenerationActions());
            private int calls;

            public RewardGenerationResultEnvelope Generate(RewardGenerationRequest request)
            {
                calls++;
                if (calls == 1) throw new InvalidOperationException("generation-transient");
                return inner.Generate(request);
            }
        }

        private class EnemyContextResolver : IEnemyTerminalSourceContextResolver
        {
            public virtual bool TryResolve(
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
                    "review-enemy-context:" + fact.Identity.EntityInstanceId);
                diagnostic = string.Empty;
                return true;
            }
        }

        private sealed class ThrowOnceEnemyContextResolver : EnemyContextResolver
        {
            private int calls;

            public override bool TryResolve(
                EnemyDeathFact fact,
                out EnemyTerminalSourceContext context,
                out string diagnostic)
            {
                calls++;
                if (calls == 1) throw new InvalidOperationException("enemy-context-transient");
                return base.TryResolve(fact, out context, out diagnostic);
            }
        }

        private class PropContextResolver : IPropTerminalSourceContextResolver
        {
            public virtual bool TryResolve(
                PropTerminalFact fact,
                out PropTerminalSourceContext context,
                out string diagnostic)
            {
                context = new PropTerminalSourceContext(
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
                PropTerminalFact fact,
                out PropTerminalSourceContext context,
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
                PropTerminalFact fact,
                out PropTerminalSourceContext context,
                out string diagnostic)
            {
                context = new PropTerminalSourceContext(
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
            IGeneratedTerminalDropPendingAdmission
        {
            private readonly PendingTerminalDropAdmissionState inner;
            private int calls;

            public FailOncePendingAdmission(PendingTerminalDropAdmissionState inner)
            {
                this.inner = inner;
            }

            public PendingTerminalDropAdmissionResult Admit(
                GeneratedTerminalDropResult result)
            {
                calls++;
                if (calls == 1)
                {
                    return PendingTerminalDropAdmissionResult.Rejected(
                        "pending-publication-transient");
                }
                return inner.Admit(result);
            }
        }

        private sealed class AlwaysAllowPropDamage : IPropDamageEligibilityPolicy
        {
            public bool CanDamage(PropDamageEligibilityContext context)
            {
                return true;
            }
        }

        [Test]
        public void TwoConsumerDeliveries_CreateOnePendingBatch()
        {
            EnemyDefinition definition = EnemyDefinition();
            EnemyDeathFact death = EnemyDeath(definition);
            CountingGenerator generator = new CountingGenerator();
            TerminalDropGenerationState authority = EnemyAuthority(
                definition,
                new EnemyContextResolver(),
                generator);
            var pending = new PendingTerminalDropAdmissionState();
            var consumer = new EnemyTerminalDropFactConsumer(authority, pending);

            consumer.Consume(death);
            Assert.That(consumer.LastAdmission.Status,
                Is.EqualTo(PendingTerminalDropAdmissionStatus.Accepted));
            consumer.Consume(death);

            Assert.That(consumer.LastAdmission.Status,
                Is.EqualTo(PendingTerminalDropAdmissionStatus.ExactReplay));
            Assert.That(pending.PendingBatchCount, Is.EqualTo(1));
            Assert.That(generator.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void FailedFirstPublication_ExactReplayCanRecoverPendingBatch()
        {
            EnemyDefinition definition = EnemyDefinition();
            EnemyDeathFact death = EnemyDeath(definition);
            TerminalDropGenerationState authority = EnemyAuthority(
                definition,
                new EnemyContextResolver(),
                new CountingGenerator());
            var durablePending = new PendingTerminalDropAdmissionState();
            var failOnce = new FailOncePendingAdmission(durablePending);
            var consumer = new EnemyTerminalDropFactConsumer(authority, failOnce);

            consumer.Consume(death);
            Assert.That(consumer.LastAdmission.Status,
                Is.EqualTo(PendingTerminalDropAdmissionStatus.Rejected));
            Assert.That(durablePending.PendingBatchCount, Is.EqualTo(0));

            consumer.Consume(death);
            Assert.That(consumer.LastAdmission.Status,
                Is.EqualTo(PendingTerminalDropAdmissionStatus.Accepted));
            Assert.That(durablePending.PendingBatchCount, Is.EqualTo(1));
        }

        [Test]
        public void RebuiltGenerationAuthority_RedeliveryStillCannotDuplicatePendingBatch()
        {
            EnemyDefinition definition = EnemyDefinition();
            EnemyDeathFact death = EnemyDeath(definition);
            var pending = new PendingTerminalDropAdmissionState();
            var firstConsumer = new EnemyTerminalDropFactConsumer(
                EnemyAuthority(definition, new EnemyContextResolver(), new CountingGenerator()),
                pending);
            var rebuiltConsumer = new EnemyTerminalDropFactConsumer(
                EnemyAuthority(definition, new EnemyContextResolver(), new CountingGenerator()),
                pending);

            firstConsumer.Consume(death);
            rebuiltConsumer.Consume(death);

            Assert.That(firstConsumer.LastAdmission.Status,
                Is.EqualTo(PendingTerminalDropAdmissionStatus.Accepted));
            Assert.That(rebuiltConsumer.LastAdmission.Status,
                Is.EqualTo(PendingTerminalDropAdmissionStatus.ExactReplay));
            Assert.That(pending.PendingBatchCount, Is.EqualTo(1));
        }

        [Test]
        public void ConflictingPendingOperation_RejectsWithoutSecondEntry()
        {
            GeneratedTerminalDropResult accepted = PipelineAuthority().Generate(
                new PipelineFact("pending-conflict"));
            var pending = new PendingTerminalDropAdmissionState();
            PendingTerminalDropAdmissionResult first = pending.Admit(accepted);
            var conflicting = new GeneratedTerminalDropResult(
                TerminalDropBindingStatus.Accepted,
                TerminalDropRejectionCode.None,
                accepted.SourceFact,
                accepted.ResolvedDropProfileStableId,
                accepted.OperationRequest,
                accepted.GenerationSeed,
                accepted.GeneratedBatch,
                accepted.GeneratedRewards,
                "sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff",
                string.Empty);

            PendingTerminalDropAdmissionResult conflict = pending.Admit(conflicting);

            Assert.That(first.Status,
                Is.EqualTo(PendingTerminalDropAdmissionStatus.Accepted));
            Assert.That(conflict.Status,
                Is.EqualTo(PendingTerminalDropAdmissionStatus.ConflictingDuplicate));
            Assert.That(pending.PendingBatchCount, Is.EqualTo(1));
        }

        [Test]
        public void ThrowingTerminalAdapter_RejectsUncachedThenRetrySucceeds()
        {
            var adapter = new ThrowOnceBridge(new PipelineBridge());
            TerminalDropGenerationState authority = PipelineAuthority(adapter: adapter);
            var fact = new PipelineFact("adapter-retry");

            AssertRetryableStage(authority, fact, "adaptation");
        }

        [Test]
        public void ThrowingEnemySourceResolver_RejectsUncachedThenRetrySucceeds()
        {
            EnemyDefinition definition = EnemyDefinition();
            TerminalDropGenerationState authority = EnemyAuthority(
                definition,
                new ThrowOnceEnemyContextResolver(),
                new CountingGenerator());

            GeneratedTerminalDropResult first = authority.Generate(EnemyDeath(definition));
            Assert.That(first.Status, Is.EqualTo(TerminalDropBindingStatus.Rejected));
            Assert.That(first.Diagnostic, Does.Contain("enemy-source-context-exception"));
            Assert.That(authority.AcceptedBatchCount, Is.EqualTo(0));

            GeneratedTerminalDropResult retry = authority.Generate(EnemyDeath(definition));
            Assert.That(retry.Status, Is.EqualTo(TerminalDropBindingStatus.Accepted));
            Assert.That(authority.AcceptedBatchCount, Is.EqualTo(1));
        }

        [Test]
        public void ThrowingPropSourceResolver_RejectsUncachedThenRetrySucceeds()
        {
            PropDefinition definition = PropDefinition();
            PropCatalog catalog = PropCatalog(definition);
            PropFactBatch fact = DestroyedPropFacts(catalog, definition);
            TerminalDropGenerationState authority = new TerminalDropGenerationState(
                new TerminalDropFactBridgeRegistry(new ITerminalDropFactBridge[]
                {
                    new PropDestructionTerminalDropFactBridge(
                        catalog,
                        new ThrowOncePropContextResolver())
                }),
                new FixedRunResolver(),
                Profiles(),
                new CountingGenerator());

            GeneratedTerminalDropResult first = authority.Generate(fact);
            Assert.That(first.Status, Is.EqualTo(TerminalDropBindingStatus.Rejected));
            Assert.That(first.Diagnostic, Does.Contain("prop-source-context-exception"));
            Assert.That(authority.AcceptedBatchCount, Is.EqualTo(0));

            GeneratedTerminalDropResult retry = authority.Generate(fact);
            Assert.That(retry.Status, Is.EqualTo(TerminalDropBindingStatus.Accepted));
            Assert.That(authority.AcceptedBatchCount, Is.EqualTo(1));
        }

        [Test]
        public void ThrowingRunResolver_RejectsUncachedThenRetrySucceeds()
        {
            TerminalDropGenerationState authority = PipelineAuthority(
                runResolver: new ThrowOnceRunResolver());
            AssertRetryableStage(
                authority,
                new PipelineFact("run-retry"),
                "run-context-resolution");
        }

        [Test]
        public void ThrowingProfileResolver_RejectsUncachedThenRetrySucceeds()
        {
            TerminalDropGenerationState authority = PipelineAuthority(
                profiles: new ThrowOnceProfileResolver(Profile()));
            AssertRetryableStage(
                authority,
                new PipelineFact("profile-retry"),
                "profile-resolution");
        }

        [Test]
        public void ThrowingGenExecutor_RejectsUncachedThenRetrySucceeds()
        {
            TerminalDropGenerationState authority = PipelineAuthority(
                generator: new ThrowOnceGenerator());
            AssertRetryableStage(
                authority,
                new PipelineFact("generation-retry"),
                "generation-execution");
        }

        [Test]
        public void PropSourceContextForDifferentProp_RejectsBeforeGen()
        {
            PropDefinition definition = PropDefinition();
            PropCatalog catalog = PropCatalog(definition);
            PropFactBatch fact = DestroyedPropFacts(catalog, definition);
            var generator = new CountingGenerator();
            TerminalDropGenerationState authority = new TerminalDropGenerationState(
                new TerminalDropFactBridgeRegistry(new ITerminalDropFactBridge[]
                {
                    new PropDestructionTerminalDropFactBridge(
                        catalog,
                        new MismatchedPropContextResolver())
                }),
                new FixedRunResolver(),
                Profiles(),
                generator);

            GeneratedTerminalDropResult result = authority.Generate(fact);

            Assert.That(result.Status, Is.EqualTo(TerminalDropBindingStatus.Rejected));
            Assert.That(result.RejectionCode,
                Is.EqualTo(TerminalDropRejectionCode.MissingSourceContext));
            Assert.That(result.Diagnostic, Does.Contain("prop-source-context-entity-mismatch"));
            Assert.That(generator.CallCount, Is.EqualTo(0));
            Assert.That(authority.AcceptedBatchCount, Is.EqualTo(0));
        }

        private static void AssertRetryableStage(
            TerminalDropGenerationState authority,
            object fact,
            string stage)
        {
            GeneratedTerminalDropResult first = authority.Generate(fact);
            Assert.That(first.Status, Is.EqualTo(TerminalDropBindingStatus.Rejected));
            Assert.That(first.Diagnostic, Does.Contain(stage));
            Assert.That(authority.AcceptedBatchCount, Is.EqualTo(0));

            GeneratedTerminalDropResult retry = authority.Generate(fact);
            Assert.That(retry.Status, Is.EqualTo(TerminalDropBindingStatus.Accepted));
            Assert.That(authority.AcceptedBatchCount, Is.EqualTo(1));
        }

        private static TerminalDropGenerationState PipelineAuthority(
            ITerminalDropFactBridge adapter = null,
            ITerminalDropRunContextResolver runResolver = null,
            IRewardProfileResolver profiles = null,
            IRewardGenerationExecutor generator = null)
        {
            return new TerminalDropGenerationState(
                new TerminalDropFactBridgeRegistry(new ITerminalDropFactBridge[]
                {
                    adapter ?? new PipelineBridge()
                }),
                runResolver ?? new FixedRunResolver(),
                profiles ?? Profiles(),
                generator ?? new CountingGenerator());
        }

        private static TerminalDropGenerationState EnemyAuthority(
            EnemyDefinition definition,
            IEnemyTerminalSourceContextResolver contexts,
            IRewardGenerationExecutor generator)
        {
            EnemyCatalog catalog = new EnemyCatalog(
                EnemyCatalog.SupportedSchemaVersion,
                Id("enemy-content", "review-blockers"),
                new[] { definition });
            return new TerminalDropGenerationState(
                new TerminalDropFactBridgeRegistry(new ITerminalDropFactBridge[]
                {
                    new ContextResolvedEnemyDeathTerminalDropFactBridge(
                        catalog,
                        contexts)
                }),
                new FixedRunResolver(),
                Profiles(),
                generator);
        }

        private static RewardProfileCatalogResolver Profiles()
        {
            return new RewardProfileCatalogResolver(new[] { Profile() });
        }

        private static RewardProfile Profile()
        {
            return RewardProfile.Create(
                ProfileId,
                new[]
                {
                    RewardGrantSpecification.CreateFixed(
                        Id("grant", "review-money"),
                        RewardGrantKind.Money,
                        Id("currency", "credits"),
                        5L)
                },
                Array.Empty<IndependentRewardRoll>(),
                Array.Empty<ExclusiveRewardGroup>());
        }

        private static EnemyDefinition EnemyDefinition()
        {
            return new EnemyDefinition(
                Id("enemy", "review-blocker"),
                Id("presentation", "review-blocker"),
                10d,
                new EnemyLevelScalingProfile(1, 100, 0d, 1d),
                Id("faction", "enemy"),
                10d,
                360d,
                Id("movement", "fixture"),
                Id("decision", "fixture"),
                Array.Empty<EnemyAttackCapabilityDescriptor>(),
                Id("experience-profile", "fixture"),
                ProfileId,
                EnemyCatalogRoomClearRole.RequiredEnemy,
                Array.Empty<StableId>());
        }

        private static EnemyDeathFact EnemyDeath(EnemyDefinition definition)
        {
            var identity = new EnemyLiveIdentity(
                Id("enemy-entity", "review-blocker"),
                Id("run-participant", "review-blocker"),
                RunId,
                Id("room-runtime", "review-blocker"),
                Id("room", "review-blocker"),
                Id("placement", "review-blocker"));
            return new EnemyDeathFact(
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

        private static PropDefinition PropDefinition()
        {
            return new PropDefinition(
                Id("prop", "review-blocker"),
                Id("presentation", "review-blocker-prop"),
                new[]
                {
                    PropCapabilities.Collision(true),
                    PropCapabilities.HealthBased(10d),
                    PropCapabilities.DamageBehavior(
                        PropDamageAlignment.Hostile,
                        Id("damage-policy", "player-normal")),
                    PropCapabilities.DropOnDestroy(ProfileId)
                });
        }

        private static PropCatalog PropCatalog(PropDefinition definition)
        {
            return new PropCatalog(
                PropCapabilityRegistry.CreateBuiltIns(),
                new[] { definition });
        }

        private static PropFactBatch DestroyedPropFacts(
            PropCatalog catalog,
            PropDefinition definition)
        {
            PropLiveCreationResult created = new PropLiveFactory().Create(
                catalog,
                new PropPlacement(
                    PlacedObjectIdentity.CreateAuthored(Id("placement", "review-prop")),
                    definition.DefinitionId),
                new AlwaysAllowPropDamage());
            Assert.That(created.IsCreated, Is.True);
            PropDamageResult destroyed = created.Runtime.ApplyDamage(
                new PropDamageCommand(
                    Id("operation", "destroy-review-prop"),
                    PlayerParticipantId,
                    Id("faction", "player"),
                    Id("damage", "kinetic"),
                    10d));
            Assert.That(destroyed.Status, Is.EqualTo(PropDamageStatus.Destroyed));
            return destroyed.Facts;
        }

        private static StableId Id(string scope, string value)
        {
            return StableId.Create(scope, value);
        }
    }
}
#endif
