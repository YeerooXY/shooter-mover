using NUnit.Framework;
using ShooterMover.UnityAdapters.CombatPresentation;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.CombatPresentation
{
    public sealed class CombatDeathVfxFactoryV1Tests
    {
        [Test]
        public void AuthoredFrames_UseInjectedSpriteAnimationInsteadOfFallback()
        {
            GameObject root = new GameObject("sprite-vfx-factory-test");
            Texture2D texture = null;
            Sprite sprite = null;
            try
            {
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.SetPixels(new[]
                {
                    Color.white,
                    Color.white,
                    Color.white,
                    Color.white,
                });
                texture.Apply(false, true);
                sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, 2f, 2f),
                    new Vector2(0.5f, 0.5f),
                    2f);

                var fallback = new CountingFallbackFactory();
                var definition = new SpriteAnimationCombatDeathVfxDefinitionV1(
                    "retained.test-animation",
                    new[] { sprite },
                    0.05f,
                    new Vector2(0.25f, -0.5f),
                    new Vector2(1.5f, 0.75f),
                    77,
                    false);
                var factory = new SpriteAnimationCombatDeathVfxFactory2D(
                    definition,
                    fallback);
                CombatDeathVfxPool2D pool = root.AddComponent<CombatDeathVfxPool2D>();
                pool.Configure(factory, 2);

                ICombatDeathVfxInstance2D instance = pool.Spawn(
                    new Vector3(3f, 4f, 0f),
                    2f);
                SpriteAnimationCombatDeathVfxInstance2D animation =
                    instance as SpriteAnimationCombatDeathVfxInstance2D;
                Assert.That(animation, Is.Not.Null);
                Assert.That(fallback.CreateCount, Is.EqualTo(0));
                Assert.That(pool.SourcePresentationId, Is.EqualTo("retained.test-animation"));
                Assert.That(animation.transform.position, Is.EqualTo(new Vector3(3.25f, 3.5f, 0f)));
                Assert.That(animation.transform.localScale, Is.EqualTo(new Vector3(3f, 1.5f, 2f)));
                SpriteRenderer renderer = animation.GetComponent<SpriteRenderer>();
                Assert.That(renderer.sprite, Is.SameAs(sprite));
                Assert.That(renderer.sortingOrder, Is.EqualTo(77));
                Assert.That(renderer.GetComponent<Collider2D>(), Is.Null);
                Assert.That(renderer.GetComponent<Rigidbody2D>(), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (sprite != null) UnityEngine.Object.DestroyImmediate(sprite);
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private sealed class CountingFallbackFactory : ICombatDeathVfxFactory2D
        {
            public string SourcePresentationId
            {
                get { return "test.fallback"; }
            }

            public int CreateCount { get; private set; }

            public ICombatDeathVfxInstance2D Create(Transform parent, int ordinal)
            {
                CreateCount++;
                return null;
            }

            public void Dispose()
            {
            }
        }
    }
}
