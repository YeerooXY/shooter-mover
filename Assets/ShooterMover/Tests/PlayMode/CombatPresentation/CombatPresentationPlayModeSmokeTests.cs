using System.Collections;
using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.UnityAdapters.CombatPresentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.CombatPresentation
{
    public sealed class CombatPresentationPlayModeSmokeTests
    {
        [UnityTest]
        public IEnumerator TwoGenericEnemies_ProjectIndependentHealthAndOneDeathExplosion()
        {
            GameObject owner = new GameObject("combat-presentation-owner");
            GameObject ordinary = new GameObject("ordinary-enemy");
            GameObject large = new GameObject("large-enemy");
            Texture2D texture = null;
            Sprite sprite = null;
            try
            {
                texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
                Color[] pixels = new Color[16 * 16];
                for (int index = 0; index < pixels.Length; index++) pixels[index] = Color.white;
                texture.SetPixels(pixels);
                texture.Apply(false, true);
                sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, 16f, 16f),
                    new Vector2(0.5f, 0.5f),
                    16f);
                ordinary.AddComponent<SpriteRenderer>().sprite = sprite;
                large.AddComponent<SpriteRenderer>().sprite = sprite;
                large.transform.localScale = new Vector3(2f, 2f, 1f);

                long generation = 1L;
                EnemyActorState ordinaryState = CreateState("ordinary", 100d);
                EnemyActorState largeState = CreateState("large", 200d);
                var ordinarySource = new EnemyActorCombatHealthSnapshotSourceV1(
                    ordinaryState.ActorId,
                    () => generation,
                    delegate(out EnemyActorState state)
                    {
                        state = ordinaryState;
                        return true;
                    });
                var largeSource = new EnemyActorCombatHealthSnapshotSourceV1(
                    largeState.ActorId,
                    () => generation,
                    delegate(out EnemyActorState state)
                    {
                        state = largeState;
                        return true;
                    });

                CombatHealthBarPresenter2D ordinaryBar =
                    ordinary.AddComponent<CombatHealthBarPresenter2D>();
                CombatHealthBarPresenter2D largeBar =
                    large.AddComponent<CombatHealthBarPresenter2D>();
                ordinaryBar.Configure(ordinaryState.ActorId, ordinarySource, Vector3.up);
                largeBar.Configure(largeState.ActorId, largeSource, Vector3.up);

                DefaultCombatExplosionPool2D pool =
                    owner.AddComponent<DefaultCombatExplosionPool2D>();
                pool.ConfigureForTests(4, 1f);
                EnemyDeathVfxPresenter2D ordinaryDeath =
                    ordinary.AddComponent<EnemyDeathVfxPresenter2D>();
                EnemyDeathVfxPresenter2D largeDeath =
                    large.AddComponent<EnemyDeathVfxPresenter2D>();
                ordinaryDeath.Configure(
                    ordinaryState.ActorId,
                    generation,
                    ordinaryBar,
                    pool);
                largeDeath.Configure(
                    largeState.ActorId,
                    generation,
                    largeBar,
                    pool);

                ordinaryState = EnemyActorStepper.Step(
                    ordinaryState,
                    new[]
                    {
                        EnemyActorCommand.Damage(
                            1L,
                            Id("event", "ordinary-damage"),
                            Id("actor", "player"),
                            EnemyContactPolicy.KineticChannelValue,
                            40d),
                    }).State;
                Assert.That(
                    ordinaryBar.Refresh(),
                    Is.EqualTo(CombatHealthBarRefreshStatusV1.Applied));
                Assert.That(ordinaryBar.CurrentSnapshot.NormalizedFill, Is.EqualTo(0.6d));
                Assert.That(largeBar.CurrentSnapshot.NormalizedFill, Is.EqualTo(1d));

                EnemyActorStepResult lethal = EnemyActorStepper.Step(
                    ordinaryState,
                    new[]
                    {
                        EnemyActorCommand.Damage(
                            2L,
                            Id("event", "ordinary-death"),
                            Id("actor", "player"),
                            EnemyContactPolicy.KineticChannelValue,
                            100d),
                    });
                ordinaryState = lethal.State;
                EnemyDestroyedNotification destroyed = null;
                for (int index = 0; index < lethal.Notifications.Count; index++)
                {
                    destroyed = lethal.Notifications[index] as EnemyDestroyedNotification;
                    if (destroyed != null) break;
                }
                Assert.That(destroyed, Is.Not.Null);
                Assert.That(
                    ordinaryBar.Refresh(),
                    Is.EqualTo(CombatHealthBarRefreshStatusV1.HiddenTerminal));

                float ordinaryBounds = EnemyPresentationBounds2D.MeasureLargestDimension(
                    ordinary.transform);
                EnemyTerminalPresentationFactV1 terminal =
                    new EnemyTerminalPresentationFactV1(
                        destroyed.EventId,
                        destroyed.TargetId,
                        generation,
                        ordinary.transform.position,
                        ordinaryBounds);
                Assert.That(
                    ordinaryDeath.TryPresent(terminal),
                    Is.EqualTo(EnemyDeathVfxPresentationStatusV1.Spawned));
                Assert.That(
                    ordinaryDeath.TryPresent(terminal),
                    Is.EqualTo(EnemyDeathVfxPresentationStatusV1.ExactReplay));
                Assert.That(pool.TotalSpawnCount, Is.EqualTo(1));
                Assert.That(ordinaryBar.IsVisible, Is.False);
                Assert.That(largeBar.IsVisible, Is.True);
                Assert.That(largeBar.CurrentSnapshot.NormalizedFill, Is.EqualTo(1d));

                float largeBounds = EnemyPresentationBounds2D.MeasureLargestDimension(
                    large.transform);
                Assert.That(largeBounds, Is.GreaterThan(ordinaryBounds));
                Assert.That(
                    new EnemyDeathVfxScaleConfigurationV1().Resolve(largeBounds),
                    Is.GreaterThan(
                        new EnemyDeathVfxScaleConfigurationV1().Resolve(ordinaryBounds)));

                generation = 2L;
                ordinaryState = CreateState("ordinary", 100d);
                Assert.That(ordinaryDeath.AdvanceLifecycle(generation), Is.True);
                Assert.That(
                    ordinaryBar.Refresh(),
                    Is.EqualTo(CombatHealthBarRefreshStatusV1.Applied));
                Assert.That(ordinaryBar.IsVisible, Is.True);
                Assert.That(ordinaryBar.CurrentSnapshot.LifecycleGeneration, Is.EqualTo(2L));

                yield return null;
            }
            finally
            {
                Object.Destroy(owner);
                Object.Destroy(ordinary);
                Object.Destroy(large);
                if (sprite != null) Object.Destroy(sprite);
                if (texture != null) Object.Destroy(texture);
            }
        }

        private static EnemyActorState CreateState(string value, double health)
        {
            return EnemyActorState.Create(
                Id("actor", value),
                Id("enemy-role", "generic"),
                health,
                1,
                EnemyContactPolicy.Create(
                    EnemyContactMode.None,
                    0d,
                    0.5d,
                    0.05d,
                    8));
        }

        private static StableId Id(string namespaceName, string value)
        {
            return StableId.Create(namespaceName, value);
        }
    }
}
