# Continuous Unity builds

`.github/workflows/unity-builds.yml` builds the two committed gym scenes on every push to `main`, same-repository pull requests, daily at 18:17 UTC (03:17 JST), and manual dispatch. Fork pull requests do not execute on private machines. Only the Windows job is enabled; Mac builds and provisioning are deferred by user request. A newer run for the same ref cancels the previous one.

Each job runs the EditMode tests, requires a passing XML result, builds a development player with Unity 6000.3.24f1, verifies its expected files, and packages it with `build-info.json` and SHA-256 checksums. Windows gets a ZIP. The retained macOS build script produces a tarball to preserve executable permissions and symlinks when that platform is resumed. Download artifacts from the workflow run. Retention is 14 days. Test results, logs and preflight failures are uploaded separately, including on failed jobs. The Mac app is an unsigned development build, not a notarized distribution or an iOS build.

CI builds the versioned scenes without regenerating the nightclub. `GymBuilder` remains the optional authoring tool; `GymCIBuild` is the build-only entry point. Neither client build job receives `OPENAI_API_KEY`. The independent `Gyms` workflow tests the backend and retains its explicitly opt-in upstream smoke check.

## Runner contract

Provision Windows now. Mac requirements below are retained for future resumption; no Mac job is scheduled.

Register **additional** repository-scoped runners for `https://github.com/jethac/lucid-loop`; preserve the runners serving other repositories. Registration requires repository admin access. Use the installation commands generated in Settings → Actions → Runners.

| Host | Suggested runner name | Required labels (in addition to defaults) |
| --- | --- | --- |
| dockurr Windows VM on stadia-testbed | lucid-loop-windows | `stadia-testbed`, `lucid-loop` |
| Apple Silicon Mac mini | lucid-loop-mac-mini | `mac-mini`, `lucid-loop` |

Default OS/architecture labels must match `Windows`, `X64` and `macOS`, `ARM64` respectively. Run each under an account with a valid Unity license and access to its build storage. Install Git, Git LFS, Bash (Git Bash on Windows), Python 3.9+ (`python` on Windows, `python3` on Mac), Unity 6000.3.24f1 and the native desktop build support module. On Windows, use the same licensed user context for the runner and Unity; a SYSTEM service does not inherit an interactive user's license.

The script requires 10 GiB free workspace space before importing/building. Provision more for initial Unity installation and caches. Keep the Mac awake during a job with the included `caffeinate` wrapper. Runner work directories should be on reliable storage, separate from interactive Unity checkouts.

Optional repository variables `UNITY_EDITOR_WINDOWS` and `UNITY_EDITOR_MACOS` override the editor executable path. Defaults are the Unity Hub versioned locations; Mac also checks the prior `/Volumes/MacMiniOffload/unity-ci/6000.3.24f1/Unity.app/Contents/MacOS/Unity` location. An explicitly configured missing path fails instead of selecting an arbitrary editor version. Install and activate Unity on the host; CI does not print or transfer license files.

## Host inspection — 2026-09-13

- stadia-testbed is Linux and hosts the running `win-soccer` dockurr Windows VM. Its existing `sg-win-microprose` runner is scoped to `jethac/tokyoretro-microprose-soccer`, not Lucid Loop. The VM has roughly 22 GiB free; Unity was absent from the standard installation path.
- The Mac mini is reachable and its Tokyo Retro / MicroProse runner services are active. No Unity editor was found under `/Applications/Unity`; the prior MacMiniOffload volume is not mounted. Internal data storage has about 2.4 GiB free. The earlier hackathon runner points to that unavailable volume.
- After transfer to `jethac/lucid-loop`, repository admin access is confirmed. The dedicated `lucid-loop-windows` runner is registered and online with the required labels, running as an interactive logon task under the VM's `docker` account for Unity Personal licensing. Existing runners remain intact. Unity editor installation is in progress. Unity Hub installation and Personal activation are pending; automatic approval review rejected the attempted Hub download/install without a detailed reason.

Once Windows Unity installation and licensing are ready, dispatch **Unity builds** using the registered runner. Confirm the Windows job runs and produces an archive before removing its pending notice from the README. Resume Mac provisioning only when requested, then restore its matrix entry with `platform: macos`, labels `["self-hosted", "macOS", "ARM64", "mac-mini", "lucid-loop"]`, `python: python3`, and `editor_variable: UNITY_EDITOR_MACOS`.

References: [GitHub self-hosted runner setup](https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/add-runners), [Unity command-line reference](https://docs.unity3d.com/6000.3/Documentation/Manual/EditorCommandLineArguments.html).

## Local validation

Actionlint accepts the workflow and all three Python validation tests pass. A clean Windows checkout passed all eight Unity EditMode tests, built both committed scenes and produced a 66 MB Windows ZIP with a SHA-256 checksum. A subsequent `-batchmode -nographics` run also passed all eight tests and the Windows player build, validating the headless command used by the VM job. This is local workstation evidence, not evidence of execution on the remote VM or Mac mini; the Mac build remains unverified until its host is provisioned.

## Unity Personal activation on Windows

The user selected Unity Personal. On the Windows VM console at `http://stadia-testbed:8006`, use the existing `docker` Windows session, install Unity Hub from Unity's official site, and sign in with the chosen Unity account. Under Hub Settings > Licenses, confirm Personal is active; if needed select Add license > Get a free personal license. Locate Unity 6000.3.24f1 in Hub after editor installation finishes. Do not put account credentials or license files in this repository.

The `LucidLoopRunner` Task Scheduler task starts at `docker` logon and runs without a stored password. Its former NETWORK SERVICE runner service is disabled to prevent two listeners. Keep the Windows user session logged in; disconnecting remote access is fine, but signing out stops this runner. After reboot, the runner requires that user to log in. Other projects' runner services are unchanged. Verify license access with an actual workflow build after activation.

[Unity Hub license activation instructions](https://docs.unity.com/en-us/hub/manage-license).
