#!/usr/bin/env python3
"""Experimental native-Linux NXM forwarding into an existing Redux Wine prefix.

The user owns the config and desktop registration. This helper never registers a
handler, stores/prints an NXM URI, or shuts down the Wine environment.
"""
import json
import os
from pathlib import Path, PurePosixPath, PureWindowsPath
import re
import subprocess
import sys
from urllib.parse import urlsplit


class ForwardingError(ValueError):
    """An error whose message is safe to display without a signed URI."""


def validate_uri(uri):
    # Preserve the original signed query byte-for-byte. Redux performs the full
    # key/expiry validation after its existing --nxm forwarding entry point.
    if not isinstance(uri, str) or len(uri) > 16384 or any(ord(c) < 33 or ord(c) == 127 for c in uri):
        raise ForwardingError("Expected one Baldur's Gate 3 NXM file link.")
    try:
        parsed = urlsplit(uri)
        valid = (parsed.scheme.lower() == "nxm"
                 and parsed.netloc.lower() == "baldursgate3"
                 and not parsed.fragment
                 and re.fullmatch(r"/mods/[1-9][0-9]*/files/[1-9][0-9]*", parsed.path))
    except ValueError:
        valid = False
    if not valid:
        raise ForwardingError("Expected one Baldur's Gate 3 NXM file link.")
    return uri


def _path(config, key, windows=False):
    value = config.get(key)
    if not isinstance(value, str) or not value or any(ord(c) < 32 for c in value):
        raise ForwardingError("The forwarding configuration needs absolute runtime and application paths.")
    path = PureWindowsPath(value) if windows else PurePosixPath(value)
    if not path.is_absolute():
        raise ForwardingError("The forwarding configuration needs absolute runtime and application paths.")
    return value


def prepare_forward(config, uri, environment=None):
    if not isinstance(config, dict):
        raise ForwardingError("The forwarding configuration must be a JSON object.")
    validate_uri(uri)
    wine = _path(config, "wine")
    prefix = _path(config, "wine_prefix")
    working_directory = _path(config, "working_directory")
    dotnet = _path(config, "windows_dotnet", windows=True)
    redux = _path(config, "windows_redux_dll", windows=True)
    if PureWindowsPath(dotnet).name.lower() != "dotnet.exe" or PureWindowsPath(redux).name.lower() != "redux.dll":
        raise ForwardingError("Configure the Windows dotnet.exe host and Redux.dll application.")
    env = dict(os.environ if environment is None else environment)
    env["WINEPREFIX"] = prefix
    # Suppress Wine debug traces, which can contain the signed command line.
    env["WINEDEBUG"] = "-all"
    return [wine, dotnet, redux, "--nxm", uri], env, working_directory


def forward(config, uri):
    arguments, environment, cwd = prepare_forward(config, uri)
    if not Path(arguments[0]).is_file() or not os.access(arguments[0], os.X_OK):
        raise ForwardingError("The configured Wine launcher is unavailable.")
    if not Path(environment["WINEPREFIX"]).is_dir() or not Path(cwd).is_dir():
        raise ForwardingError("The configured Wine prefix or working directory is unavailable.")
    # No shell, joined command text, persistent queue, or wineserver cleanup.
    # A running Redux instance normally receives the link and exits quickly.
    # On cold launch this process may stay alive with Redux; do not time it out
    # and kill an application that has just opened.
    result = subprocess.run(arguments, env=environment, cwd=cwd, shell=False,
                            stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
                            stderr=subprocess.DEVNULL, check=False)
    if result.returncode != 0:
        raise ForwardingError("Forwarding failed. Open Redux in the configured prefix and try again.")


def main(argv=None):
    arguments = list(sys.argv[1:] if argv is None else argv)
    # Avoid argparse's default error output: it can echo signed links.
    if len(arguments) != 3 or arguments[0] != "--config":
        print("Usage: redux_nxm_forward.py --config CONFIG.json NXM_LINK", file=sys.stderr)
        return 2
    try:
        validate_uri(arguments[2])
        config_path = Path(arguments[1])
        if config_path.stat().st_size > 65536:
            raise ForwardingError("The forwarding configuration is too large.")
        config = json.loads(config_path.read_text(encoding="utf-8"))
        forward(config, arguments[2])
        return 0
    except ForwardingError as error:
        print(str(error), file=sys.stderr)
    except (OSError, ValueError, TypeError):
        print("Could not read the configuration or start the configured Wine runtime.", file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.exit(main())
