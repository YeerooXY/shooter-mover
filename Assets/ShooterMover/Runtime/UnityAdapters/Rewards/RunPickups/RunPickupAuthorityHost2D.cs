using System;
using ShooterMover.RunPickups;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Rewards.RunPickups
{
    /// <summary>
    /// Scene-lifetime reference host only. It does not own reward truth; it exposes the
    /// injected engine-neutral authority to generic pickup views and presenters.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunPickupAuthorityHost2D : MonoBehaviour
    {
        private IRunPickupCollectionAuthorityV1 authority;

        public bool IsConfigured { get { return authority != null; } }
        public IRunPickupCollectionAuthorityV1 Authority
        {
            get
            {
                if (authority == null)
                {
                    throw new InvalidOperationException(
                        "Run pickup authority host has not been configured.");
                }
                return authority;
            }
        }

        public void Configure(IRunPickupCollectionAuthorityV1 authority)
        {
            if (authority == null) throw new ArgumentNullException(nameof(authority));
            if (this.authority != null && !ReferenceEquals(this.authority, authority))
            {
                throw new InvalidOperationException(
                    "A run pickup authority host cannot be rebound to another authority.");
            }
            this.authority = authority;
        }
    }
}
