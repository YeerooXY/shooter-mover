using System;
using ShooterMover.RunLoot;
using ShooterMover.UnityAdapters.Rewards.RunLoots;
using UnityEngine;

namespace ShooterMover.UI.StrongboxOpening
{
    /// <summary>
    /// Optional projection bridge from one canonical run-local pickup snapshot into the
    /// reusable loot visual. Unsupported content returns false so the generic registry
    /// sprite remains the presentation fallback.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LootVisual))]
    public sealed class LootRunView : MonoBehaviour,
        IRunLootPickupViewBinder,
        IRunLootPickupAcceptedFeedback
    {
        private LootVisual visual;
        private Action retirementCompletion;
        private bool bound;

        public bool IsBound { get { return bound; } }

        public bool TryBindRunLoot(
            RunLootSnapshot immutablePickup,
            out string diagnostic)
        {
            bound = false;
            diagnostic = string.Empty;
            if (immutablePickup == null)
            {
                diagnostic = "loot-pickup-run-projection-null";
                return false;
            }

            LootPickupPresentation projection;
            if (!LootPickupPresentation.TryCreate(
                    immutablePickup.PickupStableId,
                    immutablePickup.Reward.RewardInstanceStableId,
                    immutablePickup.Reward.Kind,
                    immutablePickup.Reward.ContentStableId,
                    immutablePickup.Reward.Quantity,
                    out projection,
                    out diagnostic))
            {
                return false;
            }

            visual = GetComponent<LootVisual>();
            if (visual == null)
            {
                diagnostic = "loot-pickup-run-projection-visual-missing";
                return false;
            }

            try
            {
                visual.Bind(projection);
            }
            catch (Exception exception)
            {
                diagnostic = "loot-pickup-run-projection-bind-exception:"
                    + exception.GetType().Name
                    + ":"
                    + exception.Message;
                return false;
            }

            SpriteRenderer legacyRenderer = GetComponent<SpriteRenderer>();
            if (legacyRenderer != null)
            {
                legacyRenderer.enabled = false;
            }

            bound = true;
            diagnostic = string.Empty;
            return true;
        }

        public bool TryPlayAcceptedCollectionFeedback(
            Transform attractionTarget,
            Action completed)
        {
            if (!bound
                || visual == null
                || attractionTarget == null
                || completed == null
                || retirementCompletion != null)
            {
                return false;
            }

            retirementCompletion = completed;
            visual.AcceptedCollectionFeedbackCompleted +=
                HandleAcceptedCollectionFeedbackCompleted;
            if (visual.PlayAcceptedCollectionFeedback(attractionTarget.position))
            {
                return true;
            }

            visual.AcceptedCollectionFeedbackCompleted -=
                HandleAcceptedCollectionFeedbackCompleted;
            retirementCompletion = null;
            return false;
        }

        private void HandleAcceptedCollectionFeedbackCompleted()
        {
            if (visual != null)
            {
                visual.AcceptedCollectionFeedbackCompleted -=
                    HandleAcceptedCollectionFeedbackCompleted;
            }

            Action completed = retirementCompletion;
            retirementCompletion = null;
            if (completed != null)
            {
                completed();
            }
        }

        private void OnDestroy()
        {
            if (visual != null)
            {
                visual.AcceptedCollectionFeedbackCompleted -=
                    HandleAcceptedCollectionFeedbackCompleted;
            }
            retirementCompletion = null;
        }
    }
}
