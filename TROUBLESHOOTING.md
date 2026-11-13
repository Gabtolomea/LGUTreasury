# Troubleshooting & Known Issues Guide

A comprehensive record of real-world problems, root causes, exact error messages, and verified solutions encountered in the LGUTreasury system.

---

## Quick Reference Index

| Symptom / Error Message | Root Cause | Section |
|---|---|---|
| `./docker-entrypoint.sh: 2: set: Illegal option -` | Windows CRLF line endings in Linux shell script | [Issue 1](#issue-1-docker-entrypoint-fails-with-set-illegal-option--) |
| `System.InvalidOperationException: No administrator account exists...` | Password under 12 characters | [Issue 2](#issue-2-admin-bootstrap-password-length-error) |
| `Unknown column 'l.IsBillingEnabled' in 'field list'` | C# model changed without creating/applying EF Core migration | [Issue 3](#issue-3-missing-column-error-on-long-term-tenants-page) |
| `"We could not save your profile picture. Please try again."` | Non-root container user lacked write permissions on `wwwroot/uploads` | [Issue 4](#issue-4-profile-picture-upload-fails-permission-denied) |
| `error CS0103: The name 'initials' does not exist in the current context` | Unscoped variable in Razor view | [Issue 5](#issue-5-razor-build-error-cs0103-in-views) |
| `System.ArgumentException: Requested value 'None' was not found` | Invalid `SslMode` enum value in connection string | [Issue 6](#issue-6-invalid-sslmode-value-in-mysql-connection-string) |
| `Authentication requires secure connection` | MySQL 8.4 `caching_sha2_password` with disabled SSL | [Issue 7](#issue-7-mysql-84-caching_sha2_password-requires-ssl) |
| `The database could not be reached for migrations` | MySQL user permissions restricted to `localhost` or wrong database | [Issue 8](#issue-8-database-unreachable-for-migrations-user-grants) |

---

### Issue 1: Docker Entrypoint Fails with `set: Illegal option -`

#### Symptoms
Container immediately enters `Restarting (2)` crash loop. Logs show:
```text
./docker-entrypoint.sh: 2: set: Illegal option -
```

#### Root Cause
The file was edited or saved on Windows with **CRLF** (`\r\n`) line endings. The Linux shell interpreter reads `set -eu\r`, interpreting the carriage return `\r` as an invalid option switch.

#### Solution
1. Ensure `.gitattributes` enforces `LF` for shell scripts:
   ```gitattributes
   *.sh text eol=lf
   ```
2. Convert the script line endings to `LF` in PowerShell:
   ```powershell
   $content = [System.IO.File]::ReadAllText("docker-entrypoint.sh")
   $content = $content.Replace("`r`n", "`n")
   [System.IO.File]::WriteAllText("docker-entrypoint.sh", $content, (New-Object System.Text.UTF8Encoding($false)))
   ```

---

### Issue 2: Admin Bootstrap Password Length Error

#### Symptoms
Application fails on startup with:
```text
System.InvalidOperationException: No administrator account exists. Set INITIAL_ADMIN_EMPLOYEE_ID and INITIAL_ADMIN_PASSWORD (at least 12 characters) for the first startup.
```

#### Root Cause
[Program.cs](file:///c:/Dev/LGUTreasury/Program.cs#L72) enforces a strict security check:
```csharp
if (string.IsNullOrWhiteSpace(initialAdminEmployeeId) ||
    string.IsNullOrWhiteSpace(initialAdminPassword) ||
    initialAdminPassword.Length < 12)
```
Setting `INITIAL_ADMIN_PASSWORD=123456789` (9 characters) triggers an immediate application crash.

#### Solution
Update [.env](file:///c:/Dev/LGUTreasury/.env) to use a password with **12 or more characters**:
```env
INITIAL_ADMIN_PASSWORD=AdminPassword123!
```

---

### Issue 3: Missing Column Error on Long Term Tenants Page

#### Symptoms
Navigating to the **Long Term Tenants** page throws:
```text
MySql.Data.MySqlClient.MySqlException (0x80004005): Unknown column 'l.IsBillingEnabled' in 'field list'
   at LGUTreasury.Controllers.LongTermTenantController.Index()
```

#### Root Cause
A property `public bool IsBillingEnabled { get; set; } = true;` was added to `Models/LongTermPayee.cs` and used in queries, but the corresponding EF Core migration was never created, leaving the database table missing that column.

#### Solution
1. Add the column to the MySQL table:
   ```sql
   ALTER TABLE LGUTreasury.LongTermPayees ADD COLUMN IsBillingEnabled tinyint(1) NOT NULL DEFAULT 1;
   ```
2. Create and commit the EF Core migration `AddIsBillingEnabledToLongTermPayees.cs` and update `ApplicationDbContextModelSnapshot.cs`.
3. Record the migration in `__EFMigrationsHistory`:
   ```sql
   INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('20261112164700_AddIsBillingEnabledToLongTermPayees', '10.0.9');
   ```

---

### Issue 4: Profile Picture Upload Fails ("Permission denied")

#### Symptoms
Uploading a profile photo returns:
`"We could not save your profile picture. Please try again."`
In container logs:
```text
mkdir: cannot create directory '/app/wwwroot/uploads': Permission denied
```

#### Root Cause
The ASP.NET Core container runs under the unprivileged non-root user `app` (`UID 1654`), but the build artifacts in `/app/wwwroot` were owned by `root:root` from the build stage. Furthermore, uploaded images were stored inside the ephemeral container filesystem without a persistent volume.

#### Solution
1. In `Dockerfile`, pass `--chown=$APP_UID:$APP_UID` on all `COPY` commands and pre-create directories:
   ```dockerfile
   FROM base AS final
   WORKDIR /app
   RUN mkdir -p /app/wwwroot/uploads/profile-pictures
   COPY --chown=$APP_UID:$APP_UID --from=build /app/publish .
   COPY --chown=$APP_UID:$APP_UID docker-entrypoint.sh .
   ```
2. In Compose files (`compose.container-mysql.yml` & `compose.native-mysql.yml`), mount a named volume:
   ```yaml
   volumes:
     - uploads-data:/app/wwwroot/uploads
   ```

---

### Issue 5: Razor Build Error CS0103 in Views

#### Symptoms
Docker build fails during `dotnet publish`:
```text
/src/Views/Home/Index.cshtml(499,137): error CS0103: The name 'initials' does not exist in the current context
```

#### Root Cause
In `Views/Home/Index.cshtml`, `@initials` was referenced, but unlike `Profile/Index.cshtml`, `Home/Index.cshtml` stores initials in `ViewData["Initials"]` rather than a local variable.

#### Solution
Reference the scoped dictionary:
```html
<span class="fallback-initials" style="display:none;">@ViewData["Initials"]</span>
```

---

### Issue 6: Invalid SslMode Value in MySQL Connection String

#### Symptoms
Container crashes immediately with:
```text
Unhandled exception. System.ArgumentException: Requested value 'None' was not found.
   at System.Enum.Parse(Type enumType, String value, Boolean ignoreCase)
   at MySql.Data.MySqlClient.MySqlConnectionStringBuilder.set_Item(String keyword, Object value)
```

#### Root Cause
The project uses Oracle's official **`MySql.Data`** driver. Its `MySqlSslMode` enum accepts:
- `Disabled` (or `0`)
- `Preferred` (or `1`)
- `Required` (or `2`)
- `VerifyCA` (or `3`)
- `VerifyFull` (or `4`)

The string `None` is supported by Pomelo, but **fails in `MySql.Data`**.

#### Solution
In [.env](file:///c:/Dev/LGUTreasury/.env), use:
```env
SslMode=Required;  # For TLS-encrypted links (recommended)
# Or
SslMode=Disabled;  # For unencrypted links
```

---

### Issue 7: MySQL 8.4 caching_sha2_password Requires SSL

#### Symptoms
Connecting to native MySQL fails with:
```text
ERROR 2061 (HY000): Authentication plugin 'caching_sha2_password' reported error: Authentication requires secure connection.
```

#### Root Cause
In MySQL 8.4, users authenticated with `caching_sha2_password` will reject unencrypted TCP connections from outside `localhost` if `SslMode=Disabled`. The password cannot be safely negotiated in plaintext without an RSA public key exchange or a TLS channel.

#### Solution
Set `SslMode=Required;` in your `NATIVE_MYSQL_CONNECTION`:
```env
NATIVE_MYSQL_CONNECTION=Server=host.docker.internal;Port=3307;Database=LGUTreasury;User=dev_user;Password=dev_password;SslMode=Required;
```
With TLS active, `caching_sha2_password` connects instantly and securely.

---

### Issue 8: Database Unreachable for Migrations (User Grants)

#### Symptoms
Container logs show:
```text
Unhandled exception. System.InvalidOperationException: The database could not be reached for migrations.
   at Program.<Main>$(String[] args) in /src/Program.cs:line 54
```
Direct query shows: `Access denied for user 'user'@'%' to database 'LGUTreasury'`.

#### Root Cause
The MySQL user was only granted privileges for `localhost` (not `%` for Docker gateway IPs) or only for a different database (e.g., `development_db` instead of `LGUTreasury`).

#### Solution
Grant full privileges to the user across all hosts for the target database:
```sql
CREATE DATABASE IF NOT EXISTS LGUTreasury CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
GRANT ALL PRIVILEGES ON LGUTreasury.* TO 'dev_user'@'%';
FLUSH PRIVILEGES;
```
