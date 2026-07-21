using ShooterMover.Application.Missions.Rooms.Content;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;

namespace ShooterMover.EnemyRuntimeComposition
{
    public static class BuiltInEnemyRuntimePolicyRegistryV1
    {
        public static EnemyRuntimePolicyRegistryV1 Create()
        {
            StableId lockedAim = StableId.Parse("enemy-aim.locked-standard");
            var movementPolicy = new DecisionMovementRuntimePolicyV1();
            var directRealizer = new DirectEnemyMovementIntentRealizerV1();
            var foundationDecision = new FoundationEnemyDecisionRuntimePolicyV1();
            var rangeAwareDecision = new RangeAwareEnemyDecisionRuntimePolicyV1();
            var aimPolicy = new LockedEnemyTargetingAimPolicyV1();
            var attackAdapter = new RequestEnemyAttackCapabilityAdapterV1();

            return new EnemyRuntimePolicyRegistryV1(
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
                    new EnemyTargetingAimPolicyRegistrationV1(
                        new EnemyTargetingAimPolicyConfigurationV1(
                            lockedAim,
                            EnemyAimCommitmentModeV1.LockedDirectionAndPoint,
                            0d,
                            0d),
                        aimPolicy),
                },
                new[]
                {
                    Attack("enemy-attack.ranged-projectile", lockedAim,
                        EnemyAttackExecutionKindV1.Projectile, attackAdapter),
                    Attack("enemy-attack.projectile-area", lockedAim,
                        EnemyAttackExecutionKindV1.Area, attackAdapter),
                    Attack("enemy-attack.contact", lockedAim,
                        EnemyAttackExecutionKindV1.Contact, attackAdapter),
                    Attack("enemy-attack.pounce", lockedAim,
                        EnemyAttackExecutionKindV1.Pounce, attackAdapter),
                });
        }

        public static EnemyPlacementRuntimeFactoryV1 CreateFactory(
            IRoomContentObjectCatalogV1 roomObjects,
            EnemyCatalogV1 enemyCatalog,
            EnemyRuntimeDownstreamPortsV1 downstream)
        {
            return new EnemyPlacementRuntimeFactoryV1(
                roomObjects,
                enemyCatalog,
                Create(),
                new DeterministicEnemyRuntimeIdentityDeriverV1(),
                new EnemyDifficultyRuntimeRegistrationV1(
                    new EnemyDifficultyScalingConfigurationV1(
                        StableId.Parse("enemy-difficulty.scalar-standard"),
                        1d,
                        0.5d,
                        0.2d,
                        0.15d),
                    new ScalarEnemyDifficultyScalingPolicyV1()),
                new EnemyPerceptionRuntimeRegistrationV1(
                    new EnemyPerceptionPolicyConfigurationV1(
                        StableId.Parse("enemy-perception.validated-standard")),
                    new ValidatedEnemyPerceptionRuntimeAdapterV1()),
                downstream ?? EnemyRuntimeDownstreamPortsV1.None());
        }

        private static EnemyMovementPolicyRegistrationV1 Movement(
            string policyId,
            double maximumSpeed,
            double acceleration,
            double turnRateDegreesPerSecond,
            IEnemyMovementRuntimePolicyV1 policy,
            IEnemyMovementIntentRealizerV1 realizer)
        {
            return new EnemyMovementPolicyRegistrationV1(
                new EnemyMovementPolicyConfigurationV1(
                    StableId.Parse(policyId),
                    maximumSpeed,
                    acceleration,
                    turnRateDegreesPerSecond,
                    true),
                policy,
                realizer);
        }

        private static EnemyDecisionPolicyRegistrationV1 Decision(
            string policyId,
            double preferredMovementDistance,
            double movementTolerance,
            IEnemyDecisionRuntimePolicyV1 policy)
        {
            bool independentBand = preferredMovementDistance > 0d;
            return new EnemyDecisionPolicyRegistrationV1(
                new EnemyDecisionPolicyConfigurationV1(
                    StableId.Parse(policyId),
                    StableId.Parse("enemy-phase.ready"),
                    independentBand,
                    preferredMovementDistance,
                    movementTolerance),
                policy);
        }

        private static EnemyAttackCapabilityRuntimeRegistrationV1 Attack(
            string capabilityId,
            StableId targetingAimPolicyId,
            EnemyAttackExecutionKindV1 executionKind,
            IEnemyAttackCapabilityAdapterV1 adapter)
        {
            return new EnemyAttackCapabilityRuntimeRegistrationV1(
                new EnemyAttackCapabilityConfigurationV1(
                    StableId.Parse(capabilityId),
                    targetingAimPolicyId,
                    executionKind),
                adapter);
        }
    }
}
