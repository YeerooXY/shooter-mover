using System;
using System.Reflection;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UnityAdapters.Enemies
{
    /// <summary>
    /// Defers player-dependent enemy attack presentation until the production player exists.
    /// It also applies the committed wind-up translation hold after the canonical driver tick.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    internal sealed class EnemyAttackBinding2D : MonoBehaviour
    {
        private const string PlayerMarkerTypeName =
            "ShooterMover.UI.ProductionFlow.PlayablePlayerMarker2D";
        private const int AcquisitionIntervalFixedTicks = 5;
        private const int AcquisitionAttemptLimit = 60;

        private RoomEnemyActor2D actor;
        private EnemyAttack2D attack;
        private Rigidbody2D body;
        private LineRenderer telegraph;
        private long revision;
        private int acquisitionAttempts;
        private int acquisitionWaitTicks;
        private string pendingDiagnostic;
        private string lastDiagnostic;
        private bool failed;

        public EnemyAttack2D CurrentAttack
        {
            get
            {
                return !failed
                    && IsActorCurrent()
                    && attack != null
                    && attack.IsBound
                    && !attack.IsTerminalStopped
                    && attack.PresentationRevision == revision
                        ? attack
                        : null;
            }
        }

        public void Bind(RoomEnemyActor2D boundActor, long presentationRevision)
        {
            if (boundActor == null) throw new ArgumentNullException(nameof(boundActor));
            if (presentationRevision <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(presentationRevision));
            }
            if (!boundActor.IsBound
                || boundActor.Runtime == null
                || boundActor.LifecycleGeneration != presentationRevision
                || boundActor.Runtime.LifecycleGeneration != presentationRevision)
            {
                throw new InvalidOperationException("enemy-attack-binding-requires-live-runtime");
            }

            if (ReferenceEquals(actor, boundActor)
                && revision == presentationRevision
                && !failed)
            {
                return;
            }

            EnemyAttack2D previous = GetComponent<EnemyAttack2D>();
            if (previous != null
                && previous.IsBound
                && previous.PresentationRevision != presentationRevision)
            {
                previous.enabled = false;
            }

            actor = boundActor;
            attack = null;
            body = null;
            telegraph = null;
            revision = presentationRevision;
            acquisitionAttempts = 0;
            acquisitionWaitTicks = 0;
            pendingDiagnostic = "enemy-attack-player-missing";
            lastDiagnostic = null;
            failed = false;
            enabled = true;
        }

        private void FixedUpdate()
        {
            try
            {
                TickBinding();
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                Fail(
                    "enemy-attack-binding-exception:"
                    + exception.GetType().Name
                    + ":"
                    + exception.Message,
                    exception);
            }
        }

        private void TickBinding()
        {
            if (!IsActorCurrent()
                || actor == null
                || !actor.IsAlive
                || actor.Runtime == null
                || actor.Runtime.ActorState == null
                || !actor.Runtime.ActorState.IsActive)
            {
                Deactivate();
                return;
            }

            if (attack == null)
            {
                TryAcquireAndBind();
                return;
            }

            if (attack.IsTerminalStopped
                || !attack.IsBound
                || attack.PresentationRevision != revision)
            {
                Deactivate();
                return;
            }
            if (body == null || telegraph == null)
            {
                Fail("enemy-attack-presentation-binding-lost", null);
                return;
            }

            // EnemyAttack2D has the default execution order. This component runs later in the
            // same FixedUpdate, after an accepted sequence has enabled its telegraph but before
            // the physics step consumes velocity. That makes the acceptance tick part of the
            // hold and hard-stops existing translation for the complete dangerous wind-up.
            if (telegraph.enabled)
            {
                body.linearVelocity = Vector2.zero;
            }
        }

        private void TryAcquireAndBind()
        {
            if (acquisitionWaitTicks > 0)
            {
                acquisitionWaitTicks--;
                return;
            }

            acquisitionAttempts++;
            MonoBehaviour marker;
            string diagnostic;
            PlayerAcquisitionStatus status = InspectPlayer(
                gameObject.scene,
                out marker,
                out diagnostic);
            if (status == PlayerAcquisitionStatus.SceneLost)
            {
                Deactivate();
                return;
            }
            if (status == PlayerAcquisitionStatus.Duplicate
                || status == PlayerAcquisitionStatus.Invalid)
            {
                Fail(diagnostic, null);
                return;
            }
            if (status == PlayerAcquisitionStatus.Pending)
            {
                pendingDiagnostic = string.IsNullOrWhiteSpace(diagnostic)
                    ? "enemy-attack-player-missing"
                    : diagnostic;
                if (acquisitionAttempts >= AcquisitionAttemptLimit)
                {
                    Fail(
                        "enemy-attack-player-acquisition-timeout:"
                        + pendingDiagnostic,
                        null);
                    return;
                }
                acquisitionWaitTicks = AcquisitionIntervalFixedTicks - 1;
                return;
            }

            EnemyAttack2D next = GetComponent<EnemyAttack2D>()
                ?? gameObject.AddComponent<EnemyAttack2D>();
            next.Bind(actor, revision);
            if (!next.IsBound
                || next.IsTerminalStopped
                || next.PresentationRevision != revision)
            {
                throw new InvalidOperationException(
                    "enemy-attack-presentation-bind-rejected");
            }

            Rigidbody2D configuredBody = GetComponent<Rigidbody2D>();
            if (configuredBody == null)
            {
                throw new InvalidOperationException("enemy-attack-rigidbody-missing-after-bind");
            }
            LineRenderer configuredTelegraph =
                next.GetComponentInChildren<LineRenderer>(true);
            if (configuredTelegraph == null)
            {
                throw new InvalidOperationException("enemy-attack-telegraph-missing-after-bind");
            }

            // EnemyAttack2D rotates the Rigidbody. Permit that rotation so its internal facing,
            // the actor transform, visible children and collider orientation remain coherent.
            configuredBody.constraints = RigidbodyConstraints2D.None;
            body = configuredBody;
            telegraph = configuredTelegraph;
            attack = next;
        }

        private PlayerAcquisitionStatus InspectPlayer(
            Scene scene,
            out MonoBehaviour marker,
            out string diagnostic)
        {
            marker = null;
            diagnostic = null;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return PlayerAcquisitionStatus.SceneLost;
            }

            int markerCount = 0;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                MonoBehaviour[] behaviours =
                    roots[rootIndex].GetComponentsInChildren<MonoBehaviour>(true);
                for (int index = 0; index < behaviours.Length; index++)
                {
                    MonoBehaviour candidate = behaviours[index];
                    if (candidate == null
                        || !string.Equals(
                            candidate.GetType().FullName,
                            PlayerMarkerTypeName,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    markerCount++;
                    if (markerCount > 1)
                    {
                        marker = null;
                        diagnostic = "enemy-attack-player-duplicated";
                        return PlayerAcquisitionStatus.Duplicate;
                    }
                    marker = candidate;
                }
            }

            if (marker == null)
            {
                diagnostic = "enemy-attack-player-missing";
                return PlayerAcquisitionStatus.Pending;
            }

            PropertyInfo property = marker.GetType().GetProperty(
                "CharacterInstanceStableId",
                BindingFlags.Instance | BindingFlags.Public);
            if (property == null || !typeof(StableId).IsAssignableFrom(property.PropertyType))
            {
                diagnostic = "enemy-attack-player-marker-contract-missing";
                return PlayerAcquisitionStatus.Invalid;
            }
            StableId entityId = property.GetValue(marker, null) as StableId;
            if (entityId == null)
            {
                diagnostic = "enemy-attack-player-unbound";
                return PlayerAcquisitionStatus.Pending;
            }

            Rigidbody2D playerBody = marker.GetComponent<Rigidbody2D>();
            Collider2D playerCollider = marker.GetComponentInChildren<Collider2D>(true);
            if (playerBody == null || playerCollider == null)
            {
                diagnostic = "enemy-attack-player-physics-missing";
                return PlayerAcquisitionStatus.Pending;
            }

            return PlayerAcquisitionStatus.Ready;
        }

        private bool IsActorCurrent()
        {
            return !failed
                && actor != null
                && actor.IsBound
                && actor.Runtime != null
                && actor.LifecycleGeneration == revision
                && actor.Runtime.LifecycleGeneration == revision;
        }

        private void Deactivate()
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
            enabled = false;
        }

        private void Fail(string diagnostic, Exception exception)
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
            failed = true;
            enabled = false;
            string message = string.IsNullOrWhiteSpace(diagnostic)
                ? "enemy-attack-binding-failed"
                : diagnostic;
            if (string.Equals(lastDiagnostic, message, StringComparison.Ordinal))
            {
                return;
            }
            lastDiagnostic = message;
            if (exception == null)
            {
                Debug.LogError(message, this);
            }
            else
            {
                Debug.LogError(message, this);
                Debug.LogException(exception, this);
            }
        }

        private void OnDisable()
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
        }

        private void OnDestroy()
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
            actor = null;
            attack = null;
            body = null;
            telegraph = null;
        }

        private static bool IsFatal(Exception exception)
        {
            return exception is OutOfMemoryException
                || exception is StackOverflowException
                || exception is AccessViolationException;
        }

        private enum PlayerAcquisitionStatus
        {
            Pending = 0,
            Ready = 1,
            Duplicate = 2,
            Invalid = 3,
            SceneLost = 4
        }
    }
}
