# Item Maker

Run `Start-ItemMaker.ps1` from this folder. It opens a local-only web app at
`http://127.0.0.1:4173`. Node.js is required; no npm install is needed.

The editor owns two source formats:

- `Content/Items/Guns/<family-id>.gun.json`: one gun family with shared firing,
  shot, delivery, guidance, impact, and effect behavior plus MK1–MK3 progression,
  damage, and art.
- `Content/Items/Gear/<set-id>.gear.json`: one four-piece set (headpiece,
  body armor, legs, boots) with MK1–MK3 progression and typed stat modifiers.

Saving through the helper writes atomically and regenerates
`ItemPackageSources.g.cs`. Unity reads those generated sources through
`ItemPackageCatalog`; raw JSON never needs to live under `Assets`.

Repository controls are intentionally narrow:

- Fetch only runs `git fetch --prune origin`.
- Pull only runs `git pull --ff-only` and refuses a dirty worktree.
- The helper never resets, commits, pushes, switches branches, merges, or deletes
  user work.

Opening `index.html` directly remains supported as offline import/export mode.
Gear stats labelled “pending” are retained as explicit metadata and do not
silently affect gameplay. A gun with `runtimeStatus: runtime-pending` may appear
in authoring/loot data, but firing must fail explicitly until its reusable
runtime behavior is implemented.

`node compile-packages.js <repository-path>` can be used by CI or pre-commit
automation to validate package identities and regenerate the deterministic
Unity source registry.
