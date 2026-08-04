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
- `LootTable`, not `StrongboxHybridLootCatalog`, inside `Rewards.Strongboxes`;
- `LootRules`, not `StrongboxHybridLootPolicy`, inside `Rewards.Strongboxes`;
- `LootValidator`, not `StrongboxHybridLootPolicyValidation`, inside `Rewards.Strongboxes`;
- `ItemGenerator`, not `StrongboxHybridEquipmentGenerationResolver`, when its Strongbox ownership is already supplied by the namespace or containing feature.

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

Do not repeat information already supplied by the containing type, namespace, or tightly bounded feature.

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

Inside `ShooterMover.Application.Rewards.Strongboxes`, prefer:

```csharp
LootTable.GetTier(tier);
LootRules rules;
LootValidator.Validate(...);
ItemGenerator generator;
```

Do not shorten a name until it becomes ambiguous. A type used from a broad mixed namespace may still need a feature prefix; a type already owned by `Rewards.Strongboxes` normally does not.

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
3. Does the containing type or namespace already provide part of the name's context?
4. Is a shorter name equally precise?
5. Would a developer naturally use this term when discussing the feature?
6. Does the name describe the actual behavior rather than the mechanism used to implement it?

A compatibility audit that only confirms that symbols exist is incomplete.

## Migration rule

Renames must be performed by bounded feature area, with all references, Unity metadata, serialized assets, tests, reflection lookups, diagnostics, and documentation audited together.

Do not run a repository-wide blind textual replacement. Preserve Unity script GUIDs during file moves. Where persisted or external names must remain stable, separate the game-facing code name from the stored representation explicitly.

Temporary compatibility bridges are acceptable only when the caller inventory cannot be proven. Mark them obsolete, document their removal condition, and do not let them become the permanent API.

## Test terminology

Tests should describe game behavior rather than implementation generation.

Prefer:

- `GroundEnemiesBlockEachOther`;
- `FlyingAndGroundEnemiesMayOverlap`;
- `TierElevenAcceptsItsAuthoredLevelTwelveOutcome`;
- `SelectingLockedLevelDoesNotLoadScene`.

Avoid test names that encode temporary architecture, migrations, or version suffixes unless the test specifically verifies that representation.
