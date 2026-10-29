import json
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


def _check_container(config, compose_executable, command_runner):
    service = config.get("app_service", "app")
    lookup_command = compose_command(config, compose_executable, ["ps", "-q", service])
    lookup_result = command_runner(lookup_command, str(project_directory(config)), 60)
    if not lookup_result.ok():
        if lookup_result.stderr.strip():
            return False, lookup_result.stderr.strip()
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
        "{{json .State}}",
        container_id,
    ]
    inspect_result = command_runner(inspect_command, str(project_directory(config)), 60)
    if not inspect_result.ok():
        if inspect_result.stderr.strip():
            return False, inspect_result.stderr.strip()
        return False, "The application container health check failed."

    status, probe_output = _container_health_details(inspect_result.stdout)
    if status in ("healthy", "running"):
        return True, ""
    if status:
        error = "The application container status is {0}.".format(status)
        if probe_output:
            error += " Last health-check output: {0}.".format(probe_output)
        return False, error
    return False, "The application container health check failed."


def _container_health_details(output):
    try:
        state = json.loads(output.strip())
    except (TypeError, ValueError):
        return output.strip(), ""

    if not isinstance(state, dict):
        return "", ""

    health = state.get("Health")
    if health is None:
        return state.get("Status", ""), ""
    if not isinstance(health, dict):
        return "", ""

    status = health.get("Status", "")
    probe_output = ""
    health_log = health.get("Log", [])
    if health_log:
        last_entry = health_log[-1]
        if isinstance(last_entry, dict):
            probe_output = str(last_entry.get("Output", "")).strip()
    probe_output = probe_output.replace("\r", " ").replace("\n", " ")
    if len(probe_output) > 200:
        probe_output = probe_output[:200] + "..."
    return status, probe_output


def _run_check(check, compose_executable, command_runner, config, logger):
    check_type = check.get("type", "command")
    if check_type == "command":
        return _check_command(check, command_runner, config, logger)
    if check_type == "url":
        return _check_url(check)
    if check_type == "container":
        return _check_container(config, compose_executable, command_runner)
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
        if check.get("type", "command") == "container" and retries > 1:
            logger.info("Waiting for {0}.".format(name))
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
