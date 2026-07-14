# Windows x86-64 development build

This baseline builds the pinned Unity project as a local Windows x86-64
**Development** player. It is intentionally limited to developer smoke builds:
there is no release packaging, signing, upload, installer, or formal build
identity workflow.

## Build scene

`ProjectSettings/EditorBuildSettings.asset` contains exactly one enabled scene:

| Build index | Scene |
|---:|---|
| 0 | `Assets/ShooterMover/Scenes/Bootstrap/Bootstrap.unity` |

`FoundationSmoke.unity` is not part of this baseline. The build proves that the
single Bootstrap scene can compile and produce a player without changing scene
content or deciding later gameplay flow.

## Prerequisites

- Windows x86-64.
- Unity `6000.3.19f1 (7689f4515d75)` installed with Windows Build Support.
- A valid Unity license for batch-mode editor use.
- Windows PowerShell 5.1 or PowerShell 7.
- Git available on `PATH` for the canonical package-lock fingerprint.
- The Unity project must not already be open in another Editor process.

The script accepts an explicit Unity executable path. When omitted, it first
uses `UNITY_EDITOR_PATH`, then checks the standard Unity Hub install path for
the version pinned in `ProjectSettings/ProjectVersion.txt`.

## Clean import

For proof from a clean local import, close Unity and remove only the repository's
local cache before running the build:

```powershell
Remove-Item -LiteralPath ".\Library" -Recurse -Force -ErrorAction SilentlyContinue
```

Do not remove tracked project files, `Assets/`, `Packages/`, or
`ProjectSettings/`. Unity recreates `Library/` during the build invocation.

## Exact build command

Run from the repository root. Quoting is intentional and supports repository
and Unity paths containing spaces.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\tools\build\Build-WindowsDevelopment.ps1" -UnityPath "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
```

Equivalent use with an environment variable:

```powershell
$env:UNITY_EDITOR_PATH = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
.\tools\build\Build-WindowsDevelopment.ps1
```

The script invokes Unity in batch mode for target `win64`, requests a
Development build, and writes the full Unity log into the owned output folder.
It places Unity's -quit argument after the build and log arguments so a clean
import can finish before the editor exits. A Unity compile/build failure is
returned as a non-zero process exit.

## Owned output and stale builds

The only build-output location owned by this script is:

```text
%LOCALAPPDATA%\ShooterMover\Builds\WindowsDevelopment
```

This repository-external location survives Unity's cleanup of the project's
temporary directory and cannot be added to the repository. Before each run,
the script verifies that exact path, rejects reparse points, clears read-only
attributes inside it, and removes stale contents. It never cleans another
directory. If a running player, Explorer window, permission, or file lock
prevents cleanup, the script stops with a clear error instead of falling back
to another output path.

## Expected output

A successful build contains at least:

```text
%LOCALAPPDATA%\ShooterMover\Builds\WindowsDevelopment
├── ShooterMover.exe
├── ShooterMover_Data/
│   ├── boot.config
│   └── globalgamemanagers
├── UnityPlayer.dll
├── unity-build.log
├── build-fingerprints.json
└── build-artifacts.txt
```

The script verifies these artifacts before reporting success.
`build-artifacts.txt` records the complete output-relative file list.
`build-fingerprints.json` records:

- the pinned editor version and revision;
- the SHA-256 of the working-tree `ProjectSettings/ProjectVersion.txt`;
- the SHA-256 of the actual `Unity.exe` used;
- the canonical `HEAD` repository-content SHA-256 of
  `Packages/packages-lock.json`;
- the working-tree SHA-256 of `Packages/packages-lock.json`;
- target, Development configuration, and local-application-data-relative
  output path.

The canonical package-lock value follows the accepted dependency-lock procedure,
so Windows line-ending conversion cannot change it. The worktree value remains
available to diagnose local rewrites.

These fingerprints are local build evidence only; they do not introduce or
replace the project's formal identity contracts.

## Launch

From the repository root:

```powershell
& "$env:LOCALAPPDATA\ShooterMover\Builds\WindowsDevelopment\ShooterMover.exe"
```

The player starts from build index 0, `Bootstrap.unity`. Exit the player
normally, then repeat the launch when performing manual smoke proof.

## Cleanup

Close the built player, then remove only the disposable owned output:

```powershell
$output = Join-Path ([System.Environment]::GetFolderPath([System.Environment+SpecialFolder]::LocalApplicationData)) "ShooterMover\Builds\WindowsDevelopment"
Remove-Item -LiteralPath $output -Recurse -Force
```

If cleanup reports an access or sharing violation, close the player and any
process holding the output, then retry. No cleanup outside
`%LOCALAPPDATA%\ShooterMover\Builds\WindowsDevelopment` is required for
ordinary builds.
