import io
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path
from unittest.mock import patch

from deploy import run
from scripts.commands import CommandResult
from scripts.cleanup import clean
from scripts.logger import Logger, redact_text
from scripts.network import get_lan_addresses


class FakeCommandRunner:
    def __init__(
        self,
        existing_app=False,
        fail_backup=False,
        fail_compose=False,
        fail_deployment=False,
        fail_health=False,
        fail_rollback=False,
        buildkit_image_ids=False,
    ):
        self.commands = []
        self.existing_app = existing_app
        self.fail_backup = fail_backup
        self.fail_compose = fail_compose
        self.fail_deployment = fail_deployment
        self.fail_health = fail_health
        self.fail_rollback = fail_rollback
        self.buildkit_image_ids = buildkit_image_ids
        self.health_checks = 0

    def __call__(self, command, cwd=None, timeout=None):
        self.commands.append(list(command))

        if command == ["docker", "compose", "version"]:
            return CommandResult(0, "Docker Compose version", "")
        if command == ["docker", "info", "--format", "{{.ServerVersion}}"]:
            return CommandResult(0, "28.0", "")
        if "up" in command and "db" in command:
            return CommandResult(0, "", "")
        if "ps" in command and ("-q" in command or "-aq" in command) and "db" in command:
            return CommandResult(0, "db-container\n", "")
        if "ps" in command and ("-q" in command or "-aq" in command) and "app" in command:
            if self.existing_app:
                return CommandResult(0, "app-container\n", "")
            return CommandResult(0, "", "")
        if command[:2] == ["docker", "inspect"]:
            if "db-container" in command:
                return CommandResult(0, "healthy\n", "")
            if "{{.Config.Image}}" in command:
                return CommandResult(0, "lgu-treasury-app:local\n", "")
            return CommandResult(0, "sha256:old-image\n", "")
        if command[:3] == ["docker", "image", "inspect"]:
            if self.buildkit_image_ids:
                return CommandResult(0, "sha256:buildkit-platform-image\n", "")
            return CommandResult(0, "sha256:old-image\n", "")
        if command[:3] == ["docker", "image", "save"]:
            output_index = command.index("--output") + 1
            Path(command[output_index]).write_bytes(b"image backup")
            return CommandResult(0, "", "")
        if command[:3] == ["docker", "load", "--input"]:
            if self.fail_rollback:
                return CommandResult(1, "", "Rollback load failed")
            return CommandResult(0, "Loaded image", "")
        if "mysqldump" in command:
            if self.fail_backup:
                return CommandResult(1, "", "Database backup failed")
            return CommandResult(0, "CREATE DATABASE LGUTreasury;\n", "")
        if "config" in command:
            if self.fail_compose:
                return CommandResult(1, "", "Compose validation failed")
            if "--services" in command:
                return CommandResult(0, "db\napp\n", "")
            return CommandResult(0, "", "")
        if "up" in command and "--build" in command and self.fail_deployment:
            return CommandResult(1, "", "Deployment failed")
        if command == ["health-check"]:
            self.health_checks += 1
            if self.fail_health and self.health_checks == 1:
                return CommandResult(1, "", "Health check failed")
            return CommandResult(0, "", "")
        if "up" in command and "--no-build" in command:
            return CommandResult(0, "", "")
        return CommandResult(0, "", "")


def make_config(project_directory):
    return {
        "project_dir": str(project_directory),
        "compose_files": ["compose.yaml"],
        "env_file": ".env",
        "required_env": ["MYSQL_PASSWORD", "MYSQL_ROOT_PASSWORD"],
        "backup_paths": ["compose.yaml", "Dockerfile"],
        "backup_dir": "backups",
        "log_file": "logs/deployment.log",
        "temporary_paths": [".deployment-tmp"],
        "minimum_free_space_mb": 1,
        "app_image": "lgu-treasury-app:local",
        "app_scheme": "https",
        "app_port": 8443,
        "database": {
            "service": "db",
            "name": "LGUTreasury",
            "readiness_retries": 1,
            "readiness_interval": 0,
            "backup_file": "database.sql",
        },
        "services": ["db", "app"],
        "compose": {
            "deploy_args": ["up", "-d", "--build", "app"],
        },
        "health_checks": [
            {
                "name": "Application health",
                "type": "command",
                "command": ["health-check"],
                "retries": 1,
                "interval": 0,
            }
        ],
    }


class DeploymentRunnerTests(unittest.TestCase):
    def create_project(self):
        temporary_directory = tempfile.TemporaryDirectory()
        project_directory = Path(temporary_directory.name)
        (project_directory / "compose.yaml").write_text(
            "services:\n  db:\n    image: mysql\n  app:\n    image: example\n",
            encoding="utf-8",
        )
        (project_directory / "Dockerfile").write_text("FROM scratch\n", encoding="utf-8")
        (project_directory / ".env").write_text(
            "MYSQL_PASSWORD=db-password\nMYSQL_ROOT_PASSWORD=root-password\n",
            encoding="utf-8",
        )
        return temporary_directory, project_directory

    @patch("scripts.preflight.shutil.which", return_value="docker")
    def test_successful_deployment_keeps_stage_order(self, unused_which):
        temporary_directory, project_directory = self.create_project()
        self.addCleanup(temporary_directory.cleanup)
        command_runner = FakeCommandRunner()

        exit_code = run(make_config(project_directory), command_runner)

        self.assertEqual(exit_code, 0)
        validation_index = next(
            index
            for index, command in enumerate(command_runner.commands)
            if "config" in command and "--services" not in command
        )
        deployment_index = next(
            index
            for index, command in enumerate(command_runner.commands)
            if "--build" in command
        )
        health_index = next(
            index
            for index, command in enumerate(command_runner.commands)
            if command == ["health-check"]
        )
        self.assertLess(validation_index, deployment_index)
        self.assertLess(deployment_index, health_index)

    @patch("scripts.preflight.shutil.which", return_value="docker")
    def test_buildkit_image_id_difference_does_not_block_stable_tag(self, unused_which):
        temporary_directory, project_directory = self.create_project()
        self.addCleanup(temporary_directory.cleanup)
        command_runner = FakeCommandRunner(
            existing_app=True,
            buildkit_image_ids=True,
        )

        exit_code = run(make_config(project_directory), command_runner)

        self.assertEqual(exit_code, 0)

    @patch("scripts.preflight.shutil.which", return_value="docker")
    @patch("deploy.get_lan_addresses", return_value=["192.168.1.50"])
    def test_successful_deployment_displays_application_urls(self, unused_addresses, unused_which):
        temporary_directory, project_directory = self.create_project()
        self.addCleanup(temporary_directory.cleanup)
        command_runner = FakeCommandRunner()
        output = io.StringIO()

        with redirect_stdout(output):
            exit_code = run(make_config(project_directory), command_runner)

        self.assertEqual(exit_code, 0)
        self.assertIn("Application URL: https://192.168.1.50:8443", output.getvalue())
        self.assertIn("Local URL: https://localhost:8443", output.getvalue())
        self.assertIn("Health endpoint: https://192.168.1.50:8443/health", output.getvalue())

    @patch("scripts.network.socket.socket")
    def test_lan_address_discovery_returns_only_primary_route_address(self, socket_factory):
        network_socket = socket_factory.return_value
        network_socket.getsockname.return_value = ("192.168.1.50", 0)

        addresses = get_lan_addresses()

        self.assertEqual(addresses, ["192.168.1.50"])
        network_socket.connect.assert_called_once_with(("8.8.8.8", 80))
        network_socket.close.assert_called_once_with()

    @patch("scripts.preflight.shutil.which", return_value="docker")
    def test_database_readiness_completes_before_database_backup(self, unused_which):
        temporary_directory, project_directory = self.create_project()
        self.addCleanup(temporary_directory.cleanup)
        command_runner = FakeCommandRunner()

        exit_code = run(make_config(project_directory), command_runner)

        self.assertEqual(exit_code, 0)
        readiness_index = next(
            index
            for index, command in enumerate(command_runner.commands)
            if command[:2] == ["docker", "inspect"] and "db-container" in command
        )
        backup_index = next(
            index
            for index, command in enumerate(command_runner.commands)
            if "mysqldump" in command
        )
        self.assertLess(readiness_index, backup_index)

    @patch("scripts.preflight.shutil.which", return_value="docker")
    def test_preflight_failure_does_not_deploy_or_rollback(self, unused_which):
        temporary_directory, project_directory = self.create_project()
        self.addCleanup(temporary_directory.cleanup)
        config = make_config(project_directory)
        config["required_env"] = ["MISSING_VALUE"]
        command_runner = FakeCommandRunner()

        exit_code = run(config, command_runner)

        self.assertNotEqual(exit_code, 0)
        self.assertFalse(any("--build" in command for command in command_runner.commands))
        self.assertFalse(any(["docker", "load", "--input"] == command[:4] for command in command_runner.commands))

    @patch("scripts.preflight.shutil.which", return_value="docker")
    def test_backup_failure_does_not_trigger_rollback(self, unused_which):
        temporary_directory, project_directory = self.create_project()
        self.addCleanup(temporary_directory.cleanup)
        command_runner = FakeCommandRunner(fail_backup=True, existing_app=True)

        exit_code = run(make_config(project_directory), command_runner)

        self.assertNotEqual(exit_code, 0)
        self.assertFalse(any("--build" in command for command in command_runner.commands))
        self.assertFalse(any(command[:3] == ["docker", "load", "--input"] for command in command_runner.commands))

    @patch("scripts.preflight.shutil.which", return_value="docker")
    def test_invalid_database_name_stops_before_deployment(self, unused_which):
        temporary_directory, project_directory = self.create_project()
        self.addCleanup(temporary_directory.cleanup)
        (project_directory / ".env").write_text(
            "MYSQL_PASSWORD=db-password\n"
            "MYSQL_ROOT_PASSWORD=root-password\n"
            "MYSQL_DATABASE=invalid;database\n",
            encoding="utf-8",
        )
        command_runner = FakeCommandRunner()

        exit_code = run(make_config(project_directory), command_runner)

        self.assertNotEqual(exit_code, 0)
        self.assertFalse(any("--build" in command for command in command_runner.commands))

    @patch("scripts.preflight.shutil.which", return_value="docker")
    def test_compose_validation_failure_does_not_trigger_rollback(self, unused_which):
        temporary_directory, project_directory = self.create_project()
        self.addCleanup(temporary_directory.cleanup)
        command_runner = FakeCommandRunner(fail_compose=True)

        exit_code = run(make_config(project_directory), command_runner)

        self.assertNotEqual(exit_code, 0)
        self.assertFalse(any("--build" in command for command in command_runner.commands))
        self.assertFalse(any(command[:3] == ["docker", "load", "--input"] for command in command_runner.commands))

    @patch("scripts.preflight.shutil.which", return_value="docker")
    def test_deployment_failure_triggers_rollback(self, unused_which):
        temporary_directory, project_directory = self.create_project()
        self.addCleanup(temporary_directory.cleanup)
        command_runner = FakeCommandRunner(existing_app=True, fail_deployment=True)

        exit_code = run(make_config(project_directory), command_runner)

        self.assertNotEqual(exit_code, 0)
        self.assertTrue(any(command[:3] == ["docker", "load", "--input"] for command in command_runner.commands))

    @patch("scripts.preflight.shutil.which", return_value="docker")
    def test_health_failure_triggers_verified_rollback(self, unused_which):
        temporary_directory, project_directory = self.create_project()
        self.addCleanup(temporary_directory.cleanup)
        command_runner = FakeCommandRunner(existing_app=True, fail_health=True)

        exit_code = run(make_config(project_directory), command_runner)

        self.assertNotEqual(exit_code, 0)
        self.assertTrue(any(command[:3] == ["docker", "load", "--input"] for command in command_runner.commands))
        self.assertGreaterEqual(command_runner.health_checks, 2)

    @patch("scripts.preflight.shutil.which", return_value="docker")
    def test_rollback_failure_returns_nonzero(self, unused_which):
        temporary_directory, project_directory = self.create_project()
        self.addCleanup(temporary_directory.cleanup)
        command_runner = FakeCommandRunner(
            existing_app=True,
            fail_deployment=True,
            fail_rollback=True,
        )

        exit_code = run(make_config(project_directory), command_runner)

        self.assertNotEqual(exit_code, 0)

    def test_secret_values_are_redacted_from_command_text(self):
        text = "MYSQL_PWD=root-password password=root-password"

        redacted = redact_text(text, ["root-password"])

        self.assertNotIn("root-password", redacted)
        self.assertIn("[REDACTED]", redacted)

    @patch.dict("os.environ", {"NO_COLOR": ""})
    def test_logger_colors_only_the_level(self):
        stream = io.StringIO()
        stream.isatty = lambda: True

        with tempfile.TemporaryDirectory() as temporary_directory:
            logger = Logger(Path(temporary_directory) / "app.log", stream=stream)
            logger.info("message")
            logger.close()

            self.assertEqual(stream.getvalue(), "\033[36m[INFO]\033[0m message\n")
            self.assertNotIn("\033[", (Path(temporary_directory) / "app.log").read_text())

    def test_cleanup_preserves_backup_and_log_paths(self):
        temporary_directory, project_directory = self.create_project()
        self.addCleanup(temporary_directory.cleanup)
        backup_path = project_directory / "backups" / "keep.txt"
        log_path = project_directory / "logs" / "keep.txt"
        backup_path.parent.mkdir()
        log_path.parent.mkdir()
        backup_path.write_text("backup", encoding="utf-8")
        log_path.write_text("log", encoding="utf-8")
        config = make_config(project_directory)
        config["temporary_paths"] = ["backups", "logs"]

        logger = type(
            "TestLogger",
            (),
            {
                "warning": lambda self, message: None,
                "ok": lambda self, message: None,
                "fail": lambda self, message: None,
                "success": lambda self, message: None,
            },
        )()

        result = clean(config, logger)

        self.assertTrue(result["ok"])
        self.assertTrue(backup_path.exists())
        self.assertTrue(log_path.exists())


if __name__ == "__main__":
    unittest.main()
