#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.Props
{
    public sealed class DestructiblePropDefinitionTests
    {
        private static readonly Type ValuesType = Find(
            "ShooterMover.ContentPackages.Props.DestructibleProps.DestructiblePropDefinitionValues");
        private static readonly Type OverridesType = Find(
            "ShooterMover.ContentPackages.Props.DestructibleProps.DestructiblePropValueOverrides");
        private static readonly Type VariantType = Find(
            "ShooterMover.ContentPackages.Props.DestructibleProps.DestructiblePropVariantDefinition");
        private static readonly Type FamilyType = Find(
            "ShooterMover.ContentPackages.Props.DestructibleProps.DestructiblePropFamilyDefinitionAsset");
        private static readonly Type ShapeType = Find(
            "ShooterMover.ContentPackages.Props.DestructibleProps.DestructiblePropColliderShape2D");
        private static readonly Type CollisionPolicyType = Find(
            "ShooterMover.ContentPackages.Props.DestructibleProps.DestructiblePropDestroyedCollisionPolicy");

        [Test]
        public void FamilyVariantAndInstanceValues_ResolveInRequiredOrder()
        {
            ScriptableObject family = CreateFamily(
                20f,
                new Vector2(2f, 1f),
                Vector2.zero,
                null,
                Variant("variant.level-1", 1, EmptyOverrides()),
                Variant("variant.level-2", 2, Overrides(40f, new Vector2(3f, 2f), new Vector2(0.25f, -0.5f), null)));
            try
            {
                object familyDefault = Resolve(
                    family,
                    "variant.level-1",
                    EmptyOverrides(),
                    "placed.family-default");
                object variant = Resolve(
                    family,
                    "variant.level-2",
                    EmptyOverrides(),
                    "placed.variant");
                object instance = Resolve(
                    family,
                    "variant.level-2",
                    Overrides(55f, new Vector2(4f, 5f), new Vector2(1f, 2f), null),
                    "placed.instance");

                Assert.That(ReadValues(familyDefault, "MaximumHealth"), Is.EqualTo(20d));
                Assert.That(ReadValues(variant, "MaximumHealth"), Is.EqualTo(40d));
                Assert.That(ReadValues(instance, "MaximumHealth"), Is.EqualTo(55d));
                Assert.That((Vector2)ReadValues(instance, "ColliderSize"), Is.EqualTo(new Vector2(4f, 5f)));
                Assert.That((Vector2)ReadValues(instance, "ColliderOffset"), Is.EqualTo(new Vector2(1f, 2f)));
                Assert.That((int?)Read(instance, "ObjectLevel"), Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(family);
            }
        }

        [Test]
        public void LevelVariants_HaveDifferentAuthoredHealthAndSprites()
        {
            Texture2D firstTexture = null;
            Texture2D secondTexture = null;
            ScriptableObject family = null;
            try
            {
                Sprite first = CreateSprite("Variant One", out firstTexture);
                Sprite second = CreateSprite("Variant Two", out secondTexture);
                family = CreateFamily(
                    20f,
                    Vector2.one,
                    Vector2.zero,
                    first,
                    Variant(
                        "variant.level-1",
                        1,
                        Overrides(25f, null, null, first)),
                    Variant(
                        "variant.level-2",
                        2,
                        Overrides(45f, null, null, second)));

                object levelOne = Resolve(
                    family,
                    "variant.level-1",
                    EmptyOverrides(),
                    "placed.level-1");
                object levelTwo = Resolve(
                    family,
                    "variant.level-2",
                    EmptyOverrides(),
                    "placed.level-2");

                Assert.That(ReadValues(levelOne, "MaximumHealth"), Is.EqualTo(25d));
                Assert.That(ReadValues(levelTwo, "MaximumHealth"), Is.EqualTo(45d));
                Assert.That(ReadValues(levelOne, "IntactSprite"), Is.SameAs(first));
                Assert.That(ReadValues(levelTwo, "IntactSprite"), Is.SameAs(second));
            }
            finally
            {
                if (family != null)
                {
                    UnityEngine.Object.DestroyImmediate(family);
                }

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

        [Test]
        public void FamilyAndVariantFingerprints_AreDeterministicAcrossVariantOrder()
        {
            object firstVariant = Variant(
                "variant.level-1",
                1,
                Overrides(25f, null, null, null));
            object secondVariant = Variant(
                "variant.reinforced",
                3,
                Overrides(70f, new Vector2(3f, 3f), Vector2.zero, null));
            ScriptableObject first = CreateFamily(
                20f,
                Vector2.one,
                Vector2.zero,
                null,
                firstVariant,
                secondVariant);
            ScriptableObject reordered = CreateFamily(
                20f,
                Vector2.one,
                Vector2.zero,
                null,
                CloneVariant(secondVariant),
                CloneVariant(firstVariant));
            try
            {
                Assert.That(Read(first, "Fingerprint"), Is.EqualTo(Read(reordered, "Fingerprint")));

                object firstPreview = Resolve(
                    first,
                    "variant.reinforced",
                    EmptyOverrides(),
                    "placed.fingerprint");
                object secondPreview = Resolve(
                    reordered,
                    "variant.reinforced",
                    EmptyOverrides(),
                    "placed.fingerprint");
                Assert.That(
                    Read(firstPreview, "VariantFingerprint"),
                    Is.EqualTo(Read(secondPreview, "VariantFingerprint")));
                Assert.That(
                    Read(firstPreview, "ResolvedFingerprint"),
                    Is.EqualTo(Read(secondPreview, "ResolvedFingerprint")));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(reordered);
            }
        }

        [Test]
        public void DuplicateVariantIdentity_FailsClosedWithDiagnostic()
        {
            ScriptableObject family = CreateFamilyWithoutValidation(
                Variant("variant.level-1", 1, EmptyOverrides()),
                Variant("variant.level-1", 2, EmptyOverrides()));
            try
            {
                TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
                    () => Invoke(family, "ValidateOrThrow"));
                Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
                Assert.That(exception.InnerException.Message, Does.Contain("duplicate variant"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(family);
            }
        }

        private static ScriptableObject CreateFamily(
            float health,
            Vector2 size,
            Vector2 offset,
            Sprite sprite,
            params object[] variants)
        {
            ScriptableObject family = CreateFamilyWithoutValidation(variants);
            object defaults = CreateValues(health, size, offset, sprite);
            SetField(family, "familyDefaults", defaults);
            Invoke(family, "ValidateOrThrow");
            return family;
        }

        private static ScriptableObject CreateFamilyWithoutValidation(
            params object[] variants)
        {
            AssertTypes();
            ScriptableObject family = ScriptableObject.CreateInstance(FamilyType);
            family.hideFlags = HideFlags.HideAndDontSave;
            SetField(family, "familyId", "family.test-props");
            SetField(family, "displayName", "Test props");
            SetField(family, "defaultVariantId", "variant.level-1");
            SetField(family, "familyDefaults", CreateValues(20f, Vector2.one, Vector2.zero, null));
            Array array = Array.CreateInstance(VariantType, variants.Length);
            for (int index = 0; index < variants.Length; index++)
            {
                array.SetValue(variants[index], index);
            }

            SetField(family, "variants", array);
            return family;
        }

        private static object CreateValues(
            float health,
            Vector2 size,
            Vector2 offset,
            Sprite sprite)
        {
            object values = Activator.CreateInstance(ValuesType);
            SetField(values, "maximumHealth", health);
            SetField(values, "colliderShape", Enum.Parse(ShapeType, "Box"));
            SetField(values, "colliderSize", size);
            SetField(values, "colliderOffset", offset);
            SetField(values, "intactPresentationId", sprite == null
                ? "presentation.test-default"
                : "presentation.test-sprite");
            SetField(values, "intactSprite", sprite);
            SetField(values, "destructionAnimationId", "animation.none");
            SetField(values, "destroyedCollisionPolicy", Enum.Parse(CollisionPolicyType, "Disable"));
            SetField(values, "inheritedRewardProfileId", "reward-profile.none");
            return values;
        }

        private static object Overrides(
            float? health,
            Vector2? size,
            Vector2? offset,
            Sprite sprite)
        {
            object value = EmptyOverrides();
            if (health.HasValue)
            {
                SetField(value, "overrideMaximumHealth", true);
                SetField(value, "maximumHealth", health.Value);
            }

            if (size.HasValue)
            {
                SetField(value, "overrideColliderSize", true);
                SetField(value, "colliderSize", size.Value);
            }

            if (offset.HasValue)
            {
                SetField(value, "overrideColliderOffset", true);
                SetField(value, "colliderOffset", offset.Value);
            }

            if (sprite != null)
            {
                SetField(value, "overrideIntactPresentation", true);
                SetField(value, "intactPresentationId", "presentation.variant-sprite");
                SetField(value, "intactSprite", sprite);
            }

            return value;
        }

        private static object EmptyOverrides()
        {
            Assert.That(OverridesType, Is.Not.Null);
            return Activator.CreateInstance(OverridesType);
        }

        private static object Variant(
            string variantId,
            int level,
            object overrides)
        {
            object variant = Activator.CreateInstance(VariantType);
            SetField(variant, "variantId", variantId);
            SetField(variant, "hasObjectLevel", true);
            SetField(variant, "objectLevel", level);
            SetField(variant, "overrides", overrides);
            return variant;
        }

        private static object CloneVariant(object source)
        {
            return Variant(
                Read(source, "VariantId").ToString(),
                ((int?)Read(source, "ObjectLevel")).Value,
                ReadField(source, "overrides"));
        }

        private static object Resolve(
            ScriptableObject family,
            string variantId,
            object instanceOverrides,
            string placedId)
        {
            MethodInfo method = FamilyType.GetMethod(
                "Resolve",
                BindingFlags.Public | BindingFlags.Instance);
            return method.Invoke(
                family,
                new object[]
                {
                    StableId.Parse(variantId),
                    instanceOverrides,
                    StableId.Parse(placedId)
                });
        }

        private static object ReadValues(object preview, string property)
        {
            return Read(Read(preview, "Values"), property);
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

        private static object ReadField(object instance, string name)
        {
            FieldInfo field = instance.GetType().GetField(
                name,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, name);
            return field.GetValue(instance);
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
            Assert.That(ValuesType, Is.Not.Null);
            Assert.That(OverridesType, Is.Not.Null);
            Assert.That(VariantType, Is.Not.Null);
            Assert.That(FamilyType, Is.Not.Null);
            Assert.That(ShapeType, Is.Not.Null);
            Assert.That(CollisionPolicyType, Is.Not.Null);
        }
    }
}
#endif
