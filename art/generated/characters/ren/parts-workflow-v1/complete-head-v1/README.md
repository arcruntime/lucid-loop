# Ren complete-head V1 archive

This frozen assembly is superseded for current visual review by
[complete-head-v2](../complete-head-v2/README.md), which revises the cap and fringe.
The facial geometry, painted skin and gaze contract are unchanged in V2.

V1 contains 26,278 Blender triangles; Unity imported 26,274. Source hashes and
export metadata are in [assembly.json](assembly.json). The native Blender file,
FBX, GLB, textures and actual Blender/browser/Unity evidence remain available.

The initial Unity binding loop mistakenly cleared `capOn` without restoring it.
Initial `unity-review/live/` captures are superseded for cap-state review; hair
crossing the cap there was a runtime bug. Corrected evidence is in
[live-cap-fix](unity-review/live-cap-fix/) and
[RenCompleteHeadCapFixReview.json](unity-review/RenCompleteHeadCapFixReview.json).
The corrected V1 local player is
`.local/ren-complete-head-v1-cap-fix/RenCompleteHead.exe`.

Do not treat either V1 or V2 as final likeness approval or measured phone
performance. V2's README links the current scene, viewer and next art work.
