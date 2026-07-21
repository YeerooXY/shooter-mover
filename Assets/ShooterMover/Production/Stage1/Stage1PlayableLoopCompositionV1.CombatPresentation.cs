using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.UI.VisibleSliceGeneralCombatHud;
using ShooterMover.UnityAdapters.CombatPresentation;
using ShooterMover.UnityAdapters.Enemies;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    public sealed partial class Stage1PlayableLoopCompositionV1
    {
        private readonly Dictionary<StableId, EnemyCombatPresentationBindingV1>
            combatPresentationByActor =
                new Dictionary<StableId, EnemyCombatPresentationBindingV1>();

        private DefaultCombatExplosionPool2D defaultCombatExplosionPool;
        private AuthoritativePlayerCombatHudSourceV1 authoritativeCombatHudSource;
        private bool combatPresentationInstalled;

        private void LateUpdate()
        {
            if (!initialized || controller == null)
            {
                return;
            }

            if (!combatPresentationInstalled)
            {
                InstallCombatPresentation();
            }

            long generation = controller.RestartGeneration;
            foreach (EnemyCombatPresentationBindingV1 binding in
                combatPresentationByActor.Values)
            {
                binding.SynchronizeLifecycle(generation);
            }

            for (int index = 0; index < pendingEnemyRewards.Count; index++)
            {
                PendingEnemyReward pending = pendingEnemyRewards[index];
                if (pending == null || pending.Destruction == null)
                {
                    continue;
                }

                EnemyCombatPresentationBindingV1 binding;
                if (!combatPresentationByActor.TryGetValue(
                        pending.Destruction.TargetId,
                        out binding))
                {
                    continue;
                }

                binding.PresentAcceptedTerminal(
                    pending.Destruction,
                    generation);
            }
        }

        private void InstallCombatPresentation()
        {
            defaultCombatExplosionPool =
                GetComponent<DefaultCombatExplosionPool2D>();
            if (defaultCombatExplosionPool == null)
            {
                defaultCombatExplosionPool =
                    gameObject.AddComponent<DefaultCombatExplosionPool2D>();
            }

            combatPresentationByActor.Clear();
            BindEnemyCombatPresentation(
                controller.MobileBlasterDroid.gameObject,
                controller.MobileBlasterDroid);
            BindEnemyCombatPresentation(
                controller.TurretPackage.gameObject,
                controller.TurretPackage.Authority);
            BindAuthoritativePlayerHud();
            combatPresentationInstalled = true;
        }

        private void BindEnemyCombatPresentation(
            GameObject presentationRoot,
            IEnemyActor2DAuthority authority)
        {
            if (presentationRoot == null || authority == null)
            {
                throw new ArgumentNullException(
                    presentationRoot == null
                        ? nameof(presentationRoot)
                        : nameof(authority));
            }

            EnemyActorState initialState;
            if (!authority.TryReadState(out initialState)
                || initialState == null)
            {
                throw new InvalidOperationException(
                    "Enemy combat presentation requires an immutable initial actor state.");
            }

            StableId actorId = initialState.ActorId;
            var source = new EnemyActorCombatHealthSnapshotSourceV1(
                actorId,
                () => controller.RestartGeneration,
                authority.TryReadState,
                new CombatPresentationAnchorFactsV1(
                    actorId,
                    0d,
                    1.2d,
                    0d));

            CombatHealthBarPresenter2D healthBar =
                presentationRoot.GetComponent<CombatHealthBarPresenter2D>();
            if (healthBar == null)
            {
                healthBar = presentationRoot.AddComponent<CombatHealthBarPresenter2D>();
            }
            healthBar.Configure(
                actorId,
                source,
                new Vector3(0f, 1.2f, 0f));

            EnemyDeathVfxPresenter2D deathVfx =
                presentationRoot.GetComponent<EnemyDeathVfxPresenter2D>();
            if (deathVfx == null)
            {
                deathVfx = presentationRoot.AddComponent<EnemyDeathVfxPresenter2D>();
            }
            deathVfx.Configure(
                actorId,
                controller.RestartGeneration,
                healthBar,
                defaultCombatExplosionPool,
                new EnemyDeathVfxScaleConfigurationV1(
                    1f,
                    0.75f,
                    2.25f));

            combatPresentationByActor.Add(
                actorId,
                new EnemyCombatPresentationBindingV1(
                    actorId,
                    presentationRoot.transform,
                    healthBar,
                    deathVfx));
        }

        private void BindAuthoritativePlayerHud()
        {
            if (controller.CombatHud == null
                || controller.PlayerLiveAuthority == null
                || !controller.PlayerLiveAuthority.IsInitialized)
            {
                throw new InvalidOperationException(
                    "The canonical gameplay HUD and player health source are required.");
            }

            var playerHealthSource =
                new PlayerHudCombatHealthSnapshotSourceV1(
                    controller.PlayerLiveAuthority.ExportHudHealth);
            authoritativeCombatHudSource =
                new AuthoritativePlayerCombatHudSourceV1(
                    controller,
                    playerHealthSource);
            controller.CombatHud.UnbindSources();
            controller.CombatHud.BindSources(
                authoritativeCombatHudSource,
                null);
        }

        private sealed class EnemyCombatPresentationBindingV1
        {
            private readonly StableId actorId;
            private readonly Transform presentationRoot;
            private readonly CombatHealthBarPresenter2D healthBar;
            private readonly EnemyDeathVfxPresenter2D deathVfx;

            public EnemyCombatPresentationBindingV1(
                StableId actorId,
                Transform presentationRoot,
                CombatHealthBarPresenter2D healthBar,
                EnemyDeathVfxPresenter2D deathVfx)
            {
                this.actorId = actorId ?? throw new ArgumentNullException(nameof(actorId));
                this.presentationRoot = presentationRoot
                    ?? throw new ArgumentNullException(nameof(presentationRoot));
                this.healthBar = healthBar ?? throw new ArgumentNullException(nameof(healthBar));
                this.deathVfx = deathVfx ?? throw new ArgumentNullException(nameof(deathVfx));
            }

            public void SynchronizeLifecycle(long generation)
            {
                if (generation > deathVfx.LifecycleGeneration)
                {
                    deathVfx.AdvanceLifecycle(generation);
                    healthBar.Refresh();
                }
            }

            public void PresentAcceptedTerminal(
                EnemyDestroyedNotification destruction,
                long lifecycleGeneration)
            {
                if (destruction == null
                    || destruction.TargetId != actorId)
                {
                    return;
                }

                deathVfx.TryPresent(
                    new EnemyTerminalPresentationFactV1(
                        destruction.EventId,
                        actorId,
                        lifecycleGeneration,
                        presentationRoot.position,
                        EnemyPresentationBounds2D.MeasureLargestDimension(
                            presentationRoot)));
            }
        }

        private sealed class AuthoritativePlayerCombatHudSourceV1 :
            IGeneralCombatHudStateSource,
            ICombatHealthBarSnapshotSourceV1
        {
            private readonly IGeneralCombatHudStateSource retainedPresentationSource;
            private readonly ICombatHealthBarSnapshotSourceV1 playerHealthSource;

            public AuthoritativePlayerCombatHudSourceV1(
                IGeneralCombatHudStateSource retainedPresentationSource,
                ICombatHealthBarSnapshotSourceV1 playerHealthSource)
            {
                this.retainedPresentationSource = retainedPresentationSource
                    ?? throw new ArgumentNullException(nameof(retainedPresentationSource));
                this.playerHealthSource = playerHealthSource
                    ?? throw new ArgumentNullException(nameof(playerHealthSource));
            }

            public bool TryRead(out CombatHealthBarSnapshotV1 snapshot)
            {
                return playerHealthSource.TryRead(out snapshot);
            }

            public bool TryRead(out GeneralCombatHudSnapshot snapshot)
            {
                GeneralCombatHudSnapshot retained;
                CombatHealthBarSnapshotV1 health;
                if (!retainedPresentationSource.TryRead(out retained)
                    || retained == null
                    || !playerHealthSource.TryRead(out health)
                    || health == null)
                {
                    snapshot = null;
                    return false;
                }

                snapshot = new GeneralCombatHudSnapshot(
                    new ShooterMover.Contracts.Combat.VitalState(
                        health.CurrentHealth,
                        health.MaximumHealth,
                        0d,
                        0d),
                    retained.ThrusterStatus,
                    retained.FocusedEnemy,
                    retained.FocusedEnemyLabel,
                    retained.RoomName,
                    retained.ObjectiveText,
                    retained.RestartKeyboardHint,
                    retained.RestartControllerHint,
                    retained.ReticleVisible,
                    retained.ReticleNormalizedX,
                    retained.ReticleNormalizedY,
                    retained.ReducedEffects,
                    health.LifecycleGeneration);
                return true;
            }
        }
    }
}
