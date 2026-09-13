# Ren corrected-head Tripo texture trial

This trial applies Tripo's highest documented texture setting to a static copy
of the corrected Ren head. It does not generate a replacement head or alter the
working facial blendshapes. Generation completed for **30 credits**; Blender
review, the Unity comparison and a local browser viewer are ready. The result shows
clearer brows and lips, but noisy eyes and a more realistic makeup treatment
than the intended anime painting. It is not approved final Ren artwork.

![Actual provider preview](originals/rendered_image.webp)

Use the [browser and Unity launch instructions](../../../../../../README.md#launch-the-ren-face-study).
The browser displays the original GLB; Unity defaults to clay left/static Tripo
right. [Blender review](review/README.md) records small geometry drift and missing
UVs on 11 of 14 objects. [Unity evidence](unity-review/RenFaceStudyTextureReview.json)
records the actual imported front render, materials and axis checks. The static
trial's live Unity UI screenshot was not captured; V2 has separate live facial
control evidence. Neither viewer establishes finished anime likeness.

The upload is `input/Ren_P2_Texture_ClosedRest.glb`: closed rest, neutral gaze,
separate eyes and liners, uniform skin in place of the rejected broad rose lip
material. `input/input-manifest.json` and `input/glb-audit.json` record the exact
source, evaluated geometry, UVs and export verification. Front remains +X;
Blender +Z up becomes glTF +Y up. The source blend remains unchanged.

The exact image reference is
[`front-paint-v1.png`](../face-paint-v1/painted-views/front-paint-v1.png), a painting
trial derived from the artist's Ren sheet. Its colors and graphic features are
useful guidance, but its feature alignment did not pass direct projection review.
Tripo receives the image unchanged with `texture_alignment=geometry`; the returned
painting must still be checked against the original artist sheet in Unity.

## Request and provenance

- Endpoint: `POST /v3/models/texture`.
- Texture model: `v3.0-20250812`, the current documented default/highest version.
- Quality: `extreme`. The returned head base color, normal and packed
  metallic/roughness maps are each **8192 × 8192**. Other parts have 32–1024-pixel
  maps, including 256-pixel irises; the quality flag does not make every map 8K.
- Guidance: `texture_prompt.image` with the uploaded reference token.
- `pbr=true`, `bake=true`, `texture_seed=20260913`.
- No remeshing, rigging, decimation or paid format conversion is requested.
- Initial balance: 2,400 available, zero frozen. Final receipt: **30 credits**;
  remaining balance: **2,370**, zero frozen, at completion.

The current [Tripo texture API documentation](https://developers.tripo3d.ai/en/docs/models-texture)
uses a nested `texture_prompt` object. Its `text`, `image` and four-view `images`
modes are mutually exclusive; `style_image` only accompanies text. Older SDK
convenience fields are not used in this V3 request.

`provenance.json` records source hashes, requested settings and current status.
Completed downloads retain provider bytes under `originals/`; `receipt.json`
records the actual cost. Credentials, upload tokens, raw CLI state and expiring
download URLs stay under the ignored `.local/character-art/tripo-texture-v1/`.

## Resume

From the repository root, with the existing BWS credential available:

```powershell
python tools/character_art/tripo_texture_jobs.py resume
```

The official Tripo CLI handles authenticated preflight, blocking task watch and
downloads. The task-creation transport makes exactly one POST and persists intent
first: CLI 0.4.0 otherwise retries paid POSTs after transient errors. Resume only
uses the saved task ID. It never starts a new generation. Three no-charge tests
cover a lost create response, changed inputs and resume without another POST.

This is an offline texture-source test. 8K is not an approved phone texture budget,
and this trial does not establish final likeness or iPhone performance.
