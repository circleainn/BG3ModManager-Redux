"""Record local package provenance separately from the strict public channel manifest."""
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import subprocess


def file_identity(path: Path) -> dict:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return {"filename": path.name, "sizeBytes": path.stat().st_size, "sha256": digest.hexdigest()}


def source_identity(root: Path) -> dict:
    try:
        commit = subprocess.check_output(
            ["git", "rev-parse", "HEAD"], cwd=root, text=True, stderr=subprocess.DEVNULL
        ).strip()
        status = subprocess.check_output(
            ["git", "status", "--porcelain", "--untracked-files=normal"],
            cwd=root, text=True, stderr=subprocess.DEVNULL,
        )
        return {"commit": commit, "dirty": bool(status.strip())}
    except (OSError, subprocess.CalledProcessError):
        return {"commit": None, "dirty": None}


def write_provenance(root: Path, version: str, archive: Path, manifest: Path) -> Path:
    # Do not serialize source paths, branch names, remote URLs, or status output.
    record = {
        "schemaVersion": 1,
        "purpose": "local-candidate-not-publication",
        "displayVersion": version,
        "createdAtUtc": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        "source": source_identity(root),
        "archive": file_identity(archive),
        "manifest": file_identity(manifest),
    }
    destination = root / "Redux-Build-Provenance.json"
    destination.write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
    return destination
