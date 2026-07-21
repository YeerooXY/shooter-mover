using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.GameplayEntities.Enemies;

namespace ShooterMover.EnemyRuntimeComposition
{
    internal static class EnemyRuntimeAuthorityFingerprintV1
    {
        public static string Decision(EnemyPlacementDecisionV1 decision)
        {
            if (decision == null) return Hash("enemy-issued-decision-v1|null");

            var builder = new StringBuilder("enemy-issued-decision-v1");
            AppendId(builder, "entity", decision.EntityInstanceId);
            AppendLong(builder, "lifecycle", decision.LifecycleGeneration);
            AppendPerception(builder, decision.Perception);
            AppendEvaluation(builder, decision.Evaluation);
            return Hash(builder.ToString());
        }

        public static string AimContext(EnemyTargetingAimContextV1 context)
        {
            if (context == null) return Hash("enemy-aim-context-v1|null");
            var builder = new StringBuilder("enemy-aim-context-v1");
            AppendPerception(builder, context.Perception);
            AppendNumber(builder, "difficulty-scalar", context.DifficultyScalar);
            return Hash(builder.ToString());
        }

        public static string AttackAttempt(
            string issuedDecisionFingerprint,
            EnemyTargetingAimContextV1 suppliedContext,
            bool callerSuppliedContext,
            double occurredAtSeconds,
            EnemyDifficultyContextV1 difficultyContext,
            EnemyDifficultyScalingV1 difficultyScaling,
            EnemyRuntimeAttackBindingV1 binding)
        {
            var builder = new StringBuilder("enemy-attack-attempt-v1");
            AppendText(builder, "issued-decision", issuedDecisionFingerprint);
            AppendBool(builder, "caller-context", callerSuppliedContext);
            AppendText(builder, "aim-context", AimContext(suppliedContext));
            AppendNumber(builder, "occurred", occurredAtSeconds);
            if (difficultyContext == null)
            {
                AppendText(builder, "difficulty", null);
            }
            else
            {
                AppendId(builder, "difficulty-id", difficultyContext.DifficultyId);
                AppendNumber(builder, "difficulty-scalar", difficultyContext.Scalar);
            }
            AppendScaling(builder, difficultyScaling);
            AppendBinding(builder, binding);
            return Hash(builder.ToString());
        }

        public static string Execution(
            EnemyAttackExecutionRequestV1 execution,
            string issuedDecisionFingerprint)
        {
            if (execution == null) return Hash("enemy-accepted-execution-v1|null");
            var builder = new StringBuilder("enemy-accepted-execution-v1");
            AppendId(builder, "operation", execution.OperationStableId);
            AppendIdentity(builder, execution.Identity);
            AppendLong(builder, "lifecycle", execution.LifecycleGeneration);
            AppendNumber(builder, "occurred", execution.OccurredAtSeconds);
            AppendDescriptor(builder, execution.Descriptor);
            AppendAttackIntent(builder, "committed", execution.CommittedIntent);
            AppendId(builder, "item-instance", execution.ItemInstanceStableId);
            AppendInt(builder, "execution-kind", (int)execution.ExecutionKind);
            AppendNumber(builder, "resolved-damage", execution.ResolvedDamage);
            AppendNumber(builder, "resolved-cooldown", execution.ResolvedCooldownSeconds);
            AppendText(builder, "issued-decision", issuedDecisionFingerprint);
            return Hash(builder.ToString());
        }

        public static string Descriptor(EnemyAttackCapabilityDescriptorV1 descriptor)
        {
            var builder = new StringBuilder("enemy-attack-descriptor-v1");
            AppendDescriptor(builder, descriptor);
            return Hash(builder.ToString());
        }

        public static string AttackIntent(EnemyAttackIntent intent)
        {
            var builder = new StringBuilder("enemy-attack-intent-v1");
            AppendAttackIntent(builder, "intent", intent);
            return Hash(builder.ToString());
        }

        public static string Impact(
            string acceptedExecutionFingerprint,
            StableId targetEntityStableId)
        {
            var builder = new StringBuilder("enemy-impact-v1");
            AppendText(builder, "execution", acceptedExecutionFingerprint);
            AppendId(builder, "target", targetEntityStableId);
            return Hash(builder.ToString());
        }

        public static bool IdentityEquals(
            EnemyRuntimeIdentityV1 left,
            EnemyRuntimeIdentityV1 right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null) return false;
            return left.EntityInstanceId == right.EntityInstanceId
                && left.RunParticipantId == right.RunParticipantId
                && left.RunStableId == right.RunStableId
                && left.RoomRuntimeInstanceStableId == right.RoomRuntimeInstanceStableId
                && left.RoomStableId == right.RoomStableId
                && left.PlacementStableId == right.PlacementStableId;
        }

        private static void AppendEvaluation(StringBuilder builder, EnemyDecisionEvaluation evaluation)
        {
            if (evaluation == null)
            {
                AppendText(builder, "evaluation", null);
                return;
            }

            EnemyDecisionSnapshot decision = evaluation.Decision;
            if (decision == null)
            {
                AppendText(builder, "decision", null);
            }
            else
            {
                AppendId(builder, "selected-target", decision.SelectedTargetId);
                AppendVector(builder, "desired-movement", decision.DesiredMovement);
                AppendVector(builder, "desired-facing", decision.DesiredFacing);
                AppendInt(builder, "movement-kind", (int)decision.MovementKind);
                AppendAttackIntent(builder, "requested", decision.RequestedAttack);
                AppendId(builder, "behavior-phase", decision.BehaviorPhaseId);
                AppendId(builder, "decision-reason", decision.ReasonCode);
            }

            EnemyDebugSnapshot debug = evaluation.Debug;
            if (debug == null)
            {
                AppendText(builder, "debug", null);
                return;
            }

            AppendId(builder, "debug-entity", debug.EntityId);
            AppendId(builder, "debug-definition", debug.DefinitionId);
            AppendInt(builder, "debug-lifecycle-phase", (int)debug.LifecyclePhase);
            AppendNumber(builder, "debug-health", debug.CurrentHealth);
            AppendNumber(builder, "debug-max-health", debug.MaximumHealth);
            AppendInt(builder, "debug-room-clear-role", (int)debug.RoomClearRole);
            AppendId(builder, "debug-selected-target", debug.SelectedTargetId);
            AppendNumber(builder, "debug-detection-radius", debug.DetectionRadius);
            AppendNumber(builder, "debug-min-range", debug.MinimumAttackRange);
            AppendNumber(builder, "debug-preferred-range", debug.PreferredAttackRange);
            AppendNumber(builder, "debug-max-range", debug.MaximumAttackRange);
            AppendNumber(builder, "debug-attack-arc", debug.AttackArcDegrees);
            AppendVector(builder, "debug-current-facing", debug.CurrentFacing);
            AppendNumber(builder, "debug-selected-distance", debug.SelectedTargetDistance);
            AppendBool(builder, "debug-los", debug.SelectedTargetHasLineOfSight);
            AppendBool(builder, "debug-detection", debug.SelectedTargetWithinDetectionRange);
            AppendBool(builder, "debug-vision", debug.SelectedTargetWithinVisionArc);
            AppendBool(builder, "debug-attack-arc-member", debug.SelectedTargetWithinAttackArc);
            AppendVector(builder, "debug-desired-movement", debug.DesiredMovement);
            AppendVector(builder, "debug-desired-facing", debug.DesiredFacing);
            AppendAttackIntent(builder, "debug-requested", debug.RequestedAttack);
            AppendId(builder, "debug-behavior-phase", debug.BehaviorPhaseId);
            AppendVector(builder, "debug-commit-direction", debug.CommitmentDirection);
            AppendVector(builder, "debug-commit-point", debug.CommitmentPoint);
            AppendId(builder, "debug-reason", debug.DecisionReasonCode);
        }

        private static void AppendPerception(StringBuilder builder, EnemyPerceptionSnapshot perception)
        {
            if (perception == null)
            {
                AppendText(builder, "perception", null);
                return;
            }

            AppendVector(builder, "observer-position", perception.ObserverPosition);
            AppendVector(builder, "observer-facing", perception.ObserverFacing);
            AppendLong(builder, "simulation-tick", perception.SimulationTick);

            var canonicalTargets = new List<string>();
            for (int index = 0; index < perception.Targets.Count; index++)
            {
                EnemyPerceivedTarget target = perception.Targets[index];
                var targetBuilder = new StringBuilder("target-v1");
                AppendId(targetBuilder, "entity", target.EntityId);
                AppendId(targetBuilder, "faction", target.FactionId);
                AppendInt(targetBuilder, "relationship", (int)target.Relationship);
                AppendVector(targetBuilder, "position", target.Position);
                AppendVector(targetBuilder, "velocity", target.Velocity);
                AppendNumber(targetBuilder, "distance", target.Distance);
                AppendVector(targetBuilder, "direction", target.Direction);
                AppendBool(targetBuilder, "los", target.HasLineOfSight);
                AppendBool(targetBuilder, "detected", target.IsWithinDetectionRange);
                AppendBool(targetBuilder, "vision", target.IsWithinVisionArc);
                canonicalTargets.Add(targetBuilder.ToString());
            }
            canonicalTargets.Sort(StringComparer.Ordinal);
            AppendInt(builder, "target-count", canonicalTargets.Count);
            for (int index = 0; index < canonicalTargets.Count; index++)
                AppendText(builder, "target", canonicalTargets[index]);
        }

        private static void AppendBinding(StringBuilder builder, EnemyRuntimeAttackBindingV1 binding)
        {
            if (binding == null)
            {
                AppendText(builder, "binding", null);
                return;
            }
            AppendDescriptor(builder, binding.Descriptor);
            EnemyTargetingAimPolicyConfigurationV1 aim = binding.TargetingAim.Configuration;
            AppendId(builder, "aim-policy", aim.PolicyId);
            AppendInt(builder, "aim-mode", (int)aim.CommitmentMode);
            AppendNumber(builder, "prediction-horizon", aim.PredictionHorizonSeconds);
            AppendNumber(builder, "prediction-distance", aim.MaximumPredictionDistance);
            EnemyAttackCapabilityConfigurationV1 capability = binding.Capability.Configuration;
            AppendId(builder, "capability", capability.CapabilityId);
            AppendId(builder, "capability-aim", capability.TargetingAimPolicyId);
            AppendInt(builder, "capability-kind", (int)capability.ExecutionKind);
        }

        private static void AppendDescriptor(
            StringBuilder builder,
            EnemyAttackCapabilityDescriptorV1 descriptor)
        {
            if (descriptor == null)
            {
                AppendText(builder, "descriptor", null);
                return;
            }

            AppendId(builder, "attack-id", descriptor.AttackId);
            AppendId(builder, "capability-id", descriptor.CapabilityId);
            AppendInt(builder, "selection-priority", descriptor.SelectionPriority);
            AppendNumber(builder, "attack-arc", descriptor.AttackArcDegrees);
            AppendNumber(builder, "minimum-range", descriptor.MinimumAttackRange);
            AppendNumber(builder, "preferred-range", descriptor.PreferredAttackRange);
            AppendNumber(builder, "maximum-range", descriptor.MaximumAttackRange);
            AppendNumber(builder, "cooldown", descriptor.CooldownSeconds);
            AppendNumber(builder, "damage", descriptor.Damage);
            AppendId(builder, "damage-channel", descriptor.DamageChannelId);

            EnemyProjectileAttackParametersV1 projectile = descriptor.Projectile;
            if (projectile == null)
            {
                AppendText(builder, "projectile", null);
            }
            else
            {
                AppendId(builder, "projectile-profile", projectile.ProjectileProfileId);
                AppendInt(builder, "projectile-count", projectile.ProjectileCount);
                AppendNumber(builder, "projectile-speed", projectile.ProjectileSpeed);
                AppendNumber(builder, "projectile-distance", projectile.MaximumTravelDistance);
                AppendNumber(builder, "projectile-radius", projectile.CollisionRadius);
                AppendNumber(builder, "projectile-spread", projectile.SpreadDegrees);
                AppendInt(builder, "projectile-pierce", projectile.PierceCount);
            }

            EnemyAreaAttackParametersV1 area = descriptor.Area;
            if (area == null)
            {
                AppendText(builder, "area", null);
            }
            else
            {
                AppendNumber(builder, "area-radius", area.Radius);
                AppendNumber(builder, "area-duration", area.DurationSeconds);
                AppendInt(builder, "area-targets", area.MaximumTargets);
            }

            EnemyMeleeAttackParametersV1 melee = descriptor.Melee;
            if (melee == null)
            {
                AppendText(builder, "melee", null);
            }
            else
            {
                AppendNumber(builder, "melee-contact-radius", melee.ContactRadius);
                AppendNumber(builder, "melee-pounce-distance", melee.PounceDistance);
                AppendNumber(builder, "melee-wind-up", melee.WindUpSeconds);
                AppendNumber(builder, "melee-commitment", melee.CommitmentSeconds);
            }
        }

        private static void AppendAttackIntent(
            StringBuilder builder,
            string prefix,
            EnemyAttackIntent intent)
        {
            if (intent == null)
            {
                AppendText(builder, prefix, null);
                return;
            }
            AppendId(builder, prefix + "-attacker", intent.AttackerEntityId);
            AppendId(builder, prefix + "-participant", intent.SourceRunParticipantId);
            AppendId(builder, prefix + "-target", intent.TargetEntityId);
            AppendId(builder, prefix + "-attack", intent.AttackId);
            AppendVector(builder, prefix + "-origin", intent.CommittedOrigin);
            AppendVector(builder, prefix + "-direction", intent.CommittedDirection);
            AppendVector(builder, prefix + "-point", intent.CommittedTargetPoint);
            AppendId(builder, prefix + "-decision", intent.DecisionId);
            AppendId(builder, prefix + "-phase", intent.BehaviorPhaseId);
            AppendId(builder, prefix + "-reason", intent.ReasonCode);
        }

        private static void AppendIdentity(StringBuilder builder, EnemyRuntimeIdentityV1 identity)
        {
            if (identity == null)
            {
                AppendText(builder, "identity", null);
                return;
            }
            AppendId(builder, "identity-entity", identity.EntityInstanceId);
            AppendId(builder, "identity-participant", identity.RunParticipantId);
            AppendId(builder, "identity-run", identity.RunStableId);
            AppendId(builder, "identity-room-runtime", identity.RoomRuntimeInstanceStableId);
            AppendId(builder, "identity-room", identity.RoomStableId);
            AppendId(builder, "identity-placement", identity.PlacementStableId);
        }

        private static void AppendScaling(StringBuilder builder, EnemyDifficultyScalingV1 scaling)
        {
            if (scaling == null)
            {
                AppendText(builder, "scaling", null);
                return;
            }
            AppendNumber(builder, "health-multiplier", scaling.HealthMultiplier);
            AppendNumber(builder, "damage-multiplier", scaling.DamageMultiplier);
            AppendNumber(builder, "cooldown-multiplier", scaling.CooldownMultiplier);
            AppendNumber(builder, "movement-multiplier", scaling.MovementMultiplier);
        }

        private static void AppendVector(StringBuilder builder, string name, EnemyVector2 vector)
        {
            builder.Append('|').Append(name).Append('|')
                .Append(vector.X.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(vector.Y.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void AppendId(StringBuilder builder, string name, StableId value)
        {
            AppendText(builder, name, value == null ? null : value.ToString());
        }

        private static void AppendNumber(StringBuilder builder, string name, double value)
        {
            AppendText(builder, name, value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void AppendLong(StringBuilder builder, string name, long value)
        {
            AppendText(builder, name, value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendInt(StringBuilder builder, string name, int value)
        {
            AppendText(builder, name, value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendBool(StringBuilder builder, string name, bool value)
        {
            AppendText(builder, name, value ? "1" : "0");
        }

        private static void AppendText(StringBuilder builder, string name, string value)
        {
            builder.Append('|').Append(name).Append('|');
            if (value == null)
            {
                builder.Append('-');
                return;
            }
            builder.Append(value.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(value);
        }

        private static string Hash(string canonical)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                var result = new StringBuilder(bytes.Length * 2);
                for (int index = 0; index < bytes.Length; index++)
                    result.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }
    }
}
