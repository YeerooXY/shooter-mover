#!/usr/bin/env python3
"""Build and verify deterministic offline Shooter Mover evidence manifests."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import re
import sys
import tempfile
import unicodedata
from pathlib import Path
from typing import Any, Dict, Iterable, List, Mapping, Optional, Sequence, Tuple

TOOL_NAME = "build_evidence_manifest.py"
TOOL_VERSION = "1.0.0"
MANIFEST_NAME = "evidence-manifest-v1.json"
CHECKSUM_NAME = "evidence-manifest-v1.sha256"
SCHEMA_ID = "shooter-mover.evidence-manifest"
DESCRIPTOR_SCHEMA_ID = "shooter-mover.evidence-package-descriptor"
CONFIG_SCHEMA_ID = "shooter-mover.evidence-run-configuration"
SHA256_RE = re.compile(r"^sha256:[0-9a-f]{64}$")
HEX_SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
SOURCE_COMMIT_RE = re.compile(r"^[0-9a-f]{40}$")
TOKEN_RE = re.compile(r"^[A-Za-z0-9._-]{1,128}$")
STABLE_ID_RE = re.compile(r"^[a-z0-9]+(?:-[a-z0-9]+)*\.[a-z0-9]+(?:-[a-z0-9]+)*$")
WINDOWS_DRIVE_RE = re.compile(r"^[A-Za-z]:")
RESERVED_ROOT_FILES = frozenset({MANIFEST_NAME, CHECKSUM_NAME})


class InvalidEvidence(ValueError):
    """Fail-closed evidence validation error with a stable reason code."""

    def __init__(self, code: str, message: str) -> None:
        super().__init__(message)
        self.code = code
        self.message = message


def _reject_duplicate_pairs(pairs: Iterable[Tuple[str, Any]]) -> Dict[str, Any]:
    result: Dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise InvalidEvidence("duplicate-json-key", f"duplicate JSON key: {key}")
        result[key] = value
    return result


def _read_bytes(path: Path) -> bytes:
    try:
        return path.read_bytes()
    except OSError as exc:
        raise InvalidEvidence("input-read-failed", f"could not read '{path}': {exc}") from exc


def _load_json_bytes(data: bytes, label: str) -> Any:
    try:
        text = data.decode("utf-8-sig")
    except UnicodeDecodeError as exc:
        raise InvalidEvidence("malformed-utf8", f"{label} is not strict UTF-8: {exc}") from exc
    try:
        return json.loads(
            text,
            object_pairs_hook=_reject_duplicate_pairs,
            parse_constant=lambda value: (_ for _ in ()).throw(
                InvalidEvidence("non-finite-json-number", f"{label} contains {value}")
            ),
        )
    except InvalidEvidence:
        raise
    except (json.JSONDecodeError, ValueError) as exc:
        raise InvalidEvidence("malformed-json", f"{label} is not valid JSON: {exc}") from exc


def _load_json(path: Path, label: str) -> Any:
    return _load_json_bytes(_read_bytes(path), label)


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
        raise InvalidEvidence("non-canonical-json", f"value cannot be canonicalized: {exc}") from exc


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
        raise InvalidEvidence("input-read-failed", f"could not hash '{path}': {exc}") from exc
    return "sha256:" + digest.hexdigest()


def _require_object(value: Any, label: str) -> Dict[str, Any]:
    if not isinstance(value, dict):
        raise InvalidEvidence("malformed-object", f"{label} must be a JSON object")
    return value


def _require_exact_keys(
    value: Mapping[str, Any],
    required: Iterable[str],
    label: str,
    optional: Iterable[str] = (),
) -> None:
    required_set = set(required)
    optional_set = set(optional)
    missing = sorted(required_set - set(value))
    unknown = sorted(set(value) - required_set - optional_set)
    if missing:
        raise InvalidEvidence("missing-field", f"{label} is missing: {', '.join(missing)}")
    if unknown:
        raise InvalidEvidence("unknown-field", f"{label} contains unknown fields: {', '.join(unknown)}")


def _require_string(value: Any, label: str) -> str:
    if not isinstance(value, str):
        raise InvalidEvidence("malformed-string", f"{label} must be a string")
    return value


def _require_token(value: Any, label: str) -> str:
    text = _require_string(value, label)
    if not TOKEN_RE.fullmatch(text):
        raise InvalidEvidence(
            "malformed-token",
            f"{label} must contain 1-128 ASCII letters, digits, dot, underscore, or hyphen",
        )
    return text


def _require_stable_id(value: Any, label: str) -> str:
    text = _require_string(value, label)
    if not STABLE_ID_RE.fullmatch(text):
        raise InvalidEvidence("malformed-stable-id", f"{label} is not a StableId v1 value")
    return text


def _require_int(value: Any, label: str, minimum: int = 0) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value < minimum:
        raise InvalidEvidence("malformed-integer", f"{label} must be an integer >= {minimum}")
    return value


def _require_bool(value: Any, label: str) -> bool:
    if not isinstance(value, bool):
        raise InvalidEvidence("malformed-boolean", f"{label} must be boolean")
    return value


def _require_finite_number(value: Any, label: str, minimum: float = 0.0) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise InvalidEvidence("malformed-number", f"{label} must be numeric")
    result = float(value)
    if not math.isfinite(result) or result < minimum:
        raise InvalidEvidence("malformed-number", f"{label} must be finite and >= {minimum}")
    return result


def normalize_relative_path(value: Any, label: str = "path") -> str:
    text = _require_string(value, label)
    if not text or text.strip() != text or "\x00" in text or "\\" in text:
        raise InvalidEvidence("unsafe-path", f"{label} must be a non-empty normalized relative path")
    if text.startswith("/") or text.startswith("//") or WINDOWS_DRIVE_RE.match(text):
        raise InvalidEvidence("unsafe-path", f"{label} must not be absolute")
    normalized = unicodedata.normalize("NFC", text)
    if normalized != text:
        raise InvalidEvidence("unsafe-path", f"{label} must already be Unicode NFC")
    parts = text.split("/")
    if any(part in ("", ".", "..") for part in parts):
        raise InvalidEvidence("unsafe-path", f"{label} contains an empty, dot, or traversal segment")
    if any(any(ord(ch) < 32 or ord(ch) == 127 for ch in part) for part in parts):
        raise InvalidEvidence("unsafe-path", f"{label} contains control characters")
    return "/".join(parts)


def _sort_paths(paths: Iterable[str]) -> List[str]:
    return sorted(paths, key=lambda value: value.encode("utf-8"))


def _root_path(package_root: Path) -> Path:
    if package_root.is_symlink():
        raise InvalidEvidence("symlink-root", "package root must not be a symlink")
    try:
        resolved = package_root.resolve(strict=True)
    except OSError as exc:
        raise InvalidEvidence("missing-package-root", f"package root is unavailable: {exc}") from exc
    if not resolved.is_dir():
        raise InvalidEvidence("missing-package-root", "package root must be a directory")
    return resolved


def _resolve_file(root: Path, relative_path: str) -> Path:
    normalized = normalize_relative_path(relative_path)
    current = root
    for part in normalized.split("/"):
        current = current / part
        try:
            if current.is_symlink():
                raise InvalidEvidence("symlink-path", f"symlink/reparse evidence path: {normalized}")
        except OSError as exc:
            raise InvalidEvidence("input-read-failed", f"could not inspect '{normalized}': {exc}") from exc
    if not current.exists() or not current.is_file():
        raise InvalidEvidence("missing-file", f"required evidence file is missing: {normalized}")
    return current


def build_inventory(root: Path) -> List[Dict[str, Any]]:
    discovered: List[Tuple[str, Path]] = []
    casefold_paths: Dict[str, str] = {}
    for current_root, directory_names, file_names in os.walk(root, topdown=True, followlinks=False):
        directory_names[:] = _sort_paths(directory_names)
        for directory_name in list(directory_names):
            directory_path = Path(current_root) / directory_name
            if directory_path.is_symlink():
                raise InvalidEvidence(
                    "symlink-path",
                    f"symlink/reparse evidence directory: {directory_path.relative_to(root).as_posix()}",
                )
        for file_name in _sort_paths(file_names):
            absolute = Path(current_root) / file_name
            relative = absolute.relative_to(root).as_posix()
            relative = normalize_relative_path(relative)
            if relative in RESERVED_ROOT_FILES:
                continue
            if absolute.is_symlink():
                raise InvalidEvidence("symlink-path", f"symlink/reparse evidence file: {relative}")
            folded = relative.casefold()
            previous = casefold_paths.get(folded)
            if previous is not None and previous != relative:
                raise InvalidEvidence(
                    "case-path-collision",
                    f"case-only path collision between '{previous}' and '{relative}'",
                )
            casefold_paths[folded] = relative
            discovered.append((relative, absolute))

    discovered.sort(key=lambda item: item[0].encode("utf-8"))
    files: List[Dict[str, Any]] = []
    for relative, absolute in discovered:
        try:
            size = absolute.stat().st_size
        except OSError as exc:
            raise InvalidEvidence("input-read-failed", f"could not stat '{relative}': {exc}") from exc
        files.append({"path": relative, "sha256": sha256_file(absolute), "sizeBytes": size})
    return files


def parse_identity_record(data: bytes) -> Dict[str, Any]:
    try:
        text = data.decode("utf-8")
    except UnicodeDecodeError as exc:
        raise InvalidEvidence("malformed-identity", f"identity is not UTF-8: {exc}") from exc
    if "\r" in text or text.endswith("\n"):
        raise InvalidEvidence(
            "malformed-identity",
            "identity must use strict LF canonical text with no trailing newline",
        )
    expected_names = [
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
    ]
    lines = text.split("\n")
    if len(lines) != len(expected_names):
        raise InvalidEvidence("malformed-identity", "identity has the wrong canonical field count")
    values: Dict[str, str] = {}
    for line, name in zip(lines, expected_names):
        prefix = name + "="
        if not line.startswith(prefix):
            raise InvalidEvidence("malformed-identity", f"expected canonical identity field {name}")
        values[name] = line[len(prefix) :]

    if values["evidence_identity_schema"] != "1":
        raise InvalidEvidence("unsupported-identity", "identity schema must be 1")
    if values["build_identity_kind"] not in ("formal-release", "development"):
        raise InvalidEvidence("malformed-identity", "unsupported build identity kind")
    if values["source_state"] not in ("clean", "dirty"):
        raise InvalidEvidence("malformed-identity", "unsupported source state")
    if values["dirty_state_policy"] not in ("reject-dirty", "allow-dirty-development"):
        raise InvalidEvidence("malformed-identity", "unsupported dirty-state policy")
    if not SOURCE_COMMIT_RE.fullmatch(values["source_commit"]):
        raise InvalidEvidence("malformed-identity", "source commit must be lowercase 40-hex")
    for field in (
        "package_lock_fingerprint",
        "build_content_fingerprint",
        "content_definition_fingerprint",
        "artifact_checksum",
        "record_fingerprint",
    ):
        if not SHA256_RE.fullmatch(values[field]):
            raise InvalidEvidence("malformed-identity", f"{field} must be canonical SHA-256")
    _require_stable_id(values["tuning_profile_id"], "identity tuning_profile_id")
    for field in ("content_catalog_version", "save_schema_version"):
        if not values[field].isdigit() or int(values[field]) < 1:
            raise InvalidEvidence("malformed-identity", f"{field} must be a positive integer")

    payload = "\n".join(lines[:-1]).encode("utf-8")
    if sha256_bytes(payload) != values["record_fingerprint"]:
        raise InvalidEvidence("identity-fingerprint-mismatch", "identity record fingerprint is invalid")

    return {
        "artifactChecksum": values["artifact_checksum"],
        "buildConfiguration": values["build_configuration"],
        "buildContentFingerprint": values["build_content_fingerprint"],
        "buildIdentityKind": values["build_identity_kind"],
        "buildTarget": values["build_target"],
        "contentCatalogVersion": int(values["content_catalog_version"]),
        "contentDefinitionFingerprint": values["content_definition_fingerprint"],
        "dirtyStatePolicy": values["dirty_state_policy"],
        "packageLockFingerprint": values["package_lock_fingerprint"],
        "recordFingerprint": values["record_fingerprint"],
        "saveSchemaVersion": int(values["save_schema_version"]),
        "sourceCommit": values["source_commit"],
        "sourceState": values["source_state"],
        "tuningProfileId": values["tuning_profile_id"],
        "unityVersion": values["unity_version"],
    }


def parse_configuration(data: bytes) -> Dict[str, Any]:
    value = _require_object(_load_json_bytes(data, "evidence configuration"), "configuration")
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
    if value["schema"] != CONFIG_SCHEMA_ID or value["version"] != 1:
        raise InvalidEvidence("unsupported-configuration", "configuration schema/version is unsupported")
    identity_reference = _require_string(value["identityReference"], "identityReference")
    if not SHA256_RE.fullmatch(identity_reference):
        raise InvalidEvidence("malformed-configuration", "identityReference must be canonical SHA-256")
    viewport = _require_object(value["viewport"], "configuration.viewport")
    diagnostics = _require_object(value["diagnostics"], "configuration.diagnostics")
    timeouts = _require_object(value["timeouts"], "configuration.timeouts")
    _require_exact_keys(viewport, ("width", "height", "fullscreen"), "configuration.viewport")
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
            "maxEventCount": _require_int(diagnostics["maxEventCount"], "maxEventCount", 1),
            "maxEventPayloadBytes": _require_int(
                diagnostics["maxEventPayloadBytes"], "maxEventPayloadBytes", 1
            ),
            "maxLogBytes": _require_int(diagnostics["maxLogBytes"], "maxLogBytes", 1),
            "retainedLogCount": _require_int(
                diagnostics["retainedLogCount"], "retainedLogCount", 1
            ),
        },
        "identityReference": identity_reference,
        "intentFixtureVersion": _require_int(
            value["intentFixtureVersion"], "intentFixtureVersion", 1
        ),
        "locale": _require_token(value["locale"], "locale"),
        "qualityProfile": _require_token(value["qualityProfile"], "qualityProfile"),
        "runSeed": _require_int(value["runSeed"], "runSeed", 0),
        "schema": CONFIG_SCHEMA_ID,
        "timeouts": {
            "setupSeconds": _require_int(timeouts["setupSeconds"], "setupSeconds", 1),
            "shutdownSeconds": _require_int(timeouts["shutdownSeconds"], "shutdownSeconds", 1),
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


def parse_descriptor(data: bytes) -> Dict[str, Any]:
    value = _require_object(_load_json_bytes(data, "evidence descriptor"), "descriptor")
    _require_exact_keys(
        value,
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
    if value["schema"] != DESCRIPTOR_SCHEMA_ID or value["version"] != 1:
        raise InvalidEvidence("unsupported-descriptor", "descriptor schema/version is unsupported")

    session = _require_object(value["session"], "descriptor.session")
    _require_exact_keys(
        session, ("sessionId", "attemptId", "parentSessionId", "state", "validity"), "session"
    )
    validity = _require_object(session["validity"], "descriptor.session.validity")
    _require_exact_keys(validity, ("status", "reasonCodes"), "session.validity")
    if validity["status"] not in ("valid", "invalid"):
        raise InvalidEvidence("malformed-validity", "validity status must be valid or invalid")
    if not isinstance(validity["reasonCodes"], list):
        raise InvalidEvidence("malformed-validity", "reasonCodes must be an array")
    reason_codes = [_require_stable_id(item, "validity reason code") for item in validity["reasonCodes"]]
    if reason_codes != _sort_paths(set(reason_codes)):
        raise InvalidEvidence("non-canonical-reasons", "validity reasonCodes must be sorted and unique")
    if validity["status"] == "valid" and reason_codes:
        raise InvalidEvidence("malformed-validity", "valid status cannot contain reason codes")
    if validity["status"] == "invalid" and not reason_codes:
        raise InvalidEvidence("malformed-validity", "invalid status requires a reason code")
    parent = session["parentSessionId"]
    if parent is not None:
        parent = _require_token(parent, "parentSessionId")

    diagnostics = _require_object(value["diagnostics"], "descriptor.diagnostics")
    _require_exact_keys(
        diagnostics,
        ("summaryPath", "eventCount", "maximumEventPayloadBytes", "logBytes", "retainedLogCount", "truncated"),
        "diagnostics",
    )
    performance = _require_object(value["performance"], "descriptor.performance")
    _require_exact_keys(
        performance,
        (
            "summaryPath",
            "state",
            "warmUpSeconds",
            "captureSeconds",
            "frameSampleCount",
            "qualityProfile",
        ),
        "performance",
    )
    build = _require_object(value["build"], "descriptor.build")
    _require_exact_keys(
        build,
        (
            "rootPath",
            "status",
            "complete",
            "exitCode",
            "fingerprintsPath",
            "artifactInventoryPath",
            "executablePath",
        ),
        "build",
    )
    if build["status"] != "succeeded" or build["complete"] is not True or build["exitCode"] != 0:
        raise InvalidEvidence(
            "windows-build-not-successful",
            "UF-010 Windows build must be succeeded, complete, and exitCode 0",
        )

    return {
        "build": {
            "artifactInventoryPath": normalize_relative_path(
                build["artifactInventoryPath"], "build.artifactInventoryPath"
            ),
            "complete": True,
            "executablePath": normalize_relative_path(
                build["executablePath"], "build.executablePath"
            ),
            "exitCode": 0,
            "fingerprintsPath": normalize_relative_path(
                build["fingerprintsPath"], "build.fingerprintsPath"
            ),
            "rootPath": normalize_relative_path(build["rootPath"], "build.rootPath"),
            "status": "succeeded",
        },
        "configurationPath": normalize_relative_path(
            value["configurationPath"], "configurationPath"
        ),
        "diagnostics": {
            "eventCount": _require_int(diagnostics["eventCount"], "diagnostics.eventCount"),
            "logBytes": _require_int(diagnostics["logBytes"], "diagnostics.logBytes"),
            "maximumEventPayloadBytes": _require_int(
                diagnostics["maximumEventPayloadBytes"],
                "diagnostics.maximumEventPayloadBytes",
            ),
            "retainedLogCount": _require_int(
                diagnostics["retainedLogCount"], "diagnostics.retainedLogCount"
            ),
            "summaryPath": normalize_relative_path(
                diagnostics["summaryPath"], "diagnostics.summaryPath"
            ),
            "truncated": _require_bool(diagnostics["truncated"], "diagnostics.truncated"),
        },
        "identityRecordPath": normalize_relative_path(
            value["identityRecordPath"], "identityRecordPath"
        ),
        "performance": {
            "captureSeconds": _require_finite_number(
                performance["captureSeconds"], "performance.captureSeconds"
            ),
            "frameSampleCount": _require_int(
                performance["frameSampleCount"], "performance.frameSampleCount"
            ),
            "qualityProfile": _require_token(
                performance["qualityProfile"], "performance.qualityProfile"
            ),
            "state": _require_token(performance["state"], "performance.state"),
            "summaryPath": normalize_relative_path(
                performance["summaryPath"], "performance.summaryPath"
            ),
            "warmUpSeconds": _require_finite_number(
                performance["warmUpSeconds"], "performance.warmUpSeconds"
            ),
        },
        "schema": DESCRIPTOR_SCHEMA_ID,
        "session": {
            "attemptId": _require_token(session["attemptId"], "attemptId"),
            "parentSessionId": parent,
            "sessionId": _require_token(session["sessionId"], "sessionId"),
            "state": _require_token(session["state"], "session.state"),
            "validity": {"reasonCodes": reason_codes, "status": validity["status"]},
        },
        "version": 1,
    }


def _parse_build_fingerprints(data: bytes) -> Dict[str, Any]:
    value = _require_object(_load_json_bytes(data, "UF-010 build fingerprints"), "build fingerprints")
    _require_exact_keys(
        value, ("schema_version", "target", "configuration", "output", "editor", "packages"), "build fingerprints"
    )
    if value["schema_version"] != 1:
        raise InvalidEvidence("unsupported-build-fingerprints", "schema_version must be 1")
    editor = _require_object(value["editor"], "build fingerprints.editor")
    packages = _require_object(value["packages"], "build fingerprints.packages")
    _require_exact_keys(
        editor,
        (
            "project_version",
            "project_version_with_revision",
            "project_version_worktree_sha256",
            "unity_executable_sha256",
        ),
        "build fingerprints.editor",
    )
    _require_exact_keys(
        packages,
        ("package_lock_repository_sha256", "package_lock_worktree_sha256"),
        "build fingerprints.packages",
    )
    if value["target"] != "StandaloneWindows64" or value["configuration"] != "Development":
        raise InvalidEvidence(
            "wrong-windows-build",
            "UF-010 fingerprints must target StandaloneWindows64 Development",
        )
    if value["output"] != "LocalAppData/ShooterMover/Builds/WindowsDevelopment/ShooterMover.exe":
        raise InvalidEvidence("wrong-windows-build", "UF-010 output identity is not canonical")
    for key in ("project_version_worktree_sha256", "unity_executable_sha256"):
        if not HEX_SHA256_RE.fullmatch(_require_string(editor[key], key)):
            raise InvalidEvidence("malformed-build-fingerprints", f"{key} is not lowercase SHA-256")
    for key in ("package_lock_repository_sha256", "package_lock_worktree_sha256"):
        if not HEX_SHA256_RE.fullmatch(_require_string(packages[key], key)):
            raise InvalidEvidence("malformed-build-fingerprints", f"{key} is not lowercase SHA-256")
    return {
        "configuration": "Development",
        "editorVersion": _require_token(editor["project_version"], "project_version"),
        "output": value["output"],
        "packageLockRepositorySha256": "sha256:" + packages["package_lock_repository_sha256"],
        "target": "StandaloneWindows64",
    }


def _parse_artifact_list(data: bytes) -> List[str]:
    try:
        text = data.decode("utf-8-sig")
    except UnicodeDecodeError as exc:
        raise InvalidEvidence("malformed-artifact-list", f"artifact list is not UTF-8: {exc}") from exc
    if "\r" in text:
        text = text.replace("\r\n", "\n")
        if "\r" in text:
            raise InvalidEvidence("malformed-artifact-list", "artifact list contains bare CR")
    lines = text.splitlines()
    if not lines or any(not line for line in lines):
        raise InvalidEvidence("malformed-artifact-list", "artifact list must be non-empty")
    normalized = [normalize_relative_path(line, "build artifact path") for line in lines]
    if normalized != _sort_paths(set(normalized)):
        raise InvalidEvidence(
            "non-canonical-artifact-list",
            "UF-010 build-artifacts.txt must be sorted and unique",
        )
    return normalized


def _validate_windows_build(
    root: Path, descriptor: Mapping[str, Any], identity: Mapping[str, Any]
) -> Dict[str, Any]:
    build = descriptor["build"]
    root_path = build["rootPath"]
    for field in ("fingerprintsPath", "artifactInventoryPath", "executablePath"):
        if not build[field].startswith(root_path + "/"):
            raise InvalidEvidence(
                "inconsistent-build-path",
                f"{field} must be beneath build.rootPath",
            )

    fingerprints_path = _resolve_file(root, build["fingerprintsPath"])
    artifact_list_path = _resolve_file(root, build["artifactInventoryPath"])
    executable_path = _resolve_file(root, build["executablePath"])
    fingerprints = _parse_build_fingerprints(_read_bytes(fingerprints_path))
    artifact_paths = _parse_artifact_list(_read_bytes(artifact_list_path))

    build_root = root.joinpath(*root_path.split("/"))
    if build_root.is_symlink() or not build_root.is_dir():
        raise InvalidEvidence("missing-build-root", "UF-010 build root is missing or unsafe")
    actual_under_build: List[str] = []
    for item in build_inventory(build_root):
        actual_under_build.append(item["path"])
    if artifact_paths != actual_under_build:
        missing = _sort_paths(set(artifact_paths) - set(actual_under_build))
        extra = _sort_paths(set(actual_under_build) - set(artifact_paths))
        raise InvalidEvidence(
            "build-artifact-inventory-mismatch",
            f"UF-010 build list mismatch; missing={missing}, extra={extra}",
        )

    required = {
        "ShooterMover.exe",
        "ShooterMover_Data/boot.config",
        "ShooterMover_Data/globalgamemanagers",
        "UnityPlayer.dll",
        "unity-build.log",
        "build-fingerprints.json",
        "build-artifacts.txt",
    }
    if not required.issubset(set(artifact_paths)):
        raise InvalidEvidence(
            "incomplete-windows-build",
            f"UF-010 required artifacts missing: {_sort_paths(required - set(artifact_paths))}",
        )
    if build["executablePath"] != root_path + "/ShooterMover.exe":
        raise InvalidEvidence("inconsistent-build-path", "executablePath must name ShooterMover.exe")
    if build["fingerprintsPath"] != root_path + "/build-fingerprints.json":
        raise InvalidEvidence(
            "inconsistent-build-path", "fingerprintsPath must name build-fingerprints.json"
        )
    if build["artifactInventoryPath"] != root_path + "/build-artifacts.txt":
        raise InvalidEvidence(
            "inconsistent-build-path", "artifactInventoryPath must name build-artifacts.txt"
        )
    if fingerprints["editorVersion"] != identity["unityVersion"]:
        raise InvalidEvidence("build-identity-mismatch", "Unity version differs from EH-001 identity")
    if fingerprints["packageLockRepositorySha256"] != identity["packageLockFingerprint"]:
        raise InvalidEvidence(
            "build-identity-mismatch",
            "UF-010 repository package-lock fingerprint differs from EH-001 identity",
        )
    if identity["buildTarget"] not in ("StandaloneWindows64", "win64"):
        raise InvalidEvidence("build-identity-mismatch", "identity build target is not Windows x86-64")
    if identity["buildConfiguration"] != "Development":
        raise InvalidEvidence("build-identity-mismatch", "identity build configuration is not Development")
    executable_sha = sha256_file(executable_path)
    if executable_sha != identity["artifactChecksum"]:
        raise InvalidEvidence(
            "artifact-checksum-mismatch",
            "EH-001 artifact checksum does not match ShooterMover.exe",
        )
    return {
        "artifactInventoryPath": build["artifactInventoryPath"],
        "artifactInventorySha256": sha256_file(artifact_list_path),
        "complete": True,
        "executablePath": build["executablePath"],
        "executableSha256": executable_sha,
        "exitCode": 0,
        "fingerprintsPath": build["fingerprintsPath"],
        "fingerprintsSha256": sha256_file(fingerprints_path),
        "rootPath": root_path,
        "status": "succeeded",
        "target": fingerprints["target"],
        "configuration": fingerprints["configuration"],
    }


def build_manifest(package_root: Path, descriptor_path: str) -> Dict[str, Any]:
    root = _root_path(package_root)
    descriptor_relative = normalize_relative_path(descriptor_path, "descriptor path")
    descriptor_file = _resolve_file(root, descriptor_relative)
    descriptor_bytes = _read_bytes(descriptor_file)
    descriptor = parse_descriptor(descriptor_bytes)

    identity_file = _resolve_file(root, descriptor["identityRecordPath"])
    identity_bytes = _read_bytes(identity_file)
    identity = parse_identity_record(identity_bytes)

    configuration_file = _resolve_file(root, descriptor["configurationPath"])
    configuration_bytes = _read_bytes(configuration_file)
    configuration = parse_configuration(configuration_bytes)
    if configuration["identityReference"] != identity["recordFingerprint"]:
        raise InvalidEvidence(
            "configuration-identity-mismatch",
            "EH-002 identityReference does not match the EH-001 record fingerprint",
        )

    diagnostics_summary = _resolve_file(root, descriptor["diagnostics"]["summaryPath"])
    performance_summary = _resolve_file(root, descriptor["performance"]["summaryPath"])
    windows_build = _validate_windows_build(root, descriptor, identity)

    inventory = build_inventory(root)
    inventory_paths = {item["path"] for item in inventory}
    required_paths = {
        descriptor_relative,
        descriptor["identityRecordPath"],
        descriptor["configurationPath"],
        descriptor["diagnostics"]["summaryPath"],
        descriptor["performance"]["summaryPath"],
        windows_build["fingerprintsPath"],
        windows_build["artifactInventoryPath"],
        windows_build["executablePath"],
    }
    missing_required = _sort_paths(required_paths - inventory_paths)
    if missing_required:
        raise InvalidEvidence(
            "manifest-input-not-in-inventory",
            f"required manifest inputs are not inventoried: {missing_required}",
        )

    invalidity_reasons = set(descriptor["session"]["validity"]["reasonCodes"])
    if descriptor["session"]["state"] != "Ended":
        invalidity_reasons.add("evidence.session-not-ended")
    diagnostics = descriptor["diagnostics"]
    bounds = configuration["diagnosticsBounds"]
    if diagnostics["eventCount"] > bounds["maxEventCount"]:
        invalidity_reasons.add("evidence.diagnostics-event-bound-exceeded")
    if diagnostics["maximumEventPayloadBytes"] > bounds["maxEventPayloadBytes"]:
        invalidity_reasons.add("evidence.diagnostics-payload-bound-exceeded")
    if diagnostics["logBytes"] > bounds["maxLogBytes"]:
        invalidity_reasons.add("evidence.diagnostics-log-bound-exceeded")
    if diagnostics["retainedLogCount"] > bounds["retainedLogCount"]:
        invalidity_reasons.add("evidence.diagnostics-retention-bound-exceeded")
    if diagnostics["truncated"]:
        invalidity_reasons.add("evidence.diagnostics-truncated")

    performance = descriptor["performance"]
    if performance["state"] != "Completed":
        invalidity_reasons.add("evidence.performance-incomplete")
    if performance["captureSeconds"] <= 0.0 or performance["frameSampleCount"] <= 0:
        invalidity_reasons.add("evidence.performance-empty")
    if performance["qualityProfile"] != configuration["qualityProfile"]:
        invalidity_reasons.add("evidence.performance-quality-mismatch")

    artifact_status = "valid" if (
        descriptor["session"]["validity"]["status"] == "valid" and not invalidity_reasons
    ) else "invalid"

    inventory_summary = {
        "fileCount": len(inventory),
        "files": inventory,
        "totalBytes": sum(item["sizeBytes"] for item in inventory),
    }
    inventory_sha = sha256_bytes(canonical_json_bytes({"files": inventory}))

    return {
        "artifactStatus": artifact_status,
        "build": windows_build,
        "configuration": {
            **configuration,
            "path": descriptor["configurationPath"],
            "sha256": sha256_bytes(configuration_bytes),
        },
        "descriptor": {
            "path": descriptor_relative,
            "sha256": sha256_bytes(descriptor_bytes),
        },
        "diagnostics": {
            **diagnostics,
            "bounds": bounds,
            "summarySha256": sha256_file(diagnostics_summary),
        },
        "identity": {
            **identity,
            "path": descriptor["identityRecordPath"],
            "sha256": sha256_bytes(identity_bytes),
        },
        "invalidityReasons": _sort_paths(invalidity_reasons),
        "inventory": inventory_summary,
        "inventorySha256": inventory_sha,
        "performance": {
            **performance,
            "summarySha256": sha256_file(performance_summary),
        },
        "schema": SCHEMA_ID,
        "session": descriptor["session"],
        "tool": {
            "hashAlgorithm": "SHA-256",
            "name": TOOL_NAME,
            "version": TOOL_VERSION,
        },
        "version": 1,
    }


def _checksum_bytes(manifest_bytes: bytes) -> bytes:
    digest = hashlib.sha256(manifest_bytes).hexdigest()
    return f"{digest}  {MANIFEST_NAME}\n".encode("ascii")


def _atomic_write(path: Path, data: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(
        prefix="." + path.name + ".", suffix=".tmp", dir=str(path.parent)
    )
    temporary_path = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary_path, path)
    finally:
        try:
            temporary_path.unlink()
        except FileNotFoundError:
            pass


def write_manifest(package_root: Path, descriptor_path: str, require_valid: bool = False) -> str:
    root = _root_path(package_root)
    manifest = build_manifest(root, descriptor_path)
    if require_valid and manifest["artifactStatus"] != "valid":
        raise InvalidEvidence(
            "artifact-invalid",
            f"artifact is invalid: {manifest['invalidityReasons']}",
        )
    manifest_bytes = canonical_json_bytes(manifest)
    manifest_path = root / MANIFEST_NAME
    checksum_path = root / CHECKSUM_NAME
    _atomic_write(manifest_path, manifest_bytes)
    _atomic_write(checksum_path, _checksum_bytes(manifest_bytes))
    return sha256_bytes(manifest_bytes)


def verify_manifest(package_root: Path, require_valid: bool = False) -> str:
    root = _root_path(package_root)
    manifest_path = root / MANIFEST_NAME
    checksum_path = root / CHECKSUM_NAME
    manifest_bytes = _read_bytes(manifest_path)
    checksum_bytes = _read_bytes(checksum_path)
    expected_checksum = _checksum_bytes(manifest_bytes)
    if checksum_bytes != expected_checksum:
        raise InvalidEvidence("manifest-checksum-mismatch", "manifest checksum file is invalid")

    parsed = _require_object(_load_json_bytes(manifest_bytes, "evidence manifest"), "manifest")
    if canonical_json_bytes(parsed) != manifest_bytes:
        raise InvalidEvidence("non-canonical-manifest", "manifest bytes are not canonical JSON")
    if parsed.get("schema") != SCHEMA_ID or parsed.get("version") != 1:
        raise InvalidEvidence("unsupported-manifest", "manifest schema/version is unsupported")
    descriptor = _require_object(parsed.get("descriptor"), "manifest.descriptor")
    descriptor_path = normalize_relative_path(descriptor.get("path"), "manifest descriptor path")
    rebuilt = build_manifest(root, descriptor_path)
    rebuilt_bytes = canonical_json_bytes(rebuilt)
    if rebuilt_bytes != manifest_bytes:
        raise InvalidEvidence(
            "evidence-package-changed",
            "manifest no longer matches current metadata or file inventory",
        )
    if require_valid and parsed.get("artifactStatus") != "valid":
        raise InvalidEvidence(
            "artifact-invalid",
            f"artifact is invalid: {parsed.get('invalidityReasons')}",
        )
    return sha256_bytes(manifest_bytes)


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Build or verify a canonical offline evidence manifest. "
            "No network, credentials, signing, upload, or release packaging is performed."
        )
    )
    subparsers = parser.add_subparsers(dest="command", required=True)
    build_parser = subparsers.add_parser("build", help="build canonical manifest/checksum files")
    build_parser.add_argument("--package-root", required=True, type=Path)
    build_parser.add_argument("--descriptor", required=True)
    build_parser.add_argument("--require-valid", action="store_true")
    verify_parser = subparsers.add_parser("verify", help="verify current package against its manifest")
    verify_parser.add_argument("--package-root", required=True, type=Path)
    verify_parser.add_argument("--require-valid", action="store_true")
    return parser


def main(argv: Optional[Sequence[str]] = None) -> int:
    arguments = _build_parser().parse_args(argv)
    try:
        if arguments.command == "build":
            digest = write_manifest(
                arguments.package_root, arguments.descriptor, arguments.require_valid
            )
            print(f"manifest built: {digest}")
        else:
            digest = verify_manifest(arguments.package_root, arguments.require_valid)
            print(f"manifest verified: {digest}")
        return 0
    except InvalidEvidence as exc:
        print(f"invalid evidence: {exc.code}: {exc.message}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
