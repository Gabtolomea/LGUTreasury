import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from scripts.automated_backup import run_automated_backup


class AutomatedBackupDriveStatusTests(unittest.TestCase):
    def run_backup_with_drive(self, drive_service_error=None):
        temporary_directory = tempfile.TemporaryDirectory()
        self.addCleanup(temporary_directory.cleanup)
        root = Path(temporary_directory.name)
        backup_dir = root / "backup_test"
        backup_dir.mkdir()
        encrypted_file = root / "backup_test.zip.enc"
        encrypted_file.write_bytes(b"encrypted backup")

        patches = [
            patch("scripts.automated_backup.environment_values", return_value={}),
            patch("scripts.automated_backup.is_container_mysql", return_value=False),
            patch("scripts.automated_backup.native_database_config", return_value={}),
            patch("scripts.automated_backup.project_directory", return_value=root),
            patch(
                "scripts.automated_backup.run_preflight",
                return_value={"ok": True, "compose_command": ["docker", "compose"]},
            ),
            patch(
                "scripts.automated_backup.create_backups",
                return_value={
                    "ok": True,
                    "errors": [],
                    "backup_dir": str(backup_dir),
                },
            ),
            patch(
                "scripts.automated_backup.encrypt_backup_directory",
                return_value={
                    "ok": True,
                    "errors": [],
                    "encrypted_file": str(encrypted_file),
                    "sha256": "test-sha256",
                },
            ),
            patch("scripts.automated_backup.Logger"),
            patch(
                "scripts.automated_backup.get_drive_service",
                side_effect=drive_service_error,
            ),
        ]

        with patches[0], patches[1], patches[2], patches[3], patches[4], patches[5], patches[6], patches[7], patches[8]:
            result = run_automated_backup(
                config={
                    "project_dir": str(root),
                    "compose_files": ["compose.native-mysql.yml"],
                    "required_env": [],
                    "env_file": None,
                    "backup_encryption": {"key_path": root / "backup.key"},
                }
            )

        return result, encrypted_file

    def test_drive_failure_marks_automated_backup_failed_but_keeps_local_file(self):
        result, encrypted_file = self.run_backup_with_drive(
            RuntimeError("authorization expired")
        )

        self.assertFalse(result["ok"])
        self.assertFalse(result["drive_upload_ok"])
        self.assertEqual(result["drive_error"], "RuntimeError")
        self.assertTrue(encrypted_file.is_file())
        self.assertIn("local encrypted backup was created successfully", result["errors"][0])
        self.assertIn(str(encrypted_file), result["errors"][0])

    def test_drive_success_marks_automated_backup_successful(self):
        with patch("scripts.automated_backup.get_drive_service", return_value=object()), patch(
            "scripts.automated_backup.upload_backup",
            return_value={"ok": True, "file_id": "school-drive-file-id"},
        ):
            with tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                backup_dir = root / "backup_test"
                backup_dir.mkdir()
                encrypted_file = root / "backup_test.zip.enc"
                encrypted_file.write_bytes(b"encrypted backup")

                with patch("scripts.automated_backup.environment_values", return_value={}), patch(
                    "scripts.automated_backup.is_container_mysql", return_value=False
                ), patch(
                    "scripts.automated_backup.native_database_config", return_value={}
                ), patch(
                    "scripts.automated_backup.project_directory", return_value=root
                ), patch(
                    "scripts.automated_backup.run_preflight",
                    return_value={"ok": True, "compose_command": ["docker", "compose"]},
                ), patch(
                    "scripts.automated_backup.create_backups",
                    return_value={"ok": True, "errors": [], "backup_dir": str(backup_dir)},
                ), patch(
                    "scripts.automated_backup.encrypt_backup_directory",
                    return_value={
                        "ok": True,
                        "errors": [],
                        "encrypted_file": str(encrypted_file),
                        "sha256": "test-sha256",
                    },
                ), patch("scripts.automated_backup.Logger"):
                    result = run_automated_backup(
                        config={
                            "project_dir": str(root),
                            "compose_files": ["compose.native-mysql.yml"],
                            "required_env": [],
                            "env_file": None,
                            "backup_encryption": {"key_path": root / "backup.key"},
                        }
                    )

        self.assertTrue(result["ok"])
        self.assertTrue(result["drive_upload_ok"])
        self.assertEqual(result["drive_file_id"], "school-drive-file-id")


if __name__ == "__main__":
    unittest.main()
