# Shooter Mover — Live Decisions

Status: recovery and Product Discovery log. Final acceptance occurs through the requirements/bootstrap pull request.

## Persistence status

- Active branch: `assembly/bootstrap-shooter-mover`
- Last persisted decision: D-040
- Unsaved accepted decisions: 0

## Recovery note

Decisions D-001 through D-039 were reconstructed from the surviving chat transcript and preserved in `RECOVERED_INTAKE_DRAFT.md`. They require section-by-section re-verification before becoming final requirements.

D-040 was re-verified directly by the user after recovery.

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

## Next discovery state

Do not resume from the previously invented objective-guidance question. It was identified as a new question rather than part of the lost original sequence.

Continue by selecting the highest-weight unresolved Product Discovery question from the recovered open areas. Major unresolved scope questions such as target users, participation, connectivity, target platforms, and MVP technical boundaries outrank additional low-level level-design details.

## Revision rules

- Never rewrite history silently.
- Mark changed decisions as superseded and add a new entry.
- Do not record unchosen options as requirements.
- Keep reconstructed decisions unverified until confirmed.
- Commit every newly accepted answer before asking the next question.
