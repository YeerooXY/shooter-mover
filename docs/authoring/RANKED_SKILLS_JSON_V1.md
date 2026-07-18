# Ranked Skills JSON V1

## Purpose

`ranked_skills_v01.json` is the editable draft content catalog consumed by the SKILL-FOUNDATION-003 ranked-skill runtime. It owns definitions and balance inputs only. XP, allocation, respec payment, save composition, UI, strongbox generation, and weapon generation remain separate authorities.

The canonical import entry point is:

```text
RankedSkillJsonCanonicalV2.Import(json)
```

The lower-level `RankedSkillJsonImporterV1` remains a compatibility parser for the original authoring representation.

## Versions

- Schema: `ranked-skills-schema-v1`
- Initial content: `ranked-skills-content-v0.1-draft`
- Status: `draft`

Changing gameplay-relevant normalized content changes the imported fingerprint. Whitespace and property formatting do not participate in the normalized fingerprint.

## Stable IDs

IDs are lowercase dotted identifiers. Display names and descriptions may change without renaming persisted gameplay identities.

Current classes:

- `class.striker`
- `class.combat_medic`
- `class.juggernaut`

## Curves

The initial importer expands compact curves deterministically:

```json
{ "start": 0.01, "step": 0.01, "count": 15 }
```

This produces one explicit runtime value per rank: `0.01` through `0.15`. `count` must equal the applicable maximum rank. Class overrides may supply a different maximum rank and curve.

Per-rank effects use `"valueSource": "rankValue"`; the runtime descriptor receives a coefficient of one and the existing projector multiplies it by the resolved rank value.

## Farming semantics

Farming modifiers are relative contributions. They must never be interpreted as percentage-point additions.

`synergy.farming.legendary_prospecting` means that eligible weapon definitions already authored as legendary receive up to `+20%` relative selection weight. It does not:

- change a definition's authored rarity;
- change weapon statistics;
- bypass level eligibility;
- bypass live/preview filtering;
- guarantee a legendary;
- add twenty percentage points.

The synergy requires rank 4 in Credit Gain, Strongbox Finder, and Strongbox Quality, plus 16 combined ranks across those three skills.

## Combined-rank gates

Combined investment gates are first-class engine-independent domain requirements through `SkillCombinedRankRequirementV2`.

`SkillSynergyDefinitionV2` now stores both:

- individual minimum-rank requirements;
- combined-rank requirements over explicitly listed skills.

Its `IsSatisfied` method evaluates both sets. `RankedSkillCatalogV2` rejects missing or impossible combined requirements, includes them in canonical fingerprints, and the standard `SkillEffectProjectorV2` applies a synergy only when all requirements pass.

The JSON importer converts authoring entries such as:

```json
{
  "requirements": [
    { "skillId": "generic.farming.credit_gain", "minimumRank": 4 },
    { "skillId": "generic.farming.strongbox_finder", "minimumRank": 4 },
    { "skillId": "generic.farming.strongbox_quality", "minimumRank": 4 }
  ],
  "combinedRankRequirements": [
    {
      "skillIds": [
        "generic.farming.credit_gain",
        "generic.farming.strongbox_finder",
        "generic.farming.strongbox_quality"
      ],
      "minimumCombinedRanks": 16
    }
  ]
}
```

into that single canonical domain definition. No second synergy projector or skill-ID special case is required.

## Validation diagnostics

Diagnostics expose:

- JSON path;
- stable definition ID where available;
- error code;
- readable message;
- severity.

The importer validates schema version, duplicate IDs, class/category/effect references, rank counts, class overrides, prerequisites, category gates, milestones, synergy feasibility, combined-rank feasibility, and the legendary relative-weight ceiling.

## Draft summary

The initial draft contains:

- 20 skills;
- 306 raw base purchasable ranks;
- 2 synergies;
- 6 farming skills;
- generic combat class-cap fixtures;
- five Striker fixtures;
- two Combat Medic fixtures;
- three Juggernaut fixtures.

The player is still expected to earn roughly 100 total points. The catalog intentionally offers far more ranks than a single character can buy.

## Validation

Focused EditMode filter:

```text
ShooterMover.Tests.EditMode.Progression.Skills.Content
```

Do not claim Unity proof without a generated XML report showing zero failures.
