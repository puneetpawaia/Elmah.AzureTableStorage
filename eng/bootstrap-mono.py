"""Extract Ubuntu Mono packages locally, without installing system packages."""

import os
from pathlib import Path
import re
import shutil
import subprocess


ROOT = Path(__file__).resolve().parents[1]
DOWNLOADS = ROOT / ".tools" / "mono-downloads"
PREFIX = ROOT / ".tools" / "mono"


def main():
    dependencies = subprocess.check_output([
        "apt-cache", "depends", "--recurse", "--no-recommends", "--no-suggests",
        "--no-conflicts", "--no-breaks", "--no-replaces", "--no-enhances",
        "mono-runtime", "libmono-system-web4.0-cil", "libmono-system-net-http4.0-cil",
        "libmono-system-runtime-serialization4.0-cil"], text=True)
    packages = sorted(set(line.strip() for line in dependencies.splitlines()
                          if re.fullmatch(r"(?:mono|libmono)[a-z0-9.+-]*", line)))
    if not packages:
        raise RuntimeError("Mono packages are absent from the local Ubuntu package index")
    # mono-devel contains the netstandard facade needed by Azure.Data.Tables.
    packages.append("mono-devel")
    DOWNLOADS.mkdir(parents=True, exist_ok=True)
    PREFIX.mkdir(parents=True, exist_ok=True)
    subprocess.run(["apt", "download", *packages], cwd=DOWNLOADS, check=True)
    for package in sorted(DOWNLOADS.glob("*.deb")):
        subprocess.run(["dpkg-deb", "--extract", str(package), str(PREFIX)], check=True)
    # Debian's package installer normally copies these templates to /etc.
    # Keep that setup inside our extracted runtime instead.
    templates = PREFIX / "usr/share/mono-2.0/mono"
    if templates.is_dir():
        shutil.copytree(templates, PREFIX / "etc/mono", dirs_exist_ok=True)
    scripts = ROOT / ".tools" / "bin"
    scripts.mkdir(exist_ok=True)
    wrapper = scripts / "mono"
    wrapper.write_text('#!/bin/sh\n'
                       'export MONO_PATH="' + str(PREFIX / "usr/lib/mono/4.5") + ':'
                       + str(PREFIX / "usr/lib/mono/4.5/Facades") + '"\n'
                       'export MONO_GAC_PREFIX="' + str(PREFIX / "usr") + '"\n'
                       'export MONO_CFG_DIR="' + str(PREFIX / "etc") + '"\n'
                       'export LD_LIBRARY_PATH="' + str(PREFIX / "usr/lib") + ':' + '$' + '{LD_LIBRARY_PATH:-}"\n'
                       'exec "' + str(PREFIX / "usr/bin/mono-sgen") + '" --config "'
                       + str(PREFIX / "etc/mono/config") + '" "$@"\n')
    os.chmod(wrapper, 0o755)
    subprocess.run([str(wrapper), "--version"], check=True)


if __name__ == "__main__":
    main()
