using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;
using DomainPowerBankState = ShooterMover.Domain.Combat.GunPowerBankState;

namespace ShooterMover.Tests.EditMode.Combat
{
    public sealed class GunPowerBankPolicyTests
    {
        private static readonly StableId AutomaticModule = StableId.Parse("behavior.automatic");

        [Test]
        public void FromProfile_CreatesImmutableValidatedMountLocalState()
        {
            GunLiveProfile profile = BuildProfile(100d, 25d);
            DomainPowerBankState state = DomainPowerBankState.FromProfile(profile, 60d);

            Assert.That(state.IsConfigured, Is.True);
            Assert.That(state.AvailableUnits, Is.EqualTo(60d));
            Assert.That(state.CapacityUnits, Is.EqualTo(100d));
            Assert.That(state.EmpoweredCostUnits, Is.EqualTo(25d));
            Assert.That(state.CanAffordEmpoweredFire, Is.True);
            Assert.That(state.IsEmpty, Is.False);

            Type stateType = typeof(DomainPowerBankState);
            Assert.That(
                stateType.GetConstructors(BindingFlags.Instance | BindingFlags.Public),
                Is.Empty);
            Assert.That(
                stateType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(property => property.CanWrite),
                Is.Empty);
            Assert.That(
                stateType.Assembly.GetReferencedAssemblies()
                    .Any(name => name.Name.StartsWith("UnityEngine", StringComparison.Ordinal)),
                Is.False);

            Assert.Throws<ArgumentNullException>(
                () => DomainPowerBankState.FromProfile(null, 0d));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => DomainPowerBankState.FromProfile(profile, -1d));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => DomainPowerBankState.FromProfile(profile, 101d));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => DomainPowerBankState.FromProfile(profile, double.NaN));

            GunLiveProfile noBankProfile = BuildProfile(0d, 0d, false);
            DomainPowerBankState none = DomainPowerBankState.FromProfile(noBankProfile, 0d);
            Assert.That(none.IsConfigured, Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => DomainPowerBankState.FromProfile(noBankProfile, 1d));
        }

        [Test]
        public void ConfiguredZeroCost_IsRejectedByConsumedProfileInvariant()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BuildProfile(100d, 0d));
        }

        [Test]
        public void ResolveFire_ExactCostSpendsAtomicallyAndEmptiesOnlyThatBank()
        {
            DomainPowerBankState original = DomainPowerBankState.FromProfile(
                BuildProfile(10d, 10d),
                10d);

            GunPowerFireDecision decision = GunPowerBankPolicy.ResolveFire(
                original,
                true,
                true);

            Assert.That(decision.Kind, Is.EqualTo(GunPowerFireDecisionKind.EmpoweredFired));
            Assert.That(decision.SpentUnits, Is.EqualTo(10d));
            Assert.That(decision.Fires, Is.True);
            Assert.That(decision.FiresEmpowered, Is.True);
            Assert.That(decision.FiresNormally, Is.False);
            Assert.That(decision.UpdatedState, Is.Not.SameAs(original));
            Assert.That(decision.UpdatedState.AvailableUnits, Is.EqualTo(0d));
            Assert.That(decision.UpdatedState.IsEmpty, Is.True);
            Assert.That(original.AvailableUnits, Is.EqualTo(10d));
            Assert.That(
                MapToContractResult(decision.Kind),
                Is.EqualTo(GunMountFireResultKind.EmpoweredFired));
        }

        [Test]
        public void ResolveFire_InsufficientCostFallsBackWithoutMutation()
        {
            DomainPowerBankState original = DomainPowerBankState.FromProfile(
                BuildProfile(20d, 10d),
                9d);

            GunPowerFireDecision decision = GunPowerBankPolicy.ResolveFire(
                original,
                true,
                true);

            Assert.That(
                decision.Kind,
                Is.EqualTo(GunPowerFireDecisionKind.NormalFallbackPowerUnavailable));
            Assert.That(decision.Fires, Is.True);
            Assert.That(decision.FiresNormally, Is.True);
            Assert.That(decision.FiresEmpowered, Is.False);
            Assert.That(decision.SpentUnits, Is.EqualTo(0d));
            Assert.That(decision.UpdatedState, Is.SameAs(original));
            Assert.That(original.AvailableUnits, Is.EqualTo(9d));
            Assert.That(
                MapToContractResult(decision.Kind),
                Is.EqualTo(GunMountFireResultKind.NormalFallbackPowerUnavailable));
        }

        [Test]
        public void ApplyRefill_OverfillCapsAtCapacityAndReportsUnappliedUnits()
        {
            DomainPowerBankState original = DomainPowerBankState.FromProfile(
                BuildProfile(100d, 25d),
                80d);
            GunPowerRefillCommand command = new GunPowerRefillCommand(
                50d,
                GunPowerRefillEligibility.AuthoredEligible);

            GunPowerRefillResult result = GunPowerBankPolicy.ApplyRefill(original, command);

            Assert.That(result.Kind, Is.EqualTo(GunPowerRefillResultKind.Applied));
            Assert.That(result.AppliedUnits, Is.EqualTo(20d));
            Assert.That(result.UnappliedUnits, Is.EqualTo(30d));
            Assert.That(result.UpdatedState.AvailableUnits, Is.EqualTo(100d));
            Assert.That(original.AvailableUnits, Is.EqualTo(80d));

            GunPowerRefillResult atCapacity = GunPowerBankPolicy.ApplyRefill(
                result.UpdatedState,
                new GunPowerRefillCommand(
                    5d,
                    GunPowerRefillEligibility.AuthoredEligible));

            Assert.That(atCapacity.Kind, Is.EqualTo(GunPowerRefillResultKind.NoChange));
            Assert.That(atCapacity.AppliedUnits, Is.EqualTo(0d));
            Assert.That(atCapacity.UnappliedUnits, Is.EqualTo(5d));
            Assert.That(atCapacity.UpdatedState, Is.SameAs(result.UpdatedState));
        }

        [Test]
        public void ApplyRefill_RequiresExplicitAuthoredEligibility()
        {
            DomainPowerBankState original = DomainPowerBankState.FromProfile(
                BuildProfile(100d, 25d),
                10d);

            GunPowerRefillResult result = GunPowerBankPolicy.ApplyRefill(
                original,
                new GunPowerRefillCommand(
                    50d,
                    GunPowerRefillEligibility.Ineligible));

            Assert.That(result.Kind, Is.EqualTo(GunPowerRefillResultKind.IneligibleSource));
            Assert.That(result.AppliedUnits, Is.EqualTo(0d));
            Assert.That(result.UnappliedUnits, Is.EqualTo(50d));
            Assert.That(result.UpdatedState, Is.SameAs(original));
            Assert.That(original.AvailableUnits, Is.EqualTo(10d));
        }

        [Test]
        public void RefillCommand_RejectsNegativeAndNonFiniteAmounts()
        {
            foreach (double invalid in new[]
            {
                -1d,
                double.NaN,
                double.PositiveInfinity,
                double.NegativeInfinity,
            })
            {
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => new GunPowerRefillCommand(
                        invalid,
                        GunPowerRefillEligibility.AuthoredEligible));
            }

            Assert.Throws<ArgumentOutOfRangeException>(
                () => new GunPowerRefillCommand(
                    1d,
                    (GunPowerRefillEligibility)999));
            Assert.Throws<ArgumentNullException>(
                () => GunPowerBankPolicy.ApplyRefill(null, null));
            Assert.Throws<ArgumentNullException>(
                () => GunPowerBankPolicy.ApplyRefill(
                    DomainPowerBankState.None,
                    null));
        }

        [Test]
        public void ResolveFire_SimultaneousMixedBanksRemainIndependentAndMatchContractSemantics()
        {
            GunMountSlot[] slots =
            {
                GunMountSlot.MountOne,
                GunMountSlot.MountTwo,
                GunMountSlot.MountThree,
                GunMountSlot.MountFour,
            };
            GunMountReadiness[] readiness =
            {
                GunMountReadiness.Ready,
                GunMountReadiness.Ready,
                GunMountReadiness.Ready,
                GunMountReadiness.Recovering,
            };
            DomainPowerBankState[] banks =
            {
                DomainPowerBankState.FromProfile(BuildProfile(100d, 20d), 60d),
                DomainPowerBankState.FromProfile(BuildProfile(10d, 10d), 10d),
                DomainPowerBankState.FromProfile(BuildProfile(100d, 10d), 4d),
                DomainPowerBankState.FromProfile(BuildProfile(100d, 25d), 100d),
            };
            GunPowerFireDecision[] decisions = new GunPowerFireDecision[banks.Length];

            for (int index = 0; index < banks.Length; index++)
            {
                decisions[index] = GunPowerBankPolicy.ResolveFire(
                    banks[index],
                    readiness[index] == GunMountReadiness.Ready,
                    true);
            }

            string trace = string.Join(
                " | ",
                decisions.Select(
                    (decision, index) => FormatTrace(slots[index], banks[index], decision)));

            Assert.That(
                trace,
                Is.EqualTo(
                    "MountOne:EmpoweredFired:60->40 | "
                    + "MountTwo:EmpoweredFired:10->0 | "
                    + "MountThree:NormalFallbackPowerUnavailable:4->4 | "
                    + "MountFour:NotReady:100->100"));
            CollectionAssert.AreEqual(
                new[]
                {
                    GunMountFireResultKind.EmpoweredFired,
                    GunMountFireResultKind.EmpoweredFired,
                    GunMountFireResultKind.NormalFallbackPowerUnavailable,
                    GunMountFireResultKind.NotReady,
                },
                decisions.Select(decision => MapToContractResult(decision.Kind)).ToArray());

            Assert.That(banks[0].AvailableUnits, Is.EqualTo(60d));
            Assert.That(banks[1].AvailableUnits, Is.EqualTo(10d));
            Assert.That(banks[2].AvailableUnits, Is.EqualTo(4d));
            Assert.That(banks[3].AvailableUnits, Is.EqualTo(100d));
        }

        [Test]
        public void ResolveFire_RepeatedFallbackNeverDrainsOrSuppressesNormalFire()
        {
            DomainPowerBankState state = DomainPowerBankState.FromProfile(
                BuildProfile(100d, 10d),
                3d);

            for (int attempt = 0; attempt < 100; attempt++)
            {
                GunPowerFireDecision decision = GunPowerBankPolicy.ResolveFire(
                    state,
                    true,
                    true);

                Assert.That(
                    decision.Kind,
                    Is.EqualTo(GunPowerFireDecisionKind.NormalFallbackPowerUnavailable));
                Assert.That(decision.Fires, Is.True);
                Assert.That(decision.FiresNormally, Is.True);
                Assert.That(decision.SpentUnits, Is.EqualTo(0d));
                Assert.That(decision.UpdatedState, Is.SameAs(state));
            }

            Assert.That(state.AvailableUnits, Is.EqualTo(3d));
        }

        [Test]
        public void ResolveFire_NotReadyNeverSpendsAndNormalRequestIgnoresBank()
        {
            DomainPowerBankState full = DomainPowerBankState.FullFromProfile(
                BuildProfile(100d, 25d));

            GunPowerFireDecision notReady = GunPowerBankPolicy.ResolveFire(
                full,
                false,
                true);
            GunPowerFireDecision normal = GunPowerBankPolicy.ResolveFire(
                full,
                true,
                false);

            Assert.That(notReady.Kind, Is.EqualTo(GunPowerFireDecisionKind.NotReady));
            Assert.That(notReady.Fires, Is.False);
            Assert.That(notReady.SpentUnits, Is.EqualTo(0d));
            Assert.That(notReady.UpdatedState, Is.SameAs(full));
            Assert.That(normal.Kind, Is.EqualTo(GunPowerFireDecisionKind.NormalFired));
            Assert.That(normal.FiresNormally, Is.True);
            Assert.That(normal.SpentUnits, Is.EqualTo(0d));
            Assert.That(normal.UpdatedState, Is.SameAs(full));
        }

        [Test]
        public void UnconfiguredBank_FallsBackAndRejectsRefillWithoutCreatingPower()
        {
            DomainPowerBankState none = DomainPowerBankState.FromProfile(
                BuildProfile(0d, 0d, false),
                0d);

            GunPowerFireDecision fire = GunPowerBankPolicy.ResolveFire(none, true, true);
            GunPowerRefillResult refill = GunPowerBankPolicy.ApplyRefill(
                none,
                new GunPowerRefillCommand(
                    10d,
                    GunPowerRefillEligibility.AuthoredEligible));

            Assert.That(
                fire.Kind,
                Is.EqualTo(GunPowerFireDecisionKind.NormalFallbackPowerUnavailable));
            Assert.That(fire.FiresNormally, Is.True);
            Assert.That(refill.Kind, Is.EqualTo(GunPowerRefillResultKind.BankNotConfigured));
            Assert.That(refill.AppliedUnits, Is.EqualTo(0d));
            Assert.That(refill.UnappliedUnits, Is.EqualTo(10d));
            Assert.That(refill.UpdatedState, Is.SameAs(none));
        }

        [Test]
        public void Policy_ExposesNoPassiveRegenerationOrElapsedTimeStep()
        {
            string[] methodNames = typeof(GunPowerBankPolicy)
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(method => method.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(
                new[] { "ApplyRefill", "ResolveFire" },
                methodNames);
            Assert.That(
                typeof(GunPowerBankPolicy).Assembly.GetReferencedAssemblies()
                    .Any(name => name.Name.StartsWith("UnityEngine", StringComparison.Ordinal)),
                Is.False);
        }

        private static GunLiveProfile BuildProfile(
            double capacityUnits,
            double empoweredCostUnits,
            bool hasIndependentPowerBank = true)
        {
            double authoredCapacity = hasIndependentPowerBank ? capacityUnits : 0d;
            double authoredCost = hasIndependentPowerBank ? empoweredCostUnits : 0d;

            return GunLiveProfile.Create(
                GunLiveProfile.CurrentProfileVersion,
                StableId.Parse("gun-profile.power-bank-test"),
                0.2d,
                1,
                0d,
                0d,
                GunCycleMode.None,
                0d,
                0d,
                0d,
                0d,
                hasIndependentPowerBank,
                authoredCapacity,
                authoredCost,
                0d,
                new[] { AutomaticModule },
                new[] { AutomaticModule },
                0);
        }

        private static GunMountFireResultKind MapToContractResult(
            GunPowerFireDecisionKind kind)
        {
            switch (kind)
            {
                case GunPowerFireDecisionKind.NormalFired:
                    return GunMountFireResultKind.NormalFired;
                case GunPowerFireDecisionKind.EmpoweredFired:
                    return GunMountFireResultKind.EmpoweredFired;
                case GunPowerFireDecisionKind.NormalFallbackPowerUnavailable:
                    return GunMountFireResultKind.NormalFallbackPowerUnavailable;
                case GunPowerFireDecisionKind.NotReady:
                    return GunMountFireResultKind.NotReady;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown decision kind.");
            }
        }

        private static string FormatTrace(
            GunMountSlot slot,
            DomainPowerBankState before,
            GunPowerFireDecision decision)
        {
            return slot
                + ":"
                + decision.Kind
                + ":"
                + before.AvailableUnits.ToString("0.###", CultureInfo.InvariantCulture)
                + "->"
                + decision.UpdatedState.AvailableUnits.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture);
        }
    }
}
