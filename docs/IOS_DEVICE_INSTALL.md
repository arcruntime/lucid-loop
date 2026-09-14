# Windows USB installation

A development-signed **Lucid Loop · Gyms 1.0 (0)** was installed successfully on the connected iPhone on 2026-09-14. The device reported `iPhone16,2`, iOS 26.6.1, with Developer Mode enabled. Installation and the installed-app inventory both confirm `com.lucidloop.gyms`.

This first install uses the earlier `9744abc` Unity export, compiled in [run 34786581140](https://github.com/jethac/lucid-loop/actions/runs/34786581140). It does not include subsequent character, environment, or loading-logo changes. [Signing run 34838941237](https://github.com/jethac/lucid-loop/actions/runs/34838941237) verified the archive checksum, signed the embedded framework and application, verified the signatures, and produced an IPA. [Installation evidence](validation/iphone-install/result.json) records its checksum. Launch, gameplay, microphone, audio, and performance still need device validation.

## Repeat an installation

The manual `ios-sign-development.yml` workflow accepts the successful unsigned build run, its attempt, and the expected unsigned app archive checksum. Signing credentials live in GitHub Actions secrets `IOS_DEVELOPMENT_P12_BASE64`, `IOS_DEVELOPMENT_P12_PASSWORD`, and `IOS_DEVELOPMENT_PROFILE_BASE64`. Local credentials are under ignored `.local/ios-signing/`; never add these files to Git. The profile must include the target phone and the signing certificate, and match the app identifier.

Download the `ios-development-ipa-<run-id>` artifact, verify the IPA against its SHA-256 companion, then use the isolated Windows tool environment:

```powershell
.local/iphone-tools/Scripts/python.exe -m pymobiledevice3 usbmux list
.local/iphone-tools/Scripts/python.exe -m pymobiledevice3 amfi developer-mode-status
.local/iphone-tools/Scripts/python.exe -m pymobiledevice3 apps install --udid <device-id> <signed-ipa-path>
```

The local environment was created with `python -m venv .local/iphone-tools` and `pip install pymobiledevice3`. Apple USB drivers are supplied by the installed iTunes package. The first signing import required removing explicit certificate-only type/format flags; the successful workflow lets Keychain infer the PKCS12 identity type. The stored PKCS12 uses the macOS-compatible PBESv1 encoding.

## TestFlight

[Before the Drop in App Store Connect](https://appstoreconnect.apple.com/apps/6811879201/distribution) now exists with bundle ID `com.lucidloop.gyms`, SKU `lucid-loop-before-the-drop`, and English (U.S.) primary language. Its internal **Development Team** group uses manual build assignment. No tester invitations have been sent.

The Apple Distribution certificate and **Before the Drop TestFlight** provisioning profile have been created. The profile and matching certificate were verified and configured in CI. Upload authentication, fresh compilation, upload, and processing are still pending. The installed build used Xcode16.4/iOS18.5; it cannot be reused for TestFlight because [Apple requires Xcode26 and SDK26 or later](https://developer.apple.com/news/upcoming-requirements/?id=04282026a). The Account Holder confirmed accepting the updated program agreement. See [TestFlight workflow setup](IOS_TESTFLIGHT.md).

## Current phone relay and corrected TestFlight export

The phone-accessible relay is `ws://192.168.1.87:8791/game`; use the separate access token in ignored `.local/iphone-relay/access-token.txt`. On 2026-09-14 its health endpoint reported ready. It uses the configured BWS OpenAI credential and does not alter the artist's loopback relay on 8790. The phone must be on the same reachable LAN; an HTTP health check on Windows does not prove device connectivity.

Internal TestFlight export `b7e7032` completed successfully in the main Unity Editor. Its export-only `LUCID_LOOP_INTERNAL_TESTFLIGHT` define enables the existing private-LAN policy in a non-Development player. The standalone C# policy checks passed with that define and without it, including rejecting public cleartext destinations. Exported ATS plist checks passed. The opaque 1024px logo icon passed preflight and visual review. Archive SHA-256: `1d0bcc8bd8735f56b2289a30e4a2292e86bd8b7be3f04eaf08c48c389dfa50e6`. The manifest records 660 dirty Unity entries; this remains a shared-worktree snapshot. The earlier `d27812d` transfer lacks the runtime LAN define and must not be used for internal LAN testing.
