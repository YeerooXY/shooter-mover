using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Development.RunDebug;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Missions.Results;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.UnityAdapters.Authoring;
using ShooterMover.UnityAdapters.Development.RunDebug;
using ShooterMover.UnityAdapters.Rewards.Pickups;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Development.RunDebug
{
    public sealed class RunDebugPhysicalPickupTests
    {
        private readonly List<UnityEngine.Object> created =
            new List<UnityEngine.Object>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int index = created.Count - 1; index >= 0; index--)
            {
                if (created[index] != null) UnityEngine.Object.Destroy(created[index]);
            }

            created.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator PhysicalPickupFlowsThroughRapHoldingsRunAndResults()
        {
            StableId runId = Id("run", "dev-physical");
            PlayerRouteProfilePayloadV1 route = Route("dev-physical");
            GameplaySceneScope2D scope = CreateScope(runId);
            PlayerHoldingsService holdings = new PlayerHoldingsService(
                Id("authority", "holdings"),
                9999L,
                new AcceptingEquipmentValidator());
            RecordingChildAuthority money =
                new RecordingChildAuthority(Id("authority", "money"));
            RecordingChildAuthority scrap =
                new RecordingChildAuthority(Id("authority", "scrap"));
            var holdingsChild =
                new PlayerHoldingsRewardChildAuthorityV1(
                    holdings,
                    new AcceptingEquipmentValidator());
            RewardApplicationServiceV1 rap = new RewardApplicationServiceV1(
                Id("authority", "rap"),
                money,
                scrap,
                holdingsChild);

            GameObject authorityObject = Track(new GameObject("RAP"));
            RewardPickupApplicationAuthority2D pickupAuthority =
                authorityObject.AddComponent<RewardPickupApplicationAuthority2D>();
            pickupAuthority.ConfigureRuntime(
                rap,
                money.AuthorityStableId,
                scrap.AuthorityStableId,
                holdings.AuthorityStableId);

            GameObject factoryObject = Track(new GameObject("Factory"));
            factoryObject.transform.SetParent(scope.transform);
            RewardPickupDropFactory2D factory =
                factoryObject.AddComponent<RewardPickupDropFactory2D>();
            factory.ConfigureRuntime(
                new RewardGenerationServiceV1(),
                ProgressionContext.Create(
                    10,
                    10,
                    Id("difficulty", "normal"),
                    1),
                123UL,
                1,
                pickupAuthority,
                scope);

            StrongboxOpeningSnapshotV1 openings =
                StrongboxOpeningSnapshotV1.CreateCanonical(
                    ShooterMover.Domain.Rewards.Strongboxes.StrongboxCanonicalV1.Fingerprint(
                        "debug-empty-catalog"),
                    0L,
                    Array.Empty<ShooterMover.Domain.Rewards.Strongboxes.StrongboxInstanceContextV1>(),
                    Array.Empty<StrongboxOpeningRecordSnapshotV1>());
            MissionRunResultAuthorityV1 runAuthority =
                new MissionRunResultAuthorityV1(
                    new MissionRunExistingAuthorityPortV1(
                        holdings,
                        delegate { return openings; }));

            GameObject bridgeObject = Track(new GameObject("Bridge"));
            RunDebugRewardBridge2D bridge =
                bridgeObject.AddComponent<RunDebugRewardBridge2D>();
            MissionResultsSessionV1 routed = null;
            bridge.ConfigureRuntime(
                runId,
                route,
                holdings,
                delegate { return openings; },
                runAuthority,
                factory,
                delegate(MissionResultsSessionV1 value) { routed = value; });

            RunDebugSpawnBatchResultV1 spawned = bridge.Spawn(
                bridge.CreateRequest(2, Id("strongbox", "common"), 77UL));
            Assert.That(spawned.Succeeded, Is.True);
            Assert.That(spawned.Snapshot.RequestedCount, Is.EqualTo(2));
            Assert.That(spawned.Snapshot.SpawnedCount, Is.EqualTo(2));
            Assert.That(spawned.Snapshot.CollectedCount, Is.Zero);
            Assert.That(spawned.Snapshot.Boxes[0].InstanceStableId,
                Is.Not.EqualTo(spawned.Snapshot.Boxes[1].InstanceStableId));

            RunDebugSpawnBatchResultV1 deterministicReplay = bridge.Spawn(
                bridge.CreateRequest(2, Id("strongbox", "common"), 77UL));
            Assert.That(
                deterministicReplay.Status,
                Is.EqualTo(RunDebugSpawnBatchStatusV1.ExactDuplicateNoChange));
            Assert.That(
                deterministicReplay.Snapshot.Boxes[0].InstanceStableId,
                Is.EqualTo(spawned.Snapshot.Boxes[0].InstanceStableId));

            RunDebugSpawnRequestV1 conflictingRequest =
                RunDebugSpawnRequestV1.CreateWithOperation(
                    spawned.Snapshot.Request.OperationStableId,
                    runId,
                    route,
                    1,
                    Id("strongbox", "common"),
                    78UL);
            Assert.That(
                bridge.Spawn(conflictingRequest).Status,
                Is.EqualTo(RunDebugSpawnBatchStatusV1.ConflictingDuplicate));

            RewardPickup2D first = FindPickup(
                factory,
                spawned.Snapshot.Boxes[0].PickupStableId);
            Assert.That(first, Is.Not.Null);
            first.TryCollect(Id("claimant", "player"));
            RunDebugSnapshotV1 collected = bridge.RefreshSnapshot();

            Assert.That(first.IsCollected, Is.True);
            Assert.That(collected.CollectedCount, Is.EqualTo(1));
            Assert.That(collected.PendingCount, Is.EqualTo(1));
            Assert.That(
                collected.Boxes[0].InstanceStableId,
                Is.EqualTo(spawned.Snapshot.Boxes[0].InstanceStableId));

            RunDebugEndResultV1 ended =
                bridge.EndRun(MissionRunCompletionStateV1.Completed);
            RunDebugEndResultV1 replay =
                bridge.EndRun(MissionRunCompletionStateV1.Completed);

            Assert.That(ended.Succeeded, Is.True);
            Assert.That(replay, Is.SameAs(ended));
            Assert.That(bridge.EndRunAuthorityCallCount, Is.EqualTo(1));
            Assert.That(routed, Is.Not.Null);
            Assert.That(routed.Snapshot.UnopenedStrongboxes.Count, Is.EqualTo(1));
            Assert.That(
                routed.Snapshot.UnopenedStrongboxes[0].InstanceStableId,
                Is.EqualTo(collected.Boxes[0].InstanceStableId));
            yield return null;
        }

        private RewardPickup2D FindPickup(
            RewardPickupDropFactory2D factory,
            StableId pickupStableId)
        {
            RewardPickup2D pickup;
            return factory.TryGetPickup(pickupStableId, out pickup) ? pickup : null;
        }

        private GameplaySceneScope2D CreateScope(StableId runId)
        {
            GameObject root = Track(new GameObject("Scope"));
            GameplaySceneScope2D scope = root.AddComponent<GameplaySceneScope2D>();
            scope.ConfigureForTests(
                "scope.dev-run-debug",
                "scope.gameplay",
                "projection.dev-run-debug",
                runId.ToString(),
                0L);
            return scope;
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            created.Add(value);
            return value;
        }

        private static PlayerRouteProfilePayloadV1 Route(string suffix)
        {
            return PlayerRouteProfilePayloadV1.Create(
                Id("character", suffix),
                Id("loadout", suffix),
                new[]
                {
                    Id("equipment-instance", suffix + "-1"),
                    Id("equipment-instance", suffix + "-2"),
                    Id("equipment-instance", suffix + "-3"),
                    Id("equipment-instance", suffix + "-4"),
                });
        }

        private static StableId Id(string namespaceName, string value)
        {
            return StableId.Create(namespaceName, value);
        }

        private sealed class AcceptingEquipmentValidator :
            IEquipmentInstanceValidator
        {
            public EquipmentInstanceValidationResponse Validate(
                EquipmentInstanceValidationRequest request)
            {
                return new EquipmentInstanceValidationResponse(
                    true,
                    "test-catalog",
                    request == null || request.Instance == null
                        ? null
                        : request.Instance.Fingerprint,
                    Array.Empty<EquipmentModelIssue>());
            }
        }

        private sealed class RecordingChildAuthority : IRewardChildAuthorityV1
        {
            private readonly Dictionary<StableId, RewardChildGrantCommandV1> applied =
                new Dictionary<StableId, RewardChildGrantCommandV1>();

            public RecordingChildAuthority(StableId authorityStableId)
            {
                AuthorityStableId = authorityStableId;
            }

            public StableId AuthorityStableId { get; }
            public long Sequence { get; private set; }

            public RewardAuthorityPreflightResultV1 Preflight(
                IReadOnlyList<RewardChildGrantCommandV1> commands)
            {
                var facts = new List<RewardAuthorityPreflightFactV1>();
                for (int index = 0; index < commands.Count; index++)
                {
                    facts.Add(new RewardAuthorityPreflightFactV1(
                        commands[index].TransactionStableId,
                        applied.ContainsKey(commands[index].TransactionStableId)
                            ? RewardAuthorityAdmissionStatusV1.AlreadyApplied
                            : RewardAuthorityAdmissionStatusV1.Accepted,
                        null));
                }

                return new RewardAuthorityPreflightResultV1(facts);
            }

            public RewardChildApplyResultV1 Apply(
                RewardChildGrantCommandV1 command)
            {
                RewardChildGrantCommandV1 prior;
                if (applied.TryGetValue(command.TransactionStableId, out prior))
                {
                    bool exact = prior.Equals(command);
                    return new RewardChildApplyResultV1(
                        command.TransactionStableId,
                        exact
                            ? RewardChildApplyStatusV1.ExactDuplicateNoChange
                            : RewardChildApplyStatusV1.ConflictingDuplicate,
                        exact,
                        exact ? null : "test-conflict");
                }

                applied.Add(command.TransactionStableId, command);
                Sequence++;
                return new RewardChildApplyResultV1(
                    command.TransactionStableId,
                    RewardChildApplyStatusV1.Applied,
                    true,
                    null);
            }
        }
    }
}
