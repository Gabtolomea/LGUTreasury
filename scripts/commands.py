import shlex
import shutil
import subprocess


class CommandResult:
    def __init__(self, returncode, stdout, stderr):
        self.returncode = returncode
        self.stdout = stdout
        self.stderr = stderr

    def ok(self):
        return self.returncode == 0


def _text(value):
    if value is None:
        return ""
    if isinstance(value, bytes):
        return value.decode(errors="replace")
    return str(value)


def run_command(command, cwd=None, timeout=None, input=None):
    try:
        completed = subprocess.run(
            command,
            cwd=cwd,
            input=input,
            capture_output=True,
            text=True,
            shell=False,
            timeout=timeout,
            check=False,
        )
        return CommandResult(
            completed.returncode,
            completed.stdout or "",
            completed.stderr or "",
        )
    except FileNotFoundError as error:
        return CommandResult(127, "", str(error))
    except subprocess.TimeoutExpired as error:
        error_text = _text(error.stderr)
        if error_text:
            error_text += " "
        error_text += "The command timed out."
        return CommandResult(124, _text(error.stdout), error_text)


def find_compose_command(command_runner, project_dir):
    docker_path = shutil.which("docker")
    if docker_path:
        result = command_runner(["docker", "compose", "version"], project_dir, 30)
        if result.ok():
            return ["docker", "compose"]

    compose_path = shutil.which("docker-compose")
    if compose_path:
        result = command_runner(["docker-compose", "version"], project_dir, 30)
        if result.ok():
            return ["docker-compose"]
    return None


def describe_command(command):
    return shlex.join([str(item) for item in command])


def execute_command(command_runner, command, cwd=None, timeout=None, input=None):
    try:
        return command_runner(command, cwd=cwd, timeout=timeout, input=input)
    except TypeError:
        try:
            return command_runner(command, cwd, timeout)
        except TypeError:
            return command_runner(command)


def run_logged_command(command_runner, command, cwd, timeout, logger, description, input=None):
    logger.detail("Running the command `{0}`.".format(describe_command(command)))
    result = execute_command(command_runner, command, cwd, timeout, input)
    if result.ok():
        logger.detail("{0} completed successfully.".format(description), level=" OK ")
        return result

    details = result.stderr.strip()
    if result.returncode == 124:
        message = "{0} timed out after {1}s.".format(description, timeout)
        if details:
            message += " The command reported: {0}.".format(details)
        logger.fail(message)
        return result

    if details:
        logger.fail("{0} failed. The command reported: {1}.".format(description, details))
    else:
        logger.fail("{0} failed with exit code {1}.".format(description, result.returncode))
    return result
