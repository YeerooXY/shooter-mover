using System;
using ShooterMover.UnityAdapters.Combat;
using UnityEngine;

namespace ShooterMover.ContentPackages.Props.DestructibleProps
{
    /// <summary>
    /// Explicit scene-local bridge between projectile hit translation and reusable props.
    /// A composition root supplies the already-owned CombatHit2DAdapter; the component
    /// never discovers an adapter by name, tag, singleton, or scene search.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DestructiblePropCombatContext2D : MonoBehaviour
    {
        [Min(0.01f)]
        [SerializeField] private float confirmedHitDamage = 6f;

        private CombatHit2DAdapter hitAdapter;

        public bool IsConfigured => hitAdapter != null;

        public CombatHit2DAdapter HitAdapter
        {
            get
            {
                if (hitAdapter == null)
                {
                    throw new InvalidOperationException(
                        "Destructible prop combat context is not configured.");
                }

                return hitAdapter;
            }
        }

        public double ConfirmedHitDamage => confirmedHitDamage;

        public void Configure(
            CombatHit2DAdapter configuredHitAdapter,
            double configuredConfirmedHitDamage)
        {
            if (hitAdapter != null)
            {
                if (ReferenceEquals(hitAdapter, configuredHitAdapter)
                    && confirmedHitDamage == configuredConfirmedHitDamage)
                {
                    return;
                }

                throw new InvalidOperationException(
                    "Destructible prop combat context is already configured.");
            }

            if (configuredHitAdapter == null)
            {
                throw new ArgumentNullException(nameof(configuredHitAdapter));
            }

            if (double.IsNaN(configuredConfirmedHitDamage)
                || double.IsInfinity(configuredConfirmedHitDamage)
                || configuredConfirmedHitDamage <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(configuredConfirmedHitDamage));
            }

            hitAdapter = configuredHitAdapter;
            confirmedHitDamage = (float)configuredConfirmedHitDamage;
        }

        private void OnValidate()
        {
            confirmedHitDamage = Mathf.Max(0.01f, confirmedHitDamage);
        }
    }
}
