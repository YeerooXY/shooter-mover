using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Foundation;

namespace ShooterMover.Application.Enemies.Foundation
{
    public sealed class EnemyPkg
    {
        public const int CurrentSchema = 1;

        public EnemyPkg(int schema, StableId version, EnemyDef enemy)
        {
            Schema = schema;
            Version = version ?? throw new ArgumentNullException(nameof(version));
            Enemy = enemy ?? throw new ArgumentNullException(nameof(enemy));
        }

        public int Schema { get; }
        public StableId Version { get; }
        public EnemyDef Enemy { get; }
    }

    public sealed class EnemyPkgIssue
    {
        public EnemyPkgIssue(string code, string path, string message)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Issue code is required.", nameof(code));
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Issue path is required.", nameof(path));
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Issue message is required.", nameof(message));

            Code = code.Trim();
            Path = path.Trim();
            Message = message.Trim();
        }

        public string Code { get; }
        public string Path { get; }
        public string Message { get; }
    }

    public sealed class EnemyPkgResult
    {
        private readonly ReadOnlyCollection<EnemyPkgIssue> issues;

        public EnemyPkgResult(EnemyPkg package, IEnumerable<EnemyPkgIssue> issues)
        {
            Package = package;
            this.issues = new ReadOnlyCollection<EnemyPkgIssue>(
                new List<EnemyPkgIssue>(issues ?? Array.Empty<EnemyPkgIssue>()));
        }

        public EnemyPkg Package { get; }
        public IReadOnlyList<EnemyPkgIssue> Issues { get { return issues; } }
        public bool IsValid { get { return Package != null && issues.Count == 0; } }
    }

    public interface IEnemyRefs
    {
        bool HasGun(StableId id);
        bool HasView(StableId id);
        bool HasMove(StableId id);
        bool HasAi(StableId id);
        bool HasEffect(StableId id);
        bool HasPerk(StableId id);
        bool HasMod(StableId id);
        bool HasXp(StableId id);
        bool HasLoot(StableId id);
    }

    public sealed class EnemyRefs : IEnemyRefs
    {
        private readonly HashSet<StableId> guns;
        private readonly HashSet<StableId> views;
        private readonly HashSet<StableId> moves;
        private readonly HashSet<StableId> ai;
        private readonly HashSet<StableId> effects;
        private readonly HashSet<StableId> perks;
        private readonly HashSet<StableId> mods;
        private readonly HashSet<StableId> xp;
        private readonly HashSet<StableId> loot;

        public EnemyRefs(
            IEnumerable<StableId> guns,
            IEnumerable<StableId> views,
            IEnumerable<StableId> moves,
            IEnumerable<StableId> ai,
            IEnumerable<StableId> effects,
            IEnumerable<StableId> perks,
            IEnumerable<StableId> mods,
            IEnumerable<StableId> xp,
            IEnumerable<StableId> loot)
        {
            this.guns = Copy(guns, nameof(guns));
            this.views = Copy(views, nameof(views));
            this.moves = Copy(moves, nameof(moves));
            this.ai = Copy(ai, nameof(ai));
            this.effects = Copy(effects, nameof(effects));
            this.perks = Copy(perks, nameof(perks));
            this.mods = Copy(mods, nameof(mods));
            this.xp = Copy(xp, nameof(xp));
            this.loot = Copy(loot, nameof(loot));
        }

        public bool HasGun(StableId id) { return Has(guns, id); }
        public bool HasView(StableId id) { return Has(views, id); }
        public bool HasMove(StableId id) { return Has(moves, id); }
        public bool HasAi(StableId id) { return Has(ai, id); }
        public bool HasEffect(StableId id) { return Has(effects, id); }
        public bool HasPerk(StableId id) { return Has(perks, id); }
        public bool HasMod(StableId id) { return Has(mods, id); }
        public bool HasXp(StableId id) { return Has(xp, id); }
        public bool HasLoot(StableId id) { return Has(loot, id); }

        private static bool Has(HashSet<StableId> source, StableId id)
        {
            return id != null && source.Contains(id);
        }

        private static HashSet<StableId> Copy(
            IEnumerable<StableId> values,
            string name)
        {
            if (values == null) throw new ArgumentNullException(name);
            var result = new HashSet<StableId>();
            foreach (StableId value in values)
            {
                if (value == null)
                    throw new ArgumentException("Reference catalogs cannot contain null IDs.", name);
                if (!result.Add(value))
                    throw new ArgumentException("Reference ID is duplicated: " + value, name);
            }
            return result;
        }
    }

    public static class EnemyPkgCheck
    {
        public static EnemyPkgResult Check(
            EnemyPkg package,
            IEnemyRefs refs,
            IEnumerable<StableId> existingEnemyIds)
        {
            if (package == null) throw new ArgumentNullException(nameof(package));
            if (refs == null) throw new ArgumentNullException(nameof(refs));

            var issues = new List<EnemyPkgIssue>();
            if (package.Schema != EnemyPkg.CurrentSchema)
            {
                Add(
                    issues,
                    "enemy-pkg-schema-unsupported",
                    "$.schema",
                    "Unsupported enemy package schema: " + package.Schema);
            }

            if (existingEnemyIds != null)
            {
                foreach (StableId id in existingEnemyIds)
                {
                    if (id != null && id == package.Enemy.Id)
                    {
                        Add(
                            issues,
                            "enemy-pkg-id-exists",
                            "$.enemy.id",
                            "Enemy ID already exists: " + id);
                        break;
                    }
                }
            }

            EnemyDef enemy = package.Enemy;
            Need(
                refs.HasView(enemy.ViewId),
                issues,
                "enemy-pkg-view-missing",
                "$.enemy.view",
                enemy.ViewId);
            Need(
                refs.HasMove(enemy.MoveId),
                issues,
                "enemy-pkg-move-missing",
                "$.enemy.move",
                enemy.MoveId);
            Need(
                refs.HasAi(enemy.AiId),
                issues,
                "enemy-pkg-ai-missing",
                "$.enemy.ai",
                enemy.AiId);
            Need(
                refs.HasXp(enemy.XpId),
                issues,
                "enemy-pkg-xp-missing",
                "$.enemy.xp",
                enemy.XpId);
            Need(
                refs.HasLoot(enemy.LootId),
                issues,
                "enemy-pkg-loot-missing",
                "$.enemy.loot",
                enemy.LootId);

            CheckAttacks(enemy.Attacks, refs, issues);
            CheckVariants(enemy.Variants, refs, issues);
            CheckPerks(enemy.Perks, refs, issues);
            CheckPhases(enemy.Phases, refs, issues);

            return new EnemyPkgResult(package, issues);
        }

        public static EnemyPkgResult Check(EnemyPkg package, IEnemyRefs refs)
        {
            return Check(package, refs, null);
        }

        private static void CheckAttacks(
            IReadOnlyList<AttackDef> attacks,
            IEnemyRefs refs,
            List<EnemyPkgIssue> issues)
        {
            for (int index = 0; index < attacks.Count; index++)
            {
                string path = "$.enemy.attacks[" + index + "]";
                GunAttack gun = attacks[index] as GunAttack;
                if (gun != null)
                {
                    Need(
                        refs.HasGun(gun.GunId),
                        issues,
                        "enemy-pkg-gun-missing",
                        path + ".gun",
                        gun.GunId);
                    continue;
                }

                MeleeAttack melee = attacks[index] as MeleeAttack;
                if (melee != null)
                {
                    CheckEffects(melee.Effects, refs, issues, path + ".effects");
                    continue;
                }

                ChargeAttack charge = attacks[index] as ChargeAttack;
                if (charge != null)
                {
                    CheckEffects(charge.Effects, refs, issues, path + ".effects");
                    continue;
                }

                ExplodeAttack explode = attacks[index] as ExplodeAttack;
                if (explode != null)
                {
                    CheckEffects(explode.Effects, refs, issues, path + ".effects");
                    bool hasExplosion = false;
                    for (int effectIndex = 0;
                        effectIndex < explode.Effects.Count;
                        effectIndex++)
                    {
                        if (explode.Effects[effectIndex] is ExplosionRef)
                        {
                            hasExplosion = true;
                            break;
                        }
                    }
                    if (!hasExplosion)
                    {
                        Add(
                            issues,
                            "enemy-pkg-explode-effect-missing",
                            path + ".effects",
                            "Explode attacks require an explosion effect.");
                    }
                    continue;
                }

                Add(
                    issues,
                    "enemy-pkg-attack-unsupported",
                    path,
                    "Unsupported attack type: " + attacks[index].GetType().Name);
            }
        }

        private static void CheckEffects(
            IReadOnlyList<EffectRef> effects,
            IEnemyRefs refs,
            List<EnemyPkgIssue> issues,
            string path)
        {
            for (int index = 0; index < effects.Count; index++)
            {
                Need(
                    refs.HasEffect(effects[index].Id),
                    issues,
                    "enemy-pkg-effect-missing",
                    path + "[" + index + "].id",
                    effects[index].Id);
            }
        }

        private static void CheckVariants(
            IReadOnlyList<VariantDef> variants,
            IEnemyRefs refs,
            List<EnemyPkgIssue> issues)
        {
            for (int index = 0; index < variants.Count; index++)
            {
                for (int modIndex = 0;
                    modIndex < variants[index].Mods.Count;
                    modIndex++)
                {
                    Need(
                        refs.HasMod(variants[index].Mods[modIndex]),
                        issues,
                        "enemy-pkg-mod-missing",
                        "$.enemy.variants[" + index + "].mods[" + modIndex + "]",
                        variants[index].Mods[modIndex]);
                }
            }
        }

        private static void CheckPerks(
            PerkRules perks,
            IEnemyRefs refs,
            List<EnemyPkgIssue> issues)
        {
            for (int index = 0; index < perks.Fixed.Count; index++)
            {
                Need(
                    refs.HasPerk(perks.Fixed[index]),
                    issues,
                    "enemy-pkg-perk-missing",
                    "$.enemy.perks.fixed[" + index + "]",
                    perks.Fixed[index]);
            }
            for (int index = 0; index < perks.Pool.Count; index++)
            {
                Need(
                    refs.HasPerk(perks.Pool[index]),
                    issues,
                    "enemy-pkg-perk-missing",
                    "$.enemy.perks.pool[" + index + "]",
                    perks.Pool[index]);
            }
        }

        private static void CheckPhases(
            IReadOnlyList<PhaseDef> phases,
            IEnemyRefs refs,
            List<EnemyPkgIssue> issues)
        {
            for (int index = 0; index < phases.Count; index++)
            {
                for (int modIndex = 0;
                    modIndex < phases[index].Mods.Count;
                    modIndex++)
                {
                    Need(
                        refs.HasMod(phases[index].Mods[modIndex]),
                        issues,
                        "enemy-pkg-mod-missing",
                        "$.enemy.phases[" + index + "].mods[" + modIndex + "]",
                        phases[index].Mods[modIndex]);
                }
            }
        }

        private static void Need(
            bool valid,
            List<EnemyPkgIssue> issues,
            string code,
            string path,
            StableId id)
        {
            if (!valid)
            {
                Add(
                    issues,
                    code,
                    path,
                    "Missing canonical reference: " + id);
            }
        }

        private static void Add(
            List<EnemyPkgIssue> issues,
            string code,
            string path,
            string message)
        {
            issues.Add(new EnemyPkgIssue(code, path, message));
        }
    }
}
