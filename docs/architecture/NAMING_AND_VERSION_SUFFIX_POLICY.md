# Game-facing naming policy

## Goal

Game code must use the terminology players and developers use when discussing the game.

A name is not acceptable merely because it matches an existing class or method. Reviews must also ask whether the name clearly explains the game concept, responsibility, or action to a developer who does not know the implementation history.

Prefer names that describe **what something is** or **what it does**. Avoid names that primarily describe how the current implementation was assembled.

## Prefer game concepts

Prefer:

- `CurrentCharacter`, not `CharacterLiveGraph` or `CharacterRuntime`;
- `Experience`, not `ExperienceAuthority`;
- `Money`, not `MoneyWallet` when the containing character already provides the ownership context;
- `Scrap`, not `ScrapWallet`;
- `Enemy`, not `CompactEnemy` after the compact implementation becomes the only current enemy system;
- `EnemyDefinition`, not `CompactEnemyDefinition`;
- `EnemySpawner`, not `CompactEnemySceneFactory`;
- `EnemyCollisionRules`, not `CompactEnemyCollisionPolicy`;
- `StrongboxLootTable`, not `StrongboxHybridLootCatalog`;
- `StrongboxLootRules`, not `StrongboxHybridLootPolicy`;
- `StrongboxLootValidator`, not `StrongboxHybridLootPolicyValidation`.

These examples define the intended direction. They are not permission for a repository-wide textual replacement.

## Architecture words require justification

Do not preserve temporary architecture, migration, or implementation-generation words in current game code unless they genuinely distinguish two current concepts.

Words requiring explicit justification include:

- `Compact`;
- `Live`;
- `Production`;
- `Hybrid`;
- `Graph`;
- `Composition`;
- `Authority`;
- `Policy`;
- `Runtime`;
- `Manager`;
- `Service`.

These words are not universally forbidden. They are inappropriate when removing them produces a clearer and unambiguous game-facing name.

For example, `Authority` may be useful when distinguishing the sole state owner from read-only projections. It should not be appended automatically to every object that owns state.

## Methods should state the action

Prefer direct actions over registration or configuration vocabulary when the real behavior is known.

Examples:

- `SetCollisionRules`, not `RegisterCollisionPolicy`;
- `Spawn`, not `ConfigureSceneFactory`;
- `Validate`, not `RunPolicyValidation`;
- `GetTier`, not `GetByTierNumber` when the argument type and containing class already make the lookup clear.

Avoid generic verbs such as `Handle`, `Process`, `Execute`, `Configure`, and `Register` when a more exact game action is available.

## Context should remove repetition

Do not repeat information already supplied by the containing type.

Prefer:

```csharp
CurrentCharacter character;
character.Experience;
character.Money;
character.Scrap;
character.Loadout;
```

Instead of:

```csharp
CharacterLiveGraph graph;
graph.ExperienceAuthority;
graph.MoneyWallet;
graph.ScrapWallet;
graph.LoadoutRuntime;
```

## Keep explicit version suffixes only where versions coexist

Retain `V1`, `V2`, and similar suffixes for:

- persisted save components and codecs;
- serialized wire, import, export, snapshot, receipt, and replay schemas;
- externally consumed contracts whose older representations remain supported;
- migrations and adapters that explicitly identify the representation they consume;
- deterministic fingerprints whose canonical byte or string layout is versioned;
- protocols where multiple versions can be active at the same time.

Do not suffix ordinary game concepts merely because they are the first implementation. This includes:

- controllers and views;
- application services;
- gameplay rules that replace rather than coexist;
- ordinary definitions and recommendations;
- runtime composition objects;
- scene adapters and presenters;
- tests named after current game behavior.

## Review requirement

Every code review that touches public or feature-level names must check:

1. Does the name explain the game concept without knowledge of repository history?
2. Does it contain temporary migration or architecture vocabulary?
3. Does the containing type already provide part of the name's context?
4. Is a shorter name equally precise?
5. Would a developer naturally use this term when discussing the feature?
6. Does the name describe the actual behavior rather than the mechanism used to implement it?

A compatibility audit that only confirms that symbols exist is incomplete.

## Migration rule

Renames must be performed by bounded feature area, with all references, Unity metadata, serialized assets, tests, reflection lookups, diagnostics, and documentation audited together.

Do not run a repository-wide blind textual replacement. Preserve Unity script GUIDs during file moves. Where persisted or external names must remain stable, separate the game-facing code name from the stored representation explicitly.

## Test terminology

Tests should describe game behavior rather than implementation generation.

Prefer:

- `GroundEnemiesBlockEachOther`;
- `FlyingAndGroundEnemiesMayOverlap`;
- `SelectingLockedLevelDoesNotLoadScene`.

Avoid test names that encode temporary architecture, migrations, or version suffixes unless the test specifically verifies that representation.
