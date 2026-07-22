using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.Enemies
{
    public sealed partial class EnemyAttackPatternAuthorityV1Tests
    {
        [Test]
        public void Stage1EnemyPatterns_ConsumeTheSingleSharedRunSession()
        {
            string projectRoot = Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."));
            string production = Path.Combine(
                projectRoot,
                "Assets/ShooterMover/Production/Stage1");
            string enemy = File.ReadAllText(Path.Combine(
                production,
                "Stage1PlayableLoopCompositionV1.EnemyAttackPatterns.cs"));
            string shared = File.ReadAllText(Path.Combine(
                production,
                "Stage1PlayableLoopCompositionV1.RunSession.cs"));
            string factory = File.ReadAllText(Path.Combine(
                production,
                "Stage1SharedRunCompositionPortsV1.cs"));

            Assert.That(enemy, Does.Not.Contain("new RunSessionAuthorityV1"));
            Assert.That(enemy, Does.Not.Contain("StartRunSessionCommandV1"));
            Assert.That(enemy, Does.Not.Contain("ProductionCharacterRunSessionStartSourceV1"));
            Assert.That(enemy, Does.Not.Contain("enemyPatternRunAuthority"));
            Assert.That(enemy, Does.Not.Contain("enemyPatternSimulationTick"));
            Assert.That(enemy, Does.Contain("TryResolveSharedRunSession"));

            Assert.That(Occurrences(shared, "new RunSessionAuthorityV1"),
                Is.EqualTo(1));
            Assert.That(shared,
                Does.Contain("ProductionConditionBoundRunSessionStartSourceV1"));
            Assert.That(shared, Does.Contain("AdvanceSharedRunSessionTime"));
            Assert.That(shared, Does.Contain("TickEnemyAttackPatterns"));

            Assert.That(factory,
                Does.Contain("IRunSessionNonConditionRuntimePortFactoryV1"));
            Assert.That(factory,
                Does.Not.Contain("Stage1StatusRunProjectionV1"));
            Assert.That(factory,
                Does.Not.Contain("Stage1ConditionRunProjectionV1"));
            Assert.That(factory,
                Does.Not.Contain("IRunSessionRuntimePortFactoryV1"));
        }

        private static int Occurrences(string source, string value)
        {
            int count = 0;
            int offset = 0;
            while (source != null
                && value != null
                && (offset = source.IndexOf(value, offset,
                    System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += value.Length;
            }
            return count;
        }
    }
}
