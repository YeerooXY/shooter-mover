# Resume This Project

Continue this project from committed repository state.

Before responding:

1. Read repository `AGENTS.md` completely.
2. Read `project_workspace.json`.
3. Read `assembly/context/CURRENT_HANDOFF.json`.
4. Verify the recorded repository, branch, commit, and pull-request state.
5. Read every file listed in `authoritative_artifacts`.
6. Read the complete active-role prompt from `assembly/prompts/`.
7. Continue from the exact recorded `next_action`.

Rules:

- Do not reconstruct project state from chat memory.
- Do not repeat already accepted questions or completed tasks.
- Do not infer missing answers.
- Treat reconstructed material as unverified until the user confirms it.
- Follow the repository's response envelope and personality-resistant operating rules.
- Persist every newly accepted decision or completed task before advancing.
- If handoff validation fails, repair or report the exact stale state before continuing.
