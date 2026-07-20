using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Enemies.Catalog
{
    public static partial class EnemyCatalogValidatorV1
    {
        public const int MaximumDefinitions = 4096;
        public const int MaximumLevel = 10000;
        public const int MaximumAttacksPerDefinition = 32;
        public const int MaximumSpecialCapabilitiesPerDefinition = 64;
        public const double MaximumHealth = 1000000000d;
        public const double MaximumDistance = 100000d;
        public const double MaximumCooldownSeconds = 3600d;
        public const double MaximumDamage = 1000000000d;

        public static EnemyCatalogValidationResultV1 Validate(
            int schemaVersion,
            StableId contentVersion,
            IEnumerable<EnemyDefinitionV1> definitions,
            IEnemyCatalogRegistryV1 registry)
        {
            var issues = new List<EnemyCatalogIssueV1>();
            if (schemaVersion != EnemyCatalogV1.SupportedSchemaVersion)
            {
                Add(
                    issues,
                    "enemy-catalog-schema-unsupported",
                    "$.schema_version",
                    "Schema version must be " + EnemyCatalogV1.SupportedSchemaVersion + ".");
            }
            if (contentVersion == null)
            {
                Add(
                    issues,
                    "enemy-catalog-content-version-invalid",
                    "$.content_version",
                    "A canonical content version StableId is required.");
            }
            if (registry == null)
            {
                Add(
                    issues,
                    "enemy-catalog-registry-missing",
                    "$",
                    "A behavior and content reference registry is required.");
            }
            if (definitions == null)
            {
                Add(
                    issues,
                    "enemy-catalog-definitions-missing",
                    "$.definitions",
                    "At least one enemy definition is required.");
                return new EnemyCatalogValidationResultV1(issues);
            }

            var values = new List<EnemyDefinitionV1>();
            foreach (EnemyDefinitionV1 definition in definitions)
            {
                values.Add(definition);
            }
            if (values.Count == 0 || values.Count > MaximumDefinitions)
            {
                Add(
                    issues,
                    "enemy-catalog-definition-count-invalid",
                    "$.definitions",
                    "Definition count must be between 1 and " + MaximumDefinitions + ".");
            }

            var ids = new HashSet<StableId>();
            for (int index = 0; index < values.Count; index++)
            {
                EnemyDefinitionV1 definition = values[index];
                string path = "$.definitions[" + index + "]";
                if (definition == null)
                {
                    Add(
                        issues,
                        "enemy-catalog-definition-null",
                        path,
                        "Enemy definitions cannot be null.");
                    continue;
                }
                if (definition.DefinitionId == null)
                {
                    Add(
                        issues,
                        "enemy-catalog-id-invalid",
                        path + ".id",
                        "A canonical enemy definition ID is required.");
                }
                else if (!ids.Add(definition.DefinitionId))
                {
                    Add(
                        issues,
                        "enemy-catalog-definition-duplicate",
                        path + ".id",
                        "Enemy definition ID is duplicated: " + definition.DefinitionId);
                }

                ValidateDefinition(issues, path, definition, registry);
            }
            return new EnemyCatalogValidationResultV1(issues);
        }

        private static void ValidateDefinition(
            List<EnemyCatalogIssueV1> issues,
            string path,
            EnemyDefinitionV1 definition,
            IEnemyCatalogRegistryV1 registry)
        {
            if (definition.PresentationId == null
                || registry == null
                || !registry.IsPresentationRegistered(definition.PresentationId))
            {
                Add(
                    issues,
                    "enemy-catalog-presentation-missing",
                    path + ".presentation",
                    "Presentation reference is missing or unregistered: "
                    + Value(definition.PresentationId));
            }

            ValidateHealth(issues, path, definition);

            if (definition.FactionId == null)
            {
                Add(
                    issues,
                    "enemy-catalog-id-invalid",
                    path + ".faction",
                    "A canonical faction ID is required.");
            }

            if (!IsFiniteInRange(definition.DetectionRadius, 0d, MaximumDistance, false))
            {
                Add(
                    issues,
                    "enemy-catalog-range-invalid",
                    path + ".perception.detection_radius",
                    "Detection radius must be finite, positive, and bounded.");
            }
            ValidateArc(issues, path + ".perception.vision_arc_degrees", definition.VisionArcDegrees);
            ValidateArc(issues, path + ".attack_geometry.attack_arc_degrees", definition.AttackArcDegrees);

            bool rangesValid = IsFiniteInRange(
                    definition.MinimumAttackRange,
                    0d,
                    MaximumDistance,
                    true)
                && IsFiniteInRange(
                    definition.PreferredAttackRange,
                    0d,
                    MaximumDistance,
                    true)
                && IsFiniteInRange(
                    definition.MaximumAttackRange,
                    0d,
                    MaximumDistance,
                    true)
                && definition.MinimumAttackRange <= definition.PreferredAttackRange
                && definition.PreferredAttackRange <= definition.MaximumAttackRange
                && definition.MaximumAttackRange <= definition.DetectionRadius;
            if (!rangesValid)
            {
                Add(
                    issues,
                    "enemy-catalog-range-invalid",
                    path + ".attack_geometry",
                    "Attack ranges must be finite, ordered, non-negative, and inside detection radius.");
            }

            if (definition.MovementPolicyId == null
                || registry == null
                || !registry.IsMovementPolicyRegistered(definition.MovementPolicyId))
            {
                Add(
                    issues,
                    "enemy-catalog-movement-policy-unknown",
                    path + ".movement_policy",
                    "Movement policy is not registered: " + Value(definition.MovementPolicyId));
            }
            if (definition.DecisionPolicyId == null
                || registry == null
                || !registry.IsDecisionPolicyRegistered(definition.DecisionPolicyId))
            {
                Add(
                    issues,
                    "enemy-catalog-decision-policy-unknown",
                    path + ".decision_policy",
                    "Decision policy is not registered: " + Value(definition.DecisionPolicyId));
            }

            ValidateAttacks(issues, path, definition, registry);

            if (definition.ExperienceProfileId == null
                || registry == null
                || !registry.IsExperienceProfileRegistered(definition.ExperienceProfileId))
            {
                Add(
                    issues,
                    "enemy-catalog-xp-profile-unknown",
                    path + ".xp_profile",
                    "Experience profile is not registered: "
                    + Value(definition.ExperienceProfileId));
            }
            if (definition.DropProfileId == null
                || registry == null
                || !registry.IsDropProfileRegistered(definition.DropProfileId))
            {
                Add(
                    issues,
                    "enemy-catalog-drop-profile-unknown",
                    path + ".drop_profile",
                    "Drop profile is not registered: " + Value(definition.DropProfileId));
            }
            if (!Enum.IsDefined(
                typeof(EnemyCatalogRoomClearRoleV1),
                definition.RoomClearRole))
            {
                Add(
                    issues,
                    "enemy-catalog-room-clear-role-invalid",
                    path + ".room_clear_role",
                    "Room-clear role is not supported.");
            }

            if (definition.SpecialCapabilityIds.Count
                > MaximumSpecialCapabilitiesPerDefinition)
            {
                Add(
                    issues,
                    "enemy-catalog-special-capability-count-invalid",
                    path + ".special_capabilities",
                    "Too many special capabilities are defined.");
            }
            var specialIds = new HashSet<StableId>();
            for (int index = 0; index < definition.SpecialCapabilityIds.Count; index++)
            {
                StableId special = definition.SpecialCapabilityIds[index];
                string specialPath = path + ".special_capabilities[" + index + "]";
                if (special == null || !specialIds.Add(special))
                {
                    Add(
                        issues,
                        "enemy-catalog-special-capability-invalid",
                        specialPath,
                        "Special capability IDs must be non-null and unique.");
                }
                else if (registry == null || !registry.IsSpecialCapabilityRegistered(special))
                {
                    Add(
                        issues,
                        "enemy-catalog-special-capability-unknown",
                        specialPath,
                        "Special capability is not registered: " + special);
                }
            }
        }

        private static void ValidateHealth(
            List<EnemyCatalogIssueV1> issues,
            string path,
            EnemyDefinitionV1 definition)
        {
            if (!IsFiniteInRange(definition.BaseHealth, 0d, MaximumHealth, false))
            {
                Add(
                    issues,
                    "enemy-catalog-health-invalid",
                    path + ".base_health",
                    "Base health must be finite, positive, and bounded.");
            }

            EnemyLevelScalingProfileV1 scaling = definition.LevelScaling;
            if (scaling == null)
            {
                Add(
                    issues,
                    "enemy-catalog-level-scaling-invalid",
                    path + ".level_scaling",
                    "A level-scaling profile is required.");
                return;
            }

            bool levelsValid = scaling.BaseLevel >= 1
                && scaling.BaseLevel <= scaling.MaximumLevel
                && scaling.MaximumLevel <= MaximumLevel;
            bool valuesValid = IsFiniteInRange(
                    scaling.AdditiveHealthPerLevel,
                    0d,
                    MaximumHealth,
                    true)
                && IsFiniteInRange(
                    scaling.MultiplicativeHealthPerLevel,
                    1d,
                    100d,
                    true);
            double maximumResolvedHealth = definition.ResolveHealth(scaling.MaximumLevel);
            bool resolvedValid = IsFiniteInRange(
                maximumResolvedHealth,
                0d,
                MaximumHealth,
                false);
            if (!levelsValid || !valuesValid || !resolvedValid)
            {
                Add(
                    issues,
                    "enemy-catalog-level-scaling-invalid",
                    path + ".level_scaling",
                    "Levels and health growth must remain finite, ordered, positive, and bounded.");
            }
        }

    }
}
