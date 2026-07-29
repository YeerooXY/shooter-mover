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
    public sealed class GunMountContractTests
    {
        [Test]
        public void FourMountState_ShuffledInput_UsesStableHudOrdering()
        {
            FourMountGunState state = new FourMountGunState(
                ReadyMount(GunMountSlot.MountFour, "rocket-launcher"),
                ReadyMount(GunMountSlot.MountTwo, "shotgun"),
                ReadyMount(GunMountSlot.MountOne, "blaster-machine-gun"),
                ReadyMount(GunMountSlot.MountThree, "arc-gun"));

            Assert.That(state.Count, Is.EqualTo(4));
            Assert.That(state.GetByHudIndex(0).Slot, Is.EqualTo(GunMountSlot.MountOne));
            Assert.That(state.GetByHudIndex(1).Slot, Is.EqualTo(GunMountSlot.MountTwo));
            Assert.That(state.GetByHudIndex(2).Slot, Is.EqualTo(GunMountSlot.MountThree));
            Assert.That(state.GetByHudIndex(3).Slot, Is.EqualTo(GunMountSlot.MountFour));
            Assert.That(
                state.GetBySlot(GunMountSlot.MountThree).GunId,
                Is.EqualTo(GunId("arc-gun")));
        }

        [Test]
        public void FourMountState_DuplicateOrMissingSlots_AreRejected()
        {
            Assert.Throws<ArgumentException>(
                () => new FourMountGunState(
                    ReadyMount(GunMountSlot.MountOne, "one"),
                    ReadyMount(GunMountSlot.MountOne, "duplicate"),
                    ReadyMount(GunMountSlot.MountThree, "three"),
                    ReadyMount(GunMountSlot.MountFour, "four")));

            Assert.Throws<ArgumentException>(
                () => new FourMountGunState(
                    ReadyMount(GunMountSlot.MountOne, "one"),
                    ReadyMount(GunMountSlot.MountTwo, "two"),
                    ReadyMount(GunMountSlot.MountThree, "three")));
        }

        [Test]
        public void SharedNormalFire_MixedReadiness_IsResolvedPerMount()
        {
            FourMountGunState mounts = new FourMountGunState(
                ReadyMount(GunMountSlot.MountOne, "blaster-machine-gun"),
                CadenceBlockedMount(GunMountSlot.MountTwo, "shotgun"),
                OverheatedMount(GunMountSlot.MountThree, "arc-gun"),
                ChargingMount(GunMountSlot.MountFour, "ricochet-gun"));
            GunArrayIntent intent = SharedIntent(power: false);

            FourMountFireResult result = new FourMountFireResult(
                intent,
                mounts,
                FiredResult(
                    mounts.GetBySlot(GunMountSlot.MountOne),
                    GunMountFireResultKind.NormalFired,
                    "normal-one"),
                NonFiredResult(
                    mounts.GetBySlot(GunMountSlot.MountTwo),
                    GunMountFireResultKind.NotReady),
                NonFiredResult(
                    mounts.GetBySlot(GunMountSlot.MountThree),
                    GunMountFireResultKind.NotReady),
                NonFiredResult(
                    mounts.GetBySlot(GunMountSlot.MountFour),
                    GunMountFireResultKind.NotReady));

            Assert.That(
                result.GetBySlot(GunMountSlot.MountOne).Kind,
                Is.EqualTo(GunMountFireResultKind.NormalFired));
            Assert.That(
                result.GetBySlot(GunMountSlot.MountTwo).Kind,
                Is.EqualTo(GunMountFireResultKind.NotReady));
            Assert.That(
                result.GetBySlot(GunMountSlot.MountThree).Kind,
                Is.EqualTo(GunMountFireResultKind.NotReady));
            Assert.That(
                result.GetBySlot(GunMountSlot.MountFour).Kind,
                Is.EqualTo(GunMountFireResultKind.NotReady));
        }

        [Test]
        public void SharedPowerFire_MixesEmpoweredFallbackUnequippedAndFaultedResults()
        {
            FourMountGunState mounts = new FourMountGunState(
                ReadyMount(
                    GunMountSlot.MountOne,
                    "blaster-machine-gun",
                    new GunPowerBankState(true, 5d, 5d, 2d)),
                ReadyMount(
                    GunMountSlot.MountTwo,
                    "shotgun",
                    new GunPowerBankState(true, 1d, 5d, 2d)),
                GunMountState.Unequipped(GunMountSlot.MountThree),
                FaultedMount(
                    GunMountSlot.MountFour,
                    "rocket-launcher",
                    new GunPowerBankState(true, 5d, 5d, 2d)));
            GunArrayIntent intent = SharedIntent(power: true);

            FourMountFireResult result = new FourMountFireResult(
                intent,
                mounts,
                NonFiredResult(
                    mounts.GetBySlot(GunMountSlot.MountFour),
                    GunMountFireResultKind.Faulted),
                FiredResult(
                    mounts.GetBySlot(GunMountSlot.MountTwo),
                    GunMountFireResultKind.NormalFallbackPowerUnavailable,
                    "fallback-two"),
                NonFiredResult(
                    mounts.GetBySlot(GunMountSlot.MountThree),
                    GunMountFireResultKind.Unequipped),
                FiredResult(
                    mounts.GetBySlot(GunMountSlot.MountOne),
                    GunMountFireResultKind.EmpoweredFired,
                    "empowered-one"));

            Assert.That(
                result.GetBySlot(GunMountSlot.MountOne).Kind,
                Is.EqualTo(GunMountFireResultKind.EmpoweredFired));
            Assert.That(
                result.GetBySlot(GunMountSlot.MountTwo).Kind,
                Is.EqualTo(GunMountFireResultKind.NormalFallbackPowerUnavailable));
            Assert.That(
                result.GetBySlot(GunMountSlot.MountThree).Kind,
                Is.EqualTo(GunMountFireResultKind.Unequipped));
            Assert.That(
                result.GetBySlot(GunMountSlot.MountFour).Kind,
                Is.EqualTo(GunMountFireResultKind.Faulted));
        }

        [Test]
        public void OneMountFault_DoesNotBlockOtherReadyMounts()
        {
            FourMountGunState mounts = new FourMountGunState(
                ReadyMount(GunMountSlot.MountOne, "one"),
                FaultedMount(GunMountSlot.MountTwo, "two", GunPowerBankState.None),
                ReadyMount(GunMountSlot.MountThree, "three"),
                ReadyMount(GunMountSlot.MountFour, "four"));

            FourMountFireResult result = new FourMountFireResult(
                SharedIntent(power: false),
                mounts,
                FiredResult(mounts.GetBySlot(GunMountSlot.MountOne), GunMountFireResultKind.NormalFired, "one"),
                NonFiredResult(mounts.GetBySlot(GunMountSlot.MountTwo), GunMountFireResultKind.Faulted),
                FiredResult(mounts.GetBySlot(GunMountSlot.MountThree), GunMountFireResultKind.NormalFired, "three"),
                FiredResult(mounts.GetBySlot(GunMountSlot.MountFour), GunMountFireResultKind.NormalFired, "four"));

            Assert.That(
                result.GetBySlot(GunMountSlot.MountTwo).Kind,
                Is.EqualTo(GunMountFireResultKind.Faulted));
            Assert.That(
                result.GetBySlot(GunMountSlot.MountOne).Kind,
                Is.EqualTo(GunMountFireResultKind.NormalFired));
            Assert.That(
                result.GetBySlot(GunMountSlot.MountThree).Kind,
                Is.EqualTo(GunMountFireResultKind.NormalFired));
            Assert.That(
                result.GetBySlot(GunMountSlot.MountFour).Kind,
                Is.EqualTo(GunMountFireResultKind.NormalFired));
        }

        [Test]
        public void UnequippedSlot_RequiresNeutralStateAndNoGunIdentity()
        {
            GunMountState unequipped = GunMountState.Unequipped(GunMountSlot.MountTwo);

            Assert.That(unequipped.IsEquipped, Is.False);
            Assert.That(unequipped.GunId, Is.Null);
            Assert.That(unequipped.Readiness, Is.EqualTo(GunMountReadiness.Unequipped));
            Assert.That(unequipped.PowerBank.IsConfigured, Is.False);
            Assert.That(unequipped.CycleResource.Kind, Is.EqualTo(GunCycleResourceKind.None));

            Assert.Throws<ArgumentException>(
                () => new GunMountState(
                    GunMountSlot.MountTwo,
                    GunId("invalid-equipped-id"),
                    GunMountReadiness.Unequipped,
                    GunCadenceState.Ready,
                    GunCycleResourceState.None,
                    GunRecoilState.None,
                    GunPowerBankState.None));
        }

        [Test]
        public void NormalFire_HasNoConsumableAmmoContract()
        {
            Assert.That(GunMountContractRules.NormalFireConsumesConsumable, Is.False);

            string[] propertyNames = typeof(GunMountState)
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

            GunArrayIntent intent = GunArrayIntent.FromPlayerIntent(frame);

            Assert.That(intent.Aim, Is.EqualTo(frame.Aim));
            Assert.That(intent.Fire, Is.EqualTo(frame.Fire));
            Assert.That(intent.PowerModifier, Is.EqualTo(frame.PowerModifier));
            Assert.That(intent.IsFireRequested, Is.True);
            Assert.That(intent.IsPowerRequested, Is.True);
            Assert.That(
                typeof(GunArrayIntent)
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Any(property => property.Name.IndexOf("Slot", StringComparison.Ordinal) >= 0),
                Is.False);
        }

        [Test]
        public void HudState_UsesCanonicalSlotOrderAndCarriesLatestPerMountResult()
        {
            FourMountGunState mounts = new FourMountGunState(
                ReadyMount(GunMountSlot.MountThree, "arc-gun"),
                ReadyMount(GunMountSlot.MountOne, "blaster-machine-gun"),
                ReadyMount(GunMountSlot.MountFour, "ricochet-gun"),
                ReadyMount(GunMountSlot.MountTwo, "shotgun"));
            GunArrayIntent intent = SharedIntent(power: false);
            FourMountFireResult fireResult = new FourMountFireResult(
                intent,
                mounts,
                FiredResult(mounts.GetBySlot(GunMountSlot.MountFour), GunMountFireResultKind.NormalFired, "four"),
                FiredResult(mounts.GetBySlot(GunMountSlot.MountTwo), GunMountFireResultKind.NormalFired, "two"),
                FiredResult(mounts.GetBySlot(GunMountSlot.MountOne), GunMountFireResultKind.NormalFired, "one"),
                FiredResult(mounts.GetBySlot(GunMountSlot.MountThree), GunMountFireResultKind.NormalFired, "three"));

            GunHudState hud = new GunHudState(mounts, fireResult);

            Assert.That(hud.Count, Is.EqualTo(4));
            for (int index = 0; index < hud.Count; index++)
            {
                GunHudSlotState slot = hud.GetByHudIndex(index);
                Assert.That(slot.HudIndex, Is.EqualTo(index));
                Assert.That(slot.Slot, Is.EqualTo(GunMountContractRules.GetSlotAtHudIndex(index)));
                Assert.That(slot.LatestFireResult.Slot, Is.EqualTo(slot.Slot));
            }
        }

        [Test]
        public void PowerBank_InvalidBounds_AreRejectedDeterministically()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new GunPowerBankState(true, 0d, 0d, 0d));
            Assert.Throws<ArgumentException>(
                () => new GunPowerBankState(true, 6d, 5d, 1d));
            Assert.Throws<ArgumentException>(
                () => new GunPowerBankState(false, 1d, 1d, 1d));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new GunPowerBankState(true, double.NaN, 5d, 1d));
        }

        [Test]
        public void CadenceResourceAndRecoil_InvalidValues_AreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GunCadenceState(-0.1d, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GunCadenceState(0d, -1));
            Assert.Throws<ArgumentException>(
                () => new GunCycleResourceState(GunCycleResourceKind.None, 1d, 1d));
            Assert.Throws<ArgumentException>(
                () => new GunCycleResourceState(GunCycleResourceKind.Heat, 6d, 5d));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new GunRecoilState(double.PositiveInfinity, 0d));
        }

        [Test]
        public void FireResult_RejectsIncorrectEmpoweredOrFallbackDisposition()
        {
            FourMountGunState mounts = new FourMountGunState(
                ReadyMount(
                    GunMountSlot.MountOne,
                    "one",
                    new GunPowerBankState(true, 5d, 5d, 2d)),
                ReadyMount(
                    GunMountSlot.MountTwo,
                    "two",
                    new GunPowerBankState(true, 1d, 5d, 2d)),
                ReadyMount(GunMountSlot.MountThree, "three"),
                ReadyMount(GunMountSlot.MountFour, "four"));
            GunArrayIntent intent = SharedIntent(power: true);

            Assert.Throws<ArgumentException>(
                () => new FourMountFireResult(
                    intent,
                    mounts,
                    FiredResult(
                        mounts.GetBySlot(GunMountSlot.MountOne),
                        GunMountFireResultKind.NormalFallbackPowerUnavailable,
                        "wrong-fallback"),
                    FiredResult(
                        mounts.GetBySlot(GunMountSlot.MountTwo),
                        GunMountFireResultKind.NormalFallbackPowerUnavailable,
                        "fallback-two"),
                    FiredResult(
                        mounts.GetBySlot(GunMountSlot.MountThree),
                        GunMountFireResultKind.NormalFallbackPowerUnavailable,
                        "fallback-three"),
                    FiredResult(
                        mounts.GetBySlot(GunMountSlot.MountFour),
                        GunMountFireResultKind.NormalFallbackPowerUnavailable,
                        "fallback-four")));

            Assert.Throws<ArgumentException>(
                () => new FourMountFireResult(
                    intent,
                    mounts,
                    FiredResult(
                        mounts.GetBySlot(GunMountSlot.MountOne),
                        GunMountFireResultKind.EmpoweredFired,
                        "empowered-one"),
                    FiredResult(
                        mounts.GetBySlot(GunMountSlot.MountTwo),
                        GunMountFireResultKind.EmpoweredFired,
                        "wrong-empowered"),
                    FiredResult(
                        mounts.GetBySlot(GunMountSlot.MountThree),
                        GunMountFireResultKind.NormalFallbackPowerUnavailable,
                        "fallback-three"),
                    FiredResult(
                        mounts.GetBySlot(GunMountSlot.MountFour),
                        GunMountFireResultKind.NormalFallbackPowerUnavailable,
                        "fallback-four")));
        }

        [Test]
        public void FireResult_RequiresCombatMessageIdentityAndKnownChannelWhenFired()
        {
            GunMountState mount = ReadyMount(GunMountSlot.MountOne, "one");

            Assert.Throws<ArgumentNullException>(
                () => new GunMountFireResult(
                    mount.Slot,
                    mount.GunId,
                    GunMountFireResultKind.NormalFired,
                    null,
                    CombatChannel.Kinetic));
            Assert.Throws<ArgumentNullException>(
                () => new GunMountFireResult(
                    mount.Slot,
                    mount.GunId,
                    GunMountFireResultKind.NormalFired,
                    EventId("event"),
                    null));
            Assert.Throws<ArgumentException>(
                () => new GunMountFireResult(
                    mount.Slot,
                    mount.GunId,
                    GunMountFireResultKind.NormalFired,
                    EventId("event"),
                    CombatChannel.System));
        }

        [Test]
        public void ContractTypes_AreGetterOnlyAndContractsAssemblyIsUnityFree()
        {
            Type[] immutableTypes =
            {
                typeof(GunCadenceState),
                typeof(GunCycleResourceState),
                typeof(GunRecoilState),
                typeof(GunPowerBankState),
                typeof(GunMountState),
                typeof(FourMountGunState),
                typeof(GunMountFireResult),
                typeof(FourMountFireResult),
                typeof(GunHudSlotState),
                typeof(GunHudState),
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

            bool hasUnityReference = typeof(GunMountState)
                .Assembly
                .GetReferencedAssemblies()
                .Any(name => name.Name.StartsWith("UnityEngine", StringComparison.Ordinal));

            Assert.That(hasUnityReference, Is.False);
        }

        private static GunArrayIntent SharedIntent(bool power)
        {
            return new GunArrayIntent(
                NormalizedIntentVector2.Create(0.5f, 0.5f),
                ButtonIntent.Held,
                power ? ButtonIntent.Held : ButtonIntent.Inactive);
        }

        private static GunMountState ReadyMount(
            GunMountSlot slot,
            string gunValue,
            GunPowerBankState powerBank = null)
        {
            return new GunMountState(
                slot,
                GunId(gunValue),
                GunMountReadiness.Ready,
                GunCadenceState.Ready,
                GunCycleResourceState.None,
                GunRecoilState.None,
                powerBank ?? GunPowerBankState.None);
        }

        private static GunMountState CadenceBlockedMount(
            GunMountSlot slot,
            string gunValue)
        {
            return new GunMountState(
                slot,
                GunId(gunValue),
                GunMountReadiness.CadenceBlocked,
                new GunCadenceState(0.25d, 0),
                GunCycleResourceState.None,
                GunRecoilState.None,
                GunPowerBankState.None);
        }

        private static GunMountState OverheatedMount(
            GunMountSlot slot,
            string gunValue)
        {
            return new GunMountState(
                slot,
                GunId(gunValue),
                GunMountReadiness.Overheated,
                GunCadenceState.Ready,
                new GunCycleResourceState(GunCycleResourceKind.Heat, 10d, 10d),
                new GunRecoilState(0.5d, 0.1d),
                GunPowerBankState.None);
        }

        private static GunMountState ChargingMount(
            GunMountSlot slot,
            string gunValue)
        {
            return new GunMountState(
                slot,
                GunId(gunValue),
                GunMountReadiness.Charging,
                GunCadenceState.Ready,
                new GunCycleResourceState(GunCycleResourceKind.Charge, 2d, 5d),
                GunRecoilState.None,
                GunPowerBankState.None);
        }

        private static GunMountState FaultedMount(
            GunMountSlot slot,
            string gunValue,
            GunPowerBankState powerBank)
        {
            return new GunMountState(
                slot,
                GunId(gunValue),
                GunMountReadiness.Faulted,
                GunCadenceState.Ready,
                GunCycleResourceState.None,
                GunRecoilState.None,
                powerBank);
        }

        private static GunMountFireResult FiredResult(
            GunMountState mount,
            GunMountFireResultKind kind,
            string eventValue)
        {
            return new GunMountFireResult(
                mount.Slot,
                mount.GunId,
                kind,
                EventId(eventValue),
                CombatChannel.Kinetic);
        }

        private static GunMountFireResult NonFiredResult(
            GunMountState mount,
            GunMountFireResultKind kind)
        {
            return new GunMountFireResult(
                mount.Slot,
                mount.GunId,
                kind,
                null,
                null);
        }

        private static StableId GunId(string value)
        {
            return StableId.Create("gun", value);
        }

        private static StableId EventId(string value)
        {
            return StableId.Create("event", value);
        }
    }
}
