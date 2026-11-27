# School Google Drive Backup Setup

This guide configures the existing Python backup runner to upload the encrypted backup to the dedicated school Gmail account.

## Security and operating model

- LGUTreasury remains a LAN-only application. Google Drive is only an off-site destination for encrypted backup archives.
- The script uploads the encrypted `.zip.enc` file, not the unencrypted SQL or recovery files.
- The local encryption key is never uploaded.
- OAuth credentials, OAuth tokens, and the cached Drive folder ID belong in the protected Windows secrets directory. They must never be committed to Git.
- The OAuth client is configured as External / Testing. Google generally expires refresh tokens for Testing apps after seven days when Drive scopes are requested. Expect to reauthorize periodically. Until reauthorized, the local encrypted backup is still created, but the backup process exits with a failure status because the cloud copy was not confirmed.

## Windows server setup

Run these steps while signed in to the same Windows account that will run the scheduled backup task.

1. Create the secrets directory if it does not exist:

   ```powershell
   New-Item -ItemType Directory -Force 'C:\ProgramData\LGUTreasury\secrets' | Out-Null
   ```

2. Copy the downloaded OAuth client JSON from the school project's **LGUTreasury Backup Desktop** client into this directory and name it exactly:

   ```text
   C:\ProgramData\LGUTreasury\secrets\credentials.json
   ```

   Do not paste the JSON contents into chat, email, source control, or the project folder.

3. Confirm that the server has the Python dependencies installed from `requirements.txt`.

4. In the project's ignored `.env` file, optionally set the Drive folder name:

   ```env
   DRIVE_BACKUP_FOLDER_NAME=LGUTreasury Backups
   ```

   The script creates this folder in the currently authorized school Gmail's Drive on first use. It caches the folder ID in `drive_folder.json` under the secrets directory.

5. Run the backup manually from the project directory:

   ```powershell
   python scripts/automated_backup.py
   ```

6. A browser window opens for Google authorization if no valid school-account token exists. Sign in using the dedicated school Gmail account and approve the requested `drive.file` permission. Confirm the first encrypted archive appears in the **LGUTreasury Backups** folder in that account's Drive.

7. The authorization token is saved as `school_token.json` in the secrets directory. Do not delete it unless you intend to authorize again.

## Scheduled task considerations

- Configure Task Scheduler to run under the same Windows account used for the initial browser authorization.
- The scheduled task should run `python scripts/automated_backup.py` with the repository directory as its working directory.
- The task should treat a non-zero exit code as a failed backup run.
- A successful run means both local encryption and Drive upload verification succeeded.
- If the task reports a Drive authorization failure, sign in to Windows, run the command manually, complete the school Gmail authorization again, and then verify the next scheduled run.
- Keep the local encrypted backup and encryption key protected according to the existing backup/restore procedure.

## Files stored outside Git

The following files are expected under `C:\ProgramData\LGUTreasury\secrets`:

| File | Purpose |
|---|---|
| `credentials.json` | OAuth desktop client credentials downloaded from Google Cloud |
| `school_token.json` | Authorized school Gmail token, created by the script |
| `drive_folder.json` | Cached ID of the app-managed Drive backup folder |
| `backup.key` | Existing local encryption key; never upload it to Drive |

None of these files should be committed to the repository.
