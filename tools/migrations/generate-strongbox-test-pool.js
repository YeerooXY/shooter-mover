"use strict";

const fs = require("fs");
const path = require("path");
const { execFileSync, spawnSync } = require("child_process");

const FAMILIES = [
  {
    "id": "hv_kestrel",
    "name": "HV Kestrel",
    "maker": "Helix Vanguard",
    "rarity": "common",
    "profile": "rifle",
    "peaks": [
      4,
      29,
      57
    ]
  },
  {
    "id": "hv_breacher",
    "name": "HV Breacher",
    "maker": "Helix Vanguard",
    "rarity": "rare",
    "profile": "shotgun",
    "peaks": [
      18,
      47,
      73
    ]
  },
  {
    "id": "hv_vanguard",
    "name": "HV Vanguard",
    "maker": "Helix Vanguard",
    "rarity": "legendary",
    "profile": "rifle",
    "peaks": [
      52,
      79,
      104
    ]
  },
  {
    "id": "teknova_spark",
    "name": "Teknova Spark",
    "maker": "Teknova",
    "rarity": "rare",
    "profile": "rifle",
    "peaks": [
      11,
      36,
      64
    ]
  },
  {
    "id": "teknova_pulse",
    "name": "Teknova Pulse",
    "maker": "Teknova",
    "rarity": "epic",
    "profile": "shotgun",
    "peaks": [
      27,
      58,
      83
    ]
  },
  {
    "id": "teknova_sovereign",
    "name": "Teknova Sovereign",
    "maker": "Teknova",
    "rarity": "legendary",
    "profile": "rifle",
    "peaks": [
      60,
      87,
      109
    ]
  },
  {
    "id": "ronsen_cinder",
    "name": "Ronsen Cinder",
    "maker": "Ronsen Dynamics",
    "rarity": "common",
    "profile": "rifle",
    "peaks": [
      7,
      32,
      55
    ]
  },
  {
    "id": "ronsen_furnace",
    "name": "Ronsen Furnace",
    "maker": "Ronsen Dynamics",
    "rarity": "rare",
    "profile": "shotgun",
    "peaks": [
      24,
      45,
      76
    ]
  },
  {
    "id": "ronsen_sunspike",
    "name": "Ronsen Sunspike",
    "maker": "Ronsen Dynamics",
    "rarity": "epic",
    "profile": "rifle",
    "peaks": [
      41,
      69,
      96
    ]
  },
  {
    "id": "virex_needle",
    "name": "Virex Needle",
    "maker": "Virex",
    "rarity": "common",
    "profile": "rifle",
    "peaks": [
      14,
      38,
      62
    ]
  },
  {
    "id": "virex_corroder",
    "name": "Virex Corroder",
    "maker": "Virex",
    "rarity": "epic",
    "profile": "shotgun",
    "peaks": [
      35,
      65,
      93
    ]
  },
  {
    "id": "virex_apex",
    "name": "Virex Apex",
    "maker": "Virex",
    "rarity": "artifact",
    "profile": "rifle",
    "peaks": [
      72,
      94,
      110
    ]
  }
];

function fail(message) { throw new Error(message); }
function root() {
  return execFileSync("git", ["rev-parse", "--show-toplevel"], { encoding: "utf8" }).trim();
}
function writeStable(file, content) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  if (!fs.existsSync(file) || fs.readFileSync(file, "utf8") !== content) {
    fs.writeFileSync(file, content, "utf8");
  }
}
function pretty(value) { return JSON.stringify(value, null, 2) + "\n"; }
function run(command, args, cwd) {
  const result = spawnSync(command, args, { cwd, encoding: "utf8" });
  if (result.status !== 0) {
    fail([`${command} ${args.join(" ")} failed`, result.stdout, result.stderr]
      .filter(Boolean).join("\n"));
  }
}

function patchCatalogue(repo) {
  const file = path.join(repo,
    "Assets/ShooterMover/Runtime/Application/Guns/Catalog/GunCatalogue.Content.cs");
  let text = fs.readFileSync(file, "utf8");
  const anchor = "            families.AddRange(BuildPr288Families());\n";
  const addition = anchor + "            families.AddRange(BuildStrongboxTestFamilies());\n";
  if (!text.includes(addition)) {
    if (!text.includes(anchor)) fail("Gun catalogue PR #288 bridge anchor is missing.");
    text = text.replace(anchor, addition);
  }
  writeStable(file, text);
}

function profileValues(profile) {
  if (profile === "shotgun") {
    return {
      category: "shotgun",
      presentation: "shotgun-physical",
      profileName: "Sweeper shotgun",
      runtimeProfile: "Sweeper",
      fire: { mode: "automatic", rate: 2 },
      shot: { projectiles: 3, spread: 24 },
      projectile: { speed: 28, radius: 0.12, range: 16 },
      impact: { pierce: 1, ricochet: 0, knockback: 6 }
    };
  }
  return {
    category: "normal-firearm",
    presentation: "normal-physical",
    profileName: "Rattler automatic-rifle",
    runtimeProfile: "Rattler",
    fire: { mode: "automatic", rate: 4 },
    shot: { projectiles: 1, spread: 0 },
    projectile: { speed: 20, radius: 0.1, range: 25 },
    impact: { pierce: 1, ricochet: 0, knockback: 0 }
  };
}

function writeContent(repo) {
  for (const family of FAMILIES) {
    const profile = profileValues(family.profile);
    const folder = path.join(repo, "Content/Weapons", profile.category, family.id);
    writeStable(path.join(folder, "weapon.json"), pretty({
      name: family.name,
      description: `Strongbox distribution test pool. Creator: ${family.maker}. Reuses the shared ${profile.profileName} profile; balance and final art are provisional.`,
      category: profile.category,
      rarity: family.rarity,
      projectileType: "bullet",
      damageType: "physical",
      art: {
        delivery: `gun-delivery-art.${profile.presentation}.v1`,
        trail: `gun-trail-art.${profile.presentation}.v1`,
        impact: `gun-impact-art.${profile.presentation}.v1`
      }
    }));
    for (let index = 0; index < 3; index++) {
      const mark = index + 1;
      writeStable(path.join(folder, `mk${mark}.json`), pretty({
        peakLevel: family.peaks[index],
        damage: 1,
        fire: profile.fire,
        shot: profile.shot,
        projectile: profile.projectile,
        impact: profile.impact,
        art: {
          side: `gun-art.${family.id}.mk${mark}.side-v1`,
          mounted: `gun-art.${family.id}.mk${mark}.mounted-top-v1`
        }
      }));
    }
  }
}

function writeRuntime(repo) {
  const lines = [
    "using ShooterMover.Domain.Guns;",
    "",
    "namespace ShooterMover.Application.Guns.Catalog",
    "{",
    "    /// <summary>",
    "    /// Synthetic deterministic catalogue depth used to exercise Strongbox rarity and level distribution.",
    "    /// Creator identity currently lives in display names and authoring descriptions; no new schema field is implied.",
    "    /// </summary>",
    "    public static partial class GunCatalogue",
    "    {",
    "        private static GunFamily[] BuildStrongboxTestFamilies()",
    "        {",
    "            return new[]",
    "            {"
  ];
  for (const family of FAMILIES) {
    const profile = profileValues(family.profile);
    lines.push(
      "                BuildFamily(",
      `                    "${family.id}",`,
      `                    "${family.name}",`,
      `                    "${family.rarity}",`,
      `                    new[] { ${family.peaks.join(", ")} },`,
      `                    ProvisionalGunTestProfile.${profile.runtimeProfile},`,
      "                    true),"
    );
  }
  lines.push("            };", "        }", "    }", "}");
  writeStable(path.join(repo,
    "Assets/ShooterMover/Runtime/Application/Guns/Catalog/GunCatalogue.StrongboxTestPool.cs"),
    lines.join("\n") + "\n");
}

function writeTest(repo) {
  const ids = FAMILIES.map(family => `            "${family.id}",`).join("\n");
  const content = `using NUnit.Framework;
using ShooterMover.Application.Guns.Catalog;
using ShooterMover.Domain.Guns;

namespace ShooterMover.Tests.EditMode.Guns.Catalog
{
    public sealed class StrongboxTestPoolTests
    {
        private static readonly string[] ExpectedFamilyIds =
        {
${ids}
        };

        [Test]
        public void TestPoolProvidesDeterministicRarityAndLevelDepth()
        {
            GunCatalogueView catalogue = GunCatalogue.Current;
            int common = 0;
            int rare = 0;
            int epic = 0;
            int legendary = 0;
            int artifact = 0;
            int marks = 0;

            for (int familyIndex = 0;
                 familyIndex < ExpectedFamilyIds.Length;
                 familyIndex++)
            {
                GunFamily family = FindFamily(
                    catalogue,
                    ExpectedFamilyIds[familyIndex]);
                Assert.That(family.Marks.Count, Is.EqualTo(3));

                switch (family.CatalogRarity)
                {
                    case "common": common += 1; break;
                    case "rare": rare += 1; break;
                    case "epic": epic += 1; break;
                    case "legendary": legendary += 1; break;
                    case "artifact": artifact += 1; break;
                    default:
                        Assert.Fail("Unexpected test-pool rarity: "
                            + family.CatalogRarity);
                        break;
                }

                for (int markIndex = 0;
                     markIndex < family.Marks.Count;
                     markIndex++)
                {
                    GunMark mark = family.Marks[markIndex];
                    Assert.That(
                        mark.Blueprint.DropMetadata.BaseSelectionWeight,
                        Is.EqualTo(1d));
                    if (markIndex > 0)
                    {
                        Assert.That(
                            mark.DropAnchorLevel,
                            Is.GreaterThan(
                                family.Marks[markIndex - 1]
                                    .DropAnchorLevel));
                    }
                    marks += 1;
                }
            }

            Assert.That(common, Is.EqualTo(3));
            Assert.That(rare, Is.EqualTo(3));
            Assert.That(epic, Is.EqualTo(3));
            Assert.That(legendary, Is.EqualTo(2));
            Assert.That(artifact, Is.EqualTo(1));
            Assert.That(marks, Is.EqualTo(36));
        }

        private static GunFamily FindFamily(
            GunCatalogueView catalogue,
            string familyId)
        {
            for (int index = 0;
                 index < catalogue.Families.Count;
                 index++)
            {
                GunFamily family = catalogue.Families[index];
                if (family.FamilyId == familyId)
                {
                    return family;
                }
            }

            Assert.Fail("Missing Strongbox test family: " + familyId);
            return null;
        }
    }
}
`;
  writeStable(path.join(repo,
    "Assets/ShooterMover/Tests/EditMode/Guns/Catalog/StrongboxTestPoolTests.cs"),
    content);
}

function writeDocumentation(repo) {
  const rows = FAMILIES.map(family => {
    const profile = family.profile === "shotgun" ? "Sweeper shotgun" : "Rattler rifle";
    return `| ${family.maker} | ${family.name} | ${profile} | ${family.rarity} | ${family.peaks.join(" / ")} |`;
  }).join("\n");
  const content = `# Strongbox Synthetic Test Pool

This pool adds deterministic catalogue depth for Strongbox distribution testing. It is synthetic test content, not recovered PR #288 data and not approved balance.

- Families: **12**
- Marks: **36**
- Rarity mix: **3 common, 3 rare, 3 epic, 2 legendary, 1 artifact**
- Base selection weight: **1 for every Mark**
- Level anchors: fixed pseudo-random values so repeated simulator runs remain comparable
- Runtime profiles: existing Rattler automatic-rifle and Sweeper shotgun profiles only

Creator identity is currently represented in the display name and description. This intentionally does not add a permanent \`creator\` or \`manufacturer\` schema field yet.

| Creator | Family | Profile | Rarity | MK1 / MK2 / MK3 peaks |
|---|---|---|---|---|
${rows}

## Intended use

Use this pool to inspect how current level affinity and family rarity affect Strongbox output. The repeated profiles deliberately model the future situation where multiple creators sell mechanically related weapon lines with different names, rarity positions, level bands, and eventually distinct art.

Do not interpret the current combat numbers, names, or rarity allocation as final game balance.
`;
  writeStable(path.join(repo,
    "Documentation/Weapons/STRONGBOX_TEST_POOL.md"), content);
}

function validate(repo) {
  for (const family of FAMILIES) {
    const profile = profileValues(family.profile);
    const folder = path.join(repo, "Content/Weapons", profile.category, family.id);
    run(process.execPath,
      [path.join(repo, "tools/item-maker/validate-weapon-folder.js"), folder],
      repo);
    run(process.execPath,
      [path.join(repo, "tools/item-maker/compile-weapon-folder.js"), folder],
      repo);
  }
}

function main() {
  const repo = root();
  patchCatalogue(repo);
  writeContent(repo);
  writeRuntime(repo);
  writeTest(repo);
  writeDocumentation(repo);
  validate(repo);
  process.stdout.write(pretty({
    families: FAMILIES.length,
    marks: FAMILIES.length * 3,
    rarityMix: FAMILIES.reduce((counts, family) => {
      counts[family.rarity] = (counts[family.rarity] || 0) + 1;
      return counts;
    }, {})
  }));
}

try { main(); }
catch (error) {
  console.error(error.stack || error.message);
  process.exitCode = 1;
}
