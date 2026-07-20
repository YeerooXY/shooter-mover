using NUnit.Framework;
using ShooterMover.Application.Persistence.Accounts;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;

namespace ShooterMover.Tests.EditMode.Persistence.Accounts
{
    public sealed class PlayerAccountSaveAuthorityV1Tests
    {
        [Test]
        public void EmptyAccount_ContainsExactlySixNullableCharacterSlots()
        {
            PlayerAccountSnapshotV1 account = PlayerAccountSnapshotV1.Empty(
                Id("account.player-one"));

            Assert.That(
                account.CharacterSlots.Count,
                Is.EqualTo(PlayerAccountSnapshotV1.CharacterSlotCount));
            Assert.That(account.CharacterSlots, Has.All.Null);
            Assert.That(account.Revision, Is.Zero);
        }

        [Test]
        public void Characters_AreIndependentDataDefinedInstances()
        {
            var authority = CreateAuthority();
            CharacterInstanceSnapshotV1 healer = Character(
                0,
                "character.healer-one",
                "class.healer",
                Component("character.experience", "level=12;xp=44"));
            CharacterInstanceSnapshotV1 juggernaut = Character(
                4,
                "character.juggernaut-one",
                "class.juggernaut",
                Component("character.experience", "level=3;xp=8"));

            PlayerAccountSaveResultV1 first = authority.Apply(
                PlayerAccountSaveCommandV1.CreateCharacter(
                    Id("operation.create-healer"),
                    0L,
                    healer));
            PlayerAccountSaveResultV1 second = authority.Apply(
                PlayerAccountSaveCommandV1.CreateCharacter(
                    Id("operation.create-juggernaut"),
                    1L,
                    juggernaut));

            Assert.That(first.Status, Is.EqualTo(
                PlayerAccountSaveStatusV1.Applied));
            Assert.That(second.Status, Is.EqualTo(
                PlayerAccountSaveStatusV1.Applied));
            Assert.That(
                authority.Current.CharacterAt(0).ClassDefinitionStableId,
                Is.EqualTo(Id("class.healer")));
            Assert.That(
                authority.Current.CharacterAt(4).ClassDefinitionStableId,
                Is.EqualTo(Id("class.juggernaut")));
            Assert.That(authority.Current.CharacterAt(1), Is.Null);
        }

        [Test]
        public void NewCharacterSubsystemComponent_RequiresNoAccountModelChange()
        {
            var authority = CreateAuthorityWithCharacter();
            SaveComponentSnapshotV1 boxes = Component(
                "character.unopened-strongboxes",
                "box.a|tier=4|seed=77;box.b|tier=5|seed=88");

            PlayerAccountSaveResultV1 result = authority.Apply(
                PlayerAccountSaveCommandV1.UpsertCharacterComponent(
                    Id("operation.save-boxes"),
                    authority.Current.Revision,
                    2,
                    Id("character.striker-one"),
                    boxes));

            SaveComponentSnapshotV1 stored;
            Assert.That(result.Status, Is.EqualTo(
                PlayerAccountSaveStatusV1.Applied));
            Assert.That(
                authority.Current.CharacterAt(2).TryGetComponent(
                    Id("character.unopened-strongboxes"),
                    out stored),
                Is.True);
            Assert.That(stored.Fingerprint, Is.EqualTo(boxes.Fingerprint));
            Assert.That(authority.Current.CharacterAt(2).Revision, Is.EqualTo(1L));
        }

        [Test]
        public void AccountComponents_SupportAchievementsCollectionsAndEvents()
        {
            var authority = CreateAuthority();
            SaveComponentSnapshotV1 achievements = Component(
                "account.achievements",
                "achievement.first-win=1");
            SaveComponentSnapshotV1 eventState = Component(
                "account.event-state",
                "event.double-drops-2026=claimed");

            authority.Apply(
                PlayerAccountSaveCommandV1.UpsertAccountComponent(
                    Id("operation.save-achievements"),
                    0L,
                    achievements));
            authority.Apply(
                PlayerAccountSaveCommandV1.UpsertAccountComponent(
                    Id("operation.save-event-state"),
                    1L,
                    eventState));

            SaveComponentSnapshotV1 stored;
            Assert.That(
                authority.Current.TryGetAccountComponent(
                    achievements.ComponentStableId,
                    out stored),
                Is.True);
            Assert.That(stored.Fingerprint, Is.EqualTo(achievements.Fingerprint));
            Assert.That(
                authority.Current.TryGetAccountComponent(
                    eventState.ComponentStableId,
                    out stored),
                Is.True);
            Assert.That(stored.Fingerprint, Is.EqualTo(eventState.Fingerprint));
        }

        [Test]
        public void DuplicateOperation_IsExactlyOnceAndConflictRejects()
        {
            var authority = CreateAuthority();
            CharacterInstanceSnapshotV1 character = Character(
                0,
                "character.healer-one",
                "class.healer");
            PlayerAccountSaveCommandV1 command =
                PlayerAccountSaveCommandV1.CreateCharacter(
                    Id("operation.create-character"),
                    0L,
                    character);

            PlayerAccountSaveResultV1 applied = authority.Apply(command);
            PlayerAccountSaveResultV1 duplicate = authority.Apply(command);
            PlayerAccountSaveResultV1 conflict = authority.Apply(
                PlayerAccountSaveCommandV1.UpsertAccountComponent(
                    Id("operation.create-character"),
                    1L,
                    Component("account.collections", "enemy.droid=1")));

            Assert.That(applied.Status, Is.EqualTo(
                PlayerAccountSaveStatusV1.Applied));
            Assert.That(duplicate.Status, Is.EqualTo(
                PlayerAccountSaveStatusV1.ExactDuplicateNoChange));
            Assert.That(conflict.Status, Is.EqualTo(
                PlayerAccountSaveStatusV1.ConflictingDuplicate));
            Assert.That(authority.Current.Revision, Is.EqualTo(1L));
        }

        [Test]
        public void ExportImport_PreservesAccountAndReplayProtection()
        {
            var source = CreateAuthorityWithCharacter();
            PlayerAccountSaveCommandV1 saveInventory =
                PlayerAccountSaveCommandV1.UpsertCharacterComponent(
                    Id("operation.save-inventory"),
                    source.Current.Revision,
                    2,
                    Id("character.striker-one"),
                    Component(
                        "character.holdings",
                        "equipment-instance.blaster-a;equipment-instance.shotgun-b"));
            source.Apply(saveInventory);
            PlayerAccountSaveAuthoritySnapshotV1 exported =
                source.ExportSnapshot();

            var restored = CreateAuthority();
            string rejection;
            bool imported = restored.TryImport(exported, out rejection);
            PlayerAccountSaveResultV1 duplicate = restored.Apply(saveInventory);

            Assert.That(imported, Is.True, rejection);
            Assert.That(
                restored.Current.Fingerprint,
                Is.EqualTo(source.Current.Fingerprint));
            Assert.That(duplicate.Status, Is.EqualTo(
                PlayerAccountSaveStatusV1.ExactDuplicateNoChange));
            Assert.That(
                restored.ExportSnapshot().Fingerprint,
                Is.EqualTo(exported.Fingerprint));
        }

        [Test]
        public void ImportForDifferentAccount_RejectsWithoutMutation()
        {
            var authority = CreateAuthority();
            string before = authority.Current.Fingerprint;
            var foreign = new PlayerAccountSaveAuthorityV1(
                PlayerAccountSnapshotV1.Empty(Id("account.foreign")));

            string rejection;
            bool imported = authority.TryImport(
                foreign.ExportSnapshot(),
                out rejection);

            Assert.That(imported, Is.False);
            Assert.That(rejection, Is.EqualTo(
                "account-save-import-account-mismatch"));
            Assert.That(authority.Current.Fingerprint, Is.EqualTo(before));
        }

        [Test]
        public void DeleteCharacter_RequiresExactInstanceIdentity()
        {
            var authority = CreateAuthorityWithCharacter();

            PlayerAccountSaveResultV1 mismatch = authority.Apply(
                PlayerAccountSaveCommandV1.DeleteCharacter(
                    Id("operation.delete-wrong"),
                    authority.Current.Revision,
                    2,
                    Id("character.someone-else")));
            PlayerAccountSaveResultV1 deleted = authority.Apply(
                PlayerAccountSaveCommandV1.DeleteCharacter(
                    Id("operation.delete-right"),
                    authority.Current.Revision,
                    2,
                    Id("character.striker-one")));

            Assert.That(mismatch.Status, Is.EqualTo(
                PlayerAccountSaveStatusV1.Rejected));
            Assert.That(deleted.Status, Is.EqualTo(
                PlayerAccountSaveStatusV1.Applied));
            Assert.That(authority.Current.CharacterAt(2), Is.Null);
        }

        private static PlayerAccountSaveAuthorityV1 CreateAuthority()
        {
            return new PlayerAccountSaveAuthorityV1(
                PlayerAccountSnapshotV1.Empty(Id("account.player-one")));
        }

        private static PlayerAccountSaveAuthorityV1
            CreateAuthorityWithCharacter()
        {
            PlayerAccountSaveAuthorityV1 authority = CreateAuthority();
            authority.Apply(
                PlayerAccountSaveCommandV1.CreateCharacter(
                    Id("operation.seed-striker"),
                    0L,
                    Character(
                        2,
                        "character.striker-one",
                        "class.striker")));
            return authority;
        }

        private static CharacterInstanceSnapshotV1 Character(
            int slotIndex,
            string characterId,
            string classId,
            params SaveComponentSnapshotV1[] components)
        {
            return new CharacterInstanceSnapshotV1(
                Id(characterId),
                Id(classId),
                slotIndex,
                "Pilot " + slotIndex,
                0L,
                components);
        }

        private static SaveComponentSnapshotV1 Component(
            string componentId,
            string payload)
        {
            return new SaveComponentSnapshotV1(
                Id(componentId),
                1,
                "content.v1",
                payload);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }
    }
}
