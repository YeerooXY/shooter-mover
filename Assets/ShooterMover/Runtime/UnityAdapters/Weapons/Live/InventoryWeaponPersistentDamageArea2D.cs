using System;
using System.Collections.Generic;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Domain.Weapons.Execution;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Weapons.Live
{
    /// <summary>
    /// Physical persistent pool emitted from the canonical core DoT description.
    /// Damage authority remains downstream; this component exposes immutable pool facts.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InventoryWeaponPersistentDamageArea2D : MonoBehaviour
    {
        private DamageOverTimeProjectileEffect sourceEffect;
        private float remainingSeconds;

        public DamageOverTimeProjectileEffect SourceEffect { get { return sourceEffect; } }
        public double DamagePerSecond { get { return sourceEffect.DotDps; } }
        public double Radius { get { return sourceEffect.PoolRadius; } }
        public float RemainingSeconds { get { return remainingSeconds; } }

        public void Configure(DamageOverTimeProjectileEffect effect)
        {
            sourceEffect = effect ?? throw new ArgumentNullException(nameof(effect));
            if (effect.PoolRadius <= 0d || effect.PoolDuration <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(effect));
            }

            remainingSeconds = (float)effect.PoolDuration;
            var collider = gameObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = (float)effect.PoolRadius;
        }

        private void Update()
        {
            if (sourceEffect == null)
            {
                return;
            }

            remainingSeconds -= Time.deltaTime;
            if (remainingSeconds <= 0f)
            {
                Destroy(gameObject);
            }
        }
    }
}
