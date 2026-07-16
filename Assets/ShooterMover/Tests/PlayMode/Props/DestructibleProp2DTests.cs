#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using UnityEngine;
using UnityEngine.TestTools;

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
        private static readonly Type AnimationType = Find(
            "ShooterMover.ContentPackages.Props.DestructibleProps.DestructiblePropDestructionAnimation");
        private static readonly Type AnimationPlayerType = Find(
            "ShooterMover.ContentPackages.Props.DestructibleProps.DestructiblePropDestructionPlayer2D");

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

        [UnityTest]
        public IEnumerator ConfiguredDestructionAnimation_PlaysAndRestartClearsIt()
        {
            GameObject obstacle = new GameObject("AnimatedDestructiblePropObstacle");
            GameObject visual = new GameObject("AnimatedDestructiblePropVisual");
            Texture2D firstTexture = null;
            Texture2D secondTexture = null;
            try
            {
                BoxCollider2D collider = obstacle.AddComponent<BoxCollider2D>();
                visual.AddComponent<SpriteRenderer>();
                object component = obstacle.AddComponent(ComponentType);
                StableId propId = StableId.Parse("prop.playmode-animated");
                Invoke(component, "Configure", propId, 12d, collider, visual);

                Sprite first = CreateSprite("Explosion Frame 1", Color.yellow, out firstTexture);
                Sprite second = CreateSprite("Explosion Frame 2", Color.red, out secondTexture);
                MethodInfo createRuntime = AnimationType.GetMethod(
                    "CreateRuntime",
                    BindingFlags.Public | BindingFlags.Static);
                Assert.That(createRuntime, Is.Not.Null);
                object animation = createRuntime.Invoke(
                    null,
                    new object[]
                    {
                        new[] { first, second },
                        0.05f,
                        Vector2.zero,
                        Vector2.one,
                        55,
                        false,
                    });

                object player = obstacle.AddComponent(AnimationPlayerType);
                Invoke(player, "Configure", component, visual.transform, animation);
                Invoke(
                    component,
                    "TryApplyConfirmedHit",
                    CreateHit("combat-event.playmode-animation", propId),
                    12d);

                Assert.That((bool)Read(player, "IsPlaying"), Is.True);
                SpriteRenderer effectRenderer =
                    (SpriteRenderer)Read(player, "EffectRenderer");
                Assert.That(effectRenderer, Is.Not.Null);
                Assert.That(effectRenderer.sprite, Is.EqualTo(first));
                Assert.That(effectRenderer.sortingOrder, Is.EqualTo(55));

                yield return new WaitForSeconds(0.06f);
                effectRenderer = (SpriteRenderer)Read(player, "EffectRenderer");
                Assert.That(effectRenderer, Is.Not.Null);
                Assert.That(effectRenderer.sprite, Is.EqualTo(second));

                Invoke(component, "Restart");
                yield return null;
                Assert.That((bool)Read(player, "IsPlaying"), Is.False);
                Assert.That(Read(player, "EffectRenderer"), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(obstacle);
                UnityEngine.Object.DestroyImmediate(visual);
                if (firstTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(firstTexture);
                }

                if (secondTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(secondTexture);
                }
            }
        }

        private static Sprite CreateSprite(
            string name,
            Color color,
            out Texture2D texture)
        {
            texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.name = name + " Texture";
            texture.SetPixel(0, 0, color);
            texture.Apply(false, false);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            sprite.name = name;
            return sprite;
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
