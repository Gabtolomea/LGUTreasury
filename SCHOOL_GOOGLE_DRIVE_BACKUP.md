# School Google Drive Backup Setup

This guide configures the existing Python backup runner to upload encrypted LGUTreasury backups to the dedicated school Gmail account. Follow it on the Windows server when you are back in the office.

## 1. Security and operating model

- LGUTreasury remains a LAN-only application. Google Drive is only an off-site destination for encrypted backup archives; it is not required for normal application use.
- The script uploads the encrypted `.zip.enc` archive, not the unencrypted SQL dump or recovery files.
- The local encryption key is never uploaded. Without that key, a downloaded encrypted archive cannot be restored.
- OAuth credentials, OAuth tokens, the cached Drive folder ID, and the encryption key belong in the protected Windows secrets directory. Never commit or send these files.
- The OAuth client is configured as **External / Testing**. Google generally expires refresh tokens for Testing apps after seven days when Drive scopes are requested. Expect to reauthorize periodically. Until reauthorized, the local encrypted backup can still be created, but the run exits with a failure status because the cloud copy was not confirmed.

## 2. Confirm the Google Cloud project and school account

Before using the Windows server, sign in to Google Cloud with the account that manages the project and verify these settings:

1. Open the **LGUTreasury School Backup** project.
2. Open **Google Auth Platform** (OAuth consent configuration).
3. Confirm the app name is **LGUTreasury Backup** and the audience/user type is **External**.
4. Confirm the publishing status is **Testing**.
5. Under **Test users**, confirm the dedicated school Gmail address is listed exactly. Add it if it is missing.
6. Under **Data Access**, confirm the requested scope is `https://www.googleapis.com/auth/drive.file`.
7. Under **Clients**, confirm the desktop OAuth client named **LGUTreasury Backup Desktop** exists and its application type is **Desktop app**.
8. Download the JSON file for that desktop client if you do not already have it. Keep the downloaded file private. Do not paste its contents into chat or commit it to Git.

Use the dedicated school Gmail account during the later browser authorization step. The Google Cloud project owner account and the Gmail account that receives the backup do not have to be the same account.

## 3. Prepare the Windows secrets directory

Run PowerShell while signed in as the Windows account that will run the scheduled backup task. The scheduled task should use this same account so it can read the local key and OAuth token.

### 3.1 Create the directory

```powershell
$Secrets = 'C:\ProgramData\LGUTreasury\secrets'
New-Item -ItemType Directory -Force $Secrets | Out-Null
```

### 3.2 Protect the directory

The following commands remove inherited permissions and grant full access to the current Windows user, SYSTEM, and local Administrators. Run them from the same Windows account that will run the backup:

```powershell
$CurrentUser = "$env:USERDOMAIN\$env:USERNAME"
icacls $Secrets /inheritance:r
icacls $Secrets /grant:r "$($CurrentUser):(OI)(CI)F" "SYSTEM:(OI)(CI)F" "BUILTIN\Administrators:(OI)(CI)F"
```

Check the resulting permissions:

```powershell
icacls $Secrets
```

If your organization has a required Windows permissions policy, follow that policy while ensuring the scheduled-task account can read and write this directory.

### 3.3 Copy the OAuth client file

Find the JSON file downloaded for **LGUTreasury Backup Desktop**. It is commonly in Downloads. Adjust the source path below if your browser saved it elsewhere:

```powershell
$CredentialsSource = Join-Path $env:USERPROFILE 'Downloads\credentials.json'
Copy-Item -LiteralPath $CredentialsSource -Destination (Join-Path $Secrets 'credentials.json')
```

If the downloaded file has a longer name, use its actual full path as `$CredentialsSource`. The destination filename must be exactly `credentials.json`.

Confirm that the file exists without printing its contents:

```powershell
Test-Path (Join-Path $Secrets 'credentials.json')
Get-Item (Join-Path $Secrets 'credentials.json') | Select-Object Name, Length
```

The first command should return `True`. Do not use `Get-Content` to display the credentials in a terminal that may be recorded or shared.

## 4. Preserve the existing encryption key

The expected key path is:

```text
C:\ProgramData\LGUTreasury\secrets\backup.key
```

Check whether it exists:

```powershell
Test-Path (Join-Path $Secrets 'backup.key')
```

**Important: do not overwrite, replace, or regenerate an existing `backup.key`.** Previously created encrypted backups depend on the exact key that encrypted them. Replacing it can make those backups permanently unrecoverable.

If the key is missing, stop here and locate the original key or follow the project's approved key-recovery procedure before running the backup. Do not generate a new key as a quick fix. Keep a protected recovery copy according to the office's backup policy.

## 5. Prepare the project

1. Check out the `feature/school-google-drive-backup` branch and pull its latest changes. Do not merge it into `main` until the branch has been reviewed, tests have been run, and the office-server test is successful.
2. Open PowerShell in the LGUTreasury repository directory.
3. Confirm the Python dependencies from `requirements.txt` are installed in the environment used to run the backup.
4. Confirm the application's existing MySQL connection and backup configuration are correct for the office server.
5. In the repository's ignored `.env` file, optionally set the Drive folder name:

   ```env
   DRIVE_BACKUP_FOLDER_NAME=LGUTreasury Backups
   ```

   The script creates this folder in the currently authorized school Gmail's Drive on first use. It stores the folder ID in `drive_folder.json` under the secrets directory.

Do not put OAuth credentials, tokens, the encryption key, or database passwords in a tracked file.

## 6. Authorize the dedicated school Gmail account

Run the backup manually from the repository directory:

```powershell
python scripts/automated_backup.py
```

1. If no valid school-account token exists, the script should open a browser for Google authorization.
2. Check the Google account shown in the browser before approving. If another Google account is active, switch accounts and select the dedicated school Gmail address.
3. Review the permission request and approve the requested `drive.file` access.
4. Wait for the script to finish. Do not close the terminal while the backup or upload is running.
5. Open Google Drive while signed in to that same school Gmail account. Confirm that the **LGUTreasury Backups** folder exists and contains the newly uploaded encrypted backup.
6. Confirm the terminal reports success. A successful run means the local encrypted backup was created and the Drive upload was verified.

The script creates these files after authorization/use:

- `school_token.json`: the OAuth token for the school Gmail account.
- `drive_folder.json`: the cached ID of the app-managed Drive folder.

Do not create these two files manually. Do not copy an old personal-account token into the school setup. If authorization opens under the wrong Google account, stop and correct the account before proceeding.

## 7. Files kept outside Git

These files are expected under `C:\ProgramData\LGUTreasury\secrets`:

| File | Purpose | How it is created |
|---|---|---|
| `credentials.json` | OAuth desktop-client configuration downloaded from Google Cloud | Copy it into the secrets directory |
| `school_token.json` | Authorized token for the dedicated school Gmail account | Created by the script during browser authorization |
| `drive_folder.json` | Cached ID of the app-managed Drive backup folder | Created by the script |
| `backup.key` | Existing local encryption key used to encrypt/decrypt backups | Preserve the existing key; never replace it casually |

All four files must remain outside Git. Never upload `backup.key`, `credentials.json`, or `school_token.json` to Google Drive as part of the backup archive. The only intended Drive upload is the encrypted backup archive.

## 8. Configure Windows Task Scheduler

After the manual run succeeds:

1. Open **Task Scheduler** and create or update the LGUTreasury backup task.
2. Set the task to run under the same Windows account used for the manual Google authorization.
3. Set the program to the Python executable in the environment that has the required dependencies installed.
4. Set the arguments to:

   ```text
   scripts\automated_backup.py
   ```

5. Set **Start in** to the full path of the LGUTreasury repository directory. This is important because the script uses the project working directory.
6. Configure the schedule according to the office backup policy (the intended interval is every 24 hours).
7. Save the task, then use **Run** in Task Scheduler for a test. Check the task's Last Run Result and the backup log.

A non-zero exit code means the run failed. Check the log and confirm both the local encrypted archive and the Drive copy before treating a backup as successful.

## 9. Reauthorization and troubleshooting

Because the OAuth app is External / Testing and requests a Drive scope, its refresh token is expected to expire after about seven days. This is a Google OAuth testing-mode limitation, not a reason to expose the application or database to the internet.

When the log reports an expired or invalid authorization:

1. Sign in to Windows using the scheduled-task account.
2. Run `python scripts/automated_backup.py` manually from the repository directory.
3. In the browser, select the dedicated school Gmail account and approve access again.
4. Confirm a new encrypted archive appears in that account's **LGUTreasury Backups** folder.
5. Confirm the manual run succeeds, then check the next scheduled run.

Do not delete `backup.key` to fix an OAuth problem. Do not delete the local encrypted backup when a Drive upload fails; retain it and investigate the authorization or network issue. The application itself should remain LAN-only throughout this setup.
