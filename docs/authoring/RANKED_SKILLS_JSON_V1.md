# Ranked Skills JSON V1

## Purpose

`ranked_skills_v01.json` is the editable draft content catalog consumed by the SKILL-FOUNDATION-003 ranked-skill runtime. It owns definitions and balance inputs only. XP, allocation, respec payment, save composition, UI, strongbox generation, and weapon generation remain separate authorities.

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

SKILL-FOUNDATION-003's original `SkillSynergyDefinitionV2` supports individual skill minimums only. The content importer therefore retains combined-rank requirements in `ImportedSkillSynergyV1` and exposes `RankedSkillJsonImporterV1.ProjectEffects` as the complete projector for imported content.

Combined-gate synergies are deliberately omitted from the legacy catalog projector list so they cannot activate early. Presentation and gameplay adapters consuming this JSON catalog must use the imported synergy projection path.

A future foundation revision may move combined requirements into the engine-independent domain type; that migration must preserve IDs, semantics, and fingerprints.

## Validation diagnostics

Diagnostics expose:

- JSON path;
- stable definition ID where available;
- error code;
- readable message;
- severity.

The importer validates schema version, duplicate IDs, class/category/effect references, rank counts, class overrides, prerequisites, category gates, milestones, synergy feasibility, and the legendary relative-weight ceiling.

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
