# Ren Studio H A-open: selected facial shape

The user selected [this exact Tripo result](https://studio.tripo3d.ai/workspace/generate/4dd5c828-eed8-4ded-9aa2-b2c3ef66c6be)
as the correct face shape, explicitly including **both cheeks and jaw**. Treat
its cheek volume, cheekbone placement, lower-face taper and chin as the shape
reference. It is different from the P2 construction head and H closed-rest bust.
This selection does not establish approval of its texture, eyes or final shader.

The [original GLB](ren-tripo-studio-h3.1-a-open-original-8k.glb) was downloaded
through the visible Studio Export dialog on 2026-09-14 JST, using **GLB / 8k
Current**, without another generation. Original download bytes are preserved:

- SHA-256: `9158e7e90ee22bce64154e2c2fe6d8880e9ab66de6f1a6c437c816f077049406`.
- 58,961,168 bytes; one mesh and primitive; **1,934,041 triangles**.
- 1,031,818 GLB accessor vertices; Studio displays 1,031,819.
- Original embedded images: one 8192x8192 JPEG and two 4096x4096 JPEGs.
  [Extracted files](embedded-original-textures/) retain the exact embedded bytes.

See [export receipt](export-receipt.json), [generation provenance](provenance.json),
[export settings](export-settings.jpg) and [actual Studio view](studio-selected-source.jpg).
The historical preview/browser failure is retained in provenance; export has now
succeeded. No API access, regeneration or additional paid task was used to recover it.

This is a dense construction reference, not the 40k-triangle complete character.
Keep it immutable. Inspect matched front, quarter and profile views before fitting
deformable topology, and account for its open-A mouth pose when comparing the
lower face with a closed-rest model. Do not replace cheek/jaw shape with a generic
smoothed chin or treat a renderer change as a geometry correction.
