using System;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;
using ShooterMover.UnityAdapters.Weapons.Live;

namespace ShooterMover.UnityAdapters.Players
{
    public sealed class PlayerRuntimeWeaponStateAdapter :
        IInventoryWeaponActorStateSource,
        IWeaponActorOwnershipResolver
    {
        private readonly PlayerRuntimeComposition playerRuntime;

        public PlayerRuntimeWeaponStateAdapter(PlayerRuntimeComposition runtime)
        {
            playerRuntime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public bool TryResolveActorState(
            out WeaponActorInstanceId actorId,
            out LifecycleGeneration lifecycleGeneration)
        {
            actorId = null;
            lifecycleGeneration = null;
            if (playerRuntime.IsDisposed)
            {
                return false;
            }

            PlayerRuntimeSnapshot snapshot;
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

            actorId = new WeaponActorInstanceId(snapshot.Player.ActorInstanceId);
            lifecycleGeneration = new LifecycleGeneration(
                snapshot.Player.LifecycleGeneration);
            return true;
        }

        public bool TryResolveParticipant(
            WeaponActorInstanceId actorId,
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

            PlayerRuntimeSnapshot snapshot;
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
    /// Production composition root for inventory-backed player weapons. The canonical overload
    /// resolves the exact instance in the first active equipped class mount before constructing the
    /// retained scheduler adapter. No scene-local weapon fallback is created.
    /// </summary>
    public sealed class PlayerInventoryWeaponRuntimeCompositionRoot : IDisposable
    {
        private PlayerInventoryWeaponRuntimeCompositionRoot(
            PlayerRuntimeWeaponStateAdapter playerState,
            RouteProfileActiveWeaponSource activeWeapon,
            InventoryWeaponRuntimeComposition runtime)
        {
            PlayerState = playerState;
            ActiveWeapon = activeWeapon;
            Runtime = runtime;
        }

        public PlayerRuntimeWeaponStateAdapter PlayerState { get; }
        public RouteProfileActiveWeaponSource ActiveWeapon { get; }
        public InventoryWeaponRuntimeComposition Runtime { get; }

        public static PlayerInventoryWeaponRuntimeCompositionRoot CreateCanonical(
            PlayerRuntimeComposition playerRuntime,
            ProductionPlayerLoadoutRuntimeV1 loadoutRuntime,
            IInventoryWeaponEffectBatchSink effectSink,
            int simulationTicksPerSecond,
            IWeaponBlueprintMappingPolicyResolver mappingPolicyResolver,
            IWeaponAugmentModifierSetResolver augmentModifierResolver)
        {
            if (loadoutRuntime == null)
            {
                throw new ArgumentNullException(nameof(loadoutRuntime));
            }

            WeaponEquipmentInstance exact;
            string rejectionCode;
            if (!loadoutRuntime.TryResolveFirstActiveEquippedWeapon(
                    out exact,
                    out rejectionCode)
                || exact == null)
            {
                throw new InvalidOperationException(
                    "The playable character has no exact active equipped weapon: "
                    + rejectionCode);
            }

            PlayerRouteProfilePayloadV1 route =
                loadoutRuntime.CurrentRoutePayload;
            int activeSlotIndex = FindExactRouteSlot(
                route,
                exact.InstanceId);
            var playerState = new PlayerRuntimeWeaponStateAdapter(
                playerRuntime ?? throw new ArgumentNullException(nameof(playerRuntime)));
            var activeWeapon = new RouteProfileActiveWeaponSource(
                route,
                activeSlotIndex);
            var executionAdapter = new InventoryBackedWeaponExecutionAdapter(
                new CanonicalWeaponEquipmentProjectionLookupV2(
                    loadoutRuntime.WeaponHoldings,
                    loadoutRuntime.EquipmentCatalog,
                    loadoutRuntime.Holdings),
                loadoutRuntime.EquipmentCatalog,
                loadoutRuntime.WeaponCatalog,
                playerState,
                effectSink ?? throw new ArgumentNullException(nameof(effectSink)),
                simulationTicksPerSecond,
                mappingPolicyResolver
                    ?? throw new ArgumentNullException(
                        nameof(mappingPolicyResolver)),
                augmentModifierResolver
                    ?? throw new ArgumentNullException(
                        nameof(augmentModifierResolver)));
            var runtime = new InventoryWeaponRuntimeComposition(
                playerState,
                activeWeapon,
                executionAdapter);
            return new PlayerInventoryWeaponRuntimeCompositionRoot(
                playerState,
                activeWeapon,
                runtime);
        }

        public static PlayerInventoryWeaponRuntimeCompositionRoot Create(
            PlayerRuntimeComposition playerRuntime,
            PlayerRouteProfilePayloadV1 routeProfile,
            IPlayerHoldingsAuthorityV1 holdings,
            EquipmentCatalog equipmentCatalog,
            WeaponCatalog weaponCatalog,
            IInventoryWeaponEffectBatchSink effectSink,
            int simulationTicksPerSecond,
            IWeaponBlueprintMappingPolicyResolver mappingPolicyResolver,
            IWeaponAugmentModifierSetResolver augmentModifierResolver,
            int initialSlotIndex = 0)
        {
            var playerState = new PlayerRuntimeWeaponStateAdapter(
                playerRuntime ?? throw new ArgumentNullException(nameof(playerRuntime)));
            var activeWeapon = new RouteProfileActiveWeaponSource(
                routeProfile ?? throw new ArgumentNullException(nameof(routeProfile)),
                initialSlotIndex);
            var executionAdapter = new InventoryBackedWeaponExecutionAdapter(
                holdings ?? throw new ArgumentNullException(nameof(holdings)),
                equipmentCatalog ?? throw new ArgumentNullException(nameof(equipmentCatalog)),
                weaponCatalog ?? throw new ArgumentNullException(nameof(weaponCatalog)),
                playerState,
                effectSink ?? throw new ArgumentNullException(nameof(effectSink)),
                simulationTicksPerSecond,
                mappingPolicyResolver
                    ?? throw new ArgumentNullException(nameof(mappingPolicyResolver)),
                augmentModifierResolver
                    ?? throw new ArgumentNullException(nameof(augmentModifierResolver)));
            var runtime = new InventoryWeaponRuntimeComposition(
                playerState,
                activeWeapon,
                executionAdapter);
            return new PlayerInventoryWeaponRuntimeCompositionRoot(
                playerState,
                activeWeapon,
                runtime);
        }

        public static PlayerInventoryWeaponRuntimeCompositionRoot Create(
            PlayerRuntimeComposition playerRuntime,
            PlayerRouteProfilePayloadV1 routeProfile,
            IPlayerHoldingsAuthorityV1 holdings,
            EquipmentCatalog equipmentCatalog,
            WeaponCatalog weaponCatalog,
            IInventoryWeaponEffectBatchSink effectSink,
            int simulationTicksPerSecond,
            int initialSlotIndex = 0)
        {
            return Create(
                playerRuntime,
                routeProfile,
                holdings,
                equipmentCatalog,
                weaponCatalog,
                effectSink,
                simulationTicksPerSecond,
                new WeaponBlueprintMappingPolicyRegistry(
                    new WeaponCatalogBlueprintMappingIntent[0]),
                new UnaugmentedWeaponModifierSetResolver(),
                initialSlotIndex);
        }

        public void Dispose()
        {
            Runtime.Dispose();
        }

        private static int FindExactRouteSlot(
            PlayerRouteProfilePayloadV1 route,
            ShooterMover.Domain.Common.StableId instanceId)
        {
            for (int index = 0; index < route.WeaponSlots.Count; index++)
            {
                if (route.WeaponSlots[index].EquipmentInstanceStableId
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
