import os
import shutil

from .commands import find_compose_command, run_logged_command
from .config import (
    environment_values,
    find_placeholder_env_values,
    is_container_mysql,
    native_database_config,
    project_directory,
    project_path,
    valid_database_name,
)


def _locate_mysqldump(config):
    configured = config.get("database", {}).get("mysqldump_executable")
    if configured and os.path.isfile(configured):
        return configured
    located = shutil.which("mysqldump.exe") or shutil.which("mysqldump")
    if located:
        return located
    if os.name == "nt":
        default_path = r"C:\Program Files\MySQL\MySQL Server 8.0\bin\mysqldump.exe"
        if os.path.isfile(default_path):
            return default_path
    else:
        for candidate in ("/usr/bin/mysqldump", "/usr/local/bin/mysqldump"):
            if os.path.isfile(candidate):
                return candidate
    return ""


def run_preflight(config, command_runner, logger):
    errors = []
    project_dir = project_directory(config)

    if project_dir.is_dir():
        logger.ok("The project directory was found.")
    else:
        errors.append("The project directory was not found.")
        logger.fail("The project directory was not found.")

    for compose_file in config.get("compose_files", ["compose.native-mysql.yml"]):
        path = project_path(config, compose_file)
        if path.is_file():
            logger.ok("The Compose file was found.")
        else:
            errors.append("The Compose file was not found: {0}.".format(compose_file))
            logger.fail("The Compose file was not found: {0}.".format(compose_file))

    env_file = config.get("env_file")
    if env_file:
        path = project_path(config, env_file)
        if path.is_file():
            logger.ok("The environment file was found.")
        else:
            errors.append("The environment file was not found: {0}.".format(env_file))
            logger.fail("The environment file was not found: {0}.".format(env_file))

    values = environment_values(config)
    for name in config.get("required_env", []):
        if values.get(name):
            logger.ok("The required environment value {0} is available.".format(name))
        else:
            errors.append("The required environment value {0} is missing.".format(name))
            logger.fail("The required environment value {0} is missing.".format(name))

    placeholders = find_placeholder_env_values(values, config.get("required_env", []))
    for name in placeholders:
        message = "The environment value {0} still contains a template placeholder.".format(name)
        errors.append(message)
        logger.fail(message)

    if values.get("ENABLE_HTTPS", "false").strip().lower() == "true":
        cert_dir = project_path(config, values.get("HTTPS_CERT_DIR") or "./.aspnet/https")
        cert_file = cert_dir / "LGUTreasury.pfx"
        if cert_file.is_file():
            logger.ok("The HTTPS certificate file was found.")
        else:
            message = "ENABLE_HTTPS is true, but the certificate file was not found: {0}.".format(
                cert_file
            )
            errors.append(message)
            logger.fail(message)

    if not is_container_mysql(config) and values.get("NATIVE_MYSQL_CONNECTION"):
        try:
            db = native_database_config(config)
            if not db.get("user") or not db.get("password"):
                message = "The native MySQL username or password is missing in NATIVE_MYSQL_CONNECTION."
                errors.append(message)
                logger.fail(message)
            elif not valid_database_name(db.get("database", "")):
                message = "The native MySQL database name in NATIVE_MYSQL_CONNECTION is invalid."
                errors.append(message)
                logger.fail(message)
            elif (
                not config.get("allow_insecure_db_tls")
                and db.get("ssl_mode") not in ("required", "verifyca", "verifyfull")
            ):
                message = (
                    "The native MySQL connection must require TLS (SslMode=Required). "
                    "Pass --allow-insecure-db-tls only if MySQL TLS is unavailable."
                )
                errors.append(message)
                logger.fail(message)
            else:
                logger.ok("The native MySQL connection string is valid.")
        except ValueError as error:
            message = "NATIVE_MYSQL_CONNECTION is invalid: {0}".format(error)
            errors.append(message)
            logger.fail(message)

        if not config.get("skip_backup"):
            if _locate_mysqldump(config):
                logger.ok("The mysqldump executable is available.")
            else:
                message = "mysqldump executable could not be located for pre-deployment backup."
                errors.append(message)
                logger.fail(message)

    if os.access(str(project_dir), os.R_OK):
        logger.ok("The project directory is readable.")
    else:
        errors.append("The project directory is not readable.")
        logger.fail("The project directory is not readable.")

    if os.access(str(project_dir), os.W_OK):
        logger.ok("The project directory is writable.")
    else:
        errors.append("The project directory is not writable.")
        logger.fail("The project directory is not writable.")

    minimum_free_space = config.get("minimum_free_space_mb", 0)
    if minimum_free_space:
        usage = shutil.disk_usage(str(project_dir))
        available_mb = usage.free / (1024 * 1024)
        if available_mb >= minimum_free_space:
            logger.ok("The project drive has enough free space.")
        else:
            errors.append("The project drive does not have enough free space.")
            logger.fail("The project drive does not have enough free space.")

    compose_command = find_compose_command(command_runner, str(project_dir))
    if compose_command:
        logger.ok("Docker Compose is available.")
    else:
        errors.append("Docker Compose is not available.")
        logger.fail("Docker Compose is not available.")

    if compose_command:
        docker_command = ["docker", "info", "--format", "{{.ServerVersion}}"]
        daemon_result = run_logged_command(
            command_runner,
            docker_command,
            str(project_dir),
            30,
            logger,
            "The Docker daemon check",
        )
        if not daemon_result.ok():
            errors.append("The Docker daemon is not available.")
            logger.fail("The Docker daemon is not available.")
        else:
            logger.ok("The Docker daemon is responding.")

    if errors:
        return {"ok": False, "errors": errors, "compose_command": compose_command}

    return {"ok": True, "errors": [], "compose_command": compose_command}
