# LGUTreasury

Revenue collection and treasury management system for the **LGU Treasurer's Office**
(ASP.NET Core MVC, .NET 10, MySQL). It records payments, manages revenue types and
payees, provides analytics, and exports revenue summary reports.

---

## Features

- **Authentication & accounts** — session-based login, registration, forgot-password,
  BCrypt password hashing, role-based admin area.
- **Record payments** — issue official receipts with line items (base / surcharge /
  interest) tied to revenue types.
- **Revenue types** — categories, rates, recurring flags, and ordinance references.
- **Long-term tenants** — billing types and monthly bills for recurring payees.
- **Analytics** — collection dashboards with preset and custom date filters.
- **Reports (`Revenue Summaries`)** — generate revenue summaries filtered by date range
  and revenue type, exported as **PDF**, **Excel (XLSX)**, or **CSV**.
  - Report types: Daily Collection, Monthly, Annual, and **Custom** (manual date range).
  - Report generation is logged to a `ReportLog` audit table.

---

## Tech stack

| Area | Technology |
|---|---|
| Runtime | .NET 10 (`Microsoft.NET.Sdk.Web`) |
| Web | ASP.NET Core MVC + Razor views |
| ORM | Entity Framework Core 10 + `MySql.EntityFrameworkCore` |
| Database | MySQL |
| PDF | QuestPDF |
| Excel | ClosedXML |
| Auth | Session-based + `BCrypt.Net-Next` |
| Tests | xUnit + EF Core InMemory |

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- MySQL server (local or remote)
- Docker Desktop (for the Docker workflow)
- Python 3.8 or newer (for the safe deployment runner)
- Git

---

## Getting started

### 1. Clone

```bash
git clone https://github.com/Gabtolomea/LGUTreasury.git
cd LGUTreasury
```

### 2. Configure credentials (`.env`)

Credentials are **not** stored in `appsettings.json`. Copy the example file and fill in
your values:

```powershell
Copy-Item .env.example .env
```

```bash
# bash
cp .env.example .env
```

`.env` contents (`__` is ASP.NET Core's config hierarchy separator):

```env
ConnectionStrings__DefaultConnection=Server=localhost;Port=3306;Database=LGUTreasury;User=YOUR_DB_USER;Password=YOUR_DB_PASSWORD;SslMode=Required;
ENABLE_HTTPS=false
```

`SslMode=Required` forces a TLS connection to MySQL. MySQL 8's default
`caching_sha2_password` plugin otherwise needs `AllowPublicKeyRetrieval=True` to
send the password over an unencrypted link; with TLS the password is encrypted
in the channel, so that flag is unnecessary.

`Program.cs` loads `.env` at startup via `DotEnv.LoadDefault()`. `.env` is git-ignored —
**never commit it**. Environment variables provided by the OS/host take precedence over `.env`.

### 3. Restore and run

```bash
dotnet restore
dotnet run --launch-profile https
```

On native startup the app applies the checked-in EF Core migrations and seeds starter data:
revenue categories/types/policies. If the database has no administrator, set
`INITIAL_ADMIN_EMPLOYEE_ID` and `INITIAL_ADMIN_PASSWORD` in `.env` first. The initial
password must be at least 12 characters. These values are used only to create the first
administrator; remove them from `.env` after the account is created.

Then open `https://localhost:7087`. HTTP requests on `http://localhost:5094` redirect to HTTPS.

---

### 4. Run with Docker Compose

> [!TIP]
> For container logs, MySQL access, database backup/restore, and maintenance commands, refer to the [Operations & Handoff Guide](HANDOFF.md).

Two Docker Compose configurations are available:
- **`compose.container-mysql.yml`**: Runs both the ASP.NET Core application and a containerized MySQL 8.4 database with persistent volumes.
- **`compose.native-mysql.yml`**: Runs only the ASP.NET Core app, connecting to an external or host MySQL server through `NATIVE_MYSQL_CONNECTION`.

Copy the example environment file and configure your database settings:

```powershell
Copy-Item .env.example .env
```

Set `COMPOSE_FILE` in `.env` to select your preferred configuration:

```env
# For containerized MySQL (app + database in Docker):
COMPOSE_FILE=compose.container-mysql.yml

# Or for native MySQL (app in Docker connecting to host MySQL):
COMPOSE_FILE=compose.native-mysql.yml
```

#### Running with Containerized MySQL

When using `compose.container-mysql.yml`, set the MySQL passwords in `.env`:

```env
MYSQL_DATABASE=LGUTreasury
MYSQL_USER=lgu_user
MYSQL_PASSWORD=YOUR_DB_PASSWORD
MYSQL_ROOT_PASSWORD=YOUR_ROOT_PASSWORD
```

Start both the application and the MySQL container:

```bash
docker compose -f compose.container-mysql.yml up --build
```

Access the MySQL console inside the database container:

```bash
docker compose -f compose.container-mysql.yml exec db mysql -u root -p
```

The database data is persisted in the Docker volume `lgu-treasury-container-mysql-data`. `docker compose down` preserves this volume; to remove it and start fresh, run `docker compose down -v`.

#### Running with Native MySQL

When using `compose.native-mysql.yml`, MySQL runs natively on the host machine or on another reachable server. Set `NATIVE_MYSQL_CONNECTION` in `.env` (use `host.docker.internal` as the host when MySQL is installed on the local machine):

```env
NATIVE_MYSQL_CONNECTION=Server=host.docker.internal;Port=3306;Database=LGUTreasury;User=YOUR_DB_USER;Password=YOUR_DB_PASSWORD;SslMode=Required;
```

Start the application:

```bash
docker compose -f compose.native-mysql.yml up --build
```

`docker compose down` stops and removes the app container. It does not affect the native MySQL server or its data.

---

#### Common Docker Settings

Docker uses `http://localhost:8092` by default. No HTTPS certificate is needed. To
enable HTTPS later, set these values in `.env`:

```powershell
New-Item -ItemType Directory -Force .aspnet\https | Out-Null
dotnet dev-certs https -ep .aspnet\https\LGUTreasury.pfx -p YOUR_CERT_PASSWORD
dotnet dev-certs https --trust
```

Set the same certificate password in `.env`:

```env
ENABLE_HTTPS=true
HTTPS_CERT_DIR=./.aspnet/https
HTTPS_CERT_PASSWORD=YOUR_CERT_PASSWORD
```

Open `http://localhost:8092` after the containers start. Docker Compose applies the
checked-in EF Core migrations and seeds the starter data. When HTTPS is
enabled, use `https://localhost:8443` and HTTP redirects to HTTPS. The development
certificate is trusted for `localhost`; LAN access needs a certificate that includes
the LAN hostname or IP address.

Useful commands (if `COMPOSE_FILE` is set in `.env`, omit `-f <filename>`):

```bash
docker compose ps
docker compose logs -f app
docker compose down
```

Other useful commands:

```bash
# Restart only the app
docker compose restart app

# Open a shell inside the app container
docker compose exec app sh

# Check process liveness
# Open http://localhost:8092/health/live
# Check application and MySQL readiness
# Open http://localhost:8092/health/ready
```

For a fresh database, configure `INITIAL_ADMIN_EMPLOYEE_ID` and
`INITIAL_ADMIN_PASSWORD` in `.env` before starting the app. Use a unique password of at
least 12 characters. Once the first administrator is created, remove both bootstrap
values from `.env`; existing administrator accounts are not recreated or reset.

When connecting to native MySQL on an Ubuntu host or Windows:
- Ensure MySQL accepts TCP connections on port 3306 (e.g. set `bind-address = 0.0.0.0` or allow the Docker bridge network `172.17.0.0/16` in `/etc/mysql/mysql.conf.d/mysqld.cnf`).
- `compose.native-mysql.yml` includes `extra_hosts: ["host.docker.internal:host-gateway"]` to route `host.docker.internal` to the host on Linux.
- The configured MySQL user must have access to the database from the Docker network.
- Keep `SslMode=Required` when the server has TLS enabled.

### Safe Docker deployment

The deployment runner supports both setups and automatically selects the configuration specified by `COMPOSE_FILE` in `.env` (or environment):

```powershell
python deploy.py
```

Show the executed commands in the terminal as well:

```powershell
python deploy.py --verbose
```

- **Containerized MySQL (`COMPOSE_FILE=compose.container-mysql.yml`)**:
  - Requires `MYSQL_PASSWORD` and `MYSQL_ROOT_PASSWORD` in `.env`.
  - Performs preflight checks on Docker and Compose.
  - Automatically dumps the containerized database via `docker compose exec` into a timestamped directory in `backups/`.
  - Backs up the active application image.
  - Validates Compose services (`db` and `app`).
  - Builds and deploys the containers with `--wait`.
  - Verifies container health for `db`, container health for `app`, and tests the live database connection via `/health/ready`.
  - Automatically rolls back the application image if deployment or health checks fail.

- **Native MySQL (`COMPOSE_FILE=compose.native-mysql.yml`)**:
  - Requires `NATIVE_MYSQL_CONNECTION` in `.env`.
  - Runs cross-platform `mysqldump` (supporting Ubuntu host with POSIX permissions and Windows with ACLs) into `backups/`.
  - Backs up the active application image.
  - Validates and deploys the application container.
  - Checks container health and the database-aware `/health/ready` endpoint.
  - Automatically rolls back the application image on failure.

Deployment logs are appended to `logs/deployment.log`. When `ENABLE_HTTPS=true`, both setups require `HTTPS_CERT_DIR` and `HTTPS_CERT_PASSWORD`. After a successful deployment, the runner prints local and LAN URLs for the active HTTP or HTTPS mode.

Each run prints the current stage, the individual preflight and health-check results, and a final summary with the deployment duration, database and application health, rollback and cleanup outcome, recovery backup location, log location, and application URLs. Docker commands are not printed by default; every executed command and its result is recorded in `logs/deployment.log`. Use `--verbose` to show the executed commands in the terminal too.

### Database Restoration

To restore a MySQL database from a previous backup:

```bash
# Restore latest backup automatically
python -m scripts.restore

# Or restore a specific backup directory or SQL file
python -m scripts.restore backups/backup_20261110_120000_123456
python -m scripts.restore backups/backup_20261110_120000_123456/database.sql
```

The restore script automatically determines whether containerized or native MySQL is active:
- For containerized MySQL, it ensures the `db` container is running and healthy, and streams the SQL dump into the container.
- For native MySQL, it uses the host `mysql` client with secure temporary credentials.

---

## Reports

The **Generate Report** page (`/Report`) is titled *Revenue Summaries*.

- **Report Type** — Daily, Monthly, Annual, or **Custom**.
  - Daily/Monthly/Annual auto-fill the From/To dates.
  - **Custom** leaves From/To for manual entry.
- **File Format** — `PDF`, `Excel (XLSX)`, or `CSV` (case-insensitive; anything else
  returns `400 Bad Request`).
- **From / To Date** — cannot be in the future; From must not be after To.
- **Revenue Type** — optional filter (all types, or one).

CSV exports escape fields per RFC 4180 and neutralize spreadsheet formula injection.

---

## Tests

The test project lives in `LGUTreasury.Tests` (xUnit, EF Core InMemory — no database
required).

```bash
dotnet test LGUTreasury.Tests/LGUTreasury.Tests.csproj
```

The deployment runner tests use only Python's standard library:

```bash
python -m unittest discover -s tests -v
```

**If the app is running**, its `bin/…/LGUTreasury.dll` is locked and a normal build fails.
Build into a separate folder instead (git-ignored):

```bash
dotnet test LGUTreasury.Tests/LGUTreasury.Tests.csproj -p:BaseOutputPath=bin2/
```

Coverage includes report date/revenue-type filtering, report-type mapping, authentication,
PDF/CSV/XLSX export, CSV safety, and view structure.

---

## Project structure

```
Controllers/        MVC controllers (Account, Record, Report, Analytics, Admin, …)
Models/             EF Core entities and view models
Data/               ApplicationDbContext
Migrations/         EF Core migrations
Views/              Razor views (per controller)
wwwroot/            Static assets (css, js, lib)
LGUTreasury.Tests/  xUnit test project
scripts/            Developer helper scripts (see MERGE_GUIDE.md)
DotEnv.cs           Minimal .env loader
Program.cs          App startup, DI, seeding
```

---

## Merging branches

See **[MERGE_GUIDE.md](MERGE_GUIDE.md)** for the merge workflow and the helper script
`scripts/merge-branch.sh`.

---

## Security notes

- Database credentials live only in `.env` (git-ignored). `.gitignore` also excludes
  `bin/`, `obj/`, `publish/`, test output, and IDE files.
- If credentials were ever committed, **rotate them** — history is not automatically purged.
