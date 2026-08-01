using NUnit.Framework;
using ShooterMover.Application.Progression.Experience;
using ShooterMover.Contracts.Progression.Experience;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Experience;
using ShooterMover.EnemyRuntimeComposition;

namespace ShooterMover.Tests.EditMode.Progression.Experience
{
    public sealed class MissionExperienceRewardTests
    {
        [Test]
        public void CurrentLevelOneMission_AwardsExactlyOneHundredXp()
        {
            ExperienceBalanceReport report = ExperienceBalanceSimulator.Simulate(
                1,
                10d,
                3,
                new[]
                {
                    new ExperienceEnemyCount(
                        MissionExperienceProfileIds.Standard,
                        1,
                        3),
                },
                1m,
                1);

            Assert.That(report.ExperiencePerMission, Is.EqualTo(100L));
            Assert.That(report.SimulatedFinalLevel, Is.EqualTo(2));
            Assert.That(report.CasualHoursToLevel100, Is.EqualTo(400d));
            Assert.That(report.EfficientHoursToLevel100, Is.EqualTo(250d));
            Assert.That(report.IdenticalRewardFiveMinuteHoursToLevel100,
                Is.EqualTo(200d));
        }

        [Test]
        public void MissionCompletionOperation_LevelsOnceAndCannotGrantTwice()
        {
            var authority = new PlayerExperience(
                PlayerExperienceCurve.CreateProduction(),
                ProgressionContext.Create(
                    1,
                    1,
                    Id("difficulty.normal"),
                    0));
            StableId operation = Id("xp-operation.mission-fixture");
            var request = new PlayerExperienceGrantRequest(operation, 100L);

            PlayerExperienceGrantFact applied = authority.Grant(request);
            PlayerExperienceGrantFact replay = authority.Grant(request);

            Assert.That(applied.Status, Is.EqualTo(PlayerExperienceGrantStatus.Applied));
            Assert.That(applied.PreviousState.Level, Is.EqualTo(1));
            Assert.That(applied.CurrentState.Level, Is.EqualTo(2));
            Assert.That(applied.LevelUpFacts.Count, Is.EqualTo(1));
            Assert.That(applied.LevelUpFacts[0].SkillPointsGranted, Is.EqualTo(1));
            Assert.That(replay.Status,
                Is.EqualTo(PlayerExperienceGrantStatus.DuplicateNoChange));
            Assert.That(authority.CurrentState.CumulativeExperience, Is.EqualTo(100L));
        }

        [TestCase("xp.enemy-light", 1, 7L)]
        [TestCase("xp.enemy-standard", 2, 15L)]
        [TestCase("xp.enemy-turret", 3, 27L)]
        [TestCase("xp.enemy-standard", 4, 35L)]
        public void EnemyReward_UsesProfileAndTier(
            string profile,
            int tier,
            long expected)
        {
            var policy = new MissionExperienceRewardPolicy();
            Assert.That(
                policy.CalculateEnemyExperience(Id(profile), tier),
                Is.EqualTo(expected));
        }

        [TestCase(0L, 0L)]
        [TestCase(1L, 0L)]
        [TestCase(2L, 1L)]
        [TestCase(15L, 4L)]
        [TestCase(30L, 8L)]
        public void FailedMission_AwardsRoundedQuarterOfEnemyXp(
            long enemyExperience,
            long expected)
        {
            Assert.That(
                MissionExperienceRewardPolicy
                    .CalculateFailedMissionExperience(enemyExperience),
                Is.EqualTo(expected));
        }

        [Test]
        public void FailedMissionOperation_CannotGrantQuarterXpTwice()
        {
            var authority = new PlayerExperience(
                PlayerExperienceCurve.CreateProduction(),
                ProgressionContext.Create(
                    1,
                    1,
                    Id("difficulty.normal"),
                    0));
            StableId operation = Id("xp-operation.failed-mission-fixture");
            var request = new PlayerExperienceGrantRequest(
                operation,
                MissionExperienceRewardPolicy
                    .CalculateFailedMissionExperience(30L));

            PlayerExperienceGrantFact applied = authority.Grant(request);
            PlayerExperienceGrantFact replay = authority.Grant(request);

            Assert.That(applied.Status,
                Is.EqualTo(PlayerExperienceGrantStatus.Applied));
            Assert.That(replay.Status,
                Is.EqualTo(PlayerExperienceGrantStatus.DuplicateNoChange));
            Assert.That(authority.CurrentState.CumulativeExperience,
                Is.EqualTo(8L));
        }

        [Test]
        public void Ledger_RecordsExactFactsAndDeduplicatesDeathReplay()
        {
            StableId run = Id("run.xp-ledger");
            StableId player = Id("participant.player");
            var ledger = new RunExperienceLedger(
                run,
                player,
                new MissionExperienceRewardPolicy());
            EnemyDeathFact death = Death(
                run,
                player,
                "death.one",
                "enemy.one",
                MissionExperienceProfileIds.Standard,
                2);

            ledger.Consume(death);
            ledger.Consume(death);
            RunExperienceLedgerSnapshot snapshot = ledger.ExportSnapshot();

            Assert.That(snapshot.EnemiesKilled, Is.EqualTo(1));
            Assert.That(snapshot.EnemyExperience, Is.EqualTo(15L));
            Assert.That(snapshot.Kills[0].DeathEventStableId, Is.EqualTo(Id("death.one")));
            Assert.That(snapshot.Kills[0].RoomStableId, Is.EqualTo(Id("room.one")));
            Assert.That(snapshot.Kills[0].KillerParticipantStableId, Is.EqualTo(player));
            Assert.That(snapshot.Kills[0].Tier, Is.EqualTo(2));
        }

        [Test]
        public void Ledger_IgnoresKillsAttributedToAnotherParticipant()
        {
            StableId run = Id("run.xp-attribution");
            var ledger = new RunExperienceLedger(
                run,
                Id("participant.player"),
                new MissionExperienceRewardPolicy());

            ledger.Consume(Death(
                run,
                Id("participant.other"),
                "death.other",
                "enemy.other",
                MissionExperienceProfileIds.Standard,
                1));

            Assert.That(ledger.ExportSnapshot().EnemiesKilled, Is.Zero);
            Assert.That(ledger.ExportSnapshot().EnemyExperience, Is.Zero);
        }

        private static EnemyDeathFact Death(
            StableId run,
            StableId killer,
            string deathId,
            string enemyId,
            StableId profile,
            int tier)
        {
            string suffix = enemyId.Replace('.', '-');
            string deathSuffix = deathId.Replace('.', '-');
            var identity = new EnemyLiveIdentity(
                Id(enemyId),
                Id("participant." + suffix),
                run,
                Id("room-runtime.one"),
                Id("room.one"),
                Id("placement." + suffix));
            return new EnemyDeathFact(
                Id(deathId),
                Id("damage." + deathSuffix),
                identity,
                Id("enemy-definition.fixture"),
                tier,
                1L,
                Id("actor.player"),
                killer,
                profile,
                Id("drop-profile.fixture"),
                EnemyActorDeathCause.IncomingDamage);
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }
    }
}
