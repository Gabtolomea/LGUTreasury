import argparse
import os
import shutil
import sys
from pathlib import Path

from .backup import _create_private_credentials, _inside
from .commands import find_compose_command, run_command, run_logged_command
from .config import (
    compose_command,
    container_database_config,
    database_name,
    environment_values,
    is_container_mysql,
    native_database_config,
    project_directory,
    project_path,
    valid_database_name,
)
from .logger import Logger


def _is_windows():
    return os.name == "nt"


def _mysql_client_executable(config):
    configured = config.get("database", {}).get("mysql_executable")
    if configured:
        path = Path(configured)
        if path.is_file():
            return str(path)

    located = shutil.which("mysql.exe") or shutil.which("mysql")
    if located:
        return located

    if _is_windows():
        default_path = Path(
            r"C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe"
        )
        if default_path.is_file():
            return str(default_path)
    else:
        for candidate in ("/usr/bin/mysql", "/usr/local/bin/mysql"):
            if Path(candidate).is_file():
                return candidate

    return ""


def find_backup_sql_file(source_path):
    path = Path(source_path)
    if path.is_file() and path.suffix.lower() == ".sql":
        return path

    if path.is_dir():
        database_sql = path / "database.sql"
        if database_sql.is_file():
            return database_sql
        sql_files = sorted(path.glob("*.sql"))
        if sql_files:
            return sql_files[0]

    return None


def _restore_container_database(
    config, sql_file, compose_executable, command_runner, logger
):
    service = config.get("db_service", "db")
    project_dir = str(project_directory(config))

    lookup_command = compose_command(
        config, compose_executable, ["ps", "-q", service]
    )
    lookup_result = run_logged_command(
        command_runner,
        lookup_command,
        project_dir,
        60,
        logger,
        "Database container lookup",
    )
    if not lookup_result.ok():
        return "The database container could not be located."

    container_id = (
        lookup_result.stdout.strip().splitlines()[0]
        if lookup_result.stdout.strip()
        else ""
    )
    if not container_id:
        logger.info("Starting the database container for restore.")
        start_command = compose_command(
            config, compose_executable, ["up", "-d", service]
        )
        start_result = run_logged_command(
            command_runner,
            start_command,
            project_dir,
            180,
            logger,
            "Database container startup",
        )
        if not start_result.ok():
            return "The database container could not be started for restoration."

    db_config = container_database_config(config)
    db_name = database_name(config)
    if not valid_database_name(db_name):
        return "The container MySQL database name is invalid."

    password = db_config.get("root_password") or db_config.get("password")
    if not password:
        return "Container MySQL password is required for database restore."

    user = (
        "root"
        if db_config.get("root_password")
        else (db_config.get("user") or "root")
    )

    try:
        sql_content = sql_file.read_text(encoding="utf-8")
    except OSError as error:
        return "The database backup file could not be read: {0}.".format(error)

    restore_args = [
        "exec",
        "-T",
        "-e",
        "MYSQL_PWD={0}".format(password),
        service,
        "mysql",
        "-u",
        user,
        db_name,
    ]
    command = compose_command(config, compose_executable, restore_args)

    result = run_logged_command(
        command_runner,
        command,
        project_dir,
        600,
        logger,
        "Containerized MySQL database restoration",
        input=sql_content,
    )

    if not result.ok():
        return (
            "The containerized MySQL database restore failed. "
            "Check the logs for details."
        )

    logger.ok("The containerized MySQL database was restored successfully.")
    return ""


def _restore_native_database(config, sql_file, command_runner, logger):
    try:
        database = native_database_config(config)
    except (ValueError, OSError) as error:
        return (
            "The native MySQL connection settings could not be read: {0}.".format(
                error
            )
        )

    if not database:
        return "NATIVE_MYSQL_CONNECTION is missing."

    if not database.get("user") or not database.get("password"):
        return "The native MySQL username or password is missing."

    if not valid_database_name(database.get("database", "")):
        return "The native MySQL database name is invalid."

    executable = _mysql_client_executable(config)
    if not executable:
        return "mysql client executable could not be located."

    try:
        sql_content = sql_file.read_text(encoding="utf-8")
    except OSError as error:
        return "The database backup file could not be read: {0}.".format(error)

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
            database["database"],
        ]

        result = run_logged_command(
            command_runner,
            command,
            str(project_directory(config)),
            600,
            logger,
            "Native MySQL database restoration",
            input=sql_content,
        )

        if not result.ok():
            return (
                "The native MySQL database restore failed. "
                "Check the logs for details."
            )

        logger.ok("The native MySQL database was restored successfully.")
        return ""

    except (OSError, RuntimeError, ValueError) as error:
        return "The native MySQL restore could not be completed: {0}.".format(
            error
        )

    finally:
        if temp_dir:
            shutil.rmtree(temp_dir, ignore_errors=True)


def restore_database(
    config,
    backup_path,
    compose_executable=None,
    command_runner=None,
    logger=None,
):
    command_runner = command_runner or run_command
    close_logger = False

    if logger is None:
        log_path = project_directory(config) / "logs" / "restore.log"
        values = environment_values(config)
        secret_values = [
            values.get("MYSQL_PASSWORD", ""),
            values.get("MYSQL_ROOT_PASSWORD", ""),
            values.get("HTTPS_CERT_PASSWORD", ""),
        ]
        logger = Logger(log_path, secret_values=secret_values)
        close_logger = True

    try:
        sql_file = find_backup_sql_file(backup_path)
        if not sql_file or not sql_file.is_file():
            error = "No database SQL backup file found at: {0}.".format(
                backup_path
            )
            logger.fail(error)
            return {"ok": False, "error": error}

        try:
            with sql_file.open("r", encoding="utf-8") as file:
                header = file.read(4096)
        except OSError as error:
            error_msg = "Could not read backup file header: {0}.".format(error)
            logger.fail(error_msg)
            return {"ok": False, "error": error_msg}

        if "MySQL dump" not in header and "MariaDB dump" not in header:
            error = (
                "The backup file does not contain a recognizable SQL dump header."
            )
            logger.fail(error)
            return {"ok": False, "error": error}

        if is_container_mysql(config):
            if not compose_executable:
                compose_executable = find_compose_command(
                    command_runner, str(project_directory(config))
                )
                if not compose_executable:
                    error = "Docker Compose executable could not be located."
                    logger.fail(error)
                    return {"ok": False, "error": error}

            restore_error = _restore_container_database(
                config,
                sql_file,
                compose_executable,
                command_runner,
                logger,
            )
        else:
            restore_error = _restore_native_database(
                config,
                sql_file,
                command_runner,
                logger,
            )

        if restore_error:
            logger.fail(restore_error)
            return {"ok": False, "error": restore_error}

        logger.success("Database restoration completed successfully.")
        return {"ok": True, "error": ""}

    finally:
        if close_logger:
            logger.close()


def main():
    parser = argparse.ArgumentParser(
        description="Restore a MySQL database from a backup directory or SQL file."
    )
    parser.add_argument(
        "backup_path",
        nargs="?",
        default=None,
        help="Path to a backup directory or database.sql file. Defaults to latest in backups/.",
    )
    args = parser.parse_args()

    project_dir = Path(__file__).resolve().parent.parent
    target_path = args.backup_path

    if not target_path:
        backups_dir = project_dir / "backups"
        if backups_dir.is_dir():
            backup_folders = sorted(
                [p for p in backups_dir.iterdir() if p.is_dir()],
                key=lambda p: p.stat().st_mtime,
                reverse=True,
            )
            if backup_folders:
                target_path = str(backup_folders[0])

    if not target_path:
        print("[FAIL] No backup path specified and no backups found in backups/.")
        sys.exit(1)

    from deploy import CONFIG
    result = restore_database(CONFIG, target_path)
    if result["ok"]:
        print("[PASS] Database restored successfully.")
        sys.exit(0)
    else:
        print("[FAIL] {0}".format(result["error"]))
        sys.exit(1)


if __name__ == "__main__":
    main()
