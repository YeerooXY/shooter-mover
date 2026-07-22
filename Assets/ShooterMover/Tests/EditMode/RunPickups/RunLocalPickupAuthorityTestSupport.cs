#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.RunPickups;
using ShooterMover.TerminalDropBinding;

namespace ShooterMover.Tests.EditMode.RunPickups
{
    public sealed partial class RunLocalPickupAuthorityV1Tests
    {
        private sealed class Fixture
        {
            public Fixture(
                FakeRunSessionPort session,
                FakeSourcePositionPort position,
                RunLocalPickupAuthorityV1 authority)
            {
                Session = session;
                Position = position;
                Authority = authority;
            }

            public FakeRunSessionPort Session { get; }
            public FakeSourcePositionPort Position { get; }
            public RunLocalPickupAuthorityV1 Authority { get; }
        }

        private static Fixture CreateFixture()
        {
            var session = new FakeRunSessionPort();
            var position = new FakeSourcePositionPort();
            return new Fixture(
                session,
                position,
                new RunLocalPickupAuthorityV1(session, position));
        }

        private static RunPickupSnapshotV1 RealizeOne(Fixture fixture)
        {
            return fixture.Authority.Realize(
                Batch(Child("money-a", RewardGrantKindV1.Money, "credits", 5L)))
                .Pickups.Single();
        }

        private static RunPickupCollectionCommandV1 Command(
            RunPickupSnapshotV1 pickup,
            StableId operationId = null,
            StableId runId = null,
            StableId childId = null,
            StableId collectorEntity = null,
            StableId collectorParticipant = null)
        {
            StableId entity = collectorEntity ?? PlayerActorId;
            StableId participant = collectorParticipant ?? PlayerParticipantId;
            return new RunPickupCollectionCommandV1(
                operationId ?? RunPickupIdentityV1.DeriveCollectionOperationStableId(
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

        private static RunPickupGeneratedRewardV1 Child(
            string instance,
            RewardGrantKindV1 kind,
            string content,
            long quantity,
            int ordinal = 0)
        {
            return new RunPickupGeneratedRewardV1(
                Id("terminaldropchild", instance),
                ordinal,
                Id("grant", "grant-" + instance),
                kind,
                Id("content", content),
                quantity,
                "generated-child-fingerprint:" + instance);
        }

        private static RunPickupGeneratedBatchV1 Batch(
            params RunPickupGeneratedRewardV1[] children)
        {
            return BatchForLifecycle(
                1L,
                children,
                "drop-operation-a",
                "batch-a");
        }

        private static RunPickupGeneratedBatchV1 Batch(
            RunPickupGeneratedRewardV1 child,
            string operation,
            string fingerprint)
        {
            return BatchForLifecycle(
                1L,
                new[] { child },
                operation,
                fingerprint);
        }

        private static RunPickupGeneratedBatchV1 Batch(
            RunPickupGeneratedRewardV1[] children,
            string operation,
            string fingerprint)
        {
            return BatchForLifecycle(1L, children, operation, fingerprint);
        }

        private static RunPickupGeneratedBatchV1 BatchForLifecycle(
            long lifecycleGeneration,
            RunPickupGeneratedRewardV1[] children,
            string operation,
            string fingerprint)
        {
            return new RunPickupGeneratedBatchV1(
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

        private static GeneratedTerminalDropResultV1 GeneratedTerminalResult()
        {
            StableId operationId = Id("terminaldropoperation", "adapter-route");
            StableId profileId = Id("drop-profile", "adapter-route");
            var source = new TerminalDropSourceFactV1(
                TerminalDropFactKindIdsV1.EnemyDeath,
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
            RewardOperationRequestV1 operation = RewardOperationRequestV1.Create(
                RunId,
                SourceEntityId,
                operationId,
                Id("commitment", "adapter-route"),
                profileId,
                "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
            var child = new GeneratedTerminalDropRewardV1(
                Id("terminaldropchild", "adapter-route"),
                0,
                Id("grant", "adapter-route"),
                RewardGrantKindV1.Money,
                Id("content", "credits"),
                5L);
            return new GeneratedTerminalDropResultV1(
                TerminalDropBindingStatusV1.Accepted,
                TerminalDropRejectionCodeV1.None,
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
