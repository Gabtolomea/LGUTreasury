import unittest

from scripts.backup import _wait_for_database
from scripts.commands import CommandResult


class RecordingLogger:
    def __init__(self):
        self.messages = []

    def info(self, message):
        self.messages.append(("info", message))

    def ok(self, message):
        self.messages.append(("ok", message))

    def fail(self, message):
        self.messages.append(("fail", message))


class DatabaseReadinessRunner:
    def __init__(self):
        self.commands = []
        self.inspect_calls = 0

    def __call__(self, command, cwd=None, timeout=None):
        self.commands.append(list(command))
        if "up" in command and "db" in command:
            return CommandResult(0, "", "")
        if "ps" in command and "db" in command:
            return CommandResult(0, "db-container\n", "")
        if command[:2] == ["docker", "inspect"]:
            self.inspect_calls += 1
            if self.inspect_calls == 1:
                return CommandResult(0, "starting\n", "")
            return CommandResult(0, "healthy\n", "")
        return CommandResult(0, "", "")


class DatabaseReadinessTests(unittest.TestCase):
    def test_readiness_retries_do_not_log_each_poll(self):
        logger = RecordingLogger()
        command_runner = DatabaseReadinessRunner()
        config = {
            "project_dir": ".",
            "compose_files": ["compose.yaml"],
            "database": {
                "service": "db",
                "readiness_retries": 3,
                "readiness_interval": 0,
            },
        }

        result = _wait_for_database(
            config,
            ["docker", "compose"],
            command_runner,
            logger,
        )

        self.assertTrue(result["ok"])
        poll_messages = [
            message
            for level, message in logger.messages
            if "Database container lookup" in message
            or "Database readiness check" in message
        ]
        self.assertEqual(poll_messages, [])


if __name__ == "__main__":
    unittest.main()
