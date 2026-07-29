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
            PlayerRouteProfilePayload route = Route("dev-physical");
            GameplayScene scope = CreateScope(runId);
            PlayerHoldingsActions holdings = new PlayerHoldingsActions(
                Id("authority", "holdings"),
                9999L,
                new AcceptingEquipmentValidator());
            RecordingChildState money =
                new RecordingChildState(Id("authority", "money"));
            RecordingChildState scrap =
                new RecordingChildState(Id("authority", "scrap"));
            var holdingsChild =
                new PlayerHoldingsRewardChildState(
                    holdings,
                    new AcceptingEquipmentValidator());
            RewardApplicationActions rap = new RewardApplicationActions(
                Id("authority", "rap"),
                money,
                scrap,
                holdingsChild);

            GameObject authorityObject = Track(new GameObject("RAP"));
            LootPickupState pickupAuthority =
                authorityObject.AddComponent<LootPickupState>();
            pickupAuthority.ConfigureRuntime(
                rap,
                money.AuthorityStableId,
                scrap.AuthorityStableId,
                holdings.AuthorityStableId);

            GameObject factoryObject = Track(new GameObject("Factory"));
            factoryObject.transform.SetParent(scope.transform);
            LootSpawner factory =
                factoryObject.AddComponent<LootSpawner>();
            factory.ConfigureRuntime(
                new RewardGenerationActions(),
                ProgressionContext.Create(
                    10,
                    10,
                    Id("difficulty", "normal"),
                    1),
                123UL,
                1,
                pickupAuthority,
                scope);

            StrongboxOpeningSnapshot openings =
                StrongboxOpeningSnapshot.CreateCanonical(
                    ShooterMover.Domain.Rewards.Strongboxes.Strongbox.Fingerprint(
                        "debug-empty-catalog"),
                    0L,
                    Array.Empty<ShooterMover.Domain.Rewards.Strongboxes.StrongboxInstanceContext>(),
                    Array.Empty<StrongboxOpeningRecordSnapshot>());
            MissionRunResultState runAuthority =
                new MissionRunResultState(
                    new MissionRunExistingStatePort(
                        holdings,
                        delegate { return openings; }));

            GameObject bridgeObject = Track(new GameObject("Bridge"));
            RunDebugRewards bridge =
                bridgeObject.AddComponent<RunDebugRewards>();
            MissionResultsSession routed = null;
            bridge.ConfigureRuntime(
                runId,
                route,
                holdings,
                delegate { return openings; },
                runAuthority,
                factory,
                delegate(MissionResultsSession value) { routed = value; });

            RunDebugSpawnBatchResult spawned = bridge.Spawn(
                bridge.CreateRequest(2, Id("strongbox", "common"), 77UL));
            Assert.That(spawned.Succeeded, Is.True);
            Assert.That(spawned.Snapshot.RequestedCount, Is.EqualTo(2));
            Assert.That(spawned.Snapshot.SpawnedCount, Is.EqualTo(2));
            Assert.That(spawned.Snapshot.CollectedCount, Is.Zero);
            Assert.That(spawned.Snapshot.Boxes[0].InstanceStableId,
                Is.Not.EqualTo(spawned.Snapshot.Boxes[1].InstanceStableId));

            RunDebugSpawnBatchResult deterministicReplay = bridge.Spawn(
                bridge.CreateRequest(2, Id("strongbox", "common"), 77UL));
            Assert.That(
                deterministicReplay.Status,
                Is.EqualTo(RunDebugSpawnBatchStatus.ExactDuplicateNoChange));
            Assert.That(
                deterministicReplay.Snapshot.Boxes[0].InstanceStableId,
                Is.EqualTo(spawned.Snapshot.Boxes[0].InstanceStableId));

            RunDebugSpawnRequest conflictingRequest =
                RunDebugSpawnRequest.CreateWithOperation(
                    spawned.Snapshot.Request.OperationStableId,
                    runId,
                    route,
                    1,
                    Id("strongbox", "common"),
                    78UL);
            Assert.That(
                bridge.Spawn(conflictingRequest).Status,
                Is.EqualTo(RunDebugSpawnBatchStatus.ConflictingDuplicate));

            LootPickup first = FindPickup(
                factory,
                spawned.Snapshot.Boxes[0].PickupStableId);
            Assert.That(first, Is.Not.Null);
            first.TryCollect(Id("claimant", "player"));
            RunDebugSnapshot collected = bridge.RefreshSnapshot();

            Assert.That(first.IsCollected, Is.True);
            Assert.That(collected.CollectedCount, Is.EqualTo(1));
            Assert.That(collected.PendingCount, Is.EqualTo(1));
            Assert.That(
                collected.Boxes[0].InstanceStableId,
                Is.EqualTo(spawned.Snapshot.Boxes[0].InstanceStableId));

            RunDebugEndResult ended =
                bridge.EndRun(MissionRunCompletionState.Completed);
            RunDebugEndResult replay =
                bridge.EndRun(MissionRunCompletionState.Completed);

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

        private LootPickup FindPickup(
            LootSpawner factory,
            StableId pickupStableId)
        {
            LootPickup pickup;
            return factory.TryGetPickup(pickupStableId, out pickup) ? pickup : null;
        }

        private GameplayScene CreateScope(StableId runId)
        {
            GameObject root = Track(new GameObject("Scope"));
            GameplayScene scope = root.AddComponent<GameplayScene>();
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

        private static PlayerRouteProfilePayload Route(string suffix)
        {
            return PlayerRouteProfilePayload.Create(
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

        private sealed class RecordingChildState : IRewardChildState
        {
            private readonly Dictionary<StableId, RewardChildGrantCommand> applied =
                new Dictionary<StableId, RewardChildGrantCommand>();

            public RecordingChildState(StableId authorityStableId)
            {
                AuthorityStableId = authorityStableId;
            }

            public StableId AuthorityStableId { get; }
            public long Sequence { get; private set; }

            public RewardStatePreflightResult Preflight(
                IReadOnlyList<RewardChildGrantCommand> commands)
            {
                var facts = new List<RewardStatePreflightFact>();
                for (int index = 0; index < commands.Count; index++)
                {
                    facts.Add(new RewardStatePreflightFact(
                        commands[index].TransactionStableId,
                        applied.ContainsKey(commands[index].TransactionStableId)
                            ? RewardStateAdmissionStatus.AlreadyApplied
                            : RewardStateAdmissionStatus.Accepted,
                        null));
                }

                return new RewardStatePreflightResult(facts);
            }

            public RewardChildApplyResult Apply(
                RewardChildGrantCommand command)
            {
                RewardChildGrantCommand prior;
                if (applied.TryGetValue(command.TransactionStableId, out prior))
                {
                    bool exact = prior.Equals(command);
                    return new RewardChildApplyResult(
                        command.TransactionStableId,
                        exact
                            ? RewardChildApplyStatus.ExactDuplicateNoChange
                            : RewardChildApplyStatus.ConflictingDuplicate,
                        exact,
                        exact ? null : "test-conflict");
                }

                applied.Add(command.TransactionStableId, command);
                Sequence++;
                return new RewardChildApplyResult(
                    command.TransactionStableId,
                    RewardChildApplyStatus.Applied,
                    true,
                    null);
            }
        }
    }
}
