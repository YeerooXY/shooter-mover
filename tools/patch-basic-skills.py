from pathlib import Path


def replace_once(path, old, new):
    file = Path(path)
    text = file.read_text(encoding="utf-8")
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"{path}: expected one patch anchor, found {count}")
    file.write_text(text.replace(old, new, 1), encoding="utf-8")


catalog = "Assets/ShooterMover/Runtime/Domain/Progression/Skills/RankedSkillFoundation.cs"
replace_once(
    catalog,
    '''            var recovery = new RankedSkillDefinition("striker.thruster_recovery", "mobility", 15, new[] { "striker" }, null, null, null, fifteen(0.01m), new[] { new SkillEffectDescriptor("movement.thruster_recovery", SkillModifierKind.Percentage, 1m) }, new[] { new SkillRankMilestone(5, new[] { new SkillEffectDescriptor("movement.recovery_delay", SkillModifierKind.Flat, -0.1m) }) });''',
    '''            var health = new RankedSkillDefinition("generic.max_health", "defense", 15, null, null, null,
                null, fifteen(0.01m), new[] { new SkillEffectDescriptor("character.maximum_health", SkillModifierKind.Percentage, 1m) }, null);
            var damage = new RankedSkillDefinition("striker.damage_bonus", "offense", 15, new[] { "striker" }, null, null,
                null, fifteen(0.01m), new[] { new SkillEffectDescriptor("combat.damage", SkillModifierKind.Percentage, 1m) }, null);
            var cash = new RankedSkillDefinition("generic.cash_drop_size", "economy", 15, null, null, null,
                null, fifteen(0.01m), new[] { new SkillEffectDescriptor("rewards.cash", SkillModifierKind.Percentage, 1m) }, null);
            var recovery = new RankedSkillDefinition("striker.thruster_recovery", "mobility", 15, new[] { "striker" }, null, null, null, fifteen(0.01m), new[] { new SkillEffectDescriptor("movement.thruster_recovery", SkillModifierKind.Percentage, 1m) }, new[] { new SkillRankMilestone(5, new[] { new SkillEffectDescriptor("movement.recovery_delay", SkillModifierKind.Flat, -0.1m) }) });''')
replace_once(
    catalog,
    '''            return new RankedSkillCatalog("skills.schema.v2", "fixture.003", new[] { armor, speed, recovery, efficiency }, new[] { synergy });''',
    '''            return new RankedSkillCatalog("skills.schema.v2", "fixture.003", new[] { armor, speed, health, damage, cash, recovery, efficiency }, new[] { synergy });''')

player_hud = "Assets/ShooterMover/UI/Game/PlayerHUD.cs"
replace_once(
    player_hud,
    '''using ShooterMover.Domain.Common;\nusing ShooterMover.GameplayEntities;''',
    '''using ShooterMover.Domain.Common;\nusing ShooterMover.Domain.Progression.Skills;\nusing ShooterMover.GameplayEntities;''')
replace_once(
    player_hud,
    '''    public sealed class PlayerHUDInstaller : MonoBehaviour\n    {\n        private bool bindingComplete;''',
    '''    public sealed class PlayerHUDInstaller : MonoBehaviour\n    {\n        private const string MaximumHealthSkillId = "generic.max_health";\n        private bool bindingComplete;''')
replace_once(
    player_hud,
    '''            Rigidbody2D body = marker.GetComponent<Rigidbody2D>();\n            TopDownMovement movement = marker.GetComponent<\n                TopDownMovement>();\n            if (body == null || movement == null)\n            {\n                return;\n            }''',
    '''            CharacterLiveGraph graph;\n            FlowProfileRecord profile;\n            RankedSkillAllocationSnapshot allocation;\n            if (!CharacterSave.TryResolveCurrent(out graph, out profile)\n                || graph == null\n                || graph.IsDisposed\n                || graph.Character == null\n                || graph.Character.CharacterInstanceStableId\n                    != marker.CharacterInstanceStableId\n                || !graph.SkillAuthority.TryGet(\n                    graph.SkillProfileId,\n                    out allocation)\n                || allocation == null)\n            {\n                return;\n            }\n\n            double maximumHealth = PlayerHUD.ProvisionalMaximumHealth\n                * (1d + allocation.RankOf(MaximumHealthSkillId) * 0.01d);\n            Rigidbody2D body = marker.GetComponent<Rigidbody2D>();\n            TopDownMovement movement = marker.GetComponent<\n                TopDownMovement>();\n            if (body == null || movement == null)\n            {\n                return;\n            }''')
replace_once(
    player_hud,
    '''                vitals.Bind(marker, body, movement);''',
    '''                vitals.Bind(marker, body, movement, maximumHealth);''')
replace_once(
    player_hud,
    '''        public bool UsesProvisionalMaximumHealth { get { return true; } }''',
    '''        public bool UsesProvisionalMaximumHealth\n        {\n            get\n            {\n                return IsBound\n                    && Math.Abs(MaximumHealth - ProvisionalMaximumHealth)\n                        < 0.000001d;\n            }\n        }''')
replace_once(
    player_hud,
    '''        public void Bind(\n            PlayerMarker configuredMarker,\n            Rigidbody2D configuredBody,\n            TopDownMovement configuredMovement)\n        {\n            Bind(\n                configuredMarker,\n                configuredBody,\n                configuredMovement,\n                new PlayablePlayerHubReturnRequest());\n        }\n\n        public void Bind(\n            PlayerMarker configuredMarker,\n            Rigidbody2D configuredBody,\n            TopDownMovement configuredMovement,\n            IPlayablePlayerHubReturnRequest configuredHubReturnRequest)\n        {''',
    '''        public void Bind(\n            PlayerMarker configuredMarker,\n            Rigidbody2D configuredBody,\n            TopDownMovement configuredMovement)\n        {\n            Bind(\n                configuredMarker,\n                configuredBody,\n                configuredMovement,\n                ProvisionalMaximumHealth,\n                new PlayablePlayerHubReturnRequest());\n        }\n\n        public void Bind(\n            PlayerMarker configuredMarker,\n            Rigidbody2D configuredBody,\n            TopDownMovement configuredMovement,\n            double maximumHealth)\n        {\n            Bind(\n                configuredMarker,\n                configuredBody,\n                configuredMovement,\n                maximumHealth,\n                new PlayablePlayerHubReturnRequest());\n        }\n\n        public void Bind(\n            PlayerMarker configuredMarker,\n            Rigidbody2D configuredBody,\n            TopDownMovement configuredMovement,\n            IPlayablePlayerHubReturnRequest configuredHubReturnRequest)\n        {\n            Bind(\n                configuredMarker,\n                configuredBody,\n                configuredMovement,\n                ProvisionalMaximumHealth,\n                configuredHubReturnRequest);\n        }\n\n        private void Bind(\n            PlayerMarker configuredMarker,\n            Rigidbody2D configuredBody,\n            TopDownMovement configuredMovement,\n            double maximumHealth,\n            IPlayablePlayerHubReturnRequest configuredHubReturnRequest)\n        {''')
replace_once(
    player_hud,
    '''            hubReturnRequest = configuredHubReturnRequest\n                ?? throw new ArgumentNullException(\n                    nameof(configuredHubReturnRequest));\n            if (marker.CharacterInstanceStableId == null''',
    '''            hubReturnRequest = configuredHubReturnRequest\n                ?? throw new ArgumentNullException(\n                    nameof(configuredHubReturnRequest));\n            if (double.IsNaN(maximumHealth)\n                || double.IsInfinity(maximumHealth)\n                || maximumHealth <= 0d)\n            {\n                throw new ArgumentOutOfRangeException(nameof(maximumHealth));\n            }\n            if (marker.CharacterInstanceStableId == null''')
replace_once(
    player_hud,
    '''                    ProvisionalMaximumHealth,\n                    0L));''',
    '''                    maximumHealth,\n                    0L));''')

player_fire = "Assets/ShooterMover/UI/Game/PlayerFire.cs"
replace_once(
    player_fire,
    '''using ShooterMover.Domain.Guns.Execution;\nusing ShooterMover.UnityAdapters.Guns.Live;''',
    '''using ShooterMover.Domain.Guns.Execution;\nusing ShooterMover.Domain.Progression.Skills;\nusing ShooterMover.UnityAdapters.Guns.Live;''')
replace_once(
    player_fire,
    '''    public sealed class PlayerFire : MonoBehaviour\n    {''',
    '''    public sealed class PlayerFire : MonoBehaviour\n    {\n        private const string DamageSkillId = "striker.damage_bonus";''')
replace_once(
    player_fire,
    '''            List<GunPlay> resolved;\n            string error;\n            if (!TryBuildGuns(\n                    currentGraph.LoadoutRuntime,\n                    Time.fixedTimeAsDouble,''',
    '''            RankedSkillAllocationSnapshot allocation;\n            if (!currentGraph.SkillAuthority.TryGet(\n                    currentGraph.SkillProfileId,\n                    out allocation)\n                || allocation == null)\n            {\n                return false;\n            }\n            double damageMultiplier = 1d\n                + allocation.RankOf(DamageSkillId) * 0.01d;\n\n            List<GunPlay> resolved;\n            string error;\n            if (!TryBuildGuns(\n                    currentGraph.LoadoutRuntime,\n                    damageMultiplier,\n                    Time.fixedTimeAsDouble,''')
replace_once(
    player_fire,
    '''        private bool TryBuildGuns(\n            PlayerLoadoutLive loadout,\n            double now,''',
    '''        private bool TryBuildGuns(\n            PlayerLoadoutLive loadout,\n            double damageMultiplier,\n            double now,''')
replace_once(
    player_fire,
    '''                    bullet = ProjectileExecutionProfile.From(gun);''',
    '''                    bullet = ProjectileExecutionProfile.From(gun)\n                        .WithDamageMultiplier(damageMultiplier);''')

projectile = "Assets/ShooterMover/Runtime/Domain/Guns/Execution/ProjectileExecutionContracts.cs"
replace_once(
    projectile,
    '''        private static void ValidateDeliveryProjection(\n            GunDeliveryType deliveryType,''',
    '''        public ProjectileExecutionProfile WithDamageMultiplier(\n            double multiplier)\n        {\n            if (double.IsNaN(multiplier)\n                || double.IsInfinity(multiplier)\n                || multiplier <= 0d)\n            {\n                throw new ArgumentOutOfRangeException(nameof(multiplier));\n            }\n            if (Math.Abs(multiplier - 1d) < 0.0000001d)\n            {\n                return this;\n            }\n\n            GunDamageSpec scaledDamage = GunDamageSpec.Create(\n                Damage.Category,\n                Damage.DirectDamage * multiplier,\n                Damage.AreaDamage * multiplier,\n                Damage.DamageOverTimePerSecond * multiplier,\n                Damage.DamageOverTimeDurationSeconds,\n                Damage.Knockback);\n            return new ProjectileExecutionProfile(\n                SourceBlueprint,\n                DefinitionId,\n                EquipmentInstanceId,\n                ExecutionMode,\n                CanonicalDeliveryType,\n                Projectile,\n                MaximumAttackDistance,\n                Pierce,\n                Ricochet,\n                Guidance,\n                Impact,\n                scaledDamage,\n                Effects,\n                MovementPenaltyPercent);\n        }\n\n        private static void ValidateDeliveryProjection(\n            GunDeliveryType deliveryType,''')

run_loot = "Assets/ShooterMover/UI/Game/RunLoot.cs"
replace_once(
    run_loot,
    '''using ShooterMover.Domain.Progression.Context;\nusing ShooterMover.Domain.Props;''',
    '''using ShooterMover.Domain.Progression.Context;\nusing ShooterMover.Domain.Progression.Skills;\nusing ShooterMover.Domain.Props;''')
replace_once(
    run_loot,
    '''    internal sealed class RunLoot\n    {\n        public const int GenerationAlgorithmVersion = 1;''',
    '''    internal sealed class RunLoot\n    {\n        public const int GenerationAlgorithmVersion = 1;\n        private const string CashDropSkillId = "generic.cash_drop_size";''')
replace_once(
    run_loot,
    '''            run.ConfigureRewardEnvironment(new RunRewardEnvironmentSnapshot(\n                gameModeId,\n                Array.Empty<StableId>(),\n                1000,\n                1000,\n                RunDropPacingCatalog.Default));''',
    '''            RankedSkillAllocationSnapshot allocation;\n            if (!graph.SkillAuthority.TryGet(\n                    graph.SkillProfileId,\n                    out allocation)\n                || allocation == null)\n            {\n                throw new InvalidOperationException(\n                    "The selected character skill allocation is unavailable at run start.");\n            }\n            int cashMultiplier = checked(\n                1000 + allocation.RankOf(CashDropSkillId) * 10);\n            run.ConfigureRewardEnvironment(new RunRewardEnvironmentSnapshot(\n                gameModeId,\n                Array.Empty<StableId>(),\n                cashMultiplier,\n                1000,\n                RunDropPacingCatalog.Default));''')

Path("tools/patch-basic-skills.py").unlink()
Path(".github/workflows/apply-basic-skills.yml").unlink()
