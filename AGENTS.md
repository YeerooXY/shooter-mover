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
- Planning merged, no accepted stage backlog: Task Splitter
- Stage backlog merged: Dispatch / Task Executor
- Stage 1 gate passed and Stage 2 plan amended: Stage 2 Task Splitter

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
  -> Stage 1 task-splitting and dependency-audit PR merge
  -> Stage 1 canonical backlog
  -> Stage 1 development waves
  -> Stage 1 evidence gate
  -> evidence-backed Stage 2 amendment and task split
  -> Stage 2 development
```

Do not blur stages.

## Durable-state rule

Chat is temporary. Git is durable. Pull requests are the approval boundary. Merged files are authoritative.

## Stage-first task-decomposition approval gate

Stage 1 may become the canonical backlog after all nine Stage 1 batches validate, their dependency graph is acyclic, their path ownership is conflict-free, and any milestone-cap pressure has a recorded human amendment.

The seven Stage 2 batch ranges are long-range planning only. Preserve them in a deferred artifact, but do not generate Stage 2 task files, add them to the canonical backlog, or mark their slots ready before `GATE-010` records a genuine signed advance decision with `stage2_unlocked=true`.

Every task-decomposition or stage-transition change uses a fresh branch from current `main`, executable validation, deterministic handoff updates, and a pull request. A merged branch is permanently closed. Before every write, inspect the branch PR state and compare it with current `main`.
