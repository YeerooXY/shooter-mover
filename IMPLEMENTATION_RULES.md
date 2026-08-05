# Shooter Mover Implementation Rules

This is the mandatory contract for planning, implementation, review, and AI-assisted development in this repository.

> **Plan broadly. Implement narrowly. Review literally.**

The committed plan defines the design. Implementation follows it. An implementation pass must not quietly redesign the feature.

If the plan is wrong, incomplete, or impossible to follow, amend and commit the plan before changing production code.

---

# 1. The plan is authoritative

Every non-trivial feature, refactor, or subsystem change requires an approved Markdown plan inside the repository before implementation begins.

The plan must define:

- exact scope and player-facing result;
- exact namespaces and file paths;
- exact class, struct, enum, interface, method, property, field, and event names when known;
- exact IDs, JSON keys, persisted identifiers, and resource paths;
- existing types and systems that must be reused;
- the source of truth for every important value;
- expected data and call flow;
- forbidden additions and explicit exclusions;
- ordered implementation steps with checkboxes;
- acceptance criteria and manual verification for every step;
- expected changed files and production-line budget;
- commit checkpoints.

Do not implement from remembered chat or a loose summary when an implementation plan exists. The committed plan is authoritative.

---

# 2. Mandatory section in every implementation plan

Every implementation plan must contain this section near the beginning. It may add stricter rules, but it may not omit or weaken these rules.

```markdown
## Mandatory implementation rules

- Follow `IMPLEMENTATION_RULES.md`.
- The names, paths, IDs, ownership, and data flow in this plan are authoritative.
- Implement only the assigned checkbox or explicitly approved group of checkboxes.
- Target approximately 100–250 changed production lines per implementation step.
- Do not exceed 350 changed production lines in one step without an approved plan amendment.
- Do not create unplanned abstractions, interfaces, wrappers, adapters, compatibility layers, managers, policies, authorities, catalogues, registries, or duplicate models.
- Do not rename, reformat, or clean up unrelated code.
- Reuse the existing types named by this plan.
- Keep one source of truth for each value.
- Use simple, game-facing names.
- Inside a bounded feature namespace or folder, omit the repeated feature prefix unless it prevents a real ambiguity.
- If the plan cannot be followed exactly, stop and record a plan conflict. Amend and commit the plan before continuing.
- Complete one small step at a time and commit after each completed step.
- Keep each commit independently reviewable and compiling whenever technically possible.
- After completing a step, tick only its checkbox and optionally add its commit or PR reference. Do not create a separate implementation report unless requested.
```

A plan without this section is not ready for implementation.

---

# 3. Small implementation steps

Implementation must be divided into small, reviewable slices.

Normal target per step:

- **100–250 changed production lines**;
- **2–5 changed production files**;
- **0–2 new production classes**;
- one clear behaviour or responsibility;
- one independently reviewable checkbox;
- one manual verification path.

A step may be smaller. Small is good.

A step above 350 changed production lines requires an approved explanation in the plan before coding begins. Generated files, Unity metadata, large data files, mechanical renames, and deletions may justify larger diffs, but the plan must identify them in advance.

Unless Nemo explicitly approves a grouped change, one AI implementation pass implements only one checkbox.

Do not:

- implement later checkboxes because they look easy;
- prepare speculative code for later steps;
- perform opportunistic cleanup;
- broaden the task after finding nearby problems.

Record nearby problems separately.

---

# 4. Exact step contract

Each implementation checkbox must define:

```markdown
### Step N — Clear step name

Status: [ ]

Create:
- exact paths

Modify:
- exact paths

Reuse:
- exact existing types

Exact names and public API:
- exact classes, methods, properties, fields, IDs, and JSON keys

Source of truth:
- exact owner of each affected value

Do not create:
- named forbidden classes, models, layers, or behaviours

Acceptance:
- exact behaviour that proves the step is complete
- explicit statement of what later behaviour is not included
- expected production change: 100–250 lines

Manual verification:
- exact action and expected result

Commit checkpoint:
- `feature: clear completed behaviour`
```

Architecture-sensitive steps require more exact detail, not more implementation freedom.

---

# 5. Naming rules

Use simple game-facing names based on what developers and players call the concept.

Names describe the current game, not temporary architecture or migration history.

Avoid these words unless they distinguish two real current concepts and the plan explains why:

```text
Compact
Live
Production
Hybrid
Graph
Composition
Authority
Policy
Runtime
```

## The feature boundary supplies context

Inside a bounded feature namespace or folder, omit the repeated feature prefix.

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

Inside strongbox code, prefer:

```text
lootTable
selectedReward
dropChance
tier
openResult
```

Do not repeat `strongbox` when the surrounding namespace, type, or method already supplies that context.

Apply the same rule elsewhere:

```text
ShooterMover.Weapons.Definition
ShooterMover.Weapons.Projectile
ShooterMover.Enemies.Definition
ShooterMover.Crafting.Recipe
ShooterMover.Rooms.Reward
```

Keep the feature name where it prevents genuine ambiguity, especially:

- IDs such as `strongbox.gold`;
- cross-feature events such as `StrongboxOpened`;
- shared namespaces or APIs where the shorter name is unclear;
- two genuinely different current concepts with otherwise identical names.

Context-aware naming is not permission to use vague names or unexplained acronyms.

## Suffixes

Use suffixes only when they describe a real responsibility:

- `Definition` — authored, mostly immutable content;
- `State` — mutable saved or session state;
- `View` — presentation only;
- `Controller` — coordinates a concrete behaviour;
- `Spawner` — creates scene instances;
- `Loader` — loads authored or persisted data;
- `Validator` — validates data and reports errors;
- `Service` — focused shared operation with no clearer game-facing noun.

Do not automatically create every suffix variation for a feature.

## Names requiring plan justification

```text
Manager
System
Handler
Processor
Coordinator
Orchestrator
Provider
Resolver
Adapter
Bridge
Facade
Pipeline
Context
Registry
Catalog
```

These words are not universally forbidden, but they often hide unclear ownership or an unnecessary layer.

## Variables and methods

Variables describe what they contain. Booleans read as questions. Methods begin with clear actions.

Prefer:

```text
currentHealth
maximumHealth
equippedWeapon
moneyEarned
remainingDashCharges
isAlive
hasTarget
canFire
shouldDropLoot
Fire
Reload
TakeDamage
SpawnEnemy
AddMoney
SaveProfile
```

Avoid vague names such as `data`, `info`, `result`, `value`, `obj`, `thing`, `temp`, `Process`, `Execute`, `Apply`, or `Handle` unless the tiny local scope makes the meaning obvious.

---

# 6. Do not overcomplicate the implementation

Choose the shortest understandable design that satisfies the approved requirement.

Do not design for hypothetical future needs unless the current plan requires them.

Do not add:

- an interface because another implementation might exist one day;
- a factory when construction, a prefab, or an existing loader is already clear;
- a policy class for a few direct conditions;
- a wrapper that only forwards calls to another wrapper;
- a second representation of data that an existing type already provides;
- a compatibility layer without a documented coexistence reason and deletion step.

Every proposed class must answer:

1. What information or behaviour does it own?
2. Who calls it?
3. Why should the caller not own that responsibility directly?
4. Why can an existing class not own it?
5. What becomes simpler because this class exists?

If the answers are weak, do not create the class.

## One source of truth

The plan must name one owner for every important value.

Example:

```text
Weapon display name -> Definition.DisplayName
Current ammunition -> State.CurrentAmmo
Player money -> PlayerProfile.Money
Current room rewards -> RunResult
Enemy health -> Health.Current
```

Views may display values. They must not become independent authoritative copies.

Mappings between nearly identical models require explicit plan approval.

---

# 7. Plan conflict rule

If implementation cannot follow the plan exactly, stop before making unapproved production changes.

Record:

```markdown
## Plan conflict

Step:
- exact checkbox

The plan requires:
- exact planned type, path, ID, or behaviour

The repository currently provides:
- exact conflicting reality

Why the plan cannot be followed:
- concise technical explanation

Recommended amendment:
- exact replacement and every affected step

Production code changed:
- none
```

Then amend, review, and commit the plan before implementation continues.

Do not silently choose a different design.

---

# 8. Commit regularly

Git is durable. Chat is not.

> **One completed implementation checkbox equals one commit by default.**

A task or pull request may contain several planned commits, but each step remains independently reviewable.

Do not:

- accumulate several completed checkboxes into one final commit;
- move to the next checkbox while the completed step is uncommitted;
- mix unrelated formatting, cleanup, or renames into the step commit.

Each implementation commit should:

- correspond to one plan checkbox;
- contain only the required files;
- compile whenever technically possible;
- include the checkbox update;
- use a clear game-facing message.

Good examples:

```text
strongboxes: load loot tables
strongboxes: validate reward entries
weapons: add held-fire timing
hub: show saved money and scrap
```

An approved plan must be committed before implementation begins. Any amendment must be committed before code that depends on it.

---

# 9. Minimal completion documentation

For an ordinary completed step, change only:

```markdown
- [ ] Step 4 — Transfer room rewards
```

to:

```markdown
- [x] Step 4 — Transfer room rewards
```

An optional commit or PR reference is allowed.

Do not create duplicate implementation reports, handoff essays, architecture summaries, or extra Markdown files unless the plan, a migration, a milestone gate, or Nemo explicitly requires one.

---

# 10. Required implementation reporting

Before editing a step, state:

1. exact checkbox;
2. files to create;
3. files to modify;
4. existing types being reused;
5. expected changed production lines;
6. affected source of truth;
7. confirmation that no later checkbox will be implemented.

After implementing it, report only:

1. completed checkbox;
2. files created, modified, or deleted;
3. production lines added and removed;
4. manual verification performed or still required;
5. commit SHA or PR reference when available;
6. limitation directly relevant to that step.

Do not add a new architecture proposal after implementation or claim later steps are complete.

---

# 11. Review checklist

Review the change against the plan before reviewing stylistic preferences.

## Contract

- Did it implement exactly the assigned checkbox?
- Did it use the planned names, paths, and IDs?
- Did it reuse the required existing types?
- Did it introduce any unplanned class, interface, abstraction, mapping, or model?
- Did it alter code outside the allowed area?
- Did it implement part of a later checkbox?
- Did it remain within the line and file budget?
- Did it preserve one source of truth?
- Did it omit repeated feature prefixes where context already supplies them?
- Did it update only the correct checkbox?
- Was the step committed separately?

## Simplicity

- Could the result be clear with fewer classes?
- Is any class only forwarding calls?
- Are two types carrying nearly identical data?
- Does a new interface have only one implementation without a current need?
- Is future flexibility creating present complexity?
- Is temporary compatibility code missing a deletion step?

## Behaviour

- Does the acceptance criterion pass?
- Does manual verification prove the planned player-facing result?
- Is existing behaviour outside the task preserved?
- Is failure behaviour understandable and local?

A change that works but violates the approved contract is not complete.

---

# 12. Warning thresholds

These require explicit review:

- a small feature touches more than 8 production files;
- one step exceeds 350 changed production lines;
- one class exceeds 500 lines;
- one class exceeds 800 lines without a documented reason;
- one step introduces more than two new production classes;
- a new interface has only one implementation;
- one value exists in three representations;
- ordinary content requires editing C# catalogues or switch statements;
- a class name requires project-history knowledge to understand;
- more code maps models than implements game behaviour;
- a simple display needs a resolver even though its definition already owns the value.

Simplify the plan before continuing unless the complexity is clearly justified.

---

# 13. Planning template

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

## IDs and persisted keys

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

Each checkbox must include its create/modify/reuse/API/source-of-truth/forbidden-additions/acceptance/line-budget/manual-verification/commit contract.

---

# Final rule

When choosing between clever architecture and a small understandable implementation that satisfies the approved game requirement, choose the small understandable implementation.

The project must become easier to reason about after each feature, not harder.
