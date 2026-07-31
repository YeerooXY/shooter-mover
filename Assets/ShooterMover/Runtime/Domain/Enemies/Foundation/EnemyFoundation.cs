using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;

namespace ShooterMover.Domain.Enemies.Foundation
{
    public enum EnemyTier
    {
        One = 1,
        Two = 2,
        Three = 3,
        Four = 4,
    }

    public enum TravelMode
    {
        Ground = 1,
        Flying = 2,
    }

    public enum FireMode
    {
        Alternating = 1,
        Simultaneous = 2,
    }

    public enum MountOrder
    {
        Listed = 1,
        Cycle = 2,
        Weighted = 3,
    }

    public enum ModStage
    {
        Base = 0,
        Tier = 1,
        Difficulty = 2,
        Variant = 3,
        FixedPerks = 4,
        RolledPerks = 5,
        Phase = 6,
        Temporary = 7,
    }

    public sealed class Vec2
    {
        public Vec2(double x, double y)
        {
            ModelGuard.Finite(x, nameof(x));
            ModelGuard.Finite(y, nameof(y));
            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }
    }

    public sealed class MountDef
    {
        public MountDef(StableId id, Vec2 position, Vec2 direction)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Position = position ?? throw new ArgumentNullException(nameof(position));
            Direction = direction ?? throw new ArgumentNullException(nameof(direction));
            if ((direction.X * direction.X) + (direction.Y * direction.Y) <= 0d)
                throw new ArgumentOutOfRangeException(nameof(direction));
        }

        public StableId Id { get; }
        public Vec2 Position { get; }
        public Vec2 Direction { get; }
    }

    public sealed class BodyDef
    {
        private readonly ReadOnlyCollection<MountDef> mounts;
        private readonly HashSet<StableId> mountIds;

        public BodyDef(
            TravelMode travel,
            double radius,
            double mass,
            IEnumerable<MountDef> mounts)
        {
            if (!Enum.IsDefined(typeof(TravelMode), travel))
                throw new ArgumentOutOfRangeException(nameof(travel));
            ModelGuard.Positive(radius, nameof(radius));
            ModelGuard.Positive(mass, nameof(mass));

            Travel = travel;
            Radius = radius;
            Mass = mass;
            this.mounts = ModelGuard.CopyUnique(
                mounts,
                value => value.Id,
                nameof(mounts),
                out mountIds);
        }

        public TravelMode Travel { get; }
        public double Radius { get; }
        public double Mass { get; }
        public IReadOnlyList<MountDef> Mounts { get { return mounts; } }

        public bool HasMount(StableId id)
        {
            return id != null && mountIds.Contains(id);
        }
    }

    public sealed class StatsDef
    {
        public StatsDef(double health)
        {
            ModelGuard.Positive(health, nameof(health));
            Health = health;
        }

        public double Health { get; }
    }

    public sealed class SenseDef
    {
        public SenseDef(double range, double arcDegrees)
        {
            ModelGuard.Positive(range, nameof(range));
            ModelGuard.Finite(arcDegrees, nameof(arcDegrees));
            if (arcDegrees <= 0d || arcDegrees > 360d)
                throw new ArgumentOutOfRangeException(nameof(arcDegrees));
            Range = range;
            ArcDegrees = arcDegrees;
        }

        public double Range { get; }
        public double ArcDegrees { get; }
    }

    public sealed class ShotPlan
    {
        private readonly ReadOnlyCollection<StableId> mounts;

        public ShotPlan(
            IEnumerable<StableId> mounts,
            FireMode fireMode,
            MountOrder order,
            int shots,
            double intervalSeconds)
        {
            if (!Enum.IsDefined(typeof(FireMode), fireMode))
                throw new ArgumentOutOfRangeException(nameof(fireMode));
            if (!Enum.IsDefined(typeof(MountOrder), order))
                throw new ArgumentOutOfRangeException(nameof(order));
            if (shots <= 0)
                throw new ArgumentOutOfRangeException(nameof(shots));
            ModelGuard.NonNegative(intervalSeconds, nameof(intervalSeconds));

            this.mounts = ModelGuard.CopyIds(mounts, nameof(mounts), true);
            FireMode = fireMode;
            Order = order;
            Shots = shots;
            IntervalSeconds = intervalSeconds;
        }

        public IReadOnlyList<StableId> Mounts { get { return mounts; } }
        public FireMode FireMode { get; }
        public MountOrder Order { get; }
        public int Shots { get; }
        public double IntervalSeconds { get; }
    }

    public abstract class EffectRef
    {
        protected EffectRef(StableId id)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
        }

        public StableId Id { get; }
    }

    public sealed class DamageRef : EffectRef
    {
        public DamageRef(StableId id) : base(id) { }
    }

    public sealed class BurnRef : EffectRef
    {
        public BurnRef(StableId id) : base(id) { }
    }

    public sealed class ExplosionRef : EffectRef
    {
        public ExplosionRef(StableId id) : base(id) { }
    }

    public sealed class SlowRef : EffectRef
    {
        public SlowRef(StableId id) : base(id) { }
    }

    public sealed class KnockbackRef : EffectRef
    {
        public KnockbackRef(StableId id) : base(id) { }
    }

    public abstract class AttackDef
    {
        protected AttackDef(StableId id)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
        }

        public StableId Id { get; }
    }

    public sealed class GunAttack : AttackDef
    {
        public GunAttack(StableId id, StableId gunId, ShotPlan plan)
            : base(id)
        {
            GunId = gunId ?? throw new ArgumentNullException(nameof(gunId));
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        }

        public StableId GunId { get; }
        public ShotPlan Plan { get; }
    }

    public sealed class MeleeAttack : AttackDef
    {
        private readonly ReadOnlyCollection<EffectRef> effects;

        public MeleeAttack(
            StableId id,
            double range,
            double windUpSeconds,
            double activeSeconds,
            double recoverySeconds,
            IEnumerable<EffectRef> effects)
            : base(id)
        {
            ModelGuard.Positive(range, nameof(range));
            ModelGuard.NonNegative(windUpSeconds, nameof(windUpSeconds));
            ModelGuard.Positive(activeSeconds, nameof(activeSeconds));
            ModelGuard.NonNegative(recoverySeconds, nameof(recoverySeconds));
            Range = range;
            WindUpSeconds = windUpSeconds;
            ActiveSeconds = activeSeconds;
            RecoverySeconds = recoverySeconds;
            this.effects = ModelGuard.CopyEffects(effects, nameof(effects));
        }

        public double Range { get; }
        public double WindUpSeconds { get; }
        public double ActiveSeconds { get; }
        public double RecoverySeconds { get; }
        public IReadOnlyList<EffectRef> Effects { get { return effects; } }
    }

    public sealed class ChargeAttack : AttackDef
    {
        private readonly ReadOnlyCollection<EffectRef> effects;

        public ChargeAttack(
            StableId id,
            double speed,
            double distance,
            double windUpSeconds,
            double recoverySeconds,
            IEnumerable<EffectRef> effects)
            : base(id)
        {
            ModelGuard.Positive(speed, nameof(speed));
            ModelGuard.Positive(distance, nameof(distance));
            ModelGuard.NonNegative(windUpSeconds, nameof(windUpSeconds));
            ModelGuard.NonNegative(recoverySeconds, nameof(recoverySeconds));
            Speed = speed;
            Distance = distance;
            WindUpSeconds = windUpSeconds;
            RecoverySeconds = recoverySeconds;
            this.effects = ModelGuard.CopyEffects(effects, nameof(effects));
        }

        public double Speed { get; }
        public double Distance { get; }
        public double WindUpSeconds { get; }
        public double RecoverySeconds { get; }
        public IReadOnlyList<EffectRef> Effects { get { return effects; } }
    }

    public sealed class ExplodeAttack : AttackDef
    {
        private readonly ReadOnlyCollection<EffectRef> effects;

        public ExplodeAttack(
            StableId id,
            double windUpSeconds,
            IEnumerable<EffectRef> effects)
            : base(id)
        {
            ModelGuard.NonNegative(windUpSeconds, nameof(windUpSeconds));
            WindUpSeconds = windUpSeconds;
            this.effects = ModelGuard.CopyEffects(effects, nameof(effects));
        }

        public double WindUpSeconds { get; }
        public IReadOnlyList<EffectRef> Effects { get { return effects; } }
    }

    public sealed class VariantDef
    {
        private readonly ReadOnlyCollection<StableId> mods;

        public VariantDef(StableId id, IEnumerable<StableId> mods)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            this.mods = ModelGuard.CopyIds(mods, nameof(mods), false);
        }

        public StableId Id { get; }
        public IReadOnlyList<StableId> Mods { get { return mods; } }
    }

    public sealed class PerkRules
    {
        private readonly ReadOnlyCollection<StableId> fixedPerks;
        private readonly ReadOnlyCollection<StableId> pool;

        public PerkRules(
            IEnumerable<StableId> fixedPerks,
            IEnumerable<StableId> pool,
            int rolls)
        {
            this.fixedPerks = ModelGuard.CopyIds(
                fixedPerks,
                nameof(fixedPerks),
                false);
            this.pool = ModelGuard.CopyIds(pool, nameof(pool), false);
            if (rolls < 0 || rolls > this.pool.Count)
                throw new ArgumentOutOfRangeException(nameof(rolls));

            var seen = new HashSet<StableId>(this.fixedPerks);
            for (int index = 0; index < this.pool.Count; index++)
            {
                if (!seen.Add(this.pool[index]))
                    throw new ArgumentException(
                        "A perk cannot be fixed and rollable: " + this.pool[index],
                        nameof(pool));
            }
            Rolls = rolls;
        }

        public IReadOnlyList<StableId> Fixed { get { return fixedPerks; } }
        public IReadOnlyList<StableId> Pool { get { return pool; } }
        public int Rolls { get; }
    }

    public sealed class PhaseDef
    {
        private readonly ReadOnlyCollection<StableId> mods;

        public PhaseDef(
            StableId id,
            double healthAtOrBelow,
            IEnumerable<StableId> mods)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            ModelGuard.Finite(healthAtOrBelow, nameof(healthAtOrBelow));
            if (healthAtOrBelow <= 0d || healthAtOrBelow >= 1d)
                throw new ArgumentOutOfRangeException(nameof(healthAtOrBelow));
            HealthAtOrBelow = healthAtOrBelow;
            this.mods = ModelGuard.CopyIds(mods, nameof(mods), false);
        }

        public StableId Id { get; }
        public double HealthAtOrBelow { get; }
        public IReadOnlyList<StableId> Mods { get { return mods; } }
    }

    public sealed class EnemyDef
    {
        private readonly ReadOnlyCollection<AttackDef> attacks;
        private readonly ReadOnlyCollection<EnemyTier> tiers;
        private readonly ReadOnlyCollection<VariantDef> variants;
        private readonly ReadOnlyCollection<PhaseDef> phases;
        private readonly HashSet<EnemyTier> tierSet;
        private readonly HashSet<StableId> variantIds;
        private readonly HashSet<StableId> perkIds;

        public EnemyDef(
            StableId id,
            StableId viewId,
            BodyDef body,
            StatsDef stats,
            SenseDef sense,
            StableId moveId,
            StableId aiId,
            IEnumerable<AttackDef> attacks,
            IEnumerable<EnemyTier> tiers,
            IEnumerable<VariantDef> variants,
            PerkRules perks,
            IEnumerable<PhaseDef> phases,
            StableId xpId,
            StableId lootId,
            EnemyCatalogRoomClearRole clearRole)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            ViewId = viewId ?? throw new ArgumentNullException(nameof(viewId));
            Body = body ?? throw new ArgumentNullException(nameof(body));
            Stats = stats ?? throw new ArgumentNullException(nameof(stats));
            Sense = sense ?? throw new ArgumentNullException(nameof(sense));
            MoveId = moveId ?? throw new ArgumentNullException(nameof(moveId));
            AiId = aiId ?? throw new ArgumentNullException(nameof(aiId));
            Perks = perks ?? throw new ArgumentNullException(nameof(perks));
            XpId = xpId ?? throw new ArgumentNullException(nameof(xpId));
            LootId = lootId ?? throw new ArgumentNullException(nameof(lootId));
            if (!Enum.IsDefined(typeof(EnemyCatalogRoomClearRole), clearRole))
                throw new ArgumentOutOfRangeException(nameof(clearRole));
            ClearRole = clearRole;

            this.attacks = ModelGuard.CopyUnique(
                attacks,
                value => value.Id,
                nameof(attacks),
                out _);
            if (this.attacks.Count == 0)
                throw new ArgumentException("An enemy needs at least one attack.", nameof(attacks));
            ValidateMounts(this.attacks, body);

            this.tiers = CopyTiers(tiers, out tierSet);
            this.variants = ModelGuard.CopyUnique(
                variants,
                value => value.Id,
                nameof(variants),
                out variantIds);
            this.phases = CopyPhases(phases);

            perkIds = new HashSet<StableId>(perks.Fixed);
            for (int index = 0; index < perks.Pool.Count; index++)
                perkIds.Add(perks.Pool[index]);
        }

        public StableId Id { get; }
        public StableId ViewId { get; }
        public BodyDef Body { get; }
        public StatsDef Stats { get; }
        public SenseDef Sense { get; }
        public StableId MoveId { get; }
        public StableId AiId { get; }
        public IReadOnlyList<AttackDef> Attacks { get { return attacks; } }
        public IReadOnlyList<EnemyTier> Tiers { get { return tiers; } }
        public IReadOnlyList<VariantDef> Variants { get { return variants; } }
        public PerkRules Perks { get; }
        public IReadOnlyList<PhaseDef> Phases { get { return phases; } }
        public StableId XpId { get; }
        public StableId LootId { get; }
        public EnemyCatalogRoomClearRole ClearRole { get; }

        public bool Allows(EnemySpawn spawn)
        {
            if (spawn == null
                || spawn.EnemyId != Id
                || !tierSet.Contains(spawn.Tier)
                || (spawn.VariantId != null && !variantIds.Contains(spawn.VariantId)))
            {
                return false;
            }

            for (int index = 0; index < spawn.PerkIds.Count; index++)
            {
                if (!perkIds.Contains(spawn.PerkIds[index]))
                    return false;
            }
            return true;
        }

        private static void ValidateMounts(
            IReadOnlyList<AttackDef> attacks,
            BodyDef body)
        {
            for (int attackIndex = 0; attackIndex < attacks.Count; attackIndex++)
            {
                GunAttack gun = attacks[attackIndex] as GunAttack;
                if (gun == null) continue;
                for (int mountIndex = 0; mountIndex < gun.Plan.Mounts.Count; mountIndex++)
                {
                    StableId mountId = gun.Plan.Mounts[mountIndex];
                    if (!body.HasMount(mountId))
                    {
                        throw new ArgumentException(
                            "Gun attack references a missing mount: " + mountId,
                            nameof(attacks));
                    }
                }
            }
        }

        private static ReadOnlyCollection<EnemyTier> CopyTiers(
            IEnumerable<EnemyTier> values,
            out HashSet<EnemyTier> set)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var copy = new List<EnemyTier>();
            set = new HashSet<EnemyTier>();
            foreach (EnemyTier value in values)
            {
                if (!Enum.IsDefined(typeof(EnemyTier), value))
                    throw new ArgumentOutOfRangeException(nameof(values));
                if (!set.Add(value))
                    throw new ArgumentException("Enemy tier is duplicated: " + value, nameof(values));
                copy.Add(value);
            }
            if (copy.Count == 0)
                throw new ArgumentException("An enemy needs at least one tier.", nameof(values));
            return new ReadOnlyCollection<EnemyTier>(copy);
        }

        private static ReadOnlyCollection<PhaseDef> CopyPhases(
            IEnumerable<PhaseDef> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var copy = new List<PhaseDef>();
            var ids = new HashSet<StableId>();
            double previous = 1d;
            foreach (PhaseDef value in values)
            {
                if (value == null)
                    throw new ArgumentException("Enemy phases cannot contain null.", nameof(values));
                if (!ids.Add(value.Id))
                    throw new ArgumentException("Enemy phase is duplicated: " + value.Id, nameof(values));
                if (value.HealthAtOrBelow >= previous)
                {
                    throw new ArgumentException(
                        "Enemy phase thresholds must be strictly descending.",
                        nameof(values));
                }
                previous = value.HealthAtOrBelow;
                copy.Add(value);
            }
            return new ReadOnlyCollection<PhaseDef>(copy);
        }
    }

    public sealed class EnemySpawn
    {
        private readonly ReadOnlyCollection<StableId> perkIds;

        public EnemySpawn(
            StableId spawnId,
            StableId enemyId,
            EnemyTier tier,
            StableId variantId,
            IEnumerable<StableId> perkIds,
            Vec2 position,
            double rotationDegrees)
        {
            SpawnId = spawnId ?? throw new ArgumentNullException(nameof(spawnId));
            EnemyId = enemyId ?? throw new ArgumentNullException(nameof(enemyId));
            if (!Enum.IsDefined(typeof(EnemyTier), tier))
                throw new ArgumentOutOfRangeException(nameof(tier));
            Tier = tier;
            VariantId = variantId;
            this.perkIds = ModelGuard.CopyIds(perkIds, nameof(perkIds), false);
            Position = position ?? throw new ArgumentNullException(nameof(position));
            ModelGuard.Finite(rotationDegrees, nameof(rotationDegrees));
            RotationDegrees = rotationDegrees;
        }

        public StableId SpawnId { get; }
        public StableId EnemyId { get; }
        public EnemyTier Tier { get; }
        public StableId VariantId { get; }
        public IReadOnlyList<StableId> PerkIds { get { return perkIds; } }
        public Vec2 Position { get; }
        public double RotationDegrees { get; }
    }

    public sealed class RollKey
    {
        public RollKey(
            ulong runSeed,
            StableId spawnId,
            StableId enemyId,
            StableId streamId)
        {
            RunSeed = runSeed;
            SpawnId = spawnId ?? throw new ArgumentNullException(nameof(spawnId));
            EnemyId = enemyId ?? throw new ArgumentNullException(nameof(enemyId));
            StreamId = streamId ?? throw new ArgumentNullException(nameof(streamId));
        }

        public ulong RunSeed { get; }
        public StableId SpawnId { get; }
        public StableId EnemyId { get; }
        public StableId StreamId { get; }

        public string Canonical
        {
            get
            {
                return RunSeed.ToString(CultureInfo.InvariantCulture)
                    + "|" + SpawnId
                    + "|" + EnemyId
                    + "|" + StreamId;
            }
        }
    }

    public static class RollStreams
    {
        public static readonly StableId Variant = StableId.Parse("enemy.variant");
        public static readonly StableId Perks = StableId.Parse("enemy.perks");
    }

    public static class ModOrder
    {
        private static readonly ReadOnlyCollection<ModStage> stages =
            new ReadOnlyCollection<ModStage>(new[]
            {
                ModStage.Base,
                ModStage.Tier,
                ModStage.Difficulty,
                ModStage.Variant,
                ModStage.FixedPerks,
                ModStage.RolledPerks,
                ModStage.Phase,
                ModStage.Temporary,
            });

        public static IReadOnlyList<ModStage> Stages { get { return stages; } }
    }

    internal static class ModelGuard
    {
        public static void Finite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name);
        }

        public static void Positive(double value, string name)
        {
            Finite(value, name);
            if (value <= 0d) throw new ArgumentOutOfRangeException(name);
        }

        public static void NonNegative(double value, string name)
        {
            Finite(value, name);
            if (value < 0d) throw new ArgumentOutOfRangeException(name);
        }

        public static ReadOnlyCollection<StableId> CopyIds(
            IEnumerable<StableId> values,
            string name,
            bool requireOne)
        {
            if (values == null) throw new ArgumentNullException(name);
            var copy = new List<StableId>();
            var seen = new HashSet<StableId>();
            foreach (StableId value in values)
            {
                if (value == null)
                    throw new ArgumentException("ID lists cannot contain null.", name);
                if (!seen.Add(value))
                    throw new ArgumentException("ID is duplicated: " + value, name);
                copy.Add(value);
            }
            if (requireOne && copy.Count == 0)
                throw new ArgumentException("At least one ID is required.", name);
            return new ReadOnlyCollection<StableId>(copy);
        }

        public static ReadOnlyCollection<EffectRef> CopyEffects(
            IEnumerable<EffectRef> values,
            string name)
        {
            if (values == null) throw new ArgumentNullException(name);
            var copy = new List<EffectRef>();
            foreach (EffectRef value in values)
            {
                if (value == null)
                    throw new ArgumentException("Effect lists cannot contain null.", name);
                copy.Add(value);
            }
            if (copy.Count == 0)
                throw new ArgumentException("At least one effect is required.", name);
            return new ReadOnlyCollection<EffectRef>(copy);
        }

        public static ReadOnlyCollection<T> CopyUnique<T>(
            IEnumerable<T> values,
            Func<T, StableId> id,
            string name,
            out HashSet<StableId> ids)
            where T : class
        {
            if (values == null) throw new ArgumentNullException(name);
            var copy = new List<T>();
            ids = new HashSet<StableId>();
            foreach (T value in values)
            {
                if (value == null)
                    throw new ArgumentException("Lists cannot contain null.", name);
                StableId valueId = id(value);
                if (valueId == null)
                    throw new ArgumentException("List item ID cannot be null.", name);
                if (!ids.Add(valueId))
                    throw new ArgumentException("ID is duplicated: " + valueId, name);
                copy.Add(value);
            }
            return new ReadOnlyCollection<T>(copy);
        }
    }
}
