
import os
import re
from pathlib import Path


def project_directory(config):
    return Path(config.get("project_dir", ".")).expanduser().resolve()


def project_path(config, value):
    path = Path(value)
    if not path.is_absolute():
        path = project_directory(config) / path
    return path.absolute()


def read_environment_file(path):
    values = {}
    if not path or not path.exists():
        return values

    for line in path.read_text(encoding="utf-8").splitlines():
        text = line.strip()
        if not text or text.startswith("#") or "=" not in text:
            continue

        name, value = text.split("=", 1)
        name = name.strip()
        value = value.strip()

        if value.startswith(("\"", "'")) and value.endswith(value[0]):
            value = value[1:-1]

        if name:
            values[name] = value

    return values


def environment_names(config):
    names = set(config.get("required_env", []))
    names.update([
        "NATIVE_MYSQL_CONNECTION",
        "MYSQL_DATABASE",
        "MYSQL_USER",
        "MYSQL_PASSWORD",
        "MYSQL_ROOT_PASSWORD",
        "ENABLE_HTTPS",
        "HTTPS_CERT_DIR",
        "HTTPS_CERT_PASSWORD",
        "INITIAL_ADMIN_EMPLOYEE_ID",
        "INITIAL_ADMIN_PASSWORD",
    ])
    return names


def environment_values(config):
    values = {}
    env_file = config.get("env_file")

    if env_file:
        values.update(read_environment_file(project_path(config, env_file)))

    for name in environment_names(config):
        if name in os.environ:
            values[name] = os.environ[name]

    return values


def parse_mysql_connection_string(connection_string):
    values = {}
    parts = []
    current = []
    quote = None
    index = 0

    while index < len(connection_string):
        char = connection_string[index]

        if quote:
            if char == quote:
                if (
                    index + 1 < len(connection_string)
                    and connection_string[index + 1] == quote
                ):
                    current.append(char)
                    index += 2
                    continue
                quote = None
            else:
                current.append(char)

        elif char in ("'", '"') and "=" in "".join(current):
            quote = char

        elif char == ";":
            parts.append("".join(current))
            current = []

        else:
            current.append(char)

        index += 1

    if quote:
        raise ValueError("The MySQL connection string contains an unclosed quote.")

    if current:
        parts.append("".join(current))

    for part in parts:
        if "=" not in part:
            continue

        key, value = part.split("=", 1)
        key = key.strip().lower()
        value = value.strip()

        if key:
            values[key] = value

    database = (
        values.get("database")
        or values.get("initial catalog")
    )

    username = (
        values.get("user")
        or values.get("user id")
        or values.get("uid")
    )

    password = values.get("password", values.get("pwd", ""))

    server = (
        values.get("server")
        or values.get("host")
        or values.get("data source")
    )

    port = values.get("port", "3306")

    ssl_mode = values.get("sslmode", "Required").strip().lower()

    try:
        port = int(port)
    except (TypeError, ValueError):
        raise ValueError("The MySQL port must be a valid number.")

    if not 1 <= port <= 65535:
        raise ValueError("The MySQL port is outside the valid range.")

    return {
        "server": server or "",
        "port": port,
        "database": database or "",
        "user": username or "",
        "password": password,
        "ssl_mode": ssl_mode,
    }


def native_database_config(config):
    values = environment_values(config)
    connection_string = values.get("NATIVE_MYSQL_CONNECTION", "")

    if not connection_string:
        return {}

    return parse_mysql_connection_string(connection_string)


def database_name(config):
    native = native_database_config(config)

    if native.get("database"):
        return native["database"]

    values = environment_values(config)
    configured_name = values.get("MYSQL_DATABASE")

    if configured_name:
        return configured_name

    return config.get("database", {}).get("name", "LGUTreasury")


def valid_database_name(name):
    return bool(re.fullmatch(r"[A-Za-z0-9_$-]+", name or ""))

def compose_command(config, compose_executable, arguments):
    command = list(compose_executable)

    for compose_file in config.get("compose_files", ["compose.native-mysql.yml"]):
        command.extend(["-f", str(project_path(config, compose_file))])

    env_file = config.get("env_file")
    if env_file:
        command.extend(["--env-file", str(project_path(config, env_file))])

    command.extend(arguments)
    return command
