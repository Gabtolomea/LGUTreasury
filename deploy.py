import argparse
import time
from copy import deepcopy
from pathlib import Path

from scripts.backup import create_data_backups, create_recovery_backups
from scripts.cleanup import clean
from scripts.commands import run_command
from scripts.config import (
    environment_values,
    is_container_mysql,
    native_database_config,
    project_directory,
)
from scripts.deployment import deploy as deploy_application
from scripts.deployment import validate_compose
from scripts.health import run_health_checks
from scripts.logger import Logger
from scripts.network import get_lan_addresses
from scripts.preflight import run_preflight
from scripts.rollback import rollback as restore_rollback


PROJECT_DIRECTORY = Path(__file__).resolve().parent

CONFIG = {
    "project_dir": str(PROJECT_DIRECTORY),
    "compose_files": ["compose.native-mysql.yml"],
    "env_file": ".env",
    "required_env": [
        "NATIVE_MYSQL_CONNECTION",
    ],
    "backup_paths": [
        "compose.native-mysql.yml",
        "compose.container-mysql.yml",
        "Dockerfile",
        "docker-entrypoint.sh",
        ".env.example",
    ],
    "backup_dir": "backups",
    "log_file": "logs/deployment.log",
    "temporary_paths": [".deployment-tmp"],
    "minimum_free_space_mb": 100,
    "app_service": "app",
    "app_image": "lgu-treasury-app:local",
    "app_scheme": "http",
    "app_port": 8092,
    "health_path": "/health/ready",
    "health_check_timeout": 120,
    "services": ["app"],
    "compose": {
        "deploy_args": [
            "up",
            "-d",
            "--build",
            "--wait",
            "--wait-timeout",
            "180",
            "app",
        ],
    },
    "database": {
        "name": "lgutreasury",
        "backup_file": "database.sql",
        "mysqldump_executable": (
            r"C:\Program Files\MySQL\MySQL Server 8.0\bin\mysqldump.exe"
        ),
    },
    "health_checks": [
        {
            "name": "Application container health",
            "type": "container",
            "retries": 20,
            "interval": 3,
        },
        {
            "name": "Application health endpoint",
            "type": "url",
            "url": "http://localhost:8092/health/ready",
            "expected_status": 200,
            "timeout": 5,
            "retries": 10,
            "interval": 3,
        },
    ],
}


class DeploymentRunner:
    def __init__(self, config=None, command_runner=None, verbose=False):
        self.config = deepcopy(CONFIG if config is None else config)
        self._configure_setup()
        if config is None:
            self._configure_transport()
        self.command_runner = command_runner or run_command
        self.verbose = verbose
        self.logger = None
        self.compose_executable = None
        self.backup_result = None
        self.rollback_result = None
        self.cleanup_result = None
        self.health_result = None
        self.errors = []
        self.failed_stage = ""
        self.deployment_started = False
        self.status = "failed"
        self.started_at = time.monotonic()

    def _configure_setup(self):
        values = environment_values(self.config)
        compose_env = values.get("COMPOSE_FILE")
        if (
            compose_env
            and self.config.get("compose_files") == ["compose.native-mysql.yml"]
        ):
            self.config["compose_files"] = [compose_env]

        if is_container_mysql(self.config):
            if self.config.get("required_env") == ["NATIVE_MYSQL_CONNECTION"]:
                self.config["required_env"] = [
                    "MYSQL_PASSWORD",
                    "MYSQL_ROOT_PASSWORD",
                ]
            if self.config.get("services") == ["app"]:
                self.config["services"] = ["db", "app"]
            if self.config.get("compose", {}).get("deploy_args") == [
                "up",
                "-d",
                "--build",
                "--wait",
                "--wait-timeout",
                "180",
                "app",
            ]:
                self.config["compose"]["deploy_args"] = [
                    "up",
                    "-d",
                    "--build",
                    "--wait",
                    "--wait-timeout",
                    "180",
                ]
            checks = self.config.get("health_checks", [])
            has_db_check = any(c.get("service") == "db" for c in checks)
            if not has_db_check and checks and checks[0].get("type") == "container":
                db_check = {
                    "name": "Database container health",
                    "type": "container",
                    "service": "db",
                    "retries": 20,
                    "interval": 3,
                }
                self.config["health_checks"] = [db_check] + checks

    def _configure_transport(self):
        values = environment_values(self.config)
        enable_https = values.get("ENABLE_HTTPS", "false").strip().lower() == "true"

        if enable_https:
            scheme = "https"
            port = 8443
            required_env = self.config.setdefault("required_env", [])
            for name in ("HTTPS_CERT_DIR", "HTTPS_CERT_PASSWORD"):
                if name not in required_env:
                    required_env.append(name)
        else:
            scheme = "http"
            port = 8092

        self.config["app_scheme"] = scheme
        self.config["app_port"] = port
        for health_check in self.config.get("health_checks", []):
            if health_check.get("type") == "url":
                # The container publishes plain HTTP on 8092 even when external
                # clients use HTTPS on 8443. Probe the HTTP endpoint to avoid
                # false failures from self-signed/local TLS certificates.
                health_check["url"] = "http://localhost:8092/health/ready"

    def initialize(self):
        log_path = project_directory(self.config) / self.config.get(
            "log_file",
            "logs/deployment.log",
        )
        values = environment_values(self.config)
        database_config = native_database_config(self.config)

        secret_values = [
            database_config.get("password", ""),
            values.get("MYSQL_PASSWORD", ""),
            values.get("MYSQL_ROOT_PASSWORD", ""),
            values.get("HTTPS_CERT_PASSWORD", ""),
            values.get("INITIAL_ADMIN_PASSWORD", ""),
        ]
        self.logger = Logger(
            log_path,
            secret_values=secret_values,
            verbose=self.verbose,
        )
        self.logger.info("Starting the Docker deployment.")

    def _fail_stage(self, stage, errors):
        self.failed_stage = stage
        self.errors.extend(errors)
        self.logger.fail("The deployment failed during the {0} stage.".format(stage))

    def _attempt_rollback(self):
        backup_dir = (self.backup_result or {}).get("backup_dir", "")
        message = "Starting automatic rollback to the previous application version."
        if backup_dir:
            message += " The recovery backup is at {0}.".format(backup_dir)
        self.logger.warning(message)
        try:
            result = restore_rollback(
                self.config,
                self.backup_result or {},
                self.compose_executable,
                self.command_runner,
                self.logger,
            )
        except Exception as error:
            message = "The automatic rollback failed unexpectedly: {0}.".format(error)
            self.logger.fail(message)
            return {"ok": False, "error": message}

        if result["ok"]:
            self.logger.ok("The automatic rollback completed successfully.")
        else:
            self.logger.fail(result["error"])
        return result

    def _run_stages(self):
        self.logger.info("Stage 1/8: Running preflight checks...")
        preflight_result = run_preflight(self.config, self.command_runner, self.logger)
        if not preflight_result["ok"]:
            self._fail_stage("preflight checks", preflight_result["errors"])
            return
        self.compose_executable = preflight_result["compose_command"]

        self.logger.info("Stage 2/8: Creating recovery backups...")
        backup_dir, recovery_errors = create_recovery_backups(self.config, self.logger)
        if recovery_errors:
            self.backup_result = {
                "ok": False,
                "errors": recovery_errors,
                "backup_dir": str(backup_dir),
            }
            self._fail_stage("recovery backups", recovery_errors)
            return

        self.logger.info(
            "Stage 3/8: Checking the database and application image backups..."
        )
        backup_errors = create_data_backups(
            self.config,
            self.compose_executable,
            self.command_runner,
            self.logger,
            backup_dir,
        )
        self.backup_result = {
            "ok": not backup_errors,
            "errors": backup_errors,
            "backup_dir": str(backup_dir),
        }
        if backup_errors:
            self._fail_stage("database and image backups", backup_errors)
            return

        self.logger.info("Stage 4/8: Validating Docker Compose configuration...")
        validation_result = validate_compose(
            self.config,
            self.compose_executable,
            self.command_runner,
            self.logger,
        )
        if not validation_result["ok"]:
            self._fail_stage("Docker Compose validation", [validation_result["error"]])
            return

        self.deployment_started = True
        self.logger.info("Stage 5/8: Deploying the application...")
        deployment_result = deploy_application(
            self.config,
            self.compose_executable,
            self.command_runner,
            self.logger,
        )
        if not deployment_result["ok"]:
            self._fail_stage("application deployment", [deployment_result["error"]])
            self.rollback_result = self._attempt_rollback()
            return

        self.logger.info("Stage 6/8: Running deployment health checks...")
        self.health_result = run_health_checks(
            self.config,
            self.compose_executable,
            self.command_runner,
            self.logger,
        )
        if not self.health_result["ok"]:
            self._fail_stage("deployment health checks", self.health_result["errors"])
            self.rollback_result = self._attempt_rollback()
            return
        self.status = "successful"

    def report(self):
        duration = time.monotonic() - self.started_at

        if self.status == "successful":
            self.logger.done(
                "Deployment completed successfully in {0:.2f}s.".format(duration)
            )
        else:
            self.logger.fail("Deployment failed in {0:.2f}s.".format(duration))
            if self.failed_stage:
                self.logger.info("Failed stage: {0}.".format(self.failed_stage))

        self._report_health_status()
        self._report_recovery_status()

        log_path = project_directory(self.config) / self.config.get(
            "log_file",
            "logs/deployment.log",
        )
        self.logger.info("Deployment log: {0}".format(log_path))
        self._report_application_urls()

    def _report_health_status(self):
        if not self.health_result:
            self.logger.info("Database health: Not checked.")
            self.logger.info("Application health: Not checked.")
            self.logger.info("Health checks: Not run.")
            return

        checks = self.health_result.get("checks", [])
        passed = len([check for check in checks if check.get("status") == "passed"])

        database_check = None
        for check in checks:
            if check.get("service") == "db":
                database_check = check
                break

        if database_check is None:
            self.logger.info("Database health: Not checked.")
        elif database_check.get("status") == "passed":
            self.logger.info("Database health: Healthy.")
        else:
            self.logger.info("Database health: Failed.")

        if self.health_result["ok"]:
            self.logger.info("Application health: Healthy.")
        else:
            self.logger.info("Application health: Failed.")

        self.logger.info("Health checks: {0}/{1} passed.".format(passed, len(checks)))

    def _report_recovery_status(self):
        if self.rollback_result:
            if self.rollback_result["ok"]:
                self.logger.info("Rollback: Completed successfully.")
            else:
                self.logger.info("Rollback: Failed.")
        else:
            self.logger.info("Rollback: Not required.")

        if self.cleanup_result:
            if self.cleanup_result["ok"]:
                self.logger.info("Cleanup: Succeeded.")
            else:
                self.logger.info("Cleanup: Failed.")
        else:
            self.logger.info("Cleanup: Not run.")

        if self.backup_result and self.backup_result.get("backup_dir"):
            self.logger.info(
                "Recovery backup: {0}".format(self.backup_result["backup_dir"])
            )
        else:
            self.logger.info("Recovery backup: Not created.")

    def _report_application_urls(self):
        scheme = self.config.get("app_scheme", "https")
        port = self.config.get("app_port", 8443)
        health_path = self.config.get("health_path", "/health/ready")
        lan_addresses = get_lan_addresses()
        for address in lan_addresses:
            self.logger.info("Application URL: {0}://{1}:{2}.".format(scheme, address, port))
        self.logger.info("Local URL: {0}://localhost:{1}.".format(scheme, port))
        if lan_addresses:
            for address in lan_addresses:
                self.logger.info(
                    "Health endpoint: {0}://{1}:{2}{3}.".format(
                        scheme,
                        address,
                        port,
                        health_path,
                    )
                )
        else:
            self.logger.info(
                "Health endpoint: {0}://localhost:{1}{2}.".format(
                    scheme,
                    port,
                    health_path,
                )
            )

    def run(self):
        self.started_at = time.monotonic()

        try:
            self.initialize()
        except Exception as error:
            print("[FAIL] The deployment logger could not be initialized: {0}.".format(error))
            return 1

        try:
            self._run_stages()
        except Exception as error:
            self.errors.append("An unexpected error occurred: {0}.".format(error))
            self.logger.fail(
                "An unexpected error occurred during deployment: {0}".format(error)
            )
            if self.deployment_started:
                self.rollback_result = self._attempt_rollback()

        try:
            self.logger.info("Stage 7/8: Performing safe cleanup...")
            self.cleanup_result = clean(self.config, self.logger)
            if not self.cleanup_result["ok"]:
                self.errors.extend(self.cleanup_result["errors"])
        except Exception as error:
            self.cleanup_result = {
                "ok": False,
                "errors": ["Cleanup failed unexpectedly: {0}.".format(error)],
            }
            self.errors.extend(self.cleanup_result["errors"])
            self.logger.fail("Cleanup failed unexpectedly: {0}".format(error))

        if self.rollback_result and not self.rollback_result["ok"]:
            self.errors.append(self.rollback_result["error"])
        if self.errors:
            self.status = "failed"

        self.logger.info("Stage 8/8: Deployment summary")
        self.report()
        self.logger.close()
        if self.status == "successful":
            return 0
        return 1


def run(config=None, command_runner=None, verbose=False):
    runner = DeploymentRunner(config, command_runner, verbose=verbose)
    return runner.run()


def main():
    parser = argparse.ArgumentParser(
        description="Deploy the LGU Treasury application with Docker Compose."
    )
    parser.add_argument(
        "--verbose",
        action="store_true",
        help="Show the executed commands in the terminal output.",
    )
    arguments = parser.parse_args()
    return run(verbose=arguments.verbose)


if __name__ == "__main__":
    raise SystemExit(main())
