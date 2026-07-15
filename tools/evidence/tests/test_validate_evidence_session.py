from __future__ import annotations

import hashlib
import importlib.util
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


MODULE_PATH = Path(__file__).resolve().parents[1] / "validate_evidence_session.py"
SPEC = importlib.util.spec_from_file_location("validate_evidence_session", MODULE_PATH)
validator = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(validator)


def canonical_json(value):
    return validator.canonical_json_bytes(value)


def write_bytes(root: Path, relative: str, data: bytes) -> None:
    path = root / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(data)


def identity_record() -> bytes:
    lines = [
        "evidence_identity_schema=1",
        "build_identity_kind=development",
        "source_commit=0123456789abcdef0123456789abcdef01234567",
        "source_state=clean",
        "dirty_state_policy=reject-dirty",
        "unity_version=6000.3.19f1",
        "package_lock_fingerprint=sha256:" + hashlib.sha256(b"package-lock").hexdigest(),
        "build_content_fingerprint=sha256:" + hashlib.sha256(b"build-content").hexdigest(),
        "content_catalog_version=1",
        "content_definition_fingerprint=sha256:"
        + hashlib.sha256(b"content-definitions").hexdigest(),
        "save_schema_version=1",
        "artifact_checksum=sha256:" + hashlib.sha256(b"ShooterMover executable").hexdigest(),
        "build_target=StandaloneWindows64",
        "build_configuration=Development",
        "tuning_profile_id=tuning.stage1-v1",
    ]
    payload = "\n".join(lines)
    return (
        payload + "\nrecord_fingerprint=" + validator.sha256_bytes(payload.encode("utf-8"))
    ).encode("utf-8")


def build_package(
    root: Path,
    *,
    invalid_reasons=None,
    include_test_results: bool = True,
    configuration_identity_override: str | None = None,
    entrypoint: str = "playmode",
):
    invalid_reasons = list(invalid_reasons or [])
    status = "invalid" if invalid_reasons else "valid"

    write_bytes(root, "windows-build/ShooterMover.exe", b"ShooterMover executable")
    write_bytes(root, "windows-build/build-fingerprints.json", b'{"schema_version":1}\n')
    write_bytes(root, "windows-build/build-artifacts.txt", b"ShooterMover.exe\n")
    write_bytes(root, "identity/build-identity.txt", b"sample BuildIdentity v1")
    write_bytes(root, "identity/content-version.txt", b"sample ContentVersion v1")
    identity_bytes = identity_record()
    write_bytes(root, "identity/evidence-identity.txt", identity_bytes)
    parsed_identity = validator._parse_identity(identity_bytes)

    identity_reference = configuration_identity_override or parsed_identity["recordFingerprint"]
    configuration = {
        "schema": validator.CONFIGURATION_SCHEMA,
        "version": 1,
        "runSeed": 104729,
        "identityReference": identity_reference,
        "intentFixtureVersion": 1,
        "qualityProfile": "Medium",
        "locale": "en-US",
        "viewport": {"width": 1280, "height": 720, "fullscreen": False},
        "diagnostics": {
            "maxEventCount": 4096,
            "maxEventPayloadBytes": 4096,
            "maxLogBytes": 8388608,
            "retainedLogCount": 6,
        },
        "timeouts": {"setupSeconds": 30, "smokeRunSeconds": 120, "shutdownSeconds": 15},
    }
    configuration_bytes = canonical_json(configuration)
    write_bytes(root, "configuration/stage1.json", configuration_bytes)
    parsed_configuration = validator._parse_configuration(configuration_bytes)

    validity = {"status": status, "reasonCodes": invalid_reasons}
    diagnostics_summary = {
        "schema": validator.DIAGNOSTICS_SUMMARY_SCHEMA,
        "version": 1,
        "entrypoint": entrypoint,
        "platform": "PlayMode" if entrypoint != "editmode" else "EditMode",
        "testFilter": "ShooterMover.Tests.PlayMode.EvidenceHarness.Sample",
        "testResultsPath": "diagnostics/test-results.xml",
        "eventCount": 7 if entrypoint == "windows-build" else 3,
        "maximumEventPayloadBytes": 1024,
        "logBytes": 256,
        "retainedLogCount": 3 if entrypoint == "windows-build" else 1,
        "truncated": False,
        "validity": validity,
    }
    write_bytes(root, "diagnostics/summary.json", canonical_json(diagnostics_summary))
    if include_test_results:
        write_bytes(
            root,
            "diagnostics/test-results.xml",
            b'<test-run result="Passed" failed="0" passed="1"></test-run>',
        )
    write_bytes(root, "diagnostics/unity.log", b"All tests passed.\n")
    if entrypoint == "windows-build":
        write_bytes(root, "diagnostics/windows/player-pass-1.log", b"pass one\n")
        write_bytes(root, "diagnostics/windows/player-pass-2.log", b"pass two\n")
        windows_summary = {
            "schema": validator.WINDOWS_SMOKE_SCHEMA,
            "version": 1,
            "buildContract": "UF-010",
            "enabledBuildScene": "Assets/ShooterMover/Scenes/Bootstrap/Bootstrap.unity",
            "startupPasses": 2,
            "harnessShellLoadVerifiedBy": "EvidenceEntrypointSmokeTests.EntryPoint_SceneAndSessionSmoke_ReturnsToCleanBootstrap",
            "restartVerified": True,
            "gracefulCloseRequests": 2,
            "cleanExitCodes": [0, 0],
            "manifestRequired": True,
        }
        write_bytes(
            root, "diagnostics/windows/windows-smoke.json", canonical_json(windows_summary)
        )

    performance_summary = {
        "schema": "shooter-mover.eh009-entrypoint-observation",
        "version": 1,
        "entrypoint": entrypoint,
        "state": "Completed",
        "warmUpSeconds": 0.0,
        "captureSeconds": 1.0,
        "frameSampleCount": 1,
        "qualityProfile": "Medium",
        "measurement": "not gameplay acceptance",
    }
    write_bytes(root, "performance/summary.json", canonical_json(performance_summary))

    session = {
        "sessionId": "session.eh010-sample",
        "attemptId": "attempt.eh010-sample-1",
        "parentSessionId": None,
        "state": "Ended",
        "validity": validity,
    }
    descriptor = {
        "schema": validator.DESCRIPTOR_SCHEMA,
        "version": 1,
        "identityRecordPath": "identity/evidence-identity.txt",
        "configurationPath": "configuration/stage1.json",
        "session": session,
        "diagnostics": {
            "summaryPath": "diagnostics/summary.json",
            "eventCount": diagnostics_summary["eventCount"],
            "maximumEventPayloadBytes": 1024,
            "logBytes": 256,
            "retainedLogCount": diagnostics_summary["retainedLogCount"],
            "truncated": False,
        },
        "performance": {
            "summaryPath": "performance/summary.json",
            "state": "Completed",
            "warmUpSeconds": 0.0,
            "captureSeconds": 1.0,
            "frameSampleCount": 1,
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
    descriptor_bytes = canonical_json(descriptor)
    write_bytes(root, "evidence-package.json", descriptor_bytes)

    files = validator._actual_inventory(root)
    manifest = {
        "artifactStatus": status,
        "build": {
            "artifactInventoryPath": "windows-build/build-artifacts.txt",
            "artifactInventorySha256": validator.sha256_file(
                root / "windows-build/build-artifacts.txt"
            ),
            "complete": True,
            "configuration": "Development",
            "executablePath": "windows-build/ShooterMover.exe",
            "executableSha256": validator.sha256_file(root / "windows-build/ShooterMover.exe"),
            "exitCode": 0,
            "fingerprintsPath": "windows-build/build-fingerprints.json",
            "fingerprintsSha256": validator.sha256_file(
                root / "windows-build/build-fingerprints.json"
            ),
            "rootPath": "windows-build",
            "status": "succeeded",
            "target": "StandaloneWindows64",
        },
        "configuration": {
            **parsed_configuration,
            "path": "configuration/stage1.json",
            "sha256": validator.sha256_bytes(configuration_bytes),
        },
        "descriptor": {
            "path": "evidence-package.json",
            "sha256": validator.sha256_bytes(descriptor_bytes),
        },
        "diagnostics": {
            **descriptor["diagnostics"],
            "bounds": parsed_configuration["diagnosticsBounds"],
            "summarySha256": validator.sha256_file(root / "diagnostics/summary.json"),
        },
        "identity": {
            **parsed_identity,
            "path": "identity/evidence-identity.txt",
            "sha256": validator.sha256_bytes(identity_bytes),
        },
        "invalidityReasons": invalid_reasons,
        "inventory": {
            "fileCount": len(files),
            "files": files,
            "totalBytes": sum(item["sizeBytes"] for item in files),
        },
        "inventorySha256": validator.sha256_bytes(canonical_json({"files": files})),
        "performance": {
            **descriptor["performance"],
            "summarySha256": validator.sha256_file(root / "performance/summary.json"),
        },
        "schema": validator.MANIFEST_SCHEMA,
        "session": session,
        "tool": {
            "hashAlgorithm": "SHA-256",
            "name": "build_evidence_manifest.py",
            "version": "1.0.0",
        },
        "version": 1,
    }
    manifest_bytes = canonical_json(manifest)
    write_bytes(root, validator.MANIFEST_NAME, manifest_bytes)
    checksum = (
        hashlib.sha256(manifest_bytes).hexdigest() + "  " + validator.MANIFEST_NAME + "\n"
    ).encode("ascii")
    write_bytes(root, validator.CHECKSUM_NAME, checksum)
    return manifest


def build_review(path: Path, manifest, *, classifications=None, gameplay="negative"):
    classifications = [dict(item) for item in (classifications or [])]
    for item in classifications:
        item.setdefault("classificationBasisCode", "classification.cs012-direct-review")
    invalid = manifest["artifactStatus"] == "invalid"
    shell_ok = not invalid
    review = {
        "schema": validator.REVIEW_SCHEMA,
        "version": validator.REVIEW_VERSION,
        "protocol": {"schema": validator.PROTOCOL_SCHEMA, "version": 1},
        "reviewId": "review.eh010-sample-001",
        "reviewerId": "reviewer.local-human",
        "bindings": {
            "manifestSha256": None,
            "identityRecordFingerprint": manifest["identity"]["recordFingerprint"],
            "identitySha256": manifest["identity"]["sha256"],
            "configurationSha256": manifest["configuration"]["sha256"],
            "sessionId": manifest["session"]["sessionId"],
            "attemptId": manifest["session"]["attemptId"],
        },
        "preparation": {
            "freshOutputConfirmed": True,
            "manifestChecksumConfirmed": True,
            "inventoryConfirmed": True,
            "identityConfigurationMatchConfirmed": True,
            "requiredArtifactsOpened": True,
        },
        "execution": {
            "diagnosticCommandUse": "none-or-evidence-safe",
            "restartObserved": shell_ok,
            "freshAttemptIdObserved": shell_ok,
            "parentAuditTrailObserved": shell_ok,
            "sessionEndedObserved": True,
            "cleanupObserved": shell_ok,
        },
        "technicalClassification": {
            "status": manifest["artifactStatus"],
            "sourceInvalidityReasons": manifest["invalidityReasons"],
            "classifications": classifications,
            "rerunRequired": invalid,
        },
        "shellReview": {
            "startupAndShellObserved": True,
            "identityAndConfigurationObserved": True,
            "restartAndLineageObserved": shell_ok,
            "cleanEndAndCleanupObserved": shell_ok,
            "failureCodes": [] if shell_ok else ["review.restart-cleanup-failed"],
        },
        "gameplayObservation": {
            "outcome": gameplay,
            "observationCode": None if gameplay == "not-recorded" else "observation.not-fun-yet",
        },
        "signoff": {
            "decision": "reject-and-rerun" if invalid else "technical-evidence-admissible",
            "reviewComplete": True,
            "humanConfirmed": True,
            "automaticApprovalNotGranted": True,
        },
    }
    package_root = path.parent / "package"
    manifest_bytes = (package_root / validator.MANIFEST_NAME).read_bytes()
    review["bindings"]["manifestSha256"] = validator.sha256_bytes(manifest_bytes)
    path.write_bytes(canonical_json(review))
    return review


class EvidenceSessionValidatorTests(unittest.TestCase):
    def make_roots(self, temporary: str):
        root = Path(temporary)
        package = root / "package"
        package.mkdir()
        review = root / "review.json"
        return package, review

    def test_valid_review_accepts_negative_gameplay_observation_without_changing_technical_validity(self):
        with tempfile.TemporaryDirectory() as temporary:
            package, review_path = self.make_roots(temporary)
            manifest = build_package(package)
            build_review(review_path, manifest, gameplay="negative")
            report = validator.validate_evidence_session(package, [review_path])
            self.assertTrue(report["technicallyAdmissible"])
            self.assertFalse(report["rerunRequired"])
            self.assertEqual(report["gameplayObservation"]["outcome"], "negative")
            self.assertFalse(report["automaticApprovalGranted"])

    def test_checksum_drift_is_rejected_and_requires_rerun(self):
        with tempfile.TemporaryDirectory() as temporary:
            package, review_path = self.make_roots(temporary)
            manifest = build_package(package)
            build_review(review_path, manifest)
            (package / validator.CHECKSUM_NAME).write_text("0" * 64 + "  evidence-manifest-v1.json\n")
            with self.assertRaises(validator.EvidenceReviewError) as context:
                validator.validate_evidence_session(package, [review_path])
            self.assertEqual(context.exception.code, "checksum-drift")
            self.assertTrue(context.exception.rerun_required)

    def test_missing_required_proof_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            package, review_path = self.make_roots(temporary)
            manifest = build_package(package, include_test_results=False)
            build_review(review_path, manifest)
            with self.assertRaises(validator.EvidenceReviewError) as context:
                validator.validate_evidence_session(package, [review_path])
            self.assertEqual(context.exception.code, "missing-proof")
            self.assertTrue(context.exception.rerun_required)

    def test_conflicting_validity_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            package, review_path = self.make_roots(temporary)
            manifest = build_package(package, invalid_reasons=["evidence.missing-asset"])
            review = build_review(
                review_path,
                manifest,
                classifications=[
                    {
                        "sourceReasonCode": "evidence.missing-asset",
                        "cs012ReasonCode": "missing-required-asset",
                    }
                ],
            )
            review["technicalClassification"]["status"] = "valid"
            review["technicalClassification"]["rerunRequired"] = False
            review["signoff"]["decision"] = "technical-evidence-admissible"
            review_path.write_bytes(canonical_json(review))
            with self.assertRaises(validator.EvidenceReviewError) as context:
                validator.validate_evidence_session(package, [review_path])
            self.assertEqual(context.exception.code, "conflicting-validity")

    def test_unsupported_protocol_version_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            package, review_path = self.make_roots(temporary)
            manifest = build_package(package)
            review = build_review(review_path, manifest)
            review["protocol"]["version"] = 2
            review_path.write_bytes(canonical_json(review))
            with self.assertRaises(validator.EvidenceReviewError) as context:
                validator.validate_evidence_session(package, [review_path])
            self.assertEqual(context.exception.code, "unsupported-protocol-version")

    def test_duplicate_review_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            package, review_path = self.make_roots(temporary)
            manifest = build_package(package)
            build_review(review_path, manifest)
            duplicate = Path(temporary) / "review-copy.json"
            duplicate.write_bytes(review_path.read_bytes())
            with self.assertRaises(validator.EvidenceReviewError) as context:
                validator.validate_evidence_session(package, [review_path, duplicate])
            self.assertEqual(context.exception.code, "duplicate-review")

    def test_missing_review_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            package, _ = self.make_roots(temporary)
            build_package(package)
            with self.assertRaises(validator.EvidenceReviewError) as context:
                validator.validate_evidence_session(package, [])
            self.assertEqual(context.exception.code, "missing-human-review")

    def test_incomplete_human_review_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            package, review_path = self.make_roots(temporary)
            manifest = build_package(package)
            review = build_review(review_path, manifest)
            del review["signoff"]["humanConfirmed"]
            review_path.write_bytes(canonical_json(review))
            with self.assertRaises(validator.EvidenceReviewError) as context:
                validator.validate_evidence_session(package, [review_path])
            self.assertEqual(context.exception.code, "incomplete-human-review")

    def test_invalid_session_classification_is_monotonic_and_requires_rerun(self):
        with tempfile.TemporaryDirectory() as temporary:
            package, review_path = self.make_roots(temporary)
            manifest = build_package(package, invalid_reasons=["evidence.missing-asset"])
            build_review(
                review_path,
                manifest,
                classifications=[
                    {
                        "sourceReasonCode": "evidence.missing-asset",
                        "cs012ReasonCode": "missing-required-asset",
                    }
                ],
                gameplay="positive",
            )
            report = validator.validate_evidence_session(package, [review_path])
            self.assertFalse(report["technicallyAdmissible"])
            self.assertTrue(report["rerunRequired"])
            self.assertEqual(report["validationOutcome"], "invalid-session-rerun-required")
            self.assertEqual(report["cs012ReasonCodes"], ["missing-required-asset"])
            self.assertEqual(report["gameplayObservation"]["outcome"], "positive")

    def test_non_monotonic_invalidity_classification_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            package, review_path = self.make_roots(temporary)
            manifest = build_package(package, invalid_reasons=["evidence.missing-asset"])
            build_review(review_path, manifest, classifications=[])
            with self.assertRaises(validator.EvidenceReviewError) as context:
                validator.validate_evidence_session(package, [review_path])
            self.assertEqual(context.exception.code, "non-monotonic-classification")

    def test_unsupported_cs012_reason_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            package, review_path = self.make_roots(temporary)
            manifest = build_package(package, invalid_reasons=["evidence.missing-asset"])
            build_review(
                review_path,
                manifest,
                classifications=[
                    {
                        "sourceReasonCode": "evidence.missing-asset",
                        "cs012ReasonCode": "made-up-reason",
                    }
                ],
            )
            with self.assertRaises(validator.EvidenceReviewError) as context:
                validator.validate_evidence_session(package, [review_path])
            self.assertEqual(context.exception.code, "unsupported-cs012-reason")

    def test_identity_configuration_mismatch_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            package, review_path = self.make_roots(temporary)
            manifest = build_package(
                package,
                configuration_identity_override="sha256:" + "f" * 64,
            )
            build_review(review_path, manifest)
            with self.assertRaises(validator.EvidenceReviewError) as context:
                validator.validate_evidence_session(package, [review_path])
            self.assertEqual(context.exception.code, "identity-configuration-mismatch")

    def test_unmanifested_file_is_rejected_as_inventory_drift(self):
        with tempfile.TemporaryDirectory() as temporary:
            package, review_path = self.make_roots(temporary)
            manifest = build_package(package)
            build_review(review_path, manifest)
            write_bytes(package, "diagnostics/unmanifested.log", b"not in manifest\n")
            with self.assertRaises(validator.EvidenceReviewError) as context:
                validator.validate_evidence_session(package, [review_path])
            self.assertEqual(context.exception.code, "inventory-drift")

    def test_classification_without_review_basis_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            package, review_path = self.make_roots(temporary)
            manifest = build_package(package, invalid_reasons=["evidence.missing-asset"])
            review = build_review(
                review_path,
                manifest,
                classifications=[
                    {
                        "sourceReasonCode": "evidence.missing-asset",
                        "cs012ReasonCode": "missing-required-asset",
                    }
                ],
            )
            del review["technicalClassification"]["classifications"][0][
                "classificationBasisCode"
            ]
            review_path.write_bytes(canonical_json(review))
            with self.assertRaises(validator.EvidenceReviewError) as context:
                validator.validate_evidence_session(package, [review_path])
            self.assertEqual(context.exception.code, "incomplete-human-review")

    def test_cli_returns_zero_for_valid_review(self):
        with tempfile.TemporaryDirectory() as temporary:
            package, review_path = self.make_roots(temporary)
            manifest = build_package(package)
            build_review(review_path, manifest)
            completed = subprocess.run(
                [
                    sys.executable,
                    "-S",
                    str(MODULE_PATH),
                    "--package-root",
                    str(package),
                    "--review",
                    str(review_path),
                ],
                check=False,
                capture_output=True,
            )
            self.assertEqual(completed.returncode, 0, completed.stderr.decode())
            report = json.loads(completed.stdout)
            self.assertEqual(report["validationOutcome"], "review-complete")

    def test_cli_returns_three_for_reviewed_invalid_session(self):
        with tempfile.TemporaryDirectory() as temporary:
            package, review_path = self.make_roots(temporary)
            manifest = build_package(package, invalid_reasons=["evidence.missing-asset"])
            build_review(
                review_path,
                manifest,
                classifications=[
                    {
                        "sourceReasonCode": "evidence.missing-asset",
                        "cs012ReasonCode": "missing-required-asset",
                    }
                ],
            )
            completed = subprocess.run(
                [
                    sys.executable,
                    "-S",
                    str(MODULE_PATH),
                    "--package-root",
                    str(package),
                    "--review",
                    str(review_path),
                ],
                check=False,
                capture_output=True,
            )
            self.assertEqual(completed.returncode, 3, completed.stderr.decode())
            report = json.loads(completed.stdout)
            self.assertTrue(report["rerunRequired"])

    def test_windows_entrypoint_requires_complete_two_pass_proof(self):
        with tempfile.TemporaryDirectory() as temporary:
            package, review_path = self.make_roots(temporary)
            manifest = build_package(package, entrypoint="windows-build")
            build_review(review_path, manifest)
            report = validator.validate_evidence_session(package, [review_path])
            self.assertEqual(report["entrypoint"], "windows-build")


if __name__ == "__main__":
    unittest.main()
