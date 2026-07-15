# Stage 1 visible-slice prototype art

This directory records the bounded VS-001 intake from the project owner's local
`Desktop/sprites` folder. The owner stated that these images were generated for
this project with ChatGPT and that they may be used in the project. This record
captures that statement; it is not independent legal verification.

All six files are **prototype-only**. Originals are preserved byte-for-byte in
`source-assets/prototype/stage1-visible-slice/`; Unity-facing copies live in
`Assets/ShooterMover/Art/Prototype/Stage1VisibleSlice/`. Remove both copies and
their inventory entries when production art replaces them.

## Intake rules

- The source files are immutable evidence. Do not crop, paint over, recompress,
  or rename them in place.
- Unity-facing filenames describe intended prototype use. The floor copy uses a
  `.jpg` extension because Unity 6000.3.19f1 treats the original `.jfif` as a
  generic asset; its bytes and SHA-256 remain identical to the preserved source.
- All Unity-facing images are single sprites at 256 pixels per unit with mipmaps
  disabled. The floor repeats; objects clamp at their edges.
- Only `crate_1.png` contains a real alpha channel. Its importer enables alpha as
  transparency. The turret, door, and explosive images contain opaque, baked-in
  checkerboard pixels and must remain temporary references until clean cutouts
  are supplied. No automatic background removal was performed.
- These assets carry presentation only. They define no collision, gameplay,
  enemy, room, or content-registry truth.

See [asset-inventory.json](asset-inventory.json) for exact paths, dimensions,
checksums, and import limitations, and [contact-sheet.md](contact-sheet.md) for
the selected visual set.
