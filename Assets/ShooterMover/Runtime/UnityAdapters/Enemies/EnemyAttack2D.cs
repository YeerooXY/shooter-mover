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
    /// simple travelling shots. It contains no enemy, policy or projectile-name switch.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyAttack2D : MonoBehaviour
    {
        private const string PlayerMarkerTypeName =
            "ShooterMover.UI.ProductionFlow.PlayablePlayerMarker2D";
        private const int PlayerAcquisitionIntervalFixedTicks = 5;
        private const int PlayerAcquisitionAttemptLimit = 60;

        private static readonly StableId PlayerFactionId =
            StableId.Create("faction", "gameplay-player");

        private readonly List<PendingShot> pending = new List<PendingShot>();
        private readonly Dictionary<StableId, EnemyShot2D> live =
            new Dictionary<StableId, EnemyShot2D>();
        private readonly Dictionary<StableId, EnemyRuntimeAttackBindingV1> supported =
            new Dictionary<StableId, EnemyRuntimeAttackBindingV1>();

        private RoomEnemyActor2D actor;
        private EnemyPlacementRuntimeInstanceV1 runtime;
        private PlayerBinding player;
        private Rigidbody2D body;
        private long revision;
        private long tick;
        private double seconds;
        private double step;
        private Vector2 facing = Vector2.right;
        private bool stopped;
        private string lastDiagnostic;
        private LineRenderer telegraph;
        private Material telegraphMaterial;
        private Texture2D shotTexture;
        private Sprite shotSprite;
        private int playerAcquisitionAttempts;
        private int playerAcquisitionWaitTicks;
        private string pendingPlayerDiagnostic;

        public event Action<EnemyHitV1> Hit;

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

        public static bool Supports(EnemyPlacementRuntimeInstanceV1 value)
        {
            if (value == null || value.Attacks == null || value.Attacks.Count == 0)
            {
                return false;
            }
            for (int index = 0; index < value.Attacks.Count; index++)
            {
                if (!IsSimpleShot(value.Attacks[index]))
                {
                    return false;
                }
            }
            return true;
        }

        public void Bind(RoomEnemyActor2D boundActor, long presentationRevision)
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

            EnemyPlacementRuntimeInstanceV1 next = boundActor.Runtime;
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
            body = EnsureBody(boundActor.gameObject);
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
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = true;
            enabled = true;
        }

        internal bool TryAccept(
            EnemyAttackSequenceDispatchV1 sequence,
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
            if (sequence.Execution.ExecutionKind != EnemyAttackExecutionKindV1.Projectile)
            {
                diagnostic = "enemy-attack-sequence-kind-unsupported";
                return false;
            }

            EnemyRuntimeAttackBindingV1 binding;
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

            var incoming = new List<EnemyAttackEffectEmissionV1>(sequence.Emissions.Count);
            var ids = new HashSet<StableId>();
            for (int index = 0; index < sequence.Emissions.Count; index++)
            {
                EnemyAttackEffectEmissionV1 emission = sequence.Emissions[index];
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
            EnemyAttackSequenceCancellationFactV1 cancellation,
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
                EnemyShot2D shot;
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
                if (body != null)
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

            EnemyPlacementDecisionV1 decision = runtime.Evaluate(perception);
            var movementContext = new EnemyMovementRealizationContextV1(
                runtime.SpawnStableId,
                runtime.RoomStableId,
                ToEnemy(position),
                ToEnemy(facing),
                tick,
                runtime.DifficultyScaling.MovementMultiplier,
                null);
            EnemyMovementRealizationV1 movement =
                runtime.RealizeMovement(decision, movementContext);

            EnemyAttackIntent requested = decision.Evaluation.Decision.RequestedAttack;
            if (requested != null)
            {
                EnemyAttackExecutionResultV1 result = runtime.TryExecuteAttack(
                    decision,
                    BuildOperationId(requested.DecisionId, tick),
                    seconds);
                if (result.Status == EnemyRuntimeOperationStatusV1.Rejected
                    && result.Rejection != EnemyRuntimeRejectionCodeV1.CooldownActive)
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

        private void ApplyMovement(EnemyMovementRealizationV1 movement)
        {
            if (movement == null)
            {
                throw new InvalidOperationException("enemy-attack-movement-missing");
            }

            Vector2 velocity = ToUnity(movement.DesiredVelocity);
            if (HasTelegraph())
            {
                // A committed dangerous wind-up is a hard translation hold. Do not decelerate
                // toward zero: clear inherited velocity before the physics step consumes it.
                body.linearVelocity = Vector2.zero;
            }
            else
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
            body.MoveRotation(nextAngle);
        }

        private void SpawnDue()
        {
            while (pending.Count > 0
                && pending[0].Emission.ScheduledAtSeconds <= seconds)
            {
                EnemyAttackEffectEmissionV1 emission = pending[0].Emission;
                pending.RemoveAt(0);
                Spawn(emission);
            }
        }

        private void Spawn(EnemyAttackEffectEmissionV1 emission)
        {
            if (!IsCurrent() || !actor.IsAlive || player == null)
            {
                return;
            }

            EnemyProjectilePayloadV1 payload = emission.Projectile.Payload;
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
            EnemyShot2D shot = shotObject.AddComponent<EnemyShot2D>();
            shot.Bind(this, actor, player, emission, direction, payload, shotSprite);
            live.Add(emission.EmissionStableId, shot);
        }

        internal void Publish(
            EnemyAttackEffectEmissionV1 emission,
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
            var fact = new EnemyHitV1(
                contactId,
                emission,
                player.EntityStableId,
                targetCollider);

            Action<EnemyHitV1> handler = Hit;
            if (handler == null) return;
            Delegate[] subscribers = handler.GetInvocationList();
            for (int index = 0; index < subscribers.Length; index++)
            {
                try
                {
                    ((Action<EnemyHitV1>)subscribers[index])(fact);
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
            EnemyAttackEffectEmissionV1 emission,
            EnemyAttackSequenceDispatchV1 sequence,
            EnemyRuntimeAttackBindingV1 binding,
            out string diagnostic)
        {
            diagnostic = null;
            if (emission == null
                || emission.Kind != EnemyAttackEffectEmissionKindV1.Projectile
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
            EnemyProjectilePayloadV1 payload = emission.Projectile.Payload;
            if (!IsSimplePayload(payload))
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

        private static bool IsSimpleShot(EnemyRuntimeAttackBindingV1 binding)
        {
            if (binding == null
                || binding.Descriptor == null
                || binding.Capability == null
                || binding.Capability.Configuration == null)
            {
                return false;
            }
            EnemyAttackCapabilityDescriptorV1 descriptor = binding.Descriptor;
            EnemyShootingPatternV1 shooting = descriptor.ShootingPattern;
            return binding.Capability.Configuration.ExecutionKind
                    == EnemyAttackExecutionKindV1.Projectile
                && shooting != null
                && descriptor.MeleePattern == null
                && descriptor.Damage > 0d
                && shooting.ShotsPerSequence > 0
                && shooting.ProjectilesPerShot > 0
                && shooting.SequenceAimPolicy
                    == EnemySequenceAimPolicyV1.LockAtSequenceStart
                && IsSimplePayload(descriptor.ProjectilePayload);
        }

        private static bool IsSimplePayload(EnemyProjectilePayloadV1 payload)
        {
            return payload != null
                && payload.ProjectileProfileId != null
                && payload.AreaPayload == null
                && payload.PierceCount == 0
                && IsFinitePositive(payload.Speed)
                && IsFinitePositive(payload.MaximumTravelDistance)
                && IsFinitePositive(payload.CollisionRadius);
        }

        private static bool IsFinitePositive(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0d;
        }

        private void BuildSupported(EnemyPlacementRuntimeInstanceV1 value)
        {
            supported.Clear();
            for (int index = 0; index < value.Attacks.Count; index++)
            {
                EnemyRuntimeAttackBindingV1 binding = value.Attacks[index];
                if (!IsSimpleShot(binding))
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
            Collider2D playerCollider = marker.GetComponentInChildren<Collider2D>(true);
            if (playerBody == null || playerCollider == null)
            {
                diagnostic = "enemy-attack-player-physics-missing";
                return PlayerAcquisitionStatus.Pending;
            }

            result = new PlayerBinding(marker, entityId, playerBody);
            return PlayerAcquisitionStatus.Ready;
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

            EnemyAttackEffectEmissionV1 emission = pending[0].Emission;
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

        private static Rigidbody2D EnsureBody(GameObject target)
        {
            Rigidbody2D result = target.GetComponent<Rigidbody2D>()
                ?? target.AddComponent<Rigidbody2D>();
            result.bodyType = RigidbodyType2D.Dynamic;
            result.gravityScale = 0f;
            result.linearDamping = 8f;
            result.angularDamping = 8f;
            result.interpolation = RigidbodyInterpolation2D.Interpolate;
            result.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            result.constraints = RigidbodyConstraints2D.None;
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

            var shots = new List<EnemyShot2D>(live.Values);
            live.Clear();
            for (int index = 0; index < shots.Count; index++)
            {
                if (shots[index] != null)
                {
                    shots[index].Cancel();
                }
            }

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
            player = null;
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
            public PendingShot(EnemyAttackEffectEmissionV1 emission)
            {
                Emission = emission ?? throw new ArgumentNullException(nameof(emission));
            }

            public EnemyAttackEffectEmissionV1 Emission { get; }

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
            public PlayerBinding(
                MonoBehaviour marker,
                StableId entityStableId,
                Rigidbody2D body)
            {
                Marker = marker ?? throw new ArgumentNullException(nameof(marker));
                EntityStableId = entityStableId
                    ?? throw new ArgumentNullException(nameof(entityStableId));
                Body = body ?? throw new ArgumentNullException(nameof(body));
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
