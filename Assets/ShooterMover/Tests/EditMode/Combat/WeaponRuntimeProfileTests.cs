using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Combat
{
    public sealed class WeaponRuntimeProfileTests
    {
        private static readonly StableId AutomaticModule = StableId.Parse("behavior.automatic");
        private static readonly StableId ProjectileModule = StableId.Parse("behavior.projectile");
        private static readonly StableId SpreadModule = StableId.Parse("behavior.spread");
        private static readonly StableId ChainModule = StableId.Parse("behavior.chain");

        [Test]
        public void Create_ProducesImmutableEngineFreeFourMountProfile()
        {
            WeaponRuntimeProfile profile = Build(new FixtureValues());

            Assert.That(profile.ProfileVersion, Is.EqualTo(1));
            Assert.That(profile.ProfileId, Is.EqualTo(StableId.Parse("weapon-profile.synthetic")));
            Assert.That(WeaponRuntimeProfile.SupportedMountCount, Is.EqualTo(4));
            Assert.That(WeaponRuntimeProfile.NormalFireConsumesConsumable, Is.False);
            Assert.That(profile.BehaviorModuleCount, Is.EqualTo(2));
            Assert.That(profile.GetBehaviorModuleId(0), Is.EqualTo(AutomaticModule));
            Assert.That(profile.GetBehaviorModuleId(1), Is.EqualTo(ProjectileModule));
            Assert.That(profile.Fingerprint, Does.StartWith("sha256:"));
            Assert.That(profile.Fingerprint, Has.Length.EqualTo(71));
            Assert.That(
                profile.DeterministicIdentity.Namespace,
                Is.EqualTo(WeaponRuntimeProfile.DeterministicIdentityNamespace));
            Assert.That(profile.DeterministicIdentity.Value, Has.Length.EqualTo(64));

            Type profileType = typeof(WeaponRuntimeProfile);
            Assert.That(profileType.IsSealed, Is.True);
            Assert.That(profileType.GetConstructors(BindingFlags.Instance | BindingFlags.Public), Is.Empty);
            Assert.That(
                profileType
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(property => property.CanWrite),
                Is.Empty);
            Assert.That(
                profileType.Assembly.GetReferencedAssemblies()
                    .Any(name => name.Name.StartsWith("UnityEngine", StringComparison.Ordinal)),
                Is.False);

            Assert.DoesNotThrow(() => WeaponRuntimeProfileValidator.Validate(profile, KnownModules()));
        }

        [Test]
        public void ModuleInput_IsDefensivelyCopied()
        {
            List<StableId> moduleIds = new List<StableId>
            {
                AutomaticModule,
                ProjectileModule,
            };
            FixtureValues values = new FixtureValues { BehaviorModuleIds = moduleIds };
            WeaponRuntimeProfile profile = Build(values);
            string canonical = profile.ToCanonicalString();

            moduleIds[0] = SpreadModule;
            moduleIds.Add(ChainModule);

            Assert.That(profile.BehaviorModuleCount, Is.EqualTo(2));
            Assert.That(profile.GetBehaviorModuleId(0), Is.EqualTo(AutomaticModule));
            Assert.That(profile.GetBehaviorModuleId(1), Is.EqualTo(ProjectileModule));
            Assert.That(profile.ToCanonicalString(), Is.EqualTo(canonical));
            Assert.Throws<ArgumentOutOfRangeException>(() => profile.GetBehaviorModuleId(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => profile.GetBehaviorModuleId(2));
        }

        [Test]
        public void CanonicalRoundTrip_ProducesIdenticalFingerprintAndIdentity()
        {
            WeaponRuntimeProfile first = Build(new FixtureValues());
            WeaponRuntimeProfile second = WeaponRuntimeProfile.ParseCanonical(
                first.ToCanonicalString(),
                KnownModules().Reverse());

            Assert.That(second, Is.EqualTo(first));
            Assert.That(second == first, Is.True);
            Assert.That(second != first, Is.False);
            Assert.That(second.GetHashCode(), Is.EqualTo(first.GetHashCode()));
            Assert.That(second.Fingerprint, Is.EqualTo(first.Fingerprint));
            Assert.That(second.DeterministicIdentity, Is.EqualTo(first.DeterministicIdentity));
            Assert.That(second.ToCanonicalString(), Is.EqualTo(first.ToCanonicalString()));
        }

        [Test]
        public void Fingerprint_IsSha256OfCanonicalUtf8Bytes()
        {
            WeaponRuntimeProfile profile = Build(new FixtureValues());
            byte[] digest;
            using (SHA256 sha256 = SHA256.Create())
            {
                digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(profile.ToCanonicalString()));
            }

            string expected = "sha256:" + string.Concat(
                digest.Select(value => value.ToString("x2")));

            Assert.That(profile.Fingerprint, Is.EqualTo(expected));
            Assert.That(
                profile.DeterministicIdentity.ToString(),
                Is.EqualTo("weapon-runtime." + expected.Substring("sha256:".Length)));
        }

        [Test]
        public void EveryMeaningfulFieldChange_ChangesDeterministicIdentity()
        {
            WeaponRuntimeProfile baseline = Build(new FixtureValues());
            NamedMutation[] mutations =
            {
                Change("profile ID", values => values.ProfileId = StableId.Parse("weapon-profile.changed")),
                Change("cadence", values => values.CadenceSeconds = 0.25d),
                Change("burst count", values => values.BurstShotCount = 4),
                Change("burst interval", values => values.BurstShotIntervalSeconds = 0.06d),
                Change("recovery", values => values.RecoverySeconds = 0.45d),
                ChangeToChargeMode(),
                Change("heat capacity", values => values.HeatCapacityUnits = 11d),
                Change("heat per shot", values => values.HeatPerShotUnits = 2.5d),
                Change("heat recovery", values => values.HeatRecoveryUnitsPerSecond = 4d),
                Change(
                    "power-bank absence",
                    values =>
                    {
                        values.HasIndependentPowerBank = false;
                        values.PowerBankCapacityUnits = 0d;
                        values.EmpoweredCostUnits = 0d;
                    }),
                Change("power-bank capacity", values => values.PowerBankCapacityUnits = 120d),
                Change("empowered cost", values => values.EmpoweredCostUnits = 20d),
                Change("recoil influence", values => values.RecoilInfluence = 0.4d),
                Change(
                    "module identity",
                    values => values.BehaviorModuleIds = new[] { AutomaticModule, SpreadModule }),
                Change(
                    "module order",
                    values => values.BehaviorModuleIds = new[] { ProjectileModule, AutomaticModule }),
                Change("presentation priority", values => values.PresentationPriority = 11),
            };

            foreach (NamedMutation mutation in mutations)
            {
                FixtureValues values = new FixtureValues();
                mutation.Apply(values);
                WeaponRuntimeProfile changed = Build(values);

                Assert.That(
                    changed.Fingerprint,
                    Is.Not.EqualTo(baseline.Fingerprint),
                    mutation.Name + " did not affect the fingerprint.");
                Assert.That(
                    changed.DeterministicIdentity,
                    Is.Not.EqualTo(baseline.DeterministicIdentity),
                    mutation.Name + " did not affect deterministic identity.");
            }
        }

        [Test]
        public void KnownModuleRegistryOrderAndExtraEntries_DoNotChangeIdentity()
        {
            FixtureValues values = new FixtureValues();
            WeaponRuntimeProfile first = Build(
                values,
                new[] { AutomaticModule, ProjectileModule, SpreadModule, ChainModule });
            WeaponRuntimeProfile second = Build(
                new FixtureValues(),
                new[] { ChainModule, SpreadModule, ProjectileModule, AutomaticModule });

            Assert.That(second, Is.EqualTo(first));
            Assert.That(second.DeterministicIdentity, Is.EqualTo(first.DeterministicIdentity));
        }

        [Test]
        public void Create_RejectsInvalidNumerics()
        {
            foreach (double invalid in new[]
            {
                double.NaN,
                double.PositiveInfinity,
                double.NegativeInfinity,
            })
            {
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => Build(new FixtureValues { CadenceSeconds = invalid }));
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => Build(new FixtureValues { BurstShotIntervalSeconds = invalid }));
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => Build(new FixtureValues { RecoverySeconds = invalid }));
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => Build(new FixtureValues { HeatCapacityUnits = invalid }));
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => Build(new FixtureValues { HeatPerShotUnits = invalid }));
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => Build(new FixtureValues { HeatRecoveryUnitsPerSecond = invalid }));
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => Build(new FixtureValues { PowerBankCapacityUnits = invalid }));
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => Build(new FixtureValues { EmpoweredCostUnits = invalid }));
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => Build(new FixtureValues { RecoilInfluence = invalid }));
            }

            FixtureValues chargeValues = ChargeValues();
            chargeValues.ChargeSeconds = double.NaN;
            Assert.Throws<ArgumentOutOfRangeException>(() => Build(chargeValues));
        }

        [Test]
        public void Create_RejectsInvalidCadenceBurstAndRecovery()
        {
            NamedMutation[] invalidMutations =
            {
                Change("zero cadence", values => values.CadenceSeconds = 0d),
                Change("zero burst count", values => values.BurstShotCount = 0),
                Change("too many burst shots", values => values.BurstShotCount = 65),
                Change(
                    "non-burst interval",
                    values =>
                    {
                        values.BurstShotCount = 1;
                        values.BurstShotIntervalSeconds = 0.01d;
                    }),
                Change("zero multi-shot interval", values => values.BurstShotIntervalSeconds = 0d),
                Change("interval beyond cadence", values => values.BurstShotIntervalSeconds = 0.21d),
                Change("negative recovery", values => values.RecoverySeconds = -0.01d),
            };

            foreach (NamedMutation mutation in invalidMutations)
            {
                FixtureValues values = new FixtureValues();
                mutation.Apply(values);
                Assert.Throws<Exception>(
                    () => Build(values),
                    mutation.Name + " should have failed validation.");
            }
        }

        [Test]
        public void Create_AcceptsNoneHeatAndChargeModesSeparately()
        {
            FixtureValues noneValues = new FixtureValues
            {
                CycleMode = WeaponCycleMode.None,
                HeatCapacityUnits = 0d,
                HeatPerShotUnits = 0d,
                HeatRecoveryUnitsPerSecond = 0d,
                ChargeSeconds = 0d,
            };
            WeaponRuntimeProfile none = Build(noneValues);
            WeaponRuntimeProfile heat = Build(new FixtureValues());
            WeaponRuntimeProfile charge = Build(ChargeValues());

            Assert.That(none.CycleMode, Is.EqualTo(WeaponCycleMode.None));
            Assert.That(heat.CycleMode, Is.EqualTo(WeaponCycleMode.Heat));
            Assert.That(charge.CycleMode, Is.EqualTo(WeaponCycleMode.Charge));
            Assert.That(charge.ChargeSeconds, Is.EqualTo(0.75d));
        }

        [Test]
        public void Create_RejectsConflictingHeatAndChargeModes()
        {
            Assert.Throws<ArgumentException>(
                () => Build(new FixtureValues { ChargeSeconds = 0.5d }));

            FixtureValues chargeWithHeat = ChargeValues();
            chargeWithHeat.HeatCapacityUnits = 10d;
            chargeWithHeat.HeatPerShotUnits = 2d;
            chargeWithHeat.HeatRecoveryUnitsPerSecond = 3d;
            Assert.Throws<ArgumentException>(() => Build(chargeWithHeat));

            FixtureValues noneWithHeat = new FixtureValues
            {
                CycleMode = WeaponCycleMode.None,
                ChargeSeconds = 0d,
            };
            Assert.Throws<ArgumentException>(() => Build(noneWithHeat));

            Assert.Throws<ArgumentOutOfRangeException>(
                () => Build(new FixtureValues { CycleMode = (WeaponCycleMode)999 }));
        }

        [Test]
        public void Create_RejectsInvalidHeatAndChargeConfiguration()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Build(new FixtureValues { HeatCapacityUnits = 0d }));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Build(new FixtureValues { HeatPerShotUnits = 0d }));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Build(new FixtureValues { HeatRecoveryUnitsPerSecond = 0d }));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Build(new FixtureValues { HeatPerShotUnits = 11d }));

            FixtureValues zeroCharge = ChargeValues();
            zeroCharge.ChargeSeconds = 0d;
            Assert.Throws<ArgumentOutOfRangeException>(() => Build(zeroCharge));
        }

        [Test]
        public void Create_RejectsZeroEmpoweredCostAndInvalidOptionalPowerBank()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Build(new FixtureValues { EmpoweredCostUnits = 0d }));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Build(new FixtureValues { PowerBankCapacityUnits = 0d }));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Build(new FixtureValues { EmpoweredCostUnits = 101d }));

            FixtureValues absentWithCapacity = new FixtureValues
            {
                HasIndependentPowerBank = false,
                EmpoweredCostUnits = 0d,
            };
            Assert.Throws<ArgumentException>(() => Build(absentWithCapacity));

            FixtureValues absentWithCost = new FixtureValues
            {
                HasIndependentPowerBank = false,
                PowerBankCapacityUnits = 0d,
            };
            Assert.Throws<ArgumentException>(() => Build(absentWithCost));

            FixtureValues absent = new FixtureValues
            {
                HasIndependentPowerBank = false,
                PowerBankCapacityUnits = 0d,
                EmpoweredCostUnits = 0d,
            };
            Assert.DoesNotThrow(() => Build(absent));
        }

        [Test]
        public void ProfilesCarryIndependentOptionalPowerBankConfiguration()
        {
            WeaponRuntimeProfile configured = Build(new FixtureValues());
            WeaponRuntimeProfile absent = Build(new FixtureValues
            {
                ProfileId = StableId.Parse("weapon-profile.no-power-bank"),
                HasIndependentPowerBank = false,
                PowerBankCapacityUnits = 0d,
                EmpoweredCostUnits = 0d,
            });

            Assert.That(configured.HasIndependentPowerBank, Is.True);
            Assert.That(configured.PowerBankCapacityUnits, Is.EqualTo(100d));
            Assert.That(configured.EmpoweredCostUnits, Is.EqualTo(25d));
            Assert.That(absent.HasIndependentPowerBank, Is.False);
            Assert.That(absent.PowerBankCapacityUnits, Is.Zero);
            Assert.That(absent.EmpoweredCostUnits, Is.Zero);
            Assert.That(absent.DeterministicIdentity, Is.Not.EqualTo(configured.DeterministicIdentity));
        }

        [Test]
        public void Create_RejectsMissingUnknownNullAndDuplicateBehaviorModules()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Build(new FixtureValues { BehaviorModuleIds = new StableId[0] }));
            Assert.Throws<ArgumentException>(
                () => Build(new FixtureValues
                {
                    BehaviorModuleIds = new[] { AutomaticModule, StableId.Parse("behavior.unknown") },
                }));
            Assert.Throws<ArgumentException>(
                () => Build(new FixtureValues
                {
                    BehaviorModuleIds = new[] { AutomaticModule, null },
                }));
            Assert.Throws<ArgumentException>(
                () => Build(new FixtureValues
                {
                    BehaviorModuleIds = new[] { AutomaticModule, AutomaticModule },
                }));
            Assert.Throws<ArgumentNullException>(
                () => Build(new FixtureValues { BehaviorModuleIds = null }));
            Assert.Throws<ArgumentNullException>(
                () => Build(new FixtureValues(), null));
            Assert.Throws<ArgumentException>(
                () => Build(
                    new FixtureValues(),
                    new StableId[] { AutomaticModule, null, ProjectileModule }));
        }

        [Test]
        public void Create_RejectsInvalidRecoilAndPresentationPriority()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Build(new FixtureValues { RecoilInfluence = -0.01d }));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Build(new FixtureValues { RecoilInfluence = 1.01d }));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Build(new FixtureValues { PresentationPriority = -1 }));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Build(new FixtureValues { PresentationPriority = 1025 }));

            Assert.DoesNotThrow(() => Build(new FixtureValues { RecoilInfluence = 0d }));
            Assert.DoesNotThrow(() => Build(new FixtureValues { RecoilInfluence = 1d }));
            Assert.DoesNotThrow(() => Build(new FixtureValues { PresentationPriority = 0 }));
            Assert.DoesNotThrow(() => Build(new FixtureValues { PresentationPriority = 1024 }));
        }

        [Test]
        public void CanonicalText_UsesStableFieldOrderAndModuleOrder()
        {
            WeaponRuntimeProfile profile = Build(new FixtureValues());
            string[] expectedNames =
            {
                "profile_version",
                "profile_id",
                "cadence_seconds",
                "burst_shot_count",
                "burst_shot_interval_seconds",
                "recovery_seconds",
                "cycle_mode",
                "heat_capacity_units",
                "heat_per_shot_units",
                "heat_recovery_units_per_second",
                "charge_seconds",
                "has_independent_power_bank",
                "power_bank_capacity_units",
                "empowered_cost_units",
                "recoil_influence",
                "behavior_module_count",
                "behavior_module_0",
                "behavior_module_1",
                "presentation_priority",
            };

            string canonical = profile.ToCanonicalString();
            string[] actualNames = canonical
                .Split('\n')
                .Select(line => line.Substring(0, line.IndexOf('=')))
                .ToArray();

            Assert.That(actualNames, Is.EqualTo(expectedNames));
            Assert.That(canonical, Does.Not.EndWith("\n"));
            Assert.That(canonical, Does.Not.Contain("\r"));
        }

        [Test]
        public void ParseCanonical_RejectsMalformedUnknownAndNonCanonicalInput()
        {
            string canonical = Build(new FixtureValues()).ToCanonicalString();
            string[] lines = canonical.Split('\n');

            Assert.Throws<FormatException>(
                () => WeaponRuntimeProfile.ParseCanonical(
                    string.Join("\n", lines.Take(lines.Length - 1).ToArray()),
                    KnownModules()));
            Assert.Throws<FormatException>(
                () => WeaponRuntimeProfile.ParseCanonical(canonical + "\n", KnownModules()));
            Assert.Throws<FormatException>(
                () => WeaponRuntimeProfile.ParseCanonical(
                    canonical.Replace("\n", "\r\n"),
                    KnownModules()));
            Assert.Throws<FormatException>(
                () => WeaponRuntimeProfile.ParseCanonical(
                    canonical.Replace("cadence_seconds=0.2", "cadence_seconds=0.20"),
                    KnownModules()));
            Assert.Throws<FormatException>(
                () => WeaponRuntimeProfile.ParseCanonical(
                    canonical.Replace("cycle_mode=heat", "cycle_mode=thermal"),
                    KnownModules()));
            Assert.Throws<FormatException>(
                () => WeaponRuntimeProfile.ParseCanonical(
                    canonical.Replace("has_independent_power_bank=true", "has_independent_power_bank=True"),
                    KnownModules()));
            Assert.Throws<FormatException>(
                () => WeaponRuntimeProfile.ParseCanonical(
                    canonical.Replace("behavior_module_count=2", "behavior_module_count=3"),
                    KnownModules()));
            Assert.Throws<ArgumentException>(
                () => WeaponRuntimeProfile.ParseCanonical(
                    canonical,
                    new[] { AutomaticModule }));
        }

        [Test]
        public void ModelContainsNoConsumableAmmunitionOrPackageSpecificSwitchSurface()
        {
            string[] forbiddenMemberTerms =
            {
                "ammo",
                "ammunition",
                "magazine",
                "clip",
                "reload",
                "blaster",
                "shotgun",
                "rocket",
                "arcgun",
                "ricochet",
            };

            string[] memberNames = typeof(WeaponRuntimeProfile)
                .GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public)
                .Select(member => member.Name.Replace("_", string.Empty).ToLowerInvariant())
                .ToArray();

            foreach (string forbidden in forbiddenMemberTerms)
            {
                Assert.That(
                    memberNames.Any(name => name.Contains(forbidden)),
                    Is.False,
                    "Unexpected package-specific or consumable member: " + forbidden);
            }
        }

        [Test]
        public void Validator_RejectsNullProfile()
        {
            Assert.Throws<ArgumentNullException>(
                () => WeaponRuntimeProfileValidator.Validate(null, KnownModules()));
        }

        private static WeaponRuntimeProfile Build(
            FixtureValues values,
            IEnumerable<StableId> knownBehaviorModuleIds = null)
        {
            return WeaponRuntimeProfile.Create(
                values.ProfileVersion,
                values.ProfileId,
                values.CadenceSeconds,
                values.BurstShotCount,
                values.BurstShotIntervalSeconds,
                values.RecoverySeconds,
                values.CycleMode,
                values.HeatCapacityUnits,
                values.HeatPerShotUnits,
                values.HeatRecoveryUnitsPerSecond,
                values.ChargeSeconds,
                values.HasIndependentPowerBank,
                values.PowerBankCapacityUnits,
                values.EmpoweredCostUnits,
                values.RecoilInfluence,
                values.BehaviorModuleIds,
                knownBehaviorModuleIds ?? KnownModules(),
                values.PresentationPriority);
        }

        private static StableId[] KnownModules()
        {
            return new[]
            {
                AutomaticModule,
                ProjectileModule,
                SpreadModule,
                ChainModule,
            };
        }

        private static FixtureValues ChargeValues()
        {
            return new FixtureValues
            {
                CycleMode = WeaponCycleMode.Charge,
                HeatCapacityUnits = 0d,
                HeatPerShotUnits = 0d,
                HeatRecoveryUnitsPerSecond = 0d,
                ChargeSeconds = 0.75d,
            };
        }

        private static NamedMutation Change(string name, Action<FixtureValues> apply)
        {
            return new NamedMutation(name, apply);
        }

        private static NamedMutation ChangeToChargeMode()
        {
            return Change(
                "cycle mode and charge timing",
                values =>
                {
                    values.CycleMode = WeaponCycleMode.Charge;
                    values.HeatCapacityUnits = 0d;
                    values.HeatPerShotUnits = 0d;
                    values.HeatRecoveryUnitsPerSecond = 0d;
                    values.ChargeSeconds = 0.75d;
                });
        }

        private sealed class NamedMutation
        {
            public NamedMutation(string name, Action<FixtureValues> apply)
            {
                Name = name;
                Apply = apply;
            }

            public string Name { get; }

            public Action<FixtureValues> Apply { get; }
        }

        private sealed class FixtureValues
        {
            public int ProfileVersion = WeaponRuntimeProfile.CurrentProfileVersion;
            public StableId ProfileId = StableId.Parse("weapon-profile.synthetic");
            public double CadenceSeconds = 0.2d;
            public int BurstShotCount = 3;
            public double BurstShotIntervalSeconds = 0.05d;
            public double RecoverySeconds = 0.4d;
            public WeaponCycleMode CycleMode = WeaponCycleMode.Heat;
            public double HeatCapacityUnits = 10d;
            public double HeatPerShotUnits = 2d;
            public double HeatRecoveryUnitsPerSecond = 3d;
            public double ChargeSeconds = 0d;
            public bool HasIndependentPowerBank = true;
            public double PowerBankCapacityUnits = 100d;
            public double EmpoweredCostUnits = 25d;
            public double RecoilInfluence = 0.35d;
            public IEnumerable<StableId> BehaviorModuleIds = new[]
            {
                AutomaticModule,
                ProjectileModule,
            };
            public int PresentationPriority = 10;
        }
    }
}
