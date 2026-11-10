
import tempfile
import unittest
import zipfile

from io import BytesIO
from pathlib import Path

from cryptography.fernet import Fernet

from scripts.backup_encryption import (
    calculate_sha256,
    decrypt_backup_archive,
    encrypt_backup_directory,
)


class BackupEncryptionTests(unittest.TestCase):

    def setUp(self):
        self.temp_directory = tempfile.TemporaryDirectory()
        self.root = Path(self.temp_directory.name)

        self.key_path = self.root / "backup.key"
        self.key_path.write_bytes(Fernet.generate_key())

        self.backup_dir = self.root / "backup_sample"
        self.backup_dir.mkdir()

        (self.backup_dir / "database.sql").write_text(
            "-- MySQL dump test\nCREATE DATABASE lgutreasury;",
            encoding="utf-8",
        )

        self.encrypted_dir = self.root / "encrypted"
        self.restore_dir = self.root / "restored"

    def tearDown(self):
        self.temp_directory.cleanup()

    def encrypt_sample(self):
        return encrypt_backup_directory(
            self.backup_dir,
            self.encrypted_dir,
            self.key_path,
        )

    def test_encrypt_backup_directory(self):
        result = self.encrypt_sample()

        self.assertTrue(result["ok"], result["errors"])
        self.assertTrue(Path(result["encrypted_file"]).is_file())
        self.assertEqual(len(result["sha256"]), 64)
        self.assertGreater(result["size_bytes"], 0)

    def test_decrypt_restores_original_files(self):
        encrypted = self.encrypt_sample()

        self.assertTrue(encrypted["ok"], encrypted["errors"])

        restored = decrypt_backup_archive(
            encrypted["encrypted_file"],
            self.restore_dir,
            self.key_path,
        )

        self.assertTrue(restored["ok"], restored["errors"])
        self.assertEqual(restored["files_restored"], 1)

        original = self.backup_dir / "database.sql"
        recovered = self.restore_dir / "database.sql"

        self.assertEqual(
            calculate_sha256(original),
            calculate_sha256(recovered),
        )

    def test_decryption_rejects_wrong_key(self):
        encrypted = self.encrypt_sample()
        self.assertTrue(encrypted["ok"], encrypted["errors"])

        wrong_key = self.root / "wrong.key"
        wrong_key.write_bytes(Fernet.generate_key())

        result = decrypt_backup_archive(
            encrypted["encrypted_file"],
            self.restore_dir,
            wrong_key,
        )

        self.assertFalse(result["ok"])
        self.assertEqual(result["files_restored"], 0)

    def test_decryption_rejects_tampered_archive(self):
        encrypted = self.encrypt_sample()
        self.assertTrue(encrypted["ok"], encrypted["errors"])

        archive = Path(encrypted["encrypted_file"])
        data = bytearray(archive.read_bytes())
        data[-1] ^= 1
        archive.write_bytes(data)

        result = decrypt_backup_archive(
            archive,
            self.restore_dir,
            self.key_path,
        )

        self.assertFalse(result["ok"])
        self.assertEqual(result["files_restored"], 0)

    def test_restore_rejects_nonempty_directory(self):
        encrypted = self.encrypt_sample()
        self.assertTrue(encrypted["ok"], encrypted["errors"])

        self.restore_dir.mkdir()
        (self.restore_dir / "existing.txt").write_text(
            "Keep this file",
            encoding="utf-8",
        )

        with self.assertRaises(ValueError):
            decrypt_backup_archive(
                encrypted["encrypted_file"],
                self.restore_dir,
                self.key_path,
            )

        self.assertTrue((self.restore_dir / "existing.txt").is_file())

    def test_decryption_rejects_unsafe_zip_path(self):
        key = self.key_path.read_bytes()
        cipher = Fernet(key)

        archive_buffer = BytesIO()

        with zipfile.ZipFile(archive_buffer, "w") as archive:
            archive.writestr("../outside.txt", "unsafe content")

        encrypted_data = cipher.encrypt(archive_buffer.getvalue())

        malicious_file = self.root / "malicious.zip.enc"
        malicious_file.write_bytes(encrypted_data)

        result = decrypt_backup_archive(
            malicious_file,
            self.restore_dir,
            self.key_path,
        )

        self.assertFalse(result["ok"])
        self.assertFalse((self.root / "outside.txt").exists())

    def test_missing_encryption_key_is_rejected(self):
        missing_key = self.root / "missing.key"

        with self.assertRaises(FileNotFoundError):
            encrypt_backup_directory(
                self.backup_dir,
                self.encrypted_dir,
                missing_key,
            )

    def test_encryption_does_not_modify_original_backup(self):
        original = self.backup_dir / "database.sql"
        original_hash = calculate_sha256(original)

        result = self.encrypt_sample()

        self.assertTrue(result["ok"], result["errors"])
        self.assertEqual(original_hash, calculate_sha256(original))


if __name__ == "__main__":
    unittest.main()
