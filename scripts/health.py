import json
import ssl
import time
from urllib.error import HTTPError, URLError
from urllib.request import urlopen

from .commands import describe_command, execute_command
from .config import compose_command, project_directory


def _check_command(check, command_runner, config, logger, timeout=None):
    command = check.get("command")
    if not command:
        return False, "The health-check command is missing."
    logger.detail("Running the command `{0}`.".format(describe_command(command)))
    result = execute_command(
        command_runner,
        command,
        str(project_directory(config)),
        timeout or check.get("timeout", 10),
    )
    if result.ok():
        return True, ""
    if result.stderr.strip():
        return False, result.stderr.strip()
    return False, "The health-check command failed (exit code {0}).".format(result.returncode)


def _check_url(check, timeout=None):
    url = check.get("url")
    if not url:
        return False, "The health-check URL is missing."

    request_timeout = timeout or check.get("timeout", 5)
    context = None
    ca_file = check.get("ca_file")
    if url.lower().startswith("https://"):
        try:
            context = ssl.create_default_context(cafile=ca_file) if ca_file else ssl.create_default_context()
        except (OSError, ssl.SSLError, ValueError) as error:
            return False, "TLS trust configuration is invalid: {0}".format(error)

    try:
        with urlopen(url, timeout=request_timeout, context=context) as response:
            expected_status = check.get("expected_status", 200)
            if response.status == expected_status:
                return True, ""
            return False, "Expected HTTP {0}, received HTTP {1}.".format(
                expected_status, response.status
            )
    except HTTPError as error:
        return False, "The health endpoint returned HTTP {0}.".format(error.code)
    except (OSError, URLError, ValueError, TimeoutError) as error:
        return False, "Health endpoint request failed: {0}".format(error)


def _check_container(config, compose_executable, command_runner, timeout=10, service=None):
    service = service or config.get("app_service", "app")
    service_name = "application" if service == config.get("app_service", "app") else service
    project_dir = str(project_directory(config))
    lookup_command = compose_command(config, compose_executable, ["ps", "-q", service])
    lookup_result = command_runner(lookup_command, project_dir, timeout)
    if not lookup_result.ok():
        if lookup_result.stderr.strip():
            return False, lookup_result.stderr.strip()
        return False, "The {0} container could not be located.".format(service_name)

    container_id = lookup_result.stdout.strip().splitlines()[0] if lookup_result.stdout.strip() else ""
    if not container_id:
        return False, "The {0} container is not running.".format(service_name)

    inspect_command = ["docker", "inspect", "--format", "{{json .State}}", container_id]
    inspect_result = command_runner(inspect_command, project_dir, timeout)
    if not inspect_result.ok():
        if inspect_result.stderr.strip():
            return False, inspect_result.stderr.strip()
        return False, "The {0} container could not be inspected.".format(service_name)

    status, probe_output = _container_health_details(inspect_result.stdout)
    if status == "healthy":
        return True, ""
    if status == "running":
        return False, "The container is running but has no Docker HEALTHCHECK configured."
    if status:
        error = "The {0} container health status is {1}.".format(service_name, status)
        if probe_output:
            error += " Last probe: {0}".format(probe_output)
        return False, error
    return False, "Docker did not return a valid container health status."


def _container_health_details(output):
    try:
        state = json.loads(output.strip())
    except (TypeError, ValueError):
        return "", ""

    if not isinstance(state, dict):
        return "", ""

    health = state.get("Health")
    if not isinstance(health, dict):
        return state.get("Status", ""), ""

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


def _run_check(check, compose_executable, command_runner, config, logger, timeout):
    check_type = check.get("type", "command")
    if check_type == "command":
        return _check_command(check, command_runner, config, logger, timeout)
    if check_type == "url":
        return _check_url(check, timeout)
    if check_type == "container":
        return _check_container(
            config,
            compose_executable,
            command_runner,
            timeout,
            service=check.get("service"),
        )
    return False, "The health-check type is not supported."


def run_health_checks(
    config,
    compose_executable,
    command_runner,
    logger,
    summary_label="All health checks",
):
    checks = config.get("health_checks", [])
    if not checks:
        message = "No health checks were configured."
        logger.warning(message)
        return {"ok": False, "errors": [message], "checks": [], "duration": 0.0}

    overall_timeout = max(1, float(config.get("health_check_timeout", 120)))
    started_at = time.monotonic()
    deadline = started_at + overall_timeout
    logger.info("Health-check deadline: {0:g}s.".format(overall_timeout))

    errors = []
    results = []
    for check in checks:
        name = check.get("name", "Application health")
        service = check.get("service", "")
        retries = check.get("retries", 3)
        interval = check.get("interval", 2)
        if not isinstance(retries, int) or retries < 1 or not isinstance(interval, (int, float)) or interval < 0:
            error = "{0} has invalid retry settings.".format(name)
            logger.fail(error)
            errors.append(error)
            results.append(
                {
                    "name": name,
                    "service": service,
                    "status": "failed",
                    "attempts": 0,
                    "duration": 0.0,
                    "error": error,
                }
            )
            continue

        check_started = time.monotonic()
        attempts = 0
        passed = False
        last_error = "The health-check deadline was exceeded."
        spinner_cm = (
            logger.spinner("Checking {0}...".format(name))
            if hasattr(logger, "spinner")
            else None
        )
        if spinner_cm is not None:
            spinner_cm.__enter__()
        try:
            for attempt in range(1, retries + 1):
                remaining = deadline - time.monotonic()
                if remaining <= 0:
                    break
                attempts = attempt
                attempt_timeout = min(float(check.get("timeout", 10)), remaining)
                passed, last_error = _run_check(
                    check, compose_executable, command_runner, config, logger, attempt_timeout
                )
                if passed:
                    break
                remaining = deadline - time.monotonic()
                if attempt < retries and remaining > 0:
                    time.sleep(min(float(interval), remaining))
        finally:
            if spinner_cm is not None:
                spinner_cm.__exit__(None, None, None)

        duration = round(time.monotonic() - check_started, 2)
        result = {
            "name": name,
            "service": service,
            "status": "passed" if passed else "failed",
            "attempts": attempts,
            "duration": duration,
            "error": "" if passed else last_error,
        }
        results.append(result)
        if passed:
            logger.ok("{0} ({1} attempt(s), {2:.2f}s)".format(name, attempts, duration))
        else:
            error = "{0} failed after {1} attempt(s): {2}".format(name, attempts, last_error)
            logger.fail(error)
            errors.append(error)

    total_duration = round(time.monotonic() - started_at, 2)
    if errors:
        logger.fail("{0} failed ({1:.2f}s).".format(summary_label, total_duration))
        return {"ok": False, "errors": errors, "checks": results, "duration": total_duration}

    logger.ok("{0} passed ({1:.2f}s).".format(summary_label, total_duration))
    return {"ok": True, "errors": [], "checks": results, "duration": total_duration}
