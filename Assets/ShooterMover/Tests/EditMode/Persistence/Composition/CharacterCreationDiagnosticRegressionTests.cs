using System;
using NUnit.Framework;
using ShooterMover.Application.Persistence.Accounts;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;

namespace ShooterMover.Tests.EditMode.Persistence.Composition
{
    public sealed class CharacterCreationDiagnosticRegressionTests
    {
        [Test]
        public void SingleProfileMigrationReportsRootStarterFailureWithoutPartialCharacter()
        {
            var authority = new PlayerAccountSaveAuthorityV1(
                PlayerAccountSnapshotV1.Empty(
                    Id("account.character-creation-diagnostic-regression")));
            var factory = new ThrowingStarterFactory();
            int saveCalls = 0;
            var composition = new CharacterCompositionCoordinatorV1(
                authority,
                factory,
                snapshot =>
                {
                    saveCalls++;
                    return Saved(snapshot);
                });

            try
            {
                LegacyCharacterProfileMigrationResultV1 result =
                    new LegacyCharacterProfileMigrationV1(
                        authority,
                        factory,
                        snapshot =>
                        {
                            saveCalls++;
                            return Saved(snapshot);
                        }).Migrate(new[]
                        {
                            LegacyProfile(),
                        });

                Assert.That(result.Succeeded, Is.False);
                Assert.That(
                    result.Diagnostic,
                    Does.Contain(
                        "character-create-transaction-rejected:"
                        + "character-create-threw:"
                        + "TypeInitializationException"
                        + "->InvalidOperationException:"
                        + "starter graph catalogue missing"));
                Assert.That(authority.Current.CharacterAt(0), Is.Null);
                Assert.That(composition.ActiveRuntime, Is.Null);
                Assert.That(composition.ActiveSlotIndex, Is.EqualTo(-1));
                Assert.That(saveCalls, Is.Zero);
            }
            finally
            {
                composition.Dispose();
            }
        }

        private static LegacyCharacterProfileV1 LegacyProfile()
        {
            StableId classId = Id("loadout-profile.juggernaut");
            PlayerRouteProfilePayloadV1 route =
                PlayerRouteProfilePayloadV1.Create(
                    Id("character.creation-diagnostic-regression"),
                    classId,
                    new StableId[
                        PlayerRouteProfilePayloadV1.WeaponSlotCount]);
            return new LegacyCharacterProfileV1(
                0,
                "Diagnostic Pilot",
                route.SelectedCharacterStableId,
                classId,
                route.Fingerprint,
                route);
        }

        private static PlayerAccountStoreResultV1 Saved(
            PlayerAccountSnapshotV1 snapshot)
        {
            return new PlayerAccountStoreResultV1(
                PlayerAccountStoreStatusV1.Saved,
                string.Empty,
                snapshot);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private sealed class ThrowingStarterFactory :
            ICharacterRuntimeGraphFactoryV1,
            IStarterCharacterRuntimeGraphFactoryV1
        {
            public ICharacterRuntimeGraphV1 CreateRestoreTarget(
                CharacterInstanceSnapshotV1 character)
            {
                throw new InvalidOperationException(
                    "Restore target creation was not expected.");
            }

            public ICharacterRuntimeGraphV1 CreateStarter(
                int slotIndex,
                StableId exactCharacterInstanceStableId,
                StableId classDefinitionStableId,
                string displayName,
                object legacyContext)
            {
                throw new TypeInitializationException(
                    "StarterGraphCatalog",
                    new InvalidOperationException(
                        "starter graph catalogue missing"));
            }
        }
    }
}
