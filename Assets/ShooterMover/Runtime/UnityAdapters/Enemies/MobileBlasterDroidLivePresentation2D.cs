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
    /// Neutral contact fact emitted by the live enemy-projectile presentation.
    /// Task B deliberately stops at this boundary; combat integration can route the
    /// fact to the run-local player damage receiver without coupling this assembly
    /// to a player-vitals implementation.
    /// </summary>
    public sealed class RoomEnemyProjectileContactV1
    {
        public RoomEnemyProjectileContactV1(
            StableId contactStableId,
            EnemyAttackEffectEmissionV1 emission,
            StableId targetEntityStableId,
            Collider2D targetCollider)
        {
            ContactStableId = contactStableId
                ?? throw new ArgumentNullException(nameof(contactStableId));
            Emission = emission ?? throw new ArgumentNullException(nameof(emission));
            TargetEntityStableId = targetEntityStableId
                ?? throw new ArgumentNullException(nameof(targetEntityStableId));
            TargetCollider = targetCollider
                ?? throw new ArgumentNullException(nameof(targetCollider));
        }

        public StableId ContactStableId { get; }
        public EnemyAttackEffectEmissionV1 Emission { get; }
        public StableId ProjectileStableId
        {
            get { return Emission.EmissionStableId; }
        }
        public StableId AttackOperationStableId
        {
            get { return Emission.Execution.OperationStableId; }
        }
        public StableId SourceEntityStableId
        {
            get { return Emission.SourceEntityStableId; }
        }
        public StableId SourceRunParticipantStableId
        {
            get { return Emission.SourceRunParticipantStableId; }
        }
        public long SourceLifecycleGeneration
        {
            get { return Emission.SourceLifecycleGeneration; }
        }
        public double ResolvedDamage
        {
            get { return Emission.ResolvedDamage; }
        }
        public StableId DamageChannelStableId
        {
            get { return Emission.Execution.Descriptor.DamageChannelId; }
        }
        public StableId TargetEntityStableId { get; }
        public Collider2D TargetCollider { get; }
    }

    /// <summary>
    /// Per-room-revision downstream adapter for canonical enemy attack-pattern facts.
    /// The runtime remains the sole decision, aim, cadence, and operation authority.
    /// </summary>
    public sealed class RoomEnemyAttackPresentationPortV1 :
        IEnemyAttackEffectPortV1,
        IEnemyAttackPatternEffectPortV1
    {
        private readonly Dictionary<StableId, MobileBlasterDroidLivePresentation2D>
            presentations = new Dictionary<StableId, MobileBlasterDroidLivePresentation2D>();
        private readonly Dictionary<StableId, string> acceptedDispatches =
            new Dictionary<StableId, string>();
        private readonly Dictionary<StableId, string> acceptedCancellations =
            new Dictionary<StableId, string>();

        public void Bind(RoomEnemyActor2D actor, long presentationRevision)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            if (presentationRevision <= 0L)
                throw new ArgumentOutOfRangeException(nameof(presentationRevision));
            if (!actor.IsBound || actor.Runtime == null)
                throw new InvalidOperationException(
                    "enemy-blaster-presentation-requires-bound-room-actor");

            EnemyPlacementRuntimeInstanceV1 runtime = actor.Runtime;
            if (!MobileBlasterDroidLivePresentation2D.IsSupportedDefinition(
                runtime.Definition))
            {
                return;
            }

            MobileBlasterDroidLivePresentation2D existing;
            if (presentations.TryGetValue(actor.ActorStableId, out existing))
            {
                if (existing == null)
                {
                    throw new InvalidOperationException(
                        "enemy-blaster-presentation-binding-lost");
                }
                existing.Bind(actor, presentationRevision);
                return;
            }

            MobileBlasterDroidLivePresentation2D presentation =
                actor.GetComponent<MobileBlasterDroidLivePresentation2D>()
                ?? actor.gameObject.AddComponent<MobileBlasterDroidLivePresentation2D>();
            presentation.Bind(actor, presentationRevision);
            presentations.Add(actor.ActorStableId, presentation);
        }

        public void Emit(EnemyAttackExecutionRequestV1 request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            throw new InvalidOperationException(
                "enemy-blaster-live-requires-canonical-pattern-dispatch");
        }

        public EnemyAttackPatternDispatchResultV1 Dispatch(
            EnemyAttackSequenceDispatchV1 sequence)
        {
            if (sequence == null) throw new ArgumentNullException(nameof(sequence));

            string retainedFingerprint;
            if (acceptedDispatches.TryGetValue(
                    sequence.DispatchStableId,
                    out retainedFingerprint))
            {
                return string.Equals(
                        retainedFingerprint,
                        sequence.Fingerprint,
                        StringComparison.Ordinal)
                    ? EnemyAttackPatternDispatchResultV1.ExactReplay(
                        sequence.DispatchStableId,
                        sequence.Fingerprint)
                    : EnemyAttackPatternDispatchResultV1.Rejected(
                        sequence.DispatchStableId,
                        sequence.Fingerprint,
                        EnemyAttackPatternDispatchRejectionCodeV1.ConflictingDuplicate);
            }

            MobileBlasterDroidLivePresentation2D presentation;
            if (!presentations.TryGetValue(
                    sequence.Execution.Identity.EntityInstanceId,
                    out presentation)
                || presentation == null)
            {
                return EnemyAttackPatternDispatchResultV1.Rejected(
                    sequence.DispatchStableId,
                    sequence.Fingerprint,
                    EnemyAttackPatternDispatchRejectionCodeV1.UnsupportedPort);
            }

            string diagnostic;
            if (!presentation.TryAcceptSequence(sequence, out diagnostic))
            {
                presentation.ReportDiagnostic(diagnostic);
                return EnemyAttackPatternDispatchResultV1.Rejected(
                    sequence.DispatchStableId,
                    sequence.Fingerprint,
                    EnemyAttackPatternDispatchRejectionCodeV1.InvalidCommand);
            }

            acceptedDispatches.Add(sequence.DispatchStableId, sequence.Fingerprint);
            return EnemyAttackPatternDispatchResultV1.Applied(
                sequence.DispatchStableId,
                sequence.Fingerprint);
        }

        public EnemyAttackPatternDispatchResultV1 Cancel(
            EnemyAttackSequenceCancellationFactV1 cancellation)
        {
            if (cancellation == null)
                throw new ArgumentNullException(nameof(cancellation));

            string retainedFingerprint;
            if (acceptedCancellations.TryGetValue(
                    cancellation.CancellationStableId,
                    out retainedFingerprint))
            {
                return string.Equals(
                        retainedFingerprint,
                        cancellation.Fingerprint,
                        StringComparison.Ordinal)
                    ? EnemyAttackPatternDispatchResultV1.ExactReplay(
                        cancellation.CancellationStableId,
                        cancellation.Fingerprint)
                    : EnemyAttackPatternDispatchResultV1.Rejected(
                        cancellation.CancellationStableId,
                        cancellation.Fingerprint,
                        EnemyAttackPatternDispatchRejectionCodeV1.ConflictingDuplicate);
            }

            MobileBlasterDroidLivePresentation2D presentation;
            if (!presentations.TryGetValue(
                    cancellation.SourceEntityStableId,
                    out presentation)
                || presentation == null)
            {
                return EnemyAttackPatternDispatchResultV1.Rejected(
                    cancellation.CancellationStableId,
                    cancellation.Fingerprint,
                    EnemyAttackPatternDispatchRejectionCodeV1.UnsupportedPort);
            }

            string diagnostic;
            if (!presentation.TryCancel(cancellation, out diagnostic))
            {
                presentation.ReportDiagnostic(diagnostic);
                return EnemyAttackPatternDispatchResultV1.Rejected(
                    cancellation.CancellationStableId,
                    cancellation.Fingerprint,
                    EnemyAttackPatternDispatchRejectionCodeV1.InvalidCommand);
            }

            acceptedCancellations.Add(
                cancellation.CancellationStableId,
                cancellation.Fingerprint);
            return EnemyAttackPatternDispatchResultV1.Applied(
                cancellation.CancellationStableId,
                cancellation.Fingerprint);
        }
    }

    [DisallowMultipleComponent]
    public sealed class MobileBlasterDroidLivePresentation2D : MonoBehaviour
    {
        private const string PlayerMarkerTypeName =
            "ShooterMover.UI.ProductionFlow.PlayablePlayerMarker2D";

        private static readonly StableId MobileBlasterDefinitionId =
            StableId.Parse("enemy.mobile-blaster-droid");
        private static readonly StableId MobileMovementPolicyId =
            StableId.Parse("enemy-movement.mobile-positioning");
        private static readonly StableId RangedDecisionPolicyId =
            StableId.Parse("enemy-decision.ranged-standard");
        private static readonly StableId RangedProjectileCapabilityId =
            StableId.Parse("enemy-attack.ranged-projectile");
        private static readonly StableId EnemyBlasterProjectileProfileId =
            StableId.Parse("projectile.enemy-blaster");
        private static readonly StableId PlayerFactionId =
            StableId.Create("faction", "gameplay-player");

        private readonly List<PendingEmission> pending =
            new List<PendingEmission>();
        private readonly Dictionary<StableId, RoomEnemyBlasterProjectile2D>
            activeProjectiles =
                new Dictionary<StableId, RoomEnemyBlasterProjectile2D>();

        private RoomEnemyActor2D actor;
        private EnemyPlacementRuntimeInstanceV1 runtime;
        private EnemyRuntimeAttackBindingV1 attackBinding;
        private PlayerBinding player;
        private Rigidbody2D body;
        private long presentationRevision;
        private long simulationTick;
        private double simulationSeconds;
        private double fixedStepSeconds;
        private Vector2 facing = Vector2.right;
        private bool stopped;
        private string lastDiagnostic;
        private LineRenderer telegraph;
        private Material telegraphMaterial;
        private Texture2D runtimeTexture;
        private Sprite runtimeSprite;

        public event Action<RoomEnemyProjectileContactV1> ProjectileContacted;

        public bool IsBound
        {
            get
            {
                return actor != null
                    && runtime != null
                    && actor.IsBound
                    && ReferenceEquals(actor.Runtime, runtime)
                    && presentationRevision == runtime.LifecycleGeneration;
            }
        }

        public bool IsTerminalStopped
        {
            get { return stopped; }
        }

        public long PresentationRevision
        {
            get { return presentationRevision; }
        }

        public static bool IsSupportedDefinition(EnemyDefinitionV1 definition)
        {
            return definition != null
                && definition.DefinitionId == MobileBlasterDefinitionId;
        }

        public void Bind(RoomEnemyActor2D boundActor, long revision)
        {
            if (boundActor == null) throw new ArgumentNullException(nameof(boundActor));
            if (revision <= 0L) throw new ArgumentOutOfRangeException(nameof(revision));
            if (!boundActor.IsBound || boundActor.Runtime == null)
                throw new InvalidOperationException(
                    "enemy-blaster-presentation-requires-live-runtime");

            EnemyPlacementRuntimeInstanceV1 nextRuntime = boundActor.Runtime;
            if (!IsSupportedDefinition(nextRuntime.Definition))
                throw new InvalidOperationException(
                    "enemy-blaster-presentation-definition-unsupported");
            if (nextRuntime.LifecycleGeneration != revision)
                throw new InvalidOperationException(
                    "enemy-blaster-presentation-revision-mismatch");

            if (ReferenceEquals(actor, boundActor)
                && ReferenceEquals(runtime, nextRuntime)
                && presentationRevision == revision
                && !stopped)
            {
                return;
            }

            StopPresentation(false);
            actor = boundActor;
            runtime = nextRuntime;
            presentationRevision = revision;
            attackBinding = ValidateCapabilities(nextRuntime);
            player = ResolveExactlyOnePlayer(boundActor.gameObject.scene);
            body = EnsureBody(boundActor.gameObject);
            EnsureActorCollider(boundActor.gameObject);
            EnsureVisuals();

            Vector2 initialFacing = boundActor.transform.right;
            facing = initialFacing.sqrMagnitude > 0.000001f
                ? initialFacing.normalized
                : Vector2.right;
            simulationTick = 0L;
            simulationSeconds = 0d;
            fixedStepSeconds = Time.fixedDeltaTime;
            if (double.IsNaN(fixedStepSeconds)
                || double.IsInfinity(fixedStepSeconds)
                || fixedStepSeconds <= 0d)
            {
                throw new InvalidOperationException(
                    "enemy-blaster-fixed-step-invalid");
            }
            stopped = false;
            lastDiagnostic = null;
            body.linearVelocity = Vector2.zero;
            body.simulated = true;
            enabled = true;
        }

        internal bool TryAcceptSequence(
            EnemyAttackSequenceDispatchV1 sequence,
            out string diagnostic)
        {
            diagnostic = null;
            if (sequence == null)
            {
                diagnostic = "enemy-blaster-sequence-missing";
                return false;
            }
            if (!IsCurrentLifecycle())
            {
                diagnostic = "enemy-blaster-sequence-stale-binding";
                return false;
            }
            if (sequence.Execution.Identity.EntityInstanceId != actor.ActorStableId
                || sequence.Execution.LifecycleGeneration != presentationRevision)
            {
                diagnostic = "enemy-blaster-sequence-source-mismatch";
                return false;
            }
            if (sequence.Execution.ExecutionKind
                != EnemyAttackExecutionKindV1.Projectile)
            {
                diagnostic = "enemy-blaster-sequence-kind-unsupported";
                return false;
            }
            if (!ReferenceEquals(
                    sequence.Execution.Descriptor,
                    attackBinding.Descriptor)
                && sequence.Execution.Descriptor.AttackId
                    != attackBinding.Descriptor.AttackId)
            {
                diagnostic = "enemy-blaster-sequence-attack-mismatch";
                return false;
            }
            if (sequence.Emissions.Count != 1)
            {
                diagnostic = "enemy-blaster-sequence-requires-one-emission";
                return false;
            }

            EnemyAttackEffectEmissionV1 emission = sequence.Emissions[0];
            if (!ValidateEmission(emission, sequence, out diagnostic))
            {
                return false;
            }
            if (ContainsProjectile(emission.EmissionStableId))
            {
                diagnostic = "enemy-blaster-projectile-identity-duplicate";
                return false;
            }

            pending.Add(new PendingEmission(emission));
            pending.Sort(PendingEmission.Compare);
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
                diagnostic = "enemy-blaster-cancellation-missing";
                return false;
            }
            if (actor == null
                || runtime == null
                || cancellation.SourceEntityStableId != runtime.SpawnStableId
                || cancellation.SourceLifecycleGeneration != presentationRevision)
            {
                diagnostic = "enemy-blaster-cancellation-source-mismatch";
                return false;
            }

            var cancelled = new HashSet<StableId>(
                cancellation.CancelledProjectileStableIds);
            for (int index = pending.Count - 1; index >= 0; index--)
            {
                if (cancelled.Contains(pending[index].Emission.EmissionStableId))
                {
                    pending.RemoveAt(index);
                }
            }
            foreach (StableId projectileId in cancelled)
            {
                RoomEnemyBlasterProjectile2D projectile;
                if (activeProjectiles.TryGetValue(projectileId, out projectile)
                    && projectile != null)
                {
                    projectile.Cancel();
                }
                activeProjectiles.Remove(projectileId);
            }

            // Terminal lifecycle cancellation must stop any already-presented shot as
            // well as scheduled emissions. This is presentation cleanup only; the
            // canonical runtime remains the cancellation authority.
            if (runtime.ActorState == null || !runtime.ActorState.IsActive)
            {
                StopPresentation(true);
            }
            RefreshTelegraph();
            return true;
        }

        internal void ReportDiagnostic(string diagnostic)
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
            if (!IsCurrentLifecycle())
            {
                StopPresentation(true);
                return;
            }
            if (!actor.IsAlive || !runtime.ActorState.IsActive)
            {
                StopPresentation(true);
                return;
            }
            if (player == null || !player.IsCurrent(gameObject.scene))
            {
                ReportDiagnostic("enemy-blaster-player-binding-lost");
                StopPresentation(true);
                return;
            }

            simulationTick = checked(simulationTick + 1L);
            simulationSeconds = simulationTick * fixedStepSeconds;
            ProcessScheduledEmissions();

            Vector2 position = body.position;
            Vector2 playerPosition = player.Position;
            bool hasLineOfSight = HasLineOfSight(position, playerPosition);
            var candidate = new EnemyPerceptionCandidate(
                player.EntityStableId,
                PlayerFactionId,
                EnemyTargetRelationship.Hostile,
                ToEnemyVector(playerPosition),
                ToEnemyVector(player.Velocity),
                hasLineOfSight);
            EnemyPerceptionSnapshot perception = EnemyPerceptionBuilder.Build(
                ToEnemyVector(position),
                ToEnemyVector(facing),
                new[] { candidate },
                runtime.Definition.DetectionRadius,
                runtime.Definition.VisionArcDegrees,
                simulationTick);

            EnemyPlacementDecisionV1 decision = runtime.Evaluate(perception);
            var movementContext = new EnemyMovementRealizationContextV1(
                runtime.SpawnStableId,
                runtime.RoomStableId,
                ToEnemyVector(position),
                ToEnemyVector(facing),
                simulationTick,
                runtime.DifficultyScaling.MovementMultiplier,
                null);
            EnemyMovementRealizationV1 movement =
                runtime.RealizeMovement(decision, movementContext);
            ApplyMovement(movement);

            EnemyAttackIntent requested =
                decision.Evaluation.Decision.RequestedAttack;
            if (requested != null)
            {
                StableId operation = BuildAttackOperationId(
                    requested.DecisionId,
                    simulationTick);
                EnemyAttackExecutionResultV1 result = runtime.TryExecuteAttack(
                    decision,
                    operation,
                    simulationSeconds);
                if (result.Status == EnemyRuntimeOperationStatusV1.Rejected
                    && result.Rejection != EnemyRuntimeRejectionCodeV1.CooldownActive)
                {
                    ReportDiagnostic(
                        "enemy-blaster-attack-rejected:"
                        + result.Rejection.ToString());
                }
            }

            RefreshTelegraph();
        }

        private void ApplyMovement(EnemyMovementRealizationV1 movement)
        {
            if (movement == null)
                throw new InvalidOperationException(
                    "enemy-blaster-movement-realization-missing");

            Vector2 desiredVelocity = ToUnityVector(movement.DesiredVelocity);
            if (HasPendingTelegraph())
            {
                desiredVelocity = Vector2.zero;
            }

            float acceleration =
                (float)(runtime.Movement.Configuration.Acceleration
                    * runtime.DifficultyScaling.MovementMultiplier);
            body.linearVelocity = Vector2.MoveTowards(
                body.linearVelocity,
                desiredVelocity,
                acceleration * Time.fixedDeltaTime);

            Vector2 desiredFacing = ToUnityVector(movement.DesiredFacing);
            if (desiredFacing.sqrMagnitude <= 0.000001f)
            {
                return;
            }
            desiredFacing.Normalize();
            float currentAngle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
            float desiredAngle =
                Mathf.Atan2(desiredFacing.y, desiredFacing.x) * Mathf.Rad2Deg;
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

        private void ProcessScheduledEmissions()
        {
            for (int index = pending.Count - 1; index >= 0; index--)
            {
                EnemyAttackEffectEmissionV1 emission = pending[index].Emission;
                if (emission.ScheduledAtSeconds > simulationSeconds)
                {
                    continue;
                }

                pending.RemoveAt(index);
                SpawnProjectile(emission);
            }
        }

        private void SpawnProjectile(EnemyAttackEffectEmissionV1 emission)
        {
            if (!IsCurrentLifecycle() || !actor.IsAlive)
            {
                return;
            }

            EnemyProjectilePayloadV1 payload = emission.Projectile.Payload;
            EnemyVector2 committedOrigin = emission.CommittedIntent.CommittedOrigin;
            EnemyVector2 committedDirection = emission.CommittedIntent.CommittedDirection;
            Vector2 direction = ToUnityVector(committedDirection);
            if (direction.sqrMagnitude <= 0.000001f)
            {
                ReportDiagnostic("enemy-blaster-projectile-direction-zero");
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

            GameObject projectileObject = new GameObject(
                "Enemy Projectile Presentation");
            projectileObject.transform.SetParent(transform.parent, true);
            projectileObject.transform.position = new Vector3(
                (float)committedOrigin.X,
                (float)committedOrigin.Y,
                transform.position.z);
            var projectile =
                projectileObject.AddComponent<RoomEnemyBlasterProjectile2D>();
            projectile.Bind(
                this,
                actor,
                player,
                emission,
                direction,
                payload,
                runtimeSprite);
            activeProjectiles.Add(emission.EmissionStableId, projectile);
        }

        internal void PublishContact(
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
            var fact = new RoomEnemyProjectileContactV1(
                contactId,
                emission,
                player.EntityStableId,
                targetCollider);

            Action<RoomEnemyProjectileContactV1> handler = ProjectileContacted;
            if (handler == null) return;
            Delegate[] subscribers = handler.GetInvocationList();
            for (int index = 0; index < subscribers.Length; index++)
            {
                try
                {
                    ((Action<RoomEnemyProjectileContactV1>)subscribers[index])(fact);
                }
                catch (Exception exception)
                {
                    if (IsFatal(exception)) throw;
                    Debug.LogException(exception, this);
                }
            }
        }

        internal void NotifyProjectileEnded(StableId projectileStableId)
        {
            if (projectileStableId == null) return;
            activeProjectiles.Remove(projectileStableId);
        }

        private bool ValidateEmission(
            EnemyAttackEffectEmissionV1 emission,
            EnemyAttackSequenceDispatchV1 sequence,
            out string diagnostic)
        {
            diagnostic = null;
            if (emission == null
                || emission.Kind != EnemyAttackEffectEmissionKindV1.Projectile
                || emission.Projectile == null)
            {
                diagnostic = "enemy-blaster-emission-projectile-required";
                return false;
            }
            if (emission.SequenceStableId != sequence.DispatchStableId
                || emission.SourceEntityStableId != runtime.SpawnStableId
                || emission.SourceLifecycleGeneration != presentationRevision)
            {
                diagnostic = "enemy-blaster-emission-lifecycle-mismatch";
                return false;
            }
            if (emission.Projectile.Payload == null
                || emission.Projectile.Payload.ProjectileProfileId
                    != EnemyBlasterProjectileProfileId
                || emission.Projectile.Payload.AreaPayload != null
                || emission.Projectile.Payload.PierceCount != 0
                || emission.Projectile.Payload.Speed <= 0d
                || emission.Projectile.Payload.MaximumTravelDistance <= 0d
                || emission.Projectile.Payload.CollisionRadius <= 0d)
            {
                diagnostic = "enemy-blaster-emission-payload-unsupported";
                return false;
            }
            if (emission.ScheduledAtSeconds <= sequence.Sequence.StartedAtSeconds)
            {
                diagnostic = "enemy-blaster-emission-readable-windup-required";
                return false;
            }
            if (emission.CommittedIntent.CommittedDirection.Length <= 0d)
            {
                diagnostic = "enemy-blaster-emission-committed-direction-zero";
                return false;
            }
            return true;
        }

        private static EnemyRuntimeAttackBindingV1 ValidateCapabilities(
            EnemyPlacementRuntimeInstanceV1 value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (value.Definition.MovementPolicyId != MobileMovementPolicyId
                || value.Movement.Configuration.PolicyId != MobileMovementPolicyId)
            {
                throw new InvalidOperationException(
                    "enemy-blaster-required-mobile-positioning-capability-missing");
            }
            if (value.Definition.DecisionPolicyId != RangedDecisionPolicyId
                || value.Decision.Configuration.PolicyId != RangedDecisionPolicyId)
            {
                throw new InvalidOperationException(
                    "enemy-blaster-required-ranged-decision-capability-missing");
            }

            EnemyRuntimeAttackBindingV1 selected = null;
            for (int index = 0; index < value.Attacks.Count; index++)
            {
                EnemyRuntimeAttackBindingV1 candidate = value.Attacks[index];
                if (candidate != null
                    && candidate.Descriptor != null
                    && candidate.Descriptor.CapabilityId
                        == RangedProjectileCapabilityId)
                {
                    if (selected != null)
                    {
                        throw new InvalidOperationException(
                            "enemy-blaster-ranged-projectile-capability-ambiguous");
                    }
                    selected = candidate;
                }
            }
            if (selected == null
                || selected.Capability.Configuration.CapabilityId
                    != RangedProjectileCapabilityId
                || selected.Capability.Configuration.ExecutionKind
                    != EnemyAttackExecutionKindV1.Projectile)
            {
                throw new InvalidOperationException(
                    "enemy-blaster-required-ranged-projectile-capability-missing");
            }

            EnemyAttackCapabilityDescriptorV1 descriptor = selected.Descriptor;
            EnemyShootingPatternV1 shooting = descriptor.ShootingPattern;
            EnemyProjectilePayloadV1 projectile = descriptor.ProjectilePayload;
            if (shooting == null
                || projectile == null
                || descriptor.MeleePattern != null
                || projectile.ProjectileProfileId != EnemyBlasterProjectileProfileId
                || projectile.AreaPayload != null
                || projectile.PierceCount != 0
                || shooting.ShotsPerSequence != 1
                || shooting.ProjectilesPerShot != 1
                || shooting.SequenceAimPolicy
                    != EnemySequenceAimPolicyV1.LockAtSequenceStart
                || shooting.WindUpSeconds <= 0d
                || descriptor.Damage <= 0d)
            {
                throw new InvalidOperationException(
                    "enemy-blaster-authored-projectile-pattern-unsupported");
            }
            return selected;
        }

        private PlayerBinding ResolveExactlyOnePlayer(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException(
                    "enemy-blaster-player-scene-invalid");

            PlayerBinding result = null;
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

                    if (result != null)
                    {
                        throw new InvalidOperationException(
                            "enemy-blaster-player-marker-duplicated");
                    }
                    PropertyInfo identityProperty = candidate.GetType().GetProperty(
                        "CharacterInstanceStableId",
                        BindingFlags.Instance | BindingFlags.Public);
                    StableId entityId = identityProperty == null
                        ? null
                        : identityProperty.GetValue(candidate, null) as StableId;
                    if (entityId == null)
                    {
                        throw new InvalidOperationException(
                            "enemy-blaster-player-marker-unbound");
                    }
                    Rigidbody2D playerBody = candidate.GetComponent<Rigidbody2D>();
                    Collider2D playerCollider =
                        candidate.GetComponentInChildren<Collider2D>(true);
                    if (playerBody == null || playerCollider == null)
                    {
                        throw new InvalidOperationException(
                            "enemy-blaster-player-physics-binding-missing");
                    }
                    result = new PlayerBinding(candidate, entityId, playerBody);
                }
            }

            return result
                ?? throw new InvalidOperationException(
                    "enemy-blaster-player-marker-missing");
        }

        private bool HasLineOfSight(Vector2 origin, Vector2 target)
        {
            RaycastHit2D[] hits = Physics2D.LinecastAll(origin, target);
            for (int index = 0; index < hits.Length; index++)
            {
                Collider2D hit = hits[index].collider;
                if (hit == null
                    || hit.isTrigger
                    || IsOwnedByActor(hit)
                    || player.IsTarget(hit))
                {
                    continue;
                }
                return false;
            }
            return true;
        }

        private bool IsOwnedByActor(Collider2D collider)
        {
            return collider != null
                && (collider.transform == actor.transform
                    || collider.transform.IsChildOf(actor.transform));
        }

        private bool IsCurrentLifecycle()
        {
            return !stopped
                && actor != null
                && runtime != null
                && actor.IsBound
                && ReferenceEquals(actor.Runtime, runtime)
                && actor.LifecycleGeneration == presentationRevision
                && runtime.LifecycleGeneration == presentationRevision;
        }

        private bool ContainsProjectile(StableId projectileStableId)
        {
            if (projectileStableId == null) return false;
            if (activeProjectiles.ContainsKey(projectileStableId)) return true;
            for (int index = 0; index < pending.Count; index++)
            {
                if (pending[index].Emission.EmissionStableId == projectileStableId)
                {
                    return true;
                }
            }
            return false;
        }

        private bool HasPendingTelegraph()
        {
            return pending.Count > 0
                && pending[0].Emission.ScheduledAtSeconds > simulationSeconds;
        }

        private void RefreshTelegraph()
        {
            if (telegraph == null) return;
            if (!IsCurrentLifecycle() || !HasPendingTelegraph())
            {
                telegraph.enabled = false;
                return;
            }

            EnemyAttackEffectEmissionV1 emission = pending[0].Emission;
            EnemyVector2 origin = emission.CommittedIntent.CommittedOrigin;
            Vector2 direction =
                ToUnityVector(emission.CommittedIntent.CommittedDirection).normalized;
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
            if (runtimeSprite == null)
            {
                runtimeTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                runtimeTexture.name = "Enemy Blaster Runtime Pixel";
                runtimeTexture.SetPixel(0, 0, Color.white);
                runtimeTexture.Apply(false, true);
                runtimeSprite = Sprite.Create(
                    runtimeTexture,
                    new Rect(0f, 0f, 1f, 1f),
                    new Vector2(0.5f, 0.5f),
                    1f);
                runtimeSprite.name = "Enemy Blaster Runtime Sprite";
            }

            if (telegraph == null)
            {
                GameObject lineObject = new GameObject(
                    "Enemy Blaster Telegraph Presentation");
                lineObject.transform.SetParent(transform, false);
                telegraph = lineObject.AddComponent<LineRenderer>();
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null)
                {
                    throw new InvalidOperationException(
                        "enemy-blaster-telegraph-shader-missing");
                }
                telegraphMaterial = new Material(shader);
                telegraphMaterial.name = "Enemy Blaster Telegraph Runtime Material";
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
            result.constraints = RigidbodyConstraints2D.FreezeRotation;
            result.simulated = true;
            return result;
        }

        private static void EnsureActorCollider(GameObject target)
        {
            Collider2D collider = target.GetComponentInChildren<Collider2D>(true);
            if (collider != null) return;
            CircleCollider2D fallback = target.AddComponent<CircleCollider2D>();
            fallback.radius = 0.45f;
            fallback.isTrigger = false;
        }

        private StableId BuildAttackOperationId(
            StableId decisionStableId,
            long tick)
        {
            return StableId.Create(
                "enemy-attack-operation",
                "mobile-blaster-"
                + Hash64(
                    runtime.SpawnStableId.ToString()
                    + "|"
                    + presentationRevision.ToString(CultureInfo.InvariantCulture)
                    + "|"
                    + tick.ToString(CultureInfo.InvariantCulture)
                    + "|"
                    + decisionStableId.ToString()).ToString(
                        "x16",
                        CultureInfo.InvariantCulture));
        }

        private void StopPresentation(bool markStopped)
        {
            pending.Clear();
            if (telegraph != null) telegraph.enabled = false;

            var projectiles = new List<RoomEnemyBlasterProjectile2D>(
                activeProjectiles.Values);
            activeProjectiles.Clear();
            for (int index = 0; index < projectiles.Count; index++)
            {
                if (projectiles[index] != null)
                {
                    projectiles[index].Cancel();
                }
            }

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
            if (markStopped)
            {
                stopped = true;
                enabled = false;
            }
        }

        private void OnDisable()
        {
            if (!stopped && actor != null && !IsCurrentLifecycle())
            {
                StopPresentation(true);
            }
        }

        private void OnDestroy()
        {
            StopPresentation(true);
            if (telegraphMaterial != null) Destroy(telegraphMaterial);
            if (runtimeSprite != null) Destroy(runtimeSprite);
            if (runtimeTexture != null) Destroy(runtimeTexture);
            telegraphMaterial = null;
            runtimeSprite = null;
            runtimeTexture = null;
            actor = null;
            runtime = null;
            player = null;
            attackBinding = null;
        }

        private static EnemyVector2 ToEnemyVector(Vector2 value)
        {
            return new EnemyVector2(value.x, value.y);
        }

        private static Vector2 ToUnityVector(EnemyVector2 value)
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

        private sealed class PendingEmission
        {
            public PendingEmission(EnemyAttackEffectEmissionV1 emission)
            {
                Emission = emission
                    ?? throw new ArgumentNullException(nameof(emission));
            }

            public EnemyAttackEffectEmissionV1 Emission { get; }

            public static int Compare(PendingEmission left, PendingEmission right)
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
                return Marker != null
                    && Body != null
                    && Marker.gameObject.scene == scene
                    && EntityStableId != null;
            }
        }
    }

    [DisallowMultipleComponent]
    internal sealed class RoomEnemyBlasterProjectile2D : MonoBehaviour
    {
        private MobileBlasterDroidLivePresentation2D owner;
        private RoomEnemyActor2D sourceActor;
        private MobileBlasterDroidLivePresentation2D.PlayerBinding target;
        private EnemyAttackEffectEmissionV1 emission;
        private Rigidbody2D body;
        private Vector2 direction;
        private Vector2 origin;
        private double maximumDistance;
        private bool ended;

        public void Bind(
            MobileBlasterDroidLivePresentation2D configuredOwner,
            RoomEnemyActor2D configuredSourceActor,
            MobileBlasterDroidLivePresentation2D.PlayerBinding configuredTarget,
            EnemyAttackEffectEmissionV1 configuredEmission,
            Vector2 configuredDirection,
            EnemyProjectilePayloadV1 payload,
            Sprite sprite)
        {
            owner = configuredOwner
                ?? throw new ArgumentNullException(nameof(configuredOwner));
            sourceActor = configuredSourceActor
                ?? throw new ArgumentNullException(nameof(configuredSourceActor));
            target = configuredTarget
                ?? throw new ArgumentNullException(nameof(configuredTarget));
            emission = configuredEmission
                ?? throw new ArgumentNullException(nameof(configuredEmission));
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (sprite == null) throw new ArgumentNullException(nameof(sprite));
            if (configuredDirection.sqrMagnitude <= 0.000001f)
                throw new ArgumentOutOfRangeException(nameof(configuredDirection));

            direction = configuredDirection.normalized;
            origin = transform.position;
            maximumDistance = payload.MaximumTravelDistance;

            SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(1f, 0.72f, 0.12f, 1f);
            renderer.sortingOrder = 300;
            float diameter = Mathf.Max(0.12f, (float)payload.CollisionRadius * 2f);
            transform.localScale = new Vector3(diameter, diameter, 1f);

            CircleCollider2D collider = gameObject.AddComponent<CircleCollider2D>();
            collider.radius = 0.5f;
            collider.isTrigger = true;

            body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.simulated = true;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            body.rotation = angle;
            enabled = true;
        }

        private void FixedUpdate()
        {
            if (ended) return;
            if (owner == null
                || sourceActor == null
                || !sourceActor.IsBound
                || !sourceActor.IsAlive
                || target == null
                || !target.IsCurrent(gameObject.scene))
            {
                End(false, null);
                return;
            }

            EnemyProjectilePayloadV1 payload = emission.Projectile.Payload;
            Vector2 next = body.position
                + direction * (float)payload.Speed * Time.fixedDeltaTime;
            body.MovePosition(next);
            if (Vector2.Distance(origin, next) >= maximumDistance)
            {
                End(false, null);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (ended || other == null) return;
            if (target != null && target.IsTarget(other))
            {
                End(true, other);
                return;
            }
            if (sourceActor != null
                && (other.transform == sourceActor.transform
                    || other.transform.IsChildOf(sourceActor.transform)))
            {
                return;
            }
            if (!other.isTrigger)
            {
                End(false, null);
            }
        }

        public void Cancel()
        {
            End(false, null);
        }

        private void End(bool contactedTarget, Collider2D targetCollider)
        {
            if (ended) return;
            ended = true;
            StableId projectileId = emission == null
                ? null
                : emission.EmissionStableId;
            if (contactedTarget
                && owner != null
                && emission != null
                && targetCollider != null)
            {
                owner.PublishContact(emission, targetCollider);
            }
            if (owner != null)
            {
                owner.NotifyProjectileEnded(projectileId);
            }
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (!ended && owner != null && emission != null)
            {
                owner.NotifyProjectileEnded(emission.EmissionStableId);
            }
            ended = true;
            owner = null;
            sourceActor = null;
            target = null;
            emission = null;
            body = null;
        }
    }
}
