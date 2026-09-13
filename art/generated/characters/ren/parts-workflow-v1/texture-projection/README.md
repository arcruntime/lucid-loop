# Fixed-view head texture projection helper

Tool: `tools/character_art/project_ren_head_texture.py`. This implements the
geometry-preserving capture/project steps described in the reviewed HOS section
of `research/japanese-anime-character-workflows-2026-09.md`. It does not generate
paintings or approve Ren's geometry, likeness, textures or animation readiness.
No Ren asset has been bound or textured by this implementation.

Run inside Blender 5.1.1 or 5.2.1, with NumPy supplied by Blender:

```powershell
& $blender --background --threads 2 --offline-mode --python-exit-code 1 `
  --python tools/character_art/project_ren_head_texture.py -- capture `
  --input accepted-head.blend --object Head --cameras cameras.json --output captures

& $blender --background --threads 2 --offline-mode --python-exit-code 1 `
  --python tools/character_art/project_ren_head_texture.py -- project `
  --input accepted-head.blend --object Head --capture captures `
  --review reviewed-views.json --output derived --size 1024
```

`cameras.json` explicitly freezes each view. Coordinates are world space; scale
is **horizontal** world-space width. Example coordinates below describe a fixture
centered at the origin, facing -Y. They are not calibrated Ren cameras.

```json
{
  "cameras": [
    {"name":"front", "position":[0,-5,0], "target":[0,0,0], "up":[0,0,1], "width":512, "height":512, "ortho_scale":4},
    {"name":"quarter", "position":[3.535,-3.535,0], "target":[0,0,0], "up":[0,0,1], "width":512, "height":512, "ortho_scale":4},
    {"name":"profile", "position":[5,0,0], "target":[0,0,0], "up":[0,0,1], "width":512, "height":512, "ortho_scale":4}
  ]
}
```

Capture writes clay PNG, world geometric-normal PNG, visibility PNG, depth preview
PNG and an authoritative NPZ per view. NPZ depth is camera-forward distance in
world units, with infinity for background; it also contains triangle IDs and
unencoded world normals. Normal PNG encodes `normal * 0.5 + 0.5`; consult the
visibility mask for background. PNG/NPZ arrays use Blender's bottom-left pixel
origin internally. Standard image viewers display the PNG normally.

The deterministic CPU triangle rasterizer supplies aligned diagnostic images,
not antialiased Cycles beauty renders. `capture.json` records exact camera matrices,
resolution, conventions, geometry/UV hashes, world transform and NPZ hashes.
Only the selected head participates; hair and other objects are not occluders.

Paintings must preserve these cameras, dimensions and silhouette. Alpha zero can
exclude areas that an artist does not approve. `reviewed-views.json` explicitly
binds approval to both the capture metadata and each exact image:

```json
{
  "capture_sha256":"SHA256 of captures/capture.json",
  "views":[
    {"camera":"front", "image":"paint/front.png", "approved":true, "sha256":"SHA256 of painted PNG"}
  ]
}
```

Paint paths resolve relative to the review JSON. The caller records actual review;
the helper never grants approval. It rejects stale hashes, unapproved views,
changed mesh/UV/world transform and incompatible image sizes.

Projection rasterizes the existing UV triangles at output texel centers, then
combines reviewed views with squared facing, raster-depth agreement and paint
alpha weights. A separate BVH first-hit ray rejects genuinely occluded surfaces,
including a hidden surface with the same normal as the visible one. Colors blend
in linear space. Back-facing and out-of-frame points receive no contribution.

Outputs are `projected-color.png`, `coverage.png`, `projected-head.blend` and
`projection-report.json`. Uncovered texels remain transparent black; there is no
automatic fill, mirrored face or hidden-back projection. The derived file uses
an opaque preview material, so uncovered areas appear black there: inspect the
coverage image. The report compares geometry/UV signatures before and after;
the CLI also verifies the source `.blend` byte hash remains unchanged on disk.

Limits: static neutral `.blend` mesh only, existing active single-tile UVs,
orthographic cameras, no enabled modifiers, no nonzero expression weights,
no degenerate geometry/UV triangles, no UDIM/wrapping. Overlapping UV interiors
are rejected at output texel centers; this is not an exact subtexel polygon
overlap validator. Shared triangle boundaries are allowed. Sampling uses nearest
painted pixels; no seam dilation, filtering, automatic camera fitting, material
partition preservation, or animated texture validation is provided. All material
slots use the new derived preview material, while mesh material indices remain
unchanged. Inspect seams and add reviewed side/back views before accepting a map.

## Synthetic validation

An isolated Blender fixture with two parallel quads and disjoint UV islands passed:
fixed-camera depth and normals; red/green paint lands on the expected UV sides;
the occluded rear quad remains unpainted despite having the same facing normal;
saved/reopened geometry and UV signatures match; source `.blend` bytes match;
unapproved/stale images, changed transforms and overlapping UV interiors reject.
An additional adversarial depth archive pointed at the rear quad: BVH visibility
still rejected it. Linear 0.25 gray survived PNG save/load within 0.005.

Historical evidence: `synthetic-fixture-report.json` records the initial Blender
5.2.1 run. The actual executable was
`C:/Users/jetha/AppData/Local/Programs/Blender/blender-5.2.1-windows-x64/blender.exe`.
The current parts pipeline uses the separate Blender 5.1.1 installation at
`C:/Program Files/Blender Foundation/Blender 5.1/blender.exe`; the same core fixture
also passes there. Version-specific regression reports record actual executable,
version and helper hash; the historical evidence has not been relabeled.

The checkout-portable fixture is
`tools/character_art/tests/blender_texture_projection_regression.py`:

```powershell
& $blender --background --threads 2 --factory-startup --offline-mode `
  --python-exit-code 1 --python tools/character_art/tests/blender_texture_projection_regression.py
```

It creates its own synthetic scene and a unique project-relative
`.local/ren-texture-projection/regression-*` output directory, and prints the
report path. It tests changed geometry, UVs and capture-review hashes in addition
to the checks above. It never opens a real Ren model.

Both actual CLI subcommands also exited 0, including a painting path relative to
the review JSON and the source byte-hash check. Python compilation and Ruff pass.
This synthetic proof does not establish Ren texture quality or full-head coverage.
