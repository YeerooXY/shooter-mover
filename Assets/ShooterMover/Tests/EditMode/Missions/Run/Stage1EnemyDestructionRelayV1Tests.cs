using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Missions.Run;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Contracts.Missions.Run;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.UnityAdapters.Missions.Run;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.Missions.Run
{
    public sealed class Stage1EnemyDestructionRelayV1Tests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Configure_RejectsMissingDependenciesWithoutMutation()
        {
            Stage1EnemyDestructionRelayV1 relay = CreateRelay();
            var binding = new RecordingBinding();

            Assert.That(relay.Configure(null, StableId.Parse("room.level1-entry")), Is.False);
            Assert.That(relay.Configure(binding, null), Is.False);
            Assert.That(relay.IsConfigured, Is.False);
            Assert.That(relay.RoomStableId, Is.Null);
        }

        [Test]
        public void Configure_IsExactlyOnceForSameBindingAndRoom()
        {
            Stage1EnemyDestructionRelayV1 relay = CreateRelay();
            var binding = new RecordingBinding();
            StableId room = StableId.Parse("room.level1-entry");

            Assert.That(relay.Configure(binding, room), Is.True);
            Assert.That(relay.Configure(binding, room), Is.True);
            Assert.That(relay.Configure(new RecordingBinding(), room), Is.False);
            Assert.That(
                relay.Configure(binding, StableId.Parse("room.level1-terminal")),
                Is.False);
            Assert.That(relay.RoomStableId, Is.EqualTo(room));
        }

        [Test]
        public void TryReportAccepted_RejectsBeforeConfigurationAndNullNotification()
        {
            Stage1EnemyDestructionRelayV1 relay = CreateRelay();
            LevelRunEnemyDestructionResultV1 result;

            Assert.That(relay.TryReportAccepted(null, out result), Is.False);
            Assert.That(result, Is.Null);

            var binding = new RecordingBinding();
            Assert.That(
                relay.Configure(binding, StableId.Parse("room.level1-entry")),
                Is.True);
            Assert.That(relay.TryReportAccepted(null, out result), Is.False);
            Assert.That(result, Is.Null);
            Assert.That(binding.ReportCount, Is.EqualTo(0));
        }

        private Stage1EnemyDestructionRelayV1 CreateRelay()
        {
            root = new GameObject("Stage1EnemyDestructionRelayV1Tests");
            return root.AddComponent<Stage1EnemyDestructionRelayV1>();
        }

        private sealed class RecordingBinding : IStage1ProductionRunBindingV1
        {
            public int ReportCount { get; private set; }
            public bool IsConfigured { get { return true; } }
            public StableId CurrentRoomStableId { get { return null; } }
            public int ActiveWeaponSlotIndex { get { return -1; } }
            public StableId ActiveEquipmentInstanceStableId { get { return null; } }

            public Stage1WeaponSlotSelectionStatusV1 SelectWeaponSlot(int slotIndex)
            {
                return Stage1WeaponSlotSelectionStatusV1.InvalidLoadout;
            }

            public Stage1RunRegistrationStatusV1 RegisterRoom(
                StableId roomStableId,
                IEnumerable<Stage1RunEnemyRegistrationV1> enemies)
            {
                return Stage1RunRegistrationStatusV1.InvalidRequest;
            }

            public LevelRunEnemyDestructionResultV1 ReportEnemyDestroyed(
                StableId roomStableId,
                EnemyDestroyedNotification acceptedDestruction)
            {
                ReportCount++;
                return null;
            }

            public Stage1SceneAdapterStatusV1 TryTraverse(
                StableId exitStableId,
                out RoomGraphOperationResultV1 traversal)
            {
                traversal = null;
                return Stage1SceneAdapterStatusV1.NotConfigured;
            }

            public Stage1SceneAdapterStatusV1 CompleteAndRouteResults(
                out Stage1RunCompletionResultV1 completion)
            {
                completion = null;
                return Stage1SceneAdapterStatusV1.NotConfigured;
            }
        }
    }
}
