from pathlib import Path

from .commands import run_logged_command
from .config import compose_command, is_container_mysql, project_directory, project_path
from .health import run_health_checks
from .restore import (
    _restore_container_database,
    _restore_native_database,
    find_backup_sql_file,
)


def find_latest_backup(config):
    backup_root = project_path(config, config.get("backup_dir", "backups"))
    if not backup_root.is_dir():
        return None
    candidates = sorted(
        [
            entry
            for entry in backup_root.iterdir()
            if entry.is_dir() and entry.name.startswith("backup_")
        ],
        key=lambda p: p.name,
        reverse=True,
    )
    return candidates[0] if candidates else None


def _restore_image(config, backup_path, project_dir, command_runner, logger):
    image_name = config.get("app_image", "lgu-treasury-app:local")
    tag_file = backup_path / "rollback-tag.txt"
    image_file = backup_path / "app-image.tar"

    if tag_file.is_file():
        rollback_tag = tag_file.read_text(encoding="utf-8").strip()
        if rollback_tag:
            tag_command = ["docker", "tag", rollback_tag, image_name]
            tag_result = run_logged_command(
                command_runner,
                tag_command,
                project_dir,
                30,
                logger,
                "Previous application image tag restoration",
            )
            if tag_result.ok():
                logger.ok("The previous application image tag was restored.")
                return True, ""
            if not image_file.is_file():
                return False, "The previous application image tag could not be restored."

    if image_file.is_file():
        load_command = ["docker", "load", "--input", str(image_file)]
        load_result = run_logged_command(
            command_runner,
            load_command,
            project_dir,
            600,
            logger,
            "Previous application image restoration",
        )
        if not load_result.ok():
            return False, "The previous application image could not be restored."
        logger.ok("The previous application image was restored.")
        return True, ""

    return False, "Rollback cannot start because no application image backup exists."


def rollback(
    config,
    backup_result,
    compose_executable,
    command_runner,
    logger,
    restore_db=False,
):
    backup_dir = backup_result.get("backup_dir")
    if not backup_dir:
        return {
            "ok": False,
            "error": "Rollback cannot start because no backup directory was created.",
        }

    backup_path = Path(backup_dir)
    project_dir = str(project_directory(config))

    ok, image_error = _restore_image(
        config, backup_path, project_dir, command_runner, logger
    )
    if not ok:
        return {"ok": False, "error": image_error}

    sql_file = find_backup_sql_file(backup_path)
    if restore_db and sql_file:
        logger.info("Restoring database from backup: {0}.".format(sql_file))
        if is_container_mysql(config):
            db_error = _restore_container_database(
                config, sql_file, compose_executable, command_runner, logger
            )
        else:
            db_error = _restore_native_database(
                config, sql_file, command_runner, logger
            )
        if db_error:
            return {"ok": False, "error": db_error}
        logger.ok("The pre-deployment database backup was restored.")
    elif sql_file and not restore_db:
        logger.warning(
            "Rolling back the application image only. If database migrations ran, "
            "restore the database manually with: python deploy.py --rollback {0} --restore-db".format(
                backup_path
            )
        )

    restore_command = compose_command(
        config,
        compose_executable,
        [
            "up",
            "-d",
            "--no-build",
            "--wait",
            "--wait-timeout",
            "180",
            config.get("app_service", "app"),
        ],
    )
    restore_result = run_logged_command(
        command_runner,
        restore_command,
        project_dir,
        600,
        logger,
        "Application rollback",
    )
    if not restore_result.ok():
        return {"ok": False, "error": "The application rollback failed."}
    logger.ok("The previous application version is running.")

    logger.info("Running rollback verification health checks...")
    health_result = run_health_checks(
        config,
        compose_executable,
        command_runner,
        logger,
        summary_label="Rollback verification health checks",
    )
    if not health_result["ok"]:
        return {"ok": False, "error": "Rollback verification failed."}

    return {"ok": True, "error": ""}
