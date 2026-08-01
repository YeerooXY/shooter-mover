using NUnit.Framework;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Persistence.Accounts;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Application.Persistence.SaveParts;
using ShooterMover.Application.Progression.Experience;
using ShooterMover.Contracts.Progression.Experience;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;

namespace ShooterMover.Tests.EditMode.Progression.Experience
{
    public sealed class MissionExperiencePersistenceTests
    {
        [Test]
        public void SuccessfulMissionGrant_SurvivesCharacterGraphRestart()
        {
            CharacterLiveGraphFactory factory =
                CharacterLiveGraphFactory.CreateVerticalSliceDefaults();
            StableId characterId = Id("character-instance.xp-restart");
            CharacterInstanceSnapshot character = StarterCharacter(
                factory,
                characterId);
            PlayerAccountSnapshot stored = null;
            var composition = new CharacterSetupFlow(
                new PlayerAccountSaveState(Account(character)),
                factory,
                snapshot =>
                {
                    stored = snapshot;
                    return Saved(snapshot);
                });
            Assert.That(composition.Select(0).Succeeded, Is.True);
            var graph = (CharacterLiveGraph)composition.ActiveRuntime;
            StableId operation = Id("xp-operation.persisted-mission");

            PlayerExperienceGrantFact grant = graph.ExperienceAuthority.Grant(
                new PlayerExperienceGrantRequest(operation, 100L));
            CharacterSetupResult persisted = composition.PersistActive(
                Id("operation.persist-xp-restart"));

            Assert.That(grant.Status, Is.EqualTo(PlayerExperienceGrantStatus.Applied));
            Assert.That(persisted.Succeeded, Is.True, persisted.Diagnostic);
            Assert.That(stored, Is.Not.Null);
            composition.Dispose();

            var restored = new CharacterSetupFlow(
                new PlayerAccountSaveState(stored),
                factory,
                Saved);
            CharacterSetupResult selected = restored.Select(0);
            var restoredGraph = (CharacterLiveGraph)restored.ActiveRuntime;

            Assert.That(selected.Succeeded, Is.True, selected.Diagnostic);
            Assert.That(restoredGraph.ExperienceAuthority.CurrentState.Level,
                Is.EqualTo(2));
            Assert.That(restoredGraph.ExperienceAuthority.CurrentState.CumulativeExperience,
                Is.EqualTo(100L));
            Assert.That(restoredGraph.ExperienceAuthority.Grant(
                    new PlayerExperienceGrantRequest(operation, 100L)).Status,
                Is.EqualTo(PlayerExperienceGrantStatus.DuplicateNoChange));
            restored.Dispose();
        }

        [Test]
        public void AbandonedRunQuarterEnemyXp_SurvivesCharacterGraphRestart()
        {
            CharacterLiveGraphFactory factory =
                CharacterLiveGraphFactory.CreateVerticalSliceDefaults();
            CharacterInstanceSnapshot character = StarterCharacter(
                factory,
                Id("character-instance.xp-abandoned"));
            var composition = new CharacterSetupFlow(
                new PlayerAccountSaveState(Account(character)),
                factory,
                Saved);
            Assert.That(composition.Select(0).Succeeded, Is.True);
            var graph = (CharacterLiveGraph)composition.ActiveRuntime;

            long awarded = MissionExperienceRewardPolicy
                .CalculateFailedMissionExperience(30L);
            PlayerExperienceGrantFact grant = graph.ExperienceAuthority.Grant(
                new PlayerExperienceGrantRequest(
                    Id("xp-operation.abandoned-run"),
                    awarded));
            CharacterSetupResult persisted = composition.PersistActive(
                Id("operation.persist-abandoned-run"));

            Assert.That(grant.Status,
                Is.EqualTo(PlayerExperienceGrantStatus.Applied));
            Assert.That(persisted.Succeeded, Is.True, persisted.Diagnostic);
            Assert.That(graph.ExperienceAuthority.CurrentState.CumulativeExperience,
                Is.EqualTo(8L));
            Assert.That(graph.ExperienceAuthority.CurrentState.Level, Is.EqualTo(1));
            composition.Dispose();

            var restored = new CharacterSetupFlow(
                new PlayerAccountSaveState(persisted.Account),
                factory,
                Saved);
            Assert.That(restored.Select(0).Succeeded, Is.True);
            var restoredGraph = (CharacterLiveGraph)restored.ActiveRuntime;
            Assert.That(
                restoredGraph.ExperienceAuthority.CurrentState
                    .CumulativeExperience,
                Is.EqualTo(8L));
            restored.Dispose();
        }

        private static CharacterInstanceSnapshot StarterCharacter(
            CharacterLiveGraphFactory factory,
            StableId characterId)
        {
            StableId classId = Id("loadout-profile.juggernaut");
            ICharacterLiveGraph starter = factory.CreateStarter(
                0,
                characterId,
                classId,
                "XP Pilot",
                null);
            var character = new CharacterInstanceSnapshot(
                characterId,
                classId,
                0,
                "XP Pilot",
                0L,
                PlayerAccountRestoreFlow.ExportComponents(
                    starter.SaveAdapters));
            starter.Dispose();
            return character;
        }

        private static PlayerAccountSnapshot Account(
            CharacterInstanceSnapshot character)
        {
            var slots = new CharacterInstanceSnapshot[
                PlayerAccountSnapshot.CharacterSlotCount];
            slots[0] = character;
            return new PlayerAccountSnapshot(
                Id("account.player-xp-integration"),
                0L,
                slots,
                null);
        }

        private static PlayerAccountStoreResult Saved(
            PlayerAccountSnapshot snapshot)
        {
            return new PlayerAccountStoreResult(
                PlayerAccountStoreStatus.Saved,
                string.Empty,
                snapshot);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }
    }
}
