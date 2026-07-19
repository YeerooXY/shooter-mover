#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.ContentPackages.Environment.VoidHazards;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.GameplayEntities;
using ShooterMover.UI.VisibleSliceGeneralCombatHud;
using ShooterMover.UnityAdapters.Players;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.VisibleSliceIntegration
{
    public sealed class Stage1PlayerLiveAuthorityPlayModeTests
    {
        private const string SceneName = "Stage1VisibleSlice";
        private const string ScenePath =
            "Assets/ShooterMover/Scenes/Prototypes/Stage1VisibleSlice.unity";
        private const string ControllerTypeName =
            "ShooterMover.TestSupport.VisibleSlice.Stage1VisibleSliceController";
        private const string LiveAdapterTypeName =
            "ShooterMover.TestSupport.VisibleSlice.Stage1PlayerLiveAuthorityAdapterV1";

        [UnityTearDown]
        public IEnumerator UnloadScene()
        {
            Scene scene = SceneManager.GetSceneByName(SceneName);
            if (scene.IsValid() && scene.isLoaded)
            {
                if (SceneManager.sceneCount == 1)
                {
                    SceneManager.SetActiveScene(
                        SceneManager.CreateScene("PLAYER-LIVE-001 Cleanup"));
                }
                AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone)
                {
                    yield return null;
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RapidRestart_TwoCallsBeforeYieldAdvanceAuthorityTwice()
        {
            MonoBehaviour controller = null;
            MonoBehaviour adapter = null;
            yield return LoadComposition(
                value => controller = value,
                value => adapter = value);

            PlayerRuntimeSnapshot before =
                Invoke<PlayerRuntimeSnapshot>(adapter, "ExportSnapshot");
            StableId actorId = before.Player.ActorInstanceId;

            PlayerRuntimeRestartResult first =
                Invoke<PlayerRuntimeRestartResult>(controller, "QuickRestart");
            PlayerRuntimeRestartResult second =
                Invoke<PlayerRuntimeRestartResult>(controller, "QuickRestart");

            Assert.That(first.Status, Is.EqualTo(PlayerRuntimeRestartStatus.Applied));
            Assert.That(second.Status, Is.EqualTo(PlayerRuntimeRestartStatus.Applied));
            PlayerRuntimeSnapshot after =
                Invoke<PlayerRuntimeSnapshot>(adapter, "ExportSnapshot");
            Assert.That(after.Player.ActorInstanceId, Is.EqualTo(actorId));
            Assert.That(
                after.Player.LifecycleGeneration,
                Is.EqualTo(before.Player.LifecycleGeneration + 2L));
            Assert.That(after.Movement.Generation, Is.EqualTo(after.Player.LifecycleGeneration));
            Assert.That(after.Player.CurrentHealth, Is.EqualTo(100d));
            Assert.That(Read<long>(controller, "RestartGeneration"),
                Is.EqualTo(after.Player.LifecycleGeneration));
        }

        [UnityTest]
        public IEnumerator LiveDamageHealingDuplicatesAndHudUseAuthoritySnapshot()
        {
            MonoBehaviour controller = null;
            MonoBehaviour adapter = null;
            yield return LoadComposition(
                value => controller = value,
                value => adapter = value);

            PlayerRuntimeSnapshot initial =
                Invoke<PlayerRuntimeSnapshot>(adapter, "ExportSnapshot");
            StableId eventId = StableId.Parse("combat-event.player-live-duplicate");
            StableId source = StableId.Parse("actor.player-live-source");
            PlayerDamageRequest damage = new PlayerDamageRequest(
                eventId,
                source,
                StableId.Parse("participant.untrusted"),
                initial.Player.ActorInstanceId,
                12.5d,
                CombatChannel.Kinetic,
                initial.Player.LifecycleGeneration);
            DamageReceiverResult applied =
                Invoke<DamageReceiverResult>(adapter, "ApplyDamage", damage);
            DamageReceiverResult duplicate =
                Invoke<DamageReceiverResult>(adapter, "ApplyDamage", damage);
            DamageReceiverResult conflict = Invoke<DamageReceiverResult>(
                adapter,
                "ApplyDamage",
                new PlayerDamageRequest(
                    eventId,
                    source,
                    null,
                    initial.Player.ActorInstanceId,
                    8d,
                    CombatChannel.Kinetic,
                    initial.Player.LifecycleGeneration));

            Assert.That(applied.Status, Is.EqualTo(DamageReceiverStatus.Applied));
            Assert.That(duplicate.Status, Is.EqualTo(DamageReceiverStatus.Duplicate));
            Assert.That(conflict.Status, Is.EqualTo(DamageReceiverStatus.RejectedInvalid));
            Assert.That(
                conflict.RejectionCode,
                Is.EqualTo(DamageReceiverRejectionCode.ConflictingDuplicate));

            PlayerActorHealingResult healed = Invoke<PlayerActorHealingResult>(
                adapter,
                "ApplyHealing",
                new PlayerHealingRequest(
                    StableId.Parse("operation.player-live-heal"),
                    source,
                    StableId.Parse("participant.untrusted"),
                    initial.Player.ActorInstanceId,
                    2.25d,
                    initial.Player.LifecycleGeneration));
            Assert.That(healed.Status, Is.EqualTo(PlayerActorOperationStatus.Applied));

            PlayerRuntimeSnapshot live =
                Invoke<PlayerRuntimeSnapshot>(adapter, "ExportSnapshot");
            GeneralCombatHudSnapshot hud = Invoke<GeneralCombatHudSnapshot>(
                adapter,
                "ExportVisibleHudSnapshot");
            Assert.That(live.Player.CurrentHealth, Is.EqualTo(89.75d));
            Assert.That(hud.PlayerVital.Health, Is.EqualTo(89.75d));
            Assert.That(hud.PlayerVital.MaximumHealth, Is.EqualTo(100d));
            Assert.That(hud.RestartGeneration, Is.EqualTo(live.Player.LifecycleGeneration));
            Assert.That(Read<int>(controller, "PlayerHealth"), Is.EqualTo(90),
                "The integer property is compatibility-only; the HUD retains fractional health.");
        }

        [UnityTest]
        public IEnumerator LethalDamageProjectsDeathOnceAndRestartRestoresParticipation()
        {
            MonoBehaviour controller = null;
            MonoBehaviour adapter = null;
            yield return LoadComposition(
                value => controller = value,
                value => adapter = value);

            PlayerRuntimeSnapshot before =
                Invoke<PlayerRuntimeSnapshot>(adapter, "ExportSnapshot");
            object droid = Read<object>(controller, "MobileBlasterDroid");
            StableId sourceActor = Read<StableId>(
                Read<object>(droid, "EnemyTarget"),
                "TargetId");
            PlayerDamageRequest lethal = new PlayerDamageRequest(
                StableId.Parse("combat-event.player-live-lethal"),
                sourceActor,
                null,
                before.Player.ActorInstanceId,
                1000d,
                CombatChannel.Kinetic,
                before.Player.LifecycleGeneration);

            DamageReceiverResult applied =
                Invoke<DamageReceiverResult>(adapter, "ApplyDamage", lethal);
            DamageReceiverResult replay =
                Invoke<DamageReceiverResult>(adapter, "ApplyDamage", lethal);
            Assert.That(applied.Status, Is.EqualTo(DamageReceiverStatus.Applied));
            Assert.That(applied.DeathFact, Is.Not.Null);
            Assert.That(replay.Status, Is.EqualTo(DamageReceiverStatus.Duplicate));
            Assert.That(Read<int>(adapter, "DeathFactCount"), Is.EqualTo(1));
            GameplayEntityDeathFact fact =
                Read<GameplayEntityDeathFact>(adapter, "LastDeathFact");
            Assert.That(fact.SourceRunParticipantId,
                Is.EqualTo(StableId.Parse("participant.stage1-mobile-droid")));
            Assert.That(Read<bool>(controller, "IsSessionActive"), Is.False);
            Assert.That(Read<bool>(controller, "IsPlayerGameplayActive"), Is.False);
            Assert.That(Read<Collider2D>(controller, "PlayerCollider").enabled, Is.False);
            Assert.That(
                Read<bool>(Read<object>(controller, "PlayerMovementLifecycle"), "IsRunning"),
                Is.False);
            Assert.That(Invoke<bool>(controller, "FireAtMobileDroidForTests"), Is.False);

            PlayerRuntimeRestartResult restart =
                Invoke<PlayerRuntimeRestartResult>(controller, "QuickRestart");
            Assert.That(restart.Status, Is.EqualTo(PlayerRuntimeRestartStatus.Applied));
            PlayerRuntimeSnapshot restored =
                Invoke<PlayerRuntimeSnapshot>(adapter, "ExportSnapshot");
            Assert.That(restored.Player.CurrentHealth, Is.EqualTo(100d));
            Assert.That(restored.Player.ActorInstanceId,
                Is.EqualTo(before.Player.ActorInstanceId));
            Assert.That(Read<bool>(controller, "IsPlayerGameplayActive"), Is.True);
            Assert.That(Read<Collider2D>(controller, "PlayerCollider").enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator MobileDroidProjectile_DamagesLiveAuthorityAfterContact()
        {
            MonoBehaviour controller = null;
            MonoBehaviour adapter = null;
            yield return LoadComposition(
                value => controller = value,
                value => adapter = value);

            Component droid = (Component)Read<object>(controller, "MobileBlasterDroid");
            Transform player = Read<Transform>(controller, "PlayerTransform");
            player.position = droid.transform.position + Vector3.right * 3f;
            float deadline = Time.time + 3f;
            while (Time.time < deadline
                && Invoke<PlayerHudHealthSnapshot>(adapter, "ExportHudHealth").CurrentHealth
                    == 100d)
            {
                yield return null;
            }
            Assert.That(
                Invoke<PlayerHudHealthSnapshot>(adapter, "ExportHudHealth").CurrentHealth,
                Is.LessThan(100d));
        }

        [UnityTest]
        public IEnumerator StaleProjectileAndVoidRequestsRejectAfterRestart()
        {
            MonoBehaviour controller = null;
            MonoBehaviour adapter = null;
            yield return LoadComposition(
                value => controller = value,
                value => adapter = value);

            PlayerRuntimeSnapshot before =
                Invoke<PlayerRuntimeSnapshot>(adapter, "ExportSnapshot");
            Invoke<PlayerRuntimeRestartResult>(controller, "QuickRestart");
            PlayerRuntimeSnapshot after =
                Invoke<PlayerRuntimeSnapshot>(adapter, "ExportSnapshot");
            StableId source = StableId.Parse("actor.stale-projectile-source");
            DamageReceiverResult staleProjectile = Invoke<DamageReceiverResult>(
                adapter,
                "ApplyProjectileDamage",
                StableId.Parse("projectile-hit.stale-g0-f1"),
                source,
                after.Player.ActorInstanceId,
                25d,
                CombatChannel.Kinetic,
                before.Player.LifecycleGeneration);
            Assert.That(staleProjectile.Status,
                Is.EqualTo(DamageReceiverStatus.RejectedByLifecycle));
            Assert.That(
                Invoke<PlayerHudHealthSnapshot>(adapter, "ExportHudHealth").CurrentHealth,
                Is.EqualTo(100d));

            VoidHazardPortResult staleVoid = Invoke<VoidHazardPortResult>(
                adapter,
                "RequestDamage",
                new VoidHazardDamageRequest(
                    StableId.Parse("void-event.stale-g0-c1"),
                    StableId.Parse("placed.demo002-void-hazard"),
                    after.Player.ActorInstanceId,
                    35d,
                    before.Player.LifecycleGeneration));
            Assert.That(staleVoid, Is.EqualTo(VoidHazardPortResult.Rejected));
            Assert.That(Read<int>(controller, "VoidDamageCount"), Is.Zero);
        }

        [UnityTest]
        public IEnumerator VoidCountChangesOnlyForAcceptedAuthorityDamage()
        {
            MonoBehaviour controller = null;
            MonoBehaviour adapter = null;
            yield return LoadComposition(
                value => controller = value,
                value => adapter = value);

            PlayerRuntimeSnapshot player =
                Invoke<PlayerRuntimeSnapshot>(adapter, "ExportSnapshot");
            StableId eventId = StableId.Parse("void-event.test-g0-c1");
            StableId hazardId = StableId.Parse("placed.demo002-void-hazard");
            VoidHazardDamageRequest acceptedRequest = new VoidHazardDamageRequest(
                eventId,
                hazardId,
                player.Player.ActorInstanceId,
                35d);
            VoidHazardPortResult accepted = Invoke<VoidHazardPortResult>(
                adapter,
                "RequestDamage",
                acceptedRequest);
            VoidHazardPortResult duplicate = Invoke<VoidHazardPortResult>(
                adapter,
                "RequestDamage",
                acceptedRequest);
            VoidHazardPortResult conflict = Invoke<VoidHazardPortResult>(
                adapter,
                "RequestDamage",
                new VoidHazardDamageRequest(
                    eventId,
                    hazardId,
                    player.Player.ActorInstanceId,
                    10d));
            VoidHazardPortResult mismatch = Invoke<VoidHazardPortResult>(
                adapter,
                "RequestDamage",
                new VoidHazardDamageRequest(
                    StableId.Parse("void-event.mismatch-g0-c2"),
                    hazardId,
                    StableId.Parse("actor.someone-else"),
                    35d));

            Assert.That(accepted, Is.EqualTo(VoidHazardPortResult.Accepted));
            Assert.That(duplicate, Is.EqualTo(VoidHazardPortResult.DuplicateNoChange));
            Assert.That(conflict, Is.EqualTo(VoidHazardPortResult.Rejected));
            Assert.That(mismatch, Is.EqualTo(VoidHazardPortResult.Rejected));
            Assert.That(Read<int>(controller, "VoidDamageCount"), Is.EqualTo(1));
            Assert.That(
                Invoke<PlayerHudHealthSnapshot>(adapter, "ExportHudHealth").CurrentHealth,
                Is.EqualTo(65d));
        }

        [UnityTest]
        public IEnumerator PhysicalVoidDamage_UsesAuthorityAndPreservesIdentityOnRestart()
        {
            MonoBehaviour controller = null;
            MonoBehaviour adapter = null;
            yield return LoadComposition(
                value => controller = value,
                value => adapter = value);

            PlayerRuntimeSnapshot before =
                Invoke<PlayerRuntimeSnapshot>(adapter, "ExportSnapshot");
            Transform player = Read<Transform>(controller, "PlayerTransform");
            player.position = new Vector3(-1.5f, 4.2f, 0f);
            float deadline = Time.time + 1f;
            while (Time.time < deadline
                && Invoke<PlayerHudHealthSnapshot>(adapter, "ExportHudHealth").CurrentHealth
                    == 100d)
            {
                yield return new WaitForFixedUpdate();
            }
            Assert.That(
                Invoke<PlayerHudHealthSnapshot>(adapter, "ExportHudHealth").CurrentHealth,
                Is.EqualTo(65d));
            Assert.That(Read<int>(controller, "VoidDamageCount"), Is.EqualTo(1));

            Invoke<PlayerRuntimeRestartResult>(controller, "QuickRestart");
            PlayerRuntimeSnapshot after =
                Invoke<PlayerRuntimeSnapshot>(adapter, "ExportSnapshot");
            Assert.That(after.Player.ActorInstanceId,
                Is.EqualTo(before.Player.ActorInstanceId));
            Assert.That(after.Player.CurrentHealth, Is.EqualTo(100d));
            Assert.That(after.Player.LifecycleGeneration,
                Is.EqualTo(before.Player.LifecycleGeneration + 1L));
        }

        private static IEnumerator LoadComposition(
            Action<MonoBehaviour> assignController,
            Action<MonoBehaviour> assignAdapter)
        {
            AsyncOperation operation = EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            while (!operation.isDone)
            {
                yield return null;
            }
            yield return null;
            MonoBehaviour controller =
                UnityEngine.Object.FindFirstObjectByType(FindType(ControllerTypeName))
                as MonoBehaviour;
            MonoBehaviour adapter =
                UnityEngine.Object.FindFirstObjectByType(FindType(LiveAdapterTypeName))
                as MonoBehaviour;
            Assert.That(controller, Is.Not.Null);
            Assert.That(adapter, Is.Not.Null);
            Assert.That(Read<bool>(adapter, "IsInitialized"), Is.True);
            assignController(controller);
            assignAdapter(adapter);
        }

        private static T Read<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(target);
        }

        private static T Invoke<T>(
            object target,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, methodName);
            object result = method.Invoke(target, arguments);
            return result == null ? default(T) : (T)result;
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }
            throw new InvalidOperationException("Required type not found: " + fullName);
        }
    }
}
#endif
