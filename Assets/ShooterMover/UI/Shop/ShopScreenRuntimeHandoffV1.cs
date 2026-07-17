using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Shops.Presentation;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Shops;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShooterMover.UI.Shop
{
    public sealed class RecordingShopScreenRouteAdapterV1 : IShopScreenRouteAdapterV1
    {
        public ShopScreenRouteV1 LastRoute { get; private set; }

        public PlayerRouteProfilePayloadV1 LastPayload { get; private set; }

        public int PresentCount { get; private set; }

        public void Present(
            ShopScreenRouteV1 route,
            PlayerRouteProfilePayloadV1 payload)
        {
            if (route != ShopScreenRouteV1.Hub)
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
    public static class ShopScreenRuntimeHandoffV1
    {
        private static readonly object Sync = new object();
        private static ShopScreenSessionV1 pendingSession;
        private static IShopScreenRouteAdapterV1 pendingRouteAdapter;

        public static void Prepare(
            ShopScreenSessionV1 session,
            IShopScreenRouteAdapterV1 routeAdapter)
        {
            lock (Sync)
            {
                pendingSession = session
                    ?? throw new ArgumentNullException(nameof(session));
                pendingRouteAdapter = routeAdapter
                    ?? throw new ArgumentNullException(nameof(routeAdapter));
            }
        }

        public static bool TryConsume(
            out ShopScreenSessionV1 session,
            out IShopScreenRouteAdapterV1 routeAdapter)
        {
            lock (Sync)
            {
                session = pendingSession;
                routeAdapter = pendingRouteAdapter;
                pendingSession = null;
                pendingRouteAdapter = null;
                return session != null && routeAdapter != null;
            }
        }

        public static void Clear()
        {
            lock (Sync)
            {
                pendingSession = null;
                pendingRouteAdapter = null;
            }
        }
    }

}
