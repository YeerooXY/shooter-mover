using System;
using System.Collections.Generic;
using ShooterMover.Application.Missions.Rooms;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Enemies;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEngine.SceneManagement;

namespace ShooterMover.UI.ProductionFlow
{
    /// <summary>
    /// Explicit integration-owned mapping from authored enemy damage channels to canonical
    /// player combat channels. Unknown or missing channels fail closed.
    /// </summary>
    public static class EnemyPlayerDamageChannelMap
    {
        private static readonly StableId KineticDamageChannelStableId =
            StableId.Parse("damage.kinetic");
        private static readonly StableId ThermalDamageChannelStableId =
            StableId.Parse("damage.thermal");

        public static bool TryMap(
            StableId damageChannelStableId,
            out CombatChannel channel)
        {
            channel = default(CombatChannel);
            if (damageChannelStableId == KineticDamageChannelStableId)
            {
                channel = CombatChannel.Kinetic;
                return true;
            }
            if (damageChannelStableId == ThermalDamageChannelStableId)
            {
                channel = CombatChannel.Thermal;
                return true;
            }
            return false;
        }
    }

    public enum EnemyPublisherResolutionStatus
    {
        Pending = 1,
        Ready = 2,
        Rejected = 3,
    }

    /// <summary>
    /// Reconciles live enemy-hit publishers against the authoritative current-room enemy
    /// projection. Props and retired occupants never contribute to the expected count.
    /// </summary>
    public static class EnemyPublisherReconciliation
    {
        public static EnemyPublisherResolutionStatus Classify(
            int authoritativeActiveEnemyCount,
            int readyPublisherCount,
            bool hasNonTerminalUnboundPublisher,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (authoritativeActiveEnemyCount < 0 || readyPublisherCount < 0)
            {
                diagnostic = "enemy-player-damage-publisher-count-invalid";
                return EnemyPublisherResolutionStatus.Rejected;
            }
            if (hasNonTerminalUnboundPublisher)
            {
                diagnostic = "enemy-player-damage-publisher-binding-pending";
                return EnemyPublisherResolutionStatus.Pending;
            }
            if (readyPublisherCount < authoritativeActiveEnemyCount)
            {
                diagnostic = "enemy-player-damage-publisher-pending:"
                    + readyPublisherCount
                    + "/"
                    + authoritativeActiveEnemyCount;
                return EnemyPublisherResolutionStatus.Pending;
            }
            if (readyPublisherCount > authoritativeActiveEnemyCount)
            {
                diagnostic = "enemy-player-damage-publisher-duplicated:"
                    + readyPublisherCount
                    + "/"
                    + authoritativeActiveEnemyCount;
                return EnemyPublisherResolutionStatus.Rejected;
            }

            return EnemyPublisherResolutionStatus.Ready;
        }

        public static int CountAuthoritativeActiveEnemies(
            RoomLiveSetup2D roomRuntime)
        {
            if (roomRuntime == null
                || !roomRuntime.IsBuilt
                || roomRuntime.Definition == null
                || roomRuntime.CurrentProjection == null)
            {
                throw new InvalidOperationException(
                    "enemy-player-damage-room-projection-pending");
            }

            RoomLiveView runtimeProjection =
                roomRuntime.CurrentProjection;
            RoomLiveRoomView roomProjection =
                runtimeProjection.GetRoom(runtimeProjection.CurrentRoomStableId);
            AuthorableRoomDefinition roomDefinition =
                roomRuntime.Definition.GetRoom(runtimeProjection.CurrentRoomStableId);

            int count = 0;
            for (int index = 0; index < roomProjection.ActiveOccupants.Count; index++)
            {
                RoomPlacedEntityDefinition placement;
                if (roomDefinition.TryGetPlacement(
                        roomProjection.ActiveOccupants[index].EntityStableId,
                        out placement)
                    && placement != null
                    && placement.PlacementKind == RoomLivePlacementKind.Enemy)
                {
                    count++;
                }
            }
            return count;
        }
    }

    /// <summary>
    /// Owns exact EnemyAttack2D subscriptions. Reconciliation replaces the complete set, so
    /// room rebuilds cannot accumulate duplicate hit delivery.
    /// </summary>
    public sealed class EnemyHitSubscriptionSet
    {
        private readonly Dictionary<EnemyAttack2D, Action<EnemyHit>> subscriptions =
            new Dictionary<EnemyAttack2D, Action<EnemyHit>>();

        public int Count { get { return subscriptions.Count; } }

        public bool Contains(EnemyAttack2D publisher)
        {
            return publisher != null && subscriptions.ContainsKey(publisher);
        }

        public void Replace(
            IReadOnlyList<EnemyAttack2D> publishers,
            Action<EnemyAttack2D, EnemyHit> receiver)
        {
            if (publishers == null) throw new ArgumentNullException(nameof(publishers));
            if (receiver == null) throw new ArgumentNullException(nameof(receiver));

            Clear();
            var seen = new HashSet<EnemyAttack2D>();
            try
            {
                for (int index = 0; index < publishers.Count; index++)
                {
                    EnemyAttack2D publisher = publishers[index];
                    if (publisher == null)
                    {
                        throw new InvalidOperationException(
                            "enemy-player-damage-publisher-missing");
                    }
                    if (!seen.Add(publisher))
                    {
                        throw new InvalidOperationException(
                            "enemy-player-damage-publisher-duplicated");
                    }

                    EnemyAttack2D capturedPublisher = publisher;
                    Action<EnemyHit> handler = hit =>
                        receiver(capturedPublisher, hit);
                    publisher.Hit += handler;
                    subscriptions.Add(publisher, handler);
                }
            }
            catch
            {
                Clear();
                throw;
            }
        }

        public bool AllCurrent(Scene scene)
        {
            foreach (KeyValuePair<EnemyAttack2D, Action<EnemyHit>> pair
                in subscriptions)
            {
                EnemyAttack2D publisher = pair.Key;
                if (publisher == null
                    || publisher.gameObject.scene != scene
                    || !publisher.gameObject.activeInHierarchy
                    || !publisher.IsBound
                    || publisher.IsTerminalStopped)
                {
                    return false;
                }
            }
            return true;
        }

        public void Clear()
        {
            foreach (KeyValuePair<EnemyAttack2D, Action<EnemyHit>> pair
                in subscriptions)
            {
                if (pair.Key != null)
                {
                    pair.Key.Hit -= pair.Value;
                }
            }
            subscriptions.Clear();
        }
    }
}
