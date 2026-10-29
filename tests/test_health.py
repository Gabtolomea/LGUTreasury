import json
import unittest
from unittest.mock import patch

from scripts.commands import CommandResult
from scripts.health import run_health_checks


class RecordingLogger:
    def __init__(self):
        self.messages = []

    def info(self, message):
        self.messages.append(("info", message))

    def ok(self, message):
        self.messages.append(("ok", message))

    def fail(self, message):
        self.messages.append(("fail", message))

    def warning(self, message):
        self.messages.append(("warning", message))

    def success(self, message):
        self.messages.append(("success", message))


class ContainerHealthRunner:
    def __init__(self, status, probe_output=""):
        if isinstance(status, list):
            self.statuses = status
        else:
            self.statuses = [status]
        self.probe_output = probe_output
        self.commands = []
        self.inspect_calls = 0

    def __call__(self, command, cwd=None, timeout=None):
        self.commands.append(list(command))
        if "ps" in command and "app" in command:
            return CommandResult(0, "app-container\n", "")
        if command[:2] == ["docker", "inspect"]:
            status_index = self.inspect_calls
            if status_index >= len(self.statuses):
                status_index = len(self.statuses) - 1
            status = self.statuses[status_index]
            self.inspect_calls += 1
            state = {
                "Status": "running",
                "Health": {
                    "Status": status,
                    "Log": [{"Output": self.probe_output}],
                },
            }
            return CommandResult(0, json.dumps(state), "")
        return CommandResult(0, "", "")


class HealthCheckTests(unittest.TestCase):
    def make_config(self, retries):
        return {
            "project_dir": ".",
            "compose_files": ["compose.yaml"],
            "health_checks": [
                {
                    "name": "Application container health",
                    "type": "container",
                    "retries": retries,
                    "interval": 0,
                }
            ],
        }

    @patch("scripts.health.time.sleep")
    def test_container_retries_do_not_log_each_poll(self, unused_sleep):
        logger = RecordingLogger()
        command_runner = ContainerHealthRunner(["starting", "healthy"])

        result = run_health_checks(
            self.make_config(retries=3),
            ["docker", "compose"],
            command_runner,
            logger,
        )

        self.assertTrue(result["ok"])
        command_messages = [
            message
            for level, message in logger.messages
            if level == "info" and message.startswith("Running the command")
        ]
        self.assertEqual(command_messages, [])
        self.assertEqual(
            [message for level, message in logger.messages if level == "ok"],
            ["Application container health passed."],
        )

    def test_unhealthy_container_includes_last_probe_output(self):
        logger = RecordingLogger()
        command_runner = ContainerHealthRunner(
            "unhealthy",
            "curl: (7) Failed to connect to localhost port 8080\n",
        )

        result = run_health_checks(
            self.make_config(retries=1),
            ["docker", "compose"],
            command_runner,
            logger,
        )

        self.assertFalse(result["ok"])
        self.assertIn("curl: (7) Failed to connect", result["errors"][0])


if __name__ == "__main__":
    unittest.main()
