using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Missions.Results;
using ShooterMover.Application.Progression.Experience;
using ShooterMover.Application.Progression.Experience.EnemyRewards;
using ShooterMover.Application.Weapons.Catalog;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Content.Definitions.Missions.Rooms;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.ContentPackages.Enemies.BlasterTurret;
using ShooterMover.ContentPackages.Enemies.MobileBlasterDroid;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Curves;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;
using ShooterMover.TestSupport.VisibleSlice;
using ShooterMover.UI.ProductionFlow;
using ShooterMover.UnityAdapters.Authoring.LevelDesign;
using ShooterMover.UnityAdapters.Enemies;
using ShooterMover.UnityAdapters.Missions.Rooms;
using ShooterMover.UnityAdapters.Weapons.Live;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    [DisallowMultipleComponent]
    internal sealed class Stage1RoomEnemyAuthorityProjection2D :
        MonoBehaviour,
        IEnemyActor2DAuthority
    {
        public long Generation
        {
            get
            {
                RoomPlacedInstance2D marker =
                    GetComponent<RoomPlacedInstance2D>();
                return marker == null ? 0L : marker.RuntimeLifecycleGeneration;
            }
        }

        public bool TryReadState(out EnemyActorState state)
        {
            state = null;
            RoomPlacedInstance2D marker = GetComponent<RoomPlacedInstance2D>();
            IEnemyActor2DAuthority source;
            EnemyActorState sourceState;
            if (marker == null
                || !marker.IsConfigured
                || !Stage1PlayableLoopCompositionV1.TryResolveProjectedEnemy(
                    marker.InstanceStableId,
                    out source)
                || !source.TryReadState(out sourceState)
                || sourceState == null)
            {
                return false;
            }

            EnemyActorState projected = EnemyActorState.Create(
                marker.InstanceStableId,
                sourceState.RoleId,
                sourceState.MaximumHealth,
                sourceState.WeightClassValue,
                sourceState.ContactPolicy);
            double missing = sourceState.MaximumHealth - sourceState.Health;
            if (missing > 0d)
            {
                projected = EnemyActorStepper.Step(
                    projected,
                    new[]
                    {
                        EnemyActorCommand.Damage(
                            0L,
                            StableId.Create(
                                "combat-event",
                                "room-projection-" + HashProjectionToken(
                                    marker.InstanceStableId.ToString())),
                            sourceState.ActorId,
                            (int)CombatChannel.Kinetic,
                            missing),
                    }).State;
            }

            state = projected;
            return true;
        }

        private static string HashProjectionToken(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(
                    Encoding.UTF8.GetBytes(value ?? string.Empty));
                var builder = new StringBuilder(24);
                for (int index = 0; index < 12; index++)
                {
                    builder.Append(hash[index].ToString(
                        "x2",
                        CultureInfo.InvariantCulture));
                }
                return builder.ToString();
            }
        }

        public EnemyActorStepResult Apply(EnemyActorCommand command)
        {
            RoomPlacedInstance2D marker = GetComponent<RoomPlacedInstance2D>();
            IEnemyActor2DAuthority source;
            if (marker == null
                || !Stage1PlayableLoopCompositionV1.TryResolveProjectedEnemy(
                    marker.InstanceStableId,
                    out source))
            {
                return null;
            }
            return source.Apply(command);
        }

        public bool Reset()
        {
            RoomPlacedInstance2D marker = GetComponent<RoomPlacedInstance2D>();
            IEnemyActor2DAuthority source;
            return marker != null
                && Stage1PlayableLoopCompositionV1.TryResolveProjectedEnemy(
                    marker.InstanceStableId,
                    out source)
                && source.Reset();
        }
    }

    [DisallowMultipleComponent]
    internal sealed class Stage1InventoryWeaponProjectileHit2D : MonoBehaviour
    {
        private Stage1PlayableLoopCompositionV1 owner;
        private InventoryWeaponEffectInstance2D effect;
        private readonly HashSet<Collider2D> hitColliders = new HashSet<Collider2D>();
        private int hitOrdinal;
        private int remainingPierce;
        private bool pierceInitialized;

        public void Configure(
            Stage1PlayableLoopCompositionV1 configuredOwner,
            InventoryWeaponEffectInstance2D configuredEffect)
        {
            owner = configuredOwner;
            effect = configuredEffect;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (owner == null
                || effect == null
                || other == null
                || !hitColliders.Add(other))
            {
                return;
            }

            int configuredPierce;
            if (!owner.TryApplyProjectileHit(
                    other,
                    effect,
                    hitOrdinal++,
                    out configuredPierce))
            {
                return;
            }

            if (!pierceInitialized)
            {
                remainingPierce = configuredPierce;
                pierceInitialized = true;
            }
            if (remainingPierce <= 0)
            {
                Destroy(gameObject);
                return;
            }
            remainingPierce--;
        }
    }

    [DisallowMultipleComponent]
    internal sealed class Stage1InventoryWeaponPoolDamage2D : MonoBehaviour
    {
        private Stage1PlayableLoopCompositionV1 owner;
        private InventoryWeaponPersistentDamageArea2D pool;
        private readonly Dictionary<Collider2D, float> nextTick =
            new Dictionary<Collider2D, float>();
        private int tick;

        public void Configure(
            Stage1PlayableLoopCompositionV1 configuredOwner,
            InventoryWeaponPersistentDamageArea2D configuredPool)
        {
            owner = configuredOwner;
            pool = configuredPool;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (owner == null || pool == null || other == null) return;
            float next;
            if (nextTick.TryGetValue(other, out next) && Time.time < next)
            {
                return;
            }
            nextTick[other] = Time.time + 0.25f;
            owner.ApplyPoolTick(pool, other, tick++);
        }
    }

}
