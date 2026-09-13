"""Package a reviewed Unity export and verify it before hosted Xcode compilation."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import shutil
import subprocess
import tarfile
from datetime import datetime, timezone

ROOT = Path(__file__).resolve().parents[1]
# Unity 6000.3.24f1's single generated IL2CPP phase. Reviewed: paths derive from
# PROJECT_DIR/build configuration, host tools are chmodded, then IL2CPP is run;
# cleanup removes only the generated configuration directory within the export.
REVIEWED_SHELL_SHA256 = "4e28832057770f09bb7edd09691a2bf8fc713edebee885b2cda78882292c9344"
MAX_BYTES = 4 * 1024**3
MAX_FILES = 20000


def digest(path):
    result = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            result.update(chunk)
    return result.hexdigest()


def git(*args):
    return subprocess.check_output(["git", *args], cwd=ROOT).decode("utf-8").strip()


def check_project(path):
    text = Path(path).read_text(encoding="utf-8-sig")
    scripts = re.findall(r'shellScript = ("(?:\\.|[^"\\])*");', text)
    hashes = [hashlib.sha256(json.loads(script).encode()).hexdigest() for script in scripts]
    if hashes != [REVIEWED_SHELL_SHA256] or re.findall(r'shellPath = ([^;]+);', text) != ["/bin/sh"]:
        raise ValueError("Export build script differs from the reviewed Unity IL2CPP phase")
    return digest(path)


def pack(export, output):
    export, output = export.resolve(), output.resolve()
    if output == export or export in output.parents:
        raise ValueError("Package output must be outside the export")
    project_hash = check_project(export / "Unity-iPhone.xcodeproj/project.pbxproj")
    files = sorted(path for path in export.rglob("*") if path.is_file())
    if any(path.is_symlink() for path in export.rglob("*")):
        raise ValueError("Unexpected export symlink")
    if len(files) > MAX_FILES or sum(path.stat().st_size for path in files) > MAX_BYTES:
        raise ValueError("Export exceeds the reviewed size limits")
    output.mkdir(parents=True, exist_ok=True)
    archive = output / "ios-xcode.tar.gz"
    if archive.exists() or (output / "export-manifest.json").exists():
        raise ValueError("Use a fresh output directory; packages are immutable")
    with tarfile.open(archive, "w:gz", compresslevel=1) as tar:
        for path in files:
            info = tar.gettarinfo(str(path), "Xcode/" + path.relative_to(export).as_posix())
            info.uid = info.gid = 0
            info.uname = info.gname = ""
            info.mode = 0o644
            with path.open("rb") as stream:
                tar.addfile(info, stream)
    if archive.stat().st_size >= 2 * 1024**3:
        raise ValueError("Archive exceeds a GitHub release asset's size limit")
    # The shared artist checkout can have local changes. Preserve that fact and
    # their hashes, rather than claim a clean commit produced this export.
    changed = set(git("diff", "--name-only", "-z", "HEAD", "--", "Unity").split("\0"))
    changed.update(git("ls-files", "--others", "--exclude-standard", "-z", "--", "Unity").split("\0"))
    dirty = [{"path": name, "sha256": digest(ROOT / name) if (ROOT / name).is_file() else None}
             for name in sorted(changed) if name]
    manifest = {
        "schema": 1, "source_commit": git("rev-parse", "HEAD"),
        "unity_version": "6000.3.24f1", "created_utc": datetime.now(timezone.utc).isoformat(),
        "archive": archive.name, "archive_root": "Xcode", "archive_sha256": digest(archive),
        "file_count": len(files), "uncompressed_bytes": sum(p.stat().st_size for p in files),
        "project_sha256": project_hash, "reviewed_shell_sha256": REVIEWED_SHELL_SHA256,
        "dirty_unity_worktree": dirty,
        "scope": "Unsigned exported application compilation; no signing or device acceptance",
    }
    (output / "export-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(manifest, indent=2))


def validate_members(members):
    names, size, count = set(), 0, 0
    for member in members:
        path = PurePosixPath(member.name)
        if (path.is_absolute() or ".." in path.parts or "\\" in member.name or
                not path.parts or path.parts[0] != "Xcode" or ":" in member.name):
            raise ValueError("Invalid archive path: " + member.name)
        if not member.isfile() and not member.isdir():
            raise ValueError("Archive links/devices are not permitted")
        if member.name in names:
            raise ValueError("Duplicate archive member")
        names.add(member.name)
        if member.isfile():
            count += 1
            size += member.size
        if size > MAX_BYTES or count > MAX_FILES:
            raise ValueError("Archive exceeds reviewed limits")
    return count, size


def verify(archive, manifest_path, expected, destination):
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if not re.fullmatch(r"[0-9a-f]{64}", expected):
        raise ValueError("Expected checksum must be lowercase SHA-256")
    if manifest.get("schema") != 1 or manifest.get("archive_root") != "Xcode" or manifest.get("archive") != "ios-xcode.tar.gz":
        raise ValueError("Unrecognized export manifest")
    if manifest.get("source_commit") != git("rev-parse", "HEAD"):
        raise ValueError("Manifest commit does not match the workflow checkout")
    if manifest.get("archive_sha256") != expected or digest(archive) != expected:
        raise ValueError("Archive checksum mismatch")
    if destination.exists():
        raise ValueError("Extraction requires a fresh destination")
    with tarfile.open(archive, "r:gz") as tar:
        members = tar.getmembers()
        count, size = validate_members(members)
        if count != manifest.get("file_count") or size != manifest.get("uncompressed_bytes"):
            raise ValueError("Archive inventory differs from manifest")
        # Only regular files/dirs are allowed, so extraction cannot follow links.
        destination.mkdir(parents=True)
        for member in members:
            target = destination / member.name
            if member.isdir():
                target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                with tar.extractfile(member) as source, target.open("xb") as output:
                    shutil.copyfileobj(source, output)
                target.chmod(0o644)
    project = destination / "Xcode/Unity-iPhone.xcodeproj/project.pbxproj"
    if check_project(project) != manifest.get("project_sha256"):
        raise ValueError("Project checksum mismatch")
    if manifest.get("reviewed_shell_sha256") != REVIEWED_SHELL_SHA256:
        raise ValueError("Manifest script review mismatch")
    print(json.dumps({"verified": True, "source_commit": manifest["source_commit"],
                      "files": count, "bytes": size, "dirty_source_files": len(manifest.get("dirty_unity_worktree", []))}))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    package = commands.add_parser("pack")
    package.add_argument("--export", type=Path, required=True)
    package.add_argument("--output", type=Path, required=True)
    checker = commands.add_parser("verify")
    checker.add_argument("--archive", type=Path, required=True)
    checker.add_argument("--manifest", type=Path, required=True)
    checker.add_argument("--sha256", required=True)
    checker.add_argument("--destination", type=Path, required=True)
    args = parser.parse_args()
    if args.command == "pack":
        pack(args.export, args.output)
    else:
        verify(args.archive, args.manifest, args.sha256, args.destination)


if __name__ == "__main__":
    main()
