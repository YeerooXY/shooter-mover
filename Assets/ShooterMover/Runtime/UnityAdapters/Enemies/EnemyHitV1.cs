using System;
using ShooterMover.Domain.Common;
using ShooterMover.EnemyRuntimeComposition;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Enemies
{
    /// <summary>
    /// Neutral hit fact emitted by a live enemy shot.
    /// </summary>
    public sealed class EnemyHitV1
    {
        public EnemyHitV1(
            StableId contactStableId,
            EnemyAttackEffectEmissionV1 emission,
            StableId targetEntityStableId,
            Collider2D targetCollider)
        {
            ContactStableId = contactStableId
                ?? throw new ArgumentNullException(nameof(contactStableId));
            Emission = emission ?? throw new ArgumentNullException(nameof(emission));
            TargetEntityStableId = targetEntityStableId
                ?? throw new ArgumentNullException(nameof(targetEntityStableId));
            TargetCollider = targetCollider
                ?? throw new ArgumentNullException(nameof(targetCollider));
        }

        public StableId ContactStableId { get; }
        public EnemyAttackEffectEmissionV1 Emission { get; }
        public StableId ProjectileStableId { get { return Emission.EmissionStableId; } }
        public StableId AttackOperationStableId
        {
            get { return Emission.Execution.OperationStableId; }
        }
        public StableId SourceEntityStableId { get { return Emission.SourceEntityStableId; } }
        public StableId SourceRunParticipantStableId
        {
            get { return Emission.SourceRunParticipantStableId; }
        }
        public long SourceLifecycleGeneration
        {
            get { return Emission.SourceLifecycleGeneration; }
        }
        public double ResolvedDamage { get { return Emission.ResolvedDamage; } }
        public StableId DamageChannelStableId
        {
            get { return Emission.Execution.Descriptor.DamageChannelId; }
        }
        public StableId TargetEntityStableId { get; }
        public Collider2D TargetCollider { get; }
    }
}
