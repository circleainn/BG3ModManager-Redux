import hashlib
import json
import re
import shutil
import sys
import zipfile
from datetime import datetime, timezone
from pathlib import Path


ROOT = Path(__file__).resolve().parent
PUBLISH_DIR = ROOT / "bin" / "Publish"
RELEASE_INVENTORY_NAME = "Redux-Release-Files.json"

version = sys.argv[1].strip() if len(sys.argv) > 1 else ""
if not version:
	raise SystemExit("A display version is required (for example: 0.1.0-alpha.15).")

archive_path = ROOT / f"BG3ModManager-Redux_v{version}.zip"
latest_path = ROOT / "BG3ModManager-Redux-Latest.zip"
update_manifest_path = ROOT / "Redux-Update-Public-Alpha.json"

THIRD_PARTY_LICENSE_FILES = (
	Path("CrossSpeak-LGPL-2.1.txt"),
	Path("Lucide-ISC.txt"),
	Path("LSLib-MIT.txt"),
	Path("Manrope-OFL-1.1.txt"),
	Path("Atkinson-Hyperlegible-OFL-1.1.txt"),
	Path("ArchivoBlack-OFL-1.1.txt"),
	Path("IBMPlexMono-OFL-1.1.txt"),
	Path("NewtonsoftJson-MIT.txt"),
	Path("ImageSharp-Split-License.txt"),
)

USER_STATE_DIRECTORIES = {
	"data",
	"orders",
	"currentorders",
	"_logs",
	"logs",
	"cache",
	"_cache",
	"backup",
	"_backup",
	"gamedirectoryinstalls",
	"restorepoints",
	"temp",
}

FORBIDDEN_FILE_NAMES = {
	"settings.json",
	"keybindings.json",
	"scriptextendersettings.json",
	"lastexported.json",
	"mod-annotations.json",
	"hosts.yml",
	".env",
}

FORBIDDEN_SUFFIXES = {
	".bak",
	".bg3redux",
	".bg3redux-report",
	".binlog",
	".dmp",
	".dump",
	".log",
	".p12",
	".pdb",
	".pem",
	".pfx",
	".suo",
	".tmp",
	".user",
}

REQUIRED_FILES = {
	Path("Redux.exe"),
	Path("Redux.dll"),
	Path("Redux.deps.json"),
	Path("Redux.runtimeconfig.json"),
	Path("_Lib/LSLib.dll"),
	Path("_Lib/LSLibNative.dll"),
	Path("_Lib/Ijwhost.dll"),
	Path("LICENSE"),
	Path("README.md"),
	Path("THIRD-PARTY-NOTICES.md"),
	Path("Resources/Fonts/Manrope-Regular.ttf"),
	Path("Resources/Fonts/AtkinsonHyperlegible-Regular.ttf"),
	Path("Resources/Fonts/ArchivoBlack-Regular.ttf"),
	Path("Resources/Fonts/IBMPlexMono-Regular.ttf"),
	Path("Updater/ReduxUpdater.exe"),
	Path("Updater/ReduxUpdater.dll"),
	Path("Updater/ReduxUpdater.deps.json"),
	Path("Updater/ReduxUpdater.runtimeconfig.json"),
}

BINARY_SUFFIXES = {".dll", ".exe"}
NEUTRAL_BUILD_ROOT = r"R:\BG3ModManager-Redux"


def remove_path(path: Path) -> None:
	if path.is_dir():
		shutil.rmtree(path)
	elif path.exists():
		path.unlink()


def prepare_publish_directory() -> None:
	if not PUBLISH_DIR.is_dir():
		raise SystemExit(f"Publish output was not found: {PUBLISH_DIR}")

	for child in list(PUBLISH_DIR.iterdir()):
		if child.is_dir() and child.name.lower() in USER_STATE_DIRECTORIES:
			remove_path(child)
	remove_path(PUBLISH_DIR / RELEASE_INVENTORY_NAME)
	# AssemblyName changed for alpha.15. Never let stale pre-rename runtime files leak into a
	# package when publishing over an existing output directory.
	for legacy_runtime_name in (
		"BG3ModManager.exe",
		"BG3ModManager.dll",
		"BG3ModManager.deps.json",
		"BG3ModManager.runtimeconfig.json",
		"BG3ModManager.pdb",
	):
		remove_path(PUBLISH_DIR / legacy_runtime_name)
	# UpdaterPayload is an MSBuild intermediate. Only the curated Updater directory is shipped.
	remove_path(PUBLISH_DIR / "UpdaterPayload")
	for stale_updater_root_file in PUBLISH_DIR.glob("ReduxUpdater.*"):
		remove_path(stale_updater_root_file)
	remove_path(PUBLISH_DIR / "_Lib" / "ReduxUpdater.dll")

	distribution_documents = {
		ROOT / "README.md": PUBLISH_DIR / "README.md",
		ROOT / "LICENSE": PUBLISH_DIR / "LICENSE",
	}
	for source, destination in distribution_documents.items():
		if not source.is_file():
			raise SystemExit(f"Required distribution document is missing: {source}")
		shutil.copy2(source, destination)

	# Combine the readable attribution summary and the full license texts retained in this
	# repository into one Markdown document. Individual source files remain in the repository for
	# provenance and maintenance, while packaged builds expose one unambiguous third-party document.
	notice_source = ROOT / "licenses" / "Third-Party-Notices.md"
	if not notice_source.is_file():
		raise SystemExit(f"Required third-party notice is missing: {notice_source}")

	remove_path(PUBLISH_DIR / "THIRD-PARTY-LICENSES.txt")
	license_sections = [
		notice_source.read_bytes().rstrip(b"\r\n")
		+ b"\r\n\r\n# Retained Third-Party License Texts\r\n"
		+ b"\r\nThe full text of each license file retained in this repository follows. "
		+ b"See the inventory above for every packaged runtime project and its upstream terms.\r\n"
	]
	for license_name in THIRD_PARTY_LICENSE_FILES:
		source = ROOT / "licenses" / license_name
		if not source.is_file():
			raise SystemExit(f"Required dependency license is missing: {source}")
		license_sections.append(
			b"\r\n## " + license_name.as_posix().encode("utf-8") + b"\r\n\r\n```text\r\n"
			+ source.read_bytes().rstrip(b"\r\n") + b"\r\n```\r\n"
		)
	(PUBLISH_DIR / "THIRD-PARTY-NOTICES.md").write_bytes(b"".join(license_sections))

	# Keep the release layout unambiguous: all distribution documents live at root.
	remove_path(PUBLISH_DIR / "licenses")
	for stale_font in (PUBLISH_DIR / "Resources" / "Fonts").glob("CrimsonPro-*"):
		remove_path(stale_font)


def replace_fixed_width(data: bytes, old: bytes, new: bytes) -> tuple[bytes, int]:
	"""Replace build-only paths without changing the binary's length or offsets."""
	if not old or old not in data:
		return data, 0
	if len(new) > len(old):
		raise SystemExit("The neutral build path must not be longer than the real build path.")
	replacement = new + (b"\0" * (len(old) - len(new)))
	return data.replace(old, replacement), data.count(old)


def sanitize_windows_pdb_paths(data: bytes) -> tuple[bytes, int]:
	"""Replace compiler PDB lookup paths that expose a developer profile directory."""
	pattern = re.compile(rb"[A-Za-z]:\\Users\\[^\x00\r\n]{1,768}?\.pdb\x00", re.IGNORECASE)
	replacement_path = b"R:\\Build\\dependency.pdb\x00"
	count = 0

	def replace(match: re.Match[bytes]) -> bytes:
		nonlocal count
		count += 1
		value = match.group(0)
		if len(replacement_path) > len(value):
			return b"\x00" * len(value)
		return replacement_path + (b"\x00" * (len(value) - len(replacement_path)))

	return pattern.sub(replace, data), count


def sanitize_binary_build_metadata() -> None:
	"""Remove machine-specific paths left in PE/PDB lookup metadata by compilers."""
	real_root = str(ROOT)
	encodings = (
		(real_root.encode("utf-8"), NEUTRAL_BUILD_ROOT.encode("utf-8")),
		(real_root.encode("utf-16-le"), NEUTRAL_BUILD_ROOT.encode("utf-16-le")),
	)

	for path in PUBLISH_DIR.rglob("*"):
		if not path.is_file() or path.suffix.lower() not in BINARY_SUFFIXES:
			continue
		data = path.read_bytes()
		changed = 0
		for old, new in encodings:
			data, count = replace_fixed_width(data, old, new)
			changed += count
		data, count = sanitize_windows_pdb_paths(data)
		changed += count
		if changed:
			path.write_bytes(data)


def private_path_markers() -> tuple[str, ...]:
	"""Return machine-specific roots that must never appear in a distribution."""
	candidates = {
		str(ROOT),
		ROOT.as_posix(),
		str(Path.home()),
		Path.home().as_posix(),
		"c:\\users\\",
		"c:/users/",
	}
	return tuple(sorted(marker.lower() for marker in candidates if marker))


def validate_package_privacy(files: list[Path]) -> None:
	markers = private_path_markers()
	for path in files:
		data = path.read_bytes().lower()
		for marker in markers:
			encoded_markers = (marker.encode("utf-8"), marker.encode("utf-16-le"))
			if any(encoded in data for encoded in encoded_markers):
				raise SystemExit(
					"Private build-path metadata was found in "
					f"{path.relative_to(PUBLISH_DIR)}."
				)


def collect_package_files() -> list[Path]:
	files: list[Path] = []
	for path in sorted(PUBLISH_DIR.rglob("*")):
		if not path.is_file():
			continue

		relative_path = path.relative_to(PUBLISH_DIR)
		lower_parts = {part.lower() for part in relative_path.parts[:-1]}
		lower_name = relative_path.name.lower()

		if lower_parts.intersection(USER_STATE_DIRECTORIES):
			raise SystemExit(f"User-state directory cannot be packaged: {relative_path}")
		if lower_name in FORBIDDEN_FILE_NAMES:
			raise SystemExit(f"Private or runtime file cannot be packaged: {relative_path}")
		if lower_name.startswith(".env."):
			raise SystemExit(f"Environment file cannot be packaged: {relative_path}")
		if relative_path.suffix.lower() in FORBIDDEN_SUFFIXES:
			raise SystemExit(f"Development or temporary file cannot be packaged: {relative_path}")

		files.append(path)

	available = {path.relative_to(PUBLISH_DIR) for path in files}
	missing = sorted(REQUIRED_FILES - available)
	if missing:
		raise SystemExit("Required package files are missing: " + ", ".join(map(str, missing)))

	return files


def write_archive(files: list[Path]) -> None:
	remove_path(archive_path)
	remove_path(latest_path)

	with zipfile.ZipFile(archive_path, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
		for path in files:
			archive.write(path, arcname=path.relative_to(PUBLISH_DIR).as_posix())

	shutil.copy2(archive_path, latest_path)


def write_release_inventory(files: list[Path]) -> None:
	"""Declare the exact files owned by Redux so updates never infer ownership."""
	paths = sorted(
		[path.relative_to(PUBLISH_DIR).as_posix() for path in files]
		+ [RELEASE_INVENTORY_NAME],
		key=str.casefold,
	)
	inventory = {
		"schemaVersion": 1,
		"files": paths,
	}
	(PUBLISH_DIR / RELEASE_INVENTORY_NAME).write_text(
		json.dumps(inventory, indent=2) + "\n",
		encoding="utf-8",
	)


def internal_version(display_version: str) -> str:
	match = re.fullmatch(
		r"0\.1\.0-alpha\.([1-9][0-9]*)(?:\.([1-9][0-9]*)(?:\.([1-9][0-9]*))?)?",
		display_version,
	)
	if not match:
		raise SystemExit(
			"Publish versions must use the 0.1.0-alpha.N, 0.1.0-alpha.N.H, "
			"or 0.1.0-alpha.N.H.M format."
		)
	alpha = int(match.group(1))
	hotfix = match.group(2)
	maintenance = match.group(3)
	if maintenance is not None:
		maintenance_number = int(maintenance)
		if maintenance_number >= 100:
			raise SystemExit("Maintenance release components must be between 1 and 99.")
		encoded_revision = (int(hotfix) * 100) + maintenance_number
		if encoded_revision > 65535:
			raise SystemExit("The encoded maintenance version exceeds the supported internal range.")
		return f"0.1.{alpha}.{encoded_revision}"
	if hotfix is not None:
		hotfix_number = int(hotfix)
		# Alpha.16.1 through alpha.16.3 retain their already-published flat revisions.
		if alpha == 16 and hotfix_number <= 3:
			return f"0.1.{alpha}.{hotfix_number}"
		encoded_revision = hotfix_number * 100
		if encoded_revision > 65535:
			raise SystemExit("The encoded hotfix version exceeds the supported internal range.")
		return f"0.1.{alpha}.{encoded_revision}"
	# Alpha.15 and alpha.16 were published before the hotfix-aware mapping existed.
	if alpha in (15, 16):
		return f"0.1.0.{alpha}"
	return f"0.1.{alpha}.0"


def sha256(path: Path) -> str:
	digest = hashlib.sha256()
	with path.open("rb") as stream:
		for chunk in iter(lambda: stream.read(1024 * 1024), b""):
			digest.update(chunk)
	return digest.hexdigest()


def write_update_manifest() -> None:
	"""Create release metadata consumed by Redux's strict public-alpha channel parser."""
	artifact_name = archive_path.name
	manifest = {
		"schemaVersion": 1,
		"channel": "public-alpha",
		"displayVersion": version,
		"internalVersion": internal_version(version),
		"publishedAtUtc": datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z"),
		"releaseNotesUrl": (
			"https://github.com/circleainn/BG3ModManager-Redux/releases/tag/"
			f"v{version}"
		),
		"artifacts": [
			{
				"kind": "portable",
				"url": (
					"https://github.com/circleainn/BG3ModManager-Redux/releases/download/"
					f"v{version}/{artifact_name}"
				),
				"sizeBytes": archive_path.stat().st_size,
				"sha256": sha256(archive_path),
			}
		],
	}
	update_manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")


prepare_publish_directory()
sanitize_binary_build_metadata()
package_files = collect_package_files()
write_release_inventory(package_files)
package_files = collect_package_files()
validate_package_privacy(package_files)
write_archive(package_files)
write_update_manifest()

print(f"Created {archive_path.name} with {len(package_files)} files.")
print(f"Updated {latest_path.name}.")
print(f"Created {update_manifest_path.name} with SHA-256 and byte-length verification metadata.")
