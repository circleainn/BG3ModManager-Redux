import json
from pathlib import Path
import subprocess
import tempfile
import unittest

from tools.maintenance.build_provenance import source_identity, write_provenance


class CandidateProvenance(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def git(self, *args):
        return subprocess.check_output(["git", *args], cwd=self.root, stderr=subprocess.DEVNULL, text=True).strip()

    def test_untracked_source_is_not_reported_as_clean(self):
        self.git("init")
        (self.root / "source.cs").write_text("original")
        self.git("add", ".")
        self.git("-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid", "commit", "-m", "fixture")
        self.assertFalse(source_identity(self.root)["dirty"])
        (self.root / "untracked.cs").write_text("not committed")
        self.assertTrue(source_identity(self.root)["dirty"])
        (self.root / "untracked.cs").unlink()
        (self.root / "source.cs").write_text("changed")
        self.assertTrue(source_identity(self.root)["dirty"])

    def test_missing_git_history_is_unknown_not_clean(self):
        self.assertEqual({"commit": None, "dirty": None}, source_identity(self.root))

    def test_same_version_different_bytes_remain_distinguishable(self):
        archive = self.root / "candidate.zip"
        manifest = self.root / "channel.json"
        archive.write_bytes(b"first")
        manifest.write_text("{}")
        output = write_provenance(self.root, "0.1.0-alpha.16.7", archive, manifest)
        first = json.loads(output.read_text())
        archive.write_bytes(b"other")
        second = json.loads(write_provenance(self.root, "0.1.0-alpha.16.7", archive, manifest).read_text())
        self.assertEqual(first["displayVersion"], second["displayVersion"])
        self.assertNotEqual(first["archive"]["sha256"], second["archive"]["sha256"])
        self.assertEqual("local-candidate-not-publication", second["purpose"])
        self.assertNotIn(str(self.root), output.read_text())


if __name__ == "__main__":
    unittest.main()
