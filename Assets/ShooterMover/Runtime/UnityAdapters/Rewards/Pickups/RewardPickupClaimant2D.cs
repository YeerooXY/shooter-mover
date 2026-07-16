using System;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Rewards.Pickups
{
    /// <summary>
    /// Explicit stable claimant identity consumed by physical pickup trigger callbacks.
    /// Unity object IDs, tags, names, and hierarchy positions are intentionally ignored.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RewardPickupClaimant2D : MonoBehaviour
    {
        [SerializeField] private string claimantId = "claimant.player";

        private StableId parsedClaimantId;
        private string configurationError;

        public bool TryGetClaimantStableId(out StableId value)
        {
            if (parsedClaimantId == null && string.IsNullOrEmpty(configurationError))
            {
                if (!StableId.TryParse(claimantId, out parsedClaimantId))
                {
                    configurationError = "Claimant ID is not a canonical StableId.";
                }
            }

            value = parsedClaimantId;
            return value != null;
        }

        public StableId ClaimantStableId
        {
            get
            {
                StableId value;
                if (!TryGetClaimantStableId(out value))
                {
                    throw new InvalidOperationException(configurationError);
                }

                return value;
            }
        }

        public string ConfigurationError { get { return configurationError ?? string.Empty; } }

        public void ConfigureForTests(string claimantId)
        {
            this.claimantId = claimantId;
            parsedClaimantId = null;
            configurationError = null;
            StableId ignored;
            if (!TryGetClaimantStableId(out ignored))
            {
                throw new ArgumentException(configurationError, nameof(claimantId));
            }
        }

        private void OnValidate()
        {
            parsedClaimantId = null;
            configurationError = null;
        }
    }
}
