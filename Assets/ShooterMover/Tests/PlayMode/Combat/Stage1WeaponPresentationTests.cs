#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.PlayMode.Combat
{
    public sealed class Stage1WeaponPresentationTests
    {
        private const string Ns = "ShooterMover.ContentPackages.Weapons.Stage1Presentation.";
        private static readonly Type P = T("Stage1WeaponPresentationProjector");
        private static readonly Type O = T("Stage1WeaponPresentationOptions");
        private static readonly Type C = T("Stage1WeaponPresentationCatalog");
        private static readonly Type A = T("Stage1WeaponCueArbiter");
        private static readonly Type F = T("Stage1WeaponPresentationFixture");

        [Test]
        public void SnapshotProjection_PreservesFourStableSlotsWithoutMutation()
        {
            FourMountStatusSnapshot source = Representative();
            string before = source.ToTraceString();
            object frame = Project(source, null, Options(false, null));
            Assert.That(Get<int>(frame, "Count"), Is.EqualTo(4));
            for (int i = 0; i < 4; i++)
            {
                object slot = Slot(frame, i);
                Assert.That(Get<int>(slot, "StableSlotNumber"), Is.EqualTo(i + 1));
                Assert.That(Get<string>(slot, "CriticalText"), Does.StartWith("S" + (i + 1)));
            }
            Assert.That(source.ToTraceString(), Is.EqualTo(before));
        }

        [Test]
        public void IdentityCatalog_DistinguishesAllFiveWeaponsWithoutColor()
        {
            HashSet<string> ids = new HashSet<string>();
            HashSet<string> glyphs = new HashSet<string>();
            HashSet<string> patterns = new HashSet<string>();
            foreach (object cue in (IEnumerable)StaticProperty(C, "Entries"))
            {
                Assert.That(ids.Add(Get<string>(cue, "WeaponId")), Is.True);
                Assert.That(glyphs.Add(Get<string>(cue, "Glyph")), Is.True);
                Assert.That(patterns.Add(Get<string>(cue, "Pattern")), Is.True);
            }
            Assert.That(ids.Count, Is.EqualTo(5));
            Assert.That(glyphs.Count, Is.EqualTo(5));
            Assert.That(patterns.Count, Is.EqualTo(5));
        }

        [Test]
        public void MissingReferences_FailClosedButKeepCriticalText()
        {
            FourMountStatusSnapshot source = new FourMountStatusSnapshot(
                Mount(1, "weapon.rocket-launcher", WeaponMountPhase.Ready, 5d, 10d, true, FourMountFireMode.Normal),
                Mount(2, "weapon.unregistered-prototype", WeaponMountPhase.Ready, 1d, 2d, true, FourMountFireMode.NoRecentAttempt),
                FourMountSlotStatusSnapshot.Unequipped(3), FourMountSlotStatusSnapshot.Unequipped(4));
            object frame = Project(source, null, Options(false, new[] { "wp10.audio.rocket-boom" }));
            object rocket = Slot(frame, 0);
            object unknown = Slot(frame, 1);
            Assert.That(Get<string>(rocket, "AudioId"), Is.Null);
            Assert.That(Get<string>(rocket, "EffectId"), Is.Not.Null);
            Assert.That(Get<string>(rocket, "ReferenceWarning"), Does.Contain("AUDIO REF MISSING"));
            Assert.That(Get<string>(rocket, "CriticalText"), Does.Contain("NORMAL SHOT"));
            Assert.That(Get<bool>(unknown, "IdentityKnown"), Is.False);
            Assert.That(Get<string>(unknown, "Glyph"), Is.EqualTo("?"));
            Assert.That(Get<string>(unknown, "ReferenceWarning"), Does.Contain("PACKAGE REF MISSING"));
        }

        [Test]
        public void ReducedEffects_RemovesEffectsButRetainsStateAndAudioIdentity()
        {
            object frame = Project(Representative(), null, Options(true, null));
            for (int i = 0; i < 4; i++)
            {
                object slot = Slot(frame, i);
                Assert.That(Get<int>(slot, "Pulses"), Is.Zero);
                Assert.That(Get<string>(slot, "EffectId"), Is.Null);
                Assert.That(Get<string>(slot, "CriticalText"), Is.Not.Empty);
            }
            object plan = Invoke(frame, "BuildCuePlan");
            Assert.That(Get<int>(plan, "EffectCount"), Is.Zero);
            Assert.That(Get<int>(plan, "AudioCount"), Is.LessThanOrEqualTo(2));
        }

        [Test]
        public void PowerProjection_ShowsUseSpendDepletionAndFallback()
        {
            object before = Project((FourMountStatusSnapshot)Call(F, "CreateBeforeSpendSnapshot"), null, Options(false, null));
            object after = Project(Representative(), before, Options(false, null));
            Assert.That(Get<string>(Slot(after, 0), "PowerChange"), Is.EqualTo("SPENT 1 POWER"));
            Assert.That(Get<string>(Slot(after, 1), "PowerChange"), Is.EqualTo("SPENT 3 POWER"));
            object rocket = Slot(after, 2);
            Assert.That(Get<string>(rocket, "Power"), Does.StartWith("POWER EMPTY"));
            Assert.That(Get<string>(rocket, "Mode"), Is.EqualTo("NORMAL FALLBACK - NO POWER"));
            Assert.That(Get<string>(rocket, "PowerChange"), Is.EqualTo("NO POWER -> NORMAL"));
        }

        [Test]
        public void CuePriority_IsBoundedAndLeavesWarningHeadroom()
        {
            FourMountStatusSnapshot source = new FourMountStatusSnapshot(
                Mount(1, "weapon.blaster-machine-gun", WeaponMountPhase.Firing, 3d, 4d, true, FourMountFireMode.Empowered),
                Mount(2, "weapon.shotgun", WeaponMountPhase.Firing, 9d, 12d, true, FourMountFireMode.Normal),
                Mount(3, "weapon.rocket-launcher", WeaponMountPhase.Firing, 0d, 10d, false, FourMountFireMode.NormalFallbackPowerUnavailable),
                Fault(4, "weapon.arc-gun"));
            object plan = Invoke(Project(source, null, Options(false, null)), "BuildCuePlan");
            Assert.That(Get<int>(plan, "AudioCount"), Is.EqualTo(Constant("MaximumAudioVoices")));
            Assert.That(Get<int>(plan, "EffectCount"), Is.EqualTo(Constant("MaximumEffects")));
            Assert.That(Get<int>(plan, "MaximumSelectedPriority"), Is.LessThan(Constant("ReservedEnemyWarningPriority")));
            Assert.That(Get<int>(Invoke(plan, "GetAudioRequest", 0), "StableSlotNumber"), Is.EqualTo(4));
            Assert.That(Get<int>(Invoke(plan, "GetAudioRequest", 1), "StableSlotNumber"), Is.EqualTo(1));
            TestContext.WriteLine((string)Invoke(plan, "ToTraceString"));
        }

        private static FourMountStatusSnapshot Representative()
        { return (FourMountStatusSnapshot)Call(F, "CreateRepresentativeSnapshot"); }

        private static FourMountSlotStatusSnapshot Mount(int number, string id, WeaponMountPhase phase,
            double power, double capacity, bool canEmpower, FourMountFireMode mode)
        {
            return new FourMountSlotStatusSnapshot(number, true, StableId.Parse(id), phase,
                phase == WeaponMountPhase.Ready, phase == WeaponMountPhase.Firing ? 0.1d : 0d,
                0, phase == WeaponMountPhase.Recovering ? 0.2d : 0d, WeaponCycleMode.None,
                0d, 0d, true, power, capacity, canEmpower, mode, null, null);
        }

        private static FourMountSlotStatusSnapshot Fault(int number, string id)
        {
            return new FourMountSlotStatusSnapshot(number, true, StableId.Parse(id), WeaponMountPhase.Faulted,
                false, 0d, 0, 0d, WeaponCycleMode.None, 0d, 0d, true, 1d, 4d, false,
                FourMountFireMode.Faulted, WeaponMountFaultKind.ExternalFault, "test fault");
        }

        private static object Project(FourMountStatusSnapshot snapshot, object previous, object options)
        { return Invoke(Activator.CreateInstance(P), "Project", snapshot, previous, options); }
        private static object Options(bool reduced, string[] missing)
        { return Call(O, "Create", reduced, missing); }
        private static object Slot(object frame, int index)
        { return Invoke(frame, "GetByStableIndex", index); }
        private static int Constant(string name)
        { return (int)A.GetField(name, BindingFlags.Public | BindingFlags.Static).GetValue(null); }
        private static Type T(string name)
        { return Type.GetType(Ns + name + ", Assembly-CSharp", true); }
        private static TValue Get<TValue>(object instance, string name)
        { return (TValue)instance.GetType().GetProperty(name).GetValue(instance, null); }
        private static object StaticProperty(Type type, string name)
        { return type.GetProperty(name, BindingFlags.Public | BindingFlags.Static).GetValue(null, null); }
        private static object Call(Type type, string name, params object[] args)
        { return InvokeCore(null, type, name, args); }
        private static object Invoke(object instance, string name, params object[] args)
        { return InvokeCore(instance, instance.GetType(), name, args); }
        private static object InvokeCore(object instance, Type type, string name, object[] args)
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                if (method.Name != name || method.GetParameters().Length != args.Length) continue;
                try { return method.Invoke(instance, args); }
                catch (TargetInvocationException error) { throw error.InnerException ?? error; }
            }
            throw new MissingMethodException(type.FullName, name);
        }
    }
}
#endif
