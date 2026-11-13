# Operations & Handoff Guide

Quick operational reference for developers and operators managing the LGUTreasury environment.

---

## 1. Quick Stack Reference

| Service | Address / Port | Credentials / Notes |
|---|---|---|
| **Web App** | `http://localhost:8092` | Redirects to `/Account/Login` |
| **MySQL (inside Docker network)** | `db:3306` | User: `lgu_user` / DB: `LGUTreasury` |
| **Default Admin Account** | Login via Web UI | **Employee ID**: `ADMIN001`<br>**Password**: set in `.env` (min 12 chars) |

---

## 2. Viewing Container Logs

### View recent logs
```bash
# View last 50 lines of web application logs
docker compose -f compose.container-mysql.yml logs app --tail 50

# View last 50 lines of MySQL database logs
docker compose -f compose.container-mysql.yml logs db --tail 50
```

### Stream logs in real-time
```bash
# Follow web app logs
docker compose -f compose.container-mysql.yml logs -f app

# Follow all container logs together
docker compose -f compose.container-mysql.yml logs -f
```

### Direct Docker command (alternative)
```bash
docker logs -f lgutreasury-app-1
docker logs -f lgutreasury-db-1
```

---

## 3. Accessing the MySQL Database

### Inside the container via CLI
To open an interactive MySQL shell:

```bash
# Connect as application user:
docker exec -it lgutreasury-db-1 mysql -u lgu_user -p LGUTreasury

# Connect as root user:
docker exec -it lgutreasury-db-1 mysql -u root -p
```
*(Enter the password configured in your `.env` file)*

### Run one-off SQL queries directly from PowerShell/terminal
```bash
docker exec lgutreasury-db-1 mysql -u lgu_user -p123456789 -e "USE LGUTreasury; SHOW TABLES;"
```

### From GUI Tools on Host (DBeaver, MySQL Workbench, DataGrip)
To expose MySQL to your host machine for external GUI access, add a port mapping in `compose.container-mysql.yml` under `db`:
```yaml
ports:
  - "3306:3306"
```
Then connect using:
- **Host**: `localhost`
- **Port**: `3306`
- **Database**: `LGUTreasury`
- **Username**: `lgu_user` (or `root`)
- **Password**: As configured in `.env`

---

## 4. Database Backup & Restore

### Export a database dump
```bash
# Export full LGUTreasury database to backup.sql
docker exec lgutreasury-db-1 mysqldump -u root -p123456789 LGUTreasury > backup.sql
```

### Import / Restore a SQL file (e.g. `import.sql` or backup dump)
To import `import.sql` into the containerized MySQL database on a production or remote host (such as `ctugwebserver`):

```bash
# On Linux host / server using Docker Compose (pass -T to disable pseudo-TTY allocation)
docker compose exec -T db mysql -u root -plgu_user_root LGUTreasury < import.sql
```

Expected output confirms tables and flushes table analyze status:
```text
mysql: [Warning] Using a password on the command line interface can be insecure.
Table   Op      Msg_type        Msg_text
LGUTreasury.__EFMigrationsHistory       analyze status  OK
LGUTreasury.RevenueCategories   analyze status  OK
LGUTreasury.RevenueTypes        analyze status  OK
LGUTreasury.RevenuePolicies     analyze status  OK
LGUTreasury.BillingTypeOptions  analyze status  OK
LGUTreasury.UserAccounts        analyze status  OK
LGUTreasury.LongTermPayees      analyze status  OK
LGUTreasury.AccountBillingTypes analyze status  OK
LGUTreasury.MonthlyBills        analyze status  OK
LGUTreasury.Payees      analyze status  OK
LGUTreasury.PaymentRecords      analyze status  OK
LGUTreasury.RecordLineItems     analyze status  OK
LGUTreasury.Editrequests        analyze status  OK
LGUTreasury.DeletedRecords      analyze status  OK
LGUTreasury.ReportLog   analyze status  OK
```

Alternatively using direct Docker container commands:
```powershell
# PowerShell (Windows)
Get-Content import.sql -Raw | docker exec -i lgutreasury-db-1 mysql -u root -plgu_user_root LGUTreasury
```
```bash
# Linux / macOS / Git Bash
docker exec -i lgutreasury-db-1 mysql -u root -plgu_user_root LGUTreasury < import.sql
```

---

## 5. Common Maintenance Commands

### Restart the web app only
```bash
docker compose -f compose.container-mysql.yml restart app
```

### Rebuild and update code changes
```bash
docker compose -f compose.container-mysql.yml up -d --build
```

### Full reset (Fresh start with clean database)
> [!WARNING]
> This command deletes the persistent volume and wipes all database records.
```bash
docker compose -f compose.container-mysql.yml down -v
docker compose -f compose.container-mysql.yml up -d --build
```

---

## 6. Critical Operational Notes

1. **Database Migrations Run Automatically**:
   - Migrations are enabled by default (`Database:ApplyMigrations: true` in `appsettings.json`). There is no need to declare this in `.env`.
2. **Initial Admin Password Constraint**:
   - `INITIAL_ADMIN_PASSWORD` in `.env` **must be at least 12 characters** (e.g. `AdminPassword123!`). Startup will throw an error if shorter.
3. **Shell Script Line Endings (`LF`)**:
   - Keep shell scripts (like `docker-entrypoint.sh`) using Unix line endings (`LF`). Windows `CRLF` will cause the Linux container to fail startup with `set: Illegal option -`. This is enforced via `.gitattributes`.

---

## 7. Native MySQL Pre-Deployment Checklist

When deploying with `compose.native-mysql.yml` (app in Docker connecting to an external or host MySQL server), consult the [Native MySQL Pre-Deployment Checklist](NATIVE_DEPLOYMENT_CHECKLIST.md) to verify:
- MySQL `bind-address` allows Docker network access (`0.0.0.0`).
- User permissions are granted for `'lgu_user'@'%'` (not just `localhost`).
- Connection string reachability via `host.docker.internal`.

---

## 8. Troubleshooting & Known Issues

For solutions to common errors (entrypoint line endings, password constraints, SSL modes, user permissions, upload permissions, and missing columns), refer to the [Troubleshooting Guide](TROUBLESHOOTING.md).

