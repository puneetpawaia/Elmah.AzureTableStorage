"""Print the latest stable versions of project dependencies from NuGet.org."""

import concurrent.futures
import json
from pathlib import Path
import urllib.request
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[1]


def latest(package_id):
    url = "https://api.nuget.org/v3-flatcontainer/" + package_id.lower() + "/index.json"
    with urllib.request.urlopen(url, timeout=60) as response:
        versions = json.load(response)["versions"]
    return package_id, next(version for version in reversed(versions) if "-" not in version)


def main():
    packages = set()
    for path in ROOT.rglob("packages.config"):
        if ".tools" not in path.parts and ".nuget" not in path.parts:
            packages.update(item.attrib["id"] for item in ET.parse(path).getroot())
    for path in ROOT.rglob("*.csproj"):
        if ".tools" not in path.parts and ".nuget" not in path.parts:
            packages.update(item.attrib["Include"] for item in ET.parse(path).getroot().iter()
                            if item.tag.rsplit("}", 1)[-1] == "PackageReference" and "Include" in item.attrib)
    packages.update(item.attrib["Include"] for item in ET.parse(ROOT / "Directory.Packages.props").getroot().iter()
                    if item.tag == "PackageVersion")
    manifest = json.loads((ROOT / ".config/dotnet-tools.json").read_text())
    packages.update(manifest["tools"])
    with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
        versions = dict(pool.map(latest, packages))
    for package_id, version in sorted(versions.items()):
        print(package_id + " " + version)
    output = ROOT / "artifacts" / "latest-package-versions.json"
    output.parent.mkdir(exist_ok=True)
    output.write_text(json.dumps(versions, indent=2) + "\n")


if __name__ == "__main__":
    main()
