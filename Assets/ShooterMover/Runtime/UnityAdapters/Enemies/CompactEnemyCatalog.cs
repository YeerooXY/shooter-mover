using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Enemies
{
    [Serializable]
    public sealed class CompactEnemyCatalogDocument
    {
        public int schema;
        public CompactEnemyLeveling leveling;
        public CompactEnemyShotDefinition[] shots;
        public CompactEnemyDefinition[] enemies;
    }

    [Serializable]
    public sealed class CompactEnemyLeveling
    {
        public int minLevel = 1;
        public int maxLevel = 100;
        public double strengthAtMax = 1d;
        public double damagePower = 1d;
        public CompactEnemyColorStop[] colors;
    }

    [Serializable]
    public sealed class CompactEnemyColorStop
    {
        public int level;
        public string color;
    }

    [Serializable]
    public sealed class CompactEnemyDefinition
    {
        public int schema;
        public string id;
        public string name;
        public string[] tags;
        public double hp;
        public double healthPower;
        public CompactEnemyMovement movement;
        public double detectionRange;
        public CompactEnemyMount[] mounts;
        public CompactEnemyAttack[] attacks;
        public string traitPool;
        public string drops;
        public string art;
        public CompactEnemyBody body;
    }

    [Serializable]
    public sealed class CompactEnemyMovement
    {
        public string kind;
        public double speed;
    }

    [Serializable]
    public sealed class CompactEnemyMount
    {
        public string id;
        public CompactEnemyPoint position;
        public double rotation;
        public string art;
    }

    [Serializable]
    public sealed class CompactEnemyAttack
    {
        public string id;
        public string kind;
        public string shot;
        public string[] emitters;
        public string firePattern;
        public double cooldown;
        public CompactEnemySequence sequence;
        public CompactEnemyVolley volley;
        public CompactEnemyRange range;
        public CompactEnemyDamage[] damage;
    }

    [Serializable]
    public sealed class CompactEnemySequence
    {
        public int triggers = 1;
        public double interval;
    }

    [Serializable]
    public sealed class CompactEnemyVolley
    {
        public int shotsPerTrigger = 1;
        public double spread;
        public string distribution;
    }

    [Serializable]
    public sealed class CompactEnemyRange
    {
        public double min;
        public double max;
    }

    [Serializable]
    public sealed class CompactEnemyDamage
    {
        public string type;
        public double amount;
        public double perSecond;
        public double duration;
        public string stack;
    }

    [Serializable]
    public sealed class CompactEnemyBody
    {
        public string shape;
        public double radius;
        public CompactEnemyPoint offset;
    }

    [Serializable]
    public sealed class CompactEnemyPoint
    {
        public double x;
        public double y;
    }

    [Serializable]
    public sealed class CompactEnemyShotDefinition
    {
        public int schema;
        public string id;
        public CompactEnemyShotDelivery delivery;
        public CompactEnemyShotImpact impact;
        public CompactEnemyShotArt art;
    }

    [Serializable]
    public sealed class CompactEnemyShotDelivery
    {
        public string kind;
        public double speed;
        public double radius;
        public double range;
    }

    [Serializable]
    public sealed class CompactEnemyShotImpact
    {
        public int pierce;
        public int ricochet;
        public double knockback;
    }

    [Serializable]
    public sealed class CompactEnemyShotArt
    {
        public string delivery;
        public string trail;
        public string impact;
    }

    public readonly struct CompactEnemyResolvedStats
    {
        public CompactEnemyResolvedStats(
            int level,
            double strength,
            double maximumHealth,
            double damageMultiplier,
            Color color)
        {
            Level = level;
            Strength = strength;
            MaximumHealth = maximumHealth;
            DamageMultiplier = damageMultiplier;
            Color = color;
        }

        public int Level { get; }
        public double Strength { get; }
        public double MaximumHealth { get; }
        public double DamageMultiplier { get; }
        public Color Color { get; }
    }

    public static class CompactEnemyCatalog
    {
        public const string ResourcePath = "Enemies/CompactEnemyCatalog";
        public const string PresentationId = "presentation.enemy-compact";

        private static CompactEnemyCatalogDocument document;
        private static Dictionary<string, CompactEnemyDefinition> enemies;
        private static Dictionary<string, CompactEnemyShotDefinition> shots;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            document = null;
            enemies = null;
            shots = null;
        }

        public static bool IsCompactPresentation(StableId presentationStableId)
        {
            return presentationStableId != null
                && string.Equals(
                    presentationStableId.ToString(),
                    PresentationId,
                    StringComparison.Ordinal);
        }

        public static bool TryResolve(
            StableId runtimeDefinitionStableId,
            out CompactEnemyDefinition definition)
        {
            definition = null;
            if (runtimeDefinitionStableId == null
                || !string.Equals(
                    runtimeDefinitionStableId.Namespace,
                    "enemy",
                    StringComparison.Ordinal))
            {
                return false;
            }

            EnsureLoaded();
            return enemies.TryGetValue(
                runtimeDefinitionStableId.Value,
                out definition);
        }

        public static bool TryResolveShot(
            string shotId,
            out CompactEnemyShotDefinition shot)
        {
            shot = null;
            if (string.IsNullOrWhiteSpace(shotId)) return false;
            EnsureLoaded();
            return shots.TryGetValue(shotId, out shot);
        }

        public static CompactEnemyResolvedStats ResolveStats(
            CompactEnemyDefinition definition,
            int requestedLevel)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            EnsureLoaded();
            CompactEnemyLeveling leveling = document.leveling;
            int level = Mathf.Clamp(
                requestedLevel,
                leveling.minLevel,
                leveling.maxLevel);
            double span = Math.Max(1d, leveling.maxLevel - leveling.minLevel);
            double normalized = (level - leveling.minLevel) / span;
            double strength = Math.Pow(
                Math.Max(1d, leveling.strengthAtMax),
                normalized);
            double health = definition.hp
                * Math.Pow(strength, Math.Max(0.0001d, definition.healthPower));
            double damageMultiplier = Math.Pow(
                strength,
                Math.Max(0.0001d, leveling.damagePower));
            return new CompactEnemyResolvedStats(
                level,
                strength,
                health,
                damageMultiplier,
                LevelColor(level, leveling));
        }

        private static void EnsureLoaded()
        {
            if (document != null) return;
            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    "compact-enemy-catalog-resource-missing:" + ResourcePath);
            }

            CompactEnemyCatalogDocument parsed =
                JsonUtility.FromJson<CompactEnemyCatalogDocument>(asset.text);
            if (parsed == null
                || parsed.schema != 1
                || parsed.leveling == null
                || parsed.enemies == null
                || parsed.shots == null)
            {
                throw new InvalidOperationException(
                    "compact-enemy-catalog-invalid");
            }

            var enemyIndex = new Dictionary<string, CompactEnemyDefinition>(
                StringComparer.Ordinal);
            for (int index = 0; index < parsed.enemies.Length; index++)
            {
                CompactEnemyDefinition enemy = parsed.enemies[index];
                if (enemy == null
                    || enemy.schema != 1
                    || string.IsNullOrWhiteSpace(enemy.id)
                    || enemy.body == null
                    || !string.Equals(enemy.body.shape, "circle", StringComparison.Ordinal)
                    || enemy.hp <= 0d
                    || enemy.movement == null
                    || enemy.attacks == null
                    || enemy.mounts == null
                    || enemyIndex.ContainsKey(enemy.id))
                {
                    throw new InvalidOperationException(
                        "compact-enemy-definition-invalid");
                }

                enemyIndex.Add(enemy.id, enemy);
            }

            var shotIndex = new Dictionary<string, CompactEnemyShotDefinition>(
                StringComparer.Ordinal);
            for (int index = 0; index < parsed.shots.Length; index++)
            {
                CompactEnemyShotDefinition shot = parsed.shots[index];
                if (shot == null
                    || shot.schema != 1
                    || string.IsNullOrWhiteSpace(shot.id)
                    || shot.delivery == null
                    || !string.Equals(
                        shot.delivery.kind,
                        "projectile",
                        StringComparison.Ordinal)
                    || shot.delivery.speed <= 0d
                    || shot.delivery.radius <= 0d
                    || shot.delivery.range <= 0d
                    || shotIndex.ContainsKey(shot.id))
                {
                    throw new InvalidOperationException(
                        "compact-enemy-shot-invalid");
                }

                shotIndex.Add(shot.id, shot);
            }

            document = parsed;
            enemies = enemyIndex;
            shots = shotIndex;
        }

        private static Color LevelColor(
            int level,
            CompactEnemyLeveling leveling)
        {
            CompactEnemyColorStop[] values = leveling.colors;
            if (values == null || values.Length == 0) return Color.white;
            if (level <= values[0].level) return ParseColor(values[0].color);
            for (int index = 1; index < values.Length; index++)
            {
                CompactEnemyColorStop right = values[index];
                if (level > right.level) continue;
                CompactEnemyColorStop left = values[index - 1];
                double width = Math.Max(1d, right.level - left.level);
                float amount = (float)((level - left.level) / width);
                return Color.Lerp(
                    ParseColor(left.color),
                    ParseColor(right.color),
                    amount);
            }

            return ParseColor(values[values.Length - 1].color);
        }

        private static Color ParseColor(string value)
        {
            Color parsed;
            return ColorUtility.TryParseHtmlString(value, out parsed)
                ? parsed
                : Color.white;
        }
    }
}
