
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Weapons.Catalog;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    public interface IWeaponRuntimePackageRegistryV1
    {
        string Fingerprint { get; }
        IReadOnlyList<string> RegisteredDefinitionIds { get; }
        bool TryResolveBehavior(WeaponDefinitionData definition, out WeaponBehaviorId behaviorId);
    }

    /// <summary>
    /// Registry for the five runtime packages that exist today. The content catalog does
    /// not infer implementation status. Missing exact definition IDs remain pending.
    /// </summary>
    public sealed class ProductionWeaponRuntimePackageRegistryV1 :
        IWeaponRuntimePackageRegistryV1
    {
        private readonly Dictionary<string, WeaponBehaviorId> behaviorByDefinitionId;
        private readonly ReadOnlyCollection<string> registeredDefinitionIds;

        private ProductionWeaponRuntimePackageRegistryV1(
            IDictionary<string, WeaponBehaviorId> registrations)
        {
            behaviorByDefinitionId = new Dictionary<string, WeaponBehaviorId>(StringComparer.Ordinal);
            var ids = new List<string>();
            foreach (KeyValuePair<string, WeaponBehaviorId> pair in registrations)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null)
                {
                    throw new ArgumentException("Runtime package registrations must be complete.", nameof(registrations));
                }
                string id = pair.Key.Trim();
                behaviorByDefinitionId.Add(id, pair.Value);
                ids.Add(id);
            }
            ids.Sort(StringComparer.Ordinal);
            registeredDefinitionIds = new ReadOnlyCollection<string>(ids);
            Fingerprint = "weapon-runtime-package-registry.production-v1|" + string.Join("|", ids);
        }

        public string Fingerprint { get; }
        public IReadOnlyList<string> RegisteredDefinitionIds { get { return registeredDefinitionIds; } }

        public static ProductionWeaponRuntimePackageRegistryV1 CreateDefault()
        {
            return new ProductionWeaponRuntimePackageRegistryV1(
                new Dictionary<string, WeaponBehaviorId>(StringComparer.Ordinal)
                {
                    { "blaster.mk1", BuiltInWeaponBehaviorIds.Projectile },
                    { "shotgun.mk1", BuiltInWeaponBehaviorIds.Projectile },
                    { "rocket_launcher.mk1", BuiltInWeaponBehaviorIds.Explosive },
                    { "chain_weapon.mk1", BuiltInWeaponBehaviorIds.Chain },
                    { "ricochet_weapon.mk1", BuiltInWeaponBehaviorIds.Projectile },
                });
        }

        public bool TryResolveBehavior(
            WeaponDefinitionData definition,
            out WeaponBehaviorId behaviorId)
        {
            behaviorId = null;
            return definition != null
                && behaviorByDefinitionId.TryGetValue(definition.DefinitionId, out behaviorId);
        }
    }
}
