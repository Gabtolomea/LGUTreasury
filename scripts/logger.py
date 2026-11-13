import os
import re
import sys
from datetime import datetime
from pathlib import Path


SECRET_PATTERN = re.compile(
    r"(?i)(password|passwd|token|secret|api[_-]?key|private[_-]?key)\s*[:=]\s*([^\s,;]+)"
)


def redact_text(text, secret_values=None):
    if text is None:
        return ""

    redacted = str(text)
    for value in secret_values or []:
        if value:
            redacted = redacted.replace(str(value), "[REDACTED]")
    return SECRET_PATTERN.sub(r"\1=[REDACTED]", redacted)


DEFAULT_MAX_LOG_BYTES = 5 * 1024 * 1024
DEFAULT_LOG_BACKUP_COUNT = 5


def _rotate_log_if_needed(log_path, max_bytes, backup_count):
    if max_bytes <= 0 or backup_count <= 0 or not log_path.exists():
        return
    try:
        if log_path.stat().st_size < max_bytes:
            return
        for index in range(backup_count, 0, -1):
            src = Path("{0}.{1}".format(log_path, index - 1)) if index > 1 else log_path
            dst = Path("{0}.{1}".format(log_path, index))
            if src.exists():
                if dst.exists():
                    dst.unlink()
                src.replace(dst)
    except OSError:
        pass


class Logger:
    def __init__(
        self,
        log_path,
        stream=None,
        secret_values=None,
        verbose=False,
        max_bytes=DEFAULT_MAX_LOG_BYTES,
        backup_count=DEFAULT_LOG_BACKUP_COUNT,
    ):
        self.log_path = Path(log_path)
        self.log_path.parent.mkdir(parents=True, exist_ok=True)
        _rotate_log_if_needed(self.log_path, max_bytes, backup_count)
        self.file = self.log_path.open("a", encoding="utf-8")
        self.stream = stream or sys.stdout
        self.secret_values = secret_values or []
        self.use_color = self.stream.isatty() and not os.environ.get("NO_COLOR")
        self.verbose = verbose

    def close(self):
        if not self.file.closed:
            self.file.close()

    def _write(self, level, message, terminal=True):
        clean_message = redact_text(message, self.secret_values)
        level_label = "[{0}]".format(level)
        line = "{0} {1}".format(level_label, clean_message)
        timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        self.file.write("[{0}] {1}\n".format(timestamp, line))
        self.file.flush()

        if not terminal:
            return

        terminal_line = line
        if self.use_color:
            color = ""
            if level == "INFO":
                color = "\033[36m"
            elif level in (" OK ", "DONE"):
                color = "\033[32m"
            elif level == "WARN":
                color = "\033[33m"
            elif level == "FAIL":
                color = "\033[31m"
            terminal_line = color + level_label + "\033[0m " + clean_message
        self.stream.write(terminal_line + "\n")
        self.stream.flush()

    def info(self, message):
        self._write("INFO", message)

    def ok(self, message):
        self._write(" OK ", message)

    def done(self, message):
        self._write("DONE", message)

    def warning(self, message):
        self._write("WARN", message)

    def fail(self, message):
        self._write("FAIL", message)

    def detail(self, message, level="INFO"):
        """Keep the message in the log file and show it in the terminal only when verbose."""
        self._write(level, message, terminal=self.verbose)
