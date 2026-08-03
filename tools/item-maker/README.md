# Weapon Maker

Run `Start-ItemMaker.ps1` from this folder. It starts the local-only helper at
`http://127.0.0.1:4173` and opens the guided Weapon Maker. Node.js is required;
no npm install is needed.

The canonical weapon source format is one split folder per family:

- `Content/Weapons/<category>/<weapon>/weapon.json`
- `Content/Weapons/<category>/<weapon>/mk1.json`
- `Content/Weapons/<category>/<weapon>/mk2.json`
- `Content/Weapons/<category>/<weapon>/mk3.json`

The helper validates the complete four-file folder in a staging directory before
atomically replacing repository content. The Strongbox Simulator uses the same
local helper and asks the open Unity project for authoritative production rolls.

Repository controls are intentionally narrow:

- Fetch only runs `git fetch --prune origin`.
- Pull only runs `git pull --ff-only` and refuses a dirty worktree.
- The helper never resets, commits, pushes, switches branches, merges, or deletes
  user work.

The retired all-in-one `Item Package` gun/gear schema and its generated Unity
registry have been removed. Gear authoring will use a dedicated canonical format
when that system is implemented rather than reviving the old mixed package model.
