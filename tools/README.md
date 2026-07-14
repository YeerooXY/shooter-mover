# Shooter Mover tools

`tools/` contains deterministic repository-side utilities. A tool must have a
declared owner, bounded inputs and outputs, a reproducible command, and a
documented rollback or removal path.

## Accepted tool roots

<!-- layout-root path="tools/" creation="tracked-marker" -->
<!-- layout-root path="tools/validation/" creation="tracked-or-create-by-owning-task" -->
<!-- layout-root path="tools/generation/" creation="create-by-owning-task" -->
<!-- layout-root path="tools/build/" creation="create-by-owning-task" -->

## Rules

- Validation tools inspect state and must not mutate product files.
- Generation tools may write only to output roots declared in
  `docs/architecture/FILE_OWNERSHIP.md`.
- Build tools write outside tracked source roots unless an artifact task
  explicitly declares a tracked manifest or checksum.
- A tool file is exclusively owned by the task that creates or is explicitly
  assigned to modify it. Shared-tool changes receive stronger review.
- Generated outputs are rewritten by their designated generator or workflow.
  They are not manual merge targets.
- Do not add package managers, downloaded binaries, credentials, remote
  services, analytics, networking, storefront, or mobile tooling without an
  accepted dependency task.
