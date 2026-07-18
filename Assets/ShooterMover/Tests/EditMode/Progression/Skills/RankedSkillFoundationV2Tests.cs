using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Progression.Skills;
using ShooterMover.Domain.Progression.Skills;

namespace ShooterMover.Tests.EditMode.Progression.Skills
{
    public sealed class RankedSkillFoundationV2Tests
    {
        private RankedSkillCatalogV2 catalog;
        private RankedSkillAllocationAuthorityV2 authority;

        [SetUp]
        public void SetUp()
        {
            catalog = RankedSkillSampleCatalogV2.Create();
            authority = new RankedSkillAllocationAuthorityV2(catalog);
            authority.Seed(RankedSkillAllocationSnapshotV2.Empty("p1", "striker", catalog));
        }

        [Test]
        public void FirstAllocationAndLevelUpPointAvailabilityAreImmediate()
        {
            var first = authority.Allocate(new AllocateSkillRankCommandV2("op1", "p1", "generic.movement_speed", 0, 1));
            Assert.That(first.Accepted, Is.True);
            var blocked = authority.Allocate(new AllocateSkillRankCommandV2("op2", "p1", "generic.movement_speed", 1, 1));
            Assert.That(blocked.Rejection, Is.EqualTo(SkillAllocationRejectionV2.InsufficientPoints));
            var afterLevel = authority.Allocate(new AllocateSkillRankCommandV2("op3", "p1", "generic.movement_speed", 1, 2));
            Assert.That(afterLevel.Accepted, Is.True);
        }

        [Test]
        public void DuplicateReplayAndConflictAreDeterministic()
        {
            var command = new AllocateSkillRankCommandV2("same", "p1", "generic.movement_speed", 0, 2);
            var first = authority.Allocate(command);
            var replay = authority.Allocate(command);
            var conflict = authority.Allocate(new AllocateSkillRankCommandV2("same", "p1", "generic.armor", 1, 2));
            Assert.That(replay.Fingerprint, Is.EqualTo(first.Fingerprint));
            Assert.That(conflict.Rejection, Is.EqualTo(SkillAllocationRejectionV2.DuplicateConflict));
            Assert.That(authority.Get("p1").AllocatedPoints, Is.EqualTo(1));
        }

        [Test]
        public void ClassCapsDoNotRequireDuplicateGenericDefinitions()
        {
            RankedSkillDefinitionV2 armor; RankedSkillDefinitionV2 speed;
            Assert.That(catalog.TryGet("generic.armor", out armor), Is.True);
            Assert.That(catalog.TryGet("generic.movement_speed", out speed), Is.True);
            Assert.That(armor.EffectiveMaximumRank("striker"), Is.EqualTo(6));
            Assert.That(armor.EffectiveMaximumRank("juggernaut"), Is.EqualTo(18));
            Assert.That(speed.EffectiveMaximumRank("combat_medic"), Is.EqualTo(6));
            Assert.That(speed.EffectiveMaximumRank("juggernaut"), Is.EqualTo(9));
            Assert.That(speed.EffectiveMaximumRank("striker"), Is.EqualTo(18));
        }

        [Test]
        public void WrongClassAndStaleVersionAreRejectedWithoutMutation()
        {
            var wrongClass = authority.Allocate(new AllocateSkillRankCommandV2("wrong", "p1", "striker.thruster_recovery", 1, 10));
            Assert.That(wrongClass.Rejection, Is.EqualTo(SkillAllocationRejectionV2.StaleVersion));
            authority.Seed(new RankedSkillAllocationSnapshotV2("medic", "combat_medic", 0, catalog.SchemaVersion, catalog.ContentVersion, null));
            var rejected = authority.Allocate(new AllocateSkillRankCommandV2("wrong-class", "medic", "striker.thruster_recovery", 0, 10));
            Assert.That(rejected.Rejection, Is.EqualTo(SkillAllocationRejectionV2.WrongClass));
            Assert.That(authority.Get("medic").AllocatedPoints, Is.Zero);
        }

        [Test]
        public void FifteenRanksAndMaximumRankRejectionAreSupported()
        {
            var ranks = new Dictionary<string, int> { { "generic.movement_speed", 3 } };
            authority.Seed(new RankedSkillAllocationSnapshotV2("p1", "striker", 3, catalog.SchemaVersion, catalog.ContentVersion, ranks));
            for (int i = 0; i < 15; i++)
            {
                var result = authority.Allocate(new AllocateSkillRankCommandV2("r" + i, "p1", "striker.thruster_recovery", 3 + i, 100));
                Assert.That(result.Accepted, Is.True);
            }
            var capped = authority.Allocate(new AllocateSkillRankCommandV2("r16", "p1", "striker.thruster_recovery", 18, 100));
            Assert.That(capped.Rejection, Is.EqualTo(SkillAllocationRejectionV2.MaximumRank));
        }

        [Test]
        public void SynergyActivatesAtEightEightNotEightSevenAndDisappearsAfterRespecState()
        {
            var projector = new SkillEffectProjectorV2();
            var seven = new RankedSkillAllocationSnapshotV2("p1", "striker", 1, catalog.SchemaVersion, catalog.ContentVersion,
                new Dictionary<string, int> { { "striker.thruster_recovery", 8 }, { "striker.movement_efficiency", 7 }, { "generic.movement_speed", 3 } });
            var eight = new RankedSkillAllocationSnapshotV2("p1", "striker", 2, catalog.SchemaVersion, catalog.ContentVersion,
                new Dictionary<string, int> { { "striker.thruster_recovery", 8 }, { "striker.movement_efficiency", 8 }, { "generic.movement_speed", 3 } });
            var empty = RankedSkillAllocationSnapshotV2.Empty("p1", "striker", catalog);
            Assert.That(projector.Project(catalog, seven).Apply("movement.maximum_charges", 2), Is.EqualTo(2));
            Assert.That(projector.Project(catalog, eight).Apply("movement.maximum_charges", 2), Is.EqualTo(3));
            Assert.That(projector.Project(catalog, empty).Apply("movement.maximum_charges", 2), Is.EqualTo(2));
            Assert.That(SkillRuntimeReconciliationV2.ClampCurrentCharges(3, 2, projector.Project(catalog, empty)), Is.EqualTo(2));
        }

        [Test]
        public void MilestonesAndStackingOrderAreDeterministic()
        {
            var snapshot = new RankedSkillAllocationSnapshotV2("p1", "striker", 1, catalog.SchemaVersion, catalog.ContentVersion,
                new Dictionary<string, int> { { "striker.thruster_recovery", 5 } });
            var effects = new SkillEffectProjectorV2().Project(catalog, snapshot);
            Assert.That(effects.Contributions.Any(x => x.SourceId == "striker.thruster_recovery@5"), Is.True);
            Assert.That(effects.Fingerprint, Is.EqualTo(new SkillEffectProjectorV2().Project(catalog, snapshot).Fingerprint));
        }

        [Test]
        public void MigrationRefundsRemovedAndCapReducedRanks()
        {
            var source = new RankedSkillAllocationSnapshotV2("p1", "striker", 4, "old", "old", new Dictionary<string, int> { { "removed.skill", 4 }, { "generic.armor", 8 } });
            var result = new SkillAllocationMigratorV2().Migrate(source, catalog);
            Assert.That(result.RefundedPoints, Is.EqualTo(6));
            Assert.That(result.Snapshot.RankOf("generic.armor"), Is.EqualTo(6));
            Assert.That(result.Diagnostics.Count, Is.EqualTo(2));
        }

        [Test]
        public void RespecChargesExactlyOnceReplaysAndRebuildsEffects()
        {
            var payment = new FakePayment(1000);
            var policy = new FixedPolicy(125);
            authority.Seed(new RankedSkillAllocationSnapshotV2("p1", "striker", 1, catalog.SchemaVersion, catalog.ContentVersion,
                new Dictionary<string, int> { { "striker.thruster_recovery", 8 }, { "striker.movement_efficiency", 8 }, { "generic.movement_speed", 3 } }));
            var respec = new SkillRespecOrchestratorV2(catalog, authority, policy, payment);
            var quote = respec.Quote("p1");
            var first = respec.Execute("respec-1", quote);
            var replay = respec.Execute("respec-1", quote);
            Assert.That(first.Accepted, Is.True);
            Assert.That(replay.Fingerprint, Is.EqualTo(first.Fingerprint));
            Assert.That(payment.ChargeCount, Is.EqualTo(1));
            Assert.That(payment.Balance, Is.EqualTo(875));
            Assert.That(first.After.AllocatedPoints, Is.Zero);
            Assert.That(first.Effects.Apply("movement.maximum_charges", 2), Is.EqualTo(2));
        }

        [Test]
        public void FailedPaymentAndStaleQuoteDoNotMutateAllocation()
        {
            authority.Seed(new RankedSkillAllocationSnapshotV2("p1", "striker", 1, catalog.SchemaVersion, catalog.ContentVersion, new Dictionary<string, int> { { "generic.movement_speed", 1 } }));
            var payment = new FakePayment(0);
            var respec = new SkillRespecOrchestratorV2(catalog, authority, new FixedPolicy(10), payment);
            var quote = respec.Quote("p1");
            var failed = respec.Execute("fail", quote);
            Assert.That(failed.Accepted, Is.False);
            Assert.That(authority.Get("p1").AllocatedPoints, Is.EqualTo(1));
            payment.Balance = 100;
            var stale = respec.Execute("stale", quote);
            Assert.That(stale.Rejection, Is.EqualTo(SkillRespecRejectionV2.StaleQuote));
            Assert.That(authority.Get("p1").AllocatedPoints, Is.EqualTo(1));
        }

        private sealed class FixedPolicy : ISkillRespecCostPolicyV2
        { private readonly long cost; public FixedPolicy(long cost) { this.cost = cost; } public long CalculateCost(string profileId, int allocatedPoints, long allocationVersion) => cost; }

        private sealed class FakePayment : ISkillRespecPaymentAuthorityV2
        {
            public FakePayment(long balance) { Balance = balance; }
            public long Balance { get; set; }
            public int ChargeCount { get; private set; }
            public string CurrencyId => "credits";
            public string PaymentStateFingerprint(string profileId) => SkillFingerprintV2.Hash(profileId + "|" + Balance);
            public SkillRespecPaymentResultV2 TryCharge(string operationId, string profileId, long amount, string expectedPaymentStateFingerprint)
            {
                if (expectedPaymentStateFingerprint != PaymentStateFingerprint(profileId) || Balance < amount) return new SkillRespecPaymentResultV2(false, string.Empty, PaymentStateFingerprint(profileId));
                Balance -= amount; ChargeCount++; return new SkillRespecPaymentResultV2(true, operationId + ":payment", PaymentStateFingerprint(profileId));
            }
        }
    }
}
