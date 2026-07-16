#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.Tests.PlayMode.Props
{
    /// <summary>
    /// Package scripts live in predefined Assembly-CSharp, so the PlayMode asmdef reaches
    /// the actual MonoBehaviour through reflection.
    /// </summary>
    public sealed class DestructibleProp2DTests
    {
        private static readonly Type ComponentType = Find(
            "ShooterMover.ContentPackages.Props.DestructibleProps.DestructibleProp2D");

        [Test]
        public void LethalDamage_DisablesBlockingColliderAndPresentationExactlyOnce()
        {
            GameObject obstacle = new GameObject("DestructiblePropObstacle");
            GameObject visual = new GameObject("DestructiblePropVisual");
            try
            {
                BoxCollider2D collider = obstacle.AddComponent<BoxCollider2D>();
                SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
                Assert.That(ComponentType, Is.Not.Null);
                object component = obstacle.AddComponent(ComponentType);
                StableId propId = StableId.Parse("prop.playmode-crate");
                Invoke(
                    component,
                    "Configure",
                    propId,
                    12d,
                    collider,
                    visual);

                HitMessage hit = CreateHit(
                    "combat-event.playmode-lethal",
                    propId);
                object lethal = Invoke(
                    component,
                    "TryApplyConfirmedHit",
                    hit,
                    12d);

                Assert.That(Read(lethal, "Status").ToString(), Is.EqualTo("Destroyed"));
                Assert.That(collider.enabled, Is.False);
                Assert.That(renderer.enabled, Is.False);
                Assert.That(
                    (int)Read(component, "DestructionNotificationCount"),
                    Is.EqualTo(1));

                object duplicate = Invoke(
                    component,
                    "TryApplyConfirmedHit",
                    hit,
                    12d);
                Assert.That(
                    Read(duplicate, "Status").ToString(),
                    Is.EqualTo("DuplicateEventIgnored"));
                Assert.That(
                    (int)Read(component, "DestructionNotificationCount"),
                    Is.EqualTo(1));
                Assert.That(collider.enabled, Is.False);
                Assert.That(renderer.enabled, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(obstacle);
                UnityEngine.Object.DestroyImmediate(visual);
            }
        }

        [Test]
        public void Restart_RestoresHealthColliderAndPresentationState()
        {
            GameObject obstacle = new GameObject("DestructiblePropObstacle");
            GameObject visual = new GameObject("DestructiblePropVisual");
            try
            {
                BoxCollider2D collider = obstacle.AddComponent<BoxCollider2D>();
                SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
                Assert.That(ComponentType, Is.Not.Null);
                object component = obstacle.AddComponent(ComponentType);
                StableId propId = StableId.Parse("prop.playmode-explosive");
                Invoke(
                    component,
                    "Configure",
                    propId,
                    12d,
                    collider,
                    visual);

                HitMessage hit = CreateHit(
                    "combat-event.playmode-restart",
                    propId);
                Invoke(component, "TryApplyConfirmedHit", hit, 20d);
                Invoke(component, "Restart");

                Assert.That(collider.enabled, Is.True);
                Assert.That(renderer.enabled, Is.True);
                Assert.That((double)Read(component, "CurrentHealth"), Is.EqualTo(12d));
                object state = Read(component, "CurrentState");
                Assert.That((bool)Read(state, "IsActive"), Is.True);
                Assert.That(
                    (int)Read(component, "DestructionNotificationCount"),
                    Is.EqualTo(0));

                object replayAfterRestart = Invoke(
                    component,
                    "TryApplyConfirmedHit",
                    hit,
                    6d);
                Assert.That(
                    Read(replayAfterRestart, "Status").ToString(),
                    Is.EqualTo("Applied"));
                Assert.That((double)Read(component, "CurrentHealth"), Is.EqualTo(6d));
                Assert.That(collider.enabled, Is.True);
                Assert.That(renderer.enabled, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(obstacle);
                UnityEngine.Object.DestroyImmediate(visual);
            }
        }

        private static HitMessage CreateHit(string eventId, StableId targetId)
        {
            return new HitMessage(
                StableId.Parse(eventId),
                StableId.Parse("actor.test-player"),
                targetId,
                CombatChannel.Kinetic,
                HitResult.Confirmed);
        }

        private static object Invoke(object instance, string name, params object[] args)
        {
            Assert.That(instance, Is.Not.Null, name);
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

        private static Type Find(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(type => type != null);
        }
    }
}
#endif
