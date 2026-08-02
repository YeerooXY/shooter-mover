"use strict";

const fs = require("fs");
const path = require("path");
const { execFileSync, spawnSync } = require("child_process");

const SOURCE_COMMIT = "d30030776909a42fed4633c49817c29b8c2eddf2";
const SOURCE_PATH = "Assets/ShooterMover/Resources/WeaponCatalog/weapon_baseline_v01.json";
const MANIFEST_PATH = "tools/migrations/pr288-weapon-manifest.json";
const REPORT_PATH = "Documentation/Weapons/PR288_CONVERSION_REPORT.md";
const GENERATED_CS_PATH = "Assets/ShooterMover/Runtime/Application/Guns/Catalog/GunCatalogue.Pr288.Generated.cs";
const CONTENT_ROOT = "Content/Weapons";
const REQUIRED_FILES = ["weapon.json", "mk1.json", "mk2.json", "mk3.json"];

const RARITY_ORDER = ["common", "rare", "epic", "legendary", "artifact"];
const RARITY_MAP = Object.freeze({
  Common: "common",
  Uncommon: "common",
  Rare: "rare",
  Epic: "epic",
  Legendary: "legendary",
  Mythic: "artifact",
  Artifact: "artifact"
});
const DAMAGE_MAP = Object.freeze({
  Kinetic: "physical",
  Energized: "energy",
  Thermal: "thermal",
  Chemical: "chemical"
});
const DIRECT_ARCHETYPES = new Set([
  "AutoRifle", "Spread", "Precision", "FastPrecision", "HeavyLMG"
]);
const CATEGORY_MAP = Object.freeze({
  AutoRifle: "normal-firearm",
  Spread: "shotgun",
  Precision: "normal-firearm",
  FastPrecision: "normal-firearm",
  HeavyLMG: "normal-firearm"
});
const FIRE_MODE_MAP = Object.freeze({
  AutoRifle: "automatic",
  HeavyLMG: "automatic",
  Spread: "semi-automatic",
  Precision: "semi-automatic",
  FastPrecision: "semi-automatic"
});
const RUNTIME_PENDING = Object.freeze({
  ClusterLauncher: "child-projectile/cluster explosion strategy",
  MineLayer: "mine placement and proximity strategy",
  Mortar: "arcing delivery plus persistent damage-zone strategy",
  ContinuousCone: "continuous cone/tick delivery strategy",
  Sprayer: "continuous sprayer delivery strategy",
  Chain: "live Unity chain-hit application",
  Beam: "live beam delivery and projection",
  Drone: "deployable autonomous drone strategy",
  AcidPoolCannon: "persistent pool plus ally-healing strategy",
  OmniPhase: "deterministic multi-damage-type cycle",
  Gravity: "charged displacement delivery strategy"
});

function fail(message) { throw new Error(message); }
function json(value) { return JSON.stringify(value, null, 2) + "\n"; }
function ensureDir(filePath) { fs.mkdirSync(path.dirname(filePath), { recursive: true }); }
function writeStable(filePath, content) {
  ensureDir(filePath);
  const previous = fs.existsSync(filePath) ? fs.readFileSync(filePath, "utf8") : null;
  if (previous !== content) fs.writeFileSync(filePath, content, "utf8");
}
function run(command, args, options = {}) {
  const result = spawnSync(command, args, { encoding: "utf8", ...options });
  if (result.status !== 0) {
    fail([`${command} ${args.join(" ")} failed`, result.stdout, result.stderr]
      .filter(Boolean).join("\n").trim());
  }
  return result.stdout;
}
function repoRoot() {
  return execFileSync("git", ["rev-parse", "--show-toplevel"], { encoding: "utf8" }).trim();
}
function readHistoricalBaseline(root) {
  const text = execFileSync("git", ["show", `${SOURCE_COMMIT}:${SOURCE_PATH}`], {
    cwd: root, encoding: "utf8", maxBuffer: 32 * 1024 * 1024
  });
  const source = JSON.parse(text);
  if (!Array.isArray(source.families) || !Array.isArray(source.definitions)) {
    fail("PR #288 baseline is missing families or definitions arrays.");
  }
  return source;
}
function definitionsByFamily(source) {
  const map = new Map();
  for (const definition of source.definitions) {
    if (!map.has(definition.FamilyId)) map.set(definition.FamilyId, []);
    map.get(definition.FamilyId).push(definition);
  }
  for (const values of map.values()) values.sort((a, b) => a.Mark - b.Mark);
  return map;
}
function buildManifest(source) {
  const byFamily = definitionsByFamily(source);
  return {
    schema: "shooter-mover.pr288-weapon-manifest/1",
    source: { commit: SOURCE_COMMIT, path: SOURCE_PATH },
    sourceFacts: {
      familyCount: source.families.length,
      definitionCount: source.definitions.length,
      rules: source.rules,
      rarityInputs: source.inputs && source.inputs.rarities ? source.inputs.rarities : {}
    },
    families: source.families.map(family => ({
      familyId: family.FamilyId,
      displayName: family.DisplayName,
      archetype: family.Archetype,
      damageType: family.DamageType,
      buildAffinity: family.BuildAffinity,
      mk1Peak: family.MK1Peak,
      markGaps: [family.GapMK1To2, family.GapMK2To3],
      maximumPlannedMark: family.MaxPlannedMark,
      markRarities: [family.MK1Rarity, family.MK2Rarity, family.MK3Rarity]
        .slice(0, family.MaxPlannedMark),
      definitionWeightModifier: family.DefinitionWeightModifier,
      acquisitionClass: family.AcquisitionClass,
      primaryEffect: family.PrimaryEffect,
      notes: family.Notes,
      exactDefinitions: (byFamily.get(family.FamilyId) || []).map(definition => ({ ...definition }))
    }))
  };
}
function normalizedFamilyRarity(family) {
  const values = family.markRarities.map(value => RARITY_MAP[value]).filter(Boolean);
  if (values.length !== family.markRarities.length) return null;
  return values.sort((a, b) => RARITY_ORDER.indexOf(b) - RARITY_ORDER.indexOf(a))[0];
}
function classify(family) {
  const definitions = family.exactDefinitions;
  if (family.maximumPlannedMark !== 3 || definitions.length !== 3 || definitions.some((d, i) => d.Mark !== i + 1)) {
    return { status: "SCHEMA_BLOCKED", behavior: "one-to-three Mark family support", approximation: "none" };
  }
  if (!DAMAGE_MAP[family.damageType]) {
    return { status: "MANUAL_DESIGN_REVIEW", behavior: `intentional ${family.damageType} damage category`, approximation: "none" };
  }
  if (family.familyId === "cryo_cannon") {
    return { status: "MANUAL_DESIGN_REVIEW", behavior: "authored slowing effect values", approximation: "none" };
  }
  if (definitions.some(d => String(d.TopBoxOnly).toLowerCase() === "yes")) {
    return { status: "MANUAL_DESIGN_REVIEW", behavior: "explicit top Strongbox tier policy", approximation: "none" };
  }
  if (family.archetype === "BurstRifle") {
    return { status: "MANUAL_DESIGN_REVIEW", behavior: "authored intra-burst interval", approximation: "none" };
  }
  if (["Launcher", "FastLauncher", "Orb"].includes(family.archetype)) {
    return { status: "SCHEMA_BLOCKED", behavior: "separate direct and area-damage values", approximation: "none" };
  }
  if (family.familyId === "homing_missile") {
    return { status: "MANUAL_DESIGN_REVIEW", behavior: "authored homing acquisition/turn/reacquisition values", approximation: "none" };
  }
  if (RUNTIME_PENDING[family.archetype]) {
    return { status: "RUNTIME_BEHAVIOR_PENDING", behavior: RUNTIME_PENDING[family.archetype], approximation: "none" };
  }
  if (!DIRECT_ARCHETYPES.has(family.archetype)) {
    return { status: "MANUAL_DESIGN_REVIEW", behavior: `explicit ${family.archetype} mapping`, approximation: "none" };
  }
  const rarity = normalizedFamilyRarity(family);
  if (!rarity) {
    return { status: "MANUAL_DESIGN_REVIEW", behavior: "current rarity mapping", approximation: "none" };
  }
  return {
    status: "READY_AFTER_SMALL_MAPPING",
    behavior: "existing travelling projectile execution",
    approximation: `per-Mark rarity normalized to family ${rarity}; bullet collision radius uses documented 0.1 current-project convention`
  };
}
function csharpNumber(value) {
  if (!Number.isFinite(Number(value))) fail(`Non-finite number: ${value}`);
  const number = Number(value);
  return Number.isInteger(number) ? `${number}d` : `${number.toString()}d`;
}
function csharpString(value) {
  return `"${String(value).replace(/\\/g, "\\\\").replace(/"/g, "\\\"")}"`;
}
function categoryEnum(damageType) {
  return {
    physical: "GunDamageCategory.Physical",
    energy: "GunDamageCategory.Energy",
    thermal: "GunDamageCategory.Thermal",
    chemical: "GunDamageCategory.Chemical"
  }[damageType];
}
function fireExpression(archetype, rate) {
  const mode = FIRE_MODE_MAP[archetype];
  if (mode === "automatic") return `FireSettings.Automatic(${csharpNumber(rate)})`;
  if (mode === "semi-automatic") return `FireSettings.SemiAutomatic(${csharpNumber(rate)})`;
  fail(`No exact fire mapping for ${archetype}`);
}
function generatedWeaponFolder(root, family, classification) {
  const category = CATEGORY_MAP[family.archetype];
  const rarity = normalizedFamilyRarity(family);
  const damageType = DAMAGE_MAP[family.damageType];
  const folder = path.join(root, CONTENT_ROOT, category, family.familyId);
  const presentation = family.archetype === "Spread" ? `shotgun-${damageType}` : `normal-${damageType}`;
  const shared = {
    name: family.displayName,
    description: `${family.primaryEffect}. ${family.notes}`.trim(),
    category,
    rarity,
    projectileType: "bullet",
    damageType,
    art: {
      delivery: `gun-delivery-art.${presentation}.v1`,
      trail: `gun-trail-art.${presentation}.v1`,
      impact: `gun-impact-art.${presentation}.v1`
    }
  };
  writeStable(path.join(folder, "weapon.json"), json(shared));
  family.exactDefinitions.forEach((definition, index) => {
    const mark = index + 1;
    const markSource = {
      peakLevel: definition.PeakDropLevel,
      damage: definition.DamagePerProjectile,
      fire: { mode: FIRE_MODE_MAP[family.archetype], rate: definition.FireRate },
      shot: { projectiles: definition.ProjectilesPerTrigger, spread: definition.SpreadDegrees },
      projectile: { speed: definition.ProjectileSpeed, radius: 0.1, range: definition.Range },
      impact: { pierce: definition.Pierce, ricochet: 0, knockback: definition.Knockback },
      art: {
        side: `gun-art.${family.familyId}.mk${mark}.side-v1`,
        mounted: `gun-art.${family.familyId}.mk${mark}.mounted-top-v1`
      }
    };
    writeStable(path.join(folder, `mk${mark}.json`), json(markSource));
  });
  return path.relative(root, folder).replace(/\\/g, "/");
}
function generateCSharp(root, generatedFamilies) {
  const lines = [];
  lines.push("// <auto-generated />");
  lines.push(`// Source: ${SOURCE_COMMIT}:${SOURCE_PATH}`);
  lines.push("using System;");
  lines.push("using ShooterMover.Domain.Common;");
  lines.push("using ShooterMover.Domain.Guns;");
  lines.push("using ShooterMover.Domain.Guns.Execution;");
  lines.push("");
  lines.push("namespace ShooterMover.Application.Guns.Catalog");
  lines.push("{");
  lines.push("    public static partial class GunCatalogue");
  lines.push("    {");
  lines.push("        private static GunFamily[] BuildPr288Families()");
  lines.push("        {");
  lines.push("            return new[]");
  lines.push("            {");
  for (const family of generatedFamilies) {
    const rarity = normalizedFamilyRarity(family);
    const category = CATEGORY_MAP[family.archetype];
    const damage = DAMAGE_MAP[family.damageType];
    const presentation = family.archetype === "Spread" ? `shotgun-${damage}` : `normal-${damage}`;
    lines.push("                BuildPr288Family(");
    lines.push(`                    ${csharpString(family.familyId)},`);
    lines.push(`                    ${csharpString(family.displayName)},`);
    lines.push(`                    ${csharpString(category)},`);
    lines.push(`                    ${csharpString(rarity)},`);
    lines.push(`                    ${csharpString(presentation)},`);
    lines.push("                    new[]");
    lines.push("                    {");
    family.exactDefinitions.forEach((d, index) => {
      lines.push("                        new Pr288MarkSource(");
      lines.push(`                            ${index + 1}, ${d.PeakDropLevel},`);
      lines.push(`                            ${csharpNumber(d.FinalBaseWeight)},`);
      lines.push(`                            ${fireExpression(family.archetype, d.FireRate)},`);
      lines.push(`                            GunShotPattern.Canonical(${d.ProjectilesPerTrigger}, ${csharpNumber(d.SpreadDegrees)}),`);
      lines.push(`                            ${csharpNumber(d.DamagePerProjectile)},`);
      lines.push(`                            ${categoryEnum(damage)},`);
      lines.push(`                            ${d.Pierce}, ${csharpNumber(d.Knockback)},`);
      lines.push(`                            ${csharpNumber(d.Range)}, ${csharpNumber(d.ProjectileSpeed)}, 0.1d)` + (index === 2 ? "" : ","));
    });
    lines.push("                    }),");
  }
  lines.push("            };");
  lines.push("        }");
  lines.push("");
  lines.push("        private sealed class Pr288MarkSource");
  lines.push("        {");
  lines.push("            public Pr288MarkSource(int mark, int peak, double weight, FireSettings fire, GunShotPattern shot, double damage, GunDamageCategory category, int pierce, double knockback, double range, double speed, double radius)");
  lines.push("            {");
  lines.push("                Mark = mark; Peak = peak; Weight = weight; Fire = fire; Shot = shot; Damage = damage; Category = category; Pierce = pierce; Knockback = knockback; Range = range; Speed = speed; Radius = radius;");
  lines.push("            }");
  lines.push("            public int Mark { get; } public int Peak { get; } public double Weight { get; }");
  lines.push("            public FireSettings Fire { get; } public GunShotPattern Shot { get; }");
  lines.push("            public double Damage { get; } public GunDamageCategory Category { get; }");
  lines.push("            public int Pierce { get; } public double Knockback { get; } public double Range { get; }");
  lines.push("            public double Speed { get; } public double Radius { get; }");
  lines.push("        }");
  lines.push("");
  lines.push("        private static GunFamily BuildPr288Family(string familyId, string displayName, string category, string rarity, string presentationKey, Pr288MarkSource[] sources)");
  lines.push("        {");
  lines.push("            StableId rarityId = StableId.Create(\"gun-rarity\", rarity);");
  lines.push("            var marks = new GunMark[sources.Length];");
  lines.push("            for (int index = 0; index < sources.Length; index++)");
  lines.push("            {");
  lines.push("                Pr288MarkSource source = sources[index];");
  lines.push("                string definitionId = familyId + \".mk\" + source.Mark;");
  lines.push("                string equipmentValue = \"gun-\" + StableFamilyToken(familyId) + \"-mk\" + source.Mark;");
  lines.push("                ProvisionalCombatProfile combat = new ProvisionalCombatProfile(");
  lines.push("                    source.Fire,");
  lines.push("                    source.Shot,");
  lines.push("                    new GunBaseStats(");
  lines.push("                        source.Damage, source.Category, null,");
  lines.push("                        PierceValue.FromLegacyInteger(source.Pierce),");
  lines.push("                        new RicochetValue(0), 0d,");
  lines.push("                        GunAttackDistance.Limited(source.Range), source.Knockback),");
  lines.push("                    ShotPattern.Create(");
  lines.push("                        GunDeliveryType.Normal,");
  lines.push("                        new GunNormalDeliverySettings(source.Speed, source.Radius),");
  lines.push("                        null, null, null, null,");
  lines.push("                        GunGuidanceSpec.Unguided(),");
  lines.push("                        StandardTravellingImpact(), GunEffects.None()),");
  lines.push("                    presentationKey);");
  lines.push("                Gun blueprint = Gun.CreateAuthored(");
  lines.push("                    new GunIdentity(new GunDefinitionId(definitionId), displayName + \" MK\" + source.Mark, familyId),");
  lines.push("                    combat.FireSettings, combat.ShotPattern, combat.BaseStats, combat.Delivery,");
  lines.push("                    new GunPresentation(");
  lines.push("                        \"gun-art.\" + familyId + \".mk\" + source.Mark + \".side-v1\",");
  lines.push("                        \"gun-art.\" + familyId + \".mk\" + source.Mark + \".mounted-top-v1\",");
  lines.push("                        \"gun-delivery-art.\" + presentationKey + \".v1\",");
  lines.push("                        \"gun-trail-art.\" + presentationKey + \".v1\",");
  lines.push("                        \"gun-impact-art.\" + presentationKey + \".v1\", null),");
  lines.push("                    new GunDropMetadata(");
  lines.push("                        StableId.Create(\"equipment\", equipmentValue), rarityId,");
  lines.push("                        GunDropAvailability.Live, source.Peak, source.Weight,");
  lines.push("                        GunStrongboxEligibility.FromMinimumTier(1)));");
  lines.push("                marks[index] = new GunMark(source.Mark, source.Peak, Math.Min(source.Peak, 100), true, blueprint);");
  lines.push("            }");
  lines.push("            return new GunFamily(familyId, displayName, StableId.Create(\"gun-category\", category), rarityId, rarity, marks);");
  lines.push("        }");
  lines.push("    }");
  lines.push("}");
  writeStable(path.join(root, GENERATED_CS_PATH), lines.join("\n") + "\n");
}
function report(root, manifest, rows) {
  const counts = Object.fromEntries([
    "READY_AND_GENERATED", "READY_AFTER_SMALL_MAPPING", "RUNTIME_BEHAVIOR_PENDING", "SCHEMA_BLOCKED", "MANUAL_DESIGN_REVIEW"
  ].map(status => [status, rows.filter(row => row.status === status).length]));
  const generated = rows.filter(row => row.generatedPath);
  const generatedDefinitions = generated.reduce((sum, row) => sum + row.markCount, 0);
  const lines = [
    "# PR #288 Weapon Conversion Report", "",
    `Historical source: \`${SOURCE_COMMIT}:${SOURCE_PATH}\``, "",
    `- Source families: **${manifest.sourceFacts.familyCount}**`,
    `- Source definitions: **${manifest.sourceFacts.definitionCount}**`,
    `- Generated families: **${generated.length}**`,
    `- Generated definitions: **${generatedDefinitions}**`,
    `- Runtime-behavior-pending families: **${counts.RUNTIME_BEHAVIOR_PENDING}**`,
    `- Schema-blocked families: **${counts.SCHEMA_BLOCKED}**`,
    `- Manual-review families: **${counts.MANUAL_DESIGN_REVIEW}**`, "",
    "## Explicit mappings", "",
    "- Damage: `Kinetic -> physical`, `Energized -> energy`, `Thermal -> thermal`, `Chemical -> chemical`.",
    "- `Photonic` and `Omni-Phase` are not normalized to Energy; they remain review/pending.",
    "- Rarity: `Common -> common`, `Uncommon -> common`, `Rare -> rare`, `Epic -> epic`, `Legendary -> legendary`, `Mythic -> artifact`, `Artifact -> artifact`.",
    "- Because current production rarity is family-owned, generated families use the highest normalized rarity among their Marks.",
    "- Historical bullet definitions do not author collision radius; generated bullet Marks use the current-project `0.1` convention and record that approximation below.",
    "- Families with fewer than three Marks are not padded or duplicated.",
    "- Launchers and explosive Orbs remain blocked because current canonical authored data has one executable damage value, while PR #288 authored separate direct and area damage.",
    "- Burst rifles remain under review because PR #288 does not author the required intra-burst interval.", "",
    "## Family results", "",
    "| Family | Source archetype | Source damage | Marks | Mapped type | Mapped damage | Status | Runtime behavior required | Approximation | Generated path | Validation | Notes |",
    "|---|---|---:|---:|---|---|---|---|---|---|---|---|"
  ];
  for (const row of rows) {
    lines.push(`| ${row.displayName} | ${row.archetype} | ${row.damageType} | ${row.markCount} | ${row.mappedType} | ${row.mappedDamage} | ${row.status} | ${row.behavior} | ${row.approximation} | ${row.generatedPath || "—"} | ${row.validation || "not generated"} | ${row.notes || ""} |`);
  }
  lines.push("", "## Validation boundary", "",
    "The Node folder validator/compiler proves deterministic source shape only. Playability additionally requires the generated definition to reach `GunCatalogProvider.GunCatalog`, the equipment projection, Strongbox candidates, and a live Unity delivery strategy. Unity validation must be reported separately and is not implied by this document.", "");
  writeStable(path.join(root, REPORT_PATH), lines.join("\n"));
}
function validateGenerated(root, rows) {
  for (const row of rows.filter(value => value.generatedPath)) {
    const folder = path.join(root, row.generatedPath);
    run(process.execPath, [path.join(root, "tools/item-maker/validate-weapon-folder.js"), folder], { cwd: root });
    run(process.execPath, [path.join(root, "tools/item-maker/compile-weapon-folder.js"), folder], { cwd: root });
    row.validation = "validator + compiler passed";
  }
}
function verifyUnique(manifest, rows) {
  const familyIds = new Set();
  const definitionIds = new Set();
  for (const family of manifest.families) {
    if (familyIds.has(family.familyId)) fail(`Duplicate family id: ${family.familyId}`);
    familyIds.add(family.familyId);
    for (const definition of family.exactDefinitions) {
      if (definitionIds.has(definition.DefinitionId)) fail(`Duplicate definition id: ${definition.DefinitionId}`);
      definitionIds.add(definition.DefinitionId);
    }
  }
  for (const row of rows.filter(value => value.generatedPath)) {
    for (const file of REQUIRED_FILES) {
      if (!fs.existsSync(path.join(row.absolutePath, file))) fail(`Missing generated file ${row.familyId}/${file}`);
    }
  }
}

function replaceOnce(text, before, after, label) {
  if (text.includes(after)) return text;
  const first = text.indexOf(before);
  if (first < 0 || text.indexOf(before, first + before.length) >= 0) {
    fail(`Cannot apply deterministic ${label} patch`);
  }
  return text.slice(0, first) + after + text.slice(first + before.length);
}
function patchWeaponValidator(root) {
  const file = path.join(root, "tools/item-maker/validate-weapon-folder.js");
  let text = fs.readFileSync(file, "utf8");
  text = replaceOnce(text,
    '  "peakLevel", "damage", "fire", "homing", "dot", "explosion", "art"\n',
    '  "peakLevel", "damage", "fire", "shot", "projectile", "impact", "homing", "dot", "explosion", "art"\n',
    "Mark gameplay fields");
  text = replaceOnce(text,
`function validateShared(weapon) {
`,
`function validateShot(shot, label) {
  rejectUnknown(shot, new Set(["projectiles", "spread"]), label);
  if (!Number.isInteger(shot.projectiles) || shot.projectiles < 1) fail(\`\${label}.projectiles: at least one projectile is required\`);
  if (!nonNegative(shot.spread)) fail(\`\${label}.spread: non-negative spread is required\`);
}

function validateImpact(impact, label) {
  rejectUnknown(impact, new Set(["pierce", "ricochet", "knockback"]), label);
  if (!has(impact, "ricochet")) fail(\`\${label}.ricochet: explicit value is required\`);
  if (!Number.isInteger(impact.pierce) || impact.pierce < 0) fail(\`\${label}.pierce: non-negative whole number is required\`);
  if (!nonNegative(impact.ricochet)) fail(\`\${label}.ricochet: non-negative value is required\`);
  if (!nonNegative(impact.knockback)) fail(\`\${label}.knockback: non-negative value is required\`);
}

function validateProjectile(projectile, label) {
  rejectUnknown(projectile, new Set(["speed", "radius", "range"]), label);
  if (!positive(projectile.speed) || !positive(projectile.radius) || !positive(projectile.range)) {
    fail(\`\${label}: positive speed, radius, and range are required\`);
  }
}

function validateShared(weapon) {
`, "validation helpers");
  text = replaceOnce(text,
`  const shot = requireObject(weapon, "shot", "weapon.json");
  rejectUnknown(shot, new Set(["projectiles", "spread"]), "weapon.json.shot");
  if (!Number.isInteger(shot.projectiles) || shot.projectiles < 1) fail("weapon.json.shot.projectiles: at least one projectile is required");
  if (!nonNegative(shot.spread)) fail("weapon.json.shot.spread: non-negative spread is required");

  const impact = requireObject(weapon, "impact", "weapon.json");
  rejectUnknown(impact, new Set(["pierce", "ricochet", "knockback"]), "weapon.json.impact");
  if (!has(impact, "ricochet")) fail("weapon.json.impact.ricochet: explicit value is required");
  if (!Number.isInteger(impact.pierce) || impact.pierce < 0) fail("weapon.json.impact.pierce: non-negative whole number is required");
  if (!nonNegative(impact.ricochet)) fail("weapon.json.impact.ricochet: non-negative value is required");
  if (!nonNegative(impact.knockback)) fail("weapon.json.impact.knockback: non-negative value is required");

`,
`  if (has(weapon, "shot")) validateShot(requireObject(weapon, "shot", "weapon.json"), "weapon.json.shot");
  if (has(weapon, "impact")) validateImpact(requireObject(weapon, "impact", "weapon.json"), "weapon.json.impact");

`, "shared shot/impact ownership");
  text = replaceOnce(text,
`    const projectile = requireObject(weapon, "projectile", "weapon.json");
    rejectUnknown(projectile, new Set(["speed", "radius", "range"]), "weapon.json.projectile");
    if (!positive(projectile.speed) || !positive(projectile.radius) || !positive(projectile.range)) {
      fail("weapon.json.projectile: positive speed, radius, and range are required");
    }
`,
`    if (has(weapon, "projectile")) validateProjectile(requireObject(weapon, "projectile", "weapon.json"), "weapon.json.projectile");
`, "shared projectile ownership");
  text = replaceOnce(text,
`    if (has(mark, "fire")) validateFire(requireObject(mark, "fire", label), \`\${label}.fire\`);
`,
`    if (has(mark, "fire")) validateFire(requireObject(mark, "fire", label), \`\${label}.fire\`);
    if (has(mark, "shot")) validateShot(requireObject(mark, "shot", label), \`\${label}.shot\`);
    if (has(mark, "projectile")) validateProjectile(requireObject(mark, "projectile", label), \`\${label}.projectile\`);
    if (has(mark, "impact")) validateImpact(requireObject(mark, "impact", label), \`\${label}.impact\`);
`, "Mark gameplay validation");
  text = replaceOnce(text,
`  validateBlockOwnership(weapon, marks, "fire", true);
  validateBlockOwnership(weapon, marks, "homing", false);
`,
`  validateBlockOwnership(weapon, marks, "fire", true);
  validateBlockOwnership(weapon, marks, "shot", true);
  validateBlockOwnership(weapon, marks, "impact", true);
  validateBlockOwnership(weapon, marks, "projectile", weapon.projectileType !== "beam");
  validateBlockOwnership(weapon, marks, "homing", false);
`, "Mark ownership rules");
  writeStable(file, text);
}
function patchGunCatalogueBridge(root) {
  const file = path.join(root, "Assets/ShooterMover/Runtime/Application/Guns/Catalog/GunCatalogue.Content.cs");
  let text = fs.readFileSync(file, "utf8");
  text = replaceOnce(text, "            var families = new[]\n", "            var families = new List<GunFamily>\n", "catalogue family list");
  text = replaceOnce(text,
`            };

            return new GunCatalogueView(
                families,
`,
`            };
            families.AddRange(BuildPr288Families());

            return new GunCatalogueView(
                families,
`, "catalogue generated-family bridge");
  writeStable(file, text);
}

function main() {
  const root = repoRoot();
  patchWeaponValidator(root);
  patchGunCatalogueBridge(root);
  const source = readHistoricalBaseline(root);
  const manifest = buildManifest(source);
  if (manifest.sourceFacts.familyCount !== 44 || manifest.sourceFacts.definitionCount !== 121) {
    fail(`Unexpected PR #288 totals: ${manifest.sourceFacts.familyCount} families / ${manifest.sourceFacts.definitionCount} definitions`);
  }
  writeStable(path.join(root, MANIFEST_PATH), json(manifest));

  const rows = [];
  const generatedFamilies = [];
  for (const family of manifest.families) {
    const classification = classify(family);
    const mappedType = CATEGORY_MAP[family.archetype] || "—";
    const mappedDamage = DAMAGE_MAP[family.damageType] || "—";
    const row = {
      familyId: family.familyId,
      displayName: family.displayName,
      archetype: family.archetype,
      damageType: family.damageType,
      markCount: family.exactDefinitions.length,
      mappedType,
      mappedDamage,
      status: classification.status,
      behavior: classification.behavior,
      approximation: classification.approximation,
      notes: family.notes
    };
    if (classification.status === "READY_AFTER_SMALL_MAPPING" || classification.status === "READY_AND_GENERATED") {
      row.generatedPath = generatedWeaponFolder(root, family, classification);
      row.absolutePath = path.join(root, row.generatedPath);
      generatedFamilies.push(family);
    }
    rows.push(row);
  }
  verifyUnique(manifest, rows);
  validateGenerated(root, rows);
  generateCSharp(root, generatedFamilies);
  report(root, manifest, rows);

  process.stdout.write(json({
    sourceFamilies: manifest.sourceFacts.familyCount,
    sourceDefinitions: manifest.sourceFacts.definitionCount,
    generatedFamilies: generatedFamilies.length,
    generatedDefinitions: generatedFamilies.reduce((sum, family) => sum + family.exactDefinitions.length, 0),
    statuses: rows.reduce((result, row) => {
      result[row.status] = (result[row.status] || 0) + 1;
      return result;
    }, {})
  }));
}

if (require.main === module) {
  try { main(); }
  catch (error) { console.error(error.stack || error.message); process.exitCode = 1; }
}

module.exports = { buildManifest, classify, normalizedFamilyRarity };
