# Naming and version suffix policy

## Goal

Game code should use the terminology players and developers use when discussing the game. A type name should carry a numeric version suffix only when multiple incompatible representations may need to coexist or when persisted/external data must be migrated explicitly.

## Keep explicit version suffixes

Retain `V1`, `V2`, and similar suffixes for:

- persisted save components and codecs;
- serialized wire, import, export, snapshot, receipt, and replay schemas;
- externally consumed contracts whose older representations remain supported;
- migrations and adapters that explicitly identify the representation they consume;
- deterministic fingerprints whose canonical byte/string layout is versioned;
- protocols where multiple versions can be active at the same time.

## Remove version suffixes

Do not suffix ordinary game concepts merely because they are the first implementation. This includes:

- controllers and views;
- application services;
- gameplay policies that replace rather than coexist;
- ordinary definitions and recommendations;
- runtime compositions;
- scene adapters and presenters;
- tests named after current game behavior.

Examples:

- `LevelSelectionController`, not `LevelSelectionControllerV1`;
- `LevelSelectionService`, not `LevelSelectionServiceV1`;
- `LevelDefinition`, not `LevelSelectionDefinitionV1`;
- `CharacterSaveSnapshotV1` when the persisted representation is genuinely versioned.

## Migration rule

Renames must be performed by bounded feature area, with all references, Unity metadata, serialized assets, tests, reflection lookups, and documentation audited together. Do not run a repository-wide textual replacement. Serialized Unity types require GUID preservation and, where necessary, moved-type metadata or an explicit asset migration.

## Test terminology

Tests should describe game behavior rather than implementation generation. Prefer names such as `SelectingLockedLevelDoesNotLoadScene` over names that encode a temporary architecture or version suffix.
