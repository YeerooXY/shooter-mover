#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.RunPickups;
using ShooterMover.TerminalDropBinding;

namespace ShooterMover.Tests.EditMode.RunPickups
{
    public sealed partial class RunLocalPickupStateTests
    {
        private sealed class Fixture
        {
            public Fixture(
                FakeRunSessionPort session,
                FakeSourcePositionPort position,
                RunLocalPickupState authority)
            {
                Session = session;
                Position = position;
                Authority = authority;
            }

            public FakeRunSessionPort Session { get; }
            public FakeSourcePositionPort Position { get; }
            public RunLocalPickupState Authority { get; }
        }

        private static Fixture CreateFixture()
        {
            var session = new FakeRunSessionPort();
            var position = new FakeSourcePositionPort();
            return new Fixture(
                session,
                position,
                new RunLocalPickupState(session, position));
        }

        private static RunPickupSnapshot RealizeOne(Fixture fixture)
        {
            return fixture.Authority.Realize(
                Batch(Child("money-a", RewardGrantKind.Money, "credits", 5L)))
                .Pickups.Single();
        }

        private static RunPickupCollectionCommand Command(
            RunPickupSnapshot pickup,
            StableId operationId = null,
            StableId runId = null,
            StableId childId = null,
            StableId collectorEntity = null,
            StableId collectorParticipant = null)
        {
            StableId entity = collectorEntity ?? PlayerActorId;
            StableId participant = collectorParticipant ?? PlayerParticipantId;
            return new RunPickupCollectionCommand(
                operationId ?? RunPickupIdentity.DeriveCollectionOperationStableId(
                    pickup.PickupStableId,
                    entity,
                    participant),
                pickup.PickupStableId,
                childId ?? pickup.Reward.RewardInstanceStableId,
                runId ?? pickup.Batch.RunStableId,
                pickup.Batch.RunLifecycleGeneration,
                entity,
                participant,
                pickup.Fingerprint);
        }

        private static RunPickupGeneratedReward Child(
            string instance,
            RewardGrantKind kind,
            string content,
            long quantity,
            int ordinal = 0)
        {
            return new RunPickupGeneratedReward(
                Id("terminaldropchild", instance),
                ordinal,
                Id("grant", "grant-" + instance),
                kind,
                Id("content", content),
                quantity,
                "generated-child-fingerprint:" + instance);
        }

        private static RunPickupGeneratedBatch Batch(
            params RunPickupGeneratedReward[] children)
        {
            return BatchForLifecycle(
                1L,
                children,
                "drop-operation-a",
                "batch-a");
        }

        private static RunPickupGeneratedBatch Batch(
            RunPickupGeneratedReward child,
            string operation,
            string fingerprint)
        {
            return BatchForLifecycle(
                1L,
                new[] { child },
                operation,
                fingerprint);
        }

        private static RunPickupGeneratedBatch Batch(
            RunPickupGeneratedReward[] children,
            string operation,
            string fingerprint)
        {
            return BatchForLifecycle(1L, children, operation, fingerprint);
        }

        private static RunPickupGeneratedBatch BatchForLifecycle(
            long lifecycleGeneration,
            RunPickupGeneratedReward[] children,
            string operation,
            string fingerprint)
        {
            return new RunPickupGeneratedBatch(
                Id("terminaldropoperation", operation),
                Id("terminal", "event-" + operation),
                Id("trigger", "event-" + operation),
                RunId,
                lifecycleGeneration,
                SourceEntityId,
                SourcePlacementId,
                3L,
                Id("definition", "source"),
                PlayerParticipantId,
                "generated-batch-fingerprint:" + fingerprint,
                children);
        }

        private static GeneratedTerminalDropResult GeneratedTerminalResult()
        {
            StableId operationId = Id("terminaldropoperation", "adapter-route");
            StableId profileId = Id("drop-profile", "adapter-route");
            var source = new TerminalDropSourceFact(
                TerminalDropFactKindIds.EnemyDeath,
                Id("terminal", "adapter-route"),
                Id("trigger", "adapter-route"),
                RunId,
                1L,
                SourceEntityId,
                SourcePlacementId,
                3L,
                Id("definition", "source"),
                PlayerParticipantId,
                PlayerActorId,
                Id("damage", "kinetic"),
                profileId,
                "source-context-fingerprint",
                "definition-fingerprint",
                "upstream-fingerprint");
            RewardOperationRequest operation = RewardOperationRequest.Create(
                RunId,
                SourceEntityId,
                operationId,
                Id("commitment", "adapter-route"),
                profileId,
                "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
            var child = new GeneratedTerminalDropReward(
                Id("terminaldropchild", "adapter-route"),
                0,
                Id("grant", "adapter-route"),
                RewardGrantKind.Money,
                Id("content", "credits"),
                5L);
            return new GeneratedTerminalDropResult(
                TerminalDropBindingStatus.Accepted,
                TerminalDropRejectionCode.None,
                source,
                profileId,
                operation,
                123UL,
                null,
                new[] { child },
                "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                string.Empty);
        }

        private static StableId Id(string scope, string value)
        {
            return StableId.Create(scope, value);
        }
    }
}
#endif
