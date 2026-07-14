# Shooter Mover UF-001 Handoff

## Outcome

Stage 1 Dispatch began after PR #14 merged. UF-001 pins the Unity editor baseline to:

- Unity 6.3 LTS;
- editor `6000.3.19f1`;
- changeset `7689f4515d75`;
- Windows x64 development host;
- Windows Build Support (IL2CPP) and a supported Visual Studio C++ toolchain.

The baseline document explicitly excludes mobile, Web, UWP, server, networking, multiplayer, analytics, storefront, advertising, account, and remote-service scope.

## Review boundary

PR #15 contains the two UF-001 product outputs and `UF-001-run-001.json`. Collaboration state is `review`; no completion is claimed before the PR is accepted.

The local machine has Unity Hub but not the exact editor. A full editor import is intentionally deferred until UF-002 supplies the deterministic package manifest and lock, preventing Unity from generating floating or unowned package state during UF-001.

## Exact next action

Review and merge PR #15. Then, on a fresh branch from current `main`, mark UF-001 done, claim UF-002, create the minimal package manifest/lock/dependency inventory, and run the first complete Unity import and no-migration verification.

Stage 2 remains blocked behind `GATE-010`.
