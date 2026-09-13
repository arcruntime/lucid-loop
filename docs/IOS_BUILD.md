# iOS target and build

**iOS is the Unity project's primary target**, per the user's platform direction. The baseline device is iPhone 15 Plus, with landscape presentation and a sustained 30 fps target. Runtime `GymRoot` already requests 30 fps; that setting does not prove device performance. Desktop gym builds are secondary diagnostics and their existing CI entry points remain available.

The versioned Editor entry point is `LucidLoop.Gyms.Editor.IosBuild`. It uses Unity's supported settings/build APIs instead of hand-writing a serialized Build Profile asset. `Configure` persists iPhone-only, device SDK, iOS 17 minimum, IL2CPP, .NET Standard, landscape and microphone permission settings. It retains the existing bundle identifier and signing settings. `Activate` selects the iOS active build target. `ExportDevelopment` exports all enabled scenes from the shared build scene list to `Unity/Builds/iOS/Xcode`; it does not rebuild gym geometry or modify character assets. Export first validates the iOS rendering policy and fails if an incompatible pipeline remains selected.

An Editor target switch requires asset reimport and assembly reload. In batch mode supply `-buildTarget iOS` at launch; setting the target inside an executing batch method is insufficient. See Unity's [SwitchActiveBuildTarget documentation](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/EditorUserBuildSettings.SwitchActiveBuildTarget.html) and [command-line build guidance](https://docs.unity3d.com/6000.0/Documentation/Manual/build-command-line.html).

## Activate locally

Coordinate with other agents and close this project's existing Unity Editor before launching a second instance. From `B:/lucid-loop`, the activation command is:

```powershell
New-Item -ItemType Directory -Force .local | Out-Null
Start-Process 'C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe' -WindowStyle Hidden -Wait -ArgumentList '-batchmode -quit -projectPath B:/lucid-loop/Unity -buildTarget iOS -executeMethod LucidLoop.Gyms.Editor.IosBuild.Activate -logFile B:/lucid-loop/.local/ios-activate.log'
```

Alternatively, in the existing Editor use **Lucid Loop → iOS → Activate iOS target**, then allow reimport/compilation to finish. Confirm `IOS_TARGET_ACTIVE: iOS` in the log and iOS in the Build Profiles window. Configuration code being present is not evidence that this machine's active target has switched.

## Export and device validation

After activation, export through **Lucid Loop → iOS → Export development Xcode project**, or use the same batch command with `-executeMethod LucidLoop.Gyms.Editor.IosBuild.ExportDevelopment` and a separate export log. Review enabled scenes first: this method follows the shared build list, which initially contains the two gyms.

The local Windows Unity 6000.3.24f1 installation contains `Editor/Data/PlaybackEngines/iOSSupport`, including ARM64, IL2CPP, Xcode-project support libraries and the iOS build program. This confirms module files exist; Unity's `BuildPipeline.IsBuildTargetSupported` and an actual export still need to pass. Compile/sign/install the generated Xcode project on a supported macOS/Xcode environment with the project's Apple development identity and provisioning. See Unity's [iOS environment setup](https://docs.unity3d.com/6000.0/Documentation/Manual/ios-environment-setup.html).

Device acceptance must measure sustained frame rate, memory, safe-area layout, touch navigation, microphone permission, networking, audio/lip-sync and interruption/background cleanup on iPhone 15 Plus. The 16:9 layout reference must adapt to the phone's actual aspect ratio and safe area; do not force a 16:9 device framebuffer.

## Verification status

Read-only inspection confirmed existing iOS 17 and landscape project settings, the pinned Editor version, runtime 30 fps request, clean tracked `ProjectSettings` files before this change, and installed iOS module files. The Editor helper APIs were checked against installed Unity API metadata and official documentation. This task did not launch Unity, change the active Editor target, serialize ProjectSettings, export Xcode or run on a device. Activation is a coordinated next step, not a completed build claim.
