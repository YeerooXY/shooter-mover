using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.EnemyRuntimeComposition;

namespace ShooterMover.Tests.EditMode.Enemies
{
    public sealed partial class EnemyAttackPatternAuthorityV1Tests
    {
        [Test]
        public void MeleeScheduler_ProvesContactAndCommittedPounceWindows()
        {
            EnemyAttackCapabilityDescriptorV1 contact = Melee(
                "contact",
                0d,
                0.1d,
                1,
                0d,
                0.65d,
                0d,
                0.65d,
                EnemyMeleeAimCommitPolicyV1.LockAtWindUp,
                EnemyMeleeTerminalOnImpactPolicyV1.ContinueSequence);
            EnemyAttackSequenceV1 contactSequence = Sequence(contact, "contact", 5d);
            Assert.That(contactSequence.MeleeStrikes, Has.Count.EqualTo(1));
            Assert.That(contactSequence.MeleeStrikes[0].ActiveFromSeconds, Is.EqualTo(5d));
            Assert.That(contactSequence.MeleeStrikes[0].ActiveUntilSeconds, Is.EqualTo(5.1d));
            Assert.That(contact.MeleePattern.HitsPerTarget, Is.EqualTo(1));
            Assert.That(
                contact.MeleePattern.AimCommitPolicy,
                Is.EqualTo(EnemyMeleeAimCommitPolicyV1.LockAtWindUp));

            EnemyAttackCapabilityDescriptorV1 pounce = Melee(
                "pounce",
                0.35d,
                0.25d,
                1,
                0d,
                0.8d,
                6d,
                1.6d,
                EnemyMeleeAimCommitPolicyV1.LockAtWindUp,
                EnemyMeleeTerminalOnImpactPolicyV1.ContinueSequence);
            EnemyAttackSequenceV1 pounceSequence = Sequence(pounce, "pounce", 8d);
            Assert.That(pounceSequence.MeleeStrikes[0].ActiveFromSeconds, Is.EqualTo(8.35d));
            Assert.That(pounceSequence.MeleeStrikes[0].ActiveUntilSeconds, Is.EqualTo(8.6d));
            Assert.That(pounce.MeleePattern.LungeDistance, Is.EqualTo(6d));
            Assert.That(
                pounce.MeleePattern.AimCommitPolicy,
                Is.EqualTo(EnemyMeleeAimCommitPolicyV1.LockAtWindUp));
            Assert.That(
                pounce.MeleePattern.TerminalOnImpactPolicy,
                Is.EqualTo(EnemyMeleeTerminalOnImpactPolicyV1.ContinueSequence));
        }

        [Test]
        public void Authority_ReplaysExactlyRejectsConflictsAndCancelsOnlyPendingLifecycleWork()
        {
            EnemyRuntimeIdentityV1 identity = Identity();
            EnemyAttackCapabilityDescriptorV1 burst = Shooting(
                "cancel-burst",
                3,
                0.2d,
                1,
                0d,
                0d,
                1d,
                12d,
                null);
            EnemyAttackExecutionRequestV1 execution = Execution(
                identity,
                burst,
                "shared-operation",
                10d);
            var authority = new EnemyAttackPatternAuthorityV1(identity, 4L);

            EnemyAttackPatternStartResultV1 first = authority.Start(execution);
            EnemyAttackPatternStartResultV1 replay = authority.Start(execution);
            EnemyAttackExecutionRequestV1 conflictingExecution = Execution(
                identity,
                Shooting(
                    "different-attack",
                    1,
                    0d,
                    1,
                    0d,
                    0d,
                    1d,
                    12d,
                    null),
                "shared-operation",
                10d);
            EnemyAttackPatternStartResultV1 conflict = authority.Start(conflictingExecution);

            Assert.That(first.Status, Is.EqualTo(EnemyAttackPatternOperationStatusV1.Applied));
            Assert.That(first.IsAccepted, Is.True);
            Assert.That(first.Emissions, Has.Count.EqualTo(3));
            Assert.That(replay.Status, Is.EqualTo(EnemyAttackPatternOperationStatusV1.ExactReplay));
            Assert.That(replay.IsAccepted, Is.True);
            Assert.That(replay.Sequence, Is.SameAs(first.Sequence));
            Assert.That(replay.Emissions[0], Is.SameAs(first.Emissions[0]));
            Assert.That(conflict.Rejection,
                Is.EqualTo(EnemyAttackPatternRejectionCodeV1.ConflictingDuplicate));

            var secondAuthority = new EnemyAttackPatternAuthorityV1(identity, 4L);
            EnemyAttackPatternStartResultV1 deterministic = secondAuthority.Start(execution);
            Assert.That(deterministic.Sequence.Fingerprint, Is.EqualTo(first.Sequence.Fingerprint));
            Assert.That(
                deterministic.Sequence.Identity.SequenceStableId,
                Is.EqualTo(first.Sequence.Identity.SequenceStableId));

            var cancellation = new EnemyAttackLifecycleCancellationCommandV1(
                Id("enemy-pattern-operation.cancel-lifecycle"),
                identity.EntityInstanceId,
                4L,
                10.1d);
            EnemyAttackPatternCancellationResultV1 cancelled = authority.CancelLifecycle(cancellation);
            EnemyAttackPatternCancellationResultV1 cancelledReplay = authority.CancelLifecycle(cancellation);
            EnemyAttackPatternCancellationResultV1 cancellationConflict = authority.CancelLifecycle(
                new EnemyAttackLifecycleCancellationCommandV1(
                    cancellation.OperationStableId,
                    identity.EntityInstanceId,
                    4L,
                    10.3d));
            var sameFactsDifferentOperation = new EnemyAttackLifecycleCancellationCommandV1(
                Id("enemy-pattern-operation.cancel-lifecycle-second"),
                identity.EntityInstanceId,
                4L,
                10.1d);
            EnemyAttackPatternCancellationResultV1 distinctSame =
                authority.CancelLifecycle(sameFactsDifferentOperation);
            EnemyAttackPatternCancellationResultV1 distinctSameReplay =
                authority.CancelLifecycle(sameFactsDifferentOperation);
            EnemyAttackPatternCancellationResultV1 distinctChanged =
                authority.CancelLifecycle(new EnemyAttackLifecycleCancellationCommandV1(
                    Id("enemy-pattern-operation.cancel-lifecycle-third"),
                    identity.EntityInstanceId,
                    4L,
                    10.3d));

            Assert.That(cancelled.Status, Is.EqualTo(EnemyAttackPatternOperationStatusV1.Applied));
            Assert.That(cancelled.IsAccepted, Is.True);
            Assert.That(cancelled.Fact.CancelledShotStableIds, Has.Count.EqualTo(2));
            Assert.That(cancelled.Fact.CancelledProjectileStableIds, Has.Count.EqualTo(2));
            Assert.That(
                authority.IsEmissionCancelled(first.Sequence.Projectiles[0].ProjectileStableId),
                Is.False,
                "The projectile emitted before lifecycle cancellation must remain valid.");
            Assert.That(
                authority.IsEmissionCancelled(first.Sequence.Projectiles[1].ProjectileStableId),
                Is.True);
            Assert.That(cancelledReplay.Status,
                Is.EqualTo(EnemyAttackPatternOperationStatusV1.ExactReplay));
            Assert.That(cancelledReplay.IsAccepted, Is.True);
            Assert.That(cancelledReplay.Fact, Is.SameAs(cancelled.Fact));
            Assert.That(cancellationConflict.Rejection,
                Is.EqualTo(EnemyAttackPatternRejectionCodeV1.ConflictingDuplicate));
            Assert.That(cancellationConflict.IsAccepted, Is.False);
            Assert.That(cancellationConflict.Fact, Is.SameAs(cancelled.Fact));
            AssertTerminalCancellation(distinctSame, cancelled.Fact);
            Assert.That(distinctSameReplay.Status,
                Is.EqualTo(EnemyAttackPatternOperationStatusV1.ExactReplay));
            AssertTerminalCancellation(distinctSameReplay, cancelled.Fact);
            AssertTerminalCancellation(distinctChanged, cancelled.Fact);
            Assert.That(authority.TerminalCancellationFact, Is.SameAs(cancelled.Fact));

            EnemyAttackPatternStartResultV1 afterTerminal = authority.Start(
                Execution(identity, burst, "after-terminal", 12d));
            Assert.That(afterTerminal.Rejection,
                Is.EqualTo(EnemyAttackPatternRejectionCodeV1.ActorTerminal));
            Assert.That(afterTerminal.IsAccepted, Is.False);
        }

        [Test]
        public void RejectedStarts_ExactReplayNeverBecomesAccepted()
        {
            EnemyRuntimeIdentityV1 identity = Identity();
            EnemyAttackCapabilityDescriptorV1 valid = Shooting(
                "rejected-replay",
                1,
                0d,
                1,
                0d,
                0d,
                1d,
                12d,
                null);

            var staleAuthority = new EnemyAttackPatternAuthorityV1(identity, 4L);
            AssertRejectedReplay(
                staleAuthority,
                Execution(identity, valid, "stale", 1d, 3L),
                EnemyAttackPatternRejectionCodeV1.StaleLifecycle);

            var invalidPatternAuthority = new EnemyAttackPatternAuthorityV1(identity, 4L);
            var invalidPattern = new EnemyAttackCapabilityDescriptorV1(
                Id("enemy-attack-profile.invalid-pattern"),
                Id("enemy-attack.ranged-projectile"),
                10,
                120d,
                0d,
                5d,
                12d,
                3d,
                Id("damage.kinetic"),
                null,
                null,
                null);
            AssertRejectedReplay(
                invalidPatternAuthority,
                Execution(identity, invalidPattern, "invalid-pattern", 2d),
                EnemyAttackPatternRejectionCodeV1.InvalidPattern);

            var terminalAuthority = new EnemyAttackPatternAuthorityV1(identity, 4L);
            EnemyAttackPatternCancellationResultV1 terminal = terminalAuthority.CancelLifecycle(
                new EnemyAttackLifecycleCancellationCommandV1(
                    Id("enemy-pattern-operation.make-terminal"),
                    identity.EntityInstanceId,
                    4L,
                    2.5d));
            Assert.That(terminal.IsAccepted, Is.True);
            AssertRejectedReplay(
                terminalAuthority,
                Execution(identity, valid, "terminal", 3d),
                EnemyAttackPatternRejectionCodeV1.ActorTerminal);

            var invalidCommandAuthority = new EnemyAttackPatternAuthorityV1(identity, 4L);
            var invalidCommand = new EnemyAttackCapabilityDescriptorV1(
                null,
                Id("enemy-attack.ranged-projectile"),
                10,
                120d,
                0d,
                5d,
                12d,
                3d,
                Id("damage.kinetic"),
                valid.ShootingPattern,
                valid.ProjectilePayload,
                null);
            AssertRejectedReplay(
                invalidCommandAuthority,
                Execution(identity, invalidCommand, "invalid-command", 4d),
                EnemyAttackPatternRejectionCodeV1.InvalidCommand);
        }

        private static void AssertRejectedReplay(
            EnemyAttackPatternAuthorityV1 authority,
            EnemyAttackExecutionRequestV1 execution,
            EnemyAttackPatternRejectionCodeV1 expected)
        {
            EnemyAttackPatternStartResultV1 first = authority.Start(execution);
            EnemyAttackPatternStartResultV1 replay = authority.Start(execution);

            Assert.That(first.Status, Is.EqualTo(EnemyAttackPatternOperationStatusV1.Rejected));
            Assert.That(first.Rejection, Is.EqualTo(expected));
            Assert.That(first.Sequence, Is.Null);
            Assert.That(first.IsAccepted, Is.False);
            Assert.That(replay.Status, Is.EqualTo(EnemyAttackPatternOperationStatusV1.ExactReplay));
            Assert.That(replay.Rejection, Is.EqualTo(expected));
            Assert.That(replay.Sequence, Is.Null);
            Assert.That(replay.IsAccepted, Is.False);
        }

        private static void AssertTerminalCancellation(
            EnemyAttackPatternCancellationResultV1 result,
            EnemyAttackSequenceCancellationFactV1 expectedFact)
        {
            Assert.That(result.Status, Is.EqualTo(EnemyAttackPatternOperationStatusV1.Rejected));
            Assert.That(result.Rejection,
                Is.EqualTo(EnemyAttackPatternRejectionCodeV1.ActorTerminal));
            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.Fact, Is.SameAs(expectedFact));
        }
    }
}
