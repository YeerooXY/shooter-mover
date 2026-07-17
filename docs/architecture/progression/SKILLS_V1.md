# Skills V1

SKILL-001 introduced the engine-independent skill progression authority. SKILL-002 removes the fixed four-branch/five-skill catalog assumption while preserving the V1 allocation and point-authority boundary.

## Catalog contract

- A catalog contains one or more explicitly identified skill trees.
- Every tree has a stable tree ID and any positive number of skills.
- Every skill has a globally unique stable skill ID plus explicit tree and category IDs.
- The default fixture is one 15-skill tree: five Offense, five Defense and five Utility skills.
- Specialized fixtures may contain five skills.
- Mixed catalogs may combine trees of different sizes in one authority.
- `CreateCompatibilityTwentySkillCatalog()` preserves the original 20 skill IDs (`offense.1` through `utility.5`) as a compatibility fixture.
- The original `SkillDefinitionV1` constructor and its single-prerequisite projection remain available for existing callers; new definitions should use the explicit tree/category constructor.

Catalog construction rejects empty trees, duplicate tree or skill IDs, unknown prerequisite skills, duplicate requirements, unknown tree/category gates and prerequisite cycles. Definitions and requirement lists retain author order so validation and allocation rejection are deterministic.

## Allocation requirements

A skill may declare:

- zero or more skill-rank prerequisites;
- zero or more category-investment requirements, each identified by an explicit tree ID, category ID and required invested-point count.

Category investment is the sum of allocated ranks in that exact tree/category pair. For example, an eight-point Offense gate is represented as `tree/offense` with `RequiredPoints = 8`.

Allocation checks run in this stable order:

1. request validity;
2. duplicate applied operation identity;
3. known skill;
4. rank cap;
5. skill prerequisites in declared order;
6. category-investment requirements in declared order;
7. available player-level-derived points.

A rejection returns a stable status, rejection code, related requirement ID, required value and actual value. Rejected operations are not recorded, so the same operation identity may be retried after its blocking requirement becomes satisfied. Applied operations remain exactly-once and replay as `DuplicateNoChange` without spending another point.

## Skill points and snapshots

- XP/player level remains the source of total earned skill points.
- Total earned points equal player level, so level 1 starts with one point.
- `SetPlayerLevel` is still the only level input boundary; the skills authority does not grant XP.
- Snapshots expose immutable ranks, applied operation IDs, spent points, available points and deterministic per-tree/per-category investment projections.

## Presentation compatibility

The existing Skills scene still consumes `SkillCatalogV1`, `Definitions`, rank snapshots and `Allocate`. It can render variable catalog sizes without owning progression truth. SKILL-002 does not add or implement gameplay effects.

## Authority boundaries

The skills runtime does not mutate XP, combat, weapons, enemies, rewards, wallets, inventory, equipment, shops, crafting or scenes. Activated and passive gameplay effects remain separate future work.
