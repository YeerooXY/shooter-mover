using System;
using System.Globalization;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Props;
using ShooterMover.Domain.Weapons.Execution;
using ShooterMover.GameplayEntities;

namespace ShooterMover.Combat.HitPolicy
{
    /// <summary>
    /// Adapts WPN-CORE-002 immutable effect descriptions into the shared hit policy.
    /// Explosion and persistent-field owners create distinct area/field effect IDs for
    /// their own lifetimes while retaining the same source actor and generation facts.
    /// </summary>
    public static class WeaponEffectHitPolicyAdapterV1
    {
        public static CombatEffectSnapshotV1 Create(
            IWeaponEffectDescription effect,
            StableId policyId,
            CombatWorldBlockerBehaviorV1 worldBlockerBehavior,
            bool allowsSelfHit,
            bool allowsFriendlyFire,
            int maximumHitsPerTarget)
        {
            if (effect == null
                || effect.Identity == null
                || !Enum.IsDefined(typeof(WeaponEffectKind), effect.Kind))
            {
                return null;
            }

            return new CombatEffectSnapshotV1(
                DeriveEffectId(effect),
                policyId,
                effect.Identity.ActorId.Value,
                effect.Identity.LifecycleGeneration.Value,
                ResolveGeometry(effect.Kind),
                worldBlockerBehavior,
                allowsSelfHit,
                allowsFriendlyFire,
                ResolvePierce(effect),
                maximumHitsPerTarget);
        }

        public static StableId DeriveEffectId(IWeaponEffectDescription effect)
        {
            if (effect == null || effect.Identity == null)
            {
                return null;
            }

            string digest = WeaponExecutionFingerprint.Compute(
                effect.Identity.ToCanonicalString()
                + "|kind=" + ((int)effect.Kind).ToString(
                    CultureInfo.InvariantCulture));
            const string prefix = "sha256:";
            string value = digest.StartsWith(prefix, StringComparison.Ordinal)
                ? digest.Substring(prefix.Length)
                : digest;
            return StableId.Create("combat-effect", value);
        }

        private static CombatEffectGeometryKindV1 ResolveGeometry(
            WeaponEffectKind kind)
        {
            return kind == WeaponEffectKind.ChainArc
                ? CombatEffectGeometryKindV1.Chain
                : CombatEffectGeometryKindV1.Projectile;
        }

        private static int ResolvePierce(IWeaponEffectDescription effect)
        {
            DirectProjectileEffect direct = effect as DirectProjectileEffect;
            if (direct != null)
            {
                return Math.Max(0, direct.Pierce);
            }

            DamageOverTimeProjectileEffect dot =
                effect as DamageOverTimeProjectileEffect;
            if (dot != null)
            {
                return Math.Max(0, dot.Pierce);
            }

            ChainArcEffect chain = effect as ChainArcEffect;
            if (chain != null)
            {
                return Math.Max(0, chain.MaximumTargets - 1);
            }

            return 0;
        }
    }

    /// <summary>
    /// Converts an accepted policy result into the existing damage receiver command.
    /// This adapter never mutates health; the selected IDamageReceiver remains the only
    /// authority that may apply the command.
    /// </summary>
    public static class CombatHitDamageCommandAdapterV1
    {
        public static bool TryCreate(
            CombatHitPolicyResultV1 policyResult,
            StableId damageEventId,
            double amount,
            CombatChannel channel,
            out DamageReceiverCommand command)
        {
            command = null;
            if (policyResult == null
                || !policyResult.DamageEligible
                || policyResult.Input == null
                || policyResult.Input.SourceActor == null
                || policyResult.Input.SourceActor.Identity == null
                || policyResult.Input.Contact == null
                || policyResult.Input.Contact.Kind != CombatHitContactKindV1.Actor
                || policyResult.Input.Contact.TargetActor == null
                || policyResult.Input.Contact.TargetActor.Identity == null
                || damageEventId == null
                || double.IsNaN(amount)
                || double.IsInfinity(amount)
                || amount <= 0d
                || !Enum.IsDefined(typeof(CombatChannel), channel)
                || channel == CombatChannel.System)
            {
                return false;
            }

            GameplayEntityIdentity source =
                policyResult.Input.SourceActor.Identity;
            CombatActorSnapshotV1 target =
                policyResult.Input.Contact.TargetActor;
            command = new DamageReceiverCommand(
                damageEventId,
                source.EntityInstanceId,
                source.Ownership.RunParticipantId,
                target.Identity.EntityInstanceId,
                amount,
                channel,
                target.LifecycleGeneration);
            return true;
        }
    }

    /// <summary>
    /// Projects the same accepted result into PROP-RUNTIME-001's existing command.
    /// The routed prop runtime remains the target authority and owns health/destruction.
    /// </summary>
    public static class CombatHitPropDamageCommandAdapterV1
    {
        public static bool TryCreate(
            CombatHitPolicyResultV1 policyResult,
            StableId operationId,
            StableId damageChannelId,
            double amount,
            out PropDamageCommandV1 command)
        {
            command = null;
            if (policyResult == null
                || !policyResult.DamageEligible
                || policyResult.Input == null
                || policyResult.Input.SourceActor == null
                || policyResult.Input.SourceActor.Identity == null
                || policyResult.Input.SourceActor.Identity.Ownership.RunParticipantId == null
                || operationId == null
                || damageChannelId == null
                || double.IsNaN(amount)
                || double.IsInfinity(amount)
                || amount <= 0d)
            {
                return false;
            }

            GameplayEntityIdentity source =
                policyResult.Input.SourceActor.Identity;
            command = new PropDamageCommandV1(
                operationId,
                source.Ownership.RunParticipantId,
                source.FactionId,
                damageChannelId,
                amount);
            return true;
        }
    }
}
