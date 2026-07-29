using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Guns.Execution;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns.Catalog;
using ShooterMover.Domain.Guns.Execution;
using ShooterMover.UnityAdapters.Guns.Live;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Guns.Live
{
    public sealed partial class InventoryGunLivePlayModeTests
    {
        private sealed class Fixture : IDisposable
        {
            private readonly GameObject emitterObject;

            public Fixture(
                GameObject gameObject,
                GunEffectEmitter emitter,
                InventoryGunLiveSetup runtime)
            {
                emitterObject = gameObject;
                Emitter = emitter;
                Runtime = runtime;
            }

            public GunEffectEmitter Emitter { get; }
            public InventoryGunLiveSetup Runtime { get; }

            public void Dispose()
            {
                DamageZone[] pools =
                    UnityEngine.Object.FindObjectsByType<DamageZone>(
                        FindObjectsSortMode.None);
                for (int index = 0; index < pools.Length; index++)
                {
                    if (pools[index] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(pools[index].gameObject);
                    }
                }

                if (emitterObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(emitterObject);
                }
            }
        }

        private sealed class InMemoryEquipmentLookup : IPlayerEquipmentInstanceLookup
        {
            private readonly Dictionary<StableId, EquipmentInstance> equipment =
                new Dictionary<StableId, EquipmentInstance>();

            public InMemoryEquipmentLookup(IEnumerable<EquipmentInstance> values)
            {
                foreach (EquipmentInstance value in values)
                {
                    equipment[value.InstanceId] = value;
                }
            }

            public bool TryResolve(
                EquipmentInstanceId equipmentInstanceId,
                out EquipmentInstance equipmentInstance)
            {
                if (equipmentInstanceId == null)
                {
                    equipmentInstance = null;
                    return false;
                }

                return equipment.TryGetValue(
                    equipmentInstanceId.Value,
                    out equipmentInstance);
            }
        }

        private sealed class FixedActorSource :
            IInventoryGunActorStateSource,
            IGunActorOwnershipResolver
        {
            public bool TryResolveActorState(
                out GunActorInstanceId actorId,
                out LifecycleGeneration lifecycleGeneration)
            {
                actorId = new GunActorInstanceId(ActorId);
                lifecycleGeneration = new LifecycleGeneration(0L);
                return true;
            }

            public bool TryResolveParticipant(
                GunActorInstanceId actorId,
                LifecycleGeneration lifecycleGeneration,
                out RunParticipantId participantId)
            {
                participantId = actorId != null
                    && actorId.Value == ActorId
                    && lifecycleGeneration != null
                    && lifecycleGeneration.Value == 0L
                    ? new RunParticipantId(ParticipantId)
                    : null;
                return participantId != null;
            }
        }
    }
}
