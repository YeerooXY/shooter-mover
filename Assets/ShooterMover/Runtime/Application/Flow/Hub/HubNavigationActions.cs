using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Contracts.Flow.Session;

namespace ShooterMover.Application.Flow.Hub
{
    public enum HubNavigationStatus
    {
        Navigated = 1,
        NoChange = 2,
        InvalidTransition = 3,
        BackAtRoot = 4,
    }

    public sealed class HubRouteRecord
    {
        public HubRouteRecord(
            long sequence,
            HubRoute previousRoute,
            HubRoute currentRoute,
            string payloadFingerprint)
        {
            if (sequence <= 0L) throw new ArgumentOutOfRangeException(nameof(sequence));
            Sequence = sequence;
            PreviousRoute = previousRoute;
            CurrentRoute = currentRoute;
            PayloadFingerprint = payloadFingerprint
                ?? throw new ArgumentNullException(nameof(payloadFingerprint));
        }

        public long Sequence { get; }
        public HubRoute PreviousRoute { get; }
        public HubRoute CurrentRoute { get; }
        public string PayloadFingerprint { get; }
    }

    public sealed class HubNavigationSnapshot
    {
        private readonly ReadOnlyCollection<HubRouteRecord> routeHistory;

        internal HubNavigationSnapshot(
            HubRoute currentRoute,
            PlayerRouteProfilePayload payload,
            IEnumerable<HubRouteRecord> routeHistory)
        {
            CurrentRoute = currentRoute;
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            this.routeHistory = new ReadOnlyCollection<HubRouteRecord>(
                new List<HubRouteRecord>(
                    routeHistory ?? throw new ArgumentNullException(nameof(routeHistory))));
        }

        public HubRoute CurrentRoute { get; }
        public PlayerRouteProfilePayload Payload { get; }
        public IReadOnlyList<HubRouteRecord> RouteHistory { get { return routeHistory; } }
    }

    public sealed class HubNavigationResult
    {
        internal HubNavigationResult(
            HubNavigationStatus status,
            string rejectionCode,
            HubRoute previousRoute,
            HubNavigationSnapshot snapshot)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            PreviousRoute = previousRoute;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public HubNavigationStatus Status { get; }
        public string RejectionCode { get; }
        public HubRoute PreviousRoute { get; }
        public HubNavigationSnapshot Snapshot { get; }
        public bool Changed { get { return Status == HubNavigationStatus.Navigated; } }
    }

    public interface IHubRouteDestinationBridge
    {
        void Present(HubRoute route, PlayerRouteProfilePayload payload);
    }

    /// <summary>
    /// Unity routing uses this transactional port so an accepted route change and its
    /// scene request cannot drift apart. The implementation must delegate route state
    /// to the exposed HubNavigationActions.
    /// </summary>
    public interface IHubRouteTransactionPort
    {
        HubNavigationActions Navigation { get; }

        bool IsTransitionPending { get; }

        bool TryNavigateTo(HubRoute route);

        bool TryNavigateBack();
    }

    /// <summary>
    /// Sole engine-independent route/history owner for Main Menu, Character Selection,
    /// Hub and Hub destinations. It always retains the exact immutable route payload.
    /// </summary>
    public sealed class HubNavigationActions
    {
        private readonly PlayerRouteProfilePayload payload;
        private readonly List<HubRoute> backStack = new List<HubRoute>();
        private readonly List<HubRouteRecord> routeHistory =
            new List<HubRouteRecord>();
        private long sequence;

        public HubNavigationActions(PlayerRouteProfilePayload payload)
        {
            this.payload = payload ?? throw new ArgumentNullException(nameof(payload));
            if (!payload.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "The route payload fingerprint is inconsistent.",
                    nameof(payload));
            }

            CurrentRoute = HubRoute.MainMenu;
        }

        public HubRoute CurrentRoute { get; private set; }
        public PlayerRouteProfilePayload Payload { get { return payload; } }

        public HubNavigationSnapshot ExportSnapshot()
        {
            return new HubNavigationSnapshot(CurrentRoute, payload, routeHistory);
        }

        public bool CanNavigateTo(HubRoute targetRoute)
        {
            return Enum.IsDefined(typeof(HubRoute), targetRoute)
                && targetRoute != CurrentRoute
                && IsForwardTransitionAllowed(CurrentRoute, targetRoute);
        }

        public bool TryPeekBackRoute(out HubRoute targetRoute)
        {
            if (backStack.Count > 0)
            {
                targetRoute = backStack[backStack.Count - 1];
                return true;
            }

            if (CurrentRoute != HubRoute.MainMenu)
            {
                targetRoute = HubRoute.MainMenu;
                return true;
            }

            targetRoute = HubRoute.MainMenu;
            return false;
        }

        public HubNavigationResult TryNavigateTo(HubRoute targetRoute)
        {
            if (!Enum.IsDefined(typeof(HubRoute), targetRoute))
            {
                return Result(
                    HubNavigationStatus.InvalidTransition,
                    "hub-route-undefined",
                    CurrentRoute);
            }

            HubRoute previous = CurrentRoute;
            if (targetRoute == CurrentRoute)
            {
                return Result(
                    HubNavigationStatus.NoChange,
                    "hub-route-already-current",
                    previous);
            }

            if (!IsForwardTransitionAllowed(CurrentRoute, targetRoute))
            {
                return Result(
                    HubNavigationStatus.InvalidTransition,
                    "hub-route-transition-invalid",
                    previous);
            }

            if (targetRoute == HubRoute.MainMenu) backStack.Clear();
            else backStack.Add(CurrentRoute);

            ApplyTransition(previous, targetRoute);
            return Result(HubNavigationStatus.Navigated, string.Empty, previous);
        }

        public HubNavigationResult NavigateBack()
        {
            HubRoute previous = CurrentRoute;
            if (backStack.Count == 0)
            {
                if (CurrentRoute == HubRoute.MainMenu)
                {
                    return Result(
                        HubNavigationStatus.BackAtRoot,
                        "hub-route-back-at-main-menu",
                        previous);
                }

                ApplyTransition(previous, HubRoute.MainMenu);
                return Result(HubNavigationStatus.Navigated, string.Empty, previous);
            }

            int lastIndex = backStack.Count - 1;
            HubRoute target = backStack[lastIndex];
            backStack.RemoveAt(lastIndex);
            ApplyTransition(previous, target);
            return Result(HubNavigationStatus.Navigated, string.Empty, previous);
        }

        public static bool IsHubDestination(HubRoute route)
        {
            return route == HubRoute.Inventory
                || route == HubRoute.Skills
                || route == HubRoute.Shop
                || route == HubRoute.Crafting
                || route == HubRoute.Play;
        }

        private static bool IsForwardTransitionAllowed(
            HubRoute currentRoute,
            HubRoute targetRoute)
        {
            if (targetRoute == HubRoute.MainMenu)
            {
                return currentRoute != HubRoute.MainMenu;
            }

            switch (currentRoute)
            {
                case HubRoute.MainMenu:
                    return targetRoute == HubRoute.CharacterSelect;
                case HubRoute.CharacterSelect:
                    return targetRoute == HubRoute.InventoryLoadoutHub;
                case HubRoute.InventoryLoadoutHub:
                    return IsHubDestination(targetRoute);
                case HubRoute.Inventory:
                case HubRoute.Skills:
                case HubRoute.Shop:
                case HubRoute.Crafting:
                case HubRoute.Play:
                    return targetRoute == HubRoute.InventoryLoadoutHub;
                default:
                    return false;
            }
        }

        private void ApplyTransition(
            HubRoute previousRoute,
            HubRoute targetRoute)
        {
            sequence = checked(sequence + 1L);
            CurrentRoute = targetRoute;
            routeHistory.Add(new HubRouteRecord(
                sequence,
                previousRoute,
                targetRoute,
                payload.Fingerprint));
        }

        private HubNavigationResult Result(
            HubNavigationStatus status,
            string rejectionCode,
            HubRoute previousRoute)
        {
            return new HubNavigationResult(
                status,
                rejectionCode,
                previousRoute,
                ExportSnapshot());
        }
    }
}
