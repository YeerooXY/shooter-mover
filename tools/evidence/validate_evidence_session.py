#!/usr/bin/env python3
"""Validate immutable Stage 1 evidence packages and one bound human review."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sys
import unicodedata
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Any, Dict, Iterable, List, Mapping, Optional, Sequence, Tuple

TOOL_NAME = "validate_evidence_session.py"
TOOL_VERSION = "1.0.0"
PROTOCOL_SCHEMA = "shooter-mover.stage1-evidence-protocol"
PROTOCOL_VERSION = 1
REVIEW_SCHEMA = "shooter-mover.stage1-evidence-review"
REVIEW_VERSION = 1
VALIDATION_SCHEMA = "shooter-mover.evidence-session-validation"
VALIDATION_VERSION = 1
MANIFEST_NAME = "evidence-manifest-v1.json"
CHECKSUM_NAME = "evidence-manifest-v1.sha256"
MANIFEST_SCHEMA = "shooter-mover.evidence-manifest"
DESCRIPTOR_SCHEMA = "shooter-mover.evidence-package-descriptor"
CONFIGURATION_SCHEMA = "shooter-mover.evidence-run-configuration"
DIAGNOSTICS_SUMMARY_SCHEMA = "shooter-mover.eh009-entrypoint-diagnostics"
WINDOWS_SMOKE_SCHEMA = "shooter-mover.eh009-windows-smoke"
SHA256_RE = re.compile(r"^sha256:[0-9a-f]{64}$")
TOKEN_RE = re.compile(r"^[A-Za-z0-9._-]{1,128}$")
STABLE_ID_RE = re.compile(r"^[a-z0-9]+(?:-[a-z0-9]+)*\.[a-z0-9]+(?:-[a-z0-9]+)*$")
SOURCE_COMMIT_RE = re.compile(r"^[0-9a-f]{40}$")
WINDOWS_DRIVE_RE = re.compile(r"^[A-Za-z]:")
RESERVED_PACKAGE_FILES = frozenset({MANIFEST_NAME, CHECKSUM_NAME})

CS012_REASON_CODES = (
    "duplicate-run-start",
    "run-end-without-start",
    "duplicate-run-end",
    "event-after-run-end",
    "missing-run-start",
    "missing-run-end",
    "crash-before-run-end",
    "run-aborted",
    "invalidating-diagnostic-command",
    "fault-injection-used",
    "mission-state-override-used",
    "progression-override-used",
    "performance-budget-breach",
    "unhandled-exception",
    "timeout",
    "missing-required-asset",
    "diagnostics-capacity-reached",
)
CS012_REASON_SET = frozenset(CS012_REASON_CODES)
INVALIDATING_COMMAND_REASONS = frozenset(
    {
        "invalidating-diagnostic-command",
        "fault-injection-used",
        "mission-state-override-used",
        "progression-override-used",
    }
)
BASE_REQUIRED_PROOF = frozenset(
    {
        "identity/build-identity.txt",
        "identity/content-version.txt",
        "diagnostics/test-results.xml",
        "diagnostics/unity.log",
    }
)
WINDOWS_REQUIRED_PROOF = frozenset(
    {
        "diagnostics/windows/player-pass-1.log",
        "diagnostics/windows/player-pass-2.log",
        "diagnostics/windows/windows-smoke.json",
    }
)


class EvidenceReviewError(ValueError):
    """Fail-closed validation error with stable remediation semantics."""

    def __init__(self, code: str, message: str, rerun_required: bool = False) -> None:
        super().__init__(message)
        self.code = code
        self.message = message
        self.rerun_required = rerun_required


def _reject_duplicate_pairs(pairs: Iterable[Tuple[str, Any]]) -> Dict[str, Any]:
    result: Dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise EvidenceReviewError("duplicate-json-key", f"duplicate JSON key: {key}")
        result[key] = value
    return result


def canonical_json_bytes(value: Any) -> bytes:
    try:
        return (
            json.dumps(
                value,
                ensure_ascii=True,
                allow_nan=False,
                sort_keys=True,
                separators=(",", ":"),
            )
            + "\n"
        ).encode("utf-8")
    except (TypeError, ValueError) as exc:
        raise EvidenceReviewError(
            "non-canonical-json", f"value cannot be canonicalized: {exc}"
        ) from exc


def sha256_bytes(data: bytes) -> str:
    return "sha256:" + hashlib.sha256(data).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    try:
        with path.open("rb") as stream:
            while True:
                chunk = stream.read(1024 * 1024)
                if not chunk:
                    break
                digest.update(chunk)
    except OSError as exc:
        raise EvidenceReviewError(
            "input-read-failed", f"could not hash '{path}': {exc}", rerun_required=True
        ) from exc
    return "sha256:" + digest.hexdigest()


def _read_bytes(path: Path, *, rerun_required: bool = False) -> bytes:
    try:
        return path.read_bytes()
    except FileNotFoundError as exc:
        raise EvidenceReviewError(
            "missing-proof",
            f"required file is missing: {path}",
            rerun_required=rerun_required,
        ) from exc
    except OSError as exc:
        raise EvidenceReviewError(
            "input-read-failed",
            f"could not read '{path}': {exc}",
            rerun_required=rerun_required,
        ) from exc


def _load_json_bytes(data: bytes, label: str, *, incomplete_review: bool = False) -> Any:
    try:
        text = data.decode("utf-8-sig")
    except UnicodeDecodeError as exc:
        code = "incomplete-human-review" if incomplete_review else "malformed-json"
        raise EvidenceReviewError(code, f"{label} is not strict UTF-8: {exc}") from exc
    try:
        return json.loads(
            text,
            object_pairs_hook=_reject_duplicate_pairs,
            parse_constant=lambda value: (_ for _ in ()).throw(
                EvidenceReviewError("malformed-json", f"{label} contains {value}")
            ),
        )
    except EvidenceReviewError:
        raise
    except (json.JSONDecodeError, ValueError) as exc:
        code = "incomplete-human-review" if incomplete_review else "malformed-json"
        raise EvidenceReviewError(code, f"{label} is not valid JSON: {exc}") from exc


def _load_json(path: Path, label: str, *, incomplete_review: bool = False) -> Tuple[Any, bytes]:
    data = _read_bytes(path)
    return _load_json_bytes(data, label, incomplete_review=incomplete_review), data


def _require_object(value: Any, label: str, code: str = "malformed-object") -> Dict[str, Any]:
    if not isinstance(value, dict):
        raise EvidenceReviewError(code, f"{label} must be a JSON object")
    return value


def _require_exact_keys(
    value: Mapping[str, Any],
    required: Iterable[str],
    label: str,
    *,
    code: str = "malformed-object",
) -> None:
    required_set = set(required)
    actual = set(value)
    missing = sorted(required_set - actual)
    unknown = sorted(actual - required_set)
    if missing or unknown:
        details: List[str] = []
        if missing:
            details.append("missing=" + ",".join(missing))
        if unknown:
            details.append("unknown=" + ",".join(unknown))
        raise EvidenceReviewError(code, f"{label} has invalid fields ({'; '.join(details)})")


def _require_string(value: Any, label: str, code: str = "malformed-string") -> str:
    if not isinstance(value, str):
        raise EvidenceReviewError(code, f"{label} must be a string")
    return value


def _require_bool(value: Any, label: str, code: str = "malformed-boolean") -> bool:
    if not isinstance(value, bool):
        raise EvidenceReviewError(code, f"{label} must be boolean")
    return value


def _require_int(
    value: Any, label: str, minimum: int = 0, code: str = "malformed-integer"
) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value < minimum:
        raise EvidenceReviewError(code, f"{label} must be an integer >= {minimum}")
    return value


def _require_positive_decimal(value: str, label: str) -> int:
    if not value.isdigit() or int(value) < 1:
        raise EvidenceReviewError(
            "malformed-identity", f"{label} must be a positive decimal integer", True
        )
    return int(value)


def _require_token(value: Any, label: str, code: str = "malformed-token") -> str:
    text = _require_string(value, label, code)
    if not TOKEN_RE.fullmatch(text):
        raise EvidenceReviewError(code, f"{label} is not a canonical token")
    return text


def _require_stable_id(value: Any, label: str, code: str = "malformed-stable-id") -> str:
    text = _require_string(value, label, code)
    if not STABLE_ID_RE.fullmatch(text):
        raise EvidenceReviewError(code, f"{label} is not a StableId v1 value")
    return text


def _require_sha256(value: Any, label: str, code: str = "malformed-sha256") -> str:
    text = _require_string(value, label, code)
    if not SHA256_RE.fullmatch(text):
        raise EvidenceReviewError(code, f"{label} is not canonical SHA-256")
    return text


def normalize_relative_path(value: Any, label: str = "path") -> str:
    text = _require_string(value, label, "unsafe-path")
    if not text or text.strip() != text or "\x00" in text or "\\" in text:
        raise EvidenceReviewError("unsafe-path", f"{label} must be a normalized relative path")
    if text.startswith("/") or text.startswith("//") or WINDOWS_DRIVE_RE.match(text):
        raise EvidenceReviewError("unsafe-path", f"{label} must not be absolute")
    normalized = unicodedata.normalize("NFC", text)
    if normalized != text:
        raise EvidenceReviewError("unsafe-path", f"{label} must already be Unicode NFC")
    parts = text.split("/")
    if any(part in ("", ".", "..") for part in parts):
        raise EvidenceReviewError("unsafe-path", f"{label} contains traversal or empty segments")
    if any(any(ord(ch) < 32 or ord(ch) == 127 for ch in part) for part in parts):
        raise EvidenceReviewError("unsafe-path", f"{label} contains control characters")
    return "/".join(parts)


def _sort_paths(values: Iterable[str]) -> List[str]:
    return sorted(values, key=lambda value: value.encode("utf-8"))


def _package_root(path: Path) -> Path:
    if path.is_symlink():
        raise EvidenceReviewError("unsafe-package-root", "package root must not be a symlink")
    try:
        root = path.resolve(strict=True)
    except OSError as exc:
        raise EvidenceReviewError(
            "missing-package-root", f"package root is unavailable: {exc}"
        ) from exc
    if not root.is_dir():
        raise EvidenceReviewError("missing-package-root", "package root must be a directory")
    return root


def _resolve_file(root: Path, relative_path: str, *, proof: bool = False) -> Path:
    normalized = normalize_relative_path(relative_path)
    current = root
    for part in normalized.split("/"):
        current = current / part
        if current.is_symlink():
            raise EvidenceReviewError(
                "unsafe-evidence-path",
                f"symlink/reparse evidence path: {normalized}",
                rerun_required=True,
            )
    if not current.exists() or not current.is_file():
        code = "missing-proof" if proof else "missing-manifest-artifact"
        raise EvidenceReviewError(
            code, f"required evidence file is missing: {normalized}", True
        )
    return current


def _actual_inventory(root: Path) -> List[Dict[str, Any]]:
    discovered: List[Tuple[str, Path]] = []
    casefold_paths: Dict[str, str] = {}
    for current_root, directory_names, file_names in os.walk(
        root, topdown=True, followlinks=False
    ):
        directory_names[:] = _sort_paths(directory_names)
        for directory_name in list(directory_names):
            directory_path = Path(current_root) / directory_name
            if directory_path.is_symlink():
                relative = directory_path.relative_to(root).as_posix()
                raise EvidenceReviewError(
                    "unsafe-evidence-path",
                    f"symlink/reparse evidence directory: {relative}",
                    True,
                )
        for file_name in _sort_paths(file_names):
            absolute = Path(current_root) / file_name
            relative = normalize_relative_path(absolute.relative_to(root).as_posix())
            if relative in RESERVED_PACKAGE_FILES:
                continue
            if absolute.is_symlink():
                raise EvidenceReviewError(
                    "unsafe-evidence-path",
                    f"symlink/reparse evidence file: {relative}",
                    True,
                )
            folded = relative.casefold()
            previous = casefold_paths.get(folded)
            if previous is not None and previous != relative:
                raise EvidenceReviewError(
                    "case-path-collision",
                    f"case-only path collision between '{previous}' and '{relative}'",
                    True,
                )
            casefold_paths[folded] = relative
            discovered.append((relative, absolute))
    discovered.sort(key=lambda item: item[0].encode("utf-8"))
    return [
        {
            "path": relative,
            "sha256": sha256_file(absolute),
            "sizeBytes": absolute.stat().st_size,
        }
        for relative, absolute in discovered
    ]


def _parse_identity(data: bytes) -> Dict[str, Any]:
    try:
        text = data.decode("utf-8")
    except UnicodeDecodeError as exc:
        raise EvidenceReviewError(
            "malformed-identity", f"identity is not UTF-8: {exc}", True
        ) from exc
    if "\r" in text or text.endswith("\n"):
        raise EvidenceReviewError(
            "malformed-identity",
            "identity must be LF-only canonical text without trailing newline",
            True,
        )
    names = (
        "evidence_identity_schema",
        "build_identity_kind",
        "source_commit",
        "source_state",
        "dirty_state_policy",
        "unity_version",
        "package_lock_fingerprint",
        "build_content_fingerprint",
        "content_catalog_version",
        "content_definition_fingerprint",
        "save_schema_version",
        "artifact_checksum",
        "build_target",
        "build_configuration",
        "tuning_profile_id",
        "record_fingerprint",
    )
    lines = text.split("\n")
    if len(lines) != len(names):
        raise EvidenceReviewError("malformed-identity", "identity has the wrong field count", True)
    values: Dict[str, str] = {}
    for name, line in zip(names, lines):
        prefix = name + "="
        if not line.startswith(prefix):
            raise EvidenceReviewError(
                "malformed-identity", f"expected identity field {name}", True
            )
        values[name] = line[len(prefix) :]
    if values["evidence_identity_schema"] != "1":
        raise EvidenceReviewError("unsupported-identity", "identity schema must be 1", True)
    if values["build_identity_kind"] not in ("formal-release", "development"):
        raise EvidenceReviewError(
            "malformed-identity", "build_identity_kind is unsupported", True
        )
    if values["source_state"] not in ("clean", "dirty"):
        raise EvidenceReviewError("malformed-identity", "source_state is unsupported", True)
    if values["dirty_state_policy"] not in (
        "reject-dirty",
        "allow-dirty-development",
    ):
        raise EvidenceReviewError(
            "malformed-identity", "dirty_state_policy is unsupported", True
        )
    if values["source_state"] == "dirty" and values["dirty_state_policy"] == "reject-dirty":
        raise EvidenceReviewError(
            "malformed-identity", "dirty source conflicts with reject-dirty policy", True
        )
    if not SOURCE_COMMIT_RE.fullmatch(values["source_commit"]):
        raise EvidenceReviewError(
            "malformed-identity", "source commit must be lowercase 40-hex", True
        )
    for key in (
        "package_lock_fingerprint",
        "build_content_fingerprint",
        "content_definition_fingerprint",
        "artifact_checksum",
        "record_fingerprint",
    ):
        _require_sha256(values[key], key, "malformed-identity")
    _require_token(values["unity_version"], "unity_version", "malformed-identity")
    _require_token(values["build_target"], "build_target", "malformed-identity")
    _require_token(
        values["build_configuration"], "build_configuration", "malformed-identity"
    )
    _require_stable_id(
        values["tuning_profile_id"], "tuning_profile_id", "malformed-identity"
    )
    payload = "\n".join(lines[:-1]).encode("utf-8")
    if sha256_bytes(payload) != values["record_fingerprint"]:
        raise EvidenceReviewError(
            "identity-fingerprint-drift", "identity fingerprint does not match", True
        )
    return {
        "artifactChecksum": values["artifact_checksum"],
        "buildConfiguration": values["build_configuration"],
        "buildContentFingerprint": values["build_content_fingerprint"],
        "buildIdentityKind": values["build_identity_kind"],
        "buildTarget": values["build_target"],
        "contentCatalogVersion": _require_positive_decimal(
            values["content_catalog_version"], "content_catalog_version"
        ),
        "contentDefinitionFingerprint": values["content_definition_fingerprint"],
        "dirtyStatePolicy": values["dirty_state_policy"],
        "packageLockFingerprint": values["package_lock_fingerprint"],
        "recordFingerprint": values["record_fingerprint"],
        "saveSchemaVersion": _require_positive_decimal(
            values["save_schema_version"], "save_schema_version"
        ),
        "sourceCommit": values["source_commit"],
        "sourceState": values["source_state"],
        "tuningProfileId": values["tuning_profile_id"],
        "unityVersion": values["unity_version"],
    }


def _parse_configuration(data: bytes) -> Dict[str, Any]:
    value = _require_object(_load_json_bytes(data, "configuration"), "configuration")
    _require_exact_keys(
        value,
        (
            "schema",
            "version",
            "runSeed",
            "identityReference",
            "intentFixtureVersion",
            "qualityProfile",
            "locale",
            "viewport",
            "diagnostics",
            "timeouts",
        ),
        "configuration",
    )
    if value["schema"] != CONFIGURATION_SCHEMA or value["version"] != 1:
        raise EvidenceReviewError(
            "unsupported-configuration", "configuration schema/version is unsupported", True
        )
    viewport = _require_object(value["viewport"], "configuration.viewport")
    diagnostics = _require_object(value["diagnostics"], "configuration.diagnostics")
    timeouts = _require_object(value["timeouts"], "configuration.timeouts")
    _require_exact_keys(
        viewport, ("width", "height", "fullscreen"), "configuration.viewport"
    )
    _require_exact_keys(
        diagnostics,
        ("maxEventCount", "maxEventPayloadBytes", "maxLogBytes", "retainedLogCount"),
        "configuration.diagnostics",
    )
    _require_exact_keys(
        timeouts,
        ("setupSeconds", "smokeRunSeconds", "shutdownSeconds"),
        "configuration.timeouts",
    )
    return {
        "diagnosticsBounds": {
            "maxEventCount": _require_int(
                diagnostics["maxEventCount"], "maxEventCount", 1
            ),
            "maxEventPayloadBytes": _require_int(
                diagnostics["maxEventPayloadBytes"], "maxEventPayloadBytes", 1
            ),
            "maxLogBytes": _require_int(diagnostics["maxLogBytes"], "maxLogBytes", 1),
            "retainedLogCount": _require_int(
                diagnostics["retainedLogCount"], "retainedLogCount", 1
            ),
        },
        "identityReference": _require_sha256(
            value["identityReference"], "identityReference"
        ),
        "intentFixtureVersion": _require_int(
            value["intentFixtureVersion"], "intentFixtureVersion", 1
        ),
        "locale": _require_token(value["locale"], "locale"),
        "qualityProfile": _require_token(value["qualityProfile"], "qualityProfile"),
        "runSeed": _require_int(value["runSeed"], "runSeed", 0),
        "schema": CONFIGURATION_SCHEMA,
        "timeouts": {
            "setupSeconds": _require_int(timeouts["setupSeconds"], "setupSeconds", 1),
            "shutdownSeconds": _require_int(
                timeouts["shutdownSeconds"], "shutdownSeconds", 1
            ),
            "smokeRunSeconds": _require_int(
                timeouts["smokeRunSeconds"], "smokeRunSeconds", 1
            ),
        },
        "version": 1,
        "viewport": {
            "fullscreen": _require_bool(viewport["fullscreen"], "fullscreen"),
            "height": _require_int(viewport["height"], "height", 1),
            "width": _require_int(viewport["width"], "width", 1),
        },
    }


def _validate_manifest(package_root: Path) -> Dict[str, Any]:
    root = _package_root(package_root)
    manifest_path = root / MANIFEST_NAME
    checksum_path = root / CHECKSUM_NAME
    manifest_bytes = _read_bytes(manifest_path, rerun_required=True)
    checksum_bytes = _read_bytes(checksum_path, rerun_required=True)
    expected_checksum = (
        f"{hashlib.sha256(manifest_bytes).hexdigest()}  {MANIFEST_NAME}\n".encode("ascii")
    )
    if checksum_bytes != expected_checksum:
        raise EvidenceReviewError("checksum-drift", "manifest checksum does not match", True)

    manifest = _require_object(_load_json_bytes(manifest_bytes, "manifest"), "manifest")
    if canonical_json_bytes(manifest) != manifest_bytes:
        raise EvidenceReviewError("manifest-drift", "manifest is not canonical JSON", True)
    _require_exact_keys(
        manifest,
        (
            "artifactStatus",
            "build",
            "configuration",
            "descriptor",
            "diagnostics",
            "identity",
            "invalidityReasons",
            "inventory",
            "inventorySha256",
            "performance",
            "schema",
            "session",
            "tool",
            "version",
        ),
        "manifest",
    )
    if manifest["schema"] != MANIFEST_SCHEMA or manifest["version"] != 1:
        raise EvidenceReviewError(
            "unsupported-manifest-version", "manifest schema/version is unsupported", True
        )
    if manifest["artifactStatus"] not in ("valid", "invalid"):
        raise EvidenceReviewError(
            "conflicting-validity", "manifest artifactStatus is unknown", True
        )
    manifest_tool = _require_object(manifest["tool"], "manifest.tool")
    _require_exact_keys(
        manifest_tool, ("hashAlgorithm", "name", "version"), "manifest.tool"
    )
    if manifest_tool != {
        "hashAlgorithm": "SHA-256",
        "name": "build_evidence_manifest.py",
        "version": "1.0.0",
    }:
        raise EvidenceReviewError(
            "unsupported-manifest-version", "manifest tool identity is unsupported", True
        )

    inventory = _require_object(manifest["inventory"], "manifest.inventory")
    _require_exact_keys(
        inventory, ("fileCount", "files", "totalBytes"), "manifest.inventory"
    )
    if not isinstance(inventory["files"], list):
        raise EvidenceReviewError(
            "inventory-drift", "manifest inventory files must be an array", True
        )
    expected_files: List[Dict[str, Any]] = []
    for index, item_value in enumerate(inventory["files"]):
        item = _require_object(item_value, f"manifest.inventory.files[{index}]")
        _require_exact_keys(
            item, ("path", "sha256", "sizeBytes"), f"inventory item {index}"
        )
        expected_files.append(
            {
                "path": normalize_relative_path(item["path"], f"inventory path {index}"),
                "sha256": _require_sha256(item["sha256"], f"inventory sha256 {index}"),
                "sizeBytes": _require_int(item["sizeBytes"], f"inventory size {index}"),
            }
        )
    expected_paths = [item["path"] for item in expected_files]
    if expected_paths != _sort_paths(set(expected_paths)):
        raise EvidenceReviewError(
            "inventory-drift", "manifest inventory paths are not sorted and unique", True
        )
    actual_files = _actual_inventory(root)
    if expected_files != actual_files:
        expected_set = set(expected_paths)
        actual_set = {item["path"] for item in actual_files}
        missing = _sort_paths(expected_set - actual_set)
        extra = _sort_paths(actual_set - expected_set)
        raise EvidenceReviewError(
            "inventory-drift",
            f"manifest inventory does not match package; missing={missing}, extra={extra}",
            True,
        )
    if inventory["fileCount"] != len(expected_files):
        raise EvidenceReviewError("inventory-drift", "manifest fileCount is incorrect", True)
    if inventory["totalBytes"] != sum(item["sizeBytes"] for item in expected_files):
        raise EvidenceReviewError("inventory-drift", "manifest totalBytes is incorrect", True)
    inventory_digest = sha256_bytes(canonical_json_bytes({"files": expected_files}))
    if manifest["inventorySha256"] != inventory_digest:
        raise EvidenceReviewError(
            "inventory-drift", "manifest inventorySha256 is incorrect", True
        )

    inventory_paths = set(expected_paths)
    descriptor_manifest = _require_object(manifest["descriptor"], "manifest.descriptor")
    descriptor_path = normalize_relative_path(
        descriptor_manifest.get("path"), "descriptor path"
    )
    descriptor_file = _resolve_file(root, descriptor_path)
    if descriptor_manifest.get("sha256") != sha256_file(descriptor_file):
        raise EvidenceReviewError(
            "manifest-drift", "descriptor checksum differs from manifest", True
        )
    descriptor = _require_object(
        _load_json_bytes(_read_bytes(descriptor_file), "descriptor"), "descriptor"
    )
    _require_exact_keys(
        descriptor,
        (
            "schema",
            "version",
            "identityRecordPath",
            "configurationPath",
            "session",
            "diagnostics",
            "performance",
            "build",
        ),
        "descriptor",
    )
    if descriptor.get("schema") != DESCRIPTOR_SCHEMA or descriptor.get("version") != 1:
        raise EvidenceReviewError(
            "unsupported-descriptor-version", "descriptor schema/version is unsupported", True
        )

    session = _require_object(manifest["session"], "manifest.session")
    _require_exact_keys(
        session,
        ("sessionId", "attemptId", "parentSessionId", "state", "validity"),
        "manifest.session",
    )
    _require_token(session["sessionId"], "manifest.session.sessionId")
    _require_token(session["attemptId"], "manifest.session.attemptId")
    if session["parentSessionId"] is not None:
        _require_token(session["parentSessionId"], "manifest.session.parentSessionId")
    _require_token(session["state"], "manifest.session.state")
    if descriptor.get("session") != session:
        raise EvidenceReviewError(
            "conflicting-validity", "descriptor and manifest session facts differ", True
        )
    validity = _require_object(session.get("validity"), "manifest.session.validity")
    _require_exact_keys(validity, ("status", "reasonCodes"), "manifest.session.validity")
    if validity.get("status") not in ("valid", "invalid") or not isinstance(
        validity.get("reasonCodes"), list
    ):
        raise EvidenceReviewError(
            "conflicting-validity", "session validity is malformed", True
        )
    session_reason_codes = [
        _require_stable_id(item, "session validity reason", "conflicting-validity")
        for item in validity["reasonCodes"]
    ]
    if session_reason_codes != _sort_paths(set(session_reason_codes)):
        raise EvidenceReviewError(
            "conflicting-validity", "session validity reasons are not canonical", True
        )
    manifest_reasons_value = manifest["invalidityReasons"]
    if not isinstance(manifest_reasons_value, list):
        raise EvidenceReviewError(
            "conflicting-validity", "manifest invalidityReasons must be an array", True
        )
    manifest_reasons = [
        _require_stable_id(item, "manifest invalidity reason", "conflicting-validity")
        for item in manifest_reasons_value
    ]
    if manifest_reasons != _sort_paths(set(manifest_reasons)):
        raise EvidenceReviewError(
            "conflicting-validity", "manifest invalidity reasons are not canonical", True
        )
    expected_status = (
        "valid" if validity["status"] == "valid" and not manifest_reasons else "invalid"
    )
    if manifest["artifactStatus"] != expected_status:
        raise EvidenceReviewError(
            "conflicting-validity", "manifest technical validity conflicts", True
        )
    if validity["status"] == "valid" and session_reason_codes:
        raise EvidenceReviewError(
            "conflicting-validity", "valid session carries reason codes", True
        )
    if validity["status"] == "invalid" and not session_reason_codes:
        raise EvidenceReviewError(
            "conflicting-validity", "invalid session has no reason code", True
        )
    if not set(session_reason_codes).issubset(set(manifest_reasons)):
        raise EvidenceReviewError(
            "conflicting-validity",
            "session invalidity reasons are absent from manifest reasons",
            True,
        )

    identity_manifest = _require_object(manifest["identity"], "manifest.identity")
    _require_exact_keys(
        identity_manifest,
        (
            "artifactChecksum",
            "buildConfiguration",
            "buildContentFingerprint",
            "buildIdentityKind",
            "buildTarget",
            "contentCatalogVersion",
            "contentDefinitionFingerprint",
            "dirtyStatePolicy",
            "packageLockFingerprint",
            "path",
            "recordFingerprint",
            "saveSchemaVersion",
            "sha256",
            "sourceCommit",
            "sourceState",
            "tuningProfileId",
            "unityVersion",
        ),
        "manifest.identity",
    )
    configuration_manifest = _require_object(
        manifest["configuration"], "manifest.configuration"
    )
    _require_exact_keys(
        configuration_manifest,
        (
            "diagnosticsBounds",
            "identityReference",
            "intentFixtureVersion",
            "locale",
            "path",
            "qualityProfile",
            "runSeed",
            "schema",
            "sha256",
            "timeouts",
            "version",
            "viewport",
        ),
        "manifest.configuration",
    )
    identity_path = normalize_relative_path(identity_manifest.get("path"), "identity path")
    configuration_path = normalize_relative_path(
        configuration_manifest.get("path"), "configuration path"
    )
    if descriptor.get("identityRecordPath") != identity_path:
        raise EvidenceReviewError(
            "identity-configuration-mismatch", "descriptor identity path differs", True
        )
    if descriptor.get("configurationPath") != configuration_path:
        raise EvidenceReviewError(
            "identity-configuration-mismatch", "descriptor configuration path differs", True
        )
    identity_file = _resolve_file(root, identity_path)
    configuration_file = _resolve_file(root, configuration_path)
    identity_bytes = _read_bytes(identity_file)
    configuration_bytes = _read_bytes(configuration_file)
    if identity_manifest.get("sha256") != sha256_bytes(identity_bytes):
        raise EvidenceReviewError(
            "identity-fingerprint-drift", "identity file checksum differs", True
        )
    if configuration_manifest.get("sha256") != sha256_bytes(configuration_bytes):
        raise EvidenceReviewError(
            "configuration-drift", "configuration file checksum differs", True
        )
    identity = _parse_identity(identity_bytes)
    configuration = _parse_configuration(configuration_bytes)
    if configuration["identityReference"] != identity["recordFingerprint"]:
        raise EvidenceReviewError(
            "identity-configuration-mismatch",
            "configuration identityReference differs from identity",
            True,
        )
    for key, value in identity.items():
        if identity_manifest.get(key) != value:
            raise EvidenceReviewError(
                "identity-fingerprint-drift", f"manifest identity field differs: {key}", True
            )
    for key in (
        "diagnosticsBounds",
        "identityReference",
        "intentFixtureVersion",
        "locale",
        "qualityProfile",
        "runSeed",
        "schema",
        "version",
    ):
        if configuration_manifest.get(key) != configuration[key]:
            raise EvidenceReviewError(
                "configuration-drift", f"manifest configuration differs: {key}", True
            )

    descriptor_diagnostics = _require_object(
        descriptor["diagnostics"], "descriptor.diagnostics"
    )
    _require_exact_keys(
        descriptor_diagnostics,
        (
            "summaryPath",
            "eventCount",
            "maximumEventPayloadBytes",
            "logBytes",
            "retainedLogCount",
            "truncated",
        ),
        "descriptor.diagnostics",
    )
    diagnostics_manifest = _require_object(manifest["diagnostics"], "manifest.diagnostics")
    _require_exact_keys(
        diagnostics_manifest,
        (
            "bounds",
            "eventCount",
            "logBytes",
            "maximumEventPayloadBytes",
            "retainedLogCount",
            "summaryPath",
            "summarySha256",
            "truncated",
        ),
        "manifest.diagnostics",
    )
    if diagnostics_manifest["bounds"] != configuration["diagnosticsBounds"]:
        raise EvidenceReviewError(
            "configuration-drift", "diagnostics bounds differ from configuration", True
        )
    diagnostics_path = normalize_relative_path(
        diagnostics_manifest.get("summaryPath"), "diagnostics path"
    )
    if descriptor_diagnostics["summaryPath"] != diagnostics_path:
        raise EvidenceReviewError(
            "manifest-drift", "descriptor diagnostics path differs", True
        )
    diagnostics_file = _resolve_file(root, diagnostics_path)
    if diagnostics_manifest.get("summarySha256") != sha256_file(diagnostics_file):
        raise EvidenceReviewError(
            "manifest-drift", "diagnostics summary checksum differs", True
        )
    diagnostics_summary = _require_object(
        _load_json_bytes(_read_bytes(diagnostics_file), "diagnostics summary"),
        "diagnostics summary",
    )
    _require_exact_keys(
        diagnostics_summary,
        (
            "schema",
            "version",
            "entrypoint",
            "platform",
            "testFilter",
            "testResultsPath",
            "eventCount",
            "maximumEventPayloadBytes",
            "logBytes",
            "retainedLogCount",
            "truncated",
            "validity",
        ),
        "diagnostics summary",
    )
    if (
        diagnostics_summary["schema"] != DIAGNOSTICS_SUMMARY_SCHEMA
        or diagnostics_summary["version"] != 1
    ):
        raise EvidenceReviewError(
            "unsupported-diagnostics-version", "diagnostics summary is unsupported", True
        )
    entrypoint = diagnostics_summary["entrypoint"]
    if entrypoint not in ("editmode", "playmode", "windows-build"):
        raise EvidenceReviewError(
            "unsupported-entrypoint", f"unsupported evidence entrypoint: {entrypoint}", True
        )
    if diagnostics_summary["testResultsPath"] != "diagnostics/test-results.xml":
        raise EvidenceReviewError(
            "missing-proof", "diagnostics summary does not bind test-results.xml", True
        )
    for key in (
        "eventCount",
        "maximumEventPayloadBytes",
        "logBytes",
        "retainedLogCount",
        "truncated",
    ):
        if diagnostics_summary[key] != diagnostics_manifest.get(key):
            raise EvidenceReviewError(
                "manifest-drift", f"diagnostics summary differs: {key}", True
            )
    if diagnostics_summary["validity"] != validity:
        raise EvidenceReviewError(
            "conflicting-validity", "diagnostics and session validity differ", True
        )

    performance_manifest = _require_object(
        manifest["performance"], "manifest.performance"
    )
    _require_exact_keys(
        performance_manifest,
        (
            "captureSeconds",
            "frameSampleCount",
            "qualityProfile",
            "state",
            "summaryPath",
            "summarySha256",
            "warmUpSeconds",
        ),
        "manifest.performance",
    )
    descriptor_performance = _require_object(
        descriptor["performance"], "descriptor.performance"
    )
    _require_exact_keys(
        descriptor_performance,
        (
            "summaryPath",
            "state",
            "warmUpSeconds",
            "captureSeconds",
            "frameSampleCount",
            "qualityProfile",
        ),
        "descriptor.performance",
    )
    if {key: performance_manifest[key] for key in descriptor_performance} != descriptor_performance:
        raise EvidenceReviewError(
            "manifest-drift", "descriptor and manifest performance facts differ", True
        )
    performance_path = normalize_relative_path(
        performance_manifest["summaryPath"], "performance path"
    )
    performance_file = _resolve_file(root, performance_path)
    if performance_manifest["summarySha256"] != sha256_file(performance_file):
        raise EvidenceReviewError(
            "manifest-drift", "performance summary checksum differs", True
        )

    build_manifest = _require_object(manifest["build"], "manifest.build")
    _require_exact_keys(
        build_manifest,
        (
            "artifactInventoryPath",
            "artifactInventorySha256",
            "complete",
            "configuration",
            "executablePath",
            "executableSha256",
            "exitCode",
            "fingerprintsPath",
            "fingerprintsSha256",
            "rootPath",
            "status",
            "target",
        ),
        "manifest.build",
    )
    if (
        build_manifest["status"] != "succeeded"
        or build_manifest["complete"] is not True
        or build_manifest["exitCode"] != 0
        or build_manifest["target"] != "StandaloneWindows64"
        or build_manifest["configuration"] != "Development"
    ):
        raise EvidenceReviewError(
            "missing-proof", "manifest does not bind a complete successful UF-010 build", True
        )
    descriptor_build = _require_object(descriptor["build"], "descriptor.build")
    _require_exact_keys(
        descriptor_build,
        (
            "rootPath",
            "status",
            "complete",
            "exitCode",
            "fingerprintsPath",
            "artifactInventoryPath",
            "executablePath",
        ),
        "descriptor.build",
    )
    for key in (
        "rootPath",
        "status",
        "complete",
        "exitCode",
        "fingerprintsPath",
        "artifactInventoryPath",
        "executablePath",
    ):
        if descriptor_build.get(key) != build_manifest.get(key):
            raise EvidenceReviewError(
                "manifest-drift", f"descriptor and manifest build facts differ: {key}", True
            )
    fingerprints_path = normalize_relative_path(
        build_manifest["fingerprintsPath"], "build fingerprints path"
    )
    artifact_inventory_path = normalize_relative_path(
        build_manifest["artifactInventoryPath"], "build artifact inventory path"
    )
    executable_path = normalize_relative_path(
        build_manifest["executablePath"], "build executable path"
    )
    fingerprints_file = _resolve_file(root, fingerprints_path)
    artifact_inventory_file = _resolve_file(root, artifact_inventory_path)
    executable_file = _resolve_file(root, executable_path)
    if build_manifest["fingerprintsSha256"] != sha256_file(fingerprints_file):
        raise EvidenceReviewError(
            "manifest-drift", "build fingerprints checksum differs", True
        )
    if build_manifest["artifactInventorySha256"] != sha256_file(artifact_inventory_file):
        raise EvidenceReviewError("manifest-drift", "build inventory checksum differs", True)
    executable_sha256 = sha256_file(executable_file)
    if build_manifest["executableSha256"] != executable_sha256:
        raise EvidenceReviewError("manifest-drift", "build executable checksum differs", True)
    if identity["artifactChecksum"] != executable_sha256:
        raise EvidenceReviewError(
            "identity-fingerprint-drift",
            "identity artifact checksum differs from executable",
            True,
        )

    required_paths = set(BASE_REQUIRED_PROOF)
    required_paths.update(
        {
            descriptor_path,
            identity_path,
            configuration_path,
            diagnostics_path,
            performance_path,
            fingerprints_path,
            artifact_inventory_path,
            executable_path,
        }
    )
    if entrypoint == "windows-build":
        required_paths.update(WINDOWS_REQUIRED_PROOF)
    missing_proof = _sort_paths(required_paths - inventory_paths)
    if missing_proof:
        raise EvidenceReviewError(
            "missing-proof", f"required proof is not manifested: {missing_proof}", True
        )
    for proof_path in required_paths:
        _resolve_file(root, proof_path, proof=True)

    test_results_path = root / "diagnostics/test-results.xml"
    try:
        xml_root = ET.fromstring(_read_bytes(test_results_path, rerun_required=True))
    except ET.ParseError as exc:
        raise EvidenceReviewError(
            "missing-proof", f"test-results.xml is malformed: {exc}", True
        ) from exc
    if xml_root.tag != "test-run" or xml_root.attrib.get("result") != "Passed":
        raise EvidenceReviewError(
            "missing-proof", "test-results.xml does not record a passing run", True
        )
    try:
        failed_count = int(xml_root.attrib.get("failed", "-1"))
    except ValueError as exc:
        raise EvidenceReviewError(
            "missing-proof", "test-results.xml failed count is malformed", True
        ) from exc
    if failed_count != 0:
        raise EvidenceReviewError("missing-proof", "test-results.xml records failed tests", True)
    if (root / "diagnostics/unity.log").stat().st_size <= 0:
        raise EvidenceReviewError("missing-proof", "unity.log is empty", True)

    if entrypoint == "windows-build":
        windows_summary_path = root / "diagnostics/windows/windows-smoke.json"
        windows_summary = _require_object(
            _load_json_bytes(_read_bytes(windows_summary_path), "Windows smoke summary"),
            "Windows smoke summary",
        )
        _require_exact_keys(
            windows_summary,
            (
                "schema",
                "version",
                "buildContract",
                "enabledBuildScene",
                "startupPasses",
                "harnessShellLoadVerifiedBy",
                "restartVerified",
                "gracefulCloseRequests",
                "cleanExitCodes",
                "manifestRequired",
            ),
            "Windows smoke summary",
        )
        if windows_summary["schema"] != WINDOWS_SMOKE_SCHEMA or windows_summary["version"] != 1:
            raise EvidenceReviewError(
                "unsupported-windows-proof", "Windows smoke summary is unsupported", True
            )
        if (
            windows_summary["buildContract"] != "UF-010"
            or windows_summary["startupPasses"] != 2
            or windows_summary["restartVerified"] is not True
            or windows_summary["gracefulCloseRequests"] != 2
            or windows_summary["cleanExitCodes"] != [0, 0]
            or windows_summary["manifestRequired"] is not True
        ):
            raise EvidenceReviewError("missing-proof", "Windows smoke proof is incomplete", True)

    return {
        "root": root,
        "manifest": manifest,
        "manifestSha256": sha256_bytes(manifest_bytes),
        "entrypoint": entrypoint,
        "manifestReasons": manifest_reasons,
        "sessionReasons": session_reason_codes,
    }


def _load_one_review(review_paths: Sequence[Path]) -> Tuple[Dict[str, Any], bytes, Path]:
    if not review_paths:
        raise EvidenceReviewError("missing-human-review", "exactly one human review is required")
    if len(review_paths) != 1:
        raise EvidenceReviewError("duplicate-review", "more than one human review was supplied")
    review_path = review_paths[0]
    if review_path.is_symlink():
        raise EvidenceReviewError(
            "incomplete-human-review", "human review must not be a symlink"
        )
    try:
        resolved = review_path.resolve(strict=True)
    except OSError as exc:
        raise EvidenceReviewError(
            "missing-human-review", f"human review is unavailable: {exc}"
        ) from exc
    if not resolved.is_file():
        raise EvidenceReviewError("missing-human-review", "human review must be a file")
    review, review_bytes = _load_json(resolved, "human review", incomplete_review=True)
    review_object = _require_object(review, "human review", "incomplete-human-review")
    if canonical_json_bytes(review_object) != review_bytes:
        raise EvidenceReviewError(
            "incomplete-human-review",
            "human review must be canonical JSON with one LF terminator",
        )
    return review_object, review_bytes, resolved


def _validate_review(package: Mapping[str, Any], review_paths: Sequence[Path]) -> Dict[str, Any]:
    review, review_bytes, review_path = _load_one_review(review_paths)
    _require_exact_keys(
        review,
        (
            "schema",
            "version",
            "protocol",
            "reviewId",
            "reviewerId",
            "bindings",
            "preparation",
            "execution",
            "technicalClassification",
            "shellReview",
            "gameplayObservation",
            "signoff",
        ),
        "human review",
        code="incomplete-human-review",
    )
    if review.get("schema") != REVIEW_SCHEMA or review.get("version") != REVIEW_VERSION:
        raise EvidenceReviewError(
            "unsupported-protocol-version", "human review schema/version is unsupported"
        )
    protocol = _require_object(review["protocol"], "review.protocol", "incomplete-human-review")
    _require_exact_keys(
        protocol, ("schema", "version"), "review.protocol", code="incomplete-human-review"
    )
    if protocol["schema"] != PROTOCOL_SCHEMA or protocol["version"] != PROTOCOL_VERSION:
        raise EvidenceReviewError(
            "unsupported-protocol-version", "Stage 1 evidence protocol version is unsupported"
        )
    review_id = _require_stable_id(
        review["reviewId"], "reviewId", "incomplete-human-review"
    )
    reviewer_id = _require_stable_id(
        review["reviewerId"], "reviewerId", "incomplete-human-review"
    )

    manifest = package["manifest"]
    session = manifest["session"]
    identity = manifest["identity"]
    configuration = manifest["configuration"]
    bindings = _require_object(review["bindings"], "review.bindings", "incomplete-human-review")
    _require_exact_keys(
        bindings,
        (
            "manifestSha256",
            "identityRecordFingerprint",
            "identitySha256",
            "configurationSha256",
            "sessionId",
            "attemptId",
        ),
        "review.bindings",
        code="incomplete-human-review",
    )
    expected_bindings = {
        "manifestSha256": package["manifestSha256"],
        "identityRecordFingerprint": identity["recordFingerprint"],
        "identitySha256": identity["sha256"],
        "configurationSha256": configuration["sha256"],
        "sessionId": session["sessionId"],
        "attemptId": session["attemptId"],
    }
    if bindings != expected_bindings:
        raise EvidenceReviewError(
            "review-binding-mismatch", "human review is bound to different evidence", True
        )

    preparation = _require_object(
        review["preparation"], "review.preparation", "incomplete-human-review"
    )
    _require_exact_keys(
        preparation,
        (
            "freshOutputConfirmed",
            "manifestChecksumConfirmed",
            "inventoryConfirmed",
            "identityConfigurationMatchConfirmed",
            "requiredArtifactsOpened",
        ),
        "review.preparation",
        code="incomplete-human-review",
    )
    for key, value in preparation.items():
        if _require_bool(value, f"review.preparation.{key}", "incomplete-human-review") is not True:
            raise EvidenceReviewError(
                "incomplete-human-review",
                f"review preparation confirmation is incomplete: {key}",
            )

    execution = _require_object(review["execution"], "review.execution", "incomplete-human-review")
    _require_exact_keys(
        execution,
        (
            "diagnosticCommandUse",
            "restartObserved",
            "freshAttemptIdObserved",
            "parentAuditTrailObserved",
            "sessionEndedObserved",
            "cleanupObserved",
        ),
        "review.execution",
        code="incomplete-human-review",
    )
    diagnostic_command_use = _require_token(
        execution["diagnosticCommandUse"],
        "review.execution.diagnosticCommandUse",
        "incomplete-human-review",
    )
    if diagnostic_command_use not in (
        "none-or-evidence-safe",
        "invalidating-command-observed",
    ):
        raise EvidenceReviewError(
            "incomplete-human-review", "diagnosticCommandUse is not a protocol v1 value"
        )
    execution_answers = {
        key: _require_bool(value, f"review.execution.{key}", "incomplete-human-review")
        for key, value in execution.items()
        if key != "diagnosticCommandUse"
    }

    classification = _require_object(
        review["technicalClassification"],
        "review.technicalClassification",
        "incomplete-human-review",
    )
    _require_exact_keys(
        classification,
        ("status", "sourceInvalidityReasons", "classifications", "rerunRequired"),
        "review.technicalClassification",
        code="incomplete-human-review",
    )
    status = classification["status"]
    if status not in ("valid", "invalid"):
        raise EvidenceReviewError(
            "incomplete-human-review", "technical status must be valid or invalid"
        )
    if status != manifest["artifactStatus"]:
        raise EvidenceReviewError(
            "conflicting-validity",
            "review technical status conflicts with immutable manifest",
            True,
        )
    source_reasons_value = classification["sourceInvalidityReasons"]
    if not isinstance(source_reasons_value, list):
        raise EvidenceReviewError(
            "incomplete-human-review", "sourceInvalidityReasons must be an array"
        )
    source_reasons = [
        _require_stable_id(item, "source invalidity reason", "incomplete-human-review")
        for item in source_reasons_value
    ]
    if source_reasons != package["manifestReasons"]:
        raise EvidenceReviewError(
            "non-monotonic-classification",
            "review must preserve every immutable manifest invalidity reason exactly once",
            True,
        )
    classification_values = classification["classifications"]
    if not isinstance(classification_values, list):
        raise EvidenceReviewError("incomplete-human-review", "classifications must be an array")
    classifications: List[Dict[str, str]] = []
    for index, item_value in enumerate(classification_values):
        item = _require_object(
            item_value, f"classification[{index}]", "incomplete-human-review"
        )
        _require_exact_keys(
            item,
            ("sourceReasonCode", "cs012ReasonCode", "classificationBasisCode"),
            f"classification[{index}]",
            code="incomplete-human-review",
        )
        source = _require_stable_id(
            item["sourceReasonCode"],
            f"classification[{index}].sourceReasonCode",
            "incomplete-human-review",
        )
        cs012 = _require_token(
            item["cs012ReasonCode"],
            f"classification[{index}].cs012ReasonCode",
            "incomplete-human-review",
        )
        basis = _require_stable_id(
            item["classificationBasisCode"],
            f"classification[{index}].classificationBasisCode",
            "incomplete-human-review",
        )
        if cs012 not in CS012_REASON_SET:
            raise EvidenceReviewError(
                "unsupported-cs012-reason",
                f"unsupported CS-012 invalidity reason: {cs012}",
                True,
            )
        classifications.append(
            {
                "sourceReasonCode": source,
                "cs012ReasonCode": cs012,
                "classificationBasisCode": basis,
            }
        )
    sorted_classifications = sorted(
        classifications, key=lambda item: item["sourceReasonCode"].encode("utf-8")
    )
    if classifications != sorted_classifications:
        raise EvidenceReviewError(
            "non-monotonic-classification",
            "classifications must be sorted by sourceReasonCode",
            True,
        )
    classified_sources = [item["sourceReasonCode"] for item in classifications]
    if classified_sources != source_reasons:
        raise EvidenceReviewError(
            "non-monotonic-classification",
            "every source invalidity reason must have exactly one CS-012 classification",
            True,
        )
    rerun_required = _require_bool(
        classification["rerunRequired"],
        "review.technicalClassification.rerunRequired",
        "incomplete-human-review",
    )
    if (status == "invalid") != rerun_required:
        raise EvidenceReviewError(
            "conflicting-validity", "technical invalidity and rerunRequired must agree", True
        )
    classified_cs012 = [item["cs012ReasonCode"] for item in classifications]
    has_invalidating_command_reason = bool(
        INVALIDATING_COMMAND_REASONS.intersection(classified_cs012)
    )
    if (
        diagnostic_command_use == "invalidating-command-observed"
    ) != has_invalidating_command_reason:
        raise EvidenceReviewError(
            "conflicting-validity",
            "diagnostic command observation and CS-012 classification disagree",
            True,
        )

    shell = _require_object(review["shellReview"], "review.shellReview", "incomplete-human-review")
    _require_exact_keys(
        shell,
        (
            "startupAndShellObserved",
            "identityAndConfigurationObserved",
            "restartAndLineageObserved",
            "cleanEndAndCleanupObserved",
            "failureCodes",
        ),
        "review.shellReview",
        code="incomplete-human-review",
    )
    shell_answers = {
        key: _require_bool(value, f"review.shellReview.{key}", "incomplete-human-review")
        for key, value in shell.items()
        if key != "failureCodes"
    }
    failure_codes_value = shell["failureCodes"]
    if not isinstance(failure_codes_value, list):
        raise EvidenceReviewError(
            "incomplete-human-review", "shell failureCodes must be an array"
        )
    failure_codes = [
        _require_stable_id(item, "shell failure code", "incomplete-human-review")
        for item in failure_codes_value
    ]
    if failure_codes != _sort_paths(set(failure_codes)):
        raise EvidenceReviewError(
            "incomplete-human-review", "shell failureCodes must be sorted and unique"
        )
    all_technical_answers = list(execution_answers.values()) + list(shell_answers.values())
    if status == "valid":
        if not all(all_technical_answers) or failure_codes:
            raise EvidenceReviewError(
                "conflicting-validity",
                "valid technical review contains a failed shell answer",
                True,
            )
    elif not all(all_technical_answers) and not failure_codes:
        raise EvidenceReviewError(
            "incomplete-human-review",
            "failed shell answers require at least one stable failure code",
        )

    gameplay = _require_object(
        review["gameplayObservation"],
        "review.gameplayObservation",
        "incomplete-human-review",
    )
    _require_exact_keys(
        gameplay,
        ("outcome", "observationCode"),
        "review.gameplayObservation",
        code="incomplete-human-review",
    )
    gameplay_outcome = gameplay["outcome"]
    if gameplay_outcome not in ("not-recorded", "positive", "mixed", "negative"):
        raise EvidenceReviewError(
            "incomplete-human-review", "gameplay outcome is unsupported"
        )
    observation_code = gameplay["observationCode"]
    if gameplay_outcome == "not-recorded":
        if observation_code is not None:
            raise EvidenceReviewError(
                "incomplete-human-review",
                "not-recorded gameplay observation must have null code",
            )
    else:
        observation_code = _require_stable_id(
            observation_code, "gameplay observation code", "incomplete-human-review"
        )

    signoff = _require_object(review["signoff"], "review.signoff", "incomplete-human-review")
    _require_exact_keys(
        signoff,
        ("decision", "reviewComplete", "humanConfirmed", "automaticApprovalNotGranted"),
        "review.signoff",
        code="incomplete-human-review",
    )
    if (
        _require_bool(signoff["reviewComplete"], "reviewComplete", "incomplete-human-review")
        is not True
    ):
        raise EvidenceReviewError("incomplete-human-review", "reviewComplete must be true")
    if (
        _require_bool(signoff["humanConfirmed"], "humanConfirmed", "incomplete-human-review")
        is not True
    ):
        raise EvidenceReviewError("incomplete-human-review", "humanConfirmed must be true")
    if (
        _require_bool(
            signoff["automaticApprovalNotGranted"],
            "automaticApprovalNotGranted",
            "incomplete-human-review",
        )
        is not True
    ):
        raise EvidenceReviewError(
            "incomplete-human-review",
            "review must acknowledge that validation is not approval",
        )
    expected_decision = (
        "technical-evidence-admissible" if status == "valid" else "reject-and-rerun"
    )
    if signoff["decision"] != expected_decision:
        raise EvidenceReviewError(
            "conflicting-validity", "sign-off decision conflicts with technical validity", True
        )

    return {
        "reviewId": review_id,
        "reviewerId": reviewer_id,
        "reviewPath": str(review_path),
        "reviewSha256": sha256_bytes(review_bytes),
        "technicalStatus": status,
        "rerunRequired": rerun_required,
        "cs012ReasonCodes": classified_cs012,
        "gameplayObservation": {
            "outcome": gameplay_outcome,
            "observationCode": observation_code,
        },
        "decision": expected_decision,
    }


def validate_evidence_session(package_root: Path, review_paths: Sequence[Path]) -> Dict[str, Any]:
    package = _validate_manifest(package_root)
    review = _validate_review(package, review_paths)
    manifest = package["manifest"]
    return {
        "schema": VALIDATION_SCHEMA,
        "version": VALIDATION_VERSION,
        "protocol": {"schema": PROTOCOL_SCHEMA, "version": PROTOCOL_VERSION},
        "tool": {"name": TOOL_NAME, "version": TOOL_VERSION},
        "manifestSha256": package["manifestSha256"],
        "sessionId": manifest["session"]["sessionId"],
        "attemptId": manifest["session"]["attemptId"],
        "entrypoint": package["entrypoint"],
        "technicalStatus": review["technicalStatus"],
        "technicallyAdmissible": review["technicalStatus"] == "valid",
        "rerunRequired": review["rerunRequired"],
        "sourceInvalidityReasons": package["manifestReasons"],
        "cs012ReasonCodes": review["cs012ReasonCodes"],
        "gameplayObservation": review["gameplayObservation"],
        "reviewId": review["reviewId"],
        "reviewSha256": review["reviewSha256"],
        "humanDecision": review["decision"],
        "validationOutcome": (
            "review-complete"
            if not review["rerunRequired"]
            else "invalid-session-rerun-required"
        ),
        "automaticApprovalGranted": False,
    }


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Validate one immutable EH-008/EH-009 evidence package and exactly one "
            "bound EH-010 human review. No repair, reclassification, approval, upload, "
            "telemetry, or external study is performed."
        )
    )
    parser.add_argument("--package-root", required=True, type=Path)
    parser.add_argument(
        "--review",
        action="append",
        type=Path,
        default=[],
        help="canonical human review JSON; supply exactly once",
    )
    return parser


def main(argv: Optional[Sequence[str]] = None) -> int:
    arguments = _build_parser().parse_args(argv)
    try:
        report = validate_evidence_session(arguments.package_root, arguments.review)
        sys.stdout.buffer.write(canonical_json_bytes(report))
        return 3 if report["rerunRequired"] else 0
    except EvidenceReviewError as exc:
        rejection = {
            "schema": VALIDATION_SCHEMA,
            "version": VALIDATION_VERSION,
            "validationOutcome": "rejected",
            "code": exc.code,
            "message": exc.message,
            "rerunRequired": exc.rerun_required,
            "automaticApprovalGranted": False,
        }
        sys.stderr.buffer.write(canonical_json_bytes(rejection))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
