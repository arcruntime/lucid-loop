"""Build committed gym scenes on the dedicated Windows VM or Mac mini runner."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import plistlib
import shutil
import subprocess
import sys
import tarfile
import xml.etree.ElementTree as ET
import zipfile

VERSION = "6000.3.24f1"
ROOT = Path(__file__).resolve().parents[2]


def editor_path(target):
    configured = os.environ.get("UNITY_EDITOR")
    if configured:
        candidates = [Path(configured)]
    elif target == "windows":
        candidates = [Path(os.environ.get("ProgramFiles", "C:/Program Files")) /
                      "Unity/Hub/Editor" / VERSION / "Editor/Unity.exe"]
    else:
        candidates = [Path("/Applications/Unity/Hub/Editor") / VERSION / "Unity.app/Contents/MacOS/Unity",
                      Path("/Volumes/MacMiniOffload/unity-ci") / VERSION / "Unity.app/Contents/MacOS/Unity"]
    for candidate in candidates:
        if candidate.is_file():
            return candidate
    raise RuntimeError("Unity {} is unavailable. Install and license it for the runner account; "
                       "set UNITY_EDITOR_WINDOWS or UNITY_EDITOR_MACOS to its executable. Checked: {}"
                       .format(VERSION, ", ".join(map(str, candidates))))


def validate_tests(path):
    result = ET.parse(path).getroot()
    if (result.tag != "test-run" or result.get("result") != "Passed"
            or int(result.get("passed", "0")) < 8 or int(result.get("failed", "0")) != 0):
        raise RuntimeError("Unity tests did not pass: " + str(result.attrib))


def run_editor(editor, args, log):
    startup = None
    if os.name == "nt":
        startup = subprocess.STARTUPINFO()
        startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        startup.wShowWindow = 0
    command = [str(editor), "-batchmode", "-nographics", "-projectPath", str(ROOT / "Unity"),
               "-logFile", str(log)] + args
    print("Unity: " + " ".join(command), flush=True)
    try:
        subprocess.run(command, cwd=ROOT, check=True, timeout=3600, startupinfo=startup)
    finally:
        if log.exists():
            print("\n".join(log.read_text(errors="replace").splitlines()[-35:]), flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--platform", choices=["windows", "macos"], required=True)
    args = parser.parse_args()
    target = args.platform
    if platform.system() != {"windows": "Windows", "macos": "Darwin"}[target]:
        raise RuntimeError("Run this target on its native build host.")
    diagnostics = ROOT / "artifacts/diagnostics"
    packages = ROOT / "artifacts/packages"
    diagnostics.mkdir(parents=True, exist_ok=True)
    packages.mkdir(parents=True, exist_ok=True)
    (diagnostics / "failure.txt").unlink(missing_ok=True)
    # Remove only this script's known output paths; never clean shared caches.
    output = ROOT / "Unity/Builds" / ("Windows" if target == "windows" else "macOS")
    if output.resolve().parent != (ROOT / "Unity/Builds").resolve():
        raise RuntimeError("Build output escaped its expected directory")
    if output.exists():
        shutil.rmtree(output)
    for old in packages.glob("lucid-loop-" + target + "*"):
        if old.is_file():
            old.unlink()
    results = diagnostics / "editmode.xml"
    results.unlink(missing_ok=True)
    awake = None
    try:
        editor = editor_path(target)
        if shutil.disk_usage(ROOT).free < 10 * 1024**3:
            raise RuntimeError("At least 10 GiB free workspace space is required for Unity imports/builds.")
        if target == "macos":
            awake = subprocess.Popen(["caffeinate", "-ims", "-w", str(os.getpid())])
        build_target = "Win64" if target == "windows" else "OSXUniversal"
        run_editor(editor, ["-buildTarget", build_target, "-runTests", "-testPlatform", "EditMode",
                            "-testResults", str(results)], diagnostics / "tests.log")
        validate_tests(results)
        method = "Windows" if target == "windows" else "MacOS"
        build_log = diagnostics / "build.log"
        run_editor(editor, ["-quit", "-buildTarget", build_target, "-executeMethod",
                            "LucidLoop.Gyms.Editor.GymCIBuild." + method], build_log)
        if "GYM_CI_BUILD_OK:" not in build_log.read_text(errors="replace"):
            raise RuntimeError("Unity exited without confirming a completed build")
        if target == "windows":
            required = [output / "LucidLoopGyms.exe", output / "UnityPlayer.dll",
                        output / "LucidLoopGyms_Data/globalgamemanagers"]
        else:
            contents = output / "LucidLoopGyms.app/Contents"
            with (contents / "Info.plist").open("rb") as source:
                info = plistlib.load(source)
            executable = info["CFBundleExecutable"]
            if not info.get("NSMicrophoneUsageDescription"):
                raise RuntimeError("Mac app is missing its microphone permission description")
            if Path(executable).name != executable:
                raise RuntimeError("Invalid app executable name in Info.plist")
            required = [contents / "MacOS" / executable, contents / "Info.plist",
                        contents / "Resources/Data/globalgamemanagers"]
            if not os.access(required[0], os.X_OK):
                raise RuntimeError("Mac app executable is missing its execute permission")
        for path in required:
            if not path.is_file() or path.stat().st_size == 0:
                raise RuntimeError("Missing player file: " + str(path))
        sha = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
        (output / "build-info.json").write_text(json.dumps({"commit": sha, "unity": VERSION,
                                                           "platform": target}, indent=2) + "\n")
        if target == "windows":
            archive = packages / "lucid-loop-windows.zip"
            with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as bundle:
                for path in output.rglob("*"):
                    if path.is_file():
                        bundle.write(path, path.relative_to(output))
        else:
            # tar preserves app executable bits and symlinks across artifact download.
            archive = packages / "lucid-loop-macos.tar.gz"
            with tarfile.open(archive, "w:gz") as bundle:
                for path in output.iterdir():
                    bundle.add(path, arcname=path.name)
        digest = hashlib.sha256()
        with archive.open("rb") as source:
            for chunk in iter(lambda: source.read(1024 * 1024), b""):
                digest.update(chunk)
        archive.with_name(archive.name + ".sha256").write_text(digest.hexdigest() + "  " + archive.name + "\n")
        print("Packaged " + str(archive), flush=True)
    except Exception as error:
        (diagnostics / "failure.txt").write_text(str(error) + "\n")
        raise
    finally:
        if awake:
            awake.terminate()


if __name__ == "__main__":
    main()
