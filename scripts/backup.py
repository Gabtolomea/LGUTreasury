
import csv
import io
import re
import shutil
import tempfile
from datetime import datetime
from pathlib import Path

from .commands import run_logged_command
from .config import (
    compose_command,
    native_database_config,
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


def _option_value(value):
    value = str(value)

    if any(char in value for char in ("\r", "\n", "\0")):
        raise ValueError("A MySQL option contains an unsupported character.")

    value = value.replace("\\", "\\\\")
    value = value.replace('"', '\\"')
    return '"' + value + '"'


def _current_windows_sid(command_runner, project_dir, logger):
    result = run_logged_command(
        command_runner,
        ["whoami", "/user", "/fo", "csv", "/nh"],
        project_dir,
        30,
        logger,
        "Windows deployment identity lookup",
    )

    if not result.ok():
        return ""

    try:
        rows = list(csv.reader(io.StringIO(result.stdout)))
        sid = rows[0][1].strip() if rows and len(rows[0]) > 1 else ""
    except (csv.Error, IndexError):
        return ""

    if not re.fullmatch(r"S-1-(?:\d+-)+\d+", sid):
        return ""

    return sid


def _create_private_credentials(config, command_runner, logger, database):
    project_dir = str(project_directory(config))
    temp_dir = Path(tempfile.mkdtemp(prefix="lgu-mysqldump-"))

    try:
        sid = _current_windows_sid(command_runner, project_dir, logger)

        if not sid:
            raise RuntimeError("Could not identify the Windows account for backup protection.")

        acl_command = [
            "icacls",
            str(temp_dir),
            "/inheritance:r",
            "/grant:r",
            "*{0}:(OI)(CI)F".format(sid),
        ]

        acl_result = run_logged_command(
            command_runner,
            acl_command,
            project_dir,
            30,
            logger,
            "Temporary backup credentials protection",
        )

        if not acl_result.ok():
            raise RuntimeError("Could not restrict access to the temporary backup credentials.")

        credentials_file = temp_dir / "client.cnf"

        contents = "\n".join([
            "[client]",
            "host={0}".format(_option_value("127.0.0.1")),
            "port={0}".format(database["port"]),
            "user={0}".format(_option_value(database["user"])),
            "password={0}".format(_option_value(database["password"])),
            "ssl-mode=REQUIRED",
            "",
        ])

        credentials_file.write_text(contents, encoding="utf-8")

        return temp_dir, credentials_file

    except Exception:
        shutil.rmtree(temp_dir, ignore_errors=True)
        raise


def _mysqldump_executable(config):
    configured = config.get("database", {}).get("mysqldump_executable")

    if configured:
        path = Path(configured)
        if path.is_file():
            return str(path)

    located = shutil.which("mysqldump.exe") or shutil.which("mysqldump")

    if located:
        return located

    default_path = Path(
        r"C:\Program Files\MySQL\MySQL Server 8.0\bin\mysqldump.exe"
    )

    if default_path.is_file():
        return str(default_path)

    return ""


def _backup_database(config, compose_executable, command_runner, logger, backup_dir):
    try:
        database = native_database_config(config)
    except (ValueError, OSError) as error:
        return "The native MySQL connection settings could not be read: {0}.".format(error)

    if not database:
        return "NATIVE_MYSQL_CONNECTION is missing."

    if not database.get("user") or not database.get("password"):
        return "The native MySQL username or password is missing."

    if not valid_database_name(database.get("database", "")):
        return "The native MySQL database name is invalid."

    if database.get("ssl_mode") not in ("required", "verifyca", "verifyfull"):
        return "The native MySQL connection must require TLS."

    executable = _mysqldump_executable(config)

    if not executable:
        return "Windows mysqldump.exe could not be located."

    backup_file = backup_dir / config.get("database", {}).get(
        "backup_file", "database.sql"
    )

    if not _inside(backup_file, backup_dir):
        return "The database backup file must stay inside the backup directory."

    temp_dir = None

    try:
        temp_dir, credentials_file = _create_private_credentials(
            config,
            command_runner,
            logger,
            database,
        )

        command = [
            executable,
            "--defaults-extra-file={0}".format(credentials_file),
            "--single-transaction",
            "--routines",
            "--events",
            "--triggers",
            "--no-tablespaces",
            "--set-gtid-purged=OFF",
            "--databases",
            database["database"],
        ]

        result = run_logged_command(
            command_runner,
            command,
            str(project_directory(config)),
            600,
            logger,
            "Native Windows MySQL database backup",
        )

        if not result.ok():
            return "The native MySQL database backup failed. Check the deployment log for details."

        if not result.stdout.strip():
            return "The MySQL backup returned an empty result."

        backup_file.write_text(result.stdout, encoding="utf-8")

        if not backup_file.exists() or backup_file.stat().st_size == 0:
            return "The database backup could not be verified."

        with backup_file.open("r", encoding="utf-8") as file:
            beginning = file.read(4096)

        if "MySQL dump" not in beginning and "MariaDB dump" not in beginning:
            return "The database backup does not contain a recognizable SQL dump header."

        logger.ok("The existing Windows MySQL database backup was saved and verified.")
        return ""

    except (OSError, RuntimeError, ValueError) as error:
        return "The native MySQL backup could not be completed: {0}.".format(error)

    finally:
        if temp_dir:
            shutil.rmtree(temp_dir, ignore_errors=True)


def _backup_app_image(config, compose_executable, command_runner, logger, backup_dir):
    project_dir = str(project_directory(config))
    service = config.get("app_service", "app")
    image_name = config.get("app_image", "lgu-treasury-app:local")

    lookup_command = compose_command(
        config, compose_executable, ["ps", "-aq", service]
    )

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
    container_id = output.splitlines()[0] if output else ""

    if not container_id:
        logger.info(
            "No previous application container was found; "
            "an image backup is not required."
        )
        return ""

    container_image_command = [
        "docker", "inspect", "--format", "{{.Config.Image}}", container_id
    ]

    container_image_result = run_logged_command(
        command_runner,
        container_image_command,
        project_dir,
        60,
        logger,
        "Previous application image lookup",
    )

    image_id_command = [
        "docker", "image", "inspect", "--format", "{{.Id}}", image_name
    ]

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

    if (
        not container_image_result.ok()
        or not image_id_result.ok()
        or not container_image
        or not stable_image_id
    ):
        return "The active application image could not be identified."

    if container_image != image_name:
        return "The active application image is not the stable rollback image."

    image_file = backup_dir / "app-image.tar"

    save_command = [
        "docker", "image", "save", "--output", str(image_file), image_name
    ]

    save_result = run_logged_command(
        command_runner,
        save_command,
        project_dir,
        600,
        logger,
        "Previous application image preservation",
    )

    if (
        not save_result.ok()
        or not image_file.exists()
        or image_file.stat().st_size == 0
    ):
        return "The previous application image could not be preserved."

    logger.ok("The previous application image was saved and verified.")
    return ""


def create_backups(config, compose_executable, command_runner, logger):
    logger.info("Creating backups.")

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

        return {
            "ok": False,
            "errors": errors,
            "backup_dir": str(backup_dir),
        }

    logger.success("Backups were created at {0}.".format(backup_dir))

    return {
        "ok": True,
        "errors": [],
        "backup_dir": str(backup_dir),
    }
