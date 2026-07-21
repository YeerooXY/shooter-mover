#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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
    public sealed class TerminalDropGenerationAuthorityV1Tests
    {
        private static readonly StableId RunId = Id("run", "drop-binding-tests");
        private static readonly StableId PlayerParticipantId = Id("participant", "player-one");
        private static readonly StableId PlayerEntityId = Id("entity", "player-one");
        private static readonly StableId MoneyProfileId = Id("drop-profile", "money");
        private static readonly StableId MultiProfileId = Id("drop-profile", "multi");
        private static readonly StableId StrongboxProfileId = Id("drop-profile", "strongbox");

        private sealed class FixtureFact
        {
            public FixtureFact(
                StableId eventId,
                StableId sourceId,
                StableId placementId,
                long sourceGeneration,
                StableId profileId,
                StableId participantId,
                string immutableToken)
            {
                EventId = eventId;
                SourceId = sourceId;
                PlacementId = placementId;
                SourceGeneration = sourceGeneration;
                ProfileId = profileId;
                ParticipantId = participantId;
                ImmutableToken = immutableToken;
            }

            public StableId EventId { get; }
            public StableId SourceId { get; }
            public StableId PlacementId { get; }
            public long SourceGeneration { get; }
            public StableId ProfileId { get; }
            public StableId ParticipantId { get; }
            public string ImmutableToken { get; }
        }

        private sealed class FixtureAdapter : ITerminalDropFactAdapterV1
        {
            private readonly StableId kindId;
            private readonly StableId definitionId;

            public FixtureAdapter(string kindValue = "fixture")
            {
                kindId = Id("terminal-drop-fact", kindValue);
                definitionId = Id("definition", "fixture-source");
            }

            public StableId FactKindStableId { get { return kindId; } }
            public Type FactType { get { return typeof(FixtureFact); } }

            public TerminalDropAdaptationResultV1 Adapt(object terminalFact)
            {
                FixtureFact fact = terminalFact as FixtureFact;
                if (fact == null)
                {
                    return TerminalDropAdaptationResultV1.Rejected(
                        TerminalDropRejectionCodeV1.InvalidTerminalFact,
                        "fixture-type-mismatch");
                }

                return TerminalDropAdaptationResultV1.Accepted(
                    new TerminalDropSourceFactV1(
                        kindId,
                        fact.EventId,
                        Id("trigger", fact.EventId.Value + "-trigger"),
                        RunId,
                        1L,
                        fact.SourceId,
                        fact.PlacementId,
                        fact.SourceGeneration,
                        definitionId,
                        fact.ParticipantId,
                        PlayerEntityId,
                        Id("damage", "kinetic"),
                        fact.ProfileId,
                        "fixture-source-context:" + fact.PlacementId,
                        "fixture-definition-fingerprint",
                        "fixture-upstream:" + fact.ImmutableToken));
            }
        }

        private sealed class AlternateFact { }

        private sealed class AlternateAdapter : ITerminalDropFactAdapterV1
        {
            public StableId FactKindStableId { get { return Id("terminal-drop-fact", "alternate"); } }
            public Type FactType { get { return typeof(AlternateFact); } }

            public TerminalDropAdaptationResultV1 Adapt(object terminalFact)
            {
                return TerminalDropAdaptationResultV1.Rejected(
                    TerminalDropRejectionCodeV1.InvalidTerminalFact,
                    "unused-alternate-adapter");
            }
        }

        private sealed class FixedRunContextResolver : ITerminalDropRunContextResolverV1
        {
            public long CurrentGeneration = 1L;
            public bool RunExists = true;
            public bool Ended;

            public bool TryResolve(
                StableId runStableId,
                long expectedLifecycleGeneration,
                out TerminalDropRunGenerationContextV1 context,
                out TerminalDropRejectionCodeV1 rejectionCode,
                out string diagnostic)
            {
                context = null;
                rejectionCode = TerminalDropRejectionCodeV1.None;
                diagnostic = string.Empty;

                if (!RunExists || runStableId != RunId)
                {
                    rejectionCode = TerminalDropRejectionCodeV1.MissingRun;
                    diagnostic = "fixture-run-missing";
                    return false;
                }
                if (expectedLifecycleGeneration != CurrentGeneration)
                {
                    rejectionCode = TerminalDropRejectionCodeV1.WrongRunLifecycle;
                    diagnostic = "fixture-run-lifecycle-mismatch";
                    return false;
                }
                if (Ended)
                {
                    rejectionCode = TerminalDropRejectionCodeV1.RunEnded;
                    diagnostic = "fixture-run-ended";
                    return false;
                }

                context = new TerminalDropRunGenerationContextV1(
                    RunId,
                    CurrentGeneration,
                    0x123456789abcdef0UL,
                    1,
                    ProgressionContext.Create(
                        10,
                        3,
                        Id("difficulty", "normal"),
                        1),
                    "fixture-event-context");
                return true;
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

        private sealed class FailOnceGenerator : IRewardGenerationExecutorV1
        {
            private readonly IRewardGenerationExecutorV1 inner =
                new ExistingRewardGenerationExecutorV1(new RewardGenerationServiceV1());

            public int CallCount { get; private set; }

            public RewardGenerationResultEnvelopeV1 Generate(RewardGenerationRequestV1 request)
            {
                CallCount++;
                return CallCount == 1 ? null : inner.Generate(request);
            }
        }

        private sealed class AlwaysAllowPropDamage : IPropDamageEligibilityPolicyV1
        {
            public bool CanDamage(PropDamageEligibilityContextV1 context)
            {
                return true;
            }
        }

        private sealed class PropSourceContextResolver : IPropTerminalSourceContextResolverV1
        {
            private readonly Dictionary<StableId, StableId> placements =
                new Dictionary<StableId, StableId>();

            public void Register(StableId participantId, StableId placementId)
            {
                placements[participantId] = placementId;
            }

            public bool TryResolve(
                PropTerminalFactV1 terminalFact,
                out PropTerminalSourceContextV1 context,
                out string diagnostic)
            {
                StableId placement;
                if (terminalFact == null
                    || !placements.TryGetValue(terminalFact.PropParticipantId, out placement))
                {
                    context = null;
                    diagnostic = "fixture-prop-context-missing";
                    return false;
                }

                context = new PropTerminalSourceContextV1(
                    RunId,
                    1L,
                    terminalFact.PropParticipantId,
                    placement,
                    1L,
                    "fixture-prop-context:" + placement);
                diagnostic = string.Empty;
                return true;
            }
        }

        [Test]
        public void OneAttributedEnemyDeath_GeneratesExactlyOneBatch()
        {
            EnemyDefinitionV1 definition = EnemyDefinition("fixture-enemy", MoneyProfileId);
            CountingGenerator generator = new CountingGenerator();
            TerminalDropGenerationAuthorityV1 authority = Authority(
                new EnemyDeathTerminalDropFactAdapterV1(EnemyCatalog(definition)),
                Profiles(MoneyProfile()),
                generator);

            GeneratedTerminalDropResultV1 result = authority.Generate(
                EnemyDeath(definition, "enemy-death-one", "enemy-placement-one", 1L));

            Assert.That(result.Status, Is.EqualTo(TerminalDropBindingStatusV1.Accepted));
            Assert.That(result.GeneratedRewards.Count, Is.EqualTo(1));
            Assert.That(generator.CallCount, Is.EqualTo(1));
            Assert.That(authority.AcceptedBatchCount, Is.EqualTo(1));
        }

        [Test]
        public void OneEligiblePropDestruction_GeneratesExactlyOneBatch()
        {
            PropDefinitionV1 definition = PropDefinition("fixture-prop", MoneyProfileId);
            PropCatalogV1 catalog = PropCatalog(definition);
            PropSourceContextResolver contexts = new PropSourceContextResolver();
            PropRuntimeV1 runtime = CreatePropRuntime(catalog, definition, "prop-placement-one");
            contexts.Register(runtime.ParticipantId, Id("placement", "prop-placement-one"));
            PropDamageResultV1 destroyed = Destroy(runtime, "destroy-prop-one");
            CountingGenerator generator = new CountingGenerator();
            TerminalDropGenerationAuthorityV1 authority = Authority(
                new PropDestructionTerminalDropFactAdapterV1(catalog, contexts),
                Profiles(MoneyProfile()),
                generator);

            GeneratedTerminalDropResultV1 result = authority.Generate(destroyed.Facts);

            Assert.That(result.Status, Is.EqualTo(TerminalDropBindingStatusV1.Accepted));
            Assert.That(result.SourceFact.SourcePlacementStableId,
                Is.EqualTo(Id("placement", "prop-placement-one")));
            Assert.That(generator.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void EnemyWithoutDropProfile_ProducesDeterministicNoDrop()
        {
            EnemyDefinitionV1 definition = EnemyDefinition("enemy-no-drop", null);
            TerminalDropGenerationAuthorityV1 authority = Authority(
                new EnemyDeathTerminalDropFactAdapterV1(EnemyCatalog(definition)),
                Profiles(),
                new CountingGenerator());

            GeneratedTerminalDropResultV1 result = authority.Generate(
                EnemyDeath(definition, "enemy-death-no-drop", "enemy-placement-no-drop", 1L));

            Assert.That(result.Status, Is.EqualTo(TerminalDropBindingStatusV1.ExplicitNoDrop));
            Assert.That(result.GeneratedRewards, Is.Empty);
            Assert.That(result.ResolvedDropProfileStableId, Is.Not.Null);
        }

        [Test]
        public void PropWithoutDropProfile_ProducesDeterministicNoDrop()
        {
            PropDefinitionV1 definition = PropDefinition("prop-no-drop", null);
            PropCatalogV1 catalog = PropCatalog(definition);
            PropSourceContextResolver contexts = new PropSourceContextResolver();
            PropRuntimeV1 runtime = CreatePropRuntime(catalog, definition, "prop-placement-no-drop");
            contexts.Register(runtime.ParticipantId, Id("placement", "prop-placement-no-drop"));
            PropDamageResultV1 destroyed = Destroy(runtime, "destroy-prop-no-drop");
            TerminalDropGenerationAuthorityV1 authority = Authority(
                new PropDestructionTerminalDropFactAdapterV1(catalog, contexts),
                Profiles(),
                new CountingGenerator());

            GeneratedTerminalDropResultV1 result = authority.Generate(destroyed.Facts);

            Assert.That(result.Status, Is.EqualTo(TerminalDropBindingStatusV1.ExplicitNoDrop));
            Assert.That(result.GeneratedRewards, Is.Empty);
        }

        [Test]
        public void ExactReplay_DoesNotGenerateSecondBatch()
        {
            CountingGenerator generator = new CountingGenerator();
            TerminalDropGenerationAuthorityV1 authority = FixtureAuthority(
                Profiles(StrongboxProfile()),
                generator);
            FixtureFact fact = Fixture("replay", StrongboxProfileId);

            authority.Generate(fact);
            GeneratedTerminalDropResultV1 replay = authority.Generate(fact);

            Assert.That(replay.Status, Is.EqualTo(TerminalDropBindingStatusV1.ExactReplay));
            Assert.That(generator.CallCount, Is.EqualTo(1));
            Assert.That(authority.AcceptedBatchCount, Is.EqualTo(1));
        }

        [Test]
        public void ExactReplay_PreservesOperationChildrenAndFingerprint()
        {
            TerminalDropGenerationAuthorityV1 authority = FixtureAuthority(
                Profiles(StrongboxProfile()));
            FixtureFact fact = Fixture("replay-identities", StrongboxProfileId);

            GeneratedTerminalDropResultV1 first = authority.Generate(fact);
            GeneratedTerminalDropResultV1 replay = authority.Generate(fact);

            Assert.That(replay.OperationRequest.SourceOperationStableId,
                Is.EqualTo(first.OperationRequest.SourceOperationStableId));
            Assert.That(replay.GeneratedRewards[0].RewardInstanceStableId,
                Is.EqualTo(first.GeneratedRewards[0].RewardInstanceStableId));
            Assert.That(replay.Fingerprint, Is.EqualTo(first.Fingerprint));
        }

        [Test]
        public void ConflictingReuseOfTerminalEvent_RejectsWithoutMutation()
        {
            CountingGenerator generator = new CountingGenerator();
            TerminalDropGenerationAuthorityV1 authority = FixtureAuthority(
                Profiles(MoneyProfile()),
                generator);

            GeneratedTerminalDropResultV1 first = authority.Generate(
                Fixture("conflict", MoneyProfileId, immutableToken: "first"));
            GeneratedTerminalDropResultV1 conflict = authority.Generate(
                Fixture("conflict", MoneyProfileId, immutableToken: "changed"));

            Assert.That(first.IsAccepted, Is.True);
            Assert.That(conflict.Status,
                Is.EqualTo(TerminalDropBindingStatusV1.ConflictingDuplicate));
            Assert.That(generator.CallCount, Is.EqualTo(1));
            Assert.That(authority.AcceptedBatchCount, Is.EqualTo(1));
        }

        [Test]
        public void DistinctEventsFromSameDefinition_ProduceDistinctOperations()
        {
            TerminalDropGenerationAuthorityV1 authority = FixtureAuthority(Profiles(MoneyProfile()));

            GeneratedTerminalDropResultV1 first = authority.Generate(
                Fixture("distinct-event-a", MoneyProfileId));
            GeneratedTerminalDropResultV1 second = authority.Generate(
                Fixture("distinct-event-b", MoneyProfileId));

            Assert.That(first.OperationRequest.SourceOperationStableId,
                Is.Not.EqualTo(second.OperationRequest.SourceOperationStableId));
        }

        [Test]
        public void SameDefinitionAtDifferentPlacements_RemainsDistinct()
        {
            TerminalDropGenerationAuthorityV1 authority = FixtureAuthority(Profiles(MoneyProfile()));

            GeneratedTerminalDropResultV1 first = authority.Generate(
                Fixture("placement-event-a", MoneyProfileId, placementValue: "placement-a"));
            GeneratedTerminalDropResultV1 second = authority.Generate(
                Fixture("placement-event-b", MoneyProfileId, placementValue: "placement-b"));

            Assert.That(first.SourceFact.SourcePlacementStableId,
                Is.Not.EqualTo(second.SourceFact.SourcePlacementStableId));
            Assert.That(first.OperationRequest.SourceOperationStableId,
                Is.Not.EqualTo(second.OperationRequest.SourceOperationStableId));
        }

        [Test]
        public void DifferentSourceLifecycleGenerations_CannotShareOperation()
        {
            TerminalDropGenerationAuthorityV1 authority = FixtureAuthority(Profiles(MoneyProfile()));

            GeneratedTerminalDropResultV1 first = authority.Generate(
                Fixture("generation-event-a", MoneyProfileId, sourceGeneration: 1L));
            GeneratedTerminalDropResultV1 second = authority.Generate(
                Fixture("generation-event-b", MoneyProfileId, sourceGeneration: 2L));

            Assert.That(first.OperationRequest.SourceOperationStableId,
                Is.Not.EqualTo(second.OperationRequest.SourceOperationStableId));
        }

        [Test]
        public void StaleRunLifecycle_RejectsSafely()
        {
            FixedRunContextResolver run = new FixedRunContextResolver { CurrentGeneration = 2L };
            CountingGenerator generator = new CountingGenerator();
            TerminalDropGenerationAuthorityV1 authority = FixtureAuthority(
                Profiles(MoneyProfile()),
                generator,
                run);

            GeneratedTerminalDropResultV1 result = authority.Generate(
                Fixture("stale-run", MoneyProfileId));

            Assert.That(result.RejectionCode,
                Is.EqualTo(TerminalDropRejectionCodeV1.WrongRunLifecycle));
            Assert.That(generator.CallCount, Is.EqualTo(0));
            Assert.That(authority.AcceptedBatchCount, Is.EqualTo(0));
        }

        [Test]
        public void MissingDefinitionResolution_Rejects()
        {
            EnemyDefinitionV1 present = EnemyDefinition("enemy-present", MoneyProfileId);
            EnemyDefinitionV1 missing = EnemyDefinition("enemy-missing", MoneyProfileId);
            TerminalDropGenerationAuthorityV1 authority = Authority(
                new EnemyDeathTerminalDropFactAdapterV1(EnemyCatalog(present)),
                Profiles(MoneyProfile()),
                new CountingGenerator());

            GeneratedTerminalDropResultV1 result = authority.Generate(
                EnemyDeath(missing, "enemy-death-missing", "enemy-placement-missing", 1L));

            Assert.That(result.RejectionCode,
                Is.EqualTo(TerminalDropRejectionCodeV1.MissingDefinition));
        }

        [Test]
        public void MissingDropProfileResolution_Rejects()
        {
            TerminalDropGenerationAuthorityV1 authority = FixtureAuthority(Profiles());

            GeneratedTerminalDropResultV1 result = authority.Generate(
                Fixture("missing-profile", Id("drop-profile", "not-registered")));

            Assert.That(result.RejectionCode,
                Is.EqualTo(TerminalDropRejectionCodeV1.MissingDropProfile));
            Assert.That(result.Diagnostic, Does.Contain("drop-profile.not-registered"));
        }

        [Test]
        public void UnsupportedFactType_FailsClosed()
        {
            TerminalDropGenerationAuthorityV1 authority = FixtureAuthority(Profiles());

            GeneratedTerminalDropResultV1 result = authority.Generate(new object());

            Assert.That(result.RejectionCode,
                Is.EqualTo(TerminalDropRejectionCodeV1.UnsupportedFactType));
            Assert.That(authority.AcceptedBatchCount, Is.EqualTo(0));
        }

        [Test]
        public void UnattributedFact_DoesNotAwardArbitraryParticipant()
        {
            CountingGenerator generator = new CountingGenerator();
            TerminalDropGenerationAuthorityV1 authority = FixtureAuthority(
                Profiles(MoneyProfile()),
                generator);

            GeneratedTerminalDropResultV1 result = authority.Generate(
                Fixture(
                    "unattributed",
                    MoneyProfileId,
                    participantId: null,
                    immutableToken: "unattributed"));

            Assert.That(result.RejectionCode,
                Is.EqualTo(TerminalDropRejectionCodeV1.UnattributedTerminalFact));
            Assert.That(generator.CallCount, Is.EqualTo(0));
        }

        [Test]
        public void RegistrationOrder_DoesNotAffectFingerprints()
        {
            ITerminalDropFactAdapterV1 first = new FixtureAdapter("fixture");
            ITerminalDropFactAdapterV1 second = new AlternateAdapter();
            TerminalDropFactAdapterRegistryV1 ordered =
                new TerminalDropFactAdapterRegistryV1(new[] { first, second });
            TerminalDropFactAdapterRegistryV1 reversed =
                new TerminalDropFactAdapterRegistryV1(new[] { second, first });
            RewardProfileCatalogResolverV1 profilesOrdered =
                Profiles(MoneyProfile(), StrongboxProfile());
            RewardProfileCatalogResolverV1 profilesReversed =
                Profiles(StrongboxProfile(), MoneyProfile());

            Assert.That(ordered.Fingerprint, Is.EqualTo(reversed.Fingerprint));
            Assert.That(profilesOrdered.Fingerprint, Is.EqualTo(profilesReversed.Fingerprint));

            FixtureFact fact = Fixture("registration-order", MoneyProfileId);
            GeneratedTerminalDropResultV1 firstResult = FixtureAuthority(profilesOrdered).Generate(fact);
            GeneratedTerminalDropResultV1 secondResult = FixtureAuthority(profilesReversed).Generate(fact);
            Assert.That(firstResult.Fingerprint, Is.EqualTo(secondResult.Fingerprint));
        }

        [Test]
        public void MultiRewardBatch_HasDeterministicChildOrdering()
        {
            TerminalDropGenerationAuthorityV1 authority = FixtureAuthority(Profiles(MultiProfile()));

            GeneratedTerminalDropResultV1 result = authority.Generate(
                Fixture("multi-reward", MultiProfileId));

            Assert.That(result.GeneratedRewards.Count, Is.EqualTo(2));
            Assert.That(result.GeneratedRewards[0].SourceGrantStableId,
                Is.EqualTo(Id("grant", "a-money")));
            Assert.That(result.GeneratedRewards[1].SourceGrantStableId,
                Is.EqualTo(Id("grant", "z-scrap")));
        }

        [Test]
        public void GeneratedStrongbox_HasOneExactStableInstanceIdentity()
        {
            TerminalDropGenerationAuthorityV1 authority = FixtureAuthority(
                Profiles(StrongboxProfile()));

            GeneratedTerminalDropResultV1 result = authority.Generate(
                Fixture("strongbox-one", StrongboxProfileId));

            Assert.That(result.GeneratedRewards.Count, Is.EqualTo(1));
            Assert.That(result.GeneratedRewards[0].Kind,
                Is.EqualTo(RewardGrantKindV1.Strongbox));
            Assert.That(result.GeneratedRewards[0].Quantity, Is.EqualTo(1L));
            Assert.That(result.GeneratedRewards[0].RewardInstanceStableId, Is.Not.Null);
        }

        [Test]
        public void SameTierStrongboxesFromDifferentFacts_HaveDistinctInstances()
        {
            TerminalDropGenerationAuthorityV1 authority = FixtureAuthority(
                Profiles(StrongboxProfile()));

            GeneratedTerminalDropResultV1 first = authority.Generate(
                Fixture("strongbox-a", StrongboxProfileId));
            GeneratedTerminalDropResultV1 second = authority.Generate(
                Fixture("strongbox-b", StrongboxProfileId));

            Assert.That(first.GeneratedRewards[0].ContentStableId,
                Is.EqualTo(second.GeneratedRewards[0].ContentStableId));
            Assert.That(first.GeneratedRewards[0].RewardInstanceStableId,
                Is.Not.EqualTo(second.GeneratedRewards[0].RewardInstanceStableId));
        }

        [Test]
        public void GenerationFailure_EmitsNoPartialAcceptedBatch()
        {
            FailOnceGenerator generator = new FailOnceGenerator();
            TerminalDropGenerationAuthorityV1 authority = FixtureAuthority(
                Profiles(MultiProfile()),
                generator);

            GeneratedTerminalDropResultV1 result = authority.Generate(
                Fixture("generation-failure", MultiProfileId));

            Assert.That(result.Status, Is.EqualTo(TerminalDropBindingStatusV1.Rejected));
            Assert.That(result.RejectionCode,
                Is.EqualTo(TerminalDropRejectionCodeV1.GenerationFailed));
            Assert.That(result.GeneratedBatch, Is.Null);
            Assert.That(result.GeneratedRewards, Is.Empty);
            Assert.That(authority.AcceptedBatchCount, Is.EqualTo(0));
        }

        [Test]
        public void ExactRetryAfterRetryableFailure_CanSucceed()
        {
            FailOnceGenerator generator = new FailOnceGenerator();
            TerminalDropGenerationAuthorityV1 authority = FixtureAuthority(
                Profiles(MoneyProfile()),
                generator);
            FixtureFact fact = Fixture("retryable-failure", MoneyProfileId);

            GeneratedTerminalDropResultV1 failed = authority.Generate(fact);
            GeneratedTerminalDropResultV1 retry = authority.Generate(fact);

            Assert.That(failed.RejectionCode,
                Is.EqualTo(TerminalDropRejectionCodeV1.GenerationFailed));
            Assert.That(retry.Status, Is.EqualTo(TerminalDropBindingStatusV1.Accepted));
            Assert.That(generator.CallCount, Is.EqualTo(2));
            Assert.That(authority.AcceptedBatchCount, Is.EqualTo(1));
        }

        [Test]
        public void Generation_DoesNotMutatePermanentAuthorities()
        {
            int permanentMutationCalls = 0;
            TerminalDropGenerationAuthorityV1 authority = FixtureAuthority(Profiles(MoneyProfile()));

            GeneratedTerminalDropResultV1 result = authority.Generate(
                Fixture("no-permanent-mutation", MoneyProfileId));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(permanentMutationCalls, Is.EqualTo(0));
            Assert.That(result.GeneratedRewards.Count, Is.EqualTo(1));
        }

        [Test]
        public void NewEnemyUsingExistingProfile_NeedsNoSharedConsumerEdit()
        {
            EnemyDefinitionV1 first = EnemyDefinition("enemy-fixture-one", MoneyProfileId);
            EnemyDefinitionV1 second = EnemyDefinition("enemy-fixture-two", MoneyProfileId);
            EnemyDeathTerminalDropFactAdapterV1 sharedAdapter =
                new EnemyDeathTerminalDropFactAdapterV1(EnemyCatalog(first, second));
            TerminalDropGenerationAuthorityV1 authority = Authority(
                sharedAdapter,
                Profiles(MoneyProfile()),
                new CountingGenerator());

            GeneratedTerminalDropResultV1 result = authority.Generate(
                EnemyDeath(second, "enemy-death-two", "enemy-placement-two", 1L));

            Assert.That(result.Status, Is.EqualTo(TerminalDropBindingStatusV1.Accepted));
            Assert.That(result.SourceFact.SourceDefinitionStableId,
                Is.EqualTo(second.DefinitionId));
        }

        [Test]
        public void NewPropUsingExistingProfile_NeedsNoSharedConsumerEdit()
        {
            PropDefinitionV1 first = PropDefinition("prop-fixture-one", MoneyProfileId);
            PropDefinitionV1 second = PropDefinition("prop-fixture-two", MoneyProfileId);
            PropCatalogV1 catalog = PropCatalog(first, second);
            PropSourceContextResolver contexts = new PropSourceContextResolver();
            PropRuntimeV1 runtime = CreatePropRuntime(catalog, second, "prop-placement-two");
            contexts.Register(runtime.ParticipantId, Id("placement", "prop-placement-two"));
            PropDestructionTerminalDropFactAdapterV1 sharedAdapter =
                new PropDestructionTerminalDropFactAdapterV1(catalog, contexts);
            TerminalDropGenerationAuthorityV1 authority = Authority(
                sharedAdapter,
                Profiles(MoneyProfile()),
                new CountingGenerator());

            GeneratedTerminalDropResultV1 result = authority.Generate(
                Destroy(runtime, "destroy-prop-two").Facts);

            Assert.That(result.Status, Is.EqualTo(TerminalDropBindingStatusV1.Accepted));
            Assert.That(result.SourceFact.SourceDefinitionStableId,
                Is.EqualTo(second.DefinitionId));
        }

        [Test]
        public void RealGenericEnemyDeathFact_ReachesExistingDropGenBoundary()
        {
            EnemyDefinitionV1 definition = EnemyDefinition("enemy-integration", MultiProfileId);
            CountingGenerator generator = new CountingGenerator();
            TerminalDropGenerationAuthorityV1 authority = Authority(
                new EnemyDeathTerminalDropFactAdapterV1(EnemyCatalog(definition)),
                Profiles(MultiProfile()),
                generator);
            EnemyDeathFactV1 fact = EnemyDeath(
                definition,
                "enemy-death-integration",
                "enemy-placement-integration",
                1L);

            GeneratedTerminalDropResultV1 result = authority.Generate(fact);

            Assert.That(result.GeneratedBatch, Is.Not.Null);
            Assert.That(result.GeneratedRewards.Count, Is.EqualTo(2));
            Assert.That(generator.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void RealGenericPropTerminalFact_ReachesSameBoundary()
        {
            PropDefinitionV1 definition = PropDefinition("prop-integration", MultiProfileId);
            PropCatalogV1 catalog = PropCatalog(definition);
            PropSourceContextResolver contexts = new PropSourceContextResolver();
            PropRuntimeV1 runtime = CreatePropRuntime(catalog, definition, "prop-placement-integration");
            contexts.Register(runtime.ParticipantId, Id("placement", "prop-placement-integration"));
            PropFactBatchV1 facts = Destroy(runtime, "destroy-prop-integration").Facts;
            TerminalDropGenerationAuthorityV1 authority = Authority(
                new PropDestructionTerminalDropFactAdapterV1(catalog, contexts),
                Profiles(MultiProfile()),
                new CountingGenerator());

            GeneratedTerminalDropResultV1 result = authority.Generate(facts);

            Assert.That(result.GeneratedBatch, Is.Not.Null);
            Assert.That(result.GeneratedRewards.Count, Is.EqualTo(2));
        }

        [Test]
        public void DuplicateDeliveryThroughTwoRoutes_CannotDuplicateBatch()
        {
            EnemyDefinitionV1 definition = EnemyDefinition("enemy-duplicate-route", StrongboxProfileId);
            CountingGenerator generator = new CountingGenerator();
            TerminalDropGenerationAuthorityV1 authority = Authority(
                new EnemyDeathTerminalDropFactAdapterV1(EnemyCatalog(definition)),
                Profiles(StrongboxProfile()),
                generator);
            EnemyDeathFactV1 fact = EnemyDeath(
                definition,
                "enemy-death-duplicate-route",
                "enemy-placement-duplicate-route",
                1L);

            GeneratedTerminalDropResultV1 firstRoute = authority.Generate(fact);
            GeneratedTerminalDropResultV1 secondRoute = authority.Generate(fact);

            Assert.That(firstRoute.IsAccepted, Is.True);
            Assert.That(secondRoute.Status,
                Is.EqualTo(TerminalDropBindingStatusV1.ExactReplay));
            Assert.That(generator.CallCount, Is.EqualTo(1));
            Assert.That(secondRoute.GeneratedRewards[0].RewardInstanceStableId,
                Is.EqualTo(firstRoute.GeneratedRewards[0].RewardInstanceStableId));
        }

        [Test]
        public void ImmutableBatch_IsPickupReadyWithoutRerolling()
        {
            CountingGenerator generator = new CountingGenerator();
            TerminalDropGenerationAuthorityV1 authority = FixtureAuthority(
                Profiles(StrongboxProfile()),
                generator);
            FixtureFact fact = Fixture("pickup-ready", StrongboxProfileId);

            GeneratedTerminalDropResultV1 generated = authority.Generate(fact);
            StableId pendingPickupIdentity =
                generated.GeneratedRewards[0].RewardInstanceStableId;
            GeneratedTerminalDropResultV1 recovered = authority.Generate(fact);

            Assert.That(recovered.GeneratedRewards[0].RewardInstanceStableId,
                Is.EqualTo(pendingPickupIdentity));
            Assert.That(recovered.GenerationSeed, Is.EqualTo(generated.GenerationSeed));
            Assert.That(generator.CallCount, Is.EqualTo(1));
        }

        private static TerminalDropGenerationAuthorityV1 FixtureAuthority(
            RewardProfileCatalogResolverV1 profiles,
            IRewardGenerationExecutorV1 generator = null,
            FixedRunContextResolver run = null)
        {
            return Authority(
                new FixtureAdapter(),
                profiles,
                generator ?? new CountingGenerator(),
                run ?? new FixedRunContextResolver());
        }

        private static TerminalDropGenerationAuthorityV1 Authority(
            ITerminalDropFactAdapterV1 adapter,
            RewardProfileCatalogResolverV1 profiles,
            IRewardGenerationExecutorV1 generator,
            ITerminalDropRunContextResolverV1 run = null)
        {
            return new TerminalDropGenerationAuthorityV1(
                new TerminalDropFactAdapterRegistryV1(new[] { adapter }),
                run ?? new FixedRunContextResolver(),
                profiles,
                generator);
        }

        private static FixtureFact Fixture(
            string eventValue,
            StableId profileId,
            string sourceValue = "fixture-source",
            string placementValue = "fixture-placement",
            long sourceGeneration = 1L,
            StableId participantId = null,
            string immutableToken = "fixture")
        {
            return new FixtureFact(
                Id("terminal", eventValue),
                Id("entity", sourceValue),
                Id("placement", placementValue),
                sourceGeneration,
                profileId,
                participantId == null && immutableToken != "unattributed"
                    ? PlayerParticipantId
                    : participantId,
                immutableToken);
        }

        private static RewardProfileCatalogResolverV1 Profiles(
            params RewardProfileV1[] profiles)
        {
            return new RewardProfileCatalogResolverV1(profiles);
        }

        private static RewardProfileV1 MoneyProfile()
        {
            return RewardProfileV1.Create(
                MoneyProfileId,
                new[]
                {
                    RewardGrantSpecificationV1.CreateFixed(
                        Id("grant", "money"),
                        RewardGrantKindV1.Money,
                        Id("currency", "credits"),
                        25L)
                },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());
        }

        private static RewardProfileV1 MultiProfile()
        {
            return RewardProfileV1.Create(
                MultiProfileId,
                new[]
                {
                    RewardGrantSpecificationV1.CreateFixed(
                        Id("grant", "z-scrap"),
                        RewardGrantKindV1.Scrap,
                        Id("currency", "scrap"),
                        2L),
                    RewardGrantSpecificationV1.CreateFixed(
                        Id("grant", "a-money"),
                        RewardGrantKindV1.Money,
                        Id("currency", "credits"),
                        10L)
                },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());
        }

        private static RewardProfileV1 StrongboxProfile()
        {
            return RewardProfileV1.Create(
                StrongboxProfileId,
                new[]
                {
                    RewardGrantSpecificationV1.CreateFixed(
                        Id("grant", "strongbox"),
                        RewardGrantKindV1.Strongbox,
                        Id("strongbox", "tier-emerald"),
                        1L)
                },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());
        }

        private static EnemyDefinitionV1 EnemyDefinition(
            string value,
            StableId dropProfileId)
        {
            return new EnemyDefinitionV1(
                Id("enemy", value),
                Id("presentation", value),
                10d,
                new EnemyLevelScalingProfileV1(1, 100, 0d, 1d),
                Id("faction", "enemy"),
                10d,
                360d,
                Id("movement", "fixture"),
                Id("decision", "fixture"),
                Array.Empty<EnemyAttackCapabilityDescriptorV1>(),
                Id("experience-profile", "fixture"),
                dropProfileId,
                EnemyCatalogRoomClearRoleV1.RequiredEnemy,
                Array.Empty<StableId>());
        }

        private static EnemyCatalogV1 EnemyCatalog(params EnemyDefinitionV1[] definitions)
        {
            return new EnemyCatalogV1(
                EnemyCatalogV1.SupportedSchemaVersion,
                Id("enemy-content", "drop-binding-tests"),
                definitions);
        }

        private static EnemyDeathFactV1 EnemyDeath(
            EnemyDefinitionV1 definition,
            string deathValue,
            string placementValue,
            long generation)
        {
            EnemyRuntimeIdentityV1 identity = new EnemyRuntimeIdentityV1(
                Id("enemy-entity", placementValue),
                Id("run-participant", placementValue),
                RunId,
                Id("room-runtime", "one"),
                Id("room", "one"),
                Id("placement", placementValue));
            return new EnemyDeathFactV1(
                Id("death", deathValue),
                Id("trigger", deathValue),
                identity,
                definition.DefinitionId,
                1,
                generation,
                PlayerEntityId,
                PlayerParticipantId,
                definition.ExperienceProfileId,
                definition.DropProfileId,
                (EnemyActorDeathCause)1);
        }

        private static PropDefinitionV1 PropDefinition(
            string value,
            StableId dropProfileId)
        {
            List<PropCapabilityV1> capabilities = new List<PropCapabilityV1>
            {
                PropCapabilitiesV1.Collision(true),
                PropCapabilitiesV1.HealthBased(10d),
                PropCapabilitiesV1.DamageBehavior(
                    PropDamageAlignmentV1.Hostile,
                    Id("damage-policy", "player-normal"))
            };
            if (dropProfileId != null)
            {
                capabilities.Add(PropCapabilitiesV1.DropOnDestroy(dropProfileId));
            }
            return new PropDefinitionV1(
                Id("prop", value),
                Id("presentation", value),
                capabilities);
        }

        private static PropCatalogV1 PropCatalog(params PropDefinitionV1[] definitions)
        {
            return new PropCatalogV1(
                PropCapabilityRegistryV1.CreateBuiltIns(),
                definitions);
        }

        private static PropRuntimeV1 CreatePropRuntime(
            PropCatalogV1 catalog,
            PropDefinitionV1 definition,
            string placementValue)
        {
            PropRuntimeCreationResultV1 created = new PropRuntimeFactoryV1().Create(
                catalog,
                new PropPlacementV1(
                    PlacedObjectIdentity.CreateAuthored(Id("placement", placementValue)),
                    definition.DefinitionId),
                new AlwaysAllowPropDamage());
            Assert.That(created.IsCreated, Is.True);
            return created.Runtime;
        }

        private static PropDamageResultV1 Destroy(
            PropRuntimeV1 runtime,
            string operationValue)
        {
            PropDamageResultV1 result = runtime.ApplyDamage(
                new PropDamageCommandV1(
                    Id("operation", operationValue),
                    PlayerParticipantId,
                    Id("faction", "player"),
                    Id("damage", "kinetic"),
                    10d));
            Assert.That(result.Status, Is.EqualTo(PropDamageStatusV1.Destroyed));
            return result;
        }

        private static StableId Id(string scope, string value)
        {
            return StableId.Create(scope, value);
        }
    }
}
#endif
