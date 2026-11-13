import os
import re
import sys
import threading
from contextlib import contextmanager
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
SPINNER_FRAMES = ("⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏")
ASCII_SPINNER_FRAMES = ("|", "/", "-", "\\")


def _supports_unicode(stream):
    encoding = getattr(stream, "encoding", None) or "utf-8"
    try:
        "".join(SPINNER_FRAMES).encode(encoding)
        return True
    except (UnicodeEncodeError, LookupError):
        return False


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
        self._lock = threading.Lock()
        self._spinner_active = False
        self._spinner_line_len = 0
        self._spinner_frames = (
            SPINNER_FRAMES if _supports_unicode(self.stream) else ASCII_SPINNER_FRAMES
        )

    def close(self):
        with self._lock:
            if not self.file.closed:
                self.file.close()

    def _clear_spinner_line_locked(self):
        if self._spinner_line_len > 0:
            self.stream.write("\r" + (" " * self._spinner_line_len) + "\r")
            self.stream.flush()
            self._spinner_line_len = 0

    def _write(self, level, message, terminal=True):
        clean_message = redact_text(message, self.secret_values)
        level_label = "[{0}]".format(level)
        line = "{0} {1}".format(level_label, clean_message)
        timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        with self._lock:
            self.file.write("[{0}] {1}\n".format(timestamp, line))
            self.file.flush()

            if not terminal:
                return

            if self._spinner_active:
                self._clear_spinner_line_locked()

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

    @contextmanager
    def spinner(self, message, interval=0.08):
        """Show a smooth circular dot spinner on interactive terminals."""
        if not self.stream.isatty():
            yield
            return

        clean_message = redact_text(message, self.secret_values)
        stop_event = threading.Event()

        def _animate():
            index = 0
            while not stop_event.is_set():
                frame = self._spinner_frames[index % len(self._spinner_frames)]
                plain = "{0} {1}".format(frame, clean_message)
                with self._lock:
                    if stop_event.is_set():
                        break
                    if self.use_color:
                        rendered = "\033[36m{0}\033[0m {1}".format(frame, clean_message)
                    else:
                        rendered = plain
                    pad = max(0, self._spinner_line_len - len(plain))
                    self.stream.write("\r" + rendered + (" " * pad))
                    self.stream.flush()
                    self._spinner_line_len = len(plain)
                index += 1
                if stop_event.wait(interval):
                    break

        with self._lock:
            self._spinner_active = True
        worker = threading.Thread(target=_animate, daemon=True)
        worker.start()
        try:
            yield
        finally:
            stop_event.set()
            worker.join(timeout=1.0)
            with self._lock:
                self._clear_spinner_line_locked()
                self._spinner_active = False

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
