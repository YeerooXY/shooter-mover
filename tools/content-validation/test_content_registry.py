#!/usr/bin/env python3
"""CS-011 deterministic registry generator and drift-validation tests."""

from __future__ import annotations

import json
import os
import random
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import content_registry as registry


def descriptor(
    definition_kind: str,
    definition_id: str,
    *,
    provenance_id: str | None = "provenance.accepted",
    prototype_only: bool = False,
    definition_version: int = 1,
    references: tuple[registry.ContentReferenceInput, ...] = (),
) -> registry.ContentDescriptorInput:
    return registry.ContentDescriptorInput(
        definition_kind=definition_kind,
        definition_id=definition_id,
        definition_version=definition_version,
        provenance_id=provenance_id,
        prototype_only=prototype_only,
        references=tuple(sorted(references)),
        source=f"fixture:{definition_id}",
    )


def write_descriptor(path: Path, value: registry.ContentDescriptorInput) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    payload = {
        "$schema": registry.DESCRIPTOR_SCHEMA_ID,
        "definition_kind": value.definition_kind,
        "definition_id": value.definition_id,
        "definition_version": value.definition_version,
        "provenance_id": value.provenance_id,
        "prototype_only": value.prototype_only,
        "references": [
            {
                "definition_kind": reference.definition_kind,
                "definition_id": reference.definition_id,
                "definition_version": reference.definition_version,
            }
            for reference in value.references
        ],
    }
    path.write_text(
        json.dumps(payload, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
        newline="\n",
    )


class RegistryGenerationTests(unittest.TestCase):
    def test_empty_catalog_matches_frozen_v1_checksums(self) -> None:
        documents = registry.build_documents((), catalog_version=1, mode="release")

        self.assertEqual(
            "d38116bd5ea8a003de1373e03c89fd8bb4b9948302df34cfe6b44e44b9afb2ca",
            documents.machine_checksum,
        )
        self.assertEqual(
            "762358376b62cafa2e04ea89cf99cddb777b79bd5824ff7481eb791321fb5fd9",
            documents.review_checksum,
        )
        self.assertEqual(b"\n", documents.machine_bytes[-1:])
        self.assertNotIn(b"\r", documents.machine_bytes)
        self.assertFalse(documents.machine_bytes.startswith(b"\xef\xbb\xbf"))
        self.assertIn(b'"entries": [  ]', documents.machine_bytes)

    def test_shuffled_descriptors_and_references_are_byte_stable(self) -> None:
        module = descriptor("shared-module", "module.arc-core")
        room = descriptor("room", "room.training")
        enemy = descriptor("enemy", "enemy.pursuer")
        weapon_references = [
            registry.ContentReferenceInput("room", "room.training", 1),
            registry.ContentReferenceInput("shared-module", "module.arc-core", 1),
        ]
        weapon = descriptor(
            "weapon",
            "weapon.arc-gun",
            references=tuple(reversed(weapon_references)),
        )
        fixtures = [weapon, enemy, room, module]

        baseline = registry.build_documents(fixtures, 7, "release")
        random.Random(773).shuffle(fixtures)
        shuffled = registry.build_documents(fixtures, 7, "release")

        self.assertEqual(baseline.machine_bytes, shuffled.machine_bytes)
        self.assertEqual(baseline.review_bytes, shuffled.review_bytes)
        self.assertLess(
            baseline.machine_bytes.index(b'"definition_id": "enemy.pursuer"'),
            baseline.machine_bytes.index(b'"definition_id": "room.training"'),
        )
        self.assertLess(
            baseline.machine_bytes.index(b'"definition_id": "module.arc-core"'),
            baseline.machine_bytes.index(b'"definition_id": "weapon.arc-gun"'),
        )

    def test_duplicate_missing_wrong_kind_and_version_errors_are_ordered(self) -> None:
        duplicate_a = descriptor("enemy", "enemy.duplicate")
        duplicate_b = descriptor("room", "enemy.duplicate")
        target = descriptor("enemy", "enemy.target", definition_version=2)
        no_provenance = descriptor(
            "environment",
            "environment.no-provenance",
            provenance_id=None,
        )
        source = descriptor(
            "weapon",
            "weapon.source",
            references=(
                registry.ContentReferenceInput("room", "enemy.target", 1),
                registry.ContentReferenceInput("enemy", "enemy.missing", 1),
            ),
        )

        with self.assertRaises(registry.CatalogValidationError) as captured:
            registry.build_documents(
                [source, no_provenance, duplicate_b, target, duplicate_a],
                catalog_version=1,
                mode="release",
            )

        errors = captured.exception.errors
        self.assertEqual(
            [
                "duplicate-definition",
                "missing-definition",
                "wrong-definition-kind",
                "unsupported-definition-version",
                "missing-provenance",
            ],
            [error.code for error in errors],
        )
        self.assertEqual("enemy.duplicate", errors[0].definition_id)
        self.assertEqual("enemy.missing", errors[1].referenced_id)
        self.assertEqual("room", errors[2].expected_kind)
        self.assertEqual(2, errors[3].actual_version)
        self.assertEqual("environment.no-provenance", errors[4].definition_id)

    def test_cycle_and_release_prototype_errors_are_not_suppressed(self) -> None:
        alpha = descriptor(
            "shared-module",
            "module.alpha",
            prototype_only=True,
            references=(
                registry.ContentReferenceInput("shared-module", "module.beta", 1),
            ),
        )
        beta = descriptor(
            "shared-module",
            "module.beta",
            references=(
                registry.ContentReferenceInput("shared-module", "module.alpha", 1),
            ),
        )

        with self.assertRaises(registry.CatalogValidationError) as captured:
            registry.build_documents([beta, alpha], 1, "release")

        self.assertEqual(
            ["cyclic-dependency", "prototype-only-definition"],
            [error.code for error in captured.exception.errors],
        )
        self.assertEqual(
            ("module.alpha", "module.beta"),
            captured.exception.errors[0].cycle,
        )

    def test_generate_check_manual_edit_failure_and_explicit_repair(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = descriptor("enemy", "enemy.pursuer")
            write_descriptor(
                root
                / "Assets/ShooterMover/Content/Definitions"
                / "pursuer.content-descriptor.json",
                source,
            )

            first = registry.generate_repository(root)
            checked = registry.check_repository(root)
            self.assertEqual(first.machine_checksum, checked.machine_checksum)

            review = root / registry.DEFAULT_REVIEW_OUTPUT
            review.write_bytes(review.read_bytes() + b" ")

            with self.assertRaises(registry.GeneratedOutputDriftError):
                registry.check_repository(root)
            with self.assertRaises(registry.GeneratedOutputDriftError):
                registry.generate_repository(root)

            repaired = registry.generate_repository(root, repair_drift=True)
            self.assertEqual(repaired.review_bytes, review.read_bytes())
            registry.check_repository(root)

    def test_reordered_output_fails_drift_check(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            write_descriptor(
                root
                / "Assets/ShooterMover/Content/Definitions"
                / "z.content-descriptor.json",
                descriptor("weapon", "weapon.zeta"),
            )
            write_descriptor(
                root
                / "Assets/ShooterMover/Content/Definitions"
                / "a.content-descriptor.json",
                descriptor("enemy", "enemy.alpha"),
            )
            registry.generate_repository(root)

            machine_path = root / registry.DEFAULT_REGISTRY_OUTPUT
            raw = json.loads(machine_path.read_text(encoding="utf-8"))
            raw["entries"].reverse()
            machine_path.write_text(
                json.dumps(raw, indent=2) + "\n",
                encoding="utf-8",
                newline="\n",
            )

            with self.assertRaises(registry.GeneratedOutputDriftError):
                registry.check_repository(root)

    def test_concurrent_invocation_fails_without_touching_outputs(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            lock_path = root / registry.LOCK_PATH
            lock_path.parent.mkdir(parents=True, exist_ok=True)
            lock_path.write_text("occupied\n", encoding="utf-8")
            with self.assertRaises(registry.ConcurrentGenerationError):
                registry.generate_repository(root)
            self.assertFalse((root / registry.DEFAULT_REGISTRY_OUTPUT).exists())
            self.assertFalse((root / registry.DEFAULT_REVIEW_OUTPUT).exists())

    def test_atomic_pair_rolls_back_when_second_replace_fails(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            registry_path = root / "registry.json"
            review_path = root / "review.json"
            registry_path.write_bytes(b"old-registry\n")
            review_path.write_bytes(b"old-review\n")

            real_replace = os.replace
            calls = 0

            def fail_second_replace(source: object, destination: object) -> None:
                nonlocal calls
                calls += 1
                if calls == 2:
                    raise OSError("injected second replace failure")
                real_replace(source, destination)

            with mock.patch.object(
                registry.os,
                "replace",
                side_effect=fail_second_replace,
            ):
                with self.assertRaises(OSError):
                    registry.atomic_write_pair(
                        registry_path,
                        b"new-registry\n",
                        review_path,
                        b"new-review\n",
                    )

            self.assertEqual(b"old-registry\n", registry_path.read_bytes())
            self.assertEqual(b"old-review\n", review_path.read_bytes())

    def test_duplicate_json_properties_fail_deterministically(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "bad.content-descriptor.json"
            path.write_text(
                '{"$schema":"'
                + registry.DESCRIPTOR_SCHEMA_ID
                + '","definition_kind":"enemy","definition_kind":"room",'
                '"definition_id":"enemy.bad","definition_version":1,'
                '"provenance_id":"provenance.accepted",'
                '"prototype_only":false,"references":[]}\n',
                encoding="utf-8",
            )
            with self.assertRaises(registry.DescriptorInputError) as captured:
                registry._load_json_file(path)
            self.assertIn("duplicate JSON property: definition_kind", str(captured.exception))


if __name__ == "__main__":
    unittest.main(verbosity=2)
