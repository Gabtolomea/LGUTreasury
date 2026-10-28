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
    names.update(["MYSQL_DATABASE", "MYSQL_USER", "MYSQL_PASSWORD", "MYSQL_ROOT_PASSWORD"])
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


def compose_command(config, compose_executable, arguments):
    command = list(compose_executable)
    for compose_file in config.get("compose_files", ["compose.yaml"]):
        command.extend(["-f", str(project_path(config, compose_file))])

    env_file = config.get("env_file")
    if env_file:
        command.extend(["--env-file", str(project_path(config, env_file))])
    command.extend(arguments)
    return command


def database_name(config):
    values = environment_values(config)
    configured_name = values.get("MYSQL_DATABASE")
    if configured_name:
        return configured_name
    return config.get("database", {}).get("name", "LGUTreasury")


def valid_database_name(name):
    return bool(re.fullmatch(r"[A-Za-z0-9_$-]+", name or ""))
