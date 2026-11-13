# Pre-Deployment Checklist for `compose.native-mysql.yml`

This checklist ensures your native/host MySQL database and environment are properly prepared before deploying the LGUTreasury web application container using `compose.native-mysql.yml`.

---

## 1. Pre-Deployment Verification Checklist

Complete and check each requirement prior to running the deployment:

### MySQL Server & Service
- [ ] **MySQL Service is Running**:
  - Windows: Verify service status in `services.msc` or PowerShell:
    ```powershell
    Get-Service -Name "*mysql*"
    ```
  - Linux: Verify with systemd:
    ```bash
    sudo systemctl status mysql
    ```
- [ ] **Bind Address Allows Remote/Docker Access**:
  - MySQL by default may bind only to `127.0.0.1`. In `/etc/mysql/mysql.conf.d/mysqld.cnf` or `my.ini`, ensure:
    ```ini
    bind-address = 0.0.0.0
    ```
- [ ] **Target Database Schema Exists**:
  - Ensure the database is created with UTF-8 character encoding:
    ```sql
    CREATE DATABASE IF NOT EXISTS LGUTreasury CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
    ```

---

### User Permissions & Networking
- [ ] **Database User Accepts Connections from Docker**:
  - Containers connect via a Docker gateway IP (typically `172.17.x.x` or `172.18.x.x`). A user granted only on `'localhost'` will be rejected with `Access denied`.
  - Grant permissions to the user with `%` (or your Docker bridge subnet):
    ```sql
    CREATE USER IF NOT EXISTS 'lgu_user'@'%' IDENTIFIED BY 'YOUR_DB_PASSWORD';
    GRANT ALL PRIVILEGES ON LGUTreasury.* TO 'lgu_user'@'%';
    FLUSH PRIVILEGES;
    ```
- [ ] **Firewall / Port Access**:
  - Ensure port `3306` is open to incoming traffic from the Docker network adapter.
- [ ] **Docker Host Gateway Reachability**:
  - `compose.native-mysql.yml` includes `host.docker.internal:host-gateway`. On Windows and modern Linux with Docker Desktop / Docker Engine $\ge 20.10$, this resolves to the host machine.

---

### Environment File (`.env`) Configuration
- [ ] **`COMPOSE_FILE` Setting**:
  ```env
  COMPOSE_FILE=compose.native-mysql.yml
  ```
- [ ] **`NATIVE_MYSQL_CONNECTION` Connection String**:
  ```env
  NATIVE_MYSQL_CONNECTION=Server=host.docker.internal;Port=3306;Database=LGUTreasury;User=lgu_user;Password=YOUR_DB_PASSWORD;SslMode=Required;
  ```
  - Use `Server=host.docker.internal` when MySQL runs on the same machine as Docker.
  - `deploy.py` enforces `SslMode=Required` (or `VerifyCA`/`VerifyFull`) by default so database credentials and backups are encrypted in transit. If your local MySQL server lacks TLS, pass `--allow-insecure-db-tls`.
- [ ] **Initial Admin Credentials (First startup only)**:
  - If initializing a fresh database, ensure:
    ```env
    INITIAL_ADMIN_EMPLOYEE_ID=ADMIN001
    INITIAL_ADMIN_PASSWORD=AdminPassword123!
    ```
    *(Password must be $\ge$ 12 characters).*

---

### Automated Deployment Runner (`deploy.py`) Prerequisites
- [ ] **`mysqldump` Installed (for recovery backups)**:
  - When using `python deploy.py`, the runner performs an automated pre-deployment database backup.
  - Ensure `mysqldump` is available in your system `PATH` (or configured in `deploy.py`).

---

## 2. Pre-Deployment Test Commands

Run these quick tests before launching production:

### Test 1: Verify host MySQL connectivity
```bash
mysql -h 127.0.0.1 -P 3306 -u lgu_user -p LGUTreasury -e "SELECT 1;"
```

### Test 2: Test connectivity from inside a Docker container
Verify that Docker can reach the host MySQL database:
```powershell
docker run --rm --add-host=host.docker.internal:host-gateway mysql:8.4 mysql -h host.docker.internal -P 3306 -u lgu_user -pYOUR_DB_PASSWORD -e "SELECT 1;"
```
*(If this outputs `1`, your networking and user host grants are 100% ready).*

---

## 3. Deploying

Once all checklist items are verified:

```powershell
# Option A: Automated runner (with automated backups & health checks)
python deploy.py

# Option B: Direct Docker Compose
docker compose -f compose.native-mysql.yml up -d --build
```

---

## 4. Post-Deployment Verification
```powershell
# Verify application container is healthy
docker compose -f compose.native-mysql.yml ps

# Inspect logs to confirm database connection and migrations
docker compose -f compose.native-mysql.yml logs app --tail 30
```
Open [http://localhost:3000](http://localhost:3000) in your browser.
