"""Inspect a Unity export's native voice packaging; does not compile Apple code."""
import argparse
import hashlib
import json
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("export", type=Path)
parser.add_argument("--output", type=Path)
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
project = (args.export / "Unity-iPhone.xcodeproj/project.pbxproj").read_text(encoding="utf-8-sig")
checks = {
    "arc_source_entry": any("LLVoice.mm in Sources" in line and
                            'COMPILER_FLAGS = "-fobjc-arc"' in line
                            for line in project.splitlines()),
    "headers_in_project": all(f"{name} in Headers" in project for name in
                              ("LLVoice.h", "LLVoiceRing.h")),
    "framework_entries": all(f"{name}.framework in Frameworks" in project for name in
                             ("AVFoundation", "AudioToolbox")),
    "cpp17": 'CLANG_CXX_LANGUAGE_STANDARD = "gnu++17"' in project,
    "ios17": "IPHONEOS_DEPLOYMENT_TARGET = 17.0;" in project,
    "standalone_test_excluded": "LLVoiceRingTests.cpp" not in project,
}
files = {}
source_byte_identity = {}
for name in ("LLVoice.h", "LLVoiceRing.h", "LLVoice.mm"):
    source = root / "Unity/Assets/Gyms/Plugins/iOS" / name
    exported = args.export / "Libraries/Gyms/Plugins/iOS" / name
    files[name] = hashlib.sha256(exported.read_bytes()).hexdigest()
    source_bytes, exported_bytes = source.read_bytes(), exported.read_bytes()
    source_byte_identity[name] = source_bytes == exported_bytes
    # Git checks out LF on hosted macOS, while the Windows export preserves CRLF.
    # Compare textual source after that conversion only; retain raw hashes below.
    checks[f"current_source_{name}"] = source_bytes.replace(b"\r\n", b"\n") == exported_bytes.replace(b"\r\n", b"\n")
generated = "\n".join(p.read_text(encoding="utf-8-sig") for p in
                      (args.export / "Il2CppOutputProject/Source/il2cppOutput").glob("LucidLoop.Gyms*.cpp"))
checks["generated_bindings"] = all(f"LLVoice_{name}" in generated for name in
    ("Start", "Stop", "SetCaptureEnabled", "ReadCapture", "WriteOutput",
     "GetConsumedOutput", "GetQueuedOutput", "GetStarved", "GetStatus"))
report = {"scope": "Unity export packaging only", "checks": checks,
          "source_comparison": "Textual source with CRLF normalized to LF only",
          "source_byte_identity": source_byte_identity,
          "exported_sha256": files, "checks_apple_compilation": False, "checks_device_behavior": False}
if args.output:
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
print(json.dumps(report, indent=2))
raise SystemExit(0 if all(checks.values()) else 1)
