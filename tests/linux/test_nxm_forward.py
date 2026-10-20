import contextlib
import importlib.util
import io
from pathlib import Path
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location(
    "redux_nxm_forward", Path(__file__).resolve().parents[2] / "tools/linux/redux_nxm_forward.py")
forwarder = importlib.util.module_from_spec(spec)
spec.loader.exec_module(forwarder)


class NxmForwardingTests(unittest.TestCase):
    def setUp(self):
        self.config = {
            "wine": "/opt/wine runtime/bin/wine",
            "wine_prefix": "/home/test/Redux prefix",
            "working_directory": "/home/test/Redux folder",
            "windows_dotnet": r"C:\Program Files\dotnet\dotnet.exe",
            "windows_redux_dll": r"Z:\home\test\Redux folder\Redux.dll",
        }
        self.uri = "nxm://baldursgate3/mods/20021/files/12345?key=a%2Bb%26c&expires=9999999999&user_id=8"

    def test_signed_link_and_paths_remain_separate_arguments(self):
        args, env, cwd = forwarder.prepare_forward(self.config, self.uri, {"DISPLAY": ":1", "WINEPREFIX": "/wrong"})
        self.assertEqual(args, [self.config["wine"], self.config["windows_dotnet"], self.config["windows_redux_dll"], "--nxm", self.uri])
        self.assertEqual(env["WINEPREFIX"], self.config["wine_prefix"])
        self.assertEqual(env["DISPLAY"], ":1")
        self.assertEqual(env["WINEDEBUG"], "-all")
        self.assertEqual(cwd, self.config["working_directory"])

    def test_wrong_games_malformed_links_and_control_characters_are_rejected(self):
        for uri in ["https://example.com/mods/1/files/2", "nxm://skyrim/mods/1/files/2",
                    "nxm://baldursgate3@evil.test/mods/1/files/2", "nxm://baldursgate3/mods/0/files/2",
                    "nxm://baldursgate3/mods/1/files/2/extra", self.uri + "\n", self.uri + "#fragment",
                    "nxm://baldursgate3/mods/1/files/2?key=a b", "nxm://[broken"]:
            with self.subTest(uri=uri), self.assertRaises(forwarder.ForwardingError):
                forwarder.validate_uri(uri)

    def test_config_requires_absolute_paths_and_windows_dotnet(self):
        for key, value in [("wine", "wine"), ("wine_prefix", "~/.wine"),
                           ("windows_dotnet", "/usr/bin/dotnet"), ("windows_dotnet", r"C:\bad.exe"),
                           ("windows_redux_dll", r"C:\other.dll"), ("working_directory", "/tmp\nnext")]:
            with self.subTest(key=key), self.assertRaises(forwarder.ForwardingError):
                forwarder.prepare_forward(dict(self.config, **{key: value}), self.uri)

    def test_delivery_never_uses_a_shell_or_emits_child_output(self):
        with patch.object(forwarder.Path, "is_file", return_value=True), \
             patch.object(forwarder.Path, "is_dir", return_value=True), \
             patch.object(forwarder.os, "access", return_value=True), \
             patch.object(forwarder.subprocess, "run") as run:
            run.return_value.returncode = 0
            forwarder.forward(self.config, self.uri)
            args, kwargs = run.call_args
            self.assertEqual(args[0][-1], self.uri)
            self.assertFalse(kwargs["shell"])
            self.assertEqual(kwargs["stdout"], forwarder.subprocess.DEVNULL)
            self.assertEqual(kwargs["stderr"], forwarder.subprocess.DEVNULL)
            self.assertNotIn("timeout", kwargs)
            run.return_value.returncode = 1
            with self.assertRaises(forwarder.ForwardingError):
                forwarder.forward(self.config, self.uri)

    def test_failures_do_not_echo_signed_links(self):
        for arguments in [[self.uri], ["--config", "/missing.json", self.uri],
                          ["--config", "/missing.json", self.uri, "extra"]]:
            output = io.StringIO()
            with contextlib.redirect_stderr(output):
                self.assertNotEqual(forwarder.main(arguments), 0)
            self.assertNotIn(self.uri, output.getvalue())
            self.assertNotIn("key=", output.getvalue())


if __name__ == "__main__":
    unittest.main()
