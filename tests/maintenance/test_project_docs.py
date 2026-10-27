import json
from pathlib import Path
import shutil
import re
import tempfile
import unittest

from tools.maintenance.check_project_docs import check


ROOT = Path(__file__).resolve().parents[2]


class DocumentationContracts(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        for name in ("docs/RELEASE_RECORDS.md", "docs/CURRENT_STATE.md", "docs/REDUX_MOD_DATABASE.md",
                     "src/GUI/Resources/ReduxModDatabase.json"):
            target = self.root / name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT / name, target)
        shutil.copytree(ROOT / "docs/releases", self.root / "docs/releases")

    def replace(self, name, old, new):
        path = self.root / name
        text = path.read_text(encoding="utf-8")
        self.assertIn(old, text)
        path.write_text(text.replace(old, new), encoding="utf-8")

    def test_current_publication_agrees(self):
        self.assertEqual([], check(self.root))

    def test_stale_table_is_rejected_even_when_headline_is_current(self):
        state = (self.root / "docs/CURRENT_STATE.md").read_text(encoding="utf-8")
        current = re.search(r"Published release line: `([^`]+)`", state)[1]
        self.replace("docs/CURRENT_STATE.md", f"| `{current}` |", "| `0.1.0-alpha.1` |")
        self.assertTrue(any("current release table" in e for e in check(self.root)))

    def test_missing_release_notes_are_rejected(self):
        for path in (self.root / "docs/releases").glob("*.md"):
            path.unlink()
        self.assertTrue(any("Missing published release notes" in e for e in check(self.root)))

    def test_new_development_version_does_not_change_published_claims(self):
        path = self.root / "src/GUI/GUI.csproj"
        path.write_text("<Project><InformationalVersion>0.1.0-alpha.99</InformationalVersion></Project>")
        self.assertEqual([], check(self.root))

    def test_changed_corpus_requires_updated_documented_units(self):
        path = self.root / "src/GUI/Resources/ReduxModDatabase.json"
        data = json.loads(path.read_text(encoding="utf-8"))
        data["loadOrderEntries"].append({"dependencies": ["example"], "loadAfter": []})
        path.write_text(json.dumps(data), encoding="utf-8")
        errors = check(self.root)
        self.assertTrue(any("Advisor entry count" in e for e in errors))
        self.assertTrue(any("entries with dependencies" in e for e in errors))


if __name__ == "__main__":
    unittest.main()
