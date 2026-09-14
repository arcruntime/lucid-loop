# Matched articulation review

Actual Blender renders compare the immutable assembled LOD0 and corrected LOD1
at identical camera, framing, lighting and shape weights. `manifest.json` records
both source hashes and the applied poses. Neither source file was saved or edited.

Visual review: reduced A remains open with visible oral interior; MBP is visibly
closed. Blink plus partial A retains closed eye lines while the mouth opens.
No obvious mouth tears appear in these frames. FV is subtle in the rendered
result; these images do not establish accurate lower-lip/upper-tooth contact or
complete English articulation quality. Close-up reduced hair, cap and neck
faceting remain apparent; LOD0 should be used for conversations.

These are Blender shape checks. Unity's closed-eye texture correction, NPR
lighting, animation mixing and LOD transitions require their own runtime checks.
The reduced model remains over its 10–12k triangle budget.

Reproduce with Blender background execution of
`tools/character_art/verify_ren_lod_speech.py` from the repository root.
