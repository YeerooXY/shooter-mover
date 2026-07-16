# Wave 1 dispatch index

Wave 0 is complete:

- `AUD-001` merged through PR #130.
- `ADR-001` merged through PR #132.
- `DEMO-001` merged through PR #131.

Exact shared base for this wave:

`0e678a9333956aa29ba2e3598265c8e1a4122e72`

All six tasks may be dispatched in parallel. Their implementation paths do not
overlap.

| Task | Agent type | Main output |
|---|---|---|
| `OBJ-001` | GitHub web/coding agent | Placed identity, scope, variants, capabilities, and overrides |
| `REW-001` | GitHub web/coding agent | Reward and economy contract vocabulary |
| `EQP-001` | GitHub web/coding agent | Equipment and augment model/definitions |
| `RNG-001` | GitHub web/coding agent | Deterministic PRNG and soft progression mathematics |
| `LED-001` | GitHub web/coding agent | Shared typed idempotent ledger primitive |
| `PRG-001` | GitHub web/coding agent | Explicit progression context and providers |

## Important ownership clarification

The roadmap's broad `RNG-001` progression path is narrowed in this wave to
`Progression/Curves/**` and random-specific paths. `PRG-001` exclusively owns
`Progression/Context/**`. Neither agent may edit the other subtree.

## Dispatch rules

- Give one prompt to each isolated agent.
- Every branch starts from the exact base above.
- Every PR targets `main` and remains draft until its required proof is complete.
- If an agent cannot execute Unity, it must still add the required tests, run
  available repository validation, state exactly what was not executed, and
  leave the PR draft for coordinator proof.
- No Wave 1 task edits Stage 1 scenes, controllers, prefabs, existing gameplay
  packages, project settings, package manifests, handoff files, or generated
  outputs.
- Do not dispatch Wave 2 consumers until all of their named Wave 1 dependencies
  have merged.

Prompt files:

- [OBJ-001](OBJ-001_WEB_AGENT.md)
- [REW-001](REW-001_WEB_AGENT.md)
- [EQP-001](EQP-001_WEB_AGENT.md)
- [RNG-001](RNG-001_WEB_AGENT.md)
- [LED-001](LED-001_WEB_AGENT.md)
- [PRG-001](PRG-001_WEB_AGENT.md)
