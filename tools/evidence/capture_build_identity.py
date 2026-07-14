#!/usr/bin/env python3
"""Compose and verify one deterministic offline Shooter Mover evidence identity."""

from __future__ import annotations

import argparse
import hashlib
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, List, Optional, Sequence, Tuple


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
SHA256_RE = re.compile(r"^sha256:[0-9a-f]{64}$")
SOURCE_COMMIT_RE = re.compile(r"^[0-9a-f]{40}$")
UNITY_VERSION_RE = re.compile(r"^[0-9]+\.[0-9]+\.[0-9]+[abfp][0-9]+$")
EVIDENCE_TOKEN_RE = re.compile(r"^[A-Za-z0-9._-]{1,64}$")
STABLE_ID_COMPONENT_RE = re.compile(r"^[a-z0-9]+(?:-[a-z0-9]+)*$")
PROVISIONAL_EXACT = frozenset({"unknown", "unset", "todo", "tbd", "null", "none"})


class InvalidEvidence(ValueError):
    """An expected fail-closed validation result."""

    def __init__(self, code: str, message: str) -> None:
        super().__init__(message)
        self.code = code
        self.message = message


@dataclass(frozen=True)
class BuildIdentity:
    kind: str
    source_state: str
    source_commit: str
    unity_version: str
    package_lock_fingerprint: str
    content_fingerprint: str
    save_schema: int
    artifact_checksum: Optional[str]


@dataclass(frozen=True)
class ContentVersion:
    catalog_version: int
    definition_fingerprint: str


@dataclass(frozen=True)
class EvidenceRecord:
    canonical_payload: str
    fingerprint: str

    @property
    def canonical_text(self) -> str:
        return self.canonical_payload + "\nrecord_fingerprint=" + self.fingerprint


def _read_utf8(path: Path) -> str:
    try:
        data = path.read_bytes()
    except OSError as exc:
        raise InvalidEvidence(
            "input-read-failed", f"could not read '{path}': {exc}"
        ) from exc

    try:
        return data.decode("utf-8")
    except UnicodeDecodeError as exc:
        raise InvalidEvidence(
            "malformed-utf8", f"'{path}' is not strict UTF-8: {exc}"
        ) from exc


def _split_canonical(text: str, expected_count: int, name: str) -> List[str]:
    if not text or "\r" in text or text.endswith("\n"):
        raise InvalidEvidence(
            f"malformed-{name}",
            f"{name} must use strict LF-separated canonical text with no trailing newline",
        )

    lines = text.split("\n")
    if len(lines) != expected_count:
        raise InvalidEvidence(
            f"malformed-{name}",
            f"{name} must contain exactly {expected_count} ordered fields",
        )
    return lines


def _read_field(line: str, expected_name: str, manifest_name: str) -> str:
    prefix = expected_name + "="
    if not line.startswith(prefix):
        raise InvalidEvidence(
            f"malformed-{manifest_name}",
            f"expected {expected_name} in its canonical position",
        )
    return line[len(prefix) :]


def _require_sha256(value: str, field_name: str, manifest_name: str) -> str:
    if not SHA256_RE.fullmatch(value) or set(value[7:]) == {"0"}:
        raise InvalidEvidence(
            f"malformed-{manifest_name}",
            f"{field_name} must be sha256:<64 lowercase non-zero hex characters>",
        )
    return value


def _parse_positive_int(value: str, field_name: str, manifest_name: str) -> int:
    if not re.fullmatch(r"[1-9][0-9]*", value):
        raise InvalidEvidence(
            f"malformed-{manifest_name}",
            f"{field_name} must be canonical positive decimal text",
        )
    try:
        parsed = int(value, 10)
    except ValueError as exc:
        raise InvalidEvidence(
            f"malformed-{manifest_name}", f"{field_name} is outside the supported range"
        ) from exc
    if parsed > 2_147_483_647:
        raise InvalidEvidence(
            f"malformed-{manifest_name}", f"{field_name} is outside the supported range"
        )
    return parsed


def parse_build_identity(text: str) -> BuildIdentity:
    lines = _split_canonical(text, 8, "build-identity")
    kind = _read_field(lines[0], "identity_kind", "build-identity")
    source_state = _read_field(lines[1], "source_state", "build-identity")
    source_commit = _read_field(lines[2], "source_commit", "build-identity")
    unity_version = _read_field(lines[3], "unity_version", "build-identity")
    package_lock_fingerprint = _read_field(
        lines[4], "package_lock_fingerprint", "build-identity"
    )
    content_fingerprint = _read_field(
        lines[5], "content_fingerprint", "build-identity"
    )
    save_schema_text = _read_field(lines[6], "save_schema", "build-identity")
    artifact_checksum_text = _read_field(
        lines[7], "artifact_checksum", "build-identity"
    )

    if kind not in ("formal-release", "development"):
        raise InvalidEvidence(
            "malformed-build-identity",
            "identity_kind must be formal-release or development",
        )
    if source_state not in ("clean", "dirty"):
        raise InvalidEvidence(
            "malformed-build-identity", "source_state must be clean or dirty"
        )
    if not SOURCE_COMMIT_RE.fullmatch(source_commit) or set(source_commit) == {"0"}:
        raise InvalidEvidence(
            "malformed-build-identity",
            "source_commit must be one complete lowercase non-zero Git SHA",
        )
    if not UNITY_VERSION_RE.fullmatch(unity_version):
        raise InvalidEvidence(
            "malformed-build-identity", "unity_version is not canonical"
        )

    package_lock_fingerprint = _require_sha256(
        package_lock_fingerprint, "package_lock_fingerprint", "build-identity"
    )
    content_fingerprint = _require_sha256(
        content_fingerprint, "content_fingerprint", "build-identity"
    )
    save_schema = _parse_positive_int(
        save_schema_text, "save_schema", "build-identity"
    )
    artifact_checksum = (
        None
        if artifact_checksum_text == "null"
        else _require_sha256(
            artifact_checksum_text, "artifact_checksum", "build-identity"
        )
    )

    if kind == "formal-release" and source_state != "clean":
        raise InvalidEvidence(
            "malformed-build-identity", "formal-release identity must be clean"
        )
    if kind == "formal-release" and artifact_checksum is None:
        raise InvalidEvidence(
            "malformed-build-identity",
            "formal-release identity requires a final artifact checksum",
        )

    return BuildIdentity(
        kind=kind,
        source_state=source_state,
        source_commit=source_commit,
        unity_version=unity_version,
        package_lock_fingerprint=package_lock_fingerprint,
        content_fingerprint=content_fingerprint,
        save_schema=save_schema,
        artifact_checksum=artifact_checksum,
    )


def parse_content_version(text: str) -> ContentVersion:
    lines = _split_canonical(text, 2, "content-version")
    catalog_version = _parse_positive_int(
        _read_field(lines[0], "catalog_version", "content-version"),
        "catalog_version",
        "content-version",
    )
    definition_fingerprint = _require_sha256(
        _read_field(lines[1], "definition_fingerprint", "content-version"),
        "definition_fingerprint",
        "content-version",
    )
    return ContentVersion(catalog_version, definition_fingerprint)


def _sha256_bytes(data: bytes) -> str:
    return "sha256:" + hashlib.sha256(data).hexdigest()


def _verify_package_lock(path: Path, expected_fingerprint: str) -> None:
    try:
        actual = _sha256_bytes(path.read_bytes())
    except OSError as exc:
        raise InvalidEvidence(
            "package-lock-read-failed", f"could not read '{path}': {exc}"
        ) from exc
    if actual != expected_fingerprint:
        raise InvalidEvidence(
            "inconsistent-package-lock",
            "BuildIdentity package_lock_fingerprint does not match the exact supplied package-lock bytes",
        )


def _parse_project_version(path: Path) -> Tuple[str, str]:
    text = _read_utf8(path)
    versions: List[str] = []
    revisions: List[str] = []
    for line in text.splitlines():
        if line.startswith("m_EditorVersion:"):
            versions.append(line.split(":", 1)[1].strip())
        elif line.startswith("m_EditorVersionWithRevision:"):
            revisions.append(line.split(":", 1)[1].strip())

    if len(versions) != 1 or len(revisions) != 1:
        raise InvalidEvidence(
            "malformed-project-version",
            "ProjectVersion.txt must contain exactly one editor version and revision",
        )
    if not UNITY_VERSION_RE.fullmatch(versions[0]):
        raise InvalidEvidence(
            "malformed-project-version", "ProjectVersion.txt editor version is not canonical"
        )
    if not revisions[0].startswith(versions[0] + " (") or not revisions[0].endswith(")"):
        raise InvalidEvidence(
            "malformed-project-version",
            "ProjectVersion.txt editor revision is missing or inconsistent",
        )
    return versions[0], revisions[0]


def _is_provisional(value: str) -> bool:
    lowered = value.lower()
    return (
        lowered in PROVISIONAL_EXACT
        or "provisional" in lowered
        or "placeholder" in lowered
    )


def _validate_evidence_token(value: str, field_name: str) -> None:
    if not value:
        raise InvalidEvidence(f"missing-{field_name}", f"{field_name} is required")
    if not EVIDENCE_TOKEN_RE.fullmatch(value):
        raise InvalidEvidence(
            f"malformed-{field_name}",
            f"{field_name} must contain 1-64 ASCII letters, digits, dot, underscore, or hyphen",
        )
    if _is_provisional(value):
        raise InvalidEvidence(
            f"provisional-{field_name}", f"{field_name} is provisional or a placeholder"
        )


def _validate_stable_id(value: str) -> None:
    if not value:
        raise InvalidEvidence(
            "missing-tuning-profile-id", "a canonical tuning-profile StableId is required"
        )
    if value.count(".") != 1:
        raise InvalidEvidence(
            "malformed-tuning-profile-id",
            "StableId must contain exactly one dot between namespace and value",
        )
    namespace_name, local_value = value.split(".", 1)
    if (
        len(namespace_name) > 32
        or len(local_value) > 96
        or len(value) > 128
        or not STABLE_ID_COMPONENT_RE.fullmatch(namespace_name)
        or not STABLE_ID_COMPONENT_RE.fullmatch(local_value)
    ):
        raise InvalidEvidence(
            "malformed-tuning-profile-id",
            "tuning-profile ID must match StableId v1 lowercase component rules",
        )
    if _is_provisional(namespace_name) or _is_provisional(local_value):
        raise InvalidEvidence(
            "provisional-tuning-profile-id",
            "the tuning-profile StableId contains a provisional or placeholder component",
        )


def compose_record(
    build_identity: BuildIdentity,
    content_version: ContentVersion,
    dirty_state_policy: str,
    build_target: str,
    build_configuration: str,
    tuning_profile_id: str,
) -> EvidenceRecord:
    if build_identity.artifact_checksum is None:
        raise InvalidEvidence(
            "provisional-build-identity",
            "evidence requires a final artifact checksum; artifact_checksum=null is provisional",
        )
    if build_identity.content_fingerprint != content_version.definition_fingerprint:
        raise InvalidEvidence(
            "inconsistent-content-version",
            "BuildIdentity content_fingerprint must equal ContentVersion definition_fingerprint for evidence identity v1",
        )

    if dirty_state_policy not in ("reject-dirty", "allow-dirty-development"):
        code = (
            "missing-dirty-state-policy"
            if not dirty_state_policy
            else "unsupported-dirty-state-policy"
        )
        raise InvalidEvidence(
            code,
            "dirty_state_policy must be reject-dirty or allow-dirty-development",
        )
    if dirty_state_policy == "reject-dirty" and build_identity.source_state == "dirty":
        raise InvalidEvidence(
            "inconsistent-dirty-state-policy",
            "reject-dirty cannot accept a dirty BuildIdentity",
        )
    if dirty_state_policy == "allow-dirty-development" and not (
        build_identity.kind == "development" and build_identity.source_state == "dirty"
    ):
        raise InvalidEvidence(
            "inconsistent-dirty-state-policy",
            "allow-dirty-development requires a dirty development BuildIdentity",
        )

    _validate_evidence_token(build_target, "build-target")
    _validate_evidence_token(build_configuration, "build-configuration")
    _validate_stable_id(tuning_profile_id)

    payload_lines = [
        "evidence_identity_schema=1",
        "build_identity_kind=" + build_identity.kind,
        "source_commit=" + build_identity.source_commit,
        "source_state=" + build_identity.source_state,
        "dirty_state_policy=" + dirty_state_policy,
        "unity_version=" + build_identity.unity_version,
        "package_lock_fingerprint=" + build_identity.package_lock_fingerprint,
        "content_catalog_version=" + str(content_version.catalog_version),
        "content_definition_fingerprint=" + content_version.definition_fingerprint,
        "save_schema_version=" + str(build_identity.save_schema),
        "artifact_checksum=" + build_identity.artifact_checksum,
        "build_target=" + build_target,
        "build_configuration=" + build_configuration,
        "tuning_profile_id=" + tuning_profile_id,
    ]
    canonical_payload = "\n".join(payload_lines)
    return EvidenceRecord(canonical_payload, _sha256_bytes(canonical_payload.encode("utf-8")))


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Verify frozen local identity inputs and emit one canonical evidence identity. "
            "No network, credential, telemetry, registry, or build operation is performed."
        )
    )
    parser.add_argument("--build-identity", required=True, type=Path)
    parser.add_argument("--content-version", required=True, type=Path)
    parser.add_argument(
        "--project-version",
        type=Path,
        default=REPOSITORY_ROOT / "ProjectSettings" / "ProjectVersion.txt",
    )
    parser.add_argument(
        "--package-lock",
        type=Path,
        default=REPOSITORY_ROOT / "Packages" / "packages-lock.json",
    )
    parser.add_argument("--dirty-state-policy", required=True)
    parser.add_argument("--build-target", required=True)
    parser.add_argument("--build-configuration", required=True)
    parser.add_argument("--tuning-profile-id", required=True)
    parser.add_argument(
        "--output",
        type=Path,
        help="Write exact UTF-8/LF canonical bytes to this local path instead of stdout.",
    )
    return parser


def run(arguments: argparse.Namespace) -> EvidenceRecord:
    build_identity = parse_build_identity(_read_utf8(arguments.build_identity))
    content_version = parse_content_version(_read_utf8(arguments.content_version))

    pinned_unity_version, _ = _parse_project_version(arguments.project_version)
    if pinned_unity_version != build_identity.unity_version:
        raise InvalidEvidence(
            "inconsistent-unity-version",
            "BuildIdentity unity_version does not match the supplied ProjectVersion.txt",
        )

    _verify_package_lock(
        arguments.package_lock, build_identity.package_lock_fingerprint
    )

    return compose_record(
        build_identity=build_identity,
        content_version=content_version,
        dirty_state_policy=arguments.dirty_state_policy,
        build_target=arguments.build_target,
        build_configuration=arguments.build_configuration,
        tuning_profile_id=arguments.tuning_profile_id,
    )


def main(argv: Optional[Sequence[str]] = None) -> int:
    parser = _build_parser()
    arguments = parser.parse_args(argv)
    try:
        record = run(arguments)
        output_bytes = record.canonical_text.encode("utf-8")
        if arguments.output is None:
            sys.stdout.buffer.write(output_bytes)
        else:
            arguments.output.parent.mkdir(parents=True, exist_ok=True)
            arguments.output.write_bytes(output_bytes)
        return 0
    except InvalidEvidence as exc:
        print(f"invalid evidence: {exc.code}: {exc.message}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
