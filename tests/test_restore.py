import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from scripts.commands import CommandResult
from scripts.restore import (
    find_backup_sql_file,
    restore_database,
    _restore_container_database,
    _restore_native_database,
)


class RecordingLogger:
    def __init__(self):
        self.messages = []

    def info(self, message):
        self.messages.append(("info", message))

    def ok(self, message):
        self.messages.append(("ok", message))

    def fail(self, message):
        self.messages.append(("fail", message))

    def success(self, message):
        self.messages.append(("success", message))


class RestoreDatabaseTests(unittest.TestCase):
    def setUp(self):
        self.database = {
            "server": "host.docker.internal",
            "port": 3306,
            "database": "lgutreasury",
            "user": "lgu_app",
            "password": "Test-Password-123!",
            "ssl_mode": "required",
        }

    def test_find_backup_sql_file_from_file_and_directory(self):
        with tempfile.TemporaryDirectory() as directory:
            backup_dir = Path(directory)
            sql_file = backup_dir / "database.sql"
            sql_file.write_text("-- MySQL dump 10.13\n", encoding="utf-8")

            self.assertEqual(find_backup_sql_file(sql_file), sql_file)
            self.assertEqual(find_backup_sql_file(backup_dir), sql_file)
            self.assertIsNone(find_backup_sql_file(backup_dir / "missing"))

    def test_restore_rejects_missing_file_or_invalid_header(self):
        logger = RecordingLogger()
        result = restore_database(
            {"project_dir": "."},
            "nonexistent-path.sql",
            logger=logger,
        )
        self.assertFalse(result["ok"])
        self.assertIn("No database SQL backup file found", result["error"])

        with tempfile.TemporaryDirectory() as directory:
            bad_file = Path(directory) / "corrupt.sql"
            bad_file.write_text("random binary garbage", encoding="utf-8")
            result = restore_database(
                {"project_dir": directory},
                bad_file,
                logger=logger,
            )
            self.assertFalse(result["ok"])
            self.assertIn("recognizable SQL dump header", result["error"])

    def test_restore_container_mysql(self):
        logger = RecordingLogger()
        executed_commands = []

        def container_runner(command, cwd=None, timeout=None, input=None):
            executed_commands.append((list(command), input))
            if "ps" in command and "db" in command:
                return CommandResult(0, "db-container\n", "")
            if "exec" in command and "mysql" in command:
                return CommandResult(0, "", "")
            return CommandResult(1, "", "Unexpected command")

        with tempfile.TemporaryDirectory() as directory:
            sql_file = Path(directory) / "database.sql"
            sql_file.write_text(
                "-- MySQL dump 10.13\nCREATE TABLE test (id int);\n",
                encoding="utf-8",
            )
            config = {
                "project_dir": directory,
                "compose_files": ["compose.container-mysql.yml"],
            }

            with patch(
                "scripts.restore.container_database_config",
                return_value={
                    "database": "LGUTreasury",
                    "user": "lgu_user",
                    "password": "user-pass",
                    "root_password": "root-pass",
                },
            ):
                result = restore_database(
                    config,
                    sql_file,
                    compose_executable=["docker", "compose"],
                    command_runner=container_runner,
                    logger=logger,
                )

            self.assertTrue(result["ok"])
            restore_cmd = next(
                cmd for cmd, inp in executed_commands
                if "exec" in cmd and "mysql" in cmd
            )
            self.assertIn("MYSQL_PWD=root-pass", restore_cmd)
            self.assertIn("LGUTreasury", restore_cmd)

    def test_restore_native_mysql(self):
        logger = RecordingLogger()
        executed_commands = []

        def native_runner(command, cwd=None, timeout=None, input=None):
            executed_commands.append((list(command), input))
            if command[0] == "whoami":
                return CommandResult(
                    0,
                    '"User","S-1-5-21-123456789-123456789-123456789-1001"\n',
                    "",
                )
            if command[0] == "icacls":
                return CommandResult(0, "Successfully processed 1 files.", "")
            if "mysql.exe" in command[0]:
                return CommandResult(0, "", "")
            return CommandResult(1, "", "Unexpected command")

        with tempfile.TemporaryDirectory() as directory:
            sql_file = Path(directory) / "database.sql"
            sql_file.write_text(
                "-- MySQL dump 10.13\nCREATE TABLE test (id int);\n",
                encoding="utf-8",
            )
            config = {
                "project_dir": directory,
                "compose_files": ["compose.native-mysql.yml"],
            }

            with patch(
                "scripts.restore.native_database_config",
                return_value=self.database,
            ), patch(
                "scripts.restore._mysql_client_executable",
                return_value="mysql.exe",
            ), patch(
                "scripts.backup._is_windows",
                return_value=True,
            ):
                result = restore_database(
                    config,
                    sql_file,
                    command_runner=native_runner,
                    logger=logger,
                )

            self.assertTrue(result["ok"])
            restore_cmd, input_data = next(
                (cmd, inp) for cmd, inp in executed_commands
                if "mysql.exe" in cmd[0]
            )
            self.assertIn("lgutreasury", restore_cmd)
            self.assertIn("CREATE TABLE test", input_data)


if __name__ == "__main__":
    unittest.main()
