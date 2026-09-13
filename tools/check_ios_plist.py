"""Autodetect and validate an exported iOS plist without loading its external DTD.

Usage: python tools/check_ios_plist.py Unity/Builds/iOS/Xcode/Info.plist --development
"""
import argparse
import plistlib
from pathlib import Path


LOCAL_RANGES = {
    "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16",
    "169.254.0.0/16", "fc00::/7", "fe80::/10",
}


def check(path: Path, development: bool) -> None:
    raw = path.read_bytes()
    assert raw.startswith(b"<?xml"), "Missing XML declaration at the start of the plist"
    # Deliberately omit fmt: this catches missing declarations that explicit FMT_XML hides.
    data = plistlib.loads(raw)
    assert isinstance(data, dict), "Expected a root dictionary"
    assert plistlib.loads(plistlib.dumps(data)) == data, "Plist roundtrip changed values"
    ats = data["NSAppTransportSecurity"]
    assert ats["NSAllowsArbitraryLoads"] is False, "Unexpected global ATS bypass"
    assert ats["NSAllowsLocalNetworking"] is development, "Wrong build-mode local policy"
    exceptions = ats.get("NSExceptionDomains", {})
    for cidr in LOCAL_RANGES:
        if development:
            assert exceptions[cidr]["NSExceptionAllowsInsecureHTTPLoads"] is True, cidr
        else:
            assert cidr not in exceptions, f"Development exception left in release: {cidr}"
    assert data.get("NSLocalNetworkUsageDescription"), "Missing local-network purpose"


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("path", type=Path)
    parser.add_argument("--development", action="store_true")
    arguments = parser.parse_args()
    check(arguments.path, arguments.development)
    print(f"IOS_PLIST_OK development={arguments.development}")
