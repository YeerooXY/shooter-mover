using System;
using ShooterMover.UnityAdapters.Rewards.RunPickups;
using UnityEngine;

namespace ShooterMover.Tests.PlayMode.RunPickups
{
    public sealed class ThrowingRunPickupAcceptedFeedback2D : MonoBehaviour,
        IRunRewardPickupAcceptedFeedback
    {
        public bool TryPlayAcceptedCollectionFeedback(
            Transform attractionTarget,
            Action completed)
        {
            throw new InvalidOperationException("test-feedback-failure");
        }
    }
}
