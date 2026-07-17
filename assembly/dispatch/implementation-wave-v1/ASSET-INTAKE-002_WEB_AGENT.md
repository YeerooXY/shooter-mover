# ASSET-INTAKE-002 — Intake current menu and combat source art

## Objective

Import the newly supplied source images without changing gameplay code, scenes, prefabs, ProjectSettings, or production authorities.

## Owned paths

- `source-assets/user-intake/menu_screens/`
- `source-assets/user-intake/enemies/moving_droid.png`
- `source-assets/user-intake/map_items/door_open.png`
- Matching documentation in `assembly/dispatch/implementation-wave-v1/` only if needed.

## Required assets

Copy the current contents of the user's `sprites/menu_screens` folder, plus `moving_droid.png` and `door_open.png`, preserving filenames and PNG bytes. Do not create Unity imported assets or `.meta` files in this task.

## Acceptance

- Every supplied file is present at the assigned source path.
- No duplicate ownership of the previously accepted door asset is introduced.
- A manifest lists filename, source category, intended consumer, and SHA-256.
- No runtime or scene files change.

## Validation

Run a repository path/duplicate audit and `git diff --check`. Manual proof must show the manifest and changed-file list. No Unity run is required for source-only intake.
