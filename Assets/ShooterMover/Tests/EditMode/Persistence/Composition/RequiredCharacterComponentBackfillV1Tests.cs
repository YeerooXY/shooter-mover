using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;

namespace ShooterMover.Tests.EditMode.Persistence.Composition
{
    public sealed class RequiredCharacterComponentBackfillV1Tests
    {
        [Test]
        public void MissingRequiredSkillComponentIsAddedWithoutReplacingExistingComponents()
        {
            ProductionCharacterRuntimeGraphFactoryV1 factory =
                ProductionCharacterRuntimeGraphFactoryV1
                    .CreateVerticalSliceDefaults();
            StableId characterId = Id("character-instance.required-backfill");
            StableId classId = Id("loadout-profile.juggernaut");
            ICharacterRuntimeGraphV1 starter = factory.CreateStarter(
                0,
                characterId,
                classId,
                "Backfill Pilot",
                null);
            IReadOnlyList<SaveComponentSnapshotV1> complete =
                PlayerAccountRestoreCoordinatorV1.ExportComponents(
                    starter.SaveAdapters);
            starter.Dispose();

            StableId missingId = KnownSaveComponentDefinitionsV1
                .RankedSkillAllocation().ComponentStableId;
            StableId preservedId = KnownSaveComponentDefinitionsV1
                .PlayerHoldings().ComponentStableId;
            SaveComponentSnapshotV1 preservedBefore = complete.Single(
                item => item.ComponentStableId == preservedId);
            List<SaveComponentSnapshotV1> incomplete = complete
                .Where(item => item.ComponentStableId != missingId)
                .ToList();
            var character = new CharacterInstanceSnapshotV1(
                characterId,
                classId,
                0,
                "Backfill Pilot",
                0L,
                incomplete);
            PlayerAccountSnapshotV1 account = Account(character);

            RequiredCharacterComponentBackfillResultV1 result =
                RequiredCharacterComponentBackfillV1.Migrate(
                    account,
                    factory);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Succeeded, Is.True, result.Diagnostic);
            Assert.That(result.Changed, Is.True);
            Assert.That(result.MigratedCharacterCount, Is.EqualTo(1));
            CharacterInstanceSnapshotV1 migrated = result.Account.CharacterAt(0);
            SaveComponentSnapshotV1 added;
            Assert.That(migrated.TryGetComponent(missingId, out added), Is.True);
            Assert.That(added, Is.Not.Null);
            SaveComponentSnapshotV1 preservedAfter;
            Assert.That(
                migrated.TryGetComponent(preservedId, out preservedAfter),
                Is.True);
            Assert.That(
                preservedAfter.Fingerprint,
                Is.EqualTo(preservedBefore.Fingerprint));
            Assert.That(
                PlayerAccountComponentSemanticsV1.Validate(result.Account)
                    .Succeeded,
                Is.True);
        }

        [Test]
        public void CompleteCharacterIsExactNoChange()
        {
            ProductionCharacterRuntimeGraphFactoryV1 factory =
                ProductionCharacterRuntimeGraphFactoryV1
                    .CreateVerticalSliceDefaults();
            StableId characterId = Id("character-instance.backfill-no-change");
            StableId classId = Id("loadout-profile.juggernaut");
            ICharacterRuntimeGraphV1 starter = factory.CreateStarter(
                0,
                characterId,
                classId,
                "Complete Pilot",
                null);
            IReadOnlyList<SaveComponentSnapshotV1> components =
                PlayerAccountRestoreCoordinatorV1.ExportComponents(
                    starter.SaveAdapters);
            starter.Dispose();
            var character = new CharacterInstanceSnapshotV1(
                characterId,
                classId,
                0,
                "Complete Pilot",
                0L,
                components);
            PlayerAccountSnapshotV1 account = Account(character);

            RequiredCharacterComponentBackfillResultV1 result =
                RequiredCharacterComponentBackfillV1.Migrate(
                    account,
                    factory);

            Assert.That(result.Succeeded, Is.True, result.Diagnostic);
            Assert.That(result.Changed, Is.False);
            Assert.That(result.MigratedCharacterCount, Is.Zero);
            Assert.That(result.Account.Fingerprint, Is.EqualTo(account.Fingerprint));
        }

        private static PlayerAccountSnapshotV1 Account(
            CharacterInstanceSnapshotV1 character)
        {
            var slots = new CharacterInstanceSnapshotV1[
                PlayerAccountSnapshotV1.CharacterSlotCount];
            slots[character.SlotIndex] = character;
            return new PlayerAccountSnapshotV1(
                Id("account.required-character-component-backfill"),
                0L,
                slots,
                null);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }
    }
}
