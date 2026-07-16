#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Authoring;
using ShooterMover.UnityAdapters.Combat;
using ShooterMover.UnityAdapters.Rewards.Sources;
using UnityEngine;

namespace ShooterMover.Tests.PlayMode.Props
{
    public sealed class DestructiblePropAuthoring2DTests
    {
        private static readonly Type RuntimeType = Find(
            "ShooterMover.ContentPackages.Props.DestructibleProps.DestructibleProp2D");
        private static readonly Type AuthoringType = Find(
            "ShooterMover.ContentPackages.Props.DestructibleProps.DestructiblePropAuthoring2D");
        private static readonly Type FamilyType = Find(
            "ShooterMover.ContentPackages.Props.DestructibleProps.DestructiblePropFamilyDefinitionAsset");
        private static readonly Type ValuesType = Find(
            "ShooterMover.ContentPackages.Props.DestructibleProps.DestructiblePropDefinitionValues");
        private static readonly Type OverridesType = Find(
            "ShooterMover.ContentPackages.Props.DestructibleProps.DestructiblePropValueOverrides");
        private static readonly Type VariantType = Find(
            "ShooterMover.ContentPackages.Props.DestructibleProps.DestructiblePropVariantDefinition");
        private static readonly Type ShapeType = Find(
            "ShooterMover.ContentPackages.Props.DestructibleProps.DestructiblePropColliderShape2D");
        private static readonly Type PolicyType = Find(
            "ShooterMover.ContentPackages.Props.DestructibleProps.DestructiblePropDestroyedCollisionPolicy");
        private static readonly Type BridgeType = Find(
            "ShooterMover.ContentPackages.Props.DestructibleProps.DestructiblePropRewardBridge2D");
        private static readonly Type AnimationPlayerType = Find(
            "ShooterMover.ContentPackages.Props.DestructibleProps.DestructiblePropDestructionPlayer2D");

        [Test]
        public void ArbitraryNamesAndTenInstances_KeepIndependentStableIdentityAndHealth()
        {
            GameObject root = new GameObject("Scope Root");
            ScriptableObject family = null;
            try
            {
                GameplaySceneScope2D scope = CreateScope(root);
                family = CreateFamily(20f, Vector2.one, Vector2.zero, null, "Disable");
                CombatHit2DAdapter adapter = new CombatHit2DAdapter(
                    StableId.Parse("actor.test-player"));
                List<Configured> configured = new List<Configured>();
                for (int index = 0; index < 10; index++)
                {
                    configured.Add(CreateConfigured(
                        root.transform,
                        scope,
                        family,
                        adapter,
                        "Decorative Object " + index,
                        "placed.prop-" + index));
                }

                Assert.That(
                    configured.Select(item => Read(item.Runtime, "PropId").ToString()).Distinct().Count(),
                    Is.EqualTo(10));

                StableId firstId = (StableId)Read(configured[0].Runtime, "PropId");
                configured[0].GameObject.name = "Renamed After Configuration";
                GameObject newParent = new GameObject("New Parent");
                newParent.transform.SetParent(root.transform, false);
                configured[0].GameObject.transform.SetParent(newParent.transform, true);
                Assert.That(Read(configured[0].Runtime, "PropId"), Is.EqualTo(firstId));

                Invoke(
                    configured[0].Runtime,
                    "TryApplyConfirmedHit",
                    CreateHit("combat-event.independent", firstId),
                    5d);
                Assert.That(Read(configured[0].Runtime, "CurrentHealth"), Is.EqualTo(15d));
                for (int index = 1; index < configured.Count; index++)
                {
                    Assert.That(Read(configured[index].Runtime, "CurrentHealth"), Is.EqualTo(20d));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (family != null)
                {
                    UnityEngine.Object.DestroyImmediate(family);
                }
            }
        }

        [Test]
        public void DuplicatePlacedIdentity_IsRejectedWithUnderstandableDiagnostic()
        {
            GameObject root = new GameObject("Duplicate Scope");
            ScriptableObject family = null;
            try
            {
                GameplaySceneScope2D scope = CreateScope(root);
                family = CreateFamily(20f, Vector2.one, Vector2.zero, null, "Disable");
                CombatHit2DAdapter adapter = new CombatHit2DAdapter(
                    StableId.Parse("actor.test-player"));
                CreateConfigured(
                    root.transform,
                    scope,
                    family,
                    adapter,
                    "First Arbitrary Name",
                    "placed.duplicate");

                GameObject second = new GameObject("Second Arbitrary Name");
                second.transform.SetParent(root.transform, false);
                BoxCollider2D collider = second.AddComponent<BoxCollider2D>();
                SpriteRenderer renderer = second.AddComponent<SpriteRenderer>();
                PlacedObjectAuthoring2D placed = second.AddComponent<PlacedObjectAuthoring2D>();
                placed.ConfigureForTests(
                    "placed.duplicate",
                    family,
                    "variant.level-1",
                    scope,
                    "scope.gameplay",
                    Array.Empty<CapabilityOverrideAuthoring>());
                object authoring = second.AddComponent(AuthoringType);
                ConfigureAuthoring(authoring, placed, family, collider, renderer);

                object result = Invoke(authoring, "TryConfigure", adapter);
                Assert.That(Read(result, "IsConfigured"), Is.False);
                Assert.That(Read(result, "Status").ToString(), Is.EqualTo("PlacedObjectBindingFailed"));
                Assert.That(
                    Read(result, "Diagnostic").ToString().ToLowerInvariant(),
                    Does.Contain("duplicate"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (family != null)
                {
                    UnityEngine.Object.DestroyImmediate(family);
                }
            }
        }

        [Test]
        public void ResolvedColliderSpriteAndDestroyedCollisionPolicy_AreAppliedAndRestarted()
        {
            GameObject root = new GameObject("Authored Scope");
            ScriptableObject family = null;
            Texture2D texture = null;
            try
            {
                Sprite sprite = CreateSprite("Resolved Sprite", out texture);
                GameplaySceneScope2D scope = CreateScope(root);
                family = CreateFamily(
                    32f,
                    new Vector2(3.5f, 1.75f),
                    new Vector2(0.25f, -0.5f),
                    sprite,
                    "KeepAsTrigger");
                Configured configured = CreateConfigured(
                    root.transform,
                    scope,
                    family,
                    new CombatHit2DAdapter(StableId.Parse("actor.test-player")),
                    "Any Designer Name",
                    "placed.policy-test");

                Assert.That(configured.Collider.size, Is.EqualTo(new Vector2(3.5f, 1.75f)));
                Assert.That(configured.Collider.offset, Is.EqualTo(new Vector2(0.25f, -0.5f)));
                Assert.That(configured.Renderer.sprite, Is.SameAs(sprite));

                StableId id = (StableId)Read(configured.Runtime, "PropId");
                Invoke(
                    configured.Runtime,
                    "TryApplyConfirmedHit",
                    CreateHit("combat-event.policy", id),
                    40d);
                Assert.That(configured.Collider.enabled, Is.True);
                Assert.That(configured.Collider.isTrigger, Is.True);
                Assert.That(configured.Renderer.enabled, Is.False);

                scope.RunRestart(1L);
                Assert.That(configured.Collider.enabled, Is.True);
                Assert.That(configured.Collider.isTrigger, Is.False);
                Assert.That(configured.Renderer.enabled, Is.True);
                Assert.That(Read(configured.Runtime, "CurrentHealth"), Is.EqualTo(32d));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (family != null)
                {
                    UnityEngine.Object.DestroyImmediate(family);
                }

                if (texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
        }

        [Test]
        public void RewardBridge_SubmitsOnceAcrossDuplicateHitAndRestart()
        {
            GameObject obstacle = new GameObject("Rewarded Prop");
            GameObject visual = new GameObject("Rewarded Visual");
            try
            {
                BoxCollider2D collider = obstacle.AddComponent<BoxCollider2D>();
                SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
                object runtime = obstacle.AddComponent(RuntimeType);
                object disablePolicy = Enum.Parse(PolicyType, "Disable");
                Invoke(
                    runtime,
                    "Configure",
                    StableId.Parse("prop.rewarded"),
                    10d,
                    collider,
                    new Renderer[] { renderer },
                    disablePolicy);

                RewardSourceAuthoring2D source =
                    obstacle.AddComponent<RewardSourceAuthoring2D>();
                object bridge = obstacle.AddComponent(BridgeType);
                Invoke(bridge, "Configure", runtime, source);
                HitMessage hit = CreateHit(
                    "combat-event.rewarded",
                    StableId.Parse("prop.rewarded"));
                Invoke(runtime, "TryApplyConfirmedHit", hit, 10d);
                Invoke(runtime, "TryApplyConfirmedHit", hit, 10d);
                Invoke(runtime, "Restart");
                Invoke(
                    runtime,
                    "TryApplyConfirmedHit",
                    CreateHit("combat-event.rewarded-after-restart", StableId.Parse("prop.rewarded")),
                    10d);

                Assert.That(Read(bridge, "SubmissionCount"), Is.EqualTo(1));
                Assert.That(Read(bridge, "HasSubmitted"), Is.True);
                Assert.That(Read(bridge, "LastSubmission"), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(obstacle);
                UnityEngine.Object.DestroyImmediate(visual);
            }
        }

        [Test]
        public void MissingOptionalAnimation_FailsSafely()
        {
            GameObject obstacle = new GameObject("No Animation Prop");
            GameObject visual = new GameObject("No Animation Visual");
            try
            {
                BoxCollider2D collider = obstacle.AddComponent<BoxCollider2D>();
                SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
                object runtime = obstacle.AddComponent(RuntimeType);
                Invoke(
                    runtime,
                    "Configure",
                    StableId.Parse("prop.no-animation"),
                    10d,
                    collider,
                    visual);
                object player = obstacle.AddComponent(AnimationPlayerType);
                Invoke(player, "Configure", runtime, visual.transform, null);
                Invoke(
                    runtime,
                    "TryApplyConfirmedHit",
                    CreateHit("combat-event.no-animation", StableId.Parse("prop.no-animation")),
                    10d);

                Assert.That(Read(player, "IsConfigured"), Is.True);
                Assert.That(Read(player, "IsPlaying"), Is.False);
                Assert.That(Read(player, "EffectRenderer"), Is.Null);
                Assert.That(renderer.enabled, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(obstacle);
                UnityEngine.Object.DestroyImmediate(visual);
            }
        }

        private static Configured CreateConfigured(
            Transform parent,
            GameplaySceneScope2D scope,
            ScriptableObject family,
            CombatHit2DAdapter adapter,
            string objectName,
            string placedId)
        {
            GameObject gameObject = new GameObject(objectName);
            gameObject.transform.SetParent(parent, false);
            BoxCollider2D collider = gameObject.AddComponent<BoxCollider2D>();
            SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
            PlacedObjectAuthoring2D placed = gameObject.AddComponent<PlacedObjectAuthoring2D>();
            placed.ConfigureForTests(
                placedId,
                family,
                "variant.level-1",
                scope,
                "scope.gameplay",
                Array.Empty<CapabilityOverrideAuthoring>());
            object authoring = gameObject.AddComponent(AuthoringType);
            ConfigureAuthoring(authoring, placed, family, collider, renderer);
            object result = Invoke(authoring, "TryConfigure", adapter);
            Assert.That(Read(result, "IsConfigured"), Is.True, Read(result, "Diagnostic").ToString());
            object runtime = Read(result, "RuntimeProp");
            Assert.That(runtime, Is.Not.Null);
            return new Configured(gameObject, collider, renderer, runtime);
        }

        private static void ConfigureAuthoring(
            object authoring,
            PlacedObjectAuthoring2D placed,
            ScriptableObject family,
            BoxCollider2D collider,
            SpriteRenderer renderer)
        {
            MethodInfo method = AuthoringType.GetMethod(
                "ConfigureForTests",
                BindingFlags.Public | BindingFlags.Instance);
            method.Invoke(
                authoring,
                new object[]
                {
                    placed,
                    family,
                    Activator.CreateInstance(OverridesType),
                    collider,
                    renderer,
                    renderer.transform,
                    6d,
                    null,
                    RewardSourceOverrideAuthoring.Inherit("reward-override.test-prop"),
                    null
                });
        }

        private static GameplaySceneScope2D CreateScope(GameObject root)
        {
            GameplaySceneScope2D scope = root.AddComponent<GameplaySceneScope2D>();
            scope.ConfigureForTests(
                "scope.gameplay",
                "scope.gameplay",
                "projection.test-props",
                "run.test-props",
                0L);
            return scope;
        }

        private static ScriptableObject CreateFamily(
            float health,
            Vector2 size,
            Vector2 offset,
            Sprite sprite,
            string policyName)
        {
            AssertTypes();
            object values = Activator.CreateInstance(ValuesType);
            SetField(values, "maximumHealth", health);
            SetField(values, "colliderShape", Enum.Parse(ShapeType, "Box"));
            SetField(values, "colliderSize", size);
            SetField(values, "colliderOffset", offset);
            SetField(values, "intactPresentationId", "presentation.test-prop");
            SetField(values, "intactSprite", sprite);
            SetField(values, "destructionAnimationId", "animation.none");
            SetField(values, "destroyedCollisionPolicy", Enum.Parse(PolicyType, policyName));
            SetField(values, "inheritedRewardProfileId", "reward-profile.none");

            object variant = Activator.CreateInstance(VariantType);
            SetField(variant, "variantId", "variant.level-1");
            SetField(variant, "hasObjectLevel", true);
            SetField(variant, "objectLevel", 1);
            SetField(variant, "overrides", Activator.CreateInstance(OverridesType));

            ScriptableObject family = ScriptableObject.CreateInstance(FamilyType);
            family.hideFlags = HideFlags.HideAndDontSave;
            SetField(family, "familyId", "family.test-props");
            SetField(family, "displayName", "Test props");
            SetField(family, "defaultVariantId", "variant.level-1");
            SetField(family, "familyDefaults", values);
            Array variants = Array.CreateInstance(VariantType, 1);
            variants.SetValue(variant, 0);
            SetField(family, "variants", variants);
            Invoke(family, "ValidateOrThrow");
            return family;
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

        private static Sprite CreateSprite(string name, out Texture2D texture)
        {
            texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.name = name + " Texture";
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, false);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            sprite.name = name;
            return sprite;
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
            PropertyInfo property = instance.GetType().GetProperty(
                name,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, name);
            return property.GetValue(instance, null);
        }

        private static void SetField(object instance, string name, object value)
        {
            FieldInfo field = instance.GetType().GetField(
                name,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(instance, value);
        }

        private static Type Find(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(type => type != null);
        }

        private static void AssertTypes()
        {
            Assert.That(RuntimeType, Is.Not.Null);
            Assert.That(AuthoringType, Is.Not.Null);
            Assert.That(FamilyType, Is.Not.Null);
            Assert.That(ValuesType, Is.Not.Null);
            Assert.That(OverridesType, Is.Not.Null);
            Assert.That(VariantType, Is.Not.Null);
            Assert.That(ShapeType, Is.Not.Null);
            Assert.That(PolicyType, Is.Not.Null);
            Assert.That(BridgeType, Is.Not.Null);
            Assert.That(AnimationPlayerType, Is.Not.Null);
        }

        private sealed class Configured
        {
            public Configured(
                GameObject gameObject,
                BoxCollider2D collider,
                SpriteRenderer renderer,
                object runtime)
            {
                GameObject = gameObject;
                Collider = collider;
                Renderer = renderer;
                Runtime = runtime;
            }

            public GameObject GameObject { get; }
            public BoxCollider2D Collider { get; }
            public SpriteRenderer Renderer { get; }
            public object Runtime { get; }
        }
    }
}
#endif
