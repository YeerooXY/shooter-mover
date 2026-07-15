using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using ShooterMover.ContentPackages.Enemies.Stage1;
using ShooterMover.ContentPackages.Weapons.BlasterMachineGun;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Content;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;

namespace ShooterMover.ContentPackages.Enemies.FourBlasterElite
{
    public enum FourBlasterEliteCadenceStage
    {
        Telegraph = 1,
        Volley = 2,
        Recovery = 3,
        Complete = 4,
    }

    /// <summary>
    /// Package-owned warning facts. The warning remains understandable without hue,
    /// animation, particles, bloom, or screen shake.
    /// </summary>
    public sealed class FourBlasterEliteWarningCue
    {
        internal FourBlasterEliteWarningCue(
            bool isVisible,
            double normalizedProgress,
            double maximumSpreadDegrees)
        {
            IsVisible = isVisible;
            NormalizedProgress = normalizedProgress;
            MaximumSpreadDegrees = maximumSpreadDegrees;
        }

        public bool IsVisible { get; }

        public double NormalizedProgress { get; }

        public double MaximumSpreadDegrees { get; }

        public int OriginMarkerCount
        {
            get { return FourBlasterElitePackage.OriginCount; }
        }

        public string ShapeToken
        {
            get { return "four-spoke-dashed-outline"; }
        }

        public string ReducedEffectsToken
        {
            get { return "four-static-spokes-with-countdown-bars"; }
        }

        public bool UsesColorOnly
        {
            get { return false; }
        }

        public bool IsReducedEffectsReadable
        {
            get { return true; }
        }
    }

    /// <summary>
    /// One ordered origin firing the accepted WP-003 Blaster plan.
    /// </summary>
    public sealed class FourBlasterEliteShot
    {
        internal FourBlasterEliteShot(
            int originIndex,
            StableId originId,
            double spreadDegrees,
            WeaponFireExecutionPlan executionPlan)
        {
            OriginIndex = originIndex;
            OriginId = originId ?? throw new ArgumentNullException(nameof(originId));
            SpreadDegrees = spreadDegrees;
            ExecutionPlan = executionPlan
                ?? throw new ArgumentNullException(nameof(executionPlan));
        }

        public int OriginIndex { get; }

        public StableId OriginId { get; }

        public double SpreadDegrees { get; }

        public WeaponFireExecutionPlan ExecutionPlan { get; }
    }

    public sealed class FourBlasterEliteCadenceResult
    {
        private readonly ReadOnlyCollection<FourBlasterEliteShot> shots;

        internal FourBlasterEliteCadenceResult(
            FourBlasterEliteCadenceStage stage,
            long cycleIndex,
            IList<FourBlasterEliteShot> shots,
            FourBlasterEliteWarningCue warningCue)
        {
            Stage = stage;
            CycleIndex = cycleIndex;
            this.shots = new ReadOnlyCollection<FourBlasterEliteShot>(
                new List<FourBlasterEliteShot>(shots));
            WarningCue = warningCue ?? throw new ArgumentNullException(nameof(warningCue));
        }

        public FourBlasterEliteCadenceStage Stage { get; }

        public long CycleIndex { get; }

        public IReadOnlyList<FourBlasterEliteShot> Shots
        {
            get { return shots; }
        }

        public FourBlasterEliteWarningCue WarningCue { get; }
    }

    /// <summary>
    /// EN-008 package identity and intentionally easy first-boss tuning.
    /// </summary>
    public static class FourBlasterElitePackage
    {
        public const int OriginCount = 4;
        public const double MaximumHealth = 160d;
        public const double TelegraphSeconds = 0.75d;
        public const double InterOriginSeconds = 0.15d;
        public const double RecoverySeconds = 1.50d;
        public const double MaximumSpreadDegrees = 8d;
        public const double MaximumAdvanceSeconds = 60d;

        private static readonly double[] OriginOffsetX = { -0.75d, 0.75d, -0.75d, 0.75d };
        private static readonly double[] OriginOffsetY = { 0.45d, 0.45d, -0.45d, -0.45d };
        private static readonly double[] SpreadDegrees = { -6d, -2d, 2d, 6d };
        private static readonly StableId[] OriginIds =
        {
            StableId.Parse("origin.enemy-four-blaster-elite-1"),
            StableId.Parse("origin.enemy-four-blaster-elite-2"),
            StableId.Parse("origin.enemy-four-blaster-elite-3"),
            StableId.Parse("origin.enemy-four-blaster-elite-4"),
        };
        private static readonly StableId[] MountIds =
        {
            StableId.Parse("mount.enemy-four-blaster-elite-1"),
            StableId.Parse("mount.enemy-four-blaster-elite-2"),
            StableId.Parse("mount.enemy-four-blaster-elite-3"),
            StableId.Parse("mount.enemy-four-blaster-elite-4"),
        };

        public static Stage1EnemyPackageDescriptor CreateDescriptor()
        {
            ContentReference movement = SharedModule("module.enemy-elite-hold-position");
            ContentReference attack = ContentReference.Create(
                BlasterMachineGunPackage.WeaponId,
                ContentDefinitionKind.Weapon,
                ContentReference.SupportedDefinitionVersion);
            ContentReference telegraph = SharedModule("module.enemy-four-origin-telegraph");
            ContentDefinitionDescriptor content = ContentDefinitionDescriptor.Create(
                Stage1EnemyPackageDescriptor.FourBlasterEliteId,
                ContentDefinitionKind.Enemy,
                ContentReference.SupportedDefinitionVersion,
                StableId.Create("provenance", "enemy-four-blaster-elite-en008"),
                false,
                movement,
                attack,
                telegraph);

            return Stage1EnemyPackageDescriptor.Create(
                Stage1EnemyPackageDescriptor.CurrentDescriptorVersion,
                content,
                Stage1EnemyPackageClassification.Elite,
                CombatChannel.Kinetic,
                CombatWeightClass.Heavy,
                movement,
                attack,
                telegraph,
                Stage1EnemyCapability.BlasterProjectile
                    | Stage1EnemyCapability.FourBlasterOrigins
                    | Stage1EnemyCapability.MildBoundedSpread
                    | Stage1EnemyCapability.SafeRecoveryWindow);
        }

        public static FourBlasterEliteSession CreateSession(StableId actorId)
        {
            return new FourBlasterEliteSession(actorId);
        }

        public static string CreateReadabilitySnapshot()
        {
            return string.Join(
                "\n",
                new[]
                {
                    "warning_shape=four-spoke-dashed-outline",
                    "origin_markers=4",
                    "countdown_bars=4",
                    "color_only=false",
                    "reduced_effects=four-static-spokes-with-countdown-bars",
                    "spread_cap_degrees="
                        + MaximumSpreadDegrees.ToString("R", CultureInfo.InvariantCulture),
                });
        }

        internal static EnemyActorState CreateInitialState(StableId actorId)
        {
            if (actorId == null)
            {
                throw new ArgumentNullException(nameof(actorId));
            }

            EnemyContactPolicy noContactDamage = EnemyContactPolicy.Create(
                EnemyContactMode.None,
                0d,
                1d,
                0d,
                1);
            return EnemyActorState.Create(
                actorId,
                Stage1EnemyPackageDescriptor.FourBlasterEliteId,
                MaximumHealth,
                (int)CombatWeightClass.Heavy,
                noContactDamage);
        }

        internal static WeaponBehaviorPipeline CreateBlasterPipeline()
        {
            return new WeaponBehaviorPipeline(
                new[] { BlasterMachineGunPackage.CreateBehaviorModule() });
        }

        internal static FourBlasterEliteShot BuildShot(
            WeaponBehaviorPipeline pipeline,
            StableId actorId,
            long cycleIndex,
            int originIndex,
            double centerX,
            double centerY,
            double normalizedAimX,
            double normalizedAimY)
        {
            if (pipeline == null)
            {
                throw new ArgumentNullException(nameof(pipeline));
            }

            if (actorId == null)
            {
                throw new ArgumentNullException(nameof(actorId));
            }

            if (originIndex < 0 || originIndex >= OriginCount)
            {
                throw new ArgumentOutOfRangeException(nameof(originIndex));
            }

            double angleRadians = SpreadDegrees[originIndex] * Math.PI / 180d;
            double cosine = Math.Cos(angleRadians);
            double sine = Math.Sin(angleRadians);
            double directionX = (normalizedAimX * cosine) - (normalizedAimY * sine);
            double directionY = (normalizedAimX * sine) + (normalizedAimY * cosine);
            double originX = centerX + OriginOffsetX[originIndex];
            double originY = centerY + OriginOffsetY[originIndex];
            long simulationStep = checked((cycleIndex * OriginCount) + originIndex);

            WeaponBehaviorInput input = new WeaponBehaviorInput(
                CreateCombatEventId(actorId, cycleIndex, originIndex),
                BlasterMachineGunPackage.WeaponId,
                MountIds[originIndex],
                simulationStep,
                BlasterMachineGunPackage.GetNormalRuntimeProfile(),
                false,
                originX,
                originY,
                directionX,
                directionY,
                1d);
            WeaponFireExecutionPlan plan = pipeline.BuildExecutionPlan(input);
            if (plan.FaultCount != 0 || plan.OperationCount != 1)
            {
                throw new InvalidOperationException(
                    "The accepted Blaster package did not produce its single bounded projectile plan.");
            }

            return new FourBlasterEliteShot(
                originIndex,
                OriginIds[originIndex],
                SpreadDegrees[originIndex],
                plan);
        }

        private static ContentReference SharedModule(string stableId)
        {
            return ContentReference.Create(
                StableId.Parse(stableId),
                ContentDefinitionKind.SharedModule,
                ContentReference.SupportedDefinitionVersion);
        }

        private static StableId CreateCombatEventId(
            StableId actorId,
            long cycleIndex,
            int originIndex)
        {
            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            string text = actorId
                + "|"
                + cycleIndex.ToString(CultureInfo.InvariantCulture)
                + "|"
                + originIndex.ToString(CultureInfo.InvariantCulture)
                + "|four-blaster-elite-shot";
            ulong hash = offsetBasis;
            for (int index = 0; index < text.Length; index++)
            {
                hash ^= text[index];
                hash *= prime;
            }

            return StableId.Create(
                "event",
                "four-blaster-elite-" + hash.ToString("x16", CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// One session owns exactly one EN-002 health state and one deterministic cadence.
    /// The four origins never own independent health, phases, target search, or rewards.
    /// </summary>
    public sealed class FourBlasterEliteSession
    {
        private readonly StableId actorId;
        private readonly WeaponBehaviorPipeline blasterPipeline;
        private EnemyActorState actorState;
        private FourBlasterEliteCadenceStage stage;
        private decimal stageElapsedSeconds;
        private int nextOriginIndex;
        private long cycleIndex;
        private bool completionEmitted;
        private int completionCount;

        internal FourBlasterEliteSession(StableId actorId)
        {
            this.actorId = actorId ?? throw new ArgumentNullException(nameof(actorId));
            blasterPipeline = FourBlasterElitePackage.CreateBlasterPipeline();
            ResetState();
        }

        public EnemyActorState ActorState
        {
            get { return actorState; }
        }

        public FourBlasterEliteCadenceStage Stage
        {
            get { return stage; }
        }

        public long CycleIndex
        {
            get { return cycleIndex; }
        }

        public bool CompletionEmitted
        {
            get { return completionEmitted; }
        }

        public int CompletionCount
        {
            get { return completionCount; }
        }

        public FourBlasterEliteWarningCue WarningCue
        {
            get { return CreateWarningCue(); }
        }

        public FourBlasterEliteCadenceResult Advance(
            double deltaTimeSeconds,
            double centerX,
            double centerY,
            double targetX,
            double targetY)
        {
            RequireFinite(deltaTimeSeconds, nameof(deltaTimeSeconds));
            RequireFinite(centerX, nameof(centerX));
            RequireFinite(centerY, nameof(centerY));
            RequireFinite(targetX, nameof(targetX));
            RequireFinite(targetY, nameof(targetY));
            if (deltaTimeSeconds < 0d
                || deltaTimeSeconds > FourBlasterElitePackage.MaximumAdvanceSeconds)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTimeSeconds));
            }

            List<FourBlasterEliteShot> shots = new List<FourBlasterEliteShot>();
            if (!actorState.IsActive)
            {
                stage = FourBlasterEliteCadenceStage.Complete;
                return Result(shots);
            }

            double aimX = targetX - centerX;
            double aimY = targetY - centerY;
            double aimLengthSquared = (aimX * aimX) + (aimY * aimY);
            if (aimLengthSquared <= 0d
                || double.IsNaN(aimLengthSquared)
                || double.IsInfinity(aimLengthSquared))
            {
                throw new ArgumentException("The elite requires one finite non-zero aim vector.");
            }

            double inverseAimLength = 1d / Math.Sqrt(aimLengthSquared);
            aimX *= inverseAimLength;
            aimY *= inverseAimLength;

            decimal remaining = (decimal)deltaTimeSeconds;
            int transitionCount = 0;
            while (remaining > 0m)
            {
                transitionCount++;
                if (transitionCount > 128)
                {
                    throw new InvalidOperationException(
                        "Cadence transition bound exceeded for one advance call.");
                }

                if (stage == FourBlasterEliteCadenceStage.Telegraph)
                {
                    decimal duration = (decimal)FourBlasterElitePackage.TelegraphSeconds;
                    decimal untilTransition = duration - stageElapsedSeconds;
                    if (remaining < untilTransition)
                    {
                        stageElapsedSeconds += remaining;
                        remaining = 0m;
                        continue;
                    }

                    remaining -= untilTransition;
                    stageElapsedSeconds = 0m;
                    stage = FourBlasterEliteCadenceStage.Volley;
                    nextOriginIndex = 0;
                    EmitNextShot(shots, centerX, centerY, aimX, aimY);
                    continue;
                }

                if (stage == FourBlasterEliteCadenceStage.Volley)
                {
                    decimal duration = (decimal)FourBlasterElitePackage.InterOriginSeconds;
                    decimal untilTransition = duration - stageElapsedSeconds;
                    if (remaining < untilTransition)
                    {
                        stageElapsedSeconds += remaining;
                        remaining = 0m;
                        continue;
                    }

                    remaining -= untilTransition;
                    stageElapsedSeconds = 0m;
                    EmitNextShot(shots, centerX, centerY, aimX, aimY);
                    continue;
                }

                if (stage == FourBlasterEliteCadenceStage.Recovery)
                {
                    decimal duration = (decimal)FourBlasterElitePackage.RecoverySeconds;
                    decimal untilTransition = duration - stageElapsedSeconds;
                    if (remaining < untilTransition)
                    {
                        stageElapsedSeconds += remaining;
                        remaining = 0m;
                        continue;
                    }

                    remaining -= untilTransition;
                    stageElapsedSeconds = 0m;
                    if (cycleIndex == long.MaxValue / FourBlasterElitePackage.OriginCount)
                    {
                        throw new InvalidOperationException("Elite cadence cycle identity exhausted.");
                    }

                    cycleIndex++;
                    nextOriginIndex = 0;
                    stage = FourBlasterEliteCadenceStage.Telegraph;
                    continue;
                }

                remaining = 0m;
            }

            return Result(shots);
        }

        public EnemyActorStepResult ApplyDamage(
            StableId eventId,
            StableId sourceId,
            CombatChannel channel,
            double amount,
            long order)
        {
            if (eventId == null)
            {
                throw new ArgumentNullException(nameof(eventId));
            }

            if (sourceId == null)
            {
                throw new ArgumentNullException(nameof(sourceId));
            }

            EnemyActorStepResult result = EnemyActorStepper.Step(
                actorState,
                new[]
                {
                    EnemyActorCommand.Damage(
                        order,
                        eventId,
                        sourceId,
                        (int)channel,
                        amount),
                });
            actorState = result.State;

            for (int index = 0; index < result.Notifications.Count; index++)
            {
                if (result.Notifications[index] is EnemyEncounterResolutionNotification
                    && !completionEmitted)
                {
                    completionEmitted = true;
                    completionCount++;
                }
            }

            if (!actorState.IsActive)
            {
                stage = FourBlasterEliteCadenceStage.Complete;
                stageElapsedSeconds = 0m;
                nextOriginIndex = 0;
            }

            return result;
        }

        public bool RestartSession()
        {
            ResetState();
            return true;
        }

        private void EmitNextShot(
            IList<FourBlasterEliteShot> shots,
            double centerX,
            double centerY,
            double aimX,
            double aimY)
        {
            shots.Add(
                FourBlasterElitePackage.BuildShot(
                    blasterPipeline,
                    actorId,
                    cycleIndex,
                    nextOriginIndex,
                    centerX,
                    centerY,
                    aimX,
                    aimY));
            nextOriginIndex++;
            if (nextOriginIndex >= FourBlasterElitePackage.OriginCount)
            {
                nextOriginIndex = 0;
                stage = FourBlasterEliteCadenceStage.Recovery;
                stageElapsedSeconds = 0m;
            }
        }

        private FourBlasterEliteCadenceResult Result(IList<FourBlasterEliteShot> shots)
        {
            return new FourBlasterEliteCadenceResult(
                stage,
                cycleIndex,
                shots,
                CreateWarningCue());
        }

        private FourBlasterEliteWarningCue CreateWarningCue()
        {
            bool visible = stage == FourBlasterEliteCadenceStage.Telegraph
                && actorState != null
                && actorState.IsActive;
            double progress = visible
                ? (double)(stageElapsedSeconds
                    / (decimal)FourBlasterElitePackage.TelegraphSeconds)
                : 0d;
            return new FourBlasterEliteWarningCue(
                visible,
                progress,
                FourBlasterElitePackage.MaximumSpreadDegrees);
        }

        private void ResetState()
        {
            actorState = FourBlasterElitePackage.CreateInitialState(actorId);
            stage = FourBlasterEliteCadenceStage.Telegraph;
            stageElapsedSeconds = 0m;
            nextOriginIndex = 0;
            cycleIndex = 0L;
            completionEmitted = false;
            completionCount = 0;
        }

        private static void RequireFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}
