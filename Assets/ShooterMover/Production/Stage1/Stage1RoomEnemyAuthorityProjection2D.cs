using System.Collections.Generic;
using System.ComponentModel;
using ShooterMover.Domain.Weapons.Execution;
using ShooterMover.Production.Stage1;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    /// <summary>
    /// Compatibility component retained for the DEMO-CUTOVER-001 presentation catalog.
    /// New code should use <see cref="Stage1RoomEnemyAuthorityAdapter2D"/>.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DisallowMultipleComponent]
    internal sealed class Stage1RoomEnemyAuthorityProjection2D :
        Stage1RoomEnemyAuthorityAdapter2D
    {
    }

    [DisallowMultipleComponent]
    internal sealed class Stage1InventoryWeaponProjectileHit2D : MonoBehaviour
    {
        private Stage1PlayableLoopCompositionV1 owner;
        private InventoryWeaponEffectInstance2D effect;
        private readonly HashSet<Collider2D> hitColliders = new HashSet<Collider2D>();
        private int hitOrdinal;
        private int remainingPierce;
        private bool pierceInitialized;

        public void Configure(
            Stage1PlayableLoopCompositionV1 configuredOwner,
            InventoryWeaponEffectInstance2D configuredEffect)
        {
            owner = configuredOwner;
            effect = configuredEffect;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (owner == null
                || effect == null
                || other == null
                || !hitColliders.Add(other))
            {
                return;
            }

            int configuredPierce;
            if (!owner.TryApplyProjectileHit(
                    other,
                    effect,
                    hitOrdinal++,
                    out configuredPierce))
            {
                return;
            }

            if (!pierceInitialized)
            {
                remainingPierce = configuredPierce;
                pierceInitialized = true;
            }
            if (remainingPierce <= 0)
            {
                Destroy(gameObject);
                return;
            }
            remainingPierce--;
        }
    }

    [DisallowMultipleComponent]
    internal sealed class Stage1InventoryWeaponPoolDamage2D : MonoBehaviour
    {
        private Stage1PlayableLoopCompositionV1 owner;
        private InventoryWeaponPersistentDamageArea2D pool;
        private readonly Dictionary<Collider2D, float> nextTick =
            new Dictionary<Collider2D, float>();
        private int tick;

        public void Configure(
            Stage1PlayableLoopCompositionV1 configuredOwner,
            InventoryWeaponPersistentDamageArea2D configuredPool)
        {
            owner = configuredOwner;
            pool = configuredPool;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (owner == null || pool == null || other == null) return;
            float next;
            if (nextTick.TryGetValue(other, out next) && Time.time < next)
            {
                return;
            }
            nextTick[other] = Time.time + 0.25f;
            owner.ApplyPoolTick(pool, other, tick++);
        }
    }
}
