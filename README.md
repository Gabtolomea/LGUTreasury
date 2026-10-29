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
Database__ApplyMigrations=true
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
revenue categories/types/policies and a default admin account.

> **Default admin (development):** employee ID `ADMIN001`, password `Admin@1234`.
> Change it immediately in any shared environment.

Then open `https://localhost:7087`. HTTP requests on `http://localhost:5094` redirect to HTTPS.

---

### 4. Run with Docker Compose

The Docker workflow runs the ASP.NET Core app and MySQL together. It is intended for
local development, uses a persistent MySQL volume, and uses HTTP by default. Native
development continues to use HTTPS.

Copy the example environment file and set your local database passwords:

```powershell
Copy-Item .env.example .env
```

Docker uses `http://localhost:8080` by default. No HTTPS certificate is needed. To
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

Start the application:

```bash
docker compose up --build
```

Open `http://localhost:8080` after the containers start. Docker Compose applies the
checked-in EF Core migrations and seeds the starter data. The Docker app connects to
MySQL through the Compose service name `db`; native development continues to use the
`localhost` connection string. When HTTPS is enabled, use `https://localhost:8443` and
HTTP redirects to HTTPS. The development certificate is trusted for `localhost`; LAN
access needs a certificate that includes the LAN hostname or IP address.

Useful commands:

```bash
docker compose ps
docker compose logs -f app
docker compose down
```

### Docker development commands

Open the MySQL console:

```bash
docker compose exec db mysql -u root -p
```

Enter the `MYSQL_ROOT_PASSWORD` from your `.env` file when asked. Then use:

```sql
SHOW DATABASES;
USE LGUTreasury;
SHOW TABLES;
EXIT;
```

If you changed `MYSQL_DATABASE` in `.env`, use that database name instead.

Other useful commands:

```bash
# View database logs
docker compose logs -f db

# Restart only the app
docker compose restart app

# Open a shell inside the app container
docker compose exec app sh

# Check the app health page
# Open http://localhost:8080/health in a browser
```

`docker compose down` preserves the database volume. To intentionally reset all local
Docker data, run `docker compose down -v`.

The development admin account is employee ID `ADMIN001` with password `Admin@1234`.
Change it when using data outside a private development environment.

### Safe Docker deployment

For the bundled-MySQL Compose workflow, run the deployment runner from the project root:

```powershell
python deploy.py
```

The runner checks Docker and the environment, waits for MySQL readiness, saves a
timestamped database dump and recovery image under `backups/`, validates Compose, deploys
the app, and checks both the container health and `/health` endpoint. It rolls back the
previous app image when deployment or health checks fail. It never removes the MySQL
volume or database backups.

Deployment logs are appended to `logs/deployment.log`. The runner requires
`MYSQL_PASSWORD` and `MYSQL_ROOT_PASSWORD` in `.env` or the host environment. When
`ENABLE_HTTPS=true`, it also requires `HTTPS_CERT_DIR` and `HTTPS_CERT_PASSWORD`. Native
MySQL deployments using `compose.native-mysql.yaml` are not handled by this runner.
After a successful deployment, it prints URLs matching the selected HTTP or HTTPS mode.

---

### 5. Run the Docker app with native MySQL

To run only the app container while using MySQL installed on the host machine, set
`NATIVE_MYSQL_CONNECTION` in `.env`. Use `host.docker.internal` as the MySQL host:

```env
NATIVE_MYSQL_CONNECTION=Server=host.docker.internal;Port=3306;Database=LGUTreasury;User=YOUR_DB_USER;Password=YOUR_DB_PASSWORD;SslMode=Required;
```

Start the app without starting the bundled MySQL container:

```bash
docker compose -f compose.yaml -f compose.native-mysql.yaml up --build --no-deps app
```

The native MySQL server must accept TCP connections from Docker Desktop on port 3306,
and the configured user must have access to the database. Keep `SslMode=Required` when
the server has TLS enabled; use the setting supported by the local development server.

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
