using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Weapons.Execution;
using ShooterMover.UnityAdapters.Weapons.Live;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Weapons.Live
{
    public sealed partial class InventoryWeaponRuntimePlayModeTests
    {
        [UnityTest]
        public IEnumerator ShotgunLaunchesSevenPhysicalPelletsOnDistinctTrajectories()
        {
            Fixture fixture = CreateFixture();
            try
            {
                Assert.That(
                    fixture.Runtime.SelectSlot(1),
                    Is.EqualTo(
                        InventoryWeaponSlotSelectionStatus.Selected));

                InventoryWeaponExecutionResult result =
                    fixture.Runtime.TryFireAtTarget(
                        new FireOperationId(
                            StableId.Parse(
                                "fire.playmode-shotgun-distinct-fan")),
                        0L,
                        123UL,
                        new WeaponVector2(0d, 0d),
                        new WeaponVector2(10d, 0d));

                Assert.That(
                    result.Status,
                    Is.EqualTo(WeaponExecutionStatus.Accepted));
                Assert.That(
                    result.EffectBatch.CoreBatch.EffectCount,
                    Is.EqualTo(7));
                Assert.That(
                    fixture.Emitter.EmittedEffects.Count,
                    Is.EqualTo(7));

                var directionKeys = new HashSet<string>();
                var startPositions = new List<Vector2>(7);
                for (int index = 0;
                    index < fixture.Emitter.EmittedEffects.Count;
                    index++)
                {
                    InventoryWeaponEffectInstance2D pellet =
                        fixture.Emitter.EmittedEffects[index];
                    Assert.That(pellet.IsLaunched, Is.True);
                    Assert.That(
                        pellet.Description,
                        Is.TypeOf<DirectProjectileEffect>());
                    directionKeys.Add(
                        pellet.TravelDirection.x.ToString("R")
                        + "|"
                        + pellet.TravelDirection.y.ToString("R"));
                    startPositions.Add(pellet.transform.position);
                }
                Assert.That(directionKeys.Count, Is.EqualTo(7));

                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();

                float minimumY = float.PositiveInfinity;
                float maximumY = float.NegativeInfinity;
                var positionKeys = new HashSet<string>();
                for (int index = 0;
                    index < fixture.Emitter.EmittedEffects.Count;
                    index++)
                {
                    Vector2 position = fixture.Emitter
                        .EmittedEffects[index].transform.position;
                    Assert.That(
                        position.x,
                        Is.GreaterThan(startPositions[index].x + 0.1f));
                    minimumY = Mathf.Min(minimumY, position.y);
                    maximumY = Mathf.Max(maximumY, position.y);
                    positionKeys.Add(
                        position.x.ToString("F4")
                        + "|"
                        + position.y.ToString("F4"));
                }

                Assert.That(positionKeys.Count, Is.EqualTo(7));
                Assert.That(minimumY, Is.LessThan(-0.1f));
                Assert.That(maximumY, Is.GreaterThan(0.1f));
                Assert.That(
                    maximumY - minimumY,
                    Is.GreaterThan(0.35f));
            }
            finally
            {
                fixture.Dispose();
            }
        }
    }
}
