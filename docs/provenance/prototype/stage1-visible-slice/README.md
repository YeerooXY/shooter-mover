# Stage 1 visible-slice prototype art

This directory records the bounded VS-001 intake from the project owner's local
`Desktop/sprites` folder. The owner stated that these images were generated for
this project with ChatGPT and that they may be used in the project. This record
captures that statement; it is not independent legal verification.

All files are **prototype-only**. Originals and superseded revisions are
preserved byte-for-byte in `source-assets/prototype/stage1-visible-slice/`;
Unity-facing copies live in
`Assets/ShooterMover/Art/Prototype/Stage1VisibleSlice/`. Remove both copies and
their inventory entries when production art replaces them.

The project owner supplied a second bounded revision on 2026-07-15. It replaces
the floor with a square opaque PNG and replaces the turret, door, and explosive
with real-alpha PNGs. Their Unity GUIDs remain stable so presentation prefabs do
not need rebinding. The first intake remains preserved as superseded evidence.

## Intake rules

- The source files are immutable evidence. Do not crop, paint over, recompress,
  or rename them in place.
- Unity-facing filenames describe intended prototype use. The revised floor is
  a native `.png`; the superseded `.jfif`/`.jpg` pair remains in source/history.
- All Unity-facing images are single sprites at 256 pixels per unit with mipmaps
  disabled. The floor repeats; objects clamp at their edges.
- The crate, revised turret, revised door, and revised explosive contain real
  alpha channels and enable alpha-as-transparency. The floor and composition
  reference remain intentionally opaque. No automatic background removal was
  performed by the repository intake.
- These assets carry presentation only. They define no collision, gameplay,
  enemy, room, or content-registry truth.

See [asset-inventory.json](asset-inventory.json) for exact paths, dimensions,
checksums, and import limitations, and [contact-sheet.md](contact-sheet.md) for
the selected visual set.
