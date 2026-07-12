# Shooter Mover — Live Decisions

Status: recovery and Product Discovery log. Final acceptance occurs through the requirements/bootstrap pull request.

## Persistence status

- Active branch: `assembly/bootstrap-shooter-mover`
- Last persisted decision: D-046
- Unsaved accepted decisions: 0

## Recovery note

Decisions D-001 through D-039 were reconstructed from the surviving chat transcript and preserved in `RECOVERED_INTAKE_DRAFT.md`. They require section-by-section re-verification before becoming final requirements.

D-040 through D-046 were verified directly by the user after recovery.

## Decision log

### D-001–D-039 — Recovered gameplay decisions

- Status: unverified recovery set
- Accepted requirement source: `assembly/intake/RECOVERED_INTAKE_DRAFT.md`
- Rule: do not present any reconstructed detail as final until the user verifies it.
- Source: surviving guided-intake transcript

### D-040 — Map system

- Status: accepted
- Choice: B — fog-of-war exploration map
- Accepted requirement: Rooms, corridors, and connections reveal as the player explores. Discovered checkpoints, shops, and relevant objectives may appear; hidden rooms and undiscovered secrets remain hidden.
- User-supplied constraints: Keep additional objective guidance light and avoid GPS-style navigation.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-041 — Primary target-player strategy

- Status: accepted
- Choice: B — core-action-first, broadly accessible
- Accepted requirement: Design primarily for players who enjoy skill-based top-down combat, while providing many meaningfully tailored difficulty settings so casual, intermediate, expert, and mastery-focused players can all have an appropriate experience.
- User-supplied constraints: Difficulty options should genuinely tailor the game for different player types rather than merely applying simple health or damage multipliers.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-042 — Core connectivity model

- Status: accepted
- Choice: B — offline core with optional online features
- Accepted requirement: The complete MVP campaign, saves, progression, and difficulty modes must work offline. Optional online systems may be added later without making the core game dependent on connectivity.
- User-supplied constraints: Multiplayer is a post-MVP goal and should be pursued only after the MVP proves successful.
- Design implication: Keep clean extension boundaries for later online services and multiplayer, but do not include their operational complexity in the MVP.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-043 — Primary post-MVP multiplayer experience

- Status: accepted
- Choice: A — real-time cooperative play first
- Accepted requirement: After a successful single-player MVP, the first major multiplayer direction should be real-time cooperative play through campaign-style combat, exploration, objectives, and bosses.
- User-supplied constraints: Leaderboards are also desired later, but they remain post-MVP and do not replace cooperative multiplayer as the primary multiplayer direction.
- Design implication: MVP systems should avoid unnecessarily hard-coding a single-player-only architecture, while no multiplayer networking, scaling, shared progression, or leaderboard service is included in the MVP scope.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-044 — Target platform order

- Status: accepted
- Choice: A — Windows PC first
- Accepted requirement: Build and release the polished MVP for Windows PC first with keyboard/mouse and gamepad support. Android is the next intended platform after the PC version is stable; other platforms may follow later.
- User-supplied constraints: Keep gameplay actions platform-neutral, use scalable UI, support suspend saves, and avoid needless PC-specific assumptions so later mobile expansion remains practical.
- Design implication: Touch controls, mobile performance validation, and app-store release work are post-MVP rather than simultaneous MVP requirements.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-045 — Level size and expected session length

- Status: accepted
- Choice: B — substantial, checkpoint-segmented levels
- Accepted requirement: Each MVP level should target roughly 45–75 minutes for an exploratory first clear and 20–35 minutes for a mastered run, divided into natural 10–20 minute checkpoint sections.
- User-supplied constraints: Every level must remain interesting throughout and should later be genuinely worth replaying and grinding rather than becoming repetitive filler.
- Design implication: Level production must prioritize distinct encounters, routes, secrets, objectives, progression opportunities, and mastery depth over raw floor area or inflated runtime.
- Supersedes: none
- Source: guided Product Discovery recovery

### D-046 — Repeat-run variation model

- Status: accepted
- Choice: B — stable levels with controlled variations
- Accepted requirement: Core layouts, bosses, major encounters, and official competitive categories remain deterministic, while optional curated modifiers, difficulty-specific enemy variants, challenge contracts, rotating bonuses, and limited reward variation may refresh repeat runs.
- User-supplied constraints: Preserve learnable mastery and fair speedrunning while making repeated play meaningfully varied.
- Design implication: Repeat-run variation must be authored and bounded rather than turning the campaign into heavily randomized roguelike runs.
- Supersedes: none
- Source: guided Product Discovery recovery

## Guided intake presentation preference

- Place the agent recommendation after all A/B/C options, at the end of each decision card.

## Next discovery state

Continue with the highest-weight unresolved Product Discovery question. The repeat-run reward and economy model now outranks other remaining level-design details because it determines whether grinding feels rewarding without becoming mandatory or exploitative.

## Revision rules

- Never rewrite history silently.
- Mark changed decisions as superseded and add a new entry.
- Do not record unchosen options as requirements.
- Keep reconstructed decisions unverified until confirmed.
- Commit every newly accepted answer before asking the next question.