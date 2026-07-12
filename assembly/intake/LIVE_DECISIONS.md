# Shooter Mover — Live Decisions

Status: recovery and Product Discovery log. Final acceptance occurs through the requirements/bootstrap pull request.

## Persistence status

- Active branch: `assembly/bootstrap-shooter-mover`
- Last persisted decision: D-041
- Unsaved accepted decisions: 0

## Recovery note

Decisions D-001 through D-039 were reconstructed from the surviving chat transcript and preserved in `RECOVERED_INTAKE_DRAFT.md`. They require section-by-section re-verification before becoming final requirements.

D-040 and D-041 were verified directly by the user after recovery.

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

## Guided intake presentation preference

- Place the agent recommendation after all A/B/C options, at the end of each decision card.

## Next discovery state

Continue with the highest-weight unresolved Product Discovery question. Connectivity and participation scope now outrank lower-level level-design details, economy, story, and technical stack selection.

## Revision rules

- Never rewrite history silently.
- Mark changed decisions as superseded and add a new entry.
- Do not record unchosen options as requirements.
- Keep reconstructed decisions unverified until confirmed.
- Commit every newly accepted answer before asking the next question.
