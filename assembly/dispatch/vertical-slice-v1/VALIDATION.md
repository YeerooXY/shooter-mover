# Playable vertical-slice dispatch validation

## Repository and PR inspection

- Inspected live repository `YeerooXY/shooter-mover`.
- Verified default branch `main`.
- Verified current `main` at `645cf24f30ee6c8762214a84060e59e35df67a05`.
- Verified merged launch foundations through repository history: SIM-001, STAT-001, PICK-001, BOXSCENE-001, and XP-001 are present before/on the launch commit.
- Verified open PR state:
  - PR #160 MENU-001 is open, draft, unmerged at `f0430794fca20cc911561478767eddbecb476f1e` and lacks Unity execution proof.
  - PR #171 SKILL-001 is open, unmerged, and its PR explicitly says Unity compile/test execution remains required.
- Verified asset-intake commit `0b1b654c1fb8cf8208904eb55041fde954cfb560` and its six source files.
- Read the attached Demo-First Flow handoff and preserved its result/strongbox identity and idempotency requirements.
- Read `AGENTS.md`, workspace/context files, prior wave dispatch format, and prior validation format.

## Dependency findings

- Existing reward, money, scrap, holdings, generator, reward-application, strongbox, shop, crafting, pickup, simulator, statistics, and XP foundations are available on the launch base.
- `RUN-001` is not merged on the launch base and no open RUN-001 implementation PR is present. `DEV-001` and `BOXUI-001` are therefore marked blocked.
- MENU-001 cannot be treated as a dependency until MENU-002 repairs it and focused Unity XML proof passes.
- SKILL-001 cannot be treated as a dependency until a proof-complete skill-authority PR merges.
- HUB-001 exclusively owns the shared route/profile payload; downstream flow tasks wait for it rather than creating parallel contracts.

## Ownership validation

- Task packets generated: 20.
- Every packet contains objective, exact ownership, dependencies, forbidden paths, acceptance criteria, focused Unity command, manual proof, merge order, assets, limitations, and parallel safety.
- Prefix/exact-path collision audit across all 20 ownership rows: **zero overlaps**.
- Stage1 serialized files have exactly one owner: DEMO-005.
- Asset source paths are read-only and not owned by implementation tasks.

## Unity validation status

Unity was **not executed** during dispatch preparation. No current-baseline cold compile, EditMode pass, or PlayMode pass is claimed. This coordinator change contains planning/dispatch Markdown/JSON only.

Implementation agents must create result files under `artifacts/test-results/` using their packet commands. A PR may claim passing Unity proof only when the referenced XML exists and reports a passed test run with zero failures. Logs, authored tests, static review, or GitHub's mergeable flag are not substitutes.

## Static preparation checks

- Required task ID set: complete (20/20).
- Ownership collision audit: passed, zero overlaps.
- Dependency graph: acyclic when external prerequisites MENU-001, RUN-001, and SKILL-001 are treated as gates.
- Asset consumers: each maps to a unique repository-owned art subtree.
- No implementation, scene, prefab, ProjectSettings, package, or production-authority file is changed by this dispatch PR.
