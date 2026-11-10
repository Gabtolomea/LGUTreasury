
from copy import deepcopy
from pathlib import Path

from deploy import CONFIG as DEPLOYMENT_CONFIG
from scripts.backup import create_backups
from scripts.backup_encryption import (
    DEFAULT_KEY_PATH,
    encrypt_backup_directory,
)
from scripts.google_drive import get_drive_service, upload_backup
from scripts.commands import run_command
from scripts.config import (
    environment_values,
    native_database_config,
    project_directory,
)
from scripts.logger import Logger
from scripts.preflight import run_preflight


def run_automated_backup(config=None, command_runner=None):
    backup_config = deepcopy(
        DEPLOYMENT_CONFIG if config is None else config
    )

    backup_config["log_file"] = "logs/automated_backup.log"

    command_runner = command_runner or run_command
    logger = None

    result = {
        "ok": False,
        "errors": [],
        "backup_dir": "",
        "encrypted_file": "",
        "sha256": "",
        "drive_upload_ok": False,
        "drive_file_id": "",
        "drive_error": "",
    }

    try:
        values = environment_values(backup_config)
        database = native_database_config(backup_config)

        secret_values = [
            database.get("password", ""),
            values.get("MYSQL_PASSWORD", ""),
            values.get("MYSQL_ROOT_PASSWORD", ""),
            values.get("HTTPS_CERT_PASSWORD", ""),
            values.get("INITIAL_ADMIN_PASSWORD", ""),
        ]

        log_path = (
            project_directory(backup_config)
            / backup_config["log_file"]
        )

        logger = Logger(
            log_path,
            secret_values=secret_values,
        )

        logger.info("Starting the automated backup process.")

        preflight = run_preflight(
            backup_config,
            command_runner,
            logger,
        )

        if not preflight["ok"]:
            result["errors"].extend(preflight["errors"])
            logger.fail("Automated backup stopped because preflight checks failed.")
            return result

        compose_executable = preflight["compose_command"]

        backup_result = create_backups(
            backup_config,
            compose_executable,
            command_runner,
            logger,
        )

        result["backup_dir"] = backup_result.get("backup_dir", "")

        if not backup_result["ok"]:
            result["errors"].extend(backup_result["errors"])
            logger.fail("Automated backup stopped because local backup creation failed.")
            return result

        logger.info("Encrypting the completed local backup.")

        encryption_config = backup_config.get("backup_encryption", {})
        key_path = Path(
            encryption_config.get("key_path", DEFAULT_KEY_PATH)
        )

        encryption_result = encrypt_backup_directory(
            Path(result["backup_dir"]),
            key_path=key_path,
        )

        if not encryption_result["ok"]:
            result["errors"].extend(encryption_result["errors"])
            logger.fail("The local backup was created, but encryption failed.")
            return result

        result["encrypted_file"] = str(
            encryption_result["encrypted_file"]
        )
        result["sha256"] = encryption_result["sha256"]

        # Local backup and encryption have succeeded.
        # Google Drive is an additional backup destination.
        result["ok"] = True

        logger.info("Uploading encrypted backup to Google Drive.")

        try:
            drive_service = get_drive_service()

            drive_result = upload_backup(
                drive_service,
                result["encrypted_file"],
            )

            result["drive_upload_ok"] = True
            result["drive_file_id"] = drive_result["file_id"]

            logger.success("Google Drive upload and verification completed.")
            logger.ok(
                "Google Drive file ID: {0}".format(
                    result["drive_file_id"]
                )
            )

        except Exception as error:
            result["drive_error"] = type(error).__name__

            logger.warning(
                "Google Drive upload failed. The local encrypted backup "
                "is safe. Error type: {0}".format(
                    result["drive_error"]
                )
            )

        logger.success("Automated backup completed successfully.")
        logger.ok(
            "Encrypted backup saved at: {0}".format(
                result["encrypted_file"]
            )
        )
        logger.ok(
            "Encrypted backup SHA-256: {0}".format(
                result["sha256"]
            )
        )

        return result

    except Exception as error:
        result["errors"].append(
            "Automated backup encountered an unexpected error: {0}".format(
                error
            )
        )

        if logger:
            logger.fail("Automated backup encountered an unexpected error.")
        else:
            print("Automated backup could not initialize.")

        return result

    finally:
        if logger:
            logger.close()


if __name__ == "__main__":
    backup_result = run_automated_backup()

    if backup_result["ok"]:
        print("\nAutomated backup completed successfully.")
        print("Local backup: {0}".format(backup_result["backup_dir"]))
        print("Encrypted file: {0}".format(backup_result["encrypted_file"]))
        print("SHA-256: {0}".format(backup_result["sha256"]))

        if backup_result["drive_upload_ok"]:
            print("Google Drive: Upload and verification successful.")
            print("Drive file ID: {0}".format(
                backup_result["drive_file_id"]
            ))
        else:
            print("Google Drive: Upload unsuccessful.")
            print("Local encrypted backup is still safe.")
            print("Drive error: {0}".format(
                backup_result["drive_error"]
            ))
    else:
        print("\nAutomated backup failed.")

        for error in backup_result["errors"]:
            print("- {0}".format(error))

        raise SystemExit(1)
