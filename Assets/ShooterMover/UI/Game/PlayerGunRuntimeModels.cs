using System;
using System.Collections.Generic;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Guns.Catalog;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Catalog;
using ShooterMover.UnityAdapters.Guns.Live;

namespace ShooterMover.UI.Game
{
    internal sealed class BoundGunResolver :
        IGunMappingPolicyResolver,
        IGunResolver
    {
        private readonly Dictionary<string, Gun> blueprints =
            new Dictionary<string, Gun>(StringComparer.Ordinal);

        internal BoundGunResolver(IEnumerable<GunMark> equippedMarks)
        {
            if (equippedMarks == null)
            {
                throw new ArgumentNullException(nameof(equippedMarks));
            }

            foreach (GunMark mark in equippedMarks)
            {
                if (mark == null
                    || mark.Blueprint == null
                    || mark.Blueprint.IsTransitionalCatalogProjection
                    || mark.Blueprint.DefinitionId == null)
                {
                    throw new ArgumentException(
                        "Every equipped gun requires an exact canonical blueprint.",
                        nameof(equippedMarks));
                }

                string definitionId = mark.Blueprint.DefinitionId.Value;
                Gun existing;
                if (blueprints.TryGetValue(definitionId, out existing))
                {
                    if (!ReferenceEquals(existing, mark.Blueprint))
                    {
                        throw new ArgumentException(
                            "One gun definition cannot resolve to conflicting blueprints.",
                            nameof(equippedMarks));
                    }
                    continue;
                }
                blueprints.Add(definitionId, mark.Blueprint);
            }

            if (blueprints.Count == 0)
            {
                throw new ArgumentException(
                    "At least one equipped gun blueprint is required.",
                    nameof(equippedMarks));
            }
        }

        public bool TryResolve(
            GunDefinitionId requested,
            out GunCatalogBlueprintMappingIntent mappingIntent)
        {
            mappingIntent = null;
            return false;
        }

        public bool TryResolveCanonical(
            GunDefinitionId requested,
            out Gun resolved)
        {
            resolved = null;
            return requested != null
                && blueprints.TryGetValue(requested.Value, out resolved);
        }
    }

    internal sealed class EquippedGun
    {
        internal EquippedGun(
            GunSlot mount,
            GunItem exactInstance,
            GunMark mark)
        {
            Mount = mount ?? throw new ArgumentNullException(nameof(mount));
            ExactInstance = exactInstance
                ?? throw new ArgumentNullException(nameof(exactInstance));
            Mark = mark ?? throw new ArgumentNullException(nameof(mark));
        }

        internal GunSlot Mount { get; }
        internal GunItem ExactInstance { get; }
        internal GunMark Mark { get; }
    }
}
