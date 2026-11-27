import tempfile
import unittest
from pathlib import Path
from unittest.mock import MagicMock, patch

from scripts import google_drive


class GoogleDriveTests(unittest.TestCase):
    def setUp(self):
        self.temp_directory = tempfile.TemporaryDirectory()
        self.root = Path(self.temp_directory.name)
        self.folder_path = self.root / "drive_folder.json"

    def tearDown(self):
        self.temp_directory.cleanup()

    def test_uses_drive_file_scope(self):
        self.assertEqual(
            google_drive.SCOPES,
            ["https://www.googleapis.com/auth/drive.file"],
        )

    def test_creates_and_persists_school_backup_folder(self):
        service = MagicMock()
        service.files.return_value.list.return_value.execute.return_value = {
            "files": []
        }
        service.files.return_value.create.return_value.execute.return_value = {
            "id": "school-folder-id",
            "name": "LGUTreasury Backups",
        }

        with patch.object(google_drive, "SECRETS_DIR", self.root), patch.object(
            google_drive, "FOLDER_ID_PATH", self.folder_path
        ):
            folder_id = google_drive.get_or_create_drive_folder(service)

        self.assertEqual(folder_id, "school-folder-id")
        self.assertTrue(self.folder_path.is_file())
        self.assertEqual(
            self.folder_path.read_text(encoding="utf-8"),
            '{\n  "folder_id": "school-folder-id"\n}',
        )
        service.files.return_value.create.assert_called_once()

    def test_reuses_saved_folder_after_validating_it(self):
        self.folder_path.write_text(
            '{"folder_id": "school-folder-id"}',
            encoding="utf-8",
        )
        service = MagicMock()
        service.files.return_value.get.return_value.execute.return_value = {
            "id": "school-folder-id",
            "name": "LGUTreasury Backups",
            "mimeType": google_drive.DRIVE_FOLDER_MIME_TYPE,
        }

        with patch.object(google_drive, "SECRETS_DIR", self.root), patch.object(
            google_drive, "FOLDER_ID_PATH", self.folder_path
        ):
            folder_id = google_drive.get_or_create_drive_folder(service)

        self.assertEqual(folder_id, "school-folder-id")
        service.files.return_value.list.assert_not_called()
        service.files.return_value.create.assert_not_called()

    def test_rejects_saved_folder_that_is_not_expected_folder(self):
        self.folder_path.write_text(
            '{"folder_id": "old-personal-folder"}',
            encoding="utf-8",
        )
        service = MagicMock()
        service.files.return_value.get.return_value.execute.return_value = {
            "id": "old-personal-folder",
            "name": "Personal Backups",
            "mimeType": google_drive.DRIVE_FOLDER_MIME_TYPE,
        }

        with patch.object(google_drive, "SECRETS_DIR", self.root), patch.object(
            google_drive, "FOLDER_ID_PATH", self.folder_path
        ):
            with self.assertRaisesRegex(RuntimeError, "does not match"):
                google_drive.get_or_create_drive_folder(service)

    def test_upload_targets_configured_folder_and_verifies_file(self):
        encrypted_file = self.root / "backup.zip.enc"
        encrypted_file.write_bytes(b"encrypted test backup")

        service = MagicMock()
        request = service.files.return_value.create.return_value
        request.next_chunk.return_value = (
            None,
            {"id": "uploaded-file-id", "name": encrypted_file.name},
        )

        expected_verification = {
            "ok": True,
            "file_id": "uploaded-file-id",
        }

        with patch.object(
            google_drive, "get_or_create_drive_folder",
            return_value="school-folder-id",
        ), patch.object(
            google_drive, "MediaFileUpload"
        ), patch.object(
            google_drive, "verify_drive_file",
            return_value=expected_verification,
        ) as verify:
            result = google_drive.upload_backup(service, encrypted_file)

        self.assertTrue(result["ok"])
        self.assertEqual(result["file_id"], "uploaded-file-id")
        self.assertEqual(result["folder_id"], "school-folder-id")
        self.assertEqual(result["verification"], expected_verification)

        metadata = service.files.return_value.create.call_args.kwargs["body"]
        self.assertEqual(metadata["parents"], ["school-folder-id"])
        verify.assert_called_once_with(
            service,
            "uploaded-file-id",
            encrypted_file.resolve(),
            "school-folder-id",
        )

    def test_upload_rejects_missing_encrypted_file(self):
        service = MagicMock()
        missing_file = self.root / "missing.zip.enc"

        with self.assertRaises(FileNotFoundError):
            google_drive.upload_backup(service, missing_file)

        service.files.assert_not_called()


if __name__ == "__main__":
    unittest.main()
