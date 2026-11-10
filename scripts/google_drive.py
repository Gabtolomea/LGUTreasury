import hashlib
from pathlib import Path

from google.auth.transport.requests import Request
from google.oauth2.credentials import Credentials
from google_auth_oauthlib.flow import InstalledAppFlow
from googleapiclient.discovery import build
from googleapiclient.http import MediaFileUpload


SECRETS_DIR = Path(
    r"C:\ProgramData\LGUTreasury\secrets"
)

CREDENTIALS_PATH = SECRETS_DIR / "credentials.json"
TOKEN_PATH = SECRETS_DIR / "token.json"

DRIVE_FOLDER_ID = "1vJ9V8tHYPqypU2oMnHi0ykgGQkfv93pG"

SCOPES = [
    "https://www.googleapis.com/auth/drive"
]


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
            "The saved Google token does not have the required Drive "
            "permission. Reauthorize the application before continuing."
        )

    if credentials and credentials.expired and credentials.refresh_token:
        credentials.refresh(Request())

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


def verify_drive_file(service, file_id, local_file):
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

    if DRIVE_FOLDER_ID not in remote_file.get("parents", []):
        raise RuntimeError(
            "The uploaded file is not in the configured backup folder."
        )

    return {
        "ok": True,
        "file_id": file_id,
        "name": remote_file.get("name", ""),
        "size": remote_size,
        "md5": remote_md5,
    }


def upload_backup(service, encrypted_file):
    encrypted_file = Path(encrypted_file).resolve()

    if not encrypted_file.is_file():
        raise FileNotFoundError(
            "Encrypted backup file was not found: {0}".format(
                encrypted_file
            )
        )

    metadata = {
        "name": encrypted_file.name,
        "parents": [DRIVE_FOLDER_ID],
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
    )

    return {
        "ok": True,
        "file_id": file_id,
        "name": response.get("name", encrypted_file.name),
        "verification": verification,
    }
