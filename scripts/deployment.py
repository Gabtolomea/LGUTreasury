from .commands import run_logged_command
from .config import compose_command, project_directory


def validate_compose(config, compose_executable, command_runner, logger):
    logger.info("Validating the Docker Compose configuration.")
    project_dir = str(project_directory(config))
    validation_command = compose_command(config, compose_executable, ["config", "--quiet"])
    validation_result = run_logged_command(
        command_runner,
        validation_command,
        project_dir,
        120,
        logger,
        "Docker Compose validation",
    )
    if not validation_result.ok():
        return {"ok": False, "error": "Docker Compose validation failed."}

    services_command = compose_command(config, compose_executable, ["config", "--services"])
    services_result = run_logged_command(
        command_runner,
        services_command,
        project_dir,
        120,
        logger,
        "Docker Compose service listing",
    )
    if not services_result.ok():
        return {"ok": False, "error": "Docker Compose services could not be listed."}

    services = services_result.stdout.split()
    for service in config.get("services", []):
        if service not in services:
            return {
                "ok": False,
                "error": "The required Compose service was not found: {0}.".format(service),
            }

    logger.success("Docker Compose validation completed successfully.")
    return {"ok": True, "error": ""}


def deploy(config, compose_executable, command_runner, logger):
    logger.info("Deploying the application.")
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
    )
    if not result.ok():
        return {"ok": False, "error": "The application deployment failed."}

    logger.success("The application deployment completed successfully.")
    return {"ok": True, "error": ""}
