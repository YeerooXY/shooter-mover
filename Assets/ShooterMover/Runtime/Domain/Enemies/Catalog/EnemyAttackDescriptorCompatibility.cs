using System;
using System.Runtime.CompilerServices;

namespace ShooterMover.Domain.Enemies.Catalog
{
    /// <summary>
    /// Records descriptors imported from the schema-v1 compatibility shape.
    /// Schema-v1 content keeps the historical one-call execution boundary until the concrete
    /// scheduled-effect adapter is available; schema-v2 descriptors are never marked here.
    /// </summary>
    public static class EnemyAttackDescriptorCompatibility
    {
        private sealed class LegacyMarker
        {
        }

        private static readonly ConditionalWeakTable<
            EnemyAttackCapabilityDescriptor,
            LegacyMarker> LegacyDescriptors =
                new ConditionalWeakTable<
                    EnemyAttackCapabilityDescriptor,
                    LegacyMarker>();

        internal static void MarkLegacyCompatibility(
            EnemyAttackCapabilityDescriptor descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            LegacyDescriptors.GetValue(descriptor, _ => new LegacyMarker());
        }

        public static bool IsLegacyCompatibility(
            EnemyAttackCapabilityDescriptor descriptor)
        {
            if (descriptor == null) return false;
            LegacyMarker marker;
            return LegacyDescriptors.TryGetValue(descriptor, out marker);
        }
    }
}
