# Shooter Mover Implementation Rules

This document is the mandatory implementation contract for all planning, coding, review, and AI-assisted development in this repository.

Its purpose is to prevent architectural drift, unnecessary complexity, naming inflation, oversized changes, and undocumented deviations from approved plans.

These rules apply to humans and AI agents.

## Core principle

> Plan broadly. Implement narrowly. Review literally.

The plan defines the design. Implementation follows the plan. An implementation pass must not quietly redesign the feature.

If the plan is wrong, incomplete, or impossible to follow, amend the plan first. Do not silently invent a replacement architecture while coding.

---

# 1. The plan is authoritative

Every non-trivial feature, refactor, or subsystem change must have an approved Markdown plan inside the repository before implementation begins.

The plan must define the intended result precisely enough that later implementation passes do not need to reinterpret the design.

The plan must include:

- exact feature scope;
- exact class, struct, enum, interface, method, property, field, event, and file names when known;
- exact namespaces and file paths;
- exact stable IDs, JSON keys, resource paths, and persisted identifiers;
- existing classes and systems that must be reused;
- the source of truth for every important piece of data;
- the expected data and call flow;
- explicit exclusions and forbidden additions;
- ordered implementation steps with checkboxes;
- acceptance criteria for every step;
- manual verification instructions;
- expected files changed;
- expected production-line budget;
- commit checkpoints.

Do not begin coding from a loose conversation summary when an exact plan is required.

Do not rely on remembered chat context. The committed plan is authoritative.

---

# 2. Mandatory section in every implementation plan

Every implementation plan must contain the following section near the beginning. It may add stricter rules, but it may not omit or weaken these rules.

```markdown
## Mandatory implementation rules

- Follow `IMPLEMENTATION_RULES.md`.
- The names, paths, IDs, ownership, and data flow in this plan are authoritative.
- Implement only the currently assigned checkbox or explicitly approved group of checkboxes.
- Target approximately 100–250 changed production lines per implementation step.
- Do not exceed 350 changed production lines in one step without an approved plan amendment.
- Do not create unplanned abstractions, interfaces, wrappers, adapters, compatibility layers, managers, policies, authorities, or duplicate models.
- Do not rename or clean up unrelated code.
- Reuse the existing types listed by this plan.
- Keep one source of truth for each value.
- Use simple, game-facing names.
- Inside a bounded feature namespace or folder, omit the repeated feature prefix unless needed to prevent a real ambiguity.
- If the plan cannot be followed exactly, stop implementation and record a plan conflict. Amend and commit the plan before continuing.
- Complete one small step at a time and commit after each completed step.
- Keep each commit reviewable and compiling whenever technically possible.
- After completing a step, update only its checkbox and optional commit or PR reference. Do not create a separate implementation report unless requested.
```

A plan without this section is not ready for implementation.

---

# 3. Small implementation steps

Implementation must be divided into small, reviewable slices.

The normal target is:

- 100–250 changed production lines;
- 2–5 changed production files;
- 0–2 new production classes;
- one clear behaviour or responsibility;
- one independently reviewable checkbox;
- one manual verification path.

A step may be smaller. Small is not a problem.

A step above 350 changed production lines requires an approved explanation in the plan before coding begins.

Generated files, Unity metadata, large data files, mechanical renames, and deletions may justify larger diffs, but the plan must identify them in advance.

## One implementation pass, one step

Unless the user explicitly approves a grouped change, one AI implementation pass should implement only one plan checkbox.

Do not implement later checkboxes because they appear easy or closely related.

Do not prepare speculative code for future steps.

Do not perform opportunistic cleanup.

Do not broaden the task after discovering nearby problems. Record them separately.

---

# 4. Exact step contract

Every implementation checkbox must define enough detail to be reviewed literally.

Recommended format:

```markdown
### Step 3 — Load recipe definitions

Status: [ ]

Create:
- `Assets/ShooterMover/Runtime/Crafting/RecipeLoader.cs`

Modify:
- `Assets/ShooterMover/Runtime/Crafting/Crafting.asmdef`

Reuse:
- `JsonFileLoader`
- `WeaponDefinitionLoader`

Exact public API:
- `public sealed class RecipeLoader`
- `public Recipe GetRecipe(string recipeId)`
- `public IReadOnlyList<Recipe> GetAllRecipes()`

Source of truth:
- Recipe data comes only from `Content/Crafting/*.json`.

Do not create:
- `CraftingManager`
- `RecipeProvider`
- `RecipeCatalogEntry`
- a second in-memory recipe model

Acceptance:
- one recipe JSON can be loaded by ID;
- an unknown ID produces the documented error;
- no later crafting behaviour is implemented;
- expected production change: 100–180 lines.

Manual verification:
- describe the exact Unity or command-line action and expected result.

Commit checkpoint:
- `crafting: load recipe definitions`
```

The more architecture-sensitive the feature is, the more exact the step must be.

---

# 5. Naming conventions

Use simple game-facing names based on what developers and players call the concept.

Names should describe the current game, not the history of the architecture.

Do not preserve temporary migration, compatibility, or architecture terminology in current names.

Avoid words such as:

- `Compact`
- `Live`
- `Production`
- `Hybrid`
- `Graph`
- `Composition`
- `Authority`
- `Policy`
- `Runtime`

Use one of these words only when it distinguishes two real, current concepts and the plan explicitly explains the distinction.

## Feature context supplies the prefix

Inside a bounded feature namespace or folder, do not repeat the feature name on every type.

Prefer:

```text
ShooterMover.Strongboxes.LootTable
ShooterMover.Strongboxes.LootRules
ShooterMover.Strongboxes.LootValidator
ShooterMover.Strongboxes.Pickup
ShooterMover.Strongboxes.OpenResult
ShooterMover.Strongboxes.RewardView
```

Not:

```text
StrongboxLootTable
StrongboxLootRules
StrongboxLootValidator
StrongboxPickup
StrongboxOpenResult
StrongboxRewardView
```

Inside strongbox code, prefer variables such as:

```text
lootTable
selectedReward
dropChance
tier
openResult
```

Do not repeat `strongbox` when the surrounding type, namespace, or method already supplies that context.

Apply the same rule to other bounded features:

```text
ShooterMover.Weapons.Definition
ShooterMover.Weapons.Projectile
ShooterMover.Enemies.Definition
ShooterMover.Crafting.Recipe
ShooterMover.Rooms.Reward
```

Keep the feature name when it is genuinely needed at a cross-feature boundary, for example:

- persisted IDs such as `strongbox.gold`;
- cross-feature events such as `StrongboxOpened`;
- APIs in a shared namespace where the shorter name would be ambiguous;
- two genuinely different current concepts with otherwise identical names.

Do not use short names that become unclear. Context-aware naming is not permission to use unexplained acronyms or vague words.

## Useful suffixes

Use suffixes only when they describe a real responsibility:

- `Definition` — authored, mostly immutable content;
- `State` — mutable saved or session state;
- `View` — presentation only;
- `Controller` — coordinates a concrete behaviour;
- `Spawner` — creates scene instances;
- `Loader` — loads persisted or authored data;
- `Validator` — validates data and reports errors;
- `Service` — a focused shared operation with no clearer game-facing noun.

Do not automatically create every possible suffix variation for a feature.

## Names requiring justification

The following words require explicit justification in the plan before being introduced:

- `Manager`
- `System`
- `Handler`
- `Processor`
- `Coordinator`
- `Orchestrator`
- `Provider`
- `Resolver`
- `Adapter`
- `Bridge`
- `Facade`
- `Pipeline`
- `Context`
- `Registry`
- `Catalog`

They are not universally forbidden, but they often hide unclear ownership or an unnecessary layer.

## Variables and methods

Variables must describe what they contain:

```text
currentHealth
maximumHealth
equippedWeapon
moneyEarned
remainingDashCharges
```

Avoid vague names such as:

```text
data
info
result
value
obj
thing
temp
stateData
```

Booleans should read as questions:

```text
isAlive
hasTarget
canFire
shouldDropLoot
wasRoomCompleted
```

Methods should begin with clear actions:

```text
Fire
Reload
TakeDamage
SpawnEnemy
AddMoney
SaveProfile
CompleteRoom
RefreshWeaponList
```

Avoid vague methods such as `Process`, `Execute`, `Apply`, `Handle`, or `UpdateData` without a specific object or behaviour.

---

# 6. Do not overcomplicate the implementation

Choose the shortest understandable design that satisfies the approved requirement.

Do not design for hypothetical future needs unless the current plan explicitly requires them.

## No abstraction without a current reason

Do not add an interface because another implementation might exist one day.

Do not add a factory when normal construction, a Unity prefab, or an existing loader already expresses the operation clearly.

Do not add a policy class for a few direct conditions.

Do not add a wrapper whose only purpose is to forward calls to another wrapper.

Do not introduce a new representation of data when an existing representation can be used directly.

## New class test

Every proposed class must answer:

1. What information or behaviour does it own?
2. Who calls it?
3. Why should the caller not perform this responsibility directly?
4. Why can an existing class not own this responsibility?
5. What becomes simpler because this class exists?

If these answers are weak, do not create the class.

## One source of truth

Every important value must have one named owner in the plan.

Example:

```text
Weapon display name -> Definition.DisplayName
Current ammunition -> State.CurrentAmmo
Player money -> PlayerProfile.Money
Current room rewards -> RunResult
Enemy health -> Health.Current
```

Views may display values. They must not create independent authoritative copies.

Adapters and mappings between nearly identical models are a warning sign and require explicit plan approval.

## Avoid compatibility architecture by default

Do not preserve obsolete systems merely to reduce immediate edits.

Do not add a compatibility layer unless:

- both systems must genuinely coexist;
- the coexistence period is documented;
- the owner and removal condition are documented;
- the compatibility code has a planned deletion step.

A temporary bridge without a removal plan becomes permanent architecture.

---

# 7. Plan conflict rule

If implementation cannot follow the plan exactly, stop before making unapproved production changes.

Record the conflict in this format:

```markdown
## Plan conflict

Step:
- Step 4 — Add reward transfer

The plan requires:
- `PlayerProfile.Inventory`

The repository currently provides:
- `PlayerProfile.WeaponInventory`

Why the plan cannot be followed:
- describe the exact technical conflict.

Recommended amendment:
- replace the planned type or path with the exact existing type or path;
- list every affected step.

Production code changed:
- none
```

Then:

1. amend the plan;
2. review the amendment;
3. commit the amended plan;
4. continue implementation in a later pass.

Do not silently choose a different design.

Do not use a plan conflict as permission to broaden the feature.

---

# 8. Commit regularly

Git is the durable project history. Chat is not.

Implementation must be committed in small, meaningful checkpoints.

## Default commit rule

> One completed implementation checkbox equals one commit.

A task or pull request may contain several planned commits, but each step must remain independently reviewable.

Do not accumulate several completed checkboxes into one large final commit.

Do not move to the next checkbox while the current completed step remains uncommitted.

## Commit quality

Each implementation commit should:

- correspond to one plan step;
- use a clear game-facing commit message;
- contain only the files required by that step;
- compile whenever technically possible;
- avoid unrelated formatting or cleanup;
- include the plan checkbox update when the step is complete.

Recommended messages:

```text
strongboxes: load loot tables
strongboxes: validate reward entries
weapons: add held-fire timing
hub: show saved money and scrap
```

Avoid messages such as:

```text
updates
fix stuff
refactor
changes
cleanup
```

## Plan commits

An approved plan must be committed before production implementation begins.

Any plan amendment must be committed before code that depends on the amendment.

---

# 9. Minimal documentation after implementation

Do not create duplicate implementation reports, handoff essays, architecture summaries, or extra Markdown files for ordinary completed steps.

The normal documentation update is only:

```markdown
- [ ] Step 4 — Transfer room rewards
```

to:

```markdown
- [x] Step 4 — Transfer room rewards
```

An optional short reference is allowed:

```markdown
- [x] Step 4 — Transfer room rewards — commit `abc1234`
```

Create additional documentation only when:

- the plan explicitly requires it;
- the change alters a public workflow or content format;
- migration or persistence behaviour must be recorded;
- a milestone gate requires durable evidence;
- the user explicitly requests it.

The repository and the checked plan should tell the implementation story without another generated report.

---

# 10. Required pre-implementation response

Before editing code for a plan step, the implementation agent must state:

1. the exact checkbox being implemented;
2. files to create;
3. files to modify;
4. existing types being reused;
5. expected changed production lines;
6. the source of truth affected;
7. confirmation that no later checkbox will be implemented.

If any of these cannot be answered from the plan, the step is not ready.

---

# 11. Required post-implementation response

After implementing a plan step, report only:

1. completed checkbox;
2. files created, modified, or deleted;
3. production lines added and removed;
4. manual verification performed or still required;
5. commit SHA or pull request reference when available;
6. known limitation directly relevant to that step.

Do not add a new architecture proposal after implementation.

Do not claim later steps are complete.

---

# 12. Review checklist

Review the change against the plan before reviewing style preferences.

## Contract review

- Did it implement exactly the assigned checkbox?
- Did it use the exact planned names, paths, and IDs?
- Did it reuse the required existing types?
- Did it introduce an unplanned class, interface, abstraction, mapping, or model?
- Did it alter code outside the allowed area?
- Did it implement part of a later checkbox?
- Did it remain within the planned line and file budget?
- Did it preserve one source of truth?
- Did it omit repeated feature prefixes where context already supplies them?
- Did it avoid vague or architecture-heavy naming?
- Did it update only the correct checkbox?
- Was the step committed separately?

## Simplicity review

- Could the same result be implemented clearly with fewer classes?
- Is any class only forwarding calls?
- Are two types carrying nearly identical data?
- Is a new interface backed by only one implementation without a current need?
- Is future flexibility being purchased with present complexity?
- Is a temporary compatibility layer missing a deletion step?
- Would deleting a new class only require deleting another wrapper around it?

## Behaviour review

- Does the acceptance criterion pass?
- Does the manual verification match the planned player-facing behaviour?
- Does the change preserve existing working behaviour outside the task?
- Is failure behaviour understandable and local?

A change that works but violates the approved structure is not complete.

---

# 13. Warning thresholds

These thresholds do not automatically prove a design is wrong, but they require review:

- one small feature touches more than 8 production files;
- one implementation step exceeds 350 production lines;
- one class exceeds 500 lines;
- one class exceeds 800 lines without a documented reason;
- a feature introduces more than two new production classes in one step;
- a new interface has only one implementation;
- the same value exists in three representations;
- adding ordinary content requires editing C# catalogues or switch statements;
- a class name requires explaining project history to understand it;
- more code maps models than implements game behaviour;
- a simple UI display needs a new resolver despite the source definition already owning the displayed value.

When a warning threshold is crossed, simplify the plan before continuing unless the complexity is clearly justified.

---

# 14. Planning template

New implementation plans should use this structure:

```markdown
# Feature Name — Implementation Plan

## Goal

## Player-facing result

## Scope

## Out of scope

## Mandatory implementation rules

[Copy the mandatory section from `IMPLEMENTATION_RULES.md`.]

## Existing systems to reuse

## Authoritative names and paths

## Stable IDs and persisted keys

## Source of truth

## Data and call flow

## Forbidden additions

## Files expected to change

## Implementation steps

- [ ] Step 1 — ...
- [ ] Step 2 — ...
- [ ] Step 3 — ...

## Manual verification

## Commit checkpoints

## Completion definition
```

Each step must then include its exact create/modify/reuse/API/acceptance/line-budget/verification/commit contract.

---

# 15. Final rule

When choosing between a clever architecture and a small understandable implementation that satisfies the approved game requirement, choose the small understandable implementation.

The project should become easier to reason about after each feature, not harder.
