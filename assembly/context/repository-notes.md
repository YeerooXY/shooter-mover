# Shooter Mover Repository Notes

## Repository identity

- Repository: `YeerooXY/shooter-mover`
- Default branch: `main`
- Requirements branch: `assembly/bootstrap-shooter-mover`
- Implementation root: `.`
- Repository visibility: public
- Product implementation state: greenfield

## Lifecycle state

The requirements/bootstrap package is ready for human review. Planning is blocked only by the pull-request approval and merge boundary.

After merge, a fresh Planning Agent must read:

1. `project_workspace.json`
2. `assembly/intake/project_intake.json`
3. `assembly/requirements/REQUIREMENTS.md`
4. `assembly/context/handoff.md`
5. the verified decision logs under `assembly/intake/`
6. the repository source, tests, contracts, and installed prompts

Planning produces `project_spec.json`, `repo_plan.json`, supporting planning-run artifacts, and collaboration contracts in a separate planning pull request. It must not create the canonical implementation backlog.

## Source boundary

- D-040 through D-233: verified
- D-001 through D-039: recovered but excluded
- D-234: presented but not selected
- Exact values and content lists described as prototype or planning variables remain unresolved by design

## Expected implementation foundations

- Unity with C#
- pinned Unity LTS and packages
- URP 2D and Unity Input System
- plain-C# gameplay and state core with Unity adapters
- typed ScriptableObject definitions with stable IDs
- additive scenes, modular prefabs, modular room packages, and authored level graph
- versioned atomic snapshots plus compact transactional journal
- layered CI and immutable formal build artifacts
- short-lived scoped branches with protected integration

## Repository hygiene

- Generated registries and review snapshots are rebuilt, not hand edited.
- Unity scenes, prefabs, ScriptableObjects, and shared modules require explicit task ownership.
- Large binary source assets should use a documented storage and backup strategy before production begins.
- Asset license and provenance records are required before release-bound use.
- No credentials, signing keys, or private service secrets belong in the repository.
