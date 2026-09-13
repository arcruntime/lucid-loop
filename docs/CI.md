# Continuous Unity builds

`.github/workflows/unity-builds.yml` builds the two committed gym scenes on every push to `main`, same-repository pull requests, daily at 18:17 UTC (03:17 JST), and manual dispatch. Fork pull requests do not execute on private machines. Only the Windows job is enabled; Mac builds and provisioning are deferred by user request. A newer run for the same ref cancels the previous one.

Each job runs the EditMode tests, requires a passing XML result, builds a development player with Unity 6000.3.24f1, verifies its expected files, and packages it with `build-info.json` and SHA-256 checksums. Windows gets a ZIP. The retained macOS build script produces a tarball to preserve executable permissions and symlinks when that platform is resumed. Download artifacts from the workflow run. Retention is 14 days. Test results, logs and preflight failures are uploaded separately, including on failed jobs. The Mac app is an unsigned development build, not a notarized distribution or an iOS build.

CI builds the versioned scenes without regenerating the nightclub. `GymBuilder` remains the optional authoring tool; `GymCIBuild` is the build-only entry point. Neither client build job receives `OPENAI_API_KEY`. The independent `Gyms` workflow tests the backend and retains its explicitly opt-in upstream smoke check.

## Runner contract

Provision Windows now. Mac requirements below are retained for future resumption; no Mac job is scheduled.

Register **additional** repository-scoped runners for `https://github.com/arcruntime/lucid-loop`; preserve the runners serving other repositories. Registration requires repository admin access. Use the installation commands generated in Settings → Actions → Runners.

| Host | Suggested runner name | Required labels (in addition to defaults) |
| --- | --- | --- |
| dockurr Windows VM on stadia-testbed | lucid-loop-windows | `stadia-testbed`, `lucid-loop` |
| Apple Silicon Mac mini | lucid-loop-mac-mini | `mac-mini`, `lucid-loop` |

Default OS/architecture labels must match `Windows`, `X64` and `macOS`, `ARM64` respectively. Run each under an account with a valid Unity license and access to its build storage. Install Git, Git LFS, Bash (Git Bash on Windows), Python 3.9+ (`python` on Windows, `python3` on Mac), Unity 6000.3.24f1 and the native desktop build support module. On Windows, use the same licensed user context for the runner and Unity; a SYSTEM service does not inherit an interactive user's license.

The script requires 15 GiB free workspace space before importing/building. Provision more for initial Unity installation and caches. Keep the Mac awake during a job with the included `caffeinate` wrapper. Runner work directories should be on reliable storage, separate from interactive Unity checkouts.

Optional repository variables `UNITY_EDITOR_WINDOWS` and `UNITY_EDITOR_MACOS` override the editor executable path. Defaults are the Unity Hub versioned locations; Mac also checks the prior `/Volumes/MacMiniOffload/unity-ci/6000.3.24f1/Unity.app/Contents/MacOS/Unity` location. An explicitly configured missing path fails instead of selecting an arbitrary editor version. Install and activate Unity on the host; CI does not print or transfer license files.

## Host inspection — 2026-09-13

- stadia-testbed is Linux and hosts the running `win-soccer` dockurr Windows VM. Its existing `sg-win-microprose` runner is scoped to `jethac/tokyoretro-microprose-soccer`, not Lucid Loop. The VM has roughly 22 GiB free; Unity was absent from the standard installation path.
- The Mac mini is reachable and its Tokyo Retro / MicroProse runner services are active. No Unity editor was found under `/Applications/Unity`; the prior MacMiniOffload volume is not mounted. Internal data storage has about 2.4 GiB free. The earlier hackathon runner points to that unavailable volume.
- The connected `jethac` GitHub account has push but not admin/maintain access to Lucid Loop; the runner API returns 403. Runner registration and remote builds are pending that access and host provisioning. Existing runner registrations, services and disks were left intact.

Once Windows prerequisites are met, register its dedicated runner with the labels above and dispatch **Unity builds**. Confirm the Windows job runs and produces an archive before removing its pending notice from the README. Resume Mac provisioning only when requested, then restore its matrix entry with `platform: macos`, labels `["self-hosted", "macOS", "ARM64", "mac-mini", "lucid-loop"]`, `python: python3`, and `editor_variable: UNITY_EDITOR_MACOS`.

References: [GitHub self-hosted runner setup](https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/add-runners), [Unity command-line reference](https://docs.unity3d.com/6000.3/Documentation/Manual/EditorCommandLineArguments.html).

## Local validation

Actionlint accepts the workflow and all three Python validation tests pass. A clean Windows checkout passed all eight Unity EditMode tests, built both committed scenes and produced a 66 MB Windows ZIP with a SHA-256 checksum. A subsequent `-batchmode -nographics` run also passed all eight tests and the Windows player build, validating the headless command used by the VM job. This is local workstation evidence, not evidence of execution on the remote VM or Mac mini; the Mac build remains unverified until its host is provisioned.
