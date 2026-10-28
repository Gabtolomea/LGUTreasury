import time
from urllib.error import HTTPError, URLError
from urllib.request import urlopen

from .commands import run_logged_command
from .config import compose_command, project_directory


def _check_command(check, command_runner, config, logger):
    command = check.get("command")
    if not command:
        return False, "The health-check command is missing."
    result = run_logged_command(
        command_runner,
        command,
        str(project_directory(config)),
        check.get("timeout", 30),
        logger,
        check.get("name", "Health check"),
    )
    if result.ok():
        return True, ""
    if result.stderr.strip():
        return False, result.stderr.strip()
    return False, "The health-check command failed."


def _check_url(check):
    url = check.get("url")
    if not url:
        return False, "The health-check URL is missing."
    try:
        with urlopen(url, timeout=check.get("timeout", 10)) as response:
            expected_status = check.get("expected_status", 200)
            if response.status == expected_status:
                return True, ""
            return False, "The health check returned an unexpected status."
    except (HTTPError, OSError, URLError, ValueError) as error:
        return False, str(error)


def _check_container(config, compose_executable, command_runner, logger):
    service = config.get("app_service", "app")
    lookup_command = compose_command(config, compose_executable, ["ps", "-q", service])
    lookup_result = run_logged_command(
        command_runner,
        lookup_command,
        str(project_directory(config)),
        60,
        logger,
        "Application container lookup",
    )
    if not lookup_result.ok():
        return False, "The application container could not be located."

    output = lookup_result.stdout.strip()
    if output:
        container_id = output.splitlines()[0]
    else:
        container_id = ""
    if not container_id:
        return False, "The application container is not running."

    inspect_command = [
        "docker",
        "inspect",
        "--format",
        "{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}",
        container_id,
    ]
    inspect_result = run_logged_command(
        command_runner,
        inspect_command,
        str(project_directory(config)),
        60,
        logger,
        "Application container health check",
    )
    status = inspect_result.stdout.strip()
    if inspect_result.ok() and status in ("healthy", "running"):
        return True, ""
    if status:
        return False, "The application container status is {0}.".format(status)
    return False, "The application container health check failed."


def _run_check(check, compose_executable, command_runner, config, logger):
    check_type = check.get("type", "command")
    if check_type == "command":
        return _check_command(check, command_runner, config, logger)
    if check_type == "url":
        return _check_url(check)
    if check_type == "container":
        return _check_container(config, compose_executable, command_runner, logger)
    return False, "The health-check type is not supported."


def run_health_checks(config, compose_executable, command_runner, logger):
    checks = config.get("health_checks", [])
    if not checks:
        logger.warning("No health checks were configured.")
        return {"ok": False, "errors": ["No health checks were configured."]}

    logger.info("Running health checks.")
    errors = []
    for check in checks:
        name = check.get("name", "Application health")
        retries = check.get("retries", 3)
        interval = check.get("interval", 2)
        if retries < 1 or interval < 0:
            errors.append("{0} has invalid retry settings.".format(name))
            logger.fail("{0} has invalid retry settings.".format(name))
            continue

        passed = False
        last_error = "The health check failed."
        for attempt in range(retries):
            passed, last_error = _run_check(
                check,
                compose_executable,
                command_runner,
                config,
                logger,
            )
            if passed:
                break
            if attempt + 1 < retries:
                time.sleep(interval)

        if passed:
            logger.ok("{0} passed.".format(name))
        else:
            error = "{0} failed: {1}".format(name, last_error)
            logger.fail(error)
            errors.append(error)

    if errors:
        logger.fail("Required health checks failed.")
        return {"ok": False, "errors": errors}

    logger.success("Health checks completed successfully.")
    return {"ok": True, "errors": []}
