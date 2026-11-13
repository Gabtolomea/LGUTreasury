import io
import json
import os
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path
from unittest.mock import patch

from deploy import DeploymentRunner, run
from scripts.backup import prune_old_backups
from scripts.commands import CommandResult
from scripts.lock import DeploymentLock, DeploymentLockError
from scripts.logger import Logger
from tests.test_deployment_runner import FakeCommandRunner, make_config


class DeploymentHardeningTests(unittest.TestCase):
    def create_project(self):
        temporary_directory = tempfile.TemporaryDirectory()
        project_dir = Path(temporary_directory.name)
        (project_dir / "compose.native-mysql.yml").write_text("services:\n  app:\n    image: lgu-treasury-app:local\n", encoding="utf-8")
        (project_dir / "compose.container-mysql.yml").write_text("services:\n  app:\n    image: lgu-treasury-app:local\n", encoding="utf-8")
        (project_dir / "Dockerfile").write_text("FROM scratch\n", encoding="utf-8")
        (project_dir / ".env").write_text(
            "NATIVE_MYSQL_CONNECTION=Server=127.0.0.1;Port=3306;Database=LGUTreasury;User=lgu_app;Password=real-secret-password;SslMode=Required;\n",
            encoding="utf-8",
        )
        return temporary_directory, project_dir

    def test_deployment_lock_blocks_concurrent_run_and_cleans_stale_pid(self):
        with tempfile.TemporaryDirectory() as tmp:
            lock_path = Path(tmp) / ".deployment.lock"
            with DeploymentLock(lock_path):
                with self.assertRaises(DeploymentLockError):
                    DeploymentLock(lock_path).acquire()
            self.assertFalse(lock_path.exists())

            # Stale PID lock should be cleaned up automatically
            lock_path.write_text(json.dumps({"pid": 99999999, "created_at": 0}), encoding="utf-8")
            with DeploymentLock(lock_path):
                self.assertTrue(lock_path.exists())
            self.assertFalse(lock_path.exists())

    @patch("scripts.preflight.shutil.which", return_value="docker")
    def test_dry_run_skips_backup_and_deployment(self, unused_which):
        temp_dir, project_dir = self.create_project()
        self.addCleanup(temp_dir.cleanup)
        config = make_config(project_dir)
        config["dry_run"] = True
        runner = FakeCommandRunner()

        exit_code = run(config, runner)

        self.assertEqual(exit_code, 0)
        self.assertFalse(any("--build" in cmd for cmd in runner.commands))
        self.assertFalse(any("mysqldump" in str(cmd).lower() for cmd in runner.commands))
        self.assertTrue(any("config" in cmd and "--services" in cmd for cmd in runner.commands))

    @patch("scripts.preflight.shutil.which", return_value="docker")
    def test_json_output_emits_valid_json(self, unused_which):
        temp_dir, project_dir = self.create_project()
        self.addCleanup(temp_dir.cleanup)
        config = make_config(project_dir)
        config["dry_run"] = True
        config["json_output"] = True
        runner = FakeCommandRunner()

        output = io.StringIO()
        with redirect_stdout(output):
            exit_code = run(config, runner)

        self.assertEqual(exit_code, 0)
        payload = json.loads(output.getvalue())
        self.assertEqual(payload["status"], "successful")
        self.assertIn("duration_seconds", payload)

    @patch("scripts.preflight.shutil.which", return_value="docker")
    def test_preflight_rejects_placeholder_env_and_missing_https_cert(self, unused_which):
        temp_dir, project_dir = self.create_project()
        self.addCleanup(temp_dir.cleanup)
        (project_dir / ".env").write_text(
            "NATIVE_MYSQL_CONNECTION=Server=127.0.0.1;Port=3306;Database=LGUTreasury;User=lgu_app;Password=YOUR_DB_PASSWORD;SslMode=Required;\n"
            "ENABLE_HTTPS=true\n"
            "HTTPS_CERT_DIR=./.aspnet/https\n",
            encoding="utf-8",
        )
        config = make_config(project_dir)
        runner = FakeCommandRunner()

        exit_code = run(config, runner)

        self.assertNotEqual(exit_code, 0)
        self.assertFalse(any("--build" in cmd for cmd in runner.commands))

    @patch("scripts.preflight.shutil.which", return_value="docker")
    def test_preflight_rejects_insecure_ssl_mode_unless_overridden(self, unused_which):
        temp_dir, project_dir = self.create_project()
        self.addCleanup(temp_dir.cleanup)
        (project_dir / ".env").write_text(
            "NATIVE_MYSQL_CONNECTION=Server=127.0.0.1;Port=3306;Database=LGUTreasury;User=lgu_app;Password=real-secret;SslMode=Preferred;\n",
            encoding="utf-8",
        )
        config = make_config(project_dir)
        runner = FakeCommandRunner()

        self.assertNotEqual(run(config, runner), 0)

        config_override = make_config(project_dir)
        config_override["allow_insecure_db_tls"] = True
        config_override["dry_run"] = True
        self.assertEqual(run(config_override, FakeCommandRunner()), 0)

    @patch("scripts.preflight.shutil.which", return_value="docker")
    def test_no_build_and_skip_backup_flags(self, unused_which):
        temp_dir, project_dir = self.create_project()
        self.addCleanup(temp_dir.cleanup)
        config = make_config(project_dir)
        config["no_build"] = True
        config["skip_backup"] = True
        runner = FakeCommandRunner()

        exit_code = run(config, runner)

        self.assertEqual(exit_code, 0)
        self.assertFalse(any("--build" in cmd for cmd in runner.commands))
        self.assertFalse(any("mysqldump" in str(cmd).lower() for cmd in runner.commands))

    @patch("scripts.preflight.shutil.which", return_value="docker")
    def test_save_image_tar_opt_in_writes_tar_and_manifest(self, unused_which):
        temp_dir, project_dir = self.create_project()
        self.addCleanup(temp_dir.cleanup)
        config = make_config(project_dir)
        config["save_image_tar"] = True
        runner = FakeCommandRunner(existing_app=True)

        exit_code = run(config, runner)

        self.assertEqual(exit_code, 0)
        self.assertTrue(any(cmd[:3] == ["docker", "image", "save"] for cmd in runner.commands))
        backup_dirs = list((project_dir / "backups").glob("backup_*"))
        self.assertEqual(len(backup_dirs), 1)
        manifest = json.loads((backup_dirs[0] / "manifest.json").read_text(encoding="utf-8"))
        self.assertTrue(manifest["has_rollback_tag"])
        self.assertTrue(manifest["has_image_tar"])

    def test_prune_old_backups_keeps_latest_n(self):
        with tempfile.TemporaryDirectory() as tmp:
            project_dir = Path(tmp)
            backups = project_dir / "backups"
            backups.mkdir()
            for index in range(1, 8):
                (backups / f"backup_2026010{index}_000000_000000").mkdir()

            logger = Logger(project_dir / "logs" / "test.log", stream=io.StringIO())
            try:
                removed = prune_old_backups({"project_dir": str(project_dir), "backup_dir": "backups"}, logger, keep=3)
            finally:
                logger.close()

            self.assertEqual(len(removed), 4)
            remaining = sorted(p.name for p in backups.iterdir())
            self.assertEqual(
                remaining,
                [
                    "backup_20260105_000000_000000",
                    "backup_20260106_000000_000000",
                    "backup_20260107_000000_000000",
                ],
            )

    @patch("scripts.preflight.shutil.which", return_value="docker")
    def test_manual_rollback_restores_latest_backup(self, unused_which):
        temp_dir, project_dir = self.create_project()
        self.addCleanup(temp_dir.cleanup)
        backup_dir = project_dir / "backups" / "backup_20260101_000000_000000"
        backup_dir.mkdir(parents=True)
        (backup_dir / "rollback-tag.txt").write_text("lgu-treasury-app:rollback", encoding="utf-8")

        config = make_config(project_dir)
        config["rollback_mode"] = True
        runner = FakeCommandRunner(existing_app=True)

        exit_code = run(config, runner)

        self.assertEqual(exit_code, 0)
        self.assertTrue(
            any(
                cmd == ["docker", "tag", "lgu-treasury-app:rollback", "lgu-treasury-app:local"]
                for cmd in runner.commands
            )
        )
        self.assertTrue(any("--no-build" in cmd for cmd in runner.commands))
