import shutil
import sys
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parent
PUBLISH_DIR = ROOT / "bin" / "Publish"

version = sys.argv[1].strip() if len(sys.argv) > 1 else ""
if not version:
	raise SystemExit("A display version is required (for example: 0.1.0-alpha.4).")

archive_path = ROOT / f"BG3ModManager-Redux_v{version}.zip"
latest_path = ROOT / "BG3ModManager-Redux-Latest.zip"

THIRD_PARTY_LICENSE_FILES = (
	Path("CrossSpeak-LGPL-2.1.txt"),
	Path("Ionicons-MIT.txt"),
	Path("LSLib-MIT.txt"),
	Path("Manrope-OFL-1.1.txt"),
	Path("Atkinson-Hyperlegible-OFL-1.1.txt"),
	Path("Monaspace-OFL-1.1.txt"),
	Path("Minipax-OFL-1.1.txt"),
	Path("Chivo-OFL-1.1.txt"),
	Path("Tabler-Icons-MIT.txt"),
)

USER_STATE_DIRECTORIES = {
	"data",
	"orders",
	"_logs",
	"logs",
	"cache",
	"_cache",
	"backup",
	"_backup",
}

FORBIDDEN_FILE_NAMES = {
	"settings.json",
	"keybindings.json",
	"scriptextendersettings.json",
	"lastexported.json",
	"hosts.yml",
	".env",
}

FORBIDDEN_SUFFIXES = {
	".bak",
	".binlog",
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
	Path("BG3ModManager.exe"),
	Path("BG3ModManager.dll"),
	Path("BG3ModManager.deps.json"),
	Path("BG3ModManager.runtimeconfig.json"),
	Path("_Lib/LSLib.dll"),
	Path("_Lib/LSLibNative.dll"),
	Path("_Lib/Ijwhost.dll"),
	Path("LICENSE"),
	Path("README.md"),
	Path("THIRD-PARTY-NOTICES.md"),
	Path("THIRD-PARTY-LICENSES.txt"),
	Path("Resources/Fonts/Manrope-Regular.ttf"),
	Path("Resources/Fonts/AtkinsonHyperlegible-Regular.ttf"),
	Path("Resources/Fonts/MonaspaceNeon-Regular.otf"),
	Path("Resources/Fonts/Minipax-Regular.ttf"),
	Path("Resources/Fonts/Chivo-Regular.ttf"),
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

	distribution_documents = {
		ROOT / "README.md": PUBLISH_DIR / "README.md",
		ROOT / "LICENSE": PUBLISH_DIR / "LICENSE",
		ROOT / "licenses" / "Third-Party-Notices.md": PUBLISH_DIR / "THIRD-PARTY-NOTICES.md",
	}
	for source, destination in distribution_documents.items():
		if not source.is_file():
			raise SystemExit(f"Required distribution document is missing: {source}")
		shutil.copy2(source, destination)

	# Preserve each dependency's complete source license in one distribution document.
	# Individual files remain in the repository for provenance and easy maintenance.
	license_sections = [
		b"BG3 MOD MANAGER REDUX - THIRD-PARTY LICENSES\r\n"
		b"The complete license text for each bundled dependency follows.\r\n"
	]
	for license_name in THIRD_PARTY_LICENSE_FILES:
		source = ROOT / "licenses" / license_name
		if not source.is_file():
			raise SystemExit(f"Required dependency license is missing: {source}")
		license_sections.append(
			b"\r\n" + (b"=" * 80) + b"\r\n"
			+ license_name.as_posix().encode("utf-8") + b"\r\n"
			+ (b"=" * 80) + b"\r\n\r\n"
			+ source.read_bytes().rstrip(b"\r\n") + b"\r\n"
		)
	(PUBLISH_DIR / "THIRD-PARTY-LICENSES.txt").write_bytes(b"".join(license_sections))

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


def sanitize_binary_build_metadata() -> None:
	"""Remove local workspace paths left in PE/PDB lookup metadata by compilers."""
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
		if changed:
			path.write_bytes(data)


def private_path_markers() -> tuple[str, ...]:
	"""Return machine-specific roots that must never appear in a distribution."""
	candidates = {
		str(ROOT),
		ROOT.as_posix(),
		str(Path.home()),
		Path.home().as_posix(),
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


prepare_publish_directory()
sanitize_binary_build_metadata()
package_files = collect_package_files()
validate_package_privacy(package_files)
write_archive(package_files)

print(f"Created {archive_path.name} with {len(package_files)} files.")
print(f"Updated {latest_path.name}.")
