"""Reclaim non-Unity LFS payloads in the dedicated Actions checkout; dry-run default."""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import stat
import subprocess

POINTER = re.compile(rb"version https://git-lfs.github.com/spec/v1\noid sha256:([0-9a-f]{64})\nsize ([0-9]+)\n")


def git(root, *args, data=None):
    return subprocess.run(["git", "-C", str(root), *args], input=data, check=True,
                          stdout=subprocess.PIPE, stderr=subprocess.PIPE).stdout


def safe_path(root, relative):
    """Reject traversal and every existing symlink/junction component, not just the leaf."""
    parts = PurePosixPath(relative).parts
    if not parts or PurePosixPath(relative).is_absolute() or any(p in (".", "..") or ":" in p or "\\" in p for p in parts):
        raise ValueError("Unsafe relative path: " + relative)
    cursor = root
    for part in parts:
        cursor = cursor / part
        if cursor.is_symlink():
            raise ValueError("Symlink path: " + relative)
        if cursor.exists():
            info = cursor.lstat()
            if getattr(info, "st_file_attributes", 0) & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400):
                raise ValueError("Reparse path: " + relative)
    if not cursor.resolve().is_relative_to(root.resolve()):
        raise ValueError("Escaped repository path: " + relative)
    return cursor


def digest(path):
    info = path.lstat()
    if not stat.S_ISREG(info.st_mode) or info.st_nlink != 1:
        raise ValueError("Not a regular file: " + str(path))
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(chunk)
    return path.stat().st_size, h.hexdigest()


def pointers_at_head(root):
    candidates = []
    for entry in git(root, "ls-tree", "-rlz", "HEAD").split(b"\0"):
        if not entry:
            continue
        metadata, name = entry.split(b"\t", 1)
        mode, kind, oid, size = metadata.split()
        if kind == b"blob" and mode in (b"100644", b"100755") and int(size) < 1024:
            candidates.append((name.decode("utf-8"), oid))
    raw = git(root, "cat-file", "--batch", data=b"".join(oid + b"\n" for _, oid in candidates))
    offset = 0
    result = []
    for name, _ in candidates:
        end = raw.index(b"\n", offset)
        size = int(raw[offset:end].split()[2])
        pointer = raw[end + 1:end + 1 + size]
        offset = end + size + 2
        match = POINTER.fullmatch(pointer)
        if match:
            result.append((name, match[1].decode(), int(match[2]), pointer))
    return result


def plan(root):
    root = Path(root).absolute()
    if root.is_symlink() or root.resolve() != root:
        raise ValueError("Repository root must be a resolved real directory")
    actual = Path(git(root, "rev-parse", "--show-toplevel").decode().strip()).resolve()
    if actual != root:
        raise ValueError("Run at the repository root")
    # Linked worktrees/external LFS stores are deliberately unsupported.
    dotgit = safe_path(root, ".git")
    if not dotgit.is_dir():
        raise ValueError("Requires an ordinary in-checkout .git directory")
    head = git(root, "rev-parse", "HEAD").decode().strip()
    pointers = pointers_at_head(root)
    protected = {oid for name, oid, _, _ in pointers if name.casefold().startswith("unity/")}
    operations, skipped, blocked = [], [], []
    cache = {}
    validated = set()
    for name, oid, size, pointer in pointers:
        if oid in protected:
            continue
        cache[oid] = size
        try:
            path = safe_path(root, name)
            if not path.exists():
                skipped.append({"path": name, "reason": "missing"})
                continue
            observed = digest(path)
            if observed == (len(pointer), hashlib.sha256(pointer).hexdigest()):
                validated.add(name)
                continue
            if observed != (size, oid):
                blocked.append(name)
                continue
            validated.add(name)
            operations.append({"path": name, "size": size, "sha256": oid, "pointer": pointer,
                               "kind": "dematerialize", "reclaimed": max(0, size - len(pointer))})
        except ValueError as error:
            skipped.append({"path": name, "reason": str(error)})
    for oid, size in cache.items():
        name = f".git/lfs/objects/{oid[:2]}/{oid[2:4]}/{oid}"
        try:
            path = safe_path(root, name)
            if path.exists():
                if digest(path) != (size, oid):
                    blocked.append(name)
                else:
                    operations.append({"path": name, "size": size, "sha256": oid,
                                       "kind": "remove_cache", "reclaimed": size})
        except ValueError as error:
            skipped.append({"path": name, "reason": str(error)})
    # Do not run in a checkout with unrelated edits. Correct LFS payloads may
    # appear in diff output when a filter is unavailable; their hashes above
    # establish equivalence to the exact HEAD pointers independently.
    changed = git(root, "diff", "--no-ext-diff", "--name-only", "-z", "HEAD").split(b"\0")
    staged = git(root, "diff", "--cached", "--no-ext-diff", "--name-only", "-z").split(b"\0")
    blocked.extend(name.decode() for name in changed if name and name.decode() not in validated)
    blocked.extend(name.decode() for name in staged if name)
    return {"root": root, "head": head, "operations": operations, "skipped": skipped,
            "blocked": sorted(set(blocked)), "protected_unity_oids": len(protected)}


def apply_plan(planned):
    root = planned["root"]
    if (os.environ.get("GITHUB_ACTIONS") != "true" or
            os.environ.get("GITHUB_REPOSITORY") != "jethac/lucid-loop" or
            not os.environ.get("GITHUB_WORKSPACE") or
            Path(os.environ["GITHUB_WORKSPACE"]).resolve() != root):
        raise ValueError("Apply requires the dedicated jethac/lucid-loop Actions workspace")
    if planned["blocked"]:
        raise ValueError("Changed or corrupt checkout files: " + ", ".join(planned["blocked"]))
    # Validate the entire plan again before the first write. The runner owns
    # this checkout exclusively for the job; no concurrent authoring is allowed.
    if plan(root) != planned:
        raise ValueError("Checkout changed after inspection")
    for op in planned["operations"]:
        path = safe_path(root, op["path"])
        if digest(path) != (op["size"], op["sha256"]):
            raise ValueError("Payload changed before mutation: " + op["path"])
        if op["kind"] == "dematerialize":
            with path.open("wb") as stream:
                stream.write(op["pointer"])
        else:
            path.unlink()  # Exactly one verified regular blob; never recursive.


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    result = plan(Path.cwd())
    summary = {"head": result["head"], "apply": args.apply, "blocked": result["blocked"],
               "skipped": result["skipped"], "protected_unity_oids": result["protected_unity_oids"]}
    for kind in ("dematerialize", "remove_cache"):
        entries = [op for op in result["operations"] if op["kind"] == kind]
        summary[kind] = {"files": len(entries), "bytes": sum(op["reclaimed"] for op in entries)}
    if args.apply:
        apply_plan(result)
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()
