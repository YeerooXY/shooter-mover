# Shooter Mover — Product Discovery Batch D-161 through D-170

Status: authoritative verified extension to `assembly/intake/LIVE_DECISIONS.md`.

## D-161 — Keyboard-and-mouse-first, device-independent input architecture

- Status: accepted with sequencing clarification
- Choice: B — keyboard and mouse plus gamepad, with keyboard and mouse implemented and tuned first
- Accepted requirement: The Windows internal MVP prioritizes keyboard movement and mouse aiming for its first playable implementation and tuning pass.
- Architecture rule: Gameplay consumes device-independent Unity Input System actions rather than hard-coded keys, mouse buttons, controller buttons, or platform-specific input checks.
- Expansion rule: Gamepad, twin-stick aiming, runtime prompt switching, and full rebinding must be addable without rewriting gameplay-domain code. Touch controls remain deferred to the later Android phase.
- Scope rule: Keyboard-and-mouse polish may precede complete controller polish, but the action-map and UI architecture must not assume keyboard and mouse are the only future input method.

## D-162 — Practical gameplay-accessibility baseline

- Status: accepted
- Choice: B — practical gameplay-accessibility baseline
- Accepted requirement: Include scalable HUD and text, color-independent warnings, reduced flashing or intense effects, configurable screen shake and camera feedback, aim-assistance settings, toggle-versus-hold alternatives where relevant, audio controls, display controls, and input rebinding.
- Readability rule: Critical attacks, hazards, pickups, navigation information, and state changes must not depend on color alone.
- Challenge rule: Assistance and accessibility settings should be separated from ordinary difficulty and reward rules where practical; exact record or achievement eligibility rules are decided later.
- Scope rule: Do not attempt a comprehensive release-scale accessibility suite before the core level is stable, but avoid architecture or content assumptions that would make common options expensive to add.

## D-163 — Versioned local-first saves with recovery

- Status: accepted
- Choice: B — versioned local profile with recovery safeguards
- Accepted requirement: The internal MVP uses a local-first save containing settings, currency, weapons, strongboxes, shop state, mission progress, records, and other persistent progression required by the slice.
- Integrity rule: Save operations use schema versioning, validation, atomic replacement, rolling backups, and recoverable failure handling.
- Portability rule: Provide manual export and import so saves can be backed up and moved without requiring an account or network service.
- Future rule: Stable profile identity and migration boundaries should leave room for multiple profiles or optional cloud synchronization later, while permanent offline and guest play remain complete.

## D-164 — Polished core audio with localization-ready text

- Status: accepted
- Choice: B — polished MVP audio and English-first localization-ready text
- Accepted requirement: Produce convincing sounds for player movement, boosting, six-to-eight weapons, impacts, enemy roles, warnings, factory machinery, interface actions, rewards, strongboxes, and other important feedback.
- Music rule: Include one suitable music track or a small bounded adaptive setup sufficient to judge the level's pacing and atmosphere.
- Voice rule: Use text-based mission communication for the internal MVP; full voice acting is not required.
- Localization rule: English is authored first, but all player-facing text uses stable localization keys and a localization-ready presentation path rather than hard-coded strings scattered through scenes and scripts.

## D-165 — Mainstream gaming-PC performance baseline

- Status: accepted
- Choice: B — stable 60 FPS at 1080p on an ordinary dedicated gaming GPU
- Accepted requirement: The Windows MVP targets stable 60 FPS at 1920×1080 on a mainstream gaming PC while supporting scalable quality settings for weaker systems.
- Budget rule: Establish measurable budgets for enemies, projectiles, dynamic lights, shadows, particles, audio voices, memory, loading, and frame-time spikes.
- Fallback rule: Lower quality profiles must be able to reduce or disable expensive lighting, shadow, particle, post-processing, and presentation features without damaging gameplay readability.
- Future rule: A stricter Android profile is deferred, but content and effect systems should expose the controls needed to build one later.

## D-166 — Structured local diagnostics and exportable playtest bundles

- Status: accepted
- Choice: B — privacy-safe structured local evidence
- Accepted requirement: Record structured local events needed for debugging and internal evaluation, including mission progression, deaths, weapon usage, rewards, errors, settings, build identity, and meaningful performance spikes.
- Export rule: Testers can create a bounded diagnostic bundle containing logs, relevant configuration, hardware or build metadata, and optional run summaries suitable for attaching to a report.
- Privacy rule: Diagnostics remain local by default, use retention limits, and exclude or redact unrelated personal information.
- Expansion rule: The event model may later feed opt-in online crash reporting, analytics, or replay tooling, but no remote telemetry backend is required for the internal MVP.

## D-167 — Interconnected authored room map

- Status: accepted as a custom structure inspired broadly by `Robokill` and `Red Storm`
- Accepted requirement: The factory is constructed as an interconnected map of authored combat and traversal rooms. The player moves through visible room connections rather than following only one corridor-like sequence.
- Map rule: The map shows explored rooms, discovered connections, current position, major objectives, and other navigation information required to traverse the facility confidently.
- Exploration rule: The critical route remains understandable while optional branches and rooms may contain strongboxes, currency, challenges, shortcuts, or other bounded rewards.
- Production rule: Rooms should be modular enough to author, test, own, and integrate independently while still forming one cohesive handcrafted level.

## D-168 — Cleared rooms remain cleared

- Status: accepted
- Choice: A — permanent room clearing within the run
- Accepted requirement: Once a room's enemy encounter has been defeated, that room remains free of enemies for the rest of the current mission run unless the entire relevant checkpoint state is rolled back after death.
- Traversal rule: Normal backtracking through cleared rooms is safe from renewed enemy attacks.
- Hazard rule: Persistent environmental hazards, moving machinery, damaging floor zones, traps, or similar environmental variables may still injure the player in a cleared room.
- Readability rule: Environmental danger must remain clearly telegraphed so an apparently cleared room does not produce arbitrary damage.

## D-169 — Respawn at the latest checkpoint

- Status: accepted
- Choice: B — return to the latest activated respawn point
- Accepted requirement: On ordinary campaign difficulties, death respawns the player at the most recent activated checkpoint rather than restarting only the room or the entire mission.
- State rule: Progress made after that checkpoint is subject to rollback according to a separately defined checkpoint-snapshot policy.
- Difficulty rule: Full-mission restart or similarly severe failure rules may be added after the MVP for nightmare, sadistic, challenge, achievement, or record-attempt modes.
- Co-op rule: The checkpoint and mission-state model should be capable of supporting a future cooperative party respawn flow without implementing co-op during the offline MVP.

## D-170 — Frequent automatic teleport checkpoints

- Status: accepted as a custom checkpoint model
- Choice: custom — automatic teleports placed approximately every six or seven rooms
- Accepted requirement: The first level contains clearly recognizable teleport installations that automatically activate as the player reaches them and become the latest respawn point.
- Cadence rule: Teleports should be fairly frequent, with an initial design target of roughly one teleport per six or seven rooms, adjusted during playtesting for actual room length and difficulty.
- Map rule: Activated and upcoming teleport locations must be legible on the room map and in the environment.
- Inspiration note: The cadence and feel may draw broad inspiration from `Robokill`, but implementation, presentation, terminology, assets, rules, and balancing remain original to this project.
- Deferred rule: Whether activated teleports also support fast travel, loadout access, healing, extraction, or other services is decided separately.

## Batch persistence

- Persisted through: D-170
- Unsaved decisions after checkpoint: 0
- Next batch boundary: D-180
