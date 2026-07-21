using System;
using ShooterMover.Combat.HitPolicy;
using ShooterMover.Domain.Common;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.GameplayEntities;
using ShooterMover.UnityAdapters.Enemies;
using ShooterMover.UnityAdapters.Players;

namespace ShooterMover.UnityAdapters.Production.Level1
{
    /// <summary>
    /// Typed production bridge between the schema-v2 enemy hit router and the already composed
    /// Level 1 player runtime. It exports immutable actor snapshots and delegates accepted damage;
    /// it owns no health, faction, replay, or lifecycle truth.
    /// </summary>
    public sealed class Level1EnemyAttackPatternCombatContextV1 :
        IEnemyAttackPatternCombatContextV1
    {
        private readonly Level1PlayerRuntimeSceneAdapterV1 player;
        private readonly EnemyAttackPatternUnitySourceRegistryV1 sources;

        public Level1EnemyAttackPatternCombatContextV1(
            Level1PlayerRuntimeSceneAdapterV1 player,
            EnemyAttackPatternUnitySourceRegistryV1 sources)
        {
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            this.sources = sources ?? throw new ArgumentNullException(nameof(sources));
        }

        public bool TryReadSource(
            EnemyAttackEffectEmissionV1 emission,
            out CombatActorSnapshotV1 source)
        {
            return sources.TryReadSource(emission, out source);
        }

        public bool TryReadTarget(
            StableId targetEntityStableId,
            out CombatActorSnapshotV1 target)
        {
            target = null;
            if (targetEntityStableId == null || !player.IsInitialized)
            {
                return false;
            }
            PlayerRuntimeSnapshot runtime = player.ExportSnapshot();
            PlayerActorSnapshot snapshot = runtime == null
                ? null
                : runtime.Player;
            if (snapshot == null
                || snapshot.ActorInstanceId != targetEntityStableId)
            {
                return false;
            }
            target = new CombatActorSnapshotV1(
                snapshot.ActorInstanceId,
                snapshot.Identity,
                snapshot.LifecycleGeneration,
                true,
                snapshot.IsAlive,
                new[]
                {
                    CombatHitCapabilityIdsV1.DamageReceiver,
                });
            return true;
        }

        public DamageReceiverResult ApplyPlayerDamage(PlayerDamageRequest request)
        {
            return request == null || !player.IsInitialized
                ? null
                : player.ApplyDamage(request);
        }
    }
}
