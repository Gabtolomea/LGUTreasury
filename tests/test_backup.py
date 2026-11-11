
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from scripts.backup import (
    _backup_database,
    _option_value,
)
from scripts.commands import CommandResult


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


class NativeMySQLBackupRunner:
    def __init__(self):
        self.commands = []
        self.credentials_content = ""

    def __call__(self, command, cwd=None, timeout=None):
        self.commands.append(list(command))

        if command[0] == "whoami":
            return CommandResult(
                0,
                '"User","S-1-5-21-123456789-123456789-123456789-1001"\n',
                "",
            )

        if command[0] == "icacls":
            return CommandResult(0, "Successfully processed 1 files.", "")

        if "mysqldump.exe" in command[0]:
            option_argument = next(
                item for item in command
                if item.startswith("--defaults-extra-file=")
            )
            credentials_file = Path(option_argument.split("=", 1)[1])

            self.credentials_content = credentials_file.read_text(
                encoding="utf-8"
            )

            return CommandResult(
                0,
                "-- MySQL dump 10.13\nCREATE DATABASE lgutreasury;\n",
                "",
            )

        return CommandResult(1, "", "Unexpected command")


class NativeMySQLBackupTests(unittest.TestCase):
    def setUp(self):
        self.database = {
            "server": "host.docker.internal",
            "port": 3306,
            "database": "lgutreasury",
            "user": "lgu_app",
            "password": "Test-Password-123!",
            "ssl_mode": "required",
        }

    def test_option_value_escapes_special_characters(self):
        result = _option_value('test"password\\value')

        self.assertEqual(
            result,
            '"test\\"password\\\\value"',
        )

    def test_backup_uses_native_mysql_and_saves_sql_file(self):
        logger = RecordingLogger()
        runner = NativeMySQLBackupRunner()

        with tempfile.TemporaryDirectory() as directory:
            backup_dir = Path(directory)

            with patch(
                "scripts.backup.native_database_config",
                return_value=self.database,
            ), patch(
                "scripts.backup._mysqldump_executable",
                return_value="mysqldump.exe",
            ):
                error = _backup_database(
                    {"project_dir": directory},
                    ["docker", "compose"],
                    runner,
                    logger,
                    backup_dir,
                )

            self.assertEqual(error, "")

            backup_file = backup_dir / "database.sql"
            self.assertTrue(backup_file.exists())
            self.assertIn(
                "CREATE DATABASE lgutreasury;",
                backup_file.read_text(encoding="utf-8"),
            )

            dump_commands = [
                command for command in runner.commands
                if "mysqldump.exe" in command[0]
            ]

            self.assertEqual(len(dump_commands), 1)
            self.assertIn("--single-transaction", dump_commands[0])
            self.assertIn("--databases", dump_commands[0])
            self.assertIn("lgutreasury", dump_commands[0])

            self.assertIn(
                'password="Test-Password-123!"',
                runner.credentials_content,
            )

            self.assertFalse(
                any(
                    "Test-Password-123!" in str(command)
                    for command in runner.commands
                )
            )

            self.assertFalse(
                any(
                    "Test-Password-123!" in message
                    for _, message in logger.messages
                )
            )

            credentials_argument = next(
                item for item in dump_commands[0]
                if item.startswith("--defaults-extra-file=")
            )
            credentials_file = Path(
                credentials_argument.split("=", 1)[1]
            )

            self.assertFalse(credentials_file.exists())

    def test_backup_rejects_missing_database_configuration(self):
        logger = RecordingLogger()
        runner = NativeMySQLBackupRunner()

        with tempfile.TemporaryDirectory() as directory:
            with patch(
                "scripts.backup.native_database_config",
                return_value={},
            ):
                error = _backup_database(
                    {"project_dir": directory},
                    ["docker", "compose"],
                    runner,
                    logger,
                    Path(directory),
                )

        self.assertEqual(error, "NATIVE_MYSQL_CONNECTION is missing.")
        self.assertEqual(runner.commands, [])

    def test_backup_rejects_non_tls_connection(self):
        logger = RecordingLogger()
        runner = NativeMySQLBackupRunner()

        database = dict(self.database)
        database["ssl_mode"] = "none"

        with tempfile.TemporaryDirectory() as directory:
            with patch(
                "scripts.backup.native_database_config",
                return_value=database,
            ):
                error = _backup_database(
                    {"project_dir": directory},
                    ["docker", "compose"],
                    runner,
                    logger,
                    Path(directory),
                )

        self.assertIn("must require TLS", error)
        self.assertEqual(runner.commands, [])

    def test_backup_rejects_invalid_database_name(self):
        logger = RecordingLogger()
        runner = NativeMySQLBackupRunner()

        database = dict(self.database)
        database["database"] = "lgu treasury; DROP DATABASE test"

        with tempfile.TemporaryDirectory() as directory:
            with patch(
                "scripts.backup.native_database_config",
                return_value=database,
            ):
                error = _backup_database(
                    {"project_dir": directory},
                    ["docker", "compose"],
                    runner,
                    logger,
                    Path(directory),
                )

        self.assertIn("database name is invalid", error)
        self.assertEqual(runner.commands, [])

    def test_backup_container_mysql_when_container_running(self):
        logger = RecordingLogger()

        def container_runner(command, cwd=None, timeout=None):
            if "ps" in command and "db" in command:
                return CommandResult(0, "db-container-123\n", "")
            if "exec" in command and "mysqldump" in command:
                return CommandResult(
                    0,
                    "-- MySQL dump 10.13\nCREATE DATABASE LGUTreasury;\n",
                    "",
                )
            return CommandResult(1, "", "Unexpected command")

        with tempfile.TemporaryDirectory() as directory:
            backup_dir = Path(directory)
            config = {
                "project_dir": directory,
                "compose_files": ["compose.container-mysql.yml"],
            }

            with patch(
                "scripts.backup.container_database_config",
                return_value={
                    "database": "LGUTreasury",
                    "user": "lgu_user",
                    "password": "user-pass",
                    "root_password": "root-pass",
                },
            ):
                error = _backup_database(
                    config,
                    ["docker", "compose"],
                    container_runner,
                    logger,
                    backup_dir,
                )

            self.assertEqual(error, "")
            backup_file = backup_dir / "database.sql"
            self.assertTrue(backup_file.exists())
            self.assertIn("CREATE DATABASE LGUTreasury;", backup_file.read_text())

    def test_backup_container_mysql_when_container_not_running(self):
        logger = RecordingLogger()

        def container_runner(command, cwd=None, timeout=None):
            if "ps" in command and "db" in command:
                return CommandResult(0, "", "")
            return CommandResult(1, "", "Unexpected command")

        with tempfile.TemporaryDirectory() as directory:
            backup_dir = Path(directory)
            config = {
                "project_dir": directory,
                "compose_files": ["compose.container-mysql.yml"],
            }

            error = _backup_database(
                config,
                ["docker", "compose"],
                container_runner,
                logger,
                backup_dir,
            )

            self.assertEqual(error, "")
            self.assertTrue(
                any("No previous database container was found" in msg for _, msg in logger.messages)
            )

    def test_backup_native_mysql_on_posix_permissions(self):
        logger = RecordingLogger()
        runner_commands = []

        def posix_runner(command, cwd=None, timeout=None):
            runner_commands.append(list(command))
            if "mysqldump" in command[0]:
                return CommandResult(
                    0,
                    "-- MySQL dump 10.13\nCREATE DATABASE lgutreasury;\n",
                    "",
                )
            return CommandResult(1, "", "Unexpected command")

        with tempfile.TemporaryDirectory() as directory:
            backup_dir = Path(directory)

            with patch(
                "scripts.backup.native_database_config",
                return_value=self.database,
            ), patch(
                "scripts.backup._mysqldump_executable",
                return_value="mysqldump",
            ), patch(
                "scripts.backup._is_windows",
                return_value=False,
            ), patch(
                "os.chmod"
            ) as mock_chmod:
                error = _backup_database(
                    {"project_dir": directory},
                    ["docker", "compose"],
                    posix_runner,
                    logger,
                    backup_dir,
                )

            self.assertEqual(error, "")
            self.assertTrue((backup_dir / "database.sql").exists())
            self.assertGreaterEqual(mock_chmod.call_count, 2)
            self.assertFalse(any("icacls" in cmd[0] for cmd in runner_commands))


if __name__ == "__main__":
    unittest.main()
