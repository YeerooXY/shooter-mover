using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.ContentPackages.Weapons.Stage1;
using ShooterMover.Contracts.Content;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;

namespace ShooterMover.ContentPackages.Weapons.ArcGun
{
    public sealed class ArcTargetSnapshot
    {
        private ArcTargetSnapshot(StableId targetId, double x, double y, bool alive, bool valid)
        {
            if (targetId == null) throw new ArgumentNullException(nameof(targetId));
            RequireFinite(x, nameof(x));
            RequireFinite(y, nameof(y));
            TargetId = targetId;
            PositionX = x;
            PositionY = y;
            IsAlive = alive;
            IsValid = valid;
        }

        public StableId TargetId { get; }
        public double PositionX { get; }
        public double PositionY { get; }
        public bool IsAlive { get; }
        public bool IsValid { get; }
        public bool IsEligible { get { return IsAlive && IsValid; } }

        public static ArcTargetSnapshot Create(
            StableId targetId,
            double positionX,
            double positionY,
            bool isAlive,
            bool isValid)
        {
            return new ArcTargetSnapshot(targetId, positionX, positionY, isAlive, isValid);
        }

        private static void RequireFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, "Position must be finite.");
            }
        }
    }

    public sealed class ArcChainStep
    {
        internal ArcChainStep(int index, StableId sourceId, StableId targetId, double distanceSquared)
        {
            AdditionalTargetIndex = index;
            SourceTargetId = sourceId;
            TargetId = targetId;
            DistanceSquared = distanceSquared;
        }

        public int AdditionalTargetIndex { get; }
        public StableId SourceTargetId { get; }
        public StableId TargetId { get; }
        public double DistanceSquared { get; }
    }

    public sealed class ArcChainResult
    {
        private readonly ReadOnlyCollection<ArcChainStep> steps;
        private readonly string canonicalText;

        internal ArcChainResult(StableId primaryTargetId, IList<ArcChainStep> sourceSteps)
        {
            PrimaryTargetId = primaryTargetId;
            steps = new ReadOnlyCollection<ArcChainStep>(new List<ArcChainStep>(sourceSteps));
            canonicalText = BuildCanonicalText();
        }

        public StableId PrimaryTargetId { get; }
        public int AdditionalHitCount { get { return steps.Count; } }
        public IReadOnlyList<ArcChainStep> Steps { get { return steps; } }

        public StableId GetAdditionalTargetId(int index)
        {
            if (index < 0 || index >= steps.Count) throw new ArgumentOutOfRangeException(nameof(index));
            return steps[index].TargetId;
        }

        public string ToCanonicalString() { return canonicalText; }
        public override string ToString() { return canonicalText; }

        private string BuildCanonicalText()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("primary_target_id=")
                .Append(PrimaryTargetId == null ? "null" : PrimaryTargetId.ToString())
                .Append("\nadditional_hit_count=")
                .Append(steps.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < steps.Count; index++)
            {
                ArcChainStep step = steps[index];
                builder.Append("\nstep_")
                    .Append(index.ToString("D2", CultureInfo.InvariantCulture))
                    .Append('=')
                    .Append(step.SourceTargetId)
                    .Append("->")
                    .Append(step.TargetId)
                    .Append('@')
                    .Append(step.DistanceSquared.ToString("R", CultureInfo.InvariantCulture));
            }
            return builder.ToString();
        }
    }

    /// <summary>
    /// Engine-independent nearest-hop resolver. Candidates are ordered by distance,
    /// then canonical StableId, and every chain owns a fresh visited set.
    /// </summary>
    public static class ArcGunChainResolver
    {
        public const int MaximumAdditionalTargets = 3;

        public static ArcChainResult Resolve(
            ArcTargetSnapshot primaryTarget,
            IEnumerable<ArcTargetSnapshot> candidateTargets,
            double maximumHopRange,
            Func<StableId, bool> tryConfirmTarget)
        {
            if (candidateTargets == null) throw new ArgumentNullException(nameof(candidateTargets));
            if (double.IsNaN(maximumHopRange)
                || double.IsInfinity(maximumHopRange)
                || maximumHopRange <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumHopRange));
            }

            if (primaryTarget == null || !primaryTarget.IsEligible)
            {
                return new ArcChainResult(
                    primaryTarget == null ? null : primaryTarget.TargetId,
                    new List<ArcChainStep>());
            }

            List<ArcTargetSnapshot> candidates = new List<ArcTargetSnapshot>();
            foreach (ArcTargetSnapshot candidate in candidateTargets)
            {
                if (candidate != null) candidates.Add(candidate);
            }

            HashSet<StableId> visited = new HashSet<StableId> { primaryTarget.TargetId };
            List<ArcChainStep> selected = new List<ArcChainStep>();
            ArcTargetSnapshot current = primaryTarget;
            double maximumDistanceSquared = maximumHopRange * maximumHopRange;

            while (selected.Count < MaximumAdditionalTargets)
            {
                List<RankedCandidate> ranked = Rank(
                    current,
                    candidates,
                    visited,
                    maximumDistanceSquared);
                ArcTargetSnapshot next = null;
                double selectedDistanceSquared = 0d;
                for (int index = 0; index < ranked.Count; index++)
                {
                    RankedCandidate candidate = ranked[index];
                    if (tryConfirmTarget != null && !tryConfirmTarget(candidate.Target.TargetId))
                    {
                        visited.Add(candidate.Target.TargetId);
                        continue;
                    }
                    next = candidate.Target;
                    selectedDistanceSquared = candidate.DistanceSquared;
                    break;
                }

                if (next == null) break;
                visited.Add(next.TargetId);
                selected.Add(new ArcChainStep(
                    selected.Count,
                    current.TargetId,
                    next.TargetId,
                    selectedDistanceSquared));
                current = next;
            }

            return new ArcChainResult(primaryTarget.TargetId, selected);
        }

        private static List<RankedCandidate> Rank(
            ArcTargetSnapshot current,
            IEnumerable<ArcTargetSnapshot> candidates,
            HashSet<StableId> visited,
            double maximumDistanceSquared)
        {
            Dictionary<StableId, RankedCandidate> unique =
                new Dictionary<StableId, RankedCandidate>();
            foreach (ArcTargetSnapshot candidate in candidates)
            {
                if (!candidate.IsEligible || visited.Contains(candidate.TargetId)) continue;
                double x = candidate.PositionX - current.PositionX;
                double y = candidate.PositionY - current.PositionY;
                double distanceSquared = (x * x) + (y * y);
                if (distanceSquared > maximumDistanceSquared) continue;

                RankedCandidate ranked = new RankedCandidate(candidate, distanceSquared);
                RankedCandidate existing;
                if (!unique.TryGetValue(candidate.TargetId, out existing)
                    || Compare(ranked, existing) < 0)
                {
                    unique[candidate.TargetId] = ranked;
                }
            }

            List<RankedCandidate> ordered = new List<RankedCandidate>(unique.Values);
            ordered.Sort(Compare);
            return ordered;
        }

        private static int Compare(RankedCandidate left, RankedCandidate right)
        {
            int distance = left.DistanceSquared.CompareTo(right.DistanceSquared);
            return distance != 0 ? distance : left.Target.TargetId.CompareTo(right.Target.TargetId);
        }

        private sealed class RankedCandidate
        {
            public RankedCandidate(ArcTargetSnapshot target, double distanceSquared)
            {
                Target = target;
                DistanceSquared = distanceSquared;
            }
            public ArcTargetSnapshot Target { get; }
            public double DistanceSquared { get; }
        }
    }

    public sealed class ArcGunChainSession
    {
        public int Generation { get; private set; }
        public ArcChainResult LastResult { get; private set; }

        public ArcChainResult Resolve(
            ArcTargetSnapshot primaryTarget,
            IEnumerable<ArcTargetSnapshot> candidateTargets,
            double maximumHopRange,
            Func<StableId, bool> tryConfirmTarget)
        {
            LastResult = ArcGunChainResolver.Resolve(
                primaryTarget,
                candidateTargets,
                maximumHopRange,
                tryConfirmTarget);
            return LastResult;
        }

        public void Reset()
        {
            LastResult = null;
            Generation++;
        }
    }

    public sealed class ArcGunTuning
    {
        internal ArcGunTuning(double damage, double range, double cadence, int targetCap)
        {
            Damage = damage;
            EffectRange = range;
            CadenceSeconds = cadence;
            AdditionalTargetCap = targetCap;
        }
        public double Damage { get; }
        public double EffectRange { get; }
        public double CadenceSeconds { get; }
        public int AdditionalTargetCap { get; }
    }

    public static class ArcGunPackage
    {
        private static readonly StableId WeaponIdValue = StableId.Parse("weapon.arc-gun");
        private static readonly StableId ModuleIdValue = StableId.Parse("module.weapon-arc-chain");

        public static StableId WeaponId { get { return WeaponIdValue; } }
        public static StableId ModuleId { get { return ModuleIdValue; } }

        public static Stage1WeaponPackageDescriptor CreateDescriptor()
        {
            ContentReference moduleReference = ContentReference.Create(
                ModuleIdValue,
                ContentDefinitionKind.SharedModule,
                ContentReference.SupportedDefinitionVersion);
            ContentDefinitionDescriptor content = ContentDefinitionDescriptor.Create(
                WeaponIdValue,
                ContentDefinitionKind.Weapon,
                ContentReference.SupportedDefinitionVersion,
                StableId.Parse("provenance.original-arc-gun-package"),
                false,
                moduleReference);
            Stage1WeaponBehaviorTopology topology = Stage1WeaponBehaviorTopology.Create(
                Stage1WeaponBehaviorKind.ArcChain,
                ArcGunChainResolver.MaximumAdditionalTargets,
                0,
                0,
                false);

            Stage1WeaponFireProfile normal = CreateFireProfile(false, topology, 12d, 6d);
            Stage1WeaponFireProfile empowered = CreateFireProfile(true, topology, 16d, 7d);
            return Stage1WeaponPackageDescriptor.Create(
                Stage1WeaponPackageDescriptor.CurrentDescriptorVersion,
                content,
                false,
                normal,
                empowered);
        }

        public static Stage1WeaponFireProfile SelectFireProfile(WeaponPowerFireDecision decision)
        {
            if (decision == null) throw new ArgumentNullException(nameof(decision));
            Stage1WeaponPackageDescriptor descriptor = CreateDescriptor();
            if (decision.FiresEmpowered) return descriptor.EmpoweredFire;
            if (decision.FiresNormally) return descriptor.NormalFire;
            throw new InvalidOperationException("A not-ready decision cannot select a fire profile.");
        }

        public static ArcGunTuning GetTuning(Stage1WeaponFireProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (profile.Topology == null
                || profile.Topology.Kind != Stage1WeaponBehaviorKind.ArcChain
                || profile.Topology.AdditionalTargetCount != ArcGunChainResolver.MaximumAdditionalTargets
                || profile.Topology.WallBounceCount != 0
                || profile.Topology.DetonationCount != 0
                || profile.Topology.HasFragmentation)
            {
                throw new ArgumentException("Arc Gun topology must remain primary plus three.", nameof(profile));
            }

            double damage = RequiredCoefficient(profile, Stage1WeaponNumericCoefficientKind.Damage);
            double range = RequiredCoefficient(profile, Stage1WeaponNumericCoefficientKind.EffectRange);
            if (damage <= 0d || range <= 0d) throw new ArgumentException("Arc tuning must be positive.");
            return new ArcGunTuning(
                damage,
                range,
                profile.RuntimeProfile.CadenceSeconds,
                ArcGunChainResolver.MaximumAdditionalTargets);
        }

        private static Stage1WeaponFireProfile CreateFireProfile(
            bool empowered,
            Stage1WeaponBehaviorTopology topology,
            double damage,
            double range)
        {
            return Stage1WeaponFireProfile.Create(
                CreateRuntimeProfile(empowered),
                topology,
                false,
                new[]
                {
                    Stage1WeaponNumericCoefficient.Create(
                        Stage1WeaponNumericCoefficientKind.Damage,
                        damage),
                    Stage1WeaponNumericCoefficient.Create(
                        Stage1WeaponNumericCoefficientKind.EffectRange,
                        range),
                });
        }

        private static WeaponRuntimeProfile CreateRuntimeProfile(bool empowered)
        {
            StableId[] modules = { ModuleIdValue };
            return WeaponRuntimeProfile.Create(
                WeaponRuntimeProfile.CurrentProfileVersion,
                StableId.Parse(empowered
                    ? "weapon-profile.arc-gun-empowered"
                    : "weapon-profile.arc-gun-normal"),
                empowered ? 0.45d : 0.65d,
                1,
                0d,
                empowered ? 0.08d : 0.12d,
                WeaponCycleMode.None,
                0d,
                0d,
                0d,
                0d,
                true,
                12d,
                3d,
                0d,
                modules,
                modules,
                2);
        }

        private static double RequiredCoefficient(
            Stage1WeaponFireProfile profile,
            Stage1WeaponNumericCoefficientKind kind)
        {
            double value = 0d;
            int found = 0;
            foreach (Stage1WeaponNumericCoefficient coefficient in profile.NumericCoefficients)
            {
                if (coefficient != null && coefficient.Kind == kind)
                {
                    value = coefficient.Value;
                    found++;
                }
            }
            if (found != 1 || double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentException("Arc profile coefficient is missing or malformed.", nameof(profile));
            }
            return value;
        }
    }

    public sealed class ArcGunChainOperation : IWeaponFireExecutionOperation
    {
        private static readonly StableId KindId = StableId.Parse("operation-kind.arc-chain");

        internal ArcGunChainOperation(WeaponBehaviorInput input, ArcGunTuning tuning)
        {
            CombatEventId = input.CombatEventId;
            WeaponId = input.WeaponId;
            MountId = input.MountId;
            SimulationStep = input.SimulationStep;
            IsEmpowered = input.IsEmpowered;
            Damage = tuning.Damage;
            EffectRange = tuning.EffectRange;
            MaximumAdditionalTargets = ArcGunChainResolver.MaximumAdditionalTargets;
            OperationId = StableId.Create("operation", PayloadHash());
        }

        public StableId OperationKindId { get { return KindId; } }
        public StableId OperationId { get; }
        public StableId CombatEventId { get; }
        public StableId WeaponId { get; }
        public StableId MountId { get; }
        public long SimulationStep { get; }
        public bool IsEmpowered { get; }
        public double Damage { get; }
        public double EffectRange { get; }
        public int MaximumAdditionalTargets { get; }

        private string PayloadHash()
        {
            string text = string.Join("\n", new[]
            {
                "kind=" + KindId,
                "event=" + CombatEventId,
                "weapon=" + WeaponId,
                "mount=" + MountId,
                "step=" + SimulationStep.ToString(CultureInfo.InvariantCulture),
                "empowered=" + (IsEmpowered ? "true" : "false"),
                "damage=" + Damage.ToString("R", CultureInfo.InvariantCulture),
                "range=" + EffectRange.ToString("R", CultureInfo.InvariantCulture),
                "cap=" + MaximumAdditionalTargets.ToString(CultureInfo.InvariantCulture),
            });
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                StringBuilder builder = new StringBuilder(bytes.Length * 2);
                foreach (byte value in bytes) builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }
    }

    public sealed class ArcGunBehaviorModule : IWeaponBehaviorModule
    {
        public StableId ModuleId { get { return ArcGunPackage.ModuleId; } }

        public WeaponBehaviorModulePlan BuildExecutionPlan(WeaponBehaviorInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (input.WeaponId != ArcGunPackage.WeaponId)
            {
                throw new ArgumentException("Arc module accepts only weapon.arc-gun.", nameof(input));
            }

            Stage1WeaponPackageDescriptor descriptor = ArcGunPackage.CreateDescriptor();
            Stage1WeaponFireProfile profile = input.IsEmpowered
                ? descriptor.EmpoweredFire
                : descriptor.NormalFire;
            if (!input.RuntimeProfile.Equals(profile.RuntimeProfile))
            {
                throw new ArgumentException("Input runtime profile does not match the Arc package.", nameof(input));
            }

            return new WeaponBehaviorModulePlan(
                ModuleId,
                new ArcGunChainOperation(input, ArcGunPackage.GetTuning(profile)));
        }
    }
}
