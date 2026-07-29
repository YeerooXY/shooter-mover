using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.GameplayEntities.Enemies;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShooterMover.UnityAdapters.Enemies
{
    /// <summary>
    /// Live driver for enemies whose authored attack set consists entirely of supported
    /// travelling projectile attacks. It contains no enemy, policy or projectile-name switch.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyAttack : MonoBehaviour
    {
        private const string PlayerMarkerTypeName =
            "ShooterMover.UI.Game.PlayerMarker";
        private const int PlayerAcquisitionIntervalFixedTicks = 5;
        private const int PlayerAcquisitionAttemptLimit = 60;

        private static readonly StableId PlayerFactionId =
            StableId.Create("faction", "gameplay-player");

        private readonly List<PendingShot> pending = new List<PendingShot>();
        private readonly Dictionary<StableId, EnemyBullet> live =
            new Dictionary<StableId, EnemyBullet>();
        private readonly Dictionary<StableId, EnemyLiveAttackBinding> supported =
            new Dictionary<StableId, EnemyLiveAttackBinding>();

        private Enemy actor;
        private EnemyInstance runtime;
        private PlayerBinding player;
        private Rigidbody2D body;
        private long revision;
        private long tick;
        private double seconds;
        private double step;
        private Vector2 facing = Vector2.right;
        private bool translationLocked;
        private bool stopped;
        private string lastDiagnostic;
        private LineRenderer telegraph;
        private Material telegraphMaterial;
        private Texture2D shotTexture;
        private Sprite shotSprite;
        private int playerAcquisitionAttempts;
        private int playerAcquisitionWaitTicks;
        private string pendingPlayerDiagnostic;

        public event Action<EnemyHit> Hit;

        public bool IsBound
        {
            get
            {
                return actor != null
                    && runtime != null
                    && actor.IsBound
                    && ReferenceEquals(actor.Runtime, runtime)
                    && revision == runtime.LifecycleGeneration;
            }
        }

        public bool IsTerminalStopped { get { return stopped; } }
        public long PresentationRevision { get { return revision; } }

        public static bool Supports(EnemyInstance value)
        {
            if (value == null || value.Attacks == null || value.Attacks.Count == 0)
            {
                return false;
            }
            for (int index = 0; index < value.Attacks.Count; index++)
            {
                if (!IsSupportedShot(value.Attacks[index]))
                {
                    return false;
                }
            }
            return true;
        }

        public void Bind(Enemy boundActor, long presentationRevision)
        {
            if (boundActor == null) throw new ArgumentNullException(nameof(boundActor));
            if (presentationRevision <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(presentationRevision));
            }
            if (!boundActor.IsBound || boundActor.Runtime == null)
            {
                throw new InvalidOperationException("enemy-attack-requires-live-runtime");
            }

            EnemyInstance next = boundActor.Runtime;
            if (!Supports(next))
            {
                throw new InvalidOperationException("enemy-attack-mechanics-unsupported");
            }
            if (next.LifecycleGeneration != presentationRevision)
            {
                throw new InvalidOperationException("enemy-attack-revision-mismatch");
            }

            if (ReferenceEquals(actor, boundActor)
                && ReferenceEquals(runtime, next)
                && revision == presentationRevision
                && !stopped)
            {
                return;
            }

            Stop(false);
            actor = boundActor;
            runtime = next;
            revision = presentationRevision;
            BuildSupported(next);
            player = null;
            body = EnsureBody(boundActor.gameObject, out translationLocked);
            EnsureCollider(boundActor.gameObject);
            EnsureVisuals();

            Vector2 initial = boundActor.transform.right;
            facing = initial.sqrMagnitude > 0.000001f ? initial.normalized : Vector2.right;
            tick = 0L;
            seconds = 0d;
            step = Time.fixedDeltaTime;
            if (double.IsNaN(step) || double.IsInfinity(step) || step <= 0d)
            {
                throw new InvalidOperationException("enemy-attack-fixed-step-invalid");
            }
            playerAcquisitionAttempts = 0;
            playerAcquisitionWaitTicks = 0;
            pendingPlayerDiagnostic = "enemy-attack-player-missing";
            stopped = false;
            lastDiagnostic = null;
            if (CanWriteBodyMotion())
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
            body.simulated = true;
            enabled = true;
        }

        internal bool TryAccept(
            EnemyAttackSequenceDispatch sequence,
            out string diagnostic)
        {
            diagnostic = null;
            if (sequence == null)
            {
                diagnostic = "enemy-attack-sequence-missing";
                return false;
            }
            if (!IsCurrent())
            {
                diagnostic = "enemy-attack-sequence-stale";
                return false;
            }
            if (sequence.Execution.Identity.EntityInstanceId != actor.ActorStableId
                || sequence.Execution.LifecycleGeneration != revision)
            {
                diagnostic = "enemy-attack-sequence-source-mismatch";
                return false;
            }
            if (sequence.Execution.ExecutionKind != EnemyAttackExecutionKind.Projectile)
            {
                diagnostic = "enemy-attack-sequence-kind-unsupported";
                return false;
            }

            EnemyLiveAttackBinding binding;
            StableId attackId = sequence.Execution.Descriptor.AttackId;
            if (!supported.TryGetValue(attackId, out binding)
                || binding == null
                || (!ReferenceEquals(sequence.Execution.Descriptor, binding.Descriptor)
                    && sequence.Execution.Descriptor.AttackId != binding.Descriptor.AttackId))
            {
                diagnostic = "enemy-attack-sequence-attack-unsupported";
                return false;
            }

            int expected;
            try
            {
                expected = checked(
                    binding.Descriptor.ShootingPattern.ShotsPerSequence
                    * binding.Descriptor.ShootingPattern.ProjectilesPerShot);
            }
            catch (OverflowException)
            {
                diagnostic = "enemy-attack-sequence-size-invalid";
                return false;
            }
            if (sequence.Emissions.Count != expected || expected < 1)
            {
                diagnostic = "enemy-attack-sequence-emission-count-mismatch";
                return false;
            }

            var incoming = new List<EnemyAttackEffectEmission>(sequence.Emissions.Count);
            var ids = new HashSet<StableId>();
            for (int index = 0; index < sequence.Emissions.Count; index++)
            {
                EnemyAttackEffectEmission emission = sequence.Emissions[index];
                if (!ValidateEmission(emission, sequence, binding, out diagnostic))
                {
                    return false;
                }
                if (!ids.Add(emission.EmissionStableId) || Contains(emission.EmissionStableId))
                {
                    diagnostic = "enemy-attack-shot-identity-duplicate";
                    return false;
                }
                incoming.Add(emission);
            }

            for (int index = 0; index < incoming.Count; index++)
            {
                pending.Add(new PendingShot(incoming[index]));
            }
            pending.Sort(PendingShot.Compare);
            RefreshTelegraph();
            return true;
        }

        internal bool TryCancel(
            EnemyAttackSequenceCancellationFact cancellation,
            out string diagnostic)
        {
            diagnostic = null;
            if (cancellation == null)
            {
                diagnostic = "enemy-attack-cancellation-missing";
                return false;
            }
            if (actor == null
                || runtime == null
                || cancellation.SourceEntityStableId != runtime.SpawnStableId
                || cancellation.SourceLifecycleGeneration != revision)
            {
                diagnostic = "enemy-attack-cancellation-source-mismatch";
                return false;
            }

            var ids = new HashSet<StableId>(cancellation.CancelledProjectileStableIds);
            for (int index = pending.Count - 1; index >= 0; index--)
            {
                if (ids.Contains(pending[index].Emission.EmissionStableId))
                {
                    pending.RemoveAt(index);
                }
            }
            foreach (StableId id in ids)
            {
                EnemyBullet shot;
                if (live.TryGetValue(id, out shot) && shot != null)
                {
                    shot.Cancel();
                }
                live.Remove(id);
            }

            if (runtime.ActorState == null || !runtime.ActorState.IsActive)
            {
                Stop(true);
            }
            RefreshTelegraph();
            return true;
        }

        internal void Report(string diagnostic)
        {
            if (string.IsNullOrWhiteSpace(diagnostic)
                || string.Equals(lastDiagnostic, diagnostic, StringComparison.Ordinal))
            {
                return;
            }
            lastDiagnostic = diagnostic;
            Debug.LogError(diagnostic, this);
        }

        private void FixedUpdate()
        {
            try
            {
                TickPresentation();
            }
            catch (Exception exception)
            {
                if (IsFatal(exception)) throw;
                Report(
                    "enemy-attack-presentation-exception:"
                    + exception.GetType().Name
                    + ":"
                    + exception.Message);
                Debug.LogException(exception, this);
                Stop(true);
            }
        }

        private void TickPresentation()
        {
            if (!IsCurrent() || !actor.IsAlive || !runtime.ActorState.IsActive)
            {
                Stop(true);
                return;
            }
            if (!EnsurePlayerBinding())
            {
                if (CanWriteBodyMotion())
                {
                    body.linearVelocity = Vector2.zero;
                }
                return;
            }

            tick = checked(tick + 1L);
            seconds = tick * step;
            SpawnDue();

            Vector2 position = body.position;
            Vector2 targetPosition = player.Position;
            var candidate = new EnemyPerceptionCandidate(
                player.EntityStableId,
                PlayerFactionId,
                EnemyTargetRelationship.Hostile,
                ToEnemy(targetPosition),
                ToEnemy(player.Velocity),
                HasLineOfSight(position, targetPosition));
            EnemyPerceptionSnapshot perception = EnemyPerceptionBuilder.Build(
                ToEnemy(position),
                ToEnemy(facing),
                new[] { candidate },
                runtime.Definition.DetectionRadius,
                runtime.Definition.VisionArcDegrees,
                tick);

            EnemyPlacementDecision decision = runtime.Evaluate(perception);
            var movementContext = new EnemyMovementRealizationContext(
                runtime.SpawnStableId,
                runtime.RoomStableId,
                ToEnemy(position),
                ToEnemy(facing),
                tick,
                runtime.DifficultyScaling.MovementMultiplier,
                null);
            EnemyMovementRealization movement =
                runtime.RealizeMovement(decision, movementContext);

            EnemyAttackIntent requested = decision.Evaluation.Decision.RequestedAttack;
            if (requested != null)
            {
                EnemyAttackExecutionResult result = runtime.TryExecuteAttack(
                    decision,
                    BuildOperationId(requested.DecisionId, tick),
                    seconds);
                if (result.Status == EnemyLiveOperationStatus.Rejected
                    && result.Rejection != EnemyLiveRejectionCode.CooldownActive)
                {
                    Report("enemy-attack-rejected:" + result.Rejection.ToString());
                }
            }

            // TryExecuteAttack synchronously dispatches accepted emissions into pending. Applying
            // movement afterwards makes the acceptance tick part of the hold without introducing
            // another decision authority.
            ApplyMovement(movement);
            RefreshTelegraph();
        }

        private bool EnsurePlayerBinding()
        {
            Scene scene = gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Stop(true);
                return false;
            }
            if (player != null)
            {
                if (player.IsCurrent(scene))
                {
                    return true;
                }
                Report("enemy-attack-player-binding-lost");
                Stop(true);
                return false;
            }
            if (playerAcquisitionWaitTicks > 0)
            {
                playerAcquisitionWaitTicks--;
                return false;
            }

            playerAcquisitionAttempts++;
            PlayerBinding acquired;
            string diagnostic;
            PlayerAcquisitionStatus status = InspectPlayer(
                scene,
                out acquired,
                out diagnostic);
            if (status == PlayerAcquisitionStatus.Ready)
            {
                player = acquired;
                pendingPlayerDiagnostic = null;
                return true;
            }
            if (status == PlayerAcquisitionStatus.Duplicate
                || status == PlayerAcquisitionStatus.Invalid)
            {
                Report(diagnostic);
                Stop(true);
                return false;
            }

            pendingPlayerDiagnostic = string.IsNullOrWhiteSpace(diagnostic)
                ? "enemy-attack-player-missing"
                : diagnostic;
            if (playerAcquisitionAttempts >= PlayerAcquisitionAttemptLimit)
            {
                Report(
                    "enemy-attack-player-acquisition-timeout:"
                    + pendingPlayerDiagnostic);
                Stop(true);
                return false;
            }
            playerAcquisitionWaitTicks = PlayerAcquisitionIntervalFixedTicks - 1;
            return false;
        }

        private void ApplyMovement(EnemyMovementRealization movement)
        {
            if (movement == null)
            {
                throw new InvalidOperationException("enemy-attack-movement-missing");
            }

            Vector2 velocity = ToUnity(movement.DesiredVelocity);
            if (CanWriteBodyMotion() && (translationLocked || HasTelegraph()))
            {
                // An authored stationary body never translates. A committed dangerous wind-up is
                // also a hard translation hold for mobile bodies.
                body.linearVelocity = Vector2.zero;
            }
            else if (CanWriteBodyMotion())
            {
                float acceleration =
                    (float)(runtime.Movement.Configuration.Acceleration
                        * runtime.DifficultyScaling.MovementMultiplier);
                body.linearVelocity = Vector2.MoveTowards(
                    body.linearVelocity,
                    velocity,
                    acceleration * Time.fixedDeltaTime);
            }

            Vector2 desired = ToUnity(movement.DesiredFacing);
            if (desired.sqrMagnitude <= 0.000001f)
            {
                return;
            }
            desired.Normalize();
            float currentAngle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
            float desiredAngle = Mathf.Atan2(desired.y, desired.x) * Mathf.Rad2Deg;
            float maximumTurn =
                (float)runtime.Movement.Configuration.TurnRateDegreesPerSecond
                * Time.fixedDeltaTime;
            float nextAngle = Mathf.MoveTowardsAngle(
                currentAngle,
                desiredAngle,
                maximumTurn);
            float radians = nextAngle * Mathf.Deg2Rad;
            facing = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            if (!translationLocked && CanWriteBodyMotion())
            {
                body.MoveRotation(nextAngle);
            }
        }

        private bool CanWriteBodyMotion()
        {
            return body != null
                && body.bodyType != RigidbodyType2D.Static;
        }

        private void SpawnDue()
        {
            while (pending.Count > 0
                && pending[0].Emission.ScheduledAtSeconds <= seconds)
            {
                EnemyAttackEffectEmission emission = pending[0].Emission;
                pending.RemoveAt(0);
                Spawn(emission);
            }
        }

        private void Spawn(EnemyAttackEffectEmission emission)
        {
            if (!IsCurrent() || !actor.IsAlive || player == null)
            {
                return;
            }

            EnemyProjectilePayload payload = emission.Projectile.Payload;
            EnemyVector2 origin = emission.CommittedIntent.CommittedOrigin;
            Vector2 direction = ToUnity(emission.CommittedIntent.CommittedDirection);
            if (direction.sqrMagnitude <= 0.000001f)
            {
                Report("enemy-attack-shot-direction-zero");
                return;
            }
            direction.Normalize();
            if (emission.Projectile.SpreadOffsetDegrees != 0d)
            {
                direction = Quaternion.Euler(
                        0f,
                        0f,
                        (float)emission.Projectile.SpreadOffsetDegrees)
                    * direction;
            }

            GameObject shotObject = new GameObject("Enemy Shot");
            shotObject.transform.SetParent(transform.parent, true);
            shotObject.transform.position = new Vector3(
                (float)origin.X,
                (float)origin.Y,
                transform.position.z);
            EnemyBullet shot = shotObject.AddComponent<EnemyBullet>();
            shot.Bind(this, actor, player, emission, direction, payload, shotSprite);
            live.Add(emission.EmissionStableId, shot);
        }

        internal void Publish(
            EnemyAttackEffectEmission emission,
            Collider2D targetCollider)
        {
            if (emission == null || targetCollider == null || player == null)
            {
                return;
            }
            StableId contactId = StableId.Create(
                "enemy-projectile-contact",
                "hit-" + Hash64(
                    emission.EmissionStableId.ToString()
                    + "|"
                    + player.EntityStableId.ToString()).ToString(
                        "x16",
                        CultureInfo.InvariantCulture));
            PublishFact(contactId, emission, targetCollider);
        }

        internal void PublishArea(
            EnemyAttackEffectEmission emission,
            Vector2 center,
            EnemyAreaPayload area)
        {
            if (emission == null
                || area == null
                || player == null
                || !Finite(center))
            {
                return;
            }

            ShowAreaPulse(center, (float)area.Radius);
            Collider2D targetCollider;
            double distanceSquared;
            if (!player.TryResolveAreaContact(
                    center,
                    (float)area.Radius,
                    out targetCollider,
                    out distanceSquared))
            {
                return;
            }

            StableId contactId = StableId.Create(
                "enemy-area-contact",
                "hit-" + Hash64(
                    emission.EmissionStableId.ToString()
                    + "|"
                    + player.EntityStableId.ToString()).ToString(
                        "x16",
                        CultureInfo.InvariantCulture));
            PublishFact(contactId, emission, targetCollider);
        }

        private void PublishFact(
            StableId contactId,
            EnemyAttackEffectEmission emission,
            Collider2D targetCollider)
        {
            if (contactId == null
                || emission == null
                || targetCollider == null
                || player == null)
            {
                return;
            }
            var fact = new EnemyHit(
                contactId,
                emission,
                player.EntityStableId,
                targetCollider);

            Action<EnemyHit> handler = Hit;
            if (handler == null) return;
            Delegate[] subscribers = handler.GetInvocationList();
            for (int index = 0; index < subscribers.Length; index++)
            {
                try
                {
                    ((Action<EnemyHit>)subscribers[index])(fact);
                }
                catch (Exception exception)
                {
                    if (IsFatal(exception)) throw;
                    Debug.LogException(exception, this);
                }
            }
        }

        internal void Ended(StableId id)
        {
            if (id != null)
            {
                live.Remove(id);
            }
        }

        private bool ValidateEmission(
            EnemyAttackEffectEmission emission,
            EnemyAttackSequenceDispatch sequence,
            EnemyLiveAttackBinding binding,
            out string diagnostic)
        {
            diagnostic = null;
            if (emission == null
                || emission.Kind != EnemyAttackEffectEmissionKind.Projectile
                || emission.Projectile == null)
            {
                diagnostic = "enemy-attack-emission-projectile-required";
                return false;
            }
            if (emission.SequenceStableId != sequence.DispatchStableId
                || emission.SourceEntityStableId != runtime.SpawnStableId
                || emission.SourceLifecycleGeneration != revision
                || emission.AttackStableId != binding.Descriptor.AttackId)
            {
                diagnostic = "enemy-attack-emission-lifecycle-mismatch";
                return false;
            }
            EnemyProjectilePayload payload = emission.Projectile.Payload;
            if (!IsSupportedPayload(payload))
            {
                diagnostic = "enemy-attack-emission-mechanics-unsupported";
                return false;
            }
            if (emission.ScheduledAtSeconds < sequence.Sequence.StartedAtSeconds)
            {
                diagnostic = "enemy-attack-emission-time-invalid";
                return false;
            }
            if (emission.CommittedIntent.CommittedDirection.Length <= 0d)
            {
                diagnostic = "enemy-attack-emission-direction-zero";
                return false;
            }
            return true;
        }

        private static bool IsSupportedShot(EnemyLiveAttackBinding binding)
        {
            if (binding == null
                || binding.Descriptor == null
                || binding.Capability == null
                || binding.Capability.Configuration == null)
            {
                return false;
            }
            EnemyAttackCapabilityDescriptor descriptor = binding.Descriptor;
            EnemyShootingPattern shooting = descriptor.ShootingPattern;
            return binding.Capability.Configuration.ExecutionKind
                    == EnemyAttackExecutionKind.Projectile
                && shooting != null
                && descriptor.MeleePattern == null
                && descriptor.Damage > 0d
                && shooting.ShotsPerSequence > 0
                && shooting.ProjectilesPerShot > 0
                && shooting.SequenceAimPolicy
                    == EnemySequenceAimPolicy.LockAtSequenceStart
                && IsSupportedPayload(descriptor.ProjectilePayload);
        }

        private static bool IsSupportedPayload(EnemyProjectilePayload payload)
        {
            if (payload == null
                || payload.ProjectileProfileId == null
                || payload.PierceCount != 0
                || !IsFinitePositive(payload.Speed)
                || !IsFinitePositive(payload.MaximumTravelDistance)
                || !IsFinitePositive(payload.CollisionRadius))
            {
                return false;
            }

            EnemyAreaPayload area = payload.AreaPayload;
            return area == null
                || (area.DurationSeconds == 0d
                    && area.MaximumTargets > 0
                    && IsFinitePositive(area.Radius));
        }

        private static bool IsFinitePositive(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d;
        }

        private void BuildSupported(EnemyInstance value)
        {
            supported.Clear();
            for (int index = 0; index < value.Attacks.Count; index++)
            {
                EnemyLiveAttackBinding binding = value.Attacks[index];
                if (!IsSupportedShot(binding))
                {
                    throw new InvalidOperationException("enemy-attack-mechanics-unsupported");
                }
                StableId attackId = binding.Descriptor.AttackId;
                if (supported.ContainsKey(attackId))
                {
                    throw new InvalidOperationException("enemy-attack-id-duplicated");
                }
                supported.Add(attackId, binding);
            }
        }

        private PlayerAcquisitionStatus InspectPlayer(
            Scene scene,
            out PlayerBinding result,
            out string diagnostic)
        {
            result = null;
            diagnostic = null;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                diagnostic = "enemy-attack-player-scene-invalid";
                return PlayerAcquisitionStatus.Invalid;
            }

            MonoBehaviour marker = null;
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
            Collider2D[] playerColliders = marker.GetComponentsInChildren<Collider2D>(true);
            if (playerBody == null || playerColliders == null || playerColliders.Length == 0)
            {
                diagnostic = "enemy-attack-player-physics-missing";
                return PlayerAcquisitionStatus.Pending;
            }

            try
            {
                result = new PlayerBinding(marker, entityId, playerBody, playerColliders);
                return PlayerAcquisitionStatus.Ready;
            }
            catch (ArgumentException)
            {
                diagnostic = "enemy-attack-player-collider-missing";
                return PlayerAcquisitionStatus.Pending;
            }
        }

        private bool HasLineOfSight(Vector2 origin, Vector2 target)
        {
            RaycastHit2D[] hits = Physics2D.LinecastAll(origin, target);
            for (int index = 0; index < hits.Length; index++)
            {
                Collider2D hit = hits[index].collider;
                if (hit == null
                    || hit.isTrigger
                    || IsOwned(hit)
                    || player.IsTarget(hit))
                {
                    continue;
                }
                return false;
            }
            return true;
        }

        private bool IsOwned(Collider2D collider)
        {
            return collider != null
                && (collider.transform == actor.transform
                    || collider.transform.IsChildOf(actor.transform));
        }

        private bool IsCurrent()
        {
            return !stopped
                && actor != null
                && runtime != null
                && actor.IsBound
                && ReferenceEquals(actor.Runtime, runtime)
                && actor.LifecycleGeneration == revision
                && runtime.LifecycleGeneration == revision;
        }

        private bool Contains(StableId id)
        {
            if (id == null) return false;
            if (live.ContainsKey(id)) return true;
            for (int index = 0; index < pending.Count; index++)
            {
                if (pending[index].Emission.EmissionStableId == id)
                {
                    return true;
                }
            }
            return false;
        }

        private bool HasTelegraph()
        {
            return pending.Count > 0
                && pending[0].Emission.ScheduledAtSeconds > seconds;
        }

        private void RefreshTelegraph()
        {
            if (telegraph == null) return;
            if (!IsCurrent() || !HasTelegraph())
            {
                telegraph.enabled = false;
                return;
            }

            EnemyAttackEffectEmission emission = pending[0].Emission;
            EnemyVector2 origin = emission.CommittedIntent.CommittedOrigin;
            Vector2 direction = ToUnity(
                emission.CommittedIntent.CommittedDirection).normalized;
            float length = (float)Math.Min(
                emission.Projectile.Payload.MaximumTravelDistance,
                runtime.Definition.DetectionRadius);
            telegraph.SetPosition(
                0,
                new Vector3((float)origin.X, (float)origin.Y, transform.position.z));
            telegraph.SetPosition(
                1,
                new Vector3(
                    (float)origin.X + direction.x * length,
                    (float)origin.Y + direction.y * length,
                    transform.position.z));
            telegraph.enabled = true;
        }

        private void EnsureVisuals()
        {
            if (shotSprite == null)
            {
                shotTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                shotTexture.name = "Enemy Shot Pixel";
                shotTexture.SetPixel(0, 0, Color.white);
                shotTexture.Apply(false, true);
                shotSprite = Sprite.Create(
                    shotTexture,
                    new Rect(0f, 0f, 1f, 1f),
                    new Vector2(0.5f, 0.5f),
                    1f);
                shotSprite.name = "Enemy Shot Sprite";
            }

            if (telegraph == null)
            {
                GameObject lineObject = new GameObject("Enemy Telegraph");
                lineObject.transform.SetParent(transform, false);
                telegraph = lineObject.AddComponent<LineRenderer>();
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null)
                {
                    throw new InvalidOperationException("enemy-attack-telegraph-shader-missing");
                }
                telegraphMaterial = new Material(shader);
                telegraphMaterial.name = "Enemy Telegraph Material";
                telegraph.sharedMaterial = telegraphMaterial;
                telegraph.positionCount = 2;
                telegraph.useWorldSpace = true;
                telegraph.startWidth = 0.09f;
                telegraph.endWidth = 0.035f;
                telegraph.startColor = new Color(1f, 0.25f, 0.1f, 0.95f);
                telegraph.endColor = new Color(1f, 0.85f, 0.15f, 0.65f);
                telegraph.sortingOrder = 250;
                telegraph.enabled = false;
            }
        }

        private void ShowAreaPulse(Vector2 center, float radius)
        {
            if (shotSprite == null || !Finite(center) || !float.IsFinite(radius) || radius <= 0f)
            {
                return;
            }
            GameObject pulse = new GameObject("Enemy Area Pulse");
            pulse.transform.SetParent(transform.parent, true);
            pulse.transform.position = new Vector3(center.x, center.y, transform.position.z);
            pulse.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
            SpriteRenderer renderer = pulse.AddComponent<SpriteRenderer>();
            renderer.sprite = shotSprite;
            renderer.color = new Color(1f, 0.18f, 0.04f, 0.28f);
            renderer.sortingOrder = 290;
            Destroy(pulse, 0.12f);
        }

        private static Rigidbody2D EnsureBody(
            GameObject target,
            out bool authoredStationary)
        {
            Rigidbody2D result = target.GetComponent<Rigidbody2D>()
                ?? target.AddComponent<Rigidbody2D>();
            authoredStationary = result.bodyType == RigidbodyType2D.Static;
            if (!authoredStationary)
            {
                result.bodyType = RigidbodyType2D.Dynamic;
            }
            result.gravityScale = 0f;
            result.linearDamping = 8f;
            result.angularDamping = 8f;
            result.interpolation = RigidbodyInterpolation2D.Interpolate;
            result.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            result.constraints = authoredStationary
                ? RigidbodyConstraints2D.FreezeAll
                : RigidbodyConstraints2D.None;
            result.simulated = true;
            return result;
        }

        private static void EnsureCollider(GameObject target)
        {
            Collider2D collider = target.GetComponentInChildren<Collider2D>(true);
            if (collider != null) return;
            CircleCollider2D fallback = target.AddComponent<CircleCollider2D>();
            fallback.radius = 0.45f;
            fallback.isTrigger = false;
        }

        private StableId BuildOperationId(StableId decisionId, long currentTick)
        {
            return StableId.Create(
                "enemy-attack-operation",
                "attack-" + Hash64(
                    runtime.SpawnStableId.ToString()
                    + "|"
                    + revision.ToString(CultureInfo.InvariantCulture)
                    + "|"
                    + currentTick.ToString(CultureInfo.InvariantCulture)
                    + "|"
                    + decisionId.ToString()).ToString(
                        "x16",
                        CultureInfo.InvariantCulture));
        }

        private void Stop(bool markStopped)
        {
            pending.Clear();
            supported.Clear();
            if (telegraph != null) telegraph.enabled = false;

            var shots = new List<EnemyBullet>(live.Values);
            live.Clear();
            for (int index = 0; index < shots.Count; index++)
            {
                if (shots[index] != null)
                {
                    shots[index].Cancel();
                }
            }

            if (CanWriteBodyMotion())
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
            player = null;
            translationLocked = false;
            playerAcquisitionAttempts = 0;
            playerAcquisitionWaitTicks = 0;
            pendingPlayerDiagnostic = null;
            if (markStopped)
            {
                stopped = true;
                enabled = false;
            }
        }

        private void OnDisable()
        {
            if (!stopped && actor != null && !IsCurrent())
            {
                Stop(true);
            }
        }

        private void OnDestroy()
        {
            Stop(true);
            if (telegraphMaterial != null) Destroy(telegraphMaterial);
            if (shotSprite != null) Destroy(shotSprite);
            if (shotTexture != null) Destroy(shotTexture);
            telegraphMaterial = null;
            shotSprite = null;
            shotTexture = null;
            actor = null;
            runtime = null;
            player = null;
        }

        private static EnemyVector2 ToEnemy(Vector2 value)
        {
            return new EnemyVector2(value.x, value.y);
        }

        private static Vector2 ToUnity(EnemyVector2 value)
        {
            return new Vector2((float)value.X, (float)value.Y);
        }

        private static bool Finite(Vector2 value)
        {
            return !float.IsNaN(value.x)
                && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y)
                && !float.IsInfinity(value.y);
        }

        private static ulong Hash64(string value)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            string normalized = value ?? string.Empty;
            for (int index = 0; index < normalized.Length; index++)
            {
                hash ^= normalized[index];
                hash *= prime;
            }
            return hash;
        }

        private static bool IsFatal(Exception exception)
        {
            return exception is OutOfMemoryException
                || exception is StackOverflowException
                || exception is AccessViolationException;
        }

        private sealed class PendingShot
        {
            public PendingShot(EnemyAttackEffectEmission emission)
            {
                Emission = emission ?? throw new ArgumentNullException(nameof(emission));
            }

            public EnemyAttackEffectEmission Emission { get; }

            public static int Compare(PendingShot left, PendingShot right)
            {
                int time = left.Emission.ScheduledAtSeconds.CompareTo(
                    right.Emission.ScheduledAtSeconds);
                return time != 0
                    ? time
                    : left.Emission.EmissionStableId.CompareTo(
                        right.Emission.EmissionStableId);
            }
        }

        internal sealed class PlayerBinding
        {
            private readonly Collider2D[] colliders;

            public PlayerBinding(
                MonoBehaviour marker,
                StableId entityStableId,
                Rigidbody2D body,
                IEnumerable<Collider2D> targetColliders)
            {
                Marker = marker ?? throw new ArgumentNullException(nameof(marker));
                EntityStableId = entityStableId
                    ?? throw new ArgumentNullException(nameof(entityStableId));
                Body = body ?? throw new ArgumentNullException(nameof(body));
                if (targetColliders == null)
                    throw new ArgumentNullException(nameof(targetColliders));

                var values = new List<Collider2D>();
                var ids = new HashSet<int>();
                foreach (Collider2D collider in targetColliders)
                {
                    if (collider != null && ids.Add(collider.GetInstanceID()))
                    {
                        values.Add(collider);
                    }
                }
                if (values.Count == 0)
                {
                    throw new ArgumentException(
                        "At least one player collider is required.",
                        nameof(targetColliders));
                }
                values.Sort((left, right) =>
                    left.GetInstanceID().CompareTo(right.GetInstanceID()));
                colliders = values.ToArray();
            }

            public MonoBehaviour Marker { get; }
            public StableId EntityStableId { get; }
            public Rigidbody2D Body { get; }
            public Vector2 Position { get { return Body.position; } }
            public Vector2 Velocity { get { return Body.linearVelocity; } }

            public bool IsTarget(Collider2D collider)
            {
                return collider != null
                    && Marker != null
                    && (collider.transform == Marker.transform
                        || collider.transform.IsChildOf(Marker.transform));
            }

            public bool TryResolveAreaContact(
                Vector2 center,
                float radius,
                out Collider2D targetCollider,
                out double distanceSquared)
            {
                targetCollider = null;
                distanceSquared = double.PositiveInfinity;
                if (!Finite(center)
                    || float.IsNaN(radius)
                    || float.IsInfinity(radius)
                    || radius <= 0f)
                {
                    return false;
                }

                for (int index = 0; index < colliders.Length; index++)
                {
                    Collider2D collider = colliders[index];
                    if (collider == null
                        || !collider.enabled
                        || !collider.gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    Vector2 closest = collider.ClosestPoint(center);
                    if (!Finite(closest)) continue;
                    double candidate = (closest - center).sqrMagnitude;
                    if (candidate < distanceSquared)
                    {
                        distanceSquared = candidate;
                        targetCollider = collider;
                    }
                }

                return targetCollider != null
                    && distanceSquared <= radius * radius;
            }

            public bool IsCurrent(Scene scene)
            {
                return scene.IsValid()
                    && scene.isLoaded
                    && Marker != null
                    && Marker.isActiveAndEnabled
                    && Body != null
                    && Marker.gameObject.scene == scene
                    && EntityStableId != null;
            }
        }

        private enum PlayerAcquisitionStatus
        {
            Pending = 0,
            Ready = 1,
            Duplicate = 2,
            Invalid = 3
        }
    }
}
