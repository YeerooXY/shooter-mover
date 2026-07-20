using System;
using System.Collections.Generic;
using ShooterMover.Application.Flow.Production;
using ShooterMover.Application.Missions.Results;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Weapons.Execution;
using ShooterMover.UI.ProductionFlow;
using ShooterMover.UnityAdapters.Weapons.Live;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    public sealed partial class Stage1PlayableLoopCompositionV1
    {
        internal bool IsHubLoadoutIntegrationReady
        {
            get
            {
                return initialized
                    && controller != null
                    && effectEmitter != null
                    && holdings != null
                    && weapons != null;
            }
        }

        internal InventoryWeaponEffectEmitter2D HubWeaponEffectEmitter
        {
            get { return effectEmitter; }
        }

        internal bool TryAdoptHubLoadout()
        {
            if (controller == null || effectEmitter == null)
            {
                diagnostic =
                    "The Level 1 weapon composition boundary is unavailable.";
                return false;
            }

            ProductionPlayerLoadoutRuntimeV1 runtime;
            ProductionFlowProfileRecordV1 currentProfile;
            if (!ProductionHubLoadoutCompositionV1.TryResolveCurrent(
                out runtime,
                out currentProfile))
            {
                diagnostic =
                    "The Hub loadout composition is not available.";
                return false;
            }

            PlayerRouteProfilePayloadV1 missionPayload =
                ProductionWeaponMountPolicyV1.NormalizeRoutePayload(
                    currentProfile.Payload);
            profile = new ProductionFlowProfileRecordV1(
                currentProfile.DisplayName,
                missionPayload);
            holdings = runtime.Holdings;
            equipmentCatalog = runtime.EquipmentCatalog;
            weaponCatalog = runtime.WeaponCatalog;

            ProductionWeaponMountSetV1 mountSet =
                ProductionWeaponMountPolicyV1.BuildMountSet(
                    missionPayload);
            var enabledMounts =
                new List<InventoryWeaponMountedRuntimeV1>(
                    mountSet.EnabledBindings.Count);
            for (int index = 0;
                index < mountSet.EnabledBindings.Count;
                index++)
            {
                ProductionWeaponMountBindingV1 binding =
                    mountSet.EnabledBindings[index];
                ProductionWeaponMountPositionV1 position =
                    ProductionWeaponMountPolicyV1.FindPosition(
                        mountSet.Layout,
                        binding.MountStableId);
                if (position == null)
                {
                    diagnostic =
                        "A configured weapon mount has no physical position.";
                    return false;
                }

                enabledMounts.Add(
                    new InventoryWeaponMountedRuntimeV1(
                        binding.MountStableId,
                        new EquipmentInstanceId(
                            binding.EquipmentInstanceStableId),
                        position.LateralOffset));
            }

            var actorState = new PlayerWeaponActorStateSourceV1(
                controller);
            var adapter = new InventoryBackedWeaponExecutionAdapter(
                holdings,
                equipmentCatalog,
                weaponCatalog,
                new PlayerWeaponOwnershipResolverV1(controller),
                effectEmitter,
                SimulationTicksPerSecond);
            weapons = new InventoryWeaponRuntimeComposition(
                actorState,
                enabledMounts,
                adapter);

            missionPort = new EmptyStrongboxMissionPortV1(holdings);
            missionResults = new MissionRunResultAuthorityV1(
                missionPort);
            UpdateWeaponDisplayNames();
            RetireInGameLoadoutSelector();
            diagnostic = string.Empty;
            return true;
        }

        internal bool IsEnemyColliderForWeaponPresentation(
            Collider2D collider)
        {
            EnemyBinding target;
            return collider != null
                && TryResolveEnemy(collider, out target)
                && target != null;
        }

        internal bool IsPlayerColliderForWeaponPresentation(
            Collider2D collider)
        {
            return controller != null
                && collider != null
                && (collider == controller.PlayerCollider
                    || (controller.PlayerTransform != null
                        && collider.transform.IsChildOf(
                            controller.PlayerTransform)));
        }

        internal IReadOnlyList<Stage1ArcTargetProjectionV1>
            GatherArcTargets(ChainArcEffect chain)
        {
            var candidates =
                new List<Stage1ArcTargetProjectionV1>();
            if (chain == null)
            {
                return candidates;
            }

            Vector2 origin = new Vector2(
                (float)chain.Origin.X,
                (float)chain.Origin.Y);
            Vector2 direction = new Vector2(
                (float)chain.Direction.X,
                (float)chain.Direction.Y).normalized;
            float maximumRange = (float)chain.MaximumRange;
            var unique = new HashSet<EnemyBinding>();

            foreach (KeyValuePair<Collider2D, EnemyBinding> pair
                in enemyByCollider)
            {
                Collider2D collider = pair.Key;
                EnemyBinding binding = pair.Value;
                if (collider == null
                    || binding == null
                    || !collider.enabled
                    || !collider.gameObject.activeInHierarchy
                    || !unique.Add(binding))
                {
                    continue;
                }

                Vector2 delta =
                    (Vector2)collider.bounds.center - origin;
                float distance = delta.magnitude;
                if (distance > maximumRange || distance <= 0.001f)
                {
                    continue;
                }
                if (Vector2.Dot(
                    direction,
                    delta / distance) < 0.25f)
                {
                    continue;
                }

                candidates.Add(
                    new Stage1ArcTargetProjectionV1(
                        collider,
                        collider.bounds.center,
                        distance,
                        binding.RoomInstanceStableId));
            }

            candidates.Sort(Stage1ArcTargetProjectionV1.Compare);
            return candidates;
        }

        internal bool TryApplyArcDamage(
            Stage1ArcTargetProjectionV1 target,
            WeaponEffectIdentity identity,
            double damage,
            string phase)
        {
            if (target == null || target.Collider == null)
            {
                return false;
            }

            EnemyBinding binding;
            return TryResolveEnemy(target.Collider, out binding)
                && ApplyEnemyDamage(
                    binding,
                    identity,
                    damage,
                    phase);
        }

        private void UpdateWeaponDisplayNames()
        {
            PlayerHoldingsSnapshotV1 snapshot =
                holdings.ExportSnapshot();
            var instances = new Dictionary<StableId, EquipmentInstance>();
            for (int index = 0;
                index < snapshot.UniqueHoldings.Count;
                index++)
            {
                UniqueHoldingSnapshotV1 holding =
                    snapshot.UniqueHoldings[index];
                if (holding == null
                    || holding.RewardKind
                        != RewardGrantKindV1.EquipmentReference
                    || holding.InstanceStableId == null
                    || holding.EquipmentInstance == null)
                {
                    continue;
                }
                instances[holding.InstanceStableId] =
                    holding.EquipmentInstance;
            }

            for (int index = 0;
                index < PlayerRouteProfilePayloadV1.WeaponSlotCount;
                index++)
            {
                StableId instanceStableId = profile.Payload
                    .WeaponSlots[index]
                    .EquipmentInstanceStableId;
                if (instanceStableId == null)
                {
                    WeaponDisplayNames[index] = "Inactive mount";
                    continue;
                }

                EquipmentInstance instance;
                if (!instances.TryGetValue(
                    instanceStableId,
                    out instance))
                {
                    WeaponDisplayNames[index] = "Missing weapon";
                    continue;
                }

                EquipmentDefinition definition =
                    equipmentCatalog.FindEquipmentDefinition(
                        instance.DefinitionId);
                WeaponDisplayNames[index] = definition == null
                    ? instance.DefinitionId.ToString()
                    : definition.DisplayName;
            }
        }

        private void RetireInGameLoadoutSelector()
        {
            if (controller.LoadoutSelector != null)
            {
                controller.LoadoutSelector.gameObject.SetActive(false);
            }
            if (controller.WeaponStrip != null)
            {
                controller.WeaponStrip.gameObject.SetActive(false);
            }
        }
    }

    internal sealed class Stage1ArcTargetProjectionV1
    {
        public Stage1ArcTargetProjectionV1(
            Collider2D collider,
            Vector3 position,
            float distance,
            StableId stableId)
        {
            Collider = collider;
            Position = position;
            Distance = distance;
            StableId = stableId;
        }

        public Collider2D Collider { get; }
        public Vector3 Position { get; }
        public float Distance { get; }
        public StableId StableId { get; }

        public static int Compare(
            Stage1ArcTargetProjectionV1 left,
            Stage1ArcTargetProjectionV1 right)
        {
            int distance = left.Distance.CompareTo(right.Distance);
            return distance != 0
                ? distance
                : left.StableId.CompareTo(right.StableId);
        }
    }
}
