#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Domain.Authoring;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Props;

namespace ShooterMover.Tests.EditMode.Props
{
    public sealed class PropLiveTests
    {
        private sealed class RecordingDamagePolicy : IPropDamageEligibilityPolicy
        {
            private readonly bool _answer;

            public RecordingDamagePolicy(bool answer)
            {
                _answer = answer;
            }

            public int CallCount { get; private set; }

            public PropDamageEligibilityContext LastContext { get; private set; }

            public bool CanDamage(PropDamageEligibilityContext context)
            {
                CallCount++;
                LastContext = context;
                return _answer;
            }
        }

        [Test]
        public void DecorativeProp_CreatesNoCombatAuthority()
        {
            PropDefinition definition = new PropDefinition(
                Id("prop.decorative-console"),
                Id("presentation.decorative-console"),
                new[]
                {
                    PropCapabilities.Collision(false),
                    PropCapabilities.Decorative()
                });
            PropLiveCreationResult created = Create(
                definition,
                "placed.decorative-console",
                null);

            Assert.That(created.IsCreated, Is.True);
            Assert.That(created.Runtime.Snapshot.HasCombatAuthority, Is.False);
            Assert.That(created.Runtime.Snapshot.CurrentHealth, Is.Null);

            PropDamageResult damage = created.Runtime.ApplyDamage(
                Damage("operation.decorative-hit", 10d));
            Assert.That(
                damage.Status,
                Is.EqualTo(PropDamageStatus.RejectedNoCombatAuthority));
            Assert.That(damage.Facts.IsEmpty, Is.True);
        }

        [Test]
        public void RepeatedDefinitionPlacements_RetainIndependentHealth()
        {
            PropDefinition definition = CoverDefinition();
            RecordingDamagePolicy policy = new RecordingDamagePolicy(true);
            PropLive first = Create(
                definition,
                "placed.cover-a",
                policy).Runtime;
            PropLive second = Create(
                definition,
                "placed.cover-b",
                policy).Runtime;

            PropDamageResult result = first.ApplyDamage(
                Damage("operation.cover-a-hit", 25d));

            Assert.That(result.Status, Is.EqualTo(PropDamageStatus.Applied));
            Assert.That(first.Snapshot.CurrentHealth, Is.EqualTo(75d));
            Assert.That(second.Snapshot.CurrentHealth, Is.EqualTo(100d));
            Assert.That(first.ParticipantId, Is.Not.EqualTo(second.ParticipantId));
            Assert.That(first.Snapshot.Fingerprint, Is.Not.EqualTo(second.Snapshot.Fingerprint));
        }

        [Test]
        public void LethalDamage_ExactReplayRecoversOriginalFacts_AndMutatesOnlyOnce()
        {
            PropDefinition definition = BarrelDefinition();
            RecordingDamagePolicy policy = new RecordingDamagePolicy(true);
            PropLive runtime = Create(
                definition,
                "placed.barrel-01",
                policy).Runtime;
            PropDamageCommand command = Damage("operation.destroy-barrel", 100d);

            PropDamageResult first = runtime.ApplyDamage(command);
            PropLiveSnapshot terminalSnapshot = runtime.Snapshot;
            PropDamageResult exactReplay = runtime.ApplyDamage(command);

            Assert.That(first.Status, Is.EqualTo(PropDamageStatus.Destroyed));
            AssertCompleteDamageFacts(first.Facts);
            Assert.That(first.Snapshot.Fingerprint, Is.EqualTo(terminalSnapshot.Fingerprint));

            Assert.That(exactReplay.Status, Is.EqualTo(first.Status));
            AssertDamageFactsEquivalent(first.Facts, exactReplay.Facts);
            Assert.That(
                exactReplay.Snapshot.Fingerprint,
                Is.EqualTo(first.Snapshot.Fingerprint));
            Assert.That(policy.CallCount, Is.EqualTo(1));
            Assert.That(runtime.Snapshot.CurrentHealth, Is.EqualTo(0d));
            Assert.That(runtime.Snapshot.IsTerminal, Is.True);
            Assert.That(runtime.Snapshot.BlocksRoomClear, Is.False);
        }

        [Test]
        public void LostFirstDamageResponse_ExactRetryRecoversCompleteDistinctFactBatch()
        {
            RecordingDamagePolicy policy = new RecordingDamagePolicy(true);
            PropLive runtime = Create(
                BarrelDefinition(),
                "placed.lost-response-barrel",
                policy).Runtime;
            PropDamageCommand command = Damage(
                "operation.lost-destroy-response",
                100d);

            runtime.ApplyDamage(command);
            PropDamageResult recovered = runtime.ApplyDamage(command);

            Assert.That(recovered.Status, Is.EqualTo(PropDamageStatus.Destroyed));
            AssertCompleteDamageFacts(recovered.Facts);
            HashSet<StableId> factIds = new HashSet<StableId>
            {
                recovered.Facts.Terminal.FactId,
                recovered.Facts.Explosion.FactId,
                recovered.Facts.DropRequest.FactId,
                recovered.Facts.Objective.FactId
            };
            Assert.That(factIds.Count, Is.EqualTo(4));
            Assert.That(factIds.Contains(command.OperationId), Is.False);
            Assert.That(policy.CallCount, Is.EqualTo(1));
            Assert.That(runtime.Snapshot.CurrentHealth, Is.EqualTo(0d));
            Assert.That(runtime.Snapshot.IsTerminal, Is.True);
        }

        [Test]
        public void ConflictingDamageReplay_EmitsNoFactsAndDoesNotMutateTerminalState()
        {
            RecordingDamagePolicy policy = new RecordingDamagePolicy(true);
            PropLive runtime = Create(
                BarrelDefinition(),
                "placed.conflicting-barrel",
                policy).Runtime;
            PropDamageCommand accepted = Damage(
                "operation.conflicting-destroy",
                100d);

            runtime.ApplyDamage(accepted);
            string terminalFingerprint = runtime.Snapshot.Fingerprint;
            PropDamageResult conflicting = runtime.ApplyDamage(
                Damage("operation.conflicting-destroy", 101d));

            Assert.That(
                conflicting.Status,
                Is.EqualTo(PropDamageStatus.RejectedConflictingReplay));
            Assert.That(conflicting.Facts.IsEmpty, Is.True);
            Assert.That(runtime.Snapshot.Fingerprint, Is.EqualTo(terminalFingerprint));
            Assert.That(runtime.Snapshot.CurrentHealth, Is.EqualTo(0d));
            Assert.That(runtime.Snapshot.IsTerminal, Is.True);
            Assert.That(policy.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void AnalogousDamageOperations_AcrossPlacementsEmitPlacementDistinctFactIds()
        {
            PropDefinition definition = BarrelDefinition();
            RecordingDamagePolicy policy = new RecordingDamagePolicy(true);
            PropLive first = Create(
                definition,
                "placed.barrel-a",
                policy).Runtime;
            PropLive second = Create(
                definition,
                "placed.barrel-b",
                policy).Runtime;
            PropDamageCommand command = Damage(
                "operation.analogous-destroy",
                100d);

            PropDamageResult firstResult = first.ApplyDamage(command);
            PropDamageResult secondResult = second.ApplyDamage(command);

            Assert.That(
                firstResult.Facts.Terminal.FactId,
                Is.Not.EqualTo(secondResult.Facts.Terminal.FactId));
            Assert.That(
                firstResult.Facts.Explosion.FactId,
                Is.Not.EqualTo(secondResult.Facts.Explosion.FactId));
            Assert.That(
                firstResult.Facts.DropRequest.FactId,
                Is.Not.EqualTo(secondResult.Facts.DropRequest.FactId));
            Assert.That(
                firstResult.Facts.Objective.FactId,
                Is.Not.EqualTo(secondResult.Facts.Objective.FactId));
        }

        [Test]
        public void DamageResistance_AppliesTheAuthoredChannelMultiplier()
        {
            RecordingDamagePolicy policy = new RecordingDamagePolicy(true);
            PropLive runtime = Create(
                CoverDefinition(),
                "placed.fire-resistant-cover",
                policy).Runtime;

            PropDamageResult result = runtime.ApplyDamage(
                new PropDamageCommand(
                    Id("operation.fire-hit"),
                    Id("participant.player"),
                    Id("faction.player"),
                    Id("damage.fire"),
                    20d));

            Assert.That(result.Status, Is.EqualTo(PropDamageStatus.Applied));
            Assert.That(result.AppliedDamage, Is.EqualTo(10d));
            Assert.That(runtime.Snapshot.CurrentHealth, Is.EqualTo(90d));
        }

        [Test]
        public void IndestructibleProp_RejectsDamageWithoutOwningHealth()
        {
            PropDefinition definition = new PropDefinition(
                Id("prop.indestructible-cover"),
                Id("presentation.indestructible-cover"),
                new[]
                {
                    PropCapabilities.Collision(true),
                    PropCapabilities.Indestructible(),
                    PropCapabilities.DamageBehavior(
                        PropDamageAlignment.Neutral,
                        Id("damage-policy.player-normal"))
                });
            RecordingDamagePolicy policy = new RecordingDamagePolicy(true);
            PropLive runtime = Create(
                definition,
                "placed.indestructible-cover",
                policy).Runtime;

            PropDamageResult result = runtime.ApplyDamage(
                Damage("operation.indestructible-hit", 25d));

            Assert.That(
                result.Status,
                Is.EqualTo(PropDamageStatus.RejectedIndestructible));
            Assert.That(runtime.Snapshot.MaximumHealth, Is.Null);
            Assert.That(runtime.Snapshot.CurrentHealth, Is.Null);
            Assert.That(policy.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void UnknownCapability_AndInvalidCombination_Reject()
        {
            PropCapability unknown = new PropCapability(
                Id("capability.prop-unknown-mechanic"),
                new KeyValuePair<string, string>[0]);
            PropDefinition unknownDefinition = new PropDefinition(
                Id("prop.unknown"),
                Id("presentation.unknown"),
                new[] { unknown });

            PropCatalogValidationException unknownException =
                Assert.Throws<PropCatalogValidationException>(
                    () => Catalog(unknownDefinition));
            Assert.That(unknownException.Message, Does.Contain("Unknown prop capability"));

            PropDefinition invalidExplosion = new PropDefinition(
                Id("prop.invalid-explosion"),
                Id("presentation.invalid-explosion"),
                new[]
                {
                    PropCapabilities.Collision(true),
                    PropCapabilities.Indestructible(),
                    PropCapabilities.DamageBehavior(
                        PropDamageAlignment.Neutral,
                        Id("damage-policy.player-normal")),
                    PropCapabilities.ExplodeOnDestroy(
                        Id("explosion-profile.barrel"))
                });

            PropCatalogValidationException combinationException =
                Assert.Throws<PropCatalogValidationException>(
                    () => Catalog(invalidExplosion));
            Assert.That(
                combinationException.Message,
                Does.Contain("require health-based destructibility"));
        }

        [Test]
        public void FriendlyFireDecision_IsInjected_NotHardcodedInRuntime()
        {
            PropDefinition definition = CoverDefinition();
            RecordingDamagePolicy policy = new RecordingDamagePolicy(false);
            PropLive runtime = Create(
                definition,
                "placed.policy-cover",
                policy).Runtime;

            PropDamageResult result = runtime.ApplyDamage(
                Damage("operation.policy-hit", 30d));

            Assert.That(result.Status, Is.EqualTo(PropDamageStatus.RejectedByPolicy));
            Assert.That(runtime.Snapshot.CurrentHealth, Is.EqualTo(100d));
            Assert.That(policy.CallCount, Is.EqualTo(1));
            Assert.That(policy.LastContext.TargetParticipantId, Is.EqualTo(runtime.ParticipantId));
            Assert.That(
                policy.LastContext.PolicyId,
                Is.EqualTo(Id("damage-policy.player-normal")));
            Assert.That(
                policy.LastContext.TargetAlignment,
                Is.EqualTo(PropDamageAlignment.Neutral));
        }

        [Test]
        public void SwitchInteraction_ExactReplayRecoversFacts_AndTogglesOnlyOnce()
        {
            PropDefinition definition = SwitchDefinition();
            PropLive first = Create(
                definition,
                "placed.switch-a",
                null).Runtime;
            PropLive second = Create(
                definition,
                "placed.switch-b",
                null).Runtime;
            PropInteractionCommand command = new PropInteractionCommand(
                Id("operation.use-switch-a"),
                Id("participant.player"));

            PropInteractionResult applied = first.Interact(command);
            string toggledFingerprint = first.Snapshot.Fingerprint;
            PropInteractionResult replay = first.Interact(command);

            Assert.That(applied.Status, Is.EqualTo(PropInteractionStatus.Applied));
            AssertCompleteInteractionFacts(applied);
            Assert.That(
                applied.SwitchFact.KindId,
                Is.EqualTo(PropFactKindIds.SwitchOn));
            Assert.That(first.Snapshot.SwitchActive, Is.True);
            Assert.That(second.Snapshot.SwitchActive, Is.False);

            Assert.That(replay.Status, Is.EqualTo(applied.Status));
            AssertTriggeredFactsEquivalent(applied.Interaction, replay.Interaction);
            AssertTriggeredFactsEquivalent(applied.SwitchFact, replay.SwitchFact);
            AssertTriggeredFactsEquivalent(applied.Objective, replay.Objective);
            Assert.That(
                replay.Snapshot.Fingerprint,
                Is.EqualTo(applied.Snapshot.Fingerprint));
            Assert.That(first.Snapshot.Fingerprint, Is.EqualTo(toggledFingerprint));
            Assert.That(first.Snapshot.SwitchActive, Is.True);

            HashSet<StableId> factIds = new HashSet<StableId>
            {
                replay.Interaction.FactId,
                replay.SwitchFact.FactId,
                replay.Objective.FactId
            };
            Assert.That(factIds.Count, Is.EqualTo(3));
            Assert.That(factIds.Contains(command.OperationId), Is.False);
        }

        [Test]
        public void LostFirstSwitchResponse_ExactRetryRecoversCompleteFactSet()
        {
            PropLive runtime = Create(
                SwitchDefinition(),
                "placed.switch-lost-response",
                null).Runtime;
            PropInteractionCommand command = new PropInteractionCommand(
                Id("operation.lost-switch-response"),
                Id("participant.player"));

            runtime.Interact(command);
            PropInteractionResult recovered = runtime.Interact(command);

            Assert.That(recovered.Status, Is.EqualTo(PropInteractionStatus.Applied));
            AssertCompleteInteractionFacts(recovered);
            Assert.That(runtime.Snapshot.SwitchActive, Is.True);
        }

        [Test]
        public void CatalogFingerprint_DoesNotDependOnDefinitionOrder()
        {
            PropDefinition cover = CoverDefinition();
            PropDefinition barrel = BarrelDefinition();

            PropCatalog first = new PropCatalog(
                PropCapabilityRegistry.CreateBuiltIns(),
                new[] { cover, barrel });
            PropCatalog reordered = new PropCatalog(
                PropCapabilityRegistry.CreateBuiltIns(),
                new[] { barrel, cover });

            Assert.That(first.Fingerprint, Is.EqualTo(reordered.Fingerprint));
        }

        private static void AssertCompleteDamageFacts(PropFactBatch facts)
        {
            Assert.That(facts.Terminal, Is.Not.Null);
            Assert.That(facts.Explosion, Is.Not.Null);
            Assert.That(facts.DropRequest, Is.Not.Null);
            Assert.That(facts.Objective, Is.Not.Null);
            Assert.That(facts.Terminal.KindId, Is.EqualTo(PropFactKindIds.Terminal));
            Assert.That(
                facts.Explosion.KindId,
                Is.EqualTo(PropFactKindIds.ExplosionRequest));
            Assert.That(
                facts.DropRequest.KindId,
                Is.EqualTo(PropFactKindIds.DropRequest));
            Assert.That(
                facts.Objective.KindId,
                Is.EqualTo(PropFactKindIds.ObjectiveOnDestroy));
        }

        private static void AssertDamageFactsEquivalent(
            PropFactBatch expected,
            PropFactBatch actual)
        {
            Assert.That(actual.Terminal.FactId, Is.EqualTo(expected.Terminal.FactId));
            Assert.That(actual.Terminal.Fingerprint, Is.EqualTo(expected.Terminal.Fingerprint));
            AssertTriggeredFactsEquivalent(expected.Explosion, actual.Explosion);
            AssertTriggeredFactsEquivalent(expected.DropRequest, actual.DropRequest);
            AssertTriggeredFactsEquivalent(expected.Objective, actual.Objective);
        }

        private static void AssertCompleteInteractionFacts(
            PropInteractionResult result)
        {
            Assert.That(result.Interaction, Is.Not.Null);
            Assert.That(result.SwitchFact, Is.Not.Null);
            Assert.That(result.Objective, Is.Not.Null);
            Assert.That(
                result.Interaction.KindId,
                Is.EqualTo(PropFactKindIds.Interaction));
            Assert.That(
                result.Objective.KindId,
                Is.EqualTo(PropFactKindIds.ObjectiveOnInteraction));
        }

        private static void AssertTriggeredFactsEquivalent(
            PropTriggeredFact expected,
            PropTriggeredFact actual)
        {
            Assert.That(actual.FactId, Is.EqualTo(expected.FactId));
            Assert.That(actual.KindId, Is.EqualTo(expected.KindId));
            Assert.That(actual.ProfileOrFactId, Is.EqualTo(expected.ProfileOrFactId));
            Assert.That(actual.Fingerprint, Is.EqualTo(expected.Fingerprint));
        }

        private static PropDefinition CoverDefinition()
        {
            return new PropDefinition(
                Id("prop.cover-standard"),
                Id("presentation.cover-standard"),
                new[]
                {
                    PropCapabilities.Collision(true),
                    PropCapabilities.HealthBased(100d),
                    PropCapabilities.DamageBehavior(
                        PropDamageAlignment.Neutral,
                        Id("damage-policy.player-normal")),
                    PropCapabilities.DamageResistance(
                        new[]
                        {
                            new KeyValuePair<StableId, double>(
                                Id("damage.kinetic"),
                                1d),
                            new KeyValuePair<StableId, double>(
                                Id("damage.fire"),
                                0.5d)
                        })
                });
        }

        private static PropDefinition BarrelDefinition()
        {
            return new PropDefinition(
                Id("prop.barrel-explosive"),
                Id("presentation.barrel-explosive"),
                new[]
                {
                    PropCapabilities.Collision(true),
                    PropCapabilities.HealthBased(50d),
                    PropCapabilities.DamageBehavior(
                        PropDamageAlignment.Hostile,
                        Id("damage-policy.player-normal")),
                    PropCapabilities.ExplodeOnDestroy(
                        Id("explosion-profile.barrel")),
                    PropCapabilities.DropOnDestroy(
                        Id("drop-profile.barrel")),
                    PropCapabilities.Objective(
                        Id("objective-fact.prop-destroyed")),
                    PropCapabilities.RoomClear(true)
                });
        }

        private static PropDefinition SwitchDefinition()
        {
            return new PropDefinition(
                Id("prop.switch-terminal"),
                Id("presentation.switch-terminal"),
                new[]
                {
                    PropCapabilities.Collision(true),
                    PropCapabilities.Interactable(Id("fact.terminal-used")),
                    PropCapabilities.Switch(Id("switch.power-grid"), false),
                    PropCapabilities.Objective(Id("objective.power-restored"))
                });
        }

        private static PropDamageCommand Damage(
            string operationId,
            double amount)
        {
            return new PropDamageCommand(
                Id(operationId),
                Id("participant.player"),
                Id("faction.player"),
                Id("damage.kinetic"),
                amount);
        }

        private static PropCatalog Catalog(PropDefinition definition)
        {
            return new PropCatalog(
                PropCapabilityRegistry.CreateBuiltIns(),
                new[] { definition });
        }

        private static PropLiveCreationResult Create(
            PropDefinition definition,
            string placementId,
            IPropDamageEligibilityPolicy policy)
        {
            PropCatalog catalog = Catalog(definition);
            PropPlacement placement = new PropPlacement(
                PlacedObjectIdentity.CreateAuthored(Id(placementId)),
                definition.DefinitionId);
            return new PropLiveFactory().Create(
                catalog,
                placement,
                policy);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }
    }
}
#endif
