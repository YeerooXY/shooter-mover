# Shooter Mover — Product Discovery Batch D-211 through D-220

Status: authoritative verified extension to `assembly/intake/LIVE_DECISIONS.md`.

## D-211 — Representative production-ready Stage 2 gate

- Status: accepted
- Choice: C — complete reliable slice plus repeatable extension proof
- Accepted requirement: The complete factory slice must provide the reliable end-to-end loop, including saves and recovery, checkpoints, banking, loot risk, one shop, strongboxes, difficulty, completion, replay, performance, and diagnostics.
- Extension proof: Before broader production begins, add at least one new room or encounter, one new enemy, and one new weapon through the intended production pipelines without foundational-system rewrites.
- Quality rule: Critical systems require automated smoke and regression coverage, save-interruption tests, and representative performance budgets.
- Collaboration rule: Content ownership boundaries must support isolated parallel human or AI-agent branches.
- Deferred polish: Final environment polish, complete music, cinematics, all difficulty tuning, and the mature economy may remain deferred.

## D-212 — Representative end-to-end final-art pipeline proof

- Status: accepted
- Choice: C — prove the real art pipeline on a representative subset
- Accepted requirement: Most Stage 2 content may remain coherent temporary art, but a representative subset must pass through the intended final production pipeline.
- Required subset: Include the player mech, at least one ordinary enemy, the elite or upgraded droid, at least two visually different weapons, and one major factory machine or environmental set.
- Rendering proof: Demonstrate representative shadows, normal maps, emissive effects, damage feedback, destruction states, animation, and dimensional-looking 2D presentation.
- Pipeline rule: Document import settings, pivots, scale, sorting, collision, animation, memory, and performance requirements.
- Repeatability rule: Another artist or AI-assisted workflow must be able to add equivalent content without manually repairing every asset inside Unity.
- Scope rule: Final art for the remaining factory content stays deferred until the pipeline is validated.

## D-213 — Developer establishes the pipeline, independent agent reproduces it

- Status: accepted
- Choice: C — reference implementation followed by independent reproduction
- Accepted requirement: The primary developer first creates one reference example and documents files, folders, data definitions, validation, naming, imports, tests, previews, save implications, and common failure cases.
- Independent proof: An isolated AI-agent branch or occasional collaborator then adds another small representative weapon, enemy variant, or room encounter without direct foundational implementation help.
- Integration rule: The contribution must integrate through review and automated checks without modifying unrelated core systems.
- Documentation rule: Missing documentation discovered during reproduction must be corrected.
- Evidence rule: Evaluate merge conflicts, ownership boundaries, and repeatability rather than manually rescuing one special contribution.

## D-214 — Layered automated and playable content validation

- Status: accepted
- Choice: C — automation establishes technical safety, playable review establishes quality
- Accepted requirement: Every content contribution first passes automated checks for required fields, valid ranges, stable identifiers, references, import rules, loot and progression eligibility, save compatibility, and basic runtime behavior.
- Smoke coverage: Test representative spawn, fire, damage, death, completion, and performance warnings where practical.
- Approval rule: Passing automation means safe to review, not approved for the game.
- Playable review: Judge combat feel, readability, audiovisual feedback, encounter purpose, weapon identity, four-weapon interaction, collision, navigation, and accessibility behavior.
- Contributor rule: Human and AI-agent contributions use the same validation gates.

## D-215 — Reusable archetypes with isolated tested extensions

- Status: accepted
- Choice: C — data-driven ordinary content with bounded code extensions
- Accepted requirement: Ordinary weapons, enemies, rooms, encounters, objectives, hazards, and rewards are assembled from typed `ScriptableObject` definitions and reusable behavior modules.
- Module rule: Maintain a compact reusable module set for representative firing, movement, enemy, objective, hazard, and reward archetypes.
- Novelty rule: Genuinely new mechanics may add isolated reusable code extensions rather than one-item hacks.
- Extension gate: New extensions require automated tests, playable validation, documentation, and compatibility with saves, loot, diagnostics, and four-weapon interactions.
- Architecture rule: Ordinary content additions must not modify unrelated foundational systems or force all creativity through one bloated universal schema.

## D-216 — Hybrid canonical content-data model

- Status: accepted
- Choice: C — `ScriptableObject` authoring with deterministic review output and selective import
- Accepted requirement: Typed `ScriptableObject` assets remain the canonical runtime and ordinary authoring format.
- Identity rule: Every content item uses a stable human-readable identifier and participates in automated searchable registries.
- Review rule: Generate deterministic text summaries or snapshots so changes remain reviewable outside opaque Unity asset diffs.
- Import rule: Use CSV or JSON selectively for bulk balance tables, localization, loot weights, or highly repetitive definitions.
- Generation rule: Generated files are never hand-edited, and import/export round trips must be deterministic and tested.
- Ownership rule: Explicit ownership boundaries reduce parallel edits to the same Unity assets.

## D-217 — Isolated content packages with generated integration

- Status: accepted
- Choice: C — owned feature packages and explicit shared-system ownership
- Accepted requirement: Each weapon, enemy, encounter, or room contribution lives mainly inside an owned feature folder containing definitions, prefabs, effects, audio, art, tests, previews, and documentation.
- Integration rule: Shared registries, lookup tables, and build indexes are generated or updated through deterministic tooling rather than manually edited by every contributor.
- Shared-system rule: Central balance tables and foundational systems have explicit owners and require separate justified tasks.
- Branch rule: Contributions should be small, reviewable, removable, rollback-safe, and resistant to Unity YAML conflicts.
- Validation rule: Reject duplicate identifiers, missing references, and incompatible package content before integration.
- Contributor rule: Human and AI-agent branches follow the same ownership and merge boundaries.

## D-218 — Modular room packages assembled by a lightweight level graph

- Status: accepted
- Choice: C — independently testable room units plus authored mission graph
- Accepted requirement: Each meaningful room or encounter is an independently previewable and testable prefab or additive-scene package.
- Room contract: Cover stable identity, doorway connections, encounter lifecycle, enemy and pickup ownership, persistent state, lighting, sorting, collision, navigation, teleports, shops, banking, rewards, and performance budgets.
- Assembly rule: A lightweight authored level graph defines room connections and controls mission-wide structure and state.
- Granularity rule: Small corridors and decorative connectors may remain lightweight prefabs rather than requiring a full scene each.
- Coupling rule: Cross-room communication uses stable identifiers and explicit events instead of fragile direct scene-object references.
- Scaling rule: Support independent branch ownership, future streaming, and later co-op room instances without overengineering the Stage 2 slice.

## D-219 — Typed engine-independent mission-state model

- Status: accepted
- Choice: C — authoritative plain-C# state keyed by stable IDs
- Accepted requirement: A typed engine-independent `MissionRunState` owns authoritative mission and room state. Loaded Unity rooms are projections of that state rather than its owners.
- Transition rule: Rooms submit explicit validated commands or events for entering rooms, starting encounters, defeating enemies, collecting rewards, unlocking routes, activating checkpoints, banking loot, and completing objectives.
- State-separation rule: Distinguish permanently retained run progress, checkpoint-secured rewards, temporary post-checkpoint state, death-restored state, suspend snapshots, and atomic economy or reward transactions.
- Reliability rule: Scene unload and reload, death rollback, fast travel, and save recovery must not reset or duplicate durable state.
- Testability rule: The domain model must be unit-testable without loading Unity scenes and must produce actionable transition logs.
- Architecture rule: Unity scripts may observe and request transitions but may not directly mutate durable mission progress.

## D-220 — Versioned snapshots with a short transactional journal

- Status: accepted
- Choice: C — atomic snapshots plus compact recovery journal
- Accepted requirement: Persist atomic versioned `MissionRunState` snapshots as the primary save format and keep a compact journal for important transitions since the latest safe snapshot.
- Journal scope: Include checkpoint activation, banking transactions, unique reward collection, route or objective changes, mission completion, and suspend/resume boundaries.
- Integrity rule: Journal records are idempotent, checksummed, sequence-numbered, and committed before risky transitions are acknowledged.
- Recovery rule: Fold the journal into fresh snapshots regularly and recover from backups when either file is incomplete.
- Exclusion rule: Do not journal routine shots, damage ticks, ordinary movement, or every enemy action.
- Diagnostics rule: Persisted recovery data remains privacy-safe and useful for diagnosing impossible or corrupted state.

## Batch persistence

- Persisted through: D-220
- Unsaved decisions after checkpoint: 0
- Next direction: define the layered Unity CI and build-validation gate for parallel human and AI-assisted production