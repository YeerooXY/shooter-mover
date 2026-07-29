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
using ShooterMover.LootDropBinding;

namespace ShooterMover.Tests.EditMode.LootDropBinding
{
    public sealed class LootDropGenerationStateTests
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

        private sealed class FixtureBridge : ILootDropFactBridge
        {
            private readonly StableId kindId;
            private readonly StableId definitionId;

            public FixtureBridge(string kindValue = "fixture")
            {
                kindId = Id("terminal-drop-fact", kindValue);
                definitionId = Id("definition", "fixture-source");
            }

            public StableId FactKindStableId { get { return kindId; } }
            public Type FactType { get { return typeof(FixtureFact); } }

            public LootDropAdaptationResult Adapt(object terminalFact)
            {
                FixtureFact fact = terminalFact as FixtureFact;
                if (fact == null)
                {
                    return LootDropAdaptationResult.Rejected(
                        LootDropRejectionCode.InvalidTerminalFact,
                        "fixture-type-mismatch");
                }

                return LootDropAdaptationResult.Accepted(
                    new LootDropSourceFact(
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

        private sealed class AlternateBridge : ILootDropFactBridge
        {
            public StableId FactKindStableId { get { return Id("terminal-drop-fact", "alternate"); } }
            public Type FactType { get { return typeof(AlternateFact); } }

            public LootDropAdaptationResult Adapt(object terminalFact)
            {
                return LootDropAdaptationResult.Rejected(
                    LootDropRejectionCode.InvalidTerminalFact,
                    "unused-alternate-adapter");
            }
        }

        private sealed class FixedRunContextResolver : ILootDropRunContextResolver
        {
            public long CurrentGeneration = 1L;
            public bool RunExists = true;
            public bool Ended;

            public bool TryResolve(
                StableId runStableId,
                long expectedLifecycleGeneration,
                out LootDropRunGenerationContext context,
                out LootDropRejectionCode rejectionCode,
                out string diagnostic)
            {
                context = null;
                rejectionCode = LootDropRejectionCode.None;
                diagnostic = string.Empty;

                if (!RunExists || runStableId != RunId)
                {
                    rejectionCode = LootDropRejectionCode.MissingRun;
                    diagnostic = "fixture-run-missing";
                    return false;
                }
                if (expectedLifecycleGeneration != CurrentGeneration)
                {
                    rejectionCode = LootDropRejectionCode.WrongRunLifecycle;
                    diagnostic = "fixture-run-lifecycle-mismatch";
                    return false;
                }
                if (Ended)
                {
                    rejectionCode = LootDropRejectionCode.RunEnded;
                    diagnostic = "fixture-run-ended";
                    return false;
                }

                context = new LootDropRunGenerationContext(
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

        private sealed class FailOnceGenerator : IRewardGenerationExecutor
        {
            private readonly IRewardGenerationExecutor inner =
                new ExistingRewardGenerationExecutor(new RewardGenerationActions());

            public int CallCount { get; private set; }

            public RewardGenerationResultEnvelope Generate(RewardGenerationRequest request)
            {
                CallCount++;
                return CallCount == 1 ? null : inner.Generate(request);
            }
        }

        private sealed class AlwaysAllowPropDamage : IPropDamageEligibilityPolicy
        {
            public bool CanDamage(PropDamageEligibilityContext context)
            {
                return true;
            }
        }

        private sealed class PropSourceContextResolver : IPropTerminalSourceContextResolver
        {
            private readonly Dictionary<StableId, StableId> placements =
                new Dictionary<StableId, StableId>();

            public void Register(StableId participantId, StableId placementId)
            {
                placements[participantId] = placementId;
            }

            public bool TryResolve(
                PropTerminalFact terminalFact,
                out PropTerminalSourceContext context,
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

                context = new PropTerminalSourceContext(
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
            EnemyDefinition definition = EnemyDefinition("fixture-enemy", MoneyProfileId);
            CountingGenerator generator = new CountingGenerator();
            LootDropGenerationState authority = Authority(
                new EnemyDeathLootDropFactBridge(EnemyCatalog(definition)),
                Profiles(MoneyProfile()),
                generator);

            GeneratedLootDropResult result = authority.Generate(
                EnemyDeath(definition, "enemy-death-one", "enemy-placement-one", 1L));

            Assert.That(result.Status, Is.EqualTo(LootDropBindingStatus.Accepted));
            Assert.That(result.GeneratedRewards.Count, Is.EqualTo(1));
            Assert.That(generator.CallCount, Is.EqualTo(1));
            Assert.That(authority.AcceptedBatchCount, Is.EqualTo(1));
        }

        [Test]
        public void OneEligiblePropDestruction_GeneratesExactlyOneBatch()
        {
            PropDefinition definition = PropDefinition("fixture-prop", MoneyProfileId);
            PropCatalog catalog = PropCatalog(definition);
            PropSourceContextResolver contexts = new PropSourceContextResolver();
            PropLive runtime = CreatePropRuntime(catalog, definition, "prop-placement-one");
            contexts.Register(runtime.ParticipantId, Id("placement", "prop-placement-one"));
            PropDamageResult destroyed = Destroy(runtime, "destroy-prop-one");
            CountingGenerator generator = new CountingGenerator();
            LootDropGenerationState authority = Authority(
                new PropDestructionLootDropFactBridge(catalog, contexts),
                Profiles(MoneyProfile()),
                generator);

            GeneratedLootDropResult result = authority.Generate(destroyed.Facts);

            Assert.That(result.Status, Is.EqualTo(LootDropBindingStatus.Accepted));
            Assert.That(result.SourceFact.SourcePlacementStableId,
                Is.EqualTo(Id("placement", "prop-placement-one")));
            Assert.That(generator.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void EnemyWithoutDropProfile_ProducesDeterministicNoDrop()
        {
            EnemyDefinition definition = EnemyDefinition("enemy-no-drop", null);
            LootDropGenerationState authority = Authority(
                new EnemyDeathLootDropFactBridge(EnemyCatalog(definition)),
                Profiles(),
                new CountingGenerator());

            GeneratedLootDropResult result = authority.Generate(
                EnemyDeath(definition, "enemy-death-no-drop", "enemy-placement-no-drop", 1L));

            Assert.That(result.Status, Is.EqualTo(LootDropBindingStatus.ExplicitNoDrop));
            Assert.That(result.GeneratedRewards, Is.Empty);
            Assert.That(result.ResolvedDropProfileStableId, Is.Not.Null);
        }

        [Test]
        public void PropWithoutDropProfile_ProducesDeterministicNoDrop()
        {
            PropDefinition definition = PropDefinition("prop-no-drop", null);
            PropCatalog catalog = PropCatalog(definition);
            PropSourceContextResolver contexts = new PropSourceContextResolver();
            PropLive runtime = CreatePropRuntime(catalog, definition, "prop-placement-no-drop");
            contexts.Register(runtime.ParticipantId, Id("placement", "prop-placement-no-drop"));
            PropDamageResult destroyed = Destroy(runtime, "destroy-prop-no-drop");
            LootDropGenerationState authority = Authority(
                new PropDestructionLootDropFactBridge(catalog, contexts),
                Profiles(),
                new CountingGenerator());

            GeneratedLootDropResult result = authority.Generate(destroyed.Facts);

            Assert.That(result.Status, Is.EqualTo(LootDropBindingStatus.ExplicitNoDrop));
            Assert.That(result.GeneratedRewards, Is.Empty);
        }

        [Test]
        public void ExactReplay_DoesNotGenerateSecondBatch()
        {
            CountingGenerator generator = new CountingGenerator();
            LootDropGenerationState authority = FixtureAuthority(
                Profiles(StrongboxProfile()),
                generator);
            FixtureFact fact = Fixture("replay", StrongboxProfileId);

            authority.Generate(fact);
            GeneratedLootDropResult replay = authority.Generate(fact);

            Assert.That(replay.Status, Is.EqualTo(LootDropBindingStatus.ExactReplay));
            Assert.That(generator.CallCount, Is.EqualTo(1));
            Assert.That(authority.AcceptedBatchCount, Is.EqualTo(1));
        }

        [Test]
        public void ExactReplay_PreservesOperationChildrenAndFingerprint()
        {
            LootDropGenerationState authority = FixtureAuthority(
                Profiles(StrongboxProfile()));
            FixtureFact fact = Fixture("replay-identities", StrongboxProfileId);

            GeneratedLootDropResult first = authority.Generate(fact);
            GeneratedLootDropResult replay = authority.Generate(fact);

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
            LootDropGenerationState authority = FixtureAuthority(
                Profiles(MoneyProfile()),
                generator);

            GeneratedLootDropResult first = authority.Generate(
                Fixture("conflict", MoneyProfileId, immutableToken: "first"));
            GeneratedLootDropResult conflict = authority.Generate(
                Fixture("conflict", MoneyProfileId, immutableToken: "changed"));

            Assert.That(first.IsAccepted, Is.True);
            Assert.That(conflict.Status,
                Is.EqualTo(LootDropBindingStatus.ConflictingDuplicate));
            Assert.That(generator.CallCount, Is.EqualTo(1));
            Assert.That(authority.AcceptedBatchCount, Is.EqualTo(1));
        }

        [Test]
        public void DistinctEventsFromSameDefinition_ProduceDistinctOperations()
        {
            LootDropGenerationState authority = FixtureAuthority(Profiles(MoneyProfile()));

            GeneratedLootDropResult first = authority.Generate(
                Fixture("distinct-event-a", MoneyProfileId));
            GeneratedLootDropResult second = authority.Generate(
                Fixture("distinct-event-b", MoneyProfileId));

            Assert.That(first.OperationRequest.SourceOperationStableId,
                Is.Not.EqualTo(second.OperationRequest.SourceOperationStableId));
        }

        [Test]
        public void SameDefinitionAtDifferentPlacements_RemainsDistinct()
        {
            LootDropGenerationState authority = FixtureAuthority(Profiles(MoneyProfile()));

            GeneratedLootDropResult first = authority.Generate(
                Fixture("placement-event-a", MoneyProfileId, placementValue: "placement-a"));
            GeneratedLootDropResult second = authority.Generate(
                Fixture("placement-event-b", MoneyProfileId, placementValue: "placement-b"));

            Assert.That(first.SourceFact.SourcePlacementStableId,
                Is.Not.EqualTo(second.SourceFact.SourcePlacementStableId));
            Assert.That(first.OperationRequest.SourceOperationStableId,
                Is.Not.EqualTo(second.OperationRequest.SourceOperationStableId));
        }

        [Test]
        public void DifferentSourceLifecycleGenerations_CannotShareOperation()
        {
            LootDropGenerationState authority = FixtureAuthority(Profiles(MoneyProfile()));

            GeneratedLootDropResult first = authority.Generate(
                Fixture("generation-event-a", MoneyProfileId, sourceGeneration: 1L));
            GeneratedLootDropResult second = authority.Generate(
                Fixture("generation-event-b", MoneyProfileId, sourceGeneration: 2L));

            Assert.That(first.OperationRequest.SourceOperationStableId,
                Is.Not.EqualTo(second.OperationRequest.SourceOperationStableId));
        }

        [Test]
        public void StaleRunLifecycle_RejectsSafely()
        {
            FixedRunContextResolver run = new FixedRunContextResolver { CurrentGeneration = 2L };
            CountingGenerator generator = new CountingGenerator();
            LootDropGenerationState authority = FixtureAuthority(
                Profiles(MoneyProfile()),
                generator,
                run);

            GeneratedLootDropResult result = authority.Generate(
                Fixture("stale-run", MoneyProfileId));

            Assert.That(result.RejectionCode,
                Is.EqualTo(LootDropRejectionCode.WrongRunLifecycle));
            Assert.That(generator.CallCount, Is.EqualTo(0));
            Assert.That(authority.AcceptedBatchCount, Is.EqualTo(0));
        }

        [Test]
        public void MissingDefinitionResolution_Rejects()
        {
            EnemyDefinition present = EnemyDefinition("enemy-present", MoneyProfileId);
            EnemyDefinition missing = EnemyDefinition("enemy-missing", MoneyProfileId);
            LootDropGenerationState authority = Authority(
                new EnemyDeathLootDropFactBridge(EnemyCatalog(present)),
                Profiles(MoneyProfile()),
                new CountingGenerator());

            GeneratedLootDropResult result = authority.Generate(
                EnemyDeath(missing, "enemy-death-missing", "enemy-placement-missing", 1L));

            Assert.That(result.RejectionCode,
                Is.EqualTo(LootDropRejectionCode.MissingDefinition));
        }

        [Test]
        public void MissingDropProfileResolution_Rejects()
        {
            LootDropGenerationState authority = FixtureAuthority(Profiles());

            GeneratedLootDropResult result = authority.Generate(
                Fixture("missing-profile", Id("drop-profile", "not-registered")));

            Assert.That(result.RejectionCode,
                Is.EqualTo(LootDropRejectionCode.MissingDropProfile));
            Assert.That(result.Diagnostic, Does.Contain("drop-profile.not-registered"));
        }

        [Test]
        public void UnsupportedFactType_FailsClosed()
        {
            LootDropGenerationState authority = FixtureAuthority(Profiles());

            GeneratedLootDropResult result = authority.Generate(new object());

            Assert.That(result.RejectionCode,
                Is.EqualTo(LootDropRejectionCode.UnsupportedFactType));
            Assert.That(authority.AcceptedBatchCount, Is.EqualTo(0));
        }

        [Test]
        public void UnattributedFact_DoesNotAwardArbitraryParticipant()
        {
            CountingGenerator generator = new CountingGenerator();
            LootDropGenerationState authority = FixtureAuthority(
                Profiles(MoneyProfile()),
                generator);

            GeneratedLootDropResult result = authority.Generate(
                Fixture(
                    "unattributed",
                    MoneyProfileId,
                    participantId: null,
                    immutableToken: "unattributed"));

            Assert.That(result.RejectionCode,
                Is.EqualTo(LootDropRejectionCode.UnattributedTerminalFact));
            Assert.That(generator.CallCount, Is.EqualTo(0));
        }

        [Test]
        public void RegistrationOrder_DoesNotAffectFingerprints()
        {
            ILootDropFactBridge first = new FixtureBridge("fixture");
            ILootDropFactBridge second = new AlternateBridge();
            LootDropFactBridgeRegistry ordered =
                new LootDropFactBridgeRegistry(new[] { first, second });
            LootDropFactBridgeRegistry reversed =
                new LootDropFactBridgeRegistry(new[] { second, first });
            RewardProfileCatalogResolver profilesOrdered =
                Profiles(MoneyProfile(), StrongboxProfile());
            RewardProfileCatalogResolver profilesReversed =
                Profiles(StrongboxProfile(), MoneyProfile());

            Assert.That(ordered.Fingerprint, Is.EqualTo(reversed.Fingerprint));
            Assert.That(profilesOrdered.Fingerprint, Is.EqualTo(profilesReversed.Fingerprint));

            FixtureFact fact = Fixture("registration-order", MoneyProfileId);
            GeneratedLootDropResult firstResult = FixtureAuthority(profilesOrdered).Generate(fact);
            GeneratedLootDropResult secondResult = FixtureAuthority(profilesReversed).Generate(fact);
            Assert.That(firstResult.Fingerprint, Is.EqualTo(secondResult.Fingerprint));
        }

        [Test]
        public void MultiRewardBatch_HasDeterministicChildOrdering()
        {
            LootDropGenerationState authority = FixtureAuthority(Profiles(MultiProfile()));

            GeneratedLootDropResult result = authority.Generate(
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
            LootDropGenerationState authority = FixtureAuthority(
                Profiles(StrongboxProfile()));

            GeneratedLootDropResult result = authority.Generate(
                Fixture("strongbox-one", StrongboxProfileId));

            Assert.That(result.GeneratedRewards.Count, Is.EqualTo(1));
            Assert.That(result.GeneratedRewards[0].Kind,
                Is.EqualTo(RewardGrantKind.Strongbox));
            Assert.That(result.GeneratedRewards[0].Quantity, Is.EqualTo(1L));
            Assert.That(result.GeneratedRewards[0].RewardInstanceStableId, Is.Not.Null);
        }

        [Test]
        public void SameTierStrongboxesFromDifferentFacts_HaveDistinctInstances()
        {
            LootDropGenerationState authority = FixtureAuthority(
                Profiles(StrongboxProfile()));

            GeneratedLootDropResult first = authority.Generate(
                Fixture("strongbox-a", StrongboxProfileId));
            GeneratedLootDropResult second = authority.Generate(
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
            LootDropGenerationState authority = FixtureAuthority(
                Profiles(MultiProfile()),
                generator);

            GeneratedLootDropResult result = authority.Generate(
                Fixture("generation-failure", MultiProfileId));

            Assert.That(result.Status, Is.EqualTo(LootDropBindingStatus.Rejected));
            Assert.That(result.RejectionCode,
                Is.EqualTo(LootDropRejectionCode.GenerationFailed));
            Assert.That(result.GeneratedBatch, Is.Null);
            Assert.That(result.GeneratedRewards, Is.Empty);
            Assert.That(authority.AcceptedBatchCount, Is.EqualTo(0));
        }

        [Test]
        public void ExactRetryAfterRetryableFailure_CanSucceed()
        {
            FailOnceGenerator generator = new FailOnceGenerator();
            LootDropGenerationState authority = FixtureAuthority(
                Profiles(MoneyProfile()),
                generator);
            FixtureFact fact = Fixture("retryable-failure", MoneyProfileId);

            GeneratedLootDropResult failed = authority.Generate(fact);
            GeneratedLootDropResult retry = authority.Generate(fact);

            Assert.That(failed.RejectionCode,
                Is.EqualTo(LootDropRejectionCode.GenerationFailed));
            Assert.That(retry.Status, Is.EqualTo(LootDropBindingStatus.Accepted));
            Assert.That(generator.CallCount, Is.EqualTo(2));
            Assert.That(authority.AcceptedBatchCount, Is.EqualTo(1));
        }

        [Test]
        public void Generation_DoesNotMutatePermanentAuthorities()
        {
            int permanentMutationCalls = 0;
            LootDropGenerationState authority = FixtureAuthority(Profiles(MoneyProfile()));

            GeneratedLootDropResult result = authority.Generate(
                Fixture("no-permanent-mutation", MoneyProfileId));

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(permanentMutationCalls, Is.EqualTo(0));
            Assert.That(result.GeneratedRewards.Count, Is.EqualTo(1));
        }

        [Test]
        public void NewEnemyUsingExistingProfile_NeedsNoSharedConsumerEdit()
        {
            EnemyDefinition first = EnemyDefinition("enemy-fixture-one", MoneyProfileId);
            EnemyDefinition second = EnemyDefinition("enemy-fixture-two", MoneyProfileId);
            EnemyDeathLootDropFactBridge sharedAdapter =
                new EnemyDeathLootDropFactBridge(EnemyCatalog(first, second));
            LootDropGenerationState authority = Authority(
                sharedAdapter,
                Profiles(MoneyProfile()),
                new CountingGenerator());

            GeneratedLootDropResult result = authority.Generate(
                EnemyDeath(second, "enemy-death-two", "enemy-placement-two", 1L));

            Assert.That(result.Status, Is.EqualTo(LootDropBindingStatus.Accepted));
            Assert.That(result.SourceFact.SourceDefinitionStableId,
                Is.EqualTo(second.DefinitionId));
        }

        [Test]
        public void NewPropUsingExistingProfile_NeedsNoSharedConsumerEdit()
        {
            PropDefinition first = PropDefinition("prop-fixture-one", MoneyProfileId);
            PropDefinition second = PropDefinition("prop-fixture-two", MoneyProfileId);
            PropCatalog catalog = PropCatalog(first, second);
            PropSourceContextResolver contexts = new PropSourceContextResolver();
            PropLive runtime = CreatePropRuntime(catalog, second, "prop-placement-two");
            contexts.Register(runtime.ParticipantId, Id("placement", "prop-placement-two"));
            PropDestructionLootDropFactBridge sharedAdapter =
                new PropDestructionLootDropFactBridge(catalog, contexts);
            LootDropGenerationState authority = Authority(
                sharedAdapter,
                Profiles(MoneyProfile()),
                new CountingGenerator());

            GeneratedLootDropResult result = authority.Generate(
                Destroy(runtime, "destroy-prop-two").Facts);

            Assert.That(result.Status, Is.EqualTo(LootDropBindingStatus.Accepted));
            Assert.That(result.SourceFact.SourceDefinitionStableId,
                Is.EqualTo(second.DefinitionId));
        }

        [Test]
        public void RealGenericEnemyDeathFact_ReachesExistingDropGenBoundary()
        {
            EnemyDefinition definition = EnemyDefinition("enemy-integration", MultiProfileId);
            CountingGenerator generator = new CountingGenerator();
            LootDropGenerationState authority = Authority(
                new EnemyDeathLootDropFactBridge(EnemyCatalog(definition)),
                Profiles(MultiProfile()),
                generator);
            EnemyDeathFact fact = EnemyDeath(
                definition,
                "enemy-death-integration",
                "enemy-placement-integration",
                1L);

            GeneratedLootDropResult result = authority.Generate(fact);

            Assert.That(result.GeneratedBatch, Is.Not.Null);
            Assert.That(result.GeneratedRewards.Count, Is.EqualTo(2));
            Assert.That(generator.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void RealGenericPropTerminalFact_ReachesSameBoundary()
        {
            PropDefinition definition = PropDefinition("prop-integration", MultiProfileId);
            PropCatalog catalog = PropCatalog(definition);
            PropSourceContextResolver contexts = new PropSourceContextResolver();
            PropLive runtime = CreatePropRuntime(catalog, definition, "prop-placement-integration");
            contexts.Register(runtime.ParticipantId, Id("placement", "prop-placement-integration"));
            PropFactBatch facts = Destroy(runtime, "destroy-prop-integration").Facts;
            LootDropGenerationState authority = Authority(
                new PropDestructionLootDropFactBridge(catalog, contexts),
                Profiles(MultiProfile()),
                new CountingGenerator());

            GeneratedLootDropResult result = authority.Generate(facts);

            Assert.That(result.GeneratedBatch, Is.Not.Null);
            Assert.That(result.GeneratedRewards.Count, Is.EqualTo(2));
        }

        [Test]
        public void DuplicateDeliveryThroughTwoRoutes_CannotDuplicateBatch()
        {
            EnemyDefinition definition = EnemyDefinition("enemy-duplicate-route", StrongboxProfileId);
            CountingGenerator generator = new CountingGenerator();
            LootDropGenerationState authority = Authority(
                new EnemyDeathLootDropFactBridge(EnemyCatalog(definition)),
                Profiles(StrongboxProfile()),
                generator);
            EnemyDeathFact fact = EnemyDeath(
                definition,
                "enemy-death-duplicate-route",
                "enemy-placement-duplicate-route",
                1L);

            GeneratedLootDropResult firstRoute = authority.Generate(fact);
            GeneratedLootDropResult secondRoute = authority.Generate(fact);

            Assert.That(firstRoute.IsAccepted, Is.True);
            Assert.That(secondRoute.Status,
                Is.EqualTo(LootDropBindingStatus.ExactReplay));
            Assert.That(generator.CallCount, Is.EqualTo(1));
            Assert.That(secondRoute.GeneratedRewards[0].RewardInstanceStableId,
                Is.EqualTo(firstRoute.GeneratedRewards[0].RewardInstanceStableId));
        }

        [Test]
        public void ImmutableBatch_IsPickupReadyWithoutRerolling()
        {
            CountingGenerator generator = new CountingGenerator();
            LootDropGenerationState authority = FixtureAuthority(
                Profiles(StrongboxProfile()),
                generator);
            FixtureFact fact = Fixture("pickup-ready", StrongboxProfileId);

            GeneratedLootDropResult generated = authority.Generate(fact);
            StableId pendingPickupIdentity =
                generated.GeneratedRewards[0].RewardInstanceStableId;
            GeneratedLootDropResult recovered = authority.Generate(fact);

            Assert.That(recovered.GeneratedRewards[0].RewardInstanceStableId,
                Is.EqualTo(pendingPickupIdentity));
            Assert.That(recovered.GenerationSeed, Is.EqualTo(generated.GenerationSeed));
            Assert.That(generator.CallCount, Is.EqualTo(1));
        }

        private static LootDropGenerationState FixtureAuthority(
            RewardProfileCatalogResolver profiles,
            IRewardGenerationExecutor generator = null,
            FixedRunContextResolver run = null)
        {
            return Authority(
                new FixtureBridge(),
                profiles,
                generator ?? new CountingGenerator(),
                run ?? new FixedRunContextResolver());
        }

        private static LootDropGenerationState Authority(
            ILootDropFactBridge adapter,
            RewardProfileCatalogResolver profiles,
            IRewardGenerationExecutor generator,
            ILootDropRunContextResolver run = null)
        {
            return new LootDropGenerationState(
                new LootDropFactBridgeRegistry(new[] { adapter }),
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

        private static RewardProfileCatalogResolver Profiles(
            params RewardProfile[] profiles)
        {
            return new RewardProfileCatalogResolver(profiles);
        }

        private static RewardProfile MoneyProfile()
        {
            return RewardProfile.Create(
                MoneyProfileId,
                new[]
                {
                    RewardGrantSpecification.CreateFixed(
                        Id("grant", "money"),
                        RewardGrantKind.Money,
                        Id("currency", "credits"),
                        25L)
                },
                Array.Empty<IndependentRewardRoll>(),
                Array.Empty<ExclusiveRewardGroup>());
        }

        private static RewardProfile MultiProfile()
        {
            return RewardProfile.Create(
                MultiProfileId,
                new[]
                {
                    RewardGrantSpecification.CreateFixed(
                        Id("grant", "z-scrap"),
                        RewardGrantKind.Scrap,
                        Id("currency", "scrap"),
                        2L),
                    RewardGrantSpecification.CreateFixed(
                        Id("grant", "a-money"),
                        RewardGrantKind.Money,
                        Id("currency", "credits"),
                        10L)
                },
                Array.Empty<IndependentRewardRoll>(),
                Array.Empty<ExclusiveRewardGroup>());
        }

        private static RewardProfile StrongboxProfile()
        {
            return RewardProfile.Create(
                StrongboxProfileId,
                new[]
                {
                    RewardGrantSpecification.CreateFixed(
                        Id("grant", "strongbox"),
                        RewardGrantKind.Strongbox,
                        Id("strongbox", "tier-emerald"),
                        1L)
                },
                Array.Empty<IndependentRewardRoll>(),
                Array.Empty<ExclusiveRewardGroup>());
        }

        private static EnemyDefinition EnemyDefinition(
            string value,
            StableId dropProfileId)
        {
            return new EnemyDefinition(
                Id("enemy", value),
                Id("presentation", value),
                10d,
                new EnemyLevelScalingProfile(1, 100, 0d, 1d),
                Id("faction", "enemy"),
                10d,
                360d,
                Id("movement", "fixture"),
                Id("decision", "fixture"),
                Array.Empty<EnemyAttackCapabilityDescriptor>(),
                Id("experience-profile", "fixture"),
                dropProfileId,
                EnemyCatalogRoomClearRole.RequiredEnemy,
                Array.Empty<StableId>());
        }

        private static EnemyCatalog EnemyCatalog(params EnemyDefinition[] definitions)
        {
            return new EnemyCatalog(
                global::ShooterMover.Domain.Enemies.Catalog.EnemyCatalog.SupportedSchemaVersion,
                Id("enemy-content", "drop-binding-tests"),
                definitions);
        }

        private static EnemyDeathFact EnemyDeath(
            EnemyDefinition definition,
            string deathValue,
            string placementValue,
            long generation)
        {
            EnemyLiveIdentity identity = new EnemyLiveIdentity(
                Id("enemy-entity", placementValue),
                Id("run-participant", placementValue),
                RunId,
                Id("room-runtime", "one"),
                Id("room", "one"),
                Id("placement", placementValue));
            return new EnemyDeathFact(
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

        private static PropDefinition PropDefinition(
            string value,
            StableId dropProfileId)
        {
            List<PropCapability> capabilities = new List<PropCapability>
            {
                PropCapabilities.Collision(true),
                PropCapabilities.HealthBased(10d),
                PropCapabilities.DamageBehavior(
                    PropDamageAlignment.Hostile,
                    Id("damage-policy", "player-normal"))
            };
            if (dropProfileId != null)
            {
                capabilities.Add(PropCapabilities.DropOnDestroy(dropProfileId));
            }
            return new PropDefinition(
                Id("prop", value),
                Id("presentation", value),
                capabilities);
        }

        private static PropCatalog PropCatalog(params PropDefinition[] definitions)
        {
            return new PropCatalog(
                PropCapabilityRegistry.CreateBuiltIns(),
                definitions);
        }

        private static PropLive CreatePropRuntime(
            PropCatalog catalog,
            PropDefinition definition,
            string placementValue)
        {
            PropLiveCreationResult created = new PropLiveFactory().Create(
                catalog,
                new PropPlacement(
                    PlacedObjectIdentity.CreateAuthored(Id("placement", placementValue)),
                    definition.DefinitionId),
                new AlwaysAllowPropDamage());
            Assert.That(created.IsCreated, Is.True);
            return created.Runtime;
        }

        private static PropDamageResult Destroy(
            PropLive runtime,
            string operationValue)
        {
            PropDamageResult result = runtime.ApplyDamage(
                new PropDamageCommand(
                    Id("operation", operationValue),
                    PlayerParticipantId,
                    Id("faction", "player"),
                    Id("damage", "kinetic"),
                    10d));
            Assert.That(result.Status, Is.EqualTo(PropDamageStatus.Destroyed));
            return result;
        }

        private static StableId Id(string scope, string value)
        {
            return StableId.Create(scope, value);
        }
    }
}
#endif
