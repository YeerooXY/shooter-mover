using System;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    public interface IProjectileFractionalPierceRoller
    {
        bool Roll(
            ProjectileLifecycleContext lifecycle,
            WeaponTargetReference impactedTarget,
            int chanceTenths);
    }

    /// <summary>
    /// Narrow adapter over the existing DeterministicRandom authority. It defines no independent
    /// hash, generator, or random sequence.
    /// </summary>
    public sealed class SharedDeterministicRandomFractionalPierceRoller
        : IProjectileFractionalPierceRoller
    {
        private static readonly StableId PierceDecisionPurpose =
            StableId.Parse("weapon.projectile-pierce");
        private static readonly StableId ProjectileOrdinalPurpose =
            StableId.Parse("weapon.projectile-ordinal");

        public bool Roll(
            ProjectileLifecycleContext lifecycle,
            WeaponTargetReference impactedTarget,
            int chanceTenths)
        {
            if (lifecycle == null)
            {
                throw new ArgumentNullException(nameof(lifecycle));
            }
            if (impactedTarget == null)
            {
                throw new ArgumentNullException(nameof(impactedTarget));
            }
            if (chanceTenths < 1 || chanceTenths > 9)
            {
                throw new ArgumentOutOfRangeException(nameof(chanceTenths));
            }

            WeaponEffectIdentity identity = lifecycle.Identity.SourceIdentity;
            DeterministicRandom decisionStream = DeterministicRandom.CreateSubstream(
                lifecycle.Random.StreamSeed,
                lifecycle.Random.AlgorithmVersion,
                PierceDecisionPurpose,
                checked((ulong)identity.ShotSequence));
            decisionStream = DeterministicRandom.CreateSubstream(
                decisionStream.StreamSeed,
                decisionStream.AlgorithmVersion,
                ProjectileOrdinalPurpose,
                checked((ulong)identity.ProjectileOrdinal.Value));
            decisionStream = DeterministicRandom.CreateSubstream(
                decisionStream.StreamSeed,
                decisionStream.AlgorithmVersion,
                identity.ActorId.Value,
                checked((ulong)identity.LifecycleGeneration.Value));
            decisionStream = DeterministicRandom.CreateSubstream(
                decisionStream.StreamSeed,
                decisionStream.AlgorithmVersion,
                identity.ParticipantId.Value,
                0UL);
            decisionStream = DeterministicRandom.CreateSubstream(
                decisionStream.StreamSeed,
                decisionStream.AlgorithmVersion,
                identity.EquipmentInstanceId.Value,
                0UL);
            decisionStream = DeterministicRandom.CreateSubstream(
                decisionStream.StreamSeed,
                decisionStream.AlgorithmVersion,
                identity.FireOperationId.Value,
                0UL);
            decisionStream = DeterministicRandom.CreateSubstream(
                decisionStream.StreamSeed,
                decisionStream.AlgorithmVersion,
                impactedTarget.ActorId.Value,
                checked((ulong)impactedTarget.LifecycleGeneration.Value));
            decisionStream.NextChance(
                (ulong)chanceTenths,
                10UL,
                out bool granted);
            return granted;
        }
    }

    /// <summary>
    /// Resolves enemy/range/termination lifecycle decisions and coordinates wall contacts without
    /// owning wall continuation, ricochet, or explosion-reason policy.
    /// </summary>
    public sealed class ProjectileImpactResolver
    {
        private readonly IProjectileFractionalPierceRoller fractionalPierceRoller;

        public ProjectileImpactResolver(
            IProjectileFractionalPierceRoller fractionalPierceRoller)
        {
            this.fractionalPierceRoller = fractionalPierceRoller
                ?? throw new ArgumentNullException(nameof(fractionalPierceRoller));
        }

        public ProjectileImpactDecision Resolve(
            ProjectileLifecycleState state,
            ProjectileContact contact)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            if (contact == null)
            {
                throw new ArgumentNullException(nameof(contact));
            }
            if (!state.IsActive)
            {
                throw new InvalidOperationException(
                    "Only active projectiles may begin impact resolution.");
            }

            switch (contact.Kind)
            {
                case ProjectileContactKind.Enemy:
                    return ResolveEnemy(state, contact);
                case ProjectileContactKind.Wall:
                    return ResolveWall(state, contact);
                case ProjectileContactKind.RangeExpiry:
                    return ResolveRangeExpiry(state, contact);
                case ProjectileContactKind.ExplicitTermination:
                    return ResolveExplicitTermination(state, contact);
                default:
                    throw new ArgumentOutOfRangeException(nameof(contact));
            }
        }

        public ProjectileImpactDecision ApplyWallResolution(
            ProjectileImpactDecision pendingDecision,
            ProjectileWallImpactResolution resolution)
        {
            if (pendingDecision == null)
            {
                throw new ArgumentNullException(nameof(pendingDecision));
            }
            if (resolution == null)
            {
                throw new ArgumentNullException(nameof(resolution));
            }
            if (!pendingDecision.RequiresWallImpactResolution
                || pendingDecision.Contact.Kind != ProjectileContactKind.Wall)
            {
                throw new ArgumentException(
                    "The supplied decision is not awaiting external wall-impact resolution.",
                    nameof(pendingDecision));
            }

            ProjectileLifecycleState result;
            switch (resolution.Kind)
            {
                case ProjectileWallImpactResolutionKind.SuccessfulBounce:
                    result = pendingDecision.StateAfter.ResolveSuccessfulWallBounce(resolution);
                    break;
                case ProjectileWallImpactResolutionKind.BlockingImpact:
                    result = pendingDecision.StateAfter.ResolveBlockingWallImpact();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(resolution));
            }

            return new ProjectileImpactDecision(
                pendingDecision.StateBefore,
                result,
                pendingDecision.Contact,
                ProjectileImpactDecisionStatus.Resolved,
                false,
                resolution.ExplosionReasons);
        }

        private ProjectileImpactDecision ResolveEnemy(
            ProjectileLifecycleState state,
            ProjectileContact contact)
        {
            if (!state.Profile.Impact.HandlesEnemyImpact)
            {
                return Ignored(state, contact);
            }
            if (contact.Target == null)
            {
                throw new ArgumentException(
                    "Enemy contacts require exact actor and lifecycle identity.",
                    nameof(contact));
            }

            ProjectileLifecycleState contacted = state.RecordContact(contact);
            ProjectilePierceState pierce = contacted.Pierce.RecordSuccessfulEnemyImpact();
            contacted = contacted.WithPierce(pierce);

            bool continues;
            ProjectileTerminationReason terminationReason = ProjectileTerminationReason.None;
            switch (state.Profile.Projectile.TerminationBehavior)
            {
                case WeaponProjectileTerminationBehavior.StopOnFirstBlockingImpact:
                    continues = false;
                    terminationReason = ProjectileTerminationReason.EnemyImpact;
                    break;
                case WeaponProjectileTerminationBehavior.ContinueUntilRangeExpiry:
                    continues = true;
                    break;
                case WeaponProjectileTerminationBehavior.StopWhenPierceIsSpent:
                    continues = TrySpendPierceContinuation(
                        contacted,
                        contact.Target,
                        out contacted);
                    if (!continues)
                    {
                        terminationReason = ProjectileTerminationReason.PierceSpent;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(state.Profile.Projectile.TerminationBehavior));
            }

            ProjectileLifecycleState result = continues
                ? contacted
                : contacted.Terminate(terminationReason);
            WeaponExplosionTriggerReason explosionReasons = ResolveExplosionReasons(
                state.Profile.Impact.ExplosionTrigger,
                contact.Kind,
                !continues);

            return new ProjectileImpactDecision(
                state,
                result,
                contact,
                ProjectileImpactDecisionStatus.Resolved,
                true,
                explosionReasons);
        }

        private bool TrySpendPierceContinuation(
            ProjectileLifecycleState state,
            WeaponTargetReference impactedTarget,
            out ProjectileLifecycleState updated)
        {
            ProjectilePierceState pierce = state.Pierce;
            if (pierce.HasGuaranteedContinuation)
            {
                updated = state.WithPierce(pierce.ConsumeGuaranteedContinuation());
                return true;
            }

            if (pierce.FractionalRollPending)
            {
                bool granted = fractionalPierceRoller.Roll(
                    state.Lifecycle,
                    impactedTarget,
                    pierce.FractionalChanceTenths);
                updated = state.WithPierce(pierce.ResolveFractionalContinuation(granted));
                return granted;
            }

            updated = state;
            return false;
        }

        private static ProjectileImpactDecision ResolveWall(
            ProjectileLifecycleState state,
            ProjectileContact contact)
        {
            if (!state.Profile.Impact.HandlesWallImpact)
            {
                return Ignored(state, contact);
            }
            if (contact.SurfaceId == null)
            {
                throw new ArgumentException(
                    "Wall contacts require stable surface identity.",
                    nameof(contact));
            }

            ProjectileLifecycleState awaiting = state.AwaitWallImpactResolution(contact);
            return new ProjectileImpactDecision(
                state,
                awaiting,
                contact,
                ProjectileImpactDecisionStatus.RequiresWallImpactResolution,
                false,
                WeaponExplosionTriggerReason.None);
        }

        private static ProjectileImpactDecision ResolveRangeExpiry(
            ProjectileLifecycleState state,
            ProjectileContact contact)
        {
            if (!state.Profile.Impact.HandlesRangeExpiry)
            {
                return Ignored(state, contact);
            }
            if (state.RemainingRange > 1e-9d)
            {
                throw new InvalidOperationException(
                    "Range expiry cannot resolve before movement reaches the authored range limit.");
            }

            ProjectileLifecycleState contacted = state.RecordContact(contact);
            ProjectileLifecycleState result = contacted.Terminate(
                ProjectileTerminationReason.RangeExpired);
            WeaponExplosionTriggerReason explosionReasons = ResolveExplosionReasons(
                state.Profile.Impact.ExplosionTrigger,
                contact.Kind,
                true);

            return new ProjectileImpactDecision(
                state,
                result,
                contact,
                ProjectileImpactDecisionStatus.Resolved,
                false,
                explosionReasons);
        }

        private static ProjectileImpactDecision ResolveExplicitTermination(
            ProjectileLifecycleState state,
            ProjectileContact contact)
        {
            if (!state.Profile.Impact.HandlesTermination)
            {
                return Ignored(state, contact);
            }

            ProjectileLifecycleState contacted = state.RecordContact(contact);
            ProjectileLifecycleState result = contacted.Terminate(
                ProjectileTerminationReason.ExplicitTermination);
            WeaponExplosionTriggerReason explosionReasons = ResolveExplosionReasons(
                state.Profile.Impact.ExplosionTrigger,
                contact.Kind,
                true);

            return new ProjectileImpactDecision(
                state,
                result,
                contact,
                ProjectileImpactDecisionStatus.Resolved,
                false,
                explosionReasons);
        }

        private static ProjectileImpactDecision Ignored(
            ProjectileLifecycleState state,
            ProjectileContact contact)
        {
            return new ProjectileImpactDecision(
                state,
                state,
                contact,
                ProjectileImpactDecisionStatus.Ignored,
                false,
                WeaponExplosionTriggerReason.None);
        }

        private static WeaponExplosionTriggerReason ResolveExplosionReasons(
            WeaponExplosionTriggerSpec trigger,
            ProjectileContactKind contactKind,
            bool terminates)
        {
            if (trigger == null)
            {
                return WeaponExplosionTriggerReason.None;
            }

            WeaponExplosionTriggerReason reasons = WeaponExplosionTriggerReason.None;
            if (contactKind == ProjectileContactKind.Enemy && trigger.OnEnemyImpact)
            {
                reasons |= WeaponExplosionTriggerReason.EnemyImpact;
            }
            if (contactKind == ProjectileContactKind.RangeExpiry && trigger.OnRangeExpiry)
            {
                reasons |= WeaponExplosionTriggerReason.RangeExpiry;
            }
            if (terminates && trigger.OnTermination)
            {
                reasons |= WeaponExplosionTriggerReason.Termination;
            }
            return reasons;
        }
    }
}
