import os

from .commands import run_logged_command
from .config import compose_command, project_directory


def validate_compose(config, compose_executable, command_runner, logger):
    project_dir = str(project_directory(config))
    services_command = compose_command(config, compose_executable, ["config", "--services"])
    services_result = run_logged_command(
        command_runner,
        services_command,
        project_dir,
        120,
        logger,
        "Docker Compose validation",
    )
    if not services_result.ok():
        return {"ok": False, "error": "Docker Compose validation failed."}

    services = services_result.stdout.split()
    for service in config.get("services", []):
        if service not in services:
            message = "The required Compose service was not found: {0}.".format(service)
            logger.fail(message)
            return {"ok": False, "error": message}

    logger.ok("Docker Compose configuration is valid.")
    return {"ok": True, "error": ""}


def _build_env():
    env = os.environ.copy()
    env.setdefault("DOCKER_BUILDKIT", "1")
    env.setdefault("COMPOSE_DOCKER_CLI_BUILD", "1")
    return env


def deploy(config, compose_executable, command_runner, logger):
    arguments = config.get("compose", {}).get(
        "deploy_args",
        ["up", "-d", "--build", config.get("app_service", "app")],
    )
    command = compose_command(config, compose_executable, arguments)
    result = run_logged_command(
        command_runner,
        command,
        str(project_directory(config)),
        900,
        logger,
        "Application deployment",
        env=_build_env(),
    )
    if not result.ok():
        return {"ok": False, "error": "The application deployment failed."}

    logger.ok("The application was deployed.")
    return {"ok": True, "error": ""}


def capture_service_logs(config, compose_executable, command_runner, logger, tail=80):
    if not compose_executable:
        return
    service = config.get("app_service", "app")
    command = compose_command(
        config,
        compose_executable,
        ["logs", "--no-color", "--tail", str(tail), service],
    )
    result = run_logged_command(
        command_runner,
        command,
        str(project_directory(config)),
        30,
        logger,
        "Recent {0} container logs".format(service),
    )
    if result.ok() and result.stdout.strip():
        logger.info("--- Recent {0} container logs (last {1} lines) ---".format(service, tail))
        for line in result.stdout.strip().splitlines()[-tail:]:
            logger.info("  " + line)
