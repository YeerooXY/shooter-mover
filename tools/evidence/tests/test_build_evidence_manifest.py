from __future__ import annotations

import hashlib
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


MODULE_PATH = Path(__file__).resolve().parents[1] / "build_evidence_manifest.py"
SPEC = importlib.util.spec_from_file_location("build_evidence_manifest", MODULE_PATH)
manifest_tool = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(manifest_tool)


def canonical_json(value):
    return (
        json.dumps(value, ensure_ascii=True, allow_nan=False, sort_keys=True, separators=(",", ":"))
        + "\n"
    ).encode("utf-8")


def write_bytes(root: Path, relative: str, data: bytes) -> None:
    path = root / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)


def make_package(root: Path, invalid_session: bool = False) -> str:
    build_root = root / "windows-build"
    write_bytes(root, "windows-build/ShooterMover.exe", b"MZ\x00ShooterMover deterministic sample\n")
    write_bytes(root, "windows-build/ShooterMover_Data/boot.config", b"wait-for-native-debugger=0\n")
    write_bytes(root, "windows-build/ShooterMover_Data/globalgamemanagers", b"global managers\n")
    write_bytes(root, "windows-build/UnityPlayer.dll", b"unity player sample\n")
    write_bytes(root, "windows-build/unity-build.log", b"Build completed with a result of 'Succeeded'.\n")

    package_lock = "sha256:" + hashlib.sha256(b"canonical package lock").hexdigest()
    fingerprints = {
        "schema_version": 1,
        "target": "StandaloneWindows64",
        "configuration": "Development",
        "output": "LocalAppData/ShooterMover/Builds/WindowsDevelopment/ShooterMover.exe",
        "editor": {
            "project_version": "6000.3.19f1",
            "project_version_with_revision": "6000.3.19f1 (7689f4515d75)",
            "project_version_worktree_sha256": hashlib.sha256(b"project version").hexdigest(),
            "unity_executable_sha256": hashlib.sha256(b"unity executable").hexdigest(),
        },
        "packages": {
            "package_lock_repository_sha256": package_lock[7:],
            "package_lock_worktree_sha256": package_lock[7:],
        },
    }
    write_bytes(root, "windows-build/build-fingerprints.json", canonical_json(fingerprints))
    write_bytes(root, "windows-build/build-artifacts.txt", b"")

    build_files = sorted(
        path.relative_to(build_root).as_posix()
        for path in build_root.rglob("*")
        if path.is_file()
    )
    write_bytes(
        root,
        "windows-build/build-artifacts.txt",
        ("\n".join(build_files) + "\n").encode("utf-8"),
    )

    executable_sha = manifest_tool.sha256_file(root / "windows-build/ShooterMover.exe")
    identity_lines = [
        "evidence_identity_schema=1",
        "build_identity_kind=development",
        "source_commit=0123456789abcdef0123456789abcdef01234567",
        "source_state=clean",
        "dirty_state_policy=reject-dirty",
        "unity_version=6000.3.19f1",
        "package_lock_fingerprint=" + package_lock,
        "build_content_fingerprint=sha256:" + hashlib.sha256(b"build content").hexdigest(),
        "content_catalog_version=1",
        "content_definition_fingerprint=sha256:"
        + hashlib.sha256(b"content definitions").hexdigest(),
        "save_schema_version=1",
        "artifact_checksum=" + executable_sha,
        "build_target=StandaloneWindows64",
        "build_configuration=Development",
        "tuning_profile_id=tuning.stage1-v1",
    ]
    identity_payload = "\n".join(identity_lines)
    identity_record = (
        identity_payload
        + "\nrecord_fingerprint="
        + manifest_tool.sha256_bytes(identity_payload.encode("utf-8"))
    )
    write_bytes(root, "identity/evidence-identity.txt", identity_record.encode("utf-8"))
    identity_fingerprint = identity_record.rsplit("=", 1)[1]

    configuration = {
        "schema": "shooter-mover.evidence-run-configuration",
        "version": 1,
        "runSeed": 104729,
        "identityReference": identity_fingerprint,
        "intentFixtureVersion": 1,
        "qualityProfile": "Medium",
        "locale": "en-US",
        "viewport": {"width": 1280, "height": 720, "fullscreen": False},
        "diagnostics": {
            "maxEventCount": 4096,
            "maxEventPayloadBytes": 4096,
            "maxLogBytes": 8388608,
            "retainedLogCount": 3,
        },
        "timeouts": {
            "setupSeconds": 30,
            "smokeRunSeconds": 120,
            "shutdownSeconds": 15,
        },
    }
    write_bytes(root, "configuration/stage1.json", canonical_json(configuration))
    write_bytes(
        root,
        "diagnostics/summary.json",
        canonical_json({"schema": "sample.diagnostics", "events": 12, "valid": not invalid_session}),
    )
    write_bytes(
        root,
        "performance/summary.json",
        canonical_json({"schema": "sample.performance", "frameSampleCount": 600}),
    )

    validity = (
        {"status": "invalid", "reasonCodes": ["evidence.intentional-invalid"]}
        if invalid_session
        else {"status": "valid", "reasonCodes": []}
    )
    descriptor = {
        "schema": "shooter-mover.evidence-package-descriptor",
        "version": 1,
        "identityRecordPath": "identity/evidence-identity.txt",
        "configurationPath": "configuration/stage1.json",
        "session": {
            "sessionId": "session-stage1-001",
            "attemptId": "attempt-001",
            "parentSessionId": None,
            "state": "Ended",
            "validity": validity,
        },
        "diagnostics": {
            "summaryPath": "diagnostics/summary.json",
            "eventCount": 12,
            "maximumEventPayloadBytes": 512,
            "logBytes": 4096,
            "retainedLogCount": 1,
            "truncated": False,
        },
        "performance": {
            "summaryPath": "performance/summary.json",
            "state": "Completed",
            "warmUpSeconds": 2.0,
            "captureSeconds": 10.0,
            "frameSampleCount": 600,
            "qualityProfile": "Medium",
        },
        "build": {
            "rootPath": "windows-build",
            "status": "succeeded",
            "complete": True,
            "exitCode": 0,
            "fingerprintsPath": "windows-build/build-fingerprints.json",
            "artifactInventoryPath": "windows-build/build-artifacts.txt",
            "executablePath": "windows-build/ShooterMover.exe",
        },
    }
    write_bytes(root, "evidence-package.json", canonical_json(descriptor))
    return "evidence-package.json"


class EvidenceManifestTests(unittest.TestCase):
    def test_sha256_known_vector(self):
        self.assertEqual(
            manifest_tool.sha256_bytes(b"abc"),
            "sha256:ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
        )

    def test_path_normalization_rejects_machine_local_and_traversal_paths(self):
        for unsafe in (
            "../secret.txt",
            "folder/../secret.txt",
            "/tmp/evidence.txt",
            "C:/evidence.txt",
            r"folder\evidence.txt",
            "folder//evidence.txt",
            "./evidence.txt",
        ):
            with self.subTest(unsafe=unsafe):
                with self.assertRaises(manifest_tool.InvalidEvidence):
                    manifest_tool.normalize_relative_path(unsafe)

    def test_canonical_order_and_checksum_are_byte_identical(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            descriptor = make_package(root)
            digest_one = manifest_tool.write_manifest(root, descriptor, require_valid=True)
            manifest_one = (root / manifest_tool.MANIFEST_NAME).read_bytes()
            checksum_one = (root / manifest_tool.CHECKSUM_NAME).read_bytes()
            digest_two = manifest_tool.write_manifest(root, descriptor, require_valid=True)
            self.assertEqual(manifest_one, (root / manifest_tool.MANIFEST_NAME).read_bytes())
            self.assertEqual(checksum_one, (root / manifest_tool.CHECKSUM_NAME).read_bytes())
            self.assertEqual(digest_one, digest_two)
            parsed = json.loads(manifest_one)
            paths = [item["path"] for item in parsed["inventory"]["files"]]
            self.assertEqual(paths, sorted(paths, key=lambda value: value.encode("utf-8")))

    def test_cross_directory_reproducibility_generates_two_identical_manifests(self):
        with tempfile.TemporaryDirectory() as first, tempfile.TemporaryDirectory() as second:
            descriptor_one = make_package(Path(first))
            descriptor_two = make_package(Path(second))
            digest_one = manifest_tool.write_manifest(Path(first), descriptor_one, require_valid=True)
            digest_two = manifest_tool.write_manifest(Path(second), descriptor_two, require_valid=True)
            manifest_one = (Path(first) / manifest_tool.MANIFEST_NAME).read_bytes()
            manifest_two = (Path(second) / manifest_tool.MANIFEST_NAME).read_bytes()
            self.assertEqual(manifest_one, manifest_two)
            self.assertEqual(digest_one, digest_two)

    def test_tamper_detection_rejects_modified_file(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            descriptor = make_package(root)
            manifest_tool.write_manifest(root, descriptor, require_valid=True)
            write_bytes(root, "diagnostics/summary.json", b'{"tampered":true}\n')
            with self.assertRaisesRegex(manifest_tool.InvalidEvidence, "manifest no longer matches"):
                manifest_tool.verify_manifest(root, require_valid=True)

    def test_missing_file_detection(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            descriptor = make_package(root)
            manifest_tool.write_manifest(root, descriptor, require_valid=True)
            (root / "performance/summary.json").unlink()
            with self.assertRaises(manifest_tool.InvalidEvidence) as context:
                manifest_tool.verify_manifest(root)
            self.assertIn(context.exception.code, {"missing-file", "evidence-package-changed"})

    def test_extra_file_detection(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            descriptor = make_package(root)
            manifest_tool.write_manifest(root, descriptor, require_valid=True)
            write_bytes(root, "unexpected.txt", b"extra\n")
            with self.assertRaisesRegex(manifest_tool.InvalidEvidence, "manifest no longer matches"):
                manifest_tool.verify_manifest(root)

    def test_reordered_manifest_inventory_is_detected_even_with_updated_checksum(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            descriptor = make_package(root)
            manifest_tool.write_manifest(root, descriptor, require_valid=True)
            manifest_path = root / manifest_tool.MANIFEST_NAME
            parsed = json.loads(manifest_path.read_bytes())
            parsed["inventory"]["files"] = list(reversed(parsed["inventory"]["files"]))
            reordered = manifest_tool.canonical_json_bytes(parsed)
            manifest_path.write_bytes(reordered)
            (root / manifest_tool.CHECKSUM_NAME).write_bytes(
                manifest_tool._checksum_bytes(reordered)
            )
            with self.assertRaisesRegex(manifest_tool.InvalidEvidence, "no longer matches"):
                manifest_tool.verify_manifest(root)

    def test_reordered_uf010_artifact_list_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            descriptor = make_package(root)
            artifact_path = root / "windows-build/build-artifacts.txt"
            lines = artifact_path.read_text(encoding="utf-8").splitlines()
            artifact_path.write_text("\n".join(reversed(lines)) + "\n", encoding="utf-8")
            with self.assertRaises(manifest_tool.InvalidEvidence) as context:
                manifest_tool.build_manifest(root, descriptor)
            self.assertEqual(context.exception.code, "non-canonical-artifact-list")

    def test_invalid_session_is_manifested_but_cannot_be_required_as_valid(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            descriptor = make_package(root, invalid_session=True)
            manifest_tool.write_manifest(root, descriptor)
            parsed = json.loads((root / manifest_tool.MANIFEST_NAME).read_bytes())
            self.assertEqual(parsed["artifactStatus"], "invalid")
            self.assertEqual(
                parsed["invalidityReasons"], ["evidence.intentional-invalid"]
            )
            with self.assertRaises(manifest_tool.InvalidEvidence) as context:
                manifest_tool.verify_manifest(root, require_valid=True)
            self.assertEqual(context.exception.code, "artifact-invalid")

    def test_incomplete_or_failed_uf010_build_cannot_be_manifested(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            descriptor = make_package(root)
            (root / "windows-build/UnityPlayer.dll").unlink()
            with self.assertRaises(manifest_tool.InvalidEvidence):
                manifest_tool.build_manifest(root, descriptor)

        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            descriptor_path = make_package(root)
            descriptor_file = root / descriptor_path
            descriptor = json.loads(descriptor_file.read_bytes())
            descriptor["build"]["status"] = "failed"
            descriptor["build"]["complete"] = False
            descriptor["build"]["exitCode"] = 1
            descriptor_file.write_bytes(canonical_json(descriptor))
            with self.assertRaises(manifest_tool.InvalidEvidence) as context:
                manifest_tool.build_manifest(root, descriptor_path)
            self.assertEqual(context.exception.code, "windows-build-not-successful")


if __name__ == "__main__":
    unittest.main()
