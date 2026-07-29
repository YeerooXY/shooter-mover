using System;
using NUnit.Framework;
using ShooterMover.Application.Missions.Results;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;

namespace ShooterMover.Tests.EditMode.Missions.Results
{
    public sealed partial class MissionRunResultStateTests
    {
        [Test]
        public void EndRun_WithZeroBoxes_FreezesVersionedEmptyPayloads()
        {
            FakeExistingStatePort port = new FakeExistingStatePort();
            MissionRunResultState authority = new MissionRunResultState(port);
            PlayerRouteProfilePayload route = CreateRoute("zero");

            MissionRunStateResult result = authority.EndRun(CreateEnd(
                "zero-end",
                "zero-run",
                route,
                MissionRunCompletionState.Completed,
                0L,
                port));

            Assert.That(result.Status, Is.EqualTo(MissionRunStateStatus.RunEnded));
            Assert.That(result.RunPayload.SchemaVersion, Is.EqualTo(MissionRunPayload.CurrentSchemaVersion));
            Assert.That(result.RunPayload.CollectedStrongboxes.Count, Is.Zero);
            Assert.That(result.RunPayload.RoutePayload, Is.SameAs(route));
            Assert.That(MissionRun.IsFingerprint(result.RunPayload.Fingerprint), Is.True);
            Assert.That(result.ResultPayload.Strongboxes.Count, Is.Zero);
            Assert.That(result.ResultPayload.UnopenedStrongboxes.Count, Is.Zero);
            Assert.That(result.ResultPayload.OpenedStrongboxes.Count, Is.Zero);
            Assert.That(result.ResultPayload.SchemaVersion, Is.EqualTo(MissionResultPayload.CurrentSchemaVersion));
            Assert.That(MissionRun.IsFingerprint(result.ResultPayload.Fingerprint), Is.True);
            Assert.That(authority.Sequence, Is.EqualTo(1L));
        }

        [Test]
        public void EndRun_WithOneUnopenedBox_PreservesExactInstanceIdentity()
        {
            FakeExistingStatePort port = new FakeExistingStatePort();
            MissionRunResultState authority = new MissionRunResultState(port);
            PlayerRouteProfilePayload route = CreateRoute("one");
            StableId instanceId = Id("box-instance", "one");

            MissionRunStateResult collected = authority.RecordCollectedStrongbox(
                CreateCollection("one-collect", "one-run", route, "standard", instanceId.Value, 0L, port));
            MissionRunStateResult ended = authority.EndRun(CreateEnd(
                "one-end",
                "one-run",
                route,
                MissionRunCompletionState.Completed,
                1L,
                port));

            Assert.That(collected.Status, Is.EqualTo(MissionRunStateStatus.StrongboxCollected));
            Assert.That(collected.RunPayload.CollectedStrongboxes.Count, Is.EqualTo(1));
            Assert.That(collected.RunPayload.CollectedStrongboxes[0].InstanceStableId, Is.EqualTo(instanceId));
            Assert.That(ended.RunPayload, Is.SameAs(collected.RunPayload));
            Assert.That(ended.ResultPayload.UnopenedStrongboxes.Count, Is.EqualTo(1));
            Assert.That(ended.ResultPayload.UnopenedStrongboxes[0].InstanceStableId, Is.EqualTo(instanceId));
            Assert.That(ended.ResultPayload.UnopenedStrongboxes[0].State, Is.EqualTo(MissionRunStrongboxState.Unopened));
        }

        [Test]
        public void EndRun_WithMultipleSameDefinitionBoxes_DoesNotCollapsePhysicalInstances()
        {
            FakeExistingStatePort port = new FakeExistingStatePort();
            MissionRunResultState authority = new MissionRunResultState(port);
            PlayerRouteProfilePayload route = CreateRoute("many");

            authority.RecordCollectedStrongbox(CreateCollection("collect-a", "many-run", route, "elite", "box-a", 0L, port));
            authority.RecordCollectedStrongbox(CreateCollection("collect-b", "many-run", route, "elite", "box-b", 1L, port));
            authority.RecordCollectedStrongbox(CreateCollection("collect-c", "many-run", route, "elite", "box-c", 2L, port));
            MissionRunStateResult ended = authority.EndRun(CreateEnd(
                "many-end",
                "many-run",
                route,
                MissionRunCompletionState.Completed,
                3L,
                port));

            Assert.That(ended.RunPayload.CollectedStrongboxes.Count, Is.EqualTo(3));
            Assert.That(ended.ResultPayload.UnopenedStrongboxes.Count, Is.EqualTo(3));
            Assert.That(ended.ResultPayload.UnopenedStrongboxes[0].DefinitionStableId,
                Is.EqualTo(ended.ResultPayload.UnopenedStrongboxes[1].DefinitionStableId));
            Assert.That(ended.ResultPayload.UnopenedStrongboxes[0].InstanceStableId,
                Is.Not.EqualTo(ended.ResultPayload.UnopenedStrongboxes[1].InstanceStableId));
            CollectionAssert.AreEquivalent(
                new[] { Id("box-instance", "box-a"), Id("box-instance", "box-b"), Id("box-instance", "box-c") },
                new[]
                {
                    ended.ResultPayload.UnopenedStrongboxes[0].InstanceStableId,
                    ended.ResultPayload.UnopenedStrongboxes[1].InstanceStableId,
                    ended.ResultPayload.UnopenedStrongboxes[2].InstanceStableId,
                });
        }

        [Test]
        public void RepeatedEndRun_ReturnsSameFrozenResultAndDoesNotReproject()
        {
            FakeExistingStatePort port = new FakeExistingStatePort();
            MissionRunResultState authority = new MissionRunResultState(port);
            PlayerRouteProfilePayload route = CreateRoute("repeat");
            authority.RecordCollectedStrongbox(CreateCollection(
                "repeat-collect", "repeat-run", route, "standard", "repeat-box", 0L, port));

            MissionRunStateResult first = authority.EndRun(CreateEnd(
                "repeat-end-a", "repeat-run", route, MissionRunCompletionState.Completed, 1L, port));
            int projectCalls = port.ProjectCalls;
            MissionRunStateResult second = authority.EndRun(CreateEnd(
                "repeat-end-b", "repeat-run", route, MissionRunCompletionState.Completed, 999L, port));

            Assert.That(second, Is.SameAs(first));
            Assert.That(second.RunPayload, Is.SameAs(first.RunPayload));
            Assert.That(second.ResultPayload, Is.SameAs(first.ResultPayload));
            Assert.That(authority.Sequence, Is.EqualTo(2L));
            Assert.That(port.ProjectCalls, Is.EqualTo(projectCalls));
        }

        [Test]
        public void ConflictingEndReplay_IsRejectedWithoutMutation()
        {
            FakeExistingStatePort port = new FakeExistingStatePort();
            MissionRunResultState authority = new MissionRunResultState(port);
            PlayerRouteProfilePayload route = CreateRoute("conflict");
            MissionRunStateResult first = authority.EndRun(CreateEnd(
                "conflict-end-a", "conflict-run", route, MissionRunCompletionState.Completed, 0L, port));

            MissionRunStateResult conflicting = authority.EndRun(CreateEnd(
                "conflict-end-b", "conflict-run", route, MissionRunCompletionState.Failed, 1L, port));

            Assert.That(first.Status, Is.EqualTo(MissionRunStateStatus.RunEnded));
            Assert.That(conflicting.Status, Is.EqualTo(MissionRunStateStatus.ConflictingDuplicate));
            Assert.That(authority.Sequence, Is.EqualTo(1L));
            MissionResultPayload stored;
            Assert.That(authority.TryGetResult(Id("run", "conflict-run"), out stored), Is.True);
            Assert.That(stored, Is.SameAs(first.ResultPayload));
        }

        [Test]
        public void StaleEndRequest_IsRejectedWithoutCreatingResult()
        {
            FakeExistingStatePort port = new FakeExistingStatePort();
            MissionRunResultState authority = new MissionRunResultState(port);
            PlayerRouteProfilePayload route = CreateRoute("stale");

            MissionRunStateResult stale = authority.EndRun(CreateEnd(
                "stale-end", "stale-run", route, MissionRunCompletionState.Completed, 5L, port));

            Assert.That(stale.Status, Is.EqualTo(MissionRunStateStatus.StaleInput));
            Assert.That(authority.Sequence, Is.Zero);
            Assert.That(port.ProjectCalls, Is.Zero);
            MissionResultPayload ignored;
            Assert.That(authority.TryGetResult(Id("run", "stale-run"), out ignored), Is.False);
        }

        [Test]
        public void ResultPreservesExactRoutePayloadObject()
        {
            FakeExistingStatePort port = new FakeExistingStatePort();
            MissionRunResultState authority = new MissionRunResultState(port);
            PlayerRouteProfilePayload route = CreateRoute("route");

            MissionRunStateResult ended = authority.EndRun(CreateEnd(
                "route-end", "route-run", route, MissionRunCompletionState.Abandoned, 0L, port));
            MissionResultsSession session = new MissionResultsSession(ended.ResultPayload);

            Assert.That(ended.ResultPayload.RoutePayload, Is.SameAs(route));
            Assert.That(session.RoutePayload, Is.SameAs(route));
            Assert.That(session.Snapshot.RoutePayload.Fingerprint, Is.EqualTo(route.Fingerprint));
            Assert.That(ended.RunPayload.RoutePayload, Is.SameAs(route));
            MissionRunPayload storedRun;
            Assert.That(authority.TryGetRun(Id("run", "route-run"), out storedRun), Is.True);
            Assert.That(storedRun, Is.SameAs(ended.RunPayload));
        }

        [Test]
        public void DuplicateWeaponDefinitionsRemainSeparateEquipmentInstancesOutsideResults()
        {
            FakeExistingStatePort port = new FakeExistingStatePort();
            MissionRunResultState authority = new MissionRunResultState(port);
            PlayerRouteProfilePayload route = CreateRoute("duplicates");
            MissionRunCollectStrongboxCommand boxA = CreateCollection(
                "dup-collect-a", "dup-run", route, "elite", "dup-box-a", 0L, port);
            MissionRunCollectStrongboxCommand boxB = CreateCollection(
                "dup-collect-b", "dup-run", route, "elite", "dup-box-b", 1L, port);
            authority.RecordCollectedStrongbox(boxA);
            authority.RecordCollectedStrongbox(boxB);
            port.States[boxA.InstanceStableId] = MissionRunStrongboxState.Opened;
            port.States[boxB.InstanceStableId] = MissionRunStrongboxState.Opened;

            EquipmentInstance rewardA = EquipmentInstance.Create(
                Id("equipment-instance", "reward-a"),
                Id("equipment", "photon-rifle"),
                10,
                Id("quality", "rare"),
                Array.Empty<AugmentInstance>());
            EquipmentInstance rewardB = EquipmentInstance.Create(
                Id("equipment-instance", "reward-b"),
                Id("equipment", "photon-rifle"),
                10,
                Id("quality", "rare"),
                Array.Empty<AugmentInstance>());
            port.OpenedEquipmentRewards.Add(rewardA);
            port.OpenedEquipmentRewards.Add(rewardB);

            MissionRunStateResult ended = authority.EndRun(CreateEnd(
                "dup-end", "dup-run", route, MissionRunCompletionState.Completed, 2L, port));
            MissionResultsSession session = new MissionResultsSession(ended.ResultPayload);

            Assert.That(session.OpenedStrongboxCount, Is.EqualTo(2));
            Assert.That(port.OpenedEquipmentRewards.Count, Is.EqualTo(2));
            Assert.That(port.OpenedEquipmentRewards[0].DefinitionId,
                Is.EqualTo(port.OpenedEquipmentRewards[1].DefinitionId));
            Assert.That(port.OpenedEquipmentRewards[0].InstanceId,
                Is.Not.EqualTo(port.OpenedEquipmentRewards[1].InstanceId));
            Assert.That(port.RewardGrantCalls, Is.Zero, "Results must not grant or recreate BOX rewards.");
        }

        [Test]
        public void ConflictingCollectionOperationReuse_IsRejectedWithoutMutation()
        {
            FakeExistingStatePort port = new FakeExistingStatePort();
            MissionRunResultState authority = new MissionRunResultState(port);
            PlayerRouteProfilePayload route = CreateRoute("collection-conflict");
            MissionRunCollectStrongboxCommand first = CreateCollection(
                "same-operation", "collection-run", route, "standard", "box-a", 0L, port);
            MissionRunCollectStrongboxCommand conflict = CreateCollection(
                "same-operation", "collection-run", route, "standard", "box-b", 1L, port);

            MissionRunStateResult applied = authority.RecordCollectedStrongbox(first);
            MissionRunStateResult rejected = authority.RecordCollectedStrongbox(conflict);

            Assert.That(applied.Status, Is.EqualTo(MissionRunStateStatus.StrongboxCollected));
            Assert.That(rejected.Status, Is.EqualTo(MissionRunStateStatus.ConflictingDuplicate));
            Assert.That(authority.Sequence, Is.EqualTo(1L));
        }
    }
}
