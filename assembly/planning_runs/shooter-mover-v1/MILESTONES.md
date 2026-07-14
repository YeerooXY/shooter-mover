# Evidence Milestones and Hard Review Caps

## 1. Budget unit

A **focused lead day** is one substantial human-led implementation, review, integration, or formal-validation day. AI runtime does not create free scope: generated work counts when it consumes review, correction, integration, or evidence effort.

Each milestone has both an effort cap and a calendar cap. Reaching either pauses new work and triggers the written review from D-231. Extensions require a specific dependency/evidence reason, a revised cap, and unchanged acceptance evidence.

Calendar caps are review triggers, not delivery promises.

## 2. Stage 1 — intrinsic game-feel proof

| Milestone | Product question and included scope | Effort cap | Calendar cap |
|---|---|---:|---:|
| S1.0 Foundation and evidence harness | reproducible resettable prototype; pinned Unity baseline, input actions, build identity, diagnostics, arena shell, quick restart | 12 lead days | 3 weeks |
| S1.1 Movement and thruster | responsive locomotion, charges, regeneration, steering, forgiveness, exit momentum, basic wall reflection, collision safeguards, HUD state | 8 lead days | 2 weeks |
| S1.2 Four-weapon combat | shared aim, independent mounts, five representative weapons, numeric-only empowered profiles, movement interactions, combat HUD, temporary effects/audio | 10 lead days | 2 weeks |
| S1.3 Enemies and short route | four simple ordinary roles, one easy Four-Blaster Elite, arena encounters, interconnected route, hazards, quick loadout selection, reduced-effects controls | 10 lead days | 2 weeks |
| S1.4 Reliability and developer gate | repeated complete sessions, settings persistence, diagnostics, performance pass, developer behavioral gate | 5 lead days | 1 week |
| S1.5 External formal round | immutable artifact, frozen population/method, sessions, interviews, evidence report | 5 lead days | 2 weeks |

**Stage 1 aggregate cap:** 50 focused lead days or 12 calendar weeks.

The 2026-07-14 Stage 1-first capacity amendment raised S1.0 from 5 to 12 focused lead days. Its 10.9-day direct estimate leaves 1.1 days for review and integration without consuming the reserves of later evidence-critical milestones.

### Technical validity

Before formal external testing:

- ten consecutive developer sessions complete without crash, progression blocker, broken restart, unusable controls, or lost settings;
- no known defect routinely prevents completion;
- representative combat stays near target frame rate and major spikes are logged;
- sessions record build/tuning identity, loadout, restarts, deaths, completion, and invalidating commands.

A formal round is invalid and rerun if more than 20% of scheduled sessions are technically invalid. Invalid sessions are reliability failures, never negative fun votes.

### Developer behavioral gate

After the frozen build is no longer being played merely to verify fixes:

- at least six voluntary additional runs across three separate days;
- at least two loadouts;
- at least one run specifically pursuing better route, execution, time, score, or challenge;
- recurring frustration does not cause avoidance.

### External round v1

Recruit 6–10 target players. Exclude close collaborators primarily motivated by helping. Each receives the same immutable build and evidence rules.

Pass signals:

- at least 60% voluntarily begin another meaningful run or loadout trial after the required first activity;
- at least 40% complete an additional run or independently request another session within seven days;
- at least 50% try a different loadout, route, challenge, or record-improvement attempt;
- no single recurring readability, control, or frustration issue affects a majority strongly enough to suppress replay.

Behavior decides; interviews explain. Threshold changes require a new versioned round before evidence collection.

### Failure policy

Allow at most two failed formal Stage 1 rounds before written diagnosis. Then allow one substantial evidence-backed pivot with a fresh gate. If that fails, stop or shelve rather than entering Stage 2.

## 3. Stage 2 — complete factory and production proof

| Milestone | Product question and included scope | Effort cap | Calendar cap |
|---|---|---:|---:|
| S2.0 Architecture and contracts | assemblies, stable IDs, commands/events, package contract, generated registry/review format, bootstrap, debt register | 8 lead days | 2 weeks |
| S2.1 Mission state and continuity | `MissionRunState`, projections, checkpoints, rollback classification, snapshots, journal, backups, migration, fault injection | 12 lead days | 3 weeks |
| S2.2 Factory combat content | 24-room graph, five ordinary roles, elite, upgraded droid, eight weapons, objectives, hazards, optional branches, teleports/storage | 15 lead days | 4 weeks |
| S2.3 Connected replay loop | menu, loadout, shop, run refreshes, currency, strongboxes, duplicates, reward review, records, immediate replay | 12 lead days | 3 weeks |
| S2.4 Accessibility, diagnostics, performance, builds | settings, support bundles, quality profiles, stress scenes, layered CI, immutable artifacts, clean-machine smoke | 10 lead days | 3 weeks |
| S2.5 Art and content-pipeline proof | representative final subset, documentation, provenance, generated review, isolated reproduction | 12 lead days | 3 weeks |
| S2.6 Reliability and readiness | regression, repeated runs, save/hardware matrices, throughput evidence, debt exit, cost/scope review | 8 lead days | 2 weeks |

**Stage 2 aggregate cap:** 77 focused lead days or 20 calendar weeks.

## 4. Stage 2 acceptance

Before production-readiness review:

- twelve consecutive complete factory runs across at least two clean Windows installations have no routine crash, blocker, lost secured reward, duplicate grant, unrecoverable save, or impossible mission state;
- interruption tests pass for snapshots, journal, checkpoints, banking, reward pickup/open/grant, completion, migrations, and suspend/resume;
- every stable ID resolves and invalid definitions/registry drift fail validation;
- primary hardware sustains representative 1080p/60 and minimum hardware completes readably at reduced visuals;
- settings/accessibility survive restart without changing hidden rules;
- one new room/encounter, enemy, and weapon use documented packages without foundational rewrites;
- an isolated contributor reproduces a representative addition through normal review;
- no blocking prototype debt remains in authority, persistence, IDs, generation, input, diagnostics, performance, or builds;
- written readiness review covers fun evidence, reliability, throughput, art pipeline, performance, scope, review capacity, cost, and expand/stop rationale.

## 5. Cap review record

Record the frozen question/version, completed and invalid evidence, overrun cause, D-232 scope cuts, resequencing, requested extension/revised cap, pivot/stop recommendation, human approver, and date. Deferred breadth cannot silently re-enter the same milestone.
