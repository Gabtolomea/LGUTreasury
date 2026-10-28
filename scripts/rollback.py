from pathlib import Path

from .commands import run_logged_command
from .config import compose_command, project_directory
from .health import run_health_checks


def rollback(config, backup_result, compose_executable, command_runner, logger):
    logger.info("Rolling back the application.")
    backup_dir = backup_result.get("backup_dir")
    if not backup_dir:
        logger.fail("Rollback cannot start because no backup directory was created.")
        return {"ok": False, "error": "No backup directory was created."}

    image_file = Path(backup_dir) / "app-image.tar"
    if not image_file.is_file():
        logger.fail("Rollback cannot start because no application image backup exists.")
        return {"ok": False, "error": "No application image backup exists."}

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
        logger.fail("The previous application image could not be restored.")
        return {"ok": False, "error": "The previous application image could not be restored."}

    restore_command = compose_command(
        config,
        compose_executable,
        ["up", "-d", "--no-build", config.get("app_service", "app")],
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
        logger.fail("The application rollback failed.")
        return {"ok": False, "error": "The application rollback failed."}

    health_result = run_health_checks(
        config,
        compose_executable,
        command_runner,
        logger,
    )
    if not health_result["ok"]:
        logger.fail("Rollback verification failed.")
        return {"ok": False, "error": "Rollback verification failed."}

    logger.success("Rollback completed and was verified successfully.")
    return {"ok": True, "error": ""}
