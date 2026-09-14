# Internal TestFlight builds

Before the Drop uses App Store Connect app **6811879201**, bundle **com.lucidloop.gyms**, and Apple Developer team **59N33TEQ4C**. The internal testing group is **Development Team**; builds are assigned manually. A successful workflow upload still needs Apple processing and group assignment before testers can install it. It does not submit an App Store release or request external beta review.

The development IPA installed over USB is version 1.0 (0), compiled with Xcode 16.4. It cannot be repackaged into a current TestFlight submission: since April 28, 2026, Apple requires Xcode 26 and iOS SDK 26 or newer. The new lane compiles the complete Unity export, including native plugins, UnityFramework and GameAssembly, using **Xcode 26.3** on a hosted Mac. It checks both the selected tools and the built application's SDK metadata. [Apple SDK requirements](https://developer.apple.com/news/upcoming-requirements/?id=04282026a), [GitHub's macOS 15 image inventory](https://github.com/actions/runner-images/blob/main/images/macos/macos-15-arm64-Readme.md).

## Signing setup

Store these as GitHub Actions repository secrets. Upload values from local files using the GitHub CLI standard input; do not print keys, paste them into workflow inputs, or commit them.

| Secret | Content |
| --- | --- |
| `IOS_DISTRIBUTION_P12_BASE64` | Base64 PKCS#12 containing the Apple Distribution certificate and its matching private key |
| `IOS_DISTRIBUTION_P12_PASSWORD` | Password protecting that PKCS#12 |
| `IOS_DISTRIBUTION_PROFILE_BASE64` | Base64 App Store provisioning profile for this bundle and team |
| `ASC_KEY_ID` | App Store Connect **team** API key identifier |
| `ASC_ISSUER_ID` | API key issuer UUID |
| `ASC_PRIVATE_KEY_BASE64` | Base64 downloaded `.p8` API private key |

The API key must have a role permitting build upload, such as Developer. New developer-account agreements may require the Account Holder's acceptance before uploads work. This workflow never accepts agreements automatically. The Apple Distribution profile is distinct from the Apple Development profile used for USB installation. The lane rejects expired, wrong-team, wildcard, development, ad hoc, and enterprise profiles; it also verifies that the imported signing identity is included in the profile.

Temporary signing material lives under `RUNNER_TEMP`, with restricted file permissions and a temporary keychain. An unconditional cleanup removes the keychain, downloaded credentials, and installed profile copies. Build evidence excludes these credential directories. No signing or upload runs on push; pushes only run helper tests.

## Export and dispatch

1. Commit and push the build tools before packaging. Export a **non-Development** iOS Unity player with the internal TestFlight local-relay option. This keeps profiling/debug development features out while preserving the approved scoped LAN relay configuration. Provide an opaque RGB 1024×1024 App Store marketing icon in the iOS icon catalog. The old export contains only the 120/180px phone icons and fails this preflight.
2. Package the new export with `tools/prepare_ios_export.py pack --export <Xcode-directory> --output <fresh-directory>`. The generated manifest records the current commit and every dirty Unity source file hash. Review that provenance before distributing a shared-worktree build; it is not automatically a clean-commit build.
3. Create a draft release containing exactly `ios-xcode.tar.gz` and `export-manifest.json`. Keep its archive SHA-256. The existing transfer mechanism verifies that digest before passing the opaque export to the Mac job, then verifies the archive inventory, source commit, project checksum, and reviewed IL2CPP shell phase before executing it.
4. Dispatch `.github/workflows/ios-testflight.yml` at **the exact source commit recorded in the manifest**, with `release_tag`, lowercase `archive_sha256`, marketing `version`, and a new `build_number`. A branch that advances after packaging will fail provenance verification unless the dispatch is pinned to the packaged commit. `upload=true` signs and uploads; `upload=false` builds and signs an IPA without contacting App Store Connect. The first TestFlight build can be version `1.0`, build `1`; later uploads must increase the build number. Retry an upload with the same number only after checking that Apple did not already accept it.

The lane changes only the extracted application's version/build plist values after verification and records those overrides in `submission-inputs.json`. It preserves the exported microphone and scoped local-network configuration. Export uses manual App Store signing and disables Xcode's automatic build-number changes. Xcode uploads with the API key; the `.ipa` and archive/upload evidence are retained as Actions artifacts. See Apple's [build upload guide](https://developer.apple.com/help/app-store-connect/manage-builds/upload-builds).

## Finish in App Store Connect

Open [the app's TestFlight page](https://appstoreconnect.apple.com/apps/6811879201/testflight/ios). Wait for processing, resolve any actual validation or export-compliance questions, then assign the build to **Development Team**. Add the intended internal tester if needed and install through Apple's TestFlight app on the iPhone. Do not describe an upload as available to testers until the processed build appears in the group.

This remains an internal local-relay build. The iPhone needs the relay's LAN address, the relay running on the Windows PC, and network reachability; TestFlight installation does not deploy the server. Check launch, connection, microphone permission/audio, interruption recovery, the prevention route and rewind, and frame rate on the physical device. Record the installed version/build and observed results. Hosted compilation and Apple processing do not establish physical-device acceptance.

## Local validation

```powershell
python -m unittest discover -s tools -p test_ios_testflight.py -v
python -m unittest discover -s tools -p test_prepare_ios_export.py -v
actionlint .github/workflows/ios-testflight.yml
```

Preflight tests exercise profile restrictions, export options, missing/transparent marketing icons, build-number validation, and preservation of LAN settings. The export-transfer tests exercise malicious archive paths/links, inventory limits, checksums, source mismatch, and the reviewed build phase. Xcode archive/sign/export/upload still requires a hosted execution with valid signing assets; creating this lane alone is not evidence of a completed TestFlight build.
