# EH-009 local smoke entrypoints

## Purpose and boundary

EH-009 exposes three explicit Windows/PowerShell entrypoints for local Stage 1
evidence:

- `tools/evidence/run_editmode_smoke.ps1` runs the accepted editor-side test
  layer, resolves EH-001 identity and EH-002 configuration, records bounded
  validity/diagnostic observations, and writes an EH-008 manifest.
- `tools/evidence/run_playmode_smoke.ps1` runs the focused scene/session
  integration fixture in Editor PlayMode and produces a separate manifest.
- `tools/evidence/run_windows_build_smoke.ps1` first invokes the accepted UF-010
  build contract, launches the player twice with clean close/exit checks, then
  runs the same focused PlayMode harness-shell/session proof and manifests the
  complete result.

The scripts are local and offline. They add no CI, signing, release packaging,
remote service, gameplay acceptance, content registry, scene, or central task
bookkeeping. The small performance object in the descriptor is an entrypoint
wall-clock observation required by EH-008; it is explicitly not a gameplay or
hardware performance acceptance claim.

## Why all three require a Windows build

EH-008 v1 accepts a valid manifest only when it binds a complete, successful
UF-010 Windows Development build. Consequently, even the EditMode and PlayMode
entrypoints require an existing accepted UF-010 output. They do not rebuild it;
only the Windows-build entrypoint owns that operation.

The accepted default location is:

```text
%LOCALAPPDATA%\ShooterMover\Builds\WindowsDevelopment
```

A different complete copied UF-010 root may be supplied with
`-WindowsBuildRoot` to the EditMode or PlayMode command.

## Required local inputs

Every run needs two caller-owned canonical CS-002 files:

1. `BuildIdentity v1`. `artifact_checksum` may be `null` or the exact SHA-256 of
   the selected `ShooterMover.exe`. The entrypoint copies the identity into its
   output, resolves `null` to the actual executable checksum, and rejects any
   conflicting non-null checksum. It never mutates the caller's source file.
2. `ContentVersion v1`, in exact canonical form.

The default EH-002 configuration is:

```text
tools/evidence/fixtures/stage1-evidence-config-v1.json
```

Its placeholder `identityReference` is replaced in the output copy by the
captured EH-001 record fingerprint. The strict EH-001 tool then verifies the
pinned editor and exact package-lock bytes. A custom configuration may be given
as a repository-relative or absolute path with `-ConfigurationPath`.

Python must be available as `python` or selected with `-PythonPath`. Unity must
be the pinned editor, selected by `-UnityPath`, `UNITY_EDITOR_PATH`, or the
standard Unity Hub location.

## Output ownership and stale-output policy

`-OutputDirectory` is caller-selected and must be outside the repository. The
path may contain spaces. It must be missing or empty. Existing content,
reparse points, symlinks, or an in-repository path are rejected; the entrypoints
never delete or overwrite stale evidence.

A successful output has this shape:

```text
evidence-package.json
evidence-manifest-v1.json
evidence-manifest-v1.sha256
identity/
  build-identity.txt
  content-version.txt
  evidence-identity.txt
configuration/
  stage1.json
diagnostics/
  summary.json
  test-results.xml
  unity.log
  windows/                 # Windows-build entrypoint only
    player-pass-1.log
    player-pass-2.log
    windows-smoke.json
performance/
  summary.json
windows-build/
  ShooterMover.exe
  ShooterMover_Data/...
  UnityPlayer.dll
  unity-build.log
  build-fingerprints.json
  build-artifacts.txt
```

Machine-local paths in newly captured Unity/player logs are replaced before
manifest generation. The copied UF-010 artifact remains byte-identical so its
accepted artifact inventory and fingerprints still verify.

## EditMode smoke

Prepare or reuse one accepted UF-010 build, then run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\tools\evidence\run_editmode_smoke.ps1 `
  -OutputDirectory "C:\evidence\shooter-mover\editmode-001" `
  -BuildIdentityPath "C:\evidence-inputs\build-identity.txt" `
  -ContentVersionPath "C:\evidence-inputs\content-version.txt" `
  -UnityPath "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
```

The public EditMode default runs the accepted EditMode test assembly. Identity
and configuration are resolved before Unity starts. Unity test failure, missing
or malformed XML, diagnostic overflow, or EH-008 build/verify failure makes the
command fail.

## PlayMode smoke

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\tools\evidence\run_playmode_smoke.ps1 `
  -OutputDirectory "C:\evidence\shooter-mover\playmode-001" `
  -BuildIdentityPath "C:\evidence-inputs\build-identity.txt" `
  -ContentVersionPath "C:\evidence-inputs\content-version.txt" `
  -UnityPath "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
```

This wrapper selects exactly:

```text
ShooterMover.Tests.PlayMode.EvidenceHarness.EvidenceEntrypointSmokeTests.EntryPoint_SceneAndSessionSmoke_ReturnsToCleanBootstrap
```

The fixture proves the real Bootstrap shell remains authoritative while the
FoundationSmoke scene loads/unloads/reloads, then runs an EH-006 quick restart
with a new attempt ID and parent lineage, ends the session, and verifies no
probe objects, markers, subscriptions, stale intent, in-flight scene operation,
or replaced Bootstrap owner remain.

## Windows-build smoke

Use a fresh caller output path:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File .\tools\evidence\run_windows_build_smoke.ps1 `
  -OutputDirectory "C:\evidence\shooter-mover\windows-001" `
  -BuildIdentityPath "C:\evidence-inputs\build-identity.txt" `
  -ContentVersionPath "C:\evidence-inputs\content-version.txt" `
  -UnityPath "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
```

The command performs these fail-closed stages:

1. Verify UF-010 still has exactly one enabled build scene and that it is
   `Assets/ShooterMover/Scenes/Bootstrap/Bootstrap.unity`.
2. Invoke `tools/build/Build-WindowsDevelopment.ps1` as the accepted build
   contract and preserve its nonzero exit code.
3. Require the complete UF-010 artifact root.
4. Launch `ShooterMover.exe`, require it to remain alive through startup,
   request a normal main-window close, require exit code `0`, and reject crash,
   unhandled-exception, null-reference, or assertion signatures.
5. Repeat the launch/clean-close flow as the standalone restart smoke.
6. Run the focused Editor PlayMode fixture to prove harness-shell load and
   EH-006 restart/cleanup semantics directly.
7. Build and verify the EH-008 manifest with `--require-valid`.

The standalone launch probe does not claim gameplay acceptance. The sole UF-010
build scene proves which shell is starting; the focused PlayMode fixture proves
that shell's composition root, scene smoke, lifecycle restart, and cleanup.

## Fast non-Unity contract checks

These checks do not launch Unity or build the player:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\evidence\run_editmode_smoke.ps1 -ContractTest
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\evidence\run_playmode_smoke.ps1 -ContractTest
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\evidence\run_windows_build_smoke.ps1 -ContractTest
```

They cover parameter-set parsing, repository-relative resolution, output paths
containing spaces, stale-output rejection, exact child exit-code observation,
wrapper routing, manifest-tool discovery, and UF-010 Bootstrap-scene binding.
The PlayMode test fixture runs these same three checks on Windows and also
statically verifies that each entrypoint retains its distinct contract and
manifest/failure tokens.

EH-008's existing standard-library suite remains the focused automated proof of
actual manifest creation, verification, canonical bytes, stale/tampered package
rejection, and failed/incomplete UF-010 rejection:

```powershell
python -S -m unittest -v tools.evidence.tests.test_build_evidence_manifest
```

## Exit semantics

The scripts return `0` only after the requested layer passes and EH-008 builds
and verifies a valid manifest. Expected local validation failures use actionable
nonzero codes. Most importantly, a Unity, Python, UF-010, nested PowerShell, or
standalone-player nonzero result is returned unchanged rather than collapsed to
a false success. Unexpected orchestration exceptions return `1`.

A failed run may leave a partial caller-owned directory for diagnosis. That
partial directory is intentionally not reusable: rerunning against it is
rejected as stale. Select a new empty path after correcting the prerequisite or
child-process failure.

## Required proof before promotion

Run and attach these exact commands on Windows with Unity `6000.3.19f1`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\evidence\run_editmode_smoke.ps1 -ContractTest
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\evidence\run_playmode_smoke.ps1 -ContractTest
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\evidence\run_windows_build_smoke.ps1 -ContractTest
python -S -m unittest -v tools.evidence.tests.test_build_evidence_manifest
```

Then run the three full commands above with three separate empty output roots.
Attach the EditMode and PlayMode test XML/logs, both standalone player logs, and
the successful Windows `evidence-manifest-v1.json` plus checksum. Also run the
focused Unity fixture directly if a Test Runner transcript is preferred:

```text
ShooterMover.Tests.PlayMode.EvidenceHarness.EvidenceEntrypointSmokeTests
```

Connector-only implementation cannot launch Windows PowerShell, Unity, or a
Windows player. Source inspection is not represented as passing execution.

## One-unit rollback

Revert the three `tools/evidence/run_*_smoke.ps1` files,
`EvidenceEntrypointSmokeTests.cs` and its inseparable `.meta`, and this document
together. The accepted UF-009 tests, UF-010 build contract, EH-006 lifecycle,
EH-008 manifest generator, scenes, project settings, registries, and gameplay
remain unchanged. Caller-owned local evidence directories may be removed after
all related Unity/player processes are closed.
