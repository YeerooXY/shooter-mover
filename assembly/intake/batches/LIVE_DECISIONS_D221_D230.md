# Shooter Mover — Product Discovery Batch D-221 through D-230

Status: authoritative verified extension to `assembly/intake/LIVE_DECISIONS.md`.

## D-221 — Layered Unity CI and build validation

- Status: accepted
- Choice: C — layered validation with escalating gates
- Accepted requirement: Ordinary branches run compilation, fast deterministic edit-mode tests, content validation, generated-registry checks, and tests targeted to affected systems.
- Integration gate: Before integration, run relevant play-mode smoke tests and a Windows player smoke build covering launch, menu, input, room load, combat, save, resume, and clean exit.
- Milestone gate: Scheduled or milestone validation runs full regression, representative performance and memory scenes, save interruption, recovery and migration checks, clean-machine installation, offline launch, and accessibility/settings persistence.
- Quality rule: Required checks retain identifiable artifacts and actionable diagnostics. Flaky tests are quarantined and repaired rather than repeatedly rerun until green.
- Deferred pipelines: Android, networking, and complete co-op CI remain deferred until those workstreams begin.

## D-222 — Immutable promoted Windows artifacts

- Status: accepted
- Choice: C — immutable artifacts promoted through release channels
- Accepted requirement: Every candidate artifact records build and content versions, source commit, Unity version, dependency and generated-content fingerprints, validation reports, symbols, checksums, and save-schema compatibility.
- Promotion rule: The exact same artifact moves through developer, internal-test, external-test, release-candidate, and public channels.
- Change rule: Any code, content, configuration, or dependency change creates a new artifact.
- Retention rule: Keep the current formal or public build, the previous known-good build, relevant migration tools, and a save-safe rollback procedure.
- Scope rule: Formal external Stage 1 tests and Stage 2 milestones use identifiable immutable artifacts even when earlier prototypes use a lighter process.

## D-223 — Privacy-safe local diagnostics

- Status: accepted
- Choice: C — local diagnostics with explicit tester export
- Accepted requirement: Bounded local logs record build, content and save-schema versions, major session and mission outcomes, deaths, restarts, checkpoints, banking, loadout changes, performance failures, exceptions, asset failures, and save/journal recovery outcomes.
- Privacy rule: Logs remain local by default; testers explicitly choose when to export or submit them. Personal identifiers, unrelated system information, and sensitive paths are excluded or redacted.
- Support rule: Logs rotate automatically, and a one-click support bundle combines diagnostics, configuration, build identity, and optional tester notes.
- Future rule: Remote telemetry remains a future opt-in consideration only if project scale justifies its privacy, security, hosting, and retention costs.

## D-224 — Versioned migrations with protected rollback

- Status: accepted
- Choice: C — explicit versioned migration envelope
- Accepted requirement: Every save records game, content and schema versions, applied migration history, and stable content identifiers.
- Migration rule: Newer builds create immutable backups, validate the snapshot and journal, apply ordered idempotent tested migrations, and atomically commit only after success.
- Safety rule: Failed or interrupted migrations restore safely. Older builds refuse to modify newer-schema saves.
- Rollback rule: Rollback uses cloned saves or compatible backups rather than destructive downgrade.
- Removal rule: Removed content uses stable-ID tombstones, replacements, refunds, or explicit compensation rules.
- Development tolerance: Announced development-channel resets may occur only after archiving or exporting old profiles; public compatibility promises remain explicit and tested.

## D-225 — Capability-gated diagnostic commands

- Status: accepted
- Choice: C — auditable diagnostic command framework with build-channel capabilities
- Accepted requirement: Developer and approved internal builds expose deterministic room warps, loadout setup, enemy spawning, state inspection, performance overlays, and fault injection.
- External-test rule: Formal test artifacts expose only commands needed for the round through hidden or controlled activation and log every use.
- Evidence rule: Sessions altered by commands are excluded from ordinary fun, achievement, record, and challenge evidence.
- Public rule: Public builds exclude progression-altering commands and retain only safe support diagnostics through an explicit workflow.
- Save rule: Save-changing diagnostics use temporary or cloned profiles rather than silently changing the primary profile.
- Product rule: Practice, accessibility, level selection, and future speedrun tools are legitimate player-facing systems rather than disguised cheats.

## D-226 — Pinned dependencies with controlled upgrades

- Status: accepted
- Choice: C — pinned baseline and dedicated upgrade windows
- Accepted requirement: Use one pinned Unity LTS editor version, locked package versions, reproducible configuration, and an approved dependency inventory recording license, source, version, and purpose.
- Environment rule: Reject incompatible editor or package versions and disable automatic upgrades.
- Upgrade rule: Every upgrade uses an isolated task and branch with compilation, content, build, save, performance, smoke, visual, and representative asset-reimport validation plus rollback.
- Dependency rule: Prefer small maintained dependencies behind project-owned adapters and avoid unnecessary broad frameworks.
- Scope rule: Do not introduce analytics, storefront, networking, or mobile SDKs before those workstreams begin.
- Reproducibility rule: Retain enough dependency metadata to reproduce promoted artifacts.

## D-227 — Explicit Windows performance tiers

- Status: accepted
- Choice: C — primary target, minimum floor, and scalable effects
- Accepted requirement: The primary target sustains stable 60 FPS at 1080p during representative heavy combat with intended four-weapon, enemy, projectile, lighting, destruction, and effect density.
- Minimum-floor rule: A declared minimum machine must complete the campaign readably using reduced visual settings without changing gameplay, input, saves, collision, simulation, or progression.
- High-end rule: Higher-end settings add only cosmetic quality, resolution, and refresh-rate headroom without gameplay advantage.
- Settings rule: Expose effect density, shadows, lighting, debris, shake, flashes, post-processing, resolution, display mode, frame cap, VSync, and transparent presets.
- Validation rule: Use a small hardware matrix covering the primary target, minimum floor, another GPU or driver family, and clean Windows installations before major milestones.
- Budget rule: Track CPU, GPU, allocation, memory, loading, atlas, particle, enemy, and projectile budgets; real hardware remains authoritative.

## D-228 — Channel-specific Windows packaging

- Status: accepted
- Choice: C — immutable portable test artifacts plus conventional public delivery
- Test-build rule: Internal and formal external builds use checksummed immutable ZIP artifacts, clear build identity, side-by-side versions, standard external save locations, and optional isolated or portable test profiles.
- Public rule: Use a primary storefront or conventional installer with optional updates while preserving complete offline launch after installation and requiring no account for campaign play.
- Architecture rule: Storefront integration remains behind adapters. A DRM-free or manual package may be offered when commercially practical.
- Update safety: Updates do not delete or silently relocate saves; failed updates leave the previous build launchable or recoverable.
- Uninstall rule: Game-file removal preserves saves unless the player separately confirms save deletion.
- Trust rule: Public artifacts should eventually be code-signed; formal test artifacts at minimum use checksums and unambiguous build identification.

## D-229 — Trustworthy least-privilege offline application

- Status: accepted
- Choice: C — practical security without invasive DRM
- Accepted requirement: Normal play requires no administrator privileges and writes only to documented game-data, save, cache, and log locations.
- Security rule: Never ship credentials, signing keys, or service secrets. Validate imported files, saves, journals, archives, and support bundles defensively.
- Privacy rule: Sanitize personal information and file paths from exported diagnostics.
- Supply-chain rule: Track dependencies, licenses, vulnerabilities, and a lightweight dependency manifest or software bill of materials for promoted builds.
- Distribution rule: Use checksums for formal artifacts and code-sign public installers and executables when practical.
- Player-ownership rule: Avoid mandatory DRM, kernel anti-cheat, and online activation. Local save editing is not treated as a security breach in offline play.
- Competitive-integrity rule: Modified profiles may be marked ineligible for protected achievements, leaderboards, or records while the campaign remains playable.

## D-230 — Short-lived protected integration workflow

- Status: accepted
- Choice: C — short-lived scoped branches with protected integration
- Accepted requirement: Every task uses a small branch with declared scope and owned files, representing one reviewable and revertable change.
- Merge gate: Before merging, update against integration, pass layered CI, include tests, documentation, and generated review snapshots, and identify shared-system or save-schema changes explicitly.
- Discipline rule: Avoid unrelated refactors and formatting churn. Foundational systems, central balance data, build configuration, dependencies, and save formats receive stronger review.
- Ownership rule: Human and AI-agent contributions follow the same merge requirements; shared assets have explicit owners.
- Generated-content rule: Generated files are rebuilt and verified rather than manually conflict-resolved.
- Failure rule: Integration failures are fixed on the task branch rather than patched silently afterward.
- Release rule: Release artifacts are built only from protected validated commits, and merged branches are removed after integration.

## Batch persistence

- Persisted through: D-230
- Unsaved decisions after checkpoint: 0
- Next direction: define explicit milestone budgets and timebox review rules so Stage 1 and Stage 2 cannot expand indefinitely.
