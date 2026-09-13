# Meshy 7 Ultra generation diagnostics

Date: 2026-09-13. Every attempt uses the reviewed new Ren head reference, explicit Meshy 7, and Ultra enabled. No attempt silently substitutes a lower-detail generation model or lower texture resolution.

| Attempt | Task ID | Distinct request | Result |
| --- | --- | --- | --- |
| Closed rest | `01a09936-e3ff-7607-b081-dce504922f44` | Front + profile, native geometry, integrated 8K/PBR texture | Failed at 45%, resource constraints |
| Open A | `01a09937-1a64-75d1-aa3c-09943a700ff1` | Same configuration, separate A expression | Failed at 45%, resource constraints |
| Closed rest retry | `01a0993a-5e2d-7658-9913-811c5ff6f20a` | Identical configuration, serialized after both earlier jobs ended | Failed at 45%, resource constraints |
| Geometry stage | `01a0993d-6a61-70f2-abcf-063a4c6c3d55` | Ultra/native geometry only, texture deferred | Failed at 99%, unexpected error |
| Minimal geometry export | `01a0993f-7651-7735-909c-bc32312bbbb3` | Omit optional thumbnails and redundant pre-remesh export flags | Failed at 99%, unexpected error |
| Auxiliary remesh export | `01a09941-5bd7-7006-b631-b985321caa24` | Save original pre-remeshed GLB; generate auxiliary 300k remesh solely to exercise another export path | Failed at 99%, resource constraints |
| Single-image geometry | `01a09945-4231-765f-bd7d-c0dd02d225da` | Image-to-3D instead of Multi-Image-to-3D; reviewed front only, Ultra/native geometry; profile reserved for later texturing | SUCCEEDED: 3,051,192 triangles, 1,521,731 vertices, no UVs or materials |
| Closed-rest 8K texture | `01a09947-e259-7035-8f82-aabb8a52199b` | Retexture the original successful GLB, explicit Meshy 7, 8K/PBR, front + profile; generate UVs because source has none | SUCCEEDED: 3,028,819 triangles, external 8192² PNG base color, 4096² PBR maps |
| Open-A single-image geometry | `01a09948-2a3a-730c-b856-a12de1a0bd2e` | Repeat successful Image-to-3D Ultra/native route with the separate A front reference | SUCCEEDED: 2,558,256 triangles, 1,243,515 vertices, no UVs or materials |
| Open-A 8K texture | `01a0994a-d152-745d-bc89-f4035680f952` | Retexture original A geometry with A front + profile at Meshy 7 8K/PBR | SUCCEEDED: 2,439,591 triangles, external 8192² PNG base color, 4096² PBR maps |

The six failed tasks returned no model download URLs and final `consumed_credits: 0`. Their initial reserved credit figures were refunded. The successful single-image route remains distinct: only its front image conditions geometry. Any resulting viewer entry must disclose that difference even if the subsequent texture stage uses both views.

All four successful stages are downloaded and hash-verified. Final combined cost is 80 credits: 25 per Ultra geometry stage and 15 per 8K texture stage. See [results-manifest.json](results-manifest.json) and each stage's provenance for originals and the service's small geometry cleanup during texturing.

The unmodified successful source is `closed-rest-geometry-single-image/model.glb` (54,875,864 bytes). Its no-UV state was inspected before texturing. `enable_original_uv: false` is necessary for that texture stage; setting it to true cannot preserve a nonexistent UV layout. No decimation is requested in either successful-route stage.

Observed errors:

```text
Generation failed due to resource constraints. Please retry. If this persists, try simplifying your input.

An unexpected error occurred. Please retry. If this persists, contact support with your task ID.
```

These messages do not establish that the references are invalid or identify a particular resource that failed. Repeated failure with one task running at a time also does not support blaming local concurrent submission alone. The cause remains unconfirmed.

The staged runner supports separate [Meshy 7 8K retexturing](https://docs.meshy.ai/en/api/retexture) with the same image references and original UVs where they exist. It preserves source task identity and file hash, can explicitly select the original pre-remeshed GLB, and refuses to substitute an absent original with the auxiliary remeshed model. No retexture job can start before an actual geometry result exists.

Sanitized request parameters, exact input hashes, timestamps, and final service responses are stored in each attempt's `task-state.json`. No credential values, data URIs, or signed download URLs are included. This document is a local diagnostic record; no support message has been sent.
