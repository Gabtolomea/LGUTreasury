import hashlib
import json
from pathlib import Path

from google.auth.exceptions import RefreshError
from google.auth.transport.requests import Request
from google.oauth2.credentials import Credentials
from google_auth_oauthlib.flow import InstalledAppFlow
from googleapiclient.discovery import build
from googleapiclient.http import MediaFileUpload


SECRETS_DIR = Path(r"C:\\ProgramData\\LGUTreasury\\secrets")
CREDENTIALS_PATH = SECRETS_DIR / "credentials.json"

# Keep the school account token separate from the previous personal-account token.
TOKEN_PATH = SECRETS_DIR / "school_token.json"
FOLDER_ID_PATH = SECRETS_DIR / "drive_folder.json"

DEFAULT_DRIVE_FOLDER_NAME = "LGUTreasury Backups"
DRIVE_FOLDER_MIME_TYPE = "application/vnd.google-apps.folder"

# This is intentionally the least-privilege scope configured in Google Cloud.
SCOPES = ["https://www.googleapis.com/auth/drive.file"]


def calculate_md5(file_path):
    digest = hashlib.md5()

    with Path(file_path).open("rb") as file:
        for chunk in iter(lambda: file.read(1024 * 1024), b""):
            digest.update(chunk)

    return digest.hexdigest()


def get_drive_service():
    if not CREDENTIALS_PATH.is_file():
        raise FileNotFoundError(
            "Google OAuth credentials were not found: {0}".format(
                CREDENTIALS_PATH
            )
        )

    credentials = None

    if TOKEN_PATH.is_file():
        credentials = Credentials.from_authorized_user_file(
            str(TOKEN_PATH),
            SCOPES,
        )

    if credentials and not credentials.has_scopes(SCOPES):
        raise RuntimeError(
            "The saved school Google token does not have the required "
            "drive.file permission. Reauthorize the school account."
        )

    if credentials and credentials.expired and credentials.refresh_token:
        try:
            credentials.refresh(Request())
        except RefreshError as error:
            raise RuntimeError(
                "Google Drive authorization must be renewed. The OAuth app "
                "is in External/Testing mode, where Drive refresh tokens "
                "generally expire after 7 days. Run the backup manually "
                "while signed in to Windows and complete Google sign-in. "
                "Original error: {0}".format(error)
            ) from error

    elif not credentials or not credentials.valid:
        flow = InstalledAppFlow.from_client_secrets_file(
            str(CREDENTIALS_PATH),
            SCOPES,
        )

        credentials = flow.run_local_server(
            host="localhost",
            port=0,
            open_browser=True,
            access_type="offline",
            prompt="consent",
        )

    SECRETS_DIR.mkdir(parents=True, exist_ok=True)
    TOKEN_PATH.write_text(
        credentials.to_json(),
        encoding="utf-8",
    )

    return build(
        "drive",
        "v3",
        credentials=credentials,
        cache_discovery=False,
    )


def _escape_drive_query_value(value):
    return str(value).replace("\\\\", "\\\\\\\\").replace("'", "\\\\'")


def get_or_create_drive_folder(service, folder_name=DEFAULT_DRIVE_FOLDER_NAME):
    """Return the app-managed Drive folder, creating it on first use.

    With the drive.file scope, the app can list files it created or files the
    user explicitly opened with the app. Creating the folder through this API
    ensures the app can access it on later runs.
    """
    if not folder_name or not folder_name.strip():
        raise ValueError("Google Drive backup folder name cannot be empty.")

    folder_name = folder_name.strip()
    SECRETS_DIR.mkdir(parents=True, exist_ok=True)

    if FOLDER_ID_PATH.is_file():
        try:
            cached = json.loads(FOLDER_ID_PATH.read_text(encoding="utf-8"))
            folder_id = cached.get("folder_id", "")
        except (OSError, ValueError, AttributeError) as error:
            raise RuntimeError(
                "The saved Google Drive folder configuration is invalid. "
                "Check {0}.".format(FOLDER_ID_PATH)
            ) from error

        if not folder_id:
            raise RuntimeError(
                "The saved Google Drive folder configuration has no folder ID."
            )

        folder = service.files().get(
            fileId=folder_id,
            fields="id,name,mimeType",
        ).execute()

        if (
            folder.get("mimeType") != DRIVE_FOLDER_MIME_TYPE
            or folder.get("name") != folder_name
        ):
            raise RuntimeError(
                "The saved Google Drive folder does not match the configured "
                "folder name. Review {0} before continuing.".format(
                    FOLDER_ID_PATH
                )
            )

        return folder_id

    escaped_name = _escape_drive_query_value(folder_name)
    response = service.files().list(
        q=(
            "name = '{0}' and mimeType = '{1}' and trashed = false".format(
                escaped_name,
                DRIVE_FOLDER_MIME_TYPE,
            )
        ),
        spaces="drive",
        pageSize=100,
        fields="files(id,name,mimeType)",
    ).execute()

    folders = response.get("files", [])

    if len(folders) > 1:
        raise RuntimeError(
            "More than one app-accessible Google Drive folder is named "
            "'{0}'. Rename or remove duplicates before running the backup."
            .format(folder_name)
        )

    if folders:
        folder_id = folders[0]["id"]
    else:
        created = service.files().create(
            body={
                "name": folder_name,
                "mimeType": DRIVE_FOLDER_MIME_TYPE,
            },
            fields="id,name",
        ).execute()
        folder_id = created["id"]

    FOLDER_ID_PATH.write_text(
        json.dumps({"folder_id": folder_id}, indent=2),
        encoding="utf-8",
    )

    return folder_id


def verify_drive_file(service, file_id, local_file, folder_id):
    local_file = Path(local_file)

    remote_file = service.files().get(
        fileId=file_id,
        fields="id,name,size,md5Checksum,parents",
    ).execute()

    local_size = local_file.stat().st_size
    local_md5 = calculate_md5(local_file)

    remote_size = int(remote_file.get("size", -1))
    remote_md5 = remote_file.get("md5Checksum", "")

    if remote_size != local_size:
        raise RuntimeError(
            "Google Drive file size does not match the local backup."
        )

    if remote_md5 != local_md5:
        raise RuntimeError(
            "Google Drive MD5 checksum does not match the local backup."
        )

    if folder_id not in remote_file.get("parents", []):
        raise RuntimeError(
            "The uploaded file is not in the configured school backup folder."
        )

    return {
        "ok": True,
        "file_id": file_id,
        "name": remote_file.get("name", ""),
        "size": remote_size,
        "md5": remote_md5,
    }


def upload_backup(
    service,
    encrypted_file,
    folder_name=DEFAULT_DRIVE_FOLDER_NAME,
):
    encrypted_file = Path(encrypted_file).resolve()

    if not encrypted_file.is_file():
        raise FileNotFoundError(
            "Encrypted backup file was not found: {0}".format(
                encrypted_file
            )
        )

    folder_id = get_or_create_drive_folder(service, folder_name)

    metadata = {
        "name": encrypted_file.name,
        "parents": [folder_id],
    }

    media = MediaFileUpload(
        str(encrypted_file),
        mimetype="application/octet-stream",
        chunksize=8 * 1024 * 1024,
        resumable=True,
    )

    request = service.files().create(
        body=metadata,
        media_body=media,
        fields="id,name",
    )

    response = None

    while response is None:
        status, response = request.next_chunk()

        if status:
            print(
                "Google Drive upload progress: {0:.0f}%".format(
                    status.progress() * 100
                )
            )

    file_id = response["id"]

    verification = verify_drive_file(
        service,
        file_id,
        encrypted_file,
        folder_id,
    )

    return {
        "ok": True,
        "file_id": file_id,
        "folder_id": folder_id,
        "name": response.get("name", encrypted_file.name),
        "verification": verification,
    }
