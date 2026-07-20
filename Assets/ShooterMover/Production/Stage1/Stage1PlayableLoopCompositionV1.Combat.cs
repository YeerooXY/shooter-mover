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
    /// <summary>
    /// DEMO-CUTOVER-001 composition adapter. The retained Stage 1 controller supplies
    /// scene-authored Unity presentation only; this component connects the accepted
    /// player, weapon, enemy, room, mission-result and flow authorities into one loop.
    /// </summary>
    public sealed partial class Stage1PlayableLoopCompositionV1
    {
        private void Update()
        {
            if (!initialized || ending)
            {
                return;
            }

            HandleRestartInput();
            if (controller.IsPlayerDead)
            {
                if (!playerDeathProjected)
                {
                    effectEmitter.ClearEmittedEffects();
                    preparedEffects.Clear();
                    preparedPools.Clear();
                    playerDeathProjected = true;
                }

                return;
            }

            playerDeathProjected = false;
            HandleWeaponSelection();
            HandleWeaponInput();
            PrepareEmittedEffects();
            PreparePersistentPools();
            ProjectCurrentRoom(false);
            HandleRoomTraversal();
        }

        private void HandleRestartInput()
        {
            if (Keyboard.current == null
                || !Keyboard.current.rKey.wasPressedThisFrame)
            {
                return;
            }

            long before = controller.RestartGeneration;
            controller.QuickRestart();
            long after = controller.RestartGeneration;
            if (after == before || after == restartObserved)
            {
                return;
            }

            restartObserved = after;
            effectEmitter.ClearEmittedEffects();
            preparedEffects.Clear();
            preparedPools.Clear();
            rooms.Restart(StableId.Create(
                "operation",
                "demo-cutover-room-restart-g"
                    + after.ToString(CultureInfo.InvariantCulture)));
            BeginRun();
            ProjectCurrentRoom(true);
        }

        private void HandleWeaponSelection()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.digit1Key.wasPressedThisFrame) weapons.SelectSlot(0);
            if (keyboard.digit2Key.wasPressedThisFrame) weapons.SelectSlot(1);
            if (keyboard.digit3Key.wasPressedThisFrame) weapons.SelectSlot(2);
            if (keyboard.digit4Key.wasPressedThisFrame) weapons.SelectSlot(3);
        }

        private void HandleWeaponInput()
        {
            bool fire = Mouse.current != null
                && Mouse.current.leftButton.isPressed;
            fire |= Keyboard.current != null
                && Keyboard.current.spaceKey.isPressed;
            if (!fire || controller.PlayerTransform == null)
            {
                return;
            }

            Camera camera = Camera.main;
            if (camera == null) return;
            Vector3 origin3 = controller.PlayerTransform.position;
            Vector3 target3;
            if (Mouse.current != null)
            {
                Vector2 screen = Mouse.current.position.ReadValue();
                target3 = camera.ScreenToWorldPoint(
                    new Vector3(screen.x, screen.y, -camera.transform.position.z));
            }
            else
            {
                target3 = origin3 + controller.PlayerTransform.up;
            }

            Vector2 targetDelta = (Vector2)(target3 - origin3);
            if (targetDelta.sqrMagnitude < 0.001f) return;

            long operation = fireSequence++;
            InventoryWeaponExecutionResult result = weapons.TryFireAtTarget(
                new FireOperationId(StableId.Create(
                    "fire-operation",
                    "demo-cutover-g"
                        + controller.RestartGeneration.ToString(CultureInfo.InvariantCulture)
                        + "-s"
                        + operation.ToString(CultureInfo.InvariantCulture))),
                (long)Math.Floor(Time.unscaledTimeAsDouble * SimulationTicksPerSecond),
                unchecked((ulong)operation + 1UL),
                new WeaponVector2(origin3.x, origin3.y),
                new WeaponVector2(target3.x, target3.y));
            if (result.Status != WeaponExecutionStatus.Accepted
                && result.Status != WeaponExecutionStatus.CooldownActive
                && result.Status != WeaponExecutionStatus.ReplayAccepted)
            {
                diagnostic = result.RejectionCode;
            }
        }

        private void PrepareEmittedEffects()
        {
            IReadOnlyList<InventoryWeaponEffectInstance2D> emitted =
                effectEmitter.EmittedEffects;
            for (int index = 0; index < emitted.Count; index++)
            {
                InventoryWeaponEffectInstance2D effect = emitted[index];
                if (effect == null || !preparedEffects.Add(effect)) continue;
                Stage1InventoryWeaponProjectileHit2D hit =
                    effect.gameObject.AddComponent<Stage1InventoryWeaponProjectileHit2D>();
                hit.Configure(this, effect);
                AddProjectilePresentation(effect);
            }
        }

        private void PreparePersistentPools()
        {
            InventoryWeaponPersistentDamageArea2D[] pools =
                FindObjectsByType<InventoryWeaponPersistentDamageArea2D>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            for (int index = 0; index < pools.Length; index++)
            {
                InventoryWeaponPersistentDamageArea2D pool = pools[index];
                if (pool == null || !preparedPools.Add(pool)) continue;
                Stage1InventoryWeaponPoolDamage2D damage =
                    pool.gameObject.AddComponent<Stage1InventoryWeaponPoolDamage2D>();
                damage.Configure(this, pool);
            }
        }

        private void AddProjectilePresentation(InventoryWeaponEffectInstance2D effect)
        {
            string id = effect.Description.Identity.WeaponDefinitionId.Value;
            ProjectilePresentation presentation;
            if (!projectilePresentation.TryGetValue(id, out presentation))
            {
                diagnostic = "Missing projectile presentation for " + id;
                return;
            }

            var renderer = effect.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = RuntimeSprite(id, presentation.Color);
            renderer.sortingOrder = 30;
            effect.transform.localScale = presentation.Scale;
            Rigidbody2D body = effect.GetComponent<Rigidbody2D>();
            if (body != null && body.linearVelocity.sqrMagnitude > 0.0001f)
            {
                effect.transform.right = body.linearVelocity.normalized;
            }
        }

        internal bool TryApplyProjectileHit(
            Collider2D collider,
            InventoryWeaponEffectInstance2D effect,
            int hitOrdinal,
            out int remainingPierce)
        {
            remainingPierce = 0;
            EnemyBinding target;
            if (collider == null
                || effect == null
                || !TryResolveEnemy(collider, out target))
            {
                return false;
            }

            IWeaponEffectDescription description = effect.Description;
            double directDamage = 0d;
            double areaDamage = 0d;
            double explosionRadius = 0d;
            double dotDps = 0d;
            double dotDuration = 0d;
            int pierce = 0;

            DirectProjectileEffect direct = description as DirectProjectileEffect;
            if (direct != null)
            {
                directDamage = direct.DirectDamage;
                pierce = direct.Pierce;
            }

            ExplosiveProjectileEffect explosive =
                description as ExplosiveProjectileEffect;
            if (explosive != null)
            {
                directDamage = explosive.DirectDamage;
                areaDamage = explosive.AreaDamage;
                explosionRadius = explosive.ExplosionRadius;
            }

            DamageOverTimeProjectileEffect dot =
                description as DamageOverTimeProjectileEffect;
            if (dot != null)
            {
                directDamage = dot.DirectDamage;
                pierce = dot.Pierce;
                dotDps = dot.DotDps;
                dotDuration = dot.DotDuration;
            }

            bool changed = ApplyEnemyDamage(
                target,
                description.Identity,
                directDamage,
                "direct-" + hitOrdinal.ToString(CultureInfo.InvariantCulture));
            if (areaDamage > 0d && explosionRadius > 0d)
            {
                ApplyAreaDamage(
                    effect.transform.position,
                    explosionRadius,
                    areaDamage,
                    description.Identity,
                    "explosion-" + hitOrdinal.ToString(CultureInfo.InvariantCulture));
            }
            if (dotDps > 0d && dotDuration > 0d)
            {
                StartCoroutine(ApplyDamageOverTime(
                    target,
                    description.Identity,
                    dotDps,
                    dotDuration,
                    "dot-" + hitOrdinal.ToString(CultureInfo.InvariantCulture)));
            }
            if (dot != null && dot.PoolRadius > 0d && dot.PoolDuration > 0d)
            {
                SpawnPersistentPool(dot, effect.transform.position);
            }

            remainingPierce = pierce;
            return changed;
        }

        private void SpawnPersistentPool(
            DamageOverTimeProjectileEffect effect,
            Vector2 position)
        {
            GameObject area = new GameObject(
                "InventoryWeaponPersistentDamageArea2D_Impact");
            area.transform.SetParent(effectEmitter.transform, false);
            area.transform.position = position;
            InventoryWeaponPersistentDamageArea2D pool =
                area.AddComponent<InventoryWeaponPersistentDamageArea2D>();
            pool.Configure(effect);
        }

        internal void ApplyPoolTick(
            InventoryWeaponPersistentDamageArea2D pool,
            Collider2D collider,
            int tick)
        {
            EnemyBinding target;
            if (pool == null
                || pool.SourceEffect == null
                || !TryResolveEnemy(collider, out target))
            {
                return;
            }

            double amount = pool.SourceEffect.DotDps * 0.25d;
            ApplyEnemyDamage(
                target,
                pool.SourceEffect.Identity,
                amount,
                "pool-" + tick.ToString(CultureInfo.InvariantCulture));
        }

        private IEnumerator ApplyDamageOverTime(
            EnemyBinding target,
            WeaponEffectIdentity identity,
            double damagePerSecond,
            double duration,
            string phase)
        {
            const double interval = 0.25d;
            int ticks = Math.Max(1, (int)Math.Ceiling(duration / interval));
            for (int index = 0; index < ticks; index++)
            {
                yield return new WaitForSeconds((float)interval);
                EnemyActorState state;
                if (!target.Authority.TryReadState(out state)
                    || state == null
                    || state.IsDestroyed)
                {
                    yield break;
                }

                ApplyEnemyDamage(
                    target,
                    identity,
                    damagePerSecond * interval,
                    phase + "-" + index.ToString(CultureInfo.InvariantCulture));
            }
        }

        private void ApplyAreaDamage(
            Vector2 origin,
            double radius,
            double amount,
            WeaponEffectIdentity identity,
            string phase)
        {
            var unique = new HashSet<EnemyBinding>();
            foreach (KeyValuePair<Collider2D, EnemyBinding> pair in enemyByCollider)
            {
                if (pair.Key == null
                    || !pair.Key.enabled
                    || !pair.Key.gameObject.activeInHierarchy)
                {
                    continue;
                }
                if (Vector2.Distance(origin, pair.Key.bounds.center) <= radius)
                {
                    unique.Add(pair.Value);
                }
            }

            foreach (EnemyBinding target in unique)
            {
                ApplyEnemyDamage(target, identity, amount, phase);
            }
        }

        private bool ApplyEnemyDamage(
            EnemyBinding target,
            WeaponEffectIdentity identity,
            double amount,
            string phase)
        {
            if (target == null || amount <= 0d || identity == null)
            {
                return false;
            }

            EnemyActorState before;
            if (!target.Authority.TryReadState(out before)
                || before == null
                || before.IsDestroyed)
            {
                return false;
            }

            StableId eventId = StableId.Create(
                "combat-event",
                "demo-cutover-" + HashToken(
                    identity.ToCanonicalString()
                    + "|" + target.RoomInstanceStableId
                    + "|" + phase));
            EnemyActorStepResult result = target.Authority.Apply(
                EnemyActorCommand.Damage(
                    enemyDamageOrder++,
                    eventId,
                    identity.ActorId.Value,
                    (int)CombatChannel.Kinetic,
                    amount));
            if (result == null || result.State == null)
            {
                return false;
            }

            EnemyDestroyedNotification destroyed = null;
            for (int index = 0; index < result.Notifications.Count; index++)
            {
                destroyed = result.Notifications[index]
                    as EnemyDestroyedNotification;
                if (destroyed != null) break;
            }

            if (destroyed != null)
            {
                HandleEnemyDestroyed(target, identity.ParticipantId.Value, destroyed);
            }

            return result.State.Health < before.Health;
        }

        private void HandleEnemyDestroyed(
            EnemyBinding target,
            StableId participantId,
            EnemyDestroyedNotification destroyed)
        {
            if (target == null
                || participantId == null
                || destroyed == null
                || !rewardedEnemies.Add(target.RoomInstanceStableId))
            {
                return;
            }

            ParticipantRunStats stats;
            if (!participantStats.TryGetValue(participantId, out stats))
            {
                stats = new ParticipantRunStats(participantId);
                participantStats.Add(participantId, stats);
            }
            stats.Kills++;

            pendingEnemyRewards.Add(
                new PendingEnemyReward(
                    participantId,
                    target.DefinitionStableId,
                    destroyed));
        }

        private bool TryResolveEnemy(
            Collider2D collider,
            out EnemyBinding binding)
        {
            if (enemyByCollider.TryGetValue(collider, out binding))
            {
                return true;
            }

            Transform current = collider.transform.parent;
            while (current != null)
            {
                Collider2D[] colliders = current.GetComponents<Collider2D>();
                for (int index = 0; index < colliders.Length; index++)
                {
                    if (enemyByCollider.TryGetValue(colliders[index], out binding))
                    {
                        return true;
                    }
                }
                current = current.parent;
            }

            binding = null;
            return false;
        }
    }
}
