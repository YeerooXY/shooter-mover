# Resume Shooter Mover Product Discovery

Continue the Shooter Mover Product Discovery from committed repository state.

## Required startup sequence

Before responding:

1. Read `AGENTS.md` completely.
2. Read `project_workspace.json`.
3. Read `assembly/context/CURRENT_HANDOFF.json`.
4. Verify repository `YeerooXY/shooter-mover`, branch `assembly/bootstrap-shooter-mover`, and the recorded commit state.
5. Read every file listed in `authoritative_artifacts`.
6. Read the complete Intake Interviewer prompt from `assembly/prompts/00-intake-interviewer.md`.
7. Continue from the exact `next_action` in `CURRENT_HANDOFF.json`.

## Current checkpoint

- Product Discovery is active after verified decision D-130.
- Decisions D-101 through D-110 were batch-persisted in:
  - `assembly/intake/batches/LIVE_DECISIONS_D101_D110.md`
  - `assembly/intake/batches/intake_session_D101_D110.json`
- Decisions D-111 through D-120 were batch-persisted in:
  - `assembly/intake/batches/LIVE_DECISIONS_D111_D120.md`
  - `assembly/intake/batches/intake_session_D111_D120.json`
- Decisions D-121 through D-130 were batch-persisted in:
  - `assembly/intake/batches/LIVE_DECISIONS_D121_D130.md`
  - `assembly/intake/batches/intake_session_D121_D130.json`
- Normal enemy pressure uses readable aimed attacks, melee, flanking, and area denial, with bounded dense barrages reserved for selected units, elites, events, and bosses.
- Encounters begin from authored visible formations with limited deterministic and telegraphed reinforcements.
- Combat lockdowns are selective rather than universal. Ordinary encounters usually allow retreat, while major fights may clearly seal exits.
- Enemy pursuit is role-based and bounded. Hunters may chase; defensive and objective-bound units usually hold their encounter area; shops and checkpoints remain safe.
- Specialised ranged enemies may predict current movement, but cannot foresee sudden boost reversals. Selected attacks track during wind-up and visibly lock before firing.
- Ordinary projectiles respect cover, with clearly communicated arcing, piercing, ricocheting, cover-destroying, or area-denial exceptions.
- Only selected physical threats such as rockets, mines, drones, or slow plasma spheres are destructible. Exact interception tuning is deferred.
- The user explicitly requested that discovery return to higher-level topics before more detailed combat rules.
- The campaign has a light but meaningful narrative delivered through concise, replay-friendly methods.
- Overall campaign structure uses a central hub with controlled branching main and optional missions rather than an open world.
- The user requested batching future repository persistence every ten discovery questions; queue D-131 through D-140 and persist at D-140 unless this instruction changes.
- `unsaved_decisions` is zero at this checkpoint.
- No pull request is open yet.
- Decisions D-001 through D-039 remain an unverified recovered set.
- Decisions D-040 through D-063 are archived verbatim in `assembly/intake/archive/LIVE_DECISIONS_THROUGH_D063.md`.
- Decisions D-064 through D-100 are in `assembly/intake/LIVE_DECISIONS.md`.

## Non-negotiable behavior

- Do not reconstruct state from chat memory.
- Do not repeat already accepted questions.
- Do not present D-001 through D-039 as final until explicitly re-verified.
- Ask exactly one highest-impact Product Discovery question per turn.
- Use a concise A/B/C decision card with pros, cons, MVP risk, scaling/refactor risk where relevant, and one recommendation placed after all options.
- If the user gives a short clear choice, record it without extended praise or repetition.
- Honour the user-requested ten-question persistence batch: track accepted D-131 through D-140 in the active conversation, then commit the full batch before asking D-141. Persist earlier if a context handoff becomes necessary or the user requests it.
- Keep already committed checkpoints at `unsaved_decisions: 0`; while an explicit batch is in progress, clearly track the queued decision range and never claim queued decisions are committed.
- Prioritize high-level product identity, protagonist, hub, world, campaign scope, platform, participation, and MVP-boundary decisions before returning to micro-level combat tuning.
- Preserve the complete offline campaign and permanent guest-play requirements.
- Treat advanced ricochet extensions as prototype-dependent and defer deep commitment until a usable core build exists.
- Treat exact combat timing, projectile health, boost coefficients, collision coefficients, and similar numeric details as later prototype-and-playtest variables unless explicitly fixed.
- Do not choose a stack until product identity, users, core experience, connectivity, participation, platform scope, and MVP boundary are sufficiently clear.

## Expected first action

Ask D-131 exactly as directed by `CURRENT_HANDOFF.json`: decide the player protagonist and mech identity model.

Do not begin with another detailed projectile, collision, AI-timing, or weapon-handling question. D-131 onward should remain at the higher product and campaign level until those major layers are sufficiently established.