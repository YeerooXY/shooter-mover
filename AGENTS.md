# Shooter Mover AI Agent Contract

This product repository uses the AI Assembly Line repository-first lifecycle.

## Start from committed state

Before ordinary implementation work, read:

1. `project_workspace.json`, `assembly/context/CURRENT_HANDOFF.json`, and
   `assembly/context/NEW_CHAT_RESUME.md`;
2. the exact task card, its dependencies, inputs, allowed areas, and required
   proof;
3. the authoritative artifacts and role guidance directly relevant to that
   task; and
4. current `main` plus relevant branch and pull-request state.

Do not require an ordinary task agent to load unrelated planning batches or
evidence files. Planning, stage-transition, wave-coordination, and formal-gate
work must still read every path listed in `authoritative_artifacts` and the
complete active-role guidance. Never reconstruct project state from remembered
chat.

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

## Streamlined Stage 1 task execution

For ordinary Stage 1 implementation tasks, the reviewed implementation pull
request is both the delivery and acceptance boundary. Do not create a second
per-task acceptance or handoff pull request.

1. Start from current `main` in a fresh branch and worktree. Never reuse a
   branch whose pull request merged.
2. Treat the task card's `depends_on` list as the dispatch authority. Its
   `blocks` list is an advisory reverse index. A dependency is satisfied when
   its implementation pull request has merged after required proof and human
   review; a separate bookkeeping merge is not required.
3. Change only the task's `allowed_areas` and inseparable Unity metadata
   permitted by the accepted ownership map. Parallel agents never edit shared
   lifecycle bookkeeping from their implementation branches.
4. Put the task ID, dependency check, exact changed paths, automated results,
   required manual proof, known limitations, and rollback note in the pull
   request. Keep the pull request draft until required proof is complete.
5. Nemo's intentional merge of a proof-complete task pull request records
   acceptance. A closed-unmerged pull request, draft, failed check, unresolved
   review finding, or premature merge does not satisfy a dependency.
6. Ordinary tasks do not create `task_runs`, duplicate submissions/reviews, or
   post-merge acceptance pull requests unless the task explicitly requires a
   repository evidence artifact or the human lead requests stronger review.
7. A coordinator may reconcile `CURRENT_HANDOFF.json`, collaboration state,
   slots, and resume notes once per development wave. Wave bookkeeping is for
   discoverability and may lag merged task pull requests by one wave; it is not
   a dependency gate.

Full repository evidence remains mandatory where the task requires it,
especially milestone gates, persistence/migration, shared serialized assets,
build/release artifacts, and other explicit strong-review boundaries. This
streamlining never weakens path ownership, automated tests, manual/playable
proof, human merge authority, milestone caps, or the Stage 2 gate.

## Stage-first task-decomposition approval gate

Stage 1 may become the canonical backlog after all nine Stage 1 batches validate, their dependency graph is acyclic, their path ownership is conflict-free, and any milestone-cap pressure has a recorded human amendment.

The seven Stage 2 batch ranges are long-range planning only. Preserve them in a deferred artifact, but do not generate Stage 2 task files, add them to the canonical backlog, or mark their slots ready before `GATE-010` records a genuine signed advance decision with `stage2_unlocked=true`.

Every task-decomposition or stage-transition change uses a fresh branch from current `main`, executable validation, deterministic handoff updates, and a pull request. A merged branch is permanently closed. Before every write, inspect the branch PR state and compare it with current `main`.
