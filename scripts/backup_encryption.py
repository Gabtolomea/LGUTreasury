import base64
import hashlib
import shutil
import stat
import tempfile
import zipfile

from pathlib import Path, PurePosixPath

from cryptography.fernet import Fernet, InvalidToken
from cryptography.exceptions import InvalidTag
from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
from cryptography.hazmat.primitives.kdf.hkdf import HKDF


DEFAULT_KEY_PATH = Path(
    r"C:\ProgramData\LGUTreasury\secrets\backup.key"
)

MAGIC = b"LGUTREASURY-AESGCM-V2\n"
SALT_SIZE = 16
NONCE_SIZE = 12
TAG_SIZE = 16
CHUNK_SIZE = 1024 * 1024

HKDF_INFO = b"LGUTreasury backup archive AES-256-GCM v2"


def load_encryption_key(key_path=DEFAULT_KEY_PATH):
    key = Path(key_path).read_bytes()

    # Validate that the key is a valid Fernet key.
    Fernet(key)

    return key


def derive_aes_key(fernet_key, salt):
    raw_key = base64.urlsafe_b64decode(fernet_key)

    return HKDF(
        algorithm=hashes.SHA256(),
        length=32,
        salt=salt,
        info=HKDF_INFO,
    ).derive(raw_key)


def calculate_sha256(file_path):
    digest = hashlib.sha256()

    with Path(file_path).open("rb") as file:
        while True:
            chunk = file.read(CHUNK_SIZE)

            if not chunk:
                break

            digest.update(chunk)

    return digest.hexdigest()


def _create_zip(source_dir, archive_path):
    with zipfile.ZipFile(
        archive_path,
        "w",
        compression=zipfile.ZIP_DEFLATED,
        compresslevel=6,
    ) as archive:

        for item in sorted(source_dir.rglob("*")):
            if item.is_symlink():
                raise ValueError(
                    "Backup contains a symbolic link: {0}".format(item)
                )

            if item.is_file():
                archive.write(
                    item,
                    item.relative_to(source_dir).as_posix(),
                )


def _encrypt_file_streaming(
    archive_path,
    encrypted_path,
    fernet_key,
):
    salt = __import__("os").urandom(SALT_SIZE)
    nonce = __import__("os").urandom(NONCE_SIZE)

    aes_key = derive_aes_key(fernet_key, salt)

    encryptor = Cipher(
        algorithms.AES(aes_key),
        modes.GCM(nonce),
    ).encryptor()

    with archive_path.open("rb") as source:
        with encrypted_path.open("wb") as destination:
            destination.write(MAGIC)
            destination.write(salt)
            destination.write(nonce)

            while True:
                chunk = source.read(CHUNK_SIZE)

                if not chunk:
                    break

                encrypted_chunk = encryptor.update(chunk)
                destination.write(encrypted_chunk)

            destination.write(encryptor.finalize())
            destination.write(encryptor.tag)


def _decrypt_file_streaming(
    encrypted_path,
    archive_path,
    fernet_key,
):
    encrypted_path = Path(encrypted_path)

    minimum_size = (
        len(MAGIC) + SALT_SIZE + NONCE_SIZE + TAG_SIZE
    )

    file_size = encrypted_path.stat().st_size

    if file_size < minimum_size:
        raise ValueError("Encrypted backup file is incomplete.")

    with encrypted_path.open("rb") as source:
        if source.read(len(MAGIC)) != MAGIC:
            raise ValueError("Unsupported encrypted backup format.")

        salt = source.read(SALT_SIZE)
        nonce = source.read(NONCE_SIZE)

        if len(salt) != SALT_SIZE or len(nonce) != NONCE_SIZE:
            raise ValueError("Encrypted backup header is incomplete.")

        aes_key = derive_aes_key(fernet_key, salt)

        ciphertext_size = file_size - minimum_size

        source.seek(file_size - TAG_SIZE)
        tag = source.read(TAG_SIZE)

        if len(tag) != TAG_SIZE:
            raise ValueError("Encrypted backup authentication tag is missing.")

        source.seek(len(MAGIC) + SALT_SIZE + NONCE_SIZE)

        decryptor = Cipher(
            algorithms.AES(aes_key),
            modes.GCM(nonce, tag),
        ).decryptor()

        remaining = ciphertext_size

        with Path(archive_path).open("wb") as destination:
            while remaining > 0:
                chunk = source.read(min(CHUNK_SIZE, remaining))

                if not chunk:
                    raise ValueError("Encrypted backup is truncated.")

                remaining -= len(chunk)
                destination.write(decryptor.update(chunk))

            destination.write(decryptor.finalize())


def encrypt_backup_directory(
    backup_dir,
    output_dir=None,
    key_path=DEFAULT_KEY_PATH,
):
    backup_dir = Path(backup_dir).resolve()
    key_path = Path(key_path)

    result = {
        "ok": False,
        "errors": [],
        "encrypted_file": "",
        "sha256": "",
        "size_bytes": 0,
    }

    if not key_path.is_file():
        raise FileNotFoundError(
            "Encryption key file does not exist: {0}".format(key_path)
        )

    try:
        if not backup_dir.is_dir():
            raise FileNotFoundError(
                "Backup directory does not exist."
            )

        if output_dir is None:
            output_dir = backup_dir.parent / "encrypted"

        output_dir = Path(output_dir).resolve()

        if output_dir == backup_dir or backup_dir in output_dir.parents:
            raise ValueError(
                "Encrypted output directory cannot be inside the source backup."
            )

        output_dir.mkdir(parents=True, exist_ok=True)

        fernet_key = load_encryption_key(key_path)

        with tempfile.TemporaryDirectory(
            prefix="lgutreasury-encryption-",
            dir=str(key_path.parent),
        ) as temporary_directory:

            temporary_directory = Path(temporary_directory)

            archive_path = temporary_directory / "backup.zip"
            encrypted_temp = temporary_directory / "backup.zip.enc"
            verification_path = temporary_directory / "verification.zip"

            # Create the ZIP archive without loading it into memory.
            _create_zip(backup_dir, archive_path)

            # Encrypt the ZIP archive in 1 MB chunks.
            _encrypt_file_streaming(
                archive_path,
                encrypted_temp,
                fernet_key,
            )

            # Verify the authentication tag and ZIP integrity.
            _decrypt_file_streaming(
                encrypted_temp,
                verification_path,
                fernet_key,
            )

            with zipfile.ZipFile(verification_path, "r") as archive:
                corrupted_file = archive.testzip()

                if corrupted_file:
                    raise ValueError(
                        "Encrypted backup verification failed: {0}".format(
                            corrupted_file
                        )
                    )

            encrypted_name = (
                backup_dir.name + ".zip.enc"
            )

            final_path = output_dir / encrypted_name

            shutil.copy2(encrypted_temp, final_path)

            result["encrypted_file"] = str(final_path)
            result["sha256"] = calculate_sha256(final_path)
            result["size_bytes"] = final_path.stat().st_size
            result["ok"] = True

        return result

    except MemoryError:
        result["errors"].append(
            "Insufficient memory while processing the backup."
        )
        return result

    except (
        OSError,
        ValueError,
        RuntimeError,
        InvalidToken,
        InvalidTag,
        zipfile.BadZipFile,
    ) as error:
        result["errors"].append(
            "{0}: {1}".format(type(error).__name__, error)
        )
        return result


def _validate_zip_members(archive, restore_dir):
    restore_dir = Path(restore_dir).resolve()

    for member in archive.infolist():
        normalized_name = member.filename.replace("\\", "/")
        member_path = PurePosixPath(normalized_name)

        if (
            member_path.is_absolute()
            or ".." in member_path.parts
            or not member_path.parts
            or ":" in member_path.parts[0]
        ):
            raise ValueError(
                "Unsafe path found in backup archive: {0}".format(
                    member.filename
                )
            )

        target_path = (
            restore_dir / Path(*member_path.parts)
        ).resolve()

        if (
            target_path != restore_dir
            and restore_dir not in target_path.parents
        ):
            raise ValueError(
                "Backup archive contains a path outside the restore directory."
            )

        file_type = (member.external_attr >> 16) & 0o170000

        if file_type == stat.S_IFLNK:
            raise ValueError(
                "Symbolic links are not allowed in backup archives."
            )


def _extract_verified_archive(archive_path, restore_dir):
    with zipfile.ZipFile(archive_path, "r") as archive:
        corrupted_file = archive.testzip()

        if corrupted_file:
            raise ValueError(
                "Backup archive failed its integrity check."
            )

        _validate_zip_members(archive, restore_dir)

        restored_files = sum(
            1 for member in archive.infolist()
            if not member.is_dir()
        )

        archive.extractall(restore_dir)

    return restored_files


def decrypt_backup_archive(
    encrypted_file,
    restore_dir,
    key_path=DEFAULT_KEY_PATH,
):
    encrypted_file = Path(encrypted_file)
    restore_dir = Path(restore_dir)
    key_path = Path(key_path)

    result = {
        "ok": False,
        "errors": [],
        "restore_dir": str(restore_dir),
        "files_restored": 0,
    }

    if not encrypted_file.is_file():
        raise FileNotFoundError(
            "Encrypted backup file does not exist."
        )

    if restore_dir.exists() and any(restore_dir.iterdir()):
        raise ValueError(
            "Restore directory must be empty."
        )

    restore_dir.mkdir(parents=True, exist_ok=True)

    try:
        fernet_key = load_encryption_key(key_path)

        with encrypted_file.open("rb") as source:
            file_header = source.read(len(MAGIC))

        with tempfile.TemporaryDirectory(
            prefix="lgutreasury-decryption-",
            dir=str(key_path.parent),
        ) as temporary_directory:

            archive_path = (
                Path(temporary_directory) / "restored.zip"
            )

            if file_header == MAGIC:
                # New streaming AES-256-GCM format.
                _decrypt_file_streaming(
                    encrypted_file,
                    archive_path,
                    fernet_key,
                )

            else:
                # Backward compatibility for older Fernet archives.
                legacy_data = Fernet(fernet_key).decrypt(
                    encrypted_file.read_bytes()
                )

                archive_path.write_bytes(legacy_data)

            result["files_restored"] = _extract_verified_archive(
                archive_path,
                restore_dir,
            )

        result["ok"] = True
        return result

    except (
        OSError,
        ValueError,
        RuntimeError,
        InvalidToken,
        InvalidTag,
        zipfile.BadZipFile,
    ) as error:
        result["errors"].append(
            "{0}: {1}".format(type(error).__name__, error)
        )

        return result