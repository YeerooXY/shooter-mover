using System;
using System.Globalization;
using System.Text;
using ShooterMover.Application.Weapons.Execution;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.UnityAdapters.Weapons.Live
{
    public interface IActiveWeaponEquipmentInstanceSource
    {
        bool TryResolveActiveEquipmentInstance(
            WeaponActorInstanceId actorId,
            LifecycleGeneration lifecycleGeneration,
            out EquipmentInstanceId equipmentInstanceId);
    }

    public interface IInventoryWeaponEffectBatchSink
    {
        WeaponEffectBatchSinkResult TryAccept(InventoryWeaponEffectBatch batch);
    }

    public sealed class InventoryWeaponFireRequest
    {
        public InventoryWeaponFireRequest(
            WeaponActorInstanceId actorId,
            FireOperationId fireOperationId,
            LifecycleGeneration lifecycleGeneration,
            long simulationTick,
            ulong deterministicSeed,
            WeaponVector2 origin,
            WeaponVector2 aimDirection)
        {
            ActorId = actorId;
            FireOperationId = fireOperationId;
            LifecycleGeneration = lifecycleGeneration;
            SimulationTick = simulationTick;
            DeterministicSeed = deterministicSeed;
            Origin = origin;
            AimDirection = aimDirection;
        }

        public WeaponActorInstanceId ActorId { get; }
        public FireOperationId FireOperationId { get; }
        public LifecycleGeneration LifecycleGeneration { get; }
        public long SimulationTick { get; }
        public ulong DeterministicSeed { get; }
        public WeaponVector2 Origin { get; }
        public WeaponVector2 AimDirection { get; }
    }

    public sealed class InventoryWeaponEffectProfile
    {
        public InventoryWeaponEffectProfile(
            WeaponDefinitionId definitionId,
            double fireRate,
            int cooldownTicks,
            int projectileCount,
            double spreadDegrees,
            double projectileSpeed,
            double range,
            double directDamagePerProjectile,
            int pierce,
            double areaDamagePerTrigger,
            double explosionRadius,
            double damageOverTimePerSecond,
            double damageOverTimeDuration,
            double poolRadius,
            double poolDuration,
            int chainTargets,
            double chainRange,
            double knockback,
            string damageType)
        {
            DefinitionId = definitionId ?? throw new ArgumentNullException(nameof(definitionId));
            FireRate = fireRate;
            CooldownTicks = cooldownTicks;
            ProjectileCount = projectileCount;
            SpreadDegrees = spreadDegrees;
            ProjectileSpeed = projectileSpeed;
            Range = range;
            DirectDamagePerProjectile = directDamagePerProjectile;
            Pierce = pierce;
            AreaDamagePerTrigger = areaDamagePerTrigger;
            ExplosionRadius = explosionRadius;
            DamageOverTimePerSecond = damageOverTimePerSecond;
            DamageOverTimeDuration = damageOverTimeDuration;
            PoolRadius = poolRadius;
            PoolDuration = poolDuration;
            ChainTargets = chainTargets;
            ChainRange = chainRange;
            Knockback = knockback;
            DamageType = damageType ?? string.Empty;
            CanonicalText = BuildCanonicalText();
            Fingerprint = WeaponExecutionFingerprint.Compute(CanonicalText);
        }

        public WeaponDefinitionId DefinitionId { get; }
        public double FireRate { get; }
        public int CooldownTicks { get; }
        public int ProjectileCount { get; }
        public double SpreadDegrees { get; }
        public double ProjectileSpeed { get; }
        public double Range { get; }
        public double DirectDamagePerProjectile { get; }
        public int Pierce { get; }
        public double AreaDamagePerTrigger { get; }
        public double ExplosionRadius { get; }
        public double DamageOverTimePerSecond { get; }
        public double DamageOverTimeDuration { get; }
        public double PoolRadius { get; }
        public double PoolDuration { get; }
        public int ChainTargets { get; }
        public double ChainRange { get; }
        public double Knockback { get; }
        public string DamageType { get; }
        public string CanonicalText { get; }
        public string Fingerprint { get; }

        internal static InventoryWeaponEffectProfile From(
            WeaponDefinitionData definition,
            int simulationTicksPerSecond)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            int cooldownTicks = Math.Max(
                1,
                (int)Math.Ceiling(simulationTicksPerSecond / definition.FireRate));
            return new InventoryWeaponEffectProfile(
                new WeaponDefinitionId(definition.DefinitionId),
                definition.FireRate,
                cooldownTicks,
                definition.ProjectilesPerTrigger,
                definition.SpreadDegrees,
                definition.ProjectileSpeed,
                definition.Range,
                definition.DamagePerProjectile,
                definition.Pierce,
                definition.AreaDamagePerTrigger,
                definition.ExplosionRadius,
                definition.DotDps,
                definition.DotDuration,
                definition.PoolRadius,
                definition.PoolDuration,
                definition.ChainTargets,
                definition.ChainRange,
                definition.Knockback,
                definition.DamageType);
        }

        private string BuildCanonicalText()
        {
            StringBuilder builder = new StringBuilder();
            Append(builder, "definition_id", DefinitionId.ToString());
            Append(builder, "fire_rate", Format(FireRate));
            Append(builder, "cooldown_ticks", CooldownTicks.ToString(CultureInfo.InvariantCulture));
            Append(builder, "projectile_count", ProjectileCount.ToString(CultureInfo.InvariantCulture));
            Append(builder, "spread_degrees", Format(SpreadDegrees));
            Append(builder, "projectile_speed", Format(ProjectileSpeed));
            Append(builder, "range", Format(Range));
            Append(builder, "direct_damage", Format(DirectDamagePerProjectile));
            Append(builder, "pierce", Pierce.ToString(CultureInfo.InvariantCulture));
            Append(builder, "area_damage", Format(AreaDamagePerTrigger));
            Append(builder, "explosion_radius", Format(ExplosionRadius));
            Append(builder, "dot_dps", Format(DamageOverTimePerSecond));
            Append(builder, "dot_duration", Format(DamageOverTimeDuration));
            Append(builder, "pool_radius", Format(PoolRadius));
            Append(builder, "pool_duration", Format(PoolDuration));
            Append(builder, "chain_targets", ChainTargets.ToString(CultureInfo.InvariantCulture));
            Append(builder, "chain_range", Format(ChainRange));
            Append(builder, "knockback", Format(Knockback));
            Append(builder, "damage_type", DamageType);
            return builder.ToString();
        }

        private static string Format(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static void Append(StringBuilder builder, string name, string value)
        {
            builder.Append(name).Append('=').Append(value ?? "null").Append('\n');
        }
    }

    public sealed class InventoryWeaponEffectBatch
    {
        public InventoryWeaponEffectBatch(
            WeaponEffectBatch coreBatch,
            InventoryWeaponEffectProfile profile)
        {
            CoreBatch = coreBatch ?? throw new ArgumentNullException(nameof(coreBatch));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            if (coreBatch.Identity == null
                || !profile.DefinitionId.Equals(coreBatch.Identity.WeaponDefinitionId))
            {
                throw new ArgumentException(
                    "The catalog profile must describe the weapon definition carried by the core batch.",
                    nameof(profile));
            }

            CanonicalText = "core_batch=" + coreBatch.Fingerprint + "\n"
                + "profile=" + profile.Fingerprint + "\n";
            Fingerprint = WeaponExecutionFingerprint.Compute(CanonicalText);
        }

        public WeaponEffectBatch CoreBatch { get; }
        public InventoryWeaponEffectProfile Profile { get; }
        public WeaponEffectIdentity Identity { get { return CoreBatch.Identity; } }
        public int EffectCount { get { return CoreBatch.EffectCount; } }
        public string CanonicalText { get; }
        public string Fingerprint { get; }
    }

    public sealed class InventoryWeaponExecutionResult
    {
        public InventoryWeaponExecutionResult(
            EquipmentInstanceId equipmentInstanceId,
            WeaponExecutionResult execution,
            InventoryWeaponEffectBatch effectBatch)
        {
            EquipmentInstanceId = equipmentInstanceId;
            Execution = execution ?? throw new ArgumentNullException(nameof(execution));
            EffectBatch = effectBatch;
        }

        public EquipmentInstanceId EquipmentInstanceId { get; }
        public WeaponExecutionResult Execution { get; }
        public InventoryWeaponEffectBatch EffectBatch { get; }
        public WeaponExecutionStatus Status { get { return Execution.Status; } }
        public string RejectionCode { get { return Execution.RejectionCode; } }
        public bool Succeeded { get { return Status == WeaponExecutionStatus.Accepted; } }
        public bool IsExactReplay { get { return Status == WeaponExecutionStatus.ReplayAccepted; } }
        public WeaponDefinitionId WeaponDefinitionId
        {
            get { return EffectBatch == null ? null : EffectBatch.Profile.DefinitionId; }
        }
    }
}
