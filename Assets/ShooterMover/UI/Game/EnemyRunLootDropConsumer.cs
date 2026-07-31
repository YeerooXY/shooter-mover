using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Domain.Common;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.UnityAdapters.Missions.Rooms;
using ShooterMover.UnityAdapters.Rewards.RunLoots;
using UnityEngine;
using EnemyActor = ShooterMover.UnityAdapters.Missions.Rooms.Enemy;

namespace ShooterMover.UI.Game
{
    /// <summary>
    /// Captures one immutable terminal source position before deterministic reward
    /// generation and realizes accepted admissions through the existing pickup bridge.
    /// Exact death-event retries reuse the cached position and never read the Transform.
    /// </summary>
    internal sealed class LootDropper :
        IEnemyDropFactConsumer
    {
        private sealed class TerminalSource
        {
            public TerminalSource(
                StableId deathEventStableId,
                StableId runStableId,
                long runLifecycleGeneration,
                StableId roomStableId,
                StableId entityStableId,
                StableId placementStableId,
                long sourceLifecycleGeneration,
                Vector2 position,
                string fingerprint)
            {
                DeathEventStableId = deathEventStableId;
                RunStableId = runStableId;
                RunLifecycleGeneration = runLifecycleGeneration;
                RoomStableId = roomStableId;
                EntityStableId = entityStableId;
                PlacementStableId = placementStableId;
                SourceLifecycleGeneration = sourceLifecycleGeneration;
                Position = position;
                Fingerprint = fingerprint;
            }

            public StableId DeathEventStableId { get; }
            public StableId RunStableId { get; }
            public long RunLifecycleGeneration { get; }
            public StableId RoomStableId { get; }
            public StableId EntityStableId { get; }
            public StableId PlacementStableId { get; }
            public long SourceLifecycleGeneration { get; }
            public Vector2 Position { get; }
            public string Fingerprint { get; }

            public bool Matches(
                EnemyDeathFact fact,
                RunSessionAggregate run)
            {
                return fact != null
                    && fact.Identity != null
                    && run != null
                    && DeathEventStableId == fact.DeathEventStableId
                    && RunStableId == fact.Identity.RunStableId
                    && RunStableId == run.RunStableId
                    && RunLifecycleGeneration == run.LifecycleGeneration
                    && RoomStableId == fact.Identity.RoomStableId
                    && EntityStableId == fact.Identity.EntityInstanceId
                    && PlacementStableId == fact.Identity.PlacementStableId
                    && SourceLifecycleGeneration == fact.LifecycleGeneration;
            }
        }

        private readonly object gate = new object();
        private readonly RoomEnemies enemies;
        private readonly RunSessionAggregate run;
        private readonly LootBridge pickupBridge;
        private readonly IEnemyDropFactConsumer inner;
        private readonly Dictionary<StableId, TerminalSource> sourcesByDeath =
            new Dictionary<StableId, TerminalSource>();

        public LootDropper(
            RoomEnemies enemies,
            RunSessionAggregate run,
            LootBridge pickupBridge,
            IEnemyDropFactConsumer inner)
        {
            this.enemies = enemies
                ?? throw new ArgumentNullException(nameof(enemies));
            this.run = run ?? throw new ArgumentNullException(nameof(run));
            this.pickupBridge = pickupBridge
                ?? throw new ArgumentNullException(nameof(pickupBridge));
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public void Consume(EnemyDeathFact fact)
        {
            if (fact == null) throw new ArgumentNullException(nameof(fact));
            if (fact.Identity == null
                || fact.DeathEventStableId == null
                || fact.Identity.RunStableId == null
                || fact.Identity.RoomStableId == null
                || fact.Identity.EntityInstanceId == null
                || fact.Identity.PlacementStableId == null)
            {
                throw new InvalidOperationException(
                    "A physical enemy reward requires complete immutable terminal identities.");
            }

            lock (gate)
            {
                TerminalSource source;
                if (!sourcesByDeath.TryGetValue(
                        fact.DeathEventStableId,
                        out source))
                {
                    source = CaptureSource(fact);
                    sourcesByDeath.Add(
                        fact.DeathEventStableId,
                        source);
                }
                else if (!source.Matches(fact, run))
                {
                    throw new InvalidOperationException(
                        "The same enemy death event was retried with conflicting source facts.");
                }

                pickupBridge.RegisterFixedSource(
                    source.RunStableId,
                    source.RunLifecycleGeneration,
                    source.EntityStableId,
                    source.PlacementStableId,
                    source.RoomStableId,
                    source.Position,
                    source.Fingerprint);

                inner.Consume(fact);
                pickupBridge.ProcessPending();
            }
        }

        private TerminalSource CaptureSource(EnemyDeathFact fact)
        {
            if (run.LifecycleState == RunSessionLifecycleState.Ended
                || fact.Identity.RunStableId != run.RunStableId)
            {
                throw new InvalidOperationException(
                    "The enemy terminal fact does not belong to the active Run Session.");
            }

            EnemyActor actor;
            if (!enemies.TryGetEnemyByActor(
                    fact.Identity.EntityInstanceId,
                    out actor)
                || actor == null
                || !actor.IsBound
                || actor.Runtime == null)
            {
                throw new InvalidOperationException(
                    "The exact terminal enemy actor is unavailable for position capture.");
            }
            if (actor.ActorStableId != fact.Identity.EntityInstanceId
                || actor.PlacementStableId != fact.Identity.PlacementStableId
                || actor.LifecycleGeneration != fact.LifecycleGeneration
                || actor.Runtime.RoomStableId != fact.Identity.RoomStableId
                || actor.Runtime.Request.RunStableId != fact.Identity.RunStableId)
            {
                throw new InvalidOperationException(
                    "The bound enemy actor does not match the immutable death fact.");
            }

            Vector2 position = actor.transform.position;
            string fingerprint = RunFingerprint.Hash(
                "enemy-terminal-position-v1|"
                + run.RunStableId + "|"
                + run.LifecycleGeneration.ToString(
                    CultureInfo.InvariantCulture) + "|"
                + fact.Identity.RoomStableId + "|"
                + fact.Identity.EntityInstanceId + "|"
                + fact.Identity.PlacementStableId + "|"
                + fact.LifecycleGeneration.ToString(
                    CultureInfo.InvariantCulture) + "|"
                + fact.DeathEventStableId + "|"
                + position.x.ToString("R", CultureInfo.InvariantCulture) + "|"
                + position.y.ToString("R", CultureInfo.InvariantCulture));

            return new TerminalSource(
                fact.DeathEventStableId,
                run.RunStableId,
                run.LifecycleGeneration,
                fact.Identity.RoomStableId,
                fact.Identity.EntityInstanceId,
                fact.Identity.PlacementStableId,
                fact.LifecycleGeneration,
                position,
                fingerprint);
        }
    }
}
