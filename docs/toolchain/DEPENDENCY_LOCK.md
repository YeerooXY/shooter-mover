# Unity Dependency Lock

## Baseline

This project uses the exact editor declared in `ProjectSettings/ProjectVersion.txt`: Unity `6000.3.19f1` (`7689f4515d75`). The package manifest intentionally contains only the three direct dependencies required by the Stage 1 fully 2D baseline. Official Unity packages use the default Unity registry; no scoped or third-party registry is configured.

The committed package lock contains 21 exact resolved entries. Core packages whose lock source is `builtin` are supplied by the pinned editor. Registry packages use `https://packages.unity.com`. Version ranges, wildcards, Git branches, previews, local paths, and floating tags are not permitted.

## Direct dependency inventory

| Package | Version and source | Purpose | License | Owner | Update policy | Principal risk | Removal path |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `com.unity.render-pipelines.universal` | `17.3.0`; Unity 6000.3 built-in core package | URP and its 2D renderer baseline | Unity Companion License; see the package `LICENSE.md` | Unity Foundation lane | Changes only with an approved editor/package-baseline task on an isolated branch; regenerate and review the whole lock | High: rendering assets, shaders, and quality settings will depend on this major line | Remove URP assets and project-setting references first, replace the rendering path, then remove the manifest entry and regenerate the lock as one reviewed change |
| `com.unity.inputsystem` | `1.19.0`; official Unity registry | Device-agnostic keyboard, mouse, and gamepad input through owned adapters | Unity Companion License; see the package `LICENSE.md` | Unity Foundation lane | Exact version only; no automatic upgrades; validate input and build smoke tests on a dedicated upgrade branch | Medium: package/API or device-behavior changes can affect every control path | Remove project input assets and adapter references, restore an approved input implementation, then remove the manifest entry and regenerate the lock |
| `com.unity.test-framework` | `1.6.0`; Unity 6000.3 built-in core package | EditMode and PlayMode test discovery and execution | Unity Companion License; see the package `LICENSE.md` | Verification and Release lane, baseline owned by Unity Foundation | Follows the pinned editor unless an approved test-baseline task says otherwise; rerun the complete test harness after change | Low runtime risk, medium verification risk: runner changes can invalidate evidence | Migrate or remove Unity Test Framework assemblies/tests, then remove the manifest entry and regenerate the lock |

License files shipped inside the resolved packages are authoritative. A license change is a dependency change and requires review even when the semantic version appears compatible.

## Resolved package list

| Package | Locked version | Source | Direct |
| --- | ---: | --- | :---: |
| `com.unity.burst` | `1.8.29` | Unity registry | No |
| `com.unity.collections` | `2.6.6` | Unity registry | No |
| `com.unity.ext.nunit` | `2.0.5` | Editor built-in | No |
| `com.unity.inputsystem` | `1.19.0` | Unity registry | Yes |
| `com.unity.mathematics` | `1.3.3` | Unity registry | No |
| `com.unity.modules.hierarchycore` | `1.0.0` | Editor built-in | No |
| `com.unity.modules.imgui` | `1.0.0` | Editor built-in | No |
| `com.unity.modules.jsonserialize` | `1.0.0` | Editor built-in | No |
| `com.unity.modules.physics` | `1.0.0` | Editor built-in | No |
| `com.unity.modules.terrain` | `1.0.0` | Editor built-in | No |
| `com.unity.modules.ui` | `1.0.0` | Editor built-in | No |
| `com.unity.modules.uielements` | `1.0.0` | Editor built-in | No |
| `com.unity.nuget.mono-cecil` | `1.11.6` | Unity registry | No |
| `com.unity.render-pipelines.core` | `17.3.0` | Editor built-in | No |
| `com.unity.render-pipelines.universal` | `17.3.0` | Editor built-in | Yes |
| `com.unity.render-pipelines.universal-config` | `17.0.3` | Editor built-in | No |
| `com.unity.searcher` | `4.9.4` | Unity registry | No |
| `com.unity.shadergraph` | `17.3.0` | Editor built-in | No |
| `com.unity.test-framework` | `1.6.0` | Editor built-in | Yes |
| `com.unity.test-framework.performance` | `3.5.0` | Unity registry | No |
| `com.unity.ugui` | `2.0.0` | Editor built-in | No |

Transitive packages are retained only because the direct set requires them. Their exact versions and dependency edges are authoritative in `Packages/packages-lock.json`; they must not be promoted to direct manifest entries without a separate justification.

## Excluded dependency categories

The resolved graph contains no analytics, crash-reporting SaaS, advertising, monetization, storefront/IAP, accounts, networking, netcode, relay, multiplayer, Vivox, mobile-platform, or third-party gameplay SDK. Unity's optional `com.unity.modules.unityanalytics` built-in module is deliberately absent.

## Update and rollback procedure

1. Start a dedicated dependency-upgrade task and branch from the current `main`.
2. Record the reason, target editor compatibility, release notes, and license impact before changing a version.
3. Change `manifest.json`, let the exact pinned editor resolve the complete lock, and review every added, removed, or changed package.
4. Run static dependency validation, EditMode and PlayMode tests, and Windows build smoke checks. Confirm excluded SDK categories remain absent.
5. Recompute and record the lock fingerprint in the task-run proof. Merge only after human review.

Rollback restores `Packages/manifest.json`, `Packages/packages-lock.json`, and this inventory from the same known-good commit. Never roll back only one of these files.

## Verification

Static verification requires all of the following:

- the manifest and lock parse as JSON;
- every direct manifest dependency has the same exact version at lock depth `0`;
- every transitive edge names an entry present in the lock;
- no direct version contains a range, wildcard, preview suffix, Git reference, branch head, URL, or local path;
- the prohibited SDK scan returns no package names;
- all three direct dependencies have complete inventory rows above.

After installing the exact editor, perform the first resolution check:

1. Open the repository through Unity Hub with `6000.3.19f1` and allow Package Manager to finish.
2. Confirm the Console reports no unresolved package or compilation error.
3. In **Window > Package Manager > In Project**, confirm URP `17.3.0`, Input System `1.19.0`, and Test Framework `1.6.0`, with no direct-package upgrade prompt.
4. Close Unity and require `git diff --exit-code -- Packages/manifest.json Packages/packages-lock.json` to pass. Any silent lock rewrite is a failed baseline check that must be reviewed, not accepted automatically.
5. Recompute the canonical repository-content fingerprint with `python -c "import hashlib,subprocess; data=subprocess.check_output(['git','show','HEAD:Packages/packages-lock.json']); print(hashlib.sha256(data).hexdigest())"` and compare it with the fingerprint below.

## Lock fingerprint

Canonical `Packages/packages-lock.json` repository-content SHA-256:

```text
2d681f0409804236336a38ee67c46f5e823205e42b2b092b226b60758411d557
```

This fingerprint covers the resolved package list, versions, sources, depths, and dependency edges. It does not cover `manifest.json` or this document. It is calculated from the canonical Git content so Windows CRLF checkout conversion cannot create a false mismatch. Use `git diff --exit-code -- Packages/manifest.json Packages/packages-lock.json` to detect a working-tree rewrite.

The first Unity `6000.3.19f1` import on 2026-07-14 resolved and registered all 21 locked packages in 19.57 seconds. The editor log contained no unresolved-package or compilation failure, both package files remained clean according to Git, and the canonical fingerprint matched.
