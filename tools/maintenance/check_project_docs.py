"""Check durable publication claims without confusing development with a release."""
import argparse
import json
from pathlib import Path
import re


def check(root: Path) -> list[str]:
    errors = []
    records = (root / "docs/RELEASE_RECORDS.md").read_text(encoding="utf-8")
    match = re.search(r"^## (0\.1\.0-alpha\.\d+(?:\.\d+){0,2})\b", records, re.M)
    if not match:
        return ["RELEASE_RECORDS.md: no published release heading found"]
    published = match[1]
    state = (root / "docs/CURRENT_STATE.md").read_text(encoding="utf-8")
    for label, pattern, expected in (
        ("published release line", r"Published release line: `([^`]+)`", published),
        ("current release table", r"Latest verified published version\s*\|\s*`([^`]+)`", published),
        ("release tag", r"The release tag is `([^`]+)`", "v" + published),
    ):
        found = re.search(pattern, state)
        if not found or found[1] != expected:
            errors.append(f"CURRENT_STATE.md: {label} must be {expected}")
    if not (root / "docs/releases" / (published + ".md")).is_file():
        errors.append(f"Missing published release notes: {published}.md")

    database = json.loads((root / "src/GUI/Resources/ReduxModDatabase.json").read_text(encoding="utf-8"))
    entries = database["loadOrderEntries"]
    guide = (root / "docs/REDUX_MOD_DATABASE.md").read_text(encoding="utf-8")
    count = re.search(r"Advisor catalog contains ([\d,]+) UUID-backed entries", guide)
    if not count or int(count[1].replace(",", "")) != len(entries):
        errors.append(f"REDUX_MOD_DATABASE.md: Advisor entry count must be {len(entries):,}")
    metrics = {
        "Nexus project records": len(database["projects"]),
        "mod.io project records": len(database["modioProjects"]),
        "Advisor entries with dependencies": sum(bool(e.get("dependencies")) for e in entries),
        "Advisor entries with explicit load-after rules": sum(bool(e.get("loadAfter")) for e in entries),
    }
    for label, actual in metrics.items():
        found = re.search(r"\| " + re.escape(label) + r" \| ([\d,]+) \|", guide)
        if not found or int(found[1].replace(",", "")) != actual:
            errors.append(f"REDUX_MOD_DATABASE.md: {label} must be {actual:,}")
    return errors


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    args = parser.parse_args()
    try:
        errors = check(args.root)
    except (OSError, ValueError, KeyError, TypeError) as error:
        errors = [f"Cannot validate documentation: {error}"]
    for error in errors:
        print("ERROR:", error)
    if not errors:
        print("Published release references and database coverage counts agree.")
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
