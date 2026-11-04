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
dotnet run
```

On first run the app creates the schema (`Database.EnsureCreated()`) and seeds starter data:
revenue categories/types/policies and a default admin account.

> **Default admin (development):** employee ID `ADMIN001`, password `Admin@1234`.
> Change it immediately in any shared environment.

Then open the URL printed in the console (e.g. `https://localhost:7xxx`).

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
