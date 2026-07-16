using ShooterMover.ContentPackages.Environment.VoidHazards;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.Tests.PlayMode.Environment.VoidHazards
{
    public sealed class VoidHazardTestPorts :
        MonoBehaviour,
        IVoidHazardCombatPort,
        IVoidHazardCheckpointPort,
        IVoidHazardRespawnPort,
        IVoidHazardEnemyFallPort,
        IVoidHazardProjectileRemovalPort,
        IVoidHazardPropRemovalPort,
        IVoidHazardPresentationPort
    {
        public int DamageRequestCount { get; private set; }
        public int InstantDeathRequestCount { get; private set; }
        public int RespawnRequestCount { get; private set; }
        public int EnemyFallRequestCount { get; private set; }
        public int ProjectileRemovalRequestCount { get; private set; }
        public int PropRemovalRequestCount { get; private set; }
        public int PresentationCount { get; private set; }

        public VoidHazardDamageRequest LastDamageRequest { get; private set; }
        public VoidHazardInstantDeathRequest LastInstantDeathRequest { get; private set; }
        public VoidHazardRespawnRequest LastRespawnRequest { get; private set; }
        public VoidHazardEnemyFallRequest LastEnemyFallRequest { get; private set; }
        public VoidHazardRemovalRequest LastProjectileRemovalRequest { get; private set; }
        public VoidHazardRemovalRequest LastPropRemovalRequest { get; private set; }
        public VoidHazardPresentationEvent LastPresentationEvent { get; private set; }

        public VoidHazardPortResult NextPortResult { get; set; } =
            VoidHazardPortResult.Accepted;
        public bool ResolveCheckpoint { get; set; } = true;
        public StableId DestinationId { get; set; } =
            StableId.Parse("destination.alpha");

        public VoidHazardPortResult RequestDamage(VoidHazardDamageRequest request)
        {
            DamageRequestCount++;
            LastDamageRequest = request;
            return NextPortResult;
        }

        public VoidHazardPortResult RequestInstantDeath(
            VoidHazardInstantDeathRequest request)
        {
            InstantDeathRequestCount++;
            LastInstantDeathRequest = request;
            return NextPortResult;
        }

        public bool TryResolveCheckpoint(
            StableId checkpointId,
            out VoidHazardRespawnDestination destination)
        {
            destination = ResolveCheckpoint
                ? new VoidHazardRespawnDestination(DestinationId)
                : null;
            return ResolveCheckpoint;
        }

        public VoidHazardPortResult RequestRespawn(VoidHazardRespawnRequest request)
        {
            RespawnRequestCount++;
            LastRespawnRequest = request;
            return NextPortResult;
        }

        public VoidHazardPortResult RequestEnemyFall(VoidHazardEnemyFallRequest request)
        {
            EnemyFallRequestCount++;
            LastEnemyFallRequest = request;
            return NextPortResult;
        }

        public VoidHazardPortResult RequestProjectileRemoval(
            VoidHazardRemovalRequest request)
        {
            ProjectileRemovalRequestCount++;
            LastProjectileRemovalRequest = request;
            return NextPortResult;
        }

        public VoidHazardPortResult RequestPropRemoval(VoidHazardRemovalRequest request)
        {
            PropRemovalRequestCount++;
            LastPropRemovalRequest = request;
            return NextPortResult;
        }

        public void Present(VoidHazardPresentationEvent presentationEvent)
        {
            PresentationCount++;
            LastPresentationEvent = presentationEvent;
        }
    }
}
