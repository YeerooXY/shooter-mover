using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.UnityAdapters.CombatPresentation;
using ShooterMover.UnityAdapters.Enemies;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.CombatPresentation
{
    public sealed class CombatPresentationPlayModeSmokeTests
    {
        [UnityTest]
        public IEnumerator ThreeGenericEnemies_UseSameRegistrationAndRetainedVfxSource()
        {
            GameObject owner = new GameObject("combat-presentation-owner");
            GameObject ordinary = new GameObject("ordinary-enemy");
            GameObject large = new GameObject("large-enemy");
            GameObject third = new GameObject("third-factory-enemy");
            try
            {
                float retainedLifetime;
                int retainedFrameCount;
                ICombatDeathVfxFactory2D retainedFactory = BuildRetainedFactory(
                    out retainedLifetime,
                    out retainedFrameCount);
                CombatDeathVfxPool2D pool = owner.AddComponent<CombatDeathVfxPool2D>();
                pool.Configure(retainedFactory, 4);

                ordinary.AddComponent<BoxCollider2D>().size = Vector2.one;
                large.AddComponent<BoxCollider2D>().size = Vector2.one;
                large.transform.localScale = new Vector3(2f, 2f, 1f);
                third.AddComponent<BoxCollider2D>().size = Vector2.one;

                FakeEnemyAuthority2D ordinaryAuthority =
                    ordinary.AddComponent<FakeEnemyAuthority2D>();
                FakeEnemyAuthority2D largeAuthority =
                    large.AddComponent<FakeEnemyAuthority2D>();
                FakeEnemyAuthority2D thirdAuthority =
                    third.AddComponent<FakeEnemyAuthority2D>();
                ordinaryAuthority.Configure("ordinary", 100d);
                largeAuthority.Configure("large", 200d);
                thirdAuthority.Configure("third", 150d);

                CombatEnemyPresentationRegistration2D ordinaryRegistration =
                    CombatEnemyPresentationRegistration2D.Attach(
                        ordinary,
                        ordinaryAuthority,
                        pool,
                        Vector3.up);
                CombatEnemyPresentationRegistration2D largeRegistration =
                    CombatEnemyPresentationRegistration2D.Attach(
                        large,
                        largeAuthority,
                        pool,
                        Vector3.up);
                CombatEnemyPresentationRegistration2D thirdRegistration =
                    CombatEnemyPresentationRegistration2D.Attach(
                        third,
                        thirdAuthority,
                        pool,
                        Vector3.up);
                var ordinaryRuntime = new CombatPresentationEnemyActorAuthority2D(
                    ordinaryAuthority,
                    ordinaryRegistration);

                Assert.That(ordinaryRegistration.GetType(), Is.EqualTo(largeRegistration.GetType()));
                Assert.That(largeRegistration.GetType(), Is.EqualTo(thirdRegistration.GetType()));
                Assert.That(pool.SourcePresentationId, Is.EqualTo("retained.asset:ExplosiveDestructionAnimation"));

                ordinaryRuntime.Apply(
                    EnemyActorCommand.Damage(
                        1L,
                        Id("combat-event", "ordinary-damage"),
                        Id("actor", "player"),
                        EnemyContactPolicy.KineticChannelValue,
                        40d));
                Assert.That(
                    ordinaryRegistration.Refresh(),
                    Is.EqualTo(CombatHealthBarRefreshStatusV1.Applied));
                Assert.That(
                    ordinaryRegistration.HealthBar.CurrentSnapshot.NormalizedFill,
                    Is.EqualTo(0.6d));
                Assert.That(
                    largeRegistration.HealthBar.CurrentSnapshot.NormalizedFill,
                    Is.EqualTo(1d));
                Assert.That(
                    thirdRegistration.HealthBar.CurrentSnapshot.NormalizedFill,
                    Is.EqualTo(1d));

                ordinaryRuntime.Apply(
                    EnemyActorCommand.Damage(
                        2L,
                        Id("combat-event", "ordinary-death"),
                        Id("actor", "player"),
                        EnemyContactPolicy.KineticChannelValue,
                        100d));
                Assert.That(pool.TotalSpawnCount, Is.EqualTo(1));
                Assert.That(ordinaryRegistration.HealthBar.IsVisible, Is.False);
                Assert.That(largeRegistration.HealthBar.IsVisible, Is.True);
                Assert.That(thirdRegistration.HealthBar.IsVisible, Is.True);

                ordinaryRuntime.Apply(
                    EnemyActorCommand.Damage(
                        2L,
                        Id("combat-event", "ordinary-death"),
                        Id("actor", "player"),
                        EnemyContactPolicy.KineticChannelValue,
                        100d));
                Assert.That(pool.TotalSpawnCount, Is.EqualTo(1));

                float ordinaryBounds = EnemyPresentationBounds2D.MeasureLargestDimension(
                    ordinary.transform);
                float largeBounds = EnemyPresentationBounds2D.MeasureLargestDimension(
                    large.transform);
                var scalePolicy = new EnemyDeathVfxScaleConfigurationV1();
                Assert.That(largeBounds, Is.GreaterThan(ordinaryBounds));
                Assert.That(
                    scalePolicy.Resolve(largeBounds),
                    Is.GreaterThan(scalePolicy.Resolve(ordinaryBounds)));

                if (retainedFrameCount == 0)
                {
                    Assert.That(
                        owner.GetComponentInChildren<FallbackRingCombatDeathVfxInstance2D>(true),
                        Is.Not.Null,
                        "The retained asset currently has no frames, so the explicit fallback should play.");
                }
                else
                {
                    Assert.That(
                        owner.GetComponentInChildren<SpriteAnimationCombatDeathVfxInstance2D>(true),
                        Is.Not.Null);
                }

                Assert.That(ordinaryRuntime.Reset(), Is.True);
                Assert.That(ordinaryRegistration.HealthBar.IsVisible, Is.True);
                Assert.That(
                    ordinaryRegistration.HealthBar.CurrentSnapshot.LifecycleGeneration,
                    Is.EqualTo(2L));

                yield return new WaitForSeconds(retainedLifetime + 0.1f);
                Assert.That(pool.ActiveCount, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.Destroy(owner);
                UnityEngine.Object.Destroy(ordinary);
                UnityEngine.Object.Destroy(large);
                UnityEngine.Object.Destroy(third);
            }
        }

        private static ICombatDeathVfxFactory2D BuildRetainedFactory(
            out float lifetime,
            out int frameCount)
        {
            ScriptableObject source = Resources.Load<ScriptableObject>(
                "CombatPresentation/Stage1DefaultEnemyDeathVfx");
            Assert.That(source, Is.Not.Null);
            PropertyInfo animationProperty = source.GetType().GetProperty(
                "Animation",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(animationProperty, Is.Not.Null);
            object animation = animationProperty.GetValue(source, null);
            Assert.That(animation, Is.Not.Null);

            Type type = animation.GetType();
            frameCount = (int)type.GetProperty("FrameCount").GetValue(animation, null);
            float secondsPerFrame =
                (float)type.GetProperty("SecondsPerFrame").GetValue(animation, null);
            Vector2 localOffset =
                (Vector2)type.GetProperty("LocalOffset").GetValue(animation, null);
            Vector2 visualScale =
                (Vector2)type.GetProperty("VisualScale").GetValue(animation, null);
            int sortingOrder =
                (int)type.GetProperty("SortingOrder").GetValue(animation, null);
            bool useUnscaledTime =
                (bool)type.GetProperty("UseUnscaledTime").GetValue(animation, null);
            MethodInfo getFrame = type.GetMethod(
                "GetFrame",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(getFrame, Is.Not.Null);

            Sprite[] frames = new Sprite[frameCount];
            for (int index = 0; index < frames.Length; index++)
            {
                frames[index] = getFrame.Invoke(animation, new object[] { index }) as Sprite;
            }
            UnityEngine.Object animationObject = animation as UnityEngine.Object;
            Assert.That(animationObject, Is.Not.Null);

            lifetime = frameCount > 0
                ? Mathf.Max(0.01f, frameCount * secondsPerFrame)
                : FallbackRingCombatDeathVfxFactory2D.DefaultLifetimeSeconds;
            var definition = new SpriteAnimationCombatDeathVfxDefinitionV1(
                "retained.asset:" + animationObject.name,
                frames,
                secondsPerFrame,
                localOffset,
                visualScale,
                sortingOrder,
                useUnscaledTime);
            return new SpriteAnimationCombatDeathVfxFactory2D(
                definition,
                new FallbackRingCombatDeathVfxFactory2D());
        }

        private static StableId Id(string namespaceName, string value)
        {
            return StableId.Create(namespaceName, value);
        }

        public sealed class FakeEnemyAuthority2D :
            MonoBehaviour,
            IEnemyActor2DAuthority,
            ICombatPresentationLifecycleSourceV1
        {
            private StableId actorId;
            private double maximumHealth;
            private EnemyActorState state;

            public long Generation { get; private set; }

            public void Configure(string value, double health)
            {
                actorId = Id("actor", value);
                maximumHealth = health;
                Generation = 1L;
                state = CreateState();
            }

            public bool TryReadState(out EnemyActorState current)
            {
                current = state;
                return current != null;
            }

            public EnemyActorStepResult Apply(EnemyActorCommand command)
            {
                EnemyActorStepResult result = EnemyActorStepper.Step(
                    state,
                    new[] { command });
                state = result.State;
                return result;
            }

            public bool Reset()
            {
                Generation++;
                state = CreateState();
                return true;
            }

            private EnemyActorState CreateState()
            {
                return EnemyActorState.Create(
                    actorId,
                    Id("enemy-role", "generic"),
                    maximumHealth,
                    1,
                    EnemyContactPolicy.Create(
                        EnemyContactMode.None,
                        0d,
                        0.5d,
                        0.05d,
                        8));
            }
        }
    }
}
