using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Movement;

namespace ShooterMover.Tests.EditMode.Movement
{
    public sealed class MovementThrusterTuningProfileTests
    {
        [Test]
        public void Create_ProducesImmutableValidatedProfile()
        {
            MovementThrusterTuningProfile profile = Build(new FixtureValues());

            Assert.That(profile.ProfileVersion, Is.EqualTo(1));
            Assert.That(profile.ProfileId, Is.EqualTo(StableId.Parse("tuning.movement-prototype")));
            Assert.That(profile.Fingerprint, Does.StartWith("sha256:"));
            Assert.That(profile.Fingerprint, Has.Length.EqualTo(71));
            Assert.That(
                profile.DeterministicIdentity.Namespace,
                Is.EqualTo(MovementThrusterTuningProfile.DeterministicIdentityNamespace));
            Assert.That(profile.DeterministicIdentity.Value, Has.Length.EqualTo(64));

            Type type = typeof(MovementThrusterTuningProfile);
            Assert.That(type.IsSealed, Is.True);
            Assert.That(type.GetConstructors(BindingFlags.Instance | BindingFlags.Public), Is.Empty);

            PropertyInfo[] mutableProperties = type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => property.CanWrite)
                .ToArray();
            Assert.That(mutableProperties, Is.Empty);

            Assert.DoesNotThrow(() => MovementThrusterTuningProfileValidator.Validate(profile));
        }

        [Test]
        public void CanonicalText_UsesDeclaredFieldOrderAndNoTrailingNewline()
        {
            MovementThrusterTuningProfile profile = Build(new FixtureValues());
            string[] expectedNames =
            {
                "profile_version",
                "profile_id",
                "base_maximum_speed",
                "base_acceleration",
                "base_braking",
                "base_counter_steer_braking",
                "base_velocity_response_exponent",
                "thruster_baseline_charge_count",
                "thruster_maximum_additional_charges",
                "thruster_recharge_seconds",
                "thruster_speed_multiplier",
                "thruster_burst_duration_seconds",
                "thruster_direction_input_threshold",
                "thruster_minimum_chain_interval_seconds",
                "thruster_steering_degrees_per_second",
                "thruster_startup_forgiveness_seconds",
                "thruster_exit_momentum_seconds",
                "thruster_exit_speed_retention",
                "thruster_exit_decay_exponent",
                "wall_reflection_speed_retention",
                "wall_reflection_input_influence",
                "wall_reflection_minimum_speed",
                "wall_reflection_maximum_contacts",
                "light_contact_momentum_retention",
                "light_contact_steering_retention",
                "heavy_contact_momentum_retention",
                "per_enemy_contact_grace_seconds",
                "simultaneous_contact_window_seconds",
                "contact_grace_capacity",
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
        public void EquivalentProfiles_RoundTripWithIdenticalIdentity()
        {
            MovementThrusterTuningProfile first = Build(new FixtureValues());
            MovementThrusterTuningProfile second =
                MovementThrusterTuningProfile.ParseCanonical(first.ToCanonicalString());

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
            MovementThrusterTuningProfile profile = Build(new FixtureValues());

            Assert.That(
                profile.Fingerprint,
                Is.EqualTo(
                    "sha256:47f00ba9eacd4504691f466f0c21a0e255b5dacfe3e26995e226f7e7406f48a7"));
            Assert.That(
                profile.DeterministicIdentity.ToString(),
                Is.EqualTo(
                    "movement-tuning.47f00ba9eacd4504691f466f0c21a0e255b5dacfe3e26995e226f7e7406f48a7"));
        }

        [Test]
        public void EveryMeaningfulFieldChange_ChangesFingerprintAndDeterministicIdentity()
        {
            MovementThrusterTuningProfile baseline = Build(new FixtureValues());
            NamedMutation[] mutations =
            {
                Change("profile_id", values => values.ProfileId = StableId.Parse("tuning.other-profile")),
                Change("base_maximum_speed", values => values.BaseMaximumSpeed = 13d),
                Change("base_acceleration", values => values.BaseAcceleration = 51d),
                Change("base_braking", values => values.BaseBraking = 61d),
                Change("base_counter_steer_braking", values => values.BaseCounterSteerBraking = 91d),
                Change("base_velocity_response_exponent", values => values.BaseVelocityResponseExponent = 1.5d),
                Change("thruster_baseline_charge_count", values => values.ThrusterBaselineChargeCount = 3),
                Change("thruster_maximum_additional_charges", values => values.ThrusterMaximumAdditionalCharges = 0),
                Change("thruster_recharge_seconds", values => values.ThrusterRechargeSeconds = 1.8d),
                Change("thruster_speed_multiplier", values => values.ThrusterSpeedMultiplier = 2.6d),
                Change("thruster_burst_duration_seconds", values => values.ThrusterBurstDurationSeconds = 0.35d),
                Change("thruster_direction_input_threshold", values => values.ThrusterDirectionInputThreshold = 0.12d),
                Change("thruster_minimum_chain_interval_seconds", values => values.ThrusterMinimumChainIntervalSeconds = 0.06d),
                Change("thruster_steering_degrees_per_second", values => values.ThrusterSteeringDegreesPerSecond = 125d),
                Change("thruster_startup_forgiveness_seconds", values => values.ThrusterStartupForgivenessSeconds = 0.05d),
                Change("thruster_exit_momentum_seconds", values => values.ThrusterExitMomentumSeconds = 0.25d),
                Change("thruster_exit_speed_retention", values => values.ThrusterExitSpeedRetention = 0.7d),
                Change("thruster_exit_decay_exponent", values => values.ThrusterExitDecayExponent = 2.1d),
                Change("wall_reflection_speed_retention", values => values.WallReflectionSpeedRetention = 0.75d),
                Change("wall_reflection_input_influence", values => values.WallReflectionInputInfluence = 0.2d),
                Change("wall_reflection_minimum_speed", values => values.WallReflectionMinimumSpeed = 6d),
                Change("wall_reflection_maximum_contacts", values => values.WallReflectionMaximumContacts = 5),
                Change("light_contact_momentum_retention", values => values.LightContactMomentumRetention = 0.75d),
                Change("light_contact_steering_retention", values => values.LightContactSteeringRetention = 0.85d),
                Change("heavy_contact_momentum_retention", values => values.HeavyContactMomentumRetention = 0.05d),
                Change("per_enemy_contact_grace_seconds", values => values.PerEnemyContactGraceSeconds = 0.55d),
                Change("simultaneous_contact_window_seconds", values => values.SimultaneousContactWindowSeconds = 0.03d),
                Change("contact_grace_capacity", values => values.ContactGraceCapacity = 129),
            };

            foreach (NamedMutation mutation in mutations)
            {
                FixtureValues changedValues = new FixtureValues();
                mutation.Apply(changedValues);
                MovementThrusterTuningProfile changed = Build(changedValues);

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
        public void ParseCanonical_RejectsMissingDuplicateReorderedUnknownAndNonCanonicalFields()
        {
            string canonical = Build(new FixtureValues()).ToCanonicalString();
            string[] lines = canonical.Split('\n');

            Assert.Throws<FormatException>(
                () => MovementThrusterTuningProfile.ParseCanonical(
                    string.Join("\n", lines.Take(lines.Length - 1).ToArray())));
            Assert.Throws<FormatException>(
                () => MovementThrusterTuningProfile.ParseCanonical(
                    canonical + "\ncontact_grace_capacity=128"));
            Assert.Throws<FormatException>(
                () => MovementThrusterTuningProfile.ParseCanonical(
                    lines[1] + "\n" + lines[0] + "\n" + string.Join("\n", lines.Skip(2).ToArray())));
            Assert.Throws<FormatException>(
                () => MovementThrusterTuningProfile.ParseCanonical(
                    canonical + "\nunknown_field=1"));
            Assert.Throws<FormatException>(
                () => MovementThrusterTuningProfile.ParseCanonical(canonical + "\n"));
            Assert.Throws<FormatException>(
                () => MovementThrusterTuningProfile.ParseCanonical(canonical.Replace("\n", "\r\n")));
            Assert.Throws<FormatException>(
                () => MovementThrusterTuningProfile.ParseCanonical(
                    canonical.Replace("base_maximum_speed=12", "base_maximum_speed=12.0")));
        }

        [Test]
        public void ParseCanonical_RejectsMalformedAndUnknownProfileVersions()
        {
            string canonical = Build(new FixtureValues()).ToCanonicalString();

            Assert.Throws<FormatException>(
                () => MovementThrusterTuningProfile.ParseCanonical(
                    canonical.Replace("profile_version=1", "profile_version=abc")));
            Assert.Throws<NotSupportedException>(
                () => MovementThrusterTuningProfile.ParseCanonical(
                    canonical.Replace("profile_version=1", "profile_version=2")));
            Assert.Throws<NotSupportedException>(
                () => Build(new FixtureValues { ProfileVersion = 2 }));
        }

        [Test]
        public void Create_RejectsMissingProfileIdAndNonFiniteValues()
        {
            Assert.Throws<ArgumentNullException>(
                () => Build(new FixtureValues { ProfileId = null }));

            foreach (double invalid in new[]
            {
                double.NaN,
                double.PositiveInfinity,
                double.NegativeInfinity,
            })
            {
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => Build(new FixtureValues { BaseMaximumSpeed = invalid }),
                    invalid.ToString());
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => Build(new FixtureValues { WallReflectionInputInfluence = invalid }),
                    invalid.ToString());
            }
        }

        [Test]
        public void Create_RejectsInvalidIndependentRanges()
        {
            NamedMutation[] invalidMutations =
            {
                Change("base maximum speed", values => values.BaseMaximumSpeed = 0d),
                Change("base acceleration", values => values.BaseAcceleration = 0d),
                Change("base braking", values => values.BaseBraking = 0d),
                Change("response exponent", values => values.BaseVelocityResponseExponent = 0d),
                Change("baseline charges", values => values.ThrusterBaselineChargeCount = 0),
                Change("additional charges", values => values.ThrusterMaximumAdditionalCharges = 2),
                Change("recharge", values => values.ThrusterRechargeSeconds = 0d),
                Change("speed multiplier", values => values.ThrusterSpeedMultiplier = 0d),
                Change("burst duration", values => values.ThrusterBurstDurationSeconds = 0d),
                Change("direction threshold", values => values.ThrusterDirectionInputThreshold = 1d),
                Change("steering", values => values.ThrusterSteeringDegreesPerSecond = -1d),
                Change("exit momentum", values => values.ThrusterExitMomentumSeconds = -1d),
                Change("exit retention", values => values.ThrusterExitSpeedRetention = 1.01d),
                Change("exit decay", values => values.ThrusterExitDecayExponent = 0d),
                Change("reflection retention", values => values.WallReflectionSpeedRetention = 1.01d),
                Change("reflection influence", values => values.WallReflectionInputInfluence = -0.01d),
                Change("reflection contacts", values => values.WallReflectionMaximumContacts = 0),
                Change("light momentum", values => values.LightContactMomentumRetention = 1.01d),
                Change("light steering", values => values.LightContactSteeringRetention = -0.01d),
                Change("contact grace", values => values.PerEnemyContactGraceSeconds = 0d),
                Change("contact capacity", values => values.ContactGraceCapacity = 0),
            };

            foreach (NamedMutation mutation in invalidMutations)
            {
                FixtureValues values = new FixtureValues();
                mutation.Apply(values);
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => Build(values),
                    mutation.Name + " should have failed validation.");
            }
        }

        [Test]
        public void Create_RejectsInvalidCrossFieldRelationships()
        {
            NamedMutation[] invalidMutations =
            {
                Change("counter steering weaker than braking", values => values.BaseCounterSteerBraking = 59d),
                Change("chain interval beyond burst", values => values.ThrusterMinimumChainIntervalSeconds = 0.31d),
                Change("forgiveness beyond burst", values => values.ThrusterStartupForgivenessSeconds = 0.31d),
                Change("reflection threshold beyond burst speed", values => values.WallReflectionMinimumSpeed = 31d),
                Change("heavy retention above light retention", values => values.HeavyContactMomentumRetention = 0.81d),
                Change("aggregation window beyond grace", values => values.SimultaneousContactWindowSeconds = 0.51d),
            };

            foreach (NamedMutation mutation in invalidMutations)
            {
                FixtureValues values = new FixtureValues();
                mutation.Apply(values);
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => Build(values),
                    mutation.Name + " should have failed validation.");
            }
        }

        [Test]
        public void Validator_RejectsNullProfile()
        {
            Assert.Throws<ArgumentNullException>(
                () => MovementThrusterTuningProfileValidator.Validate(null));
        }

        private static NamedMutation Change(string name, Action<FixtureValues> apply)
        {
            return new NamedMutation(name, apply);
        }

        private static MovementThrusterTuningProfile Build(FixtureValues values)
        {
            return MovementThrusterTuningProfile.Create(
                values.ProfileVersion,
                values.ProfileId,
                values.BaseMaximumSpeed,
                values.BaseAcceleration,
                values.BaseBraking,
                values.BaseCounterSteerBraking,
                values.BaseVelocityResponseExponent,
                values.ThrusterBaselineChargeCount,
                values.ThrusterMaximumAdditionalCharges,
                values.ThrusterRechargeSeconds,
                values.ThrusterSpeedMultiplier,
                values.ThrusterBurstDurationSeconds,
                values.ThrusterDirectionInputThreshold,
                values.ThrusterMinimumChainIntervalSeconds,
                values.ThrusterSteeringDegreesPerSecond,
                values.ThrusterStartupForgivenessSeconds,
                values.ThrusterExitMomentumSeconds,
                values.ThrusterExitSpeedRetention,
                values.ThrusterExitDecayExponent,
                values.WallReflectionSpeedRetention,
                values.WallReflectionInputInfluence,
                values.WallReflectionMinimumSpeed,
                values.WallReflectionMaximumContacts,
                values.LightContactMomentumRetention,
                values.LightContactSteeringRetention,
                values.HeavyContactMomentumRetention,
                values.PerEnemyContactGraceSeconds,
                values.SimultaneousContactWindowSeconds,
                values.ContactGraceCapacity);
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
            public int ProfileVersion = MovementThrusterTuningProfile.CurrentProfileVersion;
            public StableId ProfileId = StableId.Parse("tuning.movement-prototype");
            public double BaseMaximumSpeed = 12d;
            public double BaseAcceleration = 50d;
            public double BaseBraking = 60d;
            public double BaseCounterSteerBraking = 90d;
            public double BaseVelocityResponseExponent = 1.25d;
            public int ThrusterBaselineChargeCount = 2;
            public int ThrusterMaximumAdditionalCharges = 1;
            public double ThrusterRechargeSeconds = 1.75d;
            public double ThrusterSpeedMultiplier = 2.5d;
            public double ThrusterBurstDurationSeconds = 0.3d;
            public double ThrusterDirectionInputThreshold = 0.1d;
            public double ThrusterMinimumChainIntervalSeconds = 0.05d;
            public double ThrusterSteeringDegreesPerSecond = 120d;
            public double ThrusterStartupForgivenessSeconds = 0.04d;
            public double ThrusterExitMomentumSeconds = 0.2d;
            public double ThrusterExitSpeedRetention = 0.75d;
            public double ThrusterExitDecayExponent = 2d;
            public double WallReflectionSpeedRetention = 0.8d;
            public double WallReflectionInputInfluence = 0.15d;
            public double WallReflectionMinimumSpeed = 5d;
            public int WallReflectionMaximumContacts = 4;
            public double LightContactMomentumRetention = 0.8d;
            public double LightContactSteeringRetention = 0.9d;
            public double HeavyContactMomentumRetention = 0.1d;
            public double PerEnemyContactGraceSeconds = 0.5d;
            public double SimultaneousContactWindowSeconds = 0.02d;
            public int ContactGraceCapacity = 128;
        }
    }
}
