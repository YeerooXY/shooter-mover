using System;
using System.Collections.Generic;
using ShooterMover.ContentPackages.Props.DestructibleProps;
using ShooterMover.Domain.Common;
using ShooterMover.UI.VisibleSliceGeneralCombatHud;
using ShooterMover.UnityAdapters.CombatPresentation;
using ShooterMover.UnityAdapters.Enemies;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    public sealed partial class Stage1PlayableLoopCompositionV1
    {
        private const string DefaultEnemyDeathVfxResourcePath =
            "CombatPresentation/Stage1DefaultEnemyDeathVfx";

        private readonly Dictionary<StableId, CombatEnemyPresentationRegistration2D>
            combatPresentationByActor =
                new Dictionary<StableId, CombatEnemyPresentationRegistration2D>();

        private CombatDeathVfxPool2D combatDeathVfxPool;
        private AuthoritativePlayerCombatHudSourceV1 authoritativeCombatHudSource;
        private bool playerCombatHudBound;

        private CombatEnemyPresentationRegistration2D RegisterEnemyCombatPresentation(
            GameObject presentationRoot,
            IEnemyActor2DAuthority authority)
        {
            if (presentationRoot == null)
            {
                throw new ArgumentNullException(nameof(presentationRoot));
            }
            if (authority == null)
            {
                throw new ArgumentNullException(nameof(authority));
            }

            EnsureCombatDeathVfxPool();
            CombatEnemyPresentationRegistration2D registration =
                CombatEnemyPresentationRegistration2D.Attach(
                    presentationRoot,
                    authority,
                    combatDeathVfxPool,
                    new Vector3(0f, 1.2f, 0f),
                    new EnemyDeathVfxScaleConfigurationV1(
                        1f,
                        0.75f,
                        2.25f));
            if (combatPresentationByActor.ContainsKey(
                registration.EntityInstanceStableId))
            {
                throw new InvalidOperationException(
                    "Enemy combat presentation identity was registered twice: "
                    + registration.EntityInstanceStableId + ".");
            }
            combatPresentationByActor.Add(
                registration.EntityInstanceStableId,
                registration);
            return registration;
        }

        private void EnsureCombatDeathVfxPool()
        {
            if (combatDeathVfxPool != null)
            {
                return;
            }

            combatDeathVfxPool = GetComponent<CombatDeathVfxPool2D>();
            if (combatDeathVfxPool == null)
            {
                combatDeathVfxPool = gameObject.AddComponent<CombatDeathVfxPool2D>();
            }
            combatDeathVfxPool.Configure(BuildDefaultEnemyDeathVfxFactory());
        }

        private static ICombatDeathVfxFactory2D BuildDefaultEnemyDeathVfxFactory()
        {
            Stage1DefaultEnemyDeathVfxAsset2D source =
                Resources.Load<Stage1DefaultEnemyDeathVfxAsset2D>(
                    DefaultEnemyDeathVfxResourcePath);
            if (source == null || source.Animation == null)
            {
                throw new InvalidOperationException(
                    "The retained default explosion presentation asset is unavailable.");
            }

            DestructiblePropDestructionAnimation animation = source.Animation;
            Sprite[] frames = new Sprite[animation.FrameCount];
            for (int index = 0; index < frames.Length; index++)
            {
                frames[index] = animation.GetFrame(index);
            }

            var definition = new SpriteAnimationCombatDeathVfxDefinitionV1(
                "retained.asset:" + animation.name,
                frames,
                animation.SecondsPerFrame,
                animation.LocalOffset,
                animation.VisualScale,
                animation.SortingOrder,
                animation.UseUnscaledTime);
            return new SpriteAnimationCombatDeathVfxFactory2D(
                definition,
                new FallbackRingCombatDeathVfxFactory2D());
        }

        private void LateUpdate()
        {
            if (!initialized || controller == null)
            {
                return;
            }

            if (!playerCombatHudBound)
            {
                BindAuthoritativePlayerHud();
                playerCombatHudBound = true;
            }

            foreach (CombatEnemyPresentationRegistration2D registration in
                combatPresentationByActor.Values)
            {
                if (registration != null)
                {
                    registration.SynchronizeLifecycle();
                }
            }
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
