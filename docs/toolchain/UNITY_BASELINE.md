# Unity Editor Baseline

## Pinned editor

Shooter Mover uses exactly:

- Unity release family: Unity 6.3 LTS
- Editor version: `6000.3.19f1`
- Changeset: `7689f4515d75`
- Release date: 2026-07-01
- Primary editor host: Windows x64

`ProjectSettings/ProjectVersion.txt` is authoritative. A newer patch being available does not change the accepted baseline.

Unity identifies Unity 6.3 as the current LTS line and supports it through December 2027. The pinned patch and changeset come from Unity's official 6000.3.19f1 release record:

- [Unity 6 release support](https://unity.com/releases/unity-6/support)
- [Unity 6000.3.19f1 release and changeset](https://unity.com/releases/editor/whats-new/6000.3.19f1)

## Required Windows installation

Install the editor through Unity Hub. Select only these Hub modules for the Stage 1 Windows baseline:

- `windows-il2cpp` — Windows Build Support (IL2CPP)
- `visualstudio` — Microsoft Visual Studio, unless a supported Visual Studio 2022 installation is already recorded on the machine

For an existing Visual Studio 2022 installation, ensure the Desktop development with C++ tooling and Windows SDK `10.0.19041.0` or newer are installed. Unity documents Visual Studio 2019 with C++ Tools or later and that Windows SDK version as the IL2CPP development floor.

Optional local-only modules:

- `documentation`
- a preferred language pack

Do not install Android, iOS, tvOS, visionOS, Web, UWP, Linux, macOS, dedicated-server, or other platform modules for this baseline. Selecting the editor does not authorize analytics, advertising, accounts, storefronts, networking, relay, multiplayer, remote services, or mobile packages.

Official references:

- [Unity Hub module IDs](https://docs.unity.com/en-us/hub/hub-cli-reference)
- [Unity 6 system requirements](https://docs.unity3d.com/6000.3/Documentation/Manual/system-requirements.html)

## Supported host

The accepted development host is:

- Windows 10 version 21H1, build 19043, or newer; or a supported Windows 11 release
- x64 CPU with SSE2 support
- a hardware-vendor-supported graphics driver
- at least 8 GB RAM, with more recommended as project content grows
- an SSD or another high-IOPS drive recommended for imports and builds

Windows ARM64 is not part of the Stage 1 baseline.

## Install and verify

1. In Unity Hub, open **Installs** and install `6000.3.19f1` from the archive if it is not in the normal release list.
2. Confirm the editor changeset is `7689f4515d75`.
3. Add the required modules above.
4. Do not substitute a newer patch when the exact revision is temporarily unavailable. Use the official archive or report the baseline as blocked.
5. After `UF-002` supplies the accepted package manifest and lock file, add this repository in Unity Hub and open it with `6000.3.19f1`.
6. Reject any prompt to migrate the project or packages. A migration requires the isolated upgrade process below.

From PowerShell, verify the installed editor:

```powershell
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe'
& $unity -version
```

The command must report `6000.3.19f1`. The project must reach the Editor without rewriting `ProjectVersion.txt` or requesting an editor-version migration.

## Upgrade isolation

Editor upgrades never happen on `main` or an unrelated feature branch.

1. Create a dedicated branch named for the candidate editor version.
2. Update `ProjectVersion.txt`, package locks, toolchain records, CI images, and build identity together.
3. Reimport from a clean checkout.
4. Run compilation, edit-mode tests, play-mode smoke tests, representative Windows builds, and migration checks.
5. Review serialized and generated diffs for unintended rewrites.
6. Merge only after explicit human approval. Otherwise delete the candidate branch and continue using `6000.3.19f1`.

Automatic editor and package upgrades remain disabled by policy.
