"""Verify or refresh the narrow pinned MIT source dependency. No Unity launch.

python tools/live_speech/vendor_core.py --source .local/lipsync-audit
python tools/live_speech/vendor_core.py --verify
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / "Unity/Assets/LiveSpeech"
PIN = "acea62e125aa2200648a489900de750c3e3587fa"
parser = argparse.ArgumentParser()
parser.add_argument("--source", type=Path)
parser.add_argument("--verify", action="store_true")
args = parser.parse_args()
manifest_path = BASE / "ThirdParty/provenance.json"
if args.verify:
    manifest = json.loads(manifest_path.read_text())
    for entry in manifest["files"]:
        actual = hashlib.sha256((BASE / entry["destination"]).read_bytes()).hexdigest()
        if actual != entry["sha256"]:
            raise SystemExit("Hash mismatch: " + entry["destination"])
    print(f"Verified {len(manifest['files'])} pinned source/license/model files")
else:
    if args.source is None:
        parser.error("provide --source pinned checkout or --verify")
    source = args.source.resolve()
    sha = subprocess.check_output(["git", "-C", str(source), "rev-parse", "HEAD"], text=True).strip()
    if sha != PIN:
        raise SystemExit("Checkout is not the audited pin: " + sha)
    pairs = [(p.relative_to(source).as_posix(), "ThirdParty/Core/" + p.name)
             for p in sorted((source / "src/SplatterfaceGames.LipSync.Core").glob("*.cs"))]
    pairs += [("LICENSE", "ThirdParty/LICENSE.txt"),
              ("src/THIRD-PARTY-NOTICES.md", "ThirdParty/THIRD-PARTY-NOTICES.md"),
              ("src/SplatterfaceGames.LipSync.Core/models/model-en-mixed.bin",
               "Resources/LiveSpeech/model-en-mixed.bytes")]
    entries = []
    for origin, destination in pairs:
        # Compare working content to committed bytes before copying local checkout files.
        committed = subprocess.check_output(["git", "-C", str(source), "show", PIN + ":" + origin])
        if (source / origin).read_bytes().replace(b"\r\n", b"\n") != committed.replace(b"\r\n", b"\n"):
            raise SystemExit("Modified upstream file: " + origin)
        target = BASE / destination
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(committed)
        entries.append(dict(source=origin, destination=destination, sha256=hashlib.sha256(committed).hexdigest()))
    manifest_path.write_text(json.dumps(dict(repository="https://github.com/splatterfacegames/unity-realtime-lipsync",
        commit=PIN, installation="vendored unmodified MIT core source; project-owned asmdef",
        files=entries), indent=2) + "\n", encoding="utf-8")
    print(f"Vendored {len(entries)} pinned source/license/model files")
