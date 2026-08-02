"use strict";

const fs = require("fs");
const path = require("path");
const { execFileSync, spawnSync } = require("child_process");

const FAMILIES = [
  { id: "hv_finch", name: "HV Finch", maker: "Helix Vanguard", rarity: "common", profile: "rifle", peaks: [1, 4, 8] },
  { id: "hv_buckler", name: "HV Buckler", maker: "Helix Vanguard", rarity: "rare", profile: "shotgun", peaks: [2, 6, 10] },
  { id: "teknova_flicker", name: "Teknova Flicker", maker: "Teknova", rarity: "common", profile: "rifle", peaks: [1, 5, 9] },
  { id: "teknova_vector", name: "Teknova Vector", maker: "Teknova", rarity: "epic", profile: "rifle", peaks: [3, 7, 10] },
  { id: "ronsen_ember", name: "Ronsen Ember", maker: "Ronsen Dynamics", rarity: "common", profile: "rifle", peaks: [2, 5, 8] },
  { id: "ronsen_ashmaker", name: "Ronsen Ashmaker", maker: "Ronsen Dynamics", rarity: "rare", profile: "shotgun", peaks: [3, 6, 9] },
  { id: "virex_thorn", name: "Virex Thorn", maker: "Virex", rarity: "rare", profile: "rifle", peaks: [1, 4, 7] },
  { id: "virex_crown", name: "Virex Crown", maker: "Virex", rarity: "epic", profile: "shotgun", peaks: [2, 7, 10] },
  { id: "hv_paragon", name: "HV Paragon", maker: "Helix Vanguard", rarity: "legendary", profile: "rifle", peaks: [2, 6, 10] },
  { id: "ronsen_warden", name: "Ronsen Warden", maker: "Ronsen Dynamics", rarity: "legendary", profile: "shotgun", peaks: [4, 8, 10] },
  { id: "teknova_singularity", name: "Teknova Singularity", maker: "Teknova", rarity: "artifact", profile: "rifle", peaks: [3, 7, 10] }
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
  const anchor = "            families.AddRange(BuildStrongboxTestFamilies());\n";
  const addition = anchor + "            families.AddRange(BuildLowLevelStrongboxTestFamilies());\n";
  if (!text.includes(addition)) {
    if (!text.includes(anchor)) fail("Strongbox test-pool bridge anchor is missing.");
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
      description: `Low-level Strongbox distribution test pool. Creator: ${family.maker}. Reuses the shared ${profile.profileName} profile; balance and final art are provisional.`,
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
    "    /// Synthetic early-game depth used to exercise Strongbox rarity at levels one through ten.",
    "    /// All families remain three-Mark because the current canonical and flat catalogues require it.",
    "    /// </summary>",
    "    public static partial class GunCatalogue",
    "    {",
    "        private static GunFamily[] BuildLowLevelStrongboxTestFamilies()",
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
    "Assets/ShooterMover/Runtime/Application/Guns/Catalog/GunCatalogue.LowLevelStrongboxTestPool.cs"),
    lines.join("\n") + "\n");
}

function writeTest(repo) {
  const ids = FAMILIES.map(family => `            "${family.id}",`).join("\n");
  const content = `using NUnit.Framework;
using ShooterMover.Application.Guns.Catalog;
using ShooterMover.Domain.Guns;

namespace ShooterMover.Tests.EditMode.Guns.Catalog
{
    public sealed class LowLevelStrongboxTestPoolTests
    {
        private static readonly string[] ExpectedFamilyIds =
        {
${ids}
        };

        [Test]
        public void LowLevelPoolKeepsEveryMarkWithinLevelsOneThroughTen()
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
                        Assert.Fail("Unexpected low-level pool rarity: "
                            + family.CatalogRarity);
                        break;
                }

                for (int markIndex = 0;
                     markIndex < family.Marks.Count;
                     markIndex++)
                {
                    GunMark mark = family.Marks[markIndex];
                    Assert.That(mark.DropAnchorLevel, Is.InRange(1, 10));
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
            Assert.That(epic, Is.EqualTo(2));
            Assert.That(legendary, Is.EqualTo(2));
            Assert.That(artifact, Is.EqualTo(1));
            Assert.That(marks, Is.EqualTo(33));
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

            Assert.Fail("Missing low-level Strongbox test family: " + familyId);
            return null;
        }
    }
}
`;
  writeStable(path.join(repo,
    "Assets/ShooterMover/Tests/EditMode/Guns/Catalog/LowLevelStrongboxTestPoolTests.cs"),
    content);
}

function writeDocumentation(repo) {
  const rows = FAMILIES.map(family => {
    const profile = family.profile === "shotgun" ? "Sweeper shotgun" : "Rattler rifle";
    return `| ${family.maker} | ${family.name} | ${profile} | ${family.rarity} | ${family.peaks.join(" / ")} |`;
  }).join("\n");
  const content = `# Low-Level Strongbox Synthetic Test Pool

This pool adds deterministic early-game catalogue depth. It is synthetic test content, not recovered PR #288 data and not approved balance.

- Families: **11**
- Marks: **33**
- Every MK1-MK3 peak: **level 1 through 10**
- Rarity mix: **3 common, 3 rare, 2 epic, 2 legendary, 1 artifact**
- Base selection weight: **1 for every Mark**
- Runtime profiles: existing Rattler automatic-rifle and Sweeper shotgun profiles only

The current production catalogue requires exactly three Marks per family. MK1-MK2-only families are intentionally not introduced here because the canonical family builder rejects non-three-Mark anchors and the flat Strongbox projection reads MK1, MK2, and MK3 directly.

| Creator | Family | Profile | Rarity | MK1 / MK2 / MK3 peaks |
|---|---|---|---|---|
${rows}

## Intended use

Run low-player-level Strongbox simulations and inspect whether rare, epic, legendary, and artifact suppression behaves correctly when all candidate families are level-compatible. Fixed anchors make repeated simulation results comparable.

Do not interpret the combat numbers, names, rarity allocation, or creator lines as final game balance.
`;
  writeStable(path.join(repo,
    "Documentation/Weapons/LOW_LEVEL_STRONGBOX_TEST_POOL.md"), content);
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
    minimumPeak: Math.min(...FAMILIES.flatMap(family => family.peaks)),
    maximumPeak: Math.max(...FAMILIES.flatMap(family => family.peaks)),
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
