# Enemy Definition Builder

A standalone browser-based editor for defining enemy identity, level stats, shooting behaviour, loot settings, and implementation notes.

## Run locally

Open `index.html` in a modern browser. No build tools or server are required.

## Current export format

The tool currently exports formatted JSON. Serialization is intentionally isolated in `serializeEnemyData()` inside `app.js`, so it can later be replaced with another format or an API request without rebuilding the UI.

## Features

- General enemy identity and notes
- Multiple configurable enemy levels
- HP, damage, speed, ranges, drop chance, and loot table reference
- Shooting type, fire rate, projectile settings, spread, and burst controls
- Duplicate, collapse, and remove level controls
- Preview, clipboard copy, file export, and form reset
- Responsive layout
