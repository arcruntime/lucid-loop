# Passed Unity video transport check

This is a model-free decoder and control-validation check. **It does not demonstrate an H character scene, facial animation, final art, realtime lip-sync, audible output, a muxed performance or phone performance.** No placeholder character was instantiated.

[transport-smoke.json](transport-smoke.json) is the unchanged successful scratch Play-mode result from Unity 6000.3.24f1 on Windows/D3D11. The shared production transport presented initial frame 0, paused at a 3.2-second seek, stayed on that frame for eight updates, honored the latest of rapid 6 → 7.8 → 2.4-second seeks, replayed and advanced, stopped on actual frame 451, held the last frame for a tail seek, and replayed after the end. It also checked the decoder audio-track mute toggle and rejected seven invalid binding definitions. There were 26 frame-ready events, seven seek-completed events and zero recovery attempts.

Four unique decoded PNGs are retained: [initial](initial.png), [paused seek](paused-seek-3.2.png), [latest rapid seek](rapid-seek-last-wins-2.4.png), and [last real frame](end-last-real-frame.png). The raw JSON names two additional replay PNGs. Both have the exact same SHA256 as `initial.png`; [curation.json](curation.json) maps those names through `captureAliases`, avoiding duplicate images. All JSON and retained PNG bytes are unchanged from the passed run.

[source-frame-comparison.json](source-frame-comparison.json) records the comparison with the immutable source MP4 and its original ffprobe timestamp table. Sampled frames 0, 12, 72, 96 and 451 have decoder clock offsets no greater than 0.333 microseconds; no acting timestamps were rescaled. Four decoded texture images were compared with the corresponding ffmpeg frames, giving mean absolute RGB differences around 2.4–2.7 byte values. WindowsMediaFoundation emitted H.264 timestamp and unknown-color-primary warnings, so exact color identity is not claimed. Source comparison PNGs are reproducible intermediates and are not duplicated in this package.

The file/audio duration is 15.092971 seconds, video span is 15.066693 seconds, and the last decoded frame timestamp is 15.033333 seconds. Seeking clamps to the last real frame, not a nonexistent frame at the video-span endpoint. Audio verification covers the real decoder track's mute state only.

The failed first attempt remains at `.local/ren-h-video-smoke-v1/`, separate from this passed evidence. A synchronous editor material import blocked Play mode before any video frame was presented; the transport timed out explicitly. The revised harness waits for stable editor updates after imports. It passed with the same transport source bytes. No additional transport test was run during curation.

## Sources and checkpoint files

`curation.json` records exact input hashes, production/runtime/editor source hashes, LF-only status, `.meta` paths and GUIDs. The key inputs are the repository-root `DanielDuguay87_2093375826557296673.mp4`, `../control-curves-unity.json` (32 channels/152 keys), and `../source-frame-times.json`. The production transport SHA256 is `efd996b0591141856b907ea6724d5c9fe0c8b950a348f54819e150a88d578bd1`; the H controller SHA256 is `8af1ecde73bd1b52451b59fad4bd5da5f8bea69b362c7e64d33ed37dcc906ce6`.

Include the five listed C# sources, `RenHReferenceAnimation.md`, and every corresponding `.meta` in the checkpoint. They reuse the tracked CharacterArt assemblies and NPR frame helper. The scratch-only `RenHTransportSmoke.unity` and imported smoke MP4 are not H deliverables and are not part of this package.

## Reproduce the decoder check

Use the existing authorized scratch project with one editor owner. Do not start a second Editor on the open main project. The scratch must already contain the tracked CharacterArt/NPR dependencies. Copy the current reviewed source files and immutable inputs into the scratch project:

```powershell
$repoRoot = 'B:/lucid-loop'
$scratchProject = 'C:/Users/jetha/AppData/Local/LucidLoopScratch/ren-eye-import-verification'
$smokeAssets = "$scratchProject/Assets/CharacterArt/Generated/RenHTransportSmoke"
New-Item -ItemType Directory -Path $smokeAssets -Force | Out-Null
Copy-Item "$repoRoot/Unity/Assets/CharacterArt/Runtime/RenHReference*.cs*" "$scratchProject/Assets/CharacterArt/Runtime/" -Force
Copy-Item "$repoRoot/Unity/Assets/CharacterArt/Editor/RenHReference*Builder.cs*" "$scratchProject/Assets/CharacterArt/Editor/" -Force
Copy-Item "$repoRoot/DanielDuguay87_2093375826557296673.mp4" "$smokeAssets/Reference.mp4" -Force
Copy-Item "$repoRoot/art/generated/characters/ren/video-reference-v1/control-curves-unity.json" "$smokeAssets/Timeline.json" -Force
$outputDirectory = "$repoRoot/.local/ren-h-video-smoke-reproduction"
$unityArguments = @('-batchmode', '-force-d3d11', '--burst-disable-compilation',
  '-projectPath', $scratchProject,
  '-executeMethod', 'LucidLoop.CharacterArt.Editor.RenHReferenceVideoSmokeBuilder.Run',
  '-renHVideoSmokeOutput', $outputDirectory,
  '-logFile', "$outputDirectory-editor.log")
Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/6000.3.24f1/Editor/Unity.exe' -ArgumentList $unityArguments -WindowStyle Hidden -PassThru
```

Use a fresh output directory: the harness refuses to overwrite an existing result. Do not add `-quit` or `-nographics`; the harness enters actual Play mode, runs the decoder, writes evidence, and exits automatically. Success is `passed: true`, the `REN_H_TRANSPORT_SMOKE_OK` log marker, and exit code 0. Inspect the emitted decoded images as well as the JSON. Decoder failures and timeouts remain explicit failures.

To reproduce the original-frame comparison, extract source frames 0, 72, 96 and 451 with ffmpeg into a fresh `.local` directory, then run the included comparison script (requires NumPy and Pillow):

```powershell
New-Item -ItemType Directory -Path '.local/ren-h-source-comparison' -Force | Out-Null
ffmpeg -hide_banner -loglevel error -threads 2 -i DanielDuguay87_2093375826557296673.mp4 -an -vf "select='eq(n,0)+eq(n,72)+eq(n,96)+eq(n,451)'" -fps_mode passthrough .local/ren-h-source-comparison/source-%03d.png
python art/generated/characters/ren/video-reference-v1/unity-transport-review/compare_source.py --reference-frames .local/ren-h-source-comparison --output .local/ren-h-source-comparison/comparison.json
```

This recomputes timing and pixel differences without editing the MP4, timeline or decoded images. The frame-index correspondence was checked on the sampled images; this is not an exhaustive whole-video decoder/color certification.
