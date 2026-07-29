using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;

namespace ShooterMover.EnemyRuntimeComposition
{
    public static class BuiltInEnemyRules
    {
        public static EnemyRules Create()
        {
            StableId lockedAim = StableId.Parse("enemy-aim.locked-standard");
            var movementPolicy = new DecisionMovementLivePolicy();
            var directRealizer = new DirectEnemyMovementIntentRealizer();
            var foundationDecision = new FoundationEnemyDecisionLivePolicy();
            var rangeAwareDecision = new RangeAwareEnemyDecisionLivePolicy();
            var aimPolicy = new LockedEnemyTargetingAimPolicy();
            var attackAdapter = new RequestEnemyAttackCapabilityBridge();

            return new EnemyRules(
                new[]
                {
                    Movement("enemy-movement.mobile-positioning", 3.5d, 12d, 360d,
                        movementPolicy, directRealizer),
                    Movement("enemy-movement.pursuit", 4.5d, 14d, 420d,
                        movementPolicy, directRealizer),
                    Movement("enemy-movement.stationary", 0d, 0d, 180d,
                        movementPolicy, directRealizer),
                },
                new[]
                {
                    Decision("enemy-decision.ranged-standard", 5d, 1d,
                        foundationDecision),
                    Decision("enemy-decision.pounce-standard", 0d, 0d,
                        foundationDecision),
                    Decision("enemy-decision.turret-standard", 0d, 0d,
                        foundationDecision),
                    Decision("enemy-decision.contact-standard", 0d, 0d,
                        foundationDecision),
                    Decision("enemy-decision.multi-attack-standard", 0d, 0d,
                        rangeAwareDecision),
                },
                new[]
                {
                    new EnemyTargetingAimPolicyRegistration(
                        new EnemyTargetingAimPolicyConfiguration(
                            lockedAim,
                            EnemyAimCommitmentMode.LockedDirectionAndPoint,
                            0d,
                            0d),
                        aimPolicy),
                },
                new[]
                {
                    Attack("enemy-attack.ranged-projectile", lockedAim,
                        EnemyAttackExecutionKind.Projectile, attackAdapter),
                    Attack("enemy-attack.projectile-area", lockedAim,
                        EnemyAttackExecutionKind.Projectile, attackAdapter),
                    Attack("enemy-attack.contact", lockedAim,
                        EnemyAttackExecutionKind.Contact, attackAdapter),
                    Attack("enemy-attack.pounce", lockedAim,
                        EnemyAttackExecutionKind.Pounce, attackAdapter),
                });
        }

        public static EnemyFactory CreateFactory(
            IRoomContentObjectCatalog roomObjects,
            EnemyCatalog enemyCatalog,
            EnemyLiveDownstreamPorts downstream)
        {
            return new EnemyFactory(
                roomObjects,
                enemyCatalog,
                Create(),
                new DeterministicEnemyLiveIdentityDeriver(),
                new EnemyDifficultyLiveRegistration(
                    new EnemyDifficultyScalingConfiguration(
                        StableId.Parse("enemy-difficulty.scalar-standard"),
                        1d,
                        0.5d,
                        0.2d,
                        0.15d),
                    new ScalarEnemyDifficultyScalingPolicy()),
                new EnemyPerceptionLiveRegistration(
                    new EnemyPerceptionPolicyConfiguration(
                        StableId.Parse("enemy-perception.validated-standard")),
                    new ValidatedEnemyPerceptionLiveBridge()),
                downstream ?? EnemyLiveDownstreamPorts.None());
        }

        private static EnemyMovementPolicyRegistration Movement(
            string policyId,
            double maximumSpeed,
            double acceleration,
            double turnRateDegreesPerSecond,
            IEnemyMovementLivePolicy policy,
            IEnemyMovementIntentRealizer realizer)
        {
            return new EnemyMovementPolicyRegistration(
                new EnemyMovementPolicyConfiguration(
                    StableId.Parse(policyId),
                    maximumSpeed,
                    acceleration,
                    turnRateDegreesPerSecond,
                    true),
                policy,
                realizer);
        }

        private static EnemyDecisionPolicyRegistration Decision(
            string policyId,
            double preferredMovementDistance,
            double movementTolerance,
            IEnemyDecisionLivePolicy policy)
        {
            bool independentBand = preferredMovementDistance > 0d;
            return new EnemyDecisionPolicyRegistration(
                new EnemyDecisionPolicyConfiguration(
                    StableId.Parse(policyId),
                    StableId.Parse("enemy-phase.ready"),
                    independentBand,
                    preferredMovementDistance,
                    movementTolerance),
                policy);
        }

        private static EnemyAttackCapabilityLiveRegistration Attack(
            string capabilityId,
            StableId targetingAimPolicyId,
            EnemyAttackExecutionKind executionKind,
            IEnemyAttackCapabilityBridge adapter)
        {
            return new EnemyAttackCapabilityLiveRegistration(
                new EnemyAttackCapabilityConfiguration(
                    StableId.Parse(capabilityId),
                    targetingAimPolicyId,
                    executionKind),
                adapter);
        }
    }
}
