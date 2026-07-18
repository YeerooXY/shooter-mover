using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Missions.Run;

namespace ShooterMover.Tests.EditMode.Missions.Run
{
    public sealed class Stage1ProductionWeaponSlotSelectionV1Tests
    {
        [Test]
        public void Constructor_RetainsFourConcreteEquipmentInstances()
        {
            StableId[] slots = CreateSlots();

            var selection = new Stage1ProductionWeaponSlotSelectionV1(slots);

            Assert.That(selection.EquipmentInstanceStableIds, Is.EqualTo(slots));
            Assert.That(selection.SelectedSlotIndex, Is.EqualTo(0));
            Assert.That(selection.SelectedEquipmentInstanceStableId, Is.EqualTo(slots[0]));
        }

        [Test]
        public void SelectSlot_ChangesOnlyToRequestedConcreteSlot()
        {
            StableId[] slots = CreateSlots();
            var selection = new Stage1ProductionWeaponSlotSelectionV1(slots);

            Stage1WeaponSlotSelectionStatusV1 result = selection.SelectSlot(3);

            Assert.That(result, Is.EqualTo(Stage1WeaponSlotSelectionStatusV1.Selected));
            Assert.That(selection.SelectedSlotIndex, Is.EqualTo(3));
            Assert.That(selection.SelectedEquipmentInstanceStableId, Is.EqualTo(slots[3]));
        }

        [Test]
        public void SelectSlot_ExactRetry_DoesNotChangeState()
        {
            var selection = new Stage1ProductionWeaponSlotSelectionV1(CreateSlots(), 2);

            Stage1WeaponSlotSelectionStatusV1 result = selection.SelectSlot(2);

            Assert.That(result, Is.EqualTo(Stage1WeaponSlotSelectionStatusV1.ExactDuplicateNoChange));
            Assert.That(selection.SelectedSlotIndex, Is.EqualTo(2));
        }

        [TestCase(-1)]
        [TestCase(4)]
        public void SelectSlot_InvalidSlot_IsRejectedWithoutMutation(int requestedSlot)
        {
            var selection = new Stage1ProductionWeaponSlotSelectionV1(CreateSlots(), 1);

            Stage1WeaponSlotSelectionStatusV1 result = selection.SelectSlot(requestedSlot);

            Assert.That(result, Is.EqualTo(Stage1WeaponSlotSelectionStatusV1.InvalidSlot));
            Assert.That(selection.SelectedSlotIndex, Is.EqualTo(1));
        }

        [Test]
        public void Constructor_AllowsDuplicateDefinitionsThroughDistinctInstanceIds()
        {
            Assert.DoesNotThrow(() =>
                new Stage1ProductionWeaponSlotSelectionV1(CreateSlots()));
        }

        [Test]
        public void Constructor_RejectsDuplicateEquipmentInstanceIdentity()
        {
            StableId repeated = StableId.Parse("equipment-instance.stage1-slot-1");

            Assert.Throws<System.ArgumentException>(() =>
                new Stage1ProductionWeaponSlotSelectionV1(new[]
                {
                    repeated,
                    repeated,
                    StableId.Parse("equipment-instance.stage1-slot-3"),
                    StableId.Parse("equipment-instance.stage1-slot-4"),
                }));
        }

        private static StableId[] CreateSlots()
        {
            return new[]
            {
                StableId.Parse("equipment-instance.stage1-slot-1"),
                StableId.Parse("equipment-instance.stage1-slot-2"),
                StableId.Parse("equipment-instance.stage1-slot-3"),
                StableId.Parse("equipment-instance.stage1-slot-4"),
            };
        }
    }
}
