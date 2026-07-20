#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Domain.Authoring;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Props;

namespace ShooterMover.Tests.EditMode.Props
{
    public sealed class PropRuntimeV1Tests
    {
        private sealed class RecordingDamagePolicy : IPropDamageEligibilityPolicyV1
        {
            private readonly bool _answer;

            public RecordingDamagePolicy(bool answer)
            {
                _answer = answer;
            }

            public int CallCount { get; private set; }

            public PropDamageEligibilityContextV1 LastContext { get; private set; }

            public bool CanDamage(PropDamageEligibilityContextV1 context)
            {
                CallCount++;
                LastContext = context;
                return _answer;
            }
        }

        [Test]
        public void DecorativeProp_CreatesNoCombatAuthority()
        {
            PropDefinitionV1 definition = new PropDefinitionV1(
                Id("prop.decorative-console"),
                Id("presentation.decorative-console"),
                new[]
                {
                    PropCapabilitiesV1.Collision(false),
                    PropCapabilitiesV1.Decorative()
                });
            PropRuntimeCreationResultV1 created = Create(
                definition,
                "placed.decorative-console",
                null);

            Assert.That(created.IsCreated, Is.True);
            Assert.That(created.Runtime.Snapshot.HasCombatAuthority, Is.False);
            Assert.That(created.Runtime.Snapshot.CurrentHealth, Is.Null);

            PropDamageResultV1 damage = created.Runtime.ApplyDamage(
                Damage("operation.decorative-hit", 10d));
            Assert.That(
                damage.Status,
                Is.EqualTo(PropDamageStatusV1.RejectedNoCombatAuthority));
            Assert.That(damage.Facts.IsEmpty, Is.True);
        }

        [Test]
        public void RepeatedDefinitionPlacements_RetainIndependentHealth()
        {
            PropDefinitionV1 definition = CoverDefinition();
            RecordingDamagePolicy policy = new RecordingDamagePolicy(true);
            PropRuntimeV1 first = Create(
                definition,
                "placed.cover-a",
                policy).Runtime;
            PropRuntimeV1 second = Create(
                definition,
                "placed.cover-b",
                policy).Runtime;

            PropDamageResultV1 result = first.ApplyDamage(
                Damage("operation.cover-a-hit", 25d));

            Assert.That(result.Status, Is.EqualTo(PropDamageStatusV1.Applied));
            Assert.That(first.Snapshot.CurrentHealth, Is.EqualTo(75d));
            Assert.That(second.Snapshot.CurrentHealth, Is.EqualTo(100d));
            Assert.That(first.ParticipantId, Is.Not.EqualTo(second.ParticipantId));
            Assert.That(first.Snapshot.Fingerprint, Is.Not.EqualTo(second.Snapshot.Fingerprint));
        }

        [Test]
        public void LethalDamage_ExactReplayRecoversOriginalFacts_AndMutatesOnlyOnce()
        {
            PropDefinitionV1 definition = BarrelDefinition();
            RecordingDamagePolicy policy = new RecordingDamagePolicy(true);
            PropRuntimeV1 runtime = Create(
                definition,
                "placed.barrel-01",
                policy).Runtime;
            PropDamageCommandV1 command = Damage("operation.destroy-barrel", 100d);

            PropDamageResultV1 first = runtime.ApplyDamage(command);
            PropRuntimeSnapshotV1 terminalSnapshot = runtime.Snapshot;
            PropDamageResultV1 exactReplay = runtime.ApplyDamage(command);

            Assert.That(first.Status, Is.EqualTo(PropDamageStatusV1.Destroyed));
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
            PropRuntimeV1 runtime = Create(
                BarrelDefinition(),
                "placed.lost-response-barrel",
                policy).Runtime;
            PropDamageCommandV1 command = Damage(
                "operation.lost-destroy-response",
                100d);

            runtime.ApplyDamage(command);
            PropDamageResultV1 recovered = runtime.ApplyDamage(command);

            Assert.That(recovered.Status, Is.EqualTo(PropDamageStatusV1.Destroyed));
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
            PropRuntimeV1 runtime = Create(
                BarrelDefinition(),
                "placed.conflicting-barrel",
                policy).Runtime;
            PropDamageCommandV1 accepted = Damage(
                "operation.conflicting-destroy",
                100d);

            runtime.ApplyDamage(accepted);
            string terminalFingerprint = runtime.Snapshot.Fingerprint;
            PropDamageResultV1 conflicting = runtime.ApplyDamage(
                Damage("operation.conflicting-destroy", 101d));

            Assert.That(
                conflicting.Status,
                Is.EqualTo(PropDamageStatusV1.RejectedConflictingReplay));
            Assert.That(conflicting.Facts.IsEmpty, Is.True);
            Assert.That(runtime.Snapshot.Fingerprint, Is.EqualTo(terminalFingerprint));
            Assert.That(runtime.Snapshot.CurrentHealth, Is.EqualTo(0d));
            Assert.That(runtime.Snapshot.IsTerminal, Is.True);
            Assert.That(policy.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void AnalogousDamageOperations_AcrossPlacementsEmitPlacementDistinctFactIds()
        {
            PropDefinitionV1 definition = BarrelDefinition();
            RecordingDamagePolicy policy = new RecordingDamagePolicy(true);
            PropRuntimeV1 first = Create(
                definition,
                "placed.barrel-a",
                policy).Runtime;
            PropRuntimeV1 second = Create(
                definition,
                "placed.barrel-b",
                policy).Runtime;
            PropDamageCommandV1 command = Damage(
                "operation.analogous-destroy",
                100d);

            PropDamageResultV1 firstResult = first.ApplyDamage(command);
            PropDamageResultV1 secondResult = second.ApplyDamage(command);

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
            PropRuntimeV1 runtime = Create(
                CoverDefinition(),
                "placed.fire-resistant-cover",
                policy).Runtime;

            PropDamageResultV1 result = runtime.ApplyDamage(
                new PropDamageCommandV1(
                    Id("operation.fire-hit"),
                    Id("participant.player"),
                    Id("faction.player"),
                    Id("damage.fire"),
                    20d));

            Assert.That(result.Status, Is.EqualTo(PropDamageStatusV1.Applied));
            Assert.That(result.AppliedDamage, Is.EqualTo(10d));
            Assert.That(runtime.Snapshot.CurrentHealth, Is.EqualTo(90d));
        }

        [Test]
        public void IndestructibleProp_RejectsDamageWithoutOwningHealth()
        {
            PropDefinitionV1 definition = new PropDefinitionV1(
                Id("prop.indestructible-cover"),
                Id("presentation.indestructible-cover"),
                new[]
                {
                    PropCapabilitiesV1.Collision(true),
                    PropCapabilitiesV1.Indestructible(),
                    PropCapabilitiesV1.DamageBehavior(
                        PropDamageAlignmentV1.Neutral,
                        Id("damage-policy.player-normal"))
                });
            RecordingDamagePolicy policy = new RecordingDamagePolicy(true);
            PropRuntimeV1 runtime = Create(
                definition,
                "placed.indestructible-cover",
                policy).Runtime;

            PropDamageResultV1 result = runtime.ApplyDamage(
                Damage("operation.indestructible-hit", 25d));

            Assert.That(
                result.Status,
                Is.EqualTo(PropDamageStatusV1.RejectedIndestructible));
            Assert.That(runtime.Snapshot.MaximumHealth, Is.Null);
            Assert.That(runtime.Snapshot.CurrentHealth, Is.Null);
            Assert.That(policy.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void UnknownCapability_AndInvalidCombination_Reject()
        {
            PropCapabilityV1 unknown = new PropCapabilityV1(
                Id("capability.prop-unknown-mechanic"),
                new KeyValuePair<string, string>[0]);
            PropDefinitionV1 unknownDefinition = new PropDefinitionV1(
                Id("prop.unknown"),
                Id("presentation.unknown"),
                new[] { unknown });

            PropCatalogValidationException unknownException =
                Assert.Throws<PropCatalogValidationException>(
                    () => Catalog(unknownDefinition));
            Assert.That(unknownException.Message, Does.Contain("Unknown prop capability"));

            PropDefinitionV1 invalidExplosion = new PropDefinitionV1(
                Id("prop.invalid-explosion"),
                Id("presentation.invalid-explosion"),
                new[]
                {
                    PropCapabilitiesV1.Collision(true),
                    PropCapabilitiesV1.Indestructible(),
                    PropCapabilitiesV1.DamageBehavior(
                        PropDamageAlignmentV1.Neutral,
                        Id("damage-policy.player-normal")),
                    PropCapabilitiesV1.ExplodeOnDestroy(
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
            PropDefinitionV1 definition = CoverDefinition();
            RecordingDamagePolicy policy = new RecordingDamagePolicy(false);
            PropRuntimeV1 runtime = Create(
                definition,
                "placed.policy-cover",
                policy).Runtime;

            PropDamageResultV1 result = runtime.ApplyDamage(
                Damage("operation.policy-hit", 30d));

            Assert.That(result.Status, Is.EqualTo(PropDamageStatusV1.RejectedByPolicy));
            Assert.That(runtime.Snapshot.CurrentHealth, Is.EqualTo(100d));
            Assert.That(policy.CallCount, Is.EqualTo(1));
            Assert.That(policy.LastContext.TargetParticipantId, Is.EqualTo(runtime.ParticipantId));
            Assert.That(
                policy.LastContext.PolicyId,
                Is.EqualTo(Id("damage-policy.player-normal")));
            Assert.That(
                policy.LastContext.TargetAlignment,
                Is.EqualTo(PropDamageAlignmentV1.Neutral));
        }

        [Test]
        public void SwitchInteraction_ExactReplayRecoversFacts_AndTogglesOnlyOnce()
        {
            PropDefinitionV1 definition = SwitchDefinition();
            PropRuntimeV1 first = Create(
                definition,
                "placed.switch-a",
                null).Runtime;
            PropRuntimeV1 second = Create(
                definition,
                "placed.switch-b",
                null).Runtime;
            PropInteractionCommandV1 command = new PropInteractionCommandV1(
                Id("operation.use-switch-a"),
                Id("participant.player"));

            PropInteractionResultV1 applied = first.Interact(command);
            string toggledFingerprint = first.Snapshot.Fingerprint;
            PropInteractionResultV1 replay = first.Interact(command);

            Assert.That(applied.Status, Is.EqualTo(PropInteractionStatusV1.Applied));
            AssertCompleteInteractionFacts(applied);
            Assert.That(
                applied.SwitchFact.KindId,
                Is.EqualTo(PropFactKindIdsV1.SwitchOn));
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
            PropRuntimeV1 runtime = Create(
                SwitchDefinition(),
                "placed.switch-lost-response",
                null).Runtime;
            PropInteractionCommandV1 command = new PropInteractionCommandV1(
                Id("operation.lost-switch-response"),
                Id("participant.player"));

            runtime.Interact(command);
            PropInteractionResultV1 recovered = runtime.Interact(command);

            Assert.That(recovered.Status, Is.EqualTo(PropInteractionStatusV1.Applied));
            AssertCompleteInteractionFacts(recovered);
            Assert.That(runtime.Snapshot.SwitchActive, Is.True);
        }

        [Test]
        public void CatalogFingerprint_DoesNotDependOnDefinitionOrder()
        {
            PropDefinitionV1 cover = CoverDefinition();
            PropDefinitionV1 barrel = BarrelDefinition();

            PropCatalogV1 first = new PropCatalogV1(
                PropCapabilityRegistryV1.CreateBuiltIns(),
                new[] { cover, barrel });
            PropCatalogV1 reordered = new PropCatalogV1(
                PropCapabilityRegistryV1.CreateBuiltIns(),
                new[] { barrel, cover });

            Assert.That(first.Fingerprint, Is.EqualTo(reordered.Fingerprint));
        }

        private static void AssertCompleteDamageFacts(PropFactBatchV1 facts)
        {
            Assert.That(facts.Terminal, Is.Not.Null);
            Assert.That(facts.Explosion, Is.Not.Null);
            Assert.That(facts.DropRequest, Is.Not.Null);
            Assert.That(facts.Objective, Is.Not.Null);
            Assert.That(facts.Terminal.KindId, Is.EqualTo(PropFactKindIdsV1.Terminal));
            Assert.That(
                facts.Explosion.KindId,
                Is.EqualTo(PropFactKindIdsV1.ExplosionRequest));
            Assert.That(
                facts.DropRequest.KindId,
                Is.EqualTo(PropFactKindIdsV1.DropRequest));
            Assert.That(
                facts.Objective.KindId,
                Is.EqualTo(PropFactKindIdsV1.ObjectiveOnDestroy));
        }

        private static void AssertDamageFactsEquivalent(
            PropFactBatchV1 expected,
            PropFactBatchV1 actual)
        {
            Assert.That(actual.Terminal.FactId, Is.EqualTo(expected.Terminal.FactId));
            Assert.That(actual.Terminal.Fingerprint, Is.EqualTo(expected.Terminal.Fingerprint));
            AssertTriggeredFactsEquivalent(expected.Explosion, actual.Explosion);
            AssertTriggeredFactsEquivalent(expected.DropRequest, actual.DropRequest);
            AssertTriggeredFactsEquivalent(expected.Objective, actual.Objective);
        }

        private static void AssertCompleteInteractionFacts(
            PropInteractionResultV1 result)
        {
            Assert.That(result.Interaction, Is.Not.Null);
            Assert.That(result.SwitchFact, Is.Not.Null);
            Assert.That(result.Objective, Is.Not.Null);
            Assert.That(
                result.Interaction.KindId,
                Is.EqualTo(PropFactKindIdsV1.Interaction));
            Assert.That(
                result.Objective.KindId,
                Is.EqualTo(PropFactKindIdsV1.ObjectiveOnInteraction));
        }

        private static void AssertTriggeredFactsEquivalent(
            PropTriggeredFactV1 expected,
            PropTriggeredFactV1 actual)
        {
            Assert.That(actual.FactId, Is.EqualTo(expected.FactId));
            Assert.That(actual.KindId, Is.EqualTo(expected.KindId));
            Assert.That(actual.ProfileOrFactId, Is.EqualTo(expected.ProfileOrFactId));
            Assert.That(actual.Fingerprint, Is.EqualTo(expected.Fingerprint));
        }

        private static PropDefinitionV1 CoverDefinition()
        {
            return new PropDefinitionV1(
                Id("prop.cover-standard"),
                Id("presentation.cover-standard"),
                new[]
                {
                    PropCapabilitiesV1.Collision(true),
                    PropCapabilitiesV1.HealthBased(100d),
                    PropCapabilitiesV1.DamageBehavior(
                        PropDamageAlignmentV1.Neutral,
                        Id("damage-policy.player-normal")),
                    PropCapabilitiesV1.DamageResistance(
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

        private static PropDefinitionV1 BarrelDefinition()
        {
            return new PropDefinitionV1(
                Id("prop.barrel-explosive"),
                Id("presentation.barrel-explosive"),
                new[]
                {
                    PropCapabilitiesV1.Collision(true),
                    PropCapabilitiesV1.HealthBased(50d),
                    PropCapabilitiesV1.DamageBehavior(
                        PropDamageAlignmentV1.Hostile,
                        Id("damage-policy.player-normal")),
                    PropCapabilitiesV1.ExplodeOnDestroy(
                        Id("explosion-profile.barrel")),
                    PropCapabilitiesV1.DropOnDestroy(
                        Id("drop-profile.barrel")),
                    PropCapabilitiesV1.Objective(
                        Id("objective-fact.prop-destroyed")),
                    PropCapabilitiesV1.RoomClear(true)
                });
        }

        private static PropDefinitionV1 SwitchDefinition()
        {
            return new PropDefinitionV1(
                Id("prop.switch-terminal"),
                Id("presentation.switch-terminal"),
                new[]
                {
                    PropCapabilitiesV1.Collision(true),
                    PropCapabilitiesV1.Interactable(Id("fact.terminal-used")),
                    PropCapabilitiesV1.Switch(Id("switch.power-grid"), false),
                    PropCapabilitiesV1.Objective(Id("objective.power-restored"))
                });
        }

        private static PropDamageCommandV1 Damage(
            string operationId,
            double amount)
        {
            return new PropDamageCommandV1(
                Id(operationId),
                Id("participant.player"),
                Id("faction.player"),
                Id("damage.kinetic"),
                amount);
        }

        private static PropCatalogV1 Catalog(PropDefinitionV1 definition)
        {
            return new PropCatalogV1(
                PropCapabilityRegistryV1.CreateBuiltIns(),
                new[] { definition });
        }

        private static PropRuntimeCreationResultV1 Create(
            PropDefinitionV1 definition,
            string placementId,
            IPropDamageEligibilityPolicyV1 policy)
        {
            PropCatalogV1 catalog = Catalog(definition);
            PropPlacementV1 placement = new PropPlacementV1(
                PlacedObjectIdentity.CreateAuthored(Id(placementId)),
                definition.DefinitionId);
            return new PropRuntimeFactoryV1().Create(
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
