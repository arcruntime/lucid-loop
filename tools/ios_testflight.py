"""Preflight the reviewed Unity export and App Store distribution profile."""
import argparse
import datetime
import hashlib
import json
from pathlib import Path
import plistlib
import re
import struct

TEAM = "59N33TEQ4C"
BUNDLE = "com.lucidloop.gyms"


def check_icon(root):
    catalog = root / "Unity-iPhone/Images.xcassets/AppIcon.appiconset"
    images = json.loads((catalog / "Contents.json").read_text())["images"]
    marketing = [i for i in images if i.get("idiom") == "ios-marketing" and i.get("size") == "1024x1024"]
    if not marketing or not marketing[0].get("filename"):
        raise ValueError("Unity export needs a 1024px App Store marketing icon; configure iOS icons and re-export")
    filename = marketing[0]["filename"]
    if Path(filename).name != filename:
        raise ValueError("Icon must be a file within its asset catalog")
    data = (catalog / filename).read_bytes()
    if data[:8] != b"\x89PNG\r\n\x1a\n" or len(data) < 33:
        raise ValueError("Marketing icon must be PNG")
    width, height, depth, color = struct.unpack(">IIBB", data[16:26])
    if (width, height) != (1024, 1024) or depth != 8 or color != 2:
        raise ValueError("Marketing icon must be 1024x1024, 8-bit RGB without alpha")
    offset = 8
    while offset + 12 <= len(data):
        size = struct.unpack(">I", data[offset:offset + 4])[0]
        if data[offset + 4:offset + 8] == b"tRNS":
            raise ValueError("Marketing icon must not contain transparency")
        offset += size + 12


def prepare(root, version, build):
    if not re.fullmatch(r"[0-9]+\.[0-9]+(?:\.[0-9]+)?", version):
        raise ValueError("Version must contain two or three numeric components")
    if not re.fullmatch(r"[1-9][0-9]{0,3}(?:\.[0-9]{1,2}){0,2}", build):
        raise ValueError("Build must use Apple numeric version format (up to 4.2.2 digits)")
    check_icon(root)
    info_path = root / "Info.plist"
    info = plistlib.loads(info_path.read_bytes())
    if not info.get("NSMicrophoneUsageDescription"):
        raise ValueError("Microphone purpose string is required")
    if info.get("CFBundleIdentifier") not in (BUNDLE, "${PRODUCT_BUNDLE_IDENTIFIER}", "$(PRODUCT_BUNDLE_IDENTIFIER)"):
        raise ValueError("Unexpected application bundle identifier")
    info["CFBundleShortVersionString"] = version
    info["CFBundleVersion"] = build
    info_path.write_bytes(plistlib.dumps(info))
    print(json.dumps({"version": version, "build": build, "bundle": BUNDLE, "marketing_icon": "verified RGB 1024px"}))


def validate_profile(profile, now=None):
    entitlements = profile.get("Entitlements", {})
    now = now or datetime.datetime.now(datetime.timezone.utc).replace(tzinfo=None)
    if profile.get("TeamIdentifier") != [TEAM] or entitlements.get("application-identifier") != TEAM + "." + BUNDLE:
        raise ValueError("Distribution profile does not match the expected team and app")
    if entitlements.get("get-task-allow") is not False or "ProvisionedDevices" in profile or profile.get("ProvisionsAllDevices"):
        raise ValueError("An App Store distribution profile is required; development/ad hoc/enterprise profiles are rejected")
    if not profile.get("ExpirationDate") or profile["ExpirationDate"] <= now:
        raise ValueError("Distribution profile is expired")
    if not profile.get("DeveloperCertificates") or not re.fullmatch(r"[A-Fa-f0-9-]{36}", profile.get("UUID", "")):
        raise ValueError("Profile certificate list or UUID is missing/invalid")
    return profile["UUID"]


def profile_options(path, output):
    profile = plistlib.loads(path.read_bytes())
    uuid = validate_profile(profile)
    options = {"method": "app-store-connect", "destination": "export", "teamID": TEAM,
               "signingStyle": "manual", "signingCertificate": "Apple Distribution",
               "provisioningProfiles": {BUNDLE: uuid}, "manageAppVersionAndBuildNumber": False,
               "uploadSymbols": True}
    output.mkdir(parents=True, exist_ok=True)
    (output / "ExportOptions.plist").write_bytes(plistlib.dumps(options))
    (output / "profile-uuid.txt").write_text(uuid)
    (output / "certificate-shas.txt").write_text("\n".join(hashlib.sha1(c).hexdigest().upper() for c in profile["DeveloperCertificates"]))
    print("Validated App Store profile for " + BUNDLE)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    export = commands.add_parser("prepare")
    export.add_argument("root", type=Path)
    export.add_argument("--version", required=True)
    export.add_argument("--build", required=True)
    signing = commands.add_parser("profile")
    signing.add_argument("path", type=Path)
    signing.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    if args.command == "prepare":
        prepare(args.root, args.version, args.build)
    else:
        profile_options(args.path, args.output)
