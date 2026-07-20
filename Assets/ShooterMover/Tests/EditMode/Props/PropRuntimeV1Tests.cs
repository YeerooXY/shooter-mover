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
        public void ExplosiveBarrel_EmitsTerminalFactsOnce_AndReplayCannotDuplicateRewards()
        {
            PropDefinitionV1 definition = BarrelDefinition();
            RecordingDamagePolicy policy = new RecordingDamagePolicy(true);
            PropRuntimeV1 runtime = Create(
                definition,
                "placed.barrel-01",
                policy).Runtime;
            PropDamageCommandV1 command = Damage("operation.destroy-barrel", 100d);

            PropDamageResultV1 first = runtime.ApplyDamage(command);
            PropDamageResultV1 exactReplay = runtime.ApplyDamage(command);
            PropDamageResultV1 conflictingReplay = runtime.ApplyDamage(
                Damage("operation.destroy-barrel", 101d));

            Assert.That(first.Status, Is.EqualTo(PropDamageStatusV1.Destroyed));
            Assert.That(first.Facts.Terminal, Is.Not.Null);
            Assert.That(first.Facts.Explosion, Is.Not.Null);
            Assert.That(first.Facts.DropRequest, Is.Not.Null);
            Assert.That(first.Facts.Terminal.SourceParticipantId, Is.EqualTo(Id("participant.player")));
            Assert.That(first.Facts.DropRequest.SourceParticipantId, Is.EqualTo(Id("participant.player")));

            Assert.That(
                exactReplay.Status,
                Is.EqualTo(PropDamageStatusV1.DuplicateNoChange));
            Assert.That(exactReplay.Facts.IsEmpty, Is.True);
            Assert.That(
                conflictingReplay.Status,
                Is.EqualTo(PropDamageStatusV1.RejectedConflictingReplay));
            Assert.That(conflictingReplay.Facts.IsEmpty, Is.True);
            Assert.That(policy.CallCount, Is.EqualTo(1));
            Assert.That(runtime.Snapshot.IsTerminal, Is.True);
            Assert.That(runtime.Snapshot.BlocksRoomClear, Is.False);
        }

        [Test]
        public void SameDefinition_TwoPlacementsRemainDistinct()
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

            Assert.That(first.Placement.DefinitionId, Is.EqualTo(second.Placement.DefinitionId));
            Assert.That(first.ParticipantId, Is.Not.EqualTo(second.ParticipantId));
            Assert.That(first.Snapshot.Fingerprint, Is.Not.EqualTo(second.Snapshot.Fingerprint));
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
        public void SwitchInteraction_IsReplaySafeAndPlacementLocal()
        {
            PropDefinitionV1 definition = new PropDefinitionV1(
                Id("prop.switch-terminal"),
                Id("presentation.switch-terminal"),
                new[]
                {
                    PropCapabilitiesV1.Collision(true),
                    PropCapabilitiesV1.Interactable(Id("fact.terminal-used")),
                    PropCapabilitiesV1.Switch(Id("switch.power-grid"), false),
                    PropCapabilitiesV1.Objective(Id("objective.power-restored"))
                });
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
            PropInteractionResultV1 replay = first.Interact(command);

            Assert.That(applied.Status, Is.EqualTo(PropInteractionStatusV1.Applied));
            Assert.That(applied.Interaction, Is.Not.Null);
            Assert.That(applied.SwitchFact, Is.Not.Null);
            Assert.That(
                applied.SwitchFact.KindId,
                Is.EqualTo(Id("fact-kind.prop-switch-on")));
            Assert.That(applied.Objective, Is.Not.Null);
            Assert.That(first.Snapshot.SwitchActive, Is.True);
            Assert.That(second.Snapshot.SwitchActive, Is.False);
            Assert.That(
                replay.Status,
                Is.EqualTo(PropInteractionStatusV1.DuplicateNoChange));
            Assert.That(replay.Interaction, Is.Null);
            Assert.That(replay.SwitchFact, Is.Null);
            Assert.That(replay.Objective, Is.Null);
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
