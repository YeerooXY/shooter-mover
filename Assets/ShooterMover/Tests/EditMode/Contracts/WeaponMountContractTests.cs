using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Input;
using ShooterMover.Contracts.Presentation;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Contracts
{
    public sealed class WeaponMountContractTests
    {
        [Test]
        public void FourMountState_ShuffledInput_UsesStableHudOrdering()
        {
            FourMountWeaponState state = new FourMountWeaponState(
                ReadyMount(WeaponMountSlot.MountFour, "rocket-launcher"),
                ReadyMount(WeaponMountSlot.MountTwo, "shotgun"),
                ReadyMount(WeaponMountSlot.MountOne, "blaster-machine-gun"),
                ReadyMount(WeaponMountSlot.MountThree, "arc-gun"));

            Assert.That(state.Count, Is.EqualTo(4));
            Assert.That(state.GetByHudIndex(0).Slot, Is.EqualTo(WeaponMountSlot.MountOne));
            Assert.That(state.GetByHudIndex(1).Slot, Is.EqualTo(WeaponMountSlot.MountTwo));
            Assert.That(state.GetByHudIndex(2).Slot, Is.EqualTo(WeaponMountSlot.MountThree));
            Assert.That(state.GetByHudIndex(3).Slot, Is.EqualTo(WeaponMountSlot.MountFour));
            Assert.That(
                state.GetBySlot(WeaponMountSlot.MountThree).WeaponId,
                Is.EqualTo(WeaponId("arc-gun")));
        }

        [Test]
        public void FourMountState_DuplicateOrMissingSlots_AreRejected()
        {
            Assert.Throws<ArgumentException>(
                () => new FourMountWeaponState(
                    ReadyMount(WeaponMountSlot.MountOne, "one"),
                    ReadyMount(WeaponMountSlot.MountOne, "duplicate"),
                    ReadyMount(WeaponMountSlot.MountThree, "three"),
                    ReadyMount(WeaponMountSlot.MountFour, "four")));

            Assert.Throws<ArgumentException>(
                () => new FourMountWeaponState(
                    ReadyMount(WeaponMountSlot.MountOne, "one"),
                    ReadyMount(WeaponMountSlot.MountTwo, "two"),
                    ReadyMount(WeaponMountSlot.MountThree, "three")));
        }

        [Test]
        public void SharedNormalFire_MixedReadiness_IsResolvedPerMount()
        {
            FourMountWeaponState mounts = new FourMountWeaponState(
                ReadyMount(WeaponMountSlot.MountOne, "blaster-machine-gun"),
                CadenceBlockedMount(WeaponMountSlot.MountTwo, "shotgun"),
                OverheatedMount(WeaponMountSlot.MountThree, "arc-gun"),
                ChargingMount(WeaponMountSlot.MountFour, "ricochet-gun"));
            WeaponArrayIntent intent = SharedIntent(power: false);

            FourMountFireResult result = new FourMountFireResult(
                intent,
                mounts,
                FiredResult(
                    mounts.GetBySlot(WeaponMountSlot.MountOne),
                    WeaponMountFireResultKind.NormalFired,
                    "normal-one"),
                NonFiredResult(
                    mounts.GetBySlot(WeaponMountSlot.MountTwo),
                    WeaponMountFireResultKind.NotReady),
                NonFiredResult(
                    mounts.GetBySlot(WeaponMountSlot.MountThree),
                    WeaponMountFireResultKind.NotReady),
                NonFiredResult(
                    mounts.GetBySlot(WeaponMountSlot.MountFour),
                    WeaponMountFireResultKind.NotReady));

            Assert.That(
                result.GetBySlot(WeaponMountSlot.MountOne).Kind,
                Is.EqualTo(WeaponMountFireResultKind.NormalFired));
            Assert.That(
                result.GetBySlot(WeaponMountSlot.MountTwo).Kind,
                Is.EqualTo(WeaponMountFireResultKind.NotReady));
            Assert.That(
                result.GetBySlot(WeaponMountSlot.MountThree).Kind,
                Is.EqualTo(WeaponMountFireResultKind.NotReady));
            Assert.That(
                result.GetBySlot(WeaponMountSlot.MountFour).Kind,
                Is.EqualTo(WeaponMountFireResultKind.NotReady));
        }

        [Test]
        public void SharedPowerFire_MixesEmpoweredFallbackUnequippedAndFaultedResults()
        {
            FourMountWeaponState mounts = new FourMountWeaponState(
                ReadyMount(
                    WeaponMountSlot.MountOne,
                    "blaster-machine-gun",
                    new WeaponPowerBankState(true, 5d, 5d, 2d)),
                ReadyMount(
                    WeaponMountSlot.MountTwo,
                    "shotgun",
                    new WeaponPowerBankState(true, 1d, 5d, 2d)),
                WeaponMountState.Unequipped(WeaponMountSlot.MountThree),
                FaultedMount(
                    WeaponMountSlot.MountFour,
                    "rocket-launcher",
                    new WeaponPowerBankState(true, 5d, 5d, 2d)));
            WeaponArrayIntent intent = SharedIntent(power: true);

            FourMountFireResult result = new FourMountFireResult(
                intent,
                mounts,
                NonFiredResult(
                    mounts.GetBySlot(WeaponMountSlot.MountFour),
                    WeaponMountFireResultKind.Faulted),
                FiredResult(
                    mounts.GetBySlot(WeaponMountSlot.MountTwo),
                    WeaponMountFireResultKind.NormalFallbackPowerUnavailable,
                    "fallback-two"),
                NonFiredResult(
                    mounts.GetBySlot(WeaponMountSlot.MountThree),
                    WeaponMountFireResultKind.Unequipped),
                FiredResult(
                    mounts.GetBySlot(WeaponMountSlot.MountOne),
                    WeaponMountFireResultKind.EmpoweredFired,
                    "empowered-one"));

            Assert.That(
                result.GetBySlot(WeaponMountSlot.MountOne).Kind,
                Is.EqualTo(WeaponMountFireResultKind.EmpoweredFired));
            Assert.That(
                result.GetBySlot(WeaponMountSlot.MountTwo).Kind,
                Is.EqualTo(WeaponMountFireResultKind.NormalFallbackPowerUnavailable));
            Assert.That(
                result.GetBySlot(WeaponMountSlot.MountThree).Kind,
                Is.EqualTo(WeaponMountFireResultKind.Unequipped));
            Assert.That(
                result.GetBySlot(WeaponMountSlot.MountFour).Kind,
                Is.EqualTo(WeaponMountFireResultKind.Faulted));
        }

        [Test]
        public void OneMountFault_DoesNotBlockOtherReadyMounts()
        {
            FourMountWeaponState mounts = new FourMountWeaponState(
                ReadyMount(WeaponMountSlot.MountOne, "one"),
                FaultedMount(WeaponMountSlot.MountTwo, "two", WeaponPowerBankState.None),
                ReadyMount(WeaponMountSlot.MountThree, "three"),
                ReadyMount(WeaponMountSlot.MountFour, "four"));

            FourMountFireResult result = new FourMountFireResult(
                SharedIntent(power: false),
                mounts,
                FiredResult(mounts.GetBySlot(WeaponMountSlot.MountOne), WeaponMountFireResultKind.NormalFired, "one"),
                NonFiredResult(mounts.GetBySlot(WeaponMountSlot.MountTwo), WeaponMountFireResultKind.Faulted),
                FiredResult(mounts.GetBySlot(WeaponMountSlot.MountThree), WeaponMountFireResultKind.NormalFired, "three"),
                FiredResult(mounts.GetBySlot(WeaponMountSlot.MountFour), WeaponMountFireResultKind.NormalFired, "four"));

            Assert.That(
                result.GetBySlot(WeaponMountSlot.MountTwo).Kind,
                Is.EqualTo(WeaponMountFireResultKind.Faulted));
            Assert.That(
                result.GetBySlot(WeaponMountSlot.MountOne).Kind,
                Is.EqualTo(WeaponMountFireResultKind.NormalFired));
            Assert.That(
                result.GetBySlot(WeaponMountSlot.MountThree).Kind,
                Is.EqualTo(WeaponMountFireResultKind.NormalFired));
            Assert.That(
                result.GetBySlot(WeaponMountSlot.MountFour).Kind,
                Is.EqualTo(WeaponMountFireResultKind.NormalFired));
        }

        [Test]
        public void UnequippedSlot_RequiresNeutralStateAndNoWeaponIdentity()
        {
            WeaponMountState unequipped = WeaponMountState.Unequipped(WeaponMountSlot.MountTwo);

            Assert.That(unequipped.IsEquipped, Is.False);
            Assert.That(unequipped.WeaponId, Is.Null);
            Assert.That(unequipped.Readiness, Is.EqualTo(WeaponMountReadiness.Unequipped));
            Assert.That(unequipped.PowerBank.IsConfigured, Is.False);
            Assert.That(unequipped.CycleResource.Kind, Is.EqualTo(WeaponCycleResourceKind.None));

            Assert.Throws<ArgumentException>(
                () => new WeaponMountState(
                    WeaponMountSlot.MountTwo,
                    WeaponId("invalid-equipped-id"),
                    WeaponMountReadiness.Unequipped,
                    WeaponCadenceState.Ready,
                    WeaponCycleResourceState.None,
                    WeaponRecoilState.None,
                    WeaponPowerBankState.None));
        }

        [Test]
        public void NormalFire_HasNoConsumableAmmoContract()
        {
            Assert.That(WeaponMountContractRules.NormalFireConsumesConsumable, Is.False);

            string[] propertyNames = typeof(WeaponMountState)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.Name)
                .ToArray();

            Assert.That(propertyNames, Does.Not.Contain("NormalAmmo"));
            Assert.That(propertyNames, Does.Not.Contain("Magazine"));
            Assert.That(propertyNames, Does.Not.Contain("Reload"));
        }

        [Test]
        public void PlayerIntentFrame_ProjectsOneSharedAimFireAndPowerIntent()
        {
            PlayerIntentFrame frame = new PlayerIntentFrame(
                NormalizedIntentVector2.Create(1f, 0f),
                NormalizedIntentVector2.Create(0.25f, -0.5f),
                ButtonIntent.Pressed,
                ButtonIntent.Held,
                ButtonIntent.Inactive,
                ButtonIntent.Inactive,
                ButtonIntent.Inactive,
                ButtonIntent.Inactive,
                NormalizedIntentVector2.Zero);

            WeaponArrayIntent intent = WeaponArrayIntent.FromPlayerIntent(frame);

            Assert.That(intent.Aim, Is.EqualTo(frame.Aim));
            Assert.That(intent.Fire, Is.EqualTo(frame.Fire));
            Assert.That(intent.PowerModifier, Is.EqualTo(frame.PowerModifier));
            Assert.That(intent.IsFireRequested, Is.True);
            Assert.That(intent.IsPowerRequested, Is.True);
            Assert.That(
                typeof(WeaponArrayIntent)
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Any(property => property.Name.IndexOf("Slot", StringComparison.Ordinal) >= 0),
                Is.False);
        }

        [Test]
        public void HudState_UsesCanonicalSlotOrderAndCarriesLatestPerMountResult()
        {
            FourMountWeaponState mounts = new FourMountWeaponState(
                ReadyMount(WeaponMountSlot.MountThree, "arc-gun"),
                ReadyMount(WeaponMountSlot.MountOne, "blaster-machine-gun"),
                ReadyMount(WeaponMountSlot.MountFour, "ricochet-gun"),
                ReadyMount(WeaponMountSlot.MountTwo, "shotgun"));
            WeaponArrayIntent intent = SharedIntent(power: false);
            FourMountFireResult fireResult = new FourMountFireResult(
                intent,
                mounts,
                FiredResult(mounts.GetBySlot(WeaponMountSlot.MountFour), WeaponMountFireResultKind.NormalFired, "four"),
                FiredResult(mounts.GetBySlot(WeaponMountSlot.MountTwo), WeaponMountFireResultKind.NormalFired, "two"),
                FiredResult(mounts.GetBySlot(WeaponMountSlot.MountOne), WeaponMountFireResultKind.NormalFired, "one"),
                FiredResult(mounts.GetBySlot(WeaponMountSlot.MountThree), WeaponMountFireResultKind.NormalFired, "three"));

            WeaponHudState hud = new WeaponHudState(mounts, fireResult);

            Assert.That(hud.Count, Is.EqualTo(4));
            for (int index = 0; index < hud.Count; index++)
            {
                WeaponHudSlotState slot = hud.GetByHudIndex(index);
                Assert.That(slot.HudIndex, Is.EqualTo(index));
                Assert.That(slot.Slot, Is.EqualTo(WeaponMountContractRules.GetSlotAtHudIndex(index)));
                Assert.That(slot.LatestFireResult.Slot, Is.EqualTo(slot.Slot));
            }
        }

        [Test]
        public void PowerBank_InvalidBounds_AreRejectedDeterministically()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new WeaponPowerBankState(true, 0d, 0d, 0d));
            Assert.Throws<ArgumentException>(
                () => new WeaponPowerBankState(true, 6d, 5d, 1d));
            Assert.Throws<ArgumentException>(
                () => new WeaponPowerBankState(false, 1d, 1d, 1d));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new WeaponPowerBankState(true, double.NaN, 5d, 1d));
        }

        [Test]
        public void CadenceResourceAndRecoil_InvalidValues_AreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponCadenceState(-0.1d, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponCadenceState(0d, -1));
            Assert.Throws<ArgumentException>(
                () => new WeaponCycleResourceState(WeaponCycleResourceKind.None, 1d, 1d));
            Assert.Throws<ArgumentException>(
                () => new WeaponCycleResourceState(WeaponCycleResourceKind.Heat, 6d, 5d));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new WeaponRecoilState(double.PositiveInfinity, 0d));
        }

        [Test]
        public void FireResult_RejectsIncorrectEmpoweredOrFallbackDisposition()
        {
            FourMountWeaponState mounts = new FourMountWeaponState(
                ReadyMount(
                    WeaponMountSlot.MountOne,
                    "one",
                    new WeaponPowerBankState(true, 5d, 5d, 2d)),
                ReadyMount(
                    WeaponMountSlot.MountTwo,
                    "two",
                    new WeaponPowerBankState(true, 1d, 5d, 2d)),
                ReadyMount(WeaponMountSlot.MountThree, "three"),
                ReadyMount(WeaponMountSlot.MountFour, "four"));
            WeaponArrayIntent intent = SharedIntent(power: true);

            Assert.Throws<ArgumentException>(
                () => new FourMountFireResult(
                    intent,
                    mounts,
                    FiredResult(
                        mounts.GetBySlot(WeaponMountSlot.MountOne),
                        WeaponMountFireResultKind.NormalFallbackPowerUnavailable,
                        "wrong-fallback"),
                    FiredResult(
                        mounts.GetBySlot(WeaponMountSlot.MountTwo),
                        WeaponMountFireResultKind.NormalFallbackPowerUnavailable,
                        "fallback-two"),
                    FiredResult(
                        mounts.GetBySlot(WeaponMountSlot.MountThree),
                        WeaponMountFireResultKind.NormalFallbackPowerUnavailable,
                        "fallback-three"),
                    FiredResult(
                        mounts.GetBySlot(WeaponMountSlot.MountFour),
                        WeaponMountFireResultKind.NormalFallbackPowerUnavailable,
                        "fallback-four")));

            Assert.Throws<ArgumentException>(
                () => new FourMountFireResult(
                    intent,
                    mounts,
                    FiredResult(
                        mounts.GetBySlot(WeaponMountSlot.MountOne),
                        WeaponMountFireResultKind.EmpoweredFired,
                        "empowered-one"),
                    FiredResult(
                        mounts.GetBySlot(WeaponMountSlot.MountTwo),
                        WeaponMountFireResultKind.EmpoweredFired,
                        "wrong-empowered"),
                    FiredResult(
                        mounts.GetBySlot(WeaponMountSlot.MountThree),
                        WeaponMountFireResultKind.NormalFallbackPowerUnavailable,
                        "fallback-three"),
                    FiredResult(
                        mounts.GetBySlot(WeaponMountSlot.MountFour),
                        WeaponMountFireResultKind.NormalFallbackPowerUnavailable,
                        "fallback-four")));
        }

        [Test]
        public void FireResult_RequiresCombatMessageIdentityAndKnownChannelWhenFired()
        {
            WeaponMountState mount = ReadyMount(WeaponMountSlot.MountOne, "one");

            Assert.Throws<ArgumentNullException>(
                () => new WeaponMountFireResult(
                    mount.Slot,
                    mount.WeaponId,
                    WeaponMountFireResultKind.NormalFired,
                    null,
                    CombatChannel.Kinetic));
            Assert.Throws<ArgumentNullException>(
                () => new WeaponMountFireResult(
                    mount.Slot,
                    mount.WeaponId,
                    WeaponMountFireResultKind.NormalFired,
                    EventId("event"),
                    null));
            Assert.Throws<ArgumentException>(
                () => new WeaponMountFireResult(
                    mount.Slot,
                    mount.WeaponId,
                    WeaponMountFireResultKind.NormalFired,
                    EventId("event"),
                    CombatChannel.System));
        }

        [Test]
        public void ContractTypes_AreGetterOnlyAndContractsAssemblyIsUnityFree()
        {
            Type[] immutableTypes =
            {
                typeof(WeaponCadenceState),
                typeof(WeaponCycleResourceState),
                typeof(WeaponRecoilState),
                typeof(WeaponPowerBankState),
                typeof(WeaponMountState),
                typeof(FourMountWeaponState),
                typeof(WeaponMountFireResult),
                typeof(FourMountFireResult),
                typeof(WeaponHudSlotState),
                typeof(WeaponHudState),
            };

            foreach (Type type in immutableTypes)
            {
                Assert.That(
                    type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                        .All(property => !property.CanWrite),
                    Is.True,
                    type.FullName);
                Assert.That(
                    type.GetFields(BindingFlags.Instance | BindingFlags.Public),
                    Is.Empty,
                    type.FullName);
            }

            bool hasUnityReference = typeof(WeaponMountState)
                .Assembly
                .GetReferencedAssemblies()
                .Any(name => name.Name.StartsWith("UnityEngine", StringComparison.Ordinal));

            Assert.That(hasUnityReference, Is.False);
        }

        private static WeaponArrayIntent SharedIntent(bool power)
        {
            return new WeaponArrayIntent(
                NormalizedIntentVector2.Create(0.5f, 0.5f),
                ButtonIntent.Held,
                power ? ButtonIntent.Held : ButtonIntent.Inactive);
        }

        private static WeaponMountState ReadyMount(
            WeaponMountSlot slot,
            string weaponValue,
            WeaponPowerBankState powerBank = null)
        {
            return new WeaponMountState(
                slot,
                WeaponId(weaponValue),
                WeaponMountReadiness.Ready,
                WeaponCadenceState.Ready,
                WeaponCycleResourceState.None,
                WeaponRecoilState.None,
                powerBank ?? WeaponPowerBankState.None);
        }

        private static WeaponMountState CadenceBlockedMount(
            WeaponMountSlot slot,
            string weaponValue)
        {
            return new WeaponMountState(
                slot,
                WeaponId(weaponValue),
                WeaponMountReadiness.CadenceBlocked,
                new WeaponCadenceState(0.25d, 0),
                WeaponCycleResourceState.None,
                WeaponRecoilState.None,
                WeaponPowerBankState.None);
        }

        private static WeaponMountState OverheatedMount(
            WeaponMountSlot slot,
            string weaponValue)
        {
            return new WeaponMountState(
                slot,
                WeaponId(weaponValue),
                WeaponMountReadiness.Overheated,
                WeaponCadenceState.Ready,
                new WeaponCycleResourceState(WeaponCycleResourceKind.Heat, 10d, 10d),
                new WeaponRecoilState(0.5d, 0.1d),
                WeaponPowerBankState.None);
        }

        private static WeaponMountState ChargingMount(
            WeaponMountSlot slot,
            string weaponValue)
        {
            return new WeaponMountState(
                slot,
                WeaponId(weaponValue),
                WeaponMountReadiness.Charging,
                WeaponCadenceState.Ready,
                new WeaponCycleResourceState(WeaponCycleResourceKind.Charge, 2d, 5d),
                WeaponRecoilState.None,
                WeaponPowerBankState.None);
        }

        private static WeaponMountState FaultedMount(
            WeaponMountSlot slot,
            string weaponValue,
            WeaponPowerBankState powerBank)
        {
            return new WeaponMountState(
                slot,
                WeaponId(weaponValue),
                WeaponMountReadiness.Faulted,
                WeaponCadenceState.Ready,
                WeaponCycleResourceState.None,
                WeaponRecoilState.None,
                powerBank);
        }

        private static WeaponMountFireResult FiredResult(
            WeaponMountState mount,
            WeaponMountFireResultKind kind,
            string eventValue)
        {
            return new WeaponMountFireResult(
                mount.Slot,
                mount.WeaponId,
                kind,
                EventId(eventValue),
                CombatChannel.Kinetic);
        }

        private static WeaponMountFireResult NonFiredResult(
            WeaponMountState mount,
            WeaponMountFireResultKind kind)
        {
            return new WeaponMountFireResult(
                mount.Slot,
                mount.WeaponId,
                kind,
                null,
                null);
        }

        private static StableId WeaponId(string value)
        {
            return StableId.Create("weapon", value);
        }

        private static StableId EventId(string value)
        {
            return StableId.Create("event", value);
        }
    }
}
