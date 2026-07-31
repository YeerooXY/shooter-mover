using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.Domain.Enemies.Foundation;

namespace ShooterMover.Application.Enemies.Foundation
{
    public static class EnemyPkgJson
    {
        private static readonly DataContractJsonSerializer Serializer =
            new DataContractJsonSerializer(
                typeof(PkgDto),
                new DataContractJsonSerializerSettings
                {
                    UseSimpleDictionaryFormat = true,
                });

        public static EnemyPkgResult Import(
            string json,
            IEnemyRefs refs,
            IEnumerable<StableId> existingEnemyIds)
        {
            if (refs == null) throw new ArgumentNullException(nameof(refs));
            if (string.IsNullOrWhiteSpace(json))
            {
                return Fail(
                    "enemy-pkg-json-invalid",
                    "$",
                    "Enemy package JSON is required.");
            }

            PkgDto dto;
            try
            {
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    dto = Serializer.ReadObject(stream) as PkgDto;
                }
            }
            catch (Exception exception)
            {
                if (!(exception is SerializationException)
                    && !(exception is FormatException)
                    && !(exception is InvalidDataContractException))
                {
                    throw;
                }
                return Fail(
                    "enemy-pkg-json-invalid",
                    "$",
                    "Malformed enemy package JSON: " + exception.Message);
            }

            if (dto == null)
            {
                return Fail(
                    "enemy-pkg-json-invalid",
                    "$",
                    "JSON root must be an object.");
            }

            try
            {
                EnemyPkg package = Map(dto);
                return EnemyPkgCheck.Check(package, refs, existingEnemyIds);
            }
            catch (MapException exception)
            {
                return Fail(exception.Code, exception.Path, exception.Message);
            }
        }

        public static EnemyPkgResult Import(string json, IEnemyRefs refs)
        {
            return Import(json, refs, null);
        }

        public static string Export(EnemyPkg package)
        {
            if (package == null) throw new ArgumentNullException(nameof(package));
            PkgDto dto = Project(package);
            using (var stream = new MemoryStream())
            {
                Serializer.WriteObject(stream, dto);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static EnemyPkg Map(PkgDto dto)
        {
            StableId version = Id(dto.Version, "$.version");
            EnemyDef enemy = MapEnemy(Need(dto.Enemy, "$.enemy"));
            return new EnemyPkg(dto.Schema, version, enemy);
        }

        private static EnemyDef MapEnemy(EnemyDto dto)
        {
            const string path = "$.enemy";
            return Build(
                path,
                delegate
                {
                    return new EnemyDef(
                        Id(dto.Id, path + ".id"),
                        Id(dto.View, path + ".view"),
                        MapBody(Need(dto.Body, path + ".body"), path + ".body"),
                        MapStats(Need(dto.Stats, path + ".stats"), path + ".stats"),
                        MapSense(Need(dto.Sense, path + ".sense"), path + ".sense"),
                        Id(dto.Move, path + ".move"),
                        Id(dto.Ai, path + ".ai"),
                        MapAttacks(Need(dto.Attacks, path + ".attacks"), path + ".attacks"),
                        MapTiers(Need(dto.Tiers, path + ".tiers"), path + ".tiers"),
                        MapVariants(
                            dto.Variants ?? new List<VariantDto>(),
                            path + ".variants"),
                        MapPerks(Need(dto.Perks, path + ".perks"), path + ".perks"),
                        MapPhases(
                            dto.Phases ?? new List<PhaseDto>(),
                            path + ".phases"),
                        Id(dto.Xp, path + ".xp"),
                        Id(dto.Loot, path + ".loot"),
                        ClearRole(dto.ClearRole, path + ".clear_role"));
                });
        }

        private static BodyDef MapBody(BodyDto dto, string path)
        {
            var mounts = new List<MountDef>();
            List<MountDto> source = dto.Mounts ?? new List<MountDto>();
            for (int index = 0; index < source.Count; index++)
            {
                string itemPath = path + ".mounts[" + index + "]";
                MountDto mount = Need(source[index], itemPath);
                mounts.Add(Build(
                    itemPath,
                    delegate
                    {
                        return new MountDef(
                            Id(mount.Id, itemPath + ".id"),
                            MapVec(
                                Need(mount.Position, itemPath + ".position"),
                                itemPath + ".position"),
                            MapVec(
                                Need(mount.Direction, itemPath + ".direction"),
                                itemPath + ".direction"));
                    }));
            }

            return Build(
                path,
                delegate
                {
                    return new BodyDef(
                        Travel(dto.Travel, path + ".travel"),
                        dto.Radius,
                        dto.Mass,
                        mounts);
                });
        }

        private static StatsDef MapStats(StatsDto dto, string path)
        {
            return Build(path, delegate { return new StatsDef(dto.Health); });
        }

        private static SenseDef MapSense(SenseDto dto, string path)
        {
            return Build(
                path,
                delegate { return new SenseDef(dto.Range, dto.Arc); });
        }

        private static Vec2 MapVec(VecDto dto, string path)
        {
            return Build(path, delegate { return new Vec2(dto.X, dto.Y); });
        }

        private static List<AttackDef> MapAttacks(
            List<AttackDto> source,
            string path)
        {
            var result = new List<AttackDef>();
            for (int index = 0; index < source.Count; index++)
            {
                string itemPath = path + "[" + index + "]";
                result.Add(MapAttack(Need(source[index], itemPath), itemPath));
            }
            return result;
        }

        private static AttackDef MapAttack(AttackDto dto, string path)
        {
            string kind = Text(dto.Kind, path + ".kind");
            StableId id = Id(dto.Id, path + ".id");

            if (string.Equals(kind, "gun", StringComparison.Ordinal))
            {
                Shape(
                    dto.Plan != null
                        && dto.Melee == null
                        && dto.Charge == null
                        && dto.Explode == null,
                    path);
                return new GunAttack(
                    id,
                    Id(dto.Gun, path + ".gun"),
                    MapPlan(dto.Plan, path + ".plan"));
            }

            if (string.Equals(kind, "melee", StringComparison.Ordinal))
            {
                Shape(
                    string.IsNullOrWhiteSpace(dto.Gun)
                        && dto.Plan == null
                        && dto.Melee != null
                        && dto.Charge == null
                        && dto.Explode == null,
                    path);
                MeleeDto value = dto.Melee;
                return Build(
                    path,
                    delegate
                    {
                        return new MeleeAttack(
                            id,
                            value.Range,
                            value.WindUp,
                            value.Active,
                            value.Recovery,
                            MapEffects(
                                Need(value.Effects, path + ".effects"),
                                path + ".effects"));
                    });
            }

            if (string.Equals(kind, "charge", StringComparison.Ordinal))
            {
                Shape(
                    string.IsNullOrWhiteSpace(dto.Gun)
                        && dto.Plan == null
                        && dto.Melee == null
                        && dto.Charge != null
                        && dto.Explode == null,
                    path);
                ChargeDto value = dto.Charge;
                return Build(
                    path,
                    delegate
                    {
                        return new ChargeAttack(
                            id,
                            value.Speed,
                            value.Distance,
                            value.WindUp,
                            value.Recovery,
                            MapEffects(
                                Need(value.Effects, path + ".effects"),
                                path + ".effects"));
                    });
            }

            if (string.Equals(kind, "explode", StringComparison.Ordinal))
            {
                Shape(
                    string.IsNullOrWhiteSpace(dto.Gun)
                        && dto.Plan == null
                        && dto.Melee == null
                        && dto.Charge == null
                        && dto.Explode != null,
                    path);
                ExplodeDto value = dto.Explode;
                return Build(
                    path,
                    delegate
                    {
                        return new ExplodeAttack(
                            id,
                            value.WindUp,
                            MapEffects(
                                Need(value.Effects, path + ".effects"),
                                path + ".effects"));
                    });
            }

            throw new MapException(
                "enemy-pkg-attack-kind-invalid",
                path + ".kind",
                "Unknown attack kind: " + kind);
        }

        private static ShotPlan MapPlan(PlanDto dto, string path)
        {
            return Build(
                path,
                delegate
                {
                    return new ShotPlan(
                        MapIds(Need(dto.Mounts, path + ".mounts"), path + ".mounts"),
                        Fire(dto.FireMode, path + ".fire_mode"),
                        Order(dto.Order, path + ".order"),
                        dto.Shots,
                        dto.Interval);
                });
        }

        private static List<EffectRef> MapEffects(
            List<EffectDto> source,
            string path)
        {
            var result = new List<EffectRef>();
            for (int index = 0; index < source.Count; index++)
            {
                string itemPath = path + "[" + index + "]";
                EffectDto dto = Need(source[index], itemPath);
                StableId id = Id(dto.Id, itemPath + ".id");
                string kind = Text(dto.Kind, itemPath + ".kind");
                if (string.Equals(kind, "damage", StringComparison.Ordinal))
                    result.Add(new DamageRef(id));
                else if (string.Equals(kind, "burn", StringComparison.Ordinal))
                    result.Add(new BurnRef(id));
                else if (string.Equals(kind, "explosion", StringComparison.Ordinal))
                    result.Add(new ExplosionRef(id));
                else if (string.Equals(kind, "slow", StringComparison.Ordinal))
                    result.Add(new SlowRef(id));
                else if (string.Equals(kind, "knockback", StringComparison.Ordinal))
                    result.Add(new KnockbackRef(id));
                else
                {
                    throw new MapException(
                        "enemy-pkg-effect-kind-invalid",
                        itemPath + ".kind",
                        "Unknown effect kind: " + kind);
                }
            }
            return result;
        }

        private static List<EnemyTier> MapTiers(
            List<int> source,
            string path)
        {
            var result = new List<EnemyTier>();
            for (int index = 0; index < source.Count; index++)
            {
                int value = source[index];
                if (value < 1 || value > 4)
                {
                    throw new MapException(
                        "enemy-pkg-tier-invalid",
                        path + "[" + index + "]",
                        "Enemy tier must be between 1 and 4.");
                }
                result.Add((EnemyTier)value);
            }
            return result;
        }

        private static List<VariantDef> MapVariants(
            List<VariantDto> source,
            string path)
        {
            var result = new List<VariantDef>();
            for (int index = 0; index < source.Count; index++)
            {
                string itemPath = path + "[" + index + "]";
                VariantDto dto = Need(source[index], itemPath);
                result.Add(Build(
                    itemPath,
                    delegate
                    {
                        return new VariantDef(
                            Id(dto.Id, itemPath + ".id"),
                            MapIds(dto.Mods ?? new List<string>(), itemPath + ".mods"));
                    }));
            }
            return result;
        }

        private static PerkRules MapPerks(PerksDto dto, string path)
        {
            return Build(
                path,
                delegate
                {
                    return new PerkRules(
                        MapIds(dto.Fixed ?? new List<string>(), path + ".fixed"),
                        MapIds(dto.Pool ?? new List<string>(), path + ".pool"),
                        dto.Rolls);
                });
        }

        private static List<PhaseDef> MapPhases(
            List<PhaseDto> source,
            string path)
        {
            var result = new List<PhaseDef>();
            for (int index = 0; index < source.Count; index++)
            {
                string itemPath = path + "[" + index + "]";
                PhaseDto dto = Need(source[index], itemPath);
                result.Add(Build(
                    itemPath,
                    delegate
                    {
                        return new PhaseDef(
                            Id(dto.Id, itemPath + ".id"),
                            dto.Health,
                            MapIds(dto.Mods ?? new List<string>(), itemPath + ".mods"));
                    }));
            }
            return result;
        }

        private static List<StableId> MapIds(List<string> source, string path)
        {
            var result = new List<StableId>();
            for (int index = 0; index < source.Count; index++)
                result.Add(Id(source[index], path + "[" + index + "]"));
            return result;
        }

        private static PkgDto Project(EnemyPkg package)
        {
            return new PkgDto
            {
                Schema = package.Schema,
                Version = package.Version.ToString(),
                Enemy = ProjectEnemy(package.Enemy),
            };
        }

        private static EnemyDto ProjectEnemy(EnemyDef enemy)
        {
            return new EnemyDto
            {
                Id = enemy.Id.ToString(),
                View = enemy.ViewId.ToString(),
                Body = ProjectBody(enemy.Body),
                Stats = new StatsDto { Health = enemy.Stats.Health },
                Sense = new SenseDto
                {
                    Range = enemy.Sense.Range,
                    Arc = enemy.Sense.ArcDegrees,
                },
                Move = enemy.MoveId.ToString(),
                Ai = enemy.AiId.ToString(),
                Attacks = ProjectAttacks(enemy.Attacks),
                Tiers = ProjectTiers(enemy.Tiers),
                Variants = ProjectVariants(enemy.Variants),
                Perks = ProjectPerks(enemy.Perks),
                Phases = ProjectPhases(enemy.Phases),
                Xp = enemy.XpId.ToString(),
                Loot = enemy.LootId.ToString(),
                ClearRole = ClearRole(enemy.ClearRole),
            };
        }

        private static BodyDto ProjectBody(BodyDef body)
        {
            var mounts = new List<MountDto>();
            for (int index = 0; index < body.Mounts.Count; index++)
            {
                MountDef mount = body.Mounts[index];
                mounts.Add(new MountDto
                {
                    Id = mount.Id.ToString(),
                    Position = new VecDto
                    {
                        X = mount.Position.X,
                        Y = mount.Position.Y,
                    },
                    Direction = new VecDto
                    {
                        X = mount.Direction.X,
                        Y = mount.Direction.Y,
                    },
                });
            }
            return new BodyDto
            {
                Travel = Travel(body.Travel),
                Radius = body.Radius,
                Mass = body.Mass,
                Mounts = mounts,
            };
        }

        private static List<AttackDto> ProjectAttacks(
            IReadOnlyList<AttackDef> source)
        {
            var result = new List<AttackDto>();
            for (int index = 0; index < source.Count; index++)
            {
                AttackDef attack = source[index];
                GunAttack gun = attack as GunAttack;
                if (gun != null)
                {
                    result.Add(new AttackDto
                    {
                        Kind = "gun",
                        Id = gun.Id.ToString(),
                        Gun = gun.GunId.ToString(),
                        Plan = new PlanDto
                        {
                            Mounts = Strings(gun.Plan.Mounts),
                            FireMode = Fire(gun.Plan.FireMode),
                            Order = Order(gun.Plan.Order),
                            Shots = gun.Plan.Shots,
                            Interval = gun.Plan.IntervalSeconds,
                        },
                    });
                    continue;
                }

                MeleeAttack melee = attack as MeleeAttack;
                if (melee != null)
                {
                    result.Add(new AttackDto
                    {
                        Kind = "melee",
                        Id = melee.Id.ToString(),
                        Melee = new MeleeDto
                        {
                            Range = melee.Range,
                            WindUp = melee.WindUpSeconds,
                            Active = melee.ActiveSeconds,
                            Recovery = melee.RecoverySeconds,
                            Effects = ProjectEffects(melee.Effects),
                        },
                    });
                    continue;
                }

                ChargeAttack charge = attack as ChargeAttack;
                if (charge != null)
                {
                    result.Add(new AttackDto
                    {
                        Kind = "charge",
                        Id = charge.Id.ToString(),
                        Charge = new ChargeDto
                        {
                            Speed = charge.Speed,
                            Distance = charge.Distance,
                            WindUp = charge.WindUpSeconds,
                            Recovery = charge.RecoverySeconds,
                            Effects = ProjectEffects(charge.Effects),
                        },
                    });
                    continue;
                }

                ExplodeAttack explode = attack as ExplodeAttack;
                if (explode != null)
                {
                    result.Add(new AttackDto
                    {
                        Kind = "explode",
                        Id = explode.Id.ToString(),
                        Explode = new ExplodeDto
                        {
                            WindUp = explode.WindUpSeconds,
                            Effects = ProjectEffects(explode.Effects),
                        },
                    });
                    continue;
                }

                throw new InvalidOperationException(
                    "Unsupported enemy attack type: " + attack.GetType().Name);
            }
            return result;
        }

        private static List<EffectDto> ProjectEffects(
            IReadOnlyList<EffectRef> source)
        {
            var result = new List<EffectDto>();
            for (int index = 0; index < source.Count; index++)
            {
                EffectRef effect = source[index];
                string kind;
                if (effect is DamageRef) kind = "damage";
                else if (effect is BurnRef) kind = "burn";
                else if (effect is ExplosionRef) kind = "explosion";
                else if (effect is SlowRef) kind = "slow";
                else if (effect is KnockbackRef) kind = "knockback";
                else
                {
                    throw new InvalidOperationException(
                        "Unsupported enemy effect type: " + effect.GetType().Name);
                }
                result.Add(new EffectDto
                {
                    Kind = kind,
                    Id = effect.Id.ToString(),
                });
            }
            return result;
        }

        private static List<int> ProjectTiers(IReadOnlyList<EnemyTier> source)
        {
            var result = new List<int>();
            for (int index = 0; index < source.Count; index++)
                result.Add((int)source[index]);
            return result;
        }

        private static List<VariantDto> ProjectVariants(
            IReadOnlyList<VariantDef> source)
        {
            var result = new List<VariantDto>();
            for (int index = 0; index < source.Count; index++)
            {
                result.Add(new VariantDto
                {
                    Id = source[index].Id.ToString(),
                    Mods = Strings(source[index].Mods),
                });
            }
            return result;
        }

        private static PerksDto ProjectPerks(PerkRules perks)
        {
            return new PerksDto
            {
                Fixed = Strings(perks.Fixed),
                Pool = Strings(perks.Pool),
                Rolls = perks.Rolls,
            };
        }

        private static List<PhaseDto> ProjectPhases(
            IReadOnlyList<PhaseDef> source)
        {
            var result = new List<PhaseDto>();
            for (int index = 0; index < source.Count; index++)
            {
                result.Add(new PhaseDto
                {
                    Id = source[index].Id.ToString(),
                    Health = source[index].HealthAtOrBelow,
                    Mods = Strings(source[index].Mods),
                });
            }
            return result;
        }

        private static List<string> Strings(IReadOnlyList<StableId> source)
        {
            var result = new List<string>();
            for (int index = 0; index < source.Count; index++)
                result.Add(source[index].ToString());
            return result;
        }

        private static StableId Id(string value, string path)
        {
            string text = Text(value, path);
            try
            {
                return StableId.Parse(text);
            }
            catch (ArgumentException exception)
            {
                throw new MapException(
                    "enemy-pkg-id-invalid",
                    path,
                    "Invalid stable ID: " + exception.Message);
            }
        }

        private static string Text(string value, string path)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new MapException(
                    "enemy-pkg-value-missing",
                    path,
                    "Required value is missing.");
            }
            return value.Trim();
        }

        private static T Need<T>(T value, string path) where T : class
        {
            if (value == null)
            {
                throw new MapException(
                    "enemy-pkg-value-missing",
                    path,
                    "Required value is missing.");
            }
            return value;
        }

        private static T Build<T>(string path, Func<T> create)
        {
            try
            {
                return create();
            }
            catch (MapException)
            {
                throw;
            }
            catch (ArgumentException exception)
            {
                throw new MapException(
                    "enemy-pkg-value-invalid",
                    path,
                    exception.Message);
            }
        }

        private static void Shape(bool valid, string path)
        {
            if (!valid)
            {
                throw new MapException(
                    "enemy-pkg-attack-shape-invalid",
                    path,
                    "Attack fields do not match its kind.");
            }
        }

        private static TravelMode Travel(string value, string path)
        {
            string text = Text(value, path);
            if (text == "ground") return TravelMode.Ground;
            if (text == "flying") return TravelMode.Flying;
            throw EnumError(path, "travel mode", text);
        }

        private static string Travel(TravelMode value)
        {
            if (value == TravelMode.Ground) return "ground";
            if (value == TravelMode.Flying) return "flying";
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        private static FireMode Fire(string value, string path)
        {
            string text = Text(value, path);
            if (text == "alternating") return FireMode.Alternating;
            if (text == "simultaneous") return FireMode.Simultaneous;
            throw EnumError(path, "fire mode", text);
        }

        private static string Fire(FireMode value)
        {
            if (value == FireMode.Alternating) return "alternating";
            if (value == FireMode.Simultaneous) return "simultaneous";
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        private static MountOrder Order(string value, string path)
        {
            string text = Text(value, path);
            if (text == "listed") return MountOrder.Listed;
            if (text == "cycle") return MountOrder.Cycle;
            if (text == "weighted") return MountOrder.Weighted;
            throw EnumError(path, "mount order", text);
        }

        private static string Order(MountOrder value)
        {
            if (value == MountOrder.Listed) return "listed";
            if (value == MountOrder.Cycle) return "cycle";
            if (value == MountOrder.Weighted) return "weighted";
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        private static EnemyCatalogRoomClearRole ClearRole(
            string value,
            string path)
        {
            string text = Text(value, path);
            if (text == "required") return EnemyCatalogRoomClearRole.RequiredEnemy;
            if (text == "optional") return EnemyCatalogRoomClearRole.OptionalEnemy;
            if (text == "objective") return EnemyCatalogRoomClearRole.ObjectiveEntity;
            if (text == "ignored")
                return EnemyCatalogRoomClearRole.DoesNotAffectRoomClear;
            throw EnumError(path, "room-clear role", text);
        }

        private static string ClearRole(EnemyCatalogRoomClearRole value)
        {
            if (value == EnemyCatalogRoomClearRole.RequiredEnemy) return "required";
            if (value == EnemyCatalogRoomClearRole.OptionalEnemy) return "optional";
            if (value == EnemyCatalogRoomClearRole.ObjectiveEntity) return "objective";
            if (value == EnemyCatalogRoomClearRole.DoesNotAffectRoomClear)
                return "ignored";
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        private static MapException EnumError(
            string path,
            string type,
            string value)
        {
            return new MapException(
                "enemy-pkg-enum-invalid",
                path,
                "Unknown " + type + ": " + value);
        }

        private static EnemyPkgResult Fail(
            string code,
            string path,
            string message)
        {
            return new EnemyPkgResult(
                null,
                new[] { new EnemyPkgIssue(code, path, message) });
        }

        private sealed class MapException : Exception
        {
            public MapException(string code, string path, string message)
                : base(message)
            {
                Code = code;
                Path = path;
            }

            public string Code { get; }
            public string Path { get; }
        }

        [DataContract]
        private sealed class PkgDto
        {
            [DataMember(Name = "schema", IsRequired = true, Order = 0)]
            public int Schema;

            [DataMember(Name = "version", IsRequired = true, Order = 1)]
            public string Version;

            [DataMember(Name = "enemy", IsRequired = true, Order = 2)]
            public EnemyDto Enemy;
        }

        [DataContract]
        private sealed class EnemyDto
        {
            [DataMember(Name = "id", IsRequired = true, Order = 0)]
            public string Id;

            [DataMember(Name = "view", IsRequired = true, Order = 1)]
            public string View;

            [DataMember(Name = "body", IsRequired = true, Order = 2)]
            public BodyDto Body;

            [DataMember(Name = "stats", IsRequired = true, Order = 3)]
            public StatsDto Stats;

            [DataMember(Name = "sense", IsRequired = true, Order = 4)]
            public SenseDto Sense;

            [DataMember(Name = "move", IsRequired = true, Order = 5)]
            public string Move;

            [DataMember(Name = "ai", IsRequired = true, Order = 6)]
            public string Ai;

            [DataMember(Name = "attacks", IsRequired = true, Order = 7)]
            public List<AttackDto> Attacks;

            [DataMember(Name = "tiers", IsRequired = true, Order = 8)]
            public List<int> Tiers;

            [DataMember(Name = "variants", EmitDefaultValue = false, Order = 9)]
            public List<VariantDto> Variants;

            [DataMember(Name = "perks", IsRequired = true, Order = 10)]
            public PerksDto Perks;

            [DataMember(Name = "phases", EmitDefaultValue = false, Order = 11)]
            public List<PhaseDto> Phases;

            [DataMember(Name = "xp", IsRequired = true, Order = 12)]
            public string Xp;

            [DataMember(Name = "loot", IsRequired = true, Order = 13)]
            public string Loot;

            [DataMember(Name = "clear_role", IsRequired = true, Order = 14)]
            public string ClearRole;
        }

        [DataContract]
        private sealed class BodyDto
        {
            [DataMember(Name = "travel", IsRequired = true, Order = 0)]
            public string Travel;

            [DataMember(Name = "radius", IsRequired = true, Order = 1)]
            public double Radius;

            [DataMember(Name = "mass", IsRequired = true, Order = 2)]
            public double Mass;

            [DataMember(Name = "mounts", EmitDefaultValue = false, Order = 3)]
            public List<MountDto> Mounts;
        }

        [DataContract]
        private sealed class MountDto
        {
            [DataMember(Name = "id", IsRequired = true, Order = 0)]
            public string Id;

            [DataMember(Name = "position", IsRequired = true, Order = 1)]
            public VecDto Position;

            [DataMember(Name = "direction", IsRequired = true, Order = 2)]
            public VecDto Direction;
        }

        [DataContract]
        private sealed class VecDto
        {
            [DataMember(Name = "x", IsRequired = true, Order = 0)]
            public double X;

            [DataMember(Name = "y", IsRequired = true, Order = 1)]
            public double Y;
        }

        [DataContract]
        private sealed class StatsDto
        {
            [DataMember(Name = "health", IsRequired = true, Order = 0)]
            public double Health;
        }

        [DataContract]
        private sealed class SenseDto
        {
            [DataMember(Name = "range", IsRequired = true, Order = 0)]
            public double Range;

            [DataMember(Name = "arc", IsRequired = true, Order = 1)]
            public double Arc;
        }

        [DataContract]
        private sealed class AttackDto
        {
            [DataMember(Name = "kind", IsRequired = true, Order = 0)]
            public string Kind;

            [DataMember(Name = "id", IsRequired = true, Order = 1)]
            public string Id;

            [DataMember(Name = "gun", EmitDefaultValue = false, Order = 2)]
            public string Gun;

            [DataMember(Name = "plan", EmitDefaultValue = false, Order = 3)]
            public PlanDto Plan;

            [DataMember(Name = "melee", EmitDefaultValue = false, Order = 4)]
            public MeleeDto Melee;

            [DataMember(Name = "charge", EmitDefaultValue = false, Order = 5)]
            public ChargeDto Charge;

            [DataMember(Name = "explode", EmitDefaultValue = false, Order = 6)]
            public ExplodeDto Explode;
        }

        [DataContract]
        private sealed class PlanDto
        {
            [DataMember(Name = "mounts", IsRequired = true, Order = 0)]
            public List<string> Mounts;

            [DataMember(Name = "fire_mode", IsRequired = true, Order = 1)]
            public string FireMode;

            [DataMember(Name = "order", IsRequired = true, Order = 2)]
            public string Order;

            [DataMember(Name = "shots", IsRequired = true, Order = 3)]
            public int Shots;

            [DataMember(Name = "interval", IsRequired = true, Order = 4)]
            public double Interval;
        }

        [DataContract]
        private sealed class MeleeDto
        {
            [DataMember(Name = "range", IsRequired = true, Order = 0)]
            public double Range;

            [DataMember(Name = "wind_up", IsRequired = true, Order = 1)]
            public double WindUp;

            [DataMember(Name = "active", IsRequired = true, Order = 2)]
            public double Active;

            [DataMember(Name = "recovery", IsRequired = true, Order = 3)]
            public double Recovery;

            [DataMember(Name = "effects", IsRequired = true, Order = 4)]
            public List<EffectDto> Effects;
        }

        [DataContract]
        private sealed class ChargeDto
        {
            [DataMember(Name = "speed", IsRequired = true, Order = 0)]
            public double Speed;

            [DataMember(Name = "distance", IsRequired = true, Order = 1)]
            public double Distance;

            [DataMember(Name = "wind_up", IsRequired = true, Order = 2)]
            public double WindUp;

            [DataMember(Name = "recovery", IsRequired = true, Order = 3)]
            public double Recovery;

            [DataMember(Name = "effects", IsRequired = true, Order = 4)]
            public List<EffectDto> Effects;
        }

        [DataContract]
        private sealed class ExplodeDto
        {
            [DataMember(Name = "wind_up", IsRequired = true, Order = 0)]
            public double WindUp;

            [DataMember(Name = "effects", IsRequired = true, Order = 1)]
            public List<EffectDto> Effects;
        }

        [DataContract]
        private sealed class EffectDto
        {
            [DataMember(Name = "kind", IsRequired = true, Order = 0)]
            public string Kind;

            [DataMember(Name = "id", IsRequired = true, Order = 1)]
            public string Id;
        }

        [DataContract]
        private sealed class VariantDto
        {
            [DataMember(Name = "id", IsRequired = true, Order = 0)]
            public string Id;

            [DataMember(Name = "mods", EmitDefaultValue = false, Order = 1)]
            public List<string> Mods;
        }

        [DataContract]
        private sealed class PerksDto
        {
            [DataMember(Name = "fixed", EmitDefaultValue = false, Order = 0)]
            public List<string> Fixed;

            [DataMember(Name = "pool", EmitDefaultValue = false, Order = 1)]
            public List<string> Pool;

            [DataMember(Name = "rolls", IsRequired = true, Order = 2)]
            public int Rolls;
        }

        [DataContract]
        private sealed class PhaseDto
        {
            [DataMember(Name = "id", IsRequired = true, Order = 0)]
            public string Id;

            [DataMember(Name = "health", IsRequired = true, Order = 1)]
            public double Health;

            [DataMember(Name = "mods", EmitDefaultValue = false, Order = 2)]
            public List<string> Mods;
        }
    }
}
