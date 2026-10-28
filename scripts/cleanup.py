import shutil

from .config import project_directory, project_path


def _inside(path, parent):
    try:
        path.resolve().relative_to(parent.resolve())
        return True
    except ValueError:
        return False


def clean(config, logger):
    project_dir = project_directory(config)
    backup_dir = project_path(config, config.get("backup_dir", "backups"))
    log_file = project_path(config, config.get("log_file", "logs/deployment.log"))
    log_dir = log_file.parent
    errors = []

    for value in config.get("temporary_paths", []):
        path = project_path(config, value)
        if not _inside(path, project_dir):
            errors.append("The cleanup path is outside the project: {0}.".format(value))
            continue
        if path == project_dir:
            errors.append("The project directory cannot be removed.")
            continue
        if _inside(path, backup_dir) or _inside(path, log_dir):
            logger.warning("The protected cleanup path was preserved: {0}.".format(value))
            continue
        if not path.exists():
            continue
        try:
            if path.is_symlink():
                path.unlink()
            elif path.is_dir():
                shutil.rmtree(path)
            else:
                path.unlink()
            logger.ok("The temporary resource was removed: {0}.".format(value))
        except OSError as error:
            errors.append("The temporary resource could not be removed: {0}.".format(error))

    if errors:
        for error in errors:
            logger.fail(error)
        return {"ok": False, "errors": errors}
    logger.success("Safe cleanup completed successfully.")
    return {"ok": True, "errors": []}
