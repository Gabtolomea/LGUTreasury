from pathlib import Path

from .commands import run_logged_command
from .config import compose_command, project_directory
from .health import run_health_checks


def rollback(config, backup_result, compose_executable, command_runner, logger):
    backup_dir = backup_result.get("backup_dir")
    if not backup_dir:
        return {"ok": False, "error": "Rollback cannot start because no backup directory was created."}

    image_file = Path(backup_dir) / "app-image.tar"
    if not image_file.is_file():
        return {"ok": False, "error": "Rollback cannot start because no application image backup exists."}

    project_dir = str(project_directory(config))
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
        return {"ok": False, "error": "The previous application image could not be restored."}
    logger.ok("The previous application image was restored.")

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
