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
    public sealed class RequiredCharacterComponentBackfillTests
    {
        [Test]
        public void MissingRequiredSkillComponentIsAddedWithoutReplacingExistingComponents()
        {
            CharacterLiveGraphFactory factory =
                CharacterLiveGraphFactory
                    .CreateVerticalSliceDefaults();
            StableId characterId = Id("character-instance.required-backfill");
            StableId classId = Id("loadout-profile.juggernaut");
            ICharacterLiveGraph starter = factory.CreateStarter(
                0,
                characterId,
                classId,
                "Backfill Pilot",
                null);
            IReadOnlyList<SaveComponentSnapshot> complete =
                PlayerAccountRestoreFlow.ExportComponents(
                    starter.SaveAdapters);
            starter.Dispose();

            StableId missingId = KnownSaveComponentDefinitions
                .RankedSkillAllocation().ComponentStableId;
            StableId preservedId = KnownSaveComponentDefinitions
                .PlayerHoldings().ComponentStableId;
            SaveComponentSnapshot preservedBefore = complete.Single(
                item => item.ComponentStableId == preservedId);
            List<SaveComponentSnapshot> incomplete = complete
                .Where(item => item.ComponentStableId != missingId)
                .ToList();
            var character = new CharacterInstanceSnapshot(
                characterId,
                classId,
                0,
                "Backfill Pilot",
                0L,
                incomplete);
            PlayerAccountSnapshot account = Account(character);

            RequiredCharacterComponentBackfillResult result =
                RequiredCharacterComponentBackfill.Migrate(
                    account,
                    factory);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Succeeded, Is.True, result.Diagnostic);
            Assert.That(result.Changed, Is.True);
            Assert.That(result.MigratedCharacterCount, Is.EqualTo(1));
            CharacterInstanceSnapshot migrated = result.Account.CharacterAt(0);
            SaveComponentSnapshot added;
            Assert.That(migrated.TryGetComponent(missingId, out added), Is.True);
            Assert.That(added, Is.Not.Null);
            SaveComponentSnapshot preservedAfter;
            Assert.That(
                migrated.TryGetComponent(preservedId, out preservedAfter),
                Is.True);
            Assert.That(
                preservedAfter.Fingerprint,
                Is.EqualTo(preservedBefore.Fingerprint));
            Assert.That(
                PlayerAccountComponentSemantics.Validate(result.Account)
                    .Succeeded,
                Is.True);
        }

        [Test]
        public void CompleteCharacterIsExactNoChange()
        {
            CharacterLiveGraphFactory factory =
                CharacterLiveGraphFactory
                    .CreateVerticalSliceDefaults();
            StableId characterId = Id("character-instance.backfill-no-change");
            StableId classId = Id("loadout-profile.juggernaut");
            ICharacterLiveGraph starter = factory.CreateStarter(
                0,
                characterId,
                classId,
                "Complete Pilot",
                null);
            IReadOnlyList<SaveComponentSnapshot> components =
                PlayerAccountRestoreFlow.ExportComponents(
                    starter.SaveAdapters);
            starter.Dispose();
            var character = new CharacterInstanceSnapshot(
                characterId,
                classId,
                0,
                "Complete Pilot",
                0L,
                components);
            PlayerAccountSnapshot account = Account(character);

            RequiredCharacterComponentBackfillResult result =
                RequiredCharacterComponentBackfill.Migrate(
                    account,
                    factory);

            Assert.That(result.Succeeded, Is.True, result.Diagnostic);
            Assert.That(result.Changed, Is.False);
            Assert.That(result.MigratedCharacterCount, Is.Zero);
            Assert.That(result.Account.Fingerprint, Is.EqualTo(account.Fingerprint));
        }

        private static PlayerAccountSnapshot Account(
            CharacterInstanceSnapshot character)
        {
            var slots = new CharacterInstanceSnapshot[
                PlayerAccountSnapshot.CharacterSlotCount];
            slots[character.SlotIndex] = character;
            return new PlayerAccountSnapshot(
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
