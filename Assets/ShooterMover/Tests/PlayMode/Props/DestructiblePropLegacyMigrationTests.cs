#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Combat;
using UnityEngine;

namespace ShooterMover.Tests.PlayMode.Props
{
    public sealed class DestructiblePropLegacyMigrationTests
    {
        private static readonly Type AuthoringType = Find(
            "ShooterMover.ContentPackages.Props.DestructibleProps.DestructiblePropAuthoring2D");
        private static readonly Type RuntimeType = Find(
            "ShooterMover.ContentPackages.Props.DestructibleProps.DestructibleProp2D");
        private static readonly Type IntegrationType = Find(
            "ShooterMover.ContentPackages.Props.DestructibleProps.Stage1DestructiblePropIntegration");

        [Test]
        public void LegacyMarkerMigration_UsesGeometryNotNamesAndRetainsBehavior()
        {
            GameObject host = new GameObject("Host With Arbitrary Name");
            try
            {
                Assert.That(AuthoringType, Is.Not.Null);
                Assert.That(RuntimeType, Is.Not.Null);
                Assert.That(IntegrationType, Is.Not.Null);

                GameObject presentationRootObject = new GameObject("Presentation Root");
                presentationRootObject.transform.SetParent(host.transform, false);
                GameObject visual = new GameObject("Decorative Tea Kettle");
                visual.transform.SetParent(presentationRootObject.transform, false);
                visual.transform.position = new Vector3(2.25f, -3.5f, 0f);
                SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
                object authoring = visual.AddComponent(AuthoringType);
                Invoke(
                    authoring,
                    "ConfigureGenerated",
                    24d,
                    new Vector2(2.2f, 1.35f),
                    Vector2.zero,
                    null);

                GameObject obstacle = new GameObject("Completely Unrelated Collider Name");
                obstacle.transform.SetParent(host.transform, false);
                obstacle.transform.position = visual.transform.position;
                BoxCollider2D collider = obstacle.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(2.2f, 1.35f);

                StableId identityBefore = (StableId)InvokeStatic(
                    IntegrationType,
                    "CreateLegacyPropId",
                    authoring);
                visual.name = "Renamed Visual";
                obstacle.name = "Renamed Collider";
                GameObject newParent = new GameObject("Reparented Container");
                newParent.transform.SetParent(presentationRootObject.transform, false);
                visual.transform.SetParent(newParent.transform, true);
                StableId identityAfter = (StableId)InvokeStatic(
                    IntegrationType,
                    "CreateLegacyPropId",
                    authoring);
                Assert.That(identityAfter, Is.EqualTo(identityBefore));

                CombatHit2DAdapter adapter = new CombatHit2DAdapter(
                    StableId.Parse("actor.legacy-test-player"));
                long generation = 0L;
                object set = InvokeStatic(
                    IntegrationType,
                    "Attach",
                    host,
                    presentationRootObject.transform,
                    host.transform,
                    StableId.Parse("room.entry"),
                    adapter,
                    6d,
                    new Func<long>(() => generation));

                Assert.That(Read(set, "PropCount"), Is.EqualTo(1));
                Component runtime = obstacle.GetComponent(RuntimeType);
                Assert.That(runtime, Is.Not.Null);
                Assert.That(Read(runtime, "PropId"), Is.EqualTo(identityBefore));

                Invoke(
                    runtime,
                    "TryApplyConfirmedHit",
                    CreateHit("combat-event.legacy-name-free", identityBefore),
                    6d);
                Assert.That(Read(runtime, "CurrentHealth"), Is.EqualTo(18d));
                Assert.That(renderer.enabled, Is.True);
                Assert.That(collider.enabled, Is.True);

                Invoke(set, "RestartAll");
                Assert.That(Read(runtime, "CurrentHealth"), Is.EqualTo(24d));
                Assert.That(renderer.enabled, Is.True);
                Assert.That(collider.enabled, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static HitMessage CreateHit(string eventId, StableId targetId)
        {
            return new HitMessage(
                StableId.Parse(eventId),
                StableId.Parse("actor.legacy-test-player"),
                targetId,
                CombatChannel.Kinetic,
                HitResult.Confirmed);
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

        private static object InvokeStatic(Type type, string name, params object[] args)
        {
            MethodInfo method = type
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(candidate =>
                    candidate.Name == name
                    && candidate.GetParameters().Length == args.Length);
            return method.Invoke(null, args);
        }

        private static object Read(object instance, string name)
        {
            PropertyInfo property = instance.GetType().GetProperty(
                name,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, name);
            return property.GetValue(instance, null);
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
