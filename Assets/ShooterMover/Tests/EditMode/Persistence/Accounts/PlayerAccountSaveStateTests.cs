using NUnit.Framework;
using ShooterMover.Application.Persistence.Accounts;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;

namespace ShooterMover.Tests.EditMode.Persistence.Accounts
{
    public sealed class PlayerAccountSaveStateTests
    {
        [Test]
        public void EmptyAccount_ContainsExactlySixNullableCharacterSlots()
        {
            PlayerAccountSnapshot account = PlayerAccountSnapshot.Empty(
                Id("account.player-one"));

            Assert.That(
                account.CharacterSlots.Count,
                Is.EqualTo(PlayerAccountSnapshot.CharacterSlotCount));
            Assert.That(account.CharacterSlots, Has.All.Null);
            Assert.That(account.Revision, Is.Zero);
        }

        [Test]
        public void Characters_AreIndependentDataDefinedInstances()
        {
            var authority = CreateAuthority();
            CharacterInstanceSnapshot healer = Character(
                0,
                "character.healer-one",
                "class.healer",
                Component("character.experience", "level=12;xp=44"));
            CharacterInstanceSnapshot juggernaut = Character(
                4,
                "character.juggernaut-one",
                "class.juggernaut",
                Component("character.experience", "level=3;xp=8"));

            PlayerAccountSaveResult first = authority.Apply(
                PlayerAccountSaveCommand.CreateCharacter(
                    Id("operation.create-healer"),
                    0L,
                    healer));
            PlayerAccountSaveResult second = authority.Apply(
                PlayerAccountSaveCommand.CreateCharacter(
                    Id("operation.create-juggernaut"),
                    1L,
                    juggernaut));

            Assert.That(first.Status, Is.EqualTo(
                PlayerAccountSaveStatus.Applied));
            Assert.That(second.Status, Is.EqualTo(
                PlayerAccountSaveStatus.Applied));
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
            SavePartSnapshot boxes = Component(
                "character.unopened-strongboxes",
                "box.a|tier=4|seed=77;box.b|tier=5|seed=88");

            PlayerAccountSaveResult result = authority.Apply(
                PlayerAccountSaveCommand.UpsertCharacterComponent(
                    Id("operation.save-boxes"),
                    authority.Current.Revision,
                    2,
                    Id("character.striker-one"),
                    boxes));

            SavePartSnapshot stored;
            Assert.That(result.Status, Is.EqualTo(
                PlayerAccountSaveStatus.Applied));
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
            SavePartSnapshot achievements = Component(
                "account.achievements",
                "achievement.first-win=1");
            SavePartSnapshot eventState = Component(
                "account.event-state",
                "event.double-drops-2026=claimed");

            authority.Apply(
                PlayerAccountSaveCommand.UpsertAccountComponent(
                    Id("operation.save-achievements"),
                    0L,
                    achievements));
            authority.Apply(
                PlayerAccountSaveCommand.UpsertAccountComponent(
                    Id("operation.save-event-state"),
                    1L,
                    eventState));

            SavePartSnapshot stored;
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
            CharacterInstanceSnapshot character = Character(
                0,
                "character.healer-one",
                "class.healer");
            PlayerAccountSaveCommand command =
                PlayerAccountSaveCommand.CreateCharacter(
                    Id("operation.create-character"),
                    0L,
                    character);

            PlayerAccountSaveResult applied = authority.Apply(command);
            PlayerAccountSaveResult duplicate = authority.Apply(command);
            PlayerAccountSaveResult conflict = authority.Apply(
                PlayerAccountSaveCommand.UpsertAccountComponent(
                    Id("operation.create-character"),
                    1L,
                    Component("account.collections", "enemy.droid=1")));

            Assert.That(applied.Status, Is.EqualTo(
                PlayerAccountSaveStatus.Applied));
            Assert.That(duplicate.Status, Is.EqualTo(
                PlayerAccountSaveStatus.ExactDuplicateNoChange));
            Assert.That(conflict.Status, Is.EqualTo(
                PlayerAccountSaveStatus.ConflictingDuplicate));
            Assert.That(authority.Current.Revision, Is.EqualTo(1L));
        }

        [Test]
        public void ExportImport_PreservesAccountAndReplayProtection()
        {
            var source = CreateAuthorityWithCharacter();
            PlayerAccountSaveCommand saveInventory =
                PlayerAccountSaveCommand.UpsertCharacterComponent(
                    Id("operation.save-inventory"),
                    source.Current.Revision,
                    2,
                    Id("character.striker-one"),
                    Component(
                        "character.holdings",
                        "equipment-instance.blaster-a;equipment-instance.shotgun-b"));
            source.Apply(saveInventory);
            PlayerAccountSaveStateSnapshot exported =
                source.ExportSnapshot();

            var restored = CreateAuthority();
            string rejection;
            bool imported = restored.TryImport(exported, out rejection);
            PlayerAccountSaveResult duplicate = restored.Apply(saveInventory);

            Assert.That(imported, Is.True, rejection);
            Assert.That(
                restored.Current.Fingerprint,
                Is.EqualTo(source.Current.Fingerprint));
            Assert.That(duplicate.Status, Is.EqualTo(
                PlayerAccountSaveStatus.ExactDuplicateNoChange));
            Assert.That(
                restored.ExportSnapshot().Fingerprint,
                Is.EqualTo(exported.Fingerprint));
        }

        [Test]
        public void ImportForDifferentAccount_RejectsWithoutMutation()
        {
            var authority = CreateAuthority();
            string before = authority.Current.Fingerprint;
            var foreign = new PlayerAccountSaveState(
                PlayerAccountSnapshot.Empty(Id("account.foreign")));

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

            PlayerAccountSaveResult mismatch = authority.Apply(
                PlayerAccountSaveCommand.DeleteCharacter(
                    Id("operation.delete-wrong"),
                    authority.Current.Revision,
                    2,
                    Id("character.someone-else")));
            PlayerAccountSaveResult deleted = authority.Apply(
                PlayerAccountSaveCommand.DeleteCharacter(
                    Id("operation.delete-right"),
                    authority.Current.Revision,
                    2,
                    Id("character.striker-one")));

            Assert.That(mismatch.Status, Is.EqualTo(
                PlayerAccountSaveStatus.Rejected));
            Assert.That(deleted.Status, Is.EqualTo(
                PlayerAccountSaveStatus.Applied));
            Assert.That(authority.Current.CharacterAt(2), Is.Null);
        }

        private static PlayerAccountSaveState CreateAuthority()
        {
            return new PlayerAccountSaveState(
                PlayerAccountSnapshot.Empty(Id("account.player-one")));
        }

        private static PlayerAccountSaveState
            CreateAuthorityWithCharacter()
        {
            PlayerAccountSaveState authority = CreateAuthority();
            authority.Apply(
                PlayerAccountSaveCommand.CreateCharacter(
                    Id("operation.seed-striker"),
                    0L,
                    Character(
                        2,
                        "character.striker-one",
                        "class.striker")));
            return authority;
        }

        private static CharacterInstanceSnapshot Character(
            int slotIndex,
            string characterId,
            string classId,
            params SavePartSnapshot[] components)
        {
            return new CharacterInstanceSnapshot(
                Id(characterId),
                Id(classId),
                slotIndex,
                "Pilot " + slotIndex,
                0L,
                components);
        }

        private static SavePartSnapshot Component(
            string componentId,
            string payload)
        {
            return new SavePartSnapshot(
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
