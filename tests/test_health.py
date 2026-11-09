import json
import unittest
from unittest.mock import patch

from scripts.commands import CommandResult
from scripts.health import _container_health_details, run_health_checks


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
    def __init__(self, statuses, probe_output=""):
        self.statuses = statuses if isinstance(statuses, list) else [statuses]
        self.probe_output = probe_output
        self.commands = []
        self.inspect_calls = 0

    def __call__(self, command, cwd=None, timeout=None):
        self.commands.append((list(command), timeout))
        if "ps" in command and "app" in command:
            return CommandResult(0, "app-container\n", "")
        if command[:2] == ["docker", "inspect"]:
            status_index = min(self.inspect_calls, len(self.statuses) - 1)
            status = self.statuses[status_index]
            self.inspect_calls += 1
            state = {"Status": "running"}
            if status is not None:
                state["Health"] = {
                    "Status": status,
                    "Log": [{"Output": self.probe_output}],
                }
            return CommandResult(0, json.dumps(state), "")
        return CommandResult(0, "", "")


class HealthCheckTests(unittest.TestCase):
    def make_config(self, retries=3):
        return {
            "project_dir": ".",
            "compose_files": ["compose.native-mysql.yml"],
            "health_check_timeout": 30,
            "health_checks": [
                {
                    "name": "Application container health",
                    "type": "container",
                    "retries": retries,
                    "interval": 0,
                    "timeout": 4,
                }
            ],
        }

    @patch("scripts.health.time.sleep")
    def test_container_retries_until_healthy_and_reports_attempts(self, unused_sleep):
        logger = RecordingLogger()
        command_runner = ContainerHealthRunner(["starting", "healthy"])

        result = run_health_checks(
            self.make_config(retries=3), ["docker", "compose"], command_runner, logger
        )

        self.assertTrue(result["ok"])
        self.assertEqual(result["checks"][0]["attempts"], 2)
        self.assertEqual(result["checks"][0]["status"], "passed")
        self.assertEqual(command_runner.inspect_calls, 2)
        self.assertTrue(any("2 attempt(s)" in message for level, message in logger.messages if level == "ok"))

    def test_unhealthy_container_includes_last_probe_output(self):
        logger = RecordingLogger()
        command_runner = ContainerHealthRunner(
            "unhealthy", "curl: (7) Failed to connect to localhost port 8080\n"
        )

        result = run_health_checks(
            self.make_config(retries=1), ["docker", "compose"], command_runner, logger
        )

        self.assertFalse(result["ok"])
        self.assertIn("Failed to connect", result["errors"][0])
        self.assertEqual(result["checks"][0]["attempts"], 1)

    def test_running_container_without_healthcheck_is_not_healthy(self):
        logger = RecordingLogger()
        command_runner = ContainerHealthRunner(None)

        result = run_health_checks(
            self.make_config(retries=1), ["docker", "compose"], command_runner, logger
        )

        self.assertFalse(result["ok"])
        self.assertIn("no Docker HEALTHCHECK", result["errors"][0])

    def test_health_details_rejects_invalid_json(self):
        self.assertEqual(_container_health_details("not-json"), ("", ""))

    @patch("scripts.health.urlopen")
    def test_url_check_uses_configured_timeout(self, mock_urlopen):
        class Response:
            status = 200

            def __enter__(self):
                return self

            def __exit__(self, *args):
                return False

        mock_urlopen.return_value = Response()
        logger = RecordingLogger()
        config = {
            "project_dir": ".",
            "health_checks": [{
                "name": "Readiness endpoint",
                "type": "url",
                "url": "http://localhost:8080/health/ready",
                "timeout": 5,
                "retries": 1,
                "interval": 0,
            }],
        }

        result = run_health_checks(config, ["docker", "compose"], lambda *args: None, logger)

        self.assertTrue(result["ok"])
        self.assertEqual(mock_urlopen.call_args.kwargs["timeout"], 5)

    def test_missing_health_checks_fails_closed(self):
        logger = RecordingLogger()
        result = run_health_checks(
            {"project_dir": ".", "health_checks": []},
            ["docker", "compose"], lambda *args: None, logger
        )
        self.assertFalse(result["ok"])
        self.assertEqual(result["checks"], [])


if __name__ == "__main__":
    unittest.main()
