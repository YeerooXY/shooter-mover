# Prototype-debt register

## Purpose

This register makes temporary implementation shortcuts visible, owned, testable,
and time-bounded. It does not authorize a shortcut merely because the shortcut
is documented.

A shortcut may be accepted only when it is isolated, readable, testable,
deterministic, cheap to remove, and harmless to authoritative state and required
evidence. The human lead must explicitly approve it before the implementation
pull request merges.

The initial register is intentionally empty. UF-011 creates the rules and
record shape; it does not invent debt for work that has not occurred.

## Debt-ID pattern

Use `PD-NNN`, starting with `PD-001` and increasing monotonically. Never reuse an
ID, including after an entry is removed. Repository searches for `PD-[0-9]{3}`
locate registered debt references.

A task pull request references an approved entry by ID in its description,
implementation comments where the temporary boundary would otherwise be
unclear, and the detection-test name or evidence record. An ID is a traceability
link, not an exemption from architecture or policy.

## Admission and review rules

Before a shortcut can enter the active register:

1. Screen it against every never-acceptable category below. If any category is
   implicated, reject the shortcut; do not create an active debt entry.
2. Confirm the shortcut satisfies every allowed-shortcut condition in the
   purpose section.
3. Define executable or repeatable detection tests that fail, warn, or otherwise
   expose the shortcut and its boundary.
4. Name a concrete expiry milestone and an objective removal condition.
5. Obtain an explicit human review decision with reviewer, date, decision, and
   rationale.

Future entries must record the never-acceptable screening by category code. A
reference such as `NAC-03: not implicated` means the reviewer checked that
category; it never weakens, waives, or pre-authorizes it. If later evidence shows
that an approved shortcut reaches a never-acceptable category, the entry becomes
blocking immediately and the affected change must not merge or ship until the
violation is removed.

An entry is reviewed whenever an affected path changes, at its expiry milestone,
and before a milestone or Stage 2 acceptance decision. Expired or blocking debt
cannot be carried silently.

## Never-acceptable categories

These are rejection rules, not debt types that may be approved:

- **NAC-01 — Scene-owned durable mission truth.** A scene or scene component may
  project state, but it may not own durable mission authority.
- **NAC-02 — Mutable runtime state in shared ScriptableObjects.** Shared content
  assets remain definitions/configuration, never mutable runtime truth.
- **NAC-03 — Unstable or reused persistent IDs.** Persistent IDs must be stable,
  unique within their contract, and never recycled to mean a different thing.
- **NAC-04 — Non-atomic reward, banking, completion, or save transitions.** Risky
  transitions must preserve the required atomic durability boundary.
- **NAC-05 — Data loss or duplicate grants.** No shortcut may permit lost durable
  state, replayed grants, or more-than-once reward application.
- **NAC-06 — Hard-coded device input in gameplay rules.** Gameplay consumes
  device-independent intents; device checks remain in input adapters.
- **NAC-07 — Hand-edited generated files.** Generated outputs are changed only by
  authoritative inputs or generators and then regenerated.
- **NAC-08 — Unlicensed release assets.** Unknown, incompatible, or unverifiable
  provenance blocks release-bound use.
- **NAC-09 — Secrets in source or builds.** Credentials, private keys,
  certificates, storefront secrets, and equivalent private material never enter
  source control or produced builds.
- **NAC-10 — Missing diagnostics required for evidence.** Required evidence may
  not be replaced by an unobservable implementation or an undocumented manual
  claim.
- **NAC-11 — Deferring accessibility or performance while retaining breadth.**
  Scope breadth must be cut before required accessibility or performance work is
  postponed.

## Entry template

Copy this template only after a proposed shortcut passes the rejection screen
and receives an explicit review decision. Replace every placeholder; do not
leave ambiguous fields.

```markdown
### PD-NNN — <concise title>

- **Debt ID:** PD-NNN
- **Affected paths and owner:** `<path or narrow pattern>` — `<responsible owner>`
- **Reason and evidence question:** `<why the shortcut is needed now; the exact
  question and evidence that will decide whether it can be removed>`
- **Preserved invariants:** `<authoritative-state, persistence, identity, input,
  generated-file, provenance, diagnostics, accessibility, performance, and
  other invariants that remain true>`
- **Never-acceptable screening:** `NAC-01: not implicated; ...; NAC-11: not
  implicated`, with concise evidence for any category close to the boundary
- **Risk:** `<failure modes, severity, likelihood, blast radius, and escalation>`
- **Detection tests:** `<automated test, static check, diagnostic event, or
  repeatable manual evidence that exposes the debt and boundary>`
- **Expiry milestone:** `<named milestone or earlier fixed review boundary>`
- **Removal condition:** `<objective condition and proof required to delete the
  shortcut>`
- **Status:** `approved | blocking | removal-in-progress | expired | removed`
- **Review decision:** `<approved/rejected/removed; reviewer; YYYY-MM-DD;
  rationale; linked PR or evidence>`
```

## Status meanings

- `approved`: explicitly accepted, within its declared boundary, and not expired;
- `blocking`: new evidence, scope growth, failed detection, or policy conflict
  prevents merge, milestone acceptance, or release until removal;
- `removal-in-progress`: replacement work is active, while detection remains in
  force;
- `expired`: the expiry milestone was reached without satisfying the removal
  condition; treat as blocking;
- `removed`: the shortcut is gone and removal proof is linked. Retain the entry
  as history and never reuse its ID.

Proposals awaiting a decision stay in their task pull request and are not active
register entries. Rejected proposals remain PR review evidence rather than being
misrepresented as accepted debt.

## Active register

_No active prototype-debt entries._

## Removed-entry history

_No removed prototype-debt entries._

## Stage-boundary rule

Before Stage 2 acceptance, no blocking or expired debt may remain. Any retained
non-blocking debt must still have an owner, risk, detection test, unexpired
milestone, removal condition, and explicit human approval. Temporary
implementations must not leak beyond their declared package or lane, and a fresh
contributor must be able to reproduce the evidence without hidden rescue steps.
