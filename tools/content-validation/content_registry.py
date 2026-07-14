#!/usr/bin/env python3
"""Deterministic Content Definitions v1 registry generation and drift validation."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, Mapping, Sequence

DESCRIPTOR_SCHEMA_ID = "urn:shooter-mover:schema:content-definition-descriptor-input:1"
MACHINE_SCHEMA_ID = "urn:shooter-mover:schema:generated-registry:1"
REVIEW_SCHEMA_ID = "urn:shooter-mover:schema:generated-registry-review:1"
SUPPORTED_SCHEMA_VERSION = 1
SUPPORTED_DEFINITION_VERSION = 1
KIND_ORDER = (
    "enemy",
    "encounter",
    "environment",
    "room",
    "shared-module",
    "weapon",
)
KIND_SET = frozenset(KIND_ORDER)
DEFAULT_DESCRIPTOR_ROOTS = (
    Path("Assets/ShooterMover/Content/Definitions"),
    Path("Assets/ShooterMover/Content/SharedModules"),
    Path("Assets/ShooterMover/ContentPackages"),
)
DEFAULT_REGISTRY_OUTPUT = Path("Assets/ShooterMover/Generated/content-registry.json")
DEFAULT_REVIEW_OUTPUT = Path(
    "Assets/ShooterMover/Generated/content-review-snapshot.json"
)
LOCK_PATH = Path("tools/content-validation/.registry-generation.lock")
DESCRIPTOR_SUFFIX = ".content-descriptor.json"
STABLE_ID_RE = re.compile(
    r"^[a-z0-9]+(?:-[a-z0-9]+)*\.[a-z0-9]+(?:-[a-z0-9]+)*$"
)
ERROR_CODE_ORDER = {
    "duplicate-definition": 1,
    "missing-definition": 2,
    "wrong-definition-kind": 3,
    "unsupported-definition-version": 4,
    "cyclic-dependency": 5,
    "missing-provenance": 6,
    "tombstoned-id": 7,
    "prototype-only-definition": 8,
}


class RegistryToolError(RuntimeError):
    """Base class for deterministic user-facing tool failures."""


class DescriptorInputError(RegistryToolError):
    """A descriptor file is malformed before catalog validation."""


class CatalogValidationError(RegistryToolError):
    """The complete descriptor catalog failed Content Definitions v1 validation."""

    def __init__(self, errors: Sequence["ValidationError"]) -> None:
        self.errors = tuple(sorted(errors, key=ValidationError.sort_key))
        super().__init__("\n".join(error.to_canonical_string() for error in self.errors))


class GeneratedOutputDriftError(RegistryToolError):
    """Checked-in generated output is missing, stale, non-canonical, or hand-edited."""

    def __init__(self, messages: Sequence[str]) -> None:
        self.messages = tuple(sorted(messages))
        super().__init__("\n".join(self.messages))


class ConcurrentGenerationError(RegistryToolError):
    """Another generator invocation owns the repository lock."""


@dataclass(frozen=True, order=True)
class ContentReferenceInput:
    definition_kind: str
    definition_id: str
    definition_version: int

    def canonical_token(self) -> str:
        return (
            f"{self.definition_kind}|{self.definition_id}|"
            f"{self.definition_version}"
        )


@dataclass(frozen=True)
class ContentDescriptorInput:
    definition_kind: str
    definition_id: str
    definition_version: int
    provenance_id: str | None
    prototype_only: bool
    references: tuple[ContentReferenceInput, ...]
    source: str = ""

    def sort_key(self) -> tuple[str, str, int]:
        return (
            self.definition_kind,
            self.definition_id,
            self.definition_version,
        )

    def canonical_string(self) -> str:
        lines = [
            f"definition_kind={self.definition_kind}",
            f"definition_id={self.definition_id}",
            f"definition_version={self.definition_version}",
            f"provenance_id={self.provenance_id if self.provenance_id is not None else 'null'}",
            f"prototype_only={'true' if self.prototype_only else 'false'}",
            f"reference_count={len(self.references)}",
        ]
        for index, reference in enumerate(self.references):
            lines.append(f"reference_{index:04d}={reference.canonical_token()}")
        return "\n".join(lines)


@dataclass(frozen=True)
class ValidationError:
    code: str
    definition_id: str | None = None
    referenced_id: str | None = None
    expected_kind: str | None = None
    actual_kind: str | None = None
    expected_version: int | None = None
    actual_version: int | None = None
    detail: str | None = None
    cycle: tuple[str, ...] = ()

    def sort_key(self) -> tuple[object, ...]:
        return (
            ERROR_CODE_ORDER[self.code],
            _none_first(self.definition_id),
            _none_first(self.referenced_id),
            _none_first(self.expected_kind),
            _none_first(self.actual_kind),
            _none_first(self.expected_version),
            _none_first(self.actual_version),
            self.cycle,
            _none_first(self.detail),
        )

    def to_canonical_string(self) -> str:
        return "\n".join(
            (
                f"code={self.code}",
                f"definition_id={_nullable(self.definition_id)}",
                f"referenced_id={_nullable(self.referenced_id)}",
                f"expected_kind={_nullable(self.expected_kind)}",
                f"actual_kind={_nullable(self.actual_kind)}",
                f"expected_version={_nullable(self.expected_version)}",
                f"actual_version={_nullable(self.actual_version)}",
                f"detail={_nullable(self.detail)}",
                f"cycle={','.join(self.cycle) if self.cycle else 'null'}",
            )
        )


@dataclass(frozen=True)
class GeneratedDocuments:
    machine_bytes: bytes
    review_bytes: bytes
    definition_fingerprint: str
    registry_fingerprint: str
    snapshot_fingerprint: str

    @property
    def machine_checksum(self) -> str:
        return _raw_sha256(self.machine_bytes)

    @property
    def review_checksum(self) -> str:
        return _raw_sha256(self.review_bytes)


def _none_first(value: object | None) -> tuple[int, object]:
    return (0, "") if value is None else (1, value)


def _nullable(value: object | None) -> str:
    return "null" if value is None else str(value)


def _raw_sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _formal_sha256(text: str) -> str:
    return "sha256:" + hashlib.sha256(text.encode("utf-8")).hexdigest()


def _quote(value: str) -> str:
    return json.dumps(value, ensure_ascii=False)


def _require_stable_id(value: object, field_name: str) -> str:
    if not isinstance(value, str):
        raise DescriptorInputError(f"{field_name} must be a canonical StableId string")
    if len(value) > 128 or not STABLE_ID_RE.fullmatch(value):
        raise DescriptorInputError(
            f"{field_name} must use lowercase namespace.value StableId form"
        )
    namespace_name, local_value = value.split(".", 1)
    if len(namespace_name) > 32 or len(local_value) > 96:
        raise DescriptorInputError(
            f"{field_name} exceeds StableId v1 component length limits"
        )
    return value


def _require_kind(value: object, field_name: str) -> str:
    if not isinstance(value, str) or value not in KIND_SET:
        raise DescriptorInputError(
            f"{field_name} must be one of {', '.join(KIND_ORDER)}"
        )
    return value


def _require_positive_int(value: object, field_name: str) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value < 1:
        raise DescriptorInputError(f"{field_name} must be a positive integer")
    if value > 2_147_483_647:
        raise DescriptorInputError(f"{field_name} exceeds the supported integer range")
    return value


def _object_without_duplicate_keys(
    pairs: Sequence[tuple[str, object]],
) -> dict[str, object]:
    result: dict[str, object] = {}
    for key, value in pairs:
        if key in result:
            raise DescriptorInputError(f"duplicate JSON property: {key}")
        result[key] = value
    return result


def _load_json_file(path: Path) -> object:
    data = path.read_bytes()
    if data.startswith(b"\xef\xbb\xbf"):
        raise DescriptorInputError("UTF-8 byte-order marks are not permitted")
    try:
        text = data.decode("utf-8")
    except UnicodeDecodeError as error:
        raise DescriptorInputError(f"file is not valid UTF-8: {error}") from error
    try:
        return json.loads(text, object_pairs_hook=_object_without_duplicate_keys)
    except DescriptorInputError:
        raise
    except json.JSONDecodeError as error:
        raise DescriptorInputError(
            f"invalid JSON at line {error.lineno}, column {error.colno}: {error.msg}"
        ) from error


def _require_exact_keys(
    mapping: Mapping[str, object],
    expected: frozenset[str],
    context: str,
) -> None:
    actual = set(mapping)
    missing = sorted(expected - actual)
    unknown = sorted(actual - expected)
    if missing:
        raise DescriptorInputError(f"{context} is missing properties: {', '.join(missing)}")
    if unknown:
        raise DescriptorInputError(f"{context} has unknown properties: {', '.join(unknown)}")


def parse_descriptor_mapping(
    raw: object,
    source: str,
    require_input_schema: bool = True,
) -> ContentDescriptorInput:
    if not isinstance(raw, dict):
        raise DescriptorInputError("descriptor root must be a JSON object")

    expected = {
        "definition_kind",
        "definition_id",
        "definition_version",
        "provenance_id",
        "prototype_only",
        "references",
    }
    if require_input_schema:
        expected.add("$schema")
    _require_exact_keys(raw, frozenset(expected), "descriptor")

    if require_input_schema and raw["$schema"] != DESCRIPTOR_SCHEMA_ID:
        raise DescriptorInputError(
            f"$schema must equal {_quote(DESCRIPTOR_SCHEMA_ID)}"
        )

    definition_kind = _require_kind(raw["definition_kind"], "definition_kind")
    definition_id = _require_stable_id(raw["definition_id"], "definition_id")
    definition_version = _require_positive_int(
        raw["definition_version"], "definition_version"
    )

    provenance_raw = raw["provenance_id"]
    provenance_id = (
        None
        if provenance_raw is None
        else _require_stable_id(provenance_raw, "provenance_id")
    )

    prototype_only = raw["prototype_only"]
    if not isinstance(prototype_only, bool):
        raise DescriptorInputError("prototype_only must be a JSON boolean")

    references_raw = raw["references"]
    if not isinstance(references_raw, list):
        raise DescriptorInputError("references must be a JSON array")

    references: list[ContentReferenceInput] = []
    seen_references: set[ContentReferenceInput] = set()
    for index, reference_raw in enumerate(references_raw):
        if not isinstance(reference_raw, dict):
            raise DescriptorInputError(f"references[{index}] must be a JSON object")
        _require_exact_keys(
            reference_raw,
            frozenset(
                {
                    "definition_kind",
                    "definition_id",
                    "definition_version",
                }
            ),
            f"references[{index}]",
        )
        reference = ContentReferenceInput(
            definition_kind=_require_kind(
                reference_raw["definition_kind"],
                f"references[{index}].definition_kind",
            ),
            definition_id=_require_stable_id(
                reference_raw["definition_id"],
                f"references[{index}].definition_id",
            ),
            definition_version=_require_positive_int(
                reference_raw["definition_version"],
                f"references[{index}].definition_version",
            ),
        )
        if reference in seen_references:
            raise DescriptorInputError(
                f"references[{index}] duplicates typed reference "
                f"{reference.canonical_token()}"
            )
        seen_references.add(reference)
        references.append(reference)

    return ContentDescriptorInput(
        definition_kind=definition_kind,
        definition_id=definition_id,
        definition_version=definition_version,
        provenance_id=provenance_id,
        prototype_only=prototype_only,
        references=tuple(sorted(references)),
        source=source,
    )


def scan_descriptors(
    repo_root: Path,
    descriptor_roots: Sequence[Path] = DEFAULT_DESCRIPTOR_ROOTS,
) -> tuple[ContentDescriptorInput, ...]:
    discovered: list[Path] = []
    for root in descriptor_roots:
        absolute_root = root if root.is_absolute() else repo_root / root
        if absolute_root.is_file():
            if absolute_root.name.endswith(DESCRIPTOR_SUFFIX):
                discovered.append(absolute_root)
            continue
        if not absolute_root.exists():
            continue
        discovered.extend(
            path
            for path in absolute_root.rglob(f"*{DESCRIPTOR_SUFFIX}")
            if path.is_file()
        )

    unique_paths = sorted(set(path.resolve() for path in discovered), key=str)
    descriptors: list[ContentDescriptorInput] = []
    input_errors: list[str] = []

    for path in unique_paths:
        try:
            relative = path.relative_to(repo_root.resolve()).as_posix()
        except ValueError:
            relative = path.as_posix()
        try:
            descriptors.append(
                parse_descriptor_mapping(_load_json_file(path), source=relative)
            )
        except (OSError, RegistryToolError) as error:
            input_errors.append(f"{relative}: {error}")

    if input_errors:
        raise DescriptorInputError("\n".join(sorted(input_errors)))
    return tuple(descriptors)


def validate_catalog(
    descriptors: Iterable[ContentDescriptorInput],
    mode: str,
) -> tuple[ContentDescriptorInput, ...]:
    if mode not in {"release", "prototype"}:
        raise DescriptorInputError("validation mode must be release or prototype")

    ordered = tuple(sorted(descriptors, key=ContentDescriptorInput.sort_key))
    groups: dict[str, list[ContentDescriptorInput]] = {}
    for descriptor in ordered:
        groups.setdefault(descriptor.definition_id, []).append(descriptor)

    errors: list[ValidationError] = []
    for definition_id in sorted(groups):
        matches = groups[definition_id]
        if len(matches) > 1:
            errors.append(
                ValidationError(
                    code="duplicate-definition",
                    definition_id=definition_id,
                    detail=f"count={len(matches)}",
                )
            )

    for descriptor in ordered:
        if descriptor.definition_version != SUPPORTED_DEFINITION_VERSION:
            errors.append(
                ValidationError(
                    code="unsupported-definition-version",
                    definition_id=descriptor.definition_id,
                    expected_kind=descriptor.definition_kind,
                    actual_kind=descriptor.definition_kind,
                    expected_version=SUPPORTED_DEFINITION_VERSION,
                    actual_version=descriptor.definition_version,
                )
            )
        if descriptor.provenance_id is None:
            errors.append(
                ValidationError(
                    code="missing-provenance",
                    definition_id=descriptor.definition_id,
                )
            )
        if mode == "release" and descriptor.prototype_only:
            errors.append(
                ValidationError(
                    code="prototype-only-definition",
                    definition_id=descriptor.definition_id,
                )
            )

        for reference in descriptor.references:
            matches = groups.get(reference.definition_id)
            if not matches:
                errors.append(
                    ValidationError(
                        code="missing-definition",
                        definition_id=descriptor.definition_id,
                        referenced_id=reference.definition_id,
                        expected_kind=reference.definition_kind,
                        expected_version=reference.definition_version,
                    )
                )
                continue
            if len(matches) != 1:
                continue
            actual = matches[0]
            if actual.definition_kind != reference.definition_kind:
                errors.append(
                    ValidationError(
                        code="wrong-definition-kind",
                        definition_id=descriptor.definition_id,
                        referenced_id=reference.definition_id,
                        expected_kind=reference.definition_kind,
                        actual_kind=actual.definition_kind,
                        expected_version=reference.definition_version,
                        actual_version=actual.definition_version,
                    )
                )
                continue
            if (
                reference.definition_version != SUPPORTED_DEFINITION_VERSION
                or actual.definition_version != reference.definition_version
            ):
                errors.append(
                    ValidationError(
                        code="unsupported-definition-version",
                        definition_id=descriptor.definition_id,
                        referenced_id=reference.definition_id,
                        expected_kind=reference.definition_kind,
                        actual_kind=actual.definition_kind,
                        expected_version=reference.definition_version,
                        actual_version=actual.definition_version,
                    )
                )

    errors.extend(_find_cycles(ordered, groups))
    if errors:
        raise CatalogValidationError(errors)
    return ordered


def _find_cycles(
    descriptors: Sequence[ContentDescriptorInput],
    groups: Mapping[str, Sequence[ContentDescriptorInput]],
) -> list[ValidationError]:
    nodes = {
        descriptor.definition_id: descriptor
        for descriptor in descriptors
        if len(groups[descriptor.definition_id]) == 1
        and descriptor.definition_version == SUPPORTED_DEFINITION_VERSION
    }
    edges: dict[str, list[str]] = {}
    for definition_id in sorted(nodes):
        descriptor = nodes[definition_id]
        targets: list[str] = []
        for reference in descriptor.references:
            target = nodes.get(reference.definition_id)
            if (
                target is not None
                and reference.definition_kind == target.definition_kind
                and reference.definition_version == target.definition_version
                and reference.definition_version == SUPPORTED_DEFINITION_VERSION
            ):
                targets.append(target.definition_id)
        edges[definition_id] = sorted(targets)

    indices: dict[str, int] = {}
    low_links: dict[str, int] = {}
    stack: list[str] = []
    on_stack: set[str] = set()
    next_index = 0
    errors: list[ValidationError] = []

    def visit(definition_id: str) -> None:
        nonlocal next_index
        indices[definition_id] = next_index
        low_links[definition_id] = next_index
        next_index += 1
        stack.append(definition_id)
        on_stack.add(definition_id)

        for target in edges[definition_id]:
            if target not in indices:
                visit(target)
                low_links[definition_id] = min(
                    low_links[definition_id], low_links[target]
                )
            elif target in on_stack:
                low_links[definition_id] = min(
                    low_links[definition_id], indices[target]
                )

        if low_links[definition_id] != indices[definition_id]:
            return

        component: list[str] = []
        while True:
            member = stack.pop()
            on_stack.remove(member)
            component.append(member)
            if member == definition_id:
                break
        component.sort()
        self_loop = len(component) == 1 and component[0] in edges[component[0]]
        if len(component) > 1 or self_loop:
            errors.append(
                ValidationError(
                    code="cyclic-dependency",
                    definition_id=component[0],
                    detail=f"component_size={len(component)}",
                    cycle=tuple(component),
                )
            )

    for definition_id in sorted(nodes):
        if definition_id not in indices:
            visit(definition_id)
    return errors


def build_documents(
    descriptors: Iterable[ContentDescriptorInput],
    catalog_version: int,
    mode: str,
) -> GeneratedDocuments:
    if catalog_version < 1 or catalog_version > 2_147_483_647:
        raise DescriptorInputError("catalog_version must be a positive 32-bit integer")
    ordered = validate_catalog(descriptors, mode)

    definition_preimage_parts = [
        "format=generated-registry-definition-set-v1",
        f"entry_count={len(ordered)}",
    ]
    for index, descriptor in enumerate(ordered):
        canonical = descriptor.canonical_string()
        definition_preimage_parts.append(
            f"entry_{index:06d}_utf8_length={len(canonical.encode('utf-8'))}"
        )
        definition_preimage_parts.append(f"entry_{index:06d}={canonical}")
    definition_preimage = "\n".join(definition_preimage_parts)
    definition_fingerprint = _formal_sha256(definition_preimage)

    content_version = (
        f"catalog_version={catalog_version}\n"
        f"definition_fingerprint={definition_fingerprint}"
    )
    registry_preimage = (
        "format=generated-machine-registry-v1"
        f"\nschema_version={SUPPORTED_SCHEMA_VERSION}"
        f"\nvalidation_mode={mode}"
        f"\ncontent_version_utf8_length={len(content_version.encode('utf-8'))}"
        f"\ncontent_version={content_version}"
        f"\ndefinition_set_utf8_length={len(definition_preimage.encode('utf-8'))}"
        f"\ndefinition_set={definition_preimage}"
    )
    registry_fingerprint = _formal_sha256(registry_preimage)

    kind_counts = {
        kind: sum(1 for descriptor in ordered if descriptor.definition_kind == kind)
        for kind in KIND_ORDER
    }
    prototype_only_count = sum(1 for descriptor in ordered if descriptor.prototype_only)
    reference_count = sum(len(descriptor.references) for descriptor in ordered)

    review_preimage_parts = [
        "format=generated-registry-review-v1",
        f"schema_version={SUPPORTED_SCHEMA_VERSION}",
        f"registry_fingerprint={registry_fingerprint}",
        f"entry_count={len(ordered)}",
        f"prototype_only_count={prototype_only_count}",
        f"reference_count={reference_count}",
        f"validation_mode={mode}",
        "validation_is_valid=true",
        "validation_error_count=0",
    ]
    for index, kind in enumerate(KIND_ORDER):
        review_preimage_parts.append(f"kind_{index:02d}={kind}|{kind_counts[kind]}")
    for index, descriptor in enumerate(ordered):
        review_preimage_parts.append(
            f"entry_{index:06d}={descriptor.definition_kind}|"
            f"{descriptor.definition_id}|{descriptor.definition_version}|"
            f"{len(descriptor.references)}"
        )
    snapshot_fingerprint = _formal_sha256("\n".join(review_preimage_parts))

    machine_text = _render_machine_json(
        ordered,
        catalog_version,
        mode,
        definition_fingerprint,
        registry_fingerprint,
    )
    review_text = _render_review_json(
        ordered,
        catalog_version,
        mode,
        definition_fingerprint,
        registry_fingerprint,
        snapshot_fingerprint,
        kind_counts,
        prototype_only_count,
        reference_count,
    )
    return GeneratedDocuments(
        machine_bytes=machine_text.encode("utf-8"),
        review_bytes=review_text.encode("utf-8"),
        definition_fingerprint=definition_fingerprint,
        registry_fingerprint=registry_fingerprint,
        snapshot_fingerprint=snapshot_fingerprint,
    )


def _render_machine_json(
    descriptors: Sequence[ContentDescriptorInput],
    catalog_version: int,
    mode: str,
    definition_fingerprint: str,
    registry_fingerprint: str,
) -> str:
    chunks = [
        "{\n",
        f'  "$schema": {_quote(MACHINE_SCHEMA_ID)},\n',
        f'  "schema_version": {SUPPORTED_SCHEMA_VERSION},\n',
        f'  "validation_mode": {_quote(mode)},\n',
        f'  "catalog_version": {catalog_version},\n',
        f'  "definition_fingerprint": {_quote(definition_fingerprint)},\n',
        f'  "registry_fingerprint": {_quote(registry_fingerprint)},\n',
        f'  "entry_count": {len(descriptors)},\n',
        '  "entries": [',
    ]
    if descriptors:
        chunks.append("\n")
    for index, descriptor in enumerate(descriptors):
        chunks.extend(
            (
                "    {\n",
                f'      "definition_kind": {_quote(descriptor.definition_kind)},\n',
                f'      "definition_id": {_quote(descriptor.definition_id)},\n',
                f'      "definition_version": {descriptor.definition_version},\n',
                f'      "provenance_id": {_quote(descriptor.provenance_id or "")},\n',
                f'      "prototype_only": {"true" if descriptor.prototype_only else "false"},\n',
                f'      "reference_count": {len(descriptor.references)},\n',
                '      "references": [',
            )
        )
        if descriptor.references:
            chunks.append("\n")
        for reference_index, reference in enumerate(descriptor.references):
            chunks.extend(
                (
                    "        {\n",
                    f'          "definition_kind": {_quote(reference.definition_kind)},\n',
                    f'          "definition_id": {_quote(reference.definition_id)},\n',
                    f'          "definition_version": {reference.definition_version}\n',
                    "        }",
                )
            )
            if reference_index + 1 < len(descriptor.references):
                chunks.append(",")
            chunks.append("\n")
        chunks.append("      ]\n")
        chunks.append("    }")
        if index + 1 < len(descriptors):
            chunks.append(",")
        chunks.append("\n")
    chunks.append("  ]\n}\n")
    return "".join(chunks)


def _render_review_json(
    descriptors: Sequence[ContentDescriptorInput],
    catalog_version: int,
    mode: str,
    definition_fingerprint: str,
    registry_fingerprint: str,
    snapshot_fingerprint: str,
    kind_counts: Mapping[str, int],
    prototype_only_count: int,
    reference_count: int,
) -> str:
    chunks = [
        "{\n",
        f'  "$schema": {_quote(REVIEW_SCHEMA_ID)},\n',
        f'  "schema_version": {SUPPORTED_SCHEMA_VERSION},\n',
        f'  "machine_registry_schema_version": {SUPPORTED_SCHEMA_VERSION},\n',
        '  "validation": {\n',
        f'    "mode": {_quote(mode)},\n',
        '    "is_valid": true,\n',
        '    "error_count": 0\n',
        '  },\n',
        f'  "catalog_version": {catalog_version},\n',
        f'  "definition_fingerprint": {_quote(definition_fingerprint)},\n',
        f'  "registry_fingerprint": {_quote(registry_fingerprint)},\n',
        f'  "snapshot_fingerprint": {_quote(snapshot_fingerprint)},\n',
        '  "summary": {\n',
        f'    "entry_count": {len(descriptors)},\n',
        f'    "prototype_only_count": {prototype_only_count},\n',
        f'    "reference_count": {reference_count},\n',
        '    "kind_counts": [\n',
    ]
    for index, kind in enumerate(KIND_ORDER):
        chunks.append(
            f'      {{ "definition_kind": {_quote(kind)}, '
            f'"count": {kind_counts[kind]} }}'
        )
        if index + 1 < len(KIND_ORDER):
            chunks.append(",")
        chunks.append("\n")
    chunks.extend(("    ]\n", "  },\n", '  "entries": ['))
    if descriptors:
        chunks.append("\n")
    for index, descriptor in enumerate(descriptors):
        chunks.append(
            f'    {{ "definition_kind": {_quote(descriptor.definition_kind)}, '
            f'"definition_id": {_quote(descriptor.definition_id)}, '
            f'"definition_version": {descriptor.definition_version}, '
            f'"reference_count": {len(descriptor.references)} }}'
        )
        if index + 1 < len(descriptors):
            chunks.append(",")
        chunks.append("\n")
    chunks.append("  ]\n}\n")
    return "".join(chunks)


def _parse_machine_output(
    data: bytes, source: str
) -> tuple[tuple[ContentDescriptorInput, ...], int, str]:
    try:
        raw = json.loads(
            data.decode("utf-8"),
            object_pairs_hook=_object_without_duplicate_keys,
        )
    except (UnicodeDecodeError, json.JSONDecodeError, DescriptorInputError) as error:
        raise GeneratedOutputDriftError((f"{source}: invalid generated JSON: {error}",))

    if not isinstance(raw, dict):
        raise GeneratedOutputDriftError((f"{source}: root must be an object",))
    _require_exact_keys(
        raw,
        frozenset(
            {
                "$schema",
                "schema_version",
                "validation_mode",
                "catalog_version",
                "definition_fingerprint",
                "registry_fingerprint",
                "entry_count",
                "entries",
            }
        ),
        "machine registry",
    )
    if raw["$schema"] != MACHINE_SCHEMA_ID or raw["schema_version"] != 1:
        raise GeneratedOutputDriftError((f"{source}: unsupported machine schema",))
    mode = raw["validation_mode"]
    if mode not in {"release", "prototype"}:
        raise GeneratedOutputDriftError((f"{source}: invalid validation mode",))
    catalog_version = _require_positive_int(raw["catalog_version"], "catalog_version")
    entries = raw["entries"]
    if not isinstance(entries, list):
        raise GeneratedOutputDriftError((f"{source}: entries must be an array",))
    if raw["entry_count"] != len(entries):
        raise GeneratedOutputDriftError((f"{source}: entry_count drift",))

    descriptors: list[ContentDescriptorInput] = []
    for index, entry in enumerate(entries):
        if not isinstance(entry, dict):
            raise GeneratedOutputDriftError(
                (f"{source}: entries[{index}] must be an object",)
            )
        _require_exact_keys(
            entry,
            frozenset(
                {
                    "definition_kind",
                    "definition_id",
                    "definition_version",
                    "provenance_id",
                    "prototype_only",
                    "reference_count",
                    "references",
                }
            ),
            f"entries[{index}]",
        )
        if not isinstance(entry["references"], list):
            raise GeneratedOutputDriftError(
                (f"{source}: entries[{index}].references must be an array",)
            )
        if entry["reference_count"] != len(entry["references"]):
            raise GeneratedOutputDriftError(
                (f"{source}: entries[{index}].reference_count drift",)
            )
        descriptor_raw = {
            "definition_kind": entry["definition_kind"],
            "definition_id": entry["definition_id"],
            "definition_version": entry["definition_version"],
            "provenance_id": entry["provenance_id"],
            "prototype_only": entry["prototype_only"],
            "references": entry["references"],
        }
        descriptors.append(
            parse_descriptor_mapping(
                descriptor_raw,
                source=f"{source}:entries[{index}]",
                require_input_schema=False,
            )
        )
    return tuple(descriptors), catalog_version, mode


def validate_existing_pair(registry_path: Path, review_path: Path) -> None:
    registry_exists = registry_path.is_file()
    review_exists = review_path.is_file()
    if not registry_exists and not review_exists:
        return
    if registry_exists != review_exists:
        missing = review_path if registry_exists else registry_path
        raise GeneratedOutputDriftError(
            (f"generated output pair is incomplete; missing {missing.as_posix()}",)
        )

    registry_bytes = registry_path.read_bytes()
    review_bytes = review_path.read_bytes()
    try:
        descriptors, catalog_version, mode = _parse_machine_output(
            registry_bytes, registry_path.as_posix()
        )
        expected = build_documents(descriptors, catalog_version, mode)
    except CatalogValidationError as error:
        raise GeneratedOutputDriftError(
            (f"{registry_path.as_posix()}: invalid embedded catalog: {error}",)
        ) from error
    except DescriptorInputError as error:
        raise GeneratedOutputDriftError(
            (f"{registry_path.as_posix()}: malformed generated content: {error}",)
        ) from error

    messages: list[str] = []
    if registry_bytes != expected.machine_bytes:
        messages.append(
            f"{registry_path.as_posix()}: non-canonical or fingerprint-invalid generated file"
        )
    if review_bytes != expected.review_bytes:
        messages.append(
            f"{review_path.as_posix()}: non-canonical, mismatched, or manually edited snapshot"
        )
    if messages:
        raise GeneratedOutputDriftError(messages)


class GenerationLock:
    def __init__(self, path: Path) -> None:
        self.path = path
        self._owned = False

    def __enter__(self) -> "GenerationLock":
        self.path.parent.mkdir(parents=True, exist_ok=True)
        try:
            descriptor = os.open(
                self.path,
                os.O_CREAT | os.O_EXCL | os.O_WRONLY,
                0o644,
            )
        except FileExistsError as error:
            raise ConcurrentGenerationError(
                f"registry generation lock already exists: {self.path.as_posix()}"
            ) from error
        try:
            os.write(descriptor, b"cs-011-registry-generation-lock\n")
        finally:
            os.close(descriptor)
        self._owned = True
        return self

    def __exit__(self, exc_type: object, exc: object, traceback: object) -> None:
        if self._owned:
            try:
                self.path.unlink()
            except FileNotFoundError:
                pass
            self._owned = False


def atomic_write_pair(
    registry_path: Path,
    registry_bytes: bytes,
    review_path: Path,
    review_bytes: bytes,
) -> None:
    outputs = ((registry_path, registry_bytes), (review_path, review_bytes))
    temporary_paths: list[Path] = []
    originals: dict[Path, bytes | None] = {}
    replaced: list[Path] = []

    try:
        for path, data in outputs:
            path.parent.mkdir(parents=True, exist_ok=True)
            originals[path] = path.read_bytes() if path.exists() else None
            temporary = path.with_name(f".{path.name}.cs011.tmp")
            if temporary.exists():
                raise RegistryToolError(
                    f"stale atomic-write temporary file exists: {temporary.as_posix()}"
                )
            with temporary.open("xb") as handle:
                handle.write(data)
                handle.flush()
                os.fsync(handle.fileno())
            temporary_paths.append(temporary)

        for (path, _), temporary in zip(outputs, temporary_paths):
            os.replace(temporary, path)
            replaced.append(path)
        temporary_paths.clear()
    except Exception:
        for path in reversed(replaced):
            original = originals[path]
            if original is None:
                try:
                    path.unlink()
                except FileNotFoundError:
                    pass
            else:
                recovery = path.with_name(f".{path.name}.cs011.rollback")
                try:
                    with recovery.open("wb") as handle:
                        handle.write(original)
                        handle.flush()
                        os.fsync(handle.fileno())
                    os.replace(recovery, path)
                finally:
                    try:
                        recovery.unlink()
                    except FileNotFoundError:
                        pass
        raise
    finally:
        for temporary in temporary_paths:
            try:
                temporary.unlink()
            except FileNotFoundError:
                pass


def generate_repository(
    repo_root: Path,
    descriptor_roots: Sequence[Path] = DEFAULT_DESCRIPTOR_ROOTS,
    catalog_version: int = 1,
    mode: str = "release",
    registry_output: Path = DEFAULT_REGISTRY_OUTPUT,
    review_output: Path = DEFAULT_REVIEW_OUTPUT,
    repair_drift: bool = False,
) -> GeneratedDocuments:
    repo_root = repo_root.resolve()
    registry_path = (
        registry_output if registry_output.is_absolute() else repo_root / registry_output
    )
    review_path = review_output if review_output.is_absolute() else repo_root / review_output
    lock_path = repo_root / LOCK_PATH

    descriptors = scan_descriptors(repo_root, descriptor_roots)
    documents = build_documents(descriptors, catalog_version, mode)

    with GenerationLock(lock_path):
        if not repair_drift:
            validate_existing_pair(registry_path, review_path)
        atomic_write_pair(
            registry_path,
            documents.machine_bytes,
            review_path,
            documents.review_bytes,
        )
    return documents


def check_repository(
    repo_root: Path,
    descriptor_roots: Sequence[Path] = DEFAULT_DESCRIPTOR_ROOTS,
    catalog_version: int = 1,
    mode: str = "release",
    registry_output: Path = DEFAULT_REGISTRY_OUTPUT,
    review_output: Path = DEFAULT_REVIEW_OUTPUT,
) -> GeneratedDocuments:
    repo_root = repo_root.resolve()
    descriptors = scan_descriptors(repo_root, descriptor_roots)
    expected = build_documents(descriptors, catalog_version, mode)
    registry_path = (
        registry_output if registry_output.is_absolute() else repo_root / registry_output
    )
    review_path = review_output if review_output.is_absolute() else repo_root / review_output

    messages: list[str] = []
    for path, expected_bytes in (
        (registry_path, expected.machine_bytes),
        (review_path, expected.review_bytes),
    ):
        if not path.is_file():
            messages.append(f"{path.as_posix()}: missing generated output")
            continue
        actual = path.read_bytes()
        if actual != expected_bytes:
            messages.append(
                f"{path.as_posix()}: drift expected_sha256={_raw_sha256(expected_bytes)} "
                f"actual_sha256={_raw_sha256(actual)}"
            )
    if messages:
        raise GeneratedOutputDriftError(messages)
    return expected


def parse_args(argv: Sequence[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Generate or verify the canonical Shooter Mover content registry and "
            "human review snapshot."
        )
    )
    parser.add_argument("command", choices=("generate", "check", "validate"))
    parser.add_argument(
        "--root",
        type=Path,
        default=Path(__file__).resolve().parents[2],
        help="Repository root. Defaults to the parent containing tools/.",
    )
    parser.add_argument(
        "--descriptor-root",
        type=Path,
        action="append",
        dest="descriptor_roots",
        help=(
            "Descriptor root or descriptor file. Repeatable. Defaults to the approved "
            "Content/Definitions, Content/SharedModules, and ContentPackages roots."
        ),
    )
    parser.add_argument("--catalog-version", type=int, default=1)
    parser.add_argument("--mode", choices=("release", "prototype"), default="release")
    parser.add_argument(
        "--registry-output",
        type=Path,
        default=DEFAULT_REGISTRY_OUTPUT,
    )
    parser.add_argument(
        "--review-output",
        type=Path,
        default=DEFAULT_REVIEW_OUTPUT,
    )
    parser.add_argument(
        "--repair-drift",
        action="store_true",
        help=(
            "For generate only: replace an incomplete or manually edited output pair. "
            "Ordinary generation fails closed instead."
        ),
    )
    return parser.parse_args(argv)


def _print_success(command: str, documents: GeneratedDocuments) -> None:
    print(f"registry-{command}: ok")
    print(f"machine_sha256={documents.machine_checksum}")
    print(f"review_sha256={documents.review_checksum}")
    print(f"definition_fingerprint={documents.definition_fingerprint}")
    print(f"registry_fingerprint={documents.registry_fingerprint}")
    print(f"snapshot_fingerprint={documents.snapshot_fingerprint}")


def main(argv: Sequence[str] | None = None) -> int:
    arguments = parse_args(sys.argv[1:] if argv is None else argv)
    descriptor_roots = (
        tuple(arguments.descriptor_roots)
        if arguments.descriptor_roots
        else DEFAULT_DESCRIPTOR_ROOTS
    )
    try:
        if arguments.command == "generate":
            documents = generate_repository(
                arguments.root,
                descriptor_roots,
                arguments.catalog_version,
                arguments.mode,
                arguments.registry_output,
                arguments.review_output,
                arguments.repair_drift,
            )
        elif arguments.command == "check":
            if arguments.repair_drift:
                raise DescriptorInputError("--repair-drift is valid only with generate")
            documents = check_repository(
                arguments.root,
                descriptor_roots,
                arguments.catalog_version,
                arguments.mode,
                arguments.registry_output,
                arguments.review_output,
            )
        else:
            if arguments.repair_drift:
                raise DescriptorInputError("--repair-drift is valid only with generate")
            descriptors = scan_descriptors(arguments.root.resolve(), descriptor_roots)
            documents = build_documents(
                descriptors,
                arguments.catalog_version,
                arguments.mode,
            )
        _print_success(arguments.command, documents)
        return 0
    except CatalogValidationError as error:
        print("registry-validation: failed", file=sys.stderr)
        for validation_error in error.errors:
            print("---", file=sys.stderr)
            print(validation_error.to_canonical_string(), file=sys.stderr)
        return 2
    except DescriptorInputError as error:
        print(f"registry-input: failed\n{error}", file=sys.stderr)
        return 2
    except GeneratedOutputDriftError as error:
        print("registry-drift: failed", file=sys.stderr)
        for message in error.messages:
            print(message, file=sys.stderr)
        return 3
    except ConcurrentGenerationError as error:
        print(f"registry-lock: failed\n{error}", file=sys.stderr)
        return 4
    except (OSError, RegistryToolError) as error:
        print(f"registry-tool: failed\n{error}", file=sys.stderr)
        return 5


if __name__ == "__main__":
    raise SystemExit(main())
