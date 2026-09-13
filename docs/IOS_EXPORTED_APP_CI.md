# Hosted compilation of an exported iOS app

`.github/workflows/ios-export-build.yml` manually compiles the existing Windows-generated Unity iOS export on a GitHub-hosted `macos-15` runner. It builds the full `Unity-iPhone` scheme, including `UnityFramework` and `GameAssembly`, for unsigned arm64 iphoneos. It does not install Unity on the Mac or resume the deferred private Mac desktop lane.

## Inputs and provenance

Dispatch the workflow at the commit used to prepare the manifest, with:

- `release_tag`: the named **draft** release containing exactly named assets `ios-xcode.tar.gz` and `export-manifest.json`.
- `archive_sha256`: the expected 64-digit SHA-256 of the archive, obtained from local packaging independently of the downloaded manifest.

`tools/prepare_ios_export.py` supplies packaging and verification. Its manifest ties the archive to the source commit, exact export contents, reviewed generated build script and any explicitly recorded dirty Unity worktree paths/hashes. The exported bytes are the compilation input; this is not automatically a clean-source or reproducible Unity build. Pushes affecting the workflow or transfer tools run the archive-validation tests only; compilation requires manual dispatch.

After a successful Unity export, package it into a fresh ignored directory:

```powershell
python tools/prepare_ios_export.py pack --export Unity/Builds/iOS/Xcode --output .local/ios-export-transfer
```

The compile job checks out `github.sha` with LFS and persisted credentials disabled. The verifier requires `manifest.source_commit == git rev-parse HEAD`, validates the supplied archive checksum and manifest contents, allows only regular files/directories within the expected archive root, rejects traversal/links, and approves the single generated PBX shell phase against its reviewed hash before executing it. The extraction contract is `$RUNNER_TEMP/ios-export/Xcode`.

The workflow invokes:

```sh
python3 tools/prepare_ios_export.py verify \
  --archive "$RUNNER_TEMP/ios-transfer/ios-xcode.tar.gz" \
  --manifest "$RUNNER_TEMP/ios-transfer/export-manifest.json" \
  --sha256 "$EXPORT_SHA256" \
  --destination "$RUNNER_TEMP/ios-export"
```

The native packaging checker then compares the exported native files and generated bindings against the matching checked-out sources. Text comparison normalizes CRLF to LF because Windows exports and hosted Mac Git checkouts use different line endings; raw exported hashes and byte-identity results remain in the report. Keep the selected workflow ref unchanged while packaging and dispatching; a source-commit mismatch must fail rather than silently build another revision.

For a stable build ref while collaborators continue work, push a tag at the manifest's source commit and dispatch that tag. Stage the two files in a draft release with `gh release create --draft` and `gh release upload`; keep the release unpublished. Then run:

```powershell
gh workflow run ios-export-build.yml --ref <pinned-tag> -f release_tag=<draft-tag> -f archive_sha256=<local-archive-sha256>
```

Inspect the resulting run's actual job status. A passing push-triggered archive-test job does not mean the application compiled. The full run must also pass transfer, verification, native packaging, Xcode compilation and product checks.

## Transfer isolation

Draft-release access can require write-level repository access. A separate Ubuntu transfer job receives `contents: write` and exposes its token only to the download step. It checks that the named release is a draft, downloads the two exact asset names, verifies the archive checksum, and uploads the opaque files as an Actions artifact. It does not check out or execute exported code, run packaging scripts from the archive, or extract it.

The hosted Mac compile job receives only `contents: read`, downloads that artifact and validates it before running any generated build phase. Its checkout does not persist credentials, and no release-write token is passed to it. No OpenAI key, Unity license, Apple signing identity or provisioning profile is needed by this lane. Release creation/upload and workflow dispatch are separate operator actions; this workflow does not publish or delete releases.

## Build behavior

The inspected export contains the Mac arm64 and x86_64 IL2CPP tool deployments and bundled runtimes. The job restores executable permission only for the host's `il2cpp`, `il2cpp-compile` and matching Bee backend. It records Xcode/SDK versions, host architecture and disk availability.

The build uses the `Unity-iPhone` scheme, `ReleaseForRunning`, `-sdk iphoneos`, `-destination generic/platform=iOS`, temporary DerivedData, two Xcode jobs, and disabled code signing. A 90-minute job timeout allows substantially more work than the standalone native-plugin check. Bash `pipefail` preserves a failed xcodebuild result while tee saves the log. On success it checks that the app executable and embedded UnityFramework binary exist.

This device export is not a simulator export. Changing only the Xcode SDK would not provide simulator-compatible Unity/Burst/native libraries. Use a separately generated simulator export if that becomes a requirement.

The initial inspected export was about 1,292 MiB uncompressed. Compression size and build peak memory/disk use must be measured; the hosted runner may require tuning if full IL2CPP compilation exceeds its resources. After successful compilation and product checks, the job packages `LucidLoopGyms.app` as `LucidLoopGyms-unsigned-iphoneos.tar.gz`, preserving executable permissions, and writes its SHA-256 checksum to a companion `.sha256.txt` file.

## Evidence and acceptance

Input archive/manifest and diagnostic artifacts are retained for 14 days. Diagnostics upload runs after verification or build failure and includes available toolchain records, manifest, verification/native-packaging logs, xcodebuild output and its result bundle. Product records appear only after successful compilation and binary checks. The separate `ios-unsigned-app-<run-id>-<attempt>` artifact contains the application tarball and checksum, also retained for 14 days. Its packaging/upload steps run only after successful product checks; failed builds still upload diagnostics but do not publish an application artifact. Verify the checksum before extracting the tarball. This unsigned device app still requires signing and provisioning before installation on a physical iPhone.

This lane is newly implemented; a workflow file is not evidence of a successful run. A successful run establishes compilation/linking of the supplied generated app. It does not establish signing, installation, launch, microphone permissions, physical duplex audio/AEC, touch/keyboard layout, rendering quality or sustained iPhone performance. Record actual run results in [IMPLEMENTATION_VALIDATION.md](IMPLEMENTATION_VALIDATION.md), alongside the separate [native voice](IOS_NATIVE_VOICE.md) and [iOS build](IOS_BUILD.md) boundaries.
