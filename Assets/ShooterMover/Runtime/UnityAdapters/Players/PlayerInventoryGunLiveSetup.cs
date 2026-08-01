using System;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Guns.Catalog;
using ShooterMover.Application.Guns.Execution;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Catalog;
using ShooterMover.Domain.Guns.Execution;
using ShooterMover.UnityAdapters.Guns.Live;

namespace ShooterMover.UnityAdapters.Players
{
    public sealed class PlayerLiveGunStateBridge :
        IInventoryGunActorStateSource,
        IGunActorOwnershipResolver
    {
        private readonly PlayerSetup playerRuntime;

        public PlayerLiveGunStateBridge(PlayerSetup runtime)
        {
            playerRuntime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public bool TryResolveActorState(
            out GunActorInstanceId actorId,
            out LifecycleGeneration lifecycleGeneration)
        {
            actorId = null;
            lifecycleGeneration = null;
            if (playerRuntime.IsDisposed)
            {
                return false;
            }

            PlayerSnapshot snapshot;
            try
            {
                snapshot = playerRuntime.ExportSnapshot();
            }
            catch
            {
                return false;
            }

            if (snapshot == null
                || snapshot.Player == null
                || snapshot.Player.ActorInstanceId == null
                || snapshot.Player.LifecycleGeneration < 0L)
            {
                return false;
            }

            actorId = new GunActorInstanceId(snapshot.Player.ActorInstanceId);
            lifecycleGeneration = new LifecycleGeneration(
                snapshot.Player.LifecycleGeneration);
            return true;
        }

        public bool TryResolveParticipant(
            GunActorInstanceId actorId,
            LifecycleGeneration lifecycleGeneration,
            out RunParticipantId participantId)
        {
            participantId = null;
            if (actorId == null
                || lifecycleGeneration == null
                || playerRuntime.IsDisposed)
            {
                return false;
            }

            PlayerSnapshot snapshot;
            try
            {
                snapshot = playerRuntime.ExportSnapshot();
            }
            catch
            {
                return false;
            }

            if (snapshot == null
                || snapshot.Player == null
                || snapshot.Player.ActorInstanceId != actorId.Value
                || snapshot.Player.LifecycleGeneration != lifecycleGeneration.Value
                || snapshot.Player.RunParticipantId == null)
            {
                return false;
            }

            participantId = new RunParticipantId(snapshot.Player.RunParticipantId);
            return true;
        }
    }

    /// <summary>
    /// Production composition root for inventory-backed player guns. The canonical overload
    /// resolves the exact instance in the first active equipped class mount before constructing the
    /// retained scheduler adapter. No scene-local gun fallback is created.
    /// </summary>
    public sealed class PlayerInventoryGunLiveSetupRoot : IDisposable
    {
        private PlayerInventoryGunLiveSetupRoot(
            PlayerLiveGunStateBridge playerState,
            RouteProfileActiveGunSource activeGun,
            InventoryGunLiveSetup runtime)
        {
            PlayerState = playerState;
            ActiveGun = activeGun;
            Runtime = runtime;
        }

        public PlayerLiveGunStateBridge PlayerState { get; }
        public RouteProfileActiveGunSource ActiveGun { get; }
        public InventoryGunLiveSetup Runtime { get; }

        public static PlayerInventoryGunLiveSetupRoot CreateCanonical(
            PlayerSetup playerRuntime,
            PlayerLoadoutLive loadoutRuntime,
            IInventoryGunEffectBatchSink effectSink,
            int simulationTicksPerSecond,
            IGunMappingPolicyResolver mappingPolicyResolver,
            IGunAugmentModifierSetResolver augmentModifierResolver)
        {
            if (loadoutRuntime == null)
            {
                throw new ArgumentNullException(nameof(loadoutRuntime));
            }

            GunItem exact;
            string rejectionCode;
            if (!loadoutRuntime.TryResolveFirstActiveEquippedGun(
                    out exact,
                    out rejectionCode)
                || exact == null)
            {
                throw new InvalidOperationException(
                    "The playable character has no exact active equipped gun: "
                    + rejectionCode);
            }

            PlayerRouteProfilePayload route =
                loadoutRuntime.CurrentRoutePayload;
            int activeSlotIndex = FindExactRouteSlot(
                route,
                exact.InstanceId);
            var playerState = new PlayerLiveGunStateBridge(
                playerRuntime ?? throw new ArgumentNullException(nameof(playerRuntime)));
            var activeGun = new RouteProfileActiveGunSource(
                route,
                activeSlotIndex);
            var executionAdapter = new InventoryBackedGunExecutionBridge(
                new GunEquipmentViewLookup(
                    loadoutRuntime.GunInventory,
                    loadoutRuntime.EquipmentCatalog,
                    loadoutRuntime.Holdings),
                loadoutRuntime.EquipmentCatalog,
                loadoutRuntime.GunCatalog,
                playerState,
                effectSink ?? throw new ArgumentNullException(nameof(effectSink)),
                simulationTicksPerSecond,
                mappingPolicyResolver
                    ?? throw new ArgumentNullException(
                        nameof(mappingPolicyResolver)),
                augmentModifierResolver
                    ?? throw new ArgumentNullException(
                        nameof(augmentModifierResolver)));
            var runtime = new InventoryGunLiveSetup(
                playerState,
                activeGun,
                executionAdapter);
            return new PlayerInventoryGunLiveSetupRoot(
                playerState,
                activeGun,
                runtime);
        }

        public static PlayerInventoryGunLiveSetupRoot Create(
            PlayerSetup playerRuntime,
            PlayerRouteProfilePayload routeProfile,
            IPlayerHoldingsState holdings,
            EquipmentCatalog equipmentCatalog,
            GunCatalog gunCatalog,
            IInventoryGunEffectBatchSink effectSink,
            int simulationTicksPerSecond,
            IGunMappingPolicyResolver mappingPolicyResolver,
            IGunAugmentModifierSetResolver augmentModifierResolver,
            int initialSlotIndex = 0)
        {
            var playerState = new PlayerLiveGunStateBridge(
                playerRuntime ?? throw new ArgumentNullException(nameof(playerRuntime)));
            var activeGun = new RouteProfileActiveGunSource(
                routeProfile ?? throw new ArgumentNullException(nameof(routeProfile)),
                initialSlotIndex);
            var executionAdapter = new InventoryBackedGunExecutionBridge(
                holdings ?? throw new ArgumentNullException(nameof(holdings)),
                equipmentCatalog ?? throw new ArgumentNullException(nameof(equipmentCatalog)),
                gunCatalog ?? throw new ArgumentNullException(nameof(gunCatalog)),
                playerState,
                effectSink ?? throw new ArgumentNullException(nameof(effectSink)),
                simulationTicksPerSecond,
                mappingPolicyResolver
                    ?? throw new ArgumentNullException(nameof(mappingPolicyResolver)),
                augmentModifierResolver
                    ?? throw new ArgumentNullException(nameof(augmentModifierResolver)));
            var runtime = new InventoryGunLiveSetup(
                playerState,
                activeGun,
                executionAdapter);
            return new PlayerInventoryGunLiveSetupRoot(
                playerState,
                activeGun,
                runtime);
        }

        public static PlayerInventoryGunLiveSetupRoot Create(
            PlayerSetup playerRuntime,
            PlayerRouteProfilePayload routeProfile,
            IPlayerHoldingsState holdings,
            EquipmentCatalog equipmentCatalog,
            GunCatalog gunCatalog,
            IInventoryGunEffectBatchSink effectSink,
            int simulationTicksPerSecond,
            int initialSlotIndex = 0)
        {
            return Create(
                playerRuntime,
                routeProfile,
                holdings,
                equipmentCatalog,
                gunCatalog,
                effectSink,
                simulationTicksPerSecond,
                new GunMappingPolicyRegistry(
                    new GunCatalogBlueprintMappingIntent[0]),
                new GunAugmentResolver(),
                initialSlotIndex);
        }

        public void Dispose()
        {
            Runtime.Dispose();
        }

        private static int FindExactRouteSlot(
            PlayerRouteProfilePayload route,
            ShooterMover.Domain.Common.StableId instanceId)
        {
            for (int index = 0; index < route.GunSlots.Count; index++)
            {
                if (route.GunSlots[index].EquipmentInstanceStableId
                    == instanceId)
                {
                    return index;
                }
            }
            throw new InvalidOperationException(
                "The exact canonical gameplay instance is absent from the route projection: "
                + instanceId);
        }
    }
}
