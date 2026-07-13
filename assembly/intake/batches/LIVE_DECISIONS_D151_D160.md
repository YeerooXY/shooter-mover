# Shooter Mover — Product Discovery Batch D-151 through D-160

Status: authoritative verified extension to `assembly/intake/LIVE_DECISIONS.md`.

## D-151 — Solo lead developer supported by AI agents

- Status: accepted
- Choice: A — one human lead developer with AI-agent support and occasional specialist outsourcing
- Accepted requirement: The internal MVP is directed, reviewed, and integrated by one human developer. AI agents may support planning, implementation, tests, documentation, tooling, and bounded content-production tasks.
- Ownership rule: The human lead retains final product, code, asset, scope, and integration authority.
- Specialist rule: Art, audio, music, animation, or other specialist work may be commissioned selectively when it is more efficient than building the capability internally.
- Continuity rule: Important decisions, contracts, task boundaries, and operational knowledge must be written into the repository rather than remaining implicit in one person's memory.

## D-152 — Four or more parallel AI agents

- Status: accepted
- Choice: C — aggressive parallel AI-agent execution
- Accepted requirement: The workflow should support four or more AI agents working concurrently on independently owned tasks when suitable work is available.
- Throughput rule: Parallelism is a means to accelerate validated work, not permission to create overlapping implementations or speculative infrastructure.
- Review rule: The solo human lead remains the review and integration bottleneck, so task size, proof quality, and merge sequencing must keep review load manageable.
- Planning rule: The backlog should expose dependencies, file ownership, expected interfaces, and acceptance criteria before concurrent tasks start.

## D-153 — Isolated task branches with interface-first contracts

- Status: accepted
- Choice: B — isolated branches and explicit contracts
- Accepted requirement: Each agent works on a separate task branch with narrowly declared scope, owned modules or assets, dependencies, acceptance tests, and interfaces.
- Integration rule: Agent work is reviewed and merged individually into an integration branch or equivalent controlled checkpoint rather than being developed concurrently on one shared branch.
- Collision rule: Avoid assigning multiple agents to the same Unity scene, prefab, ScriptableObject, or source module unless explicit sequencing is established.
- Proof rule: Every branch must provide reproducible evidence that its contract and acceptance criteria are satisfied.

## D-154 — Gameplay-domain module ownership

- Status: accepted
- Choice: B — bounded gameplay-domain modules
- Accepted requirement: Divide implementation primarily around domains such as player movement, weapons and combat, enemies, levels and encounters, rewards and inventory, menus and progression, plus narrowly governed shared foundations.
- Parallelism rule: Tasks should normally fit inside one domain and depend on stable contracts rather than editing several technical layers across the project.
- Shared-contract rule: Cross-domain concepts such as damage, item identity, events, save data, and content references require explicit ownership and versioned interfaces.
- Validation rule: Domain implementation is still demonstrated through small end-to-end playable integrations rather than isolated subsystem completion alone.

## D-155 — Unity with C#

- Status: accepted after revision
- Final choice: Unity with C#
- Superseded choice: Godot 4 with statically typed GDScript
- Accepted requirement: Build the Windows-first internal MVP in Unity using C# as the primary implementation language.
- Platform rule: Preserve a credible future Android export path without implementing the Android version during the one-level Windows milestone.
- Architecture rule: Avoid unnecessary dependence on proprietary packages or editor-only assumptions when plain project-owned C# contracts are sufficient.
- Scope rule: Do not introduce DOTS, a custom engine, or another major runtime framework merely for theoretical future scale before profiling demonstrates a need.

## D-156 — Hybrid domain-core Unity architecture

- Status: accepted
- Choice: B — plain C# domain logic with Unity-facing adapters
- Accepted requirement: Keep important gameplay rules, state transitions, calculations, inventory, rewards, weapon logic, and encounter state in plain testable C# where practical. Use MonoBehaviours and other Unity components for input, physics integration, rendering, animation, audio, scene lifecycle, and engine-facing concerns.
- Boundary rule: Domain objects must not depend casually on scene searches, implicit Unity lifecycle ordering, or mutable global state.
- State rule: Avoid maintaining conflicting authoritative copies of the same state in both plain C# objects and scene components.
- Networking rule: This separation should leave room for later co-op adaptation without implementing multiplayer architecture during the offline MVP.

## D-157 — ScriptableObject definitions with separate runtime state

- Status: accepted
- Choice: B — Unity-native immutable content definitions
- Accepted requirement: Author weapons, enemies, loot tables, encounters, difficulty profiles, and similar static content through typed ScriptableObject definitions where appropriate.
- Runtime rule: Mutable per-run or per-instance state belongs in plain C# runtime objects rather than being written back into shared ScriptableObject assets.
- Identity rule: Content definitions require stable IDs, validation, and explicit reference conventions suitable for persistence and automated tests.
- Parallel-authoring rule: Keep definition assets small and domain-owned to reduce binary or serialized-asset conflicts between agents.
- Future rule: External data or modding pipelines may be added later if a real requirement emerges; they are not required for the internal MVP.

## D-158 — URP 2D lighting with normal-mapped sprites

- Status: accepted
- Choice: B — URP 2D Renderer with dynamic 2D lights and sprite normal maps
- Accepted requirement: Use Unity's Universal Render Pipeline 2D workflow to support normal-mapped sprites, restrained dynamic lighting, emissive effects, shadows, warning lights, muzzle flashes, projectiles, and explosions.
- Readability rule: Core silhouettes, attacks, pickups, navigation, and hazards must remain readable without depending on a particular dynamic light being present.
- Art-pipeline rule: The offline-3D sprite-render pipeline should be able to produce or derive consistent normal maps and lighting references where useful.
- Performance rule: Establish budgets and fallback quality settings compatible with Windows first and a possible later Android version.
- Scope rule: The runtime remains flat 2D; this choice does not reopen the orthographic-3D presentation decision.

## D-159 — Additive scenes with modular prefabs

- Status: accepted
- Choice: B — additive composition and modular authored assets
- Accepted requirement: Separate persistent systems, interface, level geometry, lighting, encounter content, and focused test environments into modular scenes and prefabs that can be composed additively.
- Ownership rule: Each task receives temporary explicit ownership of every Unity scene, prefab, or serialized asset it may edit.
- Reference rule: Cross-scene dependencies and initialization order must use explicit services, installers, registries, or equivalent contracts rather than fragile scene searches.
- Testing rule: Individual gameplay domains, rooms, encounters, and integrations should be loadable in focused verification scenes where practical.
- Scope rule: Preserve handcrafted visual authoring; do not replace the level with a primarily code-generated scene framework.

## D-160 — Layered automated validation plus playable proof

- Status: accepted
- Choice: B — automated tests, build validation, and human-playable verification
- Accepted requirement: Agent tasks must include plain-C# unit tests for domain rules where applicable, targeted Unity tests for engine integration, automated project or build validation, and a small playable proof or precise reproducible test procedure for user-visible behavior.
- Acceptance rule: Compilation alone is not sufficient proof for a gameplay task.
- Human-review rule: Movement feel, aiming, visual readability, audio impact, encounter pacing, and similar experiential qualities still require review by the human lead.
- Proportionality rule: Test depth should match task risk; do not build a comprehensive deterministic mission-simulation framework before the product requires it.
- Regression rule: Stable bugs and important integration failures should receive automated regression coverage whenever practical.

## Batch persistence

- Persisted through: D-160
- Unsaved decisions after checkpoint: 0
- Next batch boundary: D-170
