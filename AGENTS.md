# Shooter Mover AI Agent Contract

This product repository uses the AI Assembly Line repository-first lifecycle.

## Start from committed state

Before responding, read:

1. `project_workspace.json`
2. `assembly/context/CURRENT_HANDOFF.json`
3. `assembly/context/NEW_CHAT_RESUME.md`
4. every path listed in `authoritative_artifacts`
5. the complete active-role prompt under `assembly/prompts/`
6. relevant branch and pull-request state

Do not reconstruct project state from remembered chat.

## Active lifecycle routing

- No accepted `project_intake.json`: Product Discovery / Intake Interviewer
- Requirements merged, no planning package: Planning Agent
- Planning merged, no canonical backlog: Task Splitter
- Backlog merged: Dispatch / Task Executor

The current handoff selects the exact active role and next action.

## Product Discovery order

Ask the highest-impact unresolved question first:

```text
product identity
  -> target users
  -> core experience
  -> connectivity and participation
  -> other scope multipliers
  -> MVP boundary
  -> required systems
  -> stack and architecture
```

Do not continue into low-level mechanics while unresolved questions about target users, offline/online scope, solo/multiplayer scope, platform, or MVP boundary could invalidate them.

## Guided-response lock

While Product Discovery is incomplete:

1. record the accepted answer in one concise line;
2. show compact status only when materially useful;
3. ask exactly one A/B/C decision card or one focused question;
4. stop.

If the user answers only `A`, `B`, `C`, `recommended`, or another clear short selection, do not praise, re-explain, summarize, or expand it. Persist it and move directly to the next question.

Decision cards should include concise pros, cons, MVP risk, later scaling/refactor risk when relevant, and one recommendation that may be a hybrid.

## Per-decision persistence

Before asking the next question, update and commit:

- `assembly/intake/LIVE_DECISIONS.md`
- `assembly/intake/intake_session.json`
- `assembly/context/CURRENT_HANDOFF.json`
- `assembly/context/NEW_CHAT_RESUME.md` when its instructions change

Unsaved accepted decisions must remain at zero. Chat-only state is not durable.

## Recovery boundary

Decisions D-001 through D-039 are recovered but not fully re-verified. Do not silently promote reconstructed details into final requirements.

D-040 is verified: fog-of-war exploration map with light objective guidance and no automatic secret revelation.

If a recovered decision becomes relevant, either rely only on the clearly preserved requirement or ask the user to re-verify it.

## Lifecycle gates

```text
Product Discovery
  -> requirements/bootstrap PR merge
  -> planning and architecture PR merge
  -> task-splitting and dependency-audit PR merge
  -> canonical backlog
  -> development waves
```

Do not blur stages.

## Durable-state rule

Chat is temporary. Git is durable. Pull requests are the approval boundary. Merged files are authoritative.

## Task-decomposition approval gate

During the remaining Shooter Mover task split, every batch uses two turns:

1. Before writing files, present the proposed stable task IDs, titles, owner lane, and exact dependencies to the human lead. Then stop.
2. Only after explicit human continuation, create a fresh branch from current `main`, generate that one approved batch, update the index and deterministic handoffs atomically, run the executable validators, open a draft continuation PR, report it, and stop.

A merged branch is permanently closed. Before every write, inspect the branch PR state and compare it with current `main`. Never append commits to a merged branch or silently continue from a branch that is behind `main`.
