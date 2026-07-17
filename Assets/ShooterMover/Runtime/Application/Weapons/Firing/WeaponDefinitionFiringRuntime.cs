using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Application.Weapons.Firing
{
    /// <summary>
    /// Immutable runtime projection of one validated catalog definition.
    /// It contains no catalog authority and never parses JSON.
    /// </summary>
    public sealed class WeaponDefinitionFiringProfile
    {
        internal WeaponDefinitionFiringProfile(
            WeaponDefinitionData definition,
            double projectileLifetimeSeconds,
            double projectileRadius)
        {
            DefinitionId = definition.DefinitionId;
            DisplayName = definition.DisplayName;
            DamageType = definition.DamageType;
            Archetype = definition.Archetype;
            DamagePerProjectile = definition.DamagePerProjectile;
            FireRate = definition.FireRate;
            CooldownSeconds = 1d / definition.FireRate;
            ProjectilesPerTrigger = definition.ProjectilesPerTrigger;
            BurstCount = definition.BurstCount;
            ProjectileCountPerShot = checked(definition.ProjectilesPerTrigger * definition.BurstCount);
            SpreadDegrees = definition.SpreadDegrees;
            ProjectileSpeed = definition.ProjectileSpeed;
            Range = definition.Range;
            ProjectileLifetimeSeconds = projectileLifetimeSeconds;
            Pierce = definition.Pierce;
            ExplosionRadius = definition.ExplosionRadius;
            AreaDamagePerTrigger = definition.AreaDamagePerTrigger;
            DotDps = definition.DotDps;
            DotDuration = definition.DotDuration;
            PoolRadius = definition.PoolRadius;
            PoolDuration = definition.PoolDuration;
            ChainTargets = definition.ChainTargets;
            ChainRange = definition.ChainRange;
            Knockback = definition.Knockback;
            ProjectileRadius = projectileRadius;
        }

        public string DefinitionId { get; }
        public string DisplayName { get; }
        public string DamageType { get; }
        public string Archetype { get; }
        public double DamagePerProjectile { get; }
        public double FireRate { get; }
        public double CooldownSeconds { get; }
        public int ProjectilesPerTrigger { get; }
        public int BurstCount { get; }
        public int ProjectileCountPerShot { get; }
        public double SpreadDegrees { get; }
        public double ProjectileSpeed { get; }
        public double Range { get; }
        public double ProjectileLifetimeSeconds { get; }
        public int Pierce { get; }
        public double ExplosionRadius { get; }
        public double AreaDamagePerTrigger { get; }
        public double DotDps { get; }
        public double DotDuration { get; }
        public double PoolRadius { get; }
        public double PoolDuration { get; }
        public int ChainTargets { get; }
        public double ChainRange { get; }
        public double Knockback { get; }
        public double ProjectileRadius { get; }
    }

    /// <summary>
    /// Reusable, fail-closed projection over the authoritative typed catalog.
    /// </summary>
    public sealed class WeaponDefinitionFiringAdapter
    {
        private readonly WeaponCatalog catalog;
        private readonly Dictionary<string, WeaponDefinitionFiringProfile> profiles =
            new Dictionary<string, WeaponDefinitionFiringProfile>(StringComparer.Ordinal);

        public WeaponDefinitionFiringAdapter(WeaponCatalog catalog)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public WeaponDefinitionFiringProfile Resolve(string definitionId)
        {
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                throw new ArgumentException("A concrete weapon definition ID is required.", nameof(definitionId));
            }

            WeaponDefinitionFiringProfile cached;
            if (profiles.TryGetValue(definitionId, out cached))
            {
                return cached;
            }

            WeaponDefinitionData definition;
            if (!catalog.TryGetDefinition(definitionId, out definition))
            {
                throw new KeyNotFoundException("Unknown weapon definition ID: " + definitionId);
            }

            WeaponFamilyDefinition family;
            if (definition.Availability != WeaponCatalogAvailability.Live
                || (catalog.TryGetFamily(definition.FamilyId, out family)
                    && family.Availability != WeaponCatalogAvailability.Live))
            {
                throw new InvalidOperationException(
                    "Preview-only weapon definitions cannot be used for live firing: " + definitionId);
            }

            ValidateDefinition(definition);
            double lifetime = definition.Range / definition.ProjectileSpeed;
            double radius = ResolveProjectileRadius(definition);
            var profile = new WeaponDefinitionFiringProfile(definition, lifetime, radius);
            profiles.Add(definitionId, profile);
            return profile;
        }

        public bool TryResolve(
            string definitionId,
            out WeaponDefinitionFiringProfile profile,
            out string rejection)
        {
            try
            {
                profile = Resolve(definitionId);
                rejection = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                profile = null;
                rejection = exception.Message;
                return false;
            }
        }

        private static void ValidateDefinition(WeaponDefinitionData definition)
        {
            RequireFinitePositive(definition.DamagePerProjectile, "DamagePerProjectile", definition);
            RequireFinitePositive(definition.FireRate, "FireRate", definition);
            if (definition.ProjectilesPerTrigger <= 0)
            {
                throw Invalid("ProjectilesPerTrigger must be positive.", definition);
            }
            if (definition.BurstCount <= 0)
            {
                throw Invalid("BurstCount must be positive.", definition);
            }
            RequireFiniteNonNegative(definition.SpreadDegrees, "SpreadDegrees", definition);
            RequireFinitePositive(definition.ProjectileSpeed, "ProjectileSpeed", definition);
            RequireFinitePositive(definition.Range, "Range", definition);
            if (definition.Pierce < 0)
            {
                throw Invalid("Pierce cannot be negative.", definition);
            }
            RequireFiniteNonNegative(definition.ExplosionRadius, "ExplosionRadius", definition);
            RequireFiniteNonNegative(definition.AreaDamagePerTrigger, "AreaDamagePerTrigger", definition);
            RequireFiniteNonNegative(definition.DotDps, "DoTDPS", definition);
            RequireFiniteNonNegative(definition.DotDuration, "DoTDuration", definition);
            RequireFiniteNonNegative(definition.PoolRadius, "PoolRadius", definition);
            RequireFiniteNonNegative(definition.PoolDuration, "PoolDuration", definition);
            if (definition.ChainTargets < 0)
            {
                throw Invalid("ChainTargets cannot be negative.", definition);
            }
            RequireFiniteNonNegative(definition.ChainRange, "ChainRange", definition);
            RequireFiniteNonNegative(definition.Knockback, "Knockback", definition);

            double lifetime = definition.Range / definition.ProjectileSpeed;
            if (lifetime < 0.01d || lifetime > 30d)
            {
                throw Invalid(
                    "Range/projectile-speed must produce a lifetime between 0.01 and 30 seconds.",
                    definition);
            }
        }

        private static double ResolveProjectileRadius(WeaponDefinitionData definition)
        {
            // The source schema has no standalone projectile-radius field yet.
            // This deterministic geometry projection keeps the visual/physics boundary
            // data-driven without branching on weapon names or archetypes.
            double radius = definition.ProjectilesPerTrigger > 1 ? 0.06d : 0.08d;
            radius = Math.Max(radius, 0.08d + (definition.ExplosionRadius * 0.025d));
            radius = Math.Max(radius, 0.08d + (definition.PoolRadius * 0.02d));
            return Math.Min(0.24d, radius);
        }

        private static void RequireFinitePositive(
            double value,
            string field,
            WeaponDefinitionData definition)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
            {
                throw Invalid(field + " must be finite and positive.", definition);
            }
        }

        private static void RequireFiniteNonNegative(
            double value,
            string field,
            WeaponDefinitionData definition)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                throw Invalid(field + " must be finite and non-negative.", definition);
            }
        }

        private static InvalidOperationException Invalid(
            string detail,
            WeaponDefinitionData definition)
        {
            return new InvalidOperationException(
                "Invalid live weapon definition '" + definition.DefinitionId + "': " + detail);
        }
    }

    public sealed class WeaponDefinitionLoadoutSlot
    {
        public WeaponDefinitionLoadoutSlot(WeaponMountSlot slot, string definitionId)
        {
            if (!Enum.IsDefined(typeof(WeaponMountSlot), slot))
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                throw new ArgumentException("A concrete definition ID is required.", nameof(definitionId));
            }

            Slot = slot;
            DefinitionId = definitionId;
        }

        public WeaponMountSlot Slot { get; }
        public string DefinitionId { get; }
    }

    /// <summary>
    /// Immutable four-mount payload containing concrete catalog definition IDs.
    /// </summary>
    public sealed class WeaponDefinitionLoadout
    {
        private readonly ReadOnlyCollection<WeaponDefinitionLoadoutSlot> slots;

        public WeaponDefinitionLoadout(IEnumerable<WeaponDefinitionLoadoutSlot> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var ordered = new WeaponDefinitionLoadoutSlot[WeaponMountContractRules.MountCount];
            int count = 0;
            foreach (WeaponDefinitionLoadoutSlot slot in source)
            {
                if (slot == null)
                {
                    throw new ArgumentException("Loadout slots cannot contain null.", nameof(source));
                }

                int index = WeaponMountContractRules.GetHudIndex(slot.Slot);
                if (ordered[index] != null)
                {
                    throw new ArgumentException("Each mount slot must appear exactly once.", nameof(source));
                }

                ordered[index] = slot;
                count++;
            }

            if (count != WeaponMountContractRules.MountCount)
            {
                throw new ArgumentException("Exactly four mount slots are required.", nameof(source));
            }

            for (int index = 0; index < ordered.Length; index++)
            {
                if (ordered[index] == null)
                {
                    throw new ArgumentException("Each mount slot must appear exactly once.", nameof(source));
                }
            }

            slots = new ReadOnlyCollection<WeaponDefinitionLoadoutSlot>(ordered);
        }

        public IReadOnlyList<WeaponDefinitionLoadoutSlot> Slots
        {
            get { return slots; }
        }

        public WeaponDefinitionLoadoutSlot GetBySlot(WeaponMountSlot slot)
        {
            return slots[WeaponMountContractRules.GetHudIndex(slot)];
        }

        public WeaponDefinitionLoadoutSlot GetByHudIndex(int hudIndex)
        {
            if (hudIndex < 0 || hudIndex >= slots.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(hudIndex));
            }
            return slots[hudIndex];
        }
    }

    public sealed class WeaponShotIdentity
    {
        public WeaponShotIdentity(
            ulong sessionSeed,
            WeaponMountSlot mountSlot,
            string definitionId,
            long shotSequence)
        {
            if (!Enum.IsDefined(typeof(WeaponMountSlot), mountSlot))
            {
                throw new ArgumentOutOfRangeException(nameof(mountSlot));
            }
            if (string.IsNullOrWhiteSpace(definitionId))
            {
                throw new ArgumentException("Definition ID is required.", nameof(definitionId));
            }
            if (shotSequence < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(shotSequence));
            }

            SessionSeed = sessionSeed;
            MountSlot = mountSlot;
            DefinitionId = definitionId;
            ShotSequence = shotSequence;
        }

        public ulong SessionSeed { get; }
        public WeaponMountSlot MountSlot { get; }
        public string DefinitionId { get; }
        public long ShotSequence { get; }

        public string ToCanonicalString()
        {
            return "schema=shooter-mover.weapon-shot-identity-v1"
                + "\nsession_seed=" + SessionSeed.ToString(CultureInfo.InvariantCulture)
                + "\nmount_slot=" + ((int)MountSlot).ToString(CultureInfo.InvariantCulture)
                + "\ndefinition_id=" + DefinitionId
                + "\nshot_sequence=" + ShotSequence.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed class WeaponShotPlan
    {
        private readonly ReadOnlyCollection<double> spreadOffsets;

        internal WeaponShotPlan(
            WeaponShotIdentity identity,
            WeaponDefinitionFiringProfile profile,
            IEnumerable<double> offsets)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            spreadOffsets = new ReadOnlyCollection<double>(new List<double>(offsets));
        }

        public WeaponShotIdentity Identity { get; }
        public WeaponDefinitionFiringProfile Profile { get; }
        public IReadOnlyList<double> SpreadOffsetsDegrees
        {
            get { return spreadOffsets; }
        }
    }

    public static class DeterministicWeaponSpread
    {
        public static WeaponShotPlan CreatePlan(
            WeaponDefinitionFiringProfile profile,
            WeaponShotIdentity identity)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }
            if (!string.Equals(profile.DefinitionId, identity.DefinitionId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Shot identity and firing profile must use the same definition ID.",
                    nameof(identity));
            }

            var offsets = new double[profile.ProjectileCountPerShot];
            for (int projectileIndex = 0; projectileIndex < offsets.Length; projectileIndex++)
            {
                offsets[projectileIndex] = ResolveOffset(
                    identity,
                    projectileIndex,
                    profile.SpreadDegrees);
            }
            return new WeaponShotPlan(identity, profile, offsets);
        }

        public static double ResolveOffset(
            WeaponShotIdentity identity,
            int projectileIndex,
            double spreadDegrees)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }
            if (projectileIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(projectileIndex));
            }
            if (double.IsNaN(spreadDegrees)
                || double.IsInfinity(spreadDegrees)
                || spreadDegrees < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(spreadDegrees));
            }
            if (spreadDegrees == 0d)
            {
                return 0d;
            }

            string material = identity.ToCanonicalString()
                + "\nprojectile_ordinal=" + projectileIndex.ToString(CultureInfo.InvariantCulture);
            byte[] digest;
            using (SHA256 sha256 = SHA256.Create())
            {
                digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(material));
            }

            ulong value = (ulong)digest[0]
                | ((ulong)digest[1] << 8)
                | ((ulong)digest[2] << 16)
                | ((ulong)digest[3] << 24)
                | ((ulong)digest[4] << 32)
                | ((ulong)digest[5] << 40)
                | ((ulong)digest[6] << 48)
                | ((ulong)digest[7] << 56);
            double unit = value / (double)ulong.MaxValue;
            return ((unit * 2d) - 1d) * (spreadDegrees * 0.5d);
        }
    }

    public sealed class WeaponMountFiringState
    {
        private WeaponDefinitionFiringProfile profile;
        private double nextReadyTimeSeconds;
        private long nextShotSequence;

        internal WeaponMountFiringState(
            WeaponMountSlot slot,
            WeaponDefinitionFiringProfile profile)
        {
            Slot = slot;
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
        }

        public WeaponMountSlot Slot { get; }
        public WeaponDefinitionFiringProfile Profile
        {
            get { return profile; }
        }
        public string DefinitionId
        {
            get { return profile.DefinitionId; }
        }
        public double NextReadyTimeSeconds
        {
            get { return nextReadyTimeSeconds; }
        }
        public long NextShotSequence
        {
            get { return nextShotSequence; }
        }

        internal bool TryCreatePlan(
            double nowSeconds,
            ulong sessionSeed,
            out WeaponShotPlan plan)
        {
            if (double.IsNaN(nowSeconds) || double.IsInfinity(nowSeconds) || nowSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(nowSeconds));
            }
            if (nowSeconds + 0.000000001d < nextReadyTimeSeconds)
            {
                plan = null;
                return false;
            }

            var identity = new WeaponShotIdentity(
                sessionSeed,
                Slot,
                profile.DefinitionId,
                nextShotSequence);
            plan = DeterministicWeaponSpread.CreatePlan(profile, identity);
            nextShotSequence++;
            nextReadyTimeSeconds = nowSeconds + profile.CooldownSeconds;
            return true;
        }

        internal void ReplaceProfile(WeaponDefinitionFiringProfile value)
        {
            profile = value ?? throw new ArgumentNullException(nameof(value));
            ResetCooldown();
        }

        public void ResetCooldown()
        {
            nextReadyTimeSeconds = 0d;
            nextShotSequence = 0L;
        }
    }

    /// <summary>
    /// Gameplay-owned runtime cadence and shot-sequence state for four mounts.
    /// </summary>
    public sealed class WeaponMountFiringSession
    {
        private readonly WeaponDefinitionFiringAdapter adapter;
        private readonly WeaponMountFiringState[] mounts =
            new WeaponMountFiringState[WeaponMountContractRules.MountCount];
        private WeaponDefinitionLoadout loadout;

        public WeaponMountFiringSession(
            WeaponDefinitionFiringAdapter adapter,
            WeaponDefinitionLoadout loadout,
            ulong sessionSeed)
        {
            this.adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            SessionSeed = sessionSeed;
            ApplyLoadout(loadout);
        }

        public ulong SessionSeed { get; }
        public WeaponDefinitionLoadout Loadout
        {
            get { return loadout; }
        }

        public WeaponMountFiringState GetMount(WeaponMountSlot slot)
        {
            return mounts[WeaponMountContractRules.GetHudIndex(slot)];
        }

        public WeaponMountFiringState GetMountByHudIndex(int hudIndex)
        {
            if (hudIndex < 0 || hudIndex >= mounts.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(hudIndex));
            }
            return mounts[hudIndex];
        }

        public void ApplyLoadout(WeaponDefinitionLoadout value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            var resolved = new WeaponDefinitionFiringProfile[WeaponMountContractRules.MountCount];
            for (int index = 0; index < resolved.Length; index++)
            {
                resolved[index] = adapter.Resolve(value.GetByHudIndex(index).DefinitionId);
            }

            for (int index = 0; index < resolved.Length; index++)
            {
                WeaponMountSlot slot = WeaponMountContractRules.GetSlotAtHudIndex(index);
                if (mounts[index] == null)
                {
                    mounts[index] = new WeaponMountFiringState(slot, resolved[index]);
                }
                else
                {
                    mounts[index].ReplaceProfile(resolved[index]);
                }
            }

            loadout = value;
        }

        public IReadOnlyList<WeaponShotPlan> PlanReadyShots(double nowSeconds)
        {
            var result = new List<WeaponShotPlan>();
            for (int index = 0; index < mounts.Length; index++)
            {
                WeaponShotPlan plan;
                if (mounts[index].TryCreatePlan(nowSeconds, SessionSeed, out plan))
                {
                    result.Add(plan);
                }
            }
            return new ReadOnlyCollection<WeaponShotPlan>(result);
        }

        public bool TryPlanMount(
            WeaponMountSlot slot,
            double nowSeconds,
            out WeaponShotPlan plan)
        {
            return GetMount(slot).TryCreatePlan(nowSeconds, SessionSeed, out plan);
        }

        public void ResetCooldowns()
        {
            for (int index = 0; index < mounts.Length; index++)
            {
                mounts[index].ResetCooldown();
            }
        }
    }
}
