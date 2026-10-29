from pathlib import Path

from scripts.backup import create_backups
from scripts.cleanup import clean
from scripts.commands import run_command
from scripts.config import environment_values, project_directory
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
    "compose_files": ["compose.yaml"],
    "env_file": ".env",
    "required_env": [
        "MYSQL_PASSWORD",
        "MYSQL_ROOT_PASSWORD",
        "HTTPS_CERT_DIR",
        "HTTPS_CERT_PASSWORD",
    ],
    "backup_paths": ["compose.yaml", "Dockerfile", ".env.example"],
    "backup_dir": "backups",
    "log_file": "logs/deployment.log",
    "temporary_paths": [".deployment-tmp"],
    "minimum_free_space_mb": 100,
    "app_service": "app",
    "app_image": "lgu-treasury-app:local",
    "app_scheme": "https",
    "app_port": 8443,
    "health_path": "/health",
    "services": ["db", "app"],
    "compose": {
        "deploy_args": ["up", "-d", "--build", "app"],
    },
    "database": {
        "service": "db",
        "name": "LGUTreasury",
        "readiness_retries": 30,
        "readiness_interval": 2,
        "backup_file": "database.sql",
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
            "url": "https://localhost:8443/health",
            "expected_status": 200,
            "retries": 10,
            "interval": 3,
        },
    ],
}


class DeploymentRunner:
    def __init__(self, config=None, command_runner=None):
        self.config = config or CONFIG
        self.command_runner = command_runner or run_command
        self.logger = None
        self.compose_executable = None
        self.backup_result = None
        self.rollback_result = None
        self.cleanup_result = None
        self.health_result = None
        self.errors = []
        self.completed_stages = []
        self.failed_stage = ""
        self.deployment_started = False
        self.status = "failed"

    def initialize(self):
        log_path = project_directory(self.config) / self.config.get(
            "log_file",
            "logs/deployment.log",
        )
        values = environment_values(self.config)
        secret_values = [
            values.get("MYSQL_PASSWORD", ""),
            values.get("MYSQL_ROOT_PASSWORD", ""),
            values.get("HTTPS_CERT_PASSWORD", ""),
        ]
        self.logger = Logger(log_path, secret_values=secret_values)
        self.logger.info("Starting the Docker deployment.")

    def _record_failure(self, stage, message):
        self.failed_stage = stage
        self.errors.append(message)
        self.logger.fail(message)

    def _attempt_rollback(self):
        try:
            return restore_rollback(
                self.config,
                self.backup_result or {},
                self.compose_executable,
                self.command_runner,
                self.logger,
            )
        except Exception as error:
            self.logger.fail("Rollback failed unexpectedly.")
            return {
                "ok": False,
                "error": "Rollback failed unexpectedly: {0}.".format(error),
            }

    def _run_stages(self):
        preflight_result = run_preflight(self.config, self.command_runner, self.logger)
        if not preflight_result["ok"]:
            self.failed_stage = "preflight checks"
            self.errors.extend(preflight_result["errors"])
            return
        self.compose_executable = preflight_result["compose_command"]
        self.completed_stages.append("preflight checks")

        self.backup_result = create_backups(
            self.config,
            self.compose_executable,
            self.command_runner,
            self.logger,
        )
        if not self.backup_result["ok"]:
            self.failed_stage = "backups"
            self.errors.extend(self.backup_result["errors"])
            return
        self.completed_stages.append("backups")

        validation_result = validate_compose(
            self.config,
            self.compose_executable,
            self.command_runner,
            self.logger,
        )
        if not validation_result["ok"]:
            self.failed_stage = "Compose validation"
            self.errors.append(validation_result["error"])
            return
        self.completed_stages.append("Compose validation")

        self.deployment_started = True
        deployment_result = deploy_application(
            self.config,
            self.compose_executable,
            self.command_runner,
            self.logger,
        )
        if not deployment_result["ok"]:
            self.failed_stage = "deployment"
            self.errors.append(deployment_result["error"])
            self.rollback_result = self._attempt_rollback()
            return
        self.completed_stages.append("deployment")

        self.health_result = run_health_checks(
            self.config,
            self.compose_executable,
            self.command_runner,
            self.logger,
        )
        if not self.health_result["ok"]:
            self.failed_stage = "health checks"
            self.errors.extend(self.health_result["errors"])
            self.rollback_result = self._attempt_rollback()
            return
        self.completed_stages.append("health checks")
        self.status = "successful"

    def report(self):
        if self.status == "successful":
            self.logger.success("The Docker deployment completed successfully.")
        else:
            self.logger.fail("The Docker deployment did not complete successfully.")
        if self.completed_stages:
            self.logger.info("Completed stages: {0}.".format(", ".join(self.completed_stages)))
        if self.failed_stage:
            self.logger.info("Failed stage: {0}.".format(self.failed_stage))
        if self.health_result:
            health_status = "failed"
            if self.health_result["ok"]:
                health_status = "passed"
            self.logger.info("Health checks {0}.".format(health_status))
        else:
            self.logger.info("Health checks did not run.")
        if self.rollback_result:
            rollback_status = "failed"
            if self.rollback_result["ok"]:
                rollback_status = "successful"
            self.logger.info("Rollback was {0}.".format(rollback_status))
        else:
            self.logger.info("Rollback was not required.")
        if self.cleanup_result:
            cleanup_status = "failed"
            if self.cleanup_result["ok"]:
                cleanup_status = "successful"
            self.logger.info("Cleanup was {0}.".format(cleanup_status))
        if self.backup_result and self.backup_result.get("backup_dir"):
            self.logger.info(
                "Recovery files are preserved at {0}.".format(self.backup_result["backup_dir"])
            )
        log_path = project_directory(self.config) / self.config.get(
            "log_file",
            "logs/deployment.log",
        )
        self.logger.info("Deployment logs are preserved at {0}.".format(log_path))
        if self.status == "successful":
            self._report_application_urls()
        for error in self.errors:
            self.logger.fail(error)

    def _report_application_urls(self):
        scheme = self.config.get("app_scheme", "https")
        port = self.config.get("app_port", 8443)
        health_path = self.config.get("health_path", "/health")
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
        try:
            self.initialize()
        except Exception as error:
            print("[FAIL] The deployment logger could not be initialized: {0}.".format(error))
            return 1

        try:
            self._run_stages()
        except Exception as error:
            self.errors.append("An unexpected error occurred: {0}.".format(error))
            self.logger.fail("An unexpected error occurred during deployment.")
            if self.deployment_started:
                self.rollback_result = self._attempt_rollback()

        try:
            self.cleanup_result = clean(self.config, self.logger)
            if not self.cleanup_result["ok"]:
                self.errors.extend(self.cleanup_result["errors"])
        except Exception as error:
            self.errors.append("Cleanup failed unexpectedly: {0}.".format(error))
            self.logger.fail("Cleanup failed unexpectedly.")

        if self.rollback_result and not self.rollback_result["ok"]:
            self.errors.append(self.rollback_result["error"])
        if self.errors:
            self.status = "failed"

        self.report()
        self.logger.close()
        if self.status == "successful":
            return 0
        return 1


def run(config=None, command_runner=None):
    runner = DeploymentRunner(config, command_runner)
    return runner.run()


if __name__ == "__main__":
    raise SystemExit(run())
