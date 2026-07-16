#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Props
{
    /// <summary>
    /// Content-package scripts compile into predefined Assembly-CSharp, so this fixture
    /// exercises the real authority through reflection from the EditMode test asmdef.
    /// </summary>
    public sealed class DestructiblePropAuthorityTests
    {
        private static readonly Type AuthorityType = Find(
            "ShooterMover.ContentPackages.Props.DestructibleProps.DestructiblePropAuthority");
        private static readonly Type IntegrationType = Find(
            "ShooterMover.ContentPackages.Props.DestructibleProps.Stage1DestructiblePropIntegration");

        [Test]
        public void Stage1Authoring_UsesDifferentPositiveCrateAndExplosiveHealth()
        {
            Assert.That(IntegrationType, Is.Not.Null);
            double crateHealth = ReadStatic<double>(
                IntegrationType,
                "CrateMaximumHealth");
            double explosiveHealth = ReadStatic<double>(
                IntegrationType,
                "ExplosiveMaximumHealth");

            Assert.That(crateHealth, Is.GreaterThan(0d));
            Assert.That(explosiveHealth, Is.GreaterThan(0d));
            Assert.That(crateHealth, Is.Not.EqualTo(explosiveHealth));
        }

        [Test]
        public void NonlethalConfirmedHit_ReducesHealthAndRemainsActive()
        {
            StableId propId = StableId.Parse("prop.test-crate");
            object authority = CreateAuthority(propId, 20d);
            object result = Apply(
                authority,
                CreateHit("combat-event.prop-nonlethal", propId, HitResult.Confirmed),
                6d);

            Assert.That(Read(result, "Status").ToString(), Is.EqualTo("Applied"));
            object state = Read(authority, "CurrentState");
            Assert.That((double)Read(state, "CurrentHealth"), Is.EqualTo(14d));
            Assert.That((bool)Read(state, "IsActive"), Is.True);
            Assert.That(Read(result, "Destruction"), Is.Null);
        }

        [Test]
        public void LethalConfirmedHit_ProducesOneDeterministicDestructionResult()
        {
            StableId propId = StableId.Parse("prop.test-crate");
            object authority = CreateAuthority(propId, 20d);
            HitMessage hit = CreateHit(
                "combat-event.prop-lethal",
                propId,
                HitResult.Confirmed);
            object result = Apply(authority, hit, 25d);

            Assert.That(Read(result, "Status").ToString(), Is.EqualTo("Destroyed"));
            object destruction = Read(result, "Destruction");
            Assert.That(destruction, Is.Not.Null);
            Assert.That(Read(destruction, "EventId").ToString(), Is.EqualTo(hit.EventId.ToString()));
            Assert.That(Read(destruction, "PropId").ToString(), Is.EqualTo(propId.ToString()));
            object state = Read(authority, "CurrentState");
            Assert.That((double)Read(state, "CurrentHealth"), Is.EqualTo(0d));
            Assert.That((bool)Read(state, "IsDestroyed"), Is.True);

            object repeated = Apply(authority, hit, 25d);
            Assert.That(
                Read(repeated, "Status").ToString(),
                Is.EqualTo("DuplicateEventIgnored"));
            Assert.That(Read(repeated, "Destruction"), Is.Null);
        }

        [Test]
        public void DuplicateConfirmedEvent_DoesNotApplyDamageTwice()
        {
            StableId propId = StableId.Parse("prop.test-crate");
            object authority = CreateAuthority(propId, 20d);
            HitMessage hit = CreateHit(
                "combat-event.prop-duplicate",
                propId,
                HitResult.Confirmed);

            Apply(authority, hit, 6d);
            object duplicate = Apply(authority, hit, 6d);

            Assert.That(
                Read(duplicate, "Status").ToString(),
                Is.EqualTo("DuplicateEventIgnored"));
            Assert.That(
                (double)Read(Read(authority, "CurrentState"), "CurrentHealth"),
                Is.EqualTo(14d));
            Assert.That((int)Read(authority, "ProcessedEventCount"), Is.EqualTo(1));
        }

        [Test]
        public void InvalidValuesAndNonconfirmedHits_FailClosed()
        {
            StableId propId = StableId.Parse("prop.test-crate");
            object authority = CreateAuthority(propId, 20d);

            Assert.That(
                Read(Apply(authority, null, 6d), "Status").ToString(),
                Is.EqualTo("InvalidInput"));
            Assert.That(
                Read(
                    Apply(
                        authority,
                        CreateHit(
                            "combat-event.prop-zero",
                            propId,
                            HitResult.Confirmed),
                        0d),
                    "Status").ToString(),
                Is.EqualTo("InvalidInput"));
            Assert.That(
                Read(
                    Apply(
                        authority,
                        CreateHit(
                            "combat-event.prop-nan",
                            propId,
                            HitResult.Confirmed),
                        double.NaN),
                    "Status").ToString(),
                Is.EqualTo("InvalidInput"));
            Assert.That(
                Read(
                    Apply(
                        authority,
                        CreateHit(
                            "combat-event.prop-blocked",
                            propId,
                            HitResult.Blocked),
                        6d),
                    "Status").ToString(),
                Is.EqualTo("HitNotConfirmed"));
            HitMessage systemHit = new HitMessage(
                StableId.Parse("combat-event.prop-system"),
                StableId.Parse("actor.test-player"),
                propId,
                CombatChannel.System,
                HitResult.Confirmed);
            Assert.That(
                Read(Apply(authority, systemHit, 6d), "Status").ToString(),
                Is.EqualTo("InvalidInput"));
            Assert.That(
                Read(
                    Apply(
                        authority,
                        CreateHit(
                            "combat-event.prop-other",
                            StableId.Parse("prop.other"),
                            HitResult.Confirmed),
                        6d),
                    "Status").ToString(),
                Is.EqualTo("TargetMismatch"));

            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
                () => CreateAuthority(propId, 0d));
            Assert.That(exception.InnerException, Is.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                (double)Read(Read(authority, "CurrentState"), "CurrentHealth"),
                Is.EqualTo(20d));
            Assert.That((int)Read(authority, "ProcessedEventCount"), Is.EqualTo(0));
        }

        [Test]
        public void Restart_RestoresHealthAndClearsEventReplayHistory()
        {
            StableId propId = StableId.Parse("prop.test-crate");
            object authority = CreateAuthority(propId, 20d);
            HitMessage hit = CreateHit(
                "combat-event.prop-restart",
                propId,
                HitResult.Confirmed);

            Apply(authority, hit, 20d);
            Invoke(authority, "Restart");

            object state = Read(authority, "CurrentState");
            Assert.That((double)Read(state, "CurrentHealth"), Is.EqualTo(20d));
            Assert.That((bool)Read(state, "IsActive"), Is.True);
            Assert.That((int)Read(authority, "ProcessedEventCount"), Is.EqualTo(0));

            object replayAfterRestart = Apply(authority, hit, 5d);
            Assert.That(
                Read(replayAfterRestart, "Status").ToString(),
                Is.EqualTo("Applied"));
            Assert.That(
                (double)Read(Read(authority, "CurrentState"), "CurrentHealth"),
                Is.EqualTo(15d));
        }

        private static object CreateAuthority(StableId propId, double maximumHealth)
        {
            Assert.That(AuthorityType, Is.Not.Null);
            return Activator.CreateInstance(
                AuthorityType,
                new object[] { propId, maximumHealth });
        }

        private static object Apply(
            object authority,
            HitMessage hit,
            double damage)
        {
            return Invoke(authority, "ApplyConfirmedHit", hit, damage);
        }

        private static HitMessage CreateHit(
            string eventId,
            StableId targetId,
            HitResult result)
        {
            return new HitMessage(
                StableId.Parse(eventId),
                StableId.Parse("actor.test-player"),
                targetId,
                CombatChannel.Kinetic,
                result);
        }

        private static object Invoke(object instance, string name, params object[] args)
        {
            MethodInfo method = instance.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Single(candidate =>
                    candidate.Name == name
                    && candidate.GetParameters().Length == args.Length);
            return method.Invoke(instance, args);
        }

        private static object Read(object instance, string name)
        {
            Assert.That(instance, Is.Not.Null, name);
            PropertyInfo property = instance.GetType().GetProperty(
                name,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, name);
            return property.GetValue(instance, null);
        }

        private static T ReadStatic<T>(Type type, string name)
        {
            FieldInfo field = type.GetField(
                name,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(null);
        }

        private static Type Find(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(type => type != null);
        }
    }
}
#endif
