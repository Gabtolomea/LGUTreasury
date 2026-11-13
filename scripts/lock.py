import json
import os
import time
from pathlib import Path


def _pid_is_alive(pid):
    if not isinstance(pid, int) or pid <= 0:
        return False
    if os.name == "nt":
        try:
            import ctypes

            kernel32 = ctypes.windll.kernel32
            process_query_limited_information = 0x1000
            handle = kernel32.OpenProcess(process_query_limited_information, False, pid)
            if not handle:
                return False
            try:
                exit_code = ctypes.c_ulong()
                if kernel32.GetExitCodeProcess(handle, ctypes.byref(exit_code)):
                    return exit_code.value == 259  # STILL_ACTIVE
                return True
            finally:
                kernel32.CloseHandle(handle)
        except Exception:
            return True
    try:
        os.kill(pid, 0)
        return True
    except ProcessLookupError:
        return False
    except PermissionError:
        return True
    except OSError:
        return False


class DeploymentLockError(RuntimeError):
    """Raised when another deployment is already holding the lock."""


class DeploymentLock:
    """Atomic file-based deployment lock with stale-PID cleanup."""

    def __init__(self, lock_path):
        self.lock_path = Path(lock_path)
        self._acquired = False

    def _read_lock_info(self):
        try:
            return json.loads(self.lock_path.read_text(encoding="utf-8"))
        except (OSError, ValueError):
            return {}

    def acquire(self):
        self.lock_path.parent.mkdir(parents=True, exist_ok=True)
        payload = json.dumps(
            {
                "pid": os.getpid(),
                "created_at": time.time(),
            }
        )

        for _ in range(2):
            try:
                fd = os.open(
                    str(self.lock_path),
                    os.O_CREAT | os.O_EXCL | os.O_WRONLY,
                )
                try:
                    os.write(fd, payload.encode("utf-8"))
                finally:
                    os.close(fd)
                self._acquired = True
                return self
            except FileExistsError:
                info = self._read_lock_info()
                pid = info.get("pid")
                if pid and not _pid_is_alive(pid):
                    try:
                        self.lock_path.unlink()
                        continue
                    except OSError:
                        pass
                holder = " (PID {0})".format(pid) if pid else ""
                raise DeploymentLockError(
                    "Another deployment is already in progress{0}. Lock file: {1}".format(
                        holder, self.lock_path
                    )
                )

        raise DeploymentLockError(
            "Could not acquire deployment lock at {0}.".format(self.lock_path)
        )

    def release(self):
        if self._acquired:
            try:
                self.lock_path.unlink()
            except FileNotFoundError:
                pass
            self._acquired = False

    def __enter__(self):
        return self.acquire()

    def __exit__(self, exc_type, exc_val, exc_tb):
        self.release()
        return False
