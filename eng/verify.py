"""Build, test, enforce 80% library coverage, and create a NuGet package."""

import os
from pathlib import Path
import shlex
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET
import zipfile


ROOT = Path(__file__).resolve().parents[1]
ARTIFACTS = ROOT / "artifacts"
TEST_ASSEMBLY = ROOT / "tests/Elmah.AzureTableStorage.Tests/bin/Release/net48/Elmah.AzureTableStorage.Tests.exe"
LIBRARY_PROJECT = "src/Elmah.AzureTableStorage/Elmah.AzureTableStorage.csproj"


def main():
    ARTIFACTS.mkdir(exist_ok=True)
    environment = os.environ.copy()
    environment.update({
        "DOTNET_CLI_HOME": str(ROOT / ".tools/dotnet-home"),
        "NUGET_PACKAGES": str(ROOT / ".nuget/packages"),
        "DOTNET_CLI_TELEMETRY_OPTOUT": "1",
        "DOTNET_NOLOGO": "1",
    })
    local_sdk = ROOT / ".tools/dotnet/dotnet"
    dotnet = str(local_sdk) if local_sdk.is_file() else shutil.which("dotnet")
    if not dotnet:
        raise RuntimeError("Install a .NET 10 SDK or run python3 eng/bootstrap-toolchain.py")
    environment["DOTNET_ROOT"] = str(Path(dotnet).parent)
    environment["PATH"] = str(Path(dotnet).parent) + os.pathsep + environment["PATH"]

    with (ARTIFACTS / "verification.log").open("w", encoding="utf-8") as log:
        def run(command):
            print("+ " + shlex.join(str(arg) for arg in command), flush=True)
            with subprocess.Popen(command, cwd=ROOT, env=environment, stdout=subprocess.PIPE,
                                  stderr=subprocess.STDOUT, text=True, errors="replace") as process:
                for line in process.stdout:
                    print(line, end="", flush=True)
                    log.write(line)
                if process.wait() != 0:
                    raise subprocess.CalledProcessError(process.returncode, command)

        run([dotnet, "restore", "Elmah.AzureTableStorage.sln", "--locked-mode"])
        run([dotnet, "build", "Elmah.AzureTableStorage.sln", "-c", "Release", "--no-restore", "--no-incremental", "-warnaserror"])
        run([dotnet, "tool", "restore"])

        if os.name == "nt":
            target = TEST_ASSEMBLY
            target_args = '--workers=0 --result="' + str(ARTIFACTS / "unit-tests.xml") + '" --noheader --nocolor'
        else:
            target = ROOT / ".tools/bin/mono"
            if not target.is_file():
                target = shutil.which("mono")
            if not target:
                raise RuntimeError("Install Mono or run python3 eng/bootstrap-mono.py")
            target_args = '"' + str(TEST_ASSEMBLY) + '" --workers=0 --result="' + str(ARTIFACTS / "unit-tests.xml") + '" --noheader --nocolor'

        run([dotnet, "tool", "run", "coverlet", "--", str(TEST_ASSEMBLY),
             "--target", str(target), "--targetargs", target_args,
             "--format", "cobertura", "--format", "json",
             "--output", str(ARTIFACTS / "coverage") + os.sep,
             "--include", "[Elmah.AzureTableStorage]*",
             "--threshold", "80", "--threshold-type", "line",
             "--threshold-type", "branch", "--threshold-stat", "total"])
        report = ET.parse(ARTIFACTS / "coverage/coverage.cobertura.xml").getroot()
        if int(report.attrib["lines-valid"]) == 0 or int(report.attrib["branches-valid"]) == 0:
            raise RuntimeError("Coverage report has no production code")
        print("Coverage: lines {:.2%}, branches {:.2%}".format(
            float(report.attrib["line-rate"]), float(report.attrib["branch-rate"])), flush=True)

        run([dotnet, "pack", LIBRARY_PROJECT, "-c", "Release", "--no-build",
             "--no-restore", "--output", str(ARTIFACTS / "packages")])
        package = ARTIFACTS / "packages/Elmah.AzureTableStorage.1.0.1.nupkg"
        with zipfile.ZipFile(package) as bundle:
            expected = {"lib/net48/Elmah.AzureTableStorage.dll", "content/web.config.transform", "README.md"}
            missing = expected.difference(bundle.namelist())
            if missing:
                raise RuntimeError("Package is missing expected files: " + ", ".join(sorted(missing)))
        print("Verification passed; reports and package are in artifacts/.", flush=True)


if __name__ == "__main__":
    try:
        main()
    except (RuntimeError, subprocess.CalledProcessError) as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
