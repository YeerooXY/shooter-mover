using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Contracts.Flow.Session;

namespace ShooterMover.Application.Flow.Hub
{
    public enum HubNavigationStatusV1
    {
        Navigated = 1,
        NoChange = 2,
        InvalidTransition = 3,
        BackAtRoot = 4,
    }

    public sealed class HubRouteRecordV1
    {
        public HubRouteRecordV1(
            long sequence,
            HubRouteV1 previousRoute,
            HubRouteV1 currentRoute,
            string payloadFingerprint)
        {
            if (sequence <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            Sequence = sequence;
            PreviousRoute = previousRoute;
            CurrentRoute = currentRoute;
            PayloadFingerprint = payloadFingerprint
                ?? throw new ArgumentNullException(nameof(payloadFingerprint));
        }

        public long Sequence { get; }

        public HubRouteV1 PreviousRoute { get; }

        public HubRouteV1 CurrentRoute { get; }

        public string PayloadFingerprint { get; }
    }

    public sealed class HubNavigationSnapshotV1
    {
        private readonly ReadOnlyCollection<HubRouteRecordV1> routeHistory;

        internal HubNavigationSnapshotV1(
            HubRouteV1 currentRoute,
            PlayerRouteProfilePayloadV1 payload,
            IEnumerable<HubRouteRecordV1> routeHistory)
        {
            CurrentRoute = currentRoute;
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
            this.routeHistory = new ReadOnlyCollection<HubRouteRecordV1>(
                new List<HubRouteRecordV1>(
                    routeHistory ?? throw new ArgumentNullException(nameof(routeHistory))));
        }

        public HubRouteV1 CurrentRoute { get; }

        public PlayerRouteProfilePayloadV1 Payload { get; }

        public IReadOnlyList<HubRouteRecordV1> RouteHistory
        {
            get { return routeHistory; }
        }
    }

    public sealed class HubNavigationResultV1
    {
        internal HubNavigationResultV1(
            HubNavigationStatusV1 status,
            string rejectionCode,
            HubRouteV1 previousRoute,
            HubNavigationSnapshotV1 snapshot)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            PreviousRoute = previousRoute;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public HubNavigationStatusV1 Status { get; }

        public string RejectionCode { get; }

        public HubRouteV1 PreviousRoute { get; }

        public HubNavigationSnapshotV1 Snapshot { get; }

        public bool Changed
        {
            get { return Status == HubNavigationStatusV1.Navigated; }
        }
    }

    /// <summary>
    /// Presentation boundary for destination screens. Implementations receive the
    /// exact immutable payload owned by this route session and must not replace it.
    /// </summary>
    public interface IHubRouteDestinationAdapterV1
    {
        void Present(HubRouteV1 route, PlayerRouteProfilePayloadV1 payload);
    }

    /// <summary>
    /// Engine-independent route/history owner. It changes only navigation state and
    /// always retains the exact same immutable PlayerRouteProfilePayloadV1 instance.
    /// </summary>
    public sealed class HubNavigationServiceV1
    {
        private readonly PlayerRouteProfilePayloadV1 payload;
        private readonly List<HubRouteV1> backStack = new List<HubRouteV1>();
        private readonly List<HubRouteRecordV1> routeHistory =
            new List<HubRouteRecordV1>();

        private long sequence;

        public HubNavigationServiceV1(PlayerRouteProfilePayloadV1 payload)
        {
            this.payload = payload ?? throw new ArgumentNullException(nameof(payload));
            if (!payload.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "The route payload fingerprint is inconsistent.",
                    nameof(payload));
            }

            CurrentRoute = HubRouteV1.MainMenu;
        }

        public HubRouteV1 CurrentRoute { get; private set; }

        public PlayerRouteProfilePayloadV1 Payload
        {
            get { return payload; }
        }

        public HubNavigationSnapshotV1 ExportSnapshot()
        {
            return new HubNavigationSnapshotV1(
                CurrentRoute,
                payload,
                routeHistory);
        }

        public HubNavigationResultV1 TryNavigateTo(HubRouteV1 targetRoute)
        {
            if (!Enum.IsDefined(typeof(HubRouteV1), targetRoute))
            {
                return Result(
                    HubNavigationStatusV1.InvalidTransition,
                    "hub-route-undefined",
                    CurrentRoute);
            }

            HubRouteV1 previous = CurrentRoute;
            if (targetRoute == CurrentRoute)
            {
                return Result(
                    HubNavigationStatusV1.NoChange,
                    "hub-route-already-current",
                    previous);
            }

            if (!IsForwardTransitionAllowed(CurrentRoute, targetRoute))
            {
                return Result(
                    HubNavigationStatusV1.InvalidTransition,
                    "hub-route-transition-invalid",
                    previous);
            }

            if (targetRoute == HubRouteV1.MainMenu)
            {
                backStack.Clear();
            }
            else
            {
                backStack.Add(CurrentRoute);
            }

            ApplyTransition(previous, targetRoute);
            return Result(
                HubNavigationStatusV1.Navigated,
                string.Empty,
                previous);
        }

        public HubNavigationResultV1 NavigateBack()
        {
            HubRouteV1 previous = CurrentRoute;
            if (backStack.Count == 0)
            {
                if (CurrentRoute == HubRouteV1.MainMenu)
                {
                    return Result(
                        HubNavigationStatusV1.BackAtRoot,
                        "hub-route-back-at-main-menu",
                        previous);
                }

                ApplyTransition(previous, HubRouteV1.MainMenu);
                return Result(
                    HubNavigationStatusV1.Navigated,
                    string.Empty,
                    previous);
            }

            int lastIndex = backStack.Count - 1;
            HubRouteV1 target = backStack[lastIndex];
            backStack.RemoveAt(lastIndex);
            ApplyTransition(previous, target);
            return Result(
                HubNavigationStatusV1.Navigated,
                string.Empty,
                previous);
        }

        public static bool IsHubDestination(HubRouteV1 route)
        {
            return route == HubRouteV1.Inventory
                || route == HubRouteV1.Skills
                || route == HubRouteV1.Shop
                || route == HubRouteV1.Crafting
                || route == HubRouteV1.Play;
        }

        private static bool IsForwardTransitionAllowed(
            HubRouteV1 currentRoute,
            HubRouteV1 targetRoute)
        {
            if (targetRoute == HubRouteV1.MainMenu)
            {
                return currentRoute != HubRouteV1.MainMenu;
            }

            switch (currentRoute)
            {
                case HubRouteV1.MainMenu:
                    return targetRoute == HubRouteV1.CharacterSelect;
                case HubRouteV1.CharacterSelect:
                    return targetRoute == HubRouteV1.InventoryLoadoutHub;
                case HubRouteV1.InventoryLoadoutHub:
                    return IsHubDestination(targetRoute);
                case HubRouteV1.Inventory:
                case HubRouteV1.Skills:
                case HubRouteV1.Shop:
                case HubRouteV1.Crafting:
                case HubRouteV1.Play:
                    return targetRoute == HubRouteV1.InventoryLoadoutHub;
                default:
                    return false;
            }
        }

        private void ApplyTransition(
            HubRouteV1 previousRoute,
            HubRouteV1 targetRoute)
        {
            sequence = checked(sequence + 1L);
            CurrentRoute = targetRoute;
            routeHistory.Add(new HubRouteRecordV1(
                sequence,
                previousRoute,
                targetRoute,
                payload.Fingerprint));
        }

        private HubNavigationResultV1 Result(
            HubNavigationStatusV1 status,
            string rejectionCode,
            HubRouteV1 previousRoute)
        {
            return new HubNavigationResultV1(
                status,
                rejectionCode,
                previousRoute,
                ExportSnapshot());
        }
    }
}
