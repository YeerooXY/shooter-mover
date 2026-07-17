using System;
using NUnit.Framework;
using ShooterMover.Application.Missions.Results;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;

namespace ShooterMover.Tests.EditMode.Missions.Results
{
    public sealed partial class MissionRunResultAuthorityV1Tests
    {
        [Test]
        public void EndRun_WithZeroBoxes_FreezesVersionedEmptyPayloads()
        {
            FakeExistingAuthorityPort port = new FakeExistingAuthorityPort();
            MissionRunResultAuthorityV1 authority = new MissionRunResultAuthorityV1(port);
            PlayerRouteProfilePayloadV1 route = CreateRoute("zero");

            MissionRunAuthorityResultV1 result = authority.EndRun(CreateEnd(
                "zero-end",
                "zero-run",
                route,
                MissionRunCompletionStateV1.Completed,
                0L,
                port));

            Assert.That(result.Status, Is.EqualTo(MissionRunAuthorityStatusV1.RunEnded));
            Assert.That(result.RunPayload.SchemaVersion, Is.EqualTo(MissionRunPayloadV1.CurrentSchemaVersion));
            Assert.That(result.RunPayload.CollectedStrongboxes.Count, Is.Zero);
            Assert.That(result.RunPayload.RoutePayload, Is.SameAs(route));
            Assert.That(MissionRunCanonicalV1.IsFingerprint(result.RunPayload.Fingerprint), Is.True);
            Assert.That(result.ResultPayload.Strongboxes.Count, Is.Zero);
            Assert.That(result.ResultPayload.UnopenedStrongboxes.Count, Is.Zero);
            Assert.That(result.ResultPayload.OpenedStrongboxes.Count, Is.Zero);
            Assert.That(result.ResultPayload.SchemaVersion, Is.EqualTo(MissionResultPayloadV1.CurrentSchemaVersion));
            Assert.That(MissionRunCanonicalV1.IsFingerprint(result.ResultPayload.Fingerprint), Is.True);
            Assert.That(authority.Sequence, Is.EqualTo(1L));
        }

        [Test]
        public void EndRun_WithOneUnopenedBox_PreservesExactInstanceIdentity()
        {
            FakeExistingAuthorityPort port = new FakeExistingAuthorityPort();
            MissionRunResultAuthorityV1 authority = new MissionRunResultAuthorityV1(port);
            PlayerRouteProfilePayloadV1 route = CreateRoute("one");
            StableId instanceId = Id("box-instance", "one");

            MissionRunAuthorityResultV1 collected = authority.RecordCollectedStrongbox(
                CreateCollection("one-collect", "one-run", route, "standard", instanceId.Value, 0L, port));
            MissionRunAuthorityResultV1 ended = authority.EndRun(CreateEnd(
                "one-end",
                "one-run",
                route,
                MissionRunCompletionStateV1.Completed,
                1L,
                port));

            Assert.That(collected.Status, Is.EqualTo(MissionRunAuthorityStatusV1.StrongboxCollected));
            Assert.That(collected.RunPayload.CollectedStrongboxes.Count, Is.EqualTo(1));
            Assert.That(collected.RunPayload.CollectedStrongboxes[0].InstanceStableId, Is.EqualTo(instanceId));
            Assert.That(ended.RunPayload, Is.SameAs(collected.RunPayload));
            Assert.That(ended.ResultPayload.UnopenedStrongboxes.Count, Is.EqualTo(1));
            Assert.That(ended.ResultPayload.UnopenedStrongboxes[0].InstanceStableId, Is.EqualTo(instanceId));
            Assert.That(ended.ResultPayload.UnopenedStrongboxes[0].State, Is.EqualTo(MissionRunStrongboxStateV1.Unopened));
        }

        [Test]
        public void EndRun_WithMultipleSameDefinitionBoxes_DoesNotCollapsePhysicalInstances()
        {
            FakeExistingAuthorityPort port = new FakeExistingAuthorityPort();
            MissionRunResultAuthorityV1 authority = new MissionRunResultAuthorityV1(port);
            PlayerRouteProfilePayloadV1 route = CreateRoute("many");

            authority.RecordCollectedStrongbox(CreateCollection("collect-a", "many-run", route, "elite", "box-a", 0L, port));
            authority.RecordCollectedStrongbox(CreateCollection("collect-b", "many-run", route, "elite", "box-b", 1L, port));
            authority.RecordCollectedStrongbox(CreateCollection("collect-c", "many-run", route, "elite", "box-c", 2L, port));
            MissionRunAuthorityResultV1 ended = authority.EndRun(CreateEnd(
                "many-end",
                "many-run",
                route,
                MissionRunCompletionStateV1.Completed,
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
            FakeExistingAuthorityPort port = new FakeExistingAuthorityPort();
            MissionRunResultAuthorityV1 authority = new MissionRunResultAuthorityV1(port);
            PlayerRouteProfilePayloadV1 route = CreateRoute("repeat");
            authority.RecordCollectedStrongbox(CreateCollection(
                "repeat-collect", "repeat-run", route, "standard", "repeat-box", 0L, port));

            MissionRunAuthorityResultV1 first = authority.EndRun(CreateEnd(
                "repeat-end-a", "repeat-run", route, MissionRunCompletionStateV1.Completed, 1L, port));
            int projectCalls = port.ProjectCalls;
            MissionRunAuthorityResultV1 second = authority.EndRun(CreateEnd(
                "repeat-end-b", "repeat-run", route, MissionRunCompletionStateV1.Completed, 999L, port));

            Assert.That(second, Is.SameAs(first));
            Assert.That(second.RunPayload, Is.SameAs(first.RunPayload));
            Assert.That(second.ResultPayload, Is.SameAs(first.ResultPayload));
            Assert.That(authority.Sequence, Is.EqualTo(2L));
            Assert.That(port.ProjectCalls, Is.EqualTo(projectCalls));
        }

        [Test]
        public void ConflictingEndReplay_IsRejectedWithoutMutation()
        {
            FakeExistingAuthorityPort port = new FakeExistingAuthorityPort();
            MissionRunResultAuthorityV1 authority = new MissionRunResultAuthorityV1(port);
            PlayerRouteProfilePayloadV1 route = CreateRoute("conflict");
            MissionRunAuthorityResultV1 first = authority.EndRun(CreateEnd(
                "conflict-end-a", "conflict-run", route, MissionRunCompletionStateV1.Completed, 0L, port));

            MissionRunAuthorityResultV1 conflicting = authority.EndRun(CreateEnd(
                "conflict-end-b", "conflict-run", route, MissionRunCompletionStateV1.Failed, 1L, port));

            Assert.That(first.Status, Is.EqualTo(MissionRunAuthorityStatusV1.RunEnded));
            Assert.That(conflicting.Status, Is.EqualTo(MissionRunAuthorityStatusV1.ConflictingDuplicate));
            Assert.That(authority.Sequence, Is.EqualTo(1L));
            MissionResultPayloadV1 stored;
            Assert.That(authority.TryGetResult(Id("run", "conflict-run"), out stored), Is.True);
            Assert.That(stored, Is.SameAs(first.ResultPayload));
        }

        [Test]
        public void StaleEndRequest_IsRejectedWithoutCreatingResult()
        {
            FakeExistingAuthorityPort port = new FakeExistingAuthorityPort();
            MissionRunResultAuthorityV1 authority = new MissionRunResultAuthorityV1(port);
            PlayerRouteProfilePayloadV1 route = CreateRoute("stale");

            MissionRunAuthorityResultV1 stale = authority.EndRun(CreateEnd(
                "stale-end", "stale-run", route, MissionRunCompletionStateV1.Completed, 5L, port));

            Assert.That(stale.Status, Is.EqualTo(MissionRunAuthorityStatusV1.StaleInput));
            Assert.That(authority.Sequence, Is.Zero);
            Assert.That(port.ProjectCalls, Is.Zero);
            MissionResultPayloadV1 ignored;
            Assert.That(authority.TryGetResult(Id("run", "stale-run"), out ignored), Is.False);
        }

        [Test]
        public void ResultPreservesExactRoutePayloadObject()
        {
            FakeExistingAuthorityPort port = new FakeExistingAuthorityPort();
            MissionRunResultAuthorityV1 authority = new MissionRunResultAuthorityV1(port);
            PlayerRouteProfilePayloadV1 route = CreateRoute("route");

            MissionRunAuthorityResultV1 ended = authority.EndRun(CreateEnd(
                "route-end", "route-run", route, MissionRunCompletionStateV1.Abandoned, 0L, port));
            MissionResultsSessionV1 session = new MissionResultsSessionV1(ended.ResultPayload);

            Assert.That(ended.ResultPayload.RoutePayload, Is.SameAs(route));
            Assert.That(session.RoutePayload, Is.SameAs(route));
            Assert.That(session.Snapshot.RoutePayload.Fingerprint, Is.EqualTo(route.Fingerprint));
            Assert.That(ended.RunPayload.RoutePayload, Is.SameAs(route));
            MissionRunPayloadV1 storedRun;
            Assert.That(authority.TryGetRun(Id("run", "route-run"), out storedRun), Is.True);
            Assert.That(storedRun, Is.SameAs(ended.RunPayload));
        }

        [Test]
        public void DuplicateWeaponDefinitionsRemainSeparateEquipmentInstancesOutsideResults()
        {
            FakeExistingAuthorityPort port = new FakeExistingAuthorityPort();
            MissionRunResultAuthorityV1 authority = new MissionRunResultAuthorityV1(port);
            PlayerRouteProfilePayloadV1 route = CreateRoute("duplicates");
            MissionRunCollectStrongboxCommandV1 boxA = CreateCollection(
                "dup-collect-a", "dup-run", route, "elite", "dup-box-a", 0L, port);
            MissionRunCollectStrongboxCommandV1 boxB = CreateCollection(
                "dup-collect-b", "dup-run", route, "elite", "dup-box-b", 1L, port);
            authority.RecordCollectedStrongbox(boxA);
            authority.RecordCollectedStrongbox(boxB);
            port.States[boxA.InstanceStableId] = MissionRunStrongboxStateV1.Opened;
            port.States[boxB.InstanceStableId] = MissionRunStrongboxStateV1.Opened;

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

            MissionRunAuthorityResultV1 ended = authority.EndRun(CreateEnd(
                "dup-end", "dup-run", route, MissionRunCompletionStateV1.Completed, 2L, port));
            MissionResultsSessionV1 session = new MissionResultsSessionV1(ended.ResultPayload);

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
            FakeExistingAuthorityPort port = new FakeExistingAuthorityPort();
            MissionRunResultAuthorityV1 authority = new MissionRunResultAuthorityV1(port);
            PlayerRouteProfilePayloadV1 route = CreateRoute("collection-conflict");
            MissionRunCollectStrongboxCommandV1 first = CreateCollection(
                "same-operation", "collection-run", route, "standard", "box-a", 0L, port);
            MissionRunCollectStrongboxCommandV1 conflict = CreateCollection(
                "same-operation", "collection-run", route, "standard", "box-b", 1L, port);

            MissionRunAuthorityResultV1 applied = authority.RecordCollectedStrongbox(first);
            MissionRunAuthorityResultV1 rejected = authority.RecordCollectedStrongbox(conflict);

            Assert.That(applied.Status, Is.EqualTo(MissionRunAuthorityStatusV1.StrongboxCollected));
            Assert.That(rejected.Status, Is.EqualTo(MissionRunAuthorityStatusV1.ConflictingDuplicate));
            Assert.That(authority.Sequence, Is.EqualTo(1L));
        }
    }
}
