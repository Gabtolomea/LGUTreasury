import shutil
import time
from datetime import datetime
from pathlib import Path

from .commands import run_logged_command
from .config import (
    compose_command,
    database_name,
    environment_values,
    project_directory,
    project_path,
    valid_database_name,
)


def _inside(path, parent):
    try:
        path.resolve().relative_to(parent.resolve())
        return True
    except ValueError:
        return False


def _new_backup_directory(config):
    backup_root = project_path(config, config.get("backup_dir", "backups"))
    backup_root.mkdir(parents=True, exist_ok=True)
    stamp = datetime.now().strftime("backup_%Y%m%d_%H%M%S_%f")
    backup_dir = backup_root / stamp
    backup_dir.mkdir()
    return backup_dir


def _copy_path(source, project_dir, backup_dir):
    relative_path = source.relative_to(project_dir)
    destination = backup_dir / relative_path
    destination.parent.mkdir(parents=True, exist_ok=True)
    if source.is_dir():
        shutil.copytree(source, destination)
    else:
        shutil.copy2(source, destination)
    return destination


def _wait_for_database(config, compose_executable, command_runner, logger):
    database = config.get("database", {})
    service = database.get("service", "db")
    project_dir = str(project_directory(config))
    start_command = compose_command(config, compose_executable, ["up", "-d", service])
    start_result = run_logged_command(
        command_runner,
        start_command,
        project_dir,
        180,
        logger,
        "Database startup",
    )
    if not start_result.ok():
        return {"ok": False, "error": "Database startup failed."}

    retries = database.get("readiness_retries", 30)
    interval = database.get("readiness_interval", 2)
    if retries < 1 or interval < 0:
        return {"ok": False, "error": "The database readiness settings are invalid."}

    logger.info("Waiting for the database to become ready.")
    last_error = "The database did not become ready."
    container_id = ""
    for attempt in range(retries):
        lookup_command = compose_command(config, compose_executable, ["ps", "-q", service])
        lookup_result = run_logged_command(
            command_runner,
            lookup_command,
            project_dir,
            60,
            logger,
            "Database container lookup",
        )
        if lookup_result.ok():
            output = lookup_result.stdout.strip()
            if output:
                container_id = output.splitlines()[0]
            else:
                container_id = ""

        if container_id:
            inspect_command = [
                "docker",
                "inspect",
                "--format",
                "{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}",
                container_id,
            ]
            inspect_result = run_logged_command(
                command_runner,
                inspect_command,
                project_dir,
                60,
                logger,
                "Database readiness check",
            )
            status = inspect_result.stdout.strip()
            if inspect_result.ok() and status == "healthy":
                logger.ok("The database is ready.")
                return {"ok": True, "error": ""}
            if inspect_result.stderr.strip():
                last_error = inspect_result.stderr.strip()
            elif status:
                last_error = "The database health status is {0}.".format(status)

        if attempt + 1 < retries:
            time.sleep(interval)

    logger.fail("The database did not become ready.")
    return {"ok": False, "error": last_error}


def _backup_database(config, compose_executable, command_runner, logger, backup_dir):
    values = environment_values(config)
    root_password = values.get("MYSQL_ROOT_PASSWORD", "")
    if not root_password:
        return "The MySQL root password is missing."
    configured_database_name = database_name(config)
    if not valid_database_name(configured_database_name):
        return "MYSQL_DATABASE contains unsupported characters."

    backup_file = backup_dir / config.get("database", {}).get("backup_file", "database.sql")
    if not _inside(backup_file, backup_dir):
        return "The database backup file must stay inside the backup directory."

    command = compose_command(
        config,
        compose_executable,
        [
            "exec",
            "-T",
            "-e",
            "MYSQL_PWD=" + root_password,
            config.get("database", {}).get("service", "db"),
            "mysqldump",
            "--user=root",
            "--single-transaction",
            "--routines",
            "--events",
            "--databases",
            configured_database_name,
        ],
    )
    result = run_logged_command(
        command_runner,
        command,
        str(project_directory(config)),
        300,
        logger,
        "MySQL database backup",
    )
    if not result.ok():
        return "The MySQL database backup failed."

    try:
        backup_file.write_text(result.stdout, encoding="utf-8")
    except OSError as error:
        return "The database backup could not be saved: {0}.".format(error)

    if not backup_file.exists() or backup_file.stat().st_size == 0:
        return "The database backup could not be verified."
    logger.ok("The database backup was saved and verified.")
    return ""


def _backup_app_image(config, compose_executable, command_runner, logger, backup_dir):
    project_dir = str(project_directory(config))
    service = config.get("app_service", "app")
    image_name = config.get("app_image", "lgu-treasury-app:local")
    lookup_command = compose_command(config, compose_executable, ["ps", "-aq", service])
    lookup_result = run_logged_command(
        command_runner,
        lookup_command,
        project_dir,
        60,
        logger,
        "Application container lookup",
    )
    if not lookup_result.ok():
        return "The application container could not be located."

    output = lookup_result.stdout.strip()
    if output:
        container_id = output.splitlines()[0]
    else:
        container_id = ""
    if not container_id:
        logger.info("No previous application container was found; an image backup is not required.")
        return ""

    container_image_command = [
        "docker",
        "inspect",
        "--format",
        "{{.Config.Image}}",
        container_id,
    ]
    container_image_result = run_logged_command(
        command_runner,
        container_image_command,
        project_dir,
        60,
        logger,
        "Previous application image lookup",
    )
    image_id_command = ["docker", "image", "inspect", "--format", "{{.Id}}", image_name]
    image_id_result = run_logged_command(
        command_runner,
        image_id_command,
        project_dir,
        60,
        logger,
        "Stable application image lookup",
    )
    container_image = container_image_result.stdout.strip()
    stable_image_id = image_id_result.stdout.strip()
    if not container_image_result.ok() or not image_id_result.ok() or not container_image or not stable_image_id:
        return "The active application image could not be identified."
    if container_image != image_name:
        return "The active application image is not the stable rollback image."

    image_file = backup_dir / "app-image.tar"
    save_command = ["docker", "image", "save", "--output", str(image_file), image_name]
    save_result = run_logged_command(
        command_runner,
        save_command,
        project_dir,
        600,
        logger,
        "Previous application image preservation",
    )
    if not save_result.ok() or not image_file.exists() or image_file.stat().st_size == 0:
        return "The previous application image could not be preserved."
    logger.ok("The previous application image was saved and verified.")
    return ""


def create_backups(config, compose_executable, command_runner, logger):
    logger.info("Creating backups.")
    readiness = _wait_for_database(config, compose_executable, command_runner, logger)
    if not readiness["ok"]:
        return {"ok": False, "errors": [readiness["error"]], "backup_dir": None}

    backup_dir = _new_backup_directory(config)
    project_dir = project_directory(config)
    errors = []

    for value in config.get("backup_paths", []):
        source = project_path(config, value)
        if not _inside(source, project_dir):
            errors.append("The backup path is outside the project: {0}.".format(value))
            continue
        if not source.exists():
            errors.append("The backup path was not found: {0}.".format(value))
            continue
        try:
            destination = _copy_path(source, project_dir, backup_dir)
            if destination.exists():
                logger.ok("The backup was saved for {0}.".format(value))
            else:
                errors.append("The backup could not be verified: {0}.".format(value))
        except OSError as error:
            errors.append("The backup failed for {0}: {1}.".format(value, error))

    database_error = _backup_database(
        config,
        compose_executable,
        command_runner,
        logger,
        backup_dir,
    )
    if database_error:
        errors.append(database_error)

    image_error = _backup_app_image(
        config,
        compose_executable,
        command_runner,
        logger,
        backup_dir,
    )
    if image_error:
        errors.append(image_error)

    if errors:
        for error in errors:
            logger.fail(error)
        return {"ok": False, "errors": errors, "backup_dir": str(backup_dir)}

    logger.success("Backups were created at {0}.".format(backup_dir))
    return {"ok": True, "errors": [], "backup_dir": str(backup_dir)}
