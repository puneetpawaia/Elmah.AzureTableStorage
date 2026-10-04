"""Download a .NET SDK into this repository without changing the host OS."""

import hashlib
import json
from pathlib import Path
import tarfile
import urllib.request


ROOT = Path(__file__).resolve().parents[1]
TOOLS = ROOT / ".tools"
SDK_ROOT = TOOLS / "dotnet"
METADATA_URL = "https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json"


def main():
    TOOLS.mkdir(exist_ok=True)
    with urllib.request.urlopen(METADATA_URL, timeout=60) as response:
        metadata = json.load(response)
    version = metadata["latest-sdk"]
    sdk = next(release["sdk"] for release in metadata["releases"]
               if release["sdk"]["version"] == version)
    archive = next(item for item in sdk["files"]
                   if item["rid"] == "linux-x64" and item["name"].endswith(".tar.gz"))
    destination = TOOLS / ("dotnet-sdk-" + version + ".tar.gz")
    if not destination.exists():
        print("Downloading .NET SDK " + version, flush=True)
        urllib.request.urlretrieve(archive["url"], destination)
    digest = hashlib.sha512()
    with destination.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    if digest.hexdigest().lower() != archive["hash"].lower():
        raise RuntimeError("SDK checksum does not match Microsoft's release metadata")
    SDK_ROOT.mkdir(exist_ok=True)
    with tarfile.open(destination) as bundle:
        bundle.extractall(SDK_ROOT, filter="data")
    print("Installed .NET SDK " + version + " into " + str(SDK_ROOT), flush=True)


if __name__ == "__main__":
    main()
