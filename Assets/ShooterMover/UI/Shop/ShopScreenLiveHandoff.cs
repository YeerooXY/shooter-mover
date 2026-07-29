using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Shops.Presentation;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Shops;
using ShooterMover.Domain.Guns.Catalog;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.UI.Shop
{
    public sealed class RecordingShopScreenRouteBridge : IShopScreenRouteBridge
    {
        public ShopScreenRoute LastRoute { get; private set; }

        public PlayerRouteProfilePayload LastPayload { get; private set; }

        public int PresentCount { get; private set; }

        public void Present(
            ShopScreenRoute route,
            PlayerRouteProfilePayload payload)
        {
            if (route != ShopScreenRoute.Hub)
            {
                throw new ArgumentOutOfRangeException(nameof(route));
            }

            LastRoute = route;
            LastPayload = payload
                ?? throw new ArgumentNullException(nameof(payload));
            PresentCount++;
        }
    }

    /// <summary>
    /// One-shot scene handoff. It carries an already composed authority-backed session,
    /// never a copied inventory, balance, price, sold flag, or reward payload.
    /// </summary>
    public static class ShopScreenLiveHandoff
    {
        private static readonly object Sync = new object();
        private static ShopScreenSession pendingSession;
        private static IShopScreenRouteBridge pendingRouteAdapter;
        private static EquipmentCatalog pendingEquipmentCatalog;
        private static GunCatalog pendingGunCatalog;

        public static void Prepare(
            ShopScreenSession session,
            IShopScreenRouteBridge routeAdapter)
        {
            Prepare(session, routeAdapter, null, null);
        }

        public static void Prepare(
            ShopScreenSession session,
            IShopScreenRouteBridge routeAdapter,
            EquipmentCatalog equipmentCatalog,
            GunCatalog gunCatalog)
        {
            lock (Sync)
            {
                pendingSession = session
                    ?? throw new ArgumentNullException(nameof(session));
                pendingRouteAdapter = routeAdapter
                    ?? throw new ArgumentNullException(nameof(routeAdapter));
                pendingEquipmentCatalog = equipmentCatalog;
                pendingGunCatalog = gunCatalog;
            }
        }

        public static bool TryConsume(
            out ShopScreenSession session,
            out IShopScreenRouteBridge routeAdapter)
        {
            EquipmentCatalog equipmentCatalog;
            GunCatalog gunCatalog;
            return TryConsume(
                out session,
                out routeAdapter,
                out equipmentCatalog,
                out gunCatalog);
        }

        public static bool TryConsume(
            out ShopScreenSession session,
            out IShopScreenRouteBridge routeAdapter,
            out EquipmentCatalog equipmentCatalog,
            out GunCatalog gunCatalog)
        {
            lock (Sync)
            {
                session = pendingSession;
                routeAdapter = pendingRouteAdapter;
                equipmentCatalog = pendingEquipmentCatalog;
                gunCatalog = pendingGunCatalog;
                pendingSession = null;
                pendingRouteAdapter = null;
                pendingEquipmentCatalog = null;
                pendingGunCatalog = null;
                return session != null && routeAdapter != null;
            }
        }

        public static void Clear()
        {
            lock (Sync)
            {
                pendingSession = null;
                pendingRouteAdapter = null;
                pendingEquipmentCatalog = null;
                pendingGunCatalog = null;
            }
        }
    }

}
