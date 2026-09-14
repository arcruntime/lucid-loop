# Shared female rig v3 — active revised standard

Ren, Maya and Player share this one active 54-bone female definition. Apply this revised rig with character-specific skin weights; no separate Ren-only female standard is intended. Male rigs and other character meshes were not modified.

Current identity: `262fd90ef64af0453fc4580dc3a7948dd26b15f3bd0b66b943b97e9b2f2e6831`.

Previous identity: `f8b9ea26b1c099e4c4149793302868be4f732b3ee1bc4c2390248ca9b29da352`. The changed digits' original matrices remain recorded in Ren's `guarded-final-v1/digit-rest-correction.json`; this was an uncommitted source before the revision.

Revision: the authoritative guarded-final-v1 left-hand articulation correction updates 15 left digit rest transforms. All 24 core and 15 right digit rest matrices remain exactly unchanged; all 54 names and hierarchy remain unchanged. See digit-revision-proof.json and rig.json for source hash, corrected bone names and identity. Existing animation against the earlier left digit rests needs the same correction before integration.

The canonical blend contains the skeleton only, so no body skin or geometry required mutation. Ren's authoritative corrected weights live in guarded-final-v1 and are transferred to her LOD1. Maya and Player must bind their own meshes to these same revised rests. Right finger weight authoring remains unverified; this revision does not claim right-hand articulation approval.

Ren's original approved Meshy core joint placement remains authoritative; meshes must not be warped into the older female-v2 proportions. Units are metres, Blender Z-up; FBX uses -Z forward/Y-up and FBX_SCALE_UNITS to match LOD0 import scaling.
