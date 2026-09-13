# Ren bust comparison viewer

This is an isolated source-quality review scene. It does not change the existing Ren eye study or character viewer. Open `Assets/CharacterArt/Generated/Preview/Scenes/RenBustComparison.unity` and enter Play mode after building it.

The viewer compares any two imported candidates, with synchronized orthographic orbit, zoom, front/three-quarter/profile presets, and an optional source-reference column. Base color is the default. Neutral and nightclub lighting use the same simple material response for both candidates. These generated busts are not presented as rigged, expression-ready, or device-ready assets.

## Input contract

The default manifest is `Assets/CharacterArt/Generated/BustComparison/bust-comparison.json`:

```json
{
  "schemaVersion": 1,
  "title": "Ren / bust comparison",
  "defaults": { "leftId": "tripo-h-closed", "rightId": "meshy-closed" },
  "entries": [
    {
      "id": "tripo-h-closed",
      "label": "Tripo H / closed mouth",
      "provider": "Tripo",
      "modelLabel": "Recorded provider model version",
      "pose": "Closed mouth",
      "fbxAsset": "Assets/CharacterArt/Generated/BustComparison/tripo-h-closed/model.fbx",
      "frontYaw": 0,
      "triangleCount": 0,
      "notes": "Generation settings and known visual limitations.",
      "materials": [
        {
          "sourceName": "SourceMaterialName",
          "baseColorAsset": "Assets/CharacterArt/Generated/BustComparison/tripo-h-closed/basecolor.png",
          "normalAsset": "Assets/CharacterArt/Generated/BustComparison/tripo-h-closed/normal.png",
          "normalScale": 1,
          "baseColorFactor": { "r": 1, "g": 1, "b": 1, "a": 1 }
        }
      ]
    }
  ],
  "references": [
    { "label": "Original artist design", "textureAsset": "Assets/CharacterArt/Generated/BustComparison/References/ren-model-sheet.png" }
  ]
}
```

Entries must describe real generated models; the builder does not make placeholders. `frontYaw` is applied around Unity Y to face the source mesh toward Unity +Z. Each bust is uniformly scaled to an overall bounds height of 1.65 units and centered horizontally. This preserves proportions but equalizes the full bust extent, including hair; review facial scale if candidates have substantially different hair or neck bounds.

Material names map to actual imported FBX material slots. A single supplied mapping may cover all slots of a single-material export. `normalAsset`, `normalScale`, `baseColorFactor`, `metallicAsset`, and `roughnessAsset` are optional. Factors use linear values directly, matching glTF's base-color factor convention. A supplied normal is imported as a normal map. Metallic and roughness data remain available as linear source maps; they are not silently bound to URP's differently packed metallic/smoothness slot.

The lit diagnostic uses diffuse URP/Lit with metallic 0 and smoothness 0; specular highlights and environment reflections are disabled. Neutral lighting uses a reduced white directional key, gentle point fill/rim, and higher flat ambient illumination for clear shape review. Both modes render double-sided, without shadows or post-processing. Point lights require additional-light support in the URP renderer used by the review project. This is a material/likeness comparison, not a finished anime shader or club-scene performance measurement.

Club lighting uses a gently cool directional key (RGB 0.88/0.92/1, intensity 0.55), flat ambient RGB 0.30/0.28/0.34, and magenta/cyan points at intensities 1/1.4. The colored lights act as accents while the key preserves painted facial values. This LDR review has no tone mapping; the intensities were reduced after the initial 5/7 setup visibly clipped broad face regions in actual captures. The import evidence records the current settings.

**Source normal maps default to OFF.** Imported geometry normals are retained. The explicit **Source normal maps: ON/OFF** button enables the supplied maps in lit modes for separate inspection; it is disabled in base-color mode. This keeps normal-map detail from obscuring the underlying generated shape. The original map files and their import settings remain available, and toggling uses separate material assets without changing source texture pixels or geometry normals.

Source textures remain unchanged on disk. Unity imports use up to 8192 pixels, uncompressed Standalone RGBA32, mipmaps, trilinear filtering, and anisotropy 8. Base color is sRGB; normal/metallic/roughness data are linear. The viewer reports actual imported triangle counts and base-color dimensions rather than trusting a requested generation budget.

## Build and capture

Interactive menu: **Lucid Loop → Character Art → Build Ren Bust Comparison**. The builder uses an additive scene and restores the previous active scene. It does not discard the user's open scene. Open the generated scene explicitly when ready to view it.

For batch work, use a separate scratch Unity project. Never start another Editor on the user's already-open project.

```text
-executeMethod LucidLoop.CharacterArt.Editor.RenBustComparisonBuilder.BuildAndCapture
-renBustManifest Assets/CharacterArt/Generated/BustComparison/bust-comparison.json
-renBustOutput B:/path/to/captures
```

`BuildAndCapture` renders every candidate at 0°, 45°, and 90° with all three material/lighting modes, at 1536 × 1536 pixels. These default captures have supplied normal maps disabled. Every capture uses the same projection, target height, and camera distance. It also writes `unity-import-evidence.json` with actual Unity version, texture import properties, triangle counts, normalization, normal-map state, lighting settings, and image names. Add `-renBustCaptureSourceNormals` to also capture the three neutral-light views with supplied normals enabled, named `ID--neutral-source-normals--VIEW.png`; the saved scene still defaults to maps OFF.

To build a Windows viewer, call `LucidLoop.CharacterArt.Editor.RenBustComparisonBuilder.BuildWindowsViewer` with `-renBustPlayerOutput B:/path/RenBustComparison.exe`. It builds only this scene and does not alter the project's saved build-scene list.

To build the scene, render all captures, and then build the Windows viewer in one invocation, use `LucidLoop.CharacterArt.Editor.RenBustComparisonBuilder.BuildCaptureAndWindowsViewer` with both `-renBustOutput` and `-renBustPlayerOutput`. It reuses the scene just built for capture and avoids a second forced model-import/build pass.

To retry only player packaging after a completed scene/capture pass, call `LucidLoop.CharacterArt.Editor.RenBustComparisonBuilder.BuildPlayerFromSavedScene`. This leaves the saved scene and imported models intact. The static review player can be built with Unity's `--burst-disable-compilation` argument when native Burst compilation is unusually slow; this is a per-process build choice, not a mobile-performance configuration or a persistent project-setting change.

The player supports a real UI screenshot after the Unity splash screen has finished and eight additional frames have rendered. Capture mode enables background updates for the owned player process:

```text
RenBustComparison.exe -renBustCapture B:/path/viewer.png
  -renBustLeft tripo-h-closed -renBustRight meshy-closed
  -renBustView three-quarter -renBustLighting basecolor -renBustShowReference
  -renBustReferenceIndex 1
```

Other view values are `front` and `profile`; other lighting values are `neutral` and `club`. Add `-renBustSourceNormals` for an explicit source-normal diagnostic in a lit screenshot. The player captures the actual Game view at 1600 × 900 and exits. Omit `-renBustCapture` for interactive review.

Windows D3D11 screenshots require the requested viewer window to be visible and not minimized. A hidden player can write an all-black PNG even after the splash screen has finished and the capture call reports success. Use a normal visible launch for this viewer's screenshot capture and inspect the resulting image.

`-renBustReferenceIndex` selects the exact source image using its zero-based position in the manifest's `references` array. Use it together with `-renBustShowReference`. Invalid or out-of-range indices fail the capture with exit code 2 rather than silently showing a different image.
